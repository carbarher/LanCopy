using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using LanCopy.Models;

namespace LanCopy.Services;

public sealed partial class LanClient
{
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
}
