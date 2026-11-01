import csv
import itertools
import json
import random
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path

import joblib
import sklearn
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.metrics import (
    accuracy_score,
    classification_report,
    f1_score,
    precision_score,
    recall_score,
)
from sklearn.model_selection import train_test_split
from sklearn.multiclass import OneVsRestClassifier
from sklearn.pipeline import Pipeline
from sklearn.linear_model import LogisticRegression
from sklearn.preprocessing import MultiLabelBinarizer


ROOT = Path(__file__).resolve().parents[2]
DATA_DIR = ROOT / "AI" / "DATA"
MODEL_DIR = ROOT / "AI" / "models"
OUTPUT_DIR = ROOT / "AI" / "evaluation"
ROOMS_PATH = DATA_DIR / "rooms.csv"
QUERY_DATASET_PATH = DATA_DIR / "amenity_search_queries.csv"
METRICS_PATH = OUTPUT_DIR / "search_model_metrics.json"
METADATA_PATH = MODEL_DIR / "model_metadata.json"

RANDOM_SEED = 20261027
AREA_CONFIDENCE_THRESHOLD = 0.55
THRESHOLD_CANDIDATES = (0.25, 0.30, 0.35, 0.40, 0.45, 0.50)

# Canonical names were read from dbo.TienNghis in the configured SmartRentalDB.
AMENITIES = (
    "Wifi",
    "Điều hòa",
    "Nóng lạnh",
    "Chỗ để xe",
    "Máy giặt",
    "Tủ lạnh",
    "Ban công",
    "WC riêng",
    "Bếp",
    "Giường",
    "Tủ quần áo",
    "Khép kín",
)

AMENITY_PHRASES = {
    "Wifi": ("wifi", "wi-fi", "mạng wifi", "internet không dây", "internet khong day"),
    "Điều hòa": ("điều hòa", "máy lạnh", "dieu hoa", "may lanh"),
    "Nóng lạnh": ("nóng lạnh", "bình nóng lạnh", "nong lanh", "binh nong lanh"),
    "Chỗ để xe": ("chỗ để xe", "nơi để xe", "cho de xe", "noi de xe"),
    "Máy giặt": ("máy giặt", "máy giặt riêng", "may giat"),
    "Tủ lạnh": ("tủ lạnh", "tu lanh"),
    "Ban công": ("ban công", "ban cong"),
    "WC riêng": ("WC riêng", "nhà vệ sinh riêng", "wc rieng", "nha ve sinh rieng"),
    "Bếp": ("bếp", "khu bếp", "bep"),
    "Giường": ("giường", "giường ngủ", "giuong", "giuong ngu"),
    "Tủ quần áo": ("tủ quần áo", "tủ đồ", "tu quan ao", "tu do"),
    "Khép kín": ("khép kín", "phòng khép kín", "khep kin", "phong khep kin"),
}

AMENITY_QUERY_FORMS = (
    "Tìm phòng có {items}",
    "Cần phòng trọ có {items}",
    "Ưu tiên chỗ ở với {items}",
    "Tìm phòng trọ gồm {items}",
    "Phòng cần có {items}",
    "Mong muốn phòng được trang bị {items}",
)

NEUTRAL_QUERIES = (
    "Tìm phòng trọ giá hợp lý",
    "Cần tìm phòng ở Thái Nguyên",
    "Tìm chỗ ở phù hợp cho sinh viên",
    "Có phòng nào còn trống không",
    "Tìm phòng gần trường đại học",
    "Cần phòng yên tĩnh và an ninh",
    "Tìm phòng dưới hai triệu",
    "Phòng cho hai người ở",
    "Tìm phòng diện tích rộng",
    "Cần phòng gần trung tâm thành phố",
    "Tìm nhà trọ mới đăng",
    "Có phòng nào phù hợp ngân sách",
    "Tìm phòng ở khu Tân Thịnh",
    "Cần chỗ ở thuận tiện đi học",
    "Tìm phòng trọ sạch sẽ",
    "Phòng còn chỗ cho người thuê",
    "Tìm phòng theo khu vực",
    "Cần phòng có giá vừa phải",
    "Tìm chỗ ở gần bến xe",
    "Phòng trọ dành cho người đi làm",
    "Tìm phòng diện tích khoảng hai mươi mét vuông",
    "Có phòng nào mới đăng gần đây",
    "Tìm nơi ở có vị trí thuận tiện",
    "Cần thuê phòng tại Thái Nguyên",
)


