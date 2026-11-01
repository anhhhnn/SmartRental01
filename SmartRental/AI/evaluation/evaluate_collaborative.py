"""Shared evaluation and reporting utilities for the implicit ALS challenger."""

from __future__ import annotations

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
sys.path.insert(0, str(AI_ROOT / "evaluation"))

from recommendation.collaborative import CollaborativeRecommender  # noqa: E402
from recommendation.content_based import (  # noqa: E402
    ContentBasedRecommender, Interaction, RoomRecord, load_config,
    load_interactions, load_rooms,
)
from evaluate_content_based import load_user_segments  # noqa: E402

DEFAULT_SEED = 20261028
DEFAULT_MIN_INTERACTIONS = 5
DEFAULT_INTERACTIONS = AI_ROOT / "DATA" / "interactions.csv"
DEFAULT_ROOMS = AI_ROOT / "DATA" / "rooms_recommendation.csv"
DEFAULT_CONFIG = AI_ROOT / "recommendation" / "collaborative_config.json"
DEFAULT_MODEL = AI_ROOT / "models" / "collaborative_model.npz"
DEFAULT_METRICS = AI_ROOT / "evaluation" / "collaborative_metrics.json"
DEFAULT_REPORT = AI_ROOT / "evaluation" / "collaborative_report.md"


def stable_rng(user_id: str, seed: int) -> random.Random:
    digest = hashlib.sha256(f"{seed}:{user_id}".encode()).digest()
    return random.Random(int.from_bytes(digest[:8], "big"))


def split_by_user(
    interactions: list[Interaction], rooms_by_id: dict[int, RoomRecord], seed: int,
    min_interactions: int = DEFAULT_MIN_INTERACTIONS,
) -> tuple[list[Interaction], dict[str, set[int]], dict[str, object]]:
    """Seeded 20% per-user holdout, preserving at least one train item.

    Users below ``min_interactions`` remain available to the training corpus but
    are excluded from offline evaluation.
    """
    grouped: dict[str, list[Interaction]] = defaultdict(list)
    for item in interactions:
        grouped[item.user_id].append(item)
    train: list[Interaction] = []
    test: dict[str, set[int]] = {}
    excluded = Counter()
    for user_id in sorted(grouped):
        items = grouped[user_id]
        if len(items) < min_interactions:
            train.extend(items)
            excluded["below_min_interactions"] += 1
            continue
        visible = [item for item in items if rooms_by_id[item.room_id].is_visible]
        if not visible:
            train.extend(items)
            excluded["no_visible_holdout"] += 1
            continue
        holdout_size = min(max(1, math.ceil(len(items) * 0.20)), len(items) - 1, len(visible))
        selected = set(stable_rng(user_id, seed).sample(range(len(visible)), holdout_size))
        held_ids = {visible[index].room_id for index in selected}
        user_train = [item for item in items if item.room_id not in held_ids]
        if not user_train:
            train.extend(items)
            excluded["empty_train_profile"] += 1
            continue
        train.extend(user_train)
        test[user_id] = held_ids
    train_pairs = {(item.user_id, item.room_id) for item in train}
    leakage = sum((user_id, room_id) in train_pairs for user_id, ids in test.items() for room_id in ids)
    audit: dict[str, object] = {
        "seed": seed, "min_interactions": min_interactions,
        "train_interactions": len(train),
        "test_interactions": sum(map(len, test.values())),
        "train_users": len({item.user_id for item in train}),
        "evaluated_users": len(test),
        "excluded_users": sum(excluded.values()),
        "excluded_reasons": dict(excluded),
        "leaked_test_pairs": leakage,
    }
    return train, test, audit


def ndcg_at_k(recommended: list[int], relevant: set[int], k: int) -> float:
    dcg = sum(1 / math.log2(rank + 2) for rank, room_id in enumerate(recommended[:k]) if room_id in relevant)
    ideal = sum(1 / math.log2(rank + 2) for rank in range(min(k, len(relevant))))
    return dcg / ideal if ideal else 0.0


