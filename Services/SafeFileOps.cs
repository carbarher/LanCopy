using System.Collections.Concurrent;
using System.Text.Json;

namespace LanCopy.Services;

internal static class SafeFileOps
{
    private static readonly ConcurrentDictionary<string, DateTime> _cooldowns = new(StringComparer.OrdinalIgnoreCase);

    // Stat cache: evita re-stat en batch verify (TTL 5s)
    private static readonly ConcurrentDictionary<string, (DateTime Ts, long Size, long LastWriteUtcTicks, bool Exists, bool IsDir)> _statCache
        = new(StringComparer.OrdinalIgnoreCase);
    // P2: contador O(1) para evitar ConcurrentDictionary.Count que es O(n) con write-lock en hot path
    private static int _statCacheCount;
    private const int StatCacheTtlSeconds = 5;
    private const int StatCacheMax = 4096; // tope anti-crecimiento ilimitado
    private const int CooldownsMax = 4096; // tope anti-crecimiento ilimitado
    // Q3: contador O(1) para _cooldowns (mismo patrón que _statCacheCount)
    private static int _cooldownsCount;
    // P1: tick de última limpieza de cooldowns — rate-limit O(n) sweep a una vez cada 10s
    private static long _cooldownsLastCleanTick;

    public const string HardConfirmToken = "BORRAR";

    public static string Normalize(string path) => Path.GetFullPath(path.Trim());

    private static bool IsDriveRootInput(string path)
    {
        var trimmed = path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed.Length == 2 && char.IsLetter(trimmed[0]) && trimmed[1] == ':';
    }

    public static bool TryValidateMutationPath(string? path, out string normalized, out string reason, bool requireExists = true)
    {
        normalized = "";
        reason = "";

        if (string.IsNullOrWhiteSpace(path))
        {
            reason = "svc.emptyPath";
            return false;
        }

        if (IsDriveRootInput(path))
        {
            reason = "svc.driveRootProtected";
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
            reason = "svc.symlinkBlocked"; // B8: era string español hardcodeado
            return false;
        }

        if ((File.Exists(normalized) || Directory.Exists(normalized)) && HasDangerousAttributes(normalized))
        {
            reason = "svc.dangerousAttributes"; // B8: era string español hardcodeado
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
            // S4: ReadOnly eliminado — demasiado broad (bloquea documentos de usuario legítimos).
            // FileAttributes.System ya cubre los archivos realmente críticos del OS.
            return attr.HasFlag(FileAttributes.System);
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
                // entre la raÃ­z y el destino. No inspeccionar ancestros por encima de esa raÃ­z
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
        // B3: la versión anterior con GetOrAdd tenía inversión lógica:
        // GetOrAdd(key, now) en primera llamada devuelve 'now' → (now-now)=0 < seconds → true (bloqueado!)
        // Correcto: ausencia de clave = primera vez = NO está en cooldown, registrar y dejar pasar.
        if (_cooldowns.TryGetValue(key, out var registered))
        {
            if ((now - registered).TotalSeconds < seconds)
                return true; // todavía en cooldown
            // Expirado: renovar timestamp (solo uno de los threads concurrentes gana el CAS)
            _cooldowns.TryUpdate(key, now, registered);
        }
        else
        {
            // Primera vez: registrar y permitir
            if (_cooldowns.TryAdd(key, now)) Interlocked.Increment(ref _cooldownsCount);
        }
        // Q1: pasar MinCooldownPruneTtlSeconds (constante) en lugar de seconds del caller
        // evita que una llamada con cooldown corto purgue entradas de otras con cooldown largo
        // Q3: usar _cooldownsCount O(1) en lugar de .Count O(n)
        // P1: rate-limit PruneCooldowns a una vez cada 10s para evitar O(n) sweep por request
        var nowTick = Environment.TickCount64;
        var prevCooldownClean = Volatile.Read(ref _cooldownsLastCleanTick);
        if (_cooldownsCount > CooldownsMax && nowTick - prevCooldownClean > 10_000 &&
            Interlocked.CompareExchange(ref _cooldownsLastCleanTick, nowTick, prevCooldownClean) == prevCooldownClean)
            PruneCooldowns(now);
        return false;
    }

    private const int MinCooldownPruneTtlSeconds = 2; // TTL mínimo seguro para limpiar cooldowns (todos los callers usan >= 2s)
    private static void PruneCooldowns(DateTime now)
    {
        int pruned = 0;
        foreach (var kv in _cooldowns)
            if ((now - kv.Value).TotalSeconds >= MinCooldownPruneTtlSeconds)
                if (_cooldowns.TryRemove(kv.Key, out _)) pruned++;
        // B1: clamp a 0 para evitar drift negativo bajo concurrent prune+add
        if (pruned > 0) { Interlocked.Add(ref _cooldownsCount, -pruned); if (Volatile.Read(ref _cooldownsCount) < 0) Interlocked.Exchange(ref _cooldownsCount, 0); }
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
            // BUG-FIX-B2: No crear el trash en la raiz del volumen (C:\$LanCopyTrash) ya que
            // los usuarios estandar no tienen permiso de escritura en C:\. Usar una subcarpeta
            // al lado del fichero (mismo volumen -> File.Move atomico) o LocalAppData como fallback.
            var fileDir = Path.GetDirectoryName(normalizedPath) ?? "";
            string trashRoot;
            try
            {
                trashRoot = Path.Combine(fileDir, "$LanCopyTrash");
                Directory.CreateDirectory(trashRoot);
            }
            catch
            {
                // Fallback: AppData del usuario (puede ser en distinto volumen, pero seguro)
                trashRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LanCopy", "$LanCopyTrash");
                Directory.CreateDirectory(trashRoot);
            }

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
        // S6-FIX: Redirigir audit a Log.Warn (rotacion diaria, retencion 14 dias) en lugar de
        // audit-log.jsonl plano sin rotacion que crecia sin limite.
        try
        {
            Log.Warn("audit", op, new { target, result, details, actor });
        }
        catch
        {
            // no-op: auditoria nunca debe romper flujo principal
        }
    }

    // Obtener FileInfo con cachÃ© TTL 5s (evita re-stat en batch verify)
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

        // B1/Q1: TryAdd detecta atómicamente si la entrada es nueva SIN race en factory closure
        // AddOrUpdate puede invocar el factory múltiples veces (docs .NET): usar TryAdd+TryUpdate
        var newValue = (now, size, lastWriteTicks, exists, isDir);
        if (_statCache.TryAdd(normalizedPath, newValue))
            Interlocked.Increment(ref _statCacheCount);
        else
            _statCache[normalizedPath] = newValue; // actualizar valor existente
        // P2: usar _statCacheCount O(1) en lugar de .Count O(n) con write-lock
        var cnt = Volatile.Read(ref _statCacheCount);
        if (cnt < 0) { cnt = _statCache.Count; Interlocked.Exchange(ref _statCacheCount, cnt); }
        if (cnt > StatCacheMax) PruneStatCache(now);
        return (exists, isDir, size, lastWriteTicks);
    }

