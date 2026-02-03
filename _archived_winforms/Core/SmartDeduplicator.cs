using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SlskDown.Core
{
    /// <summary>
    /// Deduplicador inteligente que detecta archivos duplicados entre redes
    /// usando normalización de nombres, comparación de hashes y heurísticas
    /// </summary>
    public class SmartDeduplicator
    {
        private readonly Dictionary<string, List<SearchResult>> _resultsByNormalizedName = new Dictionary<string, List<SearchResult>>();
        private readonly Dictionary<string, SearchResult> _resultsByHash = new Dictionary<string, SearchResult>();

        /// <summary>
        /// Agrega un resultado y lo deduplica contra resultados existentes
        /// </summary>
        public DeduplicationResult AddResult(SearchResult result)
        {
            // 1. Verificar por hash exacto (más confiable)
            if (!string.IsNullOrEmpty(result.FileHash))
            {
                if (_resultsByHash.TryGetValue(result.FileHash, out var existingByHash))
                {
                    return new DeduplicationResult
                    {
                        IsDuplicate = true,
                        OriginalResult = existingByHash,
                        DuplicateResult = result,
                        MatchType = DuplicateMatchType.ExactHash,
                        BetterSource = ChooseBetterSource(existingByHash, result)
                    };
                }
                _resultsByHash[result.FileHash] = result;
            }

            // 2. Normalizar nombre de archivo
            var normalizedName = NormalizeFileName(result.FileName);
            var sizeKey = $"{normalizedName}_{result.SizeBytes}";

            if (_resultsByNormalizedName.TryGetValue(sizeKey, out var existingResults))
            {
                // Buscar coincidencia por similitud
                foreach (var existing in existingResults)
                {
                    var similarity = CalculateSimilarity(result.FileName, existing.FileName);
                    
                    if (similarity > 0.85) // 85% similar
                    {
                        return new DeduplicationResult
                        {
                            IsDuplicate = true,
                            OriginalResult = existing,
                            DuplicateResult = result,
                            MatchType = DuplicateMatchType.SimilarName,
                            Similarity = similarity,
                            BetterSource = ChooseBetterSource(existing, result)
                        };
                    }
                }
                
                existingResults.Add(result);
            }
            else
            {
                _resultsByNormalizedName[sizeKey] = new List<SearchResult> { result };
            }

            return new DeduplicationResult
            {
                IsDuplicate = false,
                OriginalResult = result
            };
        }

        /// <summary>
        /// Normaliza nombre de archivo eliminando variaciones comunes
        /// Optimizado con Span<T> para reducir allocations
        /// </summary>
        private string NormalizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return string.Empty;

            // Usar StringBuilder del pool
            var sb = StringBuilderPool.Get();
            try
            {
                var span = fileName.AsSpan();
                
                // Eliminar extensión usando Span
                var nameWithoutExt = FastStringOps.GetFileNameWithoutExtension(span);
                sb.Append(nameWithoutExt);
                
                var normalized = sb.ToString().ToLowerInvariant();
                sb.Clear();

                // Eliminar caracteres especiales
                normalized = Regex.Replace(normalized, @"[^\w\s]", " ");

                // Eliminar palabras comunes de release
                var stopWords = new[] { "proper", "repack", "internal", "limited", "festival", 
                                       "retail", "dvdrip", "brrip", "bluray", "1080p", "720p", 
                                       "x264", "x265", "aac", "mp3", "flac", "epub", "mobi" };
                
                foreach (var word in stopWords)
                {
                    normalized = Regex.Replace(normalized, $@"\b{word}\b", " ", RegexOptions.IgnoreCase);
                }

                // Eliminar años entre paréntesis o corchetes
                normalized = Regex.Replace(normalized, @"[\[\(]\d{4}[\]\)]", " ");

                // Normalizar espacios
                normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

                return normalized;
            }
            finally
            {
                StringBuilderPool.Return(sb);
            }
        }

        /// <summary>
        /// Calcula similitud entre dos nombres usando Levenshtein distance
        /// </summary>
        private double CalculateSimilarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
                return 0;

            var n1 = NormalizeFileName(s1);
            var n2 = NormalizeFileName(s2);

            if (n1 == n2)
                return 1.0;

            var distance = LevenshteinDistance(n1, n2);
            var maxLength = Math.Max(n1.Length, n2.Length);

            return maxLength == 0 ? 0 : 1.0 - ((double)distance / maxLength);
        }

        /// <summary>
        /// Calcula distancia de Levenshtein entre dos strings
        /// </summary>
        private int LevenshteinDistance(string s1, string s2)
        {
            var len1 = s1.Length;
            var len2 = s2.Length;
            var matrix = new int[len1 + 1, len2 + 1];

            for (int i = 0; i <= len1; i++)
                matrix[i, 0] = i;

            for (int j = 0; j <= len2; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= len1; i++)
            {
                for (int j = 1; j <= len2; j++)
                {
                    var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost
                    );
                }
            }

            return matrix[len1, len2];
        }

        /// <summary>
        /// Elige la mejor fuente entre dos resultados duplicados
        /// </summary>
        private SearchResult ChooseBetterSource(SearchResult r1, SearchResult r2)
        {
            // Prioridad 1: Slots libres
            var slots1 = r1.FreeSlots ?? 0;
            var slots2 = r2.FreeSlots ?? 0;
            
            if (slots1 > slots2)
                return r1;
            if (slots2 > slots1)
                return r2;

            // Prioridad 2: Cola más corta
            if (r1.QueueLength < r2.QueueLength)
                return r1;
            if (r2.QueueLength < r1.QueueLength)
                return r2;

            // Prioridad 3: Preferir Soulseek
            if (r1.NetworkSource == "Soulseek")
                return r1;
            if (r2.NetworkSource == "Soulseek")
                return r2;

            // Por defecto, mantener el primero
            return r1;
        }

        /// <summary>
        /// Limpia el deduplicador
        /// </summary>
        public void Clear()
        {
            _resultsByNormalizedName.Clear();
            _resultsByHash.Clear();
        }

        /// <summary>
        /// Obtiene estadísticas de deduplicación
        /// </summary>
        public DeduplicationStats GetStats()
        {
            return new DeduplicationStats
            {
                TotalUniqueFiles = _resultsByNormalizedName.Values.Sum(list => list.Count),
                TotalByHash = _resultsByHash.Count,
                TotalByName = _resultsByNormalizedName.Count
            };
        }
    }

    /// <summary>
    /// Resultado de deduplicación
    /// </summary>
    public class DeduplicationResult
    {
        public bool IsDuplicate { get; set; }
        public SearchResult OriginalResult { get; set; }
        public SearchResult DuplicateResult { get; set; }
        public DuplicateMatchType MatchType { get; set; }
        public double Similarity { get; set; }
        public SearchResult BetterSource { get; set; }
    }

    /// <summary>
    /// Tipo de coincidencia de duplicado
    /// </summary>
    public enum DuplicateMatchType
    {
        None,
        ExactHash,
        SimilarName,
        SameSize
    }

    /// <summary>
    /// Estadísticas de deduplicación
    /// </summary>
    public class DeduplicationStats
    {
        public int TotalUniqueFiles { get; set; }
        public int TotalByHash { get; set; }
        public int TotalByName { get; set; }
    }
}
