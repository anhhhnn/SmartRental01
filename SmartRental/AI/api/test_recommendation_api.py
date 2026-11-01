from __future__ import annotations

import unittest

from fastapi.testclient import TestClient

from main import app


class RecommendationApiTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.client_context = TestClient(app)
        cls.client = cls.client_context.__enter__()
        service = app.state.recommendation_service
        if service is None:
            raise RuntimeError(app.state.recommendation_error)
        cls.known_user = next(iter(service.recommender.by_user))

    @classmethod
    def tearDownClass(cls) -> None:
        cls.client_context.__exit__(None, None, None)

    def test_known_user_response_schema(self) -> None:
        response = self.client.get(f"/recommend/{self.known_user}")
        self.assertEqual(200, response.status_code)
        payload = response.json()
        self.assertEqual(self.known_user, payload["userId"])
        self.assertTrue(payload["userKnown"])
        self.assertFalse(payload["coldStart"])
        self.assertEqual("content_based_optimized", payload["model"])
        self.assertLessEqual(len(payload["recommendations"]), 10)
        self.assertTrue(
            all(
                {"phongtroId", "score", "rank", "reasons"} == set(item)
                for item in payload["recommendations"]
            )
        )
        self.assertTrue(
            all(
                isinstance(item["reasons"], list) and len(item["reasons"]) <= 3
                for item in payload["recommendations"]
            )
        )
        self.assertTrue(any(item["reasons"] for item in payload["recommendations"]))

    def test_unknown_user_uses_fallback(self) -> None:
        response = self.client.get("/recommend/new-user")
        self.assertEqual(200, response.status_code)
        payload = response.json()
        self.assertFalse(payload["userKnown"])
        self.assertTrue(payload["coldStart"])
        self.assertEqual("popular_fallback", payload["model"])
        self.assertTrue(
            all(
                item["reasons"] == ["Phòng đang được nhiều người quan tâm"]
                for item in payload["recommendations"]
            )
        )

    def test_top_n_five(self) -> None:
        response = self.client.get(
            f"/recommend/{self.known_user}", params={"top_n": 5}
        )
        self.assertEqual(200, response.status_code)
        self.assertEqual(5, len(response.json()["recommendations"]))

    def test_top_n_over_limit_returns_422(self) -> None:
        response = self.client.get(
            f"/recommend/{self.known_user}", params={"top_n": 100}
        )
        self.assertEqual(422, response.status_code)

    def test_health_reports_both_services(self) -> None:
        response = self.client.get("/health")
        self.assertEqual(200, response.status_code)
        payload = response.json()
        self.assertEqual("ok", payload["status"])
        self.assertEqual("ready", payload["search"])
        self.assertEqual("ready", payload["recommendation"])
        self.assertGreater(payload["rooms"], 0)
        self.assertGreater(payload["userProfiles"], 0)

    def test_health_degrades_without_breaking_search(self) -> None:
        service = app.state.recommendation_service
        try:
            app.state.recommendation_service = None
            response = self.client.get("/health")
        finally:
            app.state.recommendation_service = service

        self.assertEqual(200, response.status_code)
        self.assertEqual(
            {
                "status": "degraded",
                "search": "ready",
                "recommendation": "unavailable",
                "rooms": 0,
                "userProfiles": 0,
            },
            response.json(),
        )


if __name__ == "__main__":
    unittest.main()
