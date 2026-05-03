using System.Net.Http;

namespace ScoreDown.Infrastructure;

/// <summary>
/// Centralized HTTP client provider. Avoids socket exhaustion from multiple
/// HttpClient instances. All services share a single configured instance per timeout profile.
/// </summary>
public static class HttpClientProvider
{
    private static readonly HttpClient DefaultClient = new();
    private static readonly HttpClient LongTimeoutClient = new();
    private static readonly HttpClient ShortTimeoutClient = new();

    static HttpClientProvider()
    {
        // Standard: 30s (metadata, search)
        DefaultClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");
        DefaultClient.Timeout = TimeSpan.FromSeconds(30);

        // Long: 5min (large file downloads)
        LongTimeoutClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");
        LongTimeoutClient.Timeout = TimeSpan.FromMinutes(5);

        // Short: 10s (quick metadata checks)
        ShortTimeoutClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");
        ShortTimeoutClient.Timeout = TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// Gets the default HTTP client (30s timeout) for metadata/search operations.
    /// </summary>
    public static HttpClient GetDefault() => DefaultClient;

    /// <summary>
    /// Gets the long-timeout HTTP client (5min) for large downloads.
    /// </summary>
    public static HttpClient GetLongTimeout() => LongTimeoutClient;

    /// <summary>
    /// Gets the short-timeout HTTP client (10s) for quick checks.
    /// </summary>
    public static HttpClient GetShortTimeout() => ShortTimeoutClient;

    /// <summary>
    /// Gets dynamic timeout based on estimated size. Use for files where size is known.
    /// </summary>
    public static HttpClient GetForFileSize(long? sizeBytes = null)
    {
        // Heuristic: 1 Mbps = minimum expected speed
        // File = 500 MB → 500s = 8min timeout
        if (sizeBytes.HasValue && sizeBytes > 100 * 1024 * 1024)  // >100MB
            return GetLongTimeout();
        return GetDefault();
    }
}
