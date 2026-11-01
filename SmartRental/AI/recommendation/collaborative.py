"""Deterministic implicit-feedback ALS recommender for SmartRental.

The model treats every observed user-room pair as positive implicit feedback.
``Score`` controls confidence after a log transform; it is never interpreted as
an explicit 1--5 rating.
"""

from __future__ import annotations

import json
import math
import platform
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Iterable, Sequence

import numpy as np
from scipy import sparse

try:  # Support both ``recommendation.*`` and ``AI.recommendation.*`` imports.
    from recommendation.content_based import Interaction, RoomRecord
except ModuleNotFoundError:  # pragma: no cover - exercised by package consumers
    from .content_based import Interaction, RoomRecord


DEFAULT_SEED = 20261028


class CollaborativeRecommender:
    """Hu-Koren-Volinsky weighted matrix factorization solved with ALS."""

    def __init__(
        self,
        factors: int = 32,
        regularization: float = 0.05,
        iterations: int = 30,
        alpha: float = 20.0,
        seed: int = DEFAULT_SEED,
    ) -> None:
        if factors < 1 or iterations < 1 or regularization <= 0 or alpha <= 0:
            raise ValueError("factors/iterations must be positive and regularization/alpha > 0")
        self.config = {
            "factors": int(factors),
            "regularization": float(regularization),
            "iterations": int(iterations),
            "alpha": float(alpha),
            "strength_transform": "log1p",
            "confidence_formula": "1 + alpha * log1p(Score)",
            "seed": int(seed),
        }
        self.user_ids: list[str] = []
        self.room_ids: list[int] = []
        self.user_index: dict[str, int] = {}
        self.room_index: dict[int, int] = {}
        self.user_factors = np.empty((0, factors), dtype=np.float64)
        self.room_factors = np.empty((0, factors), dtype=np.float64)
        self.train_matrix = sparse.csr_matrix((0, 0), dtype=np.float64)
        self.visible_room_ids: set[int] = set()
        self.metadata: dict[str, object] = {}

    @staticmethod
    def _least_squares(
        fixed: np.ndarray, confidence: sparse.csr_matrix, regularization: float
    ) -> np.ndarray:
        """Solve one ALS half-step without materialising the dense preference matrix."""
        rows = confidence.shape[0]
        factors = fixed.shape[1]
        result = np.zeros((rows, factors), dtype=np.float64)
        gram = fixed.T @ fixed
        regularizer = np.eye(factors, dtype=np.float64) * regularization
        for row in range(rows):
            start, end = confidence.indptr[row], confidence.indptr[row + 1]
            indices = confidence.indices[start:end]
            values = confidence.data[start:end]
            if not len(indices):
                continue
            selected = fixed[indices]
            extra = values - 1.0
            lhs = gram + (selected.T * extra) @ selected + regularizer
            rhs = selected.T @ values
            result[row] = np.linalg.solve(lhs, rhs)
        return result

    def fit(
        self,
        interactions: Iterable[Interaction],
        rooms: Sequence[RoomRecord] | Sequence[int],
    ) -> "CollaborativeRecommender":
        items = list(interactions)
        if not items:
            raise ValueError("At least one training interaction is required")
        if not rooms:
            raise ValueError("At least one catalogue room is required")
        if isinstance(rooms[0], RoomRecord):
            records = list(rooms)  # type: ignore[arg-type]
            room_ids = [int(room.room_id) for room in records]
            visible = {int(room.room_id) for room in records if room.is_visible}
        else:
            room_ids = [int(room_id) for room_id in rooms]  # type: ignore[arg-type]
            visible = set(room_ids)
        self.user_ids = sorted({str(item.user_id) for item in items})
        self.room_ids = sorted(set(room_ids))
        if len(self.room_ids) != len(room_ids):
            raise ValueError("Catalogue contains duplicate room IDs")
        self.user_index = {value: index for index, value in enumerate(self.user_ids)}
        self.room_index = {value: index for index, value in enumerate(self.room_ids)}
        self.visible_room_ids = visible

        rows: list[int] = []
        cols: list[int] = []
        data: list[float] = []
        seen_pairs: set[tuple[str, int]] = set()
        for item in items:
            user_id, room_id = str(item.user_id), int(item.room_id)
            if room_id not in self.room_index:
                raise ValueError(f"Interaction room {room_id} is absent from the catalogue")
            pair = (user_id, room_id)
            if pair in seen_pairs:
                raise ValueError(f"Duplicate user-room interaction: {pair}")
            if item.score <= 0 or not math.isfinite(float(item.score)):
                raise ValueError("Interaction Score must be finite and greater than zero")
            seen_pairs.add(pair)
            rows.append(self.user_index[user_id])
            cols.append(self.room_index[room_id])
            data.append(1.0 + self.config["alpha"] * math.log1p(float(item.score)))
        confidence = sparse.csr_matrix(
            (np.asarray(data), (rows, cols)),
            shape=(len(self.user_ids), len(self.room_ids)),
            dtype=np.float64,
        )
        self.train_matrix = confidence.copy()
        self.train_matrix.data[:] = 1.0

        rng = np.random.default_rng(int(self.config["seed"]))
        scale = 1.0 / math.sqrt(int(self.config["factors"]))
        self.room_factors = rng.normal(0.0, scale, (len(self.room_ids), int(self.config["factors"])))
        for _ in range(int(self.config["iterations"])):
            self.user_factors = self._least_squares(
                self.room_factors, confidence, float(self.config["regularization"])
            )
            self.room_factors = self._least_squares(
                self.user_factors, confidence.T.tocsr(), float(self.config["regularization"])
            )
        if not np.isfinite(self.user_factors).all() or not np.isfinite(self.room_factors).all():
            raise ValueError("ALS produced NaN or infinite factors")
        self.metadata = {
            "algorithm": "implicit_als_hu_koren_volinsky",
            "implementation": "numpy_scipy",
            "trained_at_utc": datetime.now(timezone.utc).isoformat(),
            "python_version": platform.python_version(),
            "numpy_version": np.__version__,
            "scipy_version": __import__("scipy").__version__,
            "training_interactions": len(items),
            "users": len(self.user_ids),
            "rooms": len(self.room_ids),
            "visible_rooms": len(self.visible_room_ids),
            "config": dict(self.config),
        }
        return self

    def score_user_room(self, user_id: str, room_id: int) -> float | None:
        if str(user_id) not in self.user_index or int(room_id) not in self.room_index:
            return None
        return float(
            self.user_factors[self.user_index[str(user_id)]]
            @ self.room_factors[self.room_index[int(room_id)]]
        )

    def recommend_for_user(
        self,
        user_id: str,
        top_n: int = 10,
        exclude_seen: bool = True,
        candidate_room_ids: Iterable[int] | None = None,
    ) -> dict[str, object]:
        if top_n < 1:
            raise ValueError("top_n must be at least 1")
        user_id = str(user_id)
        if user_id not in self.user_index:
            return {"status": "cold_start", "user_id": user_id, "recommendations": []}
        allowed = self.visible_room_ids
        if candidate_room_ids is not None:
            allowed = allowed.intersection(map(int, candidate_room_ids))
        seen: set[int] = set()
        if exclude_seen:
            row = self.train_matrix.getrow(self.user_index[user_id])
            seen = {self.room_ids[index] for index in row.indices}
        candidates = [room_id for room_id in self.room_ids if room_id in allowed and room_id not in seen]
        scores = [self.score_user_room(user_id, room_id) for room_id in candidates]
        ranked = sorted(zip(candidates, scores), key=lambda pair: (-float(pair[1]), pair[0]))[:top_n]
        return {
            "status": "ok",
            "user_id": user_id,
            "recommendations": [
                {"PhongtroId": room_id, "CollaborativeScore": float(score)}
                for room_id, score in ranked
            ],
        }

    def save(self, path: Path | str) -> None:
        path = Path(path)
        path.parent.mkdir(parents=True, exist_ok=True)
        np.savez_compressed(
            path,
            user_factors=self.user_factors,
            room_factors=self.room_factors,
            user_ids=np.asarray(self.user_ids),
            room_ids=np.asarray(self.room_ids, dtype=np.int64),
            visible_room_ids=np.asarray(sorted(self.visible_room_ids), dtype=np.int64),
            train_data=self.train_matrix.data,
            train_indices=self.train_matrix.indices,
            train_indptr=self.train_matrix.indptr,
            train_shape=np.asarray(self.train_matrix.shape, dtype=np.int64),
            config_json=np.asarray(json.dumps(self.config, sort_keys=True)),
            metadata_json=np.asarray(json.dumps(self.metadata, sort_keys=True)),
        )

    @classmethod
    def load(cls, path: Path | str) -> "CollaborativeRecommender":
        with np.load(Path(path), allow_pickle=False) as archive:
            config = json.loads(str(archive["config_json"].item()))
            model = cls(
                factors=config["factors"], regularization=config["regularization"],
                iterations=config["iterations"], alpha=config["alpha"], seed=config["seed"],
            )
            model.user_ids = archive["user_ids"].astype(str).tolist()
            model.room_ids = archive["room_ids"].astype(int).tolist()
            model.visible_room_ids = set(archive["visible_room_ids"].astype(int).tolist())
            model.user_index = {value: index for index, value in enumerate(model.user_ids)}
            model.room_index = {value: index for index, value in enumerate(model.room_ids)}
            model.user_factors = archive["user_factors"].astype(np.float64)
            model.room_factors = archive["room_factors"].astype(np.float64)
            shape = tuple(archive["train_shape"].astype(int).tolist())
            model.train_matrix = sparse.csr_matrix(
                (archive["train_data"], archive["train_indices"], archive["train_indptr"]), shape=shape
            )
            model.metadata = json.loads(str(archive["metadata_json"].item()))
        return model
