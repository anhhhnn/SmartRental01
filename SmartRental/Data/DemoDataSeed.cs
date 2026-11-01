using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartRental.Models;
using SmartRental.Security;
using System.Diagnostics;

namespace SmartRental.Data;

public static class DemoDataSeed
{
    private const int Seed = 20261009;
    private const int ExpansionSeed = 20261027;
    private const int InitialRoomCount = 250;
    private const int TargetRoomCount = 450;
    private const int AdditionalReviewCount = 400;
    private const int AdditionalBookingCount = 120;
    private const int TargetConversationCount = 160;
    private const string Password = "SmartRental.Demo!2026#";
    private const string RoomMark = "#sr-ai-v2-room-";
    private const string BookingMark = "#sr-ai-v2-booking-";
    private const string MessageMark = "Demo:AIv2:";
    private const int BatchSize = 100;
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
        if (!env.IsDevelopment()) return;

        db.Database.SetCommandTimeout(120);
        var total = Stopwatch.StartNew();
        var validImagePaths = ValidateAssets(env);
        if (await IsCompleteFast(db))
        {
            log.LogInformation("Demo dataset already seeded. Skipping. ({Elapsed:F1}s)", total.Elapsed.TotalSeconds);
            return;
        }

        var phase = Stopwatch.StartNew();
        var (owners, tenants) = await EnsureUsers(db, users);
        LogPhase(log, "Users", phase);
        db.ChangeTracker.Clear();

        phase.Restart();
        var amenities = await EnsureAmenities(db);
        var oldRooms = await LoadRooms(db);
        var specs = BuildSpecs(new Random(Seed));
        var maximumRoomIndex = oldRooms.Select(room => RoomIndex(room)).DefaultIfEmpty(-1).Max();
        var specCount = Math.Max(TargetRoomCount, Math.Max(oldRooms.Count, maximumRoomIndex + 1));
        var expansionSpecs = BuildExpansionSpecs(new Random(ExpansionSeed), specCount - InitialRoomCount);
        var allSpecs = specs.Concat(expansionSpecs).ToList();
        var roomSeed = await EnsureRooms(db, oldRooms, allSpecs, owners);
        var baseRooms = roomSeed.All.Where(room => RoomIndex(room) < InitialRoomCount).ToList();
        var expansionRooms = roomSeed.All.Where(room => RoomIndex(room) >= InitialRoomCount).ToList();
        log.LogInformation("[Seed] Rooms: before={Before}, added={Added}, after={After}", oldRooms.Count, roomSeed.Added.Count, roomSeed.All.Count);
        LogPhase(log, "Rooms", phase);
        db.ChangeTracker.Clear();

        phase.Restart();
        await EnsureRoomAmenities(db, roomSeed.All, allSpecs, amenities);
        LogPhase(log, "Amenities", phase);

        phase.Restart();
        await EnsureGallery(db, roomSeed.All);
        LogPhase(log, "Images", phase);

        phase.Restart();
        var reviews = BuildReviews(baseRooms, tenants, new Random(Seed + 1))
            .Concat(BuildExpansionReviews(expansionRooms, tenants, new Random(ExpansionSeed + 1)))
            .ToList();
        await EnsureReviews(db, reviews);
        LogPhase(log, "Reviews", phase);

        phase.Restart();
        var (viewEntities, viewsByTenant) = BuildViews(baseRooms, tenants, specs, new Random(Seed + 2));
        var expansionViews = BuildExpansionViews(expansionRooms, tenants, allSpecs, new Random(ExpansionSeed + 2));
        await EnsureViews(db, viewEntities.Concat(expansionViews).ToList());
        LogPhase(log, "Views", phase);

        phase.Restart();
        var favorites = BuildFavorites(tenants, viewsByTenant, specs, new Random(Seed + 3))
            .Concat(BuildExpansionFavorites(expansionRooms, tenants, expansionViews, allSpecs, new Random(ExpansionSeed + 3)))
            .ToList();
        await EnsureFavorites(db, favorites);
        LogPhase(log, "Favorites", phase);

        phase.Restart();
        var bookings = BuildBookings(baseRooms, tenants, specs, new Random(Seed + 4))
            .Concat(BuildExpansionBookings(expansionRooms, tenants, allSpecs, new Random(ExpansionSeed + 4)))
            .ToList();
        await EnsureBookings(db, bookings);
        LogPhase(log, "Bookings", phase);

        phase.Restart();
        var conversations = await EnsureConversations(db, BuildConversations(roomSeed.All, tenants, allSpecs, new Random(Seed + 5), TargetConversationCount));
        LogPhase(log, "Conversations", phase);

        phase.Restart();
        await EnsureMessages(db, BuildMessages(conversations));
        LogPhase(log, "Messages", phase);

        phase.Restart();
        // The existing demo dataset does not define notifications; keep this phase explicit and non-destructive.
        LogPhase(log, "Notifications", phase);

