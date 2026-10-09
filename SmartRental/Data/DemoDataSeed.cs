using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartRental.Models;
using SmartRental.Security;

namespace SmartRental.Data;

public static class DemoDataSeed
{
    private const int Seed = 20261009;
    private const string Password = "SmartRental.Demo!2026#";
    private const string RoomMark = "#sr-ai-v2-room-";
    private const string BookingMark = "#sr-ai-v2-booking-";
    private const string MessageMark = "Demo:AIv2:";
    private static readonly string[] AmenityNames = ["Wifi", "WC riêng", "Chỗ để xe", "Điều hòa", "Nóng lạnh", "Máy giặt", "Tủ lạnh", "Ban công", "Bếp", "Giường", "Tủ quần áo"];
    private static readonly string[] Images =
    [
        "/uploads/phong-tro-15m2-dep-13-e1692253763444.jpg.webp",
        "/uploads/trang-tri-phong-tro-dep-cho-sinh-vien-1.jpg-e1692253784511.jpg.webp",
        "/uploads/trang-tri-phong-tro-dep-cho-sinh-vien-10.png.png.webp",
        "/uploads/trang-tri-phong-tro-dep-cho-sinh-vien-11.jpg-e1692254349984.jpg.webp",
        "/uploads/trang-tri-phong-tro-dep-cho-sinh-vien-12.jpg.jpg",
        "/uploads/trang-tri-phong-tro-dep-cho-sinh-vien-13.jpg-e1692254371546.jpg.webp",
        "/uploads/trang-tri-phong-tro-dep-cho-sinh-vien-14.jpeg-e1692254539725.jpg.webp",
        "/uploads/trang-tri-phong-tro-dep-cho-sinh-vien-16.jpg-e1692254573151.jpg.webp",
        "/uploads/trang-tri-phong-tro-dep-cho-sinh-vien-18.JPG.jpg",
        "/uploads/trang-tri-phong-tro-dep-cho-sinh-vien-19.jpeg.jpg",
        "/uploads/trang-tri-phong-tro-dep-cho-sinh-vien-20.jpeg.jpg",
        "/uploads/trang-tri-phong-tro-dep-cho-sinh-vien-21.jpg.jpg.webp"
    ];
    private static readonly AreaProfile[] Areas =
    [
        new("Tân Thịnh", Tier.Near, 1.14, "Việt Bắc"), new("Tích Lương", Tier.Near, 1.10, "Z115"),
        new("Đồng Quang", Tier.Near, 1.16, "Lương Ngọc Quyến"), new("Quang Trung", Tier.Near, 1.11, "Quang Trung"),
        new("Hoàng Văn Thụ", Tier.Near, 1.17, "Bắc Kạn"), new("Phan Đình Phùng", Tier.Near, 1.13, "Minh Cầu"),
        new("Trưng Vương", Tier.Near, 1.09, "Cách Mạng Tháng 8"), new("Túc Duyên", Tier.Medium, 1.03, "Bến Oánh"),
        new("Gia Sàng", Tier.Medium, 1.00, "Bắc Nam"), new("Phú Xá", Tier.Medium, .98, "Thống Nhất"),
        new("Trung Thành", Tier.Medium, 1.02, "Cách Mạng Tháng 8"), new("Thịnh Đán", Tier.Medium, 1.04, "Quang Trung"),
        new("Quan Triều", Tier.Medium, .97, "Dương Tự Minh"), new("Tân Long", Tier.Medium, .96, "Dương Tự Minh"),
        new("Quang Vinh", Tier.Medium, .99, "Quang Vinh"), new("Cam Giá", Tier.Far, .90, "Lưu Nhân Chú"),
        new("Hương Sơn", Tier.Far, .91, "Gang Thép"), new("Đồng Bẩm", Tier.Far, .92, "Bắc Sơn"),
        new("Chùa Hang", Tier.Far, .89, "Quốc lộ 1B"), new("Quyết Thắng", Tier.Far, .94, "Đại học"),
        new("Sơn Cẩm", Tier.Far, .86, "Sơn Cẩm")
    ];
    private static readonly string[] Families = ["Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Phan", "Vũ", "Đặng", "Bùi", "Đỗ", "Hồ", "Ngô", "Dương"];
    private static readonly string[] Middles = ["Minh", "Ngọc", "Thu", "Quốc", "Thanh", "Đức", "Mai", "Khánh", "Gia", "Hải", "Phương"];
    private static readonly string[] Givens = ["An", "Anh", "Bảo", "Chi", "Dũng", "Giang", "Hà", "Hạnh", "Hiếu", "Huy", "Lan", "Linh", "Long", "Mai", "Nam", "Nga", "Phong", "Quân", "Trang", "Tú", "Vy"];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SmartRental.DemoDataSeed");
        ValidateAssets(env);
        var owners = await EnsureUsers(users, 24, true);
        var tenants = await EnsureUsers(users, 72, false);
        var amenities = await EnsureAmenities(db);
        var oldRooms = await LoadRooms(db);
        if (await IsComplete(db, oldRooms, owners, tenants)) { await Report(db, oldRooms, owners, tenants, env, log); return; }
        await RemoveGenerated(db, oldRooms, owners, tenants);
        var specs = BuildSpecs(new Random(Seed));
        var rooms = await UpsertRooms(db, oldRooms, specs, owners, amenities);
        await AddReviews(db, rooms, tenants, new Random(Seed + 1));
        var views = await AddViews(db, rooms, tenants, specs, new Random(Seed + 2));
        await AddFavorites(db, rooms, tenants, views, specs, new Random(Seed + 3));
        await AddBookings(db, rooms, tenants, specs, new Random(Seed + 4));
        await AddChats(db, rooms, tenants, specs, new Random(Seed + 5));
        await Report(db, rooms, owners, tenants, env, log);
    }

    private static List<Spec> BuildSpecs(Random random)
    {
        var sizes = new List<double>();
        AddSizes(sizes, random, 50, 10, 15); AddSizes(sizes, random, 75, 16, 20); AddSizes(sizes, random, 63, 21, 25);
        AddSizes(sizes, random, 37, 26, 30); AddSizes(sizes, random, 17, 31, 35); AddSizes(sizes, random, 8, 36, 45); Shuffle(sizes, random);
        var result = new List<Spec>();
        for (var i = 0; i < 250; i++)
        {
            var size = sizes[i]; var location = PickArea(random); var quality = Math.Clamp(random.NextDouble() * .78 + (size >= 26 ? .08 : 0), 0, 1);
            var am = PickAmenities(random, quality); var capacity = Capacity(size, random); var price = Price(size, location.Factor, am, quality, random);
            var quantity = Quantity(random); var visible = random.NextDouble() >= .07;
            var title = Title(i, size, capacity, am, location, quality);
            var address = $"{(i % 2 == 0 ? "Số" : "Ngõ")} {7 + i * 11 % 127} đường {location.Street}, phường {location.Name}, TP. Thái Nguyên";
            var description = Description(i, size, capacity, am, location, quality);
            var popularity = .75 + TierScore(location.Level) + am.Count * .10 + quality * .8 + size * 70_000d / (double)price * .55 + (quantity > 0 ? .35 : -.35) + random.NextDouble() * .35;
            result.Add(new(size, capacity, price, quantity, visible, location, am, title, address, description, popularity));
        }
        return result;
    }

    private static async Task<List<Phongtro>> UpsertRooms(ApplicationDbContext db, List<Phongtro> old, IReadOnlyList<Spec> specs, IReadOnlyList<ApplicationUser> owners, IReadOnlyDictionary<string, TienNghi> amenities)
    {
        var rooms = new List<Phongtro>();
        for (var i = 0; i < 250; i++)
        {
            var s = specs[i]; var room = i < old.Count ? old[i] : new Phongtro(); if (i >= old.Count) db.Phongtros.Add(room);
            room.TieuDe = s.Title; room.DiaChi = s.Address; room.KhuVuc = s.Location.Name; room.Gia = s.Price; room.DienTich = s.Size;
            room.MoTa = s.Description; room.HinhAnh = Images[i % Images.Length] + RoomMark + $"{i + 1:000}"; room.NgayDang = DateTime.Today.AddDays(-(i * 7 % 120));
            room.SoLuongPhong = s.Quantity; room.TrangThai = s.Quantity > 0; room.SoNguoiToiDa = s.Capacity; room.IsVisible = s.Visible; room.OwnerId = owners[i % owners.Count].Id;
            foreach (var name in s.Amenities) room.PhongTienNghis.Add(new PhongTienNghi { TienNghi = amenities[name] });
            for (var order = 0; order < 1 + i % 5; order++) room.HinhAnhs.Add(new PhongtroHinhAnh { DuongDan = Images[(i * 3 + order) % Images.Length], ThuTu = order, IsAnhChinh = order == 0 });
            rooms.Add(room);
        }
        await db.SaveChangesAsync(); return rooms;
    }

    private static async Task AddReviews(ApplicationDbContext db, IReadOnlyList<Phongtro> rooms, IReadOnlyList<ApplicationUser> tenants, Random random)
    {
        var stars = Enumerable.Repeat(5, 140).Concat(Enumerable.Repeat(4, 190)).Concat(Enumerable.Repeat(3, 110)).Concat(Enumerable.Repeat(2, 45)).Concat(Enumerable.Repeat(1, 15)).ToList();
        Shuffle(stars, random); var used = new HashSet<(string, int)>();
        for (var i = 0; i < 500; i++)
        {
            var tenant = tenants[i % tenants.Count]; var roomIndex = (i * 37 + i / tenants.Count * 11) % rooms.Count;
            while (!used.Add((tenant.Id, rooms[roomIndex].Id))) roomIndex = (roomIndex + 17) % rooms.Count;
            db.DanhGias.Add(new DanhGia { UserId = tenant.Id, PhongtroId = rooms[roomIndex].Id, SoSao = stars[i], NoiDung = Review(stars[i], i), NgayDanhGia = DateTime.Today.AddDays(-(2 + i * 13 % 240)) });
        }
        await db.SaveChangesAsync();
    }

    private static async Task<Dictionary<string, List<Phongtro>>> AddViews(ApplicationDbContext db, IReadOnlyList<Phongtro> rooms, IReadOnlyList<ApplicationUser> tenants, IReadOnlyList<Spec> specs, Random random)
    {
        var result = new Dictionary<string, List<Phongtro>>();
        for (var i = 0; i < tenants.Count; i++)
        {
            var picked = PickWeighted(rooms, specs, 14 + i % 5, random, (room, spec) => spec.Popularity * Preference(i % 4, spec)); result[tenants[i].Id] = picked;
            for (var j = 0; j < picked.Count; j++)
            {
                var spec = specs[RoomIndex(picked[j])];
                db.LichSuXems.Add(new LichSuXem { UserId = tenants[i].Id, PhongtroId = picked[j].Id, SoLanXem = Math.Clamp((int)Math.Round(spec.Popularity * 1.4 + random.NextDouble() * 5), 1, 12), LanXemCuoi = DateTime.Today.AddDays(-((i * 3 + j) % 90)) });
            }
        }
        await db.SaveChangesAsync(); return result;
    }

    private static async Task AddFavorites(ApplicationDbContext db, IReadOnlyList<Phongtro> rooms, IReadOnlyList<ApplicationUser> tenants, IReadOnlyDictionary<string, List<Phongtro>> views, IReadOnlyList<Spec> specs, Random random)
    {
        for (var i = 0; i < tenants.Count; i++)
        {
            var picked = PickWeighted(views[tenants[i].Id], specs, i % 11, random, (room, spec) => spec.Popularity);
            for (var j = 0; j < picked.Count; j++) db.YeuThichs.Add(new YeuThich { UserId = tenants[i].Id, PhongtroId = picked[j].Id, NgayThem = DateTime.Today.AddDays(-((i + j * 5) % 100)) });
        }
        await db.SaveChangesAsync();
    }

    private static async Task AddBookings(ApplicationDbContext db, IReadOnlyList<Phongtro> rooms, IReadOnlyList<ApplicationUser> tenants, IReadOnlyList<Spec> specs, Random random)
    {
        var eligible = rooms.Where(room => room.IsVisible && room.SoLuongPhong > 0).ToList();
        var statuses = Enumerable.Repeat(TrangThaiLich.ChoXacNhan, 24).Concat(Enumerable.Repeat(TrangThaiLich.DaDongY, 54)).Concat(Enumerable.Repeat(TrangThaiLich.BaoBan, 21)).Concat(Enumerable.Repeat(TrangThaiLich.DaHuy, 21)).ToList(); Shuffle(statuses, random);
        for (var i = 0; i < 120; i++)
        {
            var room = PickWeighted(eligible, specs, 1, random, (candidate, spec) => spec.Popularity)[0]; var tenant = tenants[(i * 7 + 3) % tenants.Count];
            db.LichXemPhongs.Add(new LichXemPhong { PhongtroId = room.Id, NguoiThueId = tenant.Id, ChuTroId = room.OwnerId!, HoTen = tenant.HoTen!, SoDienThoai = $"09{(i * 7919 + 1_000_000) % 100_000_000:00000000}", ThoiGianXem = DateTime.Today.AddDays(1 + i % 28).AddHours(8 + i % 10), GhiChu = BookingMark + $"{i + 1:000}", TrangThai = statuses[i], LyDoHuy = statuses[i] == TrangThaiLich.DaHuy ? "Người thuê thay đổi lịch cá nhân." : null, NgayTao = DateTime.Today.AddDays(-(1 + i % 45)) });
        }
        await db.SaveChangesAsync();
    }

    private static async Task AddChats(ApplicationDbContext db, IReadOnlyList<Phongtro> rooms, IReadOnlyList<ApplicationUser> tenants, IReadOnlyList<Spec> specs, Random random)
    {
        var pairs = new HashSet<(string, string)>(); var conversations = new List<CuocTroChuyen>();
        for (var cursor = 0; conversations.Count < 80; cursor++)
        {
            var tenant = tenants[cursor % tenants.Count]; var ownerId = rooms[(cursor * 13 + 5) % rooms.Count].OwnerId!;
            if (!pairs.Add((tenant.Id, ownerId))) continue;
            var room = PickWeighted(rooms.Where(candidate => candidate.OwnerId == ownerId).ToList(), specs, 1, random, (candidate, spec) => spec.Popularity)[0];
            var conversation = new CuocTroChuyen { NguoiThueId = tenant.Id, ChuTroId = ownerId, PhongtroId = room.Id, NgayTao = DateTime.Today.AddDays(-(3 + cursor % 100)), LanCapNhatCuoi = DateTime.Today.AddMinutes(-cursor * 17) };
            db.CuocTroChuyens.Add(conversation); conversations.Add(conversation);
        }
        await db.SaveChangesAsync();
        string[] questions = ["Phòng này hiện còn không ạ?", "Phòng có chỗ để xe không ạ?", "Điện nước và wifi tính thế nào ạ?", "Cuối tuần em qua xem phòng được không?", "Phòng có nóng lạnh và giờ giấc tự do không ạ?", "Ở hai người có phù hợp không ạ?"];
        string[] answers = ["Hiện vẫn còn phòng, em hẹn thời gian đến xem nhé.", "Có chỗ để xe khu chung và có camera em nhé.", "Điện nước theo sử dụng, wifi đã có sẵn trong khu trọ.", "Cuối tuần xem được, em báo trước khoảng một giờ nhé.", "Có nóng lạnh; giờ tự do nhưng cần yên tĩnh sau 23 giờ.", "Em xem sức chứa tối đa trên tin, phòng bố trí khá thoáng nhé."];
        for (var i = 0; i < conversations.Count; i++) for (var j = 0; j < 6; j++)
        {
            var fromTenant = j % 2 == 0; var template = (i + j / 2) % questions.Length; var conversation = conversations[i];
            db.TinNhans.Add(new TinNhan { CuocTroChuyenId = conversation.Id, PhongtroId = conversation.PhongtroId, NguoiGuiId = fromTenant ? conversation.NguoiThueId : conversation.ChuTroId, NoiDung = fromTenant ? questions[template] : answers[template], NgayGui = conversation.NgayTao.AddMinutes(j * 11 + i % 7), DaDoc = j < 5, LoaiTinNhan = MessageMark + $"{i + 1:000}:{j + 1}" });
        }
        await db.SaveChangesAsync();
    }

    private static async Task RemoveGenerated(ApplicationDbContext db, IReadOnlyList<Phongtro> rooms, IReadOnlyList<ApplicationUser> owners, IReadOnlyList<ApplicationUser> tenants)
    {
        var roomIds = rooms.Select(room => room.Id).ToList(); var tenantIds = tenants.Select(user => user.Id).ToList(); var ownerIds = owners.Select(user => user.Id).ToList();
        if (roomIds.Count > 0)
        {
            db.DanhGias.RemoveRange(await db.DanhGias.Where(x => tenantIds.Contains(x.UserId) && roomIds.Contains(x.PhongtroId)).ToListAsync());
            db.YeuThichs.RemoveRange(await db.YeuThichs.Where(x => tenantIds.Contains(x.UserId) && roomIds.Contains(x.PhongtroId)).ToListAsync());
            db.LichSuXems.RemoveRange(await db.LichSuXems.Where(x => tenantIds.Contains(x.UserId) && roomIds.Contains(x.PhongtroId)).ToListAsync());
            db.PhongTienNghis.RemoveRange(await db.PhongTienNghis.Where(x => roomIds.Contains(x.PhongtroId)).ToListAsync());
            db.PhongtroHinhAnhs.RemoveRange(await db.PhongtroHinhAnhs.Where(x => roomIds.Contains(x.PhongtroId)).ToListAsync());
        }
        db.LichXemPhongs.RemoveRange(await db.LichXemPhongs.Where(x => x.GhiChu != null && (x.GhiChu.StartsWith("#sr-demo-booking-") || x.GhiChu.StartsWith(BookingMark))).ToListAsync());
        db.TinNhans.RemoveRange(await db.TinNhans.Where(x => x.LoaiTinNhan.StartsWith("Demo:")).ToListAsync());
        db.CuocTroChuyens.RemoveRange(await db.CuocTroChuyens.Where(x => tenantIds.Contains(x.NguoiThueId) && ownerIds.Contains(x.ChuTroId)).ToListAsync());
        await db.SaveChangesAsync(); foreach (var room in rooms) { room.PhongTienNghis.Clear(); room.HinhAnhs.Clear(); }
    }

    private static async Task<List<Phongtro>> LoadRooms(ApplicationDbContext db) => await db.Phongtros.Include(x => x.PhongTienNghis).Include(x => x.HinhAnhs).AsSplitQuery().Where(x => x.HinhAnh != null && (x.HinhAnh.Contains("#sr-demo-") || x.HinhAnh.Contains(RoomMark))).OrderBy(x => x.Id).Take(250).ToListAsync();

    private static async Task<bool> IsComplete(ApplicationDbContext db, IReadOnlyList<Phongtro> rooms, IReadOnlyList<ApplicationUser> owners, IReadOnlyList<ApplicationUser> tenants)
    {
        if (rooms.Count != 250 || rooms.Any(x => x.HinhAnh?.Contains(RoomMark) != true)) return false;
        var roomIds = rooms.Select(x => x.Id).ToList(); var tenantIds = tenants.Select(x => x.Id).ToList(); var ownerIds = owners.Select(x => x.Id).ToList();
        return await db.DanhGias.CountAsync(x => tenantIds.Contains(x.UserId) && roomIds.Contains(x.PhongtroId)) >= 400
            && await db.LichSuXems.CountAsync(x => tenantIds.Contains(x.UserId) && roomIds.Contains(x.PhongtroId)) >= 1000
            && await db.YeuThichs.CountAsync(x => tenantIds.Contains(x.UserId) && roomIds.Contains(x.PhongtroId)) >= 300
            && await db.LichXemPhongs.CountAsync(x => x.GhiChu != null && x.GhiChu.StartsWith(BookingMark)) >= 80
            && await db.CuocTroChuyens.CountAsync(x => tenantIds.Contains(x.NguoiThueId) && ownerIds.Contains(x.ChuTroId)) >= 60
            && await db.TinNhans.CountAsync(x => x.LoaiTinNhan.StartsWith(MessageMark)) >= 400;
    }

    private static async Task<List<ApplicationUser>> EnsureUsers(UserManager<ApplicationUser> manager, int count, bool owner)
    {
        var result = new List<ApplicationUser>(); var prefix = owner ? "chutro" : "nguoithue"; var role = owner ? AppRoles.ChuTro : AppRoles.NguoiThue;
        for (var i = 1; i <= count; i++)
        {
            var email = $"{prefix}{i}@smartrental.local"; var user = await manager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, HoTen = NaturalName(i + (owner ? 0 : 31)), DiaChi = "TP. Thái Nguyên" };
                Ensure(await manager.CreateAsync(user, Password), $"tạo {email}");
            }
            if (!await manager.IsInRoleAsync(user, role)) Ensure(await manager.AddToRoleAsync(user, role), $"gán role {role}"); result.Add(user);
        }
        return result;
    }

    private static async Task<Dictionary<string, TienNghi>> EnsureAmenities(ApplicationDbContext db)
    {
        var existing = await db.TienNghis.ToListAsync();
        foreach (var name in AmenityNames) if (!existing.Any(x => x.TenTienNghi.Equals(name, StringComparison.OrdinalIgnoreCase))) { var item = new TienNghi { TenTienNghi = name }; db.TienNghis.Add(item); existing.Add(item); }
        await db.SaveChangesAsync(); return AmenityNames.ToDictionary(name => name, name => existing.First(x => x.TenTienNghi.Equals(name, StringComparison.OrdinalIgnoreCase)));
    }

    private static async Task Report(ApplicationDbContext db, IReadOnlyList<Phongtro> rooms, IReadOnlyList<ApplicationUser> owners, IReadOnlyList<ApplicationUser> tenants, IWebHostEnvironment env, ILogger log)
    {
        var ids = rooms.Select(x => x.Id).ToList(); var tenantIds = tenants.Select(x => x.Id).ToList(); var ownerIds = owners.Select(x => x.Id).ToList();
        var data = await db.Phongtros.AsNoTracking().Include(x => x.PhongTienNghis).Include(x => x.HinhAnhs).Where(x => ids.Contains(x.Id)).ToListAsync();
        var invalidArea = data.Count(x => x.DienTich is < 10 or > 45); var invalidPrice = data.Count(x => x.Gia < 400_000); var invalidQuantity = data.Count(x => x.SoLuongPhong < 0);
        var invalidCapacity = data.Count(x => x.SoNguoiToiDa < 1 || x.DienTich <= 14 && x.SoNguoiToiDa > 1 || x.DienTich <= 19 && x.SoNguoiToiDa > 2 || x.DienTich <= 24 && x.SoNguoiToiDa > 3 || x.DienTich <= 30 && x.SoNguoiToiDa > 3 || x.SoNguoiToiDa > 4);
        var invalidLocation = data.Count(x => !ThaiNguyenAreas.All.Contains(x.KhuVuc)); var noImage = data.Count(x => x.HinhAnhs.Count == 0);
        var invalidImages = data.Count(x => x.HinhAnhs.Count > 10 || x.HinhAnhs.Count(y => y.IsAnhChinh) != 1); var broken = data.SelectMany(x => x.HinhAnhs).Count(x => !File.Exists(Physical(env, x.DuongDan)));
        var duplicateReview = await db.DanhGias.Where(x => tenantIds.Contains(x.UserId) && ids.Contains(x.PhongtroId)).GroupBy(x => new { x.UserId, x.PhongtroId }).CountAsync(x => x.Count() > 1);
        var duplicateFavorite = await db.YeuThichs.Where(x => tenantIds.Contains(x.UserId) && ids.Contains(x.PhongtroId)).GroupBy(x => new { x.UserId, x.PhongtroId }).CountAsync(x => x.Count() > 1);
        var duplicateConversation = await db.CuocTroChuyens.Where(x => tenantIds.Contains(x.NguoiThueId) && ownerIds.Contains(x.ChuTroId)).GroupBy(x => new { x.NguoiThueId, x.ChuTroId }).CountAsync(x => x.Count() > 1);
        var duplicateAmenity = data.Sum(x => x.PhongTienNghis.GroupBy(y => y.TienNghiId).Count(y => y.Count() > 1));
        var invalidRating = await db.DanhGias.CountAsync(x => tenantIds.Contains(x.UserId) && ids.Contains(x.PhongtroId) && (x.SoSao < 1 || x.SoSao > 5));
        var invalidBooking = await db.LichXemPhongs.CountAsync(x => x.GhiChu != null && x.GhiChu.StartsWith(BookingMark) && (!x.Phongtro.IsVisible || x.Phongtro.SoLuongPhong == 0));
        var reviews = await db.DanhGias.CountAsync(x => tenantIds.Contains(x.UserId) && ids.Contains(x.PhongtroId)); var viewRelations = await db.LichSuXems.CountAsync(x => tenantIds.Contains(x.UserId) && ids.Contains(x.PhongtroId));
        var totalViews = await db.LichSuXems.Where(x => tenantIds.Contains(x.UserId) && ids.Contains(x.PhongtroId)).SumAsync(x => x.SoLanXem); var favorites = await db.YeuThichs.CountAsync(x => tenantIds.Contains(x.UserId) && ids.Contains(x.PhongtroId));
        var bookings = await db.LichXemPhongs.CountAsync(x => x.GhiChu != null && x.GhiChu.StartsWith(BookingMark)); var conversations = await db.CuocTroChuyens.CountAsync(x => tenantIds.Contains(x.NguoiThueId) && ownerIds.Contains(x.ChuTroId)); var messages = await db.TinNhans.CountAsync(x => x.LoaiTinNhan.StartsWith(MessageMark));
        var sizeReport = string.Join("; ", data.GroupBy(SizeBand).OrderBy(x => x.Min(y => y.DienTich)).Select(x => $"{x.Key}: {x.Count()} phòng/TB {x.Average(y => y.Gia):N0}đ"));
        var tierReport = string.Join("; ", Enum.GetValues<Tier>().Select(level => { var group = data.Where(x => Areas.First(y => y.Name == x.KhuVuc).Level == level).ToList(); return $"{level}: {group.Count} phòng/TB {group.Average(x => x.Gia):N0}đ"; }));
        log.LogInformation("Demo report | Rooms={Rooms}, Landlords={Owners}, Tenants={Tenants}, Reviews={Reviews}, ViewRelations={ViewRelations}, TotalViews={TotalViews}, Favorites={Favorites}, Bookings={Bookings}, Conversations={Conversations}, Messages={Messages} | Area min/avg/max={MinArea}/{AvgArea:N1}/{MaxArea}; Price min/avg/max={MinPrice:N0}/{AvgPrice:N0}/{MaxPrice:N0}; CapacityAvg={Capacity:N1}; QuantityAvg={Quantity:N1}; AmenitiesAvg={Amenities:N1} | Size: {Size} | Tier: {Tier} | available={Available}, unavailable={Unavailable}, visible={Visible}, hidden={Hidden} | invalid area={InvalidArea}, price={InvalidPrice}, capacity={InvalidCapacity}, quantity={InvalidQuantity}, location={InvalidLocation}, no image={NoImage}, invalid images={InvalidImages}, broken={Broken}, duplicate review={DuplicateReview}, favorite={DuplicateFavorite}, conversation={DuplicateConversation}, amenity={DuplicateAmenity}, rating={InvalidRating}, booking={InvalidBooking}",
            data.Count, owners.Count, tenants.Count, reviews, viewRelations, totalViews, favorites, bookings, conversations, messages, data.Min(x => x.DienTich), data.Average(x => x.DienTich), data.Max(x => x.DienTich), data.Min(x => x.Gia), data.Average(x => x.Gia), data.Max(x => x.Gia), data.Average(x => x.SoNguoiToiDa), data.Average(x => x.SoLuongPhong), data.Average(x => x.PhongTienNghis.Count), sizeReport, tierReport, data.Count(x => x.SoLuongPhong > 0), data.Count(x => x.SoLuongPhong == 0), data.Count(x => x.IsVisible), data.Count(x => !x.IsVisible), invalidArea, invalidPrice, invalidCapacity, invalidQuantity, invalidLocation, noImage, invalidImages, broken, duplicateReview, duplicateFavorite, duplicateConversation, duplicateAmenity, invalidRating, invalidBooking);
        var invalid = invalidArea + invalidPrice + invalidCapacity + invalidQuantity + invalidLocation + noImage + invalidImages + broken + duplicateReview + duplicateFavorite + duplicateConversation + duplicateAmenity + invalidRating + invalidBooking;
        if (invalid > 0) throw new InvalidOperationException($"Demo dataset validation failed: {invalid} invalid item(s). See report.");
    }

    private static AreaProfile PickArea(Random random)
    {
        var roll = random.NextDouble(); var tier = roll < .43 ? Tier.Near : roll < .79 ? Tier.Medium : Tier.Far; var candidates = Areas.Where(x => x.Level == tier).ToArray(); return candidates[random.Next(candidates.Length)];
    }
    private static List<string> PickAmenities(Random random, double quality)
    {
        var result = new List<string>();
        foreach (var name in AmenityNames)
        {
            var chance = name switch { "Wifi" => .82, "Chỗ để xe" => .76, "WC riêng" => .65, "Nóng lạnh" => .42, "Điều hòa" => .34, "Ban công" => .24, _ => .20 };
            if (random.NextDouble() < Math.Min(.94, chance + quality * .32)) result.Add(name);
        }
        while (result.Count < 2) { var name = AmenityNames[random.Next(AmenityNames.Length)]; if (!result.Contains(name)) result.Add(name); } return result;
    }
    private static decimal Price(double size, double factor, IReadOnlyCollection<string> amenities, double quality, Random random)
    {
        var range = size switch { <= 15 => (450_000m, 1_000_000m), <= 20 => (650_000m, 1_300_000m), <= 25 => (850_000m, 1_700_000m), <= 30 => (1_100_000m, 2_200_000m), <= 35 => (1_400_000m, 2_800_000m), _ => (1_800_000m, 3_800_000m) };
        var basePrice = range.Item1 + (range.Item2 - range.Item1) * (decimal)(.25 + size % 5 / 5 * .35);
        var adjustment = amenities.Sum(name => name switch { "Điều hòa" => 120_000m, "Nóng lạnh" => 90_000m, "WC riêng" => 110_000m, "Tủ lạnh" => 100_000m, "Máy giặt" => 80_000m, "Bếp" => 75_000m, "Ban công" => 60_000m, _ => 35_000m });
        var price = (basePrice * (decimal)factor + adjustment + (decimal)quality * 130_000m) * (decimal)(.94 + random.NextDouble() * .14);
        price = Math.Clamp(price, Math.Max(400_000m, range.Item1 * .82m), range.Item2 * 1.16m); return Math.Round(price / 50_000m, MidpointRounding.AwayFromZero) * 50_000m;
    }
    private static int Capacity(double size, Random random) => size switch { <= 14 => 1, <= 19 => random.NextDouble() < .58 ? 1 : 2, <= 24 => random.NextDouble() < .10 ? 3 : random.Next(1, 3), <= 30 => random.Next(2, 4), <= 35 => random.Next(2, 5), _ => random.Next(3, 5) };
    private static int Quantity(Random random) => random.NextDouble() switch { < .13 => 0, < .36 => 1, < .59 => 2, < .77 => 3, < .93 => 4 + random.Next(2), _ => 6 + random.Next(3) };
    private static string Title(int i, double size, int capacity, IReadOnlyCollection<string> amenities, AreaProfile area, double quality)
    {
        var feature = amenities.Contains("Điều hòa") && amenities.Contains("Nóng lạnh") ? "có điều hòa, nóng lạnh" : amenities.Contains("Ban công") ? "có ban công thoáng" : amenities.Contains("WC riêng") ? "khép kín" : amenities.Contains("Chỗ để xe") ? "có chỗ để xe" : "giá hợp lý";
        return (i % 8) switch { 0 => $"Phòng {size:N0}m² {feature} tại {area.Name}", 1 => $"Phòng trọ {feature}, phù hợp {capacity} người", 2 => $"Phòng giá sinh viên khu {area.Name}", 3 => $"Phòng {(quality > .6 ? "mới sửa" : "gọn gàng")} {size:N0}m²", 4 => $"Phòng yên tĩnh tại {area.Name}, {feature}", 5 => $"Phòng sáng thoáng, phù hợp {capacity} người", 6 => $"Phòng {size:N0}m² gần tiện ích khu {area.Name}", _ => $"Phòng trọ {feature}, giờ giấc linh hoạt" };
    }
    private static string Description(int i, double size, int capacity, IReadOnlyCollection<string> amenities, AreaProfile area, double quality)
    {
        var audience = capacity == 1 ? "một sinh viên hoặc người mới đi làm" : capacity == 2 ? "hai người hoặc cặp đôi" : $"nhóm tối đa {capacity} người";
        var caveat = (i % 9) switch { 0 => "Lối vào hơi hẹp vào giờ đông.", 3 => "Khu chung cần giữ yên tĩnh sau 23 giờ.", 6 => "Phòng ở tầng trên nên cần đi cầu thang.", _ => "Giờ giấc linh hoạt và khu dân cư an ninh." };
        return $"Phòng {size:N0}m² {(quality > .58 ? "sáng, còn mới" : "gọn gàng, dễ bố trí đồ")}, phù hợp {audience}. Thuộc khu {area.Name}, có {string.Join(", ", amenities.Take(4).Select(x => x.ToLowerInvariant()))}. {caveat}";
    }
    private static string Review(int stars, int i)
    {
        string[][] text = [["Phòng ồn và vệ sinh chưa tốt, trải nghiệm không như mong đợi.", "Đường vào bất tiện, wifi yếu và chủ trọ phản hồi chậm."], ["Phòng khá bí, wifi chập chờn và chỗ để xe hơi chật.", "Giá chưa tương xứng với diện tích, khu vực tối hơi ồn."], ["Phòng ở mức ổn, giá hợp lý nhưng cách âm chưa tốt.", "Tiện nghi đủ dùng, đường thuận tiện nhưng khu chung đôi lúc chưa sạch."], ["Phòng sạch và wifi ổn, chỉ hơi ồn vào giờ cao điểm.", "Giá và diện tích khá hợp lý, chỗ để xe đôi lúc đông."], ["Phòng sạch, chủ thân thiện, vị trí thuận tiện và giá hợp lý.", "Wifi ổn định, nóng lạnh tốt, khu trọ an ninh và hàng xóm dễ chịu."]]; return text[stars - 1][i % 2];
    }
    private static List<Phongtro> PickWeighted(IReadOnlyList<Phongtro> candidates, IReadOnlyList<Spec> specs, int count, Random random, Func<Phongtro, Spec, double> weight) => candidates.Select(room => (Room: room, Key: Math.Pow(random.NextDouble(), 1 / Math.Max(.01, weight(room, specs[RoomIndex(room)]))))).OrderByDescending(x => x.Key).Take(Math.Min(count, candidates.Count)).Select(x => x.Room).ToList();
    private static int RoomIndex(Phongtro room) => int.TryParse(room.HinhAnh?.Split(RoomMark).LastOrDefault(), out var number) ? number - 1 : throw new InvalidOperationException("Invalid demo room marker.");
    private static double Preference(int profile, Spec room) => profile switch { 0 => room.Price <= 1_500_000 && room.Size <= 25 ? 1.8 : .45, 1 => room.Size is >= 20 and <= 35 && room.Capacity >= 2 ? 1.7 : .55, 2 => room.Amenities.Contains("Điều hòa") && room.Amenities.Contains("Nóng lạnh") ? 1.8 : .65, _ => room.Location.Level == Tier.Near ? 1.8 : room.Location.Level == Tier.Medium ? 1 : .55 };
    private static string NaturalName(int i) => $"{Families[i % Families.Length]} {Middles[i * 5 % Middles.Length]} {Givens[i * 7 % Givens.Length]}";
    private static void ValidateAssets(IWebHostEnvironment env) { var missing = Images.Where(x => !File.Exists(Physical(env, x))).ToList(); if (missing.Count > 0) throw new InvalidOperationException($"Missing demo images: {string.Join(", ", missing)}"); }
    private static string Physical(IWebHostEnvironment env, string path) => Path.Combine(env.WebRootPath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
    private static string SizeBand(Phongtro room) => room.DienTich switch { <= 15 => "10-15m²", <= 20 => "16-20m²", <= 25 => "21-25m²", <= 30 => "26-30m²", <= 35 => "31-35m²", _ => "36-45m²" };
    private static double TierScore(Tier tier) => tier switch { Tier.Near => .8, Tier.Medium => .45, _ => .15 };
    private static void AddSizes(ICollection<double> target, Random random, int count, int min, int max) { for (var i = 0; i < count; i++) target.Add(random.Next(min, max + 1)); }
    private static void Shuffle<T>(IList<T> values, Random random) { for (var i = values.Count - 1; i > 0; i--) { var j = random.Next(i + 1); (values[i], values[j]) = (values[j], values[i]); } }
    private static void Ensure(IdentityResult result, string operation) { if (!result.Succeeded) throw new InvalidOperationException($"Không thể {operation}: {string.Join("; ", result.Errors.Select(x => x.Description))}"); }
    private sealed record AreaProfile(string Name, Tier Level, double Factor, string Street);
    private sealed record Spec(double Size, int Capacity, decimal Price, int Quantity, bool Visible, AreaProfile Location, List<string> Amenities, string Title, string Address, string Description, double Popularity);
    private enum Tier { Near, Medium, Far }
}
