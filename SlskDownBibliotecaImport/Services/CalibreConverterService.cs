using System;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Frozen;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Serilog;
using UglyToad.PdfPig;

namespace SlskDownBibliotecaImport.Services
{
    public sealed partial class CalibreConverterService : IDisposable
    {
        private string? _ebookConvertPath;

        // Extensiones que Calibre puede convertir a TXT, en orden de preferencia
        private static readonly FrozenSet<string> s_convertibleExtensions = FrozenSet.ToFrozenSet(new[] { ".epub", ".mobi", ".azw3", ".azw", ".fb2", ".lit", ".pdb", ".pdf", ".doc", ".docx", ".odt", ".rtf", ".html", ".cbz", ".djvu", ".djv" }, StringComparer.OrdinalIgnoreCase);

        // opt187: FrozenSet estático para excludeDirs en ConvertLibraryAsync
        private static readonly FrozenSet<string> s_convertExcludeDirs = FrozenSet.ToFrozenSet(
            new[] { "incomplete", "_NoEspañol", "_NoPublicos" }, StringComparer.OrdinalIgnoreCase);
        // fix162: _filesToConvertBuf/_txtFilesBuf/_calibreFilesBuf/_pythonVarsBuf eliminados — listas locales; async sin lock.

        // Orden de preferencia para seleccionar la mejor versión
        private static readonly string[] s_extensionPriority = { ".epub", ".mobi", ".azw3", ".azw", ".fb2", ".lit", ".pdb", ".pdf", ".doc", ".docx", ".odt", ".rtf", ".html", ".cbz", ".djvu", ".djv" };

        // Extensiones que ya son texto pero necesitan limpieza TXT
        private static readonly FrozenSet<string> s_textExtensions = FrozenSet.ToFrozenSet(new[] { ".txt", ".text" }, StringComparer.OrdinalIgnoreCase);

        // Todas las extensiones que procesamos
        private static readonly FrozenSet<string> s_allSupportedExtensions = FrozenSet.ToFrozenSet(new[] { ".epub", ".mobi", ".azw3", ".azw", ".fb2", ".lit", ".pdb", ".pdf", ".doc", ".docx", ".odt", ".rtf", ".html", ".cbz", ".djvu", ".djv", ".txt", ".text" }, StringComparer.OrdinalIgnoreCase);

        // Rutas comunes de Calibre en Windows
        private static readonly string[] s_calibrePaths =
        {
            @"C:\Program Files\Calibre2\ebook-convert.exe",
            @"C:\Program Files (x86)\Calibre2\ebook-convert.exe",
            @"C:\Program Files\Calibre\ebook-convert.exe",
            @"C:\Program Files (x86)\Calibre\ebook-convert.exe",
        };

        // Regex compilados para CleanTextForReading (se usan miles de veces)
        private static readonly Regex s_rxPageNumbers = new(@"(?m)^\s*\d{1,4}\s*$", RegexOptions.Compiled);
        private static readonly Regex s_rxDecorative = new(@"(?m)^[\s\-–—=_\*\.]{3,}\s*$", RegexOptions.Compiled);
        private static readonly Regex s_rxFootnoteBracket = new(@"\[\d+\]", RegexOptions.Compiled);
        private static readonly Regex s_rxFootnoteParen = new(@"\(\d+\)", RegexOptions.Compiled);
        private static readonly Regex s_rxMultiNewline = new(@"\n{3,}", RegexOptions.Compiled);
        private static readonly Regex s_rxMultiSpace = new(@"[ \t]{2,}", RegexOptions.Compiled);
        private static readonly Regex s_rxBrokenLines = new(@"(?m)([a-záéíóúñüàèìòùâêîôûäëïöü,;])\s*\n\s*([a-záéíóúñüàèìòùâêîôûäëïöü])", RegexOptions.Compiled);
        private static readonly Regex s_rxHyphenation = new(@"(\w)-\s*\n\s*(\w)", RegexOptions.Compiled);
        private static readonly Regex s_rxLeadingWhitespace = new(@"(?m)^[ \t]+", RegexOptions.Compiled);
        private static readonly Regex s_rxTrailingWhitespace = new(@"(?m)[ \t]+$", RegexOptions.Compiled);