def evaluate_model(
    model: CollaborativeRecommender, test: dict[str, set[int]], users: Iterable[str],
    visible_room_count: int, popularity: Counter[int],
) -> dict[str, object]:
    user_ids = sorted(set(users).intersection(test).intersection(model.user_index))
    values: dict[str, list[float]] = defaultdict(list)
    recommendation_sets: list[set[int]] = []
    recommended_counter: Counter[int] = Counter()
    recommendation_popularity: list[int] = []
    for user_id in user_ids:
        result = model.recommend_for_user(user_id, top_n=10, exclude_seen=True)
        recommended = [int(row["PhongtroId"]) for row in result["recommendations"]]
        relevant = test[user_id]
        recommendation_sets.append(set(recommended))
        recommended_counter.update(recommended)
        recommendation_popularity.extend(popularity[room_id] for room_id in recommended)
        for k in (5, 10):
            hits = len(set(recommended[:k]).intersection(relevant))
            values[f"precision_at_{k}"].append(hits / k)
            values[f"recall_at_{k}"].append(hits / len(relevant))
            values[f"hit_rate_at_{k}"].append(float(hits > 0))
        values["ndcg_at_10"].append(ndcg_at_k(recommended, relevant, 10))
    result: dict[str, object] = {"eligible_users": len(user_ids)}
    metric_keys = (
        "precision_at_5", "recall_at_5", "hit_rate_at_5", "precision_at_10",
        "recall_at_10", "hit_rate_at_10", "ndcg_at_10",
    )
    for key in metric_keys:
        result[key] = statistics.fmean(values[key]) if values[key] else None
    unique = len(recommended_counter)
    result["unique_recommended_rooms_at_10"] = unique
    result["catalog_coverage_at_10"] = unique / visible_room_count if visible_room_count else None
    overlaps = []
    for left in range(len(recommendation_sets)):
        for right in range(left + 1, len(recommendation_sets)):
            union = recommendation_sets[left] | recommendation_sets[right]
            overlaps.append(len(recommendation_sets[left] & recommendation_sets[right]) / len(union) if union else 0)
    result["average_top10_jaccard_overlap"] = statistics.fmean(overlaps) if overlaps else None
    result["average_recommendation_popularity"] = (
        statistics.fmean(recommendation_popularity) if recommendation_popularity else None
    )
    result["most_recommended_rooms"] = [
        {"PhongtroId": room_id, "recommendation_count": count}
        for room_id, count in recommended_counter.most_common(10)
    ]
    return result


def mask_user_id(user_id: str) -> str:
    return f"{user_id[:4]}...{user_id[-4:]}"


def resolve_example_users(interactions: list[Interaction]) -> list[str]:
    ids = sorted({item.user_id for item in interactions})
    prefixes = ("b655", "f032", "bf07")
    return [next(user_id for user_id in ids if user_id.startswith(prefix)) for prefix in prefixes]


def example_rows(
    collaborative: CollaborativeRecommender, content: ContentBasedRecommender,
    interactions: list[Interaction], user_ids: list[str],
) -> list[dict[str, object]]:
    counts = Counter(item.user_id for item in interactions)
    rows = []
    for user_id in user_ids:
        collaborative_rows = collaborative.recommend_for_user(user_id, 5, True)["recommendations"]
        content_rows = content.recommend_for_user(user_id, 5, True)
        collab_ids = {int(row["PhongtroId"]) for row in collaborative_rows}
        content_ids = {int(row["PhongtroId"]) for row in content_rows}
        rows.append({
            "user_id_masked": mask_user_id(user_id), "interaction_count": counts[user_id],
            "collaborative_top5": collaborative_rows,
            "content_based_top5": [
                {"PhongtroId": row["PhongtroId"], "ContentScore": row["ContentScore"]}
                for row in content_rows
            ],
            "overlap_count": len(collab_ids & content_ids),
            "overlap_room_ids": sorted(collab_ids & content_ids),
        })
    return rows


def fmt(value: object) -> str:
    return "N/A" if value is None else f"{float(value):.6f}"


