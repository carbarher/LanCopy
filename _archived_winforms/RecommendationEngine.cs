using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SlskDown
{
    /// <summary>
    /// Motor de recomendaciones inteligentes para SlskDown
    /// </summary>
    public class RecommendationEngine
    {
        private readonly Dictionary<string, UserPreferences> userProfiles = new();
        private readonly Dictionary<string, List<string>> similarArtists = new();
        private readonly List<SearchHistory> searchHistory = new();
        
        public struct UserPreferences
        {
            public string[] FavoriteGenres { get; set; }
            public string[] FavoriteArtists { get; set; }
            public int PreferredBitrate { get; set; }
            public string[] PreferredCountries { get; set; }
            public Dictionary<string, int> ArtistFrequency { get; set; }
        }
        
        public struct SearchHistory
        {
            public string Query { get; set; }
            public DateTime Timestamp { get; set; }
            public int ResultsCount { get; set; }
            public bool Downloaded { get; set; }
        }
        
        public RecommendationEngine()
        {
            LoadSimilarArtists();
            LoadUserHistory();
        }
        
        /// <summary>
        /// Obtener recomendaciones basadas en bÃºsqueda actual
        /// </summary>
        public List<string> GetRecommendations(string currentArtist, int maxRecommendations = 5)
        {
            var recommendations = new List<string>();
            
            try
            {
                // 1. Artistas similares
                if (similarArtists.ContainsKey(currentArtist.ToLower()))
                {
                    recommendations.AddRange(similarArtists[currentArtist.ToLower()]);
                }
                
                // 2. Basado en historial del usuario
                var historyBased = GetHistoryBasedRecommendations(currentArtist);
                recommendations.AddRange(historyBased);
                
                // 3. Tendencias (artistas populares recientes)
                var trending = GetTrendingArtists();
                recommendations.AddRange(trending);
                
                // Eliminar duplicados y limitar
                recommendations = recommendations.Distinct().Take(maxRecommendations).ToList();
                
                Console.WriteLine($"[Recommendations] ðŸŽ¯ Generadas {recommendations.Count} recomendaciones para: {currentArtist}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Recommendations] âŒ Error generando recomendaciones: {ex.Message}");
            }
            
            return recommendations;
        }
        
        /// <summary>
        /// Obtener sugerencias de bitrate Ã³ptimo
        /// </summary>
        public int GetOptimalBitrate(string artist, string filename)
        {
            try
            {
                // Basado en preferencias del usuario y calidad del archivo
                var userPref = GetUserPreferences();
                
                // Si es FLAC, recomendar mÃ¡xima calidad
                if (filename.Contains("flac", StringComparison.OrdinalIgnoreCase))
                {
                    return userPref.PreferredBitrate >= 320 ? 320 : userPref.PreferredBitrate;
                }
                
                // Para MP3, recomendar basado en historial
                return userPref.PreferredBitrate;
            }
            catch
            {
                return 320; // Default Ã³ptimo
            }
        }
        
        /// <summary>
        /// Sugerir mejores momentos para bÃºsqueda
        /// </summary>
        public List<TimeSlot> GetOptimalSearchTimes()
        {
            var optimalTimes = new List<TimeSlot>();
            
            // Basado en anÃ¡lisis de actividad de usuarios
            optimalTimes.Add(new TimeSlot { Hour = 14, Reason = "Mayor actividad en Europa", Score = 9 });
            optimalTimes.Add(new TimeSlot { Hour = 20, Reason = "Mayor actividad en AmÃ©rica", Score = 8 });
            optimalTimes.Add(new TimeSlot { Hour = 2, Reason = "Menor congestiÃ³n", Score = 7 });
            
            return optimalTimes;
        }
        
        /// <summary>
        /// Analizar patrones de bÃºsqueda y sugerir optimizaciones
        /// </summary>
        public SearchOptimization AnalyzeSearchPatterns()
        {
            var optimization = new SearchOptimization();
            
            try
            {
                // Analizar historial reciente
                var recentSearches = searchHistory
                    .Where(h => h.Timestamp > DateTime.Now.AddDays(-7))
                    .ToList();
                
                if (recentSearches.Count == 0)
                {
                    optimization.Message = "No hay historial suficiente para anÃ¡lisis";
                    return optimization;
                }
                
                // Calcular mÃ©tricas
                optimization.AverageResultsPerSearch = recentSearches.Average(h => h.ResultsCount);
                optimization.DownloadRate = (double)recentSearches.Count(h => h.Downloaded) / recentSearches.Count;
                optimization.MostActiveHour = recentSearches.GroupBy(h => h.Timestamp.Hour)
                    .OrderByDescending(g => g.Count())
                    .First().Key;
                
                // Generar recomendaciones
                if (optimization.DownloadRate < 0.3)
                {
                    optimization.Recommendations.Add("Considera usar filtros mÃ¡s especÃ­ficos");
                }
                
                if (optimization.AverageResultsPerSearch > 1000)
                {
                    optimization.Recommendations.Add("Demasiados resultados - aÃ±ade filtros de calidad");
                }
                
                optimization.Message = "AnÃ¡lisis completado exitosamente";
            }
            catch (Exception ex)
            {
                optimization.Message = $"Error en anÃ¡lisis: {ex.Message}";
            }
            
            return optimization;
        }
        
        private void LoadSimilarArtists()
        {
            // Cargar base de datos de artistas similares
            similarArtists["the beatles"] = new List<string> { "the rolling stones", "the kinks", "the who" };
            similarArtists["pink floyd"] = new List<string> { "king crimson", "yes", "genesis" };
            similarArtists["led zeppelin"] = new List<string> { "deep purple", "black sabbath", "the who" };
            // Agregar mÃ¡s...
        }
        
        private void LoadUserHistory()
        {
            try
            {
                if (File.Exists(@"c:\p2p\SlskDown\search_history.json"))
                {
                    var json = File.ReadAllText(@"c:\p2p\SlskDown\search_history.json");
                    var history = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);
                    
                    foreach (var item in history)
                    {
                        searchHistory.Add(new SearchHistory
                        {
                            Query = item["query"].ToString(),
                            Timestamp = DateTime.Parse(item["timestamp"].ToString()),
                            ResultsCount = int.Parse(item["results"].ToString()),
                            Downloaded = bool.Parse(item["downloaded"].ToString())
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Recommendations] âŒ Error cargando historial: {ex.Message}");
            }
        }
        
        private UserPreferences GetUserPreferences()
        {
            // Implementar carga real de preferencias
            return new UserPreferences
            {
                PreferredBitrate = 320,
                PreferredCountries = new[] { "US", "ES", "MX" },
                ArtistFrequency = new Dictionary<string, int>()
            };
        }
        
        private List<string> GetHistoryBasedRecommendations(string currentArtist)
        {
            // Basado en artistas buscados frecuentemente juntos
            return new List<string>();
        }
        
        private List<string> GetTrendingArtists()
        {
            // Artistas populares en la Ãºltima semana
            return new List<string> { "taylor swift", "bad bunny", "the weeknd" };
        }
    }
    
    public struct TimeSlot
    {
        public int Hour { get; set; }
        public string Reason { get; set; }
        public int Score { get; set; }
    }
    
    public struct SearchOptimization
    {
        public string Message { get; set; }
        public double AverageResultsPerSearch { get; set; }
        public double DownloadRate { get; set; }
        public int MostActiveHour { get; set; }
        public List<string> Recommendations { get; set; }
        
        public SearchOptimization()
        {
            Message = "";
            AverageResultsPerSearch = 0;
            DownloadRate = 0;
            MostActiveHour = 0;
            Recommendations = new List<string>();
        }
    }
}

