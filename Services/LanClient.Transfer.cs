using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using LanCopy.Models;

namespace LanCopy.Services;

public sealed partial class LanClient
{
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
}