    // Poda del cache: primero entradas expiradas; si sigue lleno, las mas antiguas.
    private static void PruneStatCache(DateTime now)
    {
        int expiredRemoved = 0;
        foreach (var kv in _statCache)
            if ((now - kv.Value.Ts).TotalSeconds >= StatCacheTtlSeconds)
                if (_statCache.TryRemove(kv.Key, out _)) expiredRemoved++;
        Interlocked.Add(ref _statCacheCount, -expiredRemoved);
        // Q6: evitar OrderBy(sort) O(n log n) en el hot path — barrido simple O(n) sin alloc
        // Eliminar las entradas más antiguas aproximadamente (el dict no garantiza orden, pero la barrida
        // es suficiente para mantener el cache bajo control sin el coste del sort completo)
        // B2: Volatile.Read para ver el valor actualizado post-Interlocked.Add; sin Volatile.Write (seria un bug)
        // que sobrescribiria incrementos concurrentes de GetStatCached con un valor estale
        int afterPrune = Volatile.Read(ref _statCacheCount);
        if (afterPrune <= StatCacheMax) return; // conteo ya OK - no escribir
        var target = afterPrune - StatCacheMax / 2;
        var removed = 0;
        foreach (var kv in _statCache)
        {
            if (_statCache.TryRemove(kv.Key, out _) && ++removed >= target) break;
        }
        Interlocked.Add(ref _statCacheCount, -removed);
    }

    // Invalidar cachÃ© para archivo (al borrar/renombrar)
    public static void InvalidateStatCache(string normalizedPath)
    {
        if (_statCache.TryRemove(normalizedPath, out _)) Interlocked.Decrement(ref _statCacheCount);
    }
}

