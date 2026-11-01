using System.Text.Json.Serialization;

namespace SmartRental.ViewModels;

public class AiSearchRequest
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class AiSearchResult
{
    [JsonPropertyName("minPrice")]
    public decimal? MinPrice { get; set; }

    [JsonPropertyName("maxPrice")]
    public decimal? MaxPrice { get; set; }

    [JsonPropertyName("soNguoi")]
    public int? SoNguoi { get; set; }

    [JsonPropertyName("khuVuc")]
    public string? KhuVuc { get; set; }

    [JsonPropertyName("tienNghi")]
    public List<string> TienNghi { get; set; } = new();

    [JsonPropertyName("nearPlace")]
    public string? NearPlace { get; set; }

    [JsonPropertyName("maxDistanceKm")]
    public double? MaxDistanceKm { get; set; }
}

public class PhongtroSearchItemViewModel
{
    public Models.Phongtro Phongtro { get; set; } = null!;

    public double? DistanceKm { get; set; }

    public string? NearPlaceName { get; set; }
}
