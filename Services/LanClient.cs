using System.IO;
using System.IO.Compression;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using LanCopy.Models;

namespace LanCopy.Services;

public sealed class LanClient : IDisposable
{
    public sealed record RemoteStat(bool Exists, bool IsDirectory, long Size, long LastWriteUtcTicks);
    public sealed record RemoteHealth(int ConnCurrent, int ConnLimit, int PerIpLimit, int ActiveIps, int PinFailsTracked, int HashCacheEntries, int CommandRateTracked, int CommandRateLimit, int CommandRateWindowSeconds);

    private readonly string _host;
    private readonly int _port;
    private const long MaxCompressInMemory = 200L * 1024 * 1024;
    private const long MaxLocalHashBeforeUploadBytes = 512L * 1024 * 1024; // 512 MB
    private static readonly TimeSpan TransferIdleTimeout = TimeSpan.FromSeconds(20);
    private const int KeepAliveIdleSeconds = 15;
    private const int KeepAliveIntervalSeconds = 5;
    private const int KeepAliveRetryCount = 3;
    public string? Pin { get; set; }      // Feature 10: PIN de autenticación
    public bool UseTls { get; set; }      // Feature 9: TLS
    public bool UseCompress { get; set; } // Feature 2: compresión deflate

    public LanClient(string host, int port) { _host = host; _port = port; }

    private async Task<(TcpClient tcp, Stream stream)> OpenAsync(CancellationToken ct)
    {
        var tcp = new TcpClient
        {
            NoDelay = true,
            ReceiveBufferSize = 512 * 1024,
            SendBufferSize = 512 * 1024,
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
            var ssl = new SslStream(stream, leaveInnerStreamOpen: false,
                (sender, cert, chain, errors) => CertTrust.ValidateOrPin(_host, cert));
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = _host,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                RemoteCertificateValidationCallback = (s, c, ch, e) => CertTrust.ValidateOrPin(_host, c),
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            }, ct);
            stream = ssl;
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
        try
        {
        // Descarga atomica con reanudacion (idea-resume): se escribe a un .part y se promueve
        // al destino final solo al verificar el hash. Si existe un .part previo, se reanuda.
        var partPath = localPath + ".part";
        long resume = 0;
        if (File.Exists(partPath))
        {
            try { resume = new FileInfo(partPath).Length; } catch { resume = 0; }
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
        long rangeFrom = header.TryGetProperty("range_from", out var rfEl) ? rfEl.GetInt64() : 0;

        var dir = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        bool serverCompressed = header.TryGetProperty("compress", out var compEl) && compEl.GetBoolean()
                                && header.TryGetProperty("compressed_size", out var _compSizeProp);

        // Solo reanudamos si el servidor lo honra exactamente y no comprime; si no, empezamos limpio.
        bool doResume = resume > 0 && !serverCompressed && rangeFrom == resume;
        if (resume > 0 && !doResume) { try { File.Delete(partPath); } catch { } }

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
                var pre = new byte[Protocol.BufferSize];
                long left = rangeFrom; int pr;
                while (left > 0 && (pr = await fs.ReadAsync(pre.AsMemory(0, (int)Math.Min(pre.Length, left)), ct)) > 0)
                {
                    hasher.AppendData(pre, 0, pr);
                    left -= pr;
                    TouchIdleTimeout(idleCts);
                }
                fs.Seek(0, SeekOrigin.End);
            }

            if (serverCompressed)
            {
                var compressedSize = header.GetProperty("compressed_size").GetInt64();
                if (compressedSize < 0 || compressedSize > MaxCompressInMemory)
                    throw new InvalidDataException("compressed_size excede el limite permitido");
                using var compBuf = new MemoryStream();
                await Protocol.CopyExactAsync(stream, compBuf, compressedSize, null, ioCt);
                TouchIdleTimeout(idleCts);
                compBuf.Seek(0, SeekOrigin.Begin);
                await using var ds = new DeflateStream(compBuf, CompressionMode.Decompress);
                var dbuf = new byte[Protocol.BufferSize];
                int dr; long written = 0;
                while ((dr = await ds.ReadAsync(dbuf, ioCt)) > 0)
                {
                    written += dr;
                    if (written > size) throw new InvalidDataException("Descompresion excede el tamano declarado (posible zip-bomb)");
                    await fs.WriteAsync(dbuf.AsMemory(0, dr), ioCt);
                    TouchIdleTimeout(idleCts);
                    hasher.AppendData(dbuf, 0, dr);
                }
            }
            else
            {
                // Recibe (size - rangeFrom) bytes hasheando el contenido completo.
                var buf = new byte[Protocol.BufferSize];
                long remaining = size - rangeFrom, done = rangeFrom;
                while (remaining > 0)
                {
                    var toRead = (int)Math.Min(remaining, buf.Length);
                    var read = await stream.ReadAsync(buf.AsMemory(0, toRead), ioCt);
                    if (read == 0) throw new EndOfStreamException("svc.connCut");
                    await fs.WriteAsync(buf.AsMemory(0, read), ioCt);
                    hasher.AppendData(buf, 0, read);
                    await RateLimiter.Global.ThrottleAsync(read, ioCt);
                    remaining -= read; done += read;
                    TouchIdleTimeout(idleCts);
                    progress?.Report((done, size));
                }
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
                throw new Exception($"Checksum {mismatch} no coincide para {Path.GetFileName(localPath)}");
            }
        }
        finally
        {
            await fs.DisposeAsync();
        }

