# Recommendation demo: ba nhóm user

Sinh từ `recommendation_demo.py` ngày 2026-10-29. User ID chỉ hiển thị dạng mask.

## User A — nhiều interaction (`b655...4fc4`)

- Profile: 64 interactions; khu vực nổi bật Đồng Quang, Tân Thịnh, Sơn Cẩm; tiện nghi nổi bật Chỗ để xe, Wifi, WC riêng; giá trung bình có trọng số khoảng 1.806.402đ.
- Cold-start: `false`; model: `content_based_optimized`.

| Rank | Phòng | Reasons |
|---:|---:|---|
| 1 | 271 | Có Chỗ để xe và Wifi bạn thường quan tâm; Gần mức giá bạn thường quan tâm; Phù hợp với sức chứa bạn thường xem |
| 2 | 374 | Có Chỗ để xe và Wifi bạn thường quan tâm; Gần mức giá bạn thường quan tâm; Phù hợp với sức chứa bạn thường xem |
| 3 | 347 | Có Chỗ để xe và Wifi bạn thường quan tâm; Gần mức giá bạn thường quan tâm; Phù hợp với sức chứa bạn thường xem |
| 4 | 304 | Có Chỗ để xe và Wifi bạn thường quan tâm; Cùng khu vực bạn thường xem; Phù hợp với sức chứa bạn thường xem |
| 5 | 297 | Có Chỗ để xe và Wifi bạn thường quan tâm; Gần mức giá bạn thường quan tâm; Phù hợp với sức chứa bạn thường xem |

## User B — ít interaction (`bf07...bbfd`)

- Profile: 3 interactions; khu vực nổi bật Hương Sơn, Quan Triều, Quang Trung; tiện nghi nổi bật Chỗ để xe, Wifi, Ban công; giá trung bình có trọng số 1.300.000đ.
- Cold-start: `false`; model: `content_based_optimized`.

| Rank | Phòng | Reasons |
|---:|---:|---|
| 1 | 48 | Có Chỗ để xe và Wifi bạn thường quan tâm; Cùng khu vực bạn thường xem; Gần mức giá bạn thường quan tâm |
| 2 | 409 | Có Chỗ để xe và Wifi bạn thường quan tâm; Gần mức giá bạn thường quan tâm; Cùng khu vực bạn thường xem |
| 3 | 436 | Có Chỗ để xe và Wifi bạn thường quan tâm; Gần mức giá bạn thường quan tâm; Cùng khu vực bạn thường xem |
| 4 | 261 | Có Chỗ để xe và Wifi bạn thường quan tâm; Gần mức giá bạn thường quan tâm; Phù hợp với sức chứa bạn thường xem |
| 5 | 147 | Có Chỗ để xe và Wifi bạn thường quan tâm; Gần mức giá bạn thường quan tâm; Phù hợp với sức chứa bạn thường xem |

## User C — cold-start (`cold...demo`)

- Profile: 0 interactions, không có sở thích cá nhân hóa.
- Cold-start: `true`; model: `popular_fallback`.

| Rank | Phòng | Reason trung tính |
|---:|---:|---|
| 1 | 117 | Phòng đang được nhiều người quan tâm |
| 2 | 162 | Phòng đang được nhiều người quan tâm |
| 3 | 156 | Phòng đang được nhiều người quan tâm |
| 4 | 192 | Phòng đang được nhiều người quan tâm |
| 5 | 62 | Phòng đang được nhiều người quan tâm |

Kiểm tra chạy lại: `cd AI/evaluation; py recommendation_demo.py` và `py -m unittest test_recommendation_demo.py`.
