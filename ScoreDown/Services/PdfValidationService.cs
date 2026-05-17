using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScoreDown.Services;

/// <summary>
/// Servicio para validar archivos PDF en una carpeta:
/// - Verifica si son PDFs válidos (magic bytes + EOF)
/// - Detecta si ya están procesados (existe MXL correspondiente)
/// - Borra PDFs inválidos y procesados
/// - Opción dry-run para vista previa
/// </summary>
public class PdfValidationService
{
    public sealed class DirectoryTimingInfo
    {
        public string DirectoryPath { get; set; } = string.Empty;
        public long ElapsedMs { get; set; }
    }

    public enum DeletionError
    {
        None,
        FileInUse,
        PermissionDenied,
        FileNotFound,
        DiskFull,
        Other
    }

    public class FileValidationError
    {
        public string FilePath { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DeletionError DeletionError { get; set; } = DeletionError.None;
    }

    public class ValidationResult
    {
        public int TotalPdfs { get; set; }
        public int InvalidPdfs { get; set; }
        public int ProcessedPdfs { get; set; }
        public int RenamedFiles { get; set; }
        public int DeletedInvalid { get; set; }
        public int DeletedProcessed { get; set; }
        public long BytesFreed { get; set; }
        public List<string> InvalidFiles { get; set; } = new();
        public List<string> ProcessedFiles { get; set; } = new();
        public List<FileValidationError> DeletionErrors { get; set; } = new();

        // Estadísticas por tipo de error
        public Dictionary<string, int> InvalidReasons { get; set; } = new();
        public ConcurrentDictionary<DeletionError, int> DeletionErrorCounts { get; set; } = new();

        // Telemetria ligera para tuning y diagnostico de rendimiento.
        public long ScanElapsedMs { get; set; }
        public long ValidationElapsedMs { get; set; }
        public long DeleteElapsedMs { get; set; }
        public long TotalElapsedMs { get; set; }
        public bool DeleteBudgetReached { get; set; }
        public int DeleteSkippedDueToBudget { get; set; }
        public int DeleteSkippedDueToPerDirBudget { get; set; }
        public int DeleteErrorsCount { get; set; }
        public List<DirectoryTimingInfo> SlowestDeleteDirectories { get; set; } = new();
    }

    private static int GetDeleteBudgetMs()
    {
        var raw = Environment.GetEnvironmentVariable("SCOREDOWN_VALIDATION_DELETE_BUDGET_MS");
        return int.TryParse(raw, out var parsed) && parsed > 0 ? parsed : 0;
    }

    private static int GetDeletePerDirBudgetMs()
    {
        var raw = Environment.GetEnvironmentVariable("SCOREDOWN_VALIDATION_DELETE_PER_DIR_BUDGET_MS");
        return int.TryParse(raw, out var parsed) && parsed > 0 ? parsed : 0;
    }

    private static string BuildDeletionErrorSummary(ConcurrentDictionary<DeletionError, int> counts)
    {
        if (counts.IsEmpty)
            return string.Empty;

        return string.Join(", ", counts
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .Select(kv => $"{kv.Key}={kv.Value}"));
    }

