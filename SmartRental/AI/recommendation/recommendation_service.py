"""Reusable in-memory recommendation service for the FastAPI application.

The service is rebuilt only when the process starts.  Runtime requests never
reload the exported CSV files or refit the content feature encoders.
"""

from __future__ import annotations

import math
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any

import numpy as np
from scipy import sparse
from sklearn.metrics.pairwise import cosine_similarity

try:
    from .content_based import (
        DEFAULT_CONFIG,
        DEFAULT_INTERACTIONS,
        DEFAULT_ROOMS,
        ContentBasedRecommender,
        Interaction,
        RoomRecord,
        load_config,
        load_interactions,
        load_rooms,
    )
except ImportError:  # Support running this module directly from its directory.
    from content_based import (  # type: ignore[no-redef]
        DEFAULT_CONFIG,
        DEFAULT_INTERACTIONS,
        DEFAULT_ROOMS,
        ContentBasedRecommender,
        Interaction,
        RoomRecord,
        load_config,
        load_interactions,
        load_rooms,
    )


class RecommendationService:
    """Read-only runtime facade over the optimized content-based model."""

    model_name = "content_based_optimized"
    fallback_model_name = "popular_fallback"

    def __init__(
        self,
        rooms_path: Path | str = DEFAULT_ROOMS,
        interactions_path: Path | str = DEFAULT_INTERACTIONS,
        config_path: Path | str = DEFAULT_CONFIG,
    ) -> None:
        self.rooms_path = Path(rooms_path)
        self.interactions_path = Path(interactions_path)
        self.config_path = Path(config_path)
        self.config = load_config(self.config_path)
        self.rooms = load_rooms(self.rooms_path)
        self.interactions = load_interactions(self.interactions_path)
        self.recommender = ContentBasedRecommender(
            self.rooms, self.interactions, self.config
        )

        self._profiles: dict[str, sparse.csr_matrix] = {}
        for user_id in self.recommender.by_user:
            profile = self.recommender.user_profile(user_id)
            if profile is not None:
                self._profiles[user_id] = profile

        self._visible_indices = tuple(
            index for index, room in enumerate(self.rooms) if room.is_visible
        )
        self._fallback_indices = self._build_fallback_order()

    @property
    def room_count(self) -> int:
        return len(self.rooms)

    @property
    def visible_room_count(self) -> int:
        return len(self._visible_indices)

    @property
    def user_profile_count(self) -> int:
        return len(self._profiles)

    @property
    def config_name(self) -> str:
        return str(self.config.get("name", "unknown"))

    def _build_fallback_order(self) -> tuple[int, ...]:
        """Rank visible rooms by deterministic catalogue popularity signals."""
        interaction_count: Counter[int] = Counter()
        favorite_count: Counter[int] = Counter()
        rating_total: defaultdict[int, float] = defaultdict(float)
        rating_count: Counter[int] = Counter()
        view_count: defaultdict[int, float] = defaultdict(float)

        for item in self.interactions:
            interaction_count[item.room_id] += 1
            favorite_count[item.room_id] += int(item.favorite)
            view_count[item.room_id] += item.views
            if item.rating > 0:
                rating_total[item.room_id] += item.rating
                rating_count[item.room_id] += 1

        def rank_key(index: int) -> tuple[float, float, float, float, int]:
            room_id = self.rooms[index].room_id
            average_rating = (
                rating_total[room_id] / rating_count[room_id]
                if rating_count[room_id]
                else 0.0
            )
            return (
                -float(interaction_count[room_id]),
                -float(favorite_count[room_id]),
                -average_rating,
                -view_count[room_id],
                room_id,
            )

        return tuple(sorted(self._visible_indices, key=rank_key))

    def _fallback(self, user_id: str, top_n: int, exclude_seen: bool) -> list[dict[str, Any]]:
        seen = (
            {item.room_id for item in self.recommender.by_user.get(user_id, ())}
            if exclude_seen
            else set()
        )
        indices = [
            index for index in self._fallback_indices
            if self.rooms[index].room_id not in seen
        ][:top_n]
        catalogue_size = max(1, len(self._fallback_indices))
        fallback_rank = {index: rank for rank, index in enumerate(self._fallback_indices)}
        return [
            {
                "phongtroId": self.rooms[index].room_id,
                "score": float(1.0 - fallback_rank[index] / catalogue_size),
                "rank": rank,
                "reasons": ["Phòng đang được nhiều người quan tâm"],
            }
            for rank, index in enumerate(indices, 1)
        ]

    def _reasons_for_room(
        self, room: RoomRecord, profile_summary: dict[str, Any]
    ) -> list[str]:
        """Explain the strongest weighted feature overlaps for one room.

        Reasons are deterministic and use the same interaction weights, room
        features, and optimized feature weights as the ranking model. They are
        not generated text and deliberately omit the cosine score.
        """
        feature_weights = self.config["feature_weights"]
        candidates: list[tuple[float, int, str]] = []

        top_areas = profile_summary.get("topAreas", [])
        if room.area in top_areas:
            area_position = top_areas.index(room.area)
            candidates.append(
                (
                    float(feature_weights["khuVuc"]) * (1.0 - area_position * 0.15),
                    0,
                    "Cùng khu vực bạn thường xem",
                )
            )

        top_amenities = profile_summary.get("topAmenities", [])
        matching_amenities = [
            amenity for amenity in top_amenities if amenity in room.amenities
        ]
        if matching_amenities:
            denominator = max(1, min(3, len(top_amenities)))
            amenity_score = (
                float(feature_weights["tienNghi"])
                * min(3, len(matching_amenities))
                / denominator
            )
            if len(matching_amenities) >= 2:
                amenity_names = " và ".join(matching_amenities[:2])
                amenity_reason = f"Có {amenity_names} bạn thường quan tâm"
            else:
                amenity_reason = (
                    f"Có tiện nghi {matching_amenities[0]} bạn thường quan tâm"
                )
            candidates.append((amenity_score, 1, amenity_reason))

        average_price = profile_summary.get("averagePrice")
        if average_price and average_price > 0:
            relative_price_gap = abs(room.price - average_price) / average_price
            if relative_price_gap <= 0.25:
                candidates.append(
                    (
                        float(feature_weights["gia"]) * (1.0 - relative_price_gap),
                        2,
                        "Gần mức giá bạn thường quan tâm",
                    )
                )

        average_capacity = profile_summary.get("averageCapacity")
        if average_capacity and abs(room.capacity - average_capacity) <= 1.0:
            capacity_gap = abs(room.capacity - average_capacity)
            candidates.append(
                (
                    float(feature_weights["soNguoi"]) / (1.0 + capacity_gap),
                    3,
                    "Phù hợp với sức chứa bạn thường xem",
                )
            )

        candidates.sort(key=lambda candidate: (-candidate[0], candidate[1]))
        return [reason for _, _, reason in candidates[:3]]

    def recommend_for_user(
        self, user_id: str, top_n: int = 10, exclude_seen: bool = True
    ) -> dict[str, Any]:
        """Return a complete API-ready recommendation response."""
        if not 1 <= top_n <= 50:
            raise ValueError("top_n must be between 1 and 50.")

        normalized_user_id = str(user_id)
        user_known = normalized_user_id in self.recommender.by_user
        profile = self._profiles.get(normalized_user_id)
        if profile is None:
            recommendations = self._fallback(
                normalized_user_id, top_n, exclude_seen
            )
            return {
                "userId": normalized_user_id,
                "userKnown": user_known,
                "coldStart": True,
                "model": self.fallback_model_name,
                "recommendations": recommendations,
            }

        seen = (
            {item.room_id for item in self.recommender.by_user[normalized_user_id]}
            if exclude_seen
            else set()
        )
        candidate_indices = [
            index for index in self._visible_indices
            if self.rooms[index].room_id not in seen
        ]
        if candidate_indices:
            scores = cosine_similarity(
                profile, self.recommender.room_vectors[candidate_indices]
            ).ravel()
            ranked = sorted(
                zip(candidate_indices, scores),
                key=lambda pair: (-float(pair[1]), self.rooms[pair[0]].room_id),
            )[:top_n]
        else:
            ranked = []

        profile_summary = self.get_user_profile_summary(normalized_user_id)

        recommendations = [
            {
                "phongtroId": self.rooms[index].room_id,
                "score": float(score),
                "rank": rank,
                "reasons": self._reasons_for_room(
                    self.rooms[index], profile_summary
                ),
            }
            for rank, (index, score) in enumerate(ranked, 1)
        ]
        return {
            "userId": normalized_user_id,
            "userKnown": True,
            "coldStart": False,
            "model": self.model_name,
            "recommendations": recommendations,
        }

    def get_user_profile_summary(self, user_id: str) -> dict[str, Any]:
        """Return non-sensitive, human-readable preferences for diagnostics."""
        normalized_user_id = str(user_id)
        items: list[Interaction] = self.recommender.by_user.get(normalized_user_id, [])
        profile = self._profiles.get(normalized_user_id)
        if profile is None:
            return {
                "userKnown": normalized_user_id in self.recommender.by_user,
                "coldStart": True,
                "interactionCount": len(items),
            }

        area_weights: defaultdict[str, float] = defaultdict(float)
        amenity_weights: defaultdict[str, float] = defaultdict(float)
        price_total = 0.0
        size_total = 0.0
        capacity_total = 0.0
        total_weight = 0.0
        for item in items:
            room: RoomRecord = self.rooms[self.recommender.room_index[item.room_id]]
            weight = max(0.0, self.recommender.weight_for_interaction(item))
            if not math.isfinite(weight) or weight <= 0:
                continue
            area_weights[room.area] += weight
            for amenity in room.amenities:
                amenity_weights[amenity] += weight
            price_total += room.price * weight
            size_total += room.size * weight
            capacity_total += room.capacity * weight
            total_weight += weight

        def top_values(values: dict[str, float]) -> list[str]:
            return [
                name for name, _ in sorted(
                    values.items(), key=lambda pair: (-pair[1], pair[0])
                )[:3]
            ]

        return {
            "userKnown": True,
            "coldStart": False,
            "interactionCount": len(items),
            "topAreas": top_values(area_weights),
            "topAmenities": top_values(amenity_weights),
            "averagePrice": price_total / total_weight if total_weight else None,
            "averageSize": size_total / total_weight if total_weight else None,
            "averageCapacity": capacity_total / total_weight if total_weight else None,
        }
