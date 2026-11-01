# Báo cáo audit cấu trúc project và Git/GitHub — SmartRental

Ngày audit: 01/11/2026  
Phạm vi: `D:\aspc#\git_projec\repo\SmartRental`  
Git worktree thực tế: `D:\aspc#\git_projec\repo` (project nằm trong subfolder `SmartRental/`)  
Nguyên tắc: chỉ đánh giá; không sửa logic, database, migration, UI, model AI hoặc lịch sử Git.

## Kết luận nhanh

Project có nền tảng hợp lý cho một đồ án ASP.NET Core MVC và hiện build/test tốt. Cấu trúc chính dễ nhận biết, service AI được tách khỏi EF/business persistence, model/dataset nhỏ, GitHub đã đồng bộ và chưa phát hiện secret production. Tuy nhiên repo **chưa nên gửi giảng viên ngay** vì README gần như trống, `PhongtroController` quá lớn, Account/Profile bị trùng, AI còn artifact đặt sai chỗ, requirements chưa đủ để tái tạo môi trường, media upload cần phân loại và lịch sử migration có các bản sửa thủ công cần được xác minh bằng một database mới.

Đánh giá tổng thể: **7,0/10 — dùng được và chạy tốt, nhưng cần một lượt dọn tài liệu/cấu trúc có kiểm soát trước khi bàn giao.**

## 1. Tổng quan cấu trúc hiện tại

Repo Git nằm ở thư mục cha, gồm `.gitignore`, README hai dòng và subfolder project `SmartRental/`. Project có 285 file được track. Các khu vực chính:

```text
repo/
├── .gitignore
├── README.md
└── SmartRental/
    ├── AI/
    ├── Areas/
    ├── Controllers/
    ├── Data/
    ├── Migrations/
    ├── Models/
    ├── Properties/
    ├── Security/
    ├── Services/
    ├── ViewModels/
    ├── Views/
    ├── wwwroot/
    ├── Program.cs
    ├── SmartRental.csproj
    ├── SmartRental.sln
    ├── appsettings*.json
    └── run-dev.*
```

Các thư mục local/generated đang tồn tại nhưng không được Git track: `.vs/`, `bin/`, `obj/`, các `AI/**/__pycache__/` và `SmartRental.csproj.user`. Ngoài ra có hai thư mục local rỗng là `.github/` và `New folder/`; vì Git không lưu thư mục rỗng nên chúng không xuất hiện trên GitHub.

## 2. Đánh giá từng folder

