"""Offline evaluation for SmartRental's content-based recommender."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import random
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path
from typing import Iterable


PROJECT_ROOT = Path(__file__).resolve().parents[2]
AI_ROOT = PROJECT_ROOT / "AI"
sys.path.insert(0, str(AI_ROOT))
sys.path.insert(0, str(AI_ROOT / "DATA"))

from recommendation.content_based import (  # noqa: E402
    BASELINE_CONFIG,
    ContentBasedRecommender,
    Interaction,
    RoomRecord,
    load_interactions,
    load_rooms,
)
from export_rooms_for_recommendation import SqlCmd, load_connection_string  # noqa: E402


DEFAULT_ROOMS = AI_ROOT / "DATA" / "rooms_recommendation.csv"
DEFAULT_INTERACTIONS = AI_ROOT / "DATA" / "interactions.csv"
DEFAULT_JSON = AI_ROOT / "evaluation" / "content_based_metrics.json"
DEFAULT_REPORT = AI_ROOT / "evaluation" / "content_based_report.md"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rooms", type=Path, default=DEFAULT_ROOMS)
    parser.add_argument("--interactions", type=Path, default=DEFAULT_INTERACTIONS)
    parser.add_argument("--json-output", type=Path, default=DEFAULT_JSON)
    parser.add_argument("--report-output", type=Path, default=DEFAULT_REPORT)
    parser.add_argument("--connection-string")
    parser.add_argument("--seed", type=int, default=20261028)
    return parser.parse_args()


def load_user_segments(connection_string: str | None) -> tuple[set[str], set[str]]:
    """Return all tenant IDs and demo tenant IDs without exporting email addresses."""
    db = SqlCmd(load_connection_string(connection_string))
    rows = db.rows(
        r"""
SELECT DISTINCT u.Id,
       CASE WHEN u.Email LIKE N'nguoithue%@smartrental.local' THEN 1 ELSE 0 END
