using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartRental.Data;
using SmartRental.Models;
using SmartRental.Security;

namespace SmartRental.Controllers;

[Authorize]
public class LichXemPhongController : Controller
{
    private static readonly TimeOnly OpeningTime = new(8, 0);
    private static readonly TimeOnly ClosingTime = new(20, 0);
    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public LichXemPhongController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<IActionResult> Index(string? status)
    {
        var uid = _users.GetUserId(User)!;
        var owner = User.IsInRole(AppRoles.ChuTro) || User.IsInRole(AppRoles.Admin);
        IQueryable<LichXemPhong> query = _db.LichXemPhongs
            .Include(x => x.Phongtro)
            .Include(x => x.NguoiThue)
            .Where(x => owner ? x.ChuTroId == uid : x.NguoiThueId == uid);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.TrangThai == status);
        }

        ViewBag.IsOwner = owner;
        return View(await query.OrderByDescending(x => x.ThoiGianXem).ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        int phongtroId,
        string? hoTen,
        string? soDienThoai,
        DateOnly ngayXem,
        TimeOnly gioXem,
        string? ghiChu)
    {
        var uid = _users.GetUserId(User);
        if (uid is null) return Challenge();

        var room = await _db.Phongtros.AsNoTracking().FirstOrDefaultAsync(x => x.Id == phongtroId);
        if (room is null || string.IsNullOrWhiteSpace(room.OwnerId)) return NotFound();
        if (room.OwnerId == uid) return BookingError(phongtroId, "Bạn không thể đặt lịch xem phòng do chính mình đăng.");
        if (!room.IsVisible) return BookingError(phongtroId, "Phòng này đang tạm ẩn và không nhận lịch xem.");
        if (room.SoLuongPhong <= 0) return BookingError(phongtroId, "Phòng đã hết và hiện không nhận lịch xem.");
        if (!await _db.Users.AnyAsync(x => x.Id == room.OwnerId))
            return BookingError(phongtroId, "Không tìm thấy thông tin chủ trọ của phòng này.");

        hoTen = hoTen?.Trim();
        soDienThoai = soDienThoai?.Trim();
        ghiChu = ghiChu?.Trim();

        if (string.IsNullOrWhiteSpace(hoTen) || hoTen.Length > 150)
            return BookingError(phongtroId, "Họ tên là bắt buộc và không được vượt quá 150 ký tự.");
        if (string.IsNullOrWhiteSpace(soDienThoai) || soDienThoai.Length > 30)
            return BookingError(phongtroId, "Số điện thoại là bắt buộc và không được vượt quá 30 ký tự.");
        if (ghiChu?.Length > 500)
            return BookingError(phongtroId, "Ghi chú không được vượt quá 500 ký tự.");

        var now = VietnamNow();
        var timeValidationError = ValidateViewingTime(ngayXem, gioXem, now);
        if (timeValidationError is not null) return BookingError(phongtroId, timeValidationError);
        var viewingTime = ngayXem.ToDateTime(gioXem, DateTimeKind.Unspecified);

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var duplicate = await _db.LichXemPhongs.AnyAsync(x =>
            x.NguoiThueId == uid && x.PhongtroId == room.Id && x.ThoiGianXem == viewingTime &&
            (x.TrangThai == TrangThaiLich.ChoXacNhan || x.TrangThai == TrangThaiLich.DaDongY));
        if (duplicate)
        {
            await transaction.RollbackAsync();
            return BookingError(phongtroId, "Bạn đã có một lịch đang hoạt động cho phòng này vào đúng thời gian đã chọn.");
        }

        var booking = new LichXemPhong
        {
            PhongtroId = room.Id,
            NguoiThueId = uid,
            ChuTroId = room.OwnerId,
            HoTen = hoTen,
            SoDienThoai = soDienThoai,
            ThoiGianXem = viewingTime,
            GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu
        };
        _db.LichXemPhongs.Add(booking);
        await _db.SaveChangesAsync();

        var conversation = await GetConversationAsync(uid, room.OwnerId, room.Id);
        await _db.SaveChangesAsync();
        conversation.LanCapNhatCuoi = DateTime.UtcNow;

        _db.TinNhans.Add(new TinNhan
        {
            CuocTroChuyenId = conversation.Id,
            NguoiGuiId = uid,
            PhongtroId = room.Id,
            LoaiTinNhan = BookingMessageType(booking.Id, "YeuCau"),
            NoiDung = $"Yêu cầu xem phòng {room.TieuDe} lúc {viewingTime:HH:mm - dd/MM/yyyy}."
        });
        _db.ThongBaos.Add(new ThongBao
        {
            UserId = room.OwnerId,
            TieuDe = "Có yêu cầu xem phòng mới",
            NoiDung = $"{booking.HoTen} muốn xem {room.TieuDe} vào {viewingTime:HH:mm - dd/MM/yyyy}",
            Loai = "Booking",
            Link = $"/Messages?id={conversation.Id}"
        });

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = "Đã gửi yêu cầu đặt lịch xem phòng.";
        return RedirectToAction("Index", "Messages", new { id = conversation.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Respond(int id, string action, string? lyDo)
    {
        var uid = _users.GetUserId(User);
        if (uid is null) return Challenge();
        if (action is not "accept" and not "busy") return BadRequest();

        var booking = await _db.LichXemPhongs.Include(x => x.Phongtro).FirstOrDefaultAsync(x => x.Id == id);
        if (booking is null) return NotFound();
        if (booking.ChuTroId != uid) return Forbid();
        if (booking.TrangThai != TrangThaiLich.ChoXacNhan)
        {
            TempData["Error"] = "Lịch xem này đã được xử lý trước đó.";
            return RedirectToAction(nameof(Index));
        }

        lyDo = lyDo?.Trim();
        if (lyDo?.Length > 500)
        {
            TempData["Error"] = "Lý do báo bận không được vượt quá 500 ký tự.";
            return RedirectToAction(nameof(Index));
        }

        booking.TrangThai = action == "accept" ? TrangThaiLich.DaDongY : TrangThaiLich.BaoBan;
        booking.LyDoHuy = action == "accept" ? null : lyDo;
        var conversation = await GetConversationAsync(booking.NguoiThueId, uid, booking.PhongtroId);
        await _db.SaveChangesAsync();
        conversation.LanCapNhatCuoi = DateTime.UtcNow;

        var responseText = action == "accept"
            ? $"Chủ trọ đã đồng ý lịch xem phòng lúc {booking.ThoiGianXem:HH:mm} ngày {booking.ThoiGianXem:dd/MM/yyyy}."
            : $"Chủ trọ báo bận với lịch xem lúc {booking.ThoiGianXem:HH:mm} ngày {booking.ThoiGianXem:dd/MM/yyyy}." +
              (string.IsNullOrWhiteSpace(lyDo) ? string.Empty : $" Lý do: {lyDo}");
        _db.TinNhans.Add(new TinNhan
        {
            CuocTroChuyenId = conversation.Id,
            NguoiGuiId = uid,
            PhongtroId = booking.PhongtroId,
            LoaiTinNhan = BookingMessageType(booking.Id, "PhanHoi"),
            NoiDung = responseText
        });
        _db.ThongBaos.Add(new ThongBao
        {
            UserId = booking.NguoiThueId,
            TieuDe = action == "accept" ? "Lịch xem đã được đồng ý" : "Chủ trọ báo bận",
            NoiDung = responseText,
            Loai = "Booking",
            Link = $"/Messages?id={conversation.Id}"
        });

        await _db.SaveChangesAsync();
        return RedirectToAction("Index", "Messages", new { id = conversation.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var uid = _users.GetUserId(User);
        if (uid is null) return Challenge();
        var booking = await _db.LichXemPhongs.Include(x => x.Phongtro).FirstOrDefaultAsync(x => x.Id == id);
        if (booking is null) return NotFound();
        if (booking.NguoiThueId != uid) return Forbid();
        if (booking.TrangThai is not TrangThaiLich.ChoXacNhan and not TrangThaiLich.DaDongY)
        {
            TempData["Error"] = "Lịch xem này không thể hủy ở trạng thái hiện tại.";
            return RedirectToAction(nameof(Index));
        }

        booking.TrangThai = TrangThaiLich.DaHuy;
        var conversation = await GetConversationAsync(uid, booking.ChuTroId, booking.PhongtroId);
        await _db.SaveChangesAsync();
        conversation.LanCapNhatCuoi = DateTime.UtcNow;
        var message = $"Người thuê đã hủy lịch xem {booking.Phongtro.TieuDe} lúc {booking.ThoiGianXem:HH:mm} ngày {booking.ThoiGianXem:dd/MM/yyyy}.";

        _db.TinNhans.Add(new TinNhan
        {
            CuocTroChuyenId = conversation.Id,
            NguoiGuiId = uid,
            PhongtroId = booking.PhongtroId,
            LoaiTinNhan = BookingMessageType(booking.Id, "Huy"),
            NoiDung = message
        });
        _db.ThongBaos.Add(new ThongBao
        {
            UserId = booking.ChuTroId,
            TieuDe = "Lịch xem phòng đã bị hủy",
            NoiDung = message,
            Loai = "Booking",
            Link = $"/Messages?id={conversation.Id}"
        });

        await _db.SaveChangesAsync();
        return RedirectToAction("Index", "Messages", new { id = conversation.Id });
    }

    private async Task<CuocTroChuyen> GetConversationAsync(string tenantId, string ownerId, int roomId)
    {
        var conversation = await _db.CuocTroChuyens
            .FirstOrDefaultAsync(x => x.NguoiThueId == tenantId && x.ChuTroId == ownerId);
        if (conversation is not null) return conversation;

        conversation = new CuocTroChuyen
        {
            NguoiThueId = tenantId,
            ChuTroId = ownerId,
            PhongtroId = roomId
        };
        _db.CuocTroChuyens.Add(conversation);
        return conversation;
    }

    private IActionResult BookingError(int roomId, string message)
    {
        TempData["Error"] = message;
        return RedirectToAction("Details", "Phongtro", new { id = roomId });
    }

    private static DateTime VietnamNow() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
    internal static string? ValidateViewingTime(DateOnly viewingDate, TimeOnly viewingTime, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        if (viewingDate < today) return "Ngày xem phòng không được ở trong quá khứ.";
        if (viewingDate > today.AddYears(1)) return "Ngày xem phòng không được vượt quá 1 năm kể từ hôm nay.";
        if (viewingTime < OpeningTime || viewingTime > ClosingTime)
            return "Giờ xem phòng phải nằm trong khoảng 08:00 đến 20:00.";
        if (viewingTime.Minute is not 0 and not 30) return "Vui lòng chọn giờ xem theo mỗi 30 phút.";
        if (viewingDate.ToDateTime(viewingTime, DateTimeKind.Unspecified) <= now)
            return "Thời gian xem phòng phải lớn hơn thời điểm hiện tại.";
        return null;
    }

    private static string BookingMessageType(int bookingId, string eventName) => $"Lich:{bookingId}:{eventName}";
}
