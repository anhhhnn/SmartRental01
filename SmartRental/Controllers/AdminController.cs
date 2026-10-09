using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartRental.Data;
using SmartRental.Models;
using SmartRental.Security;
using SmartRental.ViewModels.Admin;

namespace SmartRental.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    [Route("Admin/[action]")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<AdminController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet("/Admin")]
        [HttpGet]
        public async Task<IActionResult> Index(int? year)
        {
            var currentYear = DateTime.Today.Year;
            var availableYears = await _context.Phongtros
                .AsNoTracking()
                .Select(p => p.NgayDang.Year)
                .Distinct()
                .OrderByDescending(value => value)
                .ToListAsync();
            if (!availableYears.Contains(currentYear)) availableYears.Insert(0, currentYear);
            var selectedYear = year.HasValue && availableYears.Contains(year.Value) ? year.Value : currentYear;

            var adminRoleId = await GetRoleIdAsync(AppRoles.Admin);
            var landlordRoleId = await GetRoleIdAsync(AppRoles.ChuTro);
            var tenantRoleId = await GetRoleIdAsync(AppRoles.NguoiThue);

            var adminUserIds = _context.UserRoles
                .Where(ur => adminRoleId != null && ur.RoleId == adminRoleId)
                .Select(ur => ur.UserId);
            var landlordUserIds = _context.UserRoles
                .Where(ur => landlordRoleId != null && ur.RoleId == landlordRoleId)
                .Select(ur => ur.UserId);
            var tenantUserIds = _context.UserRoles
                .Where(ur => tenantRoleId != null && ur.RoleId == tenantRoleId)
                .Select(ur => ur.UserId);

            var totalAdmins = adminRoleId == null ? 0 : await adminUserIds.Distinct().CountAsync();
            var totalLandlords = landlordRoleId == null ? 0 : await landlordUserIds.Distinct().CountAsync();
            var totalTenants = tenantRoleId == null ? 0 : await tenantUserIds.Distinct().CountAsync();
            var primaryLandlords = landlordRoleId == null
                ? 0
                : await landlordUserIds.Where(id => !adminUserIds.Contains(id)).Distinct().CountAsync();
            var primaryTenants = tenantRoleId == null
                ? 0
                : await tenantUserIds
                    .Where(id => !adminUserIds.Contains(id) && !landlordUserIds.Contains(id))
                    .Distinct()
                    .CountAsync();

            var monthlyRoomCounts = await _context.Phongtros
                .AsNoTracking()
                .Where(p => p.NgayDang.Year == selectedYear)
                .GroupBy(p => p.NgayDang.Month)
                .Select(group => new { Month = group.Key, Count = group.Count() })
                .ToListAsync();
            var monthlyNewRooms = new int[12];
            foreach (var item in monthlyRoomCounts) monthlyNewRooms[item.Month - 1] = item.Count;

            var ratingCounts = await _context.DanhGias
                .AsNoTracking()
                .Where(review => review.SoSao >= 1 && review.SoSao <= 5)
                .GroupBy(review => review.SoSao)
                .Select(group => new { Rating = group.Key, Count = group.Count() })
                .ToListAsync();
            var ratingDistribution = new int[5];
            foreach (var item in ratingCounts) ratingDistribution[item.Rating - 1] = item.Count;

            var bookingCounts = await _context.LichXemPhongs
                .AsNoTracking()
                .GroupBy(booking => booking.TrangThai)
                .Select(group => new { Status = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.Status, item => item.Count);

            var topRooms = await _context.Phongtros
                .AsNoTracking()
                .Select(room => new AdminTopRoomViewModel
                {
                    Id = room.Id,
                    Title = room.TieuDe,
                    OwnerName = room.Owner == null
                        ? "Chưa gán chủ trọ"
                        : (room.Owner.HoTen ?? room.Owner.Email ?? room.Owner.UserName ?? "Chưa đặt tên"),
                    ImageUrl = room.HinhAnh,
                    Price = room.Gia,
                    ViewCount = room.LichSuXems.Sum(view => (long?)view.SoLanXem) ?? 0,
                    FavoriteCount = room.YeuThichs.Count,
                    AverageRating = room.DanhGias.Average(review => (double?)review.SoSao),
                    RoomQuantity = room.SoLuongPhong
                })
                .OrderByDescending(room => room.ViewCount)
                .ThenByDescending(room => room.FavoriteCount)
                .ThenBy(room => room.Id)
                .Take(5)
                .ToListAsync();

            var recentRooms = await _context.Phongtros
                .AsNoTracking()
                .OrderByDescending(room => room.NgayDang)
                .ThenByDescending(room => room.Id)
                .Select(room => new AdminRecentRoomViewModel
                {
                    Id = room.Id,
                    Title = room.TieuDe,
                    OwnerName = room.Owner == null
                        ? "Chưa gán chủ trọ"
                        : (room.Owner.HoTen ?? room.Owner.Email ?? room.Owner.UserName ?? "Chưa đặt tên"),
                    PostedAt = room.NgayDang,
                    Price = room.Gia,
                    RoomQuantity = room.SoLuongPhong
                })
                .Take(5)
                .ToListAsync();

            var availableRooms = await _context.Phongtros.CountAsync(room => room.SoLuongPhong > 0);
            var unavailableRooms = await _context.Phongtros.CountAsync(room => room.SoLuongPhong == 0);

            var model = new AdminDashboardViewModel
            {
                SelectedYear = selectedYear,
                AvailableYears = availableYears,
                TotalUsers = await _userManager.Users.CountAsync(),
                TotalLandlords = totalLandlords,
                TotalTenants = totalTenants,
                TotalAdmins = totalAdmins,
                TotalRooms = await _context.Phongtros.CountAsync(),
                AvailableRooms = availableRooms,
                UnavailableRooms = unavailableRooms,
                TotalFavorites = await _context.YeuThichs.CountAsync(),
                TotalViews = await _context.LichSuXems.SumAsync(view => (long?)view.SoLanXem) ?? 0,
                PendingBookings = bookingCounts.GetValueOrDefault(TrangThaiLich.ChoXacNhan),
                TotalReviews = await _context.DanhGias.CountAsync(),
                MonthlyNewRooms = monthlyNewRooms,
                RoomStatusDistribution = [availableRooms, unavailableRooms],
                UserRoleDistribution = [primaryLandlords, primaryTenants, totalAdmins],
                RatingDistribution = ratingDistribution,
                BookingStatusDistribution =
                [
                    bookingCounts.GetValueOrDefault(TrangThaiLich.ChoXacNhan),
                    bookingCounts.GetValueOrDefault(TrangThaiLich.DaDongY),
                    bookingCounts.GetValueOrDefault(TrangThaiLich.BaoBan),
                    bookingCounts.GetValueOrDefault(TrangThaiLich.DaHuy)
                ],
                TopRooms = topRooms,
                RecentRooms = recentRooms
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users.OrderBy(u => u.Email).ToListAsync();
            var model = new List<AdminUserListItemViewModel>();
            foreach (var user in users)
            {
                model.Add(new AdminUserListItemViewModel
                {
                    Id = user.Id,
                    Email = user.Email,
                    HoTen = user.HoTen,
                    PhoneNumber = user.PhoneNumber,
                    Roles = (await _userManager.GetRolesAsync(user)).ToArray(),
                    IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow
                });
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> UserDetails(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            return View(new AdminUserDetailsViewModel
            {
                Id = user.Id,
                Email = user.Email,
                HoTen = user.HoTen,
                PhoneNumber = user.PhoneNumber,
                DiaChi = user.DiaChi,
                Roles = (await _userManager.GetRolesAsync(user)).ToArray(),
                IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow,
                RoomCount = await _context.Phongtros.CountAsync(p => p.OwnerId == user.Id),
                FavoriteCount = await _context.YeuThichs.CountAsync(y => y.UserId == user.Id)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();
            var currentUserId = _userManager.GetUserId(User);
            if (id == currentUserId)
            {
                TempData["Error"] = "Bạn không thể khóa tài khoản Admin đang đăng nhập.";
                return RedirectToAction(nameof(Users));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            if (!await _userManager.GetLockoutEnabledAsync(user))
            {
                var enabled = await _userManager.SetLockoutEnabledAsync(user, true);
                if (!enabled.Succeeded) return IdentityErrorRedirect(enabled, nameof(Users));
            }

            var isLocked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;
            var result = await _userManager.SetLockoutEndDateAsync(user, isLocked ? null : DateTimeOffset.MaxValue);
            if (!result.Succeeded) return IdentityErrorRedirect(result, nameof(Users));

            TempData["Success"] = isLocked ? "Đã mở khóa tài khoản." : "Đã khóa tài khoản.";
            return RedirectToAction(nameof(Users));
        }

        [HttpGet]
        public async Task<IActionResult> Rooms()
        {
            var model = await _context.Phongtros
                .AsNoTracking()
                .Include(p => p.Owner)
                .Include(p => p.YeuThichs)
                .OrderByDescending(p => p.NgayDang)
                .Select(p => new AdminRoomViewModel
                {
                    Id = p.Id,
                    TieuDe = p.TieuDe,
                    HinhAnh = p.HinhAnh,
                    Gia = p.Gia,
                    DienTich = p.DienTich,
                    OwnerName = p.Owner == null ? "Chưa gán chủ phòng" : (p.Owner.HoTen ?? p.Owner.Email ?? p.Owner.UserName ?? "Chưa đặt tên"),
                    IsVisible = p.IsVisible,
                    SoLuongPhong = p.SoLuongPhong,
                    NgayDang = p.NgayDang,
                    FavoriteCount = p.YeuThichs.Count
                })
                .ToListAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleRoomVisibility(int id)
        {
            var room = await _context.Phongtros.FindAsync(id);
            if (room == null) return NotFound();
            room.IsVisible = !room.IsVisible;
            await _context.SaveChangesAsync();
            TempData["Success"] = room.IsVisible ? "Đã hiển thị tin phòng trọ." : "Đã ẩn tin phòng trọ.";
            return RedirectToAction(nameof(Rooms));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRoom(int id)
        {
            var room = await _context.Phongtros.Include(x => x.HinhAnhs).FirstOrDefaultAsync(x => x.Id == id);
            if (room is null) return NotFound();
            var paths = room.HinhAnhs.Select(x => x.DuongDan).Append(room.HinhAnh).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().Distinct().ToList();
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var messages = await _context.TinNhans.Where(x => x.PhongtroId == id).ToListAsync();
                messages.ForEach(x => x.PhongtroId = null);
                _context.Phongtros.Remove(room);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                DeleteUploadFiles(paths);
                TempData["Success"] = "Đã xóa phòng và dữ liệu phụ thuộc; lịch sử chat được giữ lại.";
            }
            catch { await transaction.RollbackAsync(); TempData["Error"] = "Không thể xóa phòng. Dữ liệu chưa bị thay đổi."; }
            return RedirectToAction(nameof(Rooms));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();
            if (id == _userManager.GetUserId(User)) { TempData["Error"] = "Bạn không thể xóa tài khoản Admin đang đăng nhập."; return RedirectToAction(nameof(Users)); }
            var user = await _userManager.FindByIdAsync(id); if (user is null) return NotFound();
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var files = new List<string>();
            try
            {
                var rooms = await _context.Phongtros.Where(x => x.OwnerId == id).Include(x => x.HinhAnhs).ToListAsync();
                var roomIds = rooms.Select(x => x.Id).ToList();
                files = rooms.SelectMany(x => x.HinhAnhs.Select(i => i.DuongDan).Append(x.HinhAnh))
                    .Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().Distinct().ToList();

                // Conversation participants are Restrict FKs. Remove only conversations
                // involving this account; messages in those conversations cascade with them.
                var conversations = await _context.CuocTroChuyens.Where(x => x.NguoiThueId == id || x.ChuTroId == id).ToListAsync();
                _context.CuocTroChuyens.RemoveRange(conversations);

                // Booking user FKs are Restrict. Explicit cleanup is required for both roles,
                // including bookings made by other users for rooms owned by this account.
                var bookings = await _context.LichXemPhongs
                    .Where(x => x.NguoiThueId == id || x.ChuTroId == id || roomIds.Contains(x.PhongtroId))
                    .ToListAsync();
                _context.LichXemPhongs.RemoveRange(bookings);

                var favorites = await _context.YeuThichs
                    .Where(x => x.UserId == id || roomIds.Contains(x.PhongtroId)).ToListAsync();
                var viewHistory = await _context.LichSuXems
                    .Where(x => x.UserId == id || roomIds.Contains(x.PhongtroId)).ToListAsync();
                var reviews = await _context.DanhGias
                    .Where(x => x.UserId == id || roomIds.Contains(x.PhongtroId)).ToListAsync();
                var roomAmenities = await _context.PhongTienNghis
                    .Where(x => roomIds.Contains(x.PhongtroId)).ToListAsync();
                var roomImages = await _context.PhongtroHinhAnhs
                    .Where(x => roomIds.Contains(x.PhongtroId)).ToListAsync();
                var notifications = await _context.ThongBaos.Where(x => x.UserId == id).ToListAsync();

                _context.YeuThichs.RemoveRange(favorites);
                _context.LichSuXems.RemoveRange(viewHistory);
                _context.DanhGias.RemoveRange(reviews);
                _context.PhongTienNghis.RemoveRange(roomAmenities);
                _context.PhongtroHinhAnhs.RemoveRange(roomImages);
                _context.ThongBaos.RemoveRange(notifications);

                // Defensive cleanup for malformed legacy messages where the sender is no
                // longer one of the conversation participants.
                var conversationIds = conversations.Select(x => x.Id).ToList();
                var remainingSentMessages = await _context.TinNhans
                    .Where(x => x.NguoiGuiId == id && !conversationIds.Contains(x.CuocTroChuyenId))
                    .ToListAsync();
                _context.TinNhans.RemoveRange(remainingSentMessages);

                _context.Phongtros.RemoveRange(rooms);
                await _context.SaveChangesAsync();
                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
                await transaction.CommitAsync();
                DeleteUploadFiles(files);
                TempData["Success"] = "Đã xóa tài khoản cùng dữ liệu liên quan.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Không thể xóa tài khoản {UserId} và dữ liệu liên quan.", id);
                TempData["Error"] = ex is DbUpdateException
                    ? "Không thể xóa tài khoản do dữ liệu liên quan chưa được xử lý đầy đủ. Không có thay đổi nào được lưu."
                    : "Không thể xóa tài khoản. Không có thay đổi nào được lưu; chi tiết đã được ghi vào log hệ thống.";
            }
            return RedirectToAction(nameof(Users));
        }

        [HttpGet]
        public async Task<IActionResult> Amenities()
        {
            return View(await _context.TienNghis.AsNoTracking().OrderBy(t => t.TenTienNghi).ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAmenity(string? tenTienNghi)
        {
            if (string.IsNullOrWhiteSpace(tenTienNghi))
            {
                TempData["Error"] = "Tên tiện nghi không được để trống.";
                return RedirectToAction(nameof(Amenities));
            }
            if (tenTienNghi.Trim().Length > 100)
            {
                TempData["Error"] = "Tên tiện nghi không được vượt quá 100 ký tự.";
                return RedirectToAction(nameof(Amenities));
            }
            var normalizedName = tenTienNghi.Trim();
            if (await _context.TienNghis.AnyAsync(item => item.TenTienNghi.ToLower() == normalizedName.ToLower()))
            {
                TempData["Error"] = "Tiện nghi này đã tồn tại.";
                return RedirectToAction(nameof(Amenities));
            }
            _context.TienNghis.Add(new TienNghi { TenTienNghi = normalizedName });
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã thêm tiện nghi.";
            return RedirectToAction(nameof(Amenities));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAmenity(int id, string? tenTienNghi)
        {
            var amenity = await _context.TienNghis.FindAsync(id);
            if (amenity == null) return NotFound();
            if (string.IsNullOrWhiteSpace(tenTienNghi) || tenTienNghi.Trim().Length > 100)
            {
                TempData["Error"] = "Tên tiện nghi phải có từ 1 đến 100 ký tự.";
                return RedirectToAction(nameof(Amenities));
            }
            var normalizedName = tenTienNghi.Trim();
            if (await _context.TienNghis.AnyAsync(item => item.Id != id && item.TenTienNghi.ToLower() == normalizedName.ToLower()))
            {
                TempData["Error"] = "Tiện nghi này đã tồn tại.";
                return RedirectToAction(nameof(Amenities));
            }
            amenity.TenTienNghi = normalizedName;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật tiện nghi.";
            return RedirectToAction(nameof(Amenities));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAmenity(int id)
        {
            var amenity = await _context.TienNghis.FindAsync(id);
            if (amenity == null) return NotFound();
            var roomAmenities = await _context.PhongTienNghis.Where(pt => pt.TienNghiId == id).ToListAsync();
            _context.PhongTienNghis.RemoveRange(roomAmenities);
            _context.TienNghis.Remove(amenity);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã xóa tiện nghi và gỡ liên kết khỏi các phòng đang dùng.";
            return RedirectToAction(nameof(Amenities));
        }

        [HttpGet]
        public async Task<IActionResult> Favorites()
        {
            var model = await _context.YeuThichs.AsNoTracking()
                .Include(y => y.User)
                .Include(y => y.Phongtro)
                .OrderByDescending(y => y.NgayThem)
                .Select(y => new AdminFavoriteViewModel
                {
                    UserName = y.User.HoTen ?? y.User.Email ?? y.User.UserName ?? "Không rõ",
                    UserEmail = y.User.Email,
                    PhongtroId = y.PhongtroId,
                    RoomTitle = y.Phongtro.TieuDe,
                    NgayThem = y.NgayThem
                }).ToListAsync();
            ViewBag.FavoritesByRoom = await _context.YeuThichs
                .GroupBy(y => new { y.PhongtroId, y.Phongtro.TieuDe })
                .Select(g => new { g.Key.PhongtroId, g.Key.TieuDe, Count = g.Count() })
                .OrderByDescending(x => x.Count).ThenBy(x => x.TieuDe).ToListAsync();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Reviews()
        {
            var model = await _context.DanhGias.AsNoTracking()
                .Include(dg => dg.User)
                .Include(dg => dg.Phongtro)
                .OrderByDescending(dg => dg.NgayDanhGia)
                .Select(dg => new AdminDanhGiaViewModel
                {
                    UserId = dg.UserId,
                    UserName = dg.User.HoTen ?? dg.User.Email ?? dg.User.UserName ?? "Không rõ",
                    PhongtroId = dg.PhongtroId,
                    RoomTitle = dg.Phongtro.TieuDe,
                    SoSao = dg.SoSao,
                    NoiDung = dg.NoiDung,
                    NgayDanhGia = dg.NgayDanhGia
                }).ToListAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReview(string? userId, int phongtroId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return NotFound();
            var review = await _context.DanhGias.FindAsync(userId, phongtroId);
            if (review == null) return NotFound();
            _context.DanhGias.Remove(review);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã xóa đánh giá.";
            return RedirectToAction(nameof(Reviews));
        }

        private async Task<string?> GetRoleIdAsync(string roleName) =>
            await _context.Roles.Where(r => r.Name == roleName).Select(r => r.Id).FirstOrDefaultAsync();

        private IActionResult IdentityErrorRedirect(IdentityResult result, string action)
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
            return RedirectToAction(action);
        }

        private static void DeleteUploadFiles(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                var relative = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relative));
                var uploadsRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads"));
                if (fullPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
            }
        }
    }
}