| Folder | Vai trò hiện tại | Hợp lý? | Vấn đề | Đề xuất |
|---|---|---:|---|---|
| `Controllers/` | Điều phối MVC, truy vấn EF, CRUD phòng, tài khoản, lịch xem, tin nhắn | ⚠️ | `PhongtroController` khoảng 1.500 dòng; Account/Profile trùng; `ThongBaoController` dồn cả class vào một dòng | Giữ cấu trúc hiện tại trước mắt; sau bàn giao ưu tiên hợp nhất Profile và tách nghiệp vụ/media/search khỏi `PhongtroController` |
| `Models/` | EF/domain entities và `ErrorViewModel` mặc định MVC | ✅/⚠️ | Chủ yếu đúng; `ErrorViewModel` là ViewModel nằm cùng entity nhưng đây là convention thường gặp | Không cần di chuyển chỉ để “đẹp folder”; có thể chuyển `ErrorViewModel` khi có đợt chuẩn hóa thực sự |
| `ViewModels/` | DTO/ViewModel cho Account, Admin, Profile, AI search, recommendation | ⚠️ | Hai bộ Profile/EditProfile/ChangePassword gần trùng ở `Account/` và `Profile/`; cách nhóm folder chưa nhất quán | Chọn một luồng Account hoặc Profile rồi xóa/hợp nhất bộ còn lại sau khi kiểm tra route/view |
| `Services/` | HTTP client cho AI search/recommendation và tính khoảng cách | ✅ | Tên rõ nghĩa, nhỏ, không lẫn `ApplicationDbContext`/`SaveChanges` | Giữ; khi refactor có thể thêm interface nếu cần test/mocking, không bắt buộc cho quy mô hiện tại |
| `Data/` | DbContext, seed Identity/demo/địa điểm/tọa độ, helper geo | ⚠️ | `DemoDataSeed` rất lớn; seed và geo helper cùng cấp; tên Việt/Anh trộn nhưng vẫn hiểu được | Chỉ nên nhóm `Data/Seed/` và `Data/Geo/` trong một đợt riêng, có cập nhật namespace đầy đủ |
| `Views/` | 37 Razor views theo controller | ✅ | Cấu trúc thư mục khớp controller; tồn tại cả `Account/` và `Profile/` phản ánh phần controller trùng | Sau khi chọn luồng profile, giữ một bộ view duy nhất |
| `wwwroot/` | CSS/JS/vendor và 48 ảnh upload/demo | ⚠️ | Vendor chiếm ~9,33 MB; uploads ~3,72 MB; media demo và upload tên hash trộn chung | Giữ bộ ảnh demo tối thiểu; review ảnh hash theo DB trước khi loại; cân nhắc mô tả chính sách uploads trong README |
| `Migrations/` | 13 mốc migration cùng designer/snapshot | ⚠️ | Có migration sửa thủ công/idempotent cho `YeuThichs`; một số designer tối giản; cần kiểm tra từ DB trắng | Không xóa/rewrite; chạy thử `database update` trên DB mới trước khi bàn giao |
| `AI/api/` | FastAPI, parser/search/recommendation endpoints và test API | ✅/⚠️ | `test_model.py` mang tính script kiểm tra thủ công; cache local có mặt nhưng đã ignore | Giữ API; chuyển script thủ công sang evaluation/tests nếu chuẩn hóa |
| `AI/DATA/` | CSV search/recommendation và script export | ⚠️ | Folder viết hoa không theo snake/lowercase Python; `rooms.csv` có vẻ là export cũ dạng tab và không thấy consumer | Đổi thành `AI/data/` ở đợt có kiểm tra import/path; xác nhận rồi loại hoặc tài liệu hóa `rooms.csv` |
| `AI/models/` | Model runtime `.pkl/.npz` và metadata | ⚠️ | Có file báo cáo `.pptx` 6,47 MB đặt sai chỗ; `area_model.pkl` chưa thấy producer trong source hiện tại | Chỉ giữ model runtime/metadata; chuyển presentation sang `docs/reports/` hoặc `docs/presentations/` |
| `AI/evaluation/` | Scripts, metrics, tuning results và report | ✅ | Nhiều output nhưng dung lượng nhỏ, có giá trị chứng minh; browser E2E phụ thuộc Selenium chưa khai báo | Giữ report/metrics quan trọng; ghi lệnh tái tạo và bổ sung dependency phù hợp |
| `AI/recommendation/` | Content-based, collaborative, service và unit tests | ✅ | Trách nhiệm rõ; test nằm cạnh code là chấp nhận được ở quy mô đồ án | Giữ; có thể chuyển tests sang `AI/tests/` sau này nhưng không cấp thiết |
| `Areas/Identity/` | Razor Pages đăng nhập/đăng ký | ✅ | Đúng convention ASP.NET Core Identity | Giữ |
| `Security/` | Hằng số role | ✅ | Nhỏ nhưng đúng trách nhiệm | Giữ |
| `Properties/` | Launch profile local | ✅ | Chỉ chứa localhost/dev endpoint | Giữ |
| `docs/` | Báo cáo audit này | ✅ | Trước audit chưa có tài liệu dự án | Mở rộng có chọn lọc cho architecture/report/diagram, không biến thành kho file draft |

## 3. Điểm tốt

- Phân khu ASP.NET Core MVC cơ bản đúng convention: Controllers, Models, Views, ViewModels, Services, Data, Migrations, Areas.
- `AiSearchService`, `RecommendationService`, `GeoDistanceService` có tên và trách nhiệm rõ; không truy cập EF trực tiếp.
- AI đã tách `api`, `models`, `evaluation`, `recommendation` và data; test Python có độ phủ chức năng đáng kể.
- `run-dev.ps1` dùng đường dẫn tương đối dựa trên vị trí script, không hard-code đường dẫn máy cá nhân.
- Connection string dùng Windows Integrated Security (`Trusted_Connection=True`), không chứa mật khẩu SQL.
- Model/dataset nhỏ; không có file vượt 10 MB, 50 MB hoặc 100 MB và hiện chưa cần Git LFS.
- Không có tracked `bin/`, `obj/`, `.vs/`, virtualenv, cache Python hoặc database file.
- Git working tree sạch tại thời điểm bắt đầu audit; diff rỗng và `git diff --check` không báo whitespace error.
- Sau `git fetch origin`, local và `origin/main` có chênh lệch `0 ahead / 0 behind`.
- Build và toàn bộ bộ test Python được chạy trong audit đều pass.

## 4. Điểm chưa hợp lý

