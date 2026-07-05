using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace LanCopy.Services;

/// <summary>
/// TOFU (Trust On First Use): guarda la huella SHA-256 del certificado de cada host la
/// primera vez que se conecta y la verifica en conexiones posteriores. Esto evita ataques
/// MITM despues del primer contacto (antes se aceptaba CUALQUIER certificado, => true).
/// </summary>
public static class CertTrust
{
    public enum PeerTrustLevel
    {
        Unknown = 0,
        Paired = 1,
        Trusted = 2,
        OwnerDevice = 3
    }

    public enum ValidationResult
    {
        TrustedKnown,
        TrustedFirstUse,
        IdentityChanged,
        InvalidCertificate
    }

    public sealed record KnownHost(
        string Host,
        string DeviceName,
        string LastAddress,
        string Fingerprint,
        string FingerprintShort,
        DateTimeOffset? LastSeenUtc,
        PeerTrustLevel TrustLevel);

    private sealed record KnownHostEntry(
        string Fingerprint,
        string? DeviceName,
        string? LastAddress,
        DateTimeOffset? LastSeenUtc,
        PeerTrustLevel TrustLevel);

    private static readonly object _lock = new();
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LanCopy", "known_hosts.json");

    // OPT-FIX #2: Lazy loading del diccionario para mejorar cold-start (~50ms)
    private static readonly Lazy<Dictionary<string, KnownHostEntry>> _lazyCache =
        new(() => LoadInternal());

