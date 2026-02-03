using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown
{
    /// <summary>
    /// Motor de búsqueda fuzzy con tolerancia a errores tipográficos
    /// Usa algoritmo Levenshtein Distance para encontrar coincidencias aproximadas
    /// </summary>
    public class FuzzySearch
    {
        private readonly int maxDistance;
        private readonly Dictionary<string, string> correctionCache;
        private readonly Dictionary<string, List<string>> suggestionCache;
        
        // Configuración
        private const int DEFAULT_MAX_DISTANCE = 2; // Máximo 2 caracteres de diferencia
        private const int MIN_WORD_LENGTH_FOR_FUZZY = 4; // Solo palabras de 4+ caracteres
        private const double SIMILARITY_THRESHOLD = 0.7; // 70% similitud mínima
        
        public FuzzySearch(int maxDistance = DEFAULT_MAX_DISTANCE)
        {
            this.maxDistance = maxDistance;
            this.correctionCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            this.suggestionCache = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Calcula la distancia de Levenshtein entre dos strings
        /// </summary>
        public static int LevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source))
                return string.IsNullOrEmpty(target) ? 0 : target.Length;
            
            if (string.IsNullOrEmpty(target))
                return source.Length;
            
            int sourceLength = source.Length;
            int targetLength = target.Length;
            
            // Optimización: si la diferencia de longitud es mayor que maxDistance, retornar temprano
            if (Math.Abs(sourceLength - targetLength) > DEFAULT_MAX_DISTANCE)
                return Math.Abs(sourceLength - targetLength);
            
            var distance = new int[sourceLength + 1, targetLength + 1];
            
            // Inicializar primera fila y columna
            for (int i = 0; i <= sourceLength; i++)
                distance[i, 0] = i;
            
            for (int j = 0; j <= targetLength; j++)
                distance[0, j] = j;
            
            // Calcular distancia
            for (int i = 1; i <= sourceLength; i++)
            {
                for (int j = 1; j <= targetLength; j++)
                {
                    int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
                    
                    distance[i, j] = Math.Min(
                        Math.Min(
                            distance[i - 1, j] + 1,      // Eliminación
                            distance[i, j - 1] + 1),     // Inserción
                        distance[i - 1, j - 1] + cost);  // Sustitución
                }
            }
            
            return distance[sourceLength, targetLength];
        }
        
        /// <summary>
        /// Calcula similitud entre dos strings (0.0 - 1.0)
        /// </summary>
        public static double CalculateSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                return 0.0;
            
            int distance = LevenshteinDistance(source, target);
            int maxLength = Math.Max(source.Length, target.Length);
            
            return 1.0 - ((double)distance / maxLength);
        }
        
        /// <summary>
        /// Busca coincidencias fuzzy en una lista de candidatos
        /// </summary>
        public List<FuzzyMatch> FindMatches(string query, IEnumerable<string> candidates)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<FuzzyMatch>();
            
            var matches = new List<FuzzyMatch>();
            var queryLower = query.ToLowerInvariant();
            
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;
                
                var candidateLower = candidate.ToLowerInvariant();
                
                // Coincidencia exacta (máxima prioridad)
                if (candidateLower == queryLower)
                {
                    matches.Add(new FuzzyMatch
                    {
                        Value = candidate,
                        Distance = 0,
                        Similarity = 1.0,
                        MatchType = FuzzyMatchType.Exact
                    });
                    continue;
                }
                
                // Coincidencia de substring (alta prioridad)
                if (candidateLower.Contains(queryLower))
                {
                    matches.Add(new FuzzyMatch
                    {
                        Value = candidate,
                        Distance = 0,
                        Similarity = 0.95,
                        MatchType = FuzzyMatchType.Substring
                    });
                    continue;
                }
                
                // Coincidencia fuzzy (si la palabra es suficientemente larga)
                if (query.Length >= MIN_WORD_LENGTH_FOR_FUZZY)
                {
                    int distance = LevenshteinDistance(queryLower, candidateLower);
                    
                    if (distance <= maxDistance)
                    {
                        double similarity = CalculateSimilarity(queryLower, candidateLower);
                        
                        if (similarity >= SIMILARITY_THRESHOLD)
                        {
                            matches.Add(new FuzzyMatch
                            {
                                Value = candidate,
                                Distance = distance,
                                Similarity = similarity,
                                MatchType = FuzzyMatchType.Fuzzy
                            });
                        }
                    }
                }
            }
            
            // Ordenar por tipo de match y similitud
            return matches
                .OrderBy(m => m.MatchType)
                .ThenByDescending(m => m.Similarity)
                .ThenBy(m => m.Distance)
                .ToList();
        }
        
        /// <summary>
        /// Obtiene sugerencias de corrección para una búsqueda
        /// </summary>
        public List<string> GetSuggestions(string query, IEnumerable<string> dictionary, int maxSuggestions = 5)
        {
            // Verificar caché
            if (suggestionCache.TryGetValue(query, out var cachedSuggestions))
                return cachedSuggestions;
            
            var matches = FindMatches(query, dictionary);
            
            var suggestions = matches
                .Where(m => m.MatchType != FuzzyMatchType.Exact)
                .Take(maxSuggestions)
                .Select(m => m.Value)
                .ToList();
            
            // Guardar en caché
            suggestionCache[query] = suggestions;
            
            return suggestions;
        }
        
        /// <summary>
        /// Aprende una corrección (para mejorar futuras sugerencias)
        /// </summary>
        public void LearnCorrection(string original, string corrected)
        {
            correctionCache[original] = corrected;
        }
        
        /// <summary>
        /// Obtiene corrección aprendida
        /// </summary>
        public string GetLearnedCorrection(string query)
        {
            return correctionCache.TryGetValue(query, out var correction) ? correction : null;
        }
        
        /// <summary>
        /// Limpia cachés
        /// </summary>
        public void ClearCache()
        {
            suggestionCache.Clear();
        }
        
        /// <summary>
        /// Busca coincidencias fuzzy en nombres de autores
        /// </summary>
        public List<string> SearchAuthors(string query, List<string> authors)
        {
            // Primero verificar si hay corrección aprendida
            var learned = GetLearnedCorrection(query);
            if (learned != null)
            {
                query = learned;
            }
            
            var matches = FindMatches(query, authors);
            
            return matches
                .Where(m => m.Similarity >= SIMILARITY_THRESHOLD)
                .Select(m => m.Value)
                .ToList();
        }
        
        /// <summary>
        /// Obtiene estadísticas de búsqueda fuzzy
        /// </summary>
        public FuzzySearchStats GetStats()
        {
            return new FuzzySearchStats
            {
                CachedCorrections = correctionCache.Count,
                CachedSuggestions = suggestionCache.Count,
                MaxDistance = maxDistance,
                SimilarityThreshold = SIMILARITY_THRESHOLD
            };
        }
    }
    
    /// <summary>
    /// Resultado de coincidencia fuzzy
    /// </summary>
    public class FuzzyMatch
    {
        public string Value { get; set; }
        public int Distance { get; set; }
        public double Similarity { get; set; }
        public FuzzyMatchType MatchType { get; set; }
    }
    
    /// <summary>
    /// Tipo de coincidencia
    /// </summary>
    public enum FuzzyMatchType
    {
        Exact = 0,      // Coincidencia exacta
        Substring = 1,  // Substring encontrado
        Fuzzy = 2       // Coincidencia fuzzy
    }
    
    /// <summary>
    /// Estadísticas de búsqueda fuzzy
    /// </summary>
    public class FuzzySearchStats
    {
        public int CachedCorrections { get; set; }
        public int CachedSuggestions { get; set; }
        public int MaxDistance { get; set; }
        public double SimilarityThreshold { get; set; }
        
        public override string ToString()
        {
            return $"Fuzzy Search Stats: " +
                   $"Correcciones: {CachedCorrections} | " +
                   $"Sugerencias: {CachedSuggestions} | " +
                   $"Max Distance: {MaxDistance} | " +
                   $"Threshold: {SimilarityThreshold:P0}";
        }
    }
}