def normalize_label(value: str) -> str:
    return " ".join(value.strip().lower().split())


def split_group_ids(group_ids: list[str], seed: int) -> dict[str, str]:
    shuffled = group_ids.copy()
    random.Random(seed).shuffle(shuffled)
    train_count = max(1, int(len(shuffled) * 0.70))
    validation_count = max(1, int(len(shuffled) * 0.15))
    if train_count + validation_count >= len(shuffled):
        train_count = len(shuffled) - 2
        validation_count = 1

    return {
        **{group_id: "train" for group_id in shuffled[:train_count]},
        **{
            group_id: "validation"
            for group_id in shuffled[train_count : train_count + validation_count]
        },
        **{group_id: "test" for group_id in shuffled[train_count + validation_count :]},
    }


def format_items(items: list[str]) -> str:
    if len(items) == 1:
        return items[0]
    return ", ".join(items[:-1]) + " và " + items[-1]


def build_amenity_dataset() -> list[dict[str, object]]:
    combinations = [
        combination
        for size in range(1, 4)
        for combination in itertools.combinations(AMENITIES, size)
    ]
    split_by_group: dict[str, str] = {}
    for size in range(1, 4):
        group_ids = [
            f"{size}:{index}"
            for index, combination in enumerate(combinations)
            if len(combination) == size
        ]
        split_by_group.update(split_group_ids(group_ids, RANDOM_SEED + size))
    split_by_group.update(
        split_group_ids(
            [f"neutral:{index}" for index in range(len(NEUTRAL_QUERIES))],
            RANDOM_SEED,
        )
    )

    rows: list[dict[str, object]] = []
    for index, combination in enumerate(combinations):
        group_id = f"{len(combination)}:{index}"
        phrases = [
            AMENITY_PHRASES[name][(index + phrase_index) % len(AMENITY_PHRASES[name])]
            for phrase_index, name in enumerate(combination)
        ]
        for form_index, query_form in enumerate(AMENITY_QUERY_FORMS):
            rotated = phrases[form_index % len(phrases) :] + phrases[: form_index % len(phrases)]
            rows.append(
                {
                    "query": query_form.format(items=format_items(rotated)),
                    "amenities": list(combination),
                    "split": split_by_group[group_id],
                    "group_id": group_id,
                    "amenity_count": len(combination),
                }
            )

    for index, query in enumerate(NEUTRAL_QUERIES):
        group_id = f"neutral:{index}"
        rows.append(
            {
                "query": query,
                "amenities": [],
                "split": split_by_group[group_id],
                "group_id": group_id,
                "amenity_count": 0,
            }
        )

    return rows


def save_dataset(rows: list[dict[str, object]]) -> None:
    DATA_DIR.mkdir(parents=True, exist_ok=True)
    with QUERY_DATASET_PATH.open("w", encoding="utf-8", newline="") as dataset_file:
        writer = csv.DictWriter(
            dataset_file,
            fieldnames=("query", "amenities_json", "split", "group_id", "amenity_count"),
        )
        writer.writeheader()
        for row in rows:
            writer.writerow(
                {
                    "query": row["query"],
                    "amenities_json": json.dumps(row["amenities"], ensure_ascii=False),
                    "split": row["split"],
                    "group_id": row["group_id"],
                    "amenity_count": row["amenity_count"],
                }
            )


