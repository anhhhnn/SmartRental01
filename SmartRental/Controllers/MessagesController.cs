using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartRental.Data;
using SmartRental.Models;
using SmartRental.Security;
using SmartRental.ViewModels.Messages;

namespace SmartRental.Controllers;

[Authorize]
public class MessagesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    public MessagesController(ApplicationDbContext db, UserManager<ApplicationUser> users) { _db = db; _users = users; }

    public async Task<IActionResult> Index(int? id)
    {
        var uid = _users.GetUserId(User)!;
        var conversations = await _db.CuocTroChuyens.Where(x => x.NguoiThueId == uid || x.ChuTroId == uid)
            .Include(x => x.NguoiThue).Include(x => x.ChuTro).Include(x => x.TinNhans).OrderByDescending(x => x.LanCapNhatCuoi).ToListAsync();
        var selected = id.HasValue ? conversations.FirstOrDefault(x => x.Id == id) : conversations.FirstOrDefault();
        if (id.HasValue && selected is null) return Forbid();
        var messages = new List<TinNhan>(); var bookings = new List<LichXemPhong>();
        if (selected is not null)
        {
            var unread = await _db.TinNhans.Where(x => x.CuocTroChuyenId == selected.Id && x.NguoiGuiId != uid && !x.DaDoc).ToListAsync();
            unread.ForEach(x => x.DaDoc = true); if (unread.Count > 0) await _db.SaveChangesAsync();
            messages = await _db.TinNhans.Where(x => x.CuocTroChuyenId == selected.Id).Include(x => x.NguoiGui).Include(x => x.Phongtro).OrderBy(x => x.NgayGui).ToListAsync();
            bookings = await _db.LichXemPhongs.Where(x => x.NguoiThueId == selected.NguoiThueId && x.ChuTroId == selected.ChuTroId).Include(x => x.Phongtro).OrderByDescending(x => x.NgayTao).ToListAsync();
        }
        return View(new MessagesIndexViewModel { CurrentUserId = uid, Conversations = conversations, SelectedConversation = selected, Messages = messages, Bookings = bookings, IsCurrentUserOwner = selected?.ChuTroId == uid || User.IsInRole(AppRoles.Admin) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int phongtroId)
    {
        var uid = _users.GetUserId(User)!; var room = await _db.Phongtros.AsNoTracking().FirstOrDefaultAsync(x => x.Id == phongtroId);
        if (room is null) return NotFound(); if (string.IsNullOrWhiteSpace(room.OwnerId) || room.OwnerId == uid) return Forbid();
        var conversation = await _db.CuocTroChuyens.FirstOrDefaultAsync(x => x.NguoiThueId == uid && x.ChuTroId == room.OwnerId);
        if (conversation is null) { conversation = new CuocTroChuyen { PhongtroId = room.Id, NguoiThueId = uid, ChuTroId = room.OwnerId }; _db.CuocTroChuyens.Add(conversation); }
        _db.TinNhans.Add(new TinNhan { CuocTroChuyen = conversation, NguoiGuiId = uid, PhongtroId = room.Id, LoaiTinNhan = "PhongTro", NoiDung = $"Bạn đang hỏi về phòng: {room.TieuDe}" });
        conversation.LanCapNhatCuoi = DateTime.UtcNow; await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { id = conversation.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(int conversationId, string noiDung)
    {
        var uid = _users.GetUserId(User)!; var conversation = await _db.CuocTroChuyens.FindAsync(conversationId);
        if (conversation is null) return NotFound(); if (conversation.NguoiThueId != uid && conversation.ChuTroId != uid) return Forbid();
        if (string.IsNullOrWhiteSpace(noiDung) || noiDung.Trim().Length > 2000) { TempData["Error"] = "Tin nhắn không hợp lệ."; return RedirectToAction(nameof(Index), new { id = conversationId }); }
        var recipient = conversation.NguoiThueId == uid ? conversation.ChuTroId : conversation.NguoiThueId;
        _db.TinNhans.Add(new TinNhan { CuocTroChuyenId = conversationId, NguoiGuiId = uid, NoiDung = noiDung.Trim() }); conversation.LanCapNhatCuoi = DateTime.UtcNow;
        _db.ThongBaos.Add(new ThongBao { UserId = recipient, TieuDe = "Tin nhắn mới", NoiDung = "Bạn có một tin nhắn mới.", Loai = "Message", Link = "/Messages?id=" + conversationId });
        await _db.SaveChangesAsync(); return RedirectToAction(nameof(Index), new { id = conversationId });
    }
}
