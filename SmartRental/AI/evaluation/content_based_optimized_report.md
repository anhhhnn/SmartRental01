# Báo cáo tối ưu Content-Based Recommendation

## Kết quả baseline và optimized

| Metric | Baseline | Optimized | Change |
|---|---:|---:|---:|
| Precision@5 | 0.035135 | 0.037838 | +7.69% |
| Recall@5 | 0.016077 | 0.017440 | +8.48% |
| HitRate@5 | 0.175676 | 0.189189 | +7.69% |
| Precision@10 | 0.032432 | 0.035135 | +8.33% |
| Recall@10 | 0.029178 | 0.035307 | +21.00% |
| HitRate@10 | 0.283784 | 0.337838 | +19.05% |
| NDCG@10 | 0.032274 | 0.038274 | +18.59% |
| Coverage@10 | 0.161520 | 0.154394 | -4.41% |

## Cấu hình được chọn

- Số cấu hình đã benchmark: **24**.
- Interaction weighting: **B_non_negative**.
- Feature weighting: **feature_2**.
- Quyết định: **keep optimized config**.
- Thứ tự chọn: NDCG@10, HitRate@10, Recall@10, Precision@10; non-demo không được dùng để tune.
- Baseline giữ nguyên công thức `Views + Favorite*3 + Booking*5 + Rating`.
- Cả rating mapping có dấu và không âm đều đã được benchmark; mapping không âm cho kết quả tốt hơn trong grid này.

## Metric theo nhóm

| Nhóm | Users | Precision@10 | Recall@10 | HitRate@10 | NDCG@10 | Coverage@10 |
|---|---:|---:|---:|---:|---:|---:|
| All users | 74 | 0.035135 | 0.035307 | 0.337838 | 0.038274 | 0.154394 |
| Demo users | 72 | 0.034722 | 0.031658 | 0.333333 | 0.037376 | 0.128266 |
| Non-demo users | 2 | 0.050000 | 0.166667 | 0.500000 | 0.070633 | 0.047506 |

## Diversity và cá nhân hóa

- Optimized Coverage@10: **0.154394** (65 phòng khác nhau / 421 phòng visible).
- Overlap Top10 trung bình (pairwise Jaccard): **0.330009**.
- Overlap càng gần 0 thì danh sách giữa các user càng khác nhau; kết quả này không cho thấy mọi user nhận cùng một Top10.

## Ba ví dụ trước/sau

### User 1: `b655...4fc4`

- Interactions: 64; khu vực: Đồng Quang, Tân Thịnh, Sơn Cẩm; giá weighted average: 1,806,402 VNĐ.
- Tiện nghi: Chỗ để xe, Wifi, WC riêng, Nóng lạnh, Giường.
- Baseline Top5: 271 (0.887913), 347 (0.877368), 374 (0.870324), 297 (0.864525), 304 (0.861167).
- Optimized Top5: 271 (0.912672), 374 (0.912428), 347 (0.910082), 304 (0.903213), 297 (0.898945).

### User 2: `f032...db15`

- Interactions: 51; khu vực: Đồng Quang, Hương Sơn, Quan Triều; giá weighted average: 1,790,805 VNĐ.
- Tiện nghi: Wifi, Chỗ để xe, WC riêng, Nóng lạnh, Tủ quần áo.
- Baseline Top5: 304 (0.899204), 271 (0.898653), 297 (0.880432), 97 (0.876703), 454 (0.868718).
- Optimized Top5: 271 (0.913812), 304 (0.912093), 97 (0.904926), 297 (0.904299), 30 (0.898587).

### User 3: `bf07...bbfd`

- Interactions: 3; khu vực: Hương Sơn, Quang Trung, Quan Triều; giá weighted average: 1,300,000 VNĐ.
- Tiện nghi: Chỗ để xe, Wifi, Nóng lạnh, Tủ quần áo, WC riêng.
- Baseline Top5: 105 (0.819806), 191 (0.818470), 127 (0.813610), 436 (0.810198), 48 (0.803914).
- Optimized Top5: 48 (0.876942), 409 (0.866974), 436 (0.866652), 261 (0.856400), 147 (0.854906).

## Audit và giới hạn

- Không có data leakage: profile evaluation chỉ dựng từ train; held-out room không xuất hiện trong train của cùng user.
- CSV không có timestamp nên dùng seeded random holdout 20% theo user; đây là một limitation.
- Metric accuracy giảm: không có.
- Diversity giảm: Coverage@10 từ 0.161520 xuống 0.154394 (-4.41%).
- Chỉ có 2 user non-demo; metric non-demo chỉ để tham khảo, không dùng chọn hyperparameter.
- Dữ liệu chủ yếu từ tài khoản demo, vì vậy không thể tuyên bố chất lượng production.

## Kết luận

Nên giữ cấu hình optimized vì NDCG@10 tăng trên cùng split, nhưng chỉ xem đây là kết quả thực nghiệm trên dữ liệu demo. Bước tiếp theo hợp lý là thu thập thêm interaction non-demo rồi benchmark Collaborative Filtering và Hybrid, không thay Content-Based ngay.
