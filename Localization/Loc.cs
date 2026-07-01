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
    // Q4: thread-safe singleton — ??= no es atómico bajo contención inicial
    private static readonly Lazy<Loc> _lazy = new(() => new Loc(), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
    public static Loc Instance => _lazy.Value;

    public static readonly IReadOnlyList<(string Code, string Native)> Available = new[]
    {
        ("es", "Espa\u00f1ol"),      // Español
        ("en", "English"),
        ("fr", "Fran\u00e7ais"),     // Français
        ("de", "Deutsch"),
        ("it", "Italiano"),
        ("pt", "Portugu\u00eas"),    // Português
        ("nl", "Nederlands"),
        ("pl", "Polski"),
        ("ru", "\u0420\u0443\u0441\u0441\u043a\u0438\u0439"),    // Русский
        ("uk", "\u0423\u043a\u0440\u0430\u0457\u043d\u0441\u044c\u043a\u0430"), // Українська
        ("ar", "\u0639\u0631\u0628\u064a"),                       // عربي
        ("tr", "T\u00fcrk\u00e7e"),  // Türkçe
        ("sv", "Svenska"),
        ("cs", "\u010ce\u0161tina"),  // Čeština
        ("el", "\u0395\u03bb\u03bb\u03b7\u03bd\u03b9\u03ba\u03ac"),  // Ελληνικά
        ("hi", "\u0939\u093f\u0928\u094d\u0926\u0940"),              // हिन्दी
        ("zh", "\u4e2d\u6587"),      // 中文
        ("ja", "\u65e5\u672c\u8a9e"), // 日本語
        ("ko", "\ud55c\uad6d\uc5b4"), // 한국어
        ("vi", "Ti\u1ebfng Vi\u1ec7t"), // Tiếng Việt
    };

    private static readonly HashSet<string> SupportedCodes = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Dictionary<string, string>> _cache = new();
    // P3: lock compartido para PersistLanguage — evita race con SaveSettings de MainWindow
    internal static readonly System.Threading.SemaphoreSlim SettingsLock = new(1, 1);
    private volatile Dictionary<string, string> _current = new(); // P1: volatile para visibilidad entre threads (SetLanguage vs indexer desde Task.Run)
    private volatile Dictionary<string, string> _fallback = new(); // P1: volatile para visibilidad entre threads

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

    private readonly object _cacheLock = new(); // Q7: proteger _cache de escrituras concurrentes

    private Dictionary<string, string> Load(string code)
    {
        lock (_cacheLock) // Q7: Dictionary<> no es thread-safe para escrituras concurrentes
        {
            // P1: verificar cache ANTES del I/O y retener el lock para prevenir double-load concurrente
            if (_cache.TryGetValue(code, out var cached)) return cached;
            // Cargar el idioma dentro del lock — el I/O es infrecuente (una vez por idioma en startup)
            // asi evitamos que dos threads carguen el mismo fichero de forma redundante
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
        // P3: ejecutar en ThreadPool para que el UI thread (CmbLang_SelectionChanged) no bloquee
        // esperando SettingsLock si SaveSettings lo está reteniendo desde un contexto background.
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            SettingsLock.Wait();
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
                // escritura atómica para evitar corrupción si el proceso muere a mitad
                LanCopy.Services.JsonStore.WriteRawAtomic(SettingsPath,
                    JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = false }));
            }
            catch { }
            finally { SettingsLock.Release(); }
        });
    }
}