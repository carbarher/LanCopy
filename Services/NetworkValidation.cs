namespace LanCopy.Services;

/// <summary>
/// Validación pura de parámetros de red (puertos). Sin dependencias de UI (testeable).
/// </summary>
public static class NetworkValidation
{
    public const int DefaultPort = 8742;
    public const int MinPort = 1;
    public const int MaxPort = 65535;

    public static bool IsValidPort(int port) => port is >= MinPort and <= MaxPort;

    /// <summary>
    /// Intenta parsear un puerto desde texto. Devuelve true y el valor si es un
    /// entero dentro del rango válido [1, 65535].
    /// </summary>
    public static bool TryParsePort(string? text, out int port)
    {
        port = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (!int.TryParse(text.Trim(), out var p)) return false;
        if (!IsValidPort(p)) return false;
        port = p;
        return true;
    }

    /// <summary>Parsea un puerto o devuelve el valor por defecto si no es válido.</summary>
    public static int ParsePortOrDefault(string? text, int fallback = DefaultPort)
        => TryParsePort(text, out var p) ? p : fallback;
}