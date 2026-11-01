using Microsoft.EntityFrameworkCore;
using SmartRental.Services;

namespace SmartRental.Data;

public static class RoomCoordinateSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var environment = serviceProvider.GetRequiredService<IWebHostEnvironment>();
        if (!environment.IsDevelopment())
        {
            return;
        }

        var db = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(RoomCoordinateSeeder));
        var distanceService = serviceProvider.GetRequiredService<GeoDistanceService>();
        var roomsMissingCoordinates = await db.Phongtros
            .Where(room => room.Latitude == null || room.Longitude == null)
            .OrderBy(room => room.Id)
            .ToListAsync(cancellationToken);

        var updated = 0;
        var missingAreaCenter = 0;
        var partialCoordinates = 0;
        foreach (var room in roomsMissingCoordinates)
        {
            if (room.Latitude.HasValue || room.Longitude.HasValue)
            {
                partialCoordinates++;
                continue;
            }

            var point = GeoCoordinateGenerator.GenerateForArea(room.KhuVuc, room.Id);
            if (point is null)
            {
                missingAreaCenter++;
                continue;
            }

            room.Latitude = point.Latitude;
            room.Longitude = point.Longitude;
            updated++;
        }

        if (updated > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Room coordinates: updated={Updated}, no verified area point={MissingAreaCenter}, partial coordinate pairs preserved={PartialCoordinates}",
            updated,
            missingAreaCenter,
            partialCoordinates);

        await ReportAsync(db, distanceService, logger, cancellationToken);
    }

    private static async Task ReportAsync(
        ApplicationDbContext db,
        GeoDistanceService distanceService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var rooms = await db.Phongtros
            .AsNoTracking()
            .Select(room => new
            {
                room.KhuVuc,
                room.Latitude,
                room.Longitude,
                room.IsVisible,
                room.Gia,
                room.SoNguoiToiDa,
                Amenities = room.PhongTienNghis.Select(item => item.TienNghi.TenTienNghi).ToList()
            })
            .ToListAsync(cancellationToken);
        var completeCoordinates = rooms
            .Where(room => room.Latitude.HasValue && room.Longitude.HasValue)
            .ToList();
        var searchableRooms = completeCoordinates.Where(room => room.IsVisible).ToList();
        foreach (var group in rooms
            .Where(room => !room.Latitude.HasValue || !room.Longitude.HasValue)
            .GroupBy(room => ThaiNguyenAreas.Normalize(room.KhuVuc) ?? room.KhuVuc)
            .OrderBy(group => group.Key))
        {
            logger.LogWarning("Rooms still missing coordinates: area={Area}, count={Count}", group.Key, group.Count());
        }

        logger.LogInformation(
            "Room coordinate totals: total={Total}, complete={Complete}, missing={Missing}, searchable={Searchable}",
            rooms.Count,
            completeCoordinates.Count,
            rooms.Count - completeCoordinates.Count,
            searchableRooms.Count);

        foreach (var area in ThaiNguyenAreas.All)
        {
            var areaRooms = completeCoordinates
                .Where(room => ThaiNguyenAreas.Normalize(room.KhuVuc) == area)
                .ToList();
            if (areaRooms.Count == 0)
            {
                logger.LogInformation("Area {Area}: rooms=0", area);
                continue;
            }

            logger.LogInformation(
                "Area {Area}: rooms={Count}, latitude={MinLatitude:F6}..{MaxLatitude:F6}, longitude={MinLongitude:F6}..{MaxLongitude:F6}",
                area,
                areaRooms.Count,
                areaRooms.Min(room => room.Latitude!.Value),
                areaRooms.Max(room => room.Latitude!.Value),
                areaRooms.Min(room => room.Longitude!.Value),
                areaRooms.Max(room => room.Longitude!.Value));
        }

        var landmarks = await db.DiaDiems
            .AsNoTracking()
            .Where(place => place.IsActive)
            .Select(place => new { place.Ten, place.Latitude, place.Longitude })
            .ToListAsync(cancellationToken);
        foreach (var landmarkName in new[]
        {
            "Đại học Công nghệ Thông tin và Truyền thông",
            "Đại học Khoa học",
            "Đại học Nông Lâm",
            "Đại học Sư phạm"
        })
        {
            var landmark = landmarks.FirstOrDefault(place => place.Ten == landmarkName);
            if (landmark is null)
            {
                logger.LogWarning("Landmark not found or inactive: {Landmark}", landmarkName);
                continue;
            }

            var distances = searchableRooms.Select(room => distanceService.CalculateDistanceKm(
                room.Latitude!.Value,
                room.Longitude!.Value,
                landmark.Latitude,
                landmark.Longitude)).ToList();
            logger.LogInformation(
                "Landmark {Landmark} ({Latitude:F6},{Longitude:F6}): <=1km={Within1Km}, <=2km={Within2Km}, <=3km={Within3Km}",
                landmark.Ten,
                landmark.Latitude,
                landmark.Longitude,
                distances.Count(distance => distance <= 1),
                distances.Count(distance => distance <= 2),
                distances.Count(distance => distance <= 3));

            if (landmarkName == "Đại học Nông Lâm")
            {
                var matchingCombinedSearch = searchableRooms.Count(room =>
                    room.Gia < 2_000_000m
                    && room.SoNguoiToiDa >= 2
                    && room.Amenities.Contains("Wifi", StringComparer.OrdinalIgnoreCase)
                    && distanceService.CalculateDistanceKm(
                        room.Latitude!.Value,
                        room.Longitude!.Value,
                        landmark.Latitude,
                        landmark.Longitude) <= 3);
                logger.LogInformation(
                    "Combined AI search (price<2,000,000; capacity>=2; Wifi; <=3km from {Landmark}): matches={Matches}",
                    landmark.Ten,
                    matchingCombinedSearch);
            }
        }

        logger.LogInformation(
            "AI filter (Tân Thịnh, price<1,500,000): matches={Matches}",
            searchableRooms.Count(room => ThaiNguyenAreas.Normalize(room.KhuVuc) == "Tân Thịnh" && room.Gia < 1_500_000m));
        logger.LogInformation(
            "AI filter (capacity>=3, Điều hòa, Nóng lạnh): matches={Matches}",
            searchableRooms.Count(room => room.SoNguoiToiDa >= 3
                && room.Amenities.Contains("Điều hòa", StringComparer.OrdinalIgnoreCase)
                && room.Amenities.Contains("Nóng lạnh", StringComparer.OrdinalIgnoreCase)));
    }

}