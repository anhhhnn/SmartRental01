using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartRental.Models;
using SmartRental.Security;

namespace SmartRental.Data;

public static class DemoDataSeed
{
    private const string DemoPassword = "SmartRental.Demo!2026#";

    private static readonly string[] AmenityNames =
    [
        "Điều hòa", "Nóng lạnh", "WC riêng", "Máy giặt", "Chỗ để xe",
        "Wifi", "Tủ lạnh", "Bếp", "Ban công", "Giường", "Tủ quần áo"
    ];

    private static readonly string[] ImagePaths =
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

    private static readonly RoomSeed[] Rooms =
    [
        new("Phòng sinh viên giá rẻ gần ICTU", "Số 12 đường Z115, phường Tích Lương", 400_000, 10, "Phòng gọn gàng, gần trường, phù hợp một sinh viên."),
        new("Phòng trọ gần chợ Đồng Quang", "Ngõ 18 đường Lương Ngọc Quyến, phường Đồng Quang", 600_000, 14, "Phòng sạch sẽ, gần chợ và có chỗ để xe."),
        new("Phòng giá rẻ cho sinh viên", "Số 35 đường Quang Trung, phường Tân Thịnh", 750_000, 17, "Khu vực an ninh, điện nước tính riêng, giờ giấc tự do."),
        new("Phòng trọ yên tĩnh Túc Duyên", "Ngõ 6 đường Mỏ Bạch, phường Túc Duyên", 900_000, 19, "Không gian yên tĩnh, phù hợp sinh viên và người đi làm."),
        new("Phòng trọ có chỗ để xe", "Số 81 đường Bắc Kạn, phường Hoàng Văn Thụ", 1_000_000, 20, "Có sân để xe rộng, khu dân cư an ninh."),
        new("Phòng trọ gần ICTU", "Ngõ 42 đường Z115, phường Tích Lương", 1_200_000, 23, "Gần ICTU, đi lại thuận tiện, giờ giấc tự do."),
        new("Phòng trọ có điều hòa", "Số 16 đường Việt Bắc, phường Đồng Quang", 1_500_000, 27, "Phòng thoáng, có điều hòa và khu nấu ăn riêng."),
        new("Phòng rộng cho 2 người", "Ngõ 9 đường Phùng Chí Kiên, phường Túc Duyên", 1_700_000, 29, "Diện tích rộng, phù hợp hai người ở, gần chợ."),
        new("Studio mới xây Minh Cầu", "Số 27 đường Minh Cầu, phường Phan Đình Phùng", 1_900_000, 30, "Studio mới xây, nội thất cơ bản, khu vực an ninh."),
        new("Phòng gần trường đại học", "Ngõ 15 đường 3/2, phường Tích Lương", 2_000_000, 32, "Gần trường đại học, có wifi và chỗ để xe."),
        new("Căn hộ mini khép kín", "Số 52 đường Hoàng Văn Thụ, phường Phan Đình Phùng", 2_200_000, 36, "Căn hộ khép kín, sạch sẽ, phù hợp người đi làm."),
        new("Phòng trọ có ban công", "Ngõ 4 đường Bến Oánh, phường Túc Duyên", 2_400_000, 39, "Ban công thoáng, nhiều ánh sáng, giờ giấc tự do."),
        new("Studio đầy đủ tiện nghi", "Số 68 đường Lương Ngọc Quyến, phường Đồng Quang", 2_600_000, 40, "Studio tiện nghi, gần trung tâm và khu mua sắm."),
        new("Phòng 40m² gần trung tâm", "Ngõ 22 đường Bắc Kạn, phường Hoàng Văn Thụ", 2_800_000, 43, "Phòng rộng, giao thông thuận tiện, khu vực yên tĩnh."),
        new("Căn hộ mini gần Quang Trung", "Số 19 đường Quang Trung, phường Quang Trung", 3_000_000, 47, "Căn hộ mini sáng thoáng, điện nước tính theo sử dụng."),
        new("Căn hộ mini full nội thất", "Ngõ 31 đường Minh Cầu, phường Phan Đình Phùng", 3_200_000, 49, "Có nội thất cơ bản, chỉ cần mang đồ cá nhân đến ở."),
        new("Studio có bếp riêng", "Số 44 đường Cách Mạng Tháng 8, phường Trung Thành", 3_600_000, 52, "Bếp và WC riêng, phù hợp cặp đôi hoặc người đi làm."),
        new("Phòng rộng có ban công", "Ngõ 7 đường Dương Tự Minh, phường Tân Long", 4_000_000, 55, "Phòng rộng, ban công đón sáng và chỗ để xe an toàn."),
        new("Căn hộ mini gần trung tâm", "Số 73 đường Hoàng Văn Thụ, phường Trung Thành", 4_500_000, 60, "Vị trí trung tâm, đầy đủ tiện nghi, khu vực an ninh."),
        new("Studio cao cấp Đồng Quang", "Ngõ 11 đường Lương Ngọc Quyến, phường Đồng Quang", 5_000_000, 65, "Studio rộng, nội thất đẹp, thuận tiện đi lại."),
        new("Căn hộ dịch vụ Túc Duyên", "Số 29 đường Bến Oánh, phường Túc Duyên", 5_500_000, 70, "Căn hộ dịch vụ yên tĩnh, có khu bếp và máy giặt."),
        new("Studio cao cấp gần ICTU", "Ngõ 58 đường Z115, phường Tích Lương", 6_200_000, 75, "Studio cao cấp, đầy đủ nội thất, gần trường đại học."),
        new("Căn hộ hai phòng ngủ Trung Thành", "Số 86 đường Cách Mạng Tháng 8, phường Trung Thành", 7_500_000, 80, "Hai phòng ngủ thoáng, phù hợp gia đình nhỏ."),
        new("Căn hộ rộng gần Minh Cầu", "Ngõ 26 đường Minh Cầu, phường Phan Đình Phùng", 9_000_000, 90, "Căn hộ rộng, nội thất đầy đủ và khu vực yên tĩnh.")
    ];

