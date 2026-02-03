using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SlskDown.AI
{
    /// <summary>
    /// Generación automática de resúmenes de libros usando Ollama
    /// </summary>
    public class BookSummarizer
    {
        private readonly OllamaClient client;
        private readonly string model;
        private const int MAX_CONTENT_LENGTH = 8000; // Caracteres del libro a analizar
        private const int SUMMARY_MAX_LENGTH = 500; // Longitud máxima del resumen

        public BookSummarizer(OllamaClient ollamaClient, string modelName = "llama3.2")
        {
            client = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
            model = modelName;
        }

        /// <summary>
        /// Genera un resumen de un libro a partir de su archivo
        /// </summary>
        public async Task<BookSummary> GenerateSummaryAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new BookSummary
                    {
                        Success = false,
                        Error = "Archivo no encontrado"
                    };
                }

                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                string content = await ExtractTextFromFileAsync(filePath, extension);

                if (string.IsNullOrWhiteSpace(content))
                {
                    return new BookSummary
                    {
                        Success = false,
                        Error = "No se pudo extraer texto del archivo"
                    };
                }

                // Limitar contenido para análisis
                if (content.Length > MAX_CONTENT_LENGTH)
                {
                    content = content.Substring(0, MAX_CONTENT_LENGTH);
                }

                var prompt = $@"Analiza el siguiente fragmento de un libro y genera un resumen conciso en español.

FRAGMENTO:
{content}

Genera un resumen de máximo {SUMMARY_MAX_LENGTH} caracteres que incluya:
1. Tema principal
2. Género literario
3. Breve descripción del contenido

RESUMEN:";

                var response = await client.GenerateAsync(model, prompt);

                if (string.IsNullOrWhiteSpace(response))
                {
                    return new BookSummary
                    {
                        Success = false,
                        Error = "No se recibió respuesta del modelo"
                    };
                }

                return new BookSummary
                {
                    Success = true,
                    Summary = response.Trim(),
                    FilePath = filePath,
                    GeneratedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new BookSummary
                {
                    Success = false,
                    Error = $"Error generando resumen: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Genera resúmenes para múltiples archivos
        /// </summary>
        public async Task<List<BookSummary>> GenerateSummariesAsync(IEnumerable<string> filePaths, IProgress<int> progress = null)
        {
            var results = new List<BookSummary>();
            var files = filePaths.ToList();
            int processed = 0;

            foreach (var file in files)
            {
                var summary = await GenerateSummaryAsync(file);
                results.Add(summary);

                processed++;
                progress?.Report((processed * 100) / files.Count);
            }

            return results;
        }

        /// <summary>
        /// Extrae texto de diferentes formatos de archivo
        /// </summary>
        private async Task<string> ExtractTextFromFileAsync(string filePath, string extension)
        {
            try
            {
                switch (extension)
                {
                    case ".txt":
                        return await File.ReadAllTextAsync(filePath);

                    case ".epub":
                        return await ExtractFromEpubAsync(filePath);

                    case ".pdf":
                        return await ExtractFromPdfAsync(filePath);

                    default:
                        // Intentar leer como texto plano
                        try
                        {
                            return await File.ReadAllTextAsync(filePath);
                        }
                        catch
                        {
                            return string.Empty;
                        }
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Extrae texto de archivos EPUB (simplificado)
        /// </summary>
        private async Task<string> ExtractFromEpubAsync(string filePath)
        {
            try
            {
                // Implementación básica: intentar leer como texto
                // En producción, usar una librería como EpubSharp
                var bytes = await File.ReadAllBytesAsync(filePath);
                var text = Encoding.UTF8.GetString(bytes);
                
                // Limpiar HTML básico
                text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", " ");
                text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
                
                return text.Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Extrae texto de archivos PDF (simplificado)
        /// </summary>
        private async Task<string> ExtractFromPdfAsync(string filePath)
        {
            try
            {
                // Implementación básica: intentar leer como texto
                // En producción, usar una librería como iTextSharp o PdfPig
                var bytes = await File.ReadAllBytesAsync(filePath);
                var text = Encoding.UTF8.GetString(bytes);
                
                // Limpiar caracteres de control
                text = System.Text.RegularExpressions.Regex.Replace(text, @"[\x00-\x1F\x7F]", " ");
                text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
                
                return text.Trim();
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <summary>
    /// Resultado de la generación de resumen
    /// </summary>
    public class BookSummary
    {
        public bool Success { get; set; }
        public string Summary { get; set; }
        public string FilePath { get; set; }
        public DateTime GeneratedAt { get; set; }
        public string Error { get; set; }
    }
}
