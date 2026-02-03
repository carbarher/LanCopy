using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SlskDown.AI
{
    /// <summary>
    /// FUNCIONALIDAD #4: Búsqueda Semántica Inteligente
    /// </summary>
    public class SemanticSearch
    {
        private readonly OllamaClient ollamaClient;
        private readonly bool enabled;
        private readonly Dictionary<string, float[]> embeddingsCache = new Dictionary<string, float[]>();

        public SemanticSearch(OllamaClient client, bool enabled = true)
        {
            this.ollamaClient = client;
            this.enabled = enabled;
        }

        /// <summary>
        /// Búsqueda semántica por concepto
        /// </summary>
        public async Task<List<SemanticSearchResult>> SearchByConceptAsync(string concept, List<string> fileNames, int maxResults = 10)
        {
            if (!enabled || fileNames.Count == 0)
                return new List<SemanticSearchResult>();

            try
            {
                // Generar embedding del concepto de búsqueda
                var queryEmbedding = await GetOrGenerateEmbeddingAsync(concept);

                // Generar embeddings de los archivos
                var results = new List<SemanticSearchResult>();
                foreach (var fileName in fileNames)
                {
                    var fileEmbedding = await GetOrGenerateEmbeddingAsync(fileName);
                    var similarity = CosineSimilarity(queryEmbedding, fileEmbedding);

                    results.Add(new SemanticSearchResult
                    {
                        FileName = fileName,
                        SimilarityScore = similarity,
                        MatchReason = await ExplainMatchAsync(concept, fileName, similarity)
                    });
                }

                return results
                    .OrderByDescending(r => r.SimilarityScore)
                    .Take(maxResults)
                    .ToList();
            }
            catch (Exception ex)
            {
                return new List<SemanticSearchResult>
                {
                    new SemanticSearchResult { Error = ex.Message }
                };
            }
        }

        /// <summary>
        /// Búsqueda por descripción natural
        /// </summary>
        public async Task<List<string>> SearchByDescriptionAsync(string description, List<string> fileNames, int maxResults = 10)
        {
            if (!enabled)
                return new List<string>();

            try
            {
                var prompt = $@"Basándote en esta descripción:
""{description}""

Selecciona los {maxResults} archivos más relevantes de esta lista:

{string.Join("\n", fileNames.Select((f, i) => $"{i + 1}. {f}"))}

Responde SOLO con los números de los archivos seleccionados, separados por comas.
Ejemplo: 1,5,7,12";

                var response = await ollamaClient.GenerateAsync(prompt);
                var indices = ParseIndices(response, fileNames.Count);

                return indices.Select(i => fileNames[i]).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Expande consulta con sinónimos y términos relacionados
        /// </summary>
        public async Task<List<string>> ExpandQueryAsync(string query)
        {
            if (!enabled)
                return new List<string> { query };

            try
            {
                var prompt = $@"Para esta consulta de búsqueda:
""{query}""

Genera una lista de términos relacionados, sinónimos y variaciones que podrían ser útiles para encontrar archivos similares.
Incluye:
- Sinónimos directos
- Términos relacionados
- Variaciones de escritura
- Términos en otros idiomas si aplica

Responde con una lista de términos, uno por línea, máximo 10.";

                var response = await ollamaClient.GenerateAsync(prompt);
                var terms = response.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Take(10)
                    .ToList();

                // Incluir el término original
                if (!terms.Contains(query))
                    terms.Insert(0, query);

                return terms;
            }
            catch
            {
                return new List<string> { query };
            }
        }

        /// <summary>
        /// Encuentra archivos similares a uno dado
        /// </summary>
        public async Task<List<SemanticSearchResult>> FindSimilarFilesAsync(string fileName, List<string> allFiles, int maxResults = 5)
        {
            if (!enabled || allFiles.Count == 0)
                return new List<SemanticSearchResult>();

            try
            {
                var targetEmbedding = await GetOrGenerateEmbeddingAsync(fileName);
                var results = new List<SemanticSearchResult>();

                foreach (var file in allFiles)
                {
                    if (file == fileName) continue;

                    var fileEmbedding = await GetOrGenerateEmbeddingAsync(file);
                    var similarity = CosineSimilarity(targetEmbedding, fileEmbedding);

                    if (similarity > 0.5) // Umbral de similitud
                    {
                        results.Add(new SemanticSearchResult
                        {
                            FileName = file,
                            SimilarityScore = similarity
                        });
                    }
                }

                return results
                    .OrderByDescending(r => r.SimilarityScore)
                    .Take(maxResults)
                    .ToList();
            }
            catch
            {
                return new List<SemanticSearchResult>();
            }
        }

        /// <summary>
        /// Agrupa archivos por similitud semántica
        /// </summary>
        public async Task<List<SemanticGroup>> GroupBySimilarityAsync(List<string> fileNames, double similarityThreshold = 0.7)
        {
            if (!enabled || fileNames.Count < 2)
                return new List<SemanticGroup>();

            try
            {
                var groups = new List<SemanticGroup>();
                var processed = new HashSet<string>();

                foreach (var fileName in fileNames)
                {
                    if (processed.Contains(fileName)) continue;

                    var group = new SemanticGroup { Representative = fileName };
                    group.Members.Add(fileName);
                    processed.Add(fileName);

                    var fileEmbedding = await GetOrGenerateEmbeddingAsync(fileName);

                    foreach (var otherFile in fileNames)
                    {
                        if (processed.Contains(otherFile)) continue;

                        var otherEmbedding = await GetOrGenerateEmbeddingAsync(otherFile);
                        var similarity = CosineSimilarity(fileEmbedding, otherEmbedding);

                        if (similarity >= similarityThreshold)
                        {
                            group.Members.Add(otherFile);
                            processed.Add(otherFile);
                        }
                    }

                    if (group.Members.Count > 1)
                        groups.Add(group);
                }

                return groups;
            }
            catch
            {
                return new List<SemanticGroup>();
            }
        }

        private async Task<float[]> GetOrGenerateEmbeddingAsync(string text)
        {
            if (embeddingsCache.TryGetValue(text, out var cached))
                return cached;

            var embedding = await ollamaClient.GenerateEmbeddingsAsync(text);
            embeddingsCache[text] = embedding;

            // Limitar tamaño del caché
            if (embeddingsCache.Count > 1000)
            {
                var toRemove = embeddingsCache.Keys.Take(100).ToList();
                foreach (var key in toRemove)
                    embeddingsCache.Remove(key);
            }

            return embedding;
        }

        private double CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length || a.Length == 0)
                return 0;

            double dotProduct = 0;
            double magnitudeA = 0;
            double magnitudeB = 0;

            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                magnitudeA += a[i] * a[i];
                magnitudeB += b[i] * b[i];
            }

            magnitudeA = Math.Sqrt(magnitudeA);
            magnitudeB = Math.Sqrt(magnitudeB);

            if (magnitudeA == 0 || magnitudeB == 0)
                return 0;

            return dotProduct / (magnitudeA * magnitudeB);
        }

        private async Task<string> ExplainMatchAsync(string query, string fileName, double similarity)
        {
            if (similarity < 0.5)
                return "Baja similitud";

            try
            {
                var prompt = $@"Explica brevemente por qué este archivo:
""{fileName}""

Es relevante para esta búsqueda:
""{query}""

Responde en una frase corta (máximo 20 palabras).";

                var response = await ollamaClient.GenerateAsync(prompt);
                return response.Trim();
            }
            catch
            {
                return similarity >= 0.8 ? "Alta similitud" : "Similitud moderada";
            }
        }

        private List<int> ParseIndices(string response, int maxIndex)
        {
            return response.Split(',')
                .Select(s => s.Trim())
                .Where(s => int.TryParse(s, out _))
                .Select(int.Parse)
                .Where(i => i > 0 && i <= maxIndex)
                .Select(i => i - 1) // Convertir a índice base 0
                .ToList();
        }
    }

    public class SemanticSearchResult
    {
        public string FileName { get; set; }
        public double SimilarityScore { get; set; }
        public string MatchReason { get; set; }
        public string Error { get; set; }
    }

    public class SemanticGroup
    {
        public string Representative { get; set; }
        public List<string> Members { get; set; } = new List<string>();
    }
}
