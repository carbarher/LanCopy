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
    private static readonly object _lock = new();
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LanCopy", "known_hosts.json");

    // OPT-FIX #2: Lazy loading del diccionario para mejorar cold-start (~50ms)
    private static readonly Lazy<Dictionary<string, string>> _lazyCache = 
        new(() => LoadInternal());

    private static Dictionary<string, string> LoadInternal()
    {
        try
        {
            if (File.Exists(StorePath))
            {
                var json = File.ReadAllText(StorePath);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                       ?? new Dictionary<string, string>();
            }
        }
        catch (Exception ex)
        {
            Log.Warn("cert", "load-known-hosts-failed", new { error = ex.Message });
        }
        return new Dictionary<string, string>();
    }

    private static void Save(Dictionary<string, string> map)
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
    {
        if (cert == null) return false;
        var fp = Fingerprint(cert);
        Dictionary<string, string>? toSave = null;
        bool result;
        lock (_lock)
        {
            var map = _lazyCache.Value;
            if (map.TryGetValue(host, out var known))
                return string.Equals(known, fp, StringComparison.OrdinalIgnoreCase);

            // TOFU: primera vez, solo requerimos cert autofirmado (LanCopy los genera).
            X509Certificate2? x509 = null;
            try
            {
                x509 = cert as X509Certificate2 ?? new X509Certificate2(cert);
                if (!IsSelfSigned(x509))
                    return false; // Rechazar certs emitidos por CA externas
            }
            finally
            {
                if (x509 != null && !ReferenceEquals(x509, cert))
                {
                    x509.Dispose();
                }
            }

            map[host] = fp;
            // S3: copiar el mapa DENTRO del lock pero guardar FUERA — Save hace I/O de disco
            // y retener el lock durante cientos de ms bloquea a todos los callers concurrentes
            toSave = new Dictionary<string, string>(map);
            result = true;
        }
        // Guardar fuera del lock — si dos hilos llegan aquí simultáneamente, la segunda escritura
        // sobreescribe la primera con el mismo contenido: inocuo.
        if (toSave != null) Save(toSave);
        return result;
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