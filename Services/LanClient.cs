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
                    RemoteCertificateValidationCallback = (s, c, ch, e) =>
                    {
                        if (c is X509Certificate2 c2) RemoteCertificate = c2;
                        else if (c != null) RemoteCertificate = new X509Certificate2(c);
                        return CertTrust.ValidateOrPin(_host, c);
                    },
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
            try { tcp.Dispose(); }
            catch (Exception ex)
            {
                Log.Warn("client", "open-cleanup-dispose-failed", new { host = _host, port = _port, error = ex.Message });
            }
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
        if (!resp.TryGetProperty("entries", out var entriesEl))
            throw new InvalidDataException("svc.missingEntries"); // respuesta del servidor no incluye 'entries'
        // O2: entriesEl.Deserialize<T>() opera sobre el UTF-8 original directamente.
        // GetRawText() materializaba el JSON a string UTF-16 para re-parsearlo — doble decodificación.
        return entriesEl.Deserialize<List<FileEntry>>()!;
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
        if (!resp.TryGetProperty("entries", out var entriesEl))
            throw new InvalidDataException("svc.missingEntries"); // respuesta del servidor no incluye 'entries'
        // O2: misma mejora que ListAsync — sin GetRawText()
        return entriesEl.Deserialize<List<FileEntry>>()!;
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
            catch (Exception ex)
            {
                Log.Warn("client", "download-truncate-part-failed", new { path = partPath, error = ex.Message });
            }
        }
        bool wantCompress = UseCompress && resume == 0 && !FileEntry.IsAlreadyCompressed(remotePath);

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
        if (!header.TryGetProperty("size", out var sizeEl) || !sizeEl.TryGetInt64(out var size))
            throw new InvalidDataException("svc.missingSize"); // respuesta del servidor no incluye 'size'
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
            try { File.Delete(partPath); }
            catch (Exception ex)
            {
                Log.Warn("client", "download-delete-stale-part-failed", new { path = partPath, error = ex.Message });
            }
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
                var preBufSize = Protocol.SelectCopyBufferSize(rangeFrom);
                var pre = System.Buffers.ArrayPool<byte>.Shared.Rent(preBufSize);
                try
                {
                    long left = rangeFrom; int pr;
                    while (left > 0 && (pr = await fs.ReadAsync(pre.AsMemory(0, (int)Math.Min(preBufSize, left)), ct)) > 0)
                    {
                        hasher.AppendData(pre, 0, pr);
                        left -= pr;
                        TouchIdleTimeout(idleCts, idleTimeout);
                    }
                }
                finally { System.Buffers.ArrayPool<byte>.Shared.Return(pre); }
                fs.Seek(0, SeekOrigin.End);
            }

            if (serverCompressed)
            {
                var compressedSize = header.GetProperty("compressed_size").GetInt64();
                if (compressedSize < 0 || compressedSize > MaxCompressInMemory)
                    throw new InvalidDataException("compressed_size excede el limite permitido");
                
                // BUG-FIX: Usar nombre de archivo temporal unico (GUID) para evitar colision
                // si dos descargas simultaneas del mismo fichero usan el mismo .comp~ y se corrompen.
                var compPath = localPath + "." + System.Guid.NewGuid().ToString("N") + ".comp~";
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
                        // M4: ArrayPool evita aloc large-object por descarga (hasta 4MB según SelectCopyBufferSize)
                        var dbufSize = Protocol.SelectCopyBufferSize(size);
                        var dbuf = System.Buffers.ArrayPool<byte>.Shared.Rent(dbufSize);
                        try
                        {
                            int dr; long written = 0;
                            while ((dr = await ds.ReadAsync(dbuf.AsMemory(0, dbufSize), ioCt)) > 0)
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
                        finally { System.Buffers.ArrayPool<byte>.Shared.Return(dbuf); }
                    }
                }
                finally
                {
                    try { File.Delete(compPath); }
                    catch (Exception ex)
                    {
                        Log.Warn("client", "download-delete-temp-compressed-failed", new { path = compPath, error = ex.Message });
                    }
                }
            }
            else
            {
                // Recibe (size - rangeFrom) bytes hasheando el contenido completo.
                // M4: ArrayPool evita aloc large-object por descarga directa
                var bufSize = Protocol.SelectCopyBufferSize(size);
                var buf = System.Buffers.ArrayPool<byte>.Shared.Rent(bufSize);
                try
                {
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
                finally { System.Buffers.ArrayPool<byte>.Shared.Return(buf); }
            } // cierre del else (M4)

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
                // B9: El DisposeAsync explicito es necesario en Windows — no se puede borrar un archivo abierto.
                // El finally tambien llama DisposeAsync (doble dispose), pero FileStream.DisposeAsync es idempotente.
                await fs.DisposeAsync();
                try { File.Delete(partPath); }
                catch (Exception ex)
                {
                    Log.Warn("client", "download-delete-corrupt-part-failed", new { path = partPath, error = ex.Message });
                }
                TryDeleteResumeMap(resumeMapPath);
                throw new Exception($"Checksum {mismatch} no coincide para {Path.GetFileName(localPath)}");
            }
        }
        finally
        {
            await fs.DisposeAsync(); // Idempotente: no-op si ya fue disposed por el bloque de mismatch
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
                catch (Exception ex)
                {
                    Log.Debug("client", "upload-resume-probe-failed", new { path = remotePath, error = ex.Message });
                }
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
                // BUG-FIX: Si la compresión infla el payload >110%, degradar graciosamente a raw
                // en lugar de lanzar excepción (que causa 4 reintentos fallidos).
                // La heurística IsLikelyIncompressible es de sampling y puede fallar para
                // archivos con datos mezclados (ej: ZIP parcialmente encriptados, PDFs con imágenes).
                if (size > 0 && compressedSize > size * 1.1)
                {
                    doCompress = false;
                    compressedPayload?.Dispose();
                    fs.Seek(0, SeekOrigin.Begin);
                    Log.Debug("client", "upload-compress-ratio-degraded-to-raw", new { path = remotePath, size, compressedSize });
                }
                else
                {
                    payloadStream.Seek(0, SeekOrigin.Begin);
                }
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
        return resp.TryGetProperty("sha1", out var sha1El) ? sha1El.GetString() : null;
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
            return resp.TryGetProperty("sha256", out var sha256El2) ? sha256El2.GetString() : null;
        }
        catch (Exception ex)
        {
            Log.Debug("client", "sha256-query-failed", new { path = remotePath, error = ex.Message });
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
        catch (SocketException ex) { Log.Debug("client", "tcp-keepalive-tuning-socket-failed", new { error = ex.Message }); }
        catch (PlatformNotSupportedException ex) { Log.Debug("client", "tcp-keepalive-tuning-unsupported", new { error = ex.Message }); }
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

    public async Task<List<string>> GetDeltaHashesAsync(string remotePath, int blockSize, CancellationToken ct = default)
    {
        using var idleCts = StartIdleTimeout(ct);
        var ioCt = idleCts.Token;

        var (tcp, stream) = await OpenAsync(ioCt);
        using var _ = tcp;

        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "delta_hashes", path = remotePath, block_size = blockSize }), ioCt);

        var headerLine = await Protocol.ReadLineAsync(stream, ioCt);
        var header = JsonSerializer.Deserialize<JsonElement>(headerLine);
        EnsureOk(header);

        // P3/N9: pre-asignar capacidad con GetArrayLength() — evita rehashes para listas de hashes grandes
        var hashes = new List<string>(
            header.TryGetProperty("hashes", out var hashesEl) && hashesEl.ValueKind == JsonValueKind.Array
                ? hashesEl.GetArrayLength()
                : 0);
        if (hashesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in hashesEl.EnumerateArray())
            {
                var hStr = el.GetString();
                if (hStr != null) hashes.Add(hStr);
            }
        }
        return hashes;
    }

    public async Task DownloadChunkAsync(
        string remotePath, string localPath, long offset, long length,
        IProgress<long>? progress = null, CancellationToken ct = default)
    {
        using var idleCts = StartIdleTimeout(ct);
        var ioCt = idleCts.Token;

        var (tcp, stream) = await OpenAsync(ioCt);
        using var _ = tcp;

        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "get_chunk", path = remotePath, offset, length }), ioCt);

        var headerLine = await Protocol.ReadLineAsync(stream, ioCt);
        var header = JsonSerializer.Deserialize<JsonElement>(headerLine);
        EnsureOk(header);

        if (!header.TryGetProperty("length", out var lengthEl) || !lengthEl.TryGetInt64(out var actualLength))
            throw new InvalidDataException("svc.missingLength"); // respuesta del servidor no incluye 'length'
        if (actualLength <= 0) return;

        // Escribir en el offset exacto del archivo parcial (.part) o final
        FileStream? fs = null;
        for (int retry = 0; retry < 5; retry++)
        {
            try
            {
                fs = new FileStream(localPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                break;
            }
            catch (IOException)
            {
                if (retry == 4) throw;
                await Task.Delay(50, ct);
            }
        }
        await using var _fs = fs!;
        _fs.Seek(offset, SeekOrigin.Begin);

        var rentSize = Protocol.SelectCopyBufferSize(actualLength);
        var rented = System.Buffers.ArrayPool<byte>.Shared.Rent(rentSize);
        try
        {
            long remaining = actualLength;
            while (remaining > 0)
            {
                var toRead = (int)Math.Min(remaining, rented.Length);
                var read = await stream.ReadAsync(rented.AsMemory(0, toRead), ioCt);
                if (read == 0) throw new EndOfStreamException("svc.connCut");
                await _fs.WriteAsync(rented.AsMemory(0, read), ioCt);
                progress?.Report(read);
                remaining -= read;
                TouchIdleTimeout(idleCts);
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public async Task UploadDeltaBlocksAsync(
        string localPath, string remotePath, int blockSize, List<int> blocks, long totalSize,
        IProgress<long>? progress = null, CancellationToken ct = default)
    {
        using var idleCts = StartIdleTimeout(ct);
        var ioCt = idleCts.Token;

        var (tcp, stream) = await OpenAsync(ioCt);
        using var _ = tcp;

        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new
            {
                cmd = "put_delta_blocks",
                path = remotePath,
                block_size = blockSize,
                blocks,
                size = totalSize
            }), ioCt);

        if (blocks.Count > 0)
        {
            await using var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var rentSize = Protocol.SelectCopyBufferSize(blockSize);
            var rented = System.Buffers.ArrayPool<byte>.Shared.Rent(rentSize);
            try
            {
                for (int i = 0; i < blocks.Count; i++)
                {
                    var idx = blocks[i];
                    long blockOffset = (long)idx * blockSize;
                    long blockLen = Math.Min(blockSize, totalSize - blockOffset);
                    if (blockLen <= 0) continue;

                    fs.Seek(blockOffset, SeekOrigin.Begin);
                    long remaining = blockLen;
                    while (remaining > 0)
                    {
                        var toRead = (int)Math.Min(remaining, rented.Length);
                        var read = await fs.ReadAsync(rented.AsMemory(0, toRead), ioCt);
                        if (read == 0) throw new EndOfStreamException("svc.fileTruncated");
                        await stream.WriteAsync(rented.AsMemory(0, read), ioCt);
                        progress?.Report(read);
                        remaining -= read;
                        TouchIdleTimeout(idleCts);
                    }
                }
                await stream.FlushAsync(ioCt);
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
            }
        }

        var ackLine = await Protocol.ReadLineAsync(stream, ioCt);
        var ack = JsonSerializer.Deserialize<JsonElement>(ackLine);
        EnsureOk(ack);
    }

    public async Task DownloadParallelAsync(
        string remotePath, string localPath, int threads,
        IProgress<(long done, long total)>? progress = null,
        CancellationToken ct = default)
    {
        // 1. Obtener detalles del archivo
        var stat = await GetStatAsync(remotePath, ct);
        if (stat == null || !stat.Exists) throw new FileNotFoundException("svc.fileNotFound", remotePath);
        if (stat.IsDirectory) throw new InvalidOperationException("svc.isDir");

        var size = stat.Size;
        if (size <= 4L * 1024 * 1024 || threads <= 1)
        {
            // Fichero pequeño -> descargar directo por canal único para ahorrar overhead
            await DownloadAsync(remotePath, localPath, progress, ct);
            return;
        }

        var partPath = localPath + ".part";
        var dir = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // Pre-asignar archivo de destino
        await using (var fs = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
        {
            fs.SetLength(size);
        }

        // Dividir el archivo en chunks según hilos
        long chunkSize = size / threads;
        var tasks = new List<Task>();
        long totalDone = 0;
        var doneLock = new object();

        for (int i = 0; i < threads; i++)
        {
            long offset = i * chunkSize;
            long length = (i == threads - 1) ? (size - offset) : chunkSize;

            if (length <= 0) continue;

            var threadClient = new LanClient(_host, _port)
            {
                UseTls = this.UseTls,
                UseCompress = this.UseCompress,
                Pin = this.Pin
            };

            var chunkProgress = new Progress<long>(chunkDone =>
            {
                lock (doneLock)
                {
                    totalDone += chunkDone;
                    progress?.Report((totalDone, size));
                }
            });

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await threadClient.DownloadChunkAsync(remotePath, partPath, offset, length, chunkProgress, ct);
                }
                finally
                {
                    await threadClient.DisposeAsync();
                }
            }, ct));
        }

        try
        {
            await Task.WhenAll(tasks);

            // Validar hash local al final
            string localSha;
            await using (var finalFs = new FileStream(partPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                localSha = Convert.ToHexString(await SHA256.HashDataAsync(finalFs, ct)).ToLowerInvariant();
            }

            var expectedSha = await GetSha256Async(remotePath, ct);
            if (expectedSha != null && !string.Equals(localSha, expectedSha, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("svc.hashMismatch");
            }

            // Promover el archivo parcial a final (overwrite:true para evitar TOCTOU entre Delete+Move)
            File.Move(partPath, localPath, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(partPath)) File.Delete(partPath); } catch { }
            throw;
        }
    }

    public async Task<bool> UploadDeltaAsync(
        string localPath, string remotePath,
        IProgress<(long done, long total)>? progress = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(localPath)) throw new FileNotFoundException("svc.fileNotFound", localPath);
        var size = new FileInfo(localPath).Length;
        
        // 128 KB es un tamaño óptimo para delta sync en redes LAN
        int blockSize = 128 * 1024;
        
        // Intentar obtener hashes remotos del archivo destino
        List<string> remoteHashes;
        try
        {
            remoteHashes = await GetDeltaHashesAsync(remotePath, blockSize, ct);
        }
        catch
        {
            // Si falla u ocurre error (no existe o no lo soporta), hacemos upload normal
            return false;
        }

        if (remoteHashes.Count == 0) return false; // si no existe el remoto, subir normal

        // M6: ComputeLocalBlockHashesAsync — extracción del bloque duplicado en UploadDelta/DownloadDelta
        var localHashes = await ComputeLocalBlockHashesAsync(localPath, blockSize, ct);

        // Comparar bloques
        var modifiedBlocks = new List<int>();
        for (int i = 0; i < localHashes.Count; i++)
        {
            if (i >= remoteHashes.Count || !string.Equals(localHashes[i], remoteHashes[i], StringComparison.OrdinalIgnoreCase))
            {
                modifiedBlocks.Add(i);
            }
        }

        // Si todos los bloques coinciden y los tamaños coinciden, no es necesario transferir nada
        if (modifiedBlocks.Count == 0 && localHashes.Count == remoteHashes.Count)
        {
            progress?.Report((size, size));
            return true;
        }

        // Si son demasiados bloques modificados (ej: más del 70%), preferimos upload normal
        if (modifiedBlocks.Count > localHashes.Count * 0.7)
        {
            return false;
        }

        // Subir únicamente los bloques modificados
        long deltaDone = 0;
        var p = new Progress<long>(b =>
        {
            deltaDone += b;
            progress?.Report((deltaDone, size)); // reportar progreso respecto al archivo completo
        });

        await UploadDeltaBlocksAsync(localPath, remotePath, blockSize, modifiedBlocks, size, p, ct);
        return true;
    }

    public async Task<bool> DownloadDeltaAsync(
        string remotePath, string localPath,
        IProgress<(long done, long total)>? progress = null,
        CancellationToken ct = default)
    {
        // Obtener stat remoto para saber el tamaño real
        var stat = await GetStatAsync(remotePath, ct);
        if (stat == null || !stat.Exists) return false;
        
        var size = stat.Size;
        int blockSize = 128 * 1024;
        
        // Si el archivo local no existe, no podemos hacer delta sync
        if (!File.Exists(localPath)) return false;

        // Intentar obtener hashes remotos
        List<string> remoteHashes;
        try
        {
            remoteHashes = await GetDeltaHashesAsync(remotePath, blockSize, ct);
        }
        catch
        {
            return false;
        }

        if (remoteHashes.Count == 0) return false;

        // M6: ComputeLocalBlockHashesAsync reutilizado — antes había código duplicado idéntico
        var localHashes = await ComputeLocalBlockHashesAsync(localPath, blockSize, ct);

        var missingBlocks = new List<int>();
        for (int i = 0; i < remoteHashes.Count; i++)
        {
            if (i >= localHashes.Count || !string.Equals(remoteHashes[i], localHashes[i], StringComparison.OrdinalIgnoreCase))
            {
                missingBlocks.Add(i);
            }
        }

        if (missingBlocks.Count == 0 && remoteHashes.Count == localHashes.Count)
        {
            progress?.Report((size, size));
            return true;
        }

        if (missingBlocks.Count > remoteHashes.Count * 0.7)
        {
            return false;
        }

        // Crear una copia temporal del archivo local para actualizarla con los bloques correctos
        var partPath = localPath + ".part";
        File.Copy(localPath, partPath, overwrite: true);
        
        // Truncar/ajustar al tamaño objetivo por si acaso
        await using (var fs = new FileStream(partPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
        {
            fs.SetLength(size);
        }

        try
        {
            long deltaDone = 0;
            var p = new Progress<long>(b =>
            {
                deltaDone += b;
                progress?.Report((deltaDone, size));
            });

            // Descargar cada bloque faltante secuencialmente en el archivo parcial
            foreach (var idx in missingBlocks)
            {
                long offset = (long)idx * blockSize;
                long length = Math.Min(blockSize, size - offset);
                if (length <= 0) continue;

                await DownloadChunkAsync(remotePath, partPath, offset, length, p, ct);
            }

            // Verificar integridad
            string localSha;
            await using (var finalFs = new FileStream(partPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                localSha = Convert.ToHexString(await SHA256.HashDataAsync(finalFs, ct)).ToLowerInvariant();
            }

            var expectedSha = await GetSha256Async(remotePath, ct);
            if (expectedSha != null && !string.Equals(localSha, expectedSha, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("svc.hashMismatch");
            }

            // Promover el archivo parcial a final (overwrite:true para evitar TOCTOU entre Delete+Move)
            File.Move(partPath, localPath, overwrite: true);
            return true;
        }
        catch
        {
            try { if (File.Exists(partPath)) File.Delete(partPath); } catch { }
            throw;
        }
    }

    public async Task SendPowerAsync(string action, CancellationToken ct = default)
    {
        using var idleCts = StartIdleTimeout(ct);
        var ioCt = idleCts.Token;

        var (tcp, stream) = await OpenAsync(ioCt);
        using var _ = tcp;

        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "power", action }), ioCt);

        var headerLine = await Protocol.ReadLineAsync(stream, ioCt);
        var header = JsonSerializer.Deserialize<JsonElement>(headerLine);
        EnsureOk(header);
    }

    public async Task<List<FileEntry>> SearchRemoteAsync(string remotePath, string query, CancellationToken ct = default)
    {
        using var idleCts = StartIdleTimeout(ct);
        var ioCt = idleCts.Token;

        var (tcp, stream) = await OpenAsync(ioCt);
        using var _ = tcp;

        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "search", path = remotePath, query }), ioCt);

        var headerLine = await Protocol.ReadLineAsync(stream, ioCt);
        var header = JsonSerializer.Deserialize<JsonElement>(headerLine);
        EnsureOk(header);

        // P3/N9: pre-asignar capacidad — servidor limita resultados a 250, GetArrayLength() da el exacto
        var results = new List<FileEntry>(
            header.TryGetProperty("results", out var resEl) && resEl.ValueKind == JsonValueKind.Array
                ? Math.Min(resEl.GetArrayLength(), 250)
                : 0);
        if (resEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in resEl.EnumerateArray())
            {
                // O2: el.Deserialize<FileEntry>() en lugar de el.GetRawText() — sin string intermedia
                var entry = el.Deserialize<FileEntry>();
                if (entry != null) results.Add(entry);
            }
        }
        return results;
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
