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
    private readonly ApplicationDbContext _db; private readonly UserManager<ApplicationUser> _users;
    public LichXemPhongController(ApplicationDbContext db, UserManager<ApplicationUser> users) { _db = db; _users = users; }
    public async Task<IActionResult> Index(string? status) { var uid = _users.GetUserId(User)!; var owner = User.IsInRole(AppRoles.ChuTro) || User.IsInRole(AppRoles.Admin); IQueryable<LichXemPhong> q = _db.LichXemPhongs.Include(x => x.Phongtro).Include(x => x.NguoiThue).Where(x => owner ? x.ChuTroId == uid : x.NguoiThueId == uid); if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.TrangThai == status); ViewBag.IsOwner = owner; return View(await q.OrderByDescending(x => x.ThoiGianXem).ToListAsync()); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int phongtroId, string hoTen, string? soDienThoai, DateTime thoiGianXem, string? ghiChu)
    { var uid = _users.GetUserId(User)!; var room = await _db.Phongtros.FindAsync(phongtroId); if (room?.OwnerId is null) return NotFound(); if (room.OwnerId == uid) return Forbid(); if (thoiGianXem <= DateTime.Now || string.IsNullOrWhiteSpace(hoTen)) { TempData["Error"] = "Vui lòng nhập họ tên và thời gian xem trong tương lai."; return RedirectToAction("Details", "Phongtro", new { id = phongtroId }); } var booking = new LichXemPhong { PhongtroId = room.Id, NguoiThueId = uid, ChuTroId = room.OwnerId, HoTen = hoTen.Trim(), SoDienThoai = soDienThoai?.Trim(), ThoiGianXem = thoiGianXem, GhiChu = ghiChu?.Trim() }; _db.LichXemPhongs.Add(booking); var c = await GetConversation(uid, room.OwnerId, room.Id); c.LanCapNhatCuoi = DateTime.UtcNow; _db.TinNhans.Add(new TinNhan { CuocTroChuyen = c, NguoiGuiId = uid, PhongtroId = room.Id, LoaiTinNhan = "DatLich", NoiDung = $"{booking.HoTen} muốn đặt lịch xem phòng '{room.TieuDe}' lúc {thoiGianXem:HH:mm - dd/MM/yyyy}." }); _db.ThongBaos.Add(new ThongBao { UserId = room.OwnerId, TieuDe = "Yêu cầu xem phòng mới", NoiDung = $"{booking.HoTen} muốn xem {room.TieuDe}.", Loai = "Booking", Link = "/LichXemPhong" }); await _db.SaveChangesAsync(); TempData["Success"] = "Đã gửi yêu cầu đặt lịch."; return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Respond(int id, string action, string? lyDo) { var uid = _users.GetUserId(User)!; var b = await _db.LichXemPhongs.Include(x => x.Phongtro).FirstOrDefaultAsync(x => x.Id == id); if (b is null) return NotFound(); if (b.ChuTroId != uid) return Forbid(); if (b.TrangThai != TrangThaiLich.ChoXacNhan) return RedirectToAction(nameof(Index)); b.TrangThai = action == "accept" ? TrangThaiLich.DaDongY : TrangThaiLich.BaoBan; b.LyDoHuy = action == "accept" ? null : lyDo?.Trim(); var c = await _db.CuocTroChuyens.FirstOrDefaultAsync(x => x.NguoiThueId == b.NguoiThueId && x.ChuTroId == uid); if (c is not null) { c.LanCapNhatCuoi = DateTime.UtcNow; _db.TinNhans.Add(new TinNhan { CuocTroChuyenId = c.Id, NguoiGuiId = uid, PhongtroId = b.PhongtroId, LoaiTinNhan = "DatLich", NoiDung = action == "accept" ? $"Chủ trọ đã đồng ý lịch xem phòng lúc {b.ThoiGianXem:HH:mm ngày dd/MM/yyyy}." : $"Chủ trọ báo bận. {b.LyDoHuy}" }); } _db.ThongBaos.Add(new ThongBao { UserId = b.NguoiThueId, TieuDe = action == "accept" ? "Lịch xem đã được đồng ý" : "Chủ trọ báo bận", NoiDung = $"Lịch xem {b.Phongtro.TieuDe} đã được cập nhật.", Loai = "Booking", Link = "/LichXemPhong" }); await _db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id) { var uid = _users.GetUserId(User)!; var b = await _db.LichXemPhongs.FindAsync(id); if (b is null) return NotFound(); if (b.NguoiThueId != uid) return Forbid(); if (b.TrangThai is TrangThaiLich.ChoXacNhan or TrangThaiLich.DaDongY) { b.TrangThai = TrangThaiLich.DaHuy; await _db.SaveChangesAsync(); } return RedirectToAction(nameof(Index)); }
    private async Task<CuocTroChuyen> GetConversation(string tenantId, string ownerId, int roomId) { var c = await _db.CuocTroChuyens.FirstOrDefaultAsync(x => x.NguoiThueId == tenantId && x.ChuTroId == ownerId); if (c is not null) return c; c = new CuocTroChuyen { NguoiThueId = tenantId, ChuTroId = ownerId, PhongtroId = roomId }; _db.CuocTroChuyens.Add(c); return c; }
}