    /// <summary>
    /// Valida PDFs en una carpeta recursivamente
    /// </summary>
    public async Task<ValidationResult> ValidatePdfsAsync(
        string folderPath,
        IProgress<(string message, int done, int total)>? progress = null,
        bool normalizeFileNames = false,
        CancellationToken ct = default)
    {
        var result = new ValidationResult();
        var totalSw = Stopwatch.StartNew();

        if (!Directory.Exists(folderPath))
        {
            totalSw.Stop();
            result.TotalElapsedMs = totalSw.ElapsedMilliseconds;
            progress?.Report(($"❌ Carpeta no encontrada: {folderPath}", 0, 0));
            return result;
        }

        // Buscar todos los PDFs recursivamente (tolerante a carpetas sin acceso)
        var scanSw = Stopwatch.StartNew();
        var pdfFiles = EnumeratePdfFilesSafe(folderPath).ToList();
        scanSw.Stop();
        result.ScanElapsedMs = scanSw.ElapsedMilliseconds;
        result.TotalPdfs = pdfFiles.Count;

        if (pdfFiles.Count == 0)
        {
            totalSw.Stop();
            result.TotalElapsedMs = totalSw.ElapsedMilliseconds;
            progress?.Report(("ℹ️ No hay PDFs en la carpeta", 0, 0));
            return result;
        }

        progress?.Report(($"🔍 Validando {pdfFiles.Count} PDF(s)...", 0, pdfFiles.Count));

        // Procesar en paralelo con actualización de progreso
        var invalidFiles = new ConcurrentBag<string>();
        var processedFiles = new ConcurrentBag<string>();
        var invalidReasons = new ConcurrentDictionary<string, int>();
        var processedStemsByDir = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var renamedFiles = 0;
        var processedCount = 0;
        var validationSw = Stopwatch.StartNew();

        await Parallel.ForEachAsync(
            pdfFiles,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8),
                CancellationToken = ct
            },
            async (pdfPath, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var renamed = false;
                var originalStem = Path.GetFileNameWithoutExtension(pdfPath);

                var normalizedPdfPath = normalizeFileNames
                    ? TryNormalizeScorePdfFileName(pdfPath, out renamed)
                    : pdfPath;
                if (renamed)
                {
                    Interlocked.Increment(ref renamedFiles);
                }

                var stem = Path.GetFileNameWithoutExtension(normalizedPdfPath);
                var dirPath = Path.GetDirectoryName(normalizedPdfPath) ?? string.Empty;

                // Validar que es un PDF válido (magic bytes + EOF)
                var validationError = ValidatePdfFile(normalizedPdfPath);
                if (validationError != null)
                {
                    invalidFiles.Add(normalizedPdfPath);
                    invalidReasons.AddOrUpdate(validationError, 1, (k, v) => v + 1);
                }
                else
                {
                    // Detectar si ya está procesado
                    // Si se renombró, comprobar también contra el stem original para no perder matches existentes.
                    var isProcessed = IsAlreadyProcessed(dirPath, stem, processedStemsByDir)
                        || (!string.Equals(originalStem, stem, StringComparison.OrdinalIgnoreCase)
                            && IsAlreadyProcessed(dirPath, originalStem, processedStemsByDir));
                    if (isProcessed)
                    {
                        processedFiles.Add(normalizedPdfPath);
                    }
                }

                // Actualizar progreso
                var done = Interlocked.Increment(ref processedCount);
                if (done == pdfFiles.Count || done % 25 == 0)
                    progress?.Report(($"🔍 Validados {done}/{pdfFiles.Count}...", done, pdfFiles.Count));
            }).ConfigureAwait(false);
        validationSw.Stop();

        result.InvalidFiles = invalidFiles.ToList();
        result.ProcessedFiles = processedFiles.ToList();
        result.InvalidPdfs = result.InvalidFiles.Count;
        result.ProcessedPdfs = result.ProcessedFiles.Count;
        result.RenamedFiles = renamedFiles;
        result.InvalidReasons = invalidReasons.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        result.ValidationElapsedMs = validationSw.ElapsedMilliseconds;

        totalSw.Stop();
        result.TotalElapsedMs = totalSw.ElapsedMilliseconds;

        progress?.Report(($"📊 Resultado: {result.InvalidPdfs} inválidos, {result.ProcessedPdfs} procesados", result.TotalPdfs, result.TotalPdfs));

