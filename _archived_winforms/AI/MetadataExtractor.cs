using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SlskDown.AI
{
    /// <summary>
    /// Extracción inteligente de metadatos usando Ollama
    /// </summary>
    public class MetadataExtractor
    {
        private readonly OllamaClient client;
        private readonly string model;

        public MetadataExtractor(OllamaClient ollamaClient, string modelName = "llama3.2")
        {
            client = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
            model = modelName;
        }

        /// <summary>
        /// Extrae metadatos de un archivo basándose en su nombre y contenido
        /// </summary>
        public async Task<ExtractedMetadata> ExtractFromFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new ExtractedMetadata
                    {
                        Success = false,
                        Error = "Archivo no encontrado"
                    };
                }

                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var extension = Path.GetExtension(filePath).ToLowerInvariant();

                // Intentar extraer del nombre primero
                var metadata = await ExtractFromFileNameAsync(fileName);

                // Si no hay suficiente información, analizar contenido
                if (string.IsNullOrWhiteSpace(metadata.Author) || string.IsNullOrWhiteSpace(metadata.Title))
                {
                    var contentMetadata = await ExtractFromContentAsync(filePath, extension);
                    
                    // Combinar resultados
                    metadata.Author = metadata.Author ?? contentMetadata.Author;
                    metadata.Title = metadata.Title ?? contentMetadata.Title;
                    metadata.Year = metadata.Year ?? contentMetadata.Year;
                    metadata.Genre = metadata.Genre ?? contentMetadata.Genre;
                }

                metadata.FilePath = filePath;
                metadata.Success = true;

                return metadata;
            }
            catch (Exception ex)
            {
                return new ExtractedMetadata
                {
                    Success = false,
                    Error = $"Error extrayendo metadatos: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Extrae metadatos del nombre del archivo usando IA
        /// </summary>
        private async Task<ExtractedMetadata> ExtractFromFileNameAsync(string fileName)
        {
            var prompt = $@"Analiza el siguiente nombre de archivo de un libro y extrae los metadatos.

NOMBRE: {fileName}

Extrae la siguiente información en formato JSON:
{{
  ""author"": ""nombre del autor"",
  ""title"": ""título del libro"",
  ""year"": ""año de publicación (si está presente)"",
  ""genre"": ""género literario (si se puede inferir)""
}}

Si no puedes determinar algún campo, usa null.
Responde SOLO con el JSON, sin explicaciones adicionales.

JSON:";

            var response = await client.GenerateAsync(model, prompt);

            if (string.IsNullOrWhiteSpace(response))
            {
                return new ExtractedMetadata();
            }

            try
            {
                // Parsear respuesta JSON
                return ParseMetadataFromJson(response);
            }
            catch
            {
                // Si falla el parsing JSON, intentar extracción por regex
                return ParseMetadataFromText(response);
            }
        }

        /// <summary>
        /// Extrae metadatos del contenido del archivo
        /// </summary>
        private async Task<ExtractedMetadata> ExtractFromContentAsync(string filePath, string extension)
        {
            try
            {
                string content = string.Empty;

                // Leer primeras líneas del archivo
                switch (extension)
                {
                    case ".txt":
                    case ".epub":
                    case ".pdf":
                        var lines = await File.ReadAllLinesAsync(filePath);
                        content = string.Join("\n", lines.Take(50)); // Primeras 50 líneas
                        break;
                }

                if (string.IsNullOrWhiteSpace(content))
                {
                    return new ExtractedMetadata();
                }

                // Limitar contenido
                if (content.Length > 2000)
                {
                    content = content.Substring(0, 2000);
                }

                var prompt = $@"Analiza el siguiente fragmento del inicio de un libro y extrae los metadatos.

FRAGMENTO:
{content}

Extrae la siguiente información en formato JSON:
{{
  ""author"": ""nombre del autor"",
  ""title"": ""título del libro"",
  ""year"": ""año de publicación (si está presente)"",
  ""genre"": ""género literario""
}}

Si no puedes determinar algún campo, usa null.
Responde SOLO con el JSON, sin explicaciones adicionales.

JSON:";

                var response = await client.GenerateAsync(model, prompt);

                if (string.IsNullOrWhiteSpace(response))
                {
                    return new ExtractedMetadata();
                }

                return ParseMetadataFromJson(response);
            }
            catch
            {
                return new ExtractedMetadata();
            }
        }

        /// <summary>
        /// Parsea metadatos desde respuesta JSON
        /// </summary>
        private ExtractedMetadata ParseMetadataFromJson(string json)
        {
            try
            {
                // Limpiar JSON
                json = json.Trim();
                if (json.StartsWith("```json"))
                {
                    json = json.Substring(7);
                }
                if (json.StartsWith("```"))
                {
                    json = json.Substring(3);
                }
                if (json.EndsWith("```"))
                {
                    json = json.Substring(0, json.Length - 3);
                }
                json = json.Trim();

                // Parsear manualmente (simple)
                var metadata = new ExtractedMetadata();

                var authorMatch = Regex.Match(json, @"""author""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (authorMatch.Success && authorMatch.Groups[1].Value.ToLower() != "null")
                {
                    metadata.Author = authorMatch.Groups[1].Value;
                }

                var titleMatch = Regex.Match(json, @"""title""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (titleMatch.Success && titleMatch.Groups[1].Value.ToLower() != "null")
                {
                    metadata.Title = titleMatch.Groups[1].Value;
                }

                var yearMatch = Regex.Match(json, @"""year""\s*:\s*""?(\d{4})""?", RegexOptions.IgnoreCase);
                if (yearMatch.Success)
                {
                    metadata.Year = yearMatch.Groups[1].Value;
                }

                var genreMatch = Regex.Match(json, @"""genre""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (genreMatch.Success && genreMatch.Groups[1].Value.ToLower() != "null")
                {
                    metadata.Genre = genreMatch.Groups[1].Value;
                }

                return metadata;
            }
            catch
            {
                return new ExtractedMetadata();
            }
        }

        /// <summary>
        /// Parsea metadatos desde texto libre
        /// </summary>
        private ExtractedMetadata ParseMetadataFromText(string text)
        {
            var metadata = new ExtractedMetadata();

            // Buscar patrones comunes
            var authorMatch = Regex.Match(text, @"(?:author|autor):\s*(.+?)(?:\n|$)", RegexOptions.IgnoreCase);
            if (authorMatch.Success)
            {
                metadata.Author = authorMatch.Groups[1].Value.Trim();
            }

            var titleMatch = Regex.Match(text, @"(?:title|título):\s*(.+?)(?:\n|$)", RegexOptions.IgnoreCase);
            if (titleMatch.Success)
            {
                metadata.Title = titleMatch.Groups[1].Value.Trim();
            }

            var yearMatch = Regex.Match(text, @"(?:year|año):\s*(\d{4})", RegexOptions.IgnoreCase);
            if (yearMatch.Success)
            {
                metadata.Year = yearMatch.Groups[1].Value;
            }

            var genreMatch = Regex.Match(text, @"(?:genre|género):\s*(.+?)(?:\n|$)", RegexOptions.IgnoreCase);
            if (genreMatch.Success)
            {
                metadata.Genre = genreMatch.Groups[1].Value.Trim();
            }

            return metadata;
        }

        /// <summary>
        /// Extrae metadatos de múltiples archivos
        /// </summary>
        public async Task<List<ExtractedMetadata>> ExtractFromFilesAsync(IEnumerable<string> filePaths, IProgress<int> progress = null)
        {
            var results = new List<ExtractedMetadata>();
            var files = filePaths.ToList();
            int processed = 0;

            foreach (var file in files)
            {
                var metadata = await ExtractFromFileAsync(file);
                results.Add(metadata);

                processed++;
                progress?.Report((processed * 100) / files.Count);
            }

            return results;
        }
    }

    /// <summary>
    /// Metadatos extraídos de un archivo
    /// </summary>
    public class ExtractedMetadata
    {
        public bool Success { get; set; }
        public string Author { get; set; }
        public string Title { get; set; }
        public string Year { get; set; }
        public string Genre { get; set; }
        public string FilePath { get; set; }
        public string Error { get; set; }
    }
}
