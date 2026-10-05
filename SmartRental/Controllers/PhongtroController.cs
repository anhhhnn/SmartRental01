using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartRental.Data;
using SmartRental.Models;
using SmartRental.Security;
using SmartRental.ViewModels;

namespace SmartRental.Controllers
{
    public class PhongtroController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PhongtroController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(
       string? search,
       string? khuVuc,
       decimal? minPrice,
       decimal? maxPrice,
       double? minArea,
       double? maxArea,
       bool? status,
       List<int>? tienNghiIds,
       int page = 1)
        {
            var phongtros = _context.Phongtros
                .AsNoTracking()
                .Where(p => p.IsVisible)
                .Include(p => p.HinhAnhs)
                .Include(p => p.DanhGias)
                .Include(p => p.PhongTienNghis)
                    .ThenInclude(pt => pt.TienNghi)
                .AsQueryable();

            // Tìm theo tiêu đề / địa chỉ
            if (!string.IsNullOrWhiteSpace(search))
            {
                phongtros = phongtros.Where(p =>
                    p.TieuDe.Contains(search) ||
                    p.DiaChi.Contains(search));
            }

            // Khu vực hiện được lưu trực tiếp trong địa chỉ phòng.
            if (!string.IsNullOrWhiteSpace(khuVuc))
            {
                phongtros = phongtros.Where(p => p.DiaChi.Contains(khuVuc));
            }

            // Giá
            if (minPrice.HasValue)
            {
                phongtros = phongtros.Where(p =>
                    p.Gia >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                phongtros = phongtros.Where(p =>
                    p.Gia <= maxPrice.Value);
            }

            // Diện tích
            if (minArea.HasValue)
            {
                phongtros = phongtros.Where(p =>
                    p.DienTich >= minArea.Value);
            }

            if (maxArea.HasValue)
            {
                phongtros = phongtros.Where(p =>
                    p.DienTich <= maxArea.Value);
            }

            // Trạng thái
            if (status.HasValue)
            {
                phongtros = phongtros.Where(p =>
                    (p.SoLuongPhong > 0) == status.Value);
            }

            // LỌC THEO TIỆN NGHI
            if (tienNghiIds != null &&
                tienNghiIds.Count > 0)
            {
                foreach (var tienNghiId in tienNghiIds)
                {
                    int id = tienNghiId;

                    phongtros = phongtros.Where(p =>
                        p.PhongTienNghis.Any(pt =>
                            pt.TienNghiId == id));
                }
            }

            // Load danh sách tiện nghi cho View
            ViewBag.TienNghis =
                await _context.TienNghis
                    .OrderBy(t => t.TenTienNghi)
                    .ToListAsync();

            ViewBag.SelectedTienNghiIds =
                tienNghiIds ?? new List<int>();

            phongtros = phongtros
                .OrderByDescending(p => p.NgayDang);

            const int pageSize = 9;
            var totalItems = await phongtros.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);
            var rooms = await phongtros
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            await SetFavoriteRoomIdsAsync();

            SetPaginationViewData(page, pageSize, totalItems);
            return View(new PagedResult<Phongtro>
            {
                Items = rooms,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems
            });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddYeuThich(int phongtroId, string? returnUrl)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null)
            {
                return Challenge();
            }

            if (!await _context.Phongtros.AnyAsync(p => p.Id == phongtroId))
            {
                return NotFound();
            }

            var alreadyFavorite = await _context.YeuThichs.AnyAsync(yt =>
                yt.UserId == currentUserId && yt.PhongtroId == phongtroId);

            if (!alreadyFavorite)
            {
                _context.YeuThichs.Add(new YeuThich
                {
                    UserId = currentUserId,
                    PhongtroId = phongtroId,
                    NgayThem = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            return RedirectToLocalOrAction(returnUrl, nameof(Details), new { id = phongtroId });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveYeuThich(int phongtroId, string? returnUrl)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null)
            {
                return Challenge();
            }

            var yeuThich = await _context.YeuThichs.FindAsync(currentUserId, phongtroId);
            if (yeuThich != null)
            {
                _context.YeuThichs.Remove(yeuThich);
                await _context.SaveChangesAsync();
            }

            return RedirectToLocalOrAction(returnUrl, nameof(YeuThichCuaToi), null);
        }

        [Authorize]
        public async Task<IActionResult> YeuThichCuaToi(int page = 1)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null)
            {
                return Challenge();
            }

