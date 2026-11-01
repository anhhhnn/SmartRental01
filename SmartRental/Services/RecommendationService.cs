using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SmartRental.ViewModels;

namespace SmartRental.Services;

public sealed class RecommendationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(HttpClient httpClient, ILogger<RecommendationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<RecommendationResponse?> GetRecommendationsAsync(
        string userId,
        int topN = 8,
        bool excludeSeen = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var encodedUserId = Uri.EscapeDataString(userId);
        var requestUri = $"recommend/{encodedUserId}?top_n={topN}&exclude_seen={excludeSeen.ToString().ToLowerInvariant()}";

        try
        {
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var logLevel = response.StatusCode == HttpStatusCode.ServiceUnavailable
                    ? LogLevel.Warning
                    : LogLevel.Error;
                _logger.Log(
                    logLevel,
                    "Recommendation service returned HTTP {StatusCode} for user {UserId}.",
                    (int)response.StatusCode,
                    userId);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<RecommendationResponse>(
                cancellationToken: cancellationToken);
            if (result is null)
            {
                _logger.LogWarning(
                    "Recommendation service returned an empty payload for user {UserId}.",
                    userId);
                return null;
            }

            result.Recommendations ??= new List<RecommendationItem>();
            foreach (var item in result.Recommendations)
            {
                item.Reasons ??= new List<string>();
            }
            return result;
        }
        catch (TaskCanceledException exception)
        {
            _logger.LogWarning(
                exception,
                "Recommendation service timed out or the request was cancelled for user {UserId}.",
                userId);
            return null;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "Recommendation service is unavailable for user {UserId}.",
                userId);
            return null;
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(
                exception,
                "Recommendation service returned invalid JSON for user {UserId}.",
                userId);
            return null;
        }
        catch (NotSupportedException exception)
        {
            _logger.LogWarning(
                exception,
                "Recommendation service returned an unsupported payload for user {UserId}.",
                userId);
            return null;
        }
    }
}
