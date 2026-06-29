using System;
using System.IO;

namespace LanCopy.Services;

/// <summary>
/// Utilidades puras de seguridad de rutas para transferencias entrantes.
/// Sin dependencias de UI (testeable).
/// </summary>
public static class PathSafety
{
    /// <summary>
    /// Combina segmentos bajo baseDir rechazando rutas absolutas y traversal ("..").
    /// Garantiza que el resultado queda contenido dentro de baseDir.
    /// </summary>
    public static bool TryCombineUnder(string baseDir, out string dest, params string[] parts)
    {
        dest = "";
        try
        {
            var rootFull = Path.GetFullPath(baseDir);
            var combined = rootFull;
            foreach (var p in parts)
            {
                if (string.IsNullOrEmpty(p)) continue;
                if (Path.IsPathRooted(p)) return false;
                foreach (var seg in p.Split('/', '\\'))
                {
                    if (seg is "" or ".") continue;
                    if (seg == "..") return false;
                    combined = Path.Combine(combined, seg);
                }
            }
            var full = Path.GetFullPath(combined);
            var rootWithSep = rootFull.EndsWith(Path.DirectorySeparatorChar)
                ? rootFull : rootFull + Path.DirectorySeparatorChar;
            if (!full.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(full, rootFull, StringComparison.OrdinalIgnoreCase))
                return false;
            dest = full;
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Si dest existe, genera "nombre (2).ext", "(3)"... hasta encontrar uno libre.
    /// </summary>
    public static string MakeUnique(string dest)
    {
        if (!File.Exists(dest)) return dest;
        var dir = Path.GetDirectoryName(dest) ?? "";
        var name = Path.GetFileNameWithoutExtension(dest);
        var ext = Path.GetExtension(dest);
        for (int n = 2; n < 1000; n++)
        {
            var candidate = Path.Combine(dir, $"{name} ({n}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
        // Fallback: GUID para evitar sobrescritura silenciosa.
        return Path.Combine(dir, $"{name} ({Guid.NewGuid():N}){ext}");
    }
}