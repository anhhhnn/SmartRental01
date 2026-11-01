from __future__ import annotations

import json
import math
import tempfile
import unittest
from pathlib import Path

from recommendation_service import RecommendationService


class RecommendationServiceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.temp_directory = tempfile.TemporaryDirectory()
        directory = Path(cls.temp_directory.name)
        cls.rooms_path = directory / "rooms.csv"
        cls.interactions_path = directory / "interactions.csv"
        cls.config_path = directory / "config.json"
        cls.rooms_path.write_text(
            "PhongtroId,KhuVuc,Gia,DienTich,SoNguoiToiDa,TienNghi,IsVisible\n"
            "1,A,1000000,20,2,Wifi,1\n"
            "2,A,1200000,22,2,Wifi|Bep,1\n"
            "3,B,2000000,30,3,Bep,0\n"
            "4,B,1800000,28,3,Bep,1\n"
            "5,C,800000,16,1,,1\n",
            encoding="utf-8",
        )
        cls.interactions_path.write_text(
            "UserId,PhongtroId,Views,Favorite,Booking,Rating,Score\n"
            "known-user,1,5,1,0,4,12\n"
            "known-user,3,2,0,0,3,5\n"
            "other-user,2,8,1,1,5,20\n",
            encoding="utf-8",
        )
        cls.config_path.write_text(
            json.dumps(
                {
                    "name": "B_non_negative__feature_2",
                    "view_transform": "log1p",
                    "view_weight": 1.0,
                    "favorite_weight": 3.0,
                    "booking_weight": 5.0,
                    "rating_mapping": {
                        "0": 0.0, "1": 0.0, "2": 0.0,
                        "3": 0.5, "4": 2.0, "5": 4.0,
                    },
                    "feature_weights": {
                        "khuVuc": 1.3, "tienNghi": 1.7, "gia": 1.4,
                        "dienTich": 0.8, "soNguoi": 0.8,
                    },
                }
            ),
            encoding="utf-8",
        )
        cls.service = RecommendationService(
            cls.rooms_path, cls.interactions_path, cls.config_path
        )

    @classmethod
    def tearDownClass(cls) -> None:
        cls.temp_directory.cleanup()

    def test_known_user_has_personalized_recommendations(self) -> None:
        response = self.service.recommend_for_user("known-user")
        self.assertTrue(response["userKnown"])
        self.assertFalse(response["coldStart"])
        self.assertEqual("content_based_optimized", response["model"])
        self.assertGreater(len(response["recommendations"]), 0)
        self.assertTrue(
            all(isinstance(item["reasons"], list) and len(item["reasons"]) <= 3
                for item in response["recommendations"])
        )
        self.assertTrue(any(item["reasons"] for item in response["recommendations"]))

    def test_unknown_user_uses_cold_start(self) -> None:
        response = self.service.recommend_for_user("unknown-user", top_n=2)
        self.assertFalse(response["userKnown"])
        self.assertTrue(response["coldStart"])
        self.assertEqual("popular_fallback", response["model"])
        self.assertEqual(2, len(response["recommendations"]))
        self.assertTrue(
            all(
                item["reasons"] == ["Phòng đang được nhiều người quan tâm"]
                for item in response["recommendations"]
            )
        )

    def test_reasons_come_from_profile_and_room_features(self) -> None:
        response = self.service.recommend_for_user(
            "known-user", top_n=5, exclude_seen=False
        )
        room_two = next(
            item for item in response["recommendations"] if item["phongtroId"] == 2
        )
        self.assertIn("Cùng khu vực bạn thường xem", room_two["reasons"])
        self.assertTrue(any("Wifi" in reason for reason in room_two["reasons"]))

    def test_only_visible_rooms_are_returned(self) -> None:
        visible_ids = {1, 2, 4, 5}
        for user_id in ("known-user", "unknown-user"):
            response = self.service.recommend_for_user(
                user_id, top_n=50, exclude_seen=False
            )
            ids = {item["phongtroId"] for item in response["recommendations"]}
            self.assertTrue(ids.issubset(visible_ids))
            self.assertNotIn(3, ids)

    def test_top_n_is_respected(self) -> None:
        response = self.service.recommend_for_user(
            "known-user", top_n=2, exclude_seen=False
        )
        self.assertEqual(2, len(response["recommendations"]))

    def test_exclude_seen_removes_seen_room(self) -> None:
        excluded = self.service.recommend_for_user(
            "known-user", top_n=50, exclude_seen=True
        )
        included = self.service.recommend_for_user(
            "known-user", top_n=50, exclude_seen=False
        )
        self.assertNotIn(1, {item["phongtroId"] for item in excluded["recommendations"]})
        self.assertIn(1, {item["phongtroId"] for item in included["recommendations"]})

    def test_scores_are_finite(self) -> None:
        for user_id in ("known-user", "unknown-user"):
            response = self.service.recommend_for_user(user_id, top_n=4)
            self.assertTrue(
                all(math.isfinite(item["score"]) for item in response["recommendations"])
            )

    def test_results_are_deterministic(self) -> None:
        first = self.service.recommend_for_user("known-user", top_n=3)
        second = self.service.recommend_for_user("known-user", top_n=3)
        self.assertEqual(first, second)

    def test_optimized_config_and_profiles_load(self) -> None:
        self.assertEqual("B_non_negative__feature_2", self.service.config_name)
        self.assertEqual(2, self.service.user_profile_count)
        summary = self.service.get_user_profile_summary("known-user")
        self.assertFalse(summary["coldStart"])
        self.assertEqual(2, summary["interactionCount"])

    def test_invalid_top_n_is_rejected(self) -> None:
        for top_n in (0, -1, 51):
            with self.subTest(top_n=top_n), self.assertRaises(ValueError):
                self.service.recommend_for_user("known-user", top_n=top_n)


if __name__ == "__main__":
    unittest.main()
