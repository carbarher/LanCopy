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
        private async Task<ConversionResult> CleanTxtForReadingAsync(string inputPath, string outputPath, CancellationToken ct)
        {
            var result = new ConversionResult { InputFile = inputPath, OutputFile = outputPath };

            try
            {
                var fileSize = new FileInfo(inputPath).Length;

                if (fileSize > MAX_FILE_SIZE_FOR_MEMORY)
                {
                    await CleanLargeFileForReadingAsync(inputPath, outputPath, ct).ConfigureAwait(false);
                }
                else
                {
                    // Leer todo el contenido y cerrar el stream ANTES de abrir para escritura.
                    // Necesario cuando inputPath == outputPath (archivos .txt): si ambos streams
                    // están abiertos a la vez, el segundo FileStream lanza IOException.
                    string rawText;
                    await using (var irfs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
                    {
                        using var irr = new StreamReader(irfs);
                        rawText = await irr.ReadToEndAsync().ConfigureAwait(false);
                    }
                    var cleanText = CleanTextForReading(rawText);
                    await using var iwfs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                    await using var iw = new StreamWriter(iwfs, System.Text.Encoding.UTF8);
                    await iw.WriteAsync(cleanText).ConfigureAwait(false);
                }

                result.Success = true;
                result.OutputSizeBytes = new FileInfo(outputPath).Length;
            }
            catch (OperationCanceledException) { throw; }
            catch (UnauthorizedAccessException)
            {
                // Archivo read-only o en uso: ya existe, tratarlo como omitido (no fallo)
                result.Skipped = true;
                result.Success = true;
            }
            catch (IOException ex)
            {
                result.Error = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Limpieza TXT por bloques para archivos > 50 MB
        /// </summary>
        private static async Task CleanLargeFileForReadingAsync(string inputPath, string outputPath, CancellationToken ct)
        {
            const int bufferSize = 4 * 1024 * 1024; // 4 MB por bloque

            // Leer todo el contenido y cerrar el reader ANTES de abrir el writer.
            // Necesario cuando inputPath == outputPath (archivos .txt): ambos streams
            // no pueden estar abiertos simultáneamente sobre el mismo archivo.
            string fullText;
            using (var reader = new StreamReader(inputPath, Encoding.UTF8, true, bufferSize))
            {
                fullText = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            ct.ThrowIfCancellationRequested();
            var cleaned = CleanTextForReading(fullText);

            using var writer = new StreamWriter(outputPath, false, Encoding.UTF8, bufferSize);
            await writer.WriteAsync(cleaned).ConfigureAwait(false);
        }

        /// <summary>
        /// Limpia texto para lectura TXT óptima (regex compilados)
        /// </summary>
        private static string CleanTextForReading(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Eliminar cabecera de Project Gutenberg
            var gutenbergStart = text.IndexOf("*** START OF", StringComparison.OrdinalIgnoreCase);
            if (gutenbergStart >= 0)
            {
                var lineEnd = text.IndexOf('\n', gutenbergStart);
                if (lineEnd >= 0)
                    text = text[(lineEnd + 1)..];
            }

            // Eliminar pie de Project Gutenberg
            var gutenbergEnd = text.IndexOf("*** END OF", StringComparison.OrdinalIgnoreCase);
            if (gutenbergEnd >= 0)
                text = text[..gutenbergEnd];

            text = s_rxPageNumbers.Replace(text, "");
            text = s_rxDecorative.Replace(text, "");
            text = s_rxFootnoteBracket.Replace(text, "");
            text = s_rxFootnoteParen.Replace(text, "");
            text = s_rxMultiNewline.Replace(text, "\n\n");
            text = s_rxMultiSpace.Replace(text, " ");
            text = s_rxBrokenLines.Replace(text, "$1 $2");
            text = s_rxHyphenation.Replace(text, "$1$2");
            text = s_rxLeadingWhitespace.Replace(text, "");
            text = s_rxTrailingWhitespace.Replace(text, "");

            return text.Trim();
        }

        /// <summary>
        /// Selecciona la mejor versión de un libro entre múltiples formatos.
        /// </summary>
        private static string? SelectBestVersion(List<string> files)
        {
            if (files.Count == 0) return null;
            if (files.Count == 1) return files[0];

            foreach (var preferredExt in s_extensionPriority)
            {
                for (int i = 0; i < files.Count; i++)
                {
                    if (Path.GetExtension(files[i]).Equals(preferredExt, StringComparison.OrdinalIgnoreCase))
                        return files[i];
                }
            }

            // Si solo hay TXT, devolverlo para limpieza
            for (int i = 0; i < files.Count; i++)
            {
                if (s_textExtensions.Contains(Path.GetExtension(files[i])))
                    return files[i];
            }

            return files[0];
        }

        /// <summary>
        /// Normaliza nombre de libro para agrupar versiones del mismo título
        /// </summary>
        private static string NormalizeBookName(string name)
        {
            name = s_rxFormatSuffix1.Replace(name, "");
            name = s_rxFormatSuffix2.Replace(name, "");
            return name.Trim().ToLowerInvariant();
        }

        // --- Persistencia de progreso ---

        private static HashSet<string> LoadProgress(string path)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(path))
                {
                    foreach (var line in File.ReadLines(path))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            set.Add(line);
                    }
                }
            }
            catch (Exception ex) { Log.Error(ex, "LoadProgress failed"); }
            return set;
        }

        /// <summary>
        /// Carga el progreso previo filtrando entradas cuyo TXT de salida ya no existe en disco.
        /// Evita que conversiones fallidas/borradas queden marcadas como completadas.
        /// </summary>
        private static HashSet<string> LoadProgressValidated(string path)
        {
            var raw = LoadProgress(path);
            if (raw.Count == 0) return raw;

            var valid = new HashSet<string>(raw.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var srcPath in raw)
            {
                var txtPath = Path.Combine(
                    Path.GetDirectoryName(srcPath) ?? "",
                    Path.GetFileNameWithoutExtension(srcPath) + ".txt");
                if (File.Exists(txtPath))
                    valid.Add(srcPath);
            }
            return valid;
        }

        private static void SaveProgress(string path, HashSet<string> converted)
        {
            try
            {
                lock (converted)
                {
                    File.WriteAllLines(path, converted);
                }
            }
            catch (Exception ex) { Log.Error(ex, "SaveProgress failed"); }
        }

        /// <summary>
        /// Detecta el formato real de un archivo por sus magic bytes.
        /// Devuelve la extensión correcta (con punto) o null si no se reconoce.
        /// </summary>
        private static string? DetectRealExtensionByMagicBytes(string path)
        {
            try
            {
                Span<byte> header = stackalloc byte[8];
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8, false);
                int read = fs.Read(header);
                if (read < 4) return null;

                // ZIP: PK\x03\x04 — epub, docx, odt
                if (header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04)
                    return ".epub"; // asumimos epub (Calibre lo intentará; si falla podría ser docx/odt)

                // PDF: %PDF
                if (header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46)
                    return ".pdf";

                // MOBI/AZW: BOOKMOBI magic en offset 60 → leer más
                // Cabecera PalmDB: primeros 32 bytes son nombre, offset 60 tiene "BOOKMOBI"
                if (read >= 8)
                {
                    fs.Seek(60, SeekOrigin.Begin);
                    Span<byte> mobi = stackalloc byte[8];
                    if (fs.Read(mobi) == 8 &&
                        mobi[0] == 'B' && mobi[1] == 'O' && mobi[2] == 'O' && mobi[3] == 'K' &&
                        mobi[4] == 'M' && mobi[5] == 'O' && mobi[6] == 'B' && mobi[7] == 'I')
                        return ".mobi";
                }

                // FB2: XML con <FictionBook
                if (header[0] == 0x3C || (header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF))
                {
                    try
                    {
                        using var sr = new StreamReader(path, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                        var firstChars = new char[200];
                        int charsRead = sr.Read(firstChars, 0, 200);
                        var preview = new string(firstChars, 0, charsRead);
                        if (preview.Contains("FictionBook", StringComparison.OrdinalIgnoreCase)) return ".fb2";
                        if (preview.Contains("<html", StringComparison.OrdinalIgnoreCase)) return ".html";
                    }
                    catch { }
                }

                // RTF: {\rtf
                if (header[0] == 0x7B && header[1] == 0x5C && header[2] == 0x72 && header[3] == 0x74)
                    return ".rtf";

                // DjVu: AT&TFORM
                if (header[0] == 0x41 && header[1] == 0x54 && header[2] == 0x26 && header[3] == 0x54)
                    return ".djvu";

                return null;
            }
            catch { return null; }
        }

        private static void TryDeleteSourceFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Log.Information("[Calibre] Eliminado archivo inválido: {File}", Path.GetFileName(path));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Calibre] No se pudo eliminar {File}", Path.GetFileName(path));
            }
        }

        private static string FormatTime(TimeSpan ts)
        {
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1) return $"{ts.Minutes}m {ts.Seconds}s";
            return $"{ts.Seconds}s";
        }
    }

    public sealed class ConversionResult
    {
        public string InputFile { get; set; } = string.Empty;
        public string OutputFile { get; set; } = string.Empty;
        public bool Success { get; set; }
        public bool Skipped { get; set; }
        public bool Deleted { get; set; }
        public string? Error { get; set; }
        public long OutputSizeBytes { get; set; }
    }

    public sealed class BatchConversionResult
    {
        private int _converted;
        private int _skipped;
        private int _failed;

        public int TotalFiles { get; set; }
        public int Converted { get => _converted; set => _converted = value; }
        public int Skipped { get => _skipped; set => _skipped = value; }
        public int Failed { get => _failed; set => _failed = value; }
        public List<string> Errors { get; set; } = new();

        public void IncrementConverted() => Interlocked.Increment(ref _converted);
        public void IncrementSkipped() => Interlocked.Increment(ref _skipped);
        public void IncrementFailed() => Interlocked.Increment(ref _failed);
    }

    public sealed class TxtConversionProgressEventArgs : EventArgs
    {
        public string FileName  { get; init; } = string.Empty;
        public int    Current   { get; init; }
        public int    Total     { get; init; }
        public int    Converted { get; init; }
        public int    Failed    { get; init; }
        public int    Skipped   { get; init; }
        public bool   Success   { get; init; }
        public string? Error    { get; init; }
    }

    internal sealed record ConversionQueueItem(
        string InputPath,
        string OutputDir,
        TaskCompletionSource<ConversionResult> Tcs,
        CancellationToken Ct);
}
