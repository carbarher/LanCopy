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
        // O20: Serializar directamente al FileStream del archivo temporal para evitar crear strings en memoria
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var fs = File.Create(tmp))
            {
                JsonSerializer.Serialize(fs, value, options);
            }
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn("json-store", "write-atomic-failed", new { path, error = ex.Message });
            return false;
        }
    }

    /// <summary>Escribe una cadena JSON ya serializada de forma atómica. Devuelve true si tuvo éxito.</summary>
    public static bool WriteRawAtomic(string path, string json)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            // CQ1-FIX: temp unico con GUID para evitar colision entre escrituras concurrentes
            // (dos SaveSettings fire-and-forget rapidos podian pisar el mismo .tmp).
            var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn("json-store", "write-raw-atomic-failed", new { path, error = ex.Message });
            return false;
        }
    }
    /// <summary>Lee y deserializa. Si no existe o está corrupto, devuelve fallback.</summary>
    public static T? Read<T>(string path, T? fallback = default, JsonSerializerOptions? options = null)
    {
        try
        {
            if (!File.Exists(path)) return fallback;
            // O20: Deserializar directamente desde FileStream para evitar File.ReadAllText string allocations
            using var fs = File.OpenRead(path);
            if (fs.Length == 0) return fallback;
            return JsonSerializer.Deserialize<T>(fs, options) ?? fallback;
        }
        catch (Exception ex)
        {
            Log.Warn("json-store", "read-failed", new { path, error = ex.Message });
            return fallback;
        }
    }
}