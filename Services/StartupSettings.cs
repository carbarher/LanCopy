using System.IO;
using System.Text.Json;

namespace LanCopy.Services;

/// <summary>
/// Lee ajustes de servidor desde settings.json de forma síncrona antes de
/// <c>FileServer.Start</c>. Evita una ventana al arranque donde el PIN/TLS
/// aún no están aplicados (LoadSettingsAsync es async y llega tarde).
/// </summary>
public static class StartupSettings
{
    public const int DefaultLocalPort = 8742;

    public sealed record ServerConfig(
        int LocalPort,
        string? RequiredPin,
        bool TlsEnabled,
        bool RestrictShareRoot,
        bool ReadOnly,
        bool RequireApproval);

    public static ServerConfig Load(string settingsPath)
    {
        try
        {
            if (!File.Exists(settingsPath))
                return Defaults();

            var doc = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(settingsPath));
            return new ServerConfig(
                LocalPort: ReadLocalPort(doc),
                RequiredPin: ReadPin(doc),
                TlsEnabled: ReadBool(doc, "tlsEnabled", defaultValue: true),
                RestrictShareRoot: ReadBool(doc, "restrictShareRoot", defaultValue: true),
                ReadOnly: ReadBool(doc, "readOnly", defaultValue: false),
                RequireApproval: ReadBool(doc, "requireApproval", defaultValue: false));
        }
        catch
        {
            return Defaults();
        }
    }

    public static ServerConfig Defaults()
        => new(DefaultLocalPort, null, true, true, false, false);

    private static int ReadLocalPort(JsonElement doc)
    {
        if (!doc.TryGetProperty("localPort", out var lp))
            return DefaultLocalPort;

        if (lp.ValueKind == JsonValueKind.Number && lp.TryGetInt32(out var n) && n is >= 1 and <= 65535)
            return n;

        if (lp.ValueKind == JsonValueKind.String
            && int.TryParse(lp.GetString(), out var s)
            && s is >= 1 and <= 65535)
            return s;

        return DefaultLocalPort;
    }

    private static string? ReadPin(JsonElement doc)
    {
        if (!doc.TryGetProperty("pin", out var pinEl))
            return null;

        var pin = pinEl.GetString()?.Trim();
        return string.IsNullOrEmpty(pin) ? null : pin;
    }

    private static bool ReadBool(JsonElement doc, string name, bool defaultValue)
    {
        if (!doc.TryGetProperty(name, out var el))
            return defaultValue;

        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => defaultValue,
        };
    }
}
