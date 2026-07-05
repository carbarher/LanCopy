using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Diagnostics;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Collections.Concurrent;
using LanCopy.Models;

namespace LanCopy.Services;

public sealed partial class LanClient : IDisposable, IAsyncDisposable
{
    public sealed record RemoteStat(bool Exists, bool IsDirectory, long Size, long LastWriteUtcTicks);
    public sealed record RemoteHealth(int ConnCurrent, int ConnLimit, int PerIpLimit, int ActiveIps, int PinFailsTracked, int HashCacheEntries, int CommandRateTracked, int CommandRateLimit, int CommandRateWindowSeconds);

    private readonly string _host;
    private readonly int _port;
    /// <summary>Certificado TLS del peer remoto (disponible tras conexión TLS).</summary>
    public X509Certificate2? RemoteCertificate { get; private set; }
    private const long MaxCompressInMemory = 200L * 1024 * 1024;
    private const long MaxLocalHashBeforeUploadBytes = 512L * 1024 * 1024; // 512 MB
    private const long ResumeMapBlockSize = 4L * 1024 * 1024;
    // Las constantes de timeout, keep-alive y umbral de tamaño se centralizan en TransferOptions.
    // Usar alias locales para legibilidad interna (evita duplicar los valores aquí).
    private static readonly TimeSpan TransferIdleTimeoutSmall = TransferOptions.TransferIdleTimeoutSmall;
    private static readonly TimeSpan TransferIdleTimeoutLarge = TransferOptions.TransferIdleTimeoutLarge;
    private const long LargeTransferThresholdBytes = TransferOptions.LargeTransferThresholdBytes;
    private const int KeepAliveIdleSeconds = TransferOptions.KeepAliveIdleSeconds;
    private const int KeepAliveIntervalSeconds = TransferOptions.KeepAliveIntervalSeconds;
    private const int KeepAliveRetryCount = TransferOptions.KeepAliveRetryCount;
    private static int _adaptiveSocketBufferSize = 512 * 1024;
    private static readonly ConcurrentDictionary<string, int> _peerSocketBuffers = new(StringComparer.OrdinalIgnoreCase);
    private static int _peerProfilesLoaded;
    private static long _lastPeerProfilesPersistTick;
    private static readonly string PeerProfilesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LanCopy", "peer-network-profiles.json");
    
    // OPT-FIX #1: Connection pooling para reutilización
    // Q1: _connectionPool nunca se usa; OpenAsync siempre crea TcpClient nuevo. Dead code eliminado.
    
    public string? Pin { get; set; }      // Feature 10: PIN de autenticación
    public bool UseTls { get; set; }      // Feature 9: TLS
    public bool AllowPlaintextFallback { get; set; } // Compatibilidad explícita con servidores legacy sin TLS
    public event EventHandler<string>? TlsFallbackOccurred; // S2: avisa de degradación TLS?plaintext
    public bool UseCompress { get; set; } // Feature 2: compresión deflate

    private sealed record DownloadResumeMap(long BlockSize, long VerifiedBytes, long TotalSize, DateTime UpdatedUtc);

    public string Host => _host;
    public int Port => _port;
    public LanClient(string host, int port) { _host = host; _port = port; }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static CancellationTokenSource StartIdleTimeout(CancellationToken ct, TimeSpan? idleTimeout = null)
    {
        var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(idleTimeout ?? TransferIdleTimeoutSmall);
        return linked;
    }

    private static void TouchIdleTimeout(CancellationTokenSource cts, TimeSpan? idleTimeout = null)
    {
        try
        {
            if (!cts.IsCancellationRequested)
                cts.CancelAfter(idleTimeout ?? TransferIdleTimeoutSmall);
        }
        catch (ObjectDisposedException)
        {
            // Progress callbacks can arrive just after transfer teardown; ignore late touches.
        }
    }

    private static IProgress<(long done, long total)> WrapProgress(IProgress<(long done, long total)>? progress, CancellationTokenSource idleCts, TimeSpan? idleTimeout = null)
    {
        return new Progress<(long done, long total)>(v =>
        {
            TouchIdleTimeout(idleCts, idleTimeout);
            progress?.Report(v);
        });
    }

    private static TimeSpan SelectIdleTimeout(long expectedBytes)
        => expectedBytes >= LargeTransferThresholdBytes ? TransferIdleTimeoutLarge : TransferIdleTimeoutSmall;

