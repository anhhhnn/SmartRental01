"""Run the official, reviewable AI Search demonstration query set."""

from __future__ import annotations

import json
import sys
from collections import Counter
from pathlib import Path
from typing import Any


if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")


AI_DIR = Path(__file__).resolve().parents[1]
API_DIR = AI_DIR / "api"
if str(API_DIR) not in sys.path:
    sys.path.insert(0, str(API_DIR))

from main import parse_search  # noqa: E402


QUERY_PATH = Path(__file__).with_name("search_demo_queries.json")


def mismatches(actual: dict[str, Any], expected: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    for key, expected_value in expected.items():
        if key == "tienNghiContains":
            missing = [value for value in expected_value if value not in actual["tienNghi"]]
            if missing:
                errors.append(f"tienNghi missing {missing}; actual={actual['tienNghi']}")
        elif actual.get(key) != expected_value:
            errors.append(f"{key}: expected={expected_value!r}, actual={actual.get(key)!r}")
    return errors


def run() -> tuple[list[dict[str, Any]], Counter[str]]:
    cases = json.loads(QUERY_PATH.read_text(encoding="utf-8"))
    results: list[dict[str, Any]] = []
    counts: Counter[str] = Counter()
    for case in cases:
        actual = parse_search(case["query"])
        if not case["supported"]:
            status = "unsupported"
            errors: list[str] = []
        else:
            errors = mismatches(actual, case["expected"])
            status = "fail" if errors else "pass"
        counts[status] += 1
        results.append({**case, "status": status, "errors": errors, "actual": actual})
    return results, counts


if __name__ == "__main__":
    demo_results, summary = run()
    print(
        f"AI Search demo: {summary['pass']} pass, {summary['fail']} fail, "
        f"{summary['unsupported']} documented unsupported"
    )
    for result in demo_results:
        marker = {"pass": "PASS", "fail": "FAIL", "unsupported": "UNSUPPORTED"}[
            result["status"]
        ]
        print(f"[{marker}] {result['id']}: {result['query']}")
        for error in result["errors"]:
            print(f"  - {error}")
        if result["status"] == "unsupported":
            print(f"  - {result['note']}")
    raise SystemExit(1 if summary["fail"] else 0)
