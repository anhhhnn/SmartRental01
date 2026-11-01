"""Explainable content-based room recommendation.

Room vectors contain one-hot area, multi-hot amenities, and standardized price,
floor area, and capacity. User profiles are score-weighted means of room vectors.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Sequence

import numpy as np
from scipy import sparse
from sklearn.metrics.pairwise import cosine_similarity
from sklearn.preprocessing import MultiLabelBinarizer, OneHotEncoder, StandardScaler


PROJECT_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_ROOMS = PROJECT_ROOT / "AI" / "DATA" / "rooms_recommendation.csv"
DEFAULT_INTERACTIONS = PROJECT_ROOT / "AI" / "DATA" / "interactions.csv"
DEFAULT_CONFIG = PROJECT_ROOT / "AI" / "recommendation" / "content_based_config.json"

BASELINE_CONFIG: dict[str, object] = {
    "name": "baseline",
    "view_transform": "identity",
    "view_weight": 1.0,
    "favorite_weight": 3.0,
    "booking_weight": 5.0,
    "rating_mapping": {"0": 0.0, "1": 1.0, "2": 2.0, "3": 3.0, "4": 4.0, "5": 5.0},
    "feature_weights": {
        "khuVuc": 1.0,
        "tienNghi": 1.0,
        "gia": 1.0,
        "dienTich": 1.0,
        "soNguoi": 1.0,
    },
}


@dataclass(frozen=True)
class RoomRecord:
    room_id: int
    area: str
    price: float
    size: float
    capacity: int
    amenities: tuple[str, ...]
    is_visible: bool


@dataclass(frozen=True)
class Interaction:
    user_id: str
    room_id: int
    score: float
    booking: bool = False
    views: float = 0.0
    favorite: bool = False
    rating: int = 0


def load_config(path: Path | str = DEFAULT_CONFIG) -> dict[str, object]:
    """Load and validate a recommendation config without mutating the defaults."""
    path = Path(path)
    data = json.loads(path.read_text(encoding="utf-8"))
    required = {
        "view_transform", "view_weight", "favorite_weight", "booking_weight",
        "rating_mapping", "feature_weights",
    }
    missing = required.difference(data)
    if missing:
        raise ValueError(f"Recommendation config is missing keys: {sorted(missing)}")
    if data["view_transform"] not in {"identity", "log1p"}:
        raise ValueError("view_transform must be 'identity' or 'log1p'.")
    rating_mapping = data["rating_mapping"]
    if not isinstance(rating_mapping, dict) or not all(str(value) in rating_mapping for value in range(6)):
        raise ValueError("rating_mapping must define string keys 0 through 5.")
    feature_weights = data["feature_weights"]
    expected_features = {"khuVuc", "tienNghi", "gia", "dienTich", "soNguoi"}
    if not isinstance(feature_weights, dict) or set(feature_weights) != expected_features:
        raise ValueError(f"feature_weights must contain exactly {sorted(expected_features)}")
    numeric_values = [
        data["view_weight"], data["favorite_weight"], data["booking_weight"],
        *rating_mapping.values(), *feature_weights.values(),
    ]
    if not all(np.isfinite(float(value)) for value in numeric_values):
        raise ValueError("Recommendation config contains NaN or infinity.")
    if any(float(feature_weights[name]) <= 0 for name in expected_features):
        raise ValueError("Feature weights must be greater than zero.")
    return data


def interaction_weight(item: Interaction, config: dict[str, object]) -> float:
    """Transform raw signals into a profile weight for one user-room pair."""
    views = float(item.views)
    if views < 0 or not np.isfinite(views):
        raise ValueError("Views must be finite and non-negative.")
    view_signal = math.log1p(views) if config["view_transform"] == "log1p" else views
    rating_mapping = config["rating_mapping"]
    rating_signal = float(rating_mapping[str(item.rating)])
    weight = (
        view_signal * float(config["view_weight"])
        + float(item.favorite) * float(config["favorite_weight"])
        + float(item.booking) * float(config["booking_weight"])
        + rating_signal
    )
    if not np.isfinite(weight):
        raise ValueError("Interaction weight is NaN or infinity.")
    return weight


def _parse_bool(value: str) -> bool:
    normalized = value.strip().lower()
    if normalized in {"1", "true", "yes"}:
        return True
    if normalized in {"0", "false", "no"}:
        return False
    raise ValueError(f"Invalid boolean value: {value!r}")


def load_rooms(path: Path | str = DEFAULT_ROOMS) -> list[RoomRecord]:
    path = Path(path)
    rooms: list[RoomRecord] = []
    with path.open(encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        required = {"PhongtroId", "KhuVuc", "Gia", "DienTich", "SoNguoiToiDa", "TienNghi", "IsVisible"}
        missing = required.difference(reader.fieldnames or [])
        if missing:
            raise ValueError(f"Room dataset is missing columns: {sorted(missing)}")
        for line_number, row in enumerate(reader, 2):
            try:
                amenities = tuple(sorted({item.strip() for item in row["TienNghi"].split("|") if item.strip()}))
                room = RoomRecord(
                    room_id=int(row["PhongtroId"]),
                    area=row["KhuVuc"].strip(),
                    price=float(row["Gia"]),
                    size=float(row["DienTich"]),
                    capacity=int(row["SoNguoiToiDa"]),
                    amenities=amenities,
                    is_visible=_parse_bool(row["IsVisible"]),
                )
            except (TypeError, ValueError) as exc:
                raise ValueError(f"Invalid room data at line {line_number}: {exc}") from exc
            rooms.append(room)
    ids = [room.room_id for room in rooms]
    if not rooms or len(ids) != len(set(ids)):
        raise ValueError("Room dataset must be non-empty with exactly one row per PhongtroId.")
    return rooms


def load_interactions(path: Path | str = DEFAULT_INTERACTIONS) -> list[Interaction]:
    path = Path(path)
    interactions: list[Interaction] = []
    with path.open(encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        required = {"UserId", "PhongtroId", "Views", "Favorite", "Booking", "Rating", "Score"}
        missing = required.difference(reader.fieldnames or [])
        if missing:
            raise ValueError(f"Interaction dataset is missing columns: {sorted(missing)}")
        for line_number, row in enumerate(reader, 2):
            try:
                score = float(row["Score"])
                if score <= 0 or not np.isfinite(score):
                    raise ValueError("Score must be finite and greater than zero")
                views = float(row["Views"])
                favorite = _parse_bool(row["Favorite"])
                booking = _parse_bool(row["Booking"])
                rating = int(row["Rating"])
                if views < 0 or not np.isfinite(views):
                    raise ValueError("Views must be finite and non-negative")
                if rating not in range(6):
                    raise ValueError("Rating must be between 0 and 5")
                interactions.append(
                    Interaction(
                        user_id=row["UserId"].strip(),
                        room_id=int(row["PhongtroId"]),
                        score=score,
                        booking=booking,
                        views=views,
                        favorite=favorite,
                        rating=rating,
                    )
                )
            except (TypeError, ValueError) as exc:
                raise ValueError(f"Invalid interaction at line {line_number}: {exc}") from exc
    return interactions


class ContentBasedRecommender:
    """Content recommender whose fitted state is reproducible from the CSV files."""

    feature_groups = ("KhuVuc", "TienNghi", "Gia", "DienTich", "SoNguoiToiDa")

    def __init__(
        self,
        rooms: Sequence[RoomRecord],
        interactions: Iterable[Interaction],
        config: dict[str, object] | None = None,
    ) -> None:
        if not rooms:
            raise ValueError("At least one room is required.")
        self.rooms = list(rooms)
        # Production-facing callers use the benchmark winner by default. Tests
        # and offline comparisons can still pass BASELINE_CONFIG explicitly.
        self.config = dict(load_config(DEFAULT_CONFIG) if config is None else config)
        self.room_ids = np.asarray([room.room_id for room in self.rooms], dtype=np.int64)
        self.room_index = {room_id: index for index, room_id in enumerate(self.room_ids.tolist())}
        if len(self.room_index) != len(self.rooms):
            raise ValueError("Duplicate room IDs are not allowed.")

        self.area_encoder = OneHotEncoder(handle_unknown="ignore", sparse_output=True, dtype=np.float64)
        self.amenity_encoder = MultiLabelBinarizer(sparse_output=True)
        self.numeric_scaler = StandardScaler()

        areas = np.asarray([[room.area] for room in self.rooms], dtype=object)
        amenities = [room.amenities for room in self.rooms]
        numeric = np.asarray([[room.price, room.size, room.capacity] for room in self.rooms], dtype=np.float64)
        if not np.isfinite(numeric).all():
            raise ValueError("Numeric room features contain NaN or infinity.")

        feature_weights = self.config["feature_weights"]
        area_matrix = self.area_encoder.fit_transform(areas) * float(feature_weights["khuVuc"])
        amenity_matrix = (
            self.amenity_encoder.fit_transform(amenities).astype(np.float64)
            * float(feature_weights["tienNghi"])
        )
        numeric_scaled = self.numeric_scaler.fit_transform(numeric)
        numeric_scaled *= np.asarray([
            float(feature_weights["gia"]),
            float(feature_weights["dienTich"]),
            float(feature_weights["soNguoi"]),
        ])
        numeric_matrix = sparse.csr_matrix(numeric_scaled)
        self.room_vectors = sparse.hstack(
            [area_matrix, amenity_matrix, numeric_matrix], format="csr", dtype=np.float64
        )
        if not np.isfinite(self.room_vectors.data).all():
            raise ValueError("Room vectors contain NaN or infinity.")

        valid_interactions: list[Interaction] = []
        orphan_count = 0
        for item in interactions:
            if item.room_id in self.room_index:
                valid_interactions.append(item)
            else:
                orphan_count += 1
        if orphan_count:
            raise ValueError(f"Interactions contain {orphan_count} room references absent from the catalogue.")
        self.interactions = valid_interactions
        self.by_user: dict[str, list[Interaction]] = defaultdict(list)
        self.popularity: dict[int, float] = defaultdict(float)
        for item in self.interactions:
            self.by_user[item.user_id].append(item)
            self.popularity[item.room_id] += max(0.0, self.weight_for_interaction(item))

    def weight_for_interaction(self, item: Interaction) -> float:
        # Hand-built Interaction objects from the original public API only carry
        # score. Preserve that API when the baseline config is in use.
        has_raw_signal = bool(item.views or item.favorite or item.booking or item.rating)
        if self.config.get("name") == "baseline" and not has_raw_signal:
            return float(item.score)
        return interaction_weight(item, self.config)

    @property
    def vector_dimension(self) -> int:
        return int(self.room_vectors.shape[1])

    @property
    def feature_names(self) -> list[str]:
        area_names = self.area_encoder.get_feature_names_out(["KhuVuc"]).tolist()
        amenity_names = [f"TienNghi={name}" for name in self.amenity_encoder.classes_.tolist()]
        return area_names + amenity_names + ["Gia_scaled", "DienTich_scaled", "SoNguoiToiDa_scaled"]

    def user_profile(self, user_id: str) -> sparse.csr_matrix | None:
        items = self.by_user.get(str(user_id), [])
        if not items:
            return None
        indices = np.asarray([self.room_index[item.room_id] for item in items], dtype=np.int64)
        weights = np.asarray([self.weight_for_interaction(item) for item in items], dtype=np.float64)
        # Absolute normalization is stable for signed rating mappings and keeps
        # disliked rooms as negative evidence instead of cancelling the divisor.
        weight_sum = float(np.abs(weights).sum())
        if weight_sum <= 0 or not np.isfinite(weight_sum):
            return None
        profile = sparse.csr_matrix(self.room_vectors[indices].multiply(weights[:, None]).sum(axis=0) / weight_sum)
        if not np.isfinite(profile.data).all():
            raise ValueError(f"Profile for user {user_id!r} contains NaN or infinity.")
        return profile

    def recommend_for_user(self, user_id: str, top_n: int = 10, exclude_seen: bool = True) -> list[dict[str, object]]:
        """Return ranked rows with UserId, PhongtroId, and ContentScore.

        For cold-start users, visible rooms are ranked by aggregate interaction
        score and ContentScore is ``None`` to avoid presenting popularity as a
        personalized cosine score.
        """
        if top_n < 1:
            raise ValueError("top_n must be at least 1.")
        user_id = str(user_id)
        profile = self.user_profile(user_id)
        seen = {item.room_id for item in self.by_user.get(user_id, [])} if exclude_seen else set()
        candidate_indices = [
            index for index, room in enumerate(self.rooms)
            if room.is_visible and room.room_id not in seen
        ]
        if profile is None:
            ranked = sorted(
                candidate_indices,
                key=lambda index: (-self.popularity.get(self.rooms[index].room_id, 0.0), self.rooms[index].room_id),
            )[:top_n]
            return [
                {"UserId": user_id, "PhongtroId": self.rooms[index].room_id, "ContentScore": None}
                for index in ranked
            ]
        if not candidate_indices:
            return []
        scores = cosine_similarity(profile, self.room_vectors[candidate_indices]).ravel()
        ranked_pairs = sorted(
            zip(candidate_indices, scores),
            key=lambda pair: (-float(pair[1]), self.rooms[pair[0]].room_id),
        )[:top_n]
        return [
            {"UserId": user_id, "PhongtroId": self.rooms[index].room_id, "ContentScore": float(score)}
            for index, score in ranked_pairs
        ]


def recommend_for_user(
    user_id: str,
    top_n: int = 10,
    exclude_seen: bool = True,
    rooms_path: Path | str = DEFAULT_ROOMS,
    interactions_path: Path | str = DEFAULT_INTERACTIONS,
    config_path: Path | str = DEFAULT_CONFIG,
) -> list[dict[str, object]]:
    recommender = ContentBasedRecommender(
        load_rooms(rooms_path), load_interactions(interactions_path), load_config(config_path)
    )
    return recommender.recommend_for_user(user_id, top_n=top_n, exclude_seen=exclude_seen)


def main() -> int:
    parser = argparse.ArgumentParser(description="Recommend rooms with content similarity.")
    parser.add_argument("user_id")
    parser.add_argument("--top-n", type=int, default=10)
    parser.add_argument("--include-seen", action="store_true")
    parser.add_argument("--rooms", type=Path, default=DEFAULT_ROOMS)
    parser.add_argument("--interactions", type=Path, default=DEFAULT_INTERACTIONS)
    parser.add_argument("--config", type=Path, default=DEFAULT_CONFIG)
    args = parser.parse_args()
    rows = recommend_for_user(
        args.user_id, args.top_n, not args.include_seen, args.rooms, args.interactions, args.config
    )
    print(json.dumps(rows, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