        // Regex compilados para NormalizeBookName
        private static readonly Regex s_rxFormatSuffix1 = new(@"\s*[\(\[]\s*(epub|mobi|pdf|azw3?|fb2|lit|txt)\s*[\)\]]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_rxFormatSuffix2 = new(@"\s*-\s*(epub|mobi|pdf|azw3?|fb2|lit|txt)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Configuración de paralelismo
        private const int MAX_PARALLEL_CALIBRE = 8;
        private const int MAX_PARALLEL_TXT_CLEAN = 8;
        private const int MAX_CONSECUTIVE_ERRORS = 20;
        private const int CALIBRE_TIMEOUT_MS = 300_000; // 5 min
        private const long MAX_FILE_SIZE_FOR_MEMORY = 50 * 1024 * 1024; // 50 MB

        // Semáforo estático compartido: limita el total de procesos Calibre simultáneos
        // entre la conversión masiva (ConvertAllToTxtAsync) y el auto-TXT por descarga individual.
        // Así nunca se lanzan más de MAX_PARALLEL_CALIBRE procesos ebook-convert en paralelo.
        private static readonly SemaphoreSlim s_calibreGlobalSemaphore = new(MAX_PARALLEL_CALIBRE, MAX_PARALLEL_CALIBRE);
        private static readonly ConcurrentDictionary<string, DateTime> s_calibreFailureLogThrottle = new(StringComparer.OrdinalIgnoreCase);

        // Cola centralizada para conversiones individuales (auto-TXT post-descarga).
        // Un único worker procesa de uno en uno — sin competencia entre descargas simultáneas.
        private readonly Channel<ConversionQueueItem> _conversionQueue =
            Channel.CreateBounded<ConversionQueueItem>(new BoundedChannelOptions(100) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });
        private readonly CancellationTokenSource _workerCts = new();
        private readonly Task _workerTask;
        private static bool ShouldLogCalibreFailure(string inputPath, string stderr)
        {
            // Evitar spam del mismo error repetido (misma extensión + firma) en ráfagas.
            var ext = Path.GetExtension(inputPath) ?? "";
            string signature = stderr.Contains("No plugin to handle input format", StringComparison.OrdinalIgnoreCase)
                ? "no-plugin"
                : "generic";
            var key = $"{ext}|{signature}";
            var now = DateTime.UtcNow;
            if (s_calibreFailureLogThrottle.TryGetValue(key, out var last) && (now - last).TotalSeconds < 30)
                return false;
            s_calibreFailureLogThrottle[key] = now;
            return true;
        }


        // Durante ConvertAllToTxtAsync el worker individual se pausa para ceder todos los slots
        // del semáforo global a la conversión masiva. Se reanuda al terminar.
        private readonly SemaphoreSlim _batchPauseSemaphore = new(1, 1);

        // Archivo de progreso para reanudar conversiones interrumpidas
        private const string PROGRESS_FILE = ".txt-progress";

        public bool IsAvailable => _ebookConvertPath != null;
        public string? CalibrePath => _ebookConvertPath;

        public event EventHandler<string>? LogMessage;
        public event EventHandler<ConversionResult>? ConversionCompleted;
        public event EventHandler<TxtConversionProgressEventArgs>? ConversionProgress;
        public event EventHandler? ConversionStarted;
        public event EventHandler? ConversionFinished;

        public CalibreConverterService()
        {
            DetectCalibre();
            _workerTask = Task.Run(RunConversionWorkerAsync);
        }

        private void EmitLog(string message)
        {
            Log.Information("{CalibreMsg}", message);
            LogMessage?.Invoke(this, message);
        }

        /// <summary>
        /// Encola una conversión individual. El worker único la procesará secuencialmente.
        /// Devuelve Task que completa cuando termina la conversión.
        /// </summary>
        public Task<ConversionResult> EnqueueConversionAsync(string inputPath, string outputDir, CancellationToken ct = default)
        {
            var tcs = new TaskCompletionSource<ConversionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_conversionQueue.Writer.TryWrite(new ConversionQueueItem(inputPath, outputDir, tcs, ct)))
                tcs.TrySetResult(new ConversionResult { InputFile = inputPath, Error = "Cola cerrada" });
            return tcs.Task;
        }