    private static readonly int[] RoomQuantities = [0, 1, 2, 3, 5, 8];
    private static readonly int[] PostedDayOffsets = [0, 1, 3, 7, 14];
    private static readonly int[] ReviewStars = [5, 4, 3, 4, 5, 3, 4, 5, 4, 3, 5, 4];
    private static readonly string[] ReviewContents =
    [
        "Phòng sạch, chủ trọ thân thiện.",
        "Gần trường, đi lại thuận tiện.",
        "Giá hợp lý so với khu vực.",
        "Phòng hơi nhỏ nhưng đầy đủ tiện nghi.",
        "Khu vực yên tĩnh.",
        "Chỗ để xe thuận tiện.",
        "Phòng thoáng và đúng như mô tả.",
        "An ninh tốt, giờ giấc thoải mái.",
        "Nội thất còn mới và sạch sẽ.",
        "Điện nước rõ ràng, giá phù hợp.",
        "Vị trí thuận tiện cho người đi làm.",
        "Không gian ổn, chủ trọ hỗ trợ nhanh."
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var owners = await GetOwnersAsync(userManager);
        var reviewers = await GetReviewersAsync(userManager);
        var amenities = await GetAmenitiesAsync(db);

        var existingDemoRooms = await db.Phongtros
            .Include(room => room.PhongTienNghis)
            .Include(room => room.HinhAnhs)
            .AsSplitQuery()
            .Where(room => room.HinhAnh != null && room.HinhAnh.Contains("#sr-demo-"))
            .ToListAsync();

        var seededRooms = new List<Phongtro>(Rooms.Length);
        var today = DateTime.Today;

        for (var index = 0; index < Rooms.Length; index++)
        {
            var roomNumber = index + 1;
            var marker = $"#sr-demo-{roomNumber:00}";
            var seed = Rooms[index];
            var room = existingDemoRooms.FirstOrDefault(item => item.HinhAnh?.EndsWith(marker) == true);

            if (room is null)
            {
                room = new Phongtro
                {
                    TieuDe = seed.Title,
                    DiaChi = seed.Address,
                    Gia = seed.Price,
                    DienTich = seed.Area,
                    MoTa = seed.Description,
                    HinhAnh = ImagePaths[index % ImagePaths.Length] + marker,
                    NgayDang = today.AddDays(-PostedDayOffsets[index % PostedDayOffsets.Length]),
                    TrangThai = true,
                    SoLuongPhong = RoomQuantities[index % RoomQuantities.Length],
                    IsVisible = index is not (7 or 15 or 23),
                    OwnerId = owners[index % owners.Count].Id
                };
                db.Phongtros.Add(room);
            }

            AddMissingAmenities(room, amenities, index);
            AddMissingImages(room, index);
            seededRooms.Add(room);
        }

        await db.SaveChangesAsync();
        await AddMissingReviewsAsync(db, seededRooms, reviewers, today);
    }

