using System.IO;
using System.IO.Compression;
using System.Diagnostics;
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

public sealed class FileServer : IAsyncDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private X509Certificate2? _serverCert; // Feature 9: TLS

    public int Port { get; private set; }
    public string LocalIp { get; private set; } = "localhost";
    public string? RequiredPin { get; set; } // Feature 10: si no-null, clientes deben autenticar
    private readonly object _pinLock = new(); // B5: protege RequiredPin+PinExpiresAt contra race
    // F9: PIN con expiración opcional (null = no expira)
    public DateTimeOffset? PinExpiresAt { get; set; }
    /// <summary>Última actividad con PIN válido. Se usa para expiración por inactividad.</summary>
    private DateTimeOffset _lastAuthActivity = DateTimeOffset.UtcNow;
    /// <summary>Tiempo de inactividad tras el cual el PIN requiere re-autenticación (0 = deshabilitado).</summary>
    public TimeSpan PinIdleExpiry { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Establece un PIN temporal que se auto-limpia al expirar.</summary>
    public void SetTemporaryPin(string pin, TimeSpan duration)
    {
        lock (_pinLock) // B5: atómico
        {
            RequiredPin = pin;
            PinExpiresAt = DateTimeOffset.UtcNow.Add(duration);
        }

    }
    public bool TlsEnabled { get; set; }     // Feature 9: TLS toggle

    // SEGURIDAD: por defecto confina TODAS las operaciones a la carpeta compartida (ShareRoot).
    // Sin esto, un peer puede leer/escribir cualquier ruta del disco (path traversal).
    // El "modo navegador remoto" (acceso a todo el disco) requiere poner esto en false
    // conscientemente; entonces la proteccion recae en PIN + TLS.
    public bool RestrictToShareRoot { get; set; } = true;

    // Modo solo lectura: rechaza put/delete/rename. Util para compartir sin riesgo de escritura.
    public bool ReadOnly { get; set; }
    // Modo seguro: permite subir/renombrar/crear, pero bloquea borrado remoto.
    public bool SafeModeNoRemoteDelete { get; set; }

    // Si true, escucha solo en la IP LAN detectada en vez de IPAddress.Any (reduce exposicion).
    public bool BindLanOnly { get; set; }

    // Consentimiento del receptor: si se asigna, se invoca antes de aceptar cada fichero entrante.
    // Devolver false rechaza la transferencia. Si es null, se aceptan automaticamente (compatibilidad).
    public readonly record struct IncomingTransfer(string Ip, string FileName, long Size);
    public Func<IncomingTransfer, CancellationToken, Task<bool>>? ApproveIncoming { get; set; }

    private const int MaxConnections = 64; // Q6
    private readonly SemaphoreSlim _connLimit = new(MaxConnections, MaxConnections); // B1: usar constante
    private const int KeepAliveIdleSeconds = 15;
    private const int KeepAliveIntervalSeconds = 5;
    private const int KeepAliveRetryCount = 3;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _perIp = new();
    public int MaxPerIp { get; set; } = 8;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int fails, long untilTick)> _pinFails = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (long windowStartTick, int count)> _cmdRate = new();
    private long _cmdRateLastCleanTick; // S2: throttle cleanup para evitar O(n) per request bajo DDoS
    // B1/P1: contador O(1) para _cmdRate — evita ConcurrentDictionary.Count O(n) en hot path
    private int _cmdRateCount;
    // M8: contador O(1) para IPs activas — _perIp.Count(predicate) era O(n) LINQ en HandleHealthAsync
    private int _activeIpCount;
    public int CommandRateWindowSeconds { get; set; } = 10;
    public int CommandRateLimit { get; set; } = 120;
    public int PinMaxFails { get; set; } = 5;
    // CachÃ© SHA-256 (path -> lastWrite,size,hash): evita leer el archivo dos veces en get.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime LastWrite, long Size, string Hash)> _sha256Cache = new(StringComparer.OrdinalIgnoreCase);
    public long PinBackoffMs { get; set; } = 30_000;
    public long PinMaxBackoffMs { get; set; } = 10 * 60_000;

    private static void ConfigureClientSocket(TcpClient tcp)
    {
        try
        {
            tcp.NoDelay = true;
            tcp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            tcp.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, KeepAliveIdleSeconds);
            tcp.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, KeepAliveIntervalSeconds);
            tcp.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, KeepAliveRetryCount);
        }
        catch (SocketException ex) { Log.Debug("server", "tcp-keepalive-tuning-socket-failed", new { error = ex.Message }); }
        catch (PlatformNotSupportedException ex) { Log.Debug("server", "tcp-keepalive-tuning-unsupported", new { error = ex.Message }); }
        catch (Exception ex) { Log.Debug("server", "tcp-socket-config-failed", new { error = ex.Message }); }
    }

    private bool TryGuardRead(string? path, out string full, out string reason)
    {
        full = ""; reason = "";
        try
        {
            if (!string.IsNullOrEmpty(path))
            {
                if (string.Equals(path, "Documents", StringComparison.OrdinalIgnoreCase))
                    path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                else if (string.Equals(path, "Downloads", StringComparison.OrdinalIgnoreCase))
                    path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                else if (string.Equals(path, "Desktop", StringComparison.OrdinalIgnoreCase))
                    path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }

            // S3: ADS (Alternate Data Streams) bloqueados también en lectura
            // - sin esta guard, un peer puede leer "file.txt:Zone.Identifier" y extraer metadatos sensibles
            if (!string.IsNullOrEmpty(path) && HasNtfsAdsColon(path)) { reason = "svc.adsBlocked"; return false; }
            if (RestrictToShareRoot)
                return ShareRoot.TryResolve(path, out full, out reason);

            // Modo disco completo: permitir ruta vacía para listar unidades
            if (string.IsNullOrEmpty(path))
            {
                full = "";
                return true;
            }

            full = System.IO.Path.GetFullPath(path);

            // Permitir la lectura de raíces de unidad (ej: C:\, D:\) para poder listar su contenido
            var root = Path.GetPathRoot(full);
            if (root != null && string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // S1/Q3: modo full-disk - aplicar IsProtected (sistema) en lugar de IsProtectedForRemote para lectura.
            // Esto permite al par remoto leer y descargar archivos del perfil personal del usuario (Downloads, Desktop, etc.)
            // pero sigue impidiendo la lectura de archivos críticos del OS (Windows, Program Files, etc.)
            if (SystemProtection.IsProtected(full))
            {
                reason = "svc.accessDenied";
                return false;
            }
            return true;
        }
        catch
        {
            reason = "svc.invalidPath";
            return false;
        }
    }

    private bool TryGuardWrite(string? path, out string full, out string reason)
    {
        full = ""; reason = "";
        try
        {
            if (!string.IsNullOrEmpty(path))
            {
                if (string.Equals(path, "Documents", StringComparison.OrdinalIgnoreCase))
                    path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                else if (string.Equals(path, "Downloads", StringComparison.OrdinalIgnoreCase))
                    path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                else if (string.Equals(path, "Desktop", StringComparison.OrdinalIgnoreCase))
                    path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }

            // S4: bloquear NTFS ADS (e.g. "file.txt:evil")
            if (!string.IsNullOrEmpty(path) && HasNtfsAdsColon(path)) { reason = "svc.adsBlocked"; return false; }
            if (RestrictToShareRoot)
            {
                // En modo confinado, la raiz elegida por el usuario ya garantiza la contencion.
                // No aplicamos SystemProtection (la raiz podria estar legitimamente bajo ProgramData,
                // etc.), pero SI bloqueamos enlaces/reparse points que escapen de la raiz.
                if (!ShareRoot.TryResolve(path, out full, out reason)) return false;
                if (SafeFileOps.ContainsReparsePoint(full)) { reason = "svc.reparsePath"; return false; } // Q4: era string español
                return true;
            }

            // Modo disco completo: la unica barrera es SystemProtection + reparse.
            if (string.IsNullOrEmpty(path)) { reason = "svc.emptyPath"; return false; }
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
            // Remoto en disco completo: protege sistema + arbol personal del usuario.
            if (SystemProtection.IsProtectedForRemote(checkPath)) { reason = "svc.sysProtected"; return false; }
            if (SafeFileOps.ContainsReparsePoint(full)) { reason = "svc.reparsePath"; return false; } // Q4: era string español
            return true;
        }
        catch
        {
            reason = "svc.invalidPath";
            return false;
        }
    }

    // S4: un ':' después de la letra de unidad indica NTFS Alternate Data Stream
    private static bool HasNtfsAdsColon(string path)
    {
        var start = path.Length > 2 && path[1] == ':' ? 2 : 0;
        return path.IndexOf(':', start) >= 0;
    }

    private bool TryGetCachedSha256(string path, out string sha256)
    {
        sha256 = "";
        if (!_sha256Cache.TryGetValue(path, out var cached)) return false;

        FileInfo fi;
        try { fi = new FileInfo(path); }
        catch
        {
            _sha256Cache.TryRemove(path, out _);
            return false;
        }

        if (!fi.Exists)
        {
            _sha256Cache.TryRemove(path, out _);
            return false;
        }

        // B1: re-validar mtime Y size; en Windows NTFS puede tardar hasta 2s en actualizar LastWriteTime
        // tras una escritura. Si mtime o size difieren, el cache ya no es válido.
        if (cached.LastWrite == fi.LastWriteTimeUtc && cached.Size == fi.Length)
        {
            sha256 = cached.Hash;
            return true;
        }

        _sha256Cache.TryRemove(path, out _);
        return false;
    }

    private void StoreCachedSha256(string path, DateTime lastWriteUtc, long size, string sha256)
    {
        // B6: evict ANTES de escribir — si evict ocurre después, el entry recién añadido puede ser eliminado inmediatamente
        if (_sha256Cache.Count >= 4096)
        {
            var removed = 0;
            foreach (var key in _sha256Cache.Keys)
            {
                if (key == path) continue; // no eliminar el que estamos a punto de escribir
                if (_sha256Cache.TryRemove(key, out _))
                {
                    removed++;
                    if (removed >= 1024) break;
                }
            }
        }
        _sha256Cache[path] = (lastWriteUtc, size, sha256);
    }

    private static bool TryGetStringProperty(JsonElement req, string name, out string value)
    {
        value = "";
        if (!req.TryGetProperty(name, out var el)) return false;
        value = el.GetString() ?? "";
        return true;
    }

    private static bool TryGetInt64Property(JsonElement req, string name, out long value)
    {
        value = 0;
        return req.TryGetProperty(name, out var el) && el.TryGetInt64(out value);
    }

    private static async Task WriteBadRequestAsync(Stream stream, CancellationToken ct)
    {
        await Protocol.WriteErrorAsync(stream, "svc.badRequest", ct); // P1: bytes cacheados
    }

    private bool IsCommandRateAllowed(string ip, string cmd)
    {
        if (CommandRateLimit <= 0) return true;
        if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(cmd)) return true;

        var now = Environment.TickCount64;
        var windowMs = Math.Max(1, CommandRateWindowSeconds) * 1000L;
        var key = ip; // Q3: era "{ip}|{cmd}" — rate-limit debe ser per-IP total, no per-IP-per-command (el anterior permitía 120*N comandos)

        var isNew = false;
        var state = _cmdRate.AddOrUpdate(
            key,
            _ => { isNew = true; return (now, 1); },
            (_, s) => (now - s.windowStartTick >= windowMs) ? (now, 1) : (s.windowStartTick, s.count + 1));
        if (isNew) Interlocked.Increment(ref _cmdRateCount);

        // S2: limitar la limpieza del rate-table a una vez cada 30s en lugar de en cada petición
        // — con 8192+ IPs bajo DDoS, la limpieza O(n) por cada request crearía 8M lookups/s
        // B1: CAS atómico para que solo UN thread haga el cleanup (evita double-sweep concurrente)
        var prevClean = Volatile.Read(ref _cmdRateLastCleanTick);
        // B1/P1: usar _cmdRateCount O(1) en lugar de .Count O(n)
        if (_cmdRateCount > 8192 && now - prevClean > 30_000 &&
            Interlocked.CompareExchange(ref _cmdRateLastCleanTick, now, prevClean) == prevClean)
        {
            int removed = 0;
            foreach (var kv in _cmdRate)
                if (now - kv.Value.windowStartTick >= windowMs * 2)
                    if (_cmdRate.TryRemove(kv.Key, out _)) removed++;
            if (removed > 0) Interlocked.Add(ref _cmdRateCount, -removed);
        }

        // Q4: usar > en lugar de <= para enforcement estricto del límite:
        // con <= CommandRateLimit, dos threads concurrentes con count=limit ambos pasan (TOCTOU)
        // B1: usar < (estricto) en lugar de <= - con <=, dos threads concurrentes con count=limit ambos pasan (TOCTOU)
        return state.count < CommandRateLimit;
    }

    private static bool FixedTimeEquals(string? a, string? b)
    {
        if (a is null || b is null) return false;
        // M10: stackalloc para PINs cortos (≤256 bytes UTF-8) — evita alloc en heap por cada auth.
        // CryptographicOperations.FixedTimeEquals acepta ReadOnlySpan, por lo que el timing-safety se mantiene.
        var maxBytes = Encoding.UTF8.GetMaxByteCount(Math.Max(a.Length, b.Length));
        Span<byte> ba = maxBytes <= 256 ? stackalloc byte[maxBytes] : new byte[maxBytes];
        Span<byte> bb = maxBytes <= 256 ? stackalloc byte[maxBytes] : new byte[maxBytes];
        var la = Encoding.UTF8.GetBytes(a, ba);
        var lb = Encoding.UTF8.GetBytes(b, bb);
        if (la != lb) return false;
        return CryptographicOperations.FixedTimeEquals(ba[..la], bb[..lb]);
    }

    // Progreso de transferencias del lado servidor: recepcion ('put') y envio ('get').
    // Receiving=true cuando este equipo recibe; false cuando envia.
    public readonly record struct TransferProgressInfo(bool Receiving, string FileName, long Done, long Total);
    public event Action<TransferProgressInfo>? TransferProgress;

    // idea-clipboard: texto/portapapeles recibido de un peer (ip, texto).
    public event Action<string, string>? TextReceived;
    public event Action<string>? DisconnectNoticeReceived;

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

    // S1: contraseña del PFX derivada del MachineGuid (única por máquina).
    // Así aunque alguien robe el archivo .pfx, no puede extraer la clave privada sin el GUID.
