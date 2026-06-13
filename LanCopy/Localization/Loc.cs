using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Avalonia.Platform;

namespace LanCopy.Localization;

public sealed class Loc : INotifyPropertyChanged
{
    private static Loc? _instance;
    public static Loc Instance => _instance ??= new();

    public static readonly IReadOnlyList<(string Code, string Native)> Available = new[]
    {
        ("es", "Español"),
        ("en", "English"),
        ("fr", "Français"),
        ("de", "Deutsch"),
        ("it", "Italiano"),
        ("pt", "Português"),
        ("nl", "Nederlands"),
        ("pl", "Polski"),
        ("ru", "Русский"),
        ("uk", "Українська"),
        ("ar", "العربية"),
        ("tr", "Türkçe"),
        ("sv", "Svenska"),
        ("cs", "Čeština"),
        ("el", "Ελληνικά"),
        ("hi", "हिन्दी"),
        ("zh", "中文"),
        ("ja", "日本語"),
        ("ko", "한국어"),
        ("vi", "Tiếng Việt"),
    };

    private static readonly HashSet<string> SupportedCodes = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Dictionary<string, string>> _cache = new();
    private Dictionary<string, string> _current = new();
    private Dictionary<string, string> _fallback = new();

    public string Current { get; private set; } = "en";
    public bool IsRtl => string.Equals(Current, "ar", StringComparison.OrdinalIgnoreCase);

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? LanguageChanged;

    static Loc()
    {
        foreach (var (code, _) in Available) SupportedCodes.Add(code);
    }

    private Loc()
    {
        _fallback = Load("en");
        SetLanguage(ResolveInitialLanguage(), persist: false, notify: false);
    }

    public string this[string key]
    {
        get
        {
            if (string.IsNullOrEmpty(key)) return key ?? "";
            if (_current.TryGetValue(key, out var v)) return v;
            if (_fallback.TryGetValue(key, out var f)) return f;
            return key;
        }
    }

    public string Format(string key, params object[] args)
    {
        var fmt = this[key];
        try { return string.Format(fmt, args); }
        catch { return fmt; }
    }

    public void SetLanguage(string code, bool persist = true, bool notify = true)
    {
        if (string.IsNullOrWhiteSpace(code) || !SupportedCodes.Contains(code)) code = "en";
        Current = code;
        _current = Load(code);

        if (persist) PersistLanguage(code);

        if (notify)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRtl)));
            LanguageChanged?.Invoke();
        }

        try { CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(code); } catch { }
    }

    private Dictionary<string, string> Load(string code)
    {
        if (_cache.TryGetValue(code, out var cached)) return cached;
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var uri = new Uri($"avares://LanCopy/Assets/i18n/{code}.json");
            using var stream = AssetLoader.Open(uri);
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (parsed != null)
                foreach (var kv in parsed) dict[kv.Key] = kv.Value;
        }
        catch { }
        _cache[code] = dict;
        return dict;
    }

    private static string ResolveInitialLanguage()
    {
        var saved = ReadSavedLanguage();
        if (!string.IsNullOrWhiteSpace(saved) && SupportedCodes.Contains(saved!)) return saved!;

        try
        {
            var os = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;
            if (SupportedCodes.Contains(os)) return os;
        }
        catch { }
        return "en";
    }

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LanCopy", "settings.json");

    private static string? ReadSavedLanguage()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return null;
            var doc = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(SettingsPath));
            if (doc.ValueKind == JsonValueKind.Object &&
                doc.TryGetProperty("language", out var lang) &&
                lang.ValueKind == JsonValueKind.String)
                return lang.GetString();
        }
        catch { }
        return null;
    }

    private static void PersistLanguage(string code)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            Dictionary<string, JsonElement> map = new();
            if (File.Exists(SettingsPath))
            {
                var existing = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(SettingsPath));
                if (existing != null) map = existing;
            }
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(code));
            map["language"] = doc.RootElement.Clone();
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = false }));
        }
        catch { }
    }
}