1. `PhongtroController` đang gánh search/filter, recommendation, favorites, reviews, recently viewed, CRUD phòng, amenities và lưu/xóa ảnh. Đây là điểm nợ kỹ thuật lớn nhất, dù chưa cần refactor trước buổi nộp nếu hệ thống ổn định.
2. `AccountController` và `ProfileController` thực hiện gần cùng luồng xem/sửa profile và đổi mật khẩu; ViewModel và view cũng bị nhân đôi.
3. `ThongBaoController.cs` viết dồn một dòng, khó review/diff dù logic chưa lớn.
4. `Data/` trộn DbContext, seed và geo helper; `DemoDataSeed` dài khoảng 880 dòng có nội dung sinh demo khá lớn.
5. README ở Git root chỉ có tên và một câu mô tả; chưa đủ để người khác clone và chạy.
6. `AI/models/Bao_cao_thuc_tap_ViDucDoan.pptx` không phải model và đặt sai chỗ.
7. `AI/api/requirements.txt` chỉ pin `scikit-learn`; các dependency trực tiếp như NumPy/SciPy (được import trực tiếp), FastAPI/Uvicorn/Pydantic/httpx/joblib chưa pin; Selenium dùng cho browser E2E chưa được khai báo.
8. Không có metadata phiên bản Python như `.python-version` hoặc hướng dẫn rõ. Máy audit dùng Python 3.13.7, trong khi pickle chỉ ghi scikit-learn 1.6.1.
9. Có artifact/cache local và thư mục rỗng làm workspace nhìn chưa sạch dù chúng không lên Git.
10. Commit message hiện tại quá chung chung, nhiều lỗi chính tả và không mô tả phạm vi thay đổi.

## 5. Controllers

### Kích thước và trách nhiệm

- `PhongtroController.cs`: lớn nhất, khoảng 1.500 dòng vật lý; chứa quá nhiều nhóm nghiệp vụ và file I/O. Nên ưu tiên tách ở giai đoạn sau thành service/query/media handler hoặc các controller có ranh giới rõ, nhưng không nên refactor gấp trước khi nộp nếu không có đủ thời gian regression test.
- `AdminController.cs`: khoảng 500 dòng, quản lý dashboard/users/rooms/amenities/favorites/reviews. Lớn nhưng các action đều thuộc admin; mức ưu tiên thấp hơn `PhongtroController`.
- `LichXemPhongController.cs`: khoảng 236 dòng, hợp lý theo một feature.
- Các controller còn lại nhỏ.

### Overlap Account/Profile

`AccountController` và `ProfileController` đều có xem profile, edit profile và change password, cùng hai namespace ViewModel gần giống nhau. Có khác biệt route/tên TempData/validation length nên không được xóa một bên ngay. Cần xác định route/view nào thực sự đang được navbar và người dùng sử dụng, sau đó hợp nhất có test.

### Business logic trong controller

`PhongtroController` chứa logic tìm kiếm/filter dài, tổng hợp recommendation, validate/lưu/xóa file ảnh, update favorites/reviews và lịch sử xem. Các phần đáng tách nhất là media storage và query/search orchestration. Các controller còn lại truy cập DbContext trực tiếp theo kiểu MVC nhỏ; chưa cần ép thêm repository/service chỉ vì hình thức.

Không phát hiện controller test/debug riêng.

## 6. Models và ViewModels

- `Models/` chủ yếu là EF entities đúng vai trò: `ApplicationUser`, `Phongtro`, `DanhGia`, `YeuThich`, messaging, booking, image, amenity và location.
- Không thấy EF entity đặt trong `ViewModels/`.
- Các request/response cho AI và recommendation nằm trong `ViewModels/`, phù hợp với vai trò DTO trình bày/giao tiếp.
- `ErrorViewModel` nằm trong `Models/` là convention từ template MVC, không phải lỗi nghiêm trọng.
- Vấn đề thật sự là hai bộ ViewModel Profile/Account trùng nhau, thậm chí có khác biệt nhỏ về collection type và giới hạn địa chỉ 250/300 ký tự, dễ tạo hành vi không nhất quán.

## 7. Services

| Service | Vai trò | Đánh giá |
|---|---|---|
| `AiSearchService` | Gọi FastAPI để parse truy vấn tìm kiếm | Rõ trách nhiệm; có timeout/logging qua typed `HttpClient` |
| `RecommendationService` | Gọi endpoint recommendation | Rõ trách nhiệm; không lẫn EF/business persistence |
| `GeoDistanceService` | Tính khoảng cách địa lý | Nhỏ, thuần tính toán, phù hợp singleton |

Không thấy duplicate logic đáng kể giữa ba service. Phần duplicate chủ yếu nằm ở hai controller Account/Profile và logic media/search trong `PhongtroController`, không nằm trong service AI.

## 8. Data và seed

