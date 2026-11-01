# SmartRental

**Hệ thống tìm kiếm, quản lý và gợi ý phòng trọ tích hợp AI**<br>
Đồ án xây dựng bằng ASP.NET Core MVC, SQL Server và FastAPI.

SmartRental hỗ trợ ba nhóm người dùng: người thuê, chủ trọ và quản trị viên. Ứng dụng cung cấp tìm kiếm và bộ lọc phòng truyền thống, tìm kiếm bằng câu tiếng Việt tự nhiên, gợi ý phòng cá nhân hóa, bản đồ vị trí tham khảo, đặt lịch xem phòng, nhắn tin, yêu thích và đánh giá.

ASP.NET Core MVC là ứng dụng chính và quản lý dữ liệu nghiệp vụ qua EF Core. FastAPI là dịch vụ inference riêng cho AI Search và Recommendation; hệ thống không dùng LLM và không sinh nội dung tự do.

## Quick Start

Sau khi đã cấu hình database và cài Python dependencies, chạy cả web và AI service từ thư mục `SmartRental/`:

```powershell
.\run-dev.ps1
```

Script kiểm tra dịch vụ FastAPI tại `http://127.0.0.1:8000`, tái sử dụng đúng SmartRental API nếu dịch vụ đã chạy, khởi động FastAPI trong tiến trình ẩn nếu cần, sau đó chạy ASP.NET Core trong terminal hiện tại. Trên Windows cũng có thể chạy `run-dev.bat`.

## Chức năng chính

### Người thuê

- Đăng ký, đăng nhập và quản lý hồ sơ cá nhân.
- Tìm phòng bằng từ khóa, khu vực, giá, diện tích, sức chứa, tiện nghi và sắp xếp.
- Tìm kiếm bằng câu tiếng Việt tự nhiên qua AI Search.
- Xem chi tiết, gallery ảnh, vị trí bản đồ và các địa điểm lân cận.
- Lưu phòng yêu thích và xem lịch sử phòng đã truy cập.
- Đánh giá phòng theo số sao và bình luận.
- Đặt, theo dõi hoặc hủy lịch xem phòng.
- Bắt đầu cuộc trò chuyện và nhắn tin với chủ trọ.
- Nhận mục “Gợi ý cho bạn” khi đã đăng nhập.

### Chủ trọ

- Đăng, chỉnh sửa, xóa và quản lý danh sách phòng của mình.
- Quản lý gallery tối đa 10 ảnh/phòng; chọn ảnh chính hoặc xóa ảnh.
- Tiếp nhận, chấp nhận hoặc từ chối lịch xem phòng.
- Trao đổi với người thuê qua hệ thống nhắn tin.

Ứng dụng hiện hỗ trợ ảnh JPG, JPEG, PNG và WEBP, tối đa 5 MB mỗi ảnh. Chức năng video chưa được triển khai.

### Quản trị viên

- Xem dashboard và thống kê theo năm.
- Xem chi tiết, khóa/mở khóa hoặc xóa người dùng.
- Quản lý phòng, trạng thái hiển thị và xóa phòng.
- Tạo, sửa và xóa tiện nghi.
- Theo dõi dữ liệu yêu thích, đánh giá và xóa đánh giá vi phạm.

## AI Search

Luồng xử lý:

```text
Natural Language Query
        │
        ▼
FastAPI /parse-search
        │
        ├── rule/regex
        ├── area classifier
        └── multi-label amenity model
        │
        ▼
structured filters
        │
        ▼
ASP.NET Core MVC ── EF Core ── SQL Server
```

API hiện trả các trường `minPrice`, `maxPrice`, `soNguoi`, `khuVuc`, `tienNghi`, `nearPlace` và `maxDistanceKm`.

- Giá, số người và bán kính được nhận diện chủ yếu bằng rule/regex.
- Khu vực được dự đoán bằng model phân loại có confidence threshold.
- Tiện nghi được dự đoán bằng model multi-label.
- Landmark dùng danh sách alias/rule; ASP.NET đối chiếu landmark active trong database.
- Khoảng cách từ phòng tới landmark được ASP.NET tính bằng công thức Haversine.

