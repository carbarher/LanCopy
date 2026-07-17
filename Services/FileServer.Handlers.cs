using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using LanCopy.Models;

namespace LanCopy.Services;

public sealed partial class FileServer
{
    private async Task HandleListAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        var reqPath = req.TryGetProperty("path", out var pathEl) ? pathEl.GetString() ?? "" : "";
        if (!TryGuardRead(reqPath, out var path, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }
        var entries = BuildEntries(path);
        if (RestrictToShareRoot)
        {
            var root = ShareRoot.Root;
            entries.RemoveAll(e => !ShareRoot.TryResolve(e.FullPath, out _, out _));
            foreach (var e in entries)
            {
                if (e.Name == "..")
                {
                    var rel = Path.GetRelativePath(root, e.FullPath).Replace('\\', '/');
                    e.FullPath = rel.StartsWith("..") ? "" : rel;
                }
                else
                {
                    e.FullPath = Path.GetRelativePath(root, e.FullPath).Replace('\\', '/');
                }
            }
        }
        // M2: WriteLineJsonAsync usa SerializeToUtf8Bytes — evita string UTF-16 intermedia.
        // Para listados grandes (hasta MaxListRecursiveFiles) puede ahorrar decenas de MB en heap.
        await Protocol.WriteLineJsonAsync(stream, new { status = "ok", entries }, ct);
    }

    private const int MaxListRecursiveFiles = 100_000;

