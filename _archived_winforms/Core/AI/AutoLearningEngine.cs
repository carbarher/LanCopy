using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SlskDown.Models;

namespace SlskDown.Core.AI
{
    /// <summary>
    /// Motor de AutoLearning para análisis predictivo de patrones de usuario
    /// </summary>
    public class AutoLearningEngine
    {
        private readonly Dictionary<string, UserPattern> _userPatterns;
        private readonly List<SearchHistory> _searchHistory;
        private readonly List<LearningHistoryEntry> _downloadHistory;
        private readonly object _lockObject = new object();

        public AutoLearningEngine()
        {
            _userPatterns = new Dictionary<string, UserPattern>();
            _searchHistory = new List<SearchHistory>();
            _downloadHistory = new List<LearningHistoryEntry>();
        }

        /// <summary>
        /// Registra una búsqueda para aprendizaje
        /// </summary>
        public void RecordSearch(string query, List<AutoSearchFileResult> results, DateTime timestamp)
        {
            lock (_lockObject)
            {
                var searchEntry = new SearchHistory
                {
                    Query = query,
                    Results = results,
                    Timestamp = timestamp,
                    SelectedResults = new List<string>()
                };

                _searchHistory.Add(searchEntry);
                AnalyzeSearchPattern(searchEntry);
            }
        }

        /// <summary>
        /// Registra una descarga para aprendizaje
        /// </summary>
        public void RecordDownload(string filename, string author, string query, DateTime timestamp)
        {
            lock (_lockObject)
        {
            var downloadEntry = new LearningHistoryEntry
            {
                Filename = filename,
                Author = author,
                SourceQuery = query,
                Timestamp = timestamp
            };

            _downloadHistory.Add(downloadEntry);
            
            // Actualizar patrón de búsqueda relacionado
            var relatedSearch = _searchHistory.LastOrDefault(s => s.Query == query);
            if (relatedSearch != null)
            {
                relatedSearch.SelectedResults.Add(filename);
            }

            AnalyzeDownloadPattern(downloadEntry);
        }
        }

        /// <summary>
        /// Predice contenido basado en historial del usuario
        /// </summary>
        public async Task<List<PredictedContent>> PredictContent(string currentQuery, int maxPredictions = 10)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    var predictions = new List<PredictedContent>();
                    
                    // Analizar patrones de búsqueda similares
                    var similarQueries = FindSimilarQueries(currentQuery);
                    var frequentAuthors = GetFrequentAuthors(similarQueries);
                    var frequentTerms = GetFrequentTerms(similarQueries);

                    // Generar predicciones basadas en autores frecuentes
                    foreach (var author in frequentAuthors.Take(5))
                    {
                        predictions.Add(new PredictedContent
                        {
                            Type = PredictionType.Author,
                            Value = author.Key,
                            Confidence = author.Value,
                            Reason = $"Autor frecuente en búsquedas similares ({author.Value:F1}% confianza)"
                        });
                    }

                    // Generar predicciones basadas en términos frecuentes
                    foreach (var term in frequentTerms.Take(5))
                    {
                        predictions.Add(new PredictedContent
                        {
                            Type = PredictionType.Term,
                            Value = term.Key,
                            Confidence = term.Value * 0.8f, // Menor confianza para términos
                            Reason = $"Término común en búsquedas similares ({term.Value:F1}% confianza)"
                        });
                    }