Parser là sự kết hợp giữa rule và ML, không phải toàn bộ đều là Machine Learning. Nếu FastAPI không sẵn sàng, trang web vẫn tải và người dùng vẫn có thể dùng bộ lọc truyền thống.

## Recommendation System

```text
Views + Favorites + Bookings + Ratings
                  │
                  ▼
         interactions.csv snapshot
                  │
                  ▼
   Optimized Content-Based Recommendation
                  │
                  ▼
              FastAPI
                  │
                  ▼
        ASP.NET “Gợi ý cho bạn”
```

Model runtime chính là **Content-Based Recommendation** đã tối ưu trọng số interaction và feature. Room vector kết hợp khu vực, tiện nghi, giá, diện tích và sức chứa; user profile được tổng hợp từ các tương tác có trọng số.

- Phòng ẩn và phòng đã xem có thể được loại khỏi candidate list.
- Mỗi gợi ý có tối đa ba lý do dựa trên feature thực, ví dụ tiện nghi, khu vực, mức giá hoặc sức chứa.
- Người dùng chưa có profile nhận popularity fallback với wording trung tính.
- Collaborative Filtering bằng implicit ALS đã được đánh giá như challenger nhưng có HitRate/NDCG thấp hơn, nên **không** được dùng làm runtime chính.
- Hệ thống hiện tại không được mô tả là Hybrid Recommendation.

## Map & Location

Trang chi tiết phòng dùng Leaflet 1.9.4 và OpenStreetMap để hiển thị phòng cùng các landmark lân cận. `GeoDistanceService` sử dụng Haversine, vì vậy khoảng cách là đường chim bay, không phải quãng đường giao thông.

> Tọa độ phòng hiện tại chủ yếu được mô phỏng phục vụ demo, không đại diện GPS chính xác của từng phòng.

Leaflet và map tiles được tải từ dịch vụ bên ngoài, vì vậy phần bản đồ cần kết nối Internet để hiển thị đầy đủ.

## Công nghệ sử dụng

| Thành phần | Công nghệ/phiên bản trong project |
|---|---|
| Web application | ASP.NET Core MVC, target framework .NET 9 |
| ORM | Entity Framework Core 9.0.20 |
| Authentication/Authorization | ASP.NET Core Identity 9.0.20, role-based authorization |
| Database | Microsoft SQL Server |
| UI | Razor Views, Bootstrap 5.3.3, jQuery 3.7.1 |
| Map | Leaflet 1.9.4, OpenStreetMap |
| AI API | Python, FastAPI, Uvicorn, Pydantic |
| Machine Learning | scikit-learn 1.6.1, NumPy, SciPy, joblib |
| Giao tiếp web–AI | HTTP/JSON qua typed `HttpClient` |

FastAPI, Uvicorn, Pydantic, joblib và httpx hiện chưa được pin version trong `requirements.txt`; scikit-learn được pin ở 1.6.1 để tương thích model artifact.

## Kiến trúc hệ thống

```text
Browser
   │
   ▼
ASP.NET Core MVC
   │
   ├── EF Core ───────────── SQL Server
   │
   └── HTTP/JSON ─────────── FastAPI
                                 │
                                 ├── AI Search
                                 └── Recommendation
```

- ASP.NET Core MVC xử lý UI, Identity, authorization và nghiệp vụ chính.
- SQL Server là nguồn dữ liệu nghiệp vụ chính.
- FastAPI nạp model/CSV snapshot khi khởi động và phục vụ inference; dịch vụ này không cập nhật database.

## Cấu trúc thư mục

