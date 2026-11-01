# Báo cáo cuối AI Search

Ngày chốt báo cáo: 2026-10-29.

## 1. Kiến trúc

Luồng xử lý là `Index.cshtml -> PhongtroController -> AiSearchService -> FastAPI /parse-search`. Python phân tích câu tự nhiên thành bộ lọc có cấu trúc; ASP.NET áp dụng bộ lọc lên dữ liệu phòng và render bằng UI hiện có. Nếu FastAPI không gọi được, web vẫn tải và `AiSearchService`/controller giữ cơ chế fallback hiện tại.

FastAPI nạp model một lần khi process khởi động. Dịch vụ dùng snapshot model/CSV, không truy vấn realtime database.

## 2. Input/output

Input:

```json
{"text":"tìm phòng từ 1 đến 2 triệu cho 2 người có wifi gần ICTU trong 3km"}
```

Output có cấu trúc:

```json
{
  "minPrice": 1000000,
  "maxPrice": 2000000,
  "soNguoi": 2,
  "khuVuc": null,
  "tienNghi": ["wifi"],
  "nearPlace": "Đại học Công nghệ Thông tin và Truyền thông",
  "maxDistanceKm": 3.0
}
```

## 3. Rule và ML

- Rule/regex: giá tối thiểu, tối đa, khoảng giá, số người, khoảng cách và alias landmark.
- ML area: classifier dự đoán khu vực, có ngưỡng confidence `0.55`; dưới ngưỡng trả `null`.
- ML amenity: multi-label classifier, ngưỡng lấy từ `AI/models/model_metadata.json` (`0.25` tại thời điểm báo cáo).
- Guard rule: nếu nhận diện landmark thì không suy landmark thành khu vực.

Thiết kế này ưu tiên output có thể kiểm tra, không dùng LLM và không tạo nội dung tự do.

## 4. Area model

Nguồn đánh giá là `AI/DATA/rooms.csv`, split tham chiếu stratified 25%, seed `20261027`. Accuracy hiện có là **0.933333** trên 60 mẫu. Training-data overlap của model đã huấn luyện với nguồn đánh giá này chưa xác định, vì vậy số này chỉ là bằng chứng kỹ thuật, không phải production benchmark.

Area classifier có thể trả `KHONG_XAC_DINH`/`null` khi confidence thấp. Demo chính thức ghi nhận một câu nhiều điều kiện không nhận ra `Đồng Quang`; model không bị chỉnh chỉ để pass câu demo.

## 5. Amenity model

Model dự đoán 12 nhãn: Wifi, điều hòa, nóng lạnh, chỗ để xe, máy giặt, tủ lạnh, ban công, WC riêng, bếp, giường, tủ quần áo và khép kín.

Metric lưu sẵn: micro-F1 **1.0**, macro-F1 **1.0**, test 287 mẫu. Đây là **synthetic dataset** gồm 1.812 câu được sinh theo tổ hợp tiện nghi, split tách theo combination group. Metric không đại diện khả năng tổng quát trên câu người dùng thật.

## 6. Landmark search

Parser dùng alias xác định cho ICTU, Đại học Nông Lâm, Đại học Khoa học, Đại học Sư phạm và Trường Ngoại ngữ. ASP.NET tra landmark active theo tên/alias trong database. Không tìm thấy landmark thì hiển thị cảnh báo, không tự bịa tọa độ.

## 7. Khoảng cách Haversine

`GeoDistanceService` dùng bán kính Trái Đất 6.371 km và công thức Haversine. Bộ lọc SQL thông thường chạy trước; với tập dữ liệu prototype, khoảng cách được tính in-memory cho các phòng có latitude/longitude, lọc theo bán kính rồi sắp xếp. Giá trị Haversine được clamp `[0,1]` để tránh NaN do sai số floating-point.

Tọa độ phòng hiện là dữ liệu **simulated quanh tâm khu vực**, không phải vị trí khảo sát chính xác.

## 8. Parser tests

- Unit/API Search hiện có: nằm tại `AI/api/test_search_parser.py` và `AI/api/test_model.py`.
- Bộ demo chính thức: 26 câu tại `AI/evaluation/search_demo_queries.json`.
- Kết quả ngày 2026-10-29: **23 pass, 0 fail trong nhóm declared-supported, 3 documented unsupported**.

Ba limitation được giữ công khai:

1. câu kết hợp giá + nhiều tiện nghi + `Đồng Quang` làm area confidence thấp;
2. cú pháp giá không dấu `1tr5` chưa hỗ trợ;
3. `nguoi` không dấu chưa được rule số người nhận diện.

Script chạy lại: `cd AI/evaluation; py run_search_demo.py`.

## 9. Metric

| Thành phần | Metric | Giá trị |
|---|---|---:|
| Area | Accuracy | 0.933333 |
| Area | Test samples | 60 |
| Amenity | Micro-F1 | 1.000000 |
| Amenity | Macro-F1 | 1.000000 |
| Amenity | Test samples | 287 |
| Parser demo | Supported pass | 23/23 |
| Parser demo | Documented unsupported | 3/26 |

Các số model lấy nguyên từ `search_model_metrics.json`, không tính lại hoặc sửa metric cũ.

## 10. Limitation

- Amenity metric dùng synthetic dataset, chưa phải truy vấn thật.
- Room coordinates được mô phỏng quanh tâm khu vực.
- Service dùng snapshot CSV/model, chưa realtime DB.
- Area evaluation có nguy cơ overlap với training source chưa xác định.
- Parser còn phụ thuộc một số cú pháp/dấu tiếng Việt.
- Haversine in-memory phù hợp demo 450 phòng, chưa phải thiết kế scale lớn.

## 11. Ví dụ end-to-end

Với câu “tìm phòng từ 1 đến 2 triệu cho 2 người có wifi gần ICTU trong 3km”:

1. Python trả khoảng giá, sức chứa, Wifi, landmark ICTU và bán kính 3 km.
2. ASP.NET lọc giá/sức chứa/tiện nghi trong SQL.
3. Controller xác định tọa độ ICTU, tính Haversine, giữ phòng trong 3 km.
4. UI hiển thị “AI hiểu yêu cầu”, khoảng cách trên card và danh sách đã phân trang.

Kết luận: AI Search đủ ổn định để demo có kiểm soát, nhưng không được mô tả là chính xác tuyệt đối.
