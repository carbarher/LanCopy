using System;
using System.Globalization;

namespace LanCopy.Services;

// idea-qr: enlace de emparejamiento lancopy://connect?ip=..&port=..&pin=..
// Sirve como contenido del QR y para "Pegar enlace". Tambien acepta "ip:puerto" simple.
public static class PairingLink
{
    public static string Build(string ip, int port, string? pin)
    {
        var s = $"lancopy://connect?ip={Uri.EscapeDataString(ip)}&port={port}";
        if (!string.IsNullOrEmpty(pin)) s += $"&pin={Uri.EscapeDataString(pin)}";
        return s;
    }

    public readonly record struct Parsed(string Ip, int Port, string? Pin);

    // Devuelve null si el texto no es un enlace/endpoint valido.
    public static Parsed? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();

        // Formato URI lancopy://connect?ip=..&port=..&pin=..
        if (text.StartsWith("lancopy://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(text);
                var q = uri.Query.TrimStart('?');
                string? ip = null, pin = null; int port = 8742;
                foreach (var part in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = part.Split('=', 2);
                    if (kv.Length != 2) continue;
                    var key = kv[0].ToLowerInvariant();
                    var val = Uri.UnescapeDataString(kv[1]);
                    if (key == "ip") ip = val;
                    else if (key == "port") int.TryParse(val, out port);
                    else if (key == "pin") pin = val;
                }
                if (string.IsNullOrEmpty(ip)) return null;
                if (port <= 0 || port > 65535) port = 8742;
                return new Parsed(ip, port, string.IsNullOrEmpty(pin) ? null : pin);
            }
            catch { return null; }
        }

        // Formato simple ip:puerto o solo ip.
        var idx = text.LastIndexOf(':');
        if (idx > 0 && idx < text.Length - 1
            && int.TryParse(text[(idx + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var p2)
            && p2 > 0 && p2 <= 65535)
            return new Parsed(text[..idx], p2, null);
        if (!text.Contains('/') && !text.Contains(' '))
            return new Parsed(text, 8742, null);
        return null;
    }
}