```text
SmartRental/
├── Areas/                 # Razor Pages cho ASP.NET Identity
├── Controllers/           # MVC controllers
├── Models/                # Domain/EF Core entities
├── ViewModels/            # ViewModel và DTO cho web/AI
├── Services/              # AI HTTP clients và tính khoảng cách
├── Data/                  # DbContext, seed và dữ liệu geo
├── Views/                 # Razor Views
├── wwwroot/               # CSS, JS, thư viện và ảnh demo/upload
├── Migrations/            # EF Core migrations và model snapshot
├── AI/
│   ├── api/               # FastAPI, parser và API tests
│   ├── DATA/              # CSV snapshot và script export
│   ├── models/            # Model artifacts và metadata
│   ├── recommendation/    # Content-Based, Collaborative và tests
│   └── evaluation/        # Metrics, reports, tuning và demo tests
├── docs/                  # Tài liệu audit project
├── Properties/            # Launch profiles
├── Program.cs             # DI, middleware, route và seed startup
├── appsettings.json
├── appsettings.Development.json
├── SmartRental.csproj
├── run-dev.ps1
└── run-dev.bat
```

## Yêu cầu môi trường

- Windows với PowerShell cho script `run-dev.ps1`.
- .NET SDK hỗ trợ target `net9.0` (khuyến nghị .NET SDK 9 hoặc mới hơn; audit gần nhất dùng SDK 10.0.403).
- SQL Server local/accessible và Windows Authentication, hoặc connection string tương đương.
- Python 3.x và pip; project đã được kiểm tra với Python 3.13.7.
- Trình duyệt hiện đại. Google Chrome cần thiết nếu chạy browser E2E hiện có.
- Kết nối Internet cho Leaflet CDN/OpenStreetMap tiles.

## Cài đặt từ repository

```powershell
git clone https://github.com/anhhhnn/SmartRental01.git
cd SmartRental01\SmartRental
dotnet restore
```

### 1. Cấu hình database

Development config dùng database `SmartRentalDB`, SQL Server và Windows Authentication. Thay server name trong `appsettings.Development.json` cho phù hợp máy local. Ví dụ:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=SmartRentalDB;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "AiService": {
    "BaseUrl": "http://127.0.0.1:8000"
  }
}
```

Không commit connection string có username/password thật. Có thể override bằng .NET User Secrets hoặc environment variable `ConnectionStrings__DefaultConnection`.

### 2. Tạo database từ migration

Repository không có local tool manifest cho `dotnet-ef`. Nếu lệnh `dotnet ef` chưa có, cài CLI cùng major version với EF Core:

```powershell
dotnet tool install --global dotnet-ef --version 9.0.20
```

Sau đó, tại thư mục `SmartRental/`:

```powershell
dotnet ef database update
```

Lệnh này tạo/cập nhật schema từ các migration hiện có. Không cần commit file database local.

### 3. Seed và dữ liệu demo

Khi chạy bằng launch profile mặc định ở môi trường `Development`:

- `IdentitySeed` bảo đảm ba role `Admin`, `ChuTro`, `NguoiThue` tồn tại.
- `DiaDiemSeed`, `DemoDataSeed` và `RoomCoordinateSeeder` chạy tự động.
- Demo seed hướng tới 450 phòng, 24 tài khoản chủ trọ và 72 tài khoản người thuê, cùng lịch xem, tin nhắn, đánh giá, yêu thích và lịch sử xem.
- Seed kiểm tra trạng thái hiện có để tránh tạo lại toàn bộ dữ liệu mỗi lần chạy.

Có thể chạy riêng seed demo hoặc seed tọa độ; cả hai chỉ được phép trong `Development`:

```powershell
dotnet run -- --seed-demo-data-only
dotnet run -- --seed-room-coordinates-only
```

### 4. Cài Python dependencies

```powershell
cd AI\api
py -m pip install -r requirements.txt
cd ..\..
```

Trên môi trường không có Python Launcher `py`, dùng `python -m pip` thay thế. Không cần retrain model để chạy vì runtime artifacts đã có trong repository.

## Chạy ứng dụng

### Cách 1: chạy cả hai dịch vụ

Tại thư mục `SmartRental/`:

```powershell
.\run-dev.ps1
```

Hoặc chạy `run-dev.bat` từ File Explorer/Command Prompt.

### Cách 2: chạy thủ công

Terminal 1 — FastAPI:

```powershell
cd AI\api
py -m uvicorn main:app
```

Terminal 2 — ASP.NET Core, từ thư mục `SmartRental/`:

```powershell
dotnet run
```

URL web được hiển thị trong terminal khi ASP.NET Core khởi động. Launch profile hiện có URL local HTTP/HTTPS, nhưng không nên phụ thuộc vào port hard-code trong tài liệu hoặc khi deploy.

### Kiểm tra sau khi chạy

| Thành phần | Địa chỉ/kiểm tra |
|---|---|
| FastAPI health | <http://127.0.0.1:8000/health> |
| FastAPI Swagger | <http://127.0.0.1:8000/docs> |
| Web | URL do `dotnet run` in ra trong terminal |

Health endpoint báo riêng trạng thái AI Search và Recommendation. Trạng thái `degraded` cho biết một phần AI chưa sẵn sàng; web vẫn có cơ chế fallback.

## Demo Accounts

Các tài khoản dưới đây được `DemoDataSeed` tạo cố định và **chỉ dùng trong môi trường Development**:

| Role | Email | Password |
|---|---|---|
| Chủ trọ | `chutro1@smartrental.local` | `SmartRental.Demo!2026#` |
| Người thuê | `nguoithue1@smartrental.local` | `SmartRental.Demo!2026#` |

