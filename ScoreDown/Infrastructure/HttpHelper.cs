using System.Net.Http;

namespace ScoreDown.Infrastructure;

/// <summary>
/// Shared HTTP helper for all services. Centralizes common patterns:
/// - Retry logic for transient failures (429, 502-504)
/// - HTML fetch with automatic charset detection
/// - Consistent error handling
/// </summary>
public static class HttpHelper
{
    private const int DefaultMaxRetries = 3;
    private static readonly TimeSpan[] DefaultRetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4)
    ];

    /// <summary>
    /// Fetches HTML from URL with automatic retry on transient failures.
    /// </summary>
    public static async Task<string?> FetchHtmlAsync(
        HttpClient http,
        string url,
        int maxRetries = DefaultMaxRetries,
        CancellationToken ct = default)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                using var response = await http.GetAsync(url, ct).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                var code = (int)response.StatusCode;
                var isTransient = code is 429 or 502 or 503 or 504;

                // Don't retry on last attempt or non-transient errors
                if (!isTransient || attempt == maxRetries - 1)
                    return null;

                // Backoff before retry
                if (attempt < DefaultRetryDelays.Length)
                    await Task.Delay(DefaultRetryDelays[attempt], ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Network/parsing errors — retry if not exhausted
                if (attempt == maxRetries - 1)
                    return null;

                if (attempt < DefaultRetryDelays.Length)
                    await Task.Delay(DefaultRetryDelays[attempt], ct).ConfigureAwait(false);
            }
        }

        return null;
    }
}
