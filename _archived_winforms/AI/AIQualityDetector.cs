using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SlskDown.AI
{
    /// <summary>
    /// FUNCIONALIDAD #3: Detección de Calidad de Archivos
    /// </summary>
    public class AIQualityDetector
    {
        private readonly OllamaClient ollamaClient;
        private readonly bool enabled;

        public AIQualityDetector(OllamaClient client, bool enabled = true)
        {
            this.ollamaClient = client;
            this.enabled = enabled;
        }

        /// <summary>
        /// Analiza la calidad de un archivo basándose en su nombre y metadatos
        /// </summary>
        public async Task<QualityAnalysis> AnalyzeFileQualityAsync(string fileName, string username = null, long fileSize = 0)
        {
            if (!enabled)
                return new QualityAnalysis { FileName = fileName, QualityScore = 5.0 };

            try
            {
                var prompt = BuildQualityPrompt(fileName, username, fileSize);
                var response = await ollamaClient.GenerateAsync(prompt);
                return ParseQualityAnalysis(fileName, response);
            }
            catch (Exception ex)
            {
                return new QualityAnalysis 
                { 
                    FileName = fileName, 
                    QualityScore = 5.0,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Detecta si un archivo es spam o falso
        /// </summary>
        public async Task<bool> IsSpamOrFakeAsync(string fileName)
        {
            if (!enabled)
                return false;

            try
            {
                var prompt = $@"Analiza si este nombre de archivo es spam, falso o engañoso:

{fileName}

Indicadores de spam:
- Uso excesivo de mayúsculas
- Palabras como BEST, FREE, CLICK, DOWNLOAD NOW
- Caracteres especiales excesivos
- Nombres genéricos sin información real
- Extensiones sospechosas

Responde SOLO con: SI (es spam) o NO (es legítimo)";

                var response = await ollamaClient.GenerateAsync(prompt);
                return response.Trim().ToUpperInvariant().StartsWith("SI");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Valida coherencia de metadatos
        /// </summary>
        public async Task<MetadataValidation> ValidateMetadataAsync(string fileName, string author, long fileSize, string extension)
        {
            if (!enabled)
                return new MetadataValidation { IsValid = true };

            try
            {
                var prompt = $@"Valida la coherencia de estos metadatos de archivo:

Nombre: {fileName}
Autor: {author}
Tamaño: {FormatFileSize(fileSize)}
Extensión: {extension}

Verifica:
1. ¿El nombre coincide con el autor?
2. ¿El tamaño es razonable para el tipo de archivo?
3. ¿La extensión es apropiada para el contenido?
4. ¿Hay inconsistencias obvias?

Responde:
VALIDO: SI/NO
PROBLEMAS: [lista de problemas encontrados]
CONFIANZA: [0-10]";

                var response = await ollamaClient.GenerateAsync(prompt);
                return ParseMetadataValidation(response);
            }
            catch
            {
                return new MetadataValidation { IsValid = true };
            }
        }

        /// <summary>
        /// Analiza calidad de múltiples archivos en lote
        /// </summary>
        public async Task<List<QualityAnalysis>> AnalyzeBatchAsync(List<(string fileName, string username, long fileSize)> files)
        {
            var results = new List<QualityAnalysis>();

            foreach (var (fileName, username, fileSize) in files)
            {
                var analysis = await AnalyzeFileQualityAsync(fileName, username, fileSize);
                results.Add(analysis);
            }

            return results;
        }

        private string BuildQualityPrompt(string fileName, string username, long fileSize)
        {
            return $@"Analiza la calidad de este archivo:

Nombre: {fileName}
{(username != null ? $"Proveedor: {username}" : "")}
{(fileSize > 0 ? $"Tamaño: {FormatFileSize(fileSize)}" : "")}

Evalúa:
1. Claridad del nombre (¿es descriptivo?)
2. Profesionalismo (¿parece legítimo?)
3. Completitud de información
4. Indicadores de calidad (edición, formato, etc.)
5. Señales de advertencia (spam, fake, etc.)

Proporciona:
- Puntuación de calidad (0-10)
- Nivel de confianza (bajo, medio, alto)
- Indicadores positivos
- Indicadores negativos
- Recomendación (descargar, revisar, evitar)

Formato:
SCORE: [0-10]
CONFIANZA: [nivel]
POSITIVOS: [lista]
NEGATIVOS: [lista]
RECOMENDACION: [acción]";
        }

        private QualityAnalysis ParseQualityAnalysis(string fileName, string response)
        {
            var analysis = new QualityAnalysis { FileName = fileName };
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.StartsWith("SCORE:", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(line.Substring(6).Trim(), out var score))
                        analysis.QualityScore = score;
                }
                else if (line.StartsWith("CONFIANZA:", StringComparison.OrdinalIgnoreCase))
                {
                    analysis.ConfidenceLevel = line.Substring(10).Trim();
                }
                else if (line.StartsWith("POSITIVOS:", StringComparison.OrdinalIgnoreCase))
                {
                    analysis.PositiveIndicators = line.Substring(10).Trim();
                }
                else if (line.StartsWith("NEGATIVOS:", StringComparison.OrdinalIgnoreCase))
                {
                    analysis.NegativeIndicators = line.Substring(10).Trim();
                }
                else if (line.StartsWith("RECOMENDACION:", StringComparison.OrdinalIgnoreCase))
                {
                    analysis.Recommendation = line.Substring(14).Trim();
                }
            }

            // Clasificar por score
            if (analysis.QualityScore >= 8)
                analysis.QualityLevel = "Excelente";
            else if (analysis.QualityScore >= 6)
                analysis.QualityLevel = "Buena";
            else if (analysis.QualityScore >= 4)
                analysis.QualityLevel = "Aceptable";
            else
                analysis.QualityLevel = "Baja";

            return analysis;
        }

        private MetadataValidation ParseMetadataValidation(string response)
        {
            var validation = new MetadataValidation();
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.StartsWith("VALIDO:", StringComparison.OrdinalIgnoreCase))
                {
                    validation.IsValid = line.Substring(7).Trim().ToUpperInvariant() == "SI";
                }
                else if (line.StartsWith("PROBLEMAS:", StringComparison.OrdinalIgnoreCase))
                {
                    validation.Issues = line.Substring(10).Trim();
                }
                else if (line.StartsWith("CONFIANZA:", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(line.Substring(10).Trim(), out var confidence))
                        validation.ConfidenceScore = confidence;
                }
            }

            return validation;
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    public class QualityAnalysis
    {
        public string FileName { get; set; }
        public double QualityScore { get; set; }
        public string QualityLevel { get; set; }
        public string ConfidenceLevel { get; set; }
        public string PositiveIndicators { get; set; }
        public string NegativeIndicators { get; set; }
        public string Recommendation { get; set; }
        public string Error { get; set; }
    }

    public class MetadataValidation
    {
        public bool IsValid { get; set; }
        public string Issues { get; set; }
        public double ConfidenceScore { get; set; }
    }
}
