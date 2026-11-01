# Browser E2E và responsive report

Thời điểm chạy: 2026-10-29. Browser: Google Chrome headless trên ứng dụng thật `http://127.0.0.1:5038`, FastAPI thật `http://127.0.0.1:8000`, database development hiện tại.

## AI Search

| Nhóm | Câu kiểm tra | Kết quả UI |
|---|---|---|
| Giá | tìm phòng dưới 2 triệu | Pass — summary có “Giá tối đa” |
| Tiện nghi | phòng có wifi điều hòa | Pass — summary có “Tiện nghi” |
| Khu vực | phòng ở Tân Thịnh | Pass — summary có “Khu vực” |
| Landmark | phòng gần ICTU trong 3km | Pass — summary có “Bán kính” |
| Multi-condition | từ 1–2 triệu, 2 người, Wifi, gần ICTU 3km | Pass — summary có khoảng giá và request hoàn tất |

## Recommendation

- Known demo user: pass; heading “Gợi ý cho bạn”, 8/8 card có explanation, không có cosine score trên trang.
- Low-interaction user: API/demo test pass với profile 3 interactions và Top 5 có reasons; xem `recommendation_demo_report.md`.
- Cold-start user: API/demo test pass với `coldStart=true`, popularity fallback và wording trung tính.
- Anonymous: pass; không có `.recommendation-section`.
- User đang AI Search: pass; recommendation section tự ẩn.

## Failure mode

FastAPI PID đã được xác minh bằng OpenAPI title trước khi dừng. Khi FastAPI tắt:

- web vẫn tải và có search-results: pass;
- recommendation section tự ẩn: pass;
- truy vấn AI Search vẫn trả trang/fallback hiện có thay vì làm web crash: pass.

Sau test, FastAPI được khởi động lại; `/health` trở về `status=ok`, Search và Recommendation `ready`.

## Responsive

Script kiểm tra navbar, AI Search box, filters, room grid, room cards, pagination và horizontal overflow.

| Viewport | Thành phần bắt buộc | Horizontal overflow | Kết quả |
|---:|---|---|---|
| 1440px | Đủ | Không | Pass |
| 1024px | Đủ | Không | Pass |
| 768px | Đủ | Không | Pass |
| 390px | Đủ | Không | Pass |

Không redesign website. Script chạy lại: `AI/evaluation/browser_e2e.py`; Selenium có thể được cấp qua `PYTHONPATH` để không thêm dependency browser vào runtime application.

## Wording audit

Đã xác nhận có các cụm “Tìm bằng AI”, “AI hiểu yêu cầu”, “Gợi ý cho bạn”, “Phòng nổi bật dành cho bạn”. Không tìm thấy “AI chính xác 100%”, “Phòng phù hợp nhất tuyệt đối”, hoặc “AI chắc chắn bạn sẽ thích”.