    private static Exception MapIdleTimeout(OperationCanceledException ex, CancellationToken callerToken)
        => callerToken.IsCancellationRequested
            ? ex
            : new IOException("svc.connCut", ex);

    private static bool IsLikelyIncompressible(FileStream fs, long size)
    {
        const int sampleSize = 64 * 1024;
        if (size < sampleSize) return false;

        var originalPos = fs.Position;
        // P4: rent buffer from pool to avoid 64KB heap alloc per upload (matches server-side pattern)
        var sample = System.Buffers.ArrayPool<byte>.Shared.Rent(sampleSize);
        try
        {
            fs.Seek(0, SeekOrigin.Begin);
            var read = fs.Read(sample, 0, sampleSize);
            if (read <= 0) return false;

            int distinct = 0;
            var seen = new bool[256];
            for (int i = 0; i < read; i++)
            {
                var b = sample[i];
                if (!seen[b])
                {
                    seen[b] = true;
                    distinct++;
                }
            }

            using var ms = new MemoryStream();
            using (var ds = new DeflateStream(ms, CompressionLevel.Fastest, leaveOpen: true))
                ds.Write(sample, 0, read);
            var compressed = ms.Length;
            var ratio = compressed <= 0 ? 1.0 : compressed / (double)read;

            var highEntropy = distinct >= 240;
            return highEntropy && ratio >= 0.97;
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(sample);
            fs.Seek(originalPos, SeekOrigin.Begin);
        }
    }

    private void ReportTransferSample(long bytes, TimeSpan elapsed)
    {
        if (bytes <= 0 || elapsed <= TimeSpan.Zero) return;
        var mbps = (bytes / 1024d / 1024d) / Math.Max(0.001, elapsed.TotalSeconds);
        var target = mbps switch
        {
            >= 80 => 2 * 1024 * 1024,
            >= 30 => 1024 * 1024,
            >= 8 => 512 * 1024,
            _ => 256 * 1024
        };
        Interlocked.Exchange(ref _adaptiveSocketBufferSize, target);
        _peerSocketBuffers[_host] = target;
        _ = Task.Run(TryPersistPeerProfilesAsync); // B7: fire-and-forget async persist
    }

    private static int GetAdaptiveSocketBufferForHost(string host)
    {
        EnsurePeerProfilesLoaded();
        if (!string.IsNullOrWhiteSpace(host) && _peerSocketBuffers.TryGetValue(host, out var hostValue))
            return hostValue;
        return Volatile.Read(ref _adaptiveSocketBufferSize);
    }

