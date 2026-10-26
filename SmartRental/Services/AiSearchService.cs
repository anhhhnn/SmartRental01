using System.Net.Http.Json;
using System.Text.Json;
using SmartRental.ViewModels;

namespace SmartRental.Services;

public class AiSearchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiSearchService> _logger;

    public AiSearchService(HttpClient httpClient, ILogger<AiSearchService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<AiSearchResult?> ParseAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "parse-search",
                new AiSearchRequest { Text = text.Trim() },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AI search service returned HTTP {StatusCode}.",
                    (int)response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<AiSearchResult>(
                cancellationToken: cancellationToken);
            if (result is not null)
            {
                result.TienNghi ??= new List<string>();
            }

            return result;
        }
        catch (TaskCanceledException exception)
        {
            _logger.LogWarning(exception, "AI search service timed out or the request was cancelled.");
            return null;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "AI search service is unavailable.");
            return null;
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "AI search service returned invalid JSON.");
            return null;
        }
        catch (NotSupportedException exception)
        {
            _logger.LogWarning(exception, "AI search service returned an unsupported payload.");
            return null;
        }
    }
}
