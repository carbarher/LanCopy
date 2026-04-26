using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using LanCopy.Models;

namespace LanCopy.Services;

public sealed class FileServer
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private X509Certificate2? _serverCert; // Feature 9: TLS

    public int Port { get; private set; }
    public string LocalIp { get; private set; } = "localhost";
    public string? RequiredPin { get; set; } // Feature 10: si no-null, clientes deben autenticar
    public bool TlsEnabled { get; set; }     // Feature 9: TLS toggle

    private static readonly string CertPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LanCopy", "server.pfx");

    private void EnsureCert()
    {
        if (_serverCert != null) return;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CertPath)!);
        if (File.Exists(CertPath))
        {
            _serverCert = new X509Certificate2(CertPath, "lancopy",
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
            return;
        }
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=LanCopy", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
        var pfxBytes = cert.Export(X509ContentType.Pfx, "lancopy");
        File.WriteAllBytes(CertPath, pfxBytes);
        _serverCert = new X509Certificate2(pfxBytes, "lancopy",
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
    }

    public void Start(int port = 8742)
    {
        Port = port;
        LocalIp = ResolveLocalIp();
        if (TlsEnabled) EnsureCert();
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        _ = AcceptLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(ct);
                _ = HandleWithTimeoutAsync(client, ct);
            }
            catch (OperationCanceledException) { break; }
            catch { }
        }
    }

    private async Task HandleWithTimeoutAsync(TcpClient tcp, CancellationToken serverCt)
    {
        using var connCts = CancellationTokenSource.CreateLinkedTokenSource(serverCt);
        connCts.CancelAfter(TimeSpan.FromSeconds(60));
        try { await HandleAsync(tcp, connCts.Token); }
        catch { tcp.Dispose(); }
    }

    private async Task HandleAsync(TcpClient tcp, CancellationToken ct)
    {
        using (tcp)
        {
            tcp.NoDelay = true;
            try
            {
                // Feature 9: TLS — envuelve NetworkStream con SslStream si está activo
                Stream stream = tcp.GetStream();
                if (TlsEnabled && _serverCert != null)
                {
                    var ssl = new SslStream(stream, leaveInnerStreamOpen: false);
                    await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                    {
                        ServerCertificate = _serverCert,
                        ClientCertificateRequired = false,
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                    }, ct);
                    stream = ssl;
                }

                // Feature 10: autenticación PIN si está configurado
                if (!string.IsNullOrEmpty(RequiredPin))
                {
                    var authLine = await Protocol.ReadLineAsync(stream, ct);
                    var authReq = JsonSerializer.Deserialize<JsonElement>(authLine);
                    var authCmd = authReq.TryGetProperty("cmd", out var cmdEl) ? cmdEl.GetString() : null;
                    var authPin = authReq.TryGetProperty("pin", out var pinEl) ? pinEl.GetString() : null;
                    if (authCmd != "auth" || authPin != RequiredPin)
                    {
                        await Protocol.WriteLineAsync(stream,
                            JsonSerializer.Serialize(new { status = "error", error = "PIN inválido" }), ct);
                        return;
                    }
                    await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok" }), ct);
                }

                var line = await Protocol.ReadLineAsync(stream, ct);
                var req = JsonSerializer.Deserialize<JsonElement>(line);
                var cmd = req.GetProperty("cmd").GetString();

                switch (cmd)
                {
                    case "list":
                        bool recursive = req.TryGetProperty("recursive", out var rv) && rv.GetBoolean();
                        if (recursive) await HandleListRecursiveAsync(req, stream, ct);
                        else await HandleListAsync(req, stream, ct);
                        break;
                    case "get": await HandleGetAsync(req, stream, ct); break;
                    case "put": await HandlePutAsync(req, stream, ct); break;
                    case "delete": await HandleDeleteAsync(req, stream, ct); break;
                    case "rename": await HandleRenameAsync(req, stream, ct); break;
                    case "sha1": await HandleSha1Async(req, stream, ct); break;
                    case "sha256": await HandleSha256Async(req, stream, ct); break;
                    case "hash": await HandleHashAsync(req, stream, ct); break;
                    case "stat": await HandleStatAsync(req, stream, ct); break;
                    case "caps": await HandleCapsAsync(req, stream, ct); break;
                }
            }
            catch { }
        }
    }

    private static async Task HandleListAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        var path = req.GetProperty("path").GetString() ?? "";
        var entries = BuildEntries(path);
        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", entries }), ct);
    }

    private const int MaxListRecursiveFiles = 100_000;

    private static async Task HandleListRecursiveAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        var root = req.GetProperty("path").GetString() ?? "";
        var entries = new List<FileEntry>();
        try
        {
            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (entries.Count >= MaxListRecursiveFiles) break;
                var fi = new FileInfo(f);
                entries.Add(new FileEntry
                {
                    Name = Path.GetRelativePath(root, f),
                    FullPath = f,
                    Size = fi.Length,
                    LastWriteUtcTicks = fi.LastWriteTimeUtc.Ticks
                });
            }
        }
        catch { }
        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", entries }), ct);
    }

    // Feature 9: capabilities — anuncia soporte de compresión y TLS al cliente
    private async Task HandleCapsAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { status = "ok", compress = true, tls = TlsEnabled }), ct);
    }

    // Feature 2: límite de compresión en memoria (200 MB)
    private const long MaxCompressInMemory = 200L * 1024 * 1024;

    private static async Task HandleGetAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        var path = req.GetProperty("path").GetString()!;
        if (Directory.Exists(path))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = "Es un directorio" }), ct);
            return;
        }
        await using var fs = File.OpenRead(path);
        var size = fs.Length;

        // Compat: exponer SHA-256 y SHA-1 para clientes nuevos y viejos.
        var sha256 = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();
        fs.Seek(0, SeekOrigin.Begin);
        var sha1 = Convert.ToHexString(await SHA1.HashDataAsync(fs, ct)).ToLowerInvariant();
        fs.Seek(0, SeekOrigin.Begin);

        // Feature 2: compresión deflate opcional
        bool wantCompress = req.TryGetProperty("compress", out var ce) && ce.GetBoolean()
                            && size > 0 && size <= MaxCompressInMemory;
        if (wantCompress)
        {
            using var ms = new MemoryStream();
            await using (var ds = new DeflateStream(ms, CompressionLevel.Fastest, leaveOpen: true))
                await fs.CopyToAsync(ds, ct);
            var compressedSize = ms.Length;
            ms.Seek(0, SeekOrigin.Begin);
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new
            { status = "ok", size, sha256, sha1, compress = true, compressed_size = compressedSize }), ct);
            await Protocol.CopyExactAsync(ms, stream, compressedSize, null, ct);
        }
        else
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", size, sha256, sha1 }), ct);
            await Protocol.CopyExactAsync(fs, stream, size, null, ct);
        }
    }

    private const long MaxPutBytes = 100L * 1024 * 1024 * 1024; // 100 GB

    private static async Task HandlePutAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        var size = req.GetProperty("size").GetInt64();
        if (size < 0 || size > MaxPutBytes)
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = "Tamaño inválido" }), ct);
            return;
        }
        var path = req.GetProperty("path").GetString()!;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // Feature 2: compresión deflate opcional
        bool isCompressed = req.TryGetProperty("compress", out var ce) && ce.GetBoolean()
                            && req.TryGetProperty("compressed_size", out _);
        await using var fs = File.Create(path);
        if (isCompressed)
        {
            var compressedSize = req.GetProperty("compressed_size").GetInt64();
            using var compBuf = new MemoryStream();
            await Protocol.CopyExactAsync(stream, compBuf, compressedSize, null, ct);
            compBuf.Seek(0, SeekOrigin.Begin);
            await using var ds = new DeflateStream(compBuf, CompressionMode.Decompress);
            await ds.CopyToAsync(fs, ct);
        }
        else
        {
            await Protocol.CopyExactAsync(stream, fs, size, null, ct);
        }

        fs.Seek(0, SeekOrigin.Begin);
        var sha256 = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();
        fs.Seek(0, SeekOrigin.Begin);
        var sha1 = Convert.ToHexString(await SHA1.HashDataAsync(fs, ct)).ToLowerInvariant();

        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", sha256, sha1 }), ct);
    }

    private static async Task HandleDeleteAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        var path = req.GetProperty("path").GetString()!;
        if (!SafeFileOps.TryValidateMutationPath(path, out var normalized, out var reason))
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = reason }), ct);
            SafeFileOps.Audit("delete", path, "blocked", reason, "remote");
            return;
        }
        var key = $"remote-delete:{normalized}";
        if (SafeFileOps.IsOnCooldown(key, 2))
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = "Cooldown activo (2s)" }), ct);
            SafeFileOps.Audit("delete", normalized, "blocked", "cooldown", "remote");
            return;
        }
        try
        {
            if (SafeFileOps.TryMoveToTrash(normalized, out var moved, out var moveErr))
            {
                await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", movedPath = moved }), ct);
                SafeFileOps.Audit("delete", normalized, "ok", $"trash:{moved}", "remote");
                return;
            }

            // Fallback controlado: solo si papelera falla por causa de volumen/permiso.
            if (Directory.Exists(normalized)) Directory.Delete(normalized, recursive: true);
            else if (File.Exists(normalized)) File.Delete(normalized);
            else throw new FileNotFoundException("No encontrado");

            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok" }), ct);
            SafeFileOps.Audit("delete", normalized, "ok", $"hard-delete:{moveErr}", "remote");
        }
        catch (Exception ex)
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = ex.Message }), ct);
            SafeFileOps.Audit("delete", normalized, "error", ex.Message, "remote");
        }
    }

    private static async Task HandleRenameAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        var path = req.GetProperty("path").GetString()!;
        var newName = req.GetProperty("newname").GetString()!;

        if (!SafeFileOps.TryValidateMutationPath(path, out var normalized, out var reason))
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = reason }), ct);
            SafeFileOps.Audit("rename", path, "blocked", reason, "remote");
            return;
        }
        // Validar: el nuevo nombre no puede contener separadores de ruta
        if (string.IsNullOrWhiteSpace(newName) || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = "Nombre inválido" }), ct);
            return;
        }

        var key = $"remote-rename:{normalized}";
        if (SafeFileOps.IsOnCooldown(key, 2))
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = "Cooldown activo (2s)" }), ct);
            SafeFileOps.Audit("rename", normalized, "blocked", "cooldown", "remote");
            return;
        }

        try
        {
            var dir = Path.GetDirectoryName(normalized)!;
            var newPath = Path.Combine(dir, newName);
            if (!SafeFileOps.TryValidateMutationPath(newPath, out _, out var targetReason, requireExists: false))
            {
                await Protocol.WriteLineAsync(stream,
                    JsonSerializer.Serialize(new { status = "error", error = $"Destino bloqueado: {targetReason}" }), ct);
                SafeFileOps.Audit("rename", normalized, "blocked", $"dest:{targetReason}", "remote");
                return;
            }

            if (Directory.Exists(normalized)) Directory.Move(normalized, newPath);
            else File.Move(normalized, newPath);

            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok" }), ct);
            SafeFileOps.Audit("rename", normalized, "ok", $"to:{newPath}", "remote");
        }
        catch (Exception ex)
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = ex.Message }), ct);
            SafeFileOps.Audit("rename", normalized, "error", ex.Message, "remote");
        }
    }

    private static async Task HandleSha1Async(JsonElement req, Stream stream, CancellationToken ct)
    {
        var path = req.GetProperty("path").GetString()!;
        try
        {
            await using var fs = File.OpenRead(path);
            var sha1 = Convert.ToHexString(await SHA1.HashDataAsync(fs, ct)).ToLowerInvariant();
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", sha1 }), ct);
        }
        catch (Exception ex)
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = ex.Message }), ct);
        }
    }

    private static async Task HandleSha256Async(JsonElement req, Stream stream, CancellationToken ct)
    {
        var path = req.GetProperty("path").GetString()!;
        try
        {
            await using var fs = File.OpenRead(path);
            var sha256 = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", sha256 }), ct);
        }
        catch (Exception ex)
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = ex.Message }), ct);
        }
    }

    private static async Task HandleHashAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        var path = req.GetProperty("path").GetString()!;
        var alg = req.TryGetProperty("alg", out var algEl) ? (algEl.GetString() ?? "sha256") : "sha256";

        try
        {
            await using var fs = File.OpenRead(path);
            if (string.Equals(alg, "sha1", StringComparison.OrdinalIgnoreCase))
            {
                var sha1 = Convert.ToHexString(await SHA1.HashDataAsync(fs, ct)).ToLowerInvariant();
                await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", alg = "sha1", hash = sha1 }), ct);
                return;
            }

            var sha256 = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", alg = "sha256", hash = sha256 }), ct);
        }
        catch (Exception ex)
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = ex.Message }), ct);
        }
    }

    private static async Task HandleStatAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        var path = req.GetProperty("path").GetString()!;
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
        catch (Exception ex)
        {
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { status = "error", error = ex.Message }), ct);
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
            foreach (var d in di.GetDirectories().OrderBy(x => x.Name))
                list.Add(new FileEntry { Name = d.Name, FullPath = d.FullName, IsDirectory = true });
            foreach (var f in di.GetFiles().OrderBy(x => x.Name))
                list.Add(new FileEntry { Name = f.Name, FullPath = f.FullName, Size = f.Length, LastWriteUtcTicks = f.LastWriteTimeUtc.Ticks });
        }
        catch { }

        return list;
    }

    private static string ResolveLocalIp()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback) continue;

            foreach (var ua in ni.GetIPProperties().UnicastAddresses)
            {
                var addr = ua.Address;
                if (addr.AddressFamily != AddressFamily.InterNetwork) continue;
                var b = addr.GetAddressBytes();
                if (b[0] == 10) return addr.ToString();
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return addr.ToString();
                if (b[0] == 192 && b[1] == 168) return addr.ToString();
            }
        }
        try
        {
            using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.Connect("8.8.8.8", 80);
            return ((IPEndPoint)s.LocalEndPoint!).Address.ToString();
        }
        catch { return "localhost"; }
    }
}
