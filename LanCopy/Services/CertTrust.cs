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

    // Caché en memoria: evita leer el JSON de disco en cada conexión TLS.
    // Solo se lee al inicio (lazy) y se escribe al pinear un nuevo host.
    private static Dictionary<string, string>? _cache;

    private static Dictionary<string, string> Load()
    {
        if (_cache != null) return _cache;
        try
        {
            if (File.Exists(StorePath))
            {
                var json = File.ReadAllText(StorePath);
                _cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                         ?? new Dictionary<string, string>();
                return _cache;
            }
        }
        catch { }
        _cache = new Dictionary<string, string>();
        return _cache;
    }

    private static void Save(Dictionary<string, string> map)
    {
        _cache = map; // actualizar caché antes de ir a disco
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            // Escritura atomica: temp + replace. Evita corromper el store (y perder TODOS
            // los pins TOFU) si el proceso muere a media escritura.
            var tmp = StorePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(map));
            File.Move(tmp, StorePath, overwrite: true);
        }
        catch { }
    }

    public static string Fingerprint(X509Certificate cert)
    {
        var raw = cert.GetRawCertData();
        return Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant();
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
        lock (_lock)
        {
            var map = Load();
            if (map.TryGetValue(host, out var known))
                return string.Equals(known, fp, StringComparison.OrdinalIgnoreCase);
            map[host] = fp;
            Save(map);
            return true;
        }
    }
}