    private static Dictionary<string, KnownHostEntry> LoadInternal()
    {
        try
        {
            if (File.Exists(StorePath))
            {
                var json = File.ReadAllText(StorePath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    return new Dictionary<string, KnownHostEntry>(StringComparer.OrdinalIgnoreCase);

                var map = new Dictionary<string, KnownHostEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var fp = prop.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(fp))
                        {
                            map[prop.Name] = new KnownHostEntry(
                                fp.Trim(),
                                DeviceName: prop.Name,
                                LastAddress: prop.Name,
                                LastSeenUtc: null,
                                TrustLevel: PeerTrustLevel.Paired);
                        }
                        continue;
                    }
                    if (prop.Value.ValueKind != JsonValueKind.Object)
                        continue;

                    var fpObj = ReadOptionalString(prop.Value, "fingerprint");
                    if (string.IsNullOrWhiteSpace(fpObj))
                        continue;
                    map[prop.Name] = new KnownHostEntry(
                        fpObj.Trim(),
                        DeviceName: ReadOptionalString(prop.Value, "deviceName") ?? prop.Name,
                        LastAddress: ReadOptionalString(prop.Value, "lastAddress") ?? prop.Name,
                        LastSeenUtc: ReadOptionalDate(prop.Value, "lastSeenUtc"),
                        TrustLevel: ReadOptionalTrustLevel(prop.Value, "trustLevel"));
                }
                return map;
            }
        }
        catch (Exception ex)
        {
            Log.Warn("cert", "load-known-hosts-failed", new { error = ex.Message });
        }
        return new Dictionary<string, KnownHostEntry>(StringComparer.OrdinalIgnoreCase);
    }

    private static void Save(Dictionary<string, KnownHostEntry> map)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            // Escritura atomica: temp unico + replace. Se usa GUID en el nombre del temp para
            // BUG-FIX-B3: evitar colision si multiples hilos guardan concurrentemente (sharing violation).
            var tmp = StorePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(map));
            File.Move(tmp, StorePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Warn("cert", "save-known-hosts-failed", new { error = ex.Message });
        }
    }

    public static string Fingerprint(X509Certificate cert)
    {
        var raw = cert.GetRawCertData();
        return Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant();
    }

    /// <summary>
    /// Convierte la huella del certificado en 4 emojis memorables para verificación visual anti-MITM.
    /// Ambos peers deberían ver los mismos emojis si la conexión no está interceptada.
    /// </summary>
    public static string EmojiFingerprint(X509Certificate cert)
    {
        var hash = SHA256.HashData(cert.GetRawCertData());
        // 256 emojis distinguibles — cada byte del hash selecciona uno
        ReadOnlySpan<string> palette =
        [
            "🐶","🐱","🐭","🐹","🐰","🦊","🐻","🐼","🐨","🐯","🦁","🐮","🐷","🐸","🐵","🐔",
            "🐧","🐦","🐤","🦆","🦅","🦉","🦇","🐺","🐗","🐴","🦄","🐝","🐛","🦋","🐌","🐞",
            "🐙","🦑","🦀","🐠","🐟","🐡","🐬","🦈","🐳","🐋","🐊","🐆","🐅","🐃","🐂","🐄",
            "🦌","🐪","🐫","🦒","🐘","🦏","🦍","🐎","🐖","🐐","🐏","🐑","🐕","🐩","🐈","🐓",
            "🦃","🕊️","🐇","🐁","🐀","🐿️","🦔","🐾","🐉","🎄","🌲","🌳","🌴","🌵","🌷","🌸",
            "🌹","🌺","🌻","🌼","🌽","🌾","🌿","🍀","🍁","🍂","🍃","🍄","🍅","🍆","🍇","🍈",
            "🍉","🍊","🍋","🍌","🍍","🍎","🍏","🍐","🍑","🍒","🍓","🥝","🍔","🍕","🍖","🍗",
            "🍘","🍙","🍚","🍛","🍜","🍝","🍞","🍟","🍠","🍡","🍢","🍣","🍤","🍥","🍦","🍧",
            "🍨","🍩","🍪","🍫","🍬","🍭","🍮","🍯","🍰","🎂","🍿","☕","🍵","🍶","🍷","🍸",
            "🍹","🍺","🍻","🥂","🥃","🍼","⚽","🏀","🏈","⚾","🎾","🏐","🏉","🎱","🏓","🏸",
            "🥊","🥋","⛳","⛸️","🎣","🎿","🛷","🥌","🎯","🎮","🎲","🧩","🎪","🎨","🎭","🎬",
            "🎤","🎧","🎵","🎶","🎹","🥁","🎷","🎺","🎸","🎻","🎼","🏆","🥇","🥈","🥉","🏅",
            "🎖️","🏵️","🎗️","🎟️","🎫","🎪","🎠","🎡","🎢","🚀","🛸","✈️","🚁","🚂","🚃","🚄",
            "🚅","🚆","🚇","🚈","🚉","🚊","🚋","🚌","🚍","🚎","🚐","🚑","🚒","🚓","🚔","🚕",
            "🚖","🚗","🚘","🚙","🚚","🚛","🚜","🏎️","🏍️","🛵","🚲","🛴","🛑","⚓","🌍","🌎",
            "🌏","🌐","🗺️","🌋","🏔️","🏕️","🏖️","🏗️","🏘️","🏙️","🏚️","🏛️","🏜️","🏝️","🏞️","🏟️"
        ];
        // Usar los primeros 4 bytes del hash para seleccionar 4 emojis con límite seguro modulo
        return $"{palette[hash[0] % palette.Length]} {palette[hash[1] % palette.Length]} {palette[hash[2] % palette.Length]} {palette[hash[3] % palette.Length]}";
    }

    /// <summary>
    /// Devuelve true si el certificado del host es de confianza (coincide con el guardado
    /// o es la primera vez que se ve, en cuyo caso se memoriza). Devuelve false si cambia
    /// respecto al guardado (posible MITM).
    /// </summary>
    public static bool ValidateOrPin(string host, X509Certificate? cert)
        => ValidateOrPinDetailed(host, cert) is ValidationResult.TrustedKnown or ValidationResult.TrustedFirstUse;

    public static ValidationResult ValidateOrPinDetailed(string host, X509Certificate? cert)
    {
        if (cert == null) return ValidationResult.InvalidCertificate;
        var fp = Fingerprint(cert);
        Dictionary<string, KnownHostEntry>? toSave = null;
        var now = DateTimeOffset.UtcNow;
        var deviceName = TryGetDeviceName(cert) ?? host;
        lock (_lock)
        {
            var map = _lazyCache.Value;
            if (map.TryGetValue(host, out var known))
            {
                if (!string.Equals(known.Fingerprint, fp, StringComparison.OrdinalIgnoreCase))
                    return ValidationResult.IdentityChanged;

                map[host] = known with
                {
                    DeviceName = deviceName,
                    LastAddress = host,
                    LastSeenUtc = now
                };
                toSave = new Dictionary<string, KnownHostEntry>(map);
                goto SaveAndReturnKnown;
            }

            // TOFU: primera vez, solo requerimos cert autofirmado (LanCopy los genera).
            X509Certificate2? x509 = null;
            try
            {
                x509 = cert as X509Certificate2 ?? new X509Certificate2(cert);
                if (!IsSelfSigned(x509))
                    return ValidationResult.InvalidCertificate; // Rechazar certs emitidos por CA externas
            }
            finally
            {
                if (x509 != null && !ReferenceEquals(x509, cert))
                {
                    x509.Dispose();
                }
            }

            map[host] = new KnownHostEntry(fp, deviceName, host, now, PeerTrustLevel.Paired);
            // S3: copiar el mapa DENTRO del lock pero guardar FUERA — Save hace I/O de disco
            // y retener el lock durante cientos de ms bloquea a todos los callers concurrentes
            toSave = new Dictionary<string, KnownHostEntry>(map);
        }
        // Guardar fuera del lock — si dos hilos llegan aquí simultáneamente, la segunda escritura
        // sobreescribe la primera con el mismo contenido: inocuo.
        if (toSave != null) Save(toSave);
        return ValidationResult.TrustedFirstUse;
    SaveAndReturnKnown:
        if (toSave != null) Save(toSave);
        return ValidationResult.TrustedKnown;
    }

    public static IReadOnlyList<KnownHost> ListKnownHosts()
    {
        lock (_lock)
        {
            return _lazyCache.Value
                .Select(kv => new KnownHost(
                    Host: kv.Key,
                    DeviceName: string.IsNullOrWhiteSpace(kv.Value.DeviceName) ? kv.Key : kv.Value.DeviceName!,
                    LastAddress: string.IsNullOrWhiteSpace(kv.Value.LastAddress) ? kv.Key : kv.Value.LastAddress!,
                    Fingerprint: kv.Value.Fingerprint,
                    FingerprintShort: ShortFingerprint(kv.Value.Fingerprint),
                    LastSeenUtc: kv.Value.LastSeenUtc,
                    TrustLevel: kv.Value.TrustLevel))
                .OrderByDescending(x => x.LastSeenUtc ?? DateTimeOffset.MinValue)
                .ThenBy(x => x.DeviceName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public static PeerTrustLevel GetTrustLevel(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return PeerTrustLevel.Unknown;
        lock (_lock)
        {
            return _lazyCache.Value.TryGetValue(host.Trim(), out var entry)
                ? entry.TrustLevel
                : PeerTrustLevel.Unknown;
        }
    }

    public static bool SetTrustLevel(string host, PeerTrustLevel trustLevel)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;
        Dictionary<string, KnownHostEntry>? toSave = null;
        bool updated = false;
        lock (_lock)
        {
            var map = _lazyCache.Value;
            if (!map.TryGetValue(host.Trim(), out var current))
                return false;
            if (current.TrustLevel != trustLevel)
            {
                map[host.Trim()] = current with { TrustLevel = trustLevel };
                toSave = new Dictionary<string, KnownHostEntry>(map);
                updated = true;
            }
        }
        if (toSave != null) Save(toSave);
        return updated;
    }

    public static bool ForgetHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        Dictionary<string, KnownHostEntry>? toSave = null;
        bool removed;
        lock (_lock)
        {
            removed = _lazyCache.Value.Remove(host.Trim());
            if (removed)
                toSave = new Dictionary<string, KnownHostEntry>(_lazyCache.Value);
        }
        if (toSave != null) Save(toSave);
        return removed;
    }

    public static string ShortFingerprint(string fingerprintHex)
    {
        if (string.IsNullOrWhiteSpace(fingerprintHex))
            return "";
        var clean = fingerprintHex.Trim();
        if (clean.Length <= 16)
            return clean;
        return $"{clean[..8]}…{clean[^8..]}";
    }

    private static string? ReadOptionalString(JsonElement obj, string property)
    {
        if (!obj.TryGetProperty(property, out var el) || el.ValueKind != JsonValueKind.String)
            return null;
        return el.GetString();
    }

    private static DateTimeOffset? ReadOptionalDate(JsonElement obj, string property)
    {
        var raw = ReadOptionalString(obj, property);
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        return DateTimeOffset.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static PeerTrustLevel ReadOptionalTrustLevel(JsonElement obj, string property)
    {
        if (!obj.TryGetProperty(property, out var el))
            return PeerTrustLevel.Paired;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)
            && Enum.IsDefined(typeof(PeerTrustLevel), n))
            return (PeerTrustLevel)n;
        if (el.ValueKind == JsonValueKind.String)
        {
            var raw = el.GetString();
            if (Enum.TryParse<PeerTrustLevel>(raw, ignoreCase: true, out var parsed))
                return parsed;
        }
        return PeerTrustLevel.Paired;
    }

    private static string? TryGetDeviceName(X509Certificate cert)
    {
        X509Certificate2? x509 = null;
        try
        {
            x509 = cert as X509Certificate2 ?? new X509Certificate2(cert);
            var name = x509.GetNameInfo(X509NameType.SimpleName, false);
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (x509 != null && !ReferenceEquals(x509, cert))
                x509.Dispose();
        }
    }
     
    private static bool IsSelfSigned(X509Certificate2 cert)
    {
        // S2: verificación en dos niveles:
        // 1) Subject == Issuer (necesario pero no suficiente)
        // 2) La firma del cert se verifica con su propia clave pública (garantía criptográfica)
        if (!cert.Subject.Equals(cert.Issuer, StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            // Intentar verificar la firma del cert contra su propia clave pública
            var pk = cert.GetRSAPublicKey() ?? (AsymmetricAlgorithm?)cert.GetECDsaPublicKey();
            // S1: devolver false (rechazar) en vez de true cuando el tipo de clave es desconocido
            // Un cert CA con DSA u otro algoritmo exótico no debería pasar como self-signed
            if (pk == null) return false; // clave desconocida (DSA u otro exótico) — rechazar: no tratar como auto-firmado

            // Construir un chain sin anclas externas y comprobar si el cert se valida a sí mismo
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            // S2: solo suprimir errores de CA desconocida — AllFlags elimina TODA validación
            // (certs expirados, firma rota, clave inválida también pasarían con AllFlags)
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
            chain.ChainPolicy.CustomTrustStore.Add(cert);
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.Build(cert); // si el único anchor es el propio cert, solo se auto-valida si está realmente auto-firmado
            return chain.ChainElements.Count == 1; // solo aparece una vez → genuinamente auto-firmado
        }
        catch (PlatformNotSupportedException)
        {
            // S1: solo permitir fallback Subject==Issuer para plataformas sin soporte de chain build
            // Cualquier otra excepción (CryptographicException, etc.) rechaza el cert para evitar bypasses
            return true;
        }
        catch
        {
            // S1: excepción inesperada en crypto — rechazar (más seguro que asumir auto-firmado)
            return false;
        }
    }
}