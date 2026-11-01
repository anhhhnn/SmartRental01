import os
import json
import logging
import re
import sys
import time
import unicodedata
from contextlib import asynccontextmanager

import joblib
from fastapi import FastAPI, HTTPException, Query
from pydantic import BaseModel


BASE_DIR = os.path.dirname(os.path.abspath(__file__))
AI_DIR = os.path.abspath(os.path.join(BASE_DIR, ".."))
MODEL_DIR = os.path.join(BASE_DIR, "..", "models")
if AI_DIR not in sys.path:
    sys.path.insert(0, AI_DIR)

from recommendation.recommendation_service import RecommendationService


logger = logging.getLogger("uvicorn.error")


@asynccontextmanager
async def lifespan(application: FastAPI):
    started_at = time.perf_counter()
    application.state.recommendation_service = None
    application.state.recommendation_error = None
    try:
        service = RecommendationService()
        application.state.recommendation_service = service
        logger.info(
            "Recommendation ready: rooms=%d visible=%d profiles=%d config=%s",
            service.room_count,
            service.visible_room_count,
            service.user_profile_count,
            service.config_name,
        )
    except Exception as exc:  # Search API must remain available on recommendation failure.
        application.state.recommendation_error = str(exc)
        logger.exception("Recommendation service failed to load")
    application.state.recommendation_startup_seconds = time.perf_counter() - started_at
    yield


app = FastAPI(title="SmartRental AI Search API", lifespan=lifespan)

area_model = joblib.load(os.path.join(MODEL_DIR, "area_model.pkl"))
amenity_model = joblib.load(os.path.join(MODEL_DIR, "amenity_model.pkl"))
amenity_mlb = joblib.load(os.path.join(MODEL_DIR, "amenity_mlb.pkl"))

AREA_CONFIDENCE_THRESHOLD = 0.55
with open(os.path.join(MODEL_DIR, "model_metadata.json"), encoding="utf-8") as metadata_file:
    model_metadata = json.load(metadata_file)
AMENITY_CONFIDENCE_THRESHOLD = float(model_metadata["amenity_threshold"])

LANDMARK_ALIASES = {
    "Đại học Công nghệ Thông tin và Truyền thông": (
        "đại học công nghệ thông tin và truyền thông",
        "đại học công nghệ thông tin",
        "đại học cntt",
        "cntt thái nguyên",
        "ictu",
    ),
    "Đại học Nông Lâm": (
        "đại học nông lâm",
        "trường nông lâm",
        "nông lâm",
        "nong lam",
        "tuaf",
    ),
    "Đại học Khoa học": (
        "đại học khoa học",
        "trường khoa học",
        "dh khoa hoc",
        "khoa học",
        "khoa hoc",
        "tnus",
    ),
    "Đại học Sư phạm": (
        "đại học sư phạm",
        "trường sư phạm",
        "dh su pham",
        "sư phạm",
        "su pham",
        "tnue",
    ),
    "Trường Ngoại ngữ": (
        "trường ngoại ngữ - đại học thái nguyên",
        "trường ngoại ngữ thái nguyên",
        "đại học ngoại ngữ",
        "trường ngoại ngữ",
        "ngoại ngữ",
        "sfl tnu",
        "sfl",
    ),
}


class SearchRequest(BaseModel):
    text: str


class RecommendationItem(BaseModel):
    phongtroId: int
    score: float
    rank: int
    reasons: list[str]


class RecommendationResponse(BaseModel):
    userId: str
    userKnown: bool
    coldStart: bool
    model: str
    recommendations: list[RecommendationItem]


def normalize_text(value: str) -> str:
    decomposed = unicodedata.normalize("NFD", value.strip().lower().replace("đ", "d"))
    without_accents = "".join(
        character for character in decomposed
        if unicodedata.category(character) != "Mn"
    )
    return " ".join(without_accents.split())


