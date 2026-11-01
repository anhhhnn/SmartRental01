# Recommendation interaction dataset report

## 1. Nguồn dữ liệu và schema đã audit

Database: `SmartRentalDB`. Export chỉ đọc dữ liệu và lọc user có role `NguoiThue`.

| Signal | Bảng | Khóa chính | User | Phòng | Giá trị | Unique user-phòng |
|---|---|---|---|---|---|---|
| Views | `LichSuXems` | (`UserId`, `PhongtroId`) | `UserId` | `PhongtroId` | `SoLanXem` | Có (PK) |
| Favorite | `YeuThichs` | (`UserId`, `PhongtroId`) | `UserId` | `PhongtroId` | sự tồn tại của record | Có (PK) |
| Booking | `LichXemPhongs` | `Id` | `NguoiThueId` | `PhongtroId` | `TrangThai` | Không |
| Rating | `DanhGias` | (`UserId`, `PhongtroId`) | `UserId` | `PhongtroId` | `SoSao` | Có (PK) |
| Room | `Phongtros` | `Id` | `OwnerId` (owner) | `Id` | `TrangThai`, `IsVisible` | Không áp dụng |
| User/role | `AspNetUsers` / `AspNetUserRoles` / `AspNetRoles` | `Id` / khóa ghép / `Id` | `Id` | — | `AspNetRoles.Name` | Theo Identity |

Booking được tính binary nếu có ít nhất một record `ChoXacNhan` hoặc `DaDongY`. `BaoBan` và `DaHuy` không được tính. Phân bố trạng thái trong DB: `BaoBan`: 42, `ChoXacNhan`: 48, `DaDongY`: 108, `DaHuy`: 43.
Phòng ẩn (`IsVisible = false`) không bị loại khỏi lịch sử.

## 2. Công thức Score

`Score = Views + Favorite * 3 + Booking * 5 + Rating`.

## 3. Tổng quan

- Interaction hợp lệ trước lọc role: **3,750**
- Interaction trong dataset `NguoiThue`: **3,740**
- Unique users: **74** (DB có 74 user mang role `NguoiThue`)
- Unique rooms có interaction: **449** / 450
- Interaction/user: min 3, max 64, mean 50.54, median 51.00
- Tài khoản demo nhận diện bởi `nguoithue*@smartrental.local`: **72** / 74 users; **3,726** / 3,740 interactions (99.63%)

## 4. Density và sparsity

- Possible interactions trong ma trận quan sát: 33,226 = 74 users × 449 rooms
- Density: **11.2562%**
- Sparsity: **88.7438%**

## 5. Phân bố signal

- Views > 0: **2,763** dòng
- Favorite = 1: **748** dòng
- Booking = 1: **156** dòng
- Rating > 0: **912** dòng
- Tổng Views: **20,149**; mean Views/dòng: **5.39**

## 6. Phân bố interaction theo user

- 1 interaction: **0** users
- 2-4 interactions: **1** users
- 5-9 interactions: **0** users
- 10-19 interactions: **1** users
- >=20 interactions: **72** users
- User có 1-4 interaction (cold-start rõ): **1** / 74

## 7. Score

- Min: **1**
- Max: **17**
- Mean: **7.16**
- Median: **7.00**

## 8. Phân bố room

- Phòng không có interaction trong dataset: **1**
- Phòng có nhiều user interaction nhất: **117** (17 users)

| Rank | PhongtroId | Số user interaction |
|---:|---:|---:|
| 1 | 117 | 17 |
| 2 | 162 | 14 |
| 3 | 156 | 14 |
| 4 | 192 | 13 |
| 5 | 62 | 12 |
| 6 | 101 | 12 |
| 7 | 328 | 12 |
| 8 | 200 | 12 |
| 9 | 306 | 12 |
| 10 | 145 | 12 |

## 9. Validation

- Kết quả: **Đạt**
- Duplicate `(UserId, PhongtroId)`: **0**
- Orphan source records: **0** (views 0, favorites 0, bookings 0, ratings 0)
- Invalid source ranges: **0** (negative views 0, ratings ngoài 1-5 0)
- Range yêu cầu: `Views >= 0`; `Favorite` và `Booking` thuộc `{0,1}`; `Rating` thuộc `{0..5}`; `Score > 0`.

## 10. Giới hạn và hướng tiếp theo

- Đây là implicit-feedback heuristic, không phải nhãn đã học; các trọng số chưa được đánh giá offline.
- Booking chỉ biểu diễn đã từng tạo/được đồng ý, không phân biệt số lần và chưa có trạng thái hoàn thành.
- User không có signal và room chưa từng được tương tác không xuất hiện trong ma trận; runtime vẫn phải lọc phòng không visible.
- Role filter đã loại interaction owner/admin khỏi dataset. Các tài khoản demo vẫn mang role tenant và dữ liệu của chúng được sinh có chủ đích để mô phỏng hành vi, nên được giữ lại; tuy nhiên chúng không phải hành vi production.
- Sau khi tách phần demo có thể nhận diện bằng email, chỉ còn **14** interaction từ **2** tenant khác; không đủ để đánh giá hoặc train Collaborative Filtering production.
- Đánh giá: **Ưu tiên Content-Based (sau đó hybrid); dữ liệu hiện tại còn thưa hoặc có quá nhiều user ít interaction để chỉ dựa vào Collaborative Filtering.**

## 11. 10 dòng đầu (UserId đã mask)

```csv
UserId,PhongtroId,Views,Favorite,Booking,Rating,Score
003f...e44d,27,0,0,0,3,3
003f...e44d,40,10,0,0,0,10
003f...e44d,41,6,0,0,0,6
003f...e44d,52,0,0,0,3,3
003f...e44d,62,7,1,0,0,10
003f...e44d,89,4,0,0,0,4
003f...e44d,96,8,0,0,0,8
003f...e44d,101,6,0,0,0,6
003f...e44d,102,0,0,0,5,5
003f...e44d,120,7,0,0,0,7
```
