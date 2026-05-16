using System.Collections.Concurrent;
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
        public int DeletedInvalid { get; set; }
        public int DeletedProcessed { get; set; }
        public long BytesFreed { get; set; }
        public List<string> InvalidFiles { get; set; } = new();
        public List<string> ProcessedFiles { get; set; } = new();
        public List<FileValidationError> DeletionErrors { get; set; } = new();

        // Estadísticas por tipo de error
        public Dictionary<string, int> InvalidReasons { get; set; } = new();
        public ConcurrentDictionary<DeletionError, int> DeletionErrorCounts { get; set; } = new();
    }

    /// <summary>
    /// Valida PDFs en una carpeta recursivamente
    /// </summary>
    public async Task<ValidationResult> ValidatePdfsAsync(
        string folderPath,
        IProgress<(string message, int done, int total)>? progress = null,
        CancellationToken ct = default)
    {
        var result = new ValidationResult();

        if (!Directory.Exists(folderPath))
        {
            progress?.Report(($"❌ Carpeta no encontrada: {folderPath}", 0, 0));
            return result;
        }

        // Buscar todos los PDFs recursivamente
        var pdfFiles = Directory.GetFiles(folderPath, "*.pdf", SearchOption.AllDirectories).ToList();
        result.TotalPdfs = pdfFiles.Count;

        if (pdfFiles.Count == 0)
        {
            progress?.Report(("ℹ️ No hay PDFs en la carpeta", 0, 0));
            return result;
        }

        progress?.Report(($"🔍 Validando {pdfFiles.Count} PDF(s)...", 0, pdfFiles.Count));

        // Procesar en paralelo con actualización de progreso
        var invalidFiles = new ConcurrentBag<string>();
        var processedFiles = new ConcurrentBag<string>();
        var invalidReasons = new ConcurrentDictionary<string, int>();
        var processedCount = 0;

        await Parallel.ForEachAsync(
            pdfFiles,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = ct
            },
            async (pdfPath, cancellationToken) =>
            {
                ct.ThrowIfCancellationRequested();

                var stem = Path.GetFileNameWithoutExtension(pdfPath);
                var dirPath = Path.GetDirectoryName(pdfPath) ?? string.Empty;

                // Validar que es un PDF válido (magic bytes + EOF)
                var validationError = ValidatePdfFile(pdfPath);
                if (validationError != null)
                {
                    invalidFiles.Add(pdfPath);
                    invalidReasons.AddOrUpdate(validationError, 1, (k, v) => v + 1);
                }
                else
                {
                    // Detectar si ya está procesado
                    var isProcessed = IsAlreadyProcessed(dirPath, stem);
                    if (isProcessed)
                    {
                        processedFiles.Add(pdfPath);
                    }
                }

                // Actualizar progreso
                Interlocked.Increment(ref processedCount);
                progress?.Report(($"🔍 Validados {processedCount}/{pdfFiles.Count}...", processedCount, pdfFiles.Count));
            }).ConfigureAwait(false);

        result.InvalidFiles = invalidFiles.ToList();
        result.ProcessedFiles = processedFiles.ToList();
        result.InvalidPdfs = result.InvalidFiles.Count;
        result.ProcessedPdfs = result.ProcessedFiles.Count;
        result.InvalidReasons = invalidReasons.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        progress?.Report(($"📊 Resultado: {result.InvalidPdfs} inválidos, {result.ProcessedPdfs} procesados", result.TotalPdfs, result.TotalPdfs));

        return result;
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
        var result = await ValidatePdfsAsync(folderPath, progress, ct).ConfigureAwait(false);

        if (result.InvalidPdfs + result.ProcessedPdfs == 0)
        {
            progress?.Report(("✅ Todos los PDFs son válidos y no procesados", result.TotalPdfs, result.TotalPdfs));
            return result;
        }

        var modeLabel = dryRun ? "[DRY-RUN]" : string.Empty;
        int totalToDelete = result.InvalidPdfs + result.ProcessedPdfs;

        // Borrar PDFs inválidos
        int deletedCount = 0;
        foreach (var pdfPath in result.InvalidFiles)
        {
            if (dryRun)
            {
                var size = new FileInfo(pdfPath).Length;
                result.BytesFreed += size;
                result.DeletedInvalid++;
                deletedCount++;
                progress?.Report(($"{modeLabel} 🗑️ [SIMULADO] PDF inválido: {Path.GetFileName(pdfPath)} ({FormatBytes(size)})", deletedCount, totalToDelete));
            }
            else
            {
                var delError = TryDeleteFile(pdfPath, out var errorType);
                if (delError.IsSuccess)
                {
                    result.BytesFreed += delError.BytesFreed;
                    result.DeletedInvalid++;
                    deletedCount++;
                    progress?.Report(($"🗑️ Borrado PDF inválido: {Path.GetFileName(pdfPath)} ({FormatBytes(delError.BytesFreed)})", deletedCount, totalToDelete));
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
                    progress?.Report(($"⚠️ No se pudo borrar {Path.GetFileName(pdfPath)}: {delError.ErrorMessage}", deletedCount, totalToDelete));
                }
            }
        }

        // Borrar PDFs ya procesados
        foreach (var pdfPath in result.ProcessedFiles)
        {
            if (dryRun)
            {
                var size = new FileInfo(pdfPath).Length;
                result.BytesFreed += size;
                result.DeletedProcessed++;
                deletedCount++;
                progress?.Report(($"{modeLabel} 🗑️ [SIMULADO] PDF procesado: {Path.GetFileName(pdfPath)} ({FormatBytes(size)})", deletedCount, totalToDelete));
            }
            else
            {
                var delError = TryDeleteFile(pdfPath, out var errorType);
                if (delError.IsSuccess)
                {
                    result.BytesFreed += delError.BytesFreed;
                    result.DeletedProcessed++;
                    deletedCount++;
                    progress?.Report(($"🗑️ Borrado PDF procesado: {Path.GetFileName(pdfPath)} ({FormatBytes(delError.BytesFreed)})", deletedCount, totalToDelete));
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
                    progress?.Report(($"⚠️ No se pudo borrar {Path.GetFileName(pdfPath)}: {delError.ErrorMessage}", deletedCount, totalToDelete));
                }
            }
        }

        var finalMsg = dryRun
            ? $"✅ [DRY-RUN] Se borrarían {result.DeletedInvalid} inválidos + {result.DeletedProcessed} procesados = {FormatBytes(result.BytesFreed)}"
            : $"✅ Borrados {result.DeletedInvalid} inválidos + {result.DeletedProcessed} procesados = {FormatBytes(result.BytesFreed)} liberados";
        progress?.Report((finalMsg, result.TotalPdfs, result.TotalPdfs));

        return result;
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
    private static bool IsAlreadyProcessed(string dirPath, string pdfStem)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath))
                return false;

            var extensionsToCheck = new[] { ".mxl", ".xml", ".musicxml", ".mscz", ".mscx" };
            var extensionSet = new HashSet<string>(extensionsToCheck, StringComparer.OrdinalIgnoreCase);

            try
            {
                var files = Directory.GetFiles(dirPath, "*", SearchOption.TopDirectoryOnly);
                foreach (var filePath in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var fileExt = Path.GetExtension(filePath);

                    // Comprobar si el stem coincide (mismo nombre o con _suffix como "stem_a4")
                    if ((fileName == pdfStem || fileName.StartsWith(pdfStem + "_", StringComparison.OrdinalIgnoreCase))
                        && extensionSet.Contains(fileExt))
                    {
                        return true;
                    }
                }
            }
            catch (DirectoryNotFoundException)
            {
                // Directorio fue eliminado entre el chequeo y la búsqueda
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // Sin permisos para listar
                return false;
            }

            return false;
        }
        catch
        {
            // Cualquier otro error → asumir no procesado (fail-safe)
            return false;
        }
    }

    /// <summary>
    /// Intenta borrar un archivo con manejo de errores y retry logic para archivos en uso
    /// </summary>
    private static (bool IsSuccess, long BytesFreed, string ErrorMessage) TryDeleteFile(string filePath, out DeletionError errorType)
    {
        errorType = DeletionError.None;

        try
        {
            if (!File.Exists(filePath))
            {
                errorType = DeletionError.FileNotFound;
                return (false, 0, "Archivo no encontrado");
            }

            long bytesToFree = 0;
            try
            {
                var fileInfo = new FileInfo(filePath);
                bytesToFree = fileInfo.Length;
            }
            catch (FileNotFoundException)
            {
                errorType = DeletionError.FileNotFound;
                return (false, 0, "Archivo desapareció antes de borrar");
            }
            catch (UnauthorizedAccessException)
            {
                errorType = DeletionError.PermissionDenied;
                return (false, 0, "Permisos insuficientes para leer atributos");
            }

            // Retry logic: archivos en uso pueden liberarse en 50ms
            const int maxRetries = 3;
            const int delayMs = 50;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    File.Delete(filePath);
                    return (true, bytesToFree, string.Empty);
                }
                catch (IOException ex) when ((ex.HResult == -2147024864 || ex.HResult == -2147024816) && attempt < maxRetries - 1)
                {
                    // Archivo en uso: -2147024864 (0x80070020), -2147024816 (0x80070050)
                    System.Threading.Thread.Sleep(delayMs);
                    continue;
                }
                catch (IOException ex) when (ex.HResult == -2147024784)
                {
                    errorType = DeletionError.DiskFull;
                    return (false, 0, "Disco lleno o error de escritura");
                }
                catch (IOException ex)
                {
                    errorType = DeletionError.FileInUse;
                    return (false, 0, $"Archivo en uso: {ex.Message}");
                }
            }

            errorType = DeletionError.FileInUse;
            return (false, 0, "Archivo sigue en uso después de reintentos");
        }
        catch (UnauthorizedAccessException)
        {
            errorType = DeletionError.PermissionDenied;
            return (false, 0, "Permisos insuficientes");
        }
        catch (Exception ex)
        {
            errorType = DeletionError.Other;
            return (false, 0, $"{ex.GetType().Name}: {ex.Message}");
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

