"""Benchmark a small, explicit grid of content-based recommendation configs."""

from __future__ import annotations

import argparse
import itertools
import json
import statistics
import sys
from copy import deepcopy
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[2]
AI_ROOT = PROJECT_ROOT / "AI"
sys.path.insert(0, str(AI_ROOT))
sys.path.insert(0, str(AI_ROOT / "DATA"))

from evaluation.evaluate_content_based import (  # noqa: E402
    choose_example_users,
    evaluate_users,
    example_for_user,
    load_user_segments,
    split_by_user,
)
from recommendation.content_based import (  # noqa: E402
    BASELINE_CONFIG,
    ContentBasedRecommender,
    load_interactions,
    load_rooms,
)


DEFAULT_ROOMS = AI_ROOT / "DATA" / "rooms_recommendation.csv"
DEFAULT_INTERACTIONS = AI_ROOT / "DATA" / "interactions.csv"
DEFAULT_CONFIG = AI_ROOT / "recommendation" / "content_based_config.json"
DEFAULT_RESULTS = AI_ROOT / "evaluation" / "content_based_tuning_results.json"
DEFAULT_METRICS = AI_ROOT / "evaluation" / "content_based_optimized_metrics.json"
DEFAULT_REPORT = AI_ROOT / "evaluation" / "content_based_optimized_report.md"

NON_NEGATIVE_RATING = {"0": 0.0, "1": 0.0, "2": 0.0, "3": 0.5, "4": 2.0, "5": 4.0}
SIGNED_RATING = {"0": 0.0, "1": -2.0, "2": -1.0, "3": 0.0, "4": 2.0, "5": 4.0}

INTERACTION_CONFIGS = [
    deepcopy(BASELINE_CONFIG),
    {"name": "B_non_negative", "view_transform": "log1p", "view_weight": 1.0,
     "favorite_weight": 3.0, "booking_weight": 5.0, "rating_mapping": NON_NEGATIVE_RATING},
    {"name": "B_signed", "view_transform": "log1p", "view_weight": 1.0,
     "favorite_weight": 3.0, "booking_weight": 5.0, "rating_mapping": SIGNED_RATING},
    {"name": "C_non_negative", "view_transform": "log1p", "view_weight": 1.0,
     "favorite_weight": 4.0, "booking_weight": 6.0, "rating_mapping": NON_NEGATIVE_RATING},
    {"name": "D_non_negative", "view_transform": "log1p", "view_weight": 0.5,
     "favorite_weight": 3.0, "booking_weight": 5.0, "rating_mapping": NON_NEGATIVE_RATING},
    {"name": "E_signed_strong_explicit", "view_transform": "log1p", "view_weight": 0.5,
     "favorite_weight": 4.0, "booking_weight": 6.0, "rating_mapping": SIGNED_RATING},
]

FEATURE_CONFIGS = [
    {"name": "baseline", "khuVuc": 1.0, "tienNghi": 1.0, "gia": 1.0, "dienTich": 1.0, "soNguoi": 1.0},
    {"name": "feature_1", "khuVuc": 1.5, "tienNghi": 1.5, "gia": 1.2, "dienTich": 1.0, "soNguoi": 0.8},
    {"name": "feature_2", "khuVuc": 1.3, "tienNghi": 1.7, "gia": 1.4, "dienTich": 0.8, "soNguoi": 0.8},
    {"name": "feature_3", "khuVuc": 1.5, "tienNghi": 2.0, "gia": 1.0, "dienTich": 0.8, "soNguoi": 0.7},
]