#pragma warning disable CA1416 // Registry solo en Windows — LanCopy solo soporta Windows
    private static string GetCertPassword()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Cryptography", false);
            var guid = key?.GetValue("MachineGuid") as string;
            if (!string.IsNullOrWhiteSpace(guid))
                return "LC-" + guid; // prefijo para distinguirlo de un GUID genérico
        }
        catch (Exception ex)
        {
            Log.Warn("server", "machine-guid-read-failed", new { error = ex.Message });
        }
        return "LC-lancopy-fallback"; // S1: fallback
    }
#pragma warning restore CA1416

    private void EnsureCert()
    {
        if (_serverCert != null) return;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CertPath)!);
        var certPwd = GetCertPassword(); // S1: contraseña derivada de la máquina
        if (File.Exists(CertPath))
        {
            try
            {
                // EphemeralKeySet: clave privada en memoria, sin almacén CNG de Windows.
                // Evita problemas de permisos en SChannel y no deja rastro en el sistema.
                _serverCert = X509CertificateLoader.LoadPkcs12FromFile(CertPath, certPwd,
                    X509KeyStorageFlags.EphemeralKeySet);
                return;
            }
            catch (Exception ex)
            {
                Log.Warn("server", "cert-load-failed", new { path = CertPath, error = ex.Message });
                // Cert corrupto o no válido — borrarlo y generar uno nuevo
                try { File.Delete(CertPath); }
                catch (Exception deleteEx)
                {
                    Log.Warn("server", "cert-delete-failed", new { path = CertPath, error = deleteEx.Message });
                }
            }
        }
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=LanCopy", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
        var pfxBytes = cert.Export(X509ContentType.Pfx, certPwd); // S1: contraseña única
        File.WriteAllBytes(CertPath, pfxBytes);
        _serverCert = X509CertificateLoader.LoadPkcs12(pfxBytes, certPwd,
            X509KeyStorageFlags.EphemeralKeySet);
    }

    // Auto-detecciÃ³n TLS: lee 1 byte del stream y lo "devuelve" para que SslStream lo reciba completo.
    private sealed class PrependByteStream(byte head, Stream inner) : Stream
    {
        private bool _used;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken ct) => inner.FlushAsync(ct);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!_used && count > 0) { buffer[offset] = head; _used = true; return 1; }
            return inner.Read(buffer, offset, count);
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (!_used && !buffer.IsEmpty) { buffer.Span[0] = head; _used = true; return 1; }
            return await inner.ReadAsync(buffer, ct);
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();
        public override void Write(byte[] b, int o, int c) => inner.Write(b, o, c);
        public override Task WriteAsync(byte[] b, int o, int c, CancellationToken ct) => inner.WriteAsync(b, o, c, ct);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) => inner.WriteAsync(buffer, ct);
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }

    public void Start(int port = 8742)
    {
        Port = port;
        LocalIp = ResolveLocalIp();
        if (RestrictToShareRoot) ShareRoot.EnsureRootExists();
        EnsureCert(); // Siempre genera/carga cert para auto-detecciÃ³n TLS
        _cts = new CancellationTokenSource();
        var bindAddr = IPAddress.Any;
        if (BindLanOnly && IPAddress.TryParse(LocalIp, out var lan) && lan.AddressFamily == AddressFamily.InterNetwork)
            bindAddr = lan;
        _listener = new TcpListener(bindAddr, port);
        _listener.Start();
        Log.Info("server", "started", new
        {
            port,
            ip = LocalIp,
            tls = TlsEnabled,
            restrictShareRoot = RestrictToShareRoot,
            readOnly = ReadOnly,
            safeModeNoRemoteDelete = SafeModeNoRemoteDelete,
            bindLanOnly = BindLanOnly
        });
        _ = AcceptLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }
        try { _cts?.Dispose(); } catch { }
        _cts = null;
        _listener = null;
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
            catch (Exception ex) { Log.Warn("server", "accept-loop-error", new { error = ex.Message }); }
        }
    }

    private async Task HandleWithTimeoutAsync(TcpClient tcp, CancellationToken serverCt)
    {
        var ip = (tcp.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "?";

        if (!await _connLimit.WaitAsync(0))
        {
            try { tcp.Dispose(); }
            catch (Exception ex) { Log.Debug("server", "reject-conn-dispose-failed", new { ip, error = ex.Message }); }
            return;
        }
        var count = _perIp.AddOrUpdate(ip, 1, (_, v) => v + 1);
        if (count > MaxPerIp)
        {
            _perIp.AddOrUpdate(ip, 0, (_, v) => Math.Max(0, v - 1));
            _connLimit.Release();
            try { tcp.Dispose(); }
            catch (Exception ex) { Log.Debug("server", "reject-ip-dispose-failed", new { ip, error = ex.Message }); }
            return;
        }
        // M8: incrementar contador O(1) cuando la IP pasa el filtro y es "activa"
        Interlocked.Increment(ref _activeIpCount);

        try 
        { 
            await HandleAsync(tcp, ip, serverCt); 
        }
        catch (Exception ex) 
        { 
            Log.Warn("server", "handle-timeout-error", new { ip, error = ex.Message }); 
            try { tcp.Dispose(); }
            catch (Exception disposeEx) { Log.Debug("server", "handle-error-dispose-failed", new { ip, error = disposeEx.Message }); }
        }
        finally
        {
            // BUG-FIX #5: Mejorado finally para garantizar cleanup incluso si excepciÃ³n en desincronizaciÃ³n
            try
            {
                _perIp.AddOrUpdate(ip, 0, (_, v) => Math.Max(0, v - 1));
                // M8: decrementar cuando la IP desconecta
                if (Interlocked.Decrement(ref _activeIpCount) < 0) Interlocked.Exchange(ref _activeIpCount, 0);
                _connLimit.Release();
            }
            catch (Exception ex)
            {
                Log.Error("server", "handle-cleanup-error", new { ip, error = ex.Message });
            }
        }
    }

    private async Task HandleAsync(TcpClient tcp, string ip, CancellationToken ct)
    {
        using (tcp)
        {
            ConfigureClientSocket(tcp);
            using var hsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            hsCts.CancelAfter(TimeSpan.FromSeconds(60));
            var hs = hsCts.Token;
            SslStream? sslToDispose = null;
            try
            {
                // Feature 9: TLS â€” auto-detecta TLS leyendo el primer byte del stream
                Stream stream = tcp.GetStream();
                if (_serverCert != null)
                {
                    // Leer 1 byte para detectar TLS (0x16 = TLS ClientHello)
                    var firstByte = new byte[1];
                    int n = await stream.ReadAsync(firstByte.AsMemory(), hs);
                    if (n <= 0) return;

                    var replayed = new PrependByteStream(firstByte[0], stream);
                        if (firstByte[0] == 0x16) // TLS handshake record
                        {
                            var ssl = new SslStream(replayed, leaveInnerStreamOpen: false);
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
                        else
                        {
                            stream = replayed; // Texto plano: devolver el byte al flujo
                        }
                }

                // Lectura de cabeceras con buffer (evita 1 syscall por byte) sin consumir el payload.
                stream = new BufferedLineStream(stream);

                // Feature 10: autenticaciÃ³n PIN si estÃ¡ configurado
                // F9+B5: auto-limpiar PIN expirado bajo lock para evitar race con SetTemporaryPin
                string? pinSnapshot;
                lock (_pinLock)
                {
                    // Limpiar PIN por inactividad si ha expirado el tiempo de inactividad
                    if (PinIdleExpiry > TimeSpan.Zero && (DateTimeOffset.UtcNow - _lastAuthActivity) > PinIdleExpiry)
                    {
                        RequiredPin = null;
                        PinExpiresAt = null;
                    }
                    // B4: limpiar expirado Y leer el PIN en el mismo lock → sin race con SetTemporaryPin
                    if (PinExpiresAt.HasValue && DateTimeOffset.UtcNow > PinExpiresAt.Value)
                    {
                        RequiredPin = null;
                        PinExpiresAt = null;
                    }
                    pinSnapshot = RequiredPin;
                }
                if (!string.IsNullOrEmpty(pinSnapshot))
                {
                    // Rate-limit por IP: tras varios fallos, rechazar durante un backoff.
                    if (_pinFails.TryGetValue(ip, out var st) && st.fails >= PinMaxFails
                        && Environment.TickCount64 < st.untilTick)
                    {
                        await Protocol.WriteLineAsync(stream,
                            JsonSerializer.Serialize(new { status = "error", error = "svc.tooManyAttempts" }), ct);
                        return;
                    }

                    var authLine = await Protocol.ReadLineAsync(stream, hs);
                    var authReq = JsonSerializer.Deserialize<JsonElement>(authLine);
                    var authCmd = authReq.TryGetProperty("cmd", out var cmdEl) ? cmdEl.GetString() : null;
                    var authPin = authReq.TryGetProperty("pin", out var pinEl) ? pinEl.GetString() : null;

                    // Comparacion en tiempo constante (evita timing attacks).
                    var okPin = authCmd == "auth" && FixedTimeEquals(authPin, pinSnapshot); // B4: usa snapshot leído bajo lock
                    if (!okPin)
                    {
                        var nf = _pinFails.AddOrUpdate(
                            ip,
                            _ => (1, Environment.TickCount64 + PinBackoffMs),
                            (_, v) =>
                            {
                                var fails = v.fails + 1;
                                var shift = Math.Min(fails - 1, 20);
                                var backoff = Math.Min(PinMaxBackoffMs, PinBackoffMs * (1L << shift));
                                return (fails, Environment.TickCount64 + backoff);
                            });
                        // Poda entradas expiradas para que IPs falsificadas no acumulen memoria.
                        if (_pinFails.Count > 4096)
                            foreach (var kv in _pinFails)
                                if (Environment.TickCount64 > kv.Value.untilTick) _pinFails.TryRemove(kv.Key, out _);
                        SafeFileOps.Audit("auth", ip, "blocked", $"pin-fail #{nf.fails}", "remote");
                        await Protocol.WriteLineAsync(stream,
                            JsonSerializer.Serialize(new { status = "error", error = "svc.badPin" }), ct);
                        return;
                    }
                    _pinFails.TryRemove(ip, out _);
                    _lastAuthActivity = DateTimeOffset.UtcNow; // refresh idle timer
                    await Protocol.WriteOkAsync(stream, ct); // P1: bytes pre-calculados
                }

                var line = await Protocol.ReadLineAsync(stream, hs);
                var req = JsonSerializer.Deserialize<JsonElement>(line);
                // Usar TryGetProperty: GetProperty lanza KeyNotFoundException si falta "cmd".
                var cmd = req.TryGetProperty("cmd", out var cmdProp) ? cmdProp.GetString() ?? "" : "";
                if (!IsCommandRateAllowed(ip, cmd))
                {
                    await Protocol.WriteErrorAsync(stream, "svc.rateLimited", ct); // P1: bytes cacheados
                    SafeFileOps.Audit("rate", ip, "blocked", $"cmd:{cmd}", "remote");
                    return;
                }

                switch (cmd)
                {
                    case "list":
                        bool recursive = req.TryGetProperty("recursive", out var rv) && rv.GetBoolean();
                        if (recursive) await HandleListRecursiveAsync(req, stream, ct);
                        else await HandleListAsync(req, stream, ct);
                        break;
                    case "get": await HandleGetAsync(req, stream, ct); break;
                    case "put":
                    case "put_resume":
                    case "delete":
                    case "rename":
                    case "mkdir":
                    case "put_delta_blocks":
                        if (ReadOnly)
                        {
                            await Protocol.WriteErrorAsync(stream, "svc.readOnly", ct); // P1: bytes cacheados
                            break;
                        }
                        if (cmd == "put") await HandlePutAsync(req, stream, ip, ct);
                        else if (cmd == "put_resume") await HandlePutResumeAsync(req, stream, ip, ct);
                        else if (cmd == "delete") await HandleDeleteAsync(req, stream, ct);
                        else if (cmd == "rename") await HandleRenameAsync(req, stream, ct);
                        else if (cmd == "put_delta_blocks") await HandlePutDeltaBlocksAsync(req, stream, ip, ct);
                        else await HandleMkdirAsync(req, stream, ct);
                        break;
                    case "get_chunk": await HandleGetChunkAsync(req, stream, ct); break;
                    case "delta_hashes": await HandleDeltaHashesAsync(req, stream, ct); break;
                    case "power": await HandlePowerAsync(req, stream, ct); break;
                    case "search": await HandleSearchAsync(req, stream, ct); break;
                    case "sha1": await HandleSha1Async(req, stream, ct); break;
                    case "sha256": await HandleSha256Async(req, stream, ct); break;
                    case "hash": await HandleHashAsync(req, stream, ct); break;
                    case "stat": await HandleStatAsync(req, stream, ct); break;
                    case "caps": await HandleCapsAsync(req, stream, ct); break;
                    case "text": await HandleTextAsync(req, stream, ip, ct); break;
                    case "disconnect_notice": await HandleDisconnectNoticeAsync(stream, ip, ct); break;
                    case "health": await HandleHealthAsync(req, stream, ct); break;
                    default:
                        await Protocol.WriteErrorAsync(stream, "svc.unknownCmd", ct); // P1: bytes cacheados
                        break;
                }
            }
            catch (Exception ex) { Log.Warn("server", "handler-error", new { ip, error = ex.Message }); }
            finally
            {
                if (sslToDispose != null)
                {
                    try { await sslToDispose.DisposeAsync(); }
                    catch (Exception ex) { Log.Debug("server", "ssl-dispose-failed", new { ip, error = ex.Message }); }
                }
            }
        }
    }

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
                AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System,
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
            // Q5: loguear en lugar de swallow silencioso — el cliente recibe lista parcial con status:ok
            Log.Warn("server", "list-recursive-error", new { error = ex.Message });
        }
        // M2: WriteLineJsonAsync evita string UTF-16 intermedia (listas recursivas pueden tener 100K entradas)
        await Protocol.WriteLineJsonAsync(stream, new { status = "ok", entries }, ct);
    }

    // Feature 9: capabilities â€” anuncia soporte de compresiÃ³n y TLS al cliente
    private async Task HandleCapsAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new
            {
                status = "ok",
                compress = true,
                tls = TlsEnabled,
                version = Protocol.Version,
                minVersion = Protocol.MinSupportedVersion,
                readOnly = ReadOnly,
                safeModeNoRemoteDelete = SafeModeNoRemoteDelete,
                text = true
            }), ct);
    }

    private async Task HandleHealthAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        // M8: usar _activeIpCount O(1) en lugar de _perIp.Count(predicate) LINQ O(n)
        var activeIps = Interlocked.CompareExchange(ref _activeIpCount, 0, 0);
        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new
        {
            status = "ok",
            connCurrent = MaxConnections - _connLimit.CurrentCount,
            connLimit = MaxConnections, // Q6: usar constante
            perIpLimit = MaxPerIp,
            activeIps,
            pinFailsTracked = _pinFails.Count,
            hashCacheEntries = _sha256Cache.Count,
            commandRateTracked = _cmdRate.Count,
            commandRateLimit = CommandRateLimit,
            commandRateWindowSeconds = CommandRateWindowSeconds
        }), ct);
    }

    // idea-clipboard: recibe un texto corto (<=256 KB) y lo notifica (la UI lo copia al portapapeles).
    private const int MaxTextBytes = 256 * 1024;
    private async Task HandleTextAsync(JsonElement req, Stream stream, string ip, CancellationToken ct)
    {
        var text = req.TryGetProperty("text", out var tEl) ? tEl.GetString() ?? "" : "";
        if (Encoding.UTF8.GetByteCount(text) > MaxTextBytes)
        {
            await Protocol.WriteErrorAsync(stream, "svc.textTooLong", ct); // P1: bytes cacheados
            return;
        }
        // S5: filtro BiDi completo usando Runas (soporta emojis y previene surrogates huérfanos)
        var safeBuilder = new System.Text.StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            var v = rune.Value;
            if (v < 0x20) continue; // Control ASCII
            if (v >= 0xE000 && v <= 0xF8FF) continue; // PUA (Private Use Area)
            if (v == 0x202E || v == 0x202D) continue; // RLO / LRO
            if (v == 0x200F || v == 0x200E) continue; // RLM / LRM
            if (v == 0x2066 || v == 0x2067 || v == 0x2068) continue; // LRI/RLI/FSI
            if (v == 0x2028 || v == 0x2029) continue; // Line/Para Separator
            if (v == 0xFEFF) continue; // BOM
            if (v == 0x200B || v == 0x200C || v == 0x200D) continue; // ZWS/ZWNJ/ZWJ
            if (v == 0x202A || v == 0x202B || v == 0x202C) continue; // LRE/RLE/PDF
            if (v == 0x2069) continue; // PDI
            safeBuilder.Append(rune);
        }
        var safeText = safeBuilder.ToString();
        try { TextReceived?.Invoke(ip, safeText); } // S5: bidi chars filtrados (completo)
        catch (Exception ex) { Log.Warn("server", "text-received-handler-error", new { ip, error = ex.Message }); }
        await Protocol.WriteOkAsync(stream, ct); // P1: bytes pre-calculados
    }

    private async Task HandleDisconnectNoticeAsync(Stream stream, string ip, CancellationToken ct)
    {
        try { DisconnectNoticeReceived?.Invoke(ip); } catch (Exception ex) { Log.Warn("server", "disconnect-notice-handler-error", new { ip, error = ex.Message }); }
        await Protocol.WriteOkAsync(stream, ct); // P1: bytes pre-calculados
    }

    // Feature 2: lÃ­mite de compresiÃ³n en memoria (200 MB)
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
                // Drenar el cuerpo para que el cliente termine de escribir y reciba el ack limpio
                // (sin esto, el cierre con bytes sin leer provoca un RST que pierde el error).
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
        var key = $"remote-delete:{normalized}";
        if (SafeFileOps.IsOnCooldown(key, 2))
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
            else if (File.Exists(normalized)) File.Delete(normalized);
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

    private static string ResolveLocalIp()
    {
        var allIpv4s = new List<string>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback) continue;

            try
            {
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    var addr = ua.Address;
                    if (addr.AddressFamily != AddressFamily.InterNetwork) continue;
                    
                    var ipStr = addr.ToString();
                    allIpv4s.Add(ipStr);

                    var b = addr.GetAddressBytes();
                    if (b[0] == 10) return ipStr;
                    if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return ipStr;
                    if (b[0] == 192 && b[1] == 168) return ipStr;
                }
            }
            catch { }
        }

        if (allIpv4s.Count > 0)
        {
            return allIpv4s[0];
        }

        try
        {
            using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.Connect("192.168.1.1", 53);
            return ((IPEndPoint)s.LocalEndPoint!).Address.ToString();
        }
        catch (Exception ex)
        {
            Log.Debug("server", "resolve-local-ip-fallback-localhost", new { error = ex.Message });
            return "127.0.0.1";
        }
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
        if (!TryGetStringProperty(req, "action", out var action)) { await WriteBadRequestAsync(stream, ct); return; }

        await Protocol.WriteOkAsync(stream, ct); // P1: bytes pre-calculados

        // Apagar en segundo plano para dar tiempo a enviar el ACK TCP
        _ = Task.Run(async () =>
        {
            await Task.Delay(1000);
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var cmd = action == "reboot" ? "/r /f /t 0" : "/s /f /t 0";
                    Process.Start(new ProcessStartInfo("shutdown", cmd) { CreateNoWindow = true, UseShellExecute = false });
                }
                else if (OperatingSystem.IsLinux())
                {
                    var cmd = action == "reboot" ? "systemctl reboot" : "systemctl poweroff";
                    Process.Start(new ProcessStartInfo("sh", $"-c \"{cmd}\"") { CreateNoWindow = true, UseShellExecute = false });
                }
                else // macOS / Unix fallback
                {
                    var cmd = action == "reboot" ? "reboot" : "shutdown -h now";
                    Process.Start(new ProcessStartInfo("sh", $"-c \"{cmd}\"") { CreateNoWindow = true, UseShellExecute = false });
                }
            }
            catch (Exception ex)
            {
                Log.Warn("server", "power-action-failed", new { action, error = ex.Message });
            }
        });
    }

    private async Task HandleSearchAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        if (!TryGetStringProperty(req, "path", out var reqPath)) { await WriteBadRequestAsync(stream, ct); return; }
        if (!TryGuardRead(reqPath, out var basePath, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }
        if (!TryGetStringProperty(req, "query", out var query) || string.IsNullOrWhiteSpace(query))
        {
            await WriteBadRequestAsync(stream, ct);
            return;
        }

        if (!Directory.Exists(basePath))
        {
            await Protocol.WriteErrorAsync(stream, "svc.dirNotFound", ct); // P1: bytes cacheados
            return;
        }

        var results = new List<FileEntry>();
        try
        {
            var options = new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint, RecurseSubdirectories = true };
            var dirInfo = new DirectoryInfo(basePath);

            // Búsqueda insensible a mayúsculas
            foreach (var f in dirInfo.EnumerateFileSystemInfos("*", options))
            {
                if (f.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    var isDir = f is DirectoryInfo;
                    var size = isDir ? 0 : ((FileInfo)f).Length;
                    
                    results.Add(new FileEntry
                    {
                        Name = Path.GetRelativePath(basePath, f.FullName).Replace('\\', '/'),
                        FullPath = f.FullName,
                        IsDirectory = isDir,
                        Size = size,
                        LastWriteUtcTicks = f.LastWriteTimeUtc.Ticks
                    });

                    if (results.Count >= 250) break; // Limitar resultados para evitar DoS por OOM
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn("server", "search-failed", new { path = basePath, query, error = ex.Message });
        }

        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", results }), ct);
    }

    // IAsyncDisposable implementation for proper async resource cleanup
    public ValueTask DisposeAsync() // Q2: no async work, removed state machine
    {
        Stop(); // Stop() cancela y dispone _cts — no redundar aqui (Q2: double dispose removed)
        _listener?.Dispose();
        _serverCert?.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