FROM AspNetUsers AS u
INNER JOIN AspNetUserRoles AS ur ON ur.UserId = u.Id
INNER JOIN AspNetRoles AS r ON r.Id = ur.RoleId
WHERE r.Name = N'NguoiThue'
ORDER BY u.Id;
"""
    )
    all_tenants = {row[0] for row in rows}
    demo_users = {row[0] for row in rows if len(row) == 2 and row[1] == "1"}
    return all_tenants, demo_users


def stable_rng(user_id: str, seed: int) -> random.Random:
    digest = hashlib.sha256(f"{seed}:{user_id}".encode()).digest()
    return random.Random(int.from_bytes(digest[:8], "big"))


def split_by_user(
    interactions: list[Interaction], rooms_by_id: dict[int, RoomRecord], seed: int
) -> tuple[list[Interaction], dict[str, set[int]], dict[str, int]]:
    """Hold out 20% (at least one) visible item while retaining a train profile."""
    grouped: dict[str, list[Interaction]] = defaultdict(list)
    for item in interactions:
        grouped[item.user_id].append(item)

    train: list[Interaction] = []
    test: dict[str, set[int]] = {}
    skipped = Counter()
    for user_id, items in grouped.items():
        if len(items) < 2:
            train.extend(items)
            skipped["fewer_than_2_interactions"] += 1
            continue
        visible = [item for item in items if rooms_by_id[item.room_id].is_visible]
        if not visible:
            train.extend(items)
            skipped["no_visible_holdout"] += 1
            continue
        holdout_size = min(max(1, math.ceil(len(items) * 0.20)), len(items) - 1, len(visible))
        selected = set(stable_rng(user_id, seed).sample(range(len(visible)), holdout_size))
        held_ids = {visible[index].room_id for index in selected}
        user_train = [item for item in items if item.room_id not in held_ids]
        if not user_train:
            train.extend(items)
            skipped["empty_train_profile"] += 1
            continue
        train.extend(user_train)
        test[user_id] = held_ids
    return train, test, dict(skipped)


def ndcg_at_k(recommended: list[int], relevant: set[int], k: int) -> float:
    dcg = sum(1.0 / math.log2(rank + 2) for rank, room_id in enumerate(recommended[:k]) if room_id in relevant)
    ideal = sum(1.0 / math.log2(rank + 2) for rank in range(min(len(relevant), k)))
    return dcg / ideal if ideal else 0.0


def evaluate_users(
    model: ContentBasedRecommender, test: dict[str, set[int]], users: Iterable[str]
) -> dict[str, object]:
    user_ids = sorted(set(users).intersection(test))
    values: dict[str, list[float]] = defaultdict(list)
    for user_id in user_ids:
        relevant = test[user_id]
        recommended = [
            int(row["PhongtroId"])
            for row in model.recommend_for_user(user_id, top_n=10, exclude_seen=True)
        ]
        for k in (5, 10):
            hits = len(set(recommended[:k]).intersection(relevant))
            values[f"precision_at_{k}"].append(hits / k)
            values[f"recall_at_{k}"].append(hits / len(relevant))
            values[f"hit_rate_at_{k}"].append(float(hits > 0))
        values["ndcg_at_10"].append(ndcg_at_k(recommended, relevant, 10))
    result: dict[str, object] = {"eligible_users": len(user_ids)}
    for key in (
        "precision_at_5", "recall_at_5", "hit_rate_at_5",
        "precision_at_10", "recall_at_10", "hit_rate_at_10", "ndcg_at_10",
    ):
        result[key] = statistics.fmean(values[key]) if values[key] else None
    return result


def mask_user_id(user_id: str) -> str:
    return f"{user_id[:4]}...{user_id[-4:]}" if len(user_id) > 10 else "masked"


def choose_example_users(interactions: list[Interaction]) -> list[str]:
    counts = Counter(item.user_id for item in interactions)
    ordered = sorted(counts, key=lambda user_id: (counts[user_id], user_id))
    candidates = [ordered[-1], ordered[len(ordered) // 2], ordered[0]]
    return list(dict.fromkeys(candidates))


def example_for_user(
    user_id: str,
    model: ContentBasedRecommender,
    rooms_by_id: dict[int, RoomRecord],
) -> dict[str, object]:
    items = model.by_user[user_id]
    weighted_areas: Counter[str] = Counter()
    weighted_amenities: Counter[str] = Counter()
    raw_weights = [model.weight_for_interaction(item) for item in items]
    # Profile diagnostics describe positive preference evidence. Signed ratings
    # remain active in the model, but disliked rooms should not become a "top"
    # area or amenity in this human-readable summary.
    weights = [max(0.0, weight) for weight in raw_weights]
    if not any(weights):
        weights = [abs(weight) for weight in raw_weights]
    total_weight = sum(weights)
    weighted_price = 0.0
    for item, weight in zip(items, weights):
        room = rooms_by_id[item.room_id]
        weighted_areas[room.area] += weight
        weighted_price += room.price * weight
        for amenity in room.amenities:
            weighted_amenities[amenity] += weight
    recommendations = model.recommend_for_user(user_id, top_n=5, exclude_seen=True)
    return {
        "user_id_masked": mask_user_id(user_id),
        "interaction_count": len(items),
        "top_areas": [name for name, _ in weighted_areas.most_common(3)],
        "weighted_average_price": weighted_price / total_weight if total_weight else 0.0,
        "top_amenities": [name for name, _ in weighted_amenities.most_common(5)],
        "recommendations": [
            {"PhongtroId": row["PhongtroId"], "ContentScore": round(float(row["ContentScore"]), 6)}
            for row in recommendations
        ],
    }


def metric(value: object) -> str:
    return "N/A" if value is None else f"{float(value):.6f}"


def build_report(data: dict[str, object]) -> str:
    metrics = data["metrics"]
    all_metrics = metrics["all_users"]
    demo_metrics = metrics["demo_users"]
    real_metrics = metrics["non_demo_users"]
    lines = [
        "# Báo cáo Content-Based Recommendation",
        "",
        "## 1. Đặc trưng và dữ liệu",
        "",
        "- Nhóm đặc trưng: `KhuVuc` (one-hot), `TienNghi` (multi-hot), `Gia`, `DienTich`, `SoNguoiToiDa` (StandardScaler).",
        f"- Số phòng: **{data['room_count']}**; visible: **{data['visible_room_count']}**; hidden: **{data['hidden_room_count']}**.",
        f"- Số nhóm feature: **{data['feature_group_count']}**; số chiều vector: **{data['vector_dimension']}**.",
        f"- User có profile: **{data['profile_user_count']}**; tenant cold-start: **{data['cold_start_user_count']}**.",
        f"- Vector hợp lệ: **{data['vector_validation']}** (không NaN/infinite, chiều cố định).",
        "",
        "## 2. Phương pháp đánh giá",
        "",
        f"- Seed cố định: `{data['split']['seed']}`; giữ lại 20% interaction (ít nhất 1) theo từng user.",
        "- Chỉ user có ít nhất 2 interaction và còn profile train mới được đánh giá; test chỉ gồm phòng visible.",
        f"- Train interactions: **{data['split']['train_interactions']}**; test interactions: **{data['split']['test_interactions']}**; eligible users: **{all_metrics['eligible_users']}**.",
        "- Candidate loại phòng hidden và loại các phòng trong train profile (`exclude_seen=True`).",
        "",
        "## 3. Metric tổng thể",
        "",
        "| Nhóm | Users | Precision@5 | Recall@5 | HitRate@5 | Precision@10 | Recall@10 | HitRate@10 | NDCG@10 |",
        "|---|---:|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for label, values in (("All users", all_metrics), ("Demo users", demo_metrics), ("Non-demo users", real_metrics)):
        lines.append(
            f"| {label} | {values['eligible_users']} | {metric(values['precision_at_5'])} | "
            f"{metric(values['recall_at_5'])} | {metric(values['hit_rate_at_5'])} | "
            f"{metric(values['precision_at_10'])} | {metric(values['recall_at_10'])} | "
            f"{metric(values['hit_rate_at_10'])} | {metric(values['ndcg_at_10'])} |"
        )
    lines.extend([
        "",
        "## 4. Demo và non-demo",
        "",
        f"- Demo: **{data['segments']['demo_profile_users']}** user có profile; **{demo_metrics['eligible_users']}** user đủ điều kiện evaluate.",
        f"- Non-demo: **{data['segments']['non_demo_profile_users']}** user có profile; **{real_metrics['eligible_users']}** user đủ điều kiện evaluate.",
        "- Nhóm non-demo quá ít để suy rộng chất lượng cho người dùng thật; metric tổng thể chủ yếu phản ánh dữ liệu demo.",
        "",
        "## 5. Kiểm tra logic trên 3 user",
        "",
    ])
    for index, example in enumerate(data["examples"], 1):
        recs = ", ".join(
            f"{row['PhongtroId']} ({row['ContentScore']:.6f})" for row in example["recommendations"]
        ) or "không có"
        lines.extend([
            f"### User {index}: `{example['user_id_masked']}` ({example['interaction_count']} interactions)",
            "",
            f"- Khu vực quan tâm: {', '.join(example['top_areas']) or 'N/A'}.",
            f"- Giá trung bình có trọng số: {example['weighted_average_price']:,.0f} VNĐ.",
            f"- Tiện nghi nổi bật: {', '.join(example['top_amenities']) or 'N/A'}.",
            f"- Top 5: {recs}.",
            "",
        ])
    lines.extend([
        "## 6. Giới hạn",
        "",
        "- Interaction là implicit feedback với Score heuristic; chưa phải nhãn sở thích đã được kiểm chứng.",
        "- Split là ngẫu nhiên có seed vì dataset không có timestamp; chưa mô phỏng đầy đủ thứ tự thời gian.",
        f"- {data['segments']['demo_profile_users']}/{data['profile_user_count']} profile thuộc tài khoản demo, còn non-demo quá ít để kết luận production.",
        "- Content features hiện chưa dùng mô tả dài, tọa độ, trạng thái còn phòng hay chất lượng chủ trọ.",
        "- Cold-start trả popularity fallback; `ContentScore` để null và không được xem là cá nhân hóa.",
        "",
        "## 7. Kết luận",
        "",
        "Kết quả chỉ chứng minh pipeline chạy đúng và cung cấp baseline có thể giải thích. Chưa có cơ sở tuyên bố recommendation tốt cho người dùng thật. Chưa nên dùng Collaborative Filtering làm hướng chính; cần thu thập thêm interaction non-demo và đánh giá theo thời gian trước, sau đó mới so sánh CF và hybrid.",
        "",
    ])
    return "\n".join(lines)


def main() -> int:
    args = parse_args()
    rooms = load_rooms(args.rooms)
    interactions = load_interactions(args.interactions)
    rooms_by_id = {room.room_id: room for room in rooms}
    all_tenants, demo_users = load_user_segments(args.connection_string)
    interaction_users = {item.user_id for item in interactions}
    unknown_users = interaction_users.difference(all_tenants)
    if unknown_users:
        raise ValueError(f"{len(unknown_users)} interaction users are not current tenants.")

    train, test, skipped = split_by_user(interactions, rooms_by_id, args.seed)
    # This script owns the immutable baseline artifacts. The tuning script uses
    # the optimized config and writes separate outputs.
    evaluation_model = ContentBasedRecommender(rooms, train, BASELINE_CONFIG)
    full_model = ContentBasedRecommender(rooms, interactions, BASELINE_CONFIG)
    eligible = set(test)
    non_demo_users = all_tenants.difference(demo_users)
    metrics = {
        "all_users": evaluate_users(evaluation_model, test, eligible),
        "demo_users": evaluate_users(evaluation_model, test, demo_users),
        "non_demo_users": evaluate_users(evaluation_model, test, non_demo_users),
    }
    examples = [example_for_user(user_id, full_model, rooms_by_id) for user_id in choose_example_users(interactions)]
    vector_valid = (
        full_model.room_vectors.shape[0] == len(rooms)
        and full_model.room_vectors.shape[1] == len(full_model.feature_names)
        and bool(__import__("numpy").isfinite(full_model.room_vectors.data).all())
    )
    data: dict[str, object] = {
        "model": "content_based_cosine",
        "features": list(full_model.feature_groups),
        "feature_group_count": len(full_model.feature_groups),
        "feature_names": full_model.feature_names,
        "vector_dimension": full_model.vector_dimension,
        "vector_validation": "passed" if vector_valid else "failed",
        "room_count": len(rooms),
        "visible_room_count": sum(room.is_visible for room in rooms),
        "hidden_room_count": sum(not room.is_visible for room in rooms),
        "profile_user_count": len(interaction_users),
        "tenant_user_count": len(all_tenants),
        "cold_start_user_count": len(all_tenants.difference(interaction_users)),
        "segments": {
            "demo_tenant_users": len(demo_users),
            "non_demo_tenant_users": len(non_demo_users),
            "demo_profile_users": len(interaction_users.intersection(demo_users)),
            "non_demo_profile_users": len(interaction_users.intersection(non_demo_users)),
        },
        "split": {
            "method": "per-user seeded random 20% visible holdout",
            "seed": args.seed,
            "train_interactions": len(train),
            "test_interactions": sum(len(items) for items in test.values()),
            "skipped_users": skipped,
        },
        "metrics": metrics,
        "examples": examples,
        "limitations": [
            "Metrics are dominated by demo accounts.",
            "Only two non-demo tenants have interactions, which is insufficient for production conclusions.",
            "The interaction export has no event timestamp, so the holdout is seeded random rather than temporal.",
        ],
    }
    args.json_output.parent.mkdir(parents=True, exist_ok=True)
    args.report_output.parent.mkdir(parents=True, exist_ok=True)
    args.json_output.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    args.report_output.write_text(build_report(data), encoding="utf-8", newline="\n")
    print(f"Wrote metrics to {args.json_output.resolve()}")
    print(f"Wrote report to {args.report_output.resolve()}")
    print(json.dumps(metrics, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
