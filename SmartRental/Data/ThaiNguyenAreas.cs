using System.Globalization;
using System.Text;

namespace SmartRental.Data;

public static class ThaiNguyenAreas
{
    public static readonly IReadOnlyList<string> All =
    [
        "Đồng Quang", "Túc Duyên", "Phan Đình Phùng", "Trưng Vương", "Hoàng Văn Thụ",
        "Quang Trung", "Tân Thịnh", "Tích Lương", "Trung Thành", "Gia Sàng", "Phú Xá",
        "Cam Giá", "Hương Sơn", "Quan Triều", "Quang Vinh", "Đồng Bẩm", "Chùa Hang",
        "Thịnh Đán", "Quyết Thắng", "Sơn Cẩm", "Tân Long"
    ];

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var key = Fold(value);
        return All.FirstOrDefault(area => Fold(area) == key);
    }

    public static string? FromAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        var foldedAddress = Fold(address);
        return All.OrderByDescending(area => area.Length)
            .FirstOrDefault(area => foldedAddress.Contains(Fold(area), StringComparison.Ordinal));
    }

    private static string Fold(string value)
    {
        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(character));
        }

        return string.Join(' ', builder.ToString().Normalize(NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