- `ApplicationDbContext` ở đúng vị trí.
- `IdentitySeed`, `DiaDiemSeed`, `DemoDataSeed`, `RoomCoordinateSeeder` đều là seed; nhóm vào `Data/Seed/` sẽ giúp đọc nhanh hơn.
- `GeoAreaCenters`, `GeoCoordinateGenerator`, `ThaiNguyenAreas` là helper/config geo; có thể nhóm `Data/Geo/`.
- Seed demo chỉ chạy tự động trong Development, ngoại trừ role seed được gọi ở mọi environment. Đây là chủ ý có thể chấp nhận nhưng cần mô tả trong README.
- Seed và browser E2E có credential demo cố định. Không coi đây là secret production, nhưng không được tái sử dụng credential này khi deploy thật; README nên ghi rõ chỉ dành cho demo/development.
- `DemoDataSeed` phụ thuộc bộ ảnh demo trong `wwwroot/uploads`, vì vậy không được xóa toàn bộ uploads.

## 9. AI, dataset và model artifacts

### Dataset

| File | Quy mô | Vai trò/đánh giá | Commit? |
|---|---:|---|---:|
| `amenity_search_queries.csv` | 1.812 dòng, ~217 KB | Dataset nhỏ, giúp tái lập search model | Nên |
| `interactions.csv` | 3.740 dòng, ~187 KB | Recommendation interactions; 74 UserId đều GUID, không có email | Nên, nếu xác nhận là demo/synthetic |
| `rooms_recommendation.csv` | 450 dòng, ~45 KB | Runtime/evaluation recommendation đang dùng | Nên |
| `rooms.csv` | 238 dòng, ~105 KB | Header thực tế phân tách bằng tab; không thấy code hiện tại tham chiếu | Cần review; có thể là export cũ/duplicate |
| Metrics/config/report JSON/MD | Mỗi file nhỏ | Bằng chứng đánh giá và cấu hình reproducible | Nên giữ các bản cuối/được tham chiếu |

`interactions.csv` không chứa email theo kiểm tra header/value. GUID nội bộ vẫn là identifier; trước khi public cần xác nhận dữ liệu được seed/synthetic, không map tới người dùng thật. Report `interactions_report.md` nên ghi rõ nguồn dữ liệu.

### Model

| File | Kích thước | Vai trò | Tái tạo/commit |
|---|---:|---|---|
| `amenity_mlb.pkl` | 892 B | Label binarizer search | Script evaluation có ghi lại; nên commit |
| `amenity_model.pkl` | ~99,5 KB | Model nhận diện tiện nghi | Có producer trong `evaluate_search_models.py`; nên commit |
| `area_model.pkl` | ~67,3 KB | Model nhận diện khu vực, API load trực tiếp | Chưa thấy producer trong source hiện tại; cần bổ sung hướng dẫn/script trước khi coi là reproducible |
| `collaborative_model.npz` | ~262,8 KB | Artifact collaborative recommendation/evaluation | Có logic lưu model; nhỏ, có thể commit |
| `model_metadata.json` | 738 B | Ghi scikit-learn 1.6.1, seed, threshold | Nên commit |

Không file model nào đủ lớn để cần Git LFS. Pickle/joblib chỉ nên load từ repo tin cậy; README nên nêu yêu cầu phiên bản scikit-learn 1.6.1. `Bao_cao_thuc_tap_ViDucDoan.pptx` 6,47 MB nên chuyển khỏi `AI/models` sang docs, không cần LFS ở kích thước hiện tại.

### Temp/test artifact

- `__pycache__/*.pyc` đang tồn tại local trong nhiều folder AI, đã được ignore và không track.
- Không thấy notebook, `.pytest_cache`, virtualenv hoặc dataset >10 MB.
- `AI/api/test_model.py` là script in kết quả thủ công hơn là test assertion; nên đặt trong evaluation/tools hoặc đổi thành test có assertion khi chuẩn hóa.

## 10. File generated/temp và `.gitignore`

`.gitignore` nằm ở Git root `repo/.gitignore`, không nằm trong `SmartRental/`; cách này đúng vì nó áp dụng cho toàn repo.

### Rule đang tốt

- `bin/`, `obj/`, `.vs/`, `*.user`, `*.suo`, `*.log`, `*.tmp`
- `__pycache__/`, `*.pyc`
- `.env`
- Visual Studio, Rider, VS Code local files và SQL Server `*.mdf/*.ldf`

### Rule còn thiếu hoặc nên cân nhắc

- `.pytest_cache/`
- `.venv/`, `venv/`, `env/`
- `coverage/`, `htmlcov/`, `.coverage`, `coverage.xml` theo công cụ Python sử dụng
- Có thể cân nhắc `*.db`, `*.sqlite`, `*.bak` nếu project không chủ ý version các fixture database
- Nếu runtime upload không được version: rule riêng cho `SmartRental/wwwroot/uploads/*` kèm ngoại lệ cho bộ demo; chỉ làm sau khi tách demo/user media rõ ràng

