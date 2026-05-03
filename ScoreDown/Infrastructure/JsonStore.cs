using System.IO;
using ScoreDown.Models;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ScoreDown.Infrastructure;

/// <summary>
/// Generic JSON persistence helper. Eliminates the repetitive try/catch + File.ReadAllText +
/// Directory.CreateDirectory + File.WriteAllText pattern across all Load/Save pairs.
/// </summary>
internal static class JsonStore
{
    private static readonly JsonSerializerOptions s_pretty = new() { WriteIndented = true };

    private static JsonTypeInfo<T>? TryGetTypeInfo<T>()
    {
        var t = typeof(T);
        if (t == typeof(Dictionary<string, string>))
            return (JsonTypeInfo<T>)(object)ScoreDownJsonContext.Default.DictionaryStringString;
        if (t == typeof(List<string>))
            return (JsonTypeInfo<T>)(object)ScoreDownJsonContext.Default.ListString;
        if (t == typeof(List<ScoreDown.Models.PartituraItem>))
            return (JsonTypeInfo<T>)(object)ScoreDownJsonContext.Default.ListPartituraItem;
        if (t == typeof(List<DownloadHistoryItem>))
            return (JsonTypeInfo<T>)(object)ScoreDownJsonContext.Default.ListDownloadHistoryItem;
        if (t == typeof(UiState))
            return (JsonTypeInfo<T>)(object)ScoreDownJsonContext.Default.UiState;
        if (t == typeof(LibraryData))
            return (JsonTypeInfo<T>)(object)ScoreDownJsonContext.Default.LibraryData;
        return null;
    }

    /// <summary>
    /// Load a JSON file into <typeparamref name="T"/>. Returns <paramref name="fallback"/> on any error.
    /// </summary>
    public static T Load<T>(string path, T fallback)
    {
        try
        {
            if (!File.Exists(path)) return fallback;
            var json = File.ReadAllText(path);
            var typeInfo = TryGetTypeInfo<T>();
            return typeInfo is not null
                ? (JsonSerializer.Deserialize(json, typeInfo) ?? fallback)
                : (JsonSerializer.Deserialize<T>(json) ?? fallback);
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>
    /// Save <paramref name="value"/> as pretty-printed JSON. Silently ignores errors.
    /// </summary>
    public static void Save<T>(string path, T value)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var typeInfo = TryGetTypeInfo<T>();
            var json = typeInfo is not null
                ? JsonSerializer.Serialize(value, typeInfo)
                : JsonSerializer.Serialize(value, s_pretty);
            File.WriteAllText(path, json, System.Text.Encoding.UTF8);
        }
        catch { }
    }
}