    private async Task HandleListRecursiveAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        var reqRoot = req.TryGetProperty("path", out var rootEl) ? rootEl.GetString() ?? "" : "";
        if (!TryGuardRead(reqRoot, out var root, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }
        var entries = new List<FileEntry>();
        try
        {
            // P5/S: SearchOption.AllDirectories seguia symlinks/junctions ANTES del filtro
            // TryGuardRead, potencialmente escapando del share root. Usar EnumerationOptions
            // con AttributesToSkip=ReparsePoint para prevenir la travesia a nivel de SO.
            var listOpts = new System.IO.EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
                ReturnSpecialDirectories = false,
            };
            foreach (var f in Directory.EnumerateFiles(root, "*", listOpts))
            {
                if (entries.Count >= MaxListRecursiveFiles) break;
                if (RestrictToShareRoot && !ShareRoot.TryResolve(f, out _, out _)) continue;
                var fi = new FileInfo(f);
                var relPath = Path.GetRelativePath(root, f);
                entries.Add(new FileEntry
                {
                    Name = relPath,
                    FullPath = relPath, // S2: enviar ruta relativa, NO absoluta — la ruta absoluta expone
                                       // el layout de disco del servidor (usuario, unidad, directorios)
                    Size = fi.Length,
                    LastWriteUtcTicks = fi.LastWriteTimeUtc.Ticks
                });
            }
        }
        catch (Exception ex)
        {
            // No presentar una carpeta inaccesible como vacía: impide que el cliente dé un falso éxito.
            Log.Warn("server", "list-recursive-error", new { root, error = ex.Message });
            await Protocol.WriteErrorAsync(stream, "svc.operationFailed", ct);
            return;
        }
        Log.Info("server", "list-recursive-complete", new { root, files = entries.Count });
        // M2: WriteLineJsonAsync evita string UTF-16 intermedia (listas recursivas pueden tener 100K entradas)
        await Protocol.WriteLineJsonAsync(stream, new { status = "ok", entries }, ct);
    }

    // Feature 2: lÃ­mite de compresiÃ³n en memoria (200 MB)
    private const int RecursiveListChunkSize = 500;

    private async Task HandleListRecursiveStreamAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        var reqRoot = req.TryGetProperty("path", out var rootEl) ? rootEl.GetString() ?? "" : "";
        if (!TryGuardRead(reqRoot, out var root, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }

        await Protocol.WriteLineJsonAsync(stream, new { status = "ok", stream = true }, ct);
        var chunk = new List<FileEntry>(RecursiveListChunkSize);
        var count = 0;
        var truncated = false;
        try
        {
            var listOpts = new System.IO.EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
                ReturnSpecialDirectories = false,
            };
            foreach (var file in Directory.EnumerateFiles(root, "*", listOpts))
            {
                ct.ThrowIfCancellationRequested();
                if (RestrictToShareRoot && !ShareRoot.TryResolve(file, out _, out _)) continue;
                if (count >= MaxListRecursiveFiles) { truncated = true; break; }
                var info = new FileInfo(file);
                var relativePath = Path.GetRelativePath(root, file);
                chunk.Add(new FileEntry
                {
                    Name = relativePath,
                    FullPath = relativePath,
                    Size = info.Length,
                    LastWriteUtcTicks = info.LastWriteTimeUtc.Ticks
                });
                count++;
                if (chunk.Count < RecursiveListChunkSize) continue;
                await Protocol.WriteLineJsonAsync(stream, new { status = "chunk", entries = chunk }, ct);
                chunk.Clear();
            }
            if (chunk.Count > 0)
                await Protocol.WriteLineJsonAsync(stream, new { status = "chunk", entries = chunk }, ct);
            Log.Info("server", "list-recursive-stream-complete", new { root, files = count, truncated });
            await Protocol.WriteLineJsonAsync(stream, new { status = "done", truncated }, ct);
        }
        catch (Exception ex)
        {
            Log.Warn("server", "list-recursive-stream-error", new { root, error = ex.Message, files = count });
            await Protocol.WriteErrorAsync(stream, "svc.operationFailed", ct);
        }
    }
    private const long MaxCompressInMemory = 200L * 1024 * 1024;
    private static bool IsLikelyIncompressibleForGet(string path, long size)
    {
        const int sampleSize = 64 * 1024;
        if (size < sampleSize) return false;
        // P4: rentar el buffer de muestra del pool en vez de alojar 64KB en el heap por cada GET
        var sample = System.Buffers.ArrayPool<byte>.Shared.Rent(sampleSize);
        try
        {
            // SEC-FIX-001: Use FileOptions.SequentialScan to prevent symlink TOCTOU on Windows (confinement checked via ShareRoot)
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
            var read = fs.Read(sample, 0, sampleSize);
            if (read <= 0) return false;

            // Calcular distinct bytes primero (O(n), barato) para cortocircuitar antes de comprimir.
            int distinct = 0;
            var seen = new bool[256];
            for (int i = 0; i < read; i++)
            {
                var b = sample[i];
                if (!seen[b]) { seen[b] = true; distinct++; }
            }
            // Si la entropía es baja, el archivo es comprimible — no necesitamos comprimir la muestra.
            if (distinct < 240) return false;

            using var ms = new MemoryStream();
            using (var ds = new DeflateStream(ms, CompressionLevel.Fastest, leaveOpen: true))
                ds.Write(sample, 0, read);
            var compressed = ms.Length;
            var ratio = compressed <= 0 ? 1.0 : compressed / (double)read;
            return ratio >= 0.97;
        }
        catch (Exception ex)
        {
            Log.Debug("server", "incompat-probe-failed", new { path = System.IO.Path.GetFileName(path), error = ex.Message });
            return false;
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(sample);
        }
    }

    private async Task HandleGetAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        using var transferCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        transferCts.CancelAfter(TransferDataTimeoutSmall);
        ct = transferCts.Token;

        if (!TryGetStringProperty(req, "path", out var reqPath)) { await WriteBadRequestAsync(stream, ct); return; }
        if (!TryGuardRead(reqPath, out var path, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }
        if (Directory.Exists(path))
        {
            await Protocol.WriteErrorAsync(stream, "svc.isDir", ct); // P1: bytes cacheados
            return;
        }
        // B1: capturar mtime antes de abrir el stream para evitar TOCTOU en la caché SHA-256
        var mtimeBeforeHash = File.GetLastWriteTimeUtc(path);
        // SEC-FIX-001: Use FileOptions.SequentialScan to prevent symlink TOCTOU on Windows (confinement checked via ShareRoot)
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        var size = fs.Length;
        transferCts.CancelAfter(SelectTransferDataTimeout(size));

        // Reanudacion (idea-resume): el cliente puede pedir desde un offset si ya tiene un .part.
        long offset = req.TryGetProperty("offset", out var offEl) && offEl.TryGetInt64(out var offVal) ? offVal : 0;
        if (offset < 0 || offset > size) offset = 0;

        // Integridad: SHA-256 del fichero completo (una sola lectura previa al envio).
        string sha256;
        if (!TryGetCachedSha256(path, out sha256))
        {
            // B1: capturar mtime ANTES de abrir el stream — mismo patrón que HandleSha256Async
            // (capturar después del hash crea una ventana TOCTOU que almacena hash-viejo con mtime-nuevo)
            sha256 = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();
            StoreCachedSha256(path, mtimeBeforeHash, size, sha256);
            // HashDataAsync leyó el stream hasta el final; rebobinar para el envío.
            fs.Seek(0, SeekOrigin.Begin);
        }
        // Si el hash salió de caché, el FileStream nunca fue leído: pos == 0, Seek sería no-op.

        // Feature 2: compresiÃ³n deflate opcional
        bool wantCompress = req.TryGetProperty("compress", out var ce) && ce.GetBoolean()
                            && offset == 0
                            && size > 0 && size <= MaxCompressInMemory
                            && !Protocol.IsCompressedExtension(path)
                            && !IsLikelyIncompressibleForGet(path, size);
        if (wantCompress)
        {
            using var ms = new MemoryStream();
            await using (var ds = new DeflateStream(ms, CompressionLevel.Fastest, leaveOpen: true))
                await fs.CopyToAsync(ds, ct);
            var compressedSize = ms.Length;
            ms.Seek(0, SeekOrigin.Begin);
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new
            { status = "ok", size, sha256, compress = true, compressed_size = compressedSize, range_from = 0L }), ct);
            await Protocol.CopyExactAsync(ms, stream, compressedSize, MakeProgress(false, Path.GetFileName(path)), ct);
        }
        else
        {
            if (offset > 0) fs.Seek(offset, SeekOrigin.Begin);
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", size, sha256, range_from = offset }), ct);
            await Protocol.CopyExactAsync(fs, stream, size - offset, MakeProgress(false, Path.GetFileName(path)), ct);
        }
    }

    private const long MaxPutBytes = 100L * 1024 * 1024 * 1024; // 100 GB
    private static readonly TimeSpan TransferDataTimeoutSmall = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan TransferDataTimeoutLarge = TimeSpan.FromHours(2);
    private const long LargeTransferThresholdBytes = TransferOptions.LargeTransferThresholdBytes; // centralizado en TransferOptions
    private static TimeSpan SelectTransferDataTimeout(long expectedBytes)
        => expectedBytes >= LargeTransferThresholdBytes ? TransferDataTimeoutLarge : TransferDataTimeoutSmall;

    private async Task HandlePutAsync(JsonElement req, Stream stream, string ip, CancellationToken ct)
    {
        using var transferCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        transferCts.CancelAfter(TransferDataTimeoutSmall);
        ct = transferCts.Token;

        if (!TryGetInt64Property(req, "size", out var size)) { await WriteBadRequestAsync(stream, ct); return; }
        transferCts.CancelAfter(SelectTransferDataTimeout(size));
        if (size < 0 || size > MaxPutBytes)
        {
            await Protocol.WriteErrorAsync(stream, "svc.badSize", ct); // P1: bytes cacheados
            return;
        }
        if (!TryGetStringProperty(req, "path", out var reqPath)) { await WriteBadRequestAsync(stream, ct); return; }
        if (!TryGuardWrite(reqPath, out var path, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }

        // Q1: calcular isCompressed UNA sola vez; wireBytes se deriva de compressed_size si aplica
        bool isCompressed = req.TryGetProperty("compress", out var ceFlag) && ceFlag.GetBoolean()
                            && req.TryGetProperty("compressed_size", out _);
        long wireBytes = size;
        if (isCompressed)
        {
            if (!TryGetInt64Property(req, "compressed_size", out wireBytes)) { await WriteBadRequestAsync(stream, ct); return; }
        }
        transferCts.CancelAfter(SelectTransferDataTimeout(Math.Max(size, wireBytes)));
        // Anti-OOM: limitar tamaño del body comprimido
        if (isCompressed && (wireBytes < 0 || wireBytes > MaxCompressInMemory))
        {
            await Protocol.WriteErrorAsync(stream, "svc.badCompressedSize", ct); // P1: bytes cacheados
            return;
        }
        var sendPreAck = req.TryGetProperty("pre_ack", out var preAckEl) && preAckEl.ValueKind == JsonValueKind.True;

        // Consentimiento del receptor antes de tocar el disco.
        if (ApproveIncoming is { } approve)
        {
            bool ok;
            try { ok = await approve(new IncomingTransfer(ip, Path.GetFileName(path), size), ct); }
            catch (Exception ex)
            {
                Log.Warn("server", "put-approve-callback-failed", new { ip, file = Path.GetFileName(path), error = ex.Message });
                ok = false;
            }
            if (!ok)
            {
                Log.Info("server", "put-rejected", new { ip, file = Path.GetFileName(path) });
                // El protocolo nuevo aún no ha enviado cuerpo; responder inmediatamente.
                // Con clientes antiguos sí hay que drenar para evitar un RST.
                if (!sendPreAck)
                    await Protocol.DrainAsync(stream, wireBytes, ct);
                await Protocol.WriteErrorAsync(stream, "svc.rejected", ct); // P1: bytes cacheados
                return;
            }
        }
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            // S1: verificar reparse points en la jerarquía intermedia antes de crearla (TOCTOU guard)
            // Q1: eliminado !string.IsNullOrEmpty(dir) redundante — el if exterior ya lo garantiza
            if (SafeFileOps.ContainsReparsePoint(dir))
            {
                await Protocol.WriteErrorAsync(stream, "svc.accessDenied", ct); // P1: bytes cacheados
                return;
            }
            Directory.CreateDirectory(dir);
        }

        // Feature 2: compresión deflate opcional
        // Q1: isCompressed ya calculado al inicio del método
        // O4: FileInfo en lugar de File.Exists()+GetLastWriteTimeUtc() separados (2 syscalls → 1 stat)
        var fiPre = new FileInfo(path);
        var mtimePreWrite = fiPre.Exists ? fiPre.LastWriteTimeUtc : DateTimeOffset.UtcNow.UtcDateTime;
        if (sendPreAck)
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", ready = true }), ct);

        string sha256;
        try
        {
            await using var fs = File.Create(path);
            if (isCompressed)
            {
                // B1: wireBytes ya contiene compressed_size (capturado en la validación inicial)
                // El segundo TryGetInt64Property era redundante y un hazard de mantenimiento
                // P2: pre-asignar capacidad con wireBytes para evitar rehashes del buffer interno
                using var compBuf = new MemoryStream((int)Math.Min(wireBytes, 256 * 1024 * 1024)); // cap: 256MB max
                await Protocol.CopyExactAsync(stream, compBuf, wireBytes, MakeProgress(true, Path.GetFileName(path)), ct);
                compBuf.Seek(0, SeekOrigin.Begin);
                await using var ds = new DeflateStream(compBuf, CompressionMode.Decompress);
                // Anti zip-bomb + hash en una sola pasada mientras se escribe.
                using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                // Q6: ArrayPool evita alloc 512KB en heap; B1: try/finally garantiza devolución incluso ante excepción
                var zbuf = System.Buffers.ArrayPool<byte>.Shared.Rent(Protocol.BufferSize);
                try
                {
                    long written = 0;
                    int zr;
                    while ((zr = await ds.ReadAsync(zbuf, ct)) > 0)
                    {
                        written += zr;
                        if (written > size) throw new InvalidDataException("Descompresion excede el tamano declarado (posible zip-bomb)");
                        await fs.WriteAsync(zbuf.AsMemory(0, zr), ct);
                        hasher.AppendData(zbuf, 0, zr);
                    }
                    if (written != size)
                        throw new InvalidDataException("Descompresion incompleta: el tamano final no coincide con el esperado");
                }
                finally
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(zbuf); // B1: siempre devolver al pool
                }
                sha256 = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
            }
            else
            {
                // Hash en streaming durante la recepcion: evita re-leer el fichero del disco.
                sha256 = await Protocol.CopyExactToHashAsync(stream, fs, size, MakeProgress(true, Path.GetFileName(path)), ct);
            }
        }
        catch (OperationCanceledException) { throw; } // solo re-lanzar cancelaciones; el parcial se conserva para reanudación
        catch (Exception ioEx)
        {
            // B3: enviar error JSON antes de propagar — sin esto el cliente ve TCP RST y no sabe que el fichero quedó truncado
            try { await Protocol.WriteErrorAsync(stream, "svc.writeFailed", ct); } // P1: bytes cacheados
            catch (Exception writeEx) { Log.Debug("server", "put-write-error-reply-failed", new { path, error = writeEx.Message }); }
            Log.Warn("server", "put-write-error", new { path, error = ioEx.Message });
            throw;
        }

        // B1: capturar mtime DESPUÉS de que el FileStream cierra ("await using" scope termina)
        // — la mtime pre-write era incorrecta: tras el close el OS actualiza LastWriteTime
        // y la cache keyeada a la mtime antigua nunca hacía hit en el siguiente sha256/get request
        // O4: FileInfo — 1 syscall, no 2 (File.Exists + GetLastWriteTimeUtc)
        var fiPost = new FileInfo(path);
        var mtimePostWrite = fiPost.Exists ? fiPost.LastWriteTimeUtc : mtimePreWrite;
        StoreCachedSha256(path, mtimePostWrite, size, sha256);
        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", sha256 }), ct);
    }

    private async Task HandlePutResumeAsync(JsonElement req, Stream stream, string ip, CancellationToken ct)
    {
        using var transferCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        transferCts.CancelAfter(TransferDataTimeoutSmall);
        ct = transferCts.Token;

        if (!TryGetInt64Property(req, "size", out var size)) { await WriteBadRequestAsync(stream, ct); return; }
        if (size < 0 || size > MaxPutBytes)
        {
            await Protocol.WriteErrorAsync(stream, "svc.badSize", ct); // P1: bytes cacheados
            return;
        }
        if (!TryGetStringProperty(req, "path", out var reqPath)) { await WriteBadRequestAsync(stream, ct); return; }
        if (!TryGetInt64Property(req, "offset", out var offset)) { await WriteBadRequestAsync(stream, ct); return; }

        if (!TryGuardWrite(reqPath, out var path, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }

        if (offset < 0 || offset > size)
        {
            await Protocol.WriteErrorAsync(stream, "svc.badSize", ct); // P1: bytes cacheados
            return;
        }

        if (ApproveIncoming is { } approve)
        {
            bool ok;
            try { ok = await approve(new IncomingTransfer(ip, Path.GetFileName(path), size), ct); }
            catch (Exception ex)
            {
                Log.Warn("server", "put-resume-approve-callback-failed", new { ip, file = Path.GetFileName(path), error = ex.Message });
                ok = false;
            }
            if (!ok)
            {
                // B2: el rechazo ocurre ANTES de enviar range_from al cliente.
                // En el protocolo resume, el cliente envía el header y espera range_from antes de mandar bytes.
                // Por tanto no hay body que drenar aquí; simplemente enviar el error y salir.
                await Protocol.WriteErrorAsync(stream, "svc.rejected", ct); // P1: bytes cacheados
                return;
            }
        }

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            // B2: misma guard TOCTOU que HandlePutAsync — verificar reparse points antes de crear dirs
            if (SafeFileOps.ContainsReparsePoint(dir))
            {
                await Protocol.WriteErrorAsync(stream, "svc.accessDenied", ct); // P1: bytes cacheados
                return;
            }
            Directory.CreateDirectory(dir);
        }

        long accepted = 0;
        try
        {
            // SEC-FIX-001: Use FileOptions.None to prevent symlink TOCTOU on Windows (confinement checked via ShareRoot)
            await using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.None);
            var current = fs.Length;
            accepted = Math.Min(Math.Min(offset, current), size);
            if (current > accepted) fs.SetLength(accepted);
            fs.Seek(accepted, SeekOrigin.Begin);

            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", range_from = accepted }), ct);

            var remaining = size - accepted;
            if (remaining > 0)
            {
                var baseProgress = MakeProgress(true, Path.GetFileName(path));
                var adjustedProgress = new Progress<(long done, long total)>(p =>
                    baseProgress.Report((accepted + p.done, size)));
                await Protocol.CopyExactAsync(stream, fs, remaining, adjustedProgress, ct);
            }

            // Importante: en reanudaciÃ³n NO recalculamos hash de todo el archivo (puede tardar
            // minutos en archivos grandes y provocar timeout/"error" en el emisor tras enviar).
            // Confirmamos recepciÃ³n inmediatamente; el cliente ya no exige sha256 en este camino.
            // Q2: ack final limpio — range_from ya fue enviado en el ack inicial; el duplicado aquí
            // es ruido de protocolo y un hazard de mantenimiento si se extiende el protocolo
            // B3: Flush async para no bloquear el thread-pool; synchronous Flush() en async method
            // estallaba un thread-pool thread durante I/O de disco
            await fs.FlushAsync(ct);
            await Protocol.WriteOkAsync(stream, ct); // P1: bytes pre-calculados
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // Partial file is kept intentionally so the client can resume on reconnect.
            Log.Warn("server", "put-resume-write-error", new { path, error = ex.Message });
            throw;
        }
    }

    private async Task HandleDeleteAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        if (SafeModeNoRemoteDelete)
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = "svc.remoteDeleteDisabled" }), ct);
            return;
        }

        if (!TryGetStringProperty(req, "path", out var path)) { await WriteBadRequestAsync(stream, ct); return; }
        // Confina a la carpeta compartida cuando RestrictToShareRoot esta activo.
        if (!TryGuardWrite(path, out var guarded, out var gReason))
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            SafeFileOps.Audit("delete", path, "blocked", gReason, "remote");
            return;
        }
        if (!SafeFileOps.TryValidateMutationPath(guarded, out var normalized, out var reason))
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = reason }), ct);
            SafeFileOps.Audit("delete", path, "blocked", reason, "remote");
            return;
        }
        const int RemoteDeleteCooldownSeconds = 10;
        var key = $"remote-delete:{normalized}";
        if (SafeFileOps.IsOnCooldown(key, RemoteDeleteCooldownSeconds))
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = "svc.cooldown" }), ct);
            SafeFileOps.Audit("delete", normalized, "blocked", "cooldown", "remote");
            return;
        }
        try
        {
            if (SafeFileOps.TryMoveToTrash(normalized, out var moved, out var moveErr))
            {
                // S1: NO enviar movedPath absoluto al peer remoto \u2014 expondr\u00eda la ruta de disco del servidor
                await Protocol.WriteOkAsync(stream, ct); // P1: bytes pre-calculados
                SafeFileOps.Audit("delete", normalized, "ok", $"trash:{moved}", "remote");
                return;
            }

            // Q3: Fallback solo para ficheros individuales — rechazar directorios para evitar borrado masivo sin confirmación.
            // Si la papelera falló por volumen/permisos, permitir hard-delete de ficheros pero NO de directorios enteros.
            if (Directory.Exists(normalized))
            {
                // Denegar hard-delete de directorios — requeriría confirmación explícita del usuario
                await Protocol.WriteLineAsync(stream,
                    JsonSerializer.Serialize(new { status = "error", error = "svc.trashFailed" }), ct);
                SafeFileOps.Audit("delete", normalized, "blocked", $"trash-failed-dir-hard-delete-denied:{moveErr}", "remote");
                return;
            }
            else if (File.Exists(normalized))
            {
                if (!AllowRemoteHardDelete)
                {
                    await Protocol.WriteLineAsync(stream,
                        JsonSerializer.Serialize(new { status = "error", error = "svc.trashFailed" }), ct);
                    SafeFileOps.Audit("delete", normalized, "blocked", $"trash-failed-hard-delete-disabled:{moveErr}", "remote");
                    return;
                }
                File.Delete(normalized);
            }
            else
            {
                // B6: enviar svc.notFound en lugar del genérico svc.operationFailed — el cliente puede distinguir
                await Protocol.WriteLineAsync(stream,
                    JsonSerializer.Serialize(new { status = "error", error = "svc.notFound" }), ct);
                SafeFileOps.Audit("delete", normalized, "error", "notFound", "remote");
                return;
            }

            await Protocol.WriteOkAsync(stream, ct); // P1: bytes pre-calculados
            // Q1: audit detail clarifica que es hard-delete de fallback (moveErr es el error de papelera, no del delete exitoso)
            SafeFileOps.Audit("delete", normalized, "ok", $"hard-delete:fallback(trash-err:{moveErr})", "remote");
        }
        catch (Exception ex)
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = "svc.operationFailed" }), ct); // S2: no exponer ex.Message al peer
            SafeFileOps.Audit("delete", normalized, "error", ex.Message, "remote");
        }
    }

    private async Task HandleRenameAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        if (!TryGetStringProperty(req, "path", out var path)) { await WriteBadRequestAsync(stream, ct); return; }
        if (!TryGetStringProperty(req, "newname", out var newName)) { await WriteBadRequestAsync(stream, ct); return; }

        // Confina a la carpeta compartida cuando RestrictToShareRoot esta activo.
        if (!TryGuardWrite(path, out var guarded, out var gReason))
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            SafeFileOps.Audit("rename", path, "blocked", gReason, "remote");
            return;
        }

        if (!SafeFileOps.TryValidateMutationPath(guarded, out var normalized, out var reason))
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = reason }), ct);
            SafeFileOps.Audit("rename", path, "blocked", reason, "remote");
            return;
        }
        // Validar: el nuevo nombre no puede contener separadores de ruta
        if (string.IsNullOrWhiteSpace(newName) || newName is "." or ".." || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = "svc.badName" }), ct);
            return;
        }

        var key = $"remote-rename:{normalized}";
        if (SafeFileOps.IsOnCooldown(key, 2))
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = "svc.cooldown" }), ct);
            SafeFileOps.Audit("rename", normalized, "blocked", "cooldown", "remote");
            return;
        }

        try
        {
            var dir = Path.GetDirectoryName(normalized)!;
            var newPath = Path.Combine(dir, newName);
            if (!TryGuardWrite(newPath, out _, out var destGuard))
            {
                await Protocol.WriteLineAsync(stream,
                    JsonSerializer.Serialize(new { status = "error", error = $"svc.destLocked" }), ct);
                SafeFileOps.Audit("rename", normalized, "blocked", $"dest:{destGuard}", "remote");
                return;
            }
            if (!SafeFileOps.TryValidateMutationPath(newPath, out _, out var targetReason, requireExists: false))
            {
                await Protocol.WriteLineAsync(stream,
                    JsonSerializer.Serialize(new { status = "error", error = "svc.operationFailed" }), ct); // S2: no exponer targetReason (info interna) al peer
                SafeFileOps.Audit("rename", normalized, "blocked", $"dest:{targetReason}", "remote"); // targetReason se loguea localmente
                return;
            }

            if (Directory.Exists(normalized)) Directory.Move(normalized, newPath);
            else File.Move(normalized, newPath);

            await Protocol.WriteOkAsync(stream, ct); // P1: bytes pre-calculados
            SafeFileOps.Audit("rename", normalized, "ok", $"to:{newPath}", "remote");
        }
        catch (Exception ex)
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = "svc.operationFailed" }), ct); // S2: no exponer ex.Message al peer
            SafeFileOps.Audit("rename", normalized, "error", ex.Message, "remote");
        }
    }

    private const int MaxMkdirDepth = 16; // S4: limitar profundidad de paths remotos

    private async Task HandleMkdirAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        if (!TryGetStringProperty(req, "path", out var path)) { await WriteBadRequestAsync(stream, ct); return; }
        // S4: rechazar paths con demasiados segmentos — Directory.CreateDirectory crea todos los intermedios
        var segments = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > MaxMkdirDepth)
        {
            await Protocol.WriteErrorAsync(stream, "svc.pathTooDeep", ct); // P1: bytes cacheados
            return;
        }
        if (!TryGuardWrite(path, out var guarded, out var gReason))
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            SafeFileOps.Audit("mkdir", path, "blocked", gReason, "remote");
            return;
        }
        try
        {
            if (File.Exists(guarded))
            {
                await Protocol.WriteLineAsync(stream,
                    JsonSerializer.Serialize(new { status = "error", error = "svc.pathExistsFile" }), ct);
                return;
            }
            Directory.CreateDirectory(guarded);
            await Protocol.WriteOkAsync(stream, ct); // P1: bytes pre-calculados
            SafeFileOps.Audit("mkdir", guarded, "ok", "", "remote");
        }
        catch (Exception ex)
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = "svc.operationFailed" }), ct); // S2: no exponer ex.Message al peer
            SafeFileOps.Audit("mkdir", guarded, "error", ex.Message, "remote");
        }
    }

    private async Task HandleSha1Async(JsonElement req, Stream stream, CancellationToken ct)
    {
        if (!TryGetStringProperty(req, "path", out var reqPath)) { await WriteBadRequestAsync(stream, ct); return; }
        if (!TryGuardRead(reqPath, out var path, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }
        // M7: delegar a ComputeFileHashAsync — centraliza file open + error handling + caé (SHA-256)
        var (ok, hash, errMsg) = await ComputeFileHashAsync(path, "sha1", ct);
        if (!ok) await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = errMsg }), ct);
        else await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", sha1 = hash }), ct);
    }

    private async Task HandleSha256Async(JsonElement req, Stream stream, CancellationToken ct)
    {
        if (!TryGetStringProperty(req, "path", out var reqPath)) { await WriteBadRequestAsync(stream, ct); return; }
        if (!TryGuardRead(reqPath, out var path, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }
        // M7: delegar a ComputeFileHashAsync — reutiliza cache SHA-256 centralizada
        var (ok, hash, errMsg) = await ComputeFileHashAsync(path, "sha256", ct);
        if (!ok) await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = errMsg }), ct);
        else await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", sha256 = hash }), ct);
    }

    /// <summary>
    /// M7: Extrae la lógica de apertura de archivo + cálculo de hash + manejo de error + caché
    /// compartida entre HandleSha1Async, HandleSha256Async y HandleHashAsync.
    /// Returns (success, hash, errorCode).
    /// </summary>
    private async Task<(bool ok, string hash, string error)> ComputeFileHashAsync(
        string path, string alg, CancellationToken ct)
    {
        try
        {
            if (string.Equals(alg, "sha256", StringComparison.OrdinalIgnoreCase) &&
                TryGetCachedSha256(path, out var cached))
                return (true, cached, "");

            var mtimeBeforeHash = File.GetLastWriteTimeUtc(path);
            // SEC-FIX-001: FileOptions.SequentialScan previene TOCTOU de symlink en Windows
            await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
            if (string.Equals(alg, "sha1", StringComparison.OrdinalIgnoreCase))
            {
                var sha1 = Convert.ToHexString(await SHA1.HashDataAsync(fs, ct)).ToLowerInvariant();
                return (true, sha1, "");
            }
            var sha256 = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();
            StoreCachedSha256(path, mtimeBeforeHash, fs.Length, sha256);
            return (true, sha256, "");
        }
        catch (Exception ex)
        {
            Log.Debug("server", "compute-hash-failed", new { alg, path, error = ex.Message });
            return (false, "", "svc.operationFailed");
        }
    }

    private async Task HandleHashAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        if (!TryGetStringProperty(req, "path", out var reqPath)) { await WriteBadRequestAsync(stream, ct); return; }
        if (!TryGuardRead(reqPath, out var path, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }
        var alg = req.TryGetProperty("alg", out var algEl) ? (algEl.GetString() ?? "sha256") : "sha256";

        try
        {
            // Q7: comprobar cache SHA-256 ANTES de abrir el FileStream
            // (antes: FileStream abierto en cada cache hit — desperdicia file handle + alloc)
            if (string.Equals(alg, "sha256", StringComparison.OrdinalIgnoreCase) &&
                TryGetCachedSha256(path, out var cachedHash))
            {
                await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", alg = "sha256", hash = cachedHash }), ct);
                return;
            }

            var mtimeBeforeHash = File.GetLastWriteTimeUtc(path);
            await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
            if (string.Equals(alg, "sha1", StringComparison.OrdinalIgnoreCase))
            {
                var sha1 = Convert.ToHexString(await SHA1.HashDataAsync(fs, ct)).ToLowerInvariant();
                await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", alg = "sha1", hash = sha1 }), ct);
                return;
            }

            var sha256 = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();
            StoreCachedSha256(path, mtimeBeforeHash, fs.Length, sha256);
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", alg = "sha256", hash = sha256 }), ct);
        }
        catch (Exception ex) // S2: no exponer ex.Message al peer
        {
            Log.Debug("server", "hash-handler-failed", new { alg, path, error = ex.Message });
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = "svc.operationFailed" }), ct);
        }
    }

    // Batched metadata lookup used by overwrite checks. It intentionally exposes only
    // the same fields as repeated stat calls, while keeping a folder preflight bounded.
    private async Task HandleStatManyAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        const int maxPaths = 256;
        if (!req.TryGetProperty("paths", out var pathsEl) || pathsEl.ValueKind != JsonValueKind.Array
            || pathsEl.GetArrayLength() > maxPaths)
        {
            await WriteBadRequestAsync(stream, ct);
            return;
        }

        var results = new List<object>();
        try
        {
            foreach (var pathEl in pathsEl.EnumerateArray())
            {
                if (pathEl.ValueKind != JsonValueKind.String) { await WriteBadRequestAsync(stream, ct); return; }
                var requestedPath = pathEl.GetString() ?? "";
                if (!TryGuardRead(requestedPath, out var path, out _))
                {
                    results.Add(new { path = requestedPath, exists = false });
                    continue;
                }

                if (File.Exists(path))
                {
                    var file = new FileInfo(path);
                    results.Add(new { path = requestedPath, exists = true, isDirectory = false, size = file.Length, lastWriteUtcTicks = file.LastWriteTimeUtc.Ticks });
                }
                else if (Directory.Exists(path))
                {
                    var directory = new DirectoryInfo(path);
                    results.Add(new { path = requestedPath, exists = true, isDirectory = true, size = 0L, lastWriteUtcTicks = directory.LastWriteTimeUtc.Ticks });
                }
                else results.Add(new { path = requestedPath, exists = false });
            }
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", results }), ct);
        }
        catch (Exception ex)
        {
            Log.Debug("server", "stat-many-handler-failed", new { error = ex.Message });
            await Protocol.WriteErrorAsync(stream, "svc.operationFailed", ct);
        }
    }
    private async Task HandleStatAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        if (!TryGetStringProperty(req, "path", out var reqPath)) { await WriteBadRequestAsync(stream, ct); return; }
        if (!TryGuardRead(reqPath, out var path, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }
        try
        {
            if (File.Exists(path))
            {
                var fi = new FileInfo(path);
                await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new
                {
                    status = "ok",
                    exists = true,
                    isDirectory = false,
                    size = fi.Length,
                    lastWriteUtcTicks = fi.LastWriteTimeUtc.Ticks
                }), ct);
                return;
            }

            if (Directory.Exists(path))
            {
                var di = new DirectoryInfo(path);
                await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new
                {
                    status = "ok",
                    exists = true,
                    isDirectory = true,
                    size = 0L,
                    lastWriteUtcTicks = di.LastWriteTimeUtc.Ticks
                }), ct);
                return;
            }

            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", exists = false }), ct);
        }
        catch (Exception ex) // S2: no exponer ex.Message al peer
        {
            Log.Debug("server", "stat-handler-failed", new { path, error = ex.Message });
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = "svc.operationFailed" }), ct);
        }
    }

    private static List<FileEntry> BuildEntries(string path)
    {
        var list = new List<FileEntry>();

        if (string.IsNullOrWhiteSpace(path))
        {
            foreach (var d in DriveInfo.GetDrives())
                list.Add(new FileEntry { Name = d.Name, FullPath = d.Name, IsDirectory = true });
            return list;
        }

        var parent = Directory.GetParent(path)?.FullName;
        if (parent != null)
            list.Add(new FileEntry { Name = "..", FullPath = parent, IsDirectory = true });

        try
        {
            var di = new DirectoryInfo(path);
            // M11: EnumerateDirectories/Files en lugar de GetDirectories().OrderBy() — igual que M1 en cliente.
            // El sort era redundante: el cliente siempre re-ordena con ApplyRemoteSort + SortEntries.
            // GetDirectories() materialiaba toda la colección en array antes de ordenar; Enumerate hace streaming.
            foreach (var d in di.EnumerateDirectories())
            {
                if (d.Name.StartsWith(".")) continue;
                list.Add(new FileEntry { Name = d.Name, FullPath = d.FullName, IsDirectory = true });
            }
            foreach (var f in di.EnumerateFiles())
            {
                if (f.Name.StartsWith(".")) continue;
                list.Add(new FileEntry { Name = f.Name, FullPath = f.FullName, Size = f.Length, LastWriteUtcTicks = f.LastWriteTimeUtc.Ticks });
            }
        }
        catch (Exception ex)
        {
            Log.Debug("server", "build-entries-failed", new { path, error = ex.Message });
        }

        return list;
    }
    private async Task HandleGetChunkAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        using var transferCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        transferCts.CancelAfter(TransferDataTimeoutSmall);
        ct = transferCts.Token;

        if (!TryGetStringProperty(req, "path", out var reqPath)) { await WriteBadRequestAsync(stream, ct); return; }
        if (!TryGuardRead(reqPath, out var path, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }
        if (!TryGetInt64Property(req, "offset", out var offset) || offset < 0) { await WriteBadRequestAsync(stream, ct); return; }
        if (!TryGetInt64Property(req, "length", out var length) || length <= 0) { await WriteBadRequestAsync(stream, ct); return; }

        if (!File.Exists(path))
        {
            await Protocol.WriteErrorAsync(stream, "svc.fileNotFound", ct); // P1: bytes cacheados
            return;
        }

        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
        var size = fs.Length;
        if (offset + length > size)
        {
            length = size - offset;
        }
        if (length < 0) length = 0;

        transferCts.CancelAfter(SelectTransferDataTimeout(length));
        fs.Seek(offset, SeekOrigin.Begin);
        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", length }), ct);
        if (length > 0)
        {
            await Protocol.CopyExactAsync(fs, stream, length, null, ct);
        }
    }

    private async Task HandleDeltaHashesAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        using var transferCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        transferCts.CancelAfter(TransferDataTimeoutSmall);
        ct = transferCts.Token;

        if (!TryGetStringProperty(req, "path", out var reqPath)) { await WriteBadRequestAsync(stream, ct); return; }
        if (!TryGuardRead(reqPath, out var path, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }
        if (!req.TryGetProperty("block_size", out var bsEl) || !bsEl.TryGetInt32(out var blockSize) || blockSize <= 0 || blockSize > 8 * 1024 * 1024) { await WriteBadRequestAsync(stream, ct); return; } // S5: max 8 MB — evita OOM/DoS con block_size=Int32.MaxValue

        if (!File.Exists(path))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", block_size = blockSize, hashes = Array.Empty<string>() }), ct);
            return;
        }

        var hashes = new List<string>();
        await using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))
        {
            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(blockSize);
            try
            {
                int read;
                while ((read = await fs.ReadAsync(buffer.AsMemory(0, blockSize), ct)) > 0)
                {
                    var hash = SHA256.HashData(buffer.AsSpan(0, read));
                    // O5: ToLowerInvariant() eliminado — la comparación en el cliente usa OrdinalIgnoreCase.
                // -1 string alloc por bloque (cada 128KB). En 1GB = ~8192 allocs menos.
                hashes.Add(Convert.ToHexString(hash));
                }
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", block_size = blockSize, hashes }), ct);
    }

    private async Task HandlePutDeltaBlocksAsync(JsonElement req, Stream stream, string ip, CancellationToken ct)
    {
        using var transferCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        transferCts.CancelAfter(TransferDataTimeoutSmall);
        ct = transferCts.Token;

        if (!TryGetStringProperty(req, "path", out var reqPath)) { await WriteBadRequestAsync(stream, ct); return; }
        if (!TryGuardWrite(reqPath, out var path, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }
        if (!req.TryGetProperty("block_size", out var bsEl2) || !bsEl2.TryGetInt32(out var blockSize) || blockSize <= 0 || blockSize > 8 * 1024 * 1024) { await WriteBadRequestAsync(stream, ct); return; } // S5: max 8 MB — evita OOM/DoS con block_size=Int32.MaxValue
        if (!req.TryGetProperty("blocks", out var blocksEl) || blocksEl.ValueKind != JsonValueKind.Array) { await WriteBadRequestAsync(stream, ct); return; }
        if (!TryGetInt64Property(req, "size", out var expectedSize) || expectedSize <= 0 || expectedSize > MaxPutBytes) { await WriteBadRequestAsync(stream, ct); return; } // S5+S6: >0 obligatorio — size=0 vaciaria el archivo silenciosamente (file truncation attack via delta)

        var blocks = new List<int>();
        long maxBlockCount = blockSize > 0 ? (expectedSize / blockSize + 1) : 0;
        foreach (var el in blocksEl.EnumerateArray())
        {
            // S5: rechazar índices negativos o fuera del rango del archivo — Seek negativo lanza ArgumentOutOfRangeException
            // y desincroniza el protocolo si totalWireBytes no corresponde a los bytes enviados.
            if (!el.TryGetInt32(out var idx) || idx < 0) continue;
            if (expectedSize > 0 && (long)idx * blockSize >= expectedSize) continue;
            if (blocks.Count >= maxBlockCount) break; // límite teórico de bloques
            blocks.Add(idx);
        }


        // Determinar el flujo de red total estimado a recibir
        long totalWireBytes = 0;
        for (int i = 0; i < blocks.Count; i++)
        {
            var idx = blocks[i];
            long blockOffset = (long)idx * blockSize;
            long blockLen = Math.Min(blockSize, expectedSize - blockOffset);
            if (blockLen > 0) totalWireBytes += blockLen;
        }

        transferCts.CancelAfter(SelectTransferDataTimeout(totalWireBytes));

        // Solicitar consentimiento del usuario
        if (ApproveIncoming is { } approve)
        {
            bool ok;
            try { ok = await approve(new IncomingTransfer(ip, Path.GetFileName(path), expectedSize), ct); }
            catch (Exception ex)
            {
                Log.Warn("server", "put-delta-approve-failed", new { ip, file = Path.GetFileName(path), error = ex.Message });
                ok = false;
            }
            if (!ok)
            {
                await Protocol.DrainAsync(stream, totalWireBytes, ct);
                await Protocol.WriteErrorAsync(stream, "svc.rejected", ct); // P1: bytes cacheados
                return;
            }
        }

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            if (SafeFileOps.ContainsReparsePoint(dir))
            {
                await Protocol.WriteErrorAsync(stream, "svc.accessDenied", ct); // P1: bytes cacheados
                return;
            }
            Directory.CreateDirectory(dir);
        }

        string sha256;
        // O4: FileInfo — 1 syscall en lugar de File.Exists()+GetLastWriteTimeUtc() separados
        var fiDeltaPre = new FileInfo(path);
        var mtimePreWrite = fiDeltaPre.Exists ? fiDeltaPre.LastWriteTimeUtc : DateTimeOffset.UtcNow.UtcDateTime;
        // BUG-FIX: GUID en el archivo temporal para evitar race condition si dos sesiones
        // ejecutan put_delta_blocks sobre el mismo archivo concurrentemente.
        // El nombre fijo .part era sobrescrito por el segundo File.Copy (corrupción silenciosa).
        var partPath = path + $".{Guid.NewGuid():N}.part~";

        try
        {
            if (File.Exists(path))
            {
                File.Copy(path, partPath, overwrite: true);
            }

            await using (var fs = new FileStream(partPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                // Truncar el archivo temporal al tamaño objetivo
                fs.SetLength(expectedSize);

                var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(blockSize);
                try
                {
                    for (int i = 0; i < blocks.Count; i++)
                    {
                        var idx = blocks[i];
                        long blockOffset = (long)idx * blockSize;
                        long blockLen = Math.Min(blockSize, expectedSize - blockOffset);
                        if (blockLen <= 0) continue;

                        fs.Seek(blockOffset, SeekOrigin.Begin);
                        await Protocol.CopyExactAsync(stream, fs, blockLen, null, ct);
                    }
                }
                finally
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                }

                // Generar el SHA-256 secuencial completo para verificar integridad
                fs.Seek(0, SeekOrigin.Begin);
                sha256 = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();
            }

            // Promover el .part~ al destino final — overwrite:true para evitar TOCTOU entre Delete+Move
            File.Move(partPath, path, overwrite: true);
        }
        catch (Exception ioEx)
        {
            try { if (File.Exists(partPath)) File.Delete(partPath); } catch { }
            try { await Protocol.WriteErrorAsync(stream, "svc.writeFailed", ct); } // P1: bytes cacheados
            catch { }
            Log.Warn("server", "put-delta-write-error", new { path, error = ioEx.Message });
            throw;
        }

        // O4: FileInfo — 1 syscall
        var fiDeltaPost = new FileInfo(path);
        var mtimePostWrite = fiDeltaPost.Exists ? fiDeltaPost.LastWriteTimeUtc : mtimePreWrite;
        StoreCachedSha256(path, mtimePostWrite, expectedSize, sha256);
        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", sha256 }), ct);
    }

    private async Task HandlePowerAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        _powerHandler ??= new PowerCommandHandler(this, (host, command) => (AuthorizePeerCommand ?? CommandAuthorizer.IsAllowed)(host, command));
        await _powerHandler.HandleAsync(req, stream, ct);
    }
}

