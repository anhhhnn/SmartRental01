using Microsoft.EntityFrameworkCore;
using SmartRental.Models;

namespace SmartRental.Data;

public static class DiaDiemSeed
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        if (!environment.IsDevelopment())
        {
            return;
        }

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Campus coordinates verified against public map listings on 2026-10-23.
        // OSM ways: TNUS 968202795, TUAF 978670578. ICTU/TNUE coordinates are
        // campus entrance/listing points. Room coordinates are deliberately not seeded
        // because the existing demo addresses are synthetic and cannot be geocoded reliably.
        var landmarks = new[]
        {
            new DiaDiem
            {
                Ten = "Đại học Công nghệ Thông tin và Truyền thông",
                Loai = "Đại học",
                DiaChi = "Đường Z115, Quyết Thắng, Thái Nguyên",
                Latitude = 21.58504,
                Longitude = 105.806381,
                Alias = "ictu; cntt thái nguyên; đại học công nghệ thông tin; đại học cntt; trường công nghệ thông tin"
            },
            new DiaDiem
            {
                Ten = "Đại học Nông Lâm",
                Loai = "Đại học",
                DiaChi = "Quyết Thắng, Thái Nguyên",
                Latitude = 21.59471,
                Longitude = 105.80448,
                Alias = "nông lâm; nong lam; tuaf; đại học nông lâm; trường nông lâm"
            },
            new DiaDiem
            {
                Ten = "Đại học Khoa học",
                Loai = "Đại học",
                DiaChi = "Tân Thịnh, Thái Nguyên",
                Latitude = 21.58872,
                Longitude = 105.81008,
                Alias = "khoa học; khoa hoc; dh khoa hoc; đại học khoa học; trường khoa học; tnus"
            },
            new DiaDiem
            {
                Ten = "Đại học Sư phạm",
                Loai = "Đại học",
                DiaChi = "20 Lương Ngọc Quyến, Quang Trung, Thái Nguyên",
                Latitude = 21.5989047,
                Longitude = 105.8229194,
                Alias = "sư phạm; su pham; dh su pham; đại học sư phạm; trường sư phạm; tnue"
            }
        };

        var existingNames = await db.DiaDiems
            .AsNoTracking()
            .Select(d => d.Ten)
            .ToListAsync();
        var existing = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = landmarks.Where(d => !existing.Contains(d.Ten)).ToList();

        if (missing.Count == 0)
        {
            return;
        }

        db.DiaDiems.AddRange(missing);
        await db.SaveChangesAsync();
    }
}