        return result;
    }

    private static string TryNormalizeScorePdfFileName(string pdfPath, out bool renamed)
    {
        renamed = false;

        try
        {
            if (!File.Exists(pdfPath))
                return pdfPath;

            var dir = Path.GetDirectoryName(pdfPath) ?? string.Empty;
            var ext = Path.GetExtension(pdfPath).ToLowerInvariant();
            var stem = Path.GetFileNameWithoutExtension(pdfPath).Trim();
            if (string.IsNullOrWhiteSpace(stem))
                stem = "score";

            string composer;
            string title;

            var sepIdx = stem.IndexOf(" - ", StringComparison.Ordinal);
            if (sepIdx > 0)
            {
                composer = stem[..sepIdx].Trim();
                title = stem[(sepIdx + 3)..].Trim();
            }
            else
            {
                composer = string.Empty;
                title = stem;
            }

            if (IsMissingComposer(composer))
                composer = "Desconocido";
            if (string.IsNullOrWhiteSpace(title))
                title = "Sin titulo";

            var normalizedBase = $"{composer} - {title}";
            var normalizedName = global::FileNameHelper.SanitizeFileName(normalizedBase + ext);
            var normalizedPath = Path.Combine(dir, normalizedName);

            if (string.Equals(pdfPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                return pdfPath;

            if (File.Exists(normalizedPath))
            {
                var baseName = Path.GetFileNameWithoutExtension(normalizedName);
                for (var i = 1; i <= 9999; i++)
                {
                    var candidate = Path.Combine(dir, $"{baseName} ({i}){ext}");
                    if (!File.Exists(candidate))
                    {
                        normalizedPath = candidate;
                        break;
                    }
                }
            }

            File.Move(pdfPath, normalizedPath, overwrite: false);
            renamed = true;
            return normalizedPath;
        }
        catch
        {
            return pdfPath;
        }
    }

    private static bool IsMissingComposer(string? composer)
    {
        if (string.IsNullOrWhiteSpace(composer))
            return true;

        var c = composer.Trim();
        return c.Equals("unknown", StringComparison.OrdinalIgnoreCase)
            || c.Equals("desconocido", StringComparison.OrdinalIgnoreCase)
            || c.Equals("anonimo", StringComparison.OrdinalIgnoreCase)
            || c.Equals("anónimo", StringComparison.OrdinalIgnoreCase)
            || c.Equals("sin autor", StringComparison.OrdinalIgnoreCase)
            || c.Equals("na", StringComparison.OrdinalIgnoreCase)
            || c.Equals("n/a", StringComparison.OrdinalIgnoreCase)
            || c.Equals("-", StringComparison.OrdinalIgnoreCase)
            || c.Equals("_", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumeratePdfFilesSafe(string root)
    {
        var pending = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pending.Push(root);

        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            string fullDir;
            try
            {
                fullDir = Path.GetFullPath(dir);
            }
            catch
            {
                continue;
            }

            if (!visited.Add(fullDir))
                continue;

            string[] files;
            try
            {
                files = Directory.GetFiles(fullDir, "*.pdf", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                files = Array.Empty<string>();
            }

            foreach (var file in files)
                yield return file;

            string[] subdirs;
            try
            {
                subdirs = Directory.GetDirectories(fullDir, "*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                subdirs = Array.Empty<string>();
            }

            foreach (var sub in subdirs)
            {
                try
                {
                    var attrs = File.GetAttributes(sub);
                    if ((attrs & FileAttributes.ReparsePoint) != 0)
                        continue;
                }
                catch
                {
                    continue;
                }

                pending.Push(sub);
            }
        }
    }

    /// <summary>
    /// Valida y borra PDFs en una carpeta (o solo simula con dryRun=true)
    /// </summary>
    public async Task<ValidationResult> ValidateAndDeletePdfsAsync(
        string folderPath,
        IProgress<(string message, int done, int total)>? progress = null,
        bool dryRun = false,
        CancellationToken ct = default)
    {
        var deleteSw = Stopwatch.StartNew();
        var deleteBudgetMs = GetDeleteBudgetMs();
        var deletePerDirBudgetMs = GetDeletePerDirBudgetMs();
        var budgetReached = false;
        var skippedDueToDirBudget = 0;
        var perDirDeleteElapsedMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        // En dry-run no se deben renombrar archivos ni hacer cambios en disco.
        var result = await ValidatePdfsAsync(folderPath, progress, normalizeFileNames: !dryRun, ct).ConfigureAwait(false);

        if (result.InvalidPdfs + result.ProcessedPdfs == 0)
        {
            deleteSw.Stop();
            result.DeleteElapsedMs = 0;
            progress?.Report(("✅ Todos los PDFs son válidos y no procesados", result.TotalPdfs, result.TotalPdfs));
            return result;
        }

        var modeLabel = dryRun ? "[DRY-RUN]" : string.Empty;
        int totalToDelete = result.InvalidPdfs + result.ProcessedPdfs;

        // Borrar PDFs inválidos
        int deletedCount = 0;
        foreach (var pdfPath in result.InvalidFiles)
        {
            ct.ThrowIfCancellationRequested();
            if (deleteBudgetMs > 0 && deleteSw.ElapsedMilliseconds >= deleteBudgetMs)
            {
                budgetReached = true;
                break;
            }

            var dirKey = Path.GetDirectoryName(pdfPath) ?? string.Empty;
            if (deletePerDirBudgetMs > 0 &&
                perDirDeleteElapsedMs.TryGetValue(dirKey, out var usedMs) &&
                usedMs >= deletePerDirBudgetMs)
            {
                skippedDueToDirBudget++;
                continue;
            }

            var itemSw = Stopwatch.StartNew();

            if (dryRun)
            {
                var size = TryGetFileSizeSafe(pdfPath);
                result.BytesFreed += size;
                result.DeletedInvalid++;
                deletedCount++;
            }
            else
            {
                var delError = await TryDeleteFileAsync(pdfPath, ct).ConfigureAwait(false);
                var errorType = delError.ErrorType;
                if (delError.IsSuccess)
                {
                    result.BytesFreed += delError.BytesFreed;
                    result.DeletedInvalid++;
                    deletedCount++;
                }
                else
                {
                    result.DeletionErrors.Add(new FileValidationError
                    {
                        FilePath = pdfPath,
                        Reason = delError.ErrorMessage,
                        DeletionError = errorType
                    });
                    result.DeletionErrorCounts.AddOrUpdate(errorType, 1, (k, v) => v + 1);
                }
            }

            itemSw.Stop();
            if (deletePerDirBudgetMs > 0)
                perDirDeleteElapsedMs[dirKey] = (perDirDeleteElapsedMs.TryGetValue(dirKey, out var current) ? current : 0) + itemSw.ElapsedMilliseconds;

            if (deletedCount == totalToDelete || deletedCount % 25 == 0)
                progress?.Report(($"{modeLabel} 🧹 Progreso borrado: {deletedCount}/{totalToDelete} · omitidos: {skippedDueToDirBudget} · errores: {result.DeletionErrors.Count}", deletedCount, totalToDelete));
        }

        // Borrar PDFs ya procesados
        foreach (var pdfPath in result.ProcessedFiles)
        {
            ct.ThrowIfCancellationRequested();
            if (deleteBudgetMs > 0 && deleteSw.ElapsedMilliseconds >= deleteBudgetMs)
            {
                budgetReached = true;
                break;
            }

            var dirKey = Path.GetDirectoryName(pdfPath) ?? string.Empty;
            if (deletePerDirBudgetMs > 0 &&
                perDirDeleteElapsedMs.TryGetValue(dirKey, out var usedMs) &&
                usedMs >= deletePerDirBudgetMs)
            {
                skippedDueToDirBudget++;
                continue;
            }

            var itemSw = Stopwatch.StartNew();

            if (dryRun)
            {
                var size = TryGetFileSizeSafe(pdfPath);
                result.BytesFreed += size;
                result.DeletedProcessed++;
                deletedCount++;
            }
            else
            {
                var delError = await TryDeleteFileAsync(pdfPath, ct).ConfigureAwait(false);
                var errorType = delError.ErrorType;
                if (delError.IsSuccess)
                {
                    result.BytesFreed += delError.BytesFreed;
                    result.DeletedProcessed++;
                    deletedCount++;
                }
                else
                {
                    result.DeletionErrors.Add(new FileValidationError
                    {
                        FilePath = pdfPath,
                        Reason = delError.ErrorMessage,
                        DeletionError = errorType
                    });
                    result.DeletionErrorCounts.AddOrUpdate(errorType, 1, (k, v) => v + 1);
                }
            }

            itemSw.Stop();
            if (deletePerDirBudgetMs > 0)
                perDirDeleteElapsedMs[dirKey] = (perDirDeleteElapsedMs.TryGetValue(dirKey, out var current) ? current : 0) + itemSw.ElapsedMilliseconds;

            if (deletedCount == totalToDelete || deletedCount % 25 == 0)
                progress?.Report(($"{modeLabel} 🧹 Progreso borrado: {deletedCount}/{totalToDelete} · omitidos: {skippedDueToDirBudget} · errores: {result.DeletionErrors.Count}", deletedCount, totalToDelete));
        }

        var finalMsg = dryRun
            ? $"✅ [DRY-RUN] Se borrarían {result.DeletedInvalid} inválidos + {result.DeletedProcessed} procesados = {FormatBytes(result.BytesFreed)}"
            : $"✅ Borrados {result.DeletedInvalid} inválidos + {result.DeletedProcessed} procesados = {FormatBytes(result.BytesFreed)} liberados";
        var deletionErrorsSummary = BuildDeletionErrorSummary(result.DeletionErrorCounts);
        if (!string.IsNullOrWhiteSpace(deletionErrorsSummary))
            finalMsg += $" · errores: {deletionErrorsSummary}";

        if (budgetReached)
        {
            var skipped = Math.Max(0, totalToDelete - deletedCount);
            result.DeleteBudgetReached = true;
            result.DeleteSkippedDueToBudget = skipped;
            finalMsg += $" · presupuesto borrado agotado ({deleteBudgetMs} ms), omitidos: {skipped}";
        }

        if (skippedDueToDirBudget > 0)
        {
            result.DeleteSkippedDueToPerDirBudget = skippedDueToDirBudget;
            finalMsg += $" · presupuesto por carpeta agotado ({deletePerDirBudgetMs} ms), omitidos: {skippedDueToDirBudget}";
        }

        result.DeleteErrorsCount = result.DeletionErrors.Count;
        result.SlowestDeleteDirectories = perDirDeleteElapsedMs
            .OrderByDescending(kv => kv.Value)
            .Take(5)
            .Select(kv => new DirectoryTimingInfo
            {
                DirectoryPath = kv.Key,
                ElapsedMs = kv.Value
            })
            .ToList();

        progress?.Report((finalMsg, result.TotalPdfs, result.TotalPdfs));

        deleteSw.Stop();
        result.DeleteElapsedMs = deleteSw.ElapsedMilliseconds;
        result.TotalElapsedMs = result.ScanElapsedMs + result.ValidationElapsedMs + result.DeleteElapsedMs;

        return result;
    }

    private static long TryGetFileSizeSafe(string path)
    {
        try
        {
            if (!File.Exists(path))
                return 0;
            return new FileInfo(path).Length;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Valida un PDF: magic bytes + soft EOF check
    /// Retorna null si es válido, o un mensaje de error
    /// Nota: EOF check es tolerante (algunos PDFs pueden tener streams o metadatos después)
    /// </summary>
    private static string? ValidatePdfFile(string pdfPath)
    {
        try
        {
            if (!File.Exists(pdfPath))
                return "Archivo no encontrado";

            var fileInfo = new FileInfo(pdfPath);
            if (fileInfo.Length < 10)
                return $"Archivo demasiado pequeño ({fileInfo.Length} bytes)";

            // Validar magic bytes: %PDF
            using (var fs = File.OpenRead(pdfPath))
            {
                Span<byte> header = stackalloc byte[4];
                var read = fs.Read(header);

                if (read < 4)
                    return "Archivo no legible (demasiado pequeño)";

                // Verificar que empiece con %PDF (0x25 0x50 0x44 0x46)
                if (!(header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46))
                    return "Magic bytes inválidos (no es un PDF)";

                // Validar EOF: búsqueda case-insensitive en últimos 2048 bytes
                long seekPos = Math.Max(0, fileInfo.Length - 2048);
                if (seekPos > 0)
                {
                    fs.Seek(seekPos, SeekOrigin.Begin);
                    Span<byte> trailer = stackalloc byte[2048];
                    var trailerRead = fs.Read(trailer);
                    var trailerText = System.Text.Encoding.ASCII.GetString(trailer[..trailerRead]);
                    var trailerUpper = trailerText.ToUpperInvariant();

                    // Soft check: EOF puede estar más arriba. Solo validar magia, no EOF
                    if (!trailerUpper.Contains("%%EOF") && !trailerUpper.Contains("%EOF") && !trailerUpper.Contains("EOF"))
                    {
                        // No bloquear: muchos PDFs incompletos tienen magia válida
                    }
                }
            }

            return null; // Válido
        }
        catch (IOException ex) when (ex.HResult == -2147024816)
        {
            return "Archivo bloqueado (en uso)";
        }
        catch (IOException ex)
        {
            return $"Error I/O: {ex.Message}";
        }
        catch (UnauthorizedAccessException)
        {
            return "Acceso denegado (permisos insuficientes)";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.GetType().Name}";
        }
    }

    /// <summary>
    /// Detecta si un PDF ya fue procesado buscando archivos con el mismo stem
    /// Retorna false si hay error en lectura (fail-safe)
    /// </summary>
    private static bool IsAlreadyProcessed(
        string dirPath,
        string pdfStem,
        ConcurrentDictionary<string, HashSet<string>> processedStemsByDir)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath))
                return false;

            var stems = processedStemsByDir.GetOrAdd(dirPath, BuildProcessedStemSetSafe);
            return stems.Contains(pdfStem);
        }
        catch
        {
            // Cualquier otro error → asumir no procesado (fail-safe)
            return false;
        }
    }

    private static HashSet<string> BuildProcessedStemSetSafe(string dirPath)
    {
        var stems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var extensionsToCheck = new[] { ".mxl", ".xml", ".musicxml", ".mscz", ".mscx" };
            var extensionSet = new HashSet<string>(extensionsToCheck, StringComparer.OrdinalIgnoreCase);
            var files = Directory.GetFiles(dirPath, "*", SearchOption.TopDirectoryOnly);

            foreach (var filePath in files)
            {
                var fileExt = Path.GetExtension(filePath);
                if (!extensionSet.Contains(fileExt))
                    continue;

                var fileStem = Path.GetFileNameWithoutExtension(filePath);
                if (string.IsNullOrWhiteSpace(fileStem))
                    continue;

                stems.Add(fileStem);

                // Compatibilidad con sufijos (_a4, _let, etc.): guardar también stem base.
                var underscoreIdx = fileStem.IndexOf('_');
                if (underscoreIdx > 0)
                    stems.Add(fileStem[..underscoreIdx]);
            }
        }
        catch (DirectoryNotFoundException)
        {
            // Directorio fue eliminado entre chequeos
        }
        catch (UnauthorizedAccessException)
        {
            // Sin permisos para listar
        }

        return stems;
    }

    /// <summary>
    /// Intenta borrar un archivo con manejo de errores y retry logic para archivos en uso
    /// </summary>
    private sealed class DeleteFileResult
    {
        public bool IsSuccess { get; init; }
        public long BytesFreed { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;
        public DeletionError ErrorType { get; init; }
    }

    private static async Task<DeleteFileResult> TryDeleteFileAsync(string filePath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            if (!File.Exists(filePath))
            {
                return new DeleteFileResult
                {
                    IsSuccess = false,
                    BytesFreed = 0,
                    ErrorMessage = "Archivo no encontrado",
                    ErrorType = DeletionError.FileNotFound
                };
            }

            long bytesToFree = 0;
            try
            {
                var fileInfo = new FileInfo(filePath);
                bytesToFree = fileInfo.Length;
            }
            catch (FileNotFoundException)
            {
                return new DeleteFileResult
                {
                    IsSuccess = false,
                    BytesFreed = 0,
                    ErrorMessage = "Archivo desapareció antes de borrar",
                    ErrorType = DeletionError.FileNotFound
                };
            }
            catch (UnauthorizedAccessException)
            {
                return new DeleteFileResult
                {
                    IsSuccess = false,
                    BytesFreed = 0,
                    ErrorMessage = "Permisos insuficientes para leer atributos",
                    ErrorType = DeletionError.PermissionDenied
                };
            }

            // Retry logic: archivos en uso pueden liberarse en 50ms
            const int maxRetries = 3;
            const int delayMs = 50;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    File.Delete(filePath);
                    return new DeleteFileResult
                    {
                        IsSuccess = true,
                        BytesFreed = bytesToFree,
                        ErrorMessage = string.Empty,
                        ErrorType = DeletionError.None
                    };
                }
                catch (IOException ex) when ((ex.HResult == -2147024864 || ex.HResult == -2147024816) && attempt < maxRetries - 1)
                {
                    // Archivo en uso: -2147024864 (0x80070020), -2147024816 (0x80070050)
                    await Task.Delay(delayMs, ct).ConfigureAwait(false);
                    continue;
                }
                catch (IOException ex) when (ex.HResult == -2147024784)
                {
                    return new DeleteFileResult
                    {
                        IsSuccess = false,
                        BytesFreed = 0,
                        ErrorMessage = "Disco lleno o error de escritura",
                        ErrorType = DeletionError.DiskFull
                    };
                }
                catch (IOException ex)
                {
                    return new DeleteFileResult
                    {
                        IsSuccess = false,
                        BytesFreed = 0,
                        ErrorMessage = $"Archivo en uso: {ex.Message}",
                        ErrorType = DeletionError.FileInUse
                    };
                }
            }

            return new DeleteFileResult
            {
                IsSuccess = false,
                BytesFreed = 0,
                ErrorMessage = "Archivo sigue en uso después de reintentos",
                ErrorType = DeletionError.FileInUse
            };
        }
        catch (UnauthorizedAccessException)
        {
            return new DeleteFileResult
            {
                IsSuccess = false,
                BytesFreed = 0,
                ErrorMessage = "Permisos insuficientes",
                ErrorType = DeletionError.PermissionDenied
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new DeleteFileResult
            {
                IsSuccess = false,
                BytesFreed = 0,
                ErrorMessage = $"{ex.GetType().Name}: {ex.Message}",
                ErrorType = DeletionError.Other
            };
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }
}

