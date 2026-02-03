using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core.AI
{
    public class SearchOptimization
    {
        public string OriginalQuery { get; set; }
        public string OptimizedQuery { get; set; }
        public List<string> Improvements { get; set; } = new List<string>();
        public double ExpectedImprovement { get; set; }
    }

    /// <summary>
    /// Optimizador automático de búsquedas basado en aprendizaje
    /// </summary>
    public class SearchOptimizer
    {
        private Dictionary<string, int> queryResults = new Dictionary<string, int>();
        private Dictionary<string, List<string>> successfulPatterns = new Dictionary<string, List<string>>();

        public void RecordSearchResult(string query, int resultsCount)
        {
            queryResults[query.ToLower()] = resultsCount;

            // Aprender patrones exitosos
            if (resultsCount > 10)
            {
                var keywords = ExtractKeywords(query);
                foreach (var keyword in keywords)
                {
                    if (!successfulPatterns.ContainsKey(keyword))
                        successfulPatterns[keyword] = new List<string>();
                    
                    successfulPatterns[keyword].Add(query);
                }
            }
        }

        public SearchOptimization OptimizeQuery(string originalQuery)
        {
            var optimization = new SearchOptimization
            {
                OriginalQuery = originalQuery,
                OptimizedQuery = originalQuery
            };

            var lower = originalQuery.ToLower();

            // 1. Agregar nombre completo de autor si solo hay apellido
            if (IsLikelySurname(lower))
            {
                var fullName = ExpandAuthorName(lower);
                if (fullName != null)
                {
                    optimization.OptimizedQuery = fullName;
                    optimization.Improvements.Add($"Expandido '{lower}' a '{fullName}'");
                    optimization.ExpectedImprovement += 0.3;
                }
            }

            // 2. Agregar idioma si no está especificado
            if (!lower.Contains("español") && !lower.Contains("english") && !lower.Contains("inglés"))
            {
                optimization.OptimizedQuery += " español";
                optimization.Improvements.Add("Agregado filtro de idioma (español)");
                optimization.ExpectedImprovement += 0.2;
            }

            // 3. Agregar formato preferido
            if (!ContainsFormat(lower))
            {
                optimization.OptimizedQuery += " epub";
                optimization.Improvements.Add("Agregado formato preferido (epub)");
                optimization.ExpectedImprovement += 0.15;
            }

            // 4. Usar patrones exitosos similares
            var similarSuccessful = FindSimilarSuccessfulQueries(lower);
            if (similarSuccessful.Count > 0)
            {
                var bestPattern = similarSuccessful.First();
                optimization.Improvements.Add($"Patrón similar exitoso: '{bestPattern}'");
                optimization.ExpectedImprovement += 0.25;
            }

            // 5. Remover palabras innecesarias
            var cleaned = RemoveNoiseWords(optimization.OptimizedQuery);
            if (cleaned != optimization.OptimizedQuery)
            {
                optimization.OptimizedQuery = cleaned;
                optimization.Improvements.Add("Removidas palabras innecesarias");
                optimization.ExpectedImprovement += 0.1;
            }

            return optimization;
        }

        private bool IsLikelySurname(string query)
        {
            var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return words.Length == 1 && words[0].Length > 3;
        }

        private string ExpandAuthorName(string surname)
        {
            var authorMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["asimov"] = "Isaac Asimov",
                ["clarke"] = "Arthur C. Clarke",
                ["heinlein"] = "Robert A. Heinlein",
                ["herbert"] = "Frank Herbert",
                ["bradbury"] = "Ray Bradbury",
                ["dick"] = "Philip K. Dick",
                ["verne"] = "Jules Verne",
                ["wells"] = "H.G. Wells",
                ["tolkien"] = "J.R.R. Tolkien",
                ["martin"] = "George R.R. Martin"
            };

            return authorMap.TryGetValue(surname, out var fullName) ? fullName : null;
        }

        private bool ContainsFormat(string query)
        {
            var formats = new[] { "epub", "pdf", "mobi", "azw3", "txt" };
            return formats.Any(f => query.Contains(f));
        }

        private List<string> FindSimilarSuccessfulQueries(string query)
        {
            var keywords = ExtractKeywords(query);
            var similar = new List<string>();

            foreach (var keyword in keywords)
            {
                if (successfulPatterns.TryGetValue(keyword, out var patterns))
                {
                    similar.AddRange(patterns);
                }
            }

            return similar.Distinct().Take(3).ToList();
        }

        private List<string> ExtractKeywords(string query)
        {
            var noiseWords = new[] { "de", "el", "la", "los", "las", "un", "una", "en", "y", "o", "the", "a", "an", "in", "and", "or" };
            
            return query.ToLower()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => !noiseWords.Contains(w) && w.Length > 2)
                .ToList();
        }

        private string RemoveNoiseWords(string query)
        {
            var noiseWords = new[] { "libros", "books", "busca", "search", "encuentra", "find" };
            var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            var cleaned = words.Where(w => !noiseWords.Contains(w.ToLower())).ToList();
            
            return string.Join(" ", cleaned);
        }

        public Dictionary<string, int> GetTopQueries(int count = 10)
        {
            return queryResults
                .OrderByDescending(kvp => kvp.Value)
                .Take(count)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public string GenerateOptimizationReport(SearchOptimization opt)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("🔍 OPTIMIZACIÓN DE BÚSQUEDA\n");
            sb.AppendLine($"Original: \"{opt.OriginalQuery}\"");
            sb.AppendLine($"Optimizada: \"{opt.OptimizedQuery}\"\n");

            if (opt.Improvements.Count > 0)
            {
                sb.AppendLine("MEJORAS APLICADAS:");
                foreach (var improvement in opt.Improvements)
                {
                    sb.AppendLine($"  • {improvement}");
                }
                sb.AppendLine();
            }

            sb.AppendLine($"Mejora esperada: +{opt.ExpectedImprovement * 100:F0}% resultados\n");
            sb.AppendLine("¿Quieres usar la búsqueda optimizada?");

            return sb.ToString();
        }
    }
}
