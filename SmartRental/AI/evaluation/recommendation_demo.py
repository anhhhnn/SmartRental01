"""Produce the three official recommendation demo scenarios without exposing IDs."""

from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any


if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")


AI_DIR = Path(__file__).resolve().parents[1]
if str(AI_DIR) not in sys.path:
    sys.path.insert(0, str(AI_DIR))

from recommendation.recommendation_service import RecommendationService  # noqa: E402


def mask_user_id(user_id: str) -> str:
    if len(user_id) <= 8:
        return "***"
    return f"{user_id[:4]}...{user_id[-4:]}"


def scenario(
    service: RecommendationService, label: str, user_id: str
) -> dict[str, Any]:
    profile = service.get_user_profile_summary(user_id)
    response = service.recommend_for_user(user_id, top_n=5, exclude_seen=True)
    return {
        "group": label,
        "userIdMasked": mask_user_id(user_id),
        "profile": profile,
        "coldStart": response["coldStart"],
        "model": response["model"],
        "top5": [
            {
                "phongtroId": item["phongtroId"],
                "rank": item["rank"],
                "reasons": item["reasons"],
            }
            for item in response["recommendations"]
        ],
    }


def build_demo() -> list[dict[str, Any]]:
    service = RecommendationService()
    users_by_count = sorted(
        service.recommender.by_user,
        key=lambda user_id: (len(service.recommender.by_user[user_id]), user_id),
    )
    return [
        scenario(service, "User A - nhiều interaction", users_by_count[-1]),
        scenario(service, "User B - ít interaction", users_by_count[0]),
        scenario(service, "User C - cold-start", "cold-start-demo"),
    ]


if __name__ == "__main__":
    print(json.dumps(build_demo(), ensure_ascii=False, indent=2))
