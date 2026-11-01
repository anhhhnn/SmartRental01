from __future__ import annotations

import unittest

from recommendation_demo import build_demo


class RecommendationDemoTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.scenarios = build_demo()

    def test_three_user_groups_are_present(self) -> None:
        self.assertEqual(3, len(self.scenarios))
        self.assertFalse(self.scenarios[0]["coldStart"])
        self.assertFalse(self.scenarios[1]["coldStart"])
        self.assertTrue(self.scenarios[2]["coldStart"])

    def test_report_masks_ids_and_contains_top_five_with_reasons(self) -> None:
        for scenario in self.scenarios:
            self.assertIn("...", scenario["userIdMasked"])
            self.assertEqual(5, len(scenario["top5"]))
            self.assertTrue(all("reasons" in item for item in scenario["top5"]))

    def test_cold_start_reasons_are_neutral(self) -> None:
        self.assertTrue(
            all(
                item["reasons"] == ["Phòng đang được nhiều người quan tâm"]
                for item in self.scenarios[2]["top5"]
            )
        )


if __name__ == "__main__":
    unittest.main()
