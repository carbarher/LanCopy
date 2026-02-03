using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SlskDown.AI
{
    /// <summary>
    /// FUNCIONALIDAD #1: Clasificación Automática de Archivos con IA
    /// </summary>
    public class AIFileClassifier
    {
        private readonly OllamaClient ollamaClient;
        private readonly bool enabled;

        public AIFileClassifier(OllamaClient client, bool enabled = true)
        {
            this.ollamaClient = client;
            this.enabled = enabled;
        }

        /// <summary>
        /// Clasifica un archivo y sugiere carpeta de destino
        /// </summary>
        public async Task<FileClassification> ClassifyFileAsync(string fileName, string author = null)
        {
            if (!enabled)
                return new FileClassification { FileName = fileName, Category = "General" };

            try
            {
                var prompt = BuildClassificationPrompt(fileName, author);
                var response = await ollamaClient.GenerateAsync(prompt);
                return ParseClassificationResponse(fileName, response);
            }
            catch (Exception ex)
            {
                return new FileClassification 
                { 
                    FileName = fileName, 
                    Category = "General",
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Clasifica múltiples archivos en lote
        /// </summary>
        public async Task<List<FileClassification>> ClassifyBatchAsync(List<(string fileName, string author)> files)
        {
            var results = new List<FileClassification>();

            foreach (var (fileName, author) in files)
            {
                var classification = await ClassifyFileAsync(fileName, author);
                results.Add(classification);
            }

            return results;
        }

        /// <summary>
        /// Detecta duplicados semánticos
        /// </summary>
        public async Task<List<DuplicateGroup>> DetectSemanticDuplicatesAsync(List<string> fileNames)
        {
            if (!enabled || fileNames.Count < 2)
                return new List<DuplicateGroup>();

            try
            {
                var prompt = $@"Analiza estos nombres de archivos y agrupa los que sean duplicados semánticos (mismo contenido con diferente nombre):

{string.Join("\n", fileNames.Select((f, i) => $"{i + 1}. {f}"))}

Responde SOLO con grupos de números separados por comas, un grupo por línea. Ejemplo:
1,3,5
2,7
4,9,12";

                var response = await ollamaClient.GenerateAsync(prompt);
                return ParseDuplicateGroups(fileNames, response);
            }
            catch
            {
                return new List<DuplicateGroup>();
            }
        }

        private string BuildClassificationPrompt(string fileName, string author)
        {
            return $@"Analiza este nombre de archivo y clasifícalo:

Archivo: {fileName}
{(author != null ? $"Autor: {author}" : "")}

Proporciona:
1. Categoría principal (ej: Literatura, Ciencia, Historia, etc.)
2. Subcategoría (ej: Novela, Ensayo, Biografía, etc.)
3. Género específico si aplica (ej: Ciencia Ficción, Romance, etc.)
4. Idioma detectado
5. Carpeta sugerida (formato: Categoría/Subcategoría/Autor)

Responde en formato:
CATEGORIA: [categoría]
SUBCATEGORIA: [subcategoría]
GENERO: [género]
IDIOMA: [idioma]
CARPETA: [ruta]";
        }

        private FileClassification ParseClassificationResponse(string fileName, string response)
        {
            var classification = new FileClassification { FileName = fileName };

            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("CATEGORIA:", StringComparison.OrdinalIgnoreCase))
                    classification.Category = line.Substring(10).Trim();
                else if (line.StartsWith("SUBCATEGORIA:", StringComparison.OrdinalIgnoreCase))
                    classification.SubCategory = line.Substring(13).Trim();
                else if (line.StartsWith("GENERO:", StringComparison.OrdinalIgnoreCase))
                    classification.Genre = line.Substring(7).Trim();
                else if (line.StartsWith("IDIOMA:", StringComparison.OrdinalIgnoreCase))
                    classification.Language = line.Substring(7).Trim();
                else if (line.StartsWith("CARPETA:", StringComparison.OrdinalIgnoreCase))
                    classification.SuggestedFolder = line.Substring(8).Trim();
            }

            // Valores por defecto si no se parseó correctamente
            if (string.IsNullOrEmpty(classification.Category))
                classification.Category = "General";
            if (string.IsNullOrEmpty(classification.SuggestedFolder))
                classification.SuggestedFolder = classification.Category;

            return classification;
        }

        private List<DuplicateGroup> ParseDuplicateGroups(List<string> fileNames, string response)
        {
            var groups = new List<DuplicateGroup>();
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var indices = line.Split(',')
                    .Select(s => s.Trim())
                    .Where(s => int.TryParse(s, out _))
                    .Select(int.Parse)
                    .Where(i => i > 0 && i <= fileNames.Count)
                    .ToList();

                if (indices.Count >= 2)
                {
                    var group = new DuplicateGroup
                    {
                        Files = indices.Select(i => fileNames[i - 1]).ToList()
                    };
                    groups.Add(group);
                }
            }

            return groups;
        }
    }

    public class FileClassification
    {
        public string FileName { get; set; }
        public string Category { get; set; }
        public string SubCategory { get; set; }
        public string Genre { get; set; }
        public string Language { get; set; }
        public string SuggestedFolder { get; set; }
        public string Error { get; set; }
    }

    public class DuplicateGroup
    {
        public List<string> Files { get; set; } = new List<string>();
    }
}