            const int pageSize = 9;
            var favoritesQuery = _context.YeuThichs
                .AsNoTracking()
                .Where(yt => yt.UserId == currentUserId)
                .Include(yt => yt.Phongtro)
                .OrderByDescending(yt => yt.NgayThem);
            var totalItems = await favoritesQuery.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);
            var favorites = await favoritesQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            SetPaginationViewData(page, pageSize, totalItems);
            return View(new PagedResult<YeuThich>
            {
                Items = favorites,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems
            });
        }

        // =========================
        // PHÒNG CỦA TÔI
        // =========================
        [Authorize(Roles = AppRoles.CanManageRooms)]
        public async Task<IActionResult> MyRooms(int page = 1)
        {
            var currentUserId = _userManager.GetUserId(User);

            if (currentUserId == null)
            {
                return Challenge();
            }

            const int pageSize = 9;
            var phongtrosQuery = _context.Phongtros
                .AsNoTracking()
                .Where(p => p.OwnerId == currentUserId)
                .Include(p => p.PhongTienNghis)
                    .ThenInclude(pt => pt.TienNghi)
                .OrderByDescending(p => p.NgayDang);
            var totalItems = await phongtrosQuery.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);
            var phongtros = await phongtrosQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            SetPaginationViewData(page, pageSize, totalItems);
            return View(new PagedResult<Phongtro>
            {
                Items = phongtros,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems
            });
        }

        // =========================
        // DETAILS
        // =========================
        public async Task<IActionResult> Details(int? id, int reviewPage = 1)
        {
            if (id == null)
            {
                return NotFound();
            }

            var phongtro = await _context.Phongtros
                .Include(p => p.Owner)
                .Include(p => p.HinhAnhs)
                .Include(p => p.PhongTienNghis)
                    .ThenInclude(pt => pt.TienNghi)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (phongtro == null)
            {
                return NotFound();
            }

            await RecordViewAsync(phongtro.Id);

            await SetFavoriteRoomIdsAsync();

            const int reviewPageSize = 5;
            var reviewsQuery = _context.DanhGias
                .AsNoTracking()
                .Where(dg => dg.PhongtroId == phongtro.Id)
                .Include(dg => dg.User)
                .OrderByDescending(dg => dg.NgayDanhGia);
            var reviewCount = await reviewsQuery.CountAsync();
            var reviewTotalPages = Math.Max(1, (int)Math.Ceiling(reviewCount / (double)reviewPageSize));
            reviewPage = Math.Clamp(reviewPage, 1, reviewTotalPages);
            ViewBag.Reviews = await reviewsQuery
                .Skip((reviewPage - 1) * reviewPageSize)
                .Take(reviewPageSize)
                .ToListAsync();
            ViewBag.ReviewPage = reviewPage;
            ViewBag.ReviewTotalPages = reviewTotalPages;
            ViewBag.ReviewCount = reviewCount;
            ViewBag.AverageRating = reviewCount == 0 ? 0 : await _context.DanhGias
                .Where(dg => dg.PhongtroId == phongtro.Id)
                .AverageAsync(dg => (double)dg.SoSao);
            ViewBag.RatingDistribution = await _context.DanhGias
                .Where(dg => dg.PhongtroId == phongtro.Id)
                .GroupBy(dg => dg.SoSao)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            ViewBag.ViewCount = await _context.LichSuXems
                .Where(ls => ls.PhongtroId == phongtro.Id)
                .SumAsync(ls => (int?)ls.SoLanXem) ?? 0;
            ViewBag.FavoriteCount = await _context.YeuThichs
                .CountAsync(yt => yt.PhongtroId == phongtro.Id);
            ViewBag.OwnerActiveRoomCount = string.IsNullOrEmpty(phongtro.OwnerId)
                ? 0
                : await _context.Phongtros.CountAsync(p =>
                    p.OwnerId == phongtro.OwnerId && p.IsVisible && p.SoLuongPhong > 0);

            if (!string.IsNullOrEmpty(phongtro.OwnerId))
            {
                var ownerBookings = _context.LichXemPhongs
                    .AsNoTracking()
                    .Where(booking => booking.ChuTroId == phongtro.OwnerId);
                var totalOwnerBookings = await ownerBookings.CountAsync();
                var respondedOwnerBookings = await ownerBookings.CountAsync(booking =>
                    booking.TrangThai == TrangThaiLich.DaDongY ||
                    booking.TrangThai == TrangThaiLich.BaoBan);

                ViewBag.OwnerResponseRate = totalOwnerBookings == 0
                    ? null
                    : (int?)Math.Round(respondedOwnerBookings * 100d / totalOwnerBookings);
            }
            else
            {
                ViewBag.OwnerResponseRate = null;
            }

            var priceRange = Math.Max(phongtro.Gia * 0.25m, 500000m);
            var minimumSimilarPrice = Math.Max(0, phongtro.Gia - priceRange);
            var maximumSimilarPrice = phongtro.Gia + priceRange;
            ViewBag.SimilarRooms = await _context.Phongtros
                .AsNoTracking()
                .Where(p => p.Id != phongtro.Id && p.IsVisible &&
                    (p.DiaChi == phongtro.DiaChi ||
                     (p.Gia >= minimumSimilarPrice && p.Gia <= maximumSimilarPrice)))
                .Include(p => p.HinhAnhs)
                .OrderByDescending(p => p.DiaChi == phongtro.DiaChi)
                .ThenBy(p => Math.Abs(p.Gia - phongtro.Gia))
                .ThenByDescending(p => p.NgayDang)
                .Take(3)
                .ToListAsync();

            var currentUser = User.Identity?.IsAuthenticated == true
                ? await _userManager.GetUserAsync(User)
                : null;
            var currentUserId = currentUser?.Id;
            ViewBag.BookingHoTen = currentUser?.HoTen ?? string.Empty;
            ViewBag.BookingPhone = currentUser?.PhoneNumber ?? string.Empty;
            ViewBag.MyReview = currentUserId == null
                ? null
                : await _context.DanhGias.FindAsync(currentUserId, phongtro.Id);

            return View(phongtro);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDanhGia(int phongtroId, int soSao, string? noiDung)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();
            if (!await _context.Phongtros.AnyAsync(p => p.Id == phongtroId)) return NotFound();
            if (soSao < 1 || soSao > 5)
            {
                TempData["Error"] = "Số sao phải từ 1 đến 5.";
                return RedirectToAction(nameof(Details), new { id = phongtroId });
            }
            if (noiDung?.Length > 1000)
            {
                TempData["Error"] = "Bình luận không được vượt quá 1000 ký tự.";
                return RedirectToAction(nameof(Details), new { id = phongtroId });
            }
            if (await _context.DanhGias.AnyAsync(dg => dg.UserId == currentUserId && dg.PhongtroId == phongtroId))
            {
                TempData["Error"] = "Bạn đã đánh giá phòng này. Hãy dùng chức năng chỉnh sửa.";
                return RedirectToAction(nameof(Details), new { id = phongtroId });
            }

            _context.DanhGias.Add(new DanhGia
            {
                UserId = currentUserId,
                PhongtroId = phongtroId,
                SoSao = soSao,
                NoiDung = string.IsNullOrWhiteSpace(noiDung) ? null : noiDung.Trim(),
                NgayDanhGia = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã gửi đánh giá.";
            return RedirectToAction(nameof(Details), new { id = phongtroId });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDanhGia(int phongtroId, int soSao, string? noiDung)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();
            var review = await _context.DanhGias.FindAsync(currentUserId, phongtroId);
            if (review == null) return NotFound();
            if (soSao < 1 || soSao > 5 || noiDung?.Length > 1000)
            {
                TempData["Error"] = soSao < 1 || soSao > 5 ? "Số sao phải từ 1 đến 5." : "Bình luận không được vượt quá 1000 ký tự.";
                return RedirectToAction(nameof(Details), new { id = phongtroId });
            }
            review.SoSao = soSao;
            review.NoiDung = string.IsNullOrWhiteSpace(noiDung) ? null : noiDung.Trim();
            review.NgayCapNhat = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật đánh giá.";
            return RedirectToAction(nameof(Details), new { id = phongtroId });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDanhGia(int phongtroId, string? userId = null)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();
            var targetUserId = User.IsInRole(AppRoles.Admin) && !string.IsNullOrWhiteSpace(userId) ? userId : currentUserId;
            var review = await _context.DanhGias.FindAsync(targetUserId, phongtroId);
            if (review == null) return NotFound();
            if (targetUserId != currentUserId && !User.IsInRole(AppRoles.Admin)) return Forbid();
            _context.DanhGias.Remove(review);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã xóa đánh giá.";
            return RedirectToAction(nameof(Details), new { id = phongtroId });
        }

        [Authorize]
        public async Task<IActionResult> DaXemGanDay(int page = 1)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            const int pageSize = 9;
            var historyQuery = _context.LichSuXems
                .AsNoTracking()
                .Where(ls => ls.UserId == currentUserId)
                .Include(ls => ls.Phongtro)
                .OrderByDescending(ls => ls.LanXemCuoi);
            var totalItems = await historyQuery.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);
            var history = await historyQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            SetPaginationViewData(page, pageSize, totalItems);
            return View(new PagedResult<LichSuXem>
            {
                Items = history,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems
            });
        }

        // =========================
        // CREATE - GET
        // =========================
        [Authorize(Roles = AppRoles.CanManageRooms)]
        public async Task<IActionResult> Create()
        {
            ViewBag.TienNghis =
                await _context.TienNghis.ToListAsync();

            return View();
        }

       


        // =========================
        // CREATE - POST
        // =========================
        [Authorize(Roles = AppRoles.CanManageRooms)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Id,TieuDe,DiaChi,Gia,DienTich,MoTa,NgayDang,TrangThai,SoLuongPhong,IsVisible")]
    Phongtro phongtro,
            List<IFormFile>? imageFiles,
            bool conPhong,
            List<int>? tienNghiIds)
        {
            imageFiles ??= new List<IFormFile>();
            if (imageFiles.Count > 10) ModelState.AddModelError("imageFiles", "Chỉ được tải lên tối đa 10 ảnh.");
            if (conPhong && phongtro.SoLuongPhong < 1) ModelState.AddModelError(nameof(phongtro.SoLuongPhong), "Phòng còn trống phải có số lượng ít nhất 1.");
            if (!conPhong) phongtro.SoLuongPhong = 0;
            var imageFile = imageFiles.FirstOrDefault();
            // Nếu validation của Model bị lỗi
            if (!ModelState.IsValid)
            {
                ViewBag.TienNghis =
                    await _context.TienNghis
                        .OrderBy(t => t.TenTienNghi)
                        .ToListAsync();

                ViewBag.SelectedTienNghiIds =
                    tienNghiIds ?? new List<int>();

                return View(phongtro);
            }


            // =========================
            // XỬ LÝ ẢNH
            // =========================
            if (imageFile != null && imageFile.Length > 0)
            {
                var allowedExtensions = new[]
                {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

                var extension =
                    Path.GetExtension(imageFile.FileName)
                        .ToLowerInvariant();


                // Kiểm tra định dạng ảnh
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError(
                        "HinhAnh",
                        "Chỉ chấp nhận ảnh JPG, JPEG, PNG hoặc WEBP."
                    );

                    ViewBag.TienNghis =
                        await _context.TienNghis
                            .OrderBy(t => t.TenTienNghi)
                            .ToListAsync();

                    ViewBag.SelectedTienNghiIds =
                        tienNghiIds ?? new List<int>();

                    return View(phongtro);
                }


                // Giới hạn dung lượng 5MB
                if (imageFile.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError(
                        "HinhAnh",
                        "Ảnh không được lớn hơn 5MB."
                    );

                    ViewBag.TienNghis =
                        await _context.TienNghis
                            .OrderBy(t => t.TenTienNghi)
                            .ToListAsync();

                    ViewBag.SelectedTienNghiIds =
                        tienNghiIds ?? new List<int>();

                    return View(phongtro);
                }


                // Tạo thư mục uploads
                var uploadsFolder = GetUploadsFolder();

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }


                // Tạo tên file ngẫu nhiên
                var fileName =
                    Guid.NewGuid().ToString() + extension;


                var filePath =
                    Path.Combine(
                        uploadsFolder,
                        fileName
                    );


                // Lưu file ảnh
                using (var stream =
                       new FileStream(
                           filePath,
                           FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }


                // Lưu đường dẫn vào database
                phongtro.HinhAnh =
                    "/uploads/" + fileName;
            }


            // =========================
            // THÔNG TIN MẶC ĐỊNH
            // =========================
            var currentUserId = _userManager.GetUserId(User);

            if (currentUserId == null)
            {
                return Challenge();
            }

            phongtro.OwnerId = currentUserId;
            phongtro.NgayDang =
                DateTime.Now;


            // =========================
            // LƯU PHÒNG TRỌ
            // =========================
            _context.Phongtros.Add(phongtro);

            await _context.SaveChangesAsync();
            if (!string.IsNullOrWhiteSpace(phongtro.HinhAnh))
            {
                _context.PhongtroHinhAnhs.Add(new PhongtroHinhAnh { PhongtroId = phongtro.Id, DuongDan = phongtro.HinhAnh, IsAnhChinh = true, ThuTu = 0 });
                await _context.SaveChangesAsync();
            }
            var imageOrder = 1;
            foreach (var extraImage in imageFiles.Skip(1))
            {
                var path = await SaveImage(extraImage);
                if (path is not null) _context.PhongtroHinhAnhs.Add(new PhongtroHinhAnh { PhongtroId = phongtro.Id, DuongDan = path, ThuTu = imageOrder++ });
            }
            if (imageOrder > 1) await _context.SaveChangesAsync();


            // =========================
            // LƯU TIỆN NGHI
            // =========================
            if (tienNghiIds != null &&
                tienNghiIds.Count > 0)
            {
                // Loại bỏ ID trùng
                var uniqueIds =
                    tienNghiIds.Distinct();


                foreach (var tienNghiId in uniqueIds)
                {
                    // Kiểm tra tiện nghi tồn tại
                    var exists =
                        await _context.TienNghis.AnyAsync(
                            t => t.Id == tienNghiId
                        );

                    if (exists)
                    {
                        var phongTienNghi =
                            new PhongTienNghi
                            {
                                PhongtroId =
                                    phongtro.Id,

                                TienNghiId =
                                    tienNghiId
                            };

                        _context.PhongTienNghis
                            .Add(phongTienNghi);
                    }
                }

                await _context.SaveChangesAsync();
            }


            return RedirectToAction(
                nameof(Index)
            );
        }
        // =========================
        // EDIT - GET
        // =========================
        [Authorize(Roles = AppRoles.CanManageRooms)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var phongtro = await _context.Phongtros
                .Include(p => p.PhongTienNghis).Include(p => p.HinhAnhs)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (phongtro == null)
            {
                return NotFound();
            }

            if (!CanManageRoom(phongtro))
            {
                return Forbid();
            }

            ViewBag.TienNghis =
                await _context.TienNghis
                    .OrderBy(t => t.TenTienNghi)
                    .ToListAsync();

            ViewBag.SelectedTienNghiIds =
                phongtro.PhongTienNghis
                    .Select(pt => pt.TienNghiId)
                    .ToList();

            return View(phongtro);
        }


        // =========================
        // EDIT - POST
        // =========================
        [Authorize(Roles = AppRoles.CanManageRooms)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int? id,
            [Bind("Id,TieuDe,DiaChi,Gia,DienTich,MoTa,TrangThai,SoLuongPhong,IsVisible")]
    Phongtro phongtro,
            List<IFormFile>? imageFiles,
            bool conPhong,
            List<int>? tienNghiIds)
        {
            if (id != phongtro.Id)
            {
                return NotFound();
            }

            var phongtroCu = await _context.Phongtros
                .FirstOrDefaultAsync(p => p.Id == id);

            if (phongtroCu == null)
            {
                return NotFound();
            }

            if (!CanManageRoom(phongtroCu))
            {
                return Forbid();
            }

            // Các giá trị này luôn lấy từ dữ liệu hiện có, không tin dữ liệu form.
            phongtro.HinhAnh = phongtroCu.HinhAnh;
            phongtro.NgayDang = phongtroCu.NgayDang;
            phongtro.OwnerId = phongtroCu.OwnerId;
            imageFiles ??= new List<IFormFile>();
            if (imageFiles.Count > 10) ModelState.AddModelError("imageFiles", "Chỉ được tải lên tối đa 10 ảnh.");
            if (conPhong && phongtro.SoLuongPhong < 1) ModelState.AddModelError(nameof(phongtro.SoLuongPhong), "Phòng còn trống phải có số lượng ít nhất 1.");
            if (!conPhong) phongtro.SoLuongPhong = 0;
            var imageFile = imageFiles.FirstOrDefault();

            if (!ModelState.IsValid)
            {
                ViewBag.TienNghis = await _context.TienNghis
                    .OrderBy(t => t.TenTienNghi)
                    .ToListAsync();

                ViewBag.SelectedTienNghiIds =
                    tienNghiIds ?? new List<int>();

                return View(phongtro);
            }

            string? newImagePath = null;
            var oldImagePath = phongtroCu.HinhAnh;

            // =========================
            // XỬ LÝ ẢNH MỚI
            // =========================
            if (imageFile != null && imageFile.Length > 0)
            {
                newImagePath = await SaveImage(imageFile);

                if (newImagePath == null)
                {
                    ViewBag.TienNghis = await _context.TienNghis
                        .OrderBy(t => t.TenTienNghi)
                        .ToListAsync();

                    ViewBag.SelectedTienNghiIds =
                        tienNghiIds ?? new List<int>();

                    return View(phongtro);
                }

                phongtroCu.HinhAnh = newImagePath;
            }

            try
            {
                // =========================
                // CẬP NHẬT PHÒNG
                // =========================
                phongtroCu.TieuDe = phongtro.TieuDe;
                phongtroCu.DiaChi = phongtro.DiaChi;
                phongtroCu.Gia = phongtro.Gia;
                phongtroCu.DienTich = phongtro.DienTich;
                phongtroCu.MoTa = phongtro.MoTa;
                phongtroCu.TrangThai = phongtro.TrangThai;
                phongtroCu.SoLuongPhong = phongtro.SoLuongPhong;
                phongtroCu.IsVisible = phongtro.IsVisible;

                await _context.SaveChangesAsync();

                var imageOrder = await _context.PhongtroHinhAnhs.Where(x => x.PhongtroId == phongtroCu.Id).CountAsync();
                foreach (var extraImage in imageFiles.Skip(1))
                {
                    var path = await SaveImage(extraImage);
                    if (path is not null) _context.PhongtroHinhAnhs.Add(new PhongtroHinhAnh { PhongtroId = phongtroCu.Id, DuongDan = path, ThuTu = imageOrder++ });
                }
                if (imageFiles.Count > 1) await _context.SaveChangesAsync();


                // =========================
                // XÓA TIỆN NGHI CŨ
                // =========================
                var tienNghiCu =
                    await _context.PhongTienNghis
                        .Where(pt =>
                            pt.PhongtroId == phongtro.Id)
                        .ToListAsync();

                _context.PhongTienNghis
                    .RemoveRange(tienNghiCu);

                await _context.SaveChangesAsync();


                // =========================
                // THÊM TIỆN NGHI MỚI
                // =========================
                if (tienNghiIds != null &&
                    tienNghiIds.Count > 0)
                {
                    var uniqueIds =
                        tienNghiIds.Distinct();

                    foreach (var tienNghiId in uniqueIds)
                    {
                        var exists =
                            await _context.TienNghis
                                .AnyAsync(t =>
                                    t.Id == tienNghiId);

                        if (exists)
                        {
                            _context.PhongTienNghis.Add(
                                new PhongTienNghi
                                {
                                    PhongtroId =
                                        phongtroCu.Id,

                                    TienNghiId =
                                        tienNghiId
                                }
                            );
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                if (newImagePath != null)
                {
                    DeleteImageFile(oldImagePath);
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PhongtroExists(phongtroCu.Id))
                {
                    return NotFound();
                }

                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================
        // DELETE - GET
        // =========================
        [Authorize(Roles = AppRoles.CanManageRooms)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var phongtro = await _context.Phongtros
                .FirstOrDefaultAsync(p => p.Id == id);

            if (phongtro == null)
            {
                return NotFound();
            }

            if (!CanManageRoom(phongtro))
            {
                return Forbid();
            }

            return View(phongtro);
        }


        // =========================
        // DELETE - POST
        // =========================
        [Authorize(Roles = AppRoles.CanManageRooms)]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var phongtro = await _context.Phongtros.FindAsync(id);

            if (phongtro == null)
            {
                return NotFound();
            }

            if (!CanManageRoom(phongtro))
            {
                return Forbid();
            }

            // Xóa ảnh khỏi wwwroot/uploads
            DeleteImageFile(phongtro.HinhAnh);

            _context.Phongtros.Remove(phongtro);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }


        // =========================
        // KIỂM TRA PHÒNG CÓ TỒN TẠI
        // =========================
        private bool PhongtroExists(int id)
        {
            return _context.Phongtros.Any(p => p.Id == id);
        }

        private async Task SetFavoriteRoomIdsAsync()
        {
            var currentUserId = _userManager.GetUserId(User);
            ViewBag.FavoriteRoomIds = currentUserId == null
                ? new HashSet<int>()
                : await _context.YeuThichs
                    .Where(yt => yt.UserId == currentUserId)
                    .Select(yt => yt.PhongtroId)
                    .ToHashSetAsync();
        }

        private void SetPaginationViewData(int pageNumber, int pageSize, int totalItems)
        {
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            ViewBag.PageNumber = pageNumber;
            ViewBag.TotalPages = totalPages;
            ViewBag.HasPreviousPage = pageNumber > 1;
            ViewBag.HasNextPage = pageNumber < totalPages;
        }

        private async Task RecordViewAsync(int phongtroId)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null)
            {
                return;
            }

            var history = await _context.LichSuXems.FindAsync(currentUserId, phongtroId);
            if (history == null)
            {
                _context.LichSuXems.Add(new LichSuXem
                {
                    UserId = currentUserId,
                    PhongtroId = phongtroId,
                    LanXemCuoi = DateTime.UtcNow,
                    SoLanXem = 1
                });
            }
            else
            {
                history.LanXemCuoi = DateTime.UtcNow;
                history.SoLanXem++;
            }

            await _context.SaveChangesAsync();
        }

        private IActionResult RedirectToLocalOrAction(
            string? returnUrl,
            string action,
            object? routeValues)
        {
            return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? LocalRedirect(returnUrl)
                : RedirectToAction(action, routeValues)!;
        }

        private bool CanManageRoom(Phongtro phongtro)
        {
            if (User.IsInRole(AppRoles.Admin))
            {
                return true;
            }

            var currentUserId = _userManager.GetUserId(User);

            return currentUserId != null &&
                phongtro.OwnerId == currentUserId;
        }


        // =========================
        // LƯU ẢNH
        // =========================
        private async Task<string?> SaveImage(IFormFile imageFile)
        {
            var allowedExtensions = new[]
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp"
            };

            var extension = Path
                .GetExtension(imageFile.FileName)
                .ToLowerInvariant();

            // Kiểm tra đuôi ảnh
            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError(
                    "HinhAnh",
                    "Chỉ chấp nhận ảnh JPG, JPEG, PNG hoặc WEBP."
                );

                return null;
            }

            // Giới hạn 5MB
            if (imageFile.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError(
                    "HinhAnh",
                    "Ảnh không được lớn hơn 5MB."
                );

                return null;
            }

            var uploadsFolder = GetUploadsFolder();

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileName =
                Guid.NewGuid().ToString() + extension;

            var filePath =
                Path.Combine(uploadsFolder, fileName);

            using (var stream =
                   new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            return "/uploads/" + fileName;
        }


        // =========================
        // XÓA FILE ẢNH
        // =========================
        private void DeleteImageFile(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return;
            }

            var fileName = Path.GetFileName(imagePath);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            var fullPath = Path.Combine(GetUploadsFolder(), fileName);

            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }

        private static string GetUploadsFolder()
        {
            return Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "uploads"
            );
        }
    }
}
