using System.IO;
using System.IO.Compression;
using System.Net.Security;
using System.Net.Sockets;
using System.Diagnostics;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Collections.Concurrent;
using LanCopy.Models;

namespace LanCopy.Services;

public sealed class LanClient : IDisposable, IAsyncDisposable
{
    public sealed record RemoteStat(bool Exists, bool IsDirectory, long Size, long LastWriteUtcTicks);
    public sealed record RemoteHealth(int ConnCurrent, int ConnLimit, int PerIpLimit, int ActiveIps, int PinFailsTracked, int HashCacheEntries, int CommandRateTracked, int CommandRateLimit, int CommandRateWindowSeconds);

    private readonly string _host;
    private readonly int _port;
    private const long MaxCompressInMemory = 200L * 1024 * 1024;
    private const long MaxLocalHashBeforeUploadBytes = 512L * 1024 * 1024; // 512 MB
    private const long ResumeMapBlockSize = 4L * 1024 * 1024;
    private static readonly TimeSpan TransferIdleTimeoutSmall = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan TransferIdleTimeoutLarge = TimeSpan.FromSeconds(180);
    private const long LargeTransferThresholdBytes = 1L * 1024 * 1024 * 1024; // 1 GB
    private const int KeepAliveIdleSeconds = 15;
    private const int KeepAliveIntervalSeconds = 5;
    private const int KeepAliveRetryCount = 3;
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
    public event EventHandler<string>? TlsFallbackOccurred; // S2: avisa de degradaci�n TLS?plaintext
    public bool UseCompress { get; set; } // Feature 2: compresión deflate

    private sealed record DownloadResumeMap(long BlockSize, long VerifiedBytes, long TotalSize, DateTime UpdatedUtc);

    public string Host => _host;
    public int Port => _port;
    public LanClient(string host, int port) { _host = host; _port = port; }

    private async Task<(TcpClient tcp, Stream stream)> OpenAsync(CancellationToken ct)
    {
        var adaptiveBuffer = Math.Clamp(GetAdaptiveSocketBufferForHost(_host), 128 * 1024, 2 * 1024 * 1024);
        var tcp = new TcpClient
        {
            NoDelay = true,
            ReceiveBufferSize = adaptiveBuffer,
            SendBufferSize = adaptiveBuffer,
        };
        try
        {
        await tcp.ConnectAsync(_host, _port, ct);
        ConfigureSocket(tcp.Client);
        Stream stream = tcp.GetStream();

        // Feature 9: envolver con SslStream si TLS activo
        if (UseTls)
        {
            // TOFU real: fija la huella del cert del host en el primer uso y la verifica despues.
            var ssl = new SslStream(stream, leaveInnerStreamOpen: false);
            try
            {
                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = _host,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    RemoteCertificateValidationCallback = (s, c, ch, e) => CertTrust.ValidateOrPin(_host, c),
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                }, ct);
                stream = ssl;
            }
            catch (System.IO.IOException)
            {
                // El servidor cerró la conexión durante el handshake TLS (servidor sin TLS o en
                // texto plano). Reconectar sin TLS para compatibilidad con servidores no-TLS.
                TlsFallbackOccurred?.Invoke(this, _host);
                ssl.Dispose();
                tcp.Dispose();
                tcp = new TcpClient
                {
                    NoDelay = true,
                    ReceiveBufferSize = adaptiveBuffer,
                    SendBufferSize = adaptiveBuffer,
                };
                await tcp.ConnectAsync(_host, _port, ct);
                ConfigureSocket(tcp.Client);
                stream = tcp.GetStream();
            }
        }

        // Lectura de cabeceras con buffer (sin consumir el payload binario que sigue).
        stream = new BufferedLineStream(stream);