Không thấy rule nào hiện tại ignore nhầm source/model/dataset cần thiết. `.vscode` đang cho phép một số file cấu hình chung, đây là lựa chọn hợp lý.

### File/folder local có thể xóa an toàn sau khi đóng ứng dụng

- `.vs/`, `bin/`, `obj/`, `SmartRental.csproj.user`
- Mọi `AI/**/__pycache__/`
- `New folder/` vì rỗng
- `.github/` chỉ nên xóa nếu không định thêm workflow/template; hiện rỗng

Các mục trên đều tái tạo được hoặc rỗng, nhưng audit này không xóa.

## 11. Secrets, config và hard-coded path

### Kết quả secret scan

- Không phát hiện API key, GitHub PAT, bearer token, production password hoặc connection string chứa SQL username/password.
- `appsettings.Development.json` có connection string local SQL Server dùng `Trusted_Connection=True`; đây không phải password secret.
- Có credential demo cố định trong seed/browser E2E. Chỉ chấp nhận nếu dữ liệu và môi trường là demo; phải thay/disable khi deploy thật.
- Các hit `Token` trong model pickle là binary/model vocabulary false positive, không phải access token.

### Config

- `appsettings.json` chỉ có logging/AllowedHosts, sạch.
- `appsettings.Development.json` chứa `Server=ADMIN-PC`, là tên máy local hard-code và làm giảm tính portable. Nên hướng dẫn override bằng User Secrets/environment variable hoặc dùng instance chuẩn trong README.
- `AiService.BaseUrl=http://127.0.0.1:8000` là config development hợp lý.
- `Program.cs` còn fallback `127.0.0.1:8000`; phù hợp chạy local nhưng production nên bắt buộc cấu hình rõ.
- `launchSettings.json` dùng localhost, đúng cho development.
- `run-dev.ps1/.bat` dùng relative path; không có `D:\aspc#\` hay `C:\Users\Admin\` trong source.
- `AI/evaluation/browser_e2e.py` dùng URL local cố định, chấp nhận được cho script development nhưng nên cho phép CLI/env override.

## 12. Upload/media

- Tổng cộng 48 file, ~3,72 MB; không có video và không file riêng lẻ lớn.
- Khoảng 24 file tên mô tả là bộ ảnh demo; `DemoDataSeed` tham chiếu các ảnh này nên cần giữ để clone mới có thể seed/chạy offline.
- Khoảng 24 file tên hash/UUID có dấu hiệu là upload runtime. Ít nhất 4 cặp có nội dung SHA-256 trùng nhau.
- Không thể kết luận orphan chỉ từ source vì quan hệ nằm trong database. Trước khi xóa, cần đối chiếu `Phongtro.HinhAnh` và `PhongtroHinhAnhs` trên DB demo.
- Hướng chuyên nghiệp hơn: tách `wwwroot/demo-images/` (tracked) khỏi `wwwroot/uploads/` runtime (ignored). Chỉ thực hiện sau khi sửa seed/path có kiểm thử; không làm trong audit.

## 13. Database artifacts và migrations

### Database artifacts

Không có tracked `.mdf`, `.ldf`, `.bak`, `.db` hoặc `.sqlite`. Các `.db/.sqlite/.suo` tìm thấy chỉ nằm trong `.vs/` và đã ignore. Đây là trạng thái tốt.

### Migration

- Thứ tự timestamp nhìn chung tăng hợp lý từ InitialCreate đến room coordinates/landmarks.
- `AddYeuThich` không có designer metadata; migration sau `CreateYeuThichTable` dùng SQL idempotent để sửa tình trạng EF không discover migration trước. Đây là lịch sử bất thường nhưng có giải thích trong code; không xóa vì DB hiện tại có thể phụ thuộc.
- `CreateYeuThichTable.Down()` cố ý không drop table để bảo toàn dữ liệu, nên rollback không hoàn toàn đối xứng.
- `AddDanhGia.Designer.cs` là metadata tối giản thay vì designer đầy đủ; vẫn có `[Migration]`, nhưng cần test migration discovery.
- Snapshot hiện có và build được, nhưng audit không chạy `database update` vì thao tác đó làm thay đổi database.

Khuyến nghị bắt buộc trước bàn giao: tạo một database development trắng riêng, chạy toàn bộ migration, sau đó seed và smoke test. Không squash/xóa migration khi chưa xác nhận database đích.

## 14. README và tài liệu

README hiện nằm ở Git root và chỉ gồm:

- tên `SmartRental01`
- một câu mô tả ASP.NET Core MVC + AI

Đây là thiếu sót lớn nhất về khả năng bàn giao. README cần ít nhất:

1. Tên/mục tiêu đề tài và ảnh minh họa.
2. Stack: .NET 9, EF Core 9, SQL Server, Identity, Python/FastAPI, scikit-learn.
3. Chức năng: search/filter, AI Search, recommendation, map/distance, booking/viewing, messaging, review/favorite, admin.
4. Prerequisite và phiên bản .NET/Python/SQL Server.
5. Database setup, migration, seed và cách override connection string.
6. Python environment, lệnh cài requirements và lệnh chạy API.
7. Cách chạy một lệnh bằng `run-dev.ps1`/`.bat`.
8. Tài khoản demo và cảnh báo credential chỉ dành cho development.
9. Lệnh chạy test.
10. Cấu trúc folder, model/dataset cần thiết, limitations và troubleshooting.

Ngoài presentation đang đặt trong `AI/models`, trước audit không có docs/report ở cấp project. Các report AI `.md/.json` trong `AI/evaluation` nên giữ tại đó vì gắn trực tiếp với experiment; tài liệu đồ án/presentation tổng quát nên chuyển sang `docs/reports/` hoặc `docs/presentations/`.

## 15. Git status, history, branch và remote

### Status/diff tại thời điểm bắt đầu audit

- Branch: `main`
- Tracking: `origin/main`
- Working tree: sạch
- Modified/untracked/deleted: không có
- `git diff --stat`: rỗng
- `git diff --check`: không lỗi whitespace

Sau khi tạo báo cáo theo yêu cầu, `docs/PROJECT_STRUCTURE_GITHUB_AUDIT.md` sẽ là file untracked duy nhất cần review/commit.

### History

20 commit gần nhất thực tế chỉ có 9 commit. Lịch sử thể hiện được tiến độ lớn (initial, web demo, database, UI, function, AI search/recommendation), nhưng message thiếu chuyên nghiệp:

- `update ai recommen`
- `update AI search`
- `update button arange`
- `update the funcion website`
- `update database`
- `update giaodien`

Không cần rewrite history trước khi nộp. Từ commit kế tiếp nên dùng message cụ thể, ví dụ `docs: add setup and project audit` hoặc `fix(profile): remove duplicate account flow`.

### Branch/remote/sync

- Chỉ thấy local branch `main`; không có branch local cũ cần dọn.
- Remote: `origin` dùng HTTPS, trỏ tới `https://github.com/anhhhnn/SmartRental01.git`.
- Tên remote repo là `SmartRental01`, không trùng tuyệt đối tên folder `SmartRental` nhưng URL có vẻ đúng dự án; chủ repo cần xác nhận đây là repo muốn gửi.
- `git fetch origin` thành công ngày audit.
- `HEAD` và `origin/main` cùng commit `ee82945`; `0 ahead / 0 behind`.

