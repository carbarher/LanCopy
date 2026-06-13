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

    private static Dictionary<string, string> Load()
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
        catch { }
        return new Dictionary<string, string>();
    }

    private static void Save(Dictionary<string, string> map)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            File.WriteAllText(StorePath, JsonSerializer.Serialize(map));
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