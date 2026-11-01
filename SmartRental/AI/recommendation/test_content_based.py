from __future__ import annotations

import json
import math
import tempfile
import unittest
from pathlib import Path

import numpy as np

from content_based import (
    BASELINE_CONFIG,
    ContentBasedRecommender,
    Interaction,
    RoomRecord,
    interaction_weight,
    load_config,
)


class ContentBasedRecommenderTests(unittest.TestCase):
    def setUp(self) -> None:
        self.rooms = [
            RoomRecord(1, "A", 1_000_000, 20, 2, ("Wifi",), True),
            RoomRecord(2, "A", 1_500_000, 25, 2, ("Bếp", "Wifi"), True),
            RoomRecord(3, "B", 2_000_000, 30, 3, ("Bếp",), False),
            RoomRecord(4, "C", 900_000, 18, 1, (), True),
        ]
        self.interactions = [Interaction("user", 1, 1), Interaction("user", 2, 3)]
        self.model = ContentBasedRecommender(self.rooms, self.interactions, BASELINE_CONFIG)

    def test_vector_shape_and_values_are_valid(self) -> None:
        self.assertEqual((4, 8), self.model.room_vectors.shape)
        self.assertEqual(8, len(self.model.feature_names))
        self.assertTrue(np.isfinite(self.model.room_vectors.data).all())

    def test_profile_is_score_weighted_mean(self) -> None:
        expected = (self.model.room_vectors[0] + self.model.room_vectors[1] * 3) / 4
        actual = self.model.user_profile("user")
        self.assertIsNotNone(actual)
        np.testing.assert_allclose(actual.toarray(), expected.toarray())
        self.assertTrue(np.isfinite(actual.data).all())

    def test_hidden_and_seen_rooms_are_filtered(self) -> None:
        excluded = self.model.recommend_for_user("user", top_n=10, exclude_seen=True)
        included = self.model.recommend_for_user("user", top_n=10, exclude_seen=False)
        self.assertEqual([4], [row["PhongtroId"] for row in excluded])
        self.assertEqual({1, 2, 4}, {row["PhongtroId"] for row in included})
        self.assertNotIn(3, {row["PhongtroId"] for row in included})

    def test_top_n_is_respected_and_scores_are_finite(self) -> None:
        result = self.model.recommend_for_user("user", top_n=2, exclude_seen=False)
        self.assertEqual(2, len(result))
        self.assertTrue(all(np.isfinite(row["ContentScore"]) for row in result))

    def test_cold_start_uses_popularity_without_content_score(self) -> None:
        result = self.model.recommend_for_user("new-user", top_n=1)
        self.assertEqual(2, result[0]["PhongtroId"])
        self.assertIsNone(result[0]["ContentScore"])

    def test_config_loads_and_validates_expected_values(self) -> None:
        config = dict(BASELINE_CONFIG)
        config["favorite_weight"] = 7.0
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "config.json"
            path.write_text(json.dumps(config), encoding="utf-8")
            loaded = load_config(path)
        self.assertEqual(7.0, loaded["favorite_weight"])
        self.assertEqual("identity", loaded["view_transform"])

    def test_rating_mapping_is_applied(self) -> None:
        config = dict(BASELINE_CONFIG)
        config["rating_mapping"] = {"0": 0, "1": -2, "2": -1, "3": 0, "4": 2, "5": 4}
        low = Interaction("user", 1, 1, rating=1)
        high = Interaction("user", 1, 1, rating=5)
        self.assertEqual(-2.0, interaction_weight(low, config))
        self.assertEqual(4.0, interaction_weight(high, config))

    def test_log1p_view_transform_is_applied(self) -> None:
        config = dict(BASELINE_CONFIG)
        config["view_transform"] = "log1p"
        item = Interaction("user", 1, 10, views=10)
        self.assertAlmostEqual(math.log1p(10), interaction_weight(item, config))

    def test_signed_weights_produce_a_finite_profile(self) -> None:
        config = dict(BASELINE_CONFIG)
        config["name"] = "signed_test"
        config["rating_mapping"] = {"0": 0, "1": -2, "2": -1, "3": 0, "4": 2, "5": 4}
        interactions = [
            Interaction("signed", 1, 1, rating=1),
            Interaction("signed", 2, 1, rating=5),
        ]
        model = ContentBasedRecommender(self.rooms, interactions, config)
        profile = model.user_profile("signed")
        self.assertIsNotNone(profile)
        self.assertTrue(np.isfinite(profile.data).all())

    def test_default_model_loads_optimized_config(self) -> None:
        model = ContentBasedRecommender(self.rooms, self.interactions)
        self.assertEqual("log1p", model.config["view_transform"])
        self.assertEqual("B_non_negative", model.config["interaction_config"])


if __name__ == "__main__":
    unittest.main()
