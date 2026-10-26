import os
import re
import unicodedata

import joblib
from fastapi import FastAPI
from pydantic import BaseModel


app = FastAPI(title="SmartRental AI Search API")

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
MODEL_DIR = os.path.join(BASE_DIR, "..", "models")

area_model = joblib.load(os.path.join(MODEL_DIR, "area_model.pkl"))
amenity_model = joblib.load(os.path.join(MODEL_DIR, "amenity_model.pkl"))
amenity_mlb = joblib.load(os.path.join(MODEL_DIR, "amenity_mlb.pkl"))

AREA_CONFIDENCE_THRESHOLD = 0.55
AMENITY_CONFIDENCE_THRESHOLD = 0.35

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
}


class SearchRequest(BaseModel):
    text: str


def normalize_text(value: str) -> str:
    decomposed = unicodedata.normalize("NFD", value.strip().lower().replace("đ", "d"))
    without_accents = "".join(
        character for character in decomposed
        if unicodedata.category(character) != "Mn"
    )
    return " ".join(without_accents.split())


def extract_price(text: str):
    normalized = text.lower()
    patterns = (
        r"(?:dưới|không quá|tối đa|khoảng)\s*(\d+(?:[.,]\d+)?)\s*(?:triệu|tr)",
        r"(\d+(?:[.,]\d+)?)\s*(?:triệu|tr)",
    )

    for pattern in patterns:
        match = re.search(pattern, normalized)
        if match:
            value = float(match.group(1).replace(",", "."))
            return int(value * 1_000_000)

    return None


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

    return {
        "maxPrice": extract_price(text),
        "soNguoi": extract_people(text),
        "khuVuc": predict_area(text, near_place is not None),
        "tienNghi": predict_amenities(text),
        "nearPlace": near_place,
        "maxDistanceKm": extract_distance_km(text, near_place),
    }


@app.get("/")
def home():
    return {"message": "SmartRental AI API đang hoạt động"}


@app.post("/parse-search")
def parse_search_api(request: SearchRequest):
    return parse_search(request.text)
