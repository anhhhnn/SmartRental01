# Báo cáo Content-Based Recommendation

## 1. Đặc trưng và dữ liệu

- Nhóm đặc trưng: `KhuVuc` (one-hot), `TienNghi` (multi-hot), `Gia`, `DienTich`, `SoNguoiToiDa` (StandardScaler).
- Số phòng: **450**; visible: **421**; hidden: **29**.
- Số nhóm feature: **5**; số chiều vector: **35**.
- User có profile: **74**; tenant cold-start: **0**.
- Vector hợp lệ: **passed** (không NaN/infinite, chiều cố định).

## 2. Phương pháp đánh giá

- Seed cố định: `20261028`; giữ lại 20% interaction (ít nhất 1) theo từng user.
- Chỉ user có ít nhất 2 interaction và còn profile train mới được đánh giá; test chỉ gồm phòng visible.
- Train interactions: **2963**; test interactions: **777**; eligible users: **74**.
- Candidate loại phòng hidden và loại các phòng trong train profile (`exclude_seen=True`).

## 3. Metric tổng thể

| Nhóm | Users | Precision@5 | Recall@5 | HitRate@5 | Precision@10 | Recall@10 | HitRate@10 | NDCG@10 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| All users | 74 | 0.035135 | 0.016077 | 0.175676 | 0.032432 | 0.029178 | 0.283784 | 0.032274 |
| Demo users | 72 | 0.036111 | 0.016524 | 0.180556 | 0.033333 | 0.029989 | 0.291667 | 0.033170 |
| Non-demo users | 2 | 0.000000 | 0.000000 | 0.000000 | 0.000000 | 0.000000 | 0.000000 | 0.000000 |

## 4. Demo và non-demo

- Demo: **72** user có profile; **72** user đủ điều kiện evaluate.
- Non-demo: **2** user có profile; **2** user đủ điều kiện evaluate.
- Nhóm non-demo quá ít để suy rộng chất lượng cho người dùng thật; metric tổng thể chủ yếu phản ánh dữ liệu demo.

## 5. Kiểm tra logic trên 3 user

### User 1: `b655...4fc4` (64 interactions)

- Khu vực quan tâm: Đồng Quang, Tân Thịnh, Trung Thành.
- Giá trung bình có trọng số: 1,779,097 VNĐ.
- Tiện nghi nổi bật: Wifi, Chỗ để xe, WC riêng, Điều hòa, Tủ quần áo.
- Top 5: 271 (0.887913), 347 (0.877368), 374 (0.870324), 297 (0.864525), 304 (0.861167).

### User 2: `f032...db15` (51 interactions)

- Khu vực quan tâm: Đồng Quang, Quan Triều, Hương Sơn.
- Giá trung bình có trọng số: 1,802,586 VNĐ.
- Tiện nghi nổi bật: Wifi, Chỗ để xe, WC riêng, Nóng lạnh, Tủ quần áo.
- Top 5: 304 (0.899204), 271 (0.898653), 297 (0.880432), 97 (0.876703), 454 (0.868718).

### User 3: `bf07...bbfd` (3 interactions)

- Khu vực quan tâm: Hương Sơn, Quang Trung, Quan Triều.
- Giá trung bình có trọng số: 1,300,000 VNĐ.
- Tiện nghi nổi bật: Chỗ để xe, Wifi, Nóng lạnh, Tủ quần áo, WC riêng.
- Top 5: 105 (0.819806), 191 (0.818470), 127 (0.813610), 436 (0.810198), 48 (0.803914).

## 6. Giới hạn

- Interaction là implicit feedback với Score heuristic; chưa phải nhãn sở thích đã được kiểm chứng.
- Split là ngẫu nhiên có seed vì dataset không có timestamp; chưa mô phỏng đầy đủ thứ tự thời gian.
- 72/74 profile thuộc tài khoản demo, còn non-demo quá ít để kết luận production.
- Content features hiện chưa dùng mô tả dài, tọa độ, trạng thái còn phòng hay chất lượng chủ trọ.
- Cold-start trả popularity fallback; `ContentScore` để null và không được xem là cá nhân hóa.

## 7. Kết luận

Kết quả chỉ chứng minh pipeline chạy đúng và cung cấp baseline có thể giải thích. Chưa có cơ sở tuyên bố recommendation tốt cho người dùng thật. Chưa nên dùng Collaborative Filtering làm hướng chính; cần thu thập thêm interaction non-demo và đánh giá theo thời gian trước, sau đó mới so sánh CF và hybrid.
