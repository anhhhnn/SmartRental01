using System.Text.Json.Serialization;
using SmartRental.Models;

namespace SmartRental.ViewModels;

public sealed class RecommendationResponse
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("userKnown")]
    public bool UserKnown { get; set; }

    [JsonPropertyName("coldStart")]
    public bool ColdStart { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("recommendations")]
    public List<RecommendationItem> Recommendations { get; set; } = new();
}

public sealed class RecommendationItem
{
    [JsonPropertyName("phongtroId")]
    public int PhongtroId { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("reasons")]
    public List<string> Reasons { get; set; } = new();
}

public sealed class PhongtroIndexViewModel
{
    public PagedResult<PhongtroSearchItemViewModel> SearchResults { get; init; } = new();

    public IReadOnlyList<Phongtro> RecommendedRooms { get; init; } = Array.Empty<Phongtro>();

    public IReadOnlyDictionary<int, RecommendationItem> RecommendationByRoomId { get; init; }
        = new Dictionary<int, RecommendationItem>();

    public bool ShowRecommendations { get; init; }

    public bool RecommendationColdStart { get; init; }

    public string? RecommendationModel { get; init; }
}