## 16. Build, test và reproducibility

### Kết quả xác minh

```text
dotnet build
Build succeeded.
0 Warning(s)
0 Error(s)
```

Python trên máy audit: 3.13.7. Package runtime hiện cài được và scikit-learn đúng 1.6.1.

| Bộ test | Kết quả |
|---|---:|
| FastAPI/search/recommendation API | 32/32 pass |
| Recommendation unit tests | 30/30 pass |
| Search/recommendation demo tests | 5/5 pass |
| Tổng | **67/67 pass** |

Có `StarletteDeprecationWarning` về FastAPI TestClient dùng `httpx`; không làm test fail nhưng nên xử lý khi cập nhật dependency. Project chưa có C# test project, nên luồng MVC/EF/controller chưa có automated regression test.

### Reproducibility

Mức hiện tại: **khá nhưng chưa hoàn chỉnh**.

- Tốt: có requirements, model runtime, metadata, dataset, config, report, seed, migration và script run dev.
- Thiếu: README setup, Python version metadata, pin đầy đủ dependency, Selenium cho browser E2E, hướng dẫn tái tạo `area_model.pkl`, xác minh migration trên DB trắng.

## 17. File nên giữ, di chuyển, ignore hoặc review xóa

### Nên giữ

- Toàn bộ C#/Razor source, migrations và snapshot hiện tại.
- Ba service, ViewModel AI/recommendation, AI runtime code.
- Model `.pkl/.npz`, metadata, dataset nhỏ được runtime/training tham chiếu.
- Metrics/config/final report AI có giá trị chứng minh.
- Bộ ảnh demo được `DemoDataSeed` tham chiếu.
- `run-dev.ps1`, `run-dev.bat`, launch settings và `.gitignore` root.

### Nên di chuyển sau khi xác nhận

