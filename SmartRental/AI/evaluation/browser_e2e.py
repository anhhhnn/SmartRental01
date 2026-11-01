"""Repeatable Chrome E2E checks for AI Search, recommendation, and responsive UI.

Selenium may be supplied through PYTHONPATH so the repository does not need a
browser-test dependency at runtime.
"""

from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path
from urllib.parse import quote

from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.support import expected_conditions as expected
from selenium.webdriver.support.ui import WebDriverWait


if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")

BASE_URL = "http://127.0.0.1:5038"
CHROME = Path(r"C:\Program Files\Google\Chrome\Application\chrome.exe")
DEMO_EMAIL = "nguoithue1@smartrental.local"
DEMO_PASSWORD = "SmartRental.Demo!2026#"


def create_driver(width: int = 1440, height: int = 1000) -> webdriver.Chrome:
    options = webdriver.ChromeOptions()
    options.binary_location = str(CHROME)
    options.add_argument("--headless=new")
    options.add_argument("--disable-gpu")
    options.add_argument("--no-first-run")
    options.add_argument("--no-default-browser-check")
    options.add_argument(f"--window-size={width},{height}")
    return webdriver.Chrome(options=options)


def login(driver: webdriver.Chrome) -> None:
    driver.get(f"{BASE_URL}/Identity/Account/Login?returnUrl=%2FPhongtro")
    driver.find_element(By.ID, "Input_Email").send_keys(DEMO_EMAIL)
    driver.find_element(By.ID, "Input_Password").send_keys(DEMO_PASSWORD)
    driver.find_element(By.CSS_SELECTOR, "button[type='submit']").click()
    WebDriverWait(driver, 15).until(
        expected.url_contains("/Phongtro")
    )


def run_search(driver: webdriver.Chrome, query: str, expected_text: str) -> dict[str, object]:
    driver.get(f"{BASE_URL}/Phongtro")
    box = driver.find_element(By.ID, "aiQuery")
    box.clear()
    box.send_keys(query)
    driver.find_element(By.CSS_SELECTOR, "[data-ai-submit]").click()
    WebDriverWait(driver, 20).until(expected.url_contains("aiQuery="))
    summaries = driver.find_elements(By.CSS_SELECTOR, ".ai-search-summary")
    alerts = driver.find_elements(By.CSS_SELECTOR, ".ai-search-alert")
    text = summaries[0].text if summaries else (alerts[0].text if alerts else "")
    if expected_text.casefold() not in text.casefold():
        raise AssertionError(
            f"Query {query!r}: expected {expected_text!r} in summary/alert {text!r}"
        )
    return {"query": query, "status": "pass", "evidence": expected_text}


def responsive_check(driver: webdriver.Chrome, width: int) -> dict[str, object]:
    driver.set_window_size(width, 1000)
    driver.get(f"{BASE_URL}/Phongtro")
    WebDriverWait(driver, 15).until(
        expected.presence_of_element_located((By.CSS_SELECTOR, ".ai-search-box"))
    )
    selectors = {
        "navbar": ".navbar-smart",
        "aiSearch": ".ai-search-box",
        "filters": ".horizontal-filter-card",
        "roomGrid": ".search-results .room-grid",
        "roomCard": ".search-results .room-card",
        "pagination": ".pagination",
    }
    present = {
        name: bool(driver.find_elements(By.CSS_SELECTOR, selector))
        for name, selector in selectors.items()
    }
    overflow = driver.execute_script(
        "return document.documentElement.scrollWidth > window.innerWidth + 1"
    )
    if not all(present.values()) or overflow:
        raise AssertionError(
            f"Responsive {width}px failed: present={present}, horizontalOverflow={overflow}"
        )
    return {"viewport": width, "status": "pass", "horizontalOverflow": False}


def run_normal() -> dict[str, object]:
    driver = create_driver()
    try:
        driver.get(f"{BASE_URL}/Phongtro")
        anonymous_recommendation_hidden = not driver.find_elements(
            By.CSS_SELECTOR, ".recommendation-section"
        )
        if not anonymous_recommendation_hidden:
            raise AssertionError("Recommendation must be hidden for anonymous users")

        searches = [
            run_search(driver, "tìm phòng dưới 2 triệu", "Giá tối đa"),
            run_search(driver, "phòng có wifi điều hòa", "Tiện nghi"),
            run_search(driver, "phòng ở Tân Thịnh", "Khu vực"),
            run_search(driver, "phòng gần ICTU trong 3km", "Bán kính"),
            run_search(
                driver,
                "tìm phòng từ 1 đến 2 triệu cho 2 người có wifi gần ICTU trong 3km",
                "Khoảng giá",
            ),
        ]

        login(driver)
        WebDriverWait(driver, 20).until(
            expected.presence_of_element_located((By.CSS_SELECTOR, ".recommendation-section"))
        )
        recommendation_heading = driver.find_element(
            By.ID, "recommendation-heading"
        ).text
        reason_count = len(
            driver.find_elements(By.CSS_SELECTOR, ".room-card__recommendation-reason")
        )
        score_leaked = "cosine" in driver.page_source.casefold()
        if recommendation_heading != "Gợi ý cho bạn" or reason_count == 0 or score_leaked:
            raise AssertionError(
                f"Recommendation UI failed: heading={recommendation_heading!r}, "
                f"reasons={reason_count}, scoreLeaked={score_leaked}"
            )

        driver.get(f"{BASE_URL}/Phongtro?aiQuery={quote('phòng có wifi')}")
        recommendation_hidden_during_search = not driver.find_elements(
            By.CSS_SELECTOR, ".recommendation-section"
        )
        if not recommendation_hidden_during_search:
            raise AssertionError("Recommendation must be hidden during AI search")

        responsive = [responsive_check(driver, width) for width in (1440, 1024, 768, 390)]
        return {
            "anonymousRecommendationHidden": anonymous_recommendation_hidden,
            "searches": searches,
            "knownUser": {
                "heading": recommendation_heading,
                "reasonCards": reason_count,
                "cosineScoreVisible": score_leaked,
                "hiddenDuringSearch": recommendation_hidden_during_search,
            },
            "responsive": responsive,
        }
    finally:
        driver.quit()


def run_failure() -> dict[str, object]:
    driver = create_driver()
    try:
        login(driver)
        driver.get(f"{BASE_URL}/Phongtro")
        page_loaded = driver.find_element(By.CSS_SELECTOR, ".search-results").is_displayed()
        recommendation_hidden = not driver.find_elements(
            By.CSS_SELECTOR, ".recommendation-section"
        )
        driver.get(f"{BASE_URL}/Phongtro?aiQuery={quote('phòng dưới 2 triệu')}")
        search_fallback_visible = bool(
            driver.find_elements(By.CSS_SELECTOR, ".ai-search-alert")
            or driver.find_elements(By.CSS_SELECTOR, ".search-results")
        )
        if not (page_loaded and recommendation_hidden and search_fallback_visible):
            raise AssertionError(
                "Failure mode did not preserve page/fallback or hide recommendation"
            )
        return {
            "webLoaded": page_loaded,
            "recommendationHidden": recommendation_hidden,
            "searchFallbackPageVisible": search_fallback_visible,
        }
    finally:
        driver.quit()


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--failure", action="store_true")
    args = parser.parse_args()
    started = time.perf_counter()
    result = run_failure() if args.failure else run_normal()
    result["elapsedSeconds"] = round(time.perf_counter() - started, 3)
    print(json.dumps(result, ensure_ascii=False, indent=2))