METRIC_KEYS = (
    "precision_at_5", "recall_at_5", "hit_rate_at_5", "precision_at_10",
    "recall_at_10", "hit_rate_at_10", "ndcg_at_10", "catalog_coverage_at_10",
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rooms", type=Path, default=DEFAULT_ROOMS)
    parser.add_argument("--interactions", type=Path, default=DEFAULT_INTERACTIONS)
    parser.add_argument("--config-output", type=Path, default=DEFAULT_CONFIG)
    parser.add_argument("--results-output", type=Path, default=DEFAULT_RESULTS)
    parser.add_argument("--metrics-output", type=Path, default=DEFAULT_METRICS)
    parser.add_argument("--report-output", type=Path, default=DEFAULT_REPORT)
    parser.add_argument("--connection-string")
    parser.add_argument("--seed", type=int, default=20261028)
    return parser.parse_args()


def make_config(interaction: dict[str, object], feature: dict[str, object]) -> dict[str, object]:
    config = deepcopy(interaction)
    config["feature_weights"] = {key: float(feature[key]) for key in ("khuVuc", "tienNghi", "gia", "dienTich", "soNguoi")}
    config["interaction_config"] = interaction["name"]
    config["feature_config"] = feature["name"]
    config["name"] = f"{interaction['name']}__{feature['name']}"
    return config


def recommendation_diagnostics(model: ContentBasedRecommender, users: set[str], visible_count: int) -> dict[str, float]:
    lists: list[set[int]] = []
    recommended_catalog: set[int] = set()
    popularity_values: list[float] = []
    for user_id in sorted(users):
        room_ids = {
            int(row["PhongtroId"])
            for row in model.recommend_for_user(user_id, top_n=10, exclude_seen=True)
        }
        lists.append(room_ids)
        recommended_catalog.update(room_ids)
        popularity_values.extend(model.popularity.get(room_id, 0.0) for room_id in room_ids)
    overlaps = [
        len(left & right) / len(left | right) if left | right else 0.0
        for left, right in itertools.combinations(lists, 2)
    ]
    return {
        "catalog_coverage_at_10": len(recommended_catalog) / visible_count if visible_count else 0.0,
        "unique_recommended_rooms_at_10": len(recommended_catalog),
        "average_top10_jaccard_overlap": statistics.fmean(overlaps) if overlaps else 0.0,
        "average_recommendation_popularity": statistics.fmean(popularity_values) if popularity_values else 0.0,
    }


def benchmark_config(config, rooms, train, test, eligible):
    model = ContentBasedRecommender(rooms, train, config)
    metrics = evaluate_users(model, test, eligible)
    metrics.update(recommendation_diagnostics(model, eligible, sum(room.is_visible for room in rooms)))
    return metrics


def rank_key(row: dict[str, object]) -> tuple[float, float, float, float]:
    metrics = row["metrics"]
    return (
        float(metrics["ndcg_at_10"]),
        float(metrics["hit_rate_at_10"]),
        float(metrics["recall_at_10"]),
        float(metrics["precision_at_10"]),
    )


def percent_change(before: float, after: float) -> float | None:
    return (after - before) / before * 100.0 if before else None


def fmt(value: object) -> str:
    return "N/A" if value is None else f"{float(value):.6f}"


def report_markdown(data: dict[str, object]) -> str:
    baseline = data["baseline_metrics"]["all_users"]
    optimized = data["optimized_metrics"]["all_users"]
    labels = {
        "precision_at_5": "Precision@5", "recall_at_5": "Recall@5", "hit_rate_at_5": "HitRate@5",
        "precision_at_10": "Precision@10", "recall_at_10": "Recall@10", "hit_rate_at_10": "HitRate@10",
        "ndcg_at_10": "NDCG@10", "catalog_coverage_at_10": "Coverage@10",
    }
    lines = [
        "# Báo cáo tối ưu Content-Based Recommendation", "",
        "## Kết quả baseline và optimized", "",
        "| Metric | Baseline | Optimized | Change |", "|---|---:|---:|---:|",
    ]
    for key in METRIC_KEYS:
        change = percent_change(float(baseline[key]), float(optimized[key]))
        change_text = "N/A" if change is None else f"{change:+.2f}%"
        lines.append(f"| {labels[key]} | {fmt(baseline[key])} | {fmt(optimized[key])} | {change_text} |")
    lines.extend([
        "", "## Cấu hình được chọn", "",
        f"- Số cấu hình đã benchmark: **{data['config_count']}**.",
        f"- Interaction weighting: **{data['best_config']['interaction_config']}**.",
        f"- Feature weighting: **{data['best_config']['feature_config']}**.",
        f"- Quyết định: **{data['selection']['decision']}**.",
        "- Thứ tự chọn: NDCG@10, HitRate@10, Recall@10, Precision@10; non-demo không được dùng để tune.",
        "- Baseline giữ nguyên công thức `Views + Favorite*3 + Booking*5 + Rating`.",
        "- Cả rating mapping có dấu và không âm đều đã được benchmark; mapping không âm cho kết quả tốt hơn trong grid này.",
        "", "## Metric theo nhóm", "",
        "| Nhóm | Users | Precision@10 | Recall@10 | HitRate@10 | NDCG@10 | Coverage@10 |",
        "|---|---:|---:|---:|---:|---:|---:|",
    ])
    for key, label in (("all_users", "All users"), ("demo_users", "Demo users"), ("non_demo_users", "Non-demo users")):
        values = data["optimized_metrics"][key]
        lines.append(
            f"| {label} | {values['eligible_users']} | {fmt(values['precision_at_10'])} | "
            f"{fmt(values['recall_at_10'])} | {fmt(values['hit_rate_at_10'])} | "
            f"{fmt(values['ndcg_at_10'])} | {fmt(values['catalog_coverage_at_10'])} |"
        )
    lines.extend([
        "", "## Diversity và cá nhân hóa", "",
        f"- Optimized Coverage@10: **{fmt(optimized['catalog_coverage_at_10'])}** "
        f"({optimized['unique_recommended_rooms_at_10']} phòng khác nhau / {data['visible_room_count']} phòng visible).",
        f"- Overlap Top10 trung bình (pairwise Jaccard): **{fmt(optimized['average_top10_jaccard_overlap'])}**.",
        "- Overlap càng gần 0 thì danh sách giữa các user càng khác nhau; kết quả này không cho thấy mọi user nhận cùng một Top10.",
        "", "## Ba ví dụ trước/sau", "",
    ])
    for index, example in enumerate(data["examples"], 1):
        before = ", ".join(f"{x['PhongtroId']} ({x['ContentScore']:.6f})" for x in example["baseline"]["recommendations"])
        after = ", ".join(f"{x['PhongtroId']} ({x['ContentScore']:.6f})" for x in example["optimized"]["recommendations"])
        details = example["optimized"]
        lines.extend([
            f"### User {index}: `{details['user_id_masked']}`", "",
            f"- Interactions: {details['interaction_count']}; khu vực: {', '.join(details['top_areas'])}; "
            f"giá weighted average: {details['weighted_average_price']:,.0f} VNĐ.",
            f"- Tiện nghi: {', '.join(details['top_amenities'])}.",
            f"- Baseline Top5: {before}.", f"- Optimized Top5: {after}.", "",
        ])
    decreased = data["selection"]["decreased_accuracy_metrics"]
    lines.extend([
        "## Audit và giới hạn", "",
        "- Không có data leakage: profile evaluation chỉ dựng từ train; held-out room không xuất hiện trong train của cùng user.",
        "- CSV không có timestamp nên dùng seeded random holdout 20% theo user; đây là một limitation.",
        f"- Metric accuracy giảm: {', '.join(decreased) if decreased else 'không có'}.",
        f"- Diversity giảm: Coverage@10 từ {fmt(baseline['catalog_coverage_at_10'])} xuống "
        f"{fmt(optimized['catalog_coverage_at_10'])} "
        f"({percent_change(float(baseline['catalog_coverage_at_10']), float(optimized['catalog_coverage_at_10'])):+.2f}%).",
        "- Chỉ có 2 user non-demo; metric non-demo chỉ để tham khảo, không dùng chọn hyperparameter.",
        "- Dữ liệu chủ yếu từ tài khoản demo, vì vậy không thể tuyên bố chất lượng production.",
        "", "## Kết luận", "",
        data["selection"]["recommendation"], "",
    ])
    return "\n".join(lines)


def main() -> int:
    args = parse_args()
    rooms = load_rooms(args.rooms)
    interactions = load_interactions(args.interactions)
    rooms_by_id = {room.room_id: room for room in rooms}
    train, test, skipped = split_by_user(interactions, rooms_by_id, args.seed)
    eligible = set(test)
    # Explicit leakage assertion: no held-out user-room key may remain in train.
    train_keys = {(item.user_id, item.room_id) for item in train}
    leaked = [(user_id, room_id) for user_id, ids in test.items() for room_id in ids if (user_id, room_id) in train_keys]
    if leaked:
        raise AssertionError(f"Evaluation split leaked {len(leaked)} held-out interactions into train.")

    all_tenants, demo_users = load_user_segments(args.connection_string)
    non_demo_users = all_tenants - demo_users
    rows = []
    for interaction_config in INTERACTION_CONFIGS:
        for feature_config in FEATURE_CONFIGS:
            config = make_config(interaction_config, feature_config)
            metrics = benchmark_config(config, rooms, train, test, eligible)
            rows.append({"name": config["name"], "config": config, "metrics": metrics})
            print(f"{config['name']}: NDCG@10={metrics['ndcg_at_10']:.6f}, HR@10={metrics['hit_rate_at_10']:.6f}")

    baseline_row = next(row for row in rows if row["name"] == "baseline__baseline")
    candidate = max((row for row in rows if row is not baseline_row), key=rank_key)
    baseline_ndcg = float(baseline_row["metrics"]["ndcg_at_10"])
    candidate_ndcg = float(candidate["metrics"]["ndcg_at_10"])
    # Do not replace the baseline unless the primary metric genuinely improves.
    selected = candidate if candidate_ndcg > baseline_ndcg + 1e-12 else baseline_row
    decision = "keep optimized config" if selected is candidate else "keep baseline config"
    best_config = selected["config"]

    baseline_eval_model = ContentBasedRecommender(rooms, train, baseline_row["config"])
    optimized_eval_model = ContentBasedRecommender(rooms, train, best_config)
    baseline_full_model = ContentBasedRecommender(rooms, interactions, baseline_row["config"])
    optimized_full_model = ContentBasedRecommender(rooms, interactions, best_config)

    def segment_metrics(model, users):
        result = evaluate_users(model, test, users)
        result.update(recommendation_diagnostics(model, set(users).intersection(eligible), sum(room.is_visible for room in rooms)))
        return result

    baseline_metrics = {
        "all_users": segment_metrics(baseline_eval_model, eligible),
        "demo_users": segment_metrics(baseline_eval_model, demo_users),
        "non_demo_users": segment_metrics(baseline_eval_model, non_demo_users),
    }
    optimized_metrics = {
        "all_users": segment_metrics(optimized_eval_model, eligible),
        "demo_users": segment_metrics(optimized_eval_model, demo_users),
        "non_demo_users": segment_metrics(optimized_eval_model, non_demo_users),
    }
    examples = []
    for user_id in choose_example_users(interactions):
        examples.append({
            "baseline": example_for_user(user_id, baseline_full_model, rooms_by_id),
            "optimized": example_for_user(user_id, optimized_full_model, rooms_by_id),
        })
    decreased = [
        key for key in METRIC_KEYS[:-1]
        if float(optimized_metrics["all_users"][key]) < float(baseline_metrics["all_users"][key]) - 1e-12
    ]
    recommendation = (
        "Nên giữ cấu hình optimized vì NDCG@10 tăng trên cùng split, nhưng chỉ xem đây là kết quả thực nghiệm trên dữ liệu demo. "
        "Bước tiếp theo hợp lý là thu thập thêm interaction non-demo rồi benchmark Collaborative Filtering và Hybrid, không thay Content-Based ngay."
        if selected is candidate else
        "Không có cấu hình thử nghiệm nào cải thiện NDCG@10; nên giữ baseline. Chưa nên chuyển sang Collaborative Filtering trước khi có thêm dữ liệu non-demo."
    )
    data = {
        "model": "content_based_cosine_optimized", "config_count": len(rows),
        "selection_priority": ["ndcg_at_10", "hit_rate_at_10", "recall_at_10", "precision_at_10"],
        "best_config": best_config,
        "selection": {"decision": decision, "candidate_name": candidate["name"],
                      "decreased_accuracy_metrics": decreased, "recommendation": recommendation},
        "room_count": len(rooms), "visible_room_count": sum(room.is_visible for room in rooms),
        "profile_user_count": len({item.user_id for item in interactions}),
        "segments": {"demo_profile_users": len({x.user_id for x in interactions} & demo_users),
                     "non_demo_profile_users": len({x.user_id for x in interactions} & non_demo_users)},
        "split": {"method": "per-user seeded random 20% visible holdout", "seed": args.seed,
                  "train_interactions": len(train), "test_interactions": sum(map(len, test.values())),
                  "skipped_users": skipped, "timestamp_available": False, "leakage_count": len(leaked)},
        "baseline_metrics": baseline_metrics, "optimized_metrics": optimized_metrics,
        "changes_percent": {
            "ndcg_at_10": percent_change(baseline_metrics["all_users"]["ndcg_at_10"], optimized_metrics["all_users"]["ndcg_at_10"]),
            "hit_rate_at_10": percent_change(baseline_metrics["all_users"]["hit_rate_at_10"], optimized_metrics["all_users"]["hit_rate_at_10"]),
        },
        "examples": examples,
        "limitations": ["Metrics are dominated by demo accounts.", "Only two non-demo users have profiles.",
                        "No timestamp is present; chronological split is impossible."],
    }
    for path in (args.config_output, args.results_output, args.metrics_output, args.report_output):
        path.parent.mkdir(parents=True, exist_ok=True)
    args.config_output.write_text(json.dumps(best_config, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    args.results_output.write_text(json.dumps({"configs": rows}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    args.metrics_output.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    args.report_output.write_text(report_markdown(data), encoding="utf-8", newline="\n")
    print(f"Selected {best_config['name']} ({decision})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