def extract_price_bounds(text: str) -> tuple[int | None, int | None]:
    normalized = normalize_text(text)
    amount = r"(\d+(?:[.,]\d+)?)"
    unit = r"(?:trieu|tr)"

    range_match = re.search(
        rf"\b(?:khoang\s+)?tu\s+{amount}\s*{unit}?\s*(?:den|-)\s*{amount}\s*{unit}\b",
        normalized,
    )
    if range_match:
        minimum = int(float(range_match.group(1).replace(",", ".")) * 1_000_000)
        maximum = int(float(range_match.group(2).replace(",", ".")) * 1_000_000)
        return minimum, maximum

    minimum = None
    minimum_match = re.search(
        rf"\b(?:tu|tren|it nhat|toi thieu)\s*{amount}\s*{unit}\b",
        normalized,
    )
    if minimum_match:
        minimum = int(float(minimum_match.group(1).replace(",", ".")) * 1_000_000)

    maximum = None
    maximum_match = re.search(
        rf"\b(?:duoi|khong qua|toi da|cao nhat|khoang)\s*{amount}\s*{unit}\b",
        normalized,
    )
    if maximum_match:
        maximum = int(float(maximum_match.group(1).replace(",", ".")) * 1_000_000)
    elif minimum is None:
        # A standalone amount such as "2 triệu" keeps the existing max-price meaning.
        standalone_match = re.search(rf"\b{amount}\s*{unit}\b", normalized)
        if standalone_match:
            maximum = int(float(standalone_match.group(1).replace(",", ".")) * 1_000_000)

    return minimum, maximum


def extract_people(text: str):
    match = re.search(r"(\d+)\s*người", text.lower())
    return int(match.group(1)) if match else None


def extract_landmark(text: str):
    normalized_text = normalize_text(text)
    matches = []

    for canonical_name, aliases in LANDMARK_ALIASES.items():
        for alias in aliases:
            normalized_alias = normalize_text(alias)
            if re.search(rf"(?<!\w){re.escape(normalized_alias)}(?!\w)", normalized_text):
                matches.append((len(normalized_alias), canonical_name))

    return max(matches, default=(0, None))[1]


def extract_distance_km(text: str, near_place):
    if near_place is None:
        return None

    normalized = normalize_text(text)
    match = re.search(r"(\d+(?:[.,]\d+)?)\s*km\b", normalized)
    if not match:
        return None

    value = float(match.group(1).replace(",", "."))
    return value if value >= 0 else None


def predict_area(text: str, has_landmark: bool):
    # A landmark name must never be translated into a ward by the area model.
    if has_landmark:
        return None

    probabilities = area_model.predict_proba([text])[0]
    best_index = int(probabilities.argmax())
    area = area_model.classes_[best_index]
    confidence = float(probabilities[best_index])

    if area == "KHONG_XAC_DINH" or confidence < AREA_CONFIDENCE_THRESHOLD:
        return None

    return area


def predict_amenities(text: str):
    probabilities = amenity_model.predict_proba([text])
    prediction = (probabilities >= AMENITY_CONFIDENCE_THRESHOLD).astype(int)
    return list(amenity_mlb.inverse_transform(prediction)[0])


def parse_search(text: str):
    near_place = extract_landmark(text)
    min_price, max_price = extract_price_bounds(text)

    return {
        "minPrice": min_price,
        "maxPrice": max_price,
        "soNguoi": extract_people(text),
        "khuVuc": predict_area(text, near_place is not None),
        "tienNghi": predict_amenities(text),
        "nearPlace": near_place,
        "maxDistanceKm": extract_distance_km(text, near_place),
    }


@app.get("/")
def home():
    return {"message": "SmartRental AI API đang hoạt động"}


@app.get("/health")
def health():
    service = getattr(app.state, "recommendation_service", None)
    return {
        "status": "ok" if service is not None else "degraded",
        "search": "ready",
        "recommendation": "ready" if service is not None else "unavailable",
        "rooms": service.room_count if service is not None else 0,
        "userProfiles": service.user_profile_count if service is not None else 0,
    }


@app.post("/parse-search")
def parse_search_api(request: SearchRequest):
    return parse_search(request.text)


@app.get("/recommend/{user_id}", response_model=RecommendationResponse)
def recommend(
    user_id: str,
    top_n: int = Query(default=10, ge=1, le=50),
    exclude_seen: bool = Query(default=True),
):
    service = getattr(app.state, "recommendation_service", None)
    if service is None:
        raise HTTPException(status_code=503, detail="Recommendation service is unavailable")
    masked_user = f"***{user_id[-4:]}" if user_id else "<empty>"
    logger.info(
        "Recommendation request user=%s top_n=%d exclude_seen=%s",
        masked_user,
        top_n,
        exclude_seen,
    )
    return service.recommend_for_user(user_id, top_n, exclude_seen)
