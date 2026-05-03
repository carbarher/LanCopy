using System.Net.Http;

namespace ScoreDown.Infrastructure;

/// <summary>
/// Centralized HTTP client provider. Avoids socket exhaustion from multiple
/// HttpClient instances. All services share a single configured instance per timeout profile.
/// </summary>
public static class HttpClientProvider
{
    private static readonly HttpClient DefaultClient = CreateClient(TimeSpan.FromSeconds(30), 24);
    private static readonly HttpClient LongTimeoutClient = CreateClient(TimeSpan.FromMinutes(5), 16);
    private static readonly HttpClient ShortTimeoutClient = CreateClient(TimeSpan.FromSeconds(10), 24);

    private static HttpClient CreateClient(TimeSpan timeout, int maxConnectionsPerServer)
    {
        var handler = new SocketsHttpHandler
        {
            MaxConnectionsPerServer = maxConnectionsPerServer,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                     System.Net.DecompressionMethods.Deflate |
                                     System.Net.DecompressionMethods.Brotli,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
        };

        var client = new HttpClient(handler)
        {
            Timeout = timeout
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");

        return client;
    }

    static HttpClientProvider()
    {
        // Clients are preconfigured with handler-level pooling and decompression.
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
