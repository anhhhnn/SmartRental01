from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

import numpy as np

AI_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_ROOT))
sys.path.insert(0, str(AI_ROOT / "evaluation"))

from recommendation.collaborative import CollaborativeRecommender  # noqa: E402
from recommendation.content_based import Interaction, RoomRecord  # noqa: E402
from evaluate_collaborative import split_by_user  # noqa: E402


class CollaborativeRecommenderTests(unittest.TestCase):
    def setUp(self) -> None:
        self.rooms = [
            RoomRecord(10, "A", 1, 1, 1, (), True),
            RoomRecord(20, "A", 1, 1, 1, (), True),
            RoomRecord(30, "B", 1, 1, 1, (), False),
            RoomRecord(40, "B", 1, 1, 1, (), True),
        ]
        self.interactions = [
            Interaction("u2", 10, 3), Interaction("u2", 20, 2),
            Interaction("u1", 10, 4), Interaction("u1", 30, 1),
        ]
        self.model = CollaborativeRecommender(
            factors=3, regularization=0.1, iterations=3, alpha=10, seed=7
        ).fit(self.interactions, self.rooms)

    def test_mappings_are_sorted_and_do_not_use_raw_room_id_as_index(self) -> None:
        self.assertEqual(["u1", "u2"], self.model.user_ids)
        self.assertEqual([10, 20, 30, 40], self.model.room_ids)
        self.assertEqual(1, self.model.room_index[20])

    def test_factor_shapes_and_values_are_valid(self) -> None:
        self.assertEqual((2, 3), self.model.user_factors.shape)
        self.assertEqual((4, 3), self.model.room_factors.shape)
        self.assertTrue(np.isfinite(self.model.user_factors).all())
        self.assertTrue(np.isfinite(self.model.room_factors).all())

    def test_known_user_recommends_and_top_n_is_respected(self) -> None:
        result = self.model.recommend_for_user("u1", top_n=1)
        self.assertEqual("ok", result["status"])
        self.assertEqual(1, len(result["recommendations"]))
        self.assertTrue(np.isfinite(result["recommendations"][0]["CollaborativeScore"]))

    def test_unknown_user_returns_explicit_cold_start(self) -> None:
        result = self.model.recommend_for_user("missing")
        self.assertEqual("cold_start", result["status"])
        self.assertEqual([], result["recommendations"])

    def test_exclude_seen_filters_training_items(self) -> None:
        ids = {row["PhongtroId"] for row in self.model.recommend_for_user("u1", 10, True)["recommendations"]}
        self.assertFalse(ids & {10, 30})

    def test_hidden_and_external_candidates_are_filtered(self) -> None:
        result = self.model.recommend_for_user("u2", 10, False, candidate_room_ids={20, 30, 40, 999})
        self.assertEqual({20, 40}, {row["PhongtroId"] for row in result["recommendations"]})

    def test_save_load_preserves_recommendations_and_scores(self) -> None:
        before = self.model.recommend_for_user("u2", 3, False)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "model.npz"
            self.model.save(path)
            loaded = CollaborativeRecommender.load(path)
            after = loaded.recommend_for_user("u2", 3, False)
        self.assertEqual([x["PhongtroId"] for x in before["recommendations"]], [x["PhongtroId"] for x in after["recommendations"]])
        np.testing.assert_allclose(
            [x["CollaborativeScore"] for x in before["recommendations"]],
            [x["CollaborativeScore"] for x in after["recommendations"]],
        )

    def test_split_has_no_test_pair_in_training_matrix(self) -> None:
        interactions = [Interaction("u", room_id, 1) for room_id in (10, 20, 30, 40, 50)]
        rooms = self.rooms + [RoomRecord(50, "C", 1, 1, 1, (), True)]
        train, test, audit = split_by_user(interactions, {room.room_id: room for room in rooms}, 123)
        train_pairs = {(item.user_id, item.room_id) for item in train}
        fitted = CollaborativeRecommender(2, 0.1, 1, 10, 7).fit(train, rooms)
        self.assertEqual(0, audit["leaked_test_pairs"])
        for user_id, ids in test.items():
            for room_id in ids:
                self.assertNotIn((user_id, room_id), train_pairs)
                self.assertEqual(
                    0.0,
                    fitted.train_matrix[fitted.user_index[user_id], fitted.room_index[room_id]],
                )

    def test_training_is_deterministic_for_same_seed(self) -> None:
        other = CollaborativeRecommender(3, 0.1, 3, 10, 7).fit(self.interactions, self.rooms)
        np.testing.assert_allclose(self.model.user_factors, other.user_factors)
        np.testing.assert_allclose(self.model.room_factors, other.room_factors)

    def test_score_unknown_entity_is_none(self) -> None:
        self.assertIsNone(self.model.score_user_room("missing", 10))
        self.assertIsNone(self.model.score_user_room("u1", 999))


if __name__ == "__main__":
    unittest.main()