        // Feature 10: enviar auth si PIN configurado
        if (!string.IsNullOrEmpty(Pin))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { cmd = "auth", pin = Pin }), ct);
            var ackLine = await Protocol.ReadLineAsync(stream, ct);
            var ack = JsonSerializer.Deserialize<JsonElement>(ackLine);
            EnsureOk(ack);
        }

        return (tcp, stream);
        }
        catch
        {
            // Si falla el handshake TLS o el auth PIN, no dejar el socket colgado.
            try { tcp.Dispose(); } catch { }
            throw;
        }
    }

    // -- LIST --

    public async Task<List<FileEntry>> ListAsync(string path, CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { cmd = "list", path }), ct);
        var line = await Protocol.ReadLineAsync(stream, ct);
        var resp = JsonSerializer.Deserialize<JsonElement>(line);
        EnsureOk(resp);
        return JsonSerializer.Deserialize<List<FileEntry>>(resp.GetProperty("entries").GetRawText())!;
    }

    // -- LIST RECURSIVE (para transferir carpetas) --

    public async Task<List<FileEntry>> ListRecursiveAsync(string path, CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { cmd = "list", path, recursive = true }), ct);
        var line = await Protocol.ReadLineAsync(stream, ct);
        var resp = JsonSerializer.Deserialize<JsonElement>(line);
        EnsureOk(resp);
        return JsonSerializer.Deserialize<List<FileEntry>>(resp.GetProperty("entries").GetRawText())!;
    }

    // -- GET --

    public async Task DownloadAsync(
        string remotePath, string localPath,
        IProgress<(long done, long total)>? progress = null,
        CancellationToken ct = default)
    {
        var transferSw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
        // Descarga atomica con reanudacion (idea-resume): se escribe a un .part y se promueve
        // al destino final solo al verificar el hash. Si existe un .part previo, se reanuda.
        var partPath = localPath + ".part";
        var resumeMapPath = GetResumeMapPath(partPath);
        long resume = 0;
        // BUG-FIX-002: Usar FileStream.OpenRead atomicamente en lugar de File.Exists para evitar TOCTOU
        try
        {
            using var resumeCheckFs = new FileStream(partPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            resume = resumeCheckFs.Length;
        }
        catch (FileNotFoundException) 
        { 
            resume = 0; 
        }
        var mappedResume = LoadVerifiedOffsetFromMap(resumeMapPath, resume);
        if (mappedResume >= 0 && mappedResume < resume)
        {
            try
            {
                await using var fsFix = new FileStream(partPath, FileMode.Open, FileAccess.Write, FileShare.Read);
                fsFix.SetLength(mappedResume);
                resume = mappedResume;
            }
            catch { }
        }
        bool wantCompress = UseCompress && resume == 0; // no se comprime al reanudar

        using var idleCts = StartIdleTimeout(ct);
        var ioCt = idleCts.Token;

        var (tcp, stream) = await OpenAsync(ioCt);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "get", path = remotePath, compress = wantCompress, offset = resume }), ioCt);
        TouchIdleTimeout(idleCts);
        var headerLine = await Protocol.ReadLineAsync(stream, ioCt);
        TouchIdleTimeout(idleCts);
        var header = JsonSerializer.Deserialize<JsonElement>(headerLine);
        EnsureOk(header);
        var size = header.GetProperty("size").GetInt64();
        var idleTimeout = SelectIdleTimeout(size);
        TouchIdleTimeout(idleCts, idleTimeout);
        long rangeFrom = header.TryGetProperty("range_from", out var rfEl) ? rfEl.GetInt64() : 0;

        var dir = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        bool serverCompressed = header.TryGetProperty("compress", out var compEl) && compEl.GetBoolean()
                                && header.TryGetProperty("compressed_size", out var _compSizeProp);

        // Solo reanudamos si el servidor lo honra exactamente y no comprime; si no, empezamos limpio.
        bool doResume = resume > 0 && !serverCompressed && rangeFrom == resume;
        if (resume > 0 && !doResume)
        {
            try { File.Delete(partPath); } catch { }
            TryDeleteResumeMap(resumeMapPath);
        }
        if (serverCompressed) TryDeleteResumeMap(resumeMapPath);

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var fs = doResume
            ? new FileStream(partPath, FileMode.Open, FileAccess.ReadWrite)
            : new FileStream(partPath, FileMode.Create, FileAccess.ReadWrite);
        try
        {
            if (doResume)
            {
                // Pre-hashea los bytes ya descargados para verificar el fichero completo al final.
                fs.Seek(0, SeekOrigin.Begin);
                var pre = new byte[Protocol.SelectCopyBufferSize(rangeFrom)];
                long left = rangeFrom; int pr;
                while (left > 0 && (pr = await fs.ReadAsync(pre.AsMemory(0, (int)Math.Min(pre.Length, left)), ct)) > 0)
                {
                    hasher.AppendData(pre, 0, pr);
                    left -= pr;
                    TouchIdleTimeout(idleCts, idleTimeout);
                }
                fs.Seek(0, SeekOrigin.End);
            }

            if (serverCompressed)
            {
                var compressedSize = header.GetProperty("compressed_size").GetInt64();
                if (compressedSize < 0 || compressedSize > MaxCompressInMemory)
                    throw new InvalidDataException("compressed_size excede el limite permitido");
                
                // BUG-FIX #2: Usar archivo temporal en lugar de MemoryStream para evitar OOM
                var compPath = localPath + ".comp~";
                try
                {
                    using (var compFile = File.Create(compPath))
                    {
                        await Protocol.CopyExactAsync(stream, compFile, compressedSize, WrapProgress(null, idleCts, idleTimeout), ioCt);
                    }
                    TouchIdleTimeout(idleCts, idleTimeout);
                    
                    using (var compFile = File.OpenRead(compPath))
                    {
                        await using var ds = new DeflateStream(compFile, CompressionMode.Decompress);
                        var dbuf = new byte[Protocol.SelectCopyBufferSize(size)];
                        int dr; long written = 0;
                        while ((dr = await ds.ReadAsync(dbuf, ioCt)) > 0)
                        {
                            written += dr;
                            if (written > size) throw new InvalidDataException("Descompresion excede el tamano declarado (posible zip-bomb)");
                            await fs.WriteAsync(dbuf.AsMemory(0, dr), ioCt);
                            TouchIdleTimeout(idleCts, idleTimeout);
                            hasher.AppendData(dbuf, 0, dr);
                        }
                        if (written != size)
                            throw new InvalidDataException("Descompresion incompleta: el tamano final no coincide con el esperado");
                    }
                }
                finally
                {
                    try { File.Delete(compPath); } catch { }
                }
            }
            else
            {
                // Recibe (size - rangeFrom) bytes hasheando el contenido completo.
                var buf = new byte[Protocol.SelectCopyBufferSize(size)];
                long remaining = size - rangeFrom, done = rangeFrom;
                long nextMapCheckpoint = Math.Max(ResumeMapBlockSize, ((done / ResumeMapBlockSize) + 1) * ResumeMapBlockSize);
                while (remaining > 0)
                {
                    var toRead = (int)Math.Min(remaining, buf.Length);
                    var read = await stream.ReadAsync(buf.AsMemory(0, toRead), ioCt);
                    if (read == 0) throw new EndOfStreamException("svc.connCut");
                    await fs.WriteAsync(buf.AsMemory(0, read), ioCt);
                    hasher.AppendData(buf, 0, read);
                    await RateLimiter.Global.ThrottleAsync(read, ioCt);
                    remaining -= read; done += read;
                    TouchIdleTimeout(idleCts, idleTimeout);
                    progress?.Report((done, size));
                    if (!serverCompressed && done >= nextMapCheckpoint)
                    {
                        await SaveResumeMapAsync(resumeMapPath, size, done);
                        nextMapCheckpoint += ResumeMapBlockSize;
                    }
                }
                if (!serverCompressed) await SaveResumeMapAsync(resumeMapPath, size, size);
            }

            var actualSha256 = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
            string? mismatch = null;
            if (header.TryGetProperty("sha256", out var sha256El))
            {
                var expected = sha256El.GetString() ?? "";
                if (!string.Equals(expected, actualSha256, StringComparison.OrdinalIgnoreCase))
                    mismatch = "SHA256";
            }
            else if (header.TryGetProperty("sha1", out var sha1El))
            {
                var expected = sha1El.GetString() ?? "";
                fs.Seek(0, SeekOrigin.Begin);
                var actual = Convert.ToHexString(await SHA1.HashDataAsync(fs, ioCt)).ToLowerInvariant();
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                    mismatch = "SHA1";
            }
            if (mismatch != null)
            {
                // Hash incorrecto: el .part esta corrupto, lo borramos para no reanudar sobre basura.
                await fs.DisposeAsync();
                try { File.Delete(partPath); } catch { }
                TryDeleteResumeMap(resumeMapPath);
                throw new Exception($"Checksum {mismatch} no coincide para {Path.GetFileName(localPath)}");
            }
        }
        finally
        {
            await fs.DisposeAsync();
        }

        // Exito: promover .part al destino final (sobrescribe si existe).
        File.Move(partPath, localPath, overwrite: true);
        TryDeleteResumeMap(resumeMapPath);
        ReportTransferSample(size, transferSw.Elapsed);
        }
        catch (OperationCanceledException ex)
        {
            throw MapIdleTimeout(ex, ct);
        }
    }

    // -- PUT --

    public async Task UploadAsync(
        string localPath, string remotePath,
        IProgress<(long done, long total)>? progress = null,
        CancellationToken ct = default,
        Action<long, long>? onResumeAccepted = null)
    {
        var transferSw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Abre archivo primero para tamaño real → evita race condition (#4)
            await using var fs = File.OpenRead(localPath);
            var size = fs.Length;

            // Compresión adaptativa: omite deflate para tipos ya comprimidos y payloads
            // de alta entropía donde la ganancia suele ser negativa.
            bool doCompress = UseCompress
                && size > 0
                && size <= MaxCompressInMemory
                && !Protocol.IsCompressedExtension(localPath)
                && !IsLikelyIncompressible(fs, size);

            long resumeOffset = 0;
            bool usedResumeUpload = false;
            if (!doCompress && size > 0)
            {
                try
                {
                    var st = await GetStatAsync(remotePath, ct);
                    if (st is { Exists: true, IsDirectory: false } && st.Size > 0 && st.Size < size)
                    {
                        resumeOffset = st.Size;
                        usedResumeUpload = true;
                    }
                }
                catch { }
            }

            // Integridad SHA-256 local: en reanudación la omitimos para no bloquear
            // reconexión con ficheros grandes (el cuello de botella era re-hashear GBs).
            string? sha256Local = null;
            if (resumeOffset == 0 && size <= MaxLocalHashBeforeUploadBytes)
            {
                sha256Local = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();
                fs.Seek(0, SeekOrigin.Begin);
            }

            using var compressedPayload = doCompress ? new MemoryStream() : null;
            long compressedSize = 0;
            if (doCompress)
            {
                var payloadStream = compressedPayload ?? throw new InvalidOperationException("Compression buffer was not initialized.");

                await using (var ds = new DeflateStream(payloadStream, CompressionLevel.Fastest, leaveOpen: true))
                    await fs.CopyToAsync(ds, ct);
                compressedSize = payloadStream.Length;
                // BUG-FIX-003: Validate compression ratio to prevent unbounded memory growth
                if (size > 0 && compressedSize > size * 1.1)
                {
                    throw new InvalidOperationException("File incompressible - compression exceeded 110% of original");
                }
                payloadStream.Seek(0, SeekOrigin.Begin);
            }

            var idleTimeout = SelectIdleTimeout(size);
            using var idleCts = StartIdleTimeout(ct, idleTimeout);
            var ioCt = idleCts.Token;

            var (tcp, stream) = await OpenAsync(ioCt);
            using var _ = tcp;

            if (doCompress && compressedPayload != null)
            {
                await Protocol.WriteLineAsync(stream,
                    JsonSerializer.Serialize(new { cmd = "put", path = remotePath, size, compress = true, compressed_size = compressedSize }), ioCt);
                TouchIdleTimeout(idleCts, idleTimeout);
                await Protocol.CopyExactAsync(compressedPayload, stream, compressedSize, WrapProgress(progress, idleCts, idleTimeout), ioCt);
                TouchIdleTimeout(idleCts, idleTimeout);
            }
            else if (resumeOffset > 0)
            {
                await Protocol.WriteLineAsync(stream,
                    JsonSerializer.Serialize(new { cmd = "put_resume", path = remotePath, size, offset = resumeOffset }), ioCt);
                TouchIdleTimeout(idleCts, idleTimeout);

                var preAckLine = await Protocol.ReadLineAsync(stream, ioCt);
                TouchIdleTimeout(idleCts, idleTimeout);
                var preAck = JsonSerializer.Deserialize<JsonElement>(preAckLine);
                EnsureOk(preAck);

                var accepted = preAck.TryGetProperty("range_from", out var rf) && rf.TryGetInt64(out var rv)
                    ? rv : 0L;
                if (accepted < 0 || accepted > size) accepted = 0;
                if (accepted > 0)
                    onResumeAccepted?.Invoke(accepted, size);

                fs.Seek(accepted, SeekOrigin.Begin);
                var remaining = size - accepted;

                IProgress<(long done, long total)>? adjustedProgress = progress is null
                    ? null
                    : new Progress<(long done, long total)>(v => progress.Report((accepted + v.done, size)));

                if (remaining > 0)
                    await Protocol.CopyExactAsync(fs, stream, remaining, WrapProgress(adjustedProgress, idleCts, idleTimeout), ioCt);
                TouchIdleTimeout(idleCts, idleTimeout);
            }
            else
            {
                await Protocol.WriteLineAsync(stream,
                    JsonSerializer.Serialize(new { cmd = "put", path = remotePath, size }), ioCt);
                TouchIdleTimeout(idleCts, idleTimeout);
                await Protocol.CopyExactAsync(fs, stream, size, WrapProgress(progress, idleCts, idleTimeout), ioCt);
                TouchIdleTimeout(idleCts, idleTimeout);
            }

            var ackLine = await Protocol.ReadLineAsync(stream, ioCt);
            TouchIdleTimeout(idleCts, idleTimeout);
            var ack = JsonSerializer.Deserialize<JsonElement>(ackLine);
            EnsureOk(ack);

            // Verificar integridad SHA-256 reportada por el servidor cuando hubo envío completo.
            if (sha256Local != null && ack.TryGetProperty("sha256", out var sha256El))
            {
                var serverSha256 = sha256El.GetString() ?? "";
                if (!string.Equals(sha256Local, serverSha256, StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"Checksum SHA256 no coincide para {Path.GetFileName(localPath)}");
            }
            else if (usedResumeUpload)
            {
                var st = await GetStatAsync(remotePath, ct);
                if (st is not { Exists: true, IsDirectory: false } || st.Size != size)
                    throw new IOException("La verificación final de reanudación no coincide en tamaño remoto.");
            }
            ReportTransferSample(size, transferSw.Elapsed);
        }
        catch (OperationCanceledException ex)
        {
            throw MapIdleTimeout(ex, ct);
        }
    }

    // ── TEXT (idea-clipboard) ─────────────────────────────────────────────────────

    public async Task SendTextAsync(string text, CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "text", text }), ct);
        var line = await Protocol.ReadLineAsync(stream, ct);
        EnsureOk(JsonSerializer.Deserialize<JsonElement>(line));
    }

    public async Task SendDisconnectNoticeAsync(CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "disconnect_notice" }), ct);
        var line = await Protocol.ReadLineAsync(stream, ct);
        EnsureOk(JsonSerializer.Deserialize<JsonElement>(line));
    }

    // ── DELETE ────────────────────────────────────────────────────────────────────

    public async Task DeleteAsync(string remotePath, CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "delete", path = remotePath }), ct);
        var line = await Protocol.ReadLineAsync(stream, ct);
        EnsureOk(JsonSerializer.Deserialize<JsonElement>(line));
    }

    // ── RENAME ────────────────────────────────────────────────────────────────────

    public async Task RenameAsync(string remotePath, string newName, CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "rename", path = remotePath, newname = newName }), ct);
        var line = await Protocol.ReadLineAsync(stream, ct);
        EnsureOk(JsonSerializer.Deserialize<JsonElement>(line));
    }

    // ── MKDIR ─────────────────────────────────────────────────────────────────────

    public async Task CreateDirectoryAsync(string remotePath, CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "mkdir", path = remotePath }), ct);
        var line = await Protocol.ReadLineAsync(stream, ct);
        EnsureOk(JsonSerializer.Deserialize<JsonElement>(line));
    }

    // ── SHA1/SHA256 ───────────────────────────────────────────────────────────────

    /// <summary>
    /// SHA1 hex lowercase remoto. Devuelve null si no existe o error.
    /// </summary>
    public async Task<string?> GetSha1Async(string remotePath, CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "sha1", path = remotePath }), ct);
        var line = await Protocol.ReadLineAsync(stream, ct);
        var resp = JsonSerializer.Deserialize<JsonElement>(line);
        var status = resp.TryGetProperty("status", out var s) ? s.GetString() : null;
        if (status != "ok") return null;
        return resp.GetProperty("sha1").GetString();
    }

    /// <summary>
    /// SHA256 hex lowercase remoto. Devuelve null si no existe o peer no soporta comando.
    /// </summary>
    public async Task<string?> GetSha256Async(string remotePath, CancellationToken ct = default)
    {
        try
        {
            var (tcp, stream) = await OpenAsync(ct);
            using var _ = tcp;
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { cmd = "sha256", path = remotePath }), ct);
            var line = await Protocol.ReadLineAsync(stream, ct);
            var resp = JsonSerializer.Deserialize<JsonElement>(line);
            var status = resp.TryGetProperty("status", out var s) ? s.GetString() : null;
            if (status != "ok") return null;
            return resp.GetProperty("sha256").GetString();
        }
        catch
        {
            return null;
        }
    }

    // ── STAT ──────────────────────────────────────────────────────────────────────

    public async Task<RemoteStat?> GetStatAsync(string remotePath, CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;

        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "stat", path = remotePath }), ct);

        var line = await Protocol.ReadLineAsync(stream, ct);
        var resp = JsonSerializer.Deserialize<JsonElement>(line);
        EnsureOk(resp);

        var exists = resp.TryGetProperty("exists", out var existsEl) && existsEl.GetBoolean();
        if (!exists) return new RemoteStat(false, false, 0, 0);

        var isDirectory = resp.TryGetProperty("isDirectory", out var dirEl) && dirEl.GetBoolean();
        var size = resp.TryGetProperty("size", out var sizeEl) ? sizeEl.GetInt64() : 0L;
        var ticks = resp.TryGetProperty("lastWriteUtcTicks", out var ticksEl) ? ticksEl.GetInt64() : 0L;
        return new RemoteStat(true, isDirectory, size, ticks);
    }


    // ── HEALTH ───────────────────────────────────────────────────────────────────
    public async Task<RemoteHealth?> GetHealthAsync(CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { cmd = "health" }), ct);
        var line = await Protocol.ReadLineAsync(stream, ct);
        var resp = JsonSerializer.Deserialize<JsonElement>(line);
        EnsureOk(resp);

        int GetInt(string name, int def = 0) => resp.TryGetProperty(name, out var el) && el.TryGetInt32(out var n) ? n : def;
        return new RemoteHealth(
            GetInt("connCurrent"),
            GetInt("connLimit"),
            GetInt("perIpLimit"),
            GetInt("activeIps"),
            GetInt("pinFailsTracked"),
            GetInt("hashCacheEntries"),
            GetInt("commandRateTracked"),
            GetInt("commandRateLimit"),
            GetInt("commandRateWindowSeconds"));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static void ConfigureSocket(Socket socket)
    {
        socket.NoDelay = true;
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        try
        {
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, KeepAliveIdleSeconds);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, KeepAliveIntervalSeconds);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, KeepAliveRetryCount);
        }
        catch (SocketException) { }
        catch (PlatformNotSupportedException) { }
    }

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
        try
        {
            fs.Seek(0, SeekOrigin.Begin);
            var sample = new byte[sampleSize];
            var read = fs.Read(sample, 0, sample.Length);
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
            Debug.WriteLine($"[LanCopy] peer profile load error: {ex.Message}");
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
            Debug.WriteLine($"[LanCopy] peer profile save error: {ex.Message}");
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
            Debug.WriteLine($"[LanCopy] resume-map read IO error: {ex.Message}");
            return currentPartLength;
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"[LanCopy] resume-map read access error: {ex.Message}");
            return currentPartLength;
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[LanCopy] resume-map parse error: {ex.Message}");
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
        catch (IOException ex) { Debug.WriteLine($"[LanCopy] resume-map write IO error: {ex.Message}"); }
        catch (UnauthorizedAccessException ex) { Debug.WriteLine($"[LanCopy] resume-map write access error: {ex.Message}"); }
    }

    private static void TryDeleteResumeMap(string mapPath)
    {
        try
        {
            if (File.Exists(mapPath)) File.Delete(mapPath);
        }
        catch (IOException ex) { Debug.WriteLine($"[LanCopy] resume-map delete IO error: {ex.Message}"); }
        catch (UnauthorizedAccessException ex) { Debug.WriteLine($"[LanCopy] resume-map delete access error: {ex.Message}"); }
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
        // No hay recursos locales a liberar, el pool es global
    }

    // OPT-FIX #1: Implementar IAsyncDisposable correctamente
    public async ValueTask DisposeAsync()
    {
        Dispose(false);
        await Task.Yield();
        GC.SuppressFinalize(this);
    }
}