def build_report(data: dict[str, object]) -> str:
    metrics = data["metrics"]
    all_metrics = metrics["all_users"]
    cb = data["content_based_optimized"]
    labels = [
        ("precision_at_5", "Precision@5"), ("recall_at_5", "Recall@5"),
        ("hit_rate_at_5", "HitRate@5"), ("precision_at_10", "Precision@10"),
        ("recall_at_10", "Recall@10"), ("hit_rate_at_10", "HitRate@10"),
        ("ndcg_at_10", "NDCG@10"), ("catalog_coverage_at_10", "Coverage@10"),
    ]
    lines = [
        "# Báo cáo Collaborative Filtering challenger", "",
        "## Phương pháp và audit", "",
        "- Thuật toán: implicit-feedback ALS (Hu–Koren–Volinsky), tự triển khai bằng NumPy/SciPy.",
        "- Preference của mọi interaction quan sát là 1; confidence = `1 + alpha * log1p(Score)`. `Score` không được coi là rating 1–5.",
        f"- Schema nguồn: `{', '.join(data['audit']['schema'])}`; dataset gốc không bị sửa.",
        f"- Dataset: **{data['audit']['interactions']}** interactions, **{data['audit']['users']}** users, **{data['audit']['rooms_with_interactions']}** rooms có interaction.",
        f"- Trung bình **{data['audit']['interactions_per_user']:.2f}** interaction/user; sparsity **{data['audit']['sparsity']:.4%}**.",
        f"- Demo/non-demo: **{data['segments']['demo_users']} / {data['segments']['non_demo_users']}** users; **{data['segments']['demo_interactions']} / {data['segments']['non_demo_interactions']}** interactions.",
        f"- Candidate set: **{data['audit']['visible_rooms']}** phòng visible, giống Content-Based optimized.",
        f"- Outer split seed `{data['split']['seed']}`: **{data['split']['train_interactions']}** train, **{data['split']['test_interactions']}** test; **{data['split']['evaluated_users']}** evaluated, **{data['split']['excluded_users']}** excluded (<5 interactions).",
        f"- Leakage: **{data['split']['leaked_test_pairs']}** held-out pair xuất hiện trong training matrix.",
        "- Hyperparameter được chọn trên inner validation của outer-train; outer test chỉ dùng một lần để báo cáo và so sánh.", "",
        "## Best config", "",
        f"- Benchmarked: **{data['config_count']}** cấu hình.",
        f"- factors={data['best_config']['factors']}, regularization={data['best_config']['regularization']}, iterations={data['best_config']['iterations']}, alpha={data['best_config']['alpha']}, seed={data['best_config']['seed']}.", "",
        "## So sánh trực tiếp", "",
        "| Metric | Content-Based optimized | Collaborative | Difference |", "|---|---:|---:|---:|",
    ]
    for key, label in labels:
        difference = float(all_metrics[key]) - float(cb[key])
        lines.append(f"| {label} | {fmt(cb[key])} | {fmt(all_metrics[key])} | {difference:+.6f} |")
    lines += ["", "## Metric theo nhóm", "", "| Nhóm | Users | Precision@10 | Recall@10 | HitRate@10 | NDCG@10 | Coverage@10 |", "|---|---:|---:|---:|---:|---:|---:|"]
    for key, label in (("all_users", "All users"), ("demo_users", "Demo users"), ("non_demo_users", "Non-demo users")):
        row = metrics[key]
        lines.append(f"| {label} | {row['eligible_users']} | {fmt(row['precision_at_10'])} | {fmt(row['recall_at_10'])} | {fmt(row['hit_rate_at_10'])} | {fmt(row['ndcg_at_10'])} | {fmt(row['catalog_coverage_at_10'])} |")
    lines += ["", "Non-demo chỉ có 2 tài khoản và không được dùng để tune; các số liệu này chỉ mang tính tham khảo.", "", "## Diversity và popularity bias", "",
        f"- Coverage@10: **{fmt(all_metrics['catalog_coverage_at_10'])}** ({all_metrics['unique_recommended_rooms_at_10']}/{data['audit']['visible_rooms']} phòng).",
        f"- Average pairwise Top10 Jaccard: **{fmt(all_metrics['average_top10_jaccard_overlap'])}**.",
        f"- Average recommendation popularity: **{fmt(all_metrics['average_recommendation_popularity'])}** train users/phòng.",
        "- Top phòng xuất hiện nhiều nhất: " + ", ".join(f"{row['PhongtroId']} ({row['recommendation_count']} lần)" for row in all_metrics["most_recommended_rooms"][:5]) + ".", "", "## Ba user ví dụ", ""]
    for row in data["examples"]:
        collab = ", ".join(f"{x['PhongtroId']} ({x['CollaborativeScore']:.6f})" for x in row["collaborative_top5"])
        content = ", ".join(f"{x['PhongtroId']} ({x['ContentScore']:.6f})" for x in row["content_based_top5"])
        lines += [f"### `{row['user_id_masked']}`", "", f"- Interactions: {row['interaction_count']}.", f"- Collaborative Top5: {collab}.", f"- Content-Based Top5: {content}.", f"- Overlap: {row['overlap_count']}/5 ({row['overlap_room_ids']}).", ""]
    lines += ["## Kết luận", "", data["conclusion"], "", "Chưa nên chuyển sang Hybrid trong lượt này; chỉ nên thử Hybrid ở một thí nghiệm riêng sau khi có thêm dữ liệu non-demo và chính sách fallback cold-start.", "", "Đây là kết quả nghiên cứu trên dữ liệu chủ yếu là demo, không phải bằng chứng model production-ready.", ""]
    return "\n".join(lines)


