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

        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("/Admin")]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var landlordRoleId = await GetRoleIdAsync(AppRoles.ChuTro);
            var tenantRoleId = await GetRoleIdAsync(AppRoles.NguoiThue);

            var model = new AdminDashboardViewModel
            {
                TotalUsers = await _userManager.Users.CountAsync(),
                TotalLandlords = landlordRoleId == null ? 0 : await _context.UserRoles.CountAsync(ur => ur.RoleId == landlordRoleId),
                TotalTenants = tenantRoleId == null ? 0 : await _context.UserRoles.CountAsync(ur => ur.RoleId == tenantRoleId),
                TotalRooms = await _context.Phongtros.CountAsync(),
                ActiveRooms = await _context.Phongtros.CountAsync(p => p.TrangThai),
                HiddenRooms = await _context.Phongtros.CountAsync(p => !p.TrangThai),
                TotalFavorites = await _context.YeuThichs.CountAsync(),
                TotalAmenities = await _context.TienNghis.CountAsync(),
                TotalReviews = await _context.DanhGias.CountAsync()
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
                    TrangThai = p.TrangThai,
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
            room.TrangThai = !room.TrangThai;
            await _context.SaveChangesAsync();
            TempData["Success"] = room.TrangThai ? "Đã hiển thị phòng trọ." : "Đã tạm ẩn phòng trọ.";
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
            try
            {
                var rooms = await _context.Phongtros.Where(x => x.OwnerId == id).Include(x => x.HinhAnhs).ToListAsync();
                var files = rooms.SelectMany(x => x.HinhAnhs.Select(i => i.DuongDan).Append(x.HinhAnh)).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().Distinct().ToList();
                var conversations = await _context.CuocTroChuyens.Where(x => x.NguoiThueId == id || x.ChuTroId == id).ToListAsync();
                _context.CuocTroChuyens.RemoveRange(conversations);
                _context.Phongtros.RemoveRange(rooms);
                await _context.SaveChangesAsync();
                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
                await transaction.CommitAsync(); DeleteUploadFiles(files); TempData["Success"] = "Đã xóa tài khoản cùng dữ liệu liên quan.";
            }
            catch { await transaction.RollbackAsync(); TempData["Error"] = "Không thể xóa tài khoản. Dữ liệu chưa bị thay đổi."; }
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
            _context.TienNghis.Add(new TienNghi { TenTienNghi = tenTienNghi.Trim() });
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
            amenity.TenTienNghi = tenTienNghi.Trim();
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
