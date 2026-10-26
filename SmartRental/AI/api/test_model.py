import os

import joblib

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
MODEL_DIR = os.path.join(BASE_DIR, "..", "models")

def main():
    model = joblib.load(
        os.path.join(MODEL_DIR, "amenity_model.pkl")
    )

    mlb = joblib.load(
        os.path.join(MODEL_DIR, "amenity_mlb.pkl")
    )

    text = "Tìm phòng dưới 1,5 triệu cho 2 người ở Tân Thịnh có wifi và điều hòa"

    prediction = model.predict([text])
    proba = model.predict_proba([text])

    print("\nProbabilities:")

    for name, score in zip(mlb.classes_, proba[0]):
        print(f"{name:12s}: {score:.4f}")
    print("Classes:")
    print(mlb.classes_)

    print("\nRaw prediction:")
    print(prediction)

    print("\nDecoded:")
    print(mlb.inverse_transform(prediction))


if __name__ == "__main__":
    main()
