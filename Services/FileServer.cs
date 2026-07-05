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

public sealed partial class FileServer : IAsyncDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private X509Certificate2? _serverCert; // Feature 9: TLS
    private PowerCommandHandler? _powerHandler;

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
    // Por defecto, nunca hacer hard-delete remoto. Solo permitirlo con opt-in explícito.
    public bool AllowRemoteHardDelete { get; set; }
    // Seguridad por defecto: comandos de apagado/reinicio remotos deshabilitados salvo opt-in explícito.
    public bool RemotePowerEnabled { get; set; }

    // Si true, escucha solo en la IP LAN detectada en vez de IPAddress.Any (reduce exposicion).
    public bool BindLanOnly { get; set; }

    // Consentimiento del receptor: si se asigna, se invoca antes de aceptar cada fichero entrante.
    // Devolver false rechaza la transferencia. Si es null, se aceptan automaticamente (compatibilidad).
    public readonly record struct IncomingTransfer(string Ip, string FileName, long Size);
    public Func<IncomingTransfer, CancellationToken, Task<bool>>? ApproveIncoming { get; set; }
    // Confirmación local adicional para comandos de alto riesgo.
    public readonly record struct HighRiskCommand(string Ip, string Command, string? Path, string? Action);
    public Func<HighRiskCommand, CancellationToken, Task<bool>>? ApproveHighRisk { get; set; }
    // Autorización por peer/command. Si se asigna y devuelve false, se bloquea el comando.
    public Func<string, string, bool>? AuthorizePeerCommand { get; set; }

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

    // Chat recibido de un peer (ip, texto).
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
                var keyFlags = GetCertificateKeyStorageFlags();
                _serverCert = X509CertificateLoader.LoadPkcs12FromFile(CertPath, certPwd, keyFlags);
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
            GetCertificateKeyStorageFlags());
    }

    private static X509KeyStorageFlags GetCertificateKeyStorageFlags()
    {
        // SslStream server auth can fail with ephemeral private keys on some desktop runtimes.
        // Keep the PFX password-protected on disk, but import it into the user's key set for use.
        return X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet;
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
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Log.Info("server", "started", new
        {
            port = Port,
            ip = LocalIp,
            tls = TlsEnabled,
            restrictShareRoot = RestrictToShareRoot,
            readOnly = ReadOnly,
            safeModeNoRemoteDelete = SafeModeNoRemoteDelete,
            allowRemoteHardDelete = AllowRemoteHardDelete,
            remotePowerEnabled = RemotePowerEnabled,
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
                            if (TlsEnabled)
                            {
                                await Protocol.WriteErrorAsync(replayed, "svc.tlsRequired", hs);
                                return;
                            }
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
                if (!await AuthorizeCommandAsync(req, cmd, ip, stream, ct))
                    return;

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
                        SafeFileOps.Audit("unknown-command", ip, "blocked", cmd, "remote");
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


    internal static int ScoreLocalIpCandidate(
        IPAddress address,
        NetworkInterfaceType interfaceType,
        bool hasGateway,
        string adapterName,
        string adapterDescription)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork) return int.MinValue;

        var bytes = address.GetAddressBytes();
        if (bytes[0] == 127) return int.MinValue;

        var score = 0;
        var isApipa = bytes[0] == 169 && bytes[1] == 254;
        if (isApipa) score -= 200;

        var isPrivate = bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
        if (isPrivate) score += 100;

        if (hasGateway) score += 80;
        if (interfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211) score += 50;
        if (!IsLikelyVirtualAdapter(adapterName, adapterDescription)) score += 40;
        if (bytes[0] == 192 && bytes[1] == 168) score += 10;

        return score;
    }

    private static bool IsLikelyVirtualAdapter(string name, string description)
    {
        var text = (name + " " + description).ToLowerInvariant();
        return text.Contains("virtual", StringComparison.Ordinal)
            || text.Contains("hyper-v", StringComparison.Ordinal)
            || text.Contains("vmware", StringComparison.Ordinal)
            || text.Contains("virtualbox", StringComparison.Ordinal)
            || text.Contains("bluetooth", StringComparison.Ordinal)
            || text.Contains("tunnel", StringComparison.Ordinal)
            || text.Contains("tap", StringComparison.Ordinal)
            || text.Contains("wsl", StringComparison.Ordinal);
    }

    private static bool HasUsableIpv4Gateway(IPInterfaceProperties properties)
    {
        foreach (var gateway in properties.GatewayAddresses)
        {
            if (gateway.Address.AddressFamily == AddressFamily.InterNetwork
                && !IPAddress.Any.Equals(gateway.Address))
            {
                return true;
            }
        }
        return false;
    }

    private static string ResolveLocalIp()
    {
        var candidates = new List<(string Ip, int Score)>();

        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback) continue;

            try
            {
                var props = ni.GetIPProperties();
                var hasGateway = HasUsableIpv4Gateway(props);
                foreach (var ua in props.UnicastAddresses)
                {
                    var addr = ua.Address;
                    if (addr.AddressFamily != AddressFamily.InterNetwork) continue;
                    var score = ScoreLocalIpCandidate(addr, ni.NetworkInterfaceType, hasGateway, ni.Name, ni.Description);
                    if (score == int.MinValue) continue;
                    candidates.Add((addr.ToString(), score));
                }
            }
            catch (Exception ex)
            {
                Log.Debug("server", "resolve-local-ip-adapter-skipped", new { adapter = ni.Name, error = ex.Message });
            }
        }

        if (candidates.Count > 0)
        {
            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
            return candidates[0].Ip;
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

    // Primera capa de autorización (antes de entrar en handlers): evita decisiones dispersas.
    private async Task<bool> AuthorizeCommandAsync(
        JsonElement req,
        string cmd,
        string ip,
        Stream stream,
        CancellationToken ct)
    {
        var authorizer = AuthorizePeerCommand ?? CommandAuthorizer.IsAllowed;
        if (!authorizer(ip, cmd))
        {
            var auditCmd = string.IsNullOrWhiteSpace(cmd) ? "unknown-command" : cmd;
            SafeFileOps.Audit(auditCmd, ip, "blocked", "peer-policy-denied", "remote");
            await Protocol.WriteErrorAsync(stream, "svc.accessDenied", ct);
            return false;
        }

        if (IsHighRiskCommand(cmd) && !TlsEnabled)
        {
            SafeFileOps.Audit(cmd, ip, "blocked", "tlsRequired", "remote");
            await Protocol.WriteErrorAsync(stream, "svc.tlsRequired", ct);
            return false;
        }

        // Power commands are handled by dedicated handler
        if (string.Equals(cmd, "power", StringComparison.Ordinal))
        {
            _powerHandler ??= new PowerCommandHandler(this, (host, command) => (AuthorizePeerCommand ?? CommandAuthorizer.IsAllowed)(host, command));
            return await _powerHandler.AuthorizeAsync(req, ip, stream, ct);
        }

        if (IsHighRiskCommand(cmd))
        {
            if (!await ApproveHighRiskCommandAsync(req, cmd, ip, stream, ct))
                return false;
        }
        return true;
    }

    private static bool IsHighRiskCommand(string cmd)
        => string.Equals(cmd, "delete", StringComparison.Ordinal)
        || string.Equals(cmd, "power", StringComparison.Ordinal)
        || string.Equals(cmd, "delta_hashes", StringComparison.Ordinal)
        || string.Equals(cmd, "put_delta_blocks", StringComparison.Ordinal);

    private async Task<bool> ApproveHighRiskCommandAsync(
        JsonElement req,
        string cmd,
        string ip,
        Stream stream,
        CancellationToken ct)
    {
        if (ApproveHighRisk is not { } approve)
            return true;

        string? path = req.TryGetProperty("path", out var pathEl) ? pathEl.GetString() : null;
        string? action = req.TryGetProperty("action", out var actionEl) ? actionEl.GetString() : null;
        bool ok;
        try
        {
            ok = await approve(new HighRiskCommand(ip, cmd, path, action), ct);
        }
        catch (Exception ex)
        {
            Log.Warn("server", "high-risk-approve-callback-failed", new { ip, cmd, error = ex.Message });
            ok = false;
        }

        if (ok)
        {
            SafeFileOps.Audit(cmd, ip, "ok", $"{cmd}.approved", "remote");
            return true;
        }
        SafeFileOps.Audit(cmd, ip, "blocked", $"{cmd}.rejected", "remote");
        await Protocol.WriteErrorAsync(stream, "svc.rejected", ct);
        return false;
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