def produce_outputs(
    best_config: dict[str, object], config_count: int, tuning_results_path: Path,
    connection_string: str | None = None,
) -> dict[str, object]:
    interactions = load_interactions(DEFAULT_INTERACTIONS)
    rooms = load_rooms(DEFAULT_ROOMS)
    rooms_by_id = {room.room_id: room for room in rooms}
    train, test, split = split_by_user(interactions, rooms_by_id, DEFAULT_SEED)
    evaluation_model = CollaborativeRecommender(**{key: best_config[key] for key in ("factors", "regularization", "iterations", "alpha", "seed")}).fit(train, rooms)
    full_model = CollaborativeRecommender(**{key: best_config[key] for key in ("factors", "regularization", "iterations", "alpha", "seed")}).fit(interactions, rooms)
    full_model.metadata["evaluation_split"] = split
    full_model.metadata["tuning_results"] = str(tuning_results_path.relative_to(PROJECT_ROOT)).replace("\\", "/")
    full_model.save(DEFAULT_MODEL)

    all_tenants, demo_users = load_user_segments(connection_string)
    non_demo_users = all_tenants - demo_users
    popularity = Counter(item.room_id for item in train)
    visible_count = sum(room.is_visible for room in rooms)
    metrics = {
        "all_users": evaluate_model(evaluation_model, test, test, visible_count, popularity),
        "demo_users": evaluate_model(evaluation_model, test, demo_users, visible_count, popularity),
        "non_demo_users": evaluate_model(evaluation_model, test, non_demo_users, visible_count, popularity),
    }
    content_metrics = json.loads((AI_ROOT / "evaluation" / "content_based_optimized_metrics.json").read_text(encoding="utf-8"))["optimized_metrics"]["all_users"]
    cb = {key: content_metrics[key] for key in ("precision_at_5", "recall_at_5", "hit_rate_at_5", "precision_at_10", "recall_at_10", "hit_rate_at_10", "ndcg_at_10", "catalog_coverage_at_10")}
    content_full = ContentBasedRecommender(rooms, interactions, load_config())
    counts = Counter(item.user_id for item in interactions)
    all_m = metrics["all_users"]
    if float(all_m["ndcg_at_10"]) > float(cb["ndcg_at_10"]) * 1.05:
        conclusion = "A. Collaborative tốt hơn Content-Based trên outer test và có tiềm năng dùng trong Hybrid."
    elif float(all_m["ndcg_at_10"]) >= float(cb["ndcg_at_10"]) * 0.90:
        conclusion = "B. Collaborative tương đương Content-Based; vẫn nên thử Hybrid."
    else:
        conclusion = "C. Collaborative kém rõ rệt; giữ Content-Based làm model chính, Collaborative chỉ là challenger."
    data: dict[str, object] = {
        "model": "implicit_als_challenger", "implementation": "numpy_scipy",
        "config_count": config_count, "best_config": best_config, "split": split,
        "audit": {
            "schema": ["UserId", "PhongtroId", "Views", "Favorite", "Booking", "Rating", "Score"],
            "interactions": len(interactions), "users": len(counts),
            "rooms_with_interactions": len({item.room_id for item in interactions}),
            "catalog_rooms": len(rooms), "visible_rooms": visible_count,
            "interactions_per_user": len(interactions) / len(counts),
            "sparsity": 1 - len(interactions) / (len(counts) * len({item.room_id for item in interactions})),
        },
        "segments": {
            "demo_users": len(demo_users), "non_demo_users": len(non_demo_users),
            "demo_interactions": sum(item.user_id in demo_users for item in interactions),
            "non_demo_interactions": sum(item.user_id in non_demo_users for item in interactions),
        },
        "metrics": metrics, "content_based_optimized": cb,
        "examples": example_rows(full_model, content_full, interactions, resolve_example_users(interactions)),
        "model_artifact": str(DEFAULT_MODEL.relative_to(PROJECT_ROOT)).replace("\\", "/"),
        "conclusion": conclusion,
        "limitations": ["72/74 users and 3726/3740 interactions are demo data", "Non-demo metrics are descriptive only", "No timestamp exists, so the split is seeded random per user"],
    }
    DEFAULT_METRICS.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    DEFAULT_REPORT.write_text(build_report(data), encoding="utf-8")
    return data


if __name__ == "__main__":
    if not DEFAULT_CONFIG.exists():
        raise SystemExit("Run tune_collaborative.py first to select a config")
    config = json.loads(DEFAULT_CONFIG.read_text(encoding="utf-8"))
    produce_outputs(config, config.get("benchmarked_configs", 0), AI_ROOT / "evaluation" / "collaborative_tuning_results.json")
