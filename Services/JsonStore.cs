using System;
using System.IO;
using System.Text.Json;

namespace LanCopy.Services;

/// <summary>
/// Lectura/escritura de JSON en disco con escritura atómica (temp + replace) para
/// evitar archivos corruptos si el proceso muere a mitad de la escritura.
/// Tolerante a fallos: nunca lanza; devuelve fallback o false. Testeable.
/// </summary>
public static class JsonStore
{
    /// <summary>Serializa value y lo escribe de forma atómica. Devuelve true si tuvo éxito.</summary>
    public static bool WriteAtomic<T>(string path, T value, JsonSerializerOptions? options = null)
    {
        // Q6: serializar y delegar en WriteRawAtomic para evitar duplicar la lógica de escritura atómica
        try { return WriteRawAtomic(path, JsonSerializer.Serialize(value, options)); }
        catch { return false; }
    }

    /// <summary>Escribe una cadena JSON ya serializada de forma atómica. Devuelve true si tuvo éxito.</summary>
    public static bool WriteRawAtomic(string path, string json)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch { return false; }
    }
    /// <summary>Lee y deserializa. Si no existe o está corrupto, devuelve fallback.</summary>
    public static T? Read<T>(string path, T? fallback = default, JsonSerializerOptions? options = null)
    {
        try
        {
            if (!File.Exists(path)) return fallback;
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return fallback;
            return JsonSerializer.Deserialize<T>(json, options) ?? fallback;
        }
        catch { return fallback; }
    }
}