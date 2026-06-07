using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using SlskDownBibliotecaImport;

namespace SlskDownImportBiblioteca;

/// <summary>
/// Limpia la carpeta de origen antes de importar:
/// 1. Normaliza nombres de archivo (autor - titulo.ext).
/// 2. Elimina archivos cuyo autor no está en autores_gutenberg.txt.
/// </summary>
internal static class SourceFolderCleaner
{
    private static readonly string[] s_bookExtensions =
        { ".epub", ".mobi", ".pdf", ".azw3", ".fb2", ".djvu", ".txt" };
    private static readonly HashSet<string> s_bookExtensionsSet = new(s_bookExtensions, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> s_clearlyNonBookExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff",
        ".opf", ".nfo", ".ini", ".db", ".csv", ".bak", ".tmp", ".temp",
        ".mp3", ".flac", ".wav", ".ogg", ".m4a", ".aac",
        ".mp4", ".mkv", ".avi", ".mov", ".wmv",
        ".musicxml", ".mscx", ".mscz"
    };
    private static readonly HashSet<char> s_invalidFileNameChars = new(Path.GetInvalidFileNameChars());
    private const string GutenbergAuthorsFileName = "autores_gutenberg.txt";

    // Ruta donde buscar autores_gutenberg.txt.
    // Prioridad: override explícito -> env var -> rutas cercanas al origen -> árbol del exe.
    private static string? FindGutenbergListPath(string? pathOverride, string? srcDir)
    {
        if (!string.IsNullOrWhiteSpace(pathOverride) && File.Exists(pathOverride))
            return pathOverride;

        var envOverride = Environment.GetEnvironmentVariable("SLSDOWN_GUTENBERG_AUTHORS_PATH");
        if (!string.IsNullOrWhiteSpace(envOverride) && File.Exists(envOverride))
            return envOverride;

        if (!string.IsNullOrWhiteSpace(srcDir))
        {
            var srcCandidate = Path.Combine(srcDir, GutenbergAuthorsFileName);
            if (File.Exists(srcCandidate))
                return srcCandidate;

            var parent = Directory.GetParent(srcDir)?.FullName;
            if (!string.IsNullOrWhiteSpace(parent))
            {
                var parentCandidate = Path.Combine(parent, GutenbergAuthorsFileName);
                if (File.Exists(parentCandidate))
                    return parentCandidate;
            }
        }

        var fixedWorkspaceCandidate = Path.Combine("c:\\p2p", GutenbergAuthorsFileName);
        if (File.Exists(fixedWorkspaceCandidate))
            return fixedWorkspaceCandidate;

        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            var candidate = Path.Combine(dir, GutenbergAuthorsFileName);
            if (File.Exists(candidate))
                return candidate;
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent == null) break;
            dir = parent;
        }
        return null;
    }

    /// <summary>
    /// Carga el catálogo Gutenberg como un índice de tokens normalizados.
    /// Clave: token (palabra significativa). Valor: no importa; usamos HashSet.
    /// </summary>
    internal static HashSet<string> LoadGutenbergTokens(string? pathOverride = null, string? srcDir = null)
    {
        var path = FindGutenbergListPath(pathOverride, srcDir);
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (path == null) return tokens;

        foreach (var raw in File.ReadLines(path, Encoding.UTF8))
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;
            // Quitar anotaciones entre corchetes y después de ';'
            var semicolonIdx = line.IndexOf(';');
            if (semicolonIdx >= 0) line = line[..semicolonIdx];
            var bracketIdx = line.IndexOf('[');
            if (bracketIdx >= 0) line = line[..bracketIdx];
            line = line.Trim();
            if (line.Length < 2) continue;

            var normalized = NormalizeForLookup(line);
            foreach (var tok in TokensOf(normalized))
                tokens.Add(tok);
        }
        return tokens;
    }

    /// <summary>
    /// Limpia la carpeta de origen: normaliza nombres y elimina archivos sin autor en el catálogo.
    /// </summary>
    internal static async Task<CleanResult> CleanAsync(
        string srcDir,
        HashSet<string> gutenbergTokens,
        IProgress<string> log,
        bool dryRun,
        bool deleteUnknownNonBook,
        CancellationToken ct)
    {
        var result = new CleanResult();
        if (gutenbergTokens.Count == 0)
        {
            log.Report("⚠️ [Limpieza] No se encontró autores_gutenberg.txt; se omite la limpieza de origen.");
            return result;
        }

        return await Task.Run(() =>
        {
            const int MaxDetailedLogEntries = 80;
            const int ProgressEveryFiles = 500;
            const int LogFlushThreshold = 48;
            log.Report("🧹 [Limpieza] Iniciando limpieza de origen…");
            int scannedTotal = 0;
            int scannedBooks = 0;
            int detailedLogs = 0;
            int suppressedLogs = 0;

            int renamed = 0;
            int deleted = 0;
            int nonBookDeleted = 0;
            int unknownDeleted = 0;
            int skipped = 0;

            var authorCorrectionCache = new ConcurrentDictionary<string, string?>(StringComparer.Ordinal);
            var authorInCatalogCache = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
            var policyLock = new object();

            var catalogStamp = BuildCatalogStamp(gutenbergTokens);
            var persisted = LoadPersistentAuthorPolicyCache(catalogStamp);
            if (persisted != null)
            {
                if (persisted.Corrections != null)
                {
                    foreach (var kv in persisted.Corrections)
                        authorCorrectionCache.TryAdd(kv.Key, kv.Value);
                }
                if (persisted.InCatalog != null)
                {
                    foreach (var kv in persisted.InCatalog)
                        authorInCatalogCache.TryAdd(kv.Key, kv.Value);
                }
                log.Report($"⚡ [Cache] Reutilizadas {authorCorrectionCache.Count:N0} correcciones y {authorInCatalogCache.Count:N0} decisiones de catálogo.");
            }

            var bufferedLogs = new ConcurrentQueue<string>();
            var flushGate = new object();
            int pendingBufferedLogs = 0;

            void EnqueueBufferedLog(string message)
            {
                bufferedLogs.Enqueue(message);
                var pending = Interlocked.Increment(ref pendingBufferedLogs);
                if (pending >= LogFlushThreshold)
                {
                    FlushBufferedLogs();
                }
            }

            void FlushBufferedLogs(bool force = false)
            {
                if (!force && Volatile.Read(ref pendingBufferedLogs) < LogFlushThreshold)
                    return;

                lock (flushGate)
                {
                    if (!force && Volatile.Read(ref pendingBufferedLogs) < LogFlushThreshold)
                        return;

                    while (bufferedLogs.TryDequeue(out var line))
                        log.Report(line);

                    Interlocked.Exchange(ref pendingBufferedLogs, 0);
                }
            }

            void ReportDetail(string message)
            {
                if (Interlocked.Increment(ref detailedLogs) <= MaxDetailedLogEntries)
                    EnqueueBufferedLog(message);
                else
                    Interlocked.Increment(ref suppressedLogs);
            }

            var options = new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = GetCleanerMaxDegreeOfParallelism()
            };

            try
            {
                Parallel.ForEach(EnumerateFilesRecursiveStream(srcDir), options, filePath =>
                {
                    options.CancellationToken.ThrowIfCancellationRequested();

                    var total = Interlocked.Increment(ref scannedTotal);

                    var ext = Path.GetExtension(filePath);

                    // Limpieza temprana de basura evidente: imágenes/metadata/audio/video, etc.
                    if (IsClearlyNonBookExtension(ext))
                    {
                        try
                        {
                            if (dryRun)
                            {
                                ReportDetail($"  🧪 DRY-RUN eliminaría (no-libro): {Path.GetFileName(filePath)}");
                            }
                            else
                            {
                                File.Delete(filePath);
                                ReportDetail($"  🗑️ Eliminado (no-libro): {Path.GetFileName(filePath)}");
                            }
                            Interlocked.Increment(ref deleted);
                            Interlocked.Increment(ref nonBookDeleted);
                        }
                        catch (Exception ex)
                        {
                            ReportDetail($"  ⚠️ No se pudo eliminar no-libro {Path.GetFileName(filePath)}: {ex.Message}");
                            Interlocked.Increment(ref skipped);
                        }
                        return;
                    }

                    if (!IsBookExtension(ext))
                    {
                        if (!deleteUnknownNonBook)
                            return;

                        try
                        {
                            if (dryRun)
                            {
                                ReportDetail($"  🧪 DRY-RUN eliminaría (desconocido): {Path.GetFileName(filePath)}");
                            }
                            else
                            {
                                File.Delete(filePath);
                                ReportDetail($"  🗑️ Eliminado (desconocido): {Path.GetFileName(filePath)}");
                            }
                            Interlocked.Increment(ref deleted);
                            Interlocked.Increment(ref unknownDeleted);
                        }
                        catch (Exception ex)
                        {
                            ReportDetail($"  ⚠️ No se pudo eliminar desconocido {Path.GetFileName(filePath)}: {ex.Message}");
                            Interlocked.Increment(ref skipped);
                        }
                        return;
                    }

                    var books = Interlocked.Increment(ref scannedBooks);
                    if (books % ProgressEveryFiles == 0)
                    {
                        EnqueueBufferedLog($"🧹 [Limpieza] Progreso: {books:N0} libro(s) revisados ({total:N0} archivo(s) recorridos)…");
                    }

                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var parsed = TryResolveAuthorTitle(fileName, gutenbergTokens, out var author, out var title);

                    if (!string.IsNullOrWhiteSpace(author))
                    {
                        var correctedAuthor = authorCorrectionCache.GetOrAdd(author, a =>
                        {
                            lock (policyLock)
                            {
                                return StandaloneGutenbergPublicDomainPolicy.TryCorrectAuthorFuzzy(a, srcDir);
                            }
                        });
                        if (correctedAuthor != null && !string.Equals(author, correctedAuthor, StringComparison.Ordinal))
                        {
                            author = correctedAuthor;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(author))
                    {
                        ReportDetail($"  ⚠️ Sin autor legible, omitido: {Path.GetFileName(filePath)}");
                        Interlocked.Increment(ref skipped);
                        return;
                    }

                    var isAuthorInCatalog = authorInCatalogCache.GetOrAdd(author, a =>
                    {
                        lock (policyLock)
                        {
                            return StandaloneGutenbergPublicDomainPolicy.IsAuthorInCatalog(a, srcDir);
                        }
                    });

                    if (!isAuthorInCatalog)
                    {
                        if (parsed == ParsedAuthorResult.Ambiguous)
                        {
                            ReportDetail($"  ⚠️ Autor ambiguo (no se borra por seguridad): {Path.GetFileName(filePath)}");
                            Interlocked.Increment(ref skipped);
                            return;
                        }

                        try
                        {
                            if (dryRun)
                            {
                                ReportDetail($"  🧪 DRY-RUN eliminaría (autor no en Gutenberg): {Path.GetFileName(filePath)}");
                            }
                            else
                            {
                                File.Delete(filePath);
                                ReportDetail($"  🗑️ Eliminado (autor no en Gutenberg): {Path.GetFileName(filePath)}");
                            }
                            Interlocked.Increment(ref deleted);
                        }
                        catch (Exception ex)
                        {
                            ReportDetail($"  ⚠️ No se pudo eliminar {Path.GetFileName(filePath)}: {ex.Message}");
                        }
                        return;
                    }

                    var normalizedName = BuildNormalizedFileName(author, title, ext);
                    if (!string.Equals(normalizedName, Path.GetFileName(filePath), StringComparison.OrdinalIgnoreCase))
                    {
                        var fileDir = Path.GetDirectoryName(filePath) ?? srcDir;
                        var newPath = Path.Combine(fileDir, normalizedName);
                        try
                        {
                            if (!File.Exists(newPath) || string.Equals(newPath, filePath, StringComparison.OrdinalIgnoreCase))
                            {
                                if (dryRun)
                                {
                                    ReportDetail($"  🧪 DRY-RUN renombraría: {Path.GetFileName(filePath)} → {normalizedName}");
                                }
                                else
                                {
                                    File.Move(filePath, newPath);
                                    ReportDetail($"  ✏️ Renombrado: {Path.GetFileName(filePath)} → {normalizedName}");
                                }
                                Interlocked.Increment(ref renamed);
                            }
                            else
                            {
                                var conflictsDir = Path.Combine(srcDir, "conflictos_importacion");
                                if (dryRun)
                                {
                                    ReportDetail($"  🧪 DRY-RUN movería colisión a carpeta de conflictos: {Path.GetFileName(filePath)}");
                                }
                                else
                                {
                                    Directory.CreateDirectory(conflictsDir);
                                    var conflictPath = Path.Combine(conflictsDir, Path.GetFileName(filePath));
                                    if (File.Exists(conflictPath))
                                    {
                                        conflictPath = Path.Combine(conflictsDir, $"{Path.GetFileNameWithoutExtension(filePath)}_{Guid.NewGuid():N}{ext}");
                                    }
                                    File.Move(filePath, conflictPath);
                                    ReportDetail($"  ⚠️ Colisión: {Path.GetFileName(filePath)} ya existe en formato normalizado. Movido a 'conflictos_importacion'.");
                                }
                                Interlocked.Increment(ref skipped);
                            }
                        }
                        catch (Exception ex)
                        {
                            ReportDetail($"  ⚠️ No se pudo renombrar {Path.GetFileName(filePath)}: {ex.Message}");
                        }
                    }
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            FlushBufferedLogs(force: true);

            result.Renamed = renamed;
            result.Deleted = deleted;
            result.Skipped = skipped;
            result.ScannedTotal = scannedTotal;
            result.ScannedBooks = scannedBooks;
            result.NonBookDeleted = nonBookDeleted;
            result.UnknownDeleted = unknownDeleted;

            SavePersistentAuthorPolicyCache(
                catalogStamp,
                SnapshotLimited(authorCorrectionCache, 120_000),
                SnapshotLimited(authorInCatalogCache, 120_000));

            if (suppressedLogs > 0)
                log.Report($"ℹ️ [Limpieza] {suppressedLogs:N0} evento(s) detallados se omitieron del log para mantener la UI fluida.");

            log.Report($"🧹 [Limpieza] Completa: {result.Renamed} renombrado(s), {result.Deleted} eliminado(s) ({nonBookDeleted:N0} no-libro + {unknownDeleted:N0} desconocido), {result.Skipped} omitido(s). Recorridos: {scannedTotal:N0} archivo(s), libros: {scannedBooks:N0}.");
            return result;
        }, ct);
    }

    /// <summary>
    /// Purga al final de la importación cualquier archivo remanente en origen
    /// (no solo libros), y luego elimina carpetas vacías.
    /// </summary>
    internal static async Task<PurgeResult> PurgeRemainingBooksAndEmptyDirsAsync(
        string srcDir,
        IProgress<string> log,
        bool dryRun,
        CancellationToken ct)
    {
        var result = new PurgeResult();
        if (string.IsNullOrWhiteSpace(srcDir) || !Directory.Exists(srcDir))
            return result;

        return await Task.Run(() =>
        {
            const int LogFlushThreshold = 64;
            const int MaxForensicSamples = 30;
            var bufferedLogs = new ConcurrentQueue<string>();
            var flushGate = new object();
            int pendingBufferedLogs = 0;
            var deleteErrorSamples = new ConcurrentQueue<string>();

            void EnqueueBufferedLog(string message)
            {
                bufferedLogs.Enqueue(message);
                var pending = Interlocked.Increment(ref pendingBufferedLogs);
                if (pending >= LogFlushThreshold)
                    FlushBufferedLogs();
            }

            void FlushBufferedLogs(bool force = false)
            {
                if (!force && Volatile.Read(ref pendingBufferedLogs) < LogFlushThreshold)
                    return;

                lock (flushGate)
                {
                    if (!force && Volatile.Read(ref pendingBufferedLogs) < LogFlushThreshold)
                        return;

                    while (bufferedLogs.TryDequeue(out var line))
                        log.Report(line);

                    Interlocked.Exchange(ref pendingBufferedLogs, 0);
                }
            }

            int deleted = 0;
            int found = 0;
            int deleteErrors = 0;

            var options = new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = Math.Max(1, GetCleanerMaxDegreeOfParallelism() - 1)
            };

            try
            {
                Parallel.ForEach(EnumerateFilesRecursiveStream(srcDir), options, filePath =>
                {
                    options.CancellationToken.ThrowIfCancellationRequested();

                    Interlocked.Increment(ref found);
                    if (dryRun)
                    {
                        Interlocked.Increment(ref deleted);
                        return;
                    }

                    try
                    {
                        if (TryDeleteFileSafe(filePath))
                        {
                            Interlocked.Increment(ref deleted);
                        }
                        else
                        {
                            Interlocked.Increment(ref deleteErrors);
                            if (deleteErrorSamples.Count < MaxForensicSamples)
                                deleteErrorSamples.Enqueue($"delete-failed: {filePath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref deleteErrors);
                        if (deleteErrorSamples.Count < MaxForensicSamples)
                            deleteErrorSamples.Enqueue($"delete-error: {filePath} :: {ex.Message}");
                    }
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            result.RemainingFilesFound = found;
            result.RemainingFilesDeleted = deleted;
            result.DeleteErrors = deleteErrors;

            var directories = EnumerateDirectoriesRecursive(srcDir);
            foreach (var dirPath in directories)
            {
                ct.ThrowIfCancellationRequested();

                if (dryRun)
                {
                    if (CanDeleteDirectory(dirPath))
                        result.EmptyDirectoriesDeleted++;
                    continue;
                }

                if (TryDeleteDirectoryIfEmpty(dirPath))
                    result.EmptyDirectoriesDeleted++;
            }

            if (!dryRun && TryDeleteDirectoryIfEmpty(srcDir))
                result.EmptyDirectoriesDeleted++;
            else if (dryRun && CanDeleteDirectory(srcDir))
                result.EmptyDirectoriesDeleted++;

            if (!dryRun && (result.RemainingFilesDeleted < result.RemainingFilesFound || result.DeleteErrors > 0))
            {
                int remainingNow = 0;
                try
                {
                    foreach (var _ in EnumerateFilesRecursiveStream(srcDir))
                        remainingNow++;
                }
                catch
                {
                }

                EnqueueBufferedLog($"🧪 [Purge forense] Detectados {result.RemainingFilesFound:N0}, borrados {result.RemainingFilesDeleted:N0}, pendientes actuales {remainingNow:N0}, errores {result.DeleteErrors:N0}.");

                if (!deleteErrorSamples.IsEmpty)
                {
                    int i = 0;
                    while (deleteErrorSamples.TryDequeue(out var sample) && i < MaxForensicSamples)
                    {
                        EnqueueBufferedLog($"   ↳ {sample}");
                        i++;
                    }
                }

                var nonEmptyDirSamples = new List<string>(12);
                foreach (var dirPath in directories)
                {
                    try
                    {
                        if (!Directory.Exists(dirPath))
                            continue;

                        var entry = Directory.EnumerateFileSystemEntries(dirPath).FirstOrDefault();
                        if (entry == null)
                            continue;

                        nonEmptyDirSamples.Add($"{dirPath} -> contiene: {Path.GetFileName(entry)}");
                        if (nonEmptyDirSamples.Count >= 12)
                            break;
                    }
                    catch
                    {
                    }
                }

                foreach (var sample in nonEmptyDirSamples)
                    EnqueueBufferedLog($"   ↳ dir-no-vacia: {sample}");
            }

            var mode = dryRun ? "DRY-RUN borraría" : "Borrados";
            EnqueueBufferedLog($"🧹 [Purge origen] {mode} {result.RemainingFilesDeleted:N0}/{result.RemainingFilesFound:N0} archivo(s) remanente(s) · carpetas vacías {result.EmptyDirectoriesDeleted:N0} · errores {result.DeleteErrors:N0}");
            FlushBufferedLogs(force: true);
            return result;
        }, ct);
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private enum ParsedAuthorResult
    {
        Resolved,
        Ambiguous,
        Missing
    }

    private static IEnumerable<string> EnumerateFilesRecursiveStream(string srcDir)
    {
        if (string.IsNullOrWhiteSpace(srcDir) || !Directory.Exists(srcDir))
            yield break;

        // Fast path opcional: escáner nativo (Rust) para carpetas enormes.
        if (TryStartFastScanner(srcDir, out var scannerProcess, out var scannerReader))
        {
            using (scannerProcess)
            using (scannerReader)
            {
                string? line;
                while ((line = scannerReader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        yield return line;
                }

                try
                {
                    scannerProcess.WaitForExit(2000);
                }
                catch
                {
                }

                if (scannerProcess.ExitCode == 0)
                    yield break;
            }
        }

        var stack = new Stack<(string Path, int Depth)>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        stack.Push((srcDir, 0));
        visited.Add(srcDir);

        while (stack.Count > 0)
        {
            var (dir, depth) = stack.Pop();
            if (depth > 16) continue;

            foreach (var sub in EnumerateDirectoriesSafe(dir))
            {
                var subFull = Path.GetFullPath(sub);
                if (visited.Add(subFull))
                    stack.Push((subFull, depth + 1));
            }

            foreach (var file in EnumerateFilesSafe(dir))
                yield return file;
        }
    }

    private static IEnumerable<string> EnumerateFilesSafe(string dir)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(dir, "*", new EnumerationOptions
            {
                RecurseSubdirectories = false,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System
            });
        }
        catch
        {
            try
            {
                files = Directory.EnumerateFiles(dir);
            }
            catch
            {
                files = Array.Empty<string>();
            }
        }

        foreach (var file in files)
            yield return file;
    }

    private static IEnumerable<string> EnumerateDirectoriesSafe(string dir)
    {
        IEnumerable<string> dirs;
        try
        {
            dirs = Directory.EnumerateDirectories(dir, "*", new EnumerationOptions
            {
                RecurseSubdirectories = false,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System
            });
        }
        catch
        {
            try
            {
                dirs = Directory.EnumerateDirectories(dir);
            }
            catch
            {
                dirs = Array.Empty<string>();
            }
        }

        foreach (var sub in dirs)
            yield return sub;
    }

    private static bool TryStartFastScanner(string srcDir, out Process process, out StreamReader reader)
    {
        process = null!;
        reader = null!;

        try
        {
            if (!IsFastScannerEnabled())
                return false;

            var scannerExe = ResolveFastScannerExe();
            if (string.IsNullOrWhiteSpace(scannerExe) || !File.Exists(scannerExe))
                return false;

            var psi = new ProcessStartInfo
            {
                FileName = scannerExe,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = Path.GetDirectoryName(scannerExe) ?? AppContext.BaseDirectory,
                Arguments = QuoteArg(srcDir)
            };

            var p = new Process { StartInfo = psi };
            if (!p.Start())
                return false;

            process = p;
            reader = p.StandardOutput;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsFastScannerEnabled()
    {
        var env = Environment.GetEnvironmentVariable("SLSDOWN_IMPORT_USE_RUST_SCANNER");
        if (string.IsNullOrWhiteSpace(env)) return true;
        return env == "1" || env.Equals("true", StringComparison.OrdinalIgnoreCase) || env.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveFastScannerExe()
    {
        var env = Environment.GetEnvironmentVariable("SLSDOWN_IMPORT_FAST_SCANNER");
        if (!string.IsNullOrWhiteSpace(env)) return env;

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "tools", "slsk_import_scan.exe"),
            Path.Combine(AppContext.BaseDirectory, "slsk_import_scan.exe"),
            Path.Combine("C:\\p2p\\SlskDownImportBiblioteca\\rust\\slsk_import_scan\\target\\release\\slsk_import_scan.exe")
        };

        foreach (var candidate in candidates)
            if (File.Exists(candidate))
                return candidate;

        return null;
    }

    private static string QuoteArg(string arg)
    {
        if (string.IsNullOrEmpty(arg)) return "\"\"";
        if (!arg.Contains(' ') && !arg.Contains('\t') && !arg.Contains('"')) return arg;
        return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static int GetCleanerMaxDegreeOfParallelism()
    {
        const int hardMin = 1;
        const int hardMax = 12;

        var env = Environment.GetEnvironmentVariable("SLSDOWN_IMPORT_CLEAN_MAX_DOP");
        if (int.TryParse(env, out var parsed))
            return Math.Clamp(parsed, hardMin, hardMax);

        var cpuHalf = Math.Max(1, Environment.ProcessorCount / 2);
        return Math.Clamp(cpuHalf, hardMin, hardMax);
    }

    private static string BuildCatalogStamp(HashSet<string> gutenbergTokens)
    {
        try
        {
            using var sha = SHA256.Create();
            var ordered = gutenbergTokens.OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
            foreach (var tok in ordered)
            {
                var bytes = Encoding.UTF8.GetBytes(tok);
                sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
                var sep = new byte[] { 0 };
                sha.TransformBlock(sep, 0, sep.Length, null, 0);
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha.Hash ?? Array.Empty<byte>());
        }
        catch
        {
            return "catalog-unknown";
        }
    }

    private static string CacheDirectoryPath()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SlskDownImportBiblioteca",
            "cache");
        Directory.CreateDirectory(appData);
        return appData;
    }

    private static string AuthorPolicyCachePath() => Path.Combine(CacheDirectoryPath(), "author_policy_cache.json");

    private static AuthorPolicyCacheFile? LoadPersistentAuthorPolicyCache(string catalogStamp)
    {
        try
        {
            var path = AuthorPolicyCachePath();
            if (!File.Exists(path)) return null;

            var json = File.ReadAllText(path);
            var cache = JsonSerializer.Deserialize<AuthorPolicyCacheFile>(json);
            if (cache == null) return null;
            if (!string.Equals(cache.CatalogStamp, catalogStamp, StringComparison.Ordinal))
                return null;

            return cache;
        }
        catch
        {
            return null;
        }
    }

    private static void SavePersistentAuthorPolicyCache(
        string catalogStamp,
        Dictionary<string, string?> corrections,
        Dictionary<string, bool> inCatalog)
    {
        try
        {
            var payload = new AuthorPolicyCacheFile
            {
                CatalogStamp = catalogStamp,
                SavedUtc = DateTime.UtcNow,
                Corrections = corrections,
                InCatalog = inCatalog
            };

            var path = AuthorPolicyCachePath();
            var tmp = path + ".tmp";
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
            File.WriteAllText(tmp, json, Encoding.UTF8);
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            // Cache best-effort: no bloquear import por fallo de persistencia.
        }
    }

    private static Dictionary<string, T> SnapshotLimited<T>(ConcurrentDictionary<string, T> source, int maxItems)
    {
        var snapshot = new Dictionary<string, T>(Math.Min(source.Count, maxItems), StringComparer.Ordinal);
        int i = 0;
        foreach (var kv in source)
        {
            snapshot[kv.Key] = kv.Value;
            i++;
            if (i >= maxItems) break;
        }
        return snapshot;
    }

    private sealed class AuthorPolicyCacheFile
    {
        public string CatalogStamp { get; set; } = string.Empty;
        public DateTime SavedUtc { get; set; }
        public Dictionary<string, string?>? Corrections { get; set; }
        public Dictionary<string, bool>? InCatalog { get; set; }
    }

    private static bool IsBookExtension(string ext)
    {
        if (string.IsNullOrWhiteSpace(ext)) return false;
        return s_bookExtensionsSet.Contains(ext);
    }

    private static bool IsClearlyNonBookExtension(string ext)
    {
        if (string.IsNullOrWhiteSpace(ext)) return false;
        return s_clearlyNonBookExtensions.Contains(ext);
    }

    private static ParsedAuthorResult TryResolveAuthorTitle(
        string fileNameNoExt,
        HashSet<string> catalogTokens,
        out string author,
        out string title)
    {
        // Patrón "Título by Autor"
        var byIdx = fileNameNoExt.LastIndexOf(" by ", StringComparison.OrdinalIgnoreCase);
        if (byIdx > 0 && byIdx + 4 < fileNameNoExt.Length)
        {
            title = fileNameNoExt[..byIdx].Trim();
            author = fileNameNoExt[(byIdx + 4)..].Trim();
            return string.IsNullOrWhiteSpace(author) ? ParsedAuthorResult.Missing : ParsedAuthorResult.Resolved;
        }

        var segments = SplitNameSegments(fileNameNoExt);
        if (segments.Count >= 2)
        {
            if (segments.Count == 2)
                return ResolveTwoSegmentName(segments[0], segments[1], catalogTokens, out author, out title);

            var first = segments[0];
            var second = segments[1];
            var firstScore = CountCatalogTokenMatches(first, catalogTokens);
            var secondScore = CountCatalogTokenMatches(second, catalogTokens);

            if (secondScore > firstScore || (!LooksLikeAuthorPhrase(first) && LooksLikeAuthorPhrase(second)))
            {
                author = second;
                title = first;
                return ParsedAuthorResult.Resolved;
            }

            if (firstScore > secondScore || (LooksLikeAuthorPhrase(first) && !LooksLikeAuthorPhrase(second)))
            {
                author = first;
                title = second;
                return ParsedAuthorResult.Resolved;
            }

            author = second;
            title = first;
            return ParsedAuthorResult.Ambiguous;
        }

        author = string.Empty;
        title = fileNameNoExt.Trim();
        return ParsedAuthorResult.Missing;
    }

    private static ParsedAuthorResult ResolveTwoSegmentName(
        string left,
        string right,
        HashSet<string> catalogTokens,
        out string author,
        out string title)
    {
        var leftScore = CountCatalogTokenMatches(left, catalogTokens);
        var rightScore = CountCatalogTokenMatches(right, catalogTokens);

        if (leftScore > rightScore)
        {
            author = left;
            title = right;
            return ParsedAuthorResult.Resolved;
        }

        if (rightScore > leftScore)
        {
            author = right;
            title = left;
            return ParsedAuthorResult.Resolved;
        }

        var leftLooksAuthor = LooksLikeAuthorPhrase(left);
        var rightLooksAuthor = LooksLikeAuthorPhrase(right);

        if (leftLooksAuthor && !rightLooksAuthor)
        {
            author = left;
            title = right;
            return ParsedAuthorResult.Resolved;
        }

        if (rightLooksAuthor && !leftLooksAuthor)
        {
            author = right;
            title = left;
            return ParsedAuthorResult.Resolved;
        }

        author = left;
        title = right;
        return ParsedAuthorResult.Ambiguous;
    }

    private static List<string> SplitNameSegments(string fileNameNoExt)
    {
        var rawParts = fileNameNoExt.Split(new[] { " - ", " – ", "_" }, StringSplitOptions.RemoveEmptyEntries);
        var segments = new List<string>(rawParts.Length);
        foreach (var part in rawParts)
        {
            var cleaned = part.Trim().Trim('"', '\'', '“', '”');
            if (!string.IsNullOrWhiteSpace(cleaned))
                segments.Add(cleaned);
        }

        while (segments.Count > 2 && IsIgnorableTrailingMetadata(segments[^1]))
            segments.RemoveAt(segments.Count - 1);

        return segments;
    }

    private static bool IsIgnorableTrailingMetadata(string value)
    {
        var normalized = NormalizeForLookup(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return true;

        if (IsAllDigits(normalized.Replace(" ", string.Empty, StringComparison.Ordinal)))
            return true;

        return normalized is "spa" or "esp" or "es" or "eng" or "en" or "fre" or "fra" or "fr"
            or "ger" or "deu" or "de" or "ita" or "it" or "por" or "pt" or "rus" or "ru";
    }

    private static int CountCatalogTokenMatches(string value, HashSet<string> catalogTokens)
    {
        var normalized = NormalizeForLookup(value);
        int matches = 0;
        foreach (var tok in TokensOf(normalized))
        {
            if (catalogTokens.Contains(tok))
                matches++;
        }
        return matches;
    }

    private static bool LooksLikeAuthorPhrase(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var normalized = NormalizeForLookup(value);
        int words = 0;
        foreach (var tok in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (tok.Length >= 2)
                words++;
        }

        if (words >= 2)
            return true;

        return value.Contains(',', StringComparison.Ordinal);
    }

    private static string BuildNormalizedFileName(string author, string title, string ext)
    {
        var safeAuthor = SanitizeForFileName(string.IsNullOrWhiteSpace(author) ? "_unknown" : author);
        var safeTitle = SanitizeForFileName(string.IsNullOrWhiteSpace(title) ? "_unknown" : title);
        return $"{safeAuthor} - {safeTitle}{ext}";
    }

    private static string SanitizeForFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "_";
        var sb = new StringBuilder(Math.Min(name.Length, 100));
        foreach (var c in name.AsSpan(0, Math.Min(name.Length, 100)))
            sb.Append(s_invalidFileNameChars.Contains(c) ? '_' : c);
        var result = sb.ToString().TrimEnd();
        if (result.Length == 0) return "_";
        return result.Normalize(NormalizationForm.FormC);
    }

    /// <summary>Elimina diacríticos y convierte a minúsculas sin puntuación.</summary>
    private static string NormalizeForLookup(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var nfd = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(nfd.Length);
        bool prevSpace = false;
        foreach (var c in nfd)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            var lower = char.ToLowerInvariant(c);
            if (char.IsLetterOrDigit(lower))
            {
                sb.Append(lower);
                prevSpace = false;
            }
            else if (!prevSpace)
            {
                sb.Append(' ');
                prevSpace = true;
            }
        }
        return sb.ToString().Trim();
    }

    /// <summary>Devuelve tokens de longitud ≥ 4, no puramente numéricos, de una cadena ya normalizada.</summary>
    private static IEnumerable<string> TokensOf(string normalized)
    {
        if (string.IsNullOrEmpty(normalized)) yield break;
        foreach (var tok in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (tok.Length >= 4 && !IsAllDigits(tok)) yield return tok;
        }
    }

    private static bool IsAllDigits(string s)
    {
        foreach (var c in s)
            if (!char.IsDigit(c)) return false;
        return true;
    }

    private static List<string> EnumerateDirectoriesRecursive(string srcDir)
    {
        var dirs = new List<string>(512);
        var stack = new Stack<(string Path, int Depth)>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        stack.Push((srcDir, 0));

        while (stack.Count > 0)
        {
            var (dir, depth) = stack.Pop();
            if (depth > 16) continue;

            try
            {
                foreach (var sub in Directory.EnumerateDirectories(dir))
                {
                    var subFull = Path.GetFullPath(sub);
                    if (!visited.Add(subFull))
                        continue;

                    dirs.Add(subFull);
                    stack.Push((subFull, depth + 1));
                }
            }
            catch
            {
            }
        }

        dirs.Sort((a, b) => b.Length.CompareTo(a.Length));
        return dirs;
    }

    private static bool TryDeleteFileSafe(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return true;

            var attrs = File.GetAttributes(path);
            if ((attrs & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);

            File.Delete(path);
            return !File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private static bool CanDeleteDirectory(string dirPath)
    {
        try
        {
            return Directory.Exists(dirPath) && !Directory.EnumerateFileSystemEntries(dirPath).Any();
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDeleteDirectoryIfEmpty(string dirPath)
    {
        try
        {
            if (!Directory.Exists(dirPath))
                return false;

            if (Directory.EnumerateFileSystemEntries(dirPath).Any())
                return false;

            Directory.Delete(dirPath, recursive: false);
            return !Directory.Exists(dirPath);
        }
        catch
        {
            return false;
        }
    }

    internal sealed class CleanResult
    {
        public int Renamed { get; set; }
        public int Deleted { get; set; }
        public int Skipped { get; set; }
        public int ScannedTotal { get; set; }
        public int ScannedBooks { get; set; }
        public int NonBookDeleted { get; set; }
        public int UnknownDeleted { get; set; }
    }

    internal sealed class PurgeResult
    {
        public int RemainingFilesFound { get; set; }
        public int RemainingFilesDeleted { get; set; }
        public int EmptyDirectoriesDeleted { get; set; }
        public int DeleteErrors { get; set; }
    }
}
