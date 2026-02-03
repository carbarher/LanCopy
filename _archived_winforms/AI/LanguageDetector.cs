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
    /// Detección de idioma mejorada usando Ollama
    /// </summary>
    public class LanguageDetector
    {
        private readonly OllamaClient client;
        private readonly string model;
        private const int MAX_SAMPLE_LENGTH = 1000; // Caracteres a analizar

        public LanguageDetector(OllamaClient ollamaClient, string modelName = "llama3.2")
        {
            client = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
            model = modelName;
        }

        /// <summary>
        /// Detecta el idioma de un archivo
        /// </summary>
        public async Task<LanguageDetectionResult> DetectLanguageAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new LanguageDetectionResult
                    {
                        Success = false,
                        Error = "Archivo no encontrado"
                    };
                }

                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                string content = await ExtractTextSampleAsync(filePath, extension);

                if (string.IsNullOrWhiteSpace(content))
                {
                    // Intentar detectar por nombre de archivo
                    return await DetectFromFileNameAsync(Path.GetFileName(filePath));
                }

                return await DetectFromContentAsync(content, Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                return new LanguageDetectionResult
                {
                    Success = false,
                    Error = $"Error detectando idioma: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Detecta idioma del contenido del archivo
        /// </summary>
        private async Task<LanguageDetectionResult> DetectFromContentAsync(string content, string fileName)
        {
            // Limitar muestra
            if (content.Length > MAX_SAMPLE_LENGTH)
            {
                content = content.Substring(0, MAX_SAMPLE_LENGTH);
            }

            var prompt = $@"Analiza el siguiente texto y determina el idioma principal.

TEXTO:
{content}

NOMBRE DEL ARCHIVO: {fileName}

Responde en formato JSON:
{{
  ""language"": ""código ISO 639-1 del idioma (es, en, fr, de, it, pt, etc.)"",
  ""language_name"": ""nombre del idioma en español"",
  ""confidence"": ""alta/media/baja"",
  ""is_spanish"": true/false,
  ""mixed_languages"": [""lista de idiomas si hay mezcla""]
}}

Responde SOLO con el JSON, sin explicaciones adicionales.

JSON:";

            var response = await client.GenerateAsync(model, prompt);

            if (string.IsNullOrWhiteSpace(response))
            {
                return new LanguageDetectionResult
                {
                    Success = false,
                    Error = "No se recibió respuesta del modelo"
                };
            }

            return ParseLanguageDetection(response, fileName);
        }

        /// <summary>
        /// Detecta idioma del nombre del archivo
        /// </summary>
        private async Task<LanguageDetectionResult> DetectFromFileNameAsync(string fileName)
        {
            var prompt = $@"Analiza el siguiente nombre de archivo y determina el idioma probable del contenido.

NOMBRE: {fileName}

Responde en formato JSON:
{{
  ""language"": ""código ISO 639-1 del idioma (es, en, fr, de, it, pt, etc.)"",
  ""language_name"": ""nombre del idioma en español"",
  ""confidence"": ""baja"",
  ""is_spanish"": true/false
}}

Responde SOLO con el JSON, sin explicaciones adicionales.

JSON:";

            var response = await client.GenerateAsync(model, prompt);

            if (string.IsNullOrWhiteSpace(response))
            {
                return new LanguageDetectionResult
                {
                    Success = false,
                    Error = "No se recibió respuesta del modelo"
                };
            }

            return ParseLanguageDetection(response, fileName);
        }

        /// <summary>
        /// Parsea resultado de detección de idioma
        /// </summary>
        private LanguageDetectionResult ParseLanguageDetection(string json, string fileName)
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

                var result = new LanguageDetectionResult
                {
                    Success = true,
                    FilePath = fileName
                };

                // Parsear campos
                var langMatch = Regex.Match(json, @"""language""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (langMatch.Success)
                {
                    result.LanguageCode = langMatch.Groups[1].Value.ToLower();
                }

                var nameMatch = Regex.Match(json, @"""language_name""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (nameMatch.Success)
                {
                    result.LanguageName = nameMatch.Groups[1].Value;
                }

                var confMatch = Regex.Match(json, @"""confidence""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (confMatch.Success)
                {
                    result.Confidence = confMatch.Groups[1].Value.ToLower();
                }

                var spanishMatch = Regex.Match(json, @"""is_spanish""\s*:\s*(true|false)", RegexOptions.IgnoreCase);
                if (spanishMatch.Success)
                {
                    result.IsSpanish = spanishMatch.Groups[1].Value.ToLower() == "true";
                }

                // Parsear idiomas mezclados
                var mixedMatch = Regex.Match(json, @"""mixed_languages""\s*:\s*\[(.*?)\]", RegexOptions.IgnoreCase);
                if (mixedMatch.Success)
                {
                    var langs = Regex.Matches(mixedMatch.Groups[1].Value, @"""([^""]+)""");
                    result.MixedLanguages = langs.Cast<Match>().Select(m => m.Groups[1].Value).ToList();
                }

                return result;
            }
            catch
            {
                return new LanguageDetectionResult
                {
                    Success = false,
                    Error = "Error parseando respuesta del modelo"
                };
            }
        }

        /// <summary>
        /// Extrae muestra de texto del archivo
        /// </summary>
        private async Task<string> ExtractTextSampleAsync(string filePath, string extension)
        {
            try
            {
                switch (extension)
                {
                    case ".txt":
                        var lines = await File.ReadAllLinesAsync(filePath);
                        return string.Join("\n", lines.Take(20)); // Primeras 20 líneas

                    case ".epub":
                    case ".pdf":
                        var bytes = await File.ReadAllBytesAsync(filePath);
                        var text = Encoding.UTF8.GetString(bytes);
                        text = Regex.Replace(text, "<[^>]+>", " "); // Limpiar HTML
                        text = Regex.Replace(text, @"[\x00-\x1F\x7F]", " "); // Limpiar control chars
                        text = Regex.Replace(text, @"\s+", " ");
                        return text.Trim();

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
        /// Detecta idioma de múltiples archivos
        /// </summary>
        public async Task<List<LanguageDetectionResult>> DetectLanguagesAsync(IEnumerable<string> filePaths, IProgress<int> progress = null)
        {
            var results = new List<LanguageDetectionResult>();
            var files = filePaths.ToList();
            int processed = 0;

            foreach (var file in files)
            {
                var detection = await DetectLanguageAsync(file);
                results.Add(detection);

                processed++;
                progress?.Report((processed * 100) / files.Count);
            }

            return results;
        }

        /// <summary>
        /// Detecta si un texto es español (método rápido)
        /// </summary>
        public async Task<bool> IsSpanishAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            // Limitar muestra
            if (text.Length > 500)
            {
                text = text.Substring(0, 500);
            }

            var prompt = $@"¿El siguiente texto está en español? Responde SOLO 'SI' o 'NO'.

TEXTO: {text}

RESPUESTA:";

            var response = await client.GenerateAsync(model, prompt);
            
            return response?.Trim().ToUpper().StartsWith("SI") ?? false;
        }
    }

    /// <summary>
    /// Resultado de detección de idioma
    /// </summary>
    public class LanguageDetectionResult
    {
        public bool Success { get; set; }
        public string LanguageCode { get; set; }
        public string LanguageName { get; set; }
        public string Confidence { get; set; }
        public bool IsSpanish { get; set; }
        public List<string> MixedLanguages { get; set; } = new List<string>();
        public string FilePath { get; set; }
        public string Error { get; set; }
    }
}