def make_model() -> Pipeline:
    return Pipeline(
        [
            ("tfidf", TfidfVectorizer(ngram_range=(1, 2), sublinear_tf=True)),
            (
                "classifier",
                OneVsRestClassifier(
                    LogisticRegression(
                        max_iter=2000,
                        C=4.0,
                        solver="liblinear",
                        random_state=RANDOM_SEED,
                    )
                ),
            ),
        ]
    )


def predictions_at_threshold(probabilities, threshold: float):
    return (probabilities >= threshold).astype(int)


def amenity_metrics(actual, predicted, labels: list[str]) -> dict[str, object]:
    return {
        "micro_f1": float(f1_score(actual, predicted, average="micro", zero_division=0)),
        "macro_f1": float(f1_score(actual, predicted, average="macro", zero_division=0)),
        "micro_precision": float(
            precision_score(actual, predicted, average="micro", zero_division=0)
        ),
        "micro_recall": float(
            recall_score(actual, predicted, average="micro", zero_division=0)
        ),
        "classification_report": classification_report(
            actual,
            predicted,
            target_names=labels,
            output_dict=True,
            zero_division=0,
        ),
    }


def evaluate_area_model() -> dict[str, object]:
    with ROOMS_PATH.open(encoding="utf-8-sig", newline="") as rooms_file:
        rooms = list(csv.DictReader(rooms_file, delimiter="\t"))
    if not rooms:
        raise ValueError(f"No area examples found in {ROOMS_PATH}")

    texts = [
        " ".join((room["TieuDe"], room["DiaChi"], room["MoTa"]))
        for room in rooms
    ]
    labels = [room["KhuVuc"].strip() for room in rooms]
    indices = list(range(len(rooms)))
    train_indices, test_indices = train_test_split(
        indices,
        test_size=0.25,
        random_state=RANDOM_SEED,
        stratify=labels,
    )
    area_model = joblib.load(MODEL_DIR / "area_model.pkl")
    predictions = area_model.predict([texts[index] for index in test_indices])
    expected = [labels[index] for index in test_indices]
    incorrect = [
        {
            "expected": actual,
            "predicted": predicted,
            "query": texts[index],
        }
        for index, actual, predicted in zip(test_indices, expected, predictions)
        if actual != predicted
    ]

    return {
        "source": str(ROOMS_PATH.relative_to(ROOT)),
        "protocol": (
            "Deterministic stratified 25% reference split. The area model is pre-trained; "
            "its training-data overlap with this source is unknown."
        ),
        "train_samples": len(train_indices),
        "test_samples": len(test_indices),
        "accuracy": float(accuracy_score(expected, predictions)),
        "classification_report": classification_report(
            expected,
            predictions,
            output_dict=True,
            zero_division=0,
        ),
        "incorrect_count": len(incorrect),
        "incorrect_examples": incorrect,
    }


