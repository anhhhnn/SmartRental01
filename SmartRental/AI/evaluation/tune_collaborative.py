"""Tune implicit ALS on an inner holdout, then evaluate once on the outer holdout."""

from __future__ import annotations

import json
import sys
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[2]
AI_ROOT = PROJECT_ROOT / "AI"
sys.path.insert(0, str(AI_ROOT))
sys.path.insert(0, str(AI_ROOT / "evaluation"))

from recommendation.collaborative import CollaborativeRecommender  # noqa: E402
from recommendation.content_based import load_interactions, load_rooms  # noqa: E402
from evaluate_content_based import load_user_segments  # noqa: E402
from evaluate_collaborative import (  # noqa: E402
    DEFAULT_INTERACTIONS, DEFAULT_ROOMS, DEFAULT_SEED, evaluate_model,
    produce_outputs, split_by_user,
)

RESULTS_PATH = AI_ROOT / "evaluation" / "collaborative_tuning_results.json"
CONFIG_PATH = AI_ROOT / "recommendation" / "collaborative_config.json"

# A deliberate 12-point design covers every requested value without a costly
# 81-point Cartesian product. More iterations are concentrated near reg=0.05.
CONFIGS = [
    (16, 0.01, 15, 10), (16, 0.05, 30, 20), (16, 0.10, 15, 40),
    (32, 0.01, 15, 20), (32, 0.01, 30, 10), (32, 0.05, 15, 10),
    (32, 0.05, 30, 20), (32, 0.10, 30, 40),
    (64, 0.01, 15, 10), (64, 0.05, 15, 20), (64, 0.05, 30, 40),
    (64, 0.10, 15, 40),
]


def selection_key(row: dict[str, object]) -> tuple[float, ...]:
    metrics = row["demo_metrics"]
    return tuple(float(metrics[key]) for key in (
        "ndcg_at_10", "hit_rate_at_10", "recall_at_10", "precision_at_10",
        "catalog_coverage_at_10",
    ))


def main() -> int:
    interactions = load_interactions(DEFAULT_INTERACTIONS)
    rooms = load_rooms(DEFAULT_ROOMS)
    rooms_by_id = {room.room_id: room for room in rooms}
    outer_train, _, outer_audit = split_by_user(interactions, rooms_by_id, DEFAULT_SEED)
    inner_train, inner_test, inner_audit = split_by_user(outer_train, rooms_by_id, DEFAULT_SEED + 1)
    _, demo_users = load_user_segments(None)
    visible_count = sum(room.is_visible for room in rooms)
    from collections import Counter
    popularity = Counter(item.room_id for item in inner_train)
    rows = []
    for factors, regularization, iterations, alpha in CONFIGS:
        config = {
            "factors": factors, "regularization": regularization,
            "iterations": iterations, "alpha": alpha, "seed": DEFAULT_SEED,
            "strength_transform": "log1p",
        }
        model = CollaborativeRecommender(
            factors=factors, regularization=regularization, iterations=iterations,
            alpha=alpha, seed=DEFAULT_SEED,
        ).fit(inner_train, rooms)
        rows.append({
            "config": config,
            "demo_metrics": evaluate_model(model, inner_test, demo_users, visible_count, popularity),
            "all_metrics_reference_only": evaluate_model(model, inner_test, inner_test, visible_count, popularity),
        })
        print(f"finished {len(rows)}/{len(CONFIGS)}: {config}", flush=True)
    best = max(rows, key=selection_key)
    best_config = dict(best["config"])
    best_config["confidence_formula"] = "1 + alpha * log1p(Score)"
    best_config["benchmarked_configs"] = len(rows)
    RESULTS_PATH.write_text(json.dumps({
        "selection_policy": ["NDCG@10", "HitRate@10", "Recall@10", "Precision@10", "Coverage@10"],
        "tuning_population": "demo users only", "outer_split": outer_audit,
        "inner_split": inner_audit, "configs": rows, "selected_config": best_config,
    }, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    CONFIG_PATH.write_text(json.dumps(best_config, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    produce_outputs(best_config, len(rows), RESULTS_PATH)
    print(f"selected: {best_config}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
