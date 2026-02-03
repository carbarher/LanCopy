using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SlskDown.Models;

namespace SlskDown.Core.AI
{
    /// <summary>
    /// Motor de búsqueda inteligente con expansión de queries usando IA
    /// </summary>
    public class IntelligentSearchEngine
    {
        private readonly OllamaClient ollama;
        private readonly Func<string, Task<List<SearchResult>>> searchFunction;

        public event Action<string> OnLog;

        public IntelligentSearchEngine(OllamaClient ollama, Func<string, Task<List<SearchResult>>> searchFunction)
        {
            this.ollama = ollama;
            this.searchFunction = searchFunction;
        }

        /// <summary>
        /// Búsqueda inteligente con expansión de query
        /// </summary>
        public async Task<List<SearchResult>> SmartSearchAsync(string query)
        {
            try
            {
                Log($"🔍 Búsqueda inteligente: {query}");

                // 1. Expandir query con IA
                var expandedQueries = await ExpandQueryAsync(query);
                Log($"📝 Variaciones generadas: {expandedQueries.Count}");

                // 2. Buscar con todas las variaciones
                var allResults = new List<SearchResult>();
                foreach (var variant in expandedQueries)
                {
                    Log($"🔎 Buscando: {variant}");
                    var results = await searchFunction(variant);
                    allResults.AddRange(results);
                }

                // 3. Eliminar duplicados
                var uniqueResults = allResults
                    .GroupBy(r => r.FileName)
                    .Select(g => g.First())
                    .ToList();

                // 4. Rankear por relevancia semántica
                var rankedResults = await RankBySimilarityAsync(query, uniqueResults);

                Log($"✅ Encontrados {rankedResults.Count} resultados únicos");
                return rankedResults;
            }
            catch (Exception ex)
            {
                Log($"❌ Error en SmartSearchAsync: {ex.Message}");
                return new List<SearchResult>();
            }
        }

        /// <summary>
        /// Expande query con sinónimos y variaciones
        /// </summary>
        private async Task<List<string>> ExpandQueryAsync(string query)
        {
            try
            {
                var prompt = $@"
Genera variaciones de búsqueda para encontrar archivos P2P.
Query original: ""{query}""

Genera 5 variaciones incluyendo:
1. Nombre completo si es abreviado
2. Variaciones de ortografía
3. Sinónimos o nombres alternativos
4. Versiones en otros idiomas si aplica
5. Términos relacionados

Formato: Una variación por línea, sin numeración.
Ejemplo para ""garcia marquez"":
Gabriel García Márquez
Gabo
GGM
García Márquez Gabriel
Gabriel Garcia Marquez
";

                var response = await ollama.GetCompletionAsync(prompt, temperature: 0.5);
                
                if (string.IsNullOrEmpty(response))
                    return new List<string> { query };

                var variations = response
                    .Split('\n')
                    .Select(v => v.Trim())
                    .Where(v => !string.IsNullOrEmpty(v) && !v.StartsWith("-") && !v.StartsWith("•"))
                    .Take(5)
                    .ToList();

                // Siempre incluir query original
                if (!variations.Contains(query))
                    variations.Insert(0, query);

                return variations;
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error expandiendo query: {ex.Message}");
                return new List<string> { query };
            }
        }

        /// <summary>
        /// Rankea resultados por similitud semántica
        /// </summary>
        private async Task<List<SearchResult>> RankBySimilarityAsync(string originalQuery, List<SearchResult> results)
        {
            try
            {
                // Obtener embedding del query original
                var queryEmbedding = await ollama.GetEmbeddingAsync(originalQuery);
                if (queryEmbedding == null)
                    return results;

                // Calcular similitud para cada resultado
                foreach (var result in results)
                {
                    var resultEmbedding = await ollama.GetEmbeddingAsync(result.FileName);
                    if (resultEmbedding != null)
                    {
                        result.AIRelevanceScore = CosineSimilarity(queryEmbedding, resultEmbedding);
                    }
                }

                // Ordenar por relevancia
                return results
                    .OrderByDescending(r => r.AIRelevanceScore)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error rankeando resultados: {ex.Message}");
                return results;
            }
        }

        /// <summary>
        /// Calcula similitud coseno entre dos vectores
        /// </summary>
        private double CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length)
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

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
    }
}