Seed hiện không tạo tài khoản Admin cố định. Không sử dụng lại mật khẩu demo trong môi trường thật.

## AI model artifacts

| Artifact | Vai trò | Runtime/experimental |
|---|---|---|
| `AI/models/area_model.pkl` | Phân loại khu vực trong AI Search | Runtime |
| `AI/models/amenity_model.pkl` | Dự đoán multi-label tiện nghi | Runtime |
| `AI/models/amenity_mlb.pkl` | Ánh xạ nhãn tiện nghi | Runtime |
| `AI/models/model_metadata.json` | Version, threshold và metadata huấn luyện | Runtime metadata |
| `AI/models/collaborative_model.npz` | Implicit ALS challenger | Experimental, không dùng runtime chính |

Chỉ load file pickle/joblib từ nguồn tin cậy. Runtime artifacts trong repository đủ để chạy inference mà không retrain.

## AI Evaluation

Tài liệu và metric chi tiết nằm trong [`SmartRental/AI/evaluation/`](SmartRental/AI/evaluation/):

- [AI Search final report](SmartRental/AI/evaluation/ai_search_final_report.md)
- [Recommendation final report](SmartRental/AI/evaluation/recommendation_final_report.md)
- [Browser E2E và responsive report](SmartRental/AI/evaluation/browser_e2e_report.md)
- [Search model metrics](SmartRental/AI/evaluation/search_model_metrics.json)
- [Content-Based optimized metrics](SmartRental/AI/evaluation/content_based_optimized_metrics.json)
- [Collaborative metrics](SmartRental/AI/evaluation/collaborative_metrics.json)
- [Demo queries](SmartRental/AI/evaluation/search_demo_queries.json)

### Kết quả AI Search

| Thành phần | Kết quả lưu trong artifact |
|---|---:|
| Area classifier | Accuracy 0,933333 trên 60 mẫu tham chiếu |
| Amenity classifier | Micro-F1 1,0; Macro-F1 1,0 trên 287 mẫu test |
| Parser demo | 23/23 câu declared-supported pass; 3 câu được ghi rõ là unsupported |

Amenity evaluation dùng 1.812 câu synthetic được sinh theo tổ hợp tiện nghi; F1 1,0 không đại diện cho truy vấn người dùng thật. Area model là model pre-trained và chưa xác định được mức overlap giữa nguồn train với `rooms.csv`, nên accuracy chỉ là bằng chứng kỹ thuật, không phải production benchmark.

### Kết quả Recommendation

| Model | HitRate@10 | NDCG@10 |
|---|---:|---:|
| Content-Based baseline | 0,283784 | 0,032274 |
| Content-Based optimized | **0,337838** | **0,038274** |
| Collaborative challenger | 0,246575 | 0,026558 |

Content-Based optimized được chọn vì tốt hơn challenger ở các metric ranking chính và tạo explanation trực tiếp từ feature. Collaborative có catalogue coverage cao hơn nhưng không được dùng làm runtime chính.

