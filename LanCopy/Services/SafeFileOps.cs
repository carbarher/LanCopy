using System.Collections.Concurrent;
using System.Text.Json;

namespace LanCopy.Services;

internal static class SafeFileOps
{
    private static readonly string AuditPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LanCopy", "audit-log.jsonl");

    private static readonly ConcurrentDictionary<string, DateTime> _cooldowns = new(StringComparer.OrdinalIgnoreCase);

    // Stat cache: evita re-stat en batch verify (TTL 5s)
    private static readonly ConcurrentDictionary<string, (DateTime Ts, long Size, long LastWriteUtcTicks, bool Exists, bool IsDir)> _statCache
        = new(StringComparer.OrdinalIgnoreCase);
    private const int StatCacheTtlSeconds = 5;

    public const string HardConfirmToken = "BORRAR";

    public static string Normalize(string path) => Path.GetFullPath(path.Trim());

    public static bool TryValidateMutationPath(string? path, out string normalized, out string reason, bool requireExists = true)
    {
        normalized = "";
        reason = "";

        if (string.IsNullOrWhiteSpace(path))
        {
            reason = "Ruta vacía";
            return false;
        }

        try { normalized = Normalize(path); }
        catch
        {
            reason = "Ruta inválida";
            return false;
        }

        if (Path.GetPathRoot(normalized)?.Equals(normalized, StringComparison.OrdinalIgnoreCase) == true)
        {
            reason = "Raíz de unidad protegida";
            return false;
        }

        if (SystemProtection.IsProtected(normalized))
        {
            reason = "Ruta protegida del sistema";
            return false;
        }

        if (requireExists && !File.Exists(normalized) && !Directory.Exists(normalized))
        {
            reason = "Ruta no existe";
            return false;
        }

        if (ContainsReparsePoint(normalized))
        {
            reason = "Symlink/Junction detectado (bloqueado)";
            return false;
        }

        if ((File.Exists(normalized) || Directory.Exists(normalized)) && HasDangerousAttributes(normalized))
        {
            reason = "Atributos protegidos (Hidden/ReadOnly/System)";
            return false;
        }

        return true;
    }

    public static bool HasDangerousAttributes(string normalizedPath)
    {
        if (!File.Exists(normalizedPath) && !Directory.Exists(normalizedPath)) return false;
        try
        {
            var attr = File.GetAttributes(normalizedPath);
            return attr.HasFlag(FileAttributes.System)
                || attr.HasFlag(FileAttributes.Hidden)
                || attr.HasFlag(FileAttributes.ReadOnly);
        }
        catch
        {
            return true;
        }
    }

    public static bool ContainsReparsePoint(string normalizedPath)
    {
        try
        {
            var current = normalizedPath;

            while (!File.Exists(current) && !Directory.Exists(current))
            {
                var parent = Directory.GetParent(current)?.FullName;
                if (string.IsNullOrWhiteSpace(parent) || parent == current) break;
                current = parent;
            }

            while (!string.IsNullOrWhiteSpace(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    return true;

                var parent = Directory.GetParent(current)?.FullName;
                if (string.IsNullOrWhiteSpace(parent) || parent == current) break;
                current = parent;
            }
        }
        catch
        {
            return true;
        }

        return false;
    }

    public static bool IsOnCooldown(string key, int seconds)
    {
        var now = DateTime.UtcNow;
        if (_cooldowns.TryGetValue(key, out var last) && (now - last).TotalSeconds < seconds)
            return true;
        _cooldowns[key] = now;
        return false;
    }

    public static bool IsHighRiskDelete(IReadOnlyList<string> normalizedPaths)
    {
        if (normalizedPaths.Count >= 20) return true;

        long total = 0;
        foreach (var p in normalizedPaths)
        {
            try
            {
                if (Directory.Exists(p)) return true;
                var fi = new FileInfo(p);
                total += fi.Exists ? fi.Length : 0;
                if (total >= 500L * 1024 * 1024) return true; // 500 MB
            }
            catch
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryMoveToTrash(string normalizedPath, out string movedPath, out string error)
    {
        movedPath = "";
        error = "";

        try
        {
            var root = Path.GetPathRoot(normalizedPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                error = "No se pudo resolver unidad";
                return false;
            }

            var trashRoot = Path.Combine(root, "$LanCopyTrash");
            Directory.CreateDirectory(trashRoot);

            var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff");
            var baseName = Path.GetFileName(normalizedPath);
            movedPath = Path.Combine(trashRoot, $"{stamp}_{baseName}");

            if (Directory.Exists(normalizedPath))
                Directory.Move(normalizedPath, movedPath);
            else if (File.Exists(normalizedPath))
                File.Move(normalizedPath, movedPath);
            else
            {
                error = "No existe";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static void Audit(string op, string target, string result, string details = "", string actor = "local")
    {
        try
        {
            var dir = Path.GetDirectoryName(AuditPath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

            var line = JsonSerializer.Serialize(new
            {
                tsUtc = DateTime.UtcNow,
                op,
                target,
                result,
                details,
                actor
            });
            File.AppendAllText(AuditPath, line + Environment.NewLine);
        }
        catch
        {
            // no-op: auditoría nunca debe romper flujo principal
        }
    }

    // Obtener FileInfo con caché TTL 5s (evita re-stat en batch verify)
    public static (bool Exists, bool IsDirectory, long Size, long LastWriteUtcTicks) GetStatCached(string normalizedPath)
    {
        var now = DateTime.UtcNow;
        if (_statCache.TryGetValue(normalizedPath, out var cached)
            && (now - cached.Ts).TotalSeconds < StatCacheTtlSeconds)
        {
            return (cached.Exists, cached.IsDir, cached.Size, cached.LastWriteUtcTicks);
        }

        bool exists = false;
        bool isDir = false;
        long size = 0;
        long lastWriteTicks = 0;

        try
        {
            if (Directory.Exists(normalizedPath))
            {
                exists = true;
                isDir = true;
                var di = new DirectoryInfo(normalizedPath);
                lastWriteTicks = di.LastWriteTimeUtc.Ticks;
            }
            else if (File.Exists(normalizedPath))
            {
                exists = true;
                isDir = false;
                var fi = new FileInfo(normalizedPath);
                size = fi.Length;
                lastWriteTicks = fi.LastWriteTimeUtc.Ticks;
            }
        }
        catch
        {
            // Si no se puede stat, devolver no-existe; el cache expira en 5s
        }

        _statCache[normalizedPath] = (now, size, lastWriteTicks, exists, isDir);
        return (exists, isDir, size, lastWriteTicks);
    }

    // Invalidar caché para archivo (al borrar/renombrar)
    public static void InvalidateStatCache(string normalizedPath)
    {
        _statCache.TryRemove(normalizedPath, out _);
    }
}