        // Exito: promover .part al destino final (sobrescribe si existe).
        File.Move(partPath, localPath, overwrite: true);
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
        try
        {
            // Abre archivo primero para tamaño real → evita race condition (#4)
            await using var fs = File.OpenRead(localPath);
            var size = fs.Length;

            // Feature 2: compresión deflate opcional (solo archivos <= 200 MB)
            bool doCompress = UseCompress && size > 0 && size <= 200L * 1024 * 1024 && !Protocol.IsCompressedExtension(localPath);

            long resumeOffset = 0;
            if (!doCompress && size > 0)
            {
                try
                {
                    var st = await GetStatAsync(remotePath, ct);
                    if (st is { Exists: true, IsDirectory: false } && st.Size > 0 && st.Size < size)
                        resumeOffset = st.Size;
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

            MemoryStream? compressedPayload = null;
            long compressedSize = 0;
            if (doCompress)
            {
                compressedPayload = new MemoryStream();
                await using (var ds = new DeflateStream(compressedPayload, CompressionLevel.Fastest, leaveOpen: true))
                    await fs.CopyToAsync(ds, ct);
                compressedSize = compressedPayload.Length;
                compressedPayload.Seek(0, SeekOrigin.Begin);
            }

            using var idleCts = StartIdleTimeout(ct);
            var ioCt = idleCts.Token;

            var (tcp, stream) = await OpenAsync(ioCt);
            using var _ = tcp;

            if (doCompress && compressedPayload != null)
            {
                await Protocol.WriteLineAsync(stream,
                    JsonSerializer.Serialize(new { cmd = "put", path = remotePath, size, compress = true, compressed_size = compressedSize }), ioCt);
                TouchIdleTimeout(idleCts);
                await Protocol.CopyExactAsync(compressedPayload, stream, compressedSize, progress, ioCt);
                TouchIdleTimeout(idleCts);
            }
            else if (resumeOffset > 0)
            {
                await Protocol.WriteLineAsync(stream,
                    JsonSerializer.Serialize(new { cmd = "put_resume", path = remotePath, size, offset = resumeOffset }), ioCt);
                TouchIdleTimeout(idleCts);

                var preAckLine = await Protocol.ReadLineAsync(stream, ioCt);
                TouchIdleTimeout(idleCts);
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
                    await Protocol.CopyExactAsync(fs, stream, remaining, adjustedProgress, ioCt);
                TouchIdleTimeout(idleCts);
            }
            else
            {
                await Protocol.WriteLineAsync(stream,
                    JsonSerializer.Serialize(new { cmd = "put", path = remotePath, size }), ioCt);
                TouchIdleTimeout(idleCts);
                await Protocol.CopyExactAsync(fs, stream, size, progress, ioCt);
                TouchIdleTimeout(idleCts);
            }

            var ackLine = await Protocol.ReadLineAsync(stream, ioCt);
            TouchIdleTimeout(idleCts);
            var ack = JsonSerializer.Deserialize<JsonElement>(ackLine);
            EnsureOk(ack);

            // Verificar integridad SHA-256 reportada por el servidor cuando hubo envío completo.
            if (sha256Local != null && ack.TryGetProperty("sha256", out var sha256El))
            {
                var serverSha256 = sha256El.GetString() ?? "";
                if (!string.Equals(sha256Local, serverSha256, StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"Checksum SHA256 no coincide para {Path.GetFileName(localPath)}");
            }
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

    private static CancellationTokenSource StartIdleTimeout(CancellationToken ct)
    {
        var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(TransferIdleTimeout);
        return linked;
    }

    private static void TouchIdleTimeout(CancellationTokenSource cts)
    {
        if (!cts.IsCancellationRequested)
            cts.CancelAfter(TransferIdleTimeout);
    }

    private static Exception MapIdleTimeout(OperationCanceledException ex, CancellationToken callerToken)
        => callerToken.IsCancellationRequested
            ? ex
            : new IOException("svc.connCut", ex);

    private static void EnsureOk(JsonElement resp)
    {
        var status = resp.TryGetProperty("status", out var s) ? s.GetString() : null;
        if (status != "ok")
        {
            var msg = resp.TryGetProperty("error", out var e) ? e.GetString() : "svc.unknownError";
            throw new Exception(msg);
        }
    }

    public void Dispose() { }
}