    private static void EnsurePeerProfilesLoaded()
    {
        if (Interlocked.CompareExchange(ref _peerProfilesLoaded, 1, 0) != 0) return;
        try
        {
            if (!File.Exists(PeerProfilesPath)) return;
            var doc = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(PeerProfilesPath));
            if (doc == null) return;
            foreach (var kv in doc)
            {
                var clamped = Math.Clamp(kv.Value, 128 * 1024, 2 * 1024 * 1024);
                _peerSocketBuffers[kv.Key] = clamped;
            }
        }
        catch (Exception ex)
        {
            Log.Debug("client", "peer-profile-load-failed", new { error = ex.Message });
        }
    }

    private static readonly object _profilesFileLock = new();
    private static async Task TryPersistPeerProfilesAsync()
    {
        try
        {
            var last = Interlocked.Read(ref _lastPeerProfilesPersistTick);
            var now = Environment.TickCount64;
            if (now - last < 5000) return;
            if (Interlocked.CompareExchange(ref _lastPeerProfilesPersistTick, now, last) != last) return;
            var snapshot = _peerSocketBuffers.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
            var json = JsonSerializer.Serialize(snapshot);
            lock (_profilesFileLock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PeerProfilesPath)!);
                File.WriteAllText(PeerProfilesPath, json);
            }
        }
        catch (Exception ex)
        {
            Log.Debug("client", "peer-profile-save-failed", new { error = ex.Message });
        }
    }

    private static string GetResumeMapPath(string partPath) => partPath + ".lcmap";

    private static long LoadVerifiedOffsetFromMap(string mapPath, long currentPartLength)
    {
        if (currentPartLength <= 0 || !File.Exists(mapPath)) return currentPartLength;
        try
        {
            var map = JsonSerializer.Deserialize<DownloadResumeMap>(File.ReadAllText(mapPath));
            if (map is null) return currentPartLength;
            if (map.BlockSize <= 0 || map.VerifiedBytes < 0) return currentPartLength;
            return Math.Min(currentPartLength, map.VerifiedBytes);
        }
        catch (IOException ex)
        {
            Log.Debug("client", "resume-map-read-io-failed", new { path = mapPath, error = ex.Message });
            return currentPartLength;
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Debug("client", "resume-map-read-access-denied", new { path = mapPath, error = ex.Message });
            return currentPartLength;
        }
        catch (JsonException ex)
        {
            Log.Debug("client", "resume-map-parse-failed", new { path = mapPath, error = ex.Message });
            return currentPartLength;
        }
    }

    private static async Task SaveResumeMapAsync(string mapPath, long totalSize, long verifiedBytes, CancellationToken ct = default)
    {
        try
        {
            var safeVerified = Math.Max(0, Math.Min(totalSize, verifiedBytes));
            var json = JsonSerializer.Serialize(new DownloadResumeMap(
                BlockSize: ResumeMapBlockSize,
                VerifiedBytes: safeVerified,
                TotalSize: totalSize,
                UpdatedUtc: DateTime.UtcNow));
            await File.WriteAllTextAsync(mapPath, json, CancellationToken.None); // B7: no cancelar escritura intermedia del mapa
        }
        catch (IOException ex) { Log.Debug("client", "resume-map-write-io-failed", new { path = mapPath, error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { Log.Debug("client", "resume-map-write-access-denied", new { path = mapPath, error = ex.Message }); }
    }

    private static void TryDeleteResumeMap(string mapPath)
    {
        try
        {
            if (File.Exists(mapPath)) File.Delete(mapPath);
        }
        catch (IOException ex) { Log.Debug("client", "resume-map-delete-io-failed", new { path = mapPath, error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { Log.Debug("client", "resume-map-delete-access-denied", new { path = mapPath, error = ex.Message }); }
    }

    private static void EnsureOk(JsonElement resp)
    {
        var status = resp.TryGetProperty("status", out var s) ? s.GetString() : null;
        if (status != "ok")
        {
            var msg = resp.TryGetProperty("error", out var e) ? e.GetString() : "svc.unknownError";
            throw new Exception(msg);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            // BUG-FIX: RemoteCertificate puede ser un new X509Certificate2(c) creado en el
            // callback TLS cuando el runtime no entrega directamente un X509Certificate2.
            // X509Certificate2 implementa IDisposable (SafeHandle nativo) y debe ser dispuesto.
            var cert = RemoteCertificate;
            RemoteCertificate = null;
            try { cert?.Dispose(); }
            catch (Exception ex) { Log.Debug("client", "cert-dispose-failed", new { host = _host, error = ex.Message }); }
        }
    }

    /// <summary>
    /// M6: Método compartido entre UploadDeltaAsync y DownloadDeltaAsync — antes cada uno
    /// tenía el mismo bloque de 18 líneas duplicadas. Usa ArrayPool para el buffer de lectura
    /// y calcula hashes SHA-256 por bloques de blockSize bytes.
    /// </summary>
    private static async Task<List<string>> ComputeLocalBlockHashesAsync(
        string localPath, int blockSize, CancellationToken ct)
    {
        var hashes = new List<string>();
        await using var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 0, true);
        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(blockSize);
        try
        {
            int read;
            while ((read = await fs.ReadAsync(buffer.AsMemory(0, blockSize), ct)) > 0)
                // O5: ToLowerInvariant() eliminado — la comparación usa OrdinalIgnoreCase. -1 alloc/bloque.
                hashes.Add(Convert.ToHexString(SHA256.HashData(buffer.AsSpan(0, read))));
        }
        finally { System.Buffers.ArrayPool<byte>.Shared.Return(buffer); }
        return hashes;
    }

    // OPT-FIX #1: Implementar IAsyncDisposable correctamente
    public ValueTask DisposeAsync()
    {
        // BUG-FIX: Dispose(false) saltaba el bloque if (disposing), causando fuga del
        // SafeHandle de RemoteCertificate. Usar Dispose(true) igual que Dispose() síncrono.
        Dispose(true);
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
