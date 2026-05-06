using ScoreDown.Infrastructure;
using ScoreDown.Models;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace ScoreDown.Services;

public class DownloadService
{
    private readonly HttpClient _http;
    private readonly HttpClient _httpNoRedirect;
    // Paralelismo para descargas en lote (DownloadAllAsync)
    // Parallelismo según fuente: IMSLP es restrictivo, Mutopia/CPDL más permisivos
    private static int GetParallelism(string source) => (source ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "IMSLP" => 1,
        "CPDL" => 2,
        _ => 4
    };
    // NOTA: Timeout configurado en HttpClientProvider.GetLongTimeout() (5min)
    // Esto aplica a TODAS las descargas. Para archivos muy grandes (>1GB),
    // el caller puede implementar CancellationTokenSource con timeout dinámico
    // basado en tamaño estimado (ej: timeout = tamaño_MB / 2 segundos).
    private static readonly HashSet<char> s_invalidPathChars =
        new(Path.GetInvalidPathChars().Concat(Path.GetInvalidFileNameChars()));

    public DownloadService()
    {
        _http = HttpClientProvider.GetLongTimeout();  // Descargas pueden ser lentas/largas
        _httpNoRedirect = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        })
        {
            Timeout = _http.Timeout
        };
    }

    public Func<string, string, CancellationToken, Task<(bool success, string? error)>>? ImslpWebViewDownloadAsync { get; set; }

    private const int MaxRetries = 3;
    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(12)];

    // Returns true for transient errors worth retrying (network / server-side).
    private static bool IsTransient(Exception ex) =>
        ex is HttpRequestException
        or IOException
        or TaskCanceledException { CancellationToken.IsCancellationRequested: false };

    private static bool IsRetriableStatus(System.Net.HttpStatusCode code) =>
        code is System.Net.HttpStatusCode.RequestTimeout        // 408
              or System.Net.HttpStatusCode.TooManyRequests      // 429
              or System.Net.HttpStatusCode.InternalServerError  // 500
              or System.Net.HttpStatusCode.BadGateway           // 502
              or System.Net.HttpStatusCode.ServiceUnavailable   // 503
              or System.Net.HttpStatusCode.GatewayTimeout;      // 504

    private static TimeSpan GetRetryDelay(System.Net.Http.HttpResponseMessage response, int attempt)
    {
        var fallback = RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)];

        // Retry-After suele aparecer en 429/503. Si viene, respetarlo con tope para no bloquear demasiado.
        if (response.StatusCode is not System.Net.HttpStatusCode.TooManyRequests
            and not System.Net.HttpStatusCode.ServiceUnavailable)
            return fallback;

        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null)
            return fallback;

        var candidate = retryAfter.Delta
            ?? (retryAfter.Date.HasValue ? retryAfter.Date.Value - DateTimeOffset.UtcNow : (TimeSpan?)null)
            ?? fallback;

        if (candidate <= TimeSpan.Zero)
            return fallback;

        var max = TimeSpan.FromSeconds(60);
        return candidate > max ? max : candidate;
    }

    public async Task<DownloadResult> DownloadFileAsync(
        PartituraFile file,
        string destFolder,
        IProgress<(PartituraFile file, string state, double percent, double speedKBps)>? progress = null,
        Func<PartituraFile, bool>? shouldPause = null,
        CancellationToken ct = default,
        string? cookieHeader = null,
        string? userAgent = null)
    {
        string? tempPath = null;
        long downloaded = 0;
        DownloadResult? lastFailure = null;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            // Reset per-attempt byte counter so progress is consistent on retry.
            downloaded = 0;
            tempPath = null;
            var requestUrl = file.DownloadUrl;
            var htmlRecoveryUsed = false;

            try
            {
                if (shouldPause?.Invoke(file) == true)
                {
                    progress?.Report((file, "paused", 0, 0.0));
                    return new DownloadResult { Success = false, Paused = true, Error = "Pausado", BytesDownloaded = 0 };
                }

                Directory.CreateDirectory(destFolder);
                var destPath = Path.Combine(destFolder, file.FileName);

                if (File.Exists(destPath))
                {
                    progress?.Report((file, "skipped", 100, 0.0));
                    return new DownloadResult { Success = true, FilePath = destPath, Skipped = true, BytesDownloaded = 0 };
                }

                if (attempt == 0)
                    progress?.Report((file, "starting", 0, 0.0));
                else
                    progress?.Report((file, $"retry-{attempt}", 0, 0.0));

                if (IsImslpUrl(file.DownloadUrl) && ImslpWebViewDownloadAsync is not null)
                {
                    var webViewTempPath = Path.Combine(destFolder, file.FileName) + ".wv.tmp";
                    try
                    {
                        var (webViewOk, webViewError) = await ImslpWebViewDownloadAsync(
                            file.DownloadUrl,
                            webViewTempPath,
                            ct).ConfigureAwait(false);

                        if (webViewOk && File.Exists(webViewTempPath))
                        {
                            var webViewLen = new FileInfo(webViewTempPath).Length;
                            var webViewExt = Path.GetExtension(file.FileName).ToLowerInvariant();
                            var webViewValidErr = ValidateDownloadedFile(webViewTempPath, webViewExt, webViewLen);
                            if (webViewValidErr is null)
                            {
                                File.Move(webViewTempPath, destPath, overwrite: true);
                                progress?.Report((file, "success", 100, 0.0));
                                return new DownloadResult { Success = true, FilePath = destPath, BytesDownloaded = webViewLen };
                            }

                            progress?.Report((file, "error: [diag] wv-invalid: " + webViewValidErr, 0, 0.0));
                        }
                        else if (!string.IsNullOrWhiteSpace(webViewError))
                        {
                            progress?.Report((file, "error: [diag] wv-err: " + webViewError, 0, 0.0));
                        }
                    }
                    finally
                    {
                        if (File.Exists(webViewTempPath))
                            try { File.Delete(webViewTempPath); } catch { }
                    }
                }

                // IMSLP: HttpClient es bloqueado por Cloudflare TLS fingerprint aunque las cookies sean válidas.
                // Usar curl.exe (Windows built-in, Schannel/WinSSL) que tiene fingerprint más cercano al navegador.
                if (IsImslpUrl(file.DownloadUrl) && !string.IsNullOrWhiteSpace(cookieHeader))
                {
                    var curlTempPath = Path.Combine(destFolder, file.FileName) + ".curl.tmp";
                    try
                    {
                        var (curlOk, curlError) = await TryDownloadWithCurlAsync(
                            file.DownloadUrl, curlTempPath, cookieHeader, userAgent,
                            file.SourcePageUrl, ct).ConfigureAwait(false);

                        if (curlOk && File.Exists(curlTempPath))
                        {
                            var curlLen = new FileInfo(curlTempPath).Length;
                            var ext2 = Path.GetExtension(file.FileName).ToLowerInvariant();
                            var validErr2 = ValidateDownloadedFile(curlTempPath, ext2, curlLen);
                            if (validErr2 is null)
                            {
                                File.Move(curlTempPath, destPath, overwrite: true);
                                progress?.Report((file, "success", 100, 0.0));
                                return new DownloadResult { Success = true, FilePath = destPath, BytesDownloaded = curlLen };
                            }

                            // Si curl devolvió HTML/interstitial, intentar extraer URL real y reintentar por curl.
                            var html = SafeReadTextFile(curlTempPath);
                            var recoveredUrl = TryExtractRecoveryDownloadUrl(html, file.DownloadUrl);
                            if (!string.IsNullOrWhiteSpace(recoveredUrl)
                                && !string.Equals(recoveredUrl, file.DownloadUrl, StringComparison.OrdinalIgnoreCase))
                            {
                                var recTempPath = Path.Combine(destFolder, file.FileName) + ".rec.tmp";
                                try
                                {
                                    var (recOk, recErr) = await TryDownloadWithCurlAsync(
                                        recoveredUrl, recTempPath, cookieHeader, userAgent,
                                        file.SourcePageUrl, ct).ConfigureAwait(false);

                                    if (recOk && File.Exists(recTempPath))
                                    {
                                        var recLen = new FileInfo(recTempPath).Length;
                                        var recErr2 = ValidateDownloadedFile(recTempPath, ext2, recLen);
                                        if (recErr2 is null)
                                        {
                                            File.Move(recTempPath, destPath, overwrite: true);
                                            progress?.Report((file, "success", 100, 0.0));
                                            return new DownloadResult { Success = true, FilePath = destPath, BytesDownloaded = recLen };
                                        }
                                    }
                                    else if (!string.IsNullOrWhiteSpace(recErr))
                                    {
                                        progress?.Report((file, "error: [diag] curl-recovery-err: " + recErr, 0, 0.0));
                                    }
                                }
                                finally
                                {
                                    if (File.Exists(recTempPath))
                                        try { File.Delete(recTempPath); } catch { }
                                }
                            }

                            // Hacer visible en log principal (MainWindow sólo imprime estados con prefijo "error:").
                            progress?.Report((file, "error: [diag] curl-html: " + file.DownloadUrl, 0, 0.0));
                        }
                        else if (!string.IsNullOrWhiteSpace(curlError))
                        {
                            progress?.Report((file, "error: [diag] curl-err: " + curlError, 0, 0.0));
                        }
                    }
                    finally
                    {
                        if (File.Exists(curlTempPath))
                            try { File.Delete(curlTempPath); } catch { }
                    }

                    // Intento 2: CDN alternativa imglnks.usimg.net (CDN sin Cloudflare WAF)
                    var altUrl = TryBuildImslpCdnUrl(file.DownloadUrl);
                    if (altUrl is not null && !string.Equals(altUrl, file.DownloadUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        progress?.Report((file, "error: [diag] cdn-try: " + altUrl, 0, 0.0));
                        var cdnTempPath = Path.Combine(destFolder, file.FileName) + ".cdn.tmp";
                        try
                        {
                            // Primer intento: sin cookies (CDN abierto)
                            var (cdnOk, cdnErr) = await TryDownloadWithCurlAsync(
                                altUrl, cdnTempPath, null, userAgent, file.SourcePageUrl, ct).ConfigureAwait(false);

                            // Segundo intento: con cookies (por si CDN requiere autenticación)
                            if (!cdnOk && !string.IsNullOrWhiteSpace(cdnErr))
                            {
                                if (File.Exists(cdnTempPath)) try { File.Delete(cdnTempPath); } catch { }
                                (cdnOk, cdnErr) = await TryDownloadWithCurlAsync(
                                    altUrl, cdnTempPath, cookieHeader, userAgent, file.SourcePageUrl, ct).ConfigureAwait(false);
                            }

                            if (cdnOk && File.Exists(cdnTempPath))
                            {
                                var cdnLen = new FileInfo(cdnTempPath).Length;
                                var ext3 = Path.GetExtension(file.FileName).ToLowerInvariant();
                                var validErr3 = ValidateDownloadedFile(cdnTempPath, ext3, cdnLen);
                                if (validErr3 is null)
                                {
                                    File.Move(cdnTempPath, destPath, overwrite: true);
                                    progress?.Report((file, "success", 100, 0.0));
                                    return new DownloadResult { Success = true, FilePath = destPath, BytesDownloaded = cdnLen };
                                }
                                progress?.Report((file, "error: [diag] cdn-html: " + altUrl, 0, 0.0));
                            }
                            else if (!string.IsNullOrWhiteSpace(cdnErr))
                            {
                                progress?.Report((file, "error: [diag] cdn-err: " + cdnErr, 0, 0.0));
                            }
                        }
                        finally
                        {
                            if (File.Exists(cdnTempPath))
                                try { File.Delete(cdnTempPath); } catch { }
                        }
                    }
                    // continuar con HttpClient + interstitial recovery como último recurso
                }

                HttpResponseMessage? response = null;
                for (int htmlHop = 0; htmlHop < 2; htmlHop++)
                {
                    response?.Dispose();
                    response = await SendWithOptionalHeadersAsync(
                        requestUrl, cookieHeader, userAgent,
                        referer: file.SourcePageUrl, ct).ConfigureAwait(false);

                    var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                    if (!contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                        break;

                    if (htmlRecoveryUsed)
                        break;

                    var html = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    var recoveredUrl = TryExtractRecoveryDownloadUrl(html, requestUrl);
                    if (string.IsNullOrWhiteSpace(recoveredUrl)
                        || string.Equals(recoveredUrl, requestUrl, StringComparison.OrdinalIgnoreCase))
                        break;

                    htmlRecoveryUsed = true;
                    requestUrl = recoveredUrl;
                    progress?.Report((file, "resolviendo interstitial IMSLP", 0, 0.0));
                }

                using (response)
                {
                    if (response is null)
                    {
                        lastFailure = new DownloadResult { Success = false, Error = "Respuesta nula", BytesDownloaded = 0 };
                        progress?.Report((file, $"error: {lastFailure.Error}", 0, 0.0));
                        return lastFailure;
                    }

                    // Abortar si sigue devolviendo HTML tras intento de recuperación.
                    var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                    if (contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                    {
                        lastFailure = new DownloadResult { Success = false, Error = "HTML (bot-check/sesión caducada)", BytesDownloaded = 0 };
                        progress?.Report((file, $"error: {lastFailure.Error}", 0, 0.0));
                        return lastFailure;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        var msg = $"HTTP {(int)response.StatusCode}";
                        lastFailure = new DownloadResult { Success = false, Error = msg, BytesDownloaded = 0 };
                        progress?.Report((file, $"error: {msg}", 0, 0.0));

                        if (IsRetriableStatus(response.StatusCode) && attempt < MaxRetries)
                        {
                            var delay = GetRetryDelay(response, attempt);
                            await Task.Delay(delay, ct).ConfigureAwait(false);
                            continue;
                        }

                        return lastFailure;
                    }

                    var total = response.Content.Headers.ContentLength ?? -1;
                    if (file.SizeBytes == 0 && total > 0)
                        file.SizeBytes = total;

                    tempPath = Path.Combine(destFolder, file.FileName) + ".tmp";

                    await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                    await using var fileStream = File.Create(tempPath);

                    var buffer = new byte[262144];
                    int read;
                    var sw = System.Diagnostics.Stopwatch.StartNew();

                    while ((read = await stream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                    {
                        if (shouldPause?.Invoke(file) == true)
                        {
                            progress?.Report((file, "paused", total > 0 ? downloaded * 100.0 / total : 0, 0.0));
                            return new DownloadResult { Success = false, Paused = true, Error = "Pausado", BytesDownloaded = downloaded };
                        }

                        await fileStream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                        downloaded += read;

                        if (total > 0)
                        {
                            var pct = downloaded * 100.0 / total;
                            var speedKBps = sw.Elapsed.TotalSeconds > 0.1 ? downloaded / 1024.0 / sw.Elapsed.TotalSeconds : 0;
                            progress?.Report((file, "downloading", pct, speedKBps));
                        }
                    }

                    fileStream.Close();

                    // Validar descarga incompleta por conexión cortada
                    if (total > 0 && downloaded < total)
                    {
                        var truncErr = $"Descarga incompleta: {downloaded}/{total} bytes";
                        lastFailure = new DownloadResult { Success = false, Error = truncErr, BytesDownloaded = downloaded };
                        progress?.Report((file, $"error: {truncErr}", 0, 0.0));
                        if (attempt < MaxRetries)
                        {
                            var delay = RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)];
                            await Task.Delay(delay, ct).ConfigureAwait(false);
                            continue;
                        }
                        return lastFailure;
                    }

                    // Validar que el archivo descargado sea realmente lo que espera su extensión
                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    var validationError = ValidateDownloadedFile(tempPath, ext, downloaded);
                    if (validationError != null)
                    {
                        lastFailure = new DownloadResult { Success = false, Error = validationError, BytesDownloaded = downloaded };
                        progress?.Report((file, $"error: {validationError}", 0, 0.0));
                        if (attempt < MaxRetries)
                        {
                            var delay = RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)];
                            await Task.Delay(delay, ct).ConfigureAwait(false);
                            continue;
                        }
                        return lastFailure;
                    }

                    File.Move(tempPath, destPath, overwrite: true);
                    tempPath = null;

                    progress?.Report((file, "success", 100, 0.0));
                    return new DownloadResult { Success = true, FilePath = destPath, BytesDownloaded = downloaded };
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                progress?.Report((file, "cancelled", 0, 0.0));
                return new DownloadResult { Success = false, Cancelled = true, Error = "Cancelado", BytesDownloaded = downloaded };
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < MaxRetries)
            {
                var delay = RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)];
                progress?.Report((file, $"retry-{attempt + 1}", 0, 0.0));
                lastFailure = new DownloadResult { Success = false, Error = ex.Message, BytesDownloaded = downloaded };
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                progress?.Report((file, $"error: {ex.Message}", 0, 0.0));
                return new DownloadResult { Success = false, Error = ex.Message, BytesDownloaded = downloaded };
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
                    try { File.Delete(tempPath); } catch { }
            }
        }

        // Exhausted retries
        progress?.Report((file, "error", 0, 0.0));
        return lastFailure ?? new DownloadResult { Success = false, Error = "Reintentos agotados", BytesDownloaded = downloaded };
    }

    /// <summary>Valida magic bytes del archivo descargado. Retorna null si OK, o mensaje de error.</summary>
    private static string? ValidateDownloadedFile(string path, string ext, long downloadedBytes)
    {
        if (downloadedBytes < 16) return $"Archivo demasiado pequeño ({downloadedBytes} bytes)";

        try
        {
            Span<byte> header = stackalloc byte[16];
            using var fs = File.OpenRead(path);
            var read = fs.Read(header);
            if (read < 4) return "Archivo vacío o ilegible";

            return ext switch
            {
                ".pdf" => (header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46)
                    ? null : $"No es un PDF válido (recibido: {System.Text.Encoding.ASCII.GetString(header[..Math.Min(read, 8)]).Replace("\n", "").Replace("\r", "").Trim()})",

                ".epub" => (header[0] == 0x50 && header[1] == 0x4B) // ZIP = PK
                    ? null : "No es un EPUB válido (no es ZIP)",

                ".mxl" => (header[0] == 0x50 && header[1] == 0x4B)
                    ? null : "No es un MXL válido (no es ZIP)",

                ".xml" or ".musicxml" => (header[0] == 0x3C || (header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF && header[3] == 0x3C))
                    ? null : "No es un XML válido",

                ".ly" or ".txt" => null, // texto plano — no validar bytes

                _ => null  // extensión desconocida — dejar pasar
            };
        }
        catch (IOException)
        {
            return null; // Si no podemos leer, dejar pasar (el OS puede tener el handle)
        }
    }

    public async Task<BatchResult> DownloadAllAsync(
        IEnumerable<(PartituraItem item, PartituraFile file)> jobs,
        string destFolder,
        IProgress<(string message, double percent, int done, int total)>? progress = null,
        IProgress<(PartituraFile file, string state, double filePct, double speedKBps)>? fileProgress = null,
        Func<PartituraFile, bool>? shouldPause = null,
        Func<string, (string? cookie, string? ua)>? cookieResolver = null,
        CancellationToken ct = default)
    {
        var jobList = jobs.ToList();
        int done = 0, ok = 0, failed = 0, skipped = 0, cancelled = 0;
        long bytesDownloaded = 0;
        // Parallelismo adaptado a la fuente más restrictiva del lote (IMSLP=1, CPDL=2, resto=4).
        // En lotes mixtos usar el mínimo evita rate-limit en IMSLP cuando aparece con Mutopia/CPDL.
        var parallelism = jobList.Count == 0 ? 1
            : jobList.Min(j => GetParallelism(j.item?.Source ?? "Other"));
        var sem = new SemaphoreSlim(parallelism);
        var batchLock = new object();
        var tasks = new List<Task>();

        foreach (var (item, file) in jobList)
        {
            try
            {
                await sem.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var capturedItem = item;
            var capturedFile = file;

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var subFolder = Path.Combine(destFolder,
                        SanitizeFolderName(string.IsNullOrEmpty(capturedItem.Composer) ? "Varios" : capturedItem.Composer));

                    var sourceKey = (capturedItem?.Source ?? string.Empty).Trim();
                    var (resolvedCookie, resolvedUa) = cookieResolver?.Invoke(sourceKey) ?? (null, null);
                    var result = await DownloadFileAsync(capturedFile, subFolder, fileProgress, shouldPause, ct, resolvedCookie, resolvedUa).ConfigureAwait(false);

                    lock (batchLock)
                    {
                        done++;
                        bytesDownloaded += result.BytesDownloaded;
                        if (result.Success)
                        {
                            if (result.Skipped) skipped++; else ok++;
                        }
                        else if (result.Paused)
                        {
                            cancelled++;
                        }
                        else if (result.Cancelled)
                        {
                            cancelled++;
                        }
                        else
                        {
                            failed++;
                        }
                        progress?.Report(($"📊 {done}/{jobList.Count} — ✅{ok} ⏭️{skipped} 🛑{cancelled} ❌{failed} [{capturedFile.FileName}]",
                            done * 100.0 / jobList.Count, done, jobList.Count));
                    }
                }
                finally
                {
                    sem.Release();
                }
            }, ct));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return new BatchResult { Total = jobList.Count, Ok = ok, Failed = failed, Skipped = skipped, Cancelled = cancelled, BytesDownloaded = bytesDownloaded };
    }

    private async Task<System.Net.Http.HttpResponseMessage> SendWithOptionalHeadersAsync(
        string url, string? cookieHeader, string? userAgent, string? referer, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var currentUri))
            return await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        var cookieJar = ParseCookieHeader(cookieHeader);
        var currentReferer = referer;

        for (int hop = 0; hop < 8; hop++)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
            request.Headers.TryAddWithoutValidation("Accept", "application/pdf, application/octet-stream, application/zip, */*;q=0.8");

            var safeCookie = SanitizeHeaderValue(BuildCookieHeader(cookieJar));
            if (!string.IsNullOrWhiteSpace(safeCookie))
                request.Headers.TryAddWithoutValidation("Cookie", safeCookie);

            if (!string.IsNullOrWhiteSpace(userAgent))
            {
                var safeUserAgent = SanitizeHeaderValue(userAgent);
                if (!string.IsNullOrWhiteSpace(safeUserAgent))
                    request.Headers.TryAddWithoutValidation("User-Agent", safeUserAgent);
            }

            if (!string.IsNullOrWhiteSpace(currentReferer))
            {
                if (Uri.TryCreate(currentReferer, UriKind.Absolute, out var refererUri))
                {
                    request.Headers.Referrer = refererUri;
                }
                else
                {
                    var safeReferer = SanitizeHeaderValue(currentReferer);
                    if (!string.IsNullOrWhiteSpace(safeReferer))
                        request.Headers.TryAddWithoutValidation("Referer", safeReferer);
                }
            }

            var response = await _httpNoRedirect.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            UpdateCookieJarFromSetCookie(cookieJar, response.Headers);
            if (response.Content?.Headers is not null)
                UpdateCookieJarFromSetCookie(cookieJar, response.Content.Headers);

            if (!IsRedirect(response.StatusCode) || response.Headers.Location is null)
                return response;

            var nextUri = response.Headers.Location;
            if (!nextUri.IsAbsoluteUri)
                nextUri = new Uri(currentUri, nextUri);

            response.Dispose();
            currentReferer = currentUri.ToString();
            currentUri = nextUri;
        }

        // Si supera hops máximos, devolver último intento vía cliente normal (comportamiento degradado).
        return await _http.GetAsync(currentUri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
    }

    private static bool IsRedirect(HttpStatusCode code) =>
        code == HttpStatusCode.Moved
        || code == HttpStatusCode.Redirect
        || code == HttpStatusCode.RedirectMethod
        || code == HttpStatusCode.TemporaryRedirect
        || (int)code == 308;

    private static Dictionary<string, string> ParseCookieHeader(string? cookieHeader)
    {
        var jar = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(cookieHeader))
            return jar;

        var parts = cookieHeader.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var eq = part.IndexOf('=');
            if (eq <= 0 || eq >= part.Length - 1)
                continue;
            var name = part[..eq].Trim();
            var value = part[(eq + 1)..].Trim();
            if (name.Length == 0 || value.Length == 0)
                continue;
            jar[name] = value;
        }
        return jar;
    }

    private static string BuildCookieHeader(Dictionary<string, string> jar) =>
        jar.Count == 0 ? string.Empty : string.Join("; ", jar.Select(kv => $"{kv.Key}={kv.Value}"));

    private static void UpdateCookieJarFromSetCookie(Dictionary<string, string> jar, System.Net.Http.Headers.HttpHeaders headers)
    {
        if (!headers.TryGetValues("Set-Cookie", out var setCookies))
            return;

        foreach (var raw in setCookies)
        {
            var first = raw.Split(';', 2, StringSplitOptions.TrimEntries)[0];
            var eq = first.IndexOf('=');
            if (eq <= 0 || eq >= first.Length - 1)
                continue;

            var name = first[..eq].Trim();
            var value = first[(eq + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
                continue;

            if (string.Equals(value, "deleted", StringComparison.OrdinalIgnoreCase))
                jar.Remove(name);
            else
                jar[name] = value;
        }
    }

    private static bool IsImslpUrl(string url) =>
        url.Contains("imslp.org", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("imslp.eu", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("imglnks.usimg.net", StringComparison.OrdinalIgnoreCase);

    // Capacidades curl detectadas una sola vez al arranque (thread-safe double-check).
    private static CurlCapabilities? _curlCaps;
    private static readonly object _curlCapsLock = new();

    private sealed record CurlCapabilities(string ExePath, bool SupportsHttp2, bool SupportsCompressed);

    private static CurlCapabilities GetCurlCapabilities()
    {
        if (_curlCaps is not null)
            return _curlCaps;

        lock (_curlCapsLock)
        {
            if (_curlCaps is not null)
                return _curlCaps;

            var sys32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "curl.exe");
            var exePath = File.Exists(sys32) ? sys32 : "curl.exe";

            var versionOutput = string.Empty;
            try
            {
                var vPsi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(vPsi);
                versionOutput = p?.StandardOutput.ReadToEnd() ?? string.Empty;
                p?.WaitForExit();
            }
            catch { }

            _curlCaps = new CurlCapabilities(
                ExePath: exePath,
                SupportsHttp2: versionOutput.Contains("HTTP2", StringComparison.OrdinalIgnoreCase),
                SupportsCompressed: versionOutput.Contains("zlib", StringComparison.OrdinalIgnoreCase)
                                 || versionOutput.Contains("brotli", StringComparison.OrdinalIgnoreCase)
            );
            return _curlCaps;
        }
    }

    /// <summary>
    /// Descarga un archivo usando curl.exe (Windows built-in, Schannel TLS).
    /// Detecta capacidades del curl instalado y activa --http2/--compressed solo si disponibles.
    /// </summary>
    private static async Task<(bool success, string? error)> TryDownloadWithCurlAsync(
        string url, string outputPath, string? cookieHeader, string? userAgent, string? referer,
        CancellationToken ct)
    {
        var caps = GetCurlCapabilities();

        var psi = new ProcessStartInfo
        {
            FileName = caps.ExePath,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Flags básicos: seguir redirects, silencioso, con timeout
        psi.ArgumentList.Add("-L");
        psi.ArgumentList.Add("--silent");
        psi.ArgumentList.Add("--show-error");
        if (caps.SupportsCompressed) psi.ArgumentList.Add("--compressed");
        if (caps.SupportsHttp2) psi.ArgumentList.Add("--http2");
        psi.ArgumentList.Add("--max-time"); psi.ArgumentList.Add("300");
        psi.ArgumentList.Add("--max-redirs"); psi.ArgumentList.Add("10");

        if (!string.IsNullOrWhiteSpace(cookieHeader))
        {
            psi.ArgumentList.Add("-H");
            psi.ArgumentList.Add($"Cookie: {SanitizeHeaderValue(cookieHeader)}");
        }

        var ua = string.IsNullOrWhiteSpace(userAgent)
            ? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
            : userAgent;
        psi.ArgumentList.Add("-H"); psi.ArgumentList.Add($"User-Agent: {SanitizeHeaderValue(ua)}");
        psi.ArgumentList.Add("-H"); psi.ArgumentList.Add("Accept: application/pdf,application/octet-stream,application/zip,*/*;q=0.8");
        psi.ArgumentList.Add("-H"); psi.ArgumentList.Add("Accept-Language: en-US,en;q=0.9");
        psi.ArgumentList.Add("-H"); psi.ArgumentList.Add(@"sec-ch-ua: ""Chromium"";v=""124"", ""Google Chrome"";v=""124"", ""Not-A.Brand"";v=""99""");
        psi.ArgumentList.Add("-H"); psi.ArgumentList.Add("sec-ch-ua-mobile: ?0");
        psi.ArgumentList.Add("-H"); psi.ArgumentList.Add(@"sec-ch-ua-platform: ""Windows""");
        psi.ArgumentList.Add("-H"); psi.ArgumentList.Add("sec-fetch-dest: document");
        psi.ArgumentList.Add("-H"); psi.ArgumentList.Add("sec-fetch-mode: navigate");
        psi.ArgumentList.Add("-H"); psi.ArgumentList.Add("sec-fetch-site: none");
        psi.ArgumentList.Add("-H"); psi.ArgumentList.Add("sec-fetch-user: ?1");
        psi.ArgumentList.Add("-H"); psi.ArgumentList.Add("Upgrade-Insecure-Requests: 1");

        if (!string.IsNullOrWhiteSpace(referer))
        {
            psi.ArgumentList.Add("-H");
            psi.ArgumentList.Add($"Referer: {SanitizeHeaderValue(referer)}");
        }

        psi.ArgumentList.Add("-o"); psi.ArgumentList.Add(outputPath);
        psi.ArgumentList.Add(url);

        try
        {
            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var stderr = new System.Text.StringBuilder();
            proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

            proc.Start();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);

            if (proc.ExitCode != 0)
                return (false, $"curl exit {proc.ExitCode}: {stderr}".Trim());

            return (true, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return (false, $"curl unavailable: {ex.Message}");
        }
    }

    /// <summary>
    /// Para URLs IMSLP tipo /images/imglnks/usimg/... construye la URL alternativa del CDN
    /// imglnks.usimg.net que puede no estar protegida por Cloudflare.
    /// </summary>
    private static string? TryBuildImslpCdnUrl(string url)
    {
        // Patrón 1: /images/imglnks/usimg/X/XX/file.pdf → imglnks.usimg.net/usimg/X/XX/file.pdf
        const string pat1 = "/images/imglnks/";
        var idx1 = url.IndexOf(pat1, StringComparison.OrdinalIgnoreCase);
        if (idx1 >= 0)
        {
            var rest = url[(idx1 + pat1.Length)..]; // "usimg/X/XX/file.pdf"
            return "https://imglnks.usimg.net/" + rest;
        }

        // Patrón 2: imslp.org/images/X/XX/file.pdf → imglnks.usimg.net/usimg/X/XX/file.pdf
        // (MediaWiki hash path estándar: /images/[hex]/[hex2]/filename)
        const string pat2 = "/images/";
        var idx2 = url.IndexOf(pat2, StringComparison.OrdinalIgnoreCase);
        if (idx2 >= 0 && (url.Contains("imslp.org", StringComparison.OrdinalIgnoreCase)
                       || url.Contains("imslp.eu", StringComparison.OrdinalIgnoreCase)))
        {
            var rest = url[(idx2 + pat2.Length)..]; // "X/XX/file.pdf"
            // Verificar que parece un hash path (1 char / 2 chars / filename)
            var segs = rest.Split('/');
            if (segs.Length >= 3 && segs[0].Length == 1 && segs[1].Length == 2)
                return "https://imglnks.usimg.net/usimg/" + rest;
        }

        return null;
    }

    private static string SafeReadTextFile(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? TryExtractRecoveryDownloadUrl(string html, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        // meta refresh: <meta http-equiv="refresh" content="0;url=...">
        var meta = Regex.Match(html,
            "<meta[^>]*http-equiv=[\"']?refresh[\"']?[^>]*content=[\"'][^\"']*url=([^\"'>]+)",
            RegexOptions.IgnoreCase);
        if (meta.Success)
            return MakeAbsoluteUrl(WebUtility.HtmlDecode(meta.Groups[1].Value), baseUrl);

        // JS redirects comunes
        var js = Regex.Match(html,
            "(?:window\\.location(?:\\.href)?|location\\.href)\\s*=\\s*[\"']([^\"']+)[\"']",
            RegexOptions.IgnoreCase);
        if (js.Success)
            return MakeAbsoluteUrl(WebUtility.HtmlDecode(js.Groups[1].Value), baseUrl);

        // Fallback: primer href que apunte a PDF o endpoints de archivo IMSLP.
        var hrefs = Regex.Matches(html, "href=[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase);
        foreach (Match m in hrefs)
        {
            var href = WebUtility.HtmlDecode(m.Groups[1].Value);
            if (href.Contains("/files/imglnks/", StringComparison.OrdinalIgnoreCase)
                || href.Contains("Special:ImagefromIndex", StringComparison.OrdinalIgnoreCase)
                || href.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return MakeAbsoluteUrl(href, baseUrl);
        }

        return null;
    }

    private static string? MakeAbsoluteUrl(string? candidate, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var abs))
            return abs.ToString();

        if (Uri.TryCreate(new Uri(baseUrl), candidate, out var rel))
            return rel.ToString();

        return null;
    }

    private static string SanitizeHeaderValue(string value)
    {
        // HTTP headers solo aceptan ASCII visible; filtramos control chars y no-ASCII.
        var sb = new System.Text.StringBuilder(value.Length);
        foreach (var c in value)
            if (c >= 32 && c <= 126)
                sb.Append(c);
        return sb.ToString();
    }

    private static string SanitizeFolderName(string name)
    {
        var chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (s_invalidPathChars.Contains(chars[i])) chars[i] = '_';
        return new string(chars).Trim();
    }

    public static string SanitizeFolderNameStatic(string name) => SanitizeFolderName(name);
}

public class DownloadResult
{
    public bool Success { get; set; }
    public bool Skipped { get; set; }
    public bool Cancelled { get; set; }
    public bool Paused { get; set; }
    public long BytesDownloaded { get; set; }
    public string? FilePath { get; set; }
    public string? Error { get; set; }
}

public class BatchResult
{
    public int Total { get; set; }
    public int Ok { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public int Cancelled { get; set; }
    public long BytesDownloaded { get; set; }
}
