using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Cliente HTTP/3 optimizado con QUIC para mejor rendimiento de red
    /// Menor latencia y mejor rendimiento en redes inestables
    /// </summary>
    public class Http3ClientService : IDisposable
    {
        private readonly HttpClient _client;
        private readonly SocketsHttpHandler _handler;

        public Http3ClientService(TimeSpan? timeout = null)
        {
            _handler = new SocketsHttpHandler
            {
                // Habilitar HTTP/3 (QUIC)
                EnableMultipleHttp2Connections = true,
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                MaxConnectionsPerServer = 10,
                
                // Configuración SSL para HTTP/3
                SslOptions = new SslClientAuthenticationOptions
                {
                    // Priorizar HTTP/3, fallback a HTTP/2 y HTTP/1.1
                    ApplicationProtocols = new System.Collections.Generic.List<SslApplicationProtocol>
                    {
                        SslApplicationProtocol.Http3,
                        SslApplicationProtocol.Http2,
                        SslApplicationProtocol.Http11
                    },
                    EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12
                },

                // Configuración de conexión
                ConnectTimeout = TimeSpan.FromSeconds(10),
                ResponseDrainTimeout = TimeSpan.FromSeconds(5),
                
                // Compresión automática
                AutomaticDecompression = DecompressionMethods.All
            };

            _client = new HttpClient(_handler)
            {
                Timeout = timeout ?? TimeSpan.FromSeconds(30),
                DefaultRequestVersion = HttpVersion.Version30, // HTTP/3
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
            };

            // Headers por defecto
            _client.DefaultRequestHeaders.Add("User-Agent", "SlskDown/4.0 (HTTP/3)");
            _client.DefaultRequestHeaders.Add("Accept-Encoding", "br, gzip, deflate");
        }

        /// <summary>
        /// GET request con HTTP/3
        /// </summary>
        public async Task<string> GetStringAsync(string url, CancellationToken ct = default)
        {
            try
            {
                var response = await _client.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();
                
                LogProtocol(response);
                
                return await response.Content.ReadAsStringAsync(ct);
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"HTTP/3 request failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// GET request con retry automático
        /// </summary>
        public async Task<string> GetStringWithRetryAsync(
            string url, 
            int maxRetries = 3,
            CancellationToken ct = default)
        {
            Exception? lastException = null;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    return await GetStringAsync(url, ct);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    if (i < maxRetries - 1)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, i));
                        System.Diagnostics.Debug.WriteLine($"Retry {i + 1}/{maxRetries} after {delay.TotalSeconds}s");
                        await Task.Delay(delay, ct);
                    }
                }
            }

            throw lastException ?? new Exception("Request failed");
        }

        /// <summary>
        /// POST request con HTTP/3
        /// </summary>
        public async Task<string> PostAsync(
            string url, 
            HttpContent content,
            CancellationToken ct = default)
        {
            var response = await _client.PostAsync(url, content, ct);
            response.EnsureSuccessStatusCode();
            
            LogProtocol(response);
            
            return await response.Content.ReadAsStringAsync(ct);
        }

        /// <summary>
        /// Download de archivo con progreso
        /// </summary>
        public async Task DownloadFileAsync(
            string url,
            string destinationPath,
            IProgress<double>? progress = null,
            CancellationToken ct = default)
        {
            using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            LogProtocol(response);

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var downloadedBytes = 0L;

            using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            using var fileStream = new System.IO.FileStream(
                destinationPath, 
                System.IO.FileMode.Create, 
                System.IO.FileAccess.Write, 
                System.IO.FileShare.None,
                bufferSize: 81920, // 80KB buffer
                useAsync: true);

            var buffer = new byte[81920];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                downloadedBytes += bytesRead;

                if (progress != null && totalBytes > 0)
                {
                    var percentage = (double)downloadedBytes / totalBytes * 100;
                    progress.Report(percentage);
                }
            }
        }

        /// <summary>
        /// Verifica si HTTP/3 está disponible
        /// </summary>
        public async Task<bool> IsHttp3AvailableAsync(string testUrl = "https://www.google.com")
        {
            try
            {
                var response = await _client.GetAsync(testUrl, HttpCompletionOption.ResponseHeadersRead);
                var version = response.Version;
                
                System.Diagnostics.Debug.WriteLine($"Protocol version: {version}");
                
                return version == HttpVersion.Version30;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Obtiene estadísticas de conexión
        /// </summary>
        public ConnectionStats GetStats()
        {
            return new ConnectionStats
            {
                // ERROR: PooledConnectionLifetime = _handler.PooledConnectionLifetime,
                // ERROR: MaxConnectionsPerServer = _handler.MaxConnectionsPerServer,
                // ERROR: ConnectTimeout = _handler.ConnectTimeout
            };
        }

        private void LogProtocol(HttpResponseMessage response)
        {
            var version = response.Version;
            var protocol = version.Major switch
            {
                3 => "HTTP/3 (QUIC)",
                2 => "HTTP/2",
                1 => "HTTP/1.1",
                _ => $"HTTP/{version}"
            };

            System.Diagnostics.Debug.WriteLine($"🌐 {protocol} - {response.StatusCode}");
        }

        public void Dispose()
        {
            _client?.Dispose();
            _handler?.Dispose();
        }
    }

    /// <summary>
    /// Cliente HTTP/3 con caché integrado
    /// </summary>
    public class CachedHttp3Client : IDisposable
    {
        private readonly Http3ClientService _http3Client;
        private readonly ModernCacheService _cache;

        public CachedHttp3Client(ModernCacheService cache)
        {
            _http3Client = new Http3ClientService();
            _cache = cache;
        }

        /// <summary>
        /// GET con caché automático
        /// </summary>
        public async Task<string> GetStringCachedAsync(
            string url,
            TimeSpan? cacheDuration = null,
            CancellationToken ct = default)
        {
            var cacheKey = $"http3:{url}";
            
            // Verificar caché
            var cached = _cache.Get<string>(cacheKey);
            if (cached != null)
            {
                System.Diagnostics.Debug.WriteLine($"📦 Cache hit: {url}");
                return cached;
            }

            // Fetch desde red
            var content = await _http3Client.GetStringAsync(url, ct);
            
            // Guardar en caché
            _cache.Set(cacheKey, content, cacheDuration ?? TimeSpan.FromMinutes(10), sizeInKB: content.Length / 1024);
            
            return content;
        }

        public void Dispose()
        {
            _http3Client?.Dispose();
        }
    }

    public class Http3ConnectionStats
    {
        public TimeSpan PooledConnectionLifetime { get; set; }
        public int MaxConnectionsPerServer { get; set; }
        public TimeSpan ConnectTimeout { get; set; }
    }
}
