using System.Collections.Concurrent;
using System.Text.Json;

namespace LanCopy.Services;

internal static class SafeFileOps
{
    private static readonly string AuditPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LanCopy", "audit-log.jsonl");

    private static readonly ConcurrentDictionary<string, DateTime> _cooldowns = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _auditLock = new();

    // Stat cache: evita re-stat en batch verify (TTL 5s)
    private static readonly ConcurrentDictionary<string, (DateTime Ts, long Size, long LastWriteUtcTicks, bool Exists, bool IsDir)> _statCache
        = new(StringComparer.OrdinalIgnoreCase);
    private const int StatCacheTtlSeconds = 5;
    private const int StatCacheMax = 4096; // tope anti-crecimiento ilimitado
    private const int CooldownsMax = 4096; // tope anti-crecimiento ilimitado

    public const string HardConfirmToken = "BORRAR";

    public static string Normalize(string path) => Path.GetFullPath(path.Trim());

    public static bool TryValidateMutationPath(string? path, out string normalized, out string reason, bool requireExists = true)
    {
        normalized = "";
        reason = "";

        if (string.IsNullOrWhiteSpace(path))
        {
            reason = "svc.emptyPath";
            return false;
        }

        // SEC-004: Validate path length (max 4096 chars) and no null bytes
        if (path.Length > 4096)
        {
            reason = "svc.pathTooLong";
            return false;
        }

        if (path.Contains('\0'))
        {
            reason = "svc.pathInvalidChars";
            return false;
        }

        try { normalized = Normalize(path); }
        catch
        {
            reason = "svc.invalidPath";
            return false;
        }

        if (Path.GetPathRoot(normalized)?.Equals(normalized, StringComparison.OrdinalIgnoreCase) == true)
        {
            reason = "svc.driveRootProtected";
            return false;
        }

        if (SystemProtection.IsProtected(normalized))
        {
            reason = "svc.sysProtected";
            return false;
        }

        if (requireExists && !File.Exists(normalized) && !Directory.Exists(normalized))
        {
            reason = "svc.pathNotExist";
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
            var shareRoot = "";
            var boundedByShareRoot = false;
            try
            {
                shareRoot = Path.GetFullPath(ShareRoot.Root).TrimEnd(Path.DirectorySeparatorChar);
                boundedByShareRoot = IsPathUnder(shareRoot, Path.GetFullPath(normalizedPath));
            }
            catch
            {
                boundedByShareRoot = false;
            }

            while (!File.Exists(current) && !Directory.Exists(current))
            {
                var parent = Directory.GetParent(current)?.FullName;
                if (string.IsNullOrWhiteSpace(parent) || parent == current) break;
                current = parent;
            }

            while (!string.IsNullOrWhiteSpace(current))
            {
                // En modo "carpeta compartida", ShareRoot ya valida symlinks/reparse points
                // entre la raíz y el destino. No inspeccionar ancestros por encima de esa raíz
                // (p.ej. /var -> /private/var en macOS), para evitar falsos positivos.
                if (boundedByShareRoot && string.Equals(
                    current.TrimEnd(Path.DirectorySeparatorChar),
                    shareRoot,
                    StringComparison.OrdinalIgnoreCase))
                    break;

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

    private static bool IsPathUnder(string root, string candidate)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(candidate)) return false;
        var r = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        var c = candidate.EndsWith(Path.DirectorySeparatorChar) ? candidate : candidate + Path.DirectorySeparatorChar;
        return c.StartsWith(r, StringComparison.OrdinalIgnoreCase)
               || string.Equals(candidate.TrimEnd(Path.DirectorySeparatorChar),
                                root.TrimEnd(Path.DirectorySeparatorChar),
                                StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsOnCooldown(string key, int seconds)
    {
        var now = DateTime.UtcNow;
        // Check-and-set atomico: evita que dos peticiones concurrentes pasen el cooldown.
        bool onCooldown = false;
        _cooldowns.AddOrUpdate(key, now, (_, last) =>
        {
            if ((now - last).TotalSeconds < seconds) { onCooldown = true; return last; }
            return now;
        });
        if (_cooldowns.Count > CooldownsMax) PruneCooldowns(now, seconds);
        return onCooldown;
    }

    private static void PruneCooldowns(DateTime now, int seconds)
    {
        foreach (var kv in _cooldowns)
            if ((now - kv.Value).TotalSeconds >= seconds)
                _cooldowns.TryRemove(kv.Key, out _);
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
                error = "svc.driveResolve";
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
                error = "svc.notExist";
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
            lock (_auditLock) File.AppendAllText(AuditPath, line + Environment.NewLine);
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
        if (_statCache.Count > StatCacheMax) PruneStatCache(now);
        return (exists, isDir, size, lastWriteTicks);
    }

    // Poda del cache: primero entradas expiradas; si sigue lleno, las mas antiguas.
    private static void PruneStatCache(DateTime now)
    {
        foreach (var kv in _statCache)
            if ((now - kv.Value.Ts).TotalSeconds >= StatCacheTtlSeconds)
                _statCache.TryRemove(kv.Key, out _);
        if (_statCache.Count <= StatCacheMax) return;
        foreach (var kv in _statCache.OrderBy(x => x.Value.Ts).Take(_statCache.Count - StatCacheMax / 2))
            _statCache.TryRemove(kv.Key, out _);
    }

    // Invalidar caché para archivo (al borrar/renombrar)
    public static void InvalidateStatCache(string normalizedPath)
    {
        _statCache.TryRemove(normalizedPath, out _);
    }
}