def main() -> None:
    if sklearn.__version__ != "1.6.1":
        raise RuntimeError(
            f"Expected scikit-learn 1.6.1; found {sklearn.__version__}. "
            "Install the pinned AI/api/requirements.txt dependencies."
        )

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    rows = build_amenity_dataset()
    save_dataset(rows)

    label_binarizer = MultiLabelBinarizer(
        classes=[normalize_label(name) for name in AMENITIES]
    )
    all_labels = [
        [normalize_label(name) for name in row["amenities"]]
        for row in rows
    ]
    targets = label_binarizer.fit_transform(all_labels)
    texts = [str(row["query"]) for row in rows]
    split_indices = {
        split: [index for index, row in enumerate(rows) if row["split"] == split]
        for split in ("train", "validation", "test")
    }
    if any(not indices for indices in split_indices.values()):
        raise ValueError(f"Empty dataset split: {split_indices}")

    evaluation_model = make_model()
    train_indices = split_indices["train"]
    validation_indices = split_indices["validation"]
    test_indices = split_indices["test"]
    evaluation_model.fit(
        [texts[index] for index in train_indices],
        targets[train_indices],
    )
    validation_probabilities = evaluation_model.predict_proba(
        [texts[index] for index in validation_indices]
    )
    threshold_scores = []
    for threshold in THRESHOLD_CANDIDATES:
        validation_predictions = predictions_at_threshold(
            validation_probabilities,
            threshold,
        )
        threshold_scores.append(
            {
                "threshold": threshold,
                "micro_f1": float(
                    f1_score(
                        targets[validation_indices],
                        validation_predictions,
                        average="micro",
                        zero_division=0,
                    )
                ),
                "macro_f1": float(
                    f1_score(
                        targets[validation_indices],
                        validation_predictions,
                        average="macro",
                        zero_division=0,
                    )
                ),
            }
        )
    selected_threshold = max(
        threshold_scores,
        key=lambda score: (score["macro_f1"], score["micro_f1"]),
    )["threshold"]

    test_probabilities = evaluation_model.predict_proba(
        [texts[index] for index in test_indices]
    )
    test_predictions = predictions_at_threshold(
        test_probabilities,
        selected_threshold,
    )
    test_metrics = amenity_metrics(
        targets[test_indices],
        test_predictions,
        list(label_binarizer.classes_),
    )
    test_metrics.update(
        {
            "train_samples": len(train_indices),
            "validation_samples": len(validation_indices),
            "test_samples": len(test_indices),
            "threshold": selected_threshold,
            "threshold_candidates": threshold_scores,
            "evaluation_protocol": (
                "Held-out amenity-combination groups; deterministic synthetic Vietnamese "
                "query corpus. These metrics measure this generated corpus, not real-user "
                "generalization."
            ),
        }
    )

    production_model = make_model()
    production_model.fit(texts, targets)
    joblib.dump(production_model, MODEL_DIR / "amenity_model.pkl")
    joblib.dump(label_binarizer, MODEL_DIR / "amenity_mlb.pkl")

    metrics = {
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "scikit_learn_version": sklearn.__version__,
        "random_seed": RANDOM_SEED,
        "area_model": evaluate_area_model(),
        "amenity_model": test_metrics,
        "dataset": {
            "path": str(QUERY_DATASET_PATH.relative_to(ROOT)),
            "total_samples": len(rows),
            "split_counts": dict(Counter(str(row["split"]) for row in rows)),
            "label_count": len(label_binarizer.classes_),
            "labels": list(label_binarizer.classes_),
            "amenity_count_distribution": dict(
                sorted(Counter(int(row["amenity_count"]) for row in rows).items())
            ),
            "protocol": (
                "Exhaustive zero-, one-, two-, and three-amenity combinations with varied "
                "Vietnamese query forms; splits are disjoint by combination group."
            ),
        },
    }
    METRICS_PATH.write_text(
        json.dumps(metrics, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    metadata = {
        "trained_at_utc": metrics["generated_at_utc"],
        "scikit_learn_version": sklearn.__version__,
        "random_seed": RANDOM_SEED,
        "area_threshold": AREA_CONFIDENCE_THRESHOLD,
        "amenity_threshold": selected_threshold,
        "amenity_labels": list(label_binarizer.classes_),
        "amenity_training_samples": len(rows),
        "amenity_test_samples": len(test_indices),
        "amenity_model_file": "amenity_model.pkl",
        "amenity_label_binarizer_file": "amenity_mlb.pkl",
        "metrics_file": str(METRICS_PATH.relative_to(ROOT)),
        "training_data_file": str(QUERY_DATASET_PATH.relative_to(ROOT)),
    }
    METADATA_PATH.write_text(
        json.dumps(metadata, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    print(json.dumps(metrics, ensure_ascii=False, indent=2))
    print(f"\nWrote dataset: {QUERY_DATASET_PATH}")
    print(f"Wrote metrics: {METRICS_PATH}")
    print(f"Wrote metadata: {METADATA_PATH}")
    print(f"Trained model files in: {MODEL_DIR}")


if __name__ == "__main__":
    main()