- `AI/models/Bao_cao_thuc_tap_ViDucDoan.pptx` → `docs/presentations/` hoặc `docs/reports/`.
- Seed files → `Data/Seed/`; geo files → `Data/Geo/` nếu thực hiện một đợt namespace-safe.
- Có thể tách demo images khỏi runtime uploads.
- Có thể chuyển `AI/api/test_model.py` sang evaluation/tools.

### Nên ignore bổ sung

- `.pytest_cache/`, `.venv/`, `venv/`, `env/`, Python coverage output.
- Runtime uploads sau khi đã tách bộ demo.

### Có thể xóa sau khi xác nhận

- Cache/generated local: `.vs`, `bin`, `obj`, `*.user`, `__pycache__`.
- Hai folder local rỗng `New folder/`, `.github/` nếu không dùng.
- Các file upload hash trùng/orphan sau khi đối chiếu DB.
- `AI/DATA/rooms.csv` nếu xác nhận là export cũ không còn consumer.
- Tuning/intermediate report cũ nếu có bản final bao phủ hoàn toàn; nên giữ ít nhất config, metrics cuối và báo cáo cuối.

## 18. Cấu trúc đề xuất cuối

Không cần tách solution thành nhiều project/module ở quy mô hiện tại. Cấu trúc gọn hợp lý sau một đợt dọn có kiểm thử:

```text
repo/
├── .gitignore
├── README.md
└── SmartRental/
    ├── Areas/
    ├── Controllers/
    ├── Data/
    │   ├── ApplicationDbContext.cs
    │   ├── Geo/
    │   └── Seed/
    ├── Models/
    ├── ViewModels/
    │   ├── Account/              # chỉ giữ một luồng Account/Profile
    │   ├── Admin/
    │   └── Messages/
    ├── Services/
    ├── Views/
    ├── wwwroot/
    │   ├── css/
    │   ├── js/
    │   ├── lib/
    │   ├── demo-images/          # tracked
    │   └── uploads/              # runtime, ignored
    ├── Migrations/
    ├── AI/
    │   ├── api/
    │   ├── data/
    │   ├── models/
    │   ├── recommendation/
    │   └── evaluation/
    ├── docs/
    │   ├── architecture/
    │   ├── diagrams/
    │   ├── reports/
    │   └── presentations/
    ├── Properties/
    ├── Security/
    ├── Program.cs
    ├── appsettings.json
    ├── appsettings.Development.json
    ├── SmartRental.csproj
    ├── SmartRental.sln
    ├── run-dev.ps1
    └── run-dev.bat
```

Đây là hướng đề xuất, không phải checklist phải làm hết trước khi nộp. Việc có giá trị cao hơn “đẹp folder” là README, reproducible setup, kiểm tra migration sạch, phân loại uploads và loại duplicate Account/Profile.

## 19. Chấm mức sẵn sàng gửi giảng viên

| Tiêu chí | Mức | Nhận xét |
|---|---|---|
| 1. Folder structure | ⚠️ Nên chỉnh | Đúng khung MVC, nhưng controller/data/AI artifact còn vài điểm lộn xộn |
| 2. Naming | ⚠️ Nên chỉnh | Domain Việt/Anh chấp nhận được; `DATA`, typo commit và một số tên chưa nhất quán |
| 3. Source cleanliness | ⚠️ Nên chỉnh | Generated đã ignore, nhưng workspace còn cache/folder rỗng và controller một dòng/quá lớn |
| 4. Git history | ⚠️ Nên chỉnh | Tiến độ thấy được nhưng message yếu |
| 5. GitHub sync | ✅ Tốt | Fetch thành công, 0 ahead/behind trước report |
| 6. README | ❌ Cần sửa trước khi gửi | Chỉ hai dòng, không thể setup từ clone mới |
| 7. Secrets | ✅/⚠️ | Không có production secret; credential demo cần ghi rõ và không dùng production |
| 8. AI artifacts | ⚠️ Nên chỉnh | Runtime sạch/nhỏ; presentation đặt sai, area model chưa rõ cách tái tạo |
| 9. Dataset | ✅/⚠️ | Nhỏ, không thấy email; cần xác nhận synthetic và review `rooms.csv` |
| 10. Upload media | ⚠️ Nên chỉnh | Có bộ demo cần giữ, nhưng runtime/hash/duplicate trộn chung |
| 11. Migrations | ⚠️ Nên chỉnh | Có repair migration thủ công; cần thử trên DB trắng |
| 12. Build | ✅ Tốt | 0 warning, 0 error |
| 13. Tests | ✅/⚠️ | 67 Python tests pass; chưa có C# tests |
| 14. Reproducibility | ⚠️ Nên chỉnh | Có nhiều artifact cần thiết nhưng thiếu README/version/dependency đầy đủ |
| 15. Documentation | ❌ Cần sửa trước khi gửi | Chưa có hướng dẫn vận hành/tổng quan đầy đủ |