        /// <summary>
        /// Worker único que consume la cola secuencialmente (de uno en uno).
        /// </summary>
        private async Task RunConversionWorkerAsync()
        {
            try
            {
                var reader = _conversionQueue.Reader;
                while (await reader.WaitToReadAsync(_workerCts.Token).ConfigureAwait(false))
                {
                    while (reader.TryRead(out var item))
                    {
                        if (item.Ct.IsCancellationRequested)
                        {
                            item.Tcs.TrySetCanceled(item.Ct);
                            continue;
                        }
                        // Esperar si hay una conversión masiva en curso; mantener el slot hasta terminar ConvertToTxtCoreAsync
                        // para que el lote no adquiera el semáforo mientras esta conversión individual sigue activa.
                        await _batchPauseSemaphore.WaitAsync(_workerCts.Token).ConfigureAwait(false);
                        try
                        {
                            ConversionStarted?.Invoke(this, EventArgs.Empty);
                            var result = await ConvertToTxtCoreAsync(item.InputPath, item.OutputDir, item.Ct).ConfigureAwait(false);
                            ConversionFinished?.Invoke(this, EventArgs.Empty);
                            item.Tcs.TrySetResult(result);
                        }
                        catch (OperationCanceledException)
                        {
                            item.Tcs.TrySetCanceled(item.Ct);
                        }
                        catch (Exception ex)
                        {
                            item.Tcs.TrySetResult(new ConversionResult { InputFile = item.InputPath, Error = ex.Message });
                        }
                        finally
                        {
                            _batchPauseSemaphore.Release();
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        public void SetCustomPath(string path)
        {
            if (File.Exists(path))
            {
                _ebookConvertPath = path;
                EmitLog($"✅ Calibre configurado manualmente: {path}");
            }
            else
            {
                EmitLog($"❌ No se encontró ebook-convert en: {path}");
            }
        }

        public void Dispose()
        {
            _conversionQueue.Writer.TryComplete();
            _workerCts.Cancel();
            // Esperar a que el worker termine la conversión en curso (máx 10s)
            // para evitar dejar procesos Calibre huérfanos al cerrar la app.
            try { _workerTask.Wait(TimeSpan.FromSeconds(10)); }
            catch (Exception ex) { Log.Warning(ex, "CalibreConverterService: worker no terminó en 10s al cerrar"); }
            _workerCts.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            _conversionQueue.Writer.TryComplete();
            await _workerCts.CancelAsync().ConfigureAwait(false);
            await _workerTask.ConfigureAwait(false);
            _workerCts.Dispose();
        }

        private void DetectCalibre()
        {
            // 1. Buscar en PATH del sistema
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "where",
                        Arguments = "ebook-convert",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                var output = process.StandardOutput.ReadLine();
                process.WaitForExit(3000);

                if (!string.IsNullOrEmpty(output) && File.Exists(output))
                {
                    _ebookConvertPath = output;
                    EmitLog($"✅ Calibre detectado en PATH: {_ebookConvertPath}");
                    return;
                }
            }
            catch (Exception ex) { Log.Error(ex, "Calibre PATH detection failed"); }

            // 2. Buscar en rutas comunes
            foreach (var path in s_calibrePaths)
            {
                if (File.Exists(path))
                {
                    _ebookConvertPath = path;
                    EmitLog($"✅ Calibre detectado: {_ebookConvertPath}");
                    return;
                }
            }

            EmitLog("⚠️ Calibre no detectado. Instálalo o configura la ruta manualmente.");
        }

        private static bool NeedsTxtConversionCandidate(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            var ext = Path.GetExtension(filePath);
            if (!s_allSupportedExtensions.Contains(ext))
                return false;

            // TXT/TEXT: limpiar siempre para mantener normalización consistente.
            if (s_textExtensions.Contains(ext))
                return true;

            var dir = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(dir))
                return false;

            var txtPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(filePath) + ".txt");
            return !File.Exists(txtPath);
        }

        /// <summary>
        /// Convierte un archivo a TXT optimizado para lectura usando Calibre.
        /// Si el archivo ya es TXT, solo lo limpia para lectura.
        /// </summary>
        public async Task<ConversionResult> ConvertToTxtAsync(string inputPath, string outputDir, CancellationToken ct = default)
        {
            if (!File.Exists(inputPath))
                return new ConversionResult { InputFile = inputPath, Error = "Archivo no encontrado" };

            ConversionStarted?.Invoke(this, EventArgs.Empty);
            try
            {
                return await ConvertToTxtCoreAsync(inputPath, outputDir, ct).ConfigureAwait(false);
            }
            finally
            {
                ConversionFinished?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Lógica interna de conversión sin disparar ConversionStarted/ConversionFinished.
        /// Usado por ConvertAllToTxtAsync para evitar que cada archivo del lote
        /// decremente el contador de operaciones activas del tracker.
        /// </summary>
        private async Task<ConversionResult> ConvertToTxtCoreAsync(string inputPath, string outputDir, CancellationToken ct)
        {
            var result = new ConversionResult { InputFile = inputPath };
            var ext = Path.GetExtension(inputPath);
            var baseName = Path.GetFileNameWithoutExtension(inputPath);
            var outputPath = Path.Combine(outputDir, baseName + ".txt");
            result.OutputFile = outputPath;

            // Para .txt el outputPath == inputPath → File.Exists siempre true → skip incorrecto.
            // Solo comprobar existencia del TXT de salida cuando el input NO es ya un .txt.
            if (!s_textExtensions.Contains(ext) && File.Exists(outputPath))
            {
                result.Skipped = true;
                result.Success = true;
                return result;
            }

            Directory.CreateDirectory(outputDir);

            if (s_textExtensions.Contains(ext))
                return await CleanTxtForReadingAsync(inputPath, outputPath, ct).ConfigureAwait(false);

            if (s_convertibleExtensions.Contains(ext))
            {
                // Para PDFs: intentar extracción directa con PdfPig (sin proceso externo).
                // Retorna null si el PDF es escaneado (sin texto) → caer a Calibre.
                if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    var pdfResult = await ConvertPdfToTxtDirectAsync(inputPath, outputPath, ct).ConfigureAwait(false);
                    if (pdfResult != null) return pdfResult;
                }

                if (!IsAvailable)
                {
                    result.Error = "Calibre no disponible";
                    return result;
                }
                bool acquired = await s_calibreGlobalSemaphore.WaitAsync(TimeSpan.FromMinutes(10), ct).ConfigureAwait(false);
                if (!acquired)
                {
                    result.Error = "Timeout esperando slot de Calibre (10 min)";
                    return result;
                }
                try
                {
                    return await ConvertWithCalibreAsync(inputPath, outputPath, ct).ConfigureAwait(false);
                }
                finally
                {
                    s_calibreGlobalSemaphore.Release();
                }
            }

            result.Error = $"Formato no soportado: {ext}";
            return result;
        }

        /// <summary>
        /// Extrae texto de un PDF digital directamente con PdfPig (sin proceso externo).
        /// Retorna null si el PDF es escaneado (texto extraído menor al umbral mínimo).
        /// </summary>
        private static async Task<ConversionResult?> ConvertPdfToTxtDirectAsync(string inputPath, string outputPath, CancellationToken ct)
        {
            const int MIN_TEXT_CHARS = 200;
            try
            {
                var sb = new StringBuilder(65536); // opt186: capacidad inicial típica PDF de texto
                await Task.Run(() =>
                {
                    using var pdf = PdfDocument.Open(inputPath);
                    foreach (var page in pdf.GetPages())
                    {
                        ct.ThrowIfCancellationRequested();
                        foreach (var word in page.GetWords())
                            sb.Append(word.Text).Append(' ');
                        sb.Append('\n');
                    }
                }, ct).ConfigureAwait(false);

                if (sb.Length < MIN_TEXT_CHARS)
                    return null; // PDF escaneado → dejar que Calibre lo intente

                var cleaned = CleanTextForReading(sb.ToString());
                await using (var wfs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
                {
                    var bytes = Encoding.UTF8.GetBytes(cleaned);
                    await wfs.WriteAsync(bytes, ct).ConfigureAwait(false);
                }
                return new ConversionResult
                {
                    InputFile = inputPath,
                    OutputFile = outputPath,
                    Success = true,
                    OutputSizeBytes = new FileInfo(outputPath).Length
                };
            }
            catch (OperationCanceledException) { throw; }
            catch { return null; } // cualquier error → fallback a Calibre
        }

        /// <summary>
        /// Convierte un archivo a EPUB usando Calibre. Retorna true si tuvo éxito.
        /// </summary>
        public async Task<bool> ConvertToEpubAsync(string inputPath, string outputPath, CancellationToken ct = default, string? title = null, string? author = null)
        {
            if (!IsAvailable || !File.Exists(inputPath)) return false;

            bool acquired = await s_calibreGlobalSemaphore.WaitAsync(TimeSpan.FromMinutes(10), ct).ConfigureAwait(false);
            if (!acquired) return false;
            try
            {
                var result = await ConvertWithCalibreAsync(inputPath, outputPath, ct, title, author).ConfigureAwait(false);
                return result.Success && File.Exists(outputPath);
            }
            finally { s_calibreGlobalSemaphore.Release(); }
        }

        /// <summary>
        /// Convierte todos los libros de una carpeta a TXT.
        /// Optimizado para 5000+ libros: paralelo, con persistencia de progreso y circuit breaker.
        /// </summary>
        /// <param name="txtOutputDir">
        /// Carpeta del archivo <c>.txt-progress</c> (y creación del directorio). Los .txt generados
        /// se escriben siempre junto a cada archivo fuente, no en esta ruta.
        /// </param>
        public async Task<BatchConversionResult> ConvertAllToTxtAsync(
            string downloadsDir,
            string? txtOutputDir = null,
            IProgress<(int current, int total, string fileName)>? progress = null,
            CancellationToken ct = default,
            // fix4: si se pasa, solo se procesan estos archivos (archivos nuevos de la sesión)
            // evita escanear 10.000+ archivos de sesiones anteriores cuando solo hubo 200 descargas nuevas
            IReadOnlyCollection<string>? restrictToFiles = null)
        {
            ConversionStarted?.Invoke(this, EventArgs.Empty);
            var batch = new BatchConversionResult();
            try {
                // TXT se guarda junto al archivo original, no en subcarpeta separada
                txtOutputDir ??= downloadsDir;

                // Asegurar que el directorio de salida existe (para el archivo de progreso)
                Directory.CreateDirectory(txtOutputDir);

                // Cargar progreso previo (para reanudar conversiones interrumpidas)
                // Validar que el TXT de salida realmente existe; si no, reconvertir
                var progressFilePath = Path.Combine(txtOutputDir, PROGRESS_FILE);
                var alreadyConverted = LoadProgressValidated(progressFilePath);

                // fix6: limpiar entradas obsoletas del archivo de progreso periódicamente
                // Con restrictToFiles el archivo crece sesión a sesión sin que se limpie nunca.
                // Cada 500 entradas verificamos cuáles siguen existiendo como .txt.
                if (alreadyConverted.Count > 500 && restrictToFiles != null)
                {
                    var before = alreadyConverted.Count;
                    // Mismo criterio que LoadProgressValidated: el .txt sale junto al archivo fuente,
                    // no en txtOutputDir (que solo aloja .txt-progress).
                    alreadyConverted.RemoveWhere(f =>
                    {
                        var dir = Path.GetDirectoryName(f);
                        if (string.IsNullOrEmpty(dir)) return true;
                        var txtPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(f) + ".txt");
                        return !File.Exists(txtPath);
                    });
                    var pruned = before - alreadyConverted.Count;
                    if (pruned > 0)
                    {
                        EmitLog($"🧹 txt-progress: {pruned} entradas obsoletas eliminadas ({alreadyConverted.Count} restantes)");
                        SaveProgress(progressFilePath, alreadyConverted);
                    }
                }

                // Fase 1: Escanear archivos con enumeración lazy y filtrado temprano
                // fix4: si hay restrictToFiles, iterar solo esos; si no, escanear el directorio completo
                var sw = Stopwatch.StartNew();
                IEnumerable<string> fileSource;
                if (restrictToFiles != null && restrictToFiles.Count > 0)
                {
                    var restrictedCandidates = restrictToFiles
                        .Where(NeedsTxtConversionCandidate)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    EmitLog($"📂 Convirtiendo {restrictedCandidates.Length} archivos nuevos de la sesión...");
                    var skippedByPresence = Math.Max(0, restrictToFiles.Count - restrictedCandidates.Length);
                    if (skippedByPresence > 0)
                        EmitLog($"   ⏭️ Omitidos por TXT existente/no válido: {skippedByPresence}");

                    if (restrictedCandidates.Length == 0)
                    {
                        EmitLog("✅ Conversión omitida: archivos de sesión ya convertidos o fuera de alcance.");
                        return batch;
                    }

                    fileSource = restrictedCandidates;
                }
                else
                {
                    EmitLog("📂 Escaneando archivos...");
                    fileSource = Directory.EnumerateFiles(downloadsDir, "*.*", SearchOption.AllDirectories);
                }

                var bookFiles = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                // opt187: usar FrozenSet estático en lugar de new HashSet
                var excludeDirs = s_convertExcludeDirs;

                var downloadsDirWithSep = downloadsDir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var prefixLen = downloadsDirWithSep.Length;

                foreach (var file in fileSource)
                {
                    ct.ThrowIfCancellationRequested();

                    var ext = Path.GetExtension(file);
                    if (!s_allSupportedExtensions.Contains(ext))
                        continue;

                    if (file.Length > prefixLen)
                    {
                        var sepIdx = file.IndexOf(Path.DirectorySeparatorChar, prefixLen);
                        var topDir = sepIdx >= 0
                            ? file.AsSpan(prefixLen, sepIdx - prefixLen).ToString()
                            : null;
                        if (topDir != null && excludeDirs.Contains(topDir))
                            continue;
                    }

                    var normalizedName = NormalizeBookName(Path.GetFileNameWithoutExtension(file));

                    if (!bookFiles.TryGetValue(normalizedName, out var list))
                    {
                        list = new List<string>(2);
                        bookFiles[normalizedName] = list;
                    }
                    list.Add(file);
                }

                // Fase 2: Seleccionar mejor versión por libro y filtrar ya convertidos
                // Opt: pre-asignar capacidad estimada para evitar rehashing
                // fix162: lista local — _filesToConvertBuf de instancia async sin lock
                var filesToConvert = new List<(string file, string outputSubDir)>(bookFiles.Count);

                foreach (var (name, files) in bookFiles)
                {
                    var best = SelectBestVersion(files);
                    if (best == null) continue;

                    if (alreadyConverted.Contains(best))
                        continue;

                    var outputSubDir = Path.GetDirectoryName(best)!;
                    var bestExt = Path.GetExtension(best);

                    // Para .txt el outputPath == inputPath → File.Exists siempre true → skip incorrecto.
                    // Solo comprobar existencia del TXT de salida cuando el input NO es ya un .txt.
                    if (!s_textExtensions.Contains(bestExt))
                    {
                        var outputPath = Path.Combine(outputSubDir, Path.GetFileNameWithoutExtension(best) + ".txt");
                        if (File.Exists(outputPath))
                        {
                            batch.Skipped++;
                            continue;
                        }
                    }

                    filesToConvert.Add((best, outputSubDir));
                }

                batch.TotalFiles = filesToConvert.Count + batch.Skipped;
                EmitLog($"📚 Escaneados {bookFiles.Count} libros únicos en {sw.ElapsedMilliseconds}ms");
                EmitLog($"   📄 Por convertir: {filesToConvert.Count} | Ya existentes: {batch.Skipped} | Progreso previo: {alreadyConverted.Count}");

                if (filesToConvert.Count == 0)
                {
                    EmitLog("✅ Nada que convertir, todo está al día");
                    return batch;
                }

                // Fase 3: Separar TXT (limpieza rápida) de conversiones Calibre (lentas)
                // fix162: listas locales — _txtFilesBuf/_calibreFilesBuf de instancia async sin lock
                var txtFiles = new List<(string file, string outputSubDir)>(filesToConvert.Count / 4);
                var calibreFiles = new List<(string file, string outputSubDir)>(filesToConvert.Count);

                foreach (var item in filesToConvert)
                {
                    var ext = Path.GetExtension(item.file);
                    if (s_textExtensions.Contains(ext))
                        txtFiles.Add(item);
                    else
                        calibreFiles.Add(item);
                }

                // Fase 4: Procesar en paralelo
                int processed = 0;
                int consecutiveErrors = 0;
                var startTime = Stopwatch.StartNew();

                // 4a: Limpiar TXTs en paralelo (rápido, más hilos)
                if (txtFiles.Count > 0)
                {
                    EmitLog($"📝 Limpiando {txtFiles.Count} archivos TXT para lectura...");
                    int txtParallel = Math.Min(MAX_PARALLEL_TXT_CLEAN, txtFiles.Count);
                    using var txtSemaphore = new SemaphoreSlim(txtParallel, txtParallel);
                    var txtTasks = new Task[txtFiles.Count];
                    for (int i = 0; i < txtFiles.Count; i++)
                    {
                        var item = txtFiles[i];
                        txtTasks[i] = Task.Run(async () =>
                        {
                            await txtSemaphore.WaitAsync(ct).ConfigureAwait(false);
                            try
                            {
                                var result = await ConvertToTxtCoreAsync(item.file, item.outputSubDir, ct).ConfigureAwait(false);
                                ProcessResult(result, item.file, batch, alreadyConverted, progressFilePath,
                                    ref processed, ref consecutiveErrors, startTime, filesToConvert.Count, progress);
                            }
                            finally { txtSemaphore.Release(); }
                        }, ct);
                    }
                    await Task.WhenAll(txtTasks).ConfigureAwait(false);
                }

                // 4b: Convertir con Calibre en paralelo (lento, menos hilos)
                // Pausamos el worker individual para usar todos los slots del semáforo global.
                // Al terminar la conversión masiva, el worker se reanuda automáticamente.
                if (calibreFiles.Count > 0 && IsAvailable)
                {
                    int batchParallel = MAX_PARALLEL_CALIBRE;
                    await _batchPauseSemaphore.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        EmitLog($"🔄 Convirtiendo {calibreFiles.Count} archivos con Calibre ({batchParallel} en paralelo)...");
                        int calibreIdx = 0;
                        while (calibreIdx < calibreFiles.Count && !ct.IsCancellationRequested)
                        {
                            if (Volatile.Read(ref consecutiveErrors) >= MAX_CONSECUTIVE_ERRORS)
                            {
                                EmitLog($"⛔ Circuit breaker activado: {MAX_CONSECUTIVE_ERRORS} errores consecutivos. Conversión detenida.");
                                batch.Errors.Add($"Circuit breaker: {MAX_CONSECUTIVE_ERRORS} errores consecutivos");
                                break;
                            }

                            int batchEnd = Math.Min(calibreIdx + batchParallel, calibreFiles.Count);
                            int batchSize = batchEnd - calibreIdx;
                            var batchTasks = new Task[batchSize];
                            for (int j = 0; j < batchSize; j++)
                            {
                                var item = calibreFiles[calibreIdx + j];
                                batchTasks[j] = Task.Run(async () =>
                                {
                                    var result = await ConvertToTxtCoreAsync(item.file, item.outputSubDir, ct).ConfigureAwait(false);
                                    ProcessResult(result, item.file, batch, alreadyConverted, progressFilePath,
                                        ref processed, ref consecutiveErrors, startTime, filesToConvert.Count, progress);
                                }, ct);
                            }
                            await Task.WhenAll(batchTasks).ConfigureAwait(false);
                            calibreIdx = batchEnd;
                        }
                    }
                    finally
                    {
                        _batchPauseSemaphore.Release();
                    }
                }

                var elapsed = startTime.Elapsed;
                var speed = elapsed.TotalSeconds > 0 ? batch.Converted / elapsed.TotalSeconds : 0;
                EmitLog($"✅ Conversión completada en {FormatTime(elapsed)}:");
                EmitLog($"   ✅ Convertidos: {batch.Converted} ({speed:F1} libros/s)");
                EmitLog($"   ⏭️ Ya existían: {batch.Skipped}");
                EmitLog($"   ❌ Errores: {batch.Failed}");

                return batch;
            }
            finally
            {
                ConversionFinished?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ProcessResult(
            ConversionResult result, string file,
            BatchConversionResult batch, HashSet<string> alreadyConverted, string progressFilePath,
            ref int processed, ref int consecutiveErrors,
            Stopwatch startTime, int totalFiles,
            IProgress<(int current, int total, string fileName)>? progress)
        {
            var current = Interlocked.Increment(ref processed);

            if (result.Skipped || result.Deleted)
            {
                batch.IncrementSkipped();
                Interlocked.Exchange(ref consecutiveErrors, 0);
            }
            else if (result.Success)
            {
                batch.IncrementConverted();
                Interlocked.Exchange(ref consecutiveErrors, 0);
                lock (alreadyConverted)
                {
                    alreadyConverted.Add(file);
                    if (batch.Converted % 50 == 0)
                        SaveProgress(progressFilePath, alreadyConverted);
                }
            }
            else
            {
                batch.IncrementFailed();
                Interlocked.Increment(ref consecutiveErrors);
                lock (batch.Errors)
                {
                    if (batch.Errors.Count < 100)
                        batch.Errors.Add($"{Path.GetFileName(file)}: {result.Error}");
                }
            }

            if (current % 10 == 0 || current == totalFiles)
            {
                var elapsed = startTime.Elapsed.TotalSeconds;
                var eta = elapsed > 0 ? TimeSpan.FromSeconds((totalFiles - current) * (elapsed / current)) : TimeSpan.Zero;
                var fileName = Path.GetFileName(file);
                progress?.Report((current, totalFiles, $"{fileName} (ETA: {FormatTime(eta)})"));
            }

            ConversionCompleted?.Invoke(this, result);
            ConversionProgress?.Invoke(this, new TxtConversionProgressEventArgs
            {
                FileName  = Path.GetFileName(file),
                Current   = current,
                Total     = totalFiles,
                Converted = batch.Converted,
                Failed    = batch.Failed,
                Skipped   = batch.Skipped,
                Success   = result.Success && !result.Skipped,
                Error     = result.Error
            });
        }

        private async Task<ConversionResult> ConvertWithCalibreAsync(string inputPath, string outputPath, CancellationToken ct, string? title = null, string? author = null)
        {
            var result = new ConversionResult { InputFile = inputPath, OutputFile = outputPath };

            // Verificar integridad mínima: EPUBs/CBZ son ZIPs, deben empezar con "PK" (0x50 0x4B)
            var inputExt = Path.GetExtension(inputPath);
            if (inputExt.Equals(".epub", StringComparison.OrdinalIgnoreCase) || inputExt.Equals(".cbz", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var checkFs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (checkFs.Length < 4 || checkFs.ReadByte() != 0x50 || checkFs.ReadByte() != 0x4B)
                    {
                        result.Error = "Archivo corrupto (no es ZIP válido)";
                        result.Deleted = true;
                        checkFs.Close();
                        TryDeleteSourceFile(inputPath);
                        return result;
                    }
                }
                catch (IOException ex)
                {
                    result.Error = $"No se pudo verificar: {ex.Message}";
                    return result;
                }
            }

            var tempTxt = outputPath + ".tmp.txt";

            try
            {
                var metaArgs = new System.Text.StringBuilder();
                if (!string.IsNullOrWhiteSpace(title))
                    metaArgs.Append($" --title \"{title.Replace("\"", "'")}\"" );
                if (!string.IsNullOrWhiteSpace(author))
                    metaArgs.Append($" --authors \"{author.Replace("\"", "'")}\"" );
                var args = $"\"{inputPath}\" \"{tempTxt}\" --txt-output-formatting=plain{metaArgs}";

                var calibreDir = Path.GetDirectoryName(_ebookConvertPath)!;
                var psi = new ProcessStartInfo
                {
                    FileName = _ebookConvertPath!,
                    Arguments = args,
                    WorkingDirectory = calibreDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // fix162: lista local — _pythonVarsBuf de instancia async sin lock
                var pythonVars = new List<string>(8);
                foreach (var k in psi.Environment.Keys)
                    if (k.StartsWith("PYTHON", StringComparison.OrdinalIgnoreCase) ||
                        k.StartsWith("VSCODE_PYTHON", StringComparison.OrdinalIgnoreCase))
                        pythonVars.Add(k);
                foreach (var key in pythonVars)
                    psi.Environment.Remove(key);

                using var process = new Process { StartInfo = psi };
                process.Start();

                // Leer stderr/stdout async para evitar deadlock en pipes llenos
                var stderrTask = process.StandardError.ReadToEndAsync(ct);
                var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);

                // WaitForExitAsync es verdaderamente async (no bloquea thread del pool).
                // Combinamos timeout + cancelación externa en un único CancellationToken.
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(CALIBRE_TIMEOUT_MS);
                bool timedOut = false;
                try
                {
                    await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    timedOut = true;
                }

                if (timedOut)
                {
                    try { process.Kill(true); } catch (Exception ex) { Log.Error(ex, "Kill process failed"); }
                    try { await stderrTask.ConfigureAwait(false); } catch { }
                    try { await stdoutTask.ConfigureAwait(false); } catch { }
                    result.Error = "Timeout (5 min)";
                    return result;
                }

                // Siempre leer stderr para vaciar el pipe (evita leak) y capturar warnings
                var stderr = await stderrTask.ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    if (ShouldLogCalibreFailure(inputPath, stderr))
                        Log.Error("[Calibre] Exit {ExitCode} for {File}: {Stderr}", process.ExitCode, Path.GetFileName(inputPath), stderr);
                    // Si Calibre no reconoce el formato: detectar tipo real por magic bytes y reintentar
                    if (stderr.Contains("No plugin to handle input", StringComparison.OrdinalIgnoreCase))
                    {
                        var realExt = DetectRealExtensionByMagicBytes(inputPath);
                        if (realExt != null && !realExt.Equals(inputExt, StringComparison.OrdinalIgnoreCase)
                            && s_convertibleExtensions.Contains(realExt))
                        {
                            var fixedPath = Path.ChangeExtension(inputPath, realExt);
                            try
                            {
                                File.Move(inputPath, fixedPath, overwrite: true);
                                Log.Information("[Calibre] Reintento con extensión detectada {Ext}: {File}", realExt, Path.GetFileName(fixedPath));
                                var retryResult = await ConvertWithCalibreAsync(fixedPath, outputPath, ct, title, author).ConfigureAwait(false);
                                if (retryResult.Success) return retryResult;
                                result.Error = $"Formato incorrecto ({inputExt}→{realExt}), conversión fallida: {retryResult.Error}";
                            }
                            catch (Exception mvEx)
                            {
                                result.Error = $"No se pudo renombrar para reintento: {mvEx.Message}";
                                fixedPath = inputPath;
                            }
                            result.Deleted = true;
                            TryDeleteSourceFile(fixedPath);
                            return result;
                        }
                        result.Error = $"Calibre exit {process.ExitCode}: formato no reconocido y sin magic bytes identificables";
                        result.Deleted = true;
                        TryDeleteSourceFile(inputPath);
                        return result;
                    }
                    result.Error = $"Calibre exit {process.ExitCode}: {stderr[..Math.Min(stderr.Length, 500)]}";
                    result.Deleted = true;
                    TryDeleteSourceFile(inputPath);
                    return result;
                }

                if (File.Exists(tempTxt))
                {
                    var fileSize = new FileInfo(tempTxt).Length;
                    if (fileSize < 64)
                    {
                        try { File.Delete(tempTxt); } catch { }
                        result.Error = $"Calibre generó TXT vacío (exit 0, {fileSize} bytes) — posible DRM o PDF sin OCR";
                        result.Deleted = true;
                        TryDeleteSourceFile(inputPath);
                        return result;
                    }

                    if (fileSize > MAX_FILE_SIZE_FOR_MEMORY)
                    {
                        await CleanLargeFileForReadingAsync(tempTxt, outputPath, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        await using var trfs = new FileStream(tempTxt, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                        using var trr = new StreamReader(trfs);
                        var rawText = await trr.ReadToEndAsync().ConfigureAwait(false);
                        var cleanText = CleanTextForReading(rawText);
                        await using var twfs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                        await using var tw = new StreamWriter(twfs, System.Text.Encoding.UTF8);
                        await tw.WriteAsync(cleanText).ConfigureAwait(false);
                    }

                    try { File.Delete(tempTxt); } catch (Exception ex) { Log.Error(ex, "Delete temp failed"); }

                    result.Success = true;
                    result.OutputSizeBytes = new FileInfo(outputPath).Length;
                }
                else
                {
                    result.Error = "Calibre no generó archivo de salida";
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                try { if (File.Exists(tempTxt)) File.Delete(tempTxt); } catch { }
            }

            return result;
        }

    }
}