## Testing

Kết quả audit gần nhất:

- `dotnet build`: 0 error, 0 warning.
- FastAPI/search/recommendation API tests: 32/32 pass.
- Recommendation unit tests: 30/30 pass.
- Search/recommendation demo tests: 5/5 pass.
- Tổng các test Python không trùng trong lần audit: **67/67 pass**.
- Browser E2E riêng đã pass các luồng AI Search, Recommendation, fallback khi FastAPI dừng và responsive ở 1440, 1024, 768, 390 px.

Build:

```powershell
dotnet build
```

Chạy các nhóm Python test từ thư mục `SmartRental/`:

```powershell
Push-Location AI\api
py -m unittest discover -v -s . -p "test_*.py"
Pop-Location

Push-Location AI
py -m unittest discover -v -s recommendation -p "test_*.py"
Pop-Location

Push-Location AI\evaluation
py -m unittest -v test_search_demo.py test_recommendation_demo.py
Pop-Location
```

Browser E2E là kiểm tra tùy chọn, cần Google Chrome và Selenium; xem script [`browser_e2e.py`](SmartRental/AI/evaluation/browser_e2e.py) và report tương ứng. Project hiện chưa có C# test project tự động cho controller/EF workflow.

## Limitations

- 72/74 user và 3.726/3.740 interaction trong snapshot Recommendation là dữ liệu demo; chỉ có hai user non-demo.
- Recommendation metrics hiện tại không đại diện cho production hoặc hành vi người dùng thật.
- Interaction CSV chưa có timestamp nên chưa thể đánh giá bằng chronological split.
- Runtime Recommendation đọc snapshot CSV khi FastAPI khởi động; thay đổi database không tự cập nhật model/profile theo thời gian thực.
- Amenity model được đánh giá trên synthetic dataset; area evaluation có nguy cơ overlap nguồn train chưa xác định.
- AI Search vẫn phụ thuộc một số cách viết và dấu tiếng Việt; một số cú pháp không dấu chưa được hỗ trợ.
- Tọa độ phòng chủ yếu là simulated; Haversine chỉ tính khoảng cách đường chim bay.
- Browser map phụ thuộc Leaflet CDN và OpenStreetMap tiles.
- Media đang lưu trực tiếp trong `wwwroot/uploads`, chưa dùng object storage.
- Chưa có realtime training, model monitoring hoặc quy trình production deployment hoàn chỉnh.

## Hướng phát triển

- Thu thập interaction thật kèm timestamp và đánh giá theo thời gian.
- Tự động refresh snapshot/recommendation an toàn khi dữ liệu thay đổi.
- Chỉ xem xét Hybrid Recommendation khi có đủ user/item interactions thật.
- Dùng tọa độ đã xác minh và bổ sung khoảng cách theo tuyến đường nếu cần.
- Bổ sung model/data monitoring, versioning và kiểm thử C# cho workflow quan trọng.
- Tách media demo khỏi upload runtime và chuyển media production sang object storage.
- Hoàn thiện secrets management, CI/CD và cấu hình deployment production.

## Security notes

- Không commit API key, access token, mật khẩu database hoặc production connection string.
- Connection string trong `appsettings.Development.json` chỉ dành cho local development; nên override bằng User Secrets/environment variables.
- Demo credentials ở trên là dữ liệu công khai phục vụ Development, không phải tài khoản thật và không được tái sử dụng khi deploy.
- Model pickle/joblib có thể thực thi payload khi deserialize; chỉ dùng artifact từ repository/nguồn đã kiểm soát.

## Tài liệu bổ sung

- [Project structure & GitHub audit](SmartRental/docs/PROJECT_STRUCTURE_GITHUB_AUDIT.md)
- [AI Search final report](SmartRental/AI/evaluation/ai_search_final_report.md)
- [Recommendation final report](SmartRental/AI/evaluation/recommendation_final_report.md)

SmartRental hiện phù hợp cho mục đích đồ án và demo có kiểm soát. Các metric và kết quả trong README phản ánh artifact/test tại thời điểm audit, không phải cam kết hệ thống đã production-ready.
