using ScoreDown.Infrastructure;
using ScoreDown.Models;
using System.IO;
using System.Net.Http;

namespace ScoreDown.Services;

public class DownloadService
{
    private readonly HttpClient _http;
    // Paralelismo para descargas en lote (DownloadAllAsync)
    private const int BatchParallelism = 3;
    // NOTA: Timeout configurado en HttpClientProvider.GetLongTimeout() (5min)
    // Esto aplica a TODAS las descargas. Para archivos muy grandes (>1GB),
    // el caller puede implementar CancellationTokenSource con timeout dinámico
    // basado en tamaño estimado (ej: timeout = tamaño_MB / 2 segundos).
    private static readonly HashSet<char> s_invalidPathChars =
        new(Path.GetInvalidPathChars().Concat(Path.GetInvalidFileNameChars()));

    public DownloadService()
    {
        _http = HttpClientProvider.GetLongTimeout();  // Descargas pueden ser lentas/largas
    }

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

                using var response = await SendWithOptionalHeadersAsync(file.DownloadUrl, cookieHeader, userAgent, ct)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var msg = $"HTTP {(int)response.StatusCode} — {file.FileName}";
                    progress?.Report((file, "error", 0, 0.0));
                    lastFailure = new DownloadResult { Success = false, Error = msg, BytesDownloaded = 0 };

                    if (IsRetriableStatus(response.StatusCode) && attempt < MaxRetries)
                    {
                        var delay = RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)];
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

                var buffer = new byte[81920];
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
                File.Move(tempPath, destPath, overwrite: true);
                tempPath = null;

                progress?.Report((file, "success", 100, 0.0));
                return new DownloadResult { Success = true, FilePath = destPath, BytesDownloaded = downloaded };
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
                progress?.Report((file, "error", 0, 0.0));
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

    public async Task<BatchResult> DownloadAllAsync(
        IEnumerable<(PartituraItem item, PartituraFile file)> jobs,
        string destFolder,
        IProgress<(string message, double percent, int done, int total)>? progress = null,
        IProgress<(PartituraFile file, string state, double filePct, double speedKBps)>? fileProgress = null,
        Func<PartituraFile, bool>? shouldPause = null,
        CancellationToken ct = default)
    {
        var jobList = jobs.ToList();
        int done = 0, ok = 0, failed = 0, skipped = 0, cancelled = 0;
        long bytesDownloaded = 0;
        var sem = new SemaphoreSlim(BatchParallelism);
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

                    var result = await DownloadFileAsync(capturedFile, subFolder, fileProgress, shouldPause, ct).ConfigureAwait(false);

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
        string url, string? cookieHeader, string? userAgent, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cookieHeader) && string.IsNullOrWhiteSpace(userAgent))
            return await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrWhiteSpace(cookieHeader))
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        if (!string.IsNullOrWhiteSpace(userAgent))
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
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
