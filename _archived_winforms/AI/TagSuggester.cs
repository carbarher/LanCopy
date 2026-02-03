using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SlskDown.AI
{
    /// <summary>
    /// Sugerencias de etiquetas (tags) usando Ollama
    /// </summary>
    public class TagSuggester
    {
        private readonly OllamaClient client;
        private readonly string model;
        private const int MAX_CONTENT_LENGTH = 2000;

        public TagSuggester(OllamaClient ollamaClient, string modelName = "llama3.2")
        {
            client = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
            model = modelName;
        }

        /// <summary>
        /// Sugiere etiquetas para un archivo
        /// </summary>
        public async Task<TagSuggestion> SuggestTagsAsync(string filePath, string author = null, string title = null)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new TagSuggestion
                    {
                        Success = false,
                        Error = "Archivo no encontrado"
                    };
                }

                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                
                // Extraer muestra de contenido
                string content = await ExtractContentSampleAsync(filePath, extension);

                // Construir contexto
                var context = new StringBuilder();
                context.AppendLine($"NOMBRE ARCHIVO: {fileName}");
                
                if (!string.IsNullOrWhiteSpace(author))
                {
                    context.AppendLine($"AUTOR: {author}");
                }
                
                if (!string.IsNullOrWhiteSpace(title))
                {
                    context.AppendLine($"TÍTULO: {title}");
                }
                
                if (!string.IsNullOrWhiteSpace(content))
                {
                    context.AppendLine($"\nFRAGMENTO DEL CONTENIDO:");
                    context.AppendLine(content);
                }

                var prompt = $@"Analiza la siguiente información sobre un libro y sugiere etiquetas relevantes.

{context}

Genera etiquetas en las siguientes categorías:
1. GÉNERO: género literario principal
2. TEMAS: temas principales del libro (máximo 5)
3. ÉPOCA: época histórica o periodo temporal
4. ESTILO: estilo literario o características
5. PÚBLICO: público objetivo (adultos, juvenil, infantil, etc.)

Responde en formato JSON:
{{
  ""genre"": [""género1"", ""género2""],
  ""themes"": [""tema1"", ""tema2"", ""tema3""],
  ""period"": [""época""],
  ""style"": [""estilo1"", ""estilo2""],
  ""audience"": [""público""],
  ""all_tags"": [""lista completa de todas las etiquetas""]
}}

Usa etiquetas en español, concisas y relevantes.
Responde SOLO con el JSON, sin explicaciones adicionales.

JSON:";

                var response = await client.GenerateAsync(model, prompt);

                if (string.IsNullOrWhiteSpace(response))
                {
                    return new TagSuggestion
                    {
                        Success = false,
                        Error = "No se recibió respuesta del modelo"
                    };
                }

                return ParseTagSuggestion(response, filePath);
            }
            catch (Exception ex)
            {
                return new TagSuggestion
                {
                    Success = false,
                    Error = $"Error sugiriendo etiquetas: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Sugiere etiquetas basándose solo en metadatos
        /// </summary>
        public async Task<TagSuggestion> SuggestTagsFromMetadataAsync(string author, string title, string genre = null)
        {
            try
            {
                var prompt = $@"Analiza la siguiente información sobre un libro y sugiere etiquetas relevantes.

AUTOR: {author}
TÍTULO: {title}
{(string.IsNullOrWhiteSpace(genre) ? "" : $"GÉNERO: {genre}")}

Genera etiquetas en las siguientes categorías:
1. GÉNERO: género literario principal
2. TEMAS: temas principales del libro (máximo 5)
3. ÉPOCA: época histórica o periodo temporal
4. ESTILO: estilo literario o características
5. PÚBLICO: público objetivo

Responde en formato JSON:
{{
  ""genre"": [""género1"", ""género2""],
  ""themes"": [""tema1"", ""tema2"", ""tema3""],
  ""period"": [""época""],
  ""style"": [""estilo1"", ""estilo2""],
  ""audience"": [""público""],
  ""all_tags"": [""lista completa de todas las etiquetas""]
}}

Usa etiquetas en español, concisas y relevantes.
Responde SOLO con el JSON, sin explicaciones adicionales.

JSON:";

                var response = await client.GenerateAsync(model, prompt);

                if (string.IsNullOrWhiteSpace(response))
                {
                    return new TagSuggestion
                    {
                        Success = false,
                        Error = "No se recibió respuesta del modelo"
                    };
                }

                return ParseTagSuggestion(response, $"{author} - {title}");
            }
            catch (Exception ex)
            {
                return new TagSuggestion
                {
                    Success = false,
                    Error = $"Error sugiriendo etiquetas: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Parsea sugerencias de etiquetas desde JSON
        /// </summary>
        private TagSuggestion ParseTagSuggestion(string json, string filePath)
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

                var result = new TagSuggestion
                {
                    Success = true,
                    FilePath = filePath
                };

                // Parsear arrays de etiquetas
                result.GenreTags = ParseTagArray(json, "genre");
                result.ThemeTags = ParseTagArray(json, "themes");
                result.PeriodTags = ParseTagArray(json, "period");
                result.StyleTags = ParseTagArray(json, "style");
                result.AudienceTags = ParseTagArray(json, "audience");
                result.AllTags = ParseTagArray(json, "all_tags");

                // Si all_tags está vacío, combinar todas las categorías
                if (result.AllTags.Count == 0)
                {
                    result.AllTags.AddRange(result.GenreTags);
                    result.AllTags.AddRange(result.ThemeTags);
                    result.AllTags.AddRange(result.PeriodTags);
                    result.AllTags.AddRange(result.StyleTags);
                    result.AllTags.AddRange(result.AudienceTags);
                    result.AllTags = result.AllTags.Distinct().ToList();
                }

                return result;
            }
            catch
            {
                return new TagSuggestion
                {
                    Success = false,
                    Error = "Error parseando respuesta del modelo"
                };
            }
        }

        /// <summary>
        /// Parsea un array de etiquetas del JSON
        /// </summary>
        private List<string> ParseTagArray(string json, string fieldName)
        {
            var tags = new List<string>();

            try
            {
                var pattern = $@"""{fieldName}""\s*:\s*\[(.*?)\]";
                var match = Regex.Match(json, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                if (match.Success)
                {
                    var arrayContent = match.Groups[1].Value;
                    var tagMatches = Regex.Matches(arrayContent, @"""([^""]+)""");

                    foreach (Match tagMatch in tagMatches)
                    {
                        var tag = tagMatch.Groups[1].Value.Trim();
                        if (!string.IsNullOrWhiteSpace(tag))
                        {
                            tags.Add(tag);
                        }
                    }
                }
            }
            catch
            {
                // Ignorar errores de parsing
            }

            return tags;
        }

        /// <summary>
        /// Extrae muestra de contenido del archivo
        /// </summary>
        private async Task<string> ExtractContentSampleAsync(string filePath, string extension)
        {
            try
            {
                switch (extension)
                {
                    case ".txt":
                        var lines = await File.ReadAllLinesAsync(filePath);
                        var sample = string.Join("\n", lines.Take(30));
                        return sample.Length > MAX_CONTENT_LENGTH 
                            ? sample.Substring(0, MAX_CONTENT_LENGTH) 
                            : sample;

                    case ".epub":
                    case ".pdf":
                        var bytes = await File.ReadAllBytesAsync(filePath);
                        var text = Encoding.UTF8.GetString(bytes);
                        text = Regex.Replace(text, "<[^>]+>", " ");
                        text = Regex.Replace(text, @"[\x00-\x1F\x7F]", " ");
                        text = Regex.Replace(text, @"\s+", " ");
                        return text.Length > MAX_CONTENT_LENGTH 
                            ? text.Substring(0, MAX_CONTENT_LENGTH) 
                            : text;

                    default:
                        return string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Sugiere etiquetas para múltiples archivos
        /// </summary>
        public async Task<List<TagSuggestion>> SuggestTagsForFilesAsync(IEnumerable<string> filePaths, IProgress<int> progress = null)
        {
            var results = new List<TagSuggestion>();
            var files = filePaths.ToList();
            int processed = 0;

            foreach (var file in files)
            {
                var suggestion = await SuggestTagsAsync(file);
                results.Add(suggestion);

                processed++;
                progress?.Report((processed * 100) / files.Count);
            }

            return results;
        }
    }

    /// <summary>
    /// Sugerencia de etiquetas para un archivo
    /// </summary>
    public class TagSuggestion
    {
        public bool Success { get; set; }
        public List<string> GenreTags { get; set; } = new List<string>();
        public List<string> ThemeTags { get; set; } = new List<string>();
        public List<string> PeriodTags { get; set; } = new List<string>();
        public List<string> StyleTags { get; set; } = new List<string>();
        public List<string> AudienceTags { get; set; } = new List<string>();
        public List<string> AllTags { get; set; } = new List<string>();
        public string FilePath { get; set; }
        public string Error { get; set; }
    }
}
