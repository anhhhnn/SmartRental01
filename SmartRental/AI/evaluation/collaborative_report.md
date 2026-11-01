# Báo cáo Collaborative Filtering challenger

## Phương pháp và audit

- Thuật toán: implicit-feedback ALS (Hu–Koren–Volinsky), tự triển khai bằng NumPy/SciPy.
- Preference của mọi interaction quan sát là 1; confidence = `1 + alpha * log1p(Score)`. `Score` không được coi là rating 1–5.
- Schema nguồn: `UserId, PhongtroId, Views, Favorite, Booking, Rating, Score`; dataset gốc không bị sửa.
- Dataset: **3740** interactions, **74** users, **449** rooms có interaction.
- Trung bình **50.54** interaction/user; sparsity **88.7438%**.
- Demo/non-demo: **72 / 2** users; **3726 / 14** interactions.
- Candidate set: **421** phòng visible, giống Content-Based optimized.
- Outer split seed `20261028`: **2964** train, **776** test; **73** evaluated, **1** excluded (<5 interactions).
- Leakage: **0** held-out pair xuất hiện trong training matrix.
- Hyperparameter được chọn trên inner validation của outer-train; outer test chỉ dùng một lần để báo cáo và so sánh.

## Best config

- Benchmarked: **12** cấu hình.
- factors=64, regularization=0.05, iterations=15, alpha=20, seed=20261028.

## So sánh trực tiếp

| Metric | Content-Based optimized | Collaborative | Difference |
|---|---:|---:|---:|
| Precision@5 | 0.037838 | 0.027397 | -0.010441 |
| Recall@5 | 0.017440 | 0.012635 | -0.004805 |
| HitRate@5 | 0.189189 | 0.136986 | -0.052203 |
| Precision@10 | 0.035135 | 0.026027 | -0.009108 |
| Recall@10 | 0.035307 | 0.024182 | -0.011125 |
| HitRate@10 | 0.337838 | 0.246575 | -0.091262 |
| NDCG@10 | 0.038274 | 0.026558 | -0.011716 |
| Coverage@10 | 0.154394 | 0.558195 | +0.403800 |

## Metric theo nhóm

| Nhóm | Users | Precision@10 | Recall@10 | HitRate@10 | NDCG@10 | Coverage@10 |
|---|---:|---:|---:|---:|---:|---:|
| All users | 73 | 0.026027 | 0.024182 | 0.246575 | 0.026558 | 0.558195 |
| Demo users | 72 | 0.026389 | 0.024518 | 0.250000 | 0.026927 | 0.548694 |
| Non-demo users | 1 | 0.000000 | 0.000000 | 0.000000 | 0.000000 | 0.023753 |

Non-demo chỉ có 2 tài khoản và không được dùng để tune; các số liệu này chỉ mang tính tham khảo.

## Diversity và popularity bias

- Coverage@10: **0.558195** (235/421 phòng).
- Average pairwise Top10 Jaccard: **0.030035**.
- Average recommendation popularity: **7.250685** train users/phòng.
- Top phòng xuất hiện nhiều nhất: 172 (14 lần), 36 (11 lần), 343 (10 lần), 318 (10 lần), 438 (9 lần).

## Ba user ví dụ

### `b655...4fc4`

- Interactions: 64.
- Collaborative Top5: 104 (0.210908), 183 (0.205619), 184 (0.187585), 25 (0.178001), 86 (0.164312).
- Content-Based Top5: 271 (0.912672), 374 (0.912428), 347 (0.910082), 304 (0.903213), 297 (0.898945).
- Overlap: 0/5 ([]).

### `f032...db15`

- Interactions: 51.
- Collaborative Top5: 376 (0.185804), 341 (0.185333), 295 (0.146933), 264 (0.144849), 182 (0.141581).
- Content-Based Top5: 271 (0.913812), 304 (0.912093), 97 (0.904926), 297 (0.904299), 30 (0.898587).
- Overlap: 0/5 ([]).

### `bf07...bbfd`

- Interactions: 3.
- Collaborative Top5: 253 (0.400107), 83 (0.317516), 42 (0.290614), 37 (0.284366), 326 (0.282050).
- Content-Based Top5: 48 (0.876942), 409 (0.866974), 436 (0.866652), 261 (0.856400), 147 (0.854906).
- Overlap: 0/5 ([]).

## Kết luận

C. Collaborative kém rõ rệt; giữ Content-Based làm model chính, Collaborative chỉ là challenger.

Chưa nên chuyển sang Hybrid trong lượt này; chỉ nên thử Hybrid ở một thí nghiệm riêng sau khi có thêm dữ liệu non-demo và chính sách fallback cold-start.

Đây là kết quả nghiên cứu trên dữ liệu chủ yếu là demo, không phải bằng chứng model production-ready.
