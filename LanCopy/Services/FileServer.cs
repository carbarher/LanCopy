using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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

    // SEGURIDAD: por defecto confina TODAS las operaciones a la carpeta compartida (ShareRoot).
    // Sin esto, un peer puede leer/escribir cualquier ruta del disco (path traversal).
    // El "modo navegador remoto" (acceso a todo el disco) requiere poner esto en false
    // conscientemente; entonces la proteccion recae en PIN + TLS.
    public bool RestrictToShareRoot { get; set; } = true;

    // Modo solo lectura: rechaza put/delete/rename. Util para compartir sin riesgo de escritura.
    public bool ReadOnly { get; set; }

    // Si true, escucha solo en la IP LAN detectada en vez de IPAddress.Any (reduce exposicion).
    public bool BindLanOnly { get; set; }

    // Consentimiento del receptor: si se asigna, se invoca antes de aceptar cada fichero entrante.
    // Devolver false rechaza la transferencia. Si es null, se aceptan automaticamente (compatibilidad).
    public readonly record struct IncomingTransfer(string Ip, string FileName, long Size);
    public Func<IncomingTransfer, CancellationToken, Task<bool>>? ApproveIncoming { get; set; }

    private readonly SemaphoreSlim _connLimit = new(64, 64);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _perIp = new();
    private const int MaxPerIp = 8;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int fails, long untilTick)> _pinFails = new();
    private const int PinMaxFails = 5;
    private const long PinBackoffMs = 30_000;

    private bool TryGuardRead(string? path, out string full, out string reason)
    {
        full = ""; reason = "";
        if (RestrictToShareRoot)
            return ShareRoot.TryResolve(path, out full, out reason);
        full = string.IsNullOrEmpty(path) ? "" : System.IO.Path.GetFullPath(path);
        return true;
    }

    private bool TryGuardWrite(string? path, out string full, out string reason)
    {
        full = ""; reason = "";
        if (RestrictToShareRoot)
        {
            // En modo confinado, la raiz elegida por el usuario ya garantiza la contencion.
            // No aplicamos SystemProtection (la raiz podria estar legitimamente bajo ProgramData,
            // etc.), pero SI bloqueamos enlaces/reparse points que escapen de la raiz.
            if (!ShareRoot.TryResolve(path, out full, out reason)) return false;
            if (SafeFileOps.ContainsReparsePoint(full)) { reason = "La ruta contiene un enlace/reparse point"; return false; }
            return true;
        }

        // Modo disco completo: la unica barrera es SystemProtection + reparse.
        if (string.IsNullOrEmpty(path)) { reason = "Ruta vacia"; return false; }
        full = System.IO.Path.GetFullPath(path);
        // El fichero destino de un put aun no existe; comprobamos la proteccion sobre el
        // ancestro existente mas cercano (evita falsos positivos por File.GetAttributes).
        var checkPath = full;
        while (!File.Exists(checkPath) && !Directory.Exists(checkPath))
        {
            var parent = System.IO.Path.GetDirectoryName(checkPath);
            if (string.IsNullOrEmpty(parent) || parent == checkPath) break;
            checkPath = parent;
        }
        if (SystemProtection.IsProtected(checkPath)) { reason = "Ruta protegida del sistema"; return false; }
        if (SafeFileOps.ContainsReparsePoint(full)) { reason = "La ruta contiene un enlace/reparse point"; return false; }
        return true;
    }

    private static bool FixedTimeEquals(string? a, string? b)
    {
        if (a is null || b is null) return false;
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    // Progreso de transferencias del lado servidor: recepcion ('put') y envio ('get').
    // Receiving=true cuando este equipo recibe; false cuando envia.
    public readonly record struct TransferProgressInfo(bool Receiving, string FileName, long Done, long Total);
    public event Action<TransferProgressInfo>? TransferProgress;

    private IProgress<(long done, long total)> MakeProgress(bool receiving, string fileName) =>
        new ProgressThrottle(v => TransferProgress?.Invoke(
            new TransferProgressInfo(receiving, fileName, v.done, v.total)), intervalMs: 150);

    private sealed class ProgressThrottle(Action<(long done, long total)> sink, long intervalMs)
        : IProgress<(long done, long total)>
    {
        private long _lastMs;
        public void Report((long done, long total) v)
        {
            var now = Environment.TickCount64;
            if (v.done < v.total && now - Interlocked.Read(ref _lastMs) < intervalMs) return;
            Interlocked.Exchange(ref _lastMs, now);
            sink(v);
        }
    }

    private static readonly string CertPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LanCopy", "server.pfx");

    private void EnsureCert()
    {
        if (_serverCert != null) return;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CertPath)!);
        if (File.Exists(CertPath))
        {
            _serverCert = X509CertificateLoader.LoadPkcs12FromFile(CertPath, "lancopy",
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
            return;
        }
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=LanCopy", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
        var pfxBytes = cert.Export(X509ContentType.Pfx, "lancopy");
        File.WriteAllBytes(CertPath, pfxBytes);
        _serverCert = X509CertificateLoader.LoadPkcs12(pfxBytes, "lancopy",
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
    }

    public void Start(int port = 8742)
    {
        Port = port;
        LocalIp = ResolveLocalIp();
        if (RestrictToShareRoot) ShareRoot.EnsureRootExists();
        if (TlsEnabled) EnsureCert();
        _cts = new CancellationTokenSource();
        var bindAddr = IPAddress.Any;
        if (BindLanOnly && IPAddress.TryParse(LocalIp, out var lan) && lan.AddressFamily == AddressFamily.InterNetwork)
            bindAddr = lan;
        _listener = new TcpListener(bindAddr, port);
        _listener.Start();
        Log.Info("server", "started", new { port, ip = LocalIp, tls = TlsEnabled, restrictShareRoot = RestrictToShareRoot, readOnly = ReadOnly, bindLanOnly = BindLanOnly });
        _ = AcceptLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _cts?.Dispose();
        Log.Info("server", "stopped");
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
        var ip = (tcp.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "?";

        if (!await _connLimit.WaitAsync(0))
        {
            try { tcp.Dispose(); } catch { }
            return;
        }
        var count = _perIp.AddOrUpdate(ip, 1, (_, v) => v + 1);
        if (count > MaxPerIp)
        {
            _perIp.AddOrUpdate(ip, 0, (_, v) => Math.Max(0, v - 1));
            _connLimit.Release();
            try { tcp.Dispose(); } catch { }
            return;
        }

        try { await HandleAsync(tcp, ip, serverCt); }
        catch { try { tcp.Dispose(); } catch { } }
        finally
        {
            _perIp.AddOrUpdate(ip, 0, (_, v) => Math.Max(0, v - 1));
            _connLimit.Release();
        }
    }

    private async Task HandleAsync(TcpClient tcp, string ip, CancellationToken ct)
    {
        using (tcp)
        {
            tcp.NoDelay = true;
            using var hsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            hsCts.CancelAfter(TimeSpan.FromSeconds(60));
            var hs = hsCts.Token;
            SslStream? sslToDispose = null;
            try
            {
                // Feature 9: TLS — envuelve NetworkStream con SslStream si está activo
                Stream stream = tcp.GetStream();
                if (TlsEnabled && _serverCert != null)
                {
                    var ssl = new SslStream(stream, leaveInnerStreamOpen: false);
                    sslToDispose = ssl;
                    await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                    {
                        ServerCertificate = _serverCert,
                        ClientCertificateRequired = false,
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                    }, hs);
                    stream = ssl;
                }

                // Lectura de cabeceras con buffer (evita 1 syscall por byte) sin consumir el payload.
                stream = new BufferedLineStream(stream);

                // Feature 10: autenticación PIN si está configurado
                if (!string.IsNullOrEmpty(RequiredPin))
                {
                    // Rate-limit por IP: tras varios fallos, rechazar durante un backoff.
                    if (_pinFails.TryGetValue(ip, out var st) && st.fails >= PinMaxFails
                        && Environment.TickCount64 < st.untilTick)
                    {
                        await Protocol.WriteLineAsync(stream,
                            JsonSerializer.Serialize(new { status = "error", error = "Demasiados intentos. Espere." }), ct);
                        return;
                    }

                    var authLine = await Protocol.ReadLineAsync(stream, hs);
                    var authReq = JsonSerializer.Deserialize<JsonElement>(authLine);
                    var authCmd = authReq.TryGetProperty("cmd", out var cmdEl) ? cmdEl.GetString() : null;
                    var authPin = authReq.TryGetProperty("pin", out var pinEl) ? pinEl.GetString() : null;

                    // Comparacion en tiempo constante (evita timing attacks).
                    var okPin = authCmd == "auth" && FixedTimeEquals(authPin, RequiredPin);
                    if (!okPin)
                    {
                        var nf = _pinFails.AddOrUpdate(ip, (1, Environment.TickCount64 + PinBackoffMs),
                            (_, v) => (v.fails + 1, Environment.TickCount64 + PinBackoffMs));
                        // Poda entradas expiradas para que IPs falsificadas no acumulen memoria.
                        if (_pinFails.Count > 4096)
                            foreach (var kv in _pinFails)
                                if (Environment.TickCount64 > kv.Value.untilTick) _pinFails.TryRemove(kv.Key, out _);
                        SafeFileOps.Audit("auth", ip, "blocked", $"pin-fail #{nf.fails}", "remote");
                        await Protocol.WriteLineAsync(stream,
                            JsonSerializer.Serialize(new { status = "error", error = "PIN inválido" }), ct);
                        return;
                    }
                    _pinFails.TryRemove(ip, out _);
                    await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok" }), ct);
                }

                var line = await Protocol.ReadLineAsync(stream, hs);
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
                    case "put":
                    case "delete":
                    case "rename":
                        if (ReadOnly)
                        {
                            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = "Servidor en modo solo lectura" }), ct);
                            break;
                        }
                        if (cmd == "put") await HandlePutAsync(req, stream, ip, ct);
                        else if (cmd == "delete") await HandleDeleteAsync(req, stream, ct);
                        else await HandleRenameAsync(req, stream, ct);
                        break;
                    case "sha1": await HandleSha1Async(req, stream, ct); break;
                    case "sha256": await HandleSha256Async(req, stream, ct); break;
                    case "hash": await HandleHashAsync(req, stream, ct); break;
                    case "stat": await HandleStatAsync(req, stream, ct); break;
                    case "caps": await HandleCapsAsync(req, stream, ct); break;
                }
            }
            catch (Exception ex) { Log.Warn("server", "handler-error", new { ip, error = ex.Message }); }
            finally { if (sslToDispose != null) { try { await sslToDispose.DisposeAsync(); } catch { } } }
        }
    }

    private async Task HandleListAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        var reqPath = req.GetProperty("path").GetString() ?? "";
        if (!TryGuardRead(reqPath, out var path, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }
        var entries = BuildEntries(path);
        if (RestrictToShareRoot)
            entries.RemoveAll(e => !ShareRoot.TryResolve(e.FullPath, out _, out _));
        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", entries }), ct);
    }

    private const int MaxListRecursiveFiles = 100_000;

    private async Task HandleListRecursiveAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        var reqRoot = req.GetProperty("path").GetString() ?? "";
        if (!TryGuardRead(reqRoot, out var root, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }
        var entries = new List<FileEntry>();
        try
        {
            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (entries.Count >= MaxListRecursiveFiles) break;
                // Evita que un symlink dentro de la raiz exponga ficheros externos.
                if (RestrictToShareRoot && !ShareRoot.TryResolve(f, out _, out _)) continue;
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
            JsonSerializer.Serialize(new { status = "ok", compress = true, tls = TlsEnabled, version = Protocol.Version, minVersion = Protocol.MinSupportedVersion, readOnly = ReadOnly }), ct);
    }

    // Feature 2: límite de compresión en memoria (200 MB)
    private const long MaxCompressInMemory = 200L * 1024 * 1024;

    private async Task HandleGetAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        var reqPath = req.GetProperty("path").GetString();
        if (!TryGuardRead(reqPath, out var path, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }
        if (Directory.Exists(path))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = "Es un directorio" }), ct);
            return;
        }
        await using var fs = File.OpenRead(path);
        var size = fs.Length;

        // Integridad: SHA-256 (una sola lectura previa al envio).
        var sha256 = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();
        fs.Seek(0, SeekOrigin.Begin);

        // Feature 2: compresión deflate opcional
        bool wantCompress = req.TryGetProperty("compress", out var ce) && ce.GetBoolean()
                            && size > 0 && size <= MaxCompressInMemory
                            && !Protocol.IsCompressedExtension(path);
        if (wantCompress)
        {
            using var ms = new MemoryStream();
            await using (var ds = new DeflateStream(ms, CompressionLevel.Fastest, leaveOpen: true))
                await fs.CopyToAsync(ds, ct);
            var compressedSize = ms.Length;
            ms.Seek(0, SeekOrigin.Begin);
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new
            { status = "ok", size, sha256, compress = true, compressed_size = compressedSize }), ct);
            await Protocol.CopyExactAsync(ms, stream, compressedSize, MakeProgress(false, Path.GetFileName(path)), ct);
        }
        else
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", size, sha256 }), ct);
            await Protocol.CopyExactAsync(fs, stream, size, MakeProgress(false, Path.GetFileName(path)), ct);
        }
    }

    private const long MaxPutBytes = 100L * 1024 * 1024 * 1024; // 100 GB

    private async Task HandlePutAsync(JsonElement req, Stream stream, string ip, CancellationToken ct)
    {
        var size = req.GetProperty("size").GetInt64();
        if (size < 0 || size > MaxPutBytes)
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = "Tamaño inválido" }), ct);
            return;
        }
        var reqPath = req.GetProperty("path").GetString();
        if (!TryGuardWrite(reqPath, out var path, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }
        // Numero de bytes que el cliente va a enviar por el cable (cuerpo).
        long wireBytes = req.TryGetProperty("compress", out var ceGate) && ceGate.GetBoolean()
            && req.TryGetProperty("compressed_size", out var csGate) ? csGate.GetInt64() : size;
        // Anti-OOM: el cuerpo comprimido se descomprime en memoria; limitar su tamaño declarado.
        bool gateCompressed = req.TryGetProperty("compress", out var gc) && gc.GetBoolean()
            && req.TryGetProperty("compressed_size", out _);
        if (gateCompressed && (wireBytes < 0 || wireBytes > MaxCompressInMemory))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = "compressed_size inválido" }), ct);
            return;
        }
        // Consentimiento del receptor antes de tocar el disco.
        if (ApproveIncoming is { } approve)
        {
            bool ok;
            try { ok = await approve(new IncomingTransfer(ip, Path.GetFileName(path), size), ct); }
            catch { ok = false; }
            if (!ok)
            {
                Log.Info("server", "put-rejected", new { ip, file = Path.GetFileName(path) });
                // Drenar el cuerpo para que el cliente termine de escribir y reciba el ack limpio
                // (sin esto, el cierre con bytes sin leer provoca un RST que pierde el error).
                await Protocol.DrainAsync(stream, wireBytes, ct);
                await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = "Transferencia rechazada por el receptor" }), ct);
                return;
            }
        }
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // Feature 2: compresión deflate opcional
        bool isCompressed = req.TryGetProperty("compress", out var ce) && ce.GetBoolean()
                            && req.TryGetProperty("compressed_size", out _);
        string sha256;
        try
        {
        await using var fs = File.Create(path);
        if (isCompressed)
        {
            var compressedSize = req.GetProperty("compressed_size").GetInt64();
            using var compBuf = new MemoryStream();
            await Protocol.CopyExactAsync(stream, compBuf, compressedSize, MakeProgress(true, Path.GetFileName(path)), ct);
            compBuf.Seek(0, SeekOrigin.Begin);
            await using var ds = new DeflateStream(compBuf, CompressionMode.Decompress);
            // Anti zip-bomb + hash en una sola pasada mientras se escribe.
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var zbuf = new byte[Protocol.BufferSize];
            long written = 0;
            int zr;
            while ((zr = await ds.ReadAsync(zbuf, ct)) > 0)
            {
                written += zr;
                if (written > size) throw new InvalidDataException("Descompresion excede el tamano declarado (posible zip-bomb)");
                await fs.WriteAsync(zbuf.AsMemory(0, zr), ct);
                hasher.AppendData(zbuf, 0, zr);
            }
            sha256 = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
        }
        else
        {
            // Hash en streaming durante la recepcion: evita re-leer el fichero del disco.
            sha256 = await Protocol.CopyExactToHashAsync(stream, fs, size, MakeProgress(true, Path.GetFileName(path)), ct);
        }
        }
        catch
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
            throw;
        }

        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", sha256 }), ct);
    }

    private async Task HandleDeleteAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        var path = req.GetProperty("path").GetString()!;
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

    private async Task HandleRenameAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        var path = req.GetProperty("path").GetString()!;
        var newName = req.GetProperty("newname").GetString()!;

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
            if (!TryGuardWrite(newPath, out _, out var destGuard))
            {
                await Protocol.WriteLineAsync(stream,
                    JsonSerializer.Serialize(new { status = "error", error = $"Destino bloqueado: {destGuard}" }), ct);
                SafeFileOps.Audit("rename", normalized, "blocked", $"dest:{destGuard}", "remote");
                return;
            }
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

    private async Task HandleSha1Async(JsonElement req, Stream stream, CancellationToken ct)
    {
        var reqPath = req.GetProperty("path").GetString();
        if (!TryGuardRead(reqPath, out var path, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }
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

    private async Task HandleSha256Async(JsonElement req, Stream stream, CancellationToken ct)
    {
        var reqPath = req.GetProperty("path").GetString();
        if (!TryGuardRead(reqPath, out var path, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }
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

    private async Task HandleHashAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        var reqPath = req.GetProperty("path").GetString();
        if (!TryGuardRead(reqPath, out var path, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }
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

    private async Task HandleStatAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        var reqPath = req.GetProperty("path").GetString();
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
