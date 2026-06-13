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

    private readonly string _host;
    private readonly int _port;
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
        await tcp.ConnectAsync(_host, _port, ct);
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

    // â”€â”€ LIST â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

    // â”€â”€ LIST RECURSIVE (para transferir carpetas) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

    // â”€â”€ GET â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task DownloadAsync(
        string remotePath, string localPath,
        IProgress<(long done, long total)>? progress = null,
        CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "get", path = remotePath, compress = UseCompress }), ct);
        var headerLine = await Protocol.ReadLineAsync(stream, ct);
        var header = JsonSerializer.Deserialize<JsonElement>(headerLine);
        EnsureOk(header);
        var size = header.GetProperty("size").GetInt64();

        var dir = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        bool serverCompressed = header.TryGetProperty("compress", out var compEl) && compEl.GetBoolean()
                                && header.TryGetProperty("compressed_size", out var _compSizeProp);

        await using var fs = File.Create(localPath);
        string? actualSha256 = null;
        if (serverCompressed)
        {
            var compressedSize = header.GetProperty("compressed_size").GetInt64();
            using var compBuf = new MemoryStream();
            await Protocol.CopyExactAsync(stream, compBuf, compressedSize, null, ct);
            compBuf.Seek(0, SeekOrigin.Begin);
            await using var ds = new DeflateStream(compBuf, CompressionMode.Decompress);
            // Hash en una pasada mientras se descomprime/escribe.
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var dbuf = new byte[Protocol.BufferSize];
            int dr;
            while ((dr = await ds.ReadAsync(dbuf, ct)) > 0)
            {
                await fs.WriteAsync(dbuf.AsMemory(0, dr), ct);
                hasher.AppendData(dbuf, 0, dr);
            }
            actualSha256 = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
        }
        else
        {
            // Hash en streaming durante la recepcion: evita re-leer el fichero del disco.
            actualSha256 = await Protocol.CopyExactToHashAsync(stream, fs, size, progress, ct);
        }

        // Verificar integridad. Preferir SHA-256; fallback SHA-1 para peers antiguos.
        if (header.TryGetProperty("sha256", out var sha256El))
        {
            var expected = sha256El.GetString() ?? "";
            if (!string.Equals(expected, actualSha256, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Checksum SHA256 no coincide para {Path.GetFileName(localPath)}");
        }
        else if (header.TryGetProperty("sha1", out var sha1El))
        {
            var expected = sha1El.GetString() ?? "";
            fs.Seek(0, SeekOrigin.Begin);
            var actual = Convert.ToHexString(await SHA1.HashDataAsync(fs, ct)).ToLowerInvariant();
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Checksum SHA1 no coincide para {Path.GetFileName(localPath)}");
        }
    }

    // â”€â”€ PUT â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task UploadAsync(
        string localPath, string remotePath,
        IProgress<(long done, long total)>? progress = null,
        CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;

        // Abre archivo primero para tamaño real → evita race condition (#4)
        await using var fs = File.OpenRead(localPath);
        var size = fs.Length;

        // Integridad: SHA-256 (una sola lectura).
        var sha256Local = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();
        fs.Seek(0, SeekOrigin.Begin);

        // Feature 2: compresión deflate opcional (solo archivos <= 200 MB)
        bool doCompress = UseCompress && size > 0 && size <= 200L * 1024 * 1024;
        if (doCompress)
        {
            using var ms = new MemoryStream();
            await using (var ds = new DeflateStream(ms, CompressionLevel.Fastest, leaveOpen: true))
                await fs.CopyToAsync(ds, ct);
            var compressedSize = ms.Length;
            ms.Seek(0, SeekOrigin.Begin);
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { cmd = "put", path = remotePath, size, compress = true, compressed_size = compressedSize }), ct);
            await Protocol.CopyExactAsync(ms, stream, compressedSize, progress, ct);
        }
        else
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { cmd = "put", path = remotePath, size }), ct);
            await Protocol.CopyExactAsync(fs, stream, size, progress, ct);
        }
        var ackLine = await Protocol.ReadLineAsync(stream, ct);
        var ack = JsonSerializer.Deserialize<JsonElement>(ackLine);
        EnsureOk(ack);

        // Verificar integridad SHA-256 reportada por el servidor.
        if (ack.TryGetProperty("sha256", out var sha256El))
        {
            var serverSha256 = sha256El.GetString() ?? "";
            if (!string.Equals(sha256Local, serverSha256, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Checksum SHA256 no coincide para {Path.GetFileName(localPath)}");
        }
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

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static void EnsureOk(JsonElement resp)
    {
        var status = resp.TryGetProperty("status", out var s) ? s.GetString() : null;
        if (status != "ok")
        {
            var msg = resp.TryGetProperty("error", out var e) ? e.GetString() : "Error desconocido";
            throw new Exception(msg);
        }
    }

    public void Dispose() { }
}