        await Report(db, roomSeed.All, owners, tenants, validImagePaths, log);
        log.LogInformation("[Seed] TOTAL: {Elapsed:F1}s", total.Elapsed.TotalSeconds);
    }

    private static List<Spec> BuildSpecs(Random random)
    {
        var sizes = new List<double>();
        AddSizes(sizes, random, 50, 10, 15); AddSizes(sizes, random, 75, 16, 20); AddSizes(sizes, random, 63, 21, 25);
        AddSizes(sizes, random, 37, 26, 30); AddSizes(sizes, random, 17, 31, 35); AddSizes(sizes, random, 8, 36, 45); Shuffle(sizes, random);
        var result = new List<Spec>();
        for (var i = 0; i < InitialRoomCount; i++)
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

    private static List<Spec> BuildExpansionSpecs(Random random, int count)
    {
        var sizes = new List<double>(count);
        AddSizes(sizes, random, count * 20 / 100, 10, 15);
        AddSizes(sizes, random, count * 30 / 100, 16, 20);
        AddSizes(sizes, random, count * 25 / 100, 21, 25);
        AddSizes(sizes, random, count * 15 / 100, 26, 30);
        AddSizes(sizes, random, count * 7 / 100, 31, 35);
        AddSizes(sizes, random, count - sizes.Count, 36, 45);
        Shuffle(sizes, random);

        var result = new List<Spec>(count);
        for (var i = 0; i < count; i++)
        {
            var globalIndex = InitialRoomCount + i;
            var size = sizes[i];
            var location = Areas[random.Next(Areas.Length)];
            var quality = Math.Clamp(random.NextDouble() * .82 + (size >= 26 ? .08 : 0), 0, 1);
            var amenities = PickExpansionAmenities(random, quality);
            var capacity = Capacity(size, random);
            var price = ExpansionPrice(size, location.Factor, amenities, quality, random);
            var quantity = Quantity(random);
            var visible = random.NextDouble() >= .07;
            var addressNumber = 7 + globalIndex * 11 % 127;
            var title = ExpansionTitle(globalIndex, size, capacity, amenities, location, quality, addressNumber);
            var address = $"{(globalIndex % 2 == 0 ? "Số" : "Ngõ")} {addressNumber} đường {location.Street}, phường {location.Name}, TP. Thái Nguyên";
            var description = Description(globalIndex, size, capacity, amenities, location, quality);
            var popularity = .75 + TierScore(location.Level) + amenities.Count * .10 + quality * .8
                + size * 70_000d / (double)price * .55 + (quantity > 0 ? .35 : -.35) + random.NextDouble() * .35;
            result.Add(new(size, capacity, price, quantity, visible, location, amenities, title, address, description, popularity));
        }

        return result;
    }

    private static async Task<RoomSeedResult> EnsureRooms(ApplicationDbContext db, List<Phongtro> old, IReadOnlyList<Spec> specs, IReadOnlyList<ApplicationUser> owners)
    {
        var roomsByIndex = new Dictionary<int, Phongtro>();
        foreach (var room in old)
        {
            if (!TryGetRoomIndex(room, out var index) || index >= specs.Count)
            {
                throw new InvalidOperationException($"Demo room {room.Id} has an unsupported room marker.");
            }

            if (!roomsByIndex.TryAdd(index, room))
            {
                throw new InvalidOperationException($"Duplicate demo room sequence {index + 1}.");
            }
        }

        var desiredCount = Math.Max(TargetRoomCount, old.Count);
        var roomsToAdd = Math.Max(0, desiredCount - old.Count);
        var newRooms = new List<Phongtro>(roomsToAdd);
        for (var index = 0; index < specs.Count && newRooms.Count < roomsToAdd; index++)
        {
            if (roomsByIndex.ContainsKey(index)) continue;

            var spec = specs[index];
            var point = GeoCoordinateGenerator.GenerateForArea(spec.Location.Name, index + 1);
            var room = new Phongtro
            {
                TieuDe = spec.Title,
                DiaChi = spec.Address,
                KhuVuc = spec.Location.Name,
                Latitude = point?.Latitude,
                Longitude = point?.Longitude,
                Gia = spec.Price,
                DienTich = spec.Size,
                MoTa = spec.Description,
                HinhAnh = Images[index % Images.Length] + RoomMark + $"{index + 1:000}",
                NgayDang = DateTime.Today.AddDays(-((index * 7 + ExpansionSeed) % 120)),
                TrangThai = true,
                SoLuongPhong = spec.Quantity,
                SoNguoiToiDa = spec.Capacity,
                IsVisible = spec.Visible,
                OwnerId = owners[index % owners.Count].Id
            };
            roomsByIndex.Add(index, room);
            newRooms.Add(room);
        }

        if (newRooms.Count != roomsToAdd)
        {
            throw new InvalidOperationException("Unable to allocate stable demo room sequences for the target count.");
        }

        if (newRooms.Count > 0)
        {
            await db.Phongtros.AddRangeAsync(newRooms);
            await db.SaveChangesAsync();
        }

        var allRooms = roomsByIndex.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList();
        return new RoomSeedResult(allRooms, newRooms);
    }

    private static bool TryGetRoomIndex(Phongtro room, out int index)
    {
        foreach (var marker in new[] { RoomMark, "#sr-demo-" })
        {
            var markerPosition = room.HinhAnh?.IndexOf(marker, StringComparison.Ordinal);
            if (markerPosition is null or < 0) continue;

            var start = markerPosition.Value + marker.Length;
            var end = start;
            while (end < room.HinhAnh!.Length && char.IsDigit(room.HinhAnh[end])) end++;
            if (end > start && int.TryParse(room.HinhAnh.AsSpan(start, end - start), out var sequence) && sequence > 0)
            {
                index = sequence - 1;
                return true;
            }
        }

        index = -1;
        return false;
    }

    private static async Task EnsureRoomAmenities(ApplicationDbContext db, IReadOnlyList<Phongtro> rooms, IReadOnlyList<Spec> specs, IReadOnlyDictionary<string, TienNghi> amenities)
    {
        var roomIds = rooms.Select(x => x.Id).ToList();
        var existing = (await db.PhongTienNghis.AsNoTracking().Where(x => roomIds.Contains(x.PhongtroId))
            .Select(x => new { x.PhongtroId, x.TienNghiId }).ToListAsync())
            .Select(x => (x.PhongtroId, x.TienNghiId)).ToHashSet();
        var missing = new List<PhongTienNghi>();
        foreach (var room in rooms)
        {
            foreach (var name in specs[RoomIndex(room)].Amenities)
            {
                var key = (RoomId: room.Id, AmenityId: amenities[name].Id);
                if (existing.Add(key)) missing.Add(new PhongTienNghi { PhongtroId = key.RoomId, TienNghiId = key.AmenityId });
            }
        }
        await SaveInBatchesAsync(db, missing);
    }

    private static async Task EnsureGallery(ApplicationDbContext db, IReadOnlyList<Phongtro> rooms)
    {
        var roomIds = rooms.Select(x => x.Id).ToList();
        var existing = (await db.PhongtroHinhAnhs.AsNoTracking().Where(x => roomIds.Contains(x.PhongtroId))
            .Select(x => new { x.PhongtroId, x.ThuTu }).ToListAsync())
            .Select(x => (x.PhongtroId, x.ThuTu)).ToHashSet();
        var missing = new List<PhongtroHinhAnh>();
        foreach (var room in rooms)
        {
            var roomIndex = RoomIndex(room);
            for (var order = 0; order < 1 + roomIndex % 5; order++)
            {
                if (existing.Add((room.Id, order))) missing.Add(new PhongtroHinhAnh
                {
                    PhongtroId = room.Id,
                    DuongDan = Images[(roomIndex * 3 + order) % Images.Length],
                    ThuTu = order,
                    IsAnhChinh = order == 0
                });
            }
        }
        await SaveInBatchesAsync(db, missing);
    }

    private static List<DanhGia> BuildReviews(IReadOnlyList<Phongtro> rooms, IReadOnlyList<ApplicationUser> tenants, Random random)
    {
        var stars = Enumerable.Repeat(5, 140).Concat(Enumerable.Repeat(4, 190)).Concat(Enumerable.Repeat(3, 110)).Concat(Enumerable.Repeat(2, 45)).Concat(Enumerable.Repeat(1, 15)).ToList();
        Shuffle(stars, random); var used = new HashSet<(string, int)>(); var result = new List<DanhGia>(500);
        for (var i = 0; i < 500; i++)
        {
            var tenant = tenants[i % tenants.Count]; var roomIndex = (i * 37 + i / tenants.Count * 11) % rooms.Count;
            while (!used.Add((tenant.Id, rooms[roomIndex].Id))) roomIndex = (roomIndex + 17) % rooms.Count;
            result.Add(new DanhGia { UserId = tenant.Id, PhongtroId = rooms[roomIndex].Id, SoSao = stars[i], NoiDung = Review(stars[i], i), NgayDanhGia = DateTime.Today.AddDays(-(2 + i * 13 % 240)) });
        }
        return result;
    }

    private static List<DanhGia> BuildExpansionReviews(IReadOnlyList<Phongtro> rooms, IReadOnlyList<ApplicationUser> tenants, Random random)
    {
        var count = rooms.Count * 2;
        var stars = Enumerable.Repeat(5, count * 45 / 100)
            .Concat(Enumerable.Repeat(4, count * 35 / 100))
            .Concat(Enumerable.Repeat(3, count * 15 / 100))
            .Concat(Enumerable.Repeat(2, count * 4 / 100))
            .Concat(Enumerable.Repeat(1, count - (count * 45 / 100 + count * 35 / 100 + count * 15 / 100 + count * 4 / 100)))
            .ToList();
        Shuffle(stars, random);
        var result = new List<DanhGia>(count);
        for (var i = 0; i < count; i++)
        {
            var room = rooms[i / 2];
            var tenant = tenants[(i * 17 + 3) % tenants.Count];
            result.Add(new DanhGia
            {
                UserId = tenant.Id,
                PhongtroId = room.Id,
                SoSao = stars[i],
                NoiDung = Review(stars[i], i + 500),
                NgayDanhGia = DateTime.Today.AddDays(-(1 + i * 11 % 240))
            });
        }

        return result;
    }

    private static (List<LichSuXem> Entities, Dictionary<string, List<Phongtro>> ByTenant) BuildViews(IReadOnlyList<Phongtro> rooms, IReadOnlyList<ApplicationUser> tenants, IReadOnlyList<Spec> specs, Random random)
    {
        var result = new Dictionary<string, List<Phongtro>>(); var entities = new List<LichSuXem>(); var used = new HashSet<(string, int)>();
        for (var i = 0; i < tenants.Count; i++)
        {
            var picked = PickWeighted(rooms, specs, 14 + i % 5, random, (room, spec) => spec.Popularity * Preference(i % 4, spec)); result[tenants[i].Id] = picked;
            for (var j = 0; j < picked.Count; j++)
            {
                var spec = specs[RoomIndex(picked[j])];
                if (used.Add((tenants[i].Id, picked[j].Id))) entities.Add(new LichSuXem { UserId = tenants[i].Id, PhongtroId = picked[j].Id, SoLanXem = Math.Clamp((int)Math.Round(spec.Popularity * 1.4 + random.NextDouble() * 5), 2, 12), LanXemCuoi = DateTime.Today.AddDays(-((i * 3 + j) % 90)) });
            }
        }
        return (entities, result);
    }

    private static List<LichSuXem> BuildExpansionViews(IReadOnlyList<Phongtro> rooms, IReadOnlyList<ApplicationUser> tenants, IReadOnlyList<Spec> specs, Random random)
    {
        var result = new List<LichSuXem>(rooms.Count * 8);
        for (var roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
        {
            var room = rooms[roomIndex];
            var tenantIndexes = Enumerable.Range(0, tenants.Count).ToList();
            Shuffle(tenantIndexes, random);
            for (var viewerIndex = 0; viewerIndex < Math.Min(8, tenantIndexes.Count); viewerIndex++)
            {
                var tenantIndex = tenantIndexes[viewerIndex];
                var spec = specs[RoomIndex(room)];
                result.Add(new LichSuXem
                {
                    UserId = tenants[tenantIndex].Id,
                    PhongtroId = room.Id,
                    SoLanXem = Math.Clamp((int)Math.Round(spec.Popularity * 1.6 + random.NextDouble() * 5), 2, 12),
                    LanXemCuoi = DateTime.Today.AddDays(-((roomIndex * 3 + viewerIndex) % 90))
                });
            }
        }

        return result;
    }

    private static List<YeuThich> BuildFavorites(IReadOnlyList<ApplicationUser> tenants, IReadOnlyDictionary<string, List<Phongtro>> views, IReadOnlyList<Spec> specs, Random random)
    {
        var result = new List<YeuThich>(); var used = new HashSet<(string, int)>();
        for (var i = 0; i < tenants.Count; i++)
        {
            var picked = PickWeighted(views[tenants[i].Id], specs, i % 11, random, (room, spec) => spec.Popularity);
            for (var j = 0; j < picked.Count; j++) if (used.Add((tenants[i].Id, picked[j].Id))) result.Add(new YeuThich { UserId = tenants[i].Id, PhongtroId = picked[j].Id, NgayThem = DateTime.Today.AddDays(-((i + j * 5) % 100)) });
        }
        return result;
    }

    private static List<YeuThich> BuildExpansionFavorites(IReadOnlyList<Phongtro> rooms, IReadOnlyList<ApplicationUser> tenants, IReadOnlyList<LichSuXem> views, IReadOnlyList<Spec> specs, Random random)
    {
        var viewersByRoom = views.GroupBy(view => view.PhongtroId)
            .ToDictionary(group => group.Key, group => group.Select(view => view.UserId).Distinct().ToList());
        var rankedRooms = rooms.OrderBy(room => specs[RoomIndex(room)].Popularity).ToList();
        var favorites = new List<YeuThich>(rooms.Count * 2);
        for (var rank = 0; rank < rankedRooms.Count; rank++)
        {
            var room = rankedRooms[rank];
            var favoriteCount = rank < rankedRooms.Count / 4 ? 1 : rank >= rankedRooms.Count * 3 / 4 ? 3 : 2;
            if (!viewersByRoom.TryGetValue(room.Id, out var viewers)) continue;
            foreach (var userId in viewers.OrderBy(_ => random.Next()).Take(favoriteCount))
            {
                favorites.Add(new YeuThich
                {
                    UserId = userId,
                    PhongtroId = room.Id,
                    NgayThem = DateTime.Today.AddDays(-((room.Id * 7 + favorites.Count) % 100))
                });
            }
        }

        return favorites;
    }

    private static List<LichXemPhong> BuildBookings(IReadOnlyList<Phongtro> rooms, IReadOnlyList<ApplicationUser> tenants, IReadOnlyList<Spec> specs, Random random)
    {
        var eligible = rooms.Where(room => room.IsVisible && room.SoLuongPhong > 0).ToList();
        var statuses = Enumerable.Repeat(TrangThaiLich.ChoXacNhan, 24).Concat(Enumerable.Repeat(TrangThaiLich.DaDongY, 54)).Concat(Enumerable.Repeat(TrangThaiLich.BaoBan, 21)).Concat(Enumerable.Repeat(TrangThaiLich.DaHuy, 21)).ToList(); Shuffle(statuses, random);
        var result = new List<LichXemPhong>(120);
        for (var i = 0; i < 120; i++)
        {
            var room = PickWeighted(eligible, specs, 1, random, (candidate, spec) => spec.Popularity)[0]; var tenant = tenants[(i * 7 + 3) % tenants.Count];
            result.Add(new LichXemPhong { PhongtroId = room.Id, NguoiThueId = tenant.Id, ChuTroId = room.OwnerId!, HoTen = tenant.HoTen!, SoDienThoai = $"09{(i * 7919 + 1_000_000) % 100_000_000:00000000}", ThoiGianXem = DateTime.Today.AddDays(1 + i % 28).AddHours(8 + i % 10), GhiChu = BookingMark + $"{i + 1:000}", TrangThai = statuses[i], LyDoHuy = statuses[i] == TrangThaiLich.DaHuy ? "Người thuê thay đổi lịch cá nhân." : null, NgayTao = DateTime.Today.AddDays(-(1 + i % 45)) });
        }
        return result;
    }

    private static List<LichXemPhong> BuildExpansionBookings(IReadOnlyList<Phongtro> rooms, IReadOnlyList<ApplicationUser> tenants, IReadOnlyList<Spec> specs, Random random)
    {
        var eligible = rooms.Where(room => room.IsVisible && room.SoLuongPhong > 0).ToList();
        if (eligible.Count == 0) return new List<LichXemPhong>();

        var count = Math.Min(AdditionalBookingCount, rooms.Count * 3 / 5);
        var statuses = Enumerable.Repeat(TrangThaiLich.ChoXacNhan, count * 20 / 100)
            .Concat(Enumerable.Repeat(TrangThaiLich.DaDongY, count * 45 / 100))
            .Concat(Enumerable.Repeat(TrangThaiLich.BaoBan, count * 18 / 100))
            .Concat(Enumerable.Repeat(TrangThaiLich.DaHuy, count - count * 20 / 100 - count * 45 / 100 - count * 18 / 100))
            .ToList();
        Shuffle(statuses, random);

        var result = new List<LichXemPhong>(count);
        for (var i = 0; i < count; i++)
        {
            var room = PickWeighted(eligible, specs, 1, random, (_, spec) => spec.Popularity)[0];
            var tenant = tenants[(i * 7 + 11) % tenants.Count];
            result.Add(new LichXemPhong
            {
                PhongtroId = room.Id,
                NguoiThueId = tenant.Id,
                ChuTroId = room.OwnerId!,
                HoTen = tenant.HoTen!,
                SoDienThoai = $"09{(i * 3571 + 2_000_000) % 100_000_000:00000000}",
                ThoiGianXem = DateTime.Today.AddDays(1 + i % 28).AddHours(8 + i % 10),
                GhiChu = BookingMark + $"{120 + i + 1:000}",
                TrangThai = statuses[i],
                LyDoHuy = statuses[i] == TrangThaiLich.DaHuy ? "Người thuê thay đổi lịch cá nhân." : null,
                NgayTao = DateTime.Today.AddDays(-(1 + i % 45))
            });
        }

        return result;
    }

    private static List<CuocTroChuyen> BuildConversations(IReadOnlyList<Phongtro> rooms, IReadOnlyList<ApplicationUser> tenants, IReadOnlyList<Spec> specs, Random random, int targetCount)
    {
        var pairs = new HashSet<(string, string)>(); var conversations = new List<CuocTroChuyen>();
        var roomsByOwner = rooms.GroupBy(x => x.OwnerId!).ToDictionary(x => x.Key, x => x.ToList());
        for (var cursor = 0; conversations.Count < targetCount; cursor++)
        {
            var tenant = tenants[cursor % tenants.Count]; var ownerId = rooms[(cursor * 13 + 5) % rooms.Count].OwnerId!;
            if (!pairs.Add((tenant.Id, ownerId))) continue;
            var room = PickWeighted(roomsByOwner[ownerId], specs, 1, random, (candidate, spec) => spec.Popularity)[0];
            var conversation = new CuocTroChuyen { NguoiThueId = tenant.Id, ChuTroId = ownerId, PhongtroId = room.Id, NgayTao = DateTime.Today.AddDays(-(3 + cursor % 100)), LanCapNhatCuoi = DateTime.Today.AddMinutes(-cursor * 17) };
            conversations.Add(conversation);
        }
        return conversations;
    }

    private static List<TinNhan> BuildMessages(IReadOnlyList<CuocTroChuyen> conversations)
    {
        var messages = new List<TinNhan>(conversations.Count * 6);
        string[] questions = ["Phòng này hiện còn không ạ?", "Phòng có chỗ để xe không ạ?", "Điện nước và wifi tính thế nào ạ?", "Cuối tuần em qua xem phòng được không?", "Phòng có nóng lạnh và giờ giấc tự do không ạ?", "Ở hai người có phù hợp không ạ?"];
        string[] answers = ["Hiện vẫn còn phòng, em hẹn thời gian đến xem nhé.", "Có chỗ để xe khu chung và có camera em nhé.", "Điện nước theo sử dụng, wifi đã có sẵn trong khu trọ.", "Cuối tuần xem được, em báo trước khoảng một giờ nhé.", "Có nóng lạnh; giờ tự do nhưng cần yên tĩnh sau 23 giờ.", "Em xem sức chứa tối đa trên tin, phòng bố trí khá thoáng nhé."];
        for (var i = 0; i < conversations.Count; i++) for (var j = 0; j < 6; j++)
        {
            var fromTenant = j % 2 == 0; var template = (i + j / 2) % questions.Length; var conversation = conversations[i];
            messages.Add(new TinNhan { CuocTroChuyenId = conversation.Id, PhongtroId = conversation.PhongtroId, NguoiGuiId = fromTenant ? conversation.NguoiThueId : conversation.ChuTroId, NoiDung = fromTenant ? questions[template] : answers[template], NgayGui = conversation.NgayTao.AddMinutes(j * 11 + i % 7), DaDoc = j < 5, LoaiTinNhan = MessageMark + $"{i + 1:000}:{j + 1}" });
        }
        return messages;
    }

    private static async Task EnsureReviews(ApplicationDbContext db, IReadOnlyCollection<DanhGia> desired)
    {
        var userIds = desired.Select(x => x.UserId).Distinct().ToList();
        var roomIds = desired.Select(x => x.PhongtroId).Distinct().ToList();
        var keys = (await db.DanhGias.AsNoTracking().Where(x => userIds.Contains(x.UserId) && roomIds.Contains(x.PhongtroId))
            .Select(x => new { x.UserId, x.PhongtroId }).ToListAsync())
            .Select(x => (x.UserId, x.PhongtroId)).ToHashSet();
        await SaveInBatchesAsync(db, desired.Where(x => keys.Add((x.UserId, x.PhongtroId))));
    }

    private static async Task EnsureViews(ApplicationDbContext db, IReadOnlyCollection<LichSuXem> desired)
    {
        var userIds = desired.Select(x => x.UserId).Distinct().ToList();
        var roomIds = desired.Select(x => x.PhongtroId).Distinct().ToList();
        var keys = (await db.LichSuXems.AsNoTracking().Where(x => userIds.Contains(x.UserId) && roomIds.Contains(x.PhongtroId))
            .Select(x => new { x.UserId, x.PhongtroId }).ToListAsync())
            .Select(x => (x.UserId, x.PhongtroId)).ToHashSet();
        await SaveInBatchesAsync(db, desired.Where(x => keys.Add((x.UserId, x.PhongtroId))));
    }

    private static async Task EnsureFavorites(ApplicationDbContext db, IReadOnlyCollection<YeuThich> desired)
    {
        var userIds = desired.Select(x => x.UserId).Distinct().ToList();
        var roomIds = desired.Select(x => x.PhongtroId).Distinct().ToList();
        var keys = (await db.YeuThichs.AsNoTracking().Where(x => userIds.Contains(x.UserId) && roomIds.Contains(x.PhongtroId))
            .Select(x => new { x.UserId, x.PhongtroId }).ToListAsync())
            .Select(x => (x.UserId, x.PhongtroId)).ToHashSet();
        await SaveInBatchesAsync(db, desired.Where(x => keys.Add((x.UserId, x.PhongtroId))));
    }

    private static async Task EnsureBookings(ApplicationDbContext db, IReadOnlyCollection<LichXemPhong> desired)
    {
        var existing = (await db.LichXemPhongs.AsNoTracking()
            .Where(x => x.GhiChu != null && x.GhiChu.StartsWith(BookingMark))
            .Select(x => x.GhiChu!).ToListAsync()).ToHashSet(StringComparer.Ordinal);
        await SaveInBatchesAsync(db, desired.Where(x => x.GhiChu is not null && existing.Add(x.GhiChu)));
    }

    private static async Task<List<CuocTroChuyen>> EnsureConversations(ApplicationDbContext db, IReadOnlyCollection<CuocTroChuyen> desired)
    {
        var tenantIds = desired.Select(x => x.NguoiThueId).Distinct().ToList();
        var ownerIds = desired.Select(x => x.ChuTroId).Distinct().ToList();
        var existing = await db.CuocTroChuyens.AsNoTracking()
            .Where(x => tenantIds.Contains(x.NguoiThueId) && ownerIds.Contains(x.ChuTroId)).ToListAsync();
        var keys = existing.Select(x => (x.NguoiThueId, x.ChuTroId)).ToHashSet();
        await SaveInBatchesAsync(db, desired.Where(x => keys.Add((x.NguoiThueId, x.ChuTroId))));
        var all = await db.CuocTroChuyens.AsNoTracking()
            .Where(x => tenantIds.Contains(x.NguoiThueId) && ownerIds.Contains(x.ChuTroId)).ToListAsync();
        var byPair = all.ToDictionary(x => (x.NguoiThueId, x.ChuTroId));
        return desired.Select(x => byPair[(x.NguoiThueId, x.ChuTroId)]).ToList();
    }

    private static async Task EnsureMessages(ApplicationDbContext db, IReadOnlyCollection<TinNhan> desired)
    {
        var existing = (await db.TinNhans.AsNoTracking().Where(x => x.LoaiTinNhan.StartsWith(MessageMark))
            .Select(x => x.LoaiTinNhan).ToListAsync()).ToHashSet(StringComparer.Ordinal);
        await SaveInBatchesAsync(db, desired.Where(x => existing.Add(x.LoaiTinNhan)));
    }

    private static async Task SaveInBatchesAsync<T>(ApplicationDbContext db, IEnumerable<T> items) where T : class
    {
        foreach (var batch in items.Chunk(BatchSize))
        {
            await db.Set<T>().AddRangeAsync(batch);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
    }

    private static void LogPhase(ILogger log, string name, Stopwatch timer) =>
        log.LogInformation("[Seed] {Phase}: {Elapsed:F1}s", name, timer.Elapsed.TotalSeconds);

    private static async Task<List<Phongtro>> LoadRooms(ApplicationDbContext db) => await db.Phongtros
        .Where(x => x.HinhAnh != null && (x.HinhAnh.Contains("#sr-demo-") || x.HinhAnh.Contains(RoomMark)))
        .ToListAsync();

    private static async Task<bool> IsCompleteFast(ApplicationDbContext db)
    {
        var state = await db.Phongtros
            .Where(x => x.HinhAnh != null && (x.HinhAnh.Contains(RoomMark) || x.HinhAnh.Contains("#sr-demo-")))
            .GroupBy(_ => 1)
            .Select(rooms => new
            {
                Rooms = rooms.Count(),
                Owners = db.Users.Count(x => x.Email != null && x.Email.StartsWith("chutro") && x.Email.EndsWith("@smartrental.local")),
                Tenants = db.Users.Count(x => x.Email != null && x.Email.StartsWith("nguoithue") && x.Email.EndsWith("@smartrental.local")),
                OwnerRoles = db.UserRoles.Count(link => db.Users.Any(user => user.Id == link.UserId && user.Email != null && user.Email.StartsWith("chutro")) && db.Roles.Any(role => role.Id == link.RoleId && role.Name == AppRoles.ChuTro)),
                TenantRoles = db.UserRoles.Count(link => db.Users.Any(user => user.Id == link.UserId && user.Email != null && user.Email.StartsWith("nguoithue")) && db.Roles.Any(role => role.Id == link.RoleId && role.Name == AppRoles.NguoiThue)),
                RoomAmenities = db.PhongTienNghis.Count(x => x.Phongtro.HinhAnh != null && (x.Phongtro.HinhAnh.Contains(RoomMark) || x.Phongtro.HinhAnh.Contains("#sr-demo-"))),
                GalleryImages = db.PhongtroHinhAnhs.Count(x => x.Phongtro.HinhAnh != null && (x.Phongtro.HinhAnh.Contains(RoomMark) || x.Phongtro.HinhAnh.Contains("#sr-demo-"))),
                Reviews = db.DanhGias.Count(x => x.User.Email != null && x.User.Email.StartsWith("nguoithue") && x.Phongtro.HinhAnh != null && (x.Phongtro.HinhAnh.Contains(RoomMark) || x.Phongtro.HinhAnh.Contains("#sr-demo-"))),
                Views = db.LichSuXems.Count(x => x.User.Email != null && x.User.Email.StartsWith("nguoithue") && x.Phongtro.HinhAnh != null && (x.Phongtro.HinhAnh.Contains(RoomMark) || x.Phongtro.HinhAnh.Contains("#sr-demo-"))),
                Favorites = db.YeuThichs.Count(x => x.User.Email != null && x.User.Email.StartsWith("nguoithue") && x.Phongtro.HinhAnh != null && (x.Phongtro.HinhAnh.Contains(RoomMark) || x.Phongtro.HinhAnh.Contains("#sr-demo-"))),
                Bookings = db.LichXemPhongs.Count(x => x.GhiChu != null && x.GhiChu.StartsWith(BookingMark)),
                Conversations = db.CuocTroChuyens.Count(x => x.NguoiThue.Email != null && x.NguoiThue.Email.StartsWith("nguoithue") && x.ChuTro.Email != null && x.ChuTro.Email.StartsWith("chutro")),
                Messages = db.TinNhans.Count(x => x.LoaiTinNhan.StartsWith(MessageMark))
            })
            .AsNoTracking()
            .SingleOrDefaultAsync();
        var allSpecs = BuildSpecs(new Random(Seed))
            .Concat(BuildExpansionSpecs(new Random(ExpansionSeed), TargetRoomCount - InitialRoomCount))
            .ToList();
        var expectedAmenities = allSpecs.Sum(x => x.Amenities.Count);
        var expectedGalleryImages = Enumerable.Range(0, allSpecs.Count).Sum(index => 1 + index % 5);
        return state is not null && state.Rooms >= TargetRoomCount && state.Owners >= 24 && state.Tenants >= 72
            && state.OwnerRoles >= 24 && state.TenantRoles >= 72 && state.RoomAmenities >= expectedAmenities && state.GalleryImages >= 750
            && state.GalleryImages >= expectedGalleryImages
            && state.Reviews >= InitialRoomCount * 2 + AdditionalReviewCount
            && state.Views >= 1149 + (TargetRoomCount - InitialRoomCount) * 8
            && state.Favorites >= 345 + (TargetRoomCount - InitialRoomCount) * 2
            && state.Bookings >= 120 + AdditionalBookingCount
            && state.Conversations >= TargetConversationCount
            && state.Messages >= TargetConversationCount * 6;
    }

    private static async Task<(List<ApplicationUser> Owners, List<ApplicationUser> Tenants)> EnsureUsers(ApplicationDbContext db, UserManager<ApplicationUser> manager)
    {
        var desired = Enumerable.Range(1, 24).Select(i => (Email: $"chutro{i}@smartrental.local", Role: AppRoles.ChuTro, Owner: true, Index: i))
            .Concat(Enumerable.Range(1, 72).Select(i => (Email: $"nguoithue{i}@smartrental.local", Role: AppRoles.NguoiThue, Owner: false, Index: i)))
            .ToList();
        var emails = desired.Select(x => x.Email).ToList();
        var byEmail = (await db.Users.Where(x => x.Email != null && emails.Contains(x.Email)).ToListAsync())
            .ToDictionary(x => x.Email!, StringComparer.OrdinalIgnoreCase);
        var roleIds = await db.Roles.AsNoTracking().Where(x => x.Name == AppRoles.ChuTro || x.Name == AppRoles.NguoiThue).ToDictionaryAsync(x => x.Name!, x => x.Id);
        if (!roleIds.ContainsKey(AppRoles.ChuTro) || !roleIds.ContainsKey(AppRoles.NguoiThue)) throw new InvalidOperationException("Demo roles are missing.");
        var existingIds = byEmail.Values.Select(x => x.Id).ToList();
        var assigned = (await db.UserRoles.AsNoTracking().Where(x => existingIds.Contains(x.UserId)).ToListAsync())
            .Select(x => (x.UserId, x.RoleId)).ToHashSet();
        var owners = new List<ApplicationUser>(24); var tenants = new List<ApplicationUser>(72);
        foreach (var item in desired)
        {
            if (!byEmail.TryGetValue(item.Email, out var user))
            {
                user = new ApplicationUser { UserName = item.Email, Email = item.Email, EmailConfirmed = true, HoTen = NaturalName(item.Index + (item.Owner ? 0 : 31)), DiaChi = "TP. Thái Nguyên" };
                Ensure(await manager.CreateAsync(user, Password), $"tạo {item.Email}");
                byEmail[item.Email] = user;
            }
            var roleId = roleIds[item.Role];
            if (!assigned.Contains((user.Id, roleId)))
            {
                Ensure(await manager.AddToRoleAsync(user, item.Role), $"gán role {item.Role}");
                assigned.Add((user.Id, roleId));
            }
            (item.Owner ? owners : tenants).Add(user);
        }
        return (owners, tenants);
    }

    private static async Task<Dictionary<string, TienNghi>> EnsureAmenities(ApplicationDbContext db)
    {
        var existing = await db.TienNghis.ToListAsync();
        var byName = existing.ToDictionary(x => x.TenTienNghi, StringComparer.OrdinalIgnoreCase); var missing = new List<TienNghi>();
        foreach (var name in AmenityNames) if (!byName.ContainsKey(name)) { var item = new TienNghi { TenTienNghi = name }; missing.Add(item); byName[name] = item; }
        if (missing.Count > 0) { db.TienNghis.AddRange(missing); await db.SaveChangesAsync(); }
        return AmenityNames.ToDictionary(name => name, name => byName[name], StringComparer.OrdinalIgnoreCase);
    }

    private static async Task Report(ApplicationDbContext db, IReadOnlyList<Phongtro> rooms, IReadOnlyList<ApplicationUser> owners, IReadOnlyList<ApplicationUser> tenants, IReadOnlySet<string> validImagePaths, ILogger log)
    {
        var ids = rooms.Select(x => x.Id).ToList(); var tenantIds = tenants.Select(x => x.Id).ToList(); var ownerIds = owners.Select(x => x.Id).ToList();
        var data = await db.Phongtros.AsNoTracking().Include(x => x.PhongTienNghis).Include(x => x.HinhAnhs).AsSplitQuery().Where(x => ids.Contains(x.Id)).ToListAsync();
        var invalidArea = data.Count(x => x.DienTich is < 10 or > 45); var invalidPrice = data.Count(x => x.Gia < 400_000); var invalidQuantity = data.Count(x => x.SoLuongPhong < 0);
        var invalidCapacity = data.Count(x => x.SoNguoiToiDa < 1 || x.DienTich <= 14 && x.SoNguoiToiDa > 1 || x.DienTich <= 19 && x.SoNguoiToiDa > 2 || x.DienTich <= 24 && x.SoNguoiToiDa > 3 || x.DienTich <= 30 && x.SoNguoiToiDa > 3 || x.SoNguoiToiDa > 4);
        var invalidLocation = data.Count(x => !ThaiNguyenAreas.All.Contains(x.KhuVuc)); var noImage = data.Count(x => x.HinhAnhs.Count == 0);
        var invalidImages = data.Count(x => x.HinhAnhs.Count > 10 || x.HinhAnhs.Count(y => y.IsAnhChinh) != 1); var broken = data.SelectMany(x => x.HinhAnhs).Count(x => !validImagePaths.Contains(x.DuongDan));
        var reviewData = await db.DanhGias.AsNoTracking().Where(x => tenantIds.Contains(x.UserId) && ids.Contains(x.PhongtroId)).Select(x => new { x.UserId, x.PhongtroId, x.SoSao }).ToListAsync();
        var viewData = await db.LichSuXems.AsNoTracking().Where(x => tenantIds.Contains(x.UserId) && ids.Contains(x.PhongtroId)).Select(x => new { x.UserId, x.PhongtroId, x.SoLanXem }).ToListAsync();
        var favoriteData = await db.YeuThichs.AsNoTracking().Where(x => tenantIds.Contains(x.UserId) && ids.Contains(x.PhongtroId)).Select(x => new { x.UserId, x.PhongtroId }).ToListAsync();
        var bookingData = await db.LichXemPhongs.AsNoTracking().Where(x => x.GhiChu != null && x.GhiChu.StartsWith(BookingMark)).Select(x => new { x.Phongtro.IsVisible, x.Phongtro.SoLuongPhong }).ToListAsync();
        var conversationData = await db.CuocTroChuyens.AsNoTracking().Where(x => tenantIds.Contains(x.NguoiThueId) && ownerIds.Contains(x.ChuTroId)).Select(x => new { x.NguoiThueId, x.ChuTroId }).ToListAsync();
        var messages = await db.TinNhans.CountAsync(x => x.LoaiTinNhan.StartsWith(MessageMark));
        var duplicateReview = reviewData.GroupBy(x => (x.UserId, x.PhongtroId)).Count(x => x.Count() > 1);
        var duplicateFavorite = favoriteData.GroupBy(x => (x.UserId, x.PhongtroId)).Count(x => x.Count() > 1);
        var duplicateConversation = conversationData.GroupBy(x => (x.NguoiThueId, x.ChuTroId)).Count(x => x.Count() > 1);
        var duplicateAmenity = data.Sum(x => x.PhongTienNghis.GroupBy(y => y.TienNghiId).Count(y => y.Count() > 1));
        var invalidRating = reviewData.Count(x => x.SoSao is < 1 or > 5);
        var invalidBooking = bookingData.Count(x => !x.IsVisible || x.SoLuongPhong == 0);
        var reviews = reviewData.Count; var viewRelations = viewData.Count;
        var totalViews = viewData.Sum(x => x.SoLanXem); var favorites = favoriteData.Count;
        var bookings = bookingData.Count; var conversations = conversationData.Count;
        var sizeReport = string.Join("; ", data.GroupBy(SizeBand).OrderBy(x => x.Min(y => y.DienTich)).Select(x => $"{x.Key}: {x.Count()} phòng/TB {x.Average(y => y.Gia):N0}đ"));
        var tierReport = string.Join("; ", Enum.GetValues<Tier>().Select(level => { var group = data.Where(x => Areas.First(y => y.Name == x.KhuVuc).Level == level).ToList(); return $"{level}: {group.Count} phòng/TB {group.Average(x => x.Gia):N0}đ"; }));
        var capacityReport = string.Join(", ", data.GroupBy(x => x.SoNguoiToiDa).OrderBy(x => x.Key).Select(x => $"{x.Key} người={x.Count()}"));
        var quantityReport = string.Join(", ", data.GroupBy(x => x.SoLuongPhong).OrderBy(x => x.Key).Select(x => $"{x.Key} phòng={x.Count()}"));
        var ratingGroups = reviewData.GroupBy(x => x.SoSao).OrderBy(x => x.Key).Select(x => new { Stars = x.Key, Count = x.Count() }).ToList();
        var ratingReport = string.Join(", ", ratingGroups.Select(x => $"{x.Stars} sao={x.Count}"));
        var areaPriceReport = string.Join("; ", data.GroupBy(x => x.KhuVuc).OrderBy(x => x.Key)
            .Select(group => $"{group.Key}: {group.Count()} phòng, TB {group.Average(x => x.Gia):N0}đ"));
        var amenityPriceReport = string.Join("; ", data.GroupBy(x => x.PhongTienNghis.Count).OrderBy(x => x.Key)
            .Select(group => $"{group.Key} tiện nghi: {group.Count()} phòng, TB {group.Average(x => x.Gia):N0}đ"));
        var coordinateReport = $"complete={data.Count(x => x.Latitude.HasValue && x.Longitude.HasValue)}, missing={data.Count(x => !x.Latitude.HasValue || !x.Longitude.HasValue)}, invalid={data.Count(x => x.Latitude is < 18 or > 23 || x.Longitude is < 104 or > 107)}";
        var duplicateRoomIds = data.GroupBy(x => x.Id).Count(group => group.Count() > 1);
        log.LogInformation("Demo report | Rooms={Rooms}, Landlords={Owners}, Tenants={Tenants}, Reviews={Reviews}, ViewRelations={ViewRelations}, TotalViews={TotalViews}, Favorites={Favorites}, Bookings={Bookings}, Conversations={Conversations}, Messages={Messages} | Area min/avg/max={MinArea}/{AvgArea:N1}/{MaxArea}; Price min/avg/max={MinPrice:N0}/{AvgPrice:N0}/{MaxPrice:N0}; CapacityAvg={Capacity:N1}; QuantityAvg={Quantity:N1}; AmenitiesAvg={Amenities:N1} | Size: {Size} | Tier: {Tier} | Capacity: {CapacityDistribution} | Quantity: {QuantityDistribution} | Rating: {RatingDistribution} | available={Available}, unavailable={Unavailable}, visible={Visible}, hidden={Hidden} | invalid area={InvalidArea}, price={InvalidPrice}, capacity={InvalidCapacity}, quantity={InvalidQuantity}, location={InvalidLocation}, no image={NoImage}, invalid images={InvalidImages}, broken={Broken}, duplicate review={DuplicateReview}, favorite={DuplicateFavorite}, conversation={DuplicateConversation}, amenity={DuplicateAmenity}, rating={InvalidRating}, booking={InvalidBooking} | Correlations: area->price bands; location->price tier factors with overlap; amenities->capped adjustments; area->capacity rules; popularity->weighted views/favorites/bookings.",
            data.Count, owners.Count, tenants.Count, reviews, viewRelations, totalViews, favorites, bookings, conversations, messages, data.Min(x => x.DienTich), data.Average(x => x.DienTich), data.Max(x => x.DienTich), data.Min(x => x.Gia), data.Average(x => x.Gia), data.Max(x => x.Gia), data.Average(x => x.SoNguoiToiDa), data.Average(x => x.SoLuongPhong), data.Average(x => x.PhongTienNghis.Count), sizeReport, tierReport, capacityReport, quantityReport, ratingReport, data.Count(x => x.SoLuongPhong > 0), data.Count(x => x.SoLuongPhong == 0), data.Count(x => x.IsVisible), data.Count(x => !x.IsVisible), invalidArea, invalidPrice, invalidCapacity, invalidQuantity, invalidLocation, noImage, invalidImages, broken, duplicateReview, duplicateFavorite, duplicateConversation, duplicateAmenity, invalidRating, invalidBooking);
        log.LogInformation("Demo distribution by KhuVuc | {Areas}", areaPriceReport);
        log.LogInformation("Demo price by amenity count | {Amenities}", amenityPriceReport);
        log.LogInformation("Demo coordinates | {Coordinates}; duplicate room IDs={DuplicateRoomIds}", coordinateReport, duplicateRoomIds);
        var invalid = invalidArea + invalidPrice + invalidCapacity + invalidQuantity + invalidLocation + noImage + invalidImages + broken + duplicateReview + duplicateFavorite + duplicateConversation + duplicateAmenity + invalidRating + invalidBooking;
        if (invalid > 0 || duplicateRoomIds > 0 || data.Any(room => room.Latitude is < 18 or > 23 || room.Longitude is < 104 or > 107))
            throw new InvalidOperationException($"Demo dataset validation failed: {invalid + duplicateRoomIds} invalid item(s). See report.");
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

    private static List<string> PickExpansionAmenities(Random random, double quality)
    {
        var profile = random.NextDouble() switch
        {
            < .25 => 0,
            < .68 => 1,
            < .93 => 2,
            _ => 3
        };
        var result = new List<string>();
        foreach (var name in AmenityNames)
        {
            var chance = name switch
            {
                "Wifi" => profile switch { 0 => .72, 1 => .91, 2 => .96, _ => .99 },
                "Chỗ để xe" => profile switch { 0 => .50, 1 => .78, 2 => .88, _ => .95 },
                "WC riêng" => profile switch { 0 => .48, 1 => .68, 2 => .86, _ => .96 },
                "Điều hòa" => profile switch { 0 => .12, 1 => .38, 2 => .68, _ => .88 },
                "Nóng lạnh" => profile switch { 0 => .18, 1 => .42, 2 => .72, _ => .90 },
                "Máy giặt" => profile switch { 0 => .04, 1 => .12, 2 => .38, _ => .68 },
                "Tủ lạnh" => profile switch { 0 => .05, 1 => .18, 2 => .48, _ => .76 },
                "Ban công" => profile switch { 0 => .08, 1 => .18, 2 => .42, _ => .68 },
                "Bếp" => profile switch { 0 => .16, 1 => .36, 2 => .66, _ => .84 },
                "Giường" => profile switch { 0 => .52, 1 => .72, 2 => .86, _ => .95 },
                _ => profile switch { 0 => .42, 1 => .62, 2 => .82, _ => .94 }
            };
            if (random.NextDouble() < Math.Min(.99, chance + quality * .06)) result.Add(name);
        }

        while (result.Count < 2)
        {
            var name = AmenityNames[random.Next(AmenityNames.Length)];
            if (!result.Contains(name)) result.Add(name);
        }

        return result;
    }

    private static decimal ExpansionPrice(double size, double areaFactor, IReadOnlyCollection<string> amenities, double quality, Random random)
    {
        var range = size switch
        {
            <= 15 => (800_000m, 1_150_000m),
            <= 20 => (800_000m, 1_500_000m),
            <= 25 => (1_200_000m, 1_950_000m),
            <= 30 => (1_450_000m, 2_550_000m),
            <= 35 => (2_100_000m, 2_850_000m),
            _ => (2_150_000m, 3_350_000m)
        };
        var basePrice = range.Item1 + (range.Item2 - range.Item1) * (decimal)(.25 + random.NextDouble() * .55);
        var amenityPremium = amenities.Sum(name => name switch
        {
            "Wifi" => 25_000m,
            "Điều hòa" => 105_000m,
            "Nóng lạnh" => 85_000m,
            "Chỗ để xe" => 25_000m,
            "Máy giặt" => 75_000m,
            "Tủ lạnh" => 85_000m,
            "Ban công" => 55_000m,
            "WC riêng" => 95_000m,
            "Bếp" => 55_000m,
            "Giường" => 25_000m,
            _ => 35_000m
        });
        var adjusted = (basePrice * (decimal)areaFactor + amenityPremium + (decimal)quality * 100_000m)
            * (decimal)(.94 + random.NextDouble() * .12);
        adjusted = Math.Clamp(adjusted, 700_000m, 3_800_000m);
        return Math.Round(adjusted / 50_000m, MidpointRounding.AwayFromZero) * 50_000m;
    }

    private static string ExpansionTitle(int index, double size, int capacity, IReadOnlyCollection<string> amenities, AreaProfile area, double quality, int addressNumber)
    {
        var feature = amenities.Contains("Điều hòa") && amenities.Contains("Nóng lạnh")
            ? "điều hòa, nóng lạnh"
            : amenities.Contains("Ban công")
                ? "ban công thoáng"
                : amenities.Contains("WC riêng")
                    ? "khép kín"
                    : amenities.Contains("Chỗ để xe")
                        ? "chỗ để xe"
                        : "đầy đủ tiện ích cơ bản";
        var opening = (index % 10) switch
        {
            0 => "Phòng trọ gọn gàng",
            1 => "Phòng giá sinh viên",
            2 => "Phòng sáng thoáng",
            3 => "Phòng khép kín",
            4 => "Phòng yên tĩnh",
            5 => "Phòng mới sửa",
            6 => "Phòng tiện nghi",
            7 => "Phòng phù hợp",
            8 => "Phòng ở linh hoạt",
            _ => "Phòng trọ sạch sẽ"
        };
        var condition = quality > .65 ? "sạch, còn mới" : "giá hợp lý";
        return $"{opening} {size:N0}m², {feature}, {condition} tại {area.Name} (ngõ {addressNumber}), phù hợp {capacity} người";
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
        string[][] text =
        [
            ["Phòng ồn và vệ sinh chưa tốt, trải nghiệm không như mong đợi.", "Đường vào bất tiện, wifi yếu và chủ trọ phản hồi chậm.", "Khu chung hơi bừa, buổi tối có tiếng xe nên khó nghỉ ngơi.", "Chỗ để xe chật và việc xử lý sự cố còn chậm.", "Phòng cần cải thiện vệ sinh, ánh sáng và cách âm."],
            ["Phòng khá bí, wifi chập chờn và chỗ để xe hơi chật.", "Giá chưa tương xứng với diện tích, khu vực tối hơi ồn.", "Nóng lạnh hoạt động chưa ổn định, chủ nhà phản hồi hơi lâu.", "Phòng ở được nhưng khu chung cần được vệ sinh thường xuyên hơn.", "Tiếng ồn buổi tối và chỗ để xe là hai điểm bất tiện."],
            ["Phòng ở mức ổn, giá hợp lý nhưng cách âm chưa tốt.", "Tiện nghi đủ dùng, đường thuận tiện nhưng khu chung đôi lúc chưa sạch.", "Wifi dùng được, diện tích vừa phải và chủ nhà khá dễ trao đổi.", "Vị trí tiện đi học, phòng ổn nhưng nhà vệ sinh cần thông thoáng hơn.", "Giá chấp nhận được, tuy nhiên chỗ để xe hơi đông vào giờ cao điểm."],
            ["Phòng sạch và wifi ổn, chỉ hơi ồn vào giờ cao điểm.", "Giá và diện tích khá hợp lý, chỗ để xe đôi lúc đông.", "Điều hòa và nóng lạnh dùng tốt, chủ nhà xử lý yêu cầu khá nhanh.", "Khu vực an ninh, phòng sáng và đi lại thuận tiện.", "Tiện nghi ổn định, chỉ cần cải thiện thêm khả năng cách âm."],
            ["Phòng sạch, chủ thân thiện, vị trí thuận tiện và giá hợp lý.", "Wifi ổn định, nóng lạnh tốt, khu trọ an ninh và hàng xóm dễ chịu.", "Phòng thoáng, điều hòa hoạt động tốt và chủ nhà hỗ trợ nhanh.", "Khu vực yên tĩnh, tiện nghi đầy đủ và chỗ để xe an toàn.", "Phòng đúng mô tả, sạch sẽ và thuận tiện cho việc đi học."]
        ];
        var choices = text[stars - 1];
        return choices[i % choices.Length];
    }
    private static List<Phongtro> PickWeighted(IReadOnlyList<Phongtro> candidates, IReadOnlyList<Spec> specs, int count, Random random, Func<Phongtro, Spec, double> weight) => candidates.Select(room => (Room: room, Key: Math.Pow(random.NextDouble(), 1 / Math.Max(.01, weight(room, specs[RoomIndex(room)]))))).OrderByDescending(x => x.Key).Take(Math.Min(count, candidates.Count)).Select(x => x.Room).ToList();
    private static int RoomIndex(Phongtro room) => TryGetRoomIndex(room, out var index)
        ? index
        : throw new InvalidOperationException($"Invalid demo room marker on room {room.Id}.");
    private static double Preference(int profile, Spec room) => profile switch { 0 => room.Price <= 1_500_000 && room.Size <= 25 ? 1.8 : .45, 1 => room.Size is >= 20 and <= 35 && room.Capacity >= 2 ? 1.7 : .55, 2 => room.Amenities.Contains("Điều hòa") && room.Amenities.Contains("Nóng lạnh") ? 1.8 : .65, _ => room.Location.Level == Tier.Near ? 1.8 : room.Location.Level == Tier.Medium ? 1 : .55 };
    private static string NaturalName(int i) => $"{Families[i % Families.Length]} {Middles[i * 5 % Middles.Length]} {Givens[i * 7 % Givens.Length]}";
    private static HashSet<string> ValidateAssets(IWebHostEnvironment env) { var valid = Images.Where(x => File.Exists(Physical(env, x))).ToHashSet(StringComparer.OrdinalIgnoreCase); var missing = Images.Where(x => !valid.Contains(x)).ToList(); if (missing.Count > 0) throw new InvalidOperationException($"Missing demo images: {string.Join(", ", missing)}"); return valid; }
    private static string Physical(IWebHostEnvironment env, string path) => Path.Combine(env.WebRootPath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
    private static string SizeBand(Phongtro room) => room.DienTich switch { <= 15 => "10-15m²", <= 20 => "16-20m²", <= 25 => "21-25m²", <= 30 => "26-30m²", <= 35 => "31-35m²", _ => "36-45m²" };
    private static double TierScore(Tier tier) => tier switch { Tier.Near => .8, Tier.Medium => .45, _ => .15 };
    private static void AddSizes(ICollection<double> target, Random random, int count, int min, int max) { for (var i = 0; i < count; i++) target.Add(random.Next(min, max + 1)); }
    private static void Shuffle<T>(IList<T> values, Random random) { for (var i = values.Count - 1; i > 0; i--) { var j = random.Next(i + 1); (values[i], values[j]) = (values[j], values[i]); } }
    private static void Ensure(IdentityResult result, string operation) { if (!result.Succeeded) throw new InvalidOperationException($"Không thể {operation}: {string.Join("; ", result.Errors.Select(x => x.Description))}"); }
    private sealed record AreaProfile(string Name, Tier Level, double Factor, string Street);
    private sealed record RoomSeedResult(List<Phongtro> All, List<Phongtro> Added);
    private sealed record Spec(
        double Size,
        int Capacity,
        decimal Price,
        int Quantity,
        bool Visible,
        AreaProfile Location,
        List<string> Amenities,
        string Title,
        string Address,
        string Description,
        double Popularity,
        double? Latitude = null,
        double? Longitude = null);
    private enum Tier { Near, Medium, Far }
}
