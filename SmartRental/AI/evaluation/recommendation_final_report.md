# Báo cáo cuối Recommendation

Ngày chốt báo cáo: 2026-10-29.

## 1. Interaction dataset

Snapshot `AI/DATA/interactions.csv` có **3.740 interactions**, **74 users**, 449 phòng có interaction; catalogue có 450 phòng, trong đó 421 visible. Schema: `UserId, PhongtroId, Views, Favorite, Booking, Rating, Score`.

**72/74 user và 3.726/3.740 interaction là dữ liệu demo.** Chỉ có 2 non-demo user, vì vậy Recommendation metric không đại diện production.

## 2. Scoring heuristic

Baseline giữ nguyên công thức `Views + Favorite*3 + Booking*5 + Rating`. Optimized dùng `log1p(Views)`, trọng số favorite `3`, booking `5`, mapping rating không âm `{0:0,1:0,2:0,3:0.5,4:2,5:4}`. Score là implicit-feedback strength, không được coi là rating dự đoán.

## 3. Content-Based baseline

Room vector gồm khu vực one-hot, tiện nghi multi-hot, và giá/diện tích/sức chứa đã standardize. User profile là trung bình room vector có trọng số interaction. Ranking dùng cosine similarity, loại phòng đã xem và phòng hidden.

## 4. Optimized Content-Based

Cấu hình chính: `B_non_negative__feature_2`. Trọng số feature: khu vực `1.3`, tiện nghi `1.7`, giá `1.4`, diện tích `0.8`, sức chứa `0.8`.

So với baseline trên cùng protocol, optimized tăng NDCG@10 từ 0.032274 lên 0.038274 và Hit@10 từ 0.283784 lên 0.337838; Coverage@10 giảm từ 0.161520 xuống 0.154394.

## 5. Tuning 24 cấu hình

Đã benchmark **24 cấu hình** (interaction weighting × feature weighting). Thứ tự chọn: NDCG@10, Hit@10, Recall@10, Precision@10. Non-demo segment không dùng để tune. Kết quả đầy đủ giữ tại `content_based_tuning_results.json`.

## 6. Collaborative experiment

Challenger là implicit-feedback ALS (NumPy/SciPy), benchmark **12 cấu hình**; best config: factors 64, regularization 0.05, iterations 15, alpha 20, seed 20261028. Outer test không có leaked pair. Collaborative có Coverage cao hơn nhưng accuracy ranking thấp hơn Content-Based optimized.

## 7. So sánh model

Các số dưới đây lấy nguyên từ metric JSON hiện có:

| Model | P@10 | R@10 | Hit@10 | NDCG@10 | Coverage@10 |
|---|---:|---:|---:|---:|---:|
| Content-Based baseline | 0.032432 | 0.029178 | 0.283784 | 0.032274 | 0.161520 |
| Content-Based optimized | 0.035135 | 0.035307 | 0.337838 | 0.038274 | 0.154394 |
| Collaborative | 0.026027 | 0.024182 | 0.246575 | 0.026558 | 0.558195 |

## 8. Lý do chọn Content-Based

Content-Based optimized thắng Collaborative ở P@10, R@10, Hit@10 và NDCG@10; tận dụng được feature phòng và giải thích trực tiếp bằng overlap. Collaborative được giữ làm challenger nghiên cứu vì Coverage tốt, không dùng làm model runtime chính.

## 9. Cold-start

User không có profile nhận danh sách popularity deterministic dựa trên lượng interaction, favorite, rating trung bình và views; chỉ phòng visible được trả. Response đặt `coldStart=true`, model `popular_fallback`, và reason trung tính “Phòng đang được nhiều người quan tâm”. Không gắn wording cá nhân hóa cho cold-start.

## 10. API integration

FastAPI cung cấp `GET /recommend/{userId}` với `top_n` 1–50 và `exclude_seen`. ASP.NET gọi qua typed `HttpClient`, map `PhongtroId -> RecommendationItem`, truy vấn room theo ID rồi khôi phục thứ tự rank. Anonymous không gọi recommendation; khi search/filter/page đang active thì section tự ẩn; API lỗi thì web vẫn tải và section không xuất hiện.

## 11. Explanation

Mỗi item có `reasons` (0–3 phần tử). Python là nơi duy nhất sinh explanation; C# chỉ deserialize/map/render.

Candidate reason được tính từ user profile có trọng số, feature phòng và đúng feature weight optimized:

- overlap khu vực;
- overlap tiện nghi (nêu tối đa hai tiện nghi thực);
- độ gần weighted-average price trong ngưỡng 25%;
- sức chứa cách weighted-average không quá 1 người.

Các reason được xếp theo weighted contribution và lấy tối đa 3. Nếu không có overlap đủ điều kiện, trả mảng rỗng thay vì bịa nội dung. UI hiển thị tối đa hai dòng và không hiển thị cosine score.

Ví dụ item:

```json
{
  "phongtroId": 271,
  "score": 0.9126718166,
  "rank": 1,
  "reasons": [
    "Có Chỗ để xe và Wifi bạn thường quan tâm",
    "Gần mức giá bạn thường quan tâm",
    "Phù hợp với sức chứa bạn thường xem"
  ]
}
```

## 12. Limitation demo-data

- Phần lớn interaction là demo: 72/74 user, 3.726/3.740 interaction.
- Chỉ có 2 non-demo user; metric non-demo chỉ mô tả, không suy rộng.
- Recommendation metric không đại diện production.
- CSV không có timestamp nên dùng seeded random holdout, chưa mô phỏng chronological split.
- Room coordinates là simulated quanh tâm khu vực.
- AI Search amenity metric dùng synthetic dataset.
- Runtime service dùng snapshot CSV, chưa realtime DB; thay đổi DB chưa tự cập nhật profile/catalogue.
- Popularity fallback là heuristic catalogue, chưa có kiểm thử online.

Kết luận: hệ thống đủ để demo/bảo vệ pipeline, explainability và giới hạn; chưa đủ bằng chứng để tuyên bố production-ready. Chưa nên thêm AI thứ ba trước khi có thêm interaction thật và đánh giá theo thời gian.