## 20. Top 5 việc cần làm trước khi gửi

1. **Viết README hoàn chỉnh** và thử làm theo README từ một clone/folder sạch.
2. **Xác minh database trắng**: chạy migration đầy đủ, seed demo, khởi động web + AI và smoke test các luồng chính.
3. **Chuẩn hóa Python reproducibility**: pin dependency trực tiếp, khai báo Python version/Selenium, ghi lệnh test và cách tạo model; làm rõ `area_model.pkl`.
4. **Phân loại artifact/media**: chuyển presentation khỏi `AI/models`, giữ ảnh demo, đối chiếu/xử lý upload hash trùng/orphan, review `rooms.csv`.
5. **Giải quyết duplication có rủi ro thấp**: chọn một luồng Account/Profile; nếu chưa đủ thời gian refactor `PhongtroController`, ít nhất ghi limitation và không thay đổi gấp trước khi demo.

## 21. Checklist trước khi gửi giảng viên

- [x] Build 0 error
- [x] Build 0 warning
- [x] Không phát hiện production secret/API key/PAT
- [ ] Credential demo được ghi rõ và không dùng cho production
- [ ] Không còn folder/cache local không cần thiết trong bản bàn giao
- [x] Không track `bin/obj/.vs`
- [x] Không track DB backup/database file
- [x] Không track virtualenv
- [ ] `.gitignore` bổ sung Python venv/pytest/coverage phù hợp
- [ ] README đủ hướng dẫn chạy
- [ ] `requirements.txt` đầy đủ và pin có chủ ý
- [ ] Python version được ghi rõ
- [ ] Migrations chạy thành công trên database trắng
- [x] Model AI cần cho runtime có trong repo
- [x] Dataset runtime/demo cần thiết có trong repo
- [ ] Nguồn/synthetic status của `interactions.csv` được xác nhận
- [ ] Demo media đủ dùng và runtime/orphan media đã review
- [x] Python tests hiện tại pass 67/67
- [ ] Có smoke test MVC/EF trên DB trắng
- [ ] Git status sạch sau khi commit tài liệu/dọn có kiểm soát
- [ ] Commit cuối có message rõ nghĩa
- [x] Local đã sync `origin/main` tại thời điểm trước khi tạo report
- [ ] Clone mới có thể chạy hoàn toàn theo README

## 22. Trả lời 15 câu hỏi bàn giao

1. **Cấu trúc hiện tại có hợp lý không?** Có, ở mức nền tảng MVC tốt; còn duplication và một số folder/artifact cần dọn.
2. **Folder/file cần chỉnh trước khi gửi?** README, `AI/models` (presentation), requirements/Python metadata, Account/Profile duplicate, uploads và `rooms.csv`; Data subfolder là ưu tiên thấp hơn.
3. **Có file rác/tạm không?** Có local `.vs/bin/obj/__pycache__/*.user` và hai folder rỗng, nhưng đều không được track; uploads có file trùng cần review.
4. **Có secret không?** Không thấy production secret. Có connection local Integrated Security và credential demo cố định cần giới hạn Development.
5. **`.gitignore` ổn chưa?** Tốt cho .NET/IDE, còn thiếu các rule Python venv/pytest/coverage phổ biến.
6. **README ổn chưa?** Chưa; đây là blocker bàn giao.
7. **AI folder sạch không?** Khá sạch và có tổ chức; presentation đặt sai chỗ, cache local, dependency chưa đủ và area model chưa rõ cách tái tạo.
8. **Có file quá lớn không?** Không có file >10 MB. File lớn nhất được track là presentation ~6,47 MB; chưa cần LFS.
9. **Git status hiện tại?** Baseline sạch; sau audit có report mới cần review/commit.
10. **Local sync GitHub không?** Có tại thời điểm fetch: `0 ahead / 0 behind` so với `origin/main`.
11. **Build/test?** Build 0 warning/0 error; 67/67 Python tests pass; có một deprecation warning; chưa có C# tests.
12. **Có cần refactor folder không?** Có thể nhóm Data và chuẩn hóa AI data, nhưng không cấp thiết. Duplication Account/Profile và `PhongtroController` quan trọng hơn việc trang trí folder.
13. **Top 5 việc cần làm?** README; DB trắng; Python reproducibility; artifact/media cleanup; hợp nhất Account/Profile.
14. **Điểm tổng thể?** 7,0/10.
15. **Có thể gửi GitHub ngay chưa?** **Chưa nên.** Code đang chạy tốt, nhưng cần tối thiểu README đầy đủ, xác minh clone/DB trắng và review artifact/media trước khi gửi.