                    return predictions.OrderByDescending(p => p.Confidence).Take(maxPredictions).ToList();
                }
            });
        }

        /// <summary>
        /// Obtiene sugerencias de búsqueda en tiempo real
        /// </summary>
        public List<string> GetSearchSuggestions(string partialQuery)
        {
            lock (_lockObject)
            {
                var suggestions = new HashSet<string>();
                
                // Sugerencias basadas en historial
                var historicalSuggestions = _searchHistory
                    .Where(s => s.Query.Contains(partialQuery, StringComparison.OrdinalIgnoreCase))
                    .Select(s => s.Query)
                    .Take(5);

                foreach (var suggestion in historicalSuggestions)
                {
                    suggestions.Add(suggestion);
                }

                // Sugerencias basadas en patrones
                var patternSuggestions = GeneratePatternSuggestions(partialQuery);
                foreach (var suggestion in patternSuggestions)
                {
                    suggestions.Add(suggestion);
                }

                return suggestions.Take(10).ToList();
            }
        }

        /// <summary>
        /// Analiza patrones de búsqueda
        /// </summary>
        private void AnalyzeSearchPattern(SearchHistory searchEntry)
        {
            // Extraer términos clave
            var terms = ExtractTerms(searchEntry.Query);
            
            // Actualizar frecuencia de términos
            foreach (var term in terms)
            {
                if (!_userPatterns.ContainsKey("terms"))
                {
                    _userPatterns["terms"] = new UserPattern { Type = PatternType.Terms };
                }
                
                _userPatterns["terms"].TermFrequency[term] = 
                    _userPatterns["terms"].TermFrequency.GetValueOrDefault(term, 0) + 1;
            }
        }

        /// <summary>
        /// Analiza patrones de descarga
        /// </summary>
        private void AnalyzeDownloadPattern(LearningHistoryEntry downloadEntry)
        {
            // Actualizar frecuencia de autores
            if (!_userPatterns.ContainsKey("authors"))
            {
                _userPatterns["authors"] = new UserPattern { Type = PatternType.Authors };
            }
            
            _userPatterns["authors"].AuthorFrequency[downloadEntry.Author] = 
                _userPatterns["authors"].AuthorFrequency.GetValueOrDefault(downloadEntry.Author, 0) + 1;

            // Actualizar patrones de tiempo
            AnalyzeTimePatterns(downloadEntry.Timestamp);
        }

        /// <summary>
        /// Encuentra consultas similares basadas en términos
        /// </summary>
        private List<SearchHistory> FindSimilarQueries(string query)
        {
            var queryTerms = ExtractTerms(query);
            
            return _searchHistory
                .Where(s => queryTerms.Any(term => 
                    s.Query.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(s => CalculateSimilarity(query, s.Query))
                .Take(20)
                .ToList();
        }

        /// <summary>
        /// Obtiene autores frecuentes en búsquedas similares
        /// </summary>
        private Dictionary<string, float> GetFrequentAuthors(List<SearchHistory> searches)
        {
            var authorCounts = new Dictionary<string, int>();
            
            foreach (var search in searches)
            {
                foreach (var result in search.Results)
                {
                    authorCounts[result.Author] = authorCounts.GetValueOrDefault(result.Author, 0) + 1;
                }
            }

            var total = authorCounts.Values.Sum();
            return authorCounts
                .ToDictionary(kvp => kvp.Key, kvp => total > 0 ? (float)kvp.Value / total : 0f)
                .OrderByDescending(kvp => kvp.Value)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Obtiene términos frecuentes en búsquedas similares
        /// </summary>
        private Dictionary<string, float> GetFrequentTerms(List<SearchHistory> searches)
        {
            var termCounts = new Dictionary<string, int>();
            
            foreach (var search in searches)
            {
                var terms = ExtractTerms(search.Query);
                foreach (var term in terms)
                {
                    termCounts[term] = termCounts.GetValueOrDefault(term, 0) + 1;
                }
            }

            var total = termCounts.Values.Sum();
            return termCounts
                .ToDictionary(kvp => kvp.Key, kvp => total > 0 ? (float)kvp.Value / total : 0f)
                .OrderByDescending(kvp => kvp.Value)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Extrae términos clave de una consulta
        /// </summary>
        private List<string> ExtractTerms(string query)
        {
            // Separar por espacios y eliminar palabras comunes
            var stopWords = new HashSet<string> { "el", "la", "los", "las", "de", "del", "en", "con", "por", "para", "the", "a", "an", "and", "or", "in", "on", "at", "to", "for" };
            
            return query.Split(new[] { ' ', '-', '_', '.', ',', ';', ':' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(term => term.Length > 2 && !stopWords.Contains(term.ToLower()))
                .Select(term => term.ToLower())
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Calcula similitud entre dos cadenas
        /// </summary>
        private float CalculateSimilarity(string str1, string str2)
        {
            var terms1 = ExtractTerms(str1);
            var terms2 = ExtractTerms(str2);
            
            var intersection = terms1.Intersect(terms2).Count();
            var union = terms1.Union(terms2).Count();
            
            return union > 0 ? (float)intersection / union : 0f;
        }

        /// <summary>
        /// Genera sugerencias basadas en patrones
        /// </summary>
        private List<string> GeneratePatternSuggestions(string partialQuery)
        {
            var suggestions = new List<string>();
            
            // Completar con términos frecuentes
            if (_userPatterns.ContainsKey("terms"))
            {
                var frequentTerms = _userPatterns["terms"].TermFrequency
                    .Where(kvp => kvp.Key.StartsWith(partialQuery, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(3)
                    .Select(kvp => kvp.Key);
                
                suggestions.AddRange(frequentTerms);
            }

            return suggestions;
        }

        /// <summary>
        /// Analiza patrones de tiempo de uso
        /// </summary>
        private void AnalyzeTimePatterns(DateTime timestamp)
        {
            if (!_userPatterns.ContainsKey("time"))
            {
                _userPatterns["time"] = new UserPattern { Type = PatternType.Time };
            }

            var hour = timestamp.Hour;
            _userPatterns["time"].TimePatterns[hour] = 
                _userPatterns["time"].TimePatterns.GetValueOrDefault(hour, 0) + 1;
        }

        /// <summary>
        /// Guarda los patrones aprendidos
        /// </summary>
        public void SavePatterns(string filePath)
        {
            lock (_lockObject)
            {
                var data = new LearningData
                {
                    UserPatterns = _userPatterns,
                    SearchHistory = _searchHistory.TakeLast(1000).ToList(), // Limitar historial
                    LearningHistoryEntry = _downloadHistory.TakeLast(1000).ToList()
                };

                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                System.IO.File.WriteAllText(filePath, json);
            }
        }

        /// <summary>
        /// Carga patrones aprendidos
        /// </summary>
        public void LoadPatterns(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
                return;

            lock (_lockObject)
            {
                try
                {
                    var json = System.IO.File.ReadAllText(filePath);
                    var data = JsonConvert.DeserializeObject<LearningData>(json);
                    
                    if (data != null)
                    {
                        _userPatterns.Clear();
                        foreach (var kvp in data.UserPatterns)
                        {
                            _userPatterns[kvp.Key] = kvp.Value;
                        }

                        _searchHistory.Clear();
                        _searchHistory.AddRange(data.SearchHistory);

                        _downloadHistory.Clear();
                        _downloadHistory.AddRange(data.LearningHistoryEntry);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue
                    System.Diagnostics.Debug.WriteLine($"Error loading patterns: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Patrones de usuario aprendidos
    /// </summary>
    public class UserPattern
    {
        public PatternType Type { get; set; }
        public Dictionary<string, int> TermFrequency { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> AuthorFrequency { get; set; } = new Dictionary<string, int>();
        public Dictionary<int, int> TimePatterns { get; set; } = new Dictionary<int, int>();
    }

    /// <summary>
    /// Entrada de historial de búsqueda
    /// </summary>
    public class SearchHistory
    {
        public string Query { get; set; }
        public List<AutoSearchFileResult> Results { get; set; } = new List<AutoSearchFileResult>();
        public DateTime Timestamp { get; set; }
        public List<string> SelectedResults { get; set; } = new List<string>();
    }

    /// <summary>
    /// Entrada de historial de descarga para aprendizaje
    /// </summary>
    public class LearningHistoryEntry
    {
        public string Filename { get; set; }
        public string Author { get; set; }
        public string SourceQuery { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Contenido predicho por el motor de IA
    /// </summary>
    public class PredictedContent
    {
        public PredictionType Type { get; set; }
        public string Value { get; set; }
        public float Confidence { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>
    /// Datos de aprendizaje serializables
    /// </summary>
    public class LearningData
    {
        public Dictionary<string, UserPattern> UserPatterns { get; set; } = new Dictionary<string, UserPattern>();
        public List<SearchHistory> SearchHistory { get; set; } = new List<SearchHistory>();
        public List<LearningHistoryEntry> LearningHistoryEntry { get; set; } = new List<LearningHistoryEntry>();
    }

    /// <summary>
    /// Tipos de patrones
    /// </summary>
    public enum PatternType
    {
        Terms,
        Authors,
        Time,
        Genres
    }

    /// <summary>
    /// Tipos de predicción
    /// </summary>
    public enum PredictionType
    {
        Author,
        Term,
        Genre,
        Quality
    }
}