    private static async Task<List<ApplicationUser>> GetOwnersAsync(UserManager<ApplicationUser> userManager)
    {
        var owners = (await userManager.GetUsersInRoleAsync(AppRoles.ChuTro))
            .OrderBy(user => user.Email?.EndsWith("@smartrental.local") == true)
            .ThenBy(user => user.Email)
            .Take(3)
            .ToList();

        var demoOwners = new[]
        {
            (Email: "chutro1@smartrental.local", Name: "Nguyễn Minh Anh"),
            (Email: "chutro2@smartrental.local", Name: "Trần Hoàng Nam"),
            (Email: "chutro3@smartrental.local", Name: "Lê Thu Hà")
        };

        foreach (var demoOwner in demoOwners)
        {
            if (owners.Count >= 3) break;

            var user = await EnsureUserAsync(userManager, demoOwner.Email, demoOwner.Name, AppRoles.ChuTro);
            if (owners.All(owner => owner.Id != user.Id)) owners.Add(user);
        }

        return owners;
    }

    private static async Task<List<ApplicationUser>> GetReviewersAsync(UserManager<ApplicationUser> userManager)
    {
        var reviewers = new List<ApplicationUser>
        {
            await EnsureUserAsync(userManager, "nguoithue1@smartrental.local", "Phạm Ngọc Mai", AppRoles.NguoiThue),
            await EnsureUserAsync(userManager, "nguoithue2@smartrental.local", "Đỗ Quốc Bảo", AppRoles.NguoiThue)
        };

        return reviewers;
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                HoTen = fullName,
                DiaChi = "Thái Nguyên"
            };

            EnsureSucceeded(await userManager.CreateAsync(user, DemoPassword), $"tạo tài khoản {email}");
        }

        if (!await userManager.IsInRoleAsync(user, role))
            EnsureSucceeded(await userManager.AddToRoleAsync(user, role), $"gán role {role} cho {email}");

        return user;
    }

    private static async Task<List<TienNghi>> GetAmenitiesAsync(ApplicationDbContext db)
    {
        var existing = await db.TienNghis.ToListAsync();
        var byName = existing
            .GroupBy(item => item.TenTienNghi.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var name in AmenityNames)
        {
            if (byName.ContainsKey(name)) continue;
            var amenity = new TienNghi { TenTienNghi = name };
            db.TienNghis.Add(amenity);
            existing.Add(amenity);
            byName[name] = amenity;
        }

        await db.SaveChangesAsync();
        return AmenityNames.Select(name => byName[name]).ToList();
    }

    private static void AddMissingAmenities(Phongtro room, IReadOnlyList<TienNghi> amenities, int roomIndex)
    {
        var amenityCount = 3 + roomIndex % 5;
        for (var offset = 0; offset < amenityCount; offset++)
        {
            var amenity = amenities[(roomIndex * 2 + offset) % amenities.Count];
            if (room.PhongTienNghis.All(link => link.TienNghiId != amenity.Id))
                room.PhongTienNghis.Add(new PhongTienNghi { TienNghi = amenity });
        }
    }

    private static void AddMissingImages(Phongtro room, int roomIndex)
    {
        var imageCount = 2 + roomIndex % 4;
        for (var order = 0; order < imageCount; order++)
        {
            var path = ImagePaths[(roomIndex + order) % ImagePaths.Length];
            if (room.HinhAnhs.Any(image => image.DuongDan == path)) continue;
            room.HinhAnhs.Add(new PhongtroHinhAnh
            {
                DuongDan = path,
                ThuTu = order,
                IsAnhChinh = order == 0
            });
        }
    }

    private static async Task AddMissingReviewsAsync(
        ApplicationDbContext db,
        IReadOnlyList<Phongtro> rooms,
        IReadOnlyList<ApplicationUser> reviewers,
        DateTime today)
    {
        var reviewedRoomIds = rooms.Take(12).Select(room => room.Id).ToList();
        var reviewerIds = reviewers.Select(user => user.Id).ToList();
        var existingKeys = await db.DanhGias
            .Where(review => reviewedRoomIds.Contains(review.PhongtroId) && reviewerIds.Contains(review.UserId))
            .Select(review => new { review.UserId, review.PhongtroId })
            .ToListAsync();

        for (var index = 0; index < reviewedRoomIds.Count; index++)
        {
            var reviewer = reviewers[index % reviewers.Count];
            var roomId = reviewedRoomIds[index];
            if (existingKeys.Any(key => key.UserId == reviewer.Id && key.PhongtroId == roomId)) continue;

            db.DanhGias.Add(new DanhGia
            {
                UserId = reviewer.Id,
                PhongtroId = roomId,
                SoSao = ReviewStars[index],
                NoiDung = ReviewContents[index],
                NgayDanhGia = today.AddDays(-(index % 10))
            });
        }

        await db.SaveChangesAsync();
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded) return;
        var errors = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"Không thể {operation}: {errors}");
    }

    private sealed record RoomSeed(
        string Title,
        string Address,
        decimal Price,
        double Area,
        string Description);
}
