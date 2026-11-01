from __future__ import annotations

import unittest

from run_search_demo import run


class SearchDemoTests(unittest.TestCase):
    def test_all_declared_supported_queries_match_expectations(self) -> None:
        results, counts = run()
        failures = [result for result in results if result["status"] == "fail"]
        self.assertEqual([], failures)
        self.assertGreaterEqual(counts["pass"], 20)

    def test_unsupported_queries_are_explicitly_documented(self) -> None:
        results, counts = run()
        unsupported = [result for result in results if result["status"] == "unsupported"]
        self.assertEqual(3, counts["unsupported"])
        self.assertTrue(all(result.get("note") for result in unsupported))


if __name__ == "__main__":
    unittest.main()
