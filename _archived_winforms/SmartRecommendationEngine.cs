using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown
{
    /// <summary>
    /// Motor de recomendaciones inteligentes con algoritmos de ML
    /// Collaborative filtering + Content-based filtering + Trending analysis
    /// </summary>
    public class SmartRecommendationEngine
    {
        private readonly Dictionary<string, UserProfile> userProfiles;
        private readonly Dictionary<string, List<string>> similarAuthors;
        private readonly List<RecommendationHistoryEntry> downloadHistory;
        private readonly Dictionary<string, AuthorPopularity> trendingAuthors;
        
        // Configuración
        private const int MAX_RECOMMENDATIONS = 20;
        private const double SIMILARITY_THRESHOLD = 0.3;
        private const int TRENDING_WINDOW_DAYS = 7;
        
        public SmartRecommendationEngine()
        {
            userProfiles = new Dictionary<string, UserProfile>();
            similarAuthors = new Dictionary<string, List<string>>();
            downloadHistory = new List<RecommendationHistoryEntry>();
            trendingAuthors = new Dictionary<string, AuthorPopularity>();
            
            Console.WriteLine("[RecommendationEngine] Inicializado");
        }
        
        /// <summary>
        /// Registra una descarga para aprendizaje
        /// </summary>
        public void RecordDownload(string author, string filename, string genre = null)
        {
            var download = new RecommendationHistoryEntry
            {
                Author = author,
                Filename = filename,
                Genre = genre,
                Timestamp = DateTime.Now
            };
            
            downloadHistory.Add(download);
            
            // Actualizar perfil de usuario
            UpdateUserProfile(author, genre);
            
            // Actualizar trending
            UpdateTrending(author);
        }
        
        /// <summary>
        /// Actualiza el perfil del usuario
        /// </summary>
        private void UpdateUserProfile(string author, string genre)
        {
            const string userId = "default"; // Single user por ahora
            
            if (!userProfiles.ContainsKey(userId))
            {
                userProfiles[userId] = new UserProfile
                {
                    AuthorFrequency = new Dictionary<string, int>(),
                    GenreFrequency = new Dictionary<string, int>(),
                    TotalDownloads = 0
                };
            }
            
            var profile = userProfiles[userId];
            
            // Incrementar frecuencia de autor
            if (profile.AuthorFrequency.ContainsKey(author))
                profile.AuthorFrequency[author]++;
            else
                profile.AuthorFrequency[author] = 1;
            
            // Incrementar frecuencia de género
            if (!string.IsNullOrEmpty(genre))
            {
                if (profile.GenreFrequency.ContainsKey(genre))
                    profile.GenreFrequency[genre]++;
                else
                    profile.GenreFrequency[genre] = 1;
            }
            
            profile.TotalDownloads++;
        }
        
        /// <summary>
        /// Actualiza autores trending
        /// </summary>
        private void UpdateTrending(string author)
        {
            if (!trendingAuthors.ContainsKey(author))
            {
                trendingAuthors[author] = new AuthorPopularity
                {
                    Author = author,
                    DownloadCount = 0,
                    FirstSeen = DateTime.Now
                };
            }
            
            trendingAuthors[author].DownloadCount++;
            trendingAuthors[author].LastSeen = DateTime.Now;
        }
        
        /// <summary>
        /// Obtiene recomendaciones personalizadas
        /// </summary>
        public List<Recommendation> GetRecommendations(int maxResults = MAX_RECOMMENDATIONS)
        {
            var recommendations = new List<Recommendation>();
            
            // 1. Collaborative Filtering (40%)
            var collaborative = GetCollaborativeRecommendations();
            recommendations.AddRange(collaborative.Take(maxResults / 2));
            
            // 2. Content-Based Filtering (30%)
            var contentBased = GetContentBasedRecommendations();
            recommendations.AddRange(contentBased.Take(maxResults / 3));
            
            // 3. Trending (30%)
            var trending = GetTrendingRecommendations();
            recommendations.AddRange(trending.Take(maxResults / 3));
            
            // Eliminar duplicados y ordenar por score
            return recommendations
                .GroupBy(r => r.Author)
                .Select(g => g.OrderByDescending(r => r.Score).First())
                .OrderByDescending(r => r.Score)
                .Take(maxResults)
                .ToList();
        }
        
        /// <summary>
        /// Collaborative Filtering: "Usuarios que descargaron X también descargaron Y"
        /// </summary>
        private List<Recommendation> GetCollaborativeRecommendations()
        {
            var recommendations = new List<Recommendation>();
            const string userId = "default";
            
            if (!userProfiles.ContainsKey(userId))
                return recommendations;
            
            var profile = userProfiles[userId];
            var userAuthors = profile.AuthorFrequency.Keys.ToHashSet();
            
            // Encontrar autores similares basados en co-ocurrencia
            var coOccurrence = new Dictionary<string, int>();
            
            foreach (var author in userAuthors)
            {
                if (similarAuthors.ContainsKey(author))
                {
                    foreach (var similar in similarAuthors[author])
                    {
                        if (!userAuthors.Contains(similar))
                        {
                            if (coOccurrence.ContainsKey(similar))
                                coOccurrence[similar]++;
                            else
                                coOccurrence[similar] = 1;
                        }
                    }
                }
            }
            
            // Convertir a recomendaciones
            foreach (var kvp in coOccurrence.OrderByDescending(x => x.Value))
            {
                recommendations.Add(new Recommendation
                {
                    Author = kvp.Key,
                    Score = kvp.Value * 10.0, // Normalizar
                    Reason = $"Usuarios que descargaron {GetTopAuthor(profile)} también descargaron este autor",
                    Type = RecommendationType.Collaborative
                });
            }
            
            return recommendations;
        }
        
        /// <summary>
        /// Content-Based Filtering: Basado en géneros y patrones de descarga
        /// </summary>
        private List<Recommendation> GetContentBasedRecommendations()
        {
            var recommendations = new List<Recommendation>();
            const string userId = "default";
            
            if (!userProfiles.ContainsKey(userId))
                return recommendations;
            
            var profile = userProfiles[userId];
            
            // Obtener géneros favoritos
            var topGenres = profile.GenreFrequency
                .OrderByDescending(kvp => kvp.Value)
                .Take(3)
                .Select(kvp => kvp.Key)
                .ToList();
            
            if (!topGenres.Any())
                return recommendations;
            
            // Buscar autores del mismo género que no hemos descargado
            var userAuthors = profile.AuthorFrequency.Keys.ToHashSet();
            var genreAuthors = downloadHistory
                .Where(d => topGenres.Contains(d.Genre) && !userAuthors.Contains(d.Author))
                .GroupBy(d => d.Author)
                .Select(g => new { Author = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count);
            
            foreach (var item in genreAuthors)
            {
                var topGenre = topGenres.First();
                var percentage = (double)profile.GenreFrequency[topGenre] / profile.TotalDownloads * 100;
                
                recommendations.Add(new Recommendation
                {
                    Author = item.Author,
                    Score = item.Count * 8.0,
                    Reason = $"Basado en tu interés en {topGenre} ({percentage:F0}% de tus descargas)",
                    Type = RecommendationType.ContentBased
                });
            }
            
            return recommendations;
        }
        
        /// <summary>
        /// Trending: Autores más populares recientemente
        /// </summary>
        private List<Recommendation> GetTrendingRecommendations()
        {
            var recommendations = new List<Recommendation>();
            var cutoffDate = DateTime.Now.AddDays(-TRENDING_WINDOW_DAYS);
            
            var trending = trendingAuthors.Values
                .Where(a => a.LastSeen >= cutoffDate)
                .OrderByDescending(a => a.DownloadCount)
                .Take(10);
            
            foreach (var author in trending)
            {
                var growth = CalculateGrowth(author);
                
                recommendations.Add(new Recommendation
                {
                    Author = author.Author,
                    Score = author.DownloadCount * 5.0 + growth,
                    Reason = $"Trending ahora (+{growth:F0}% esta semana)",
                    Type = RecommendationType.Trending
                });
            }
            
            return recommendations;
        }
        
        /// <summary>
        /// Calcula el crecimiento de popularidad
        /// </summary>
        private double CalculateGrowth(AuthorPopularity author)
        {
            var recentDownloads = downloadHistory
                .Count(d => d.Author == author.Author && 
                           d.Timestamp >= DateTime.Now.AddDays(-TRENDING_WINDOW_DAYS));
            
            var olderDownloads = downloadHistory
                .Count(d => d.Author == author.Author && 
                           d.Timestamp < DateTime.Now.AddDays(-TRENDING_WINDOW_DAYS) &&
                           d.Timestamp >= DateTime.Now.AddDays(-TRENDING_WINDOW_DAYS * 2));
            
            if (olderDownloads == 0)
                return recentDownloads * 100.0;
            
            return ((double)recentDownloads / olderDownloads - 1.0) * 100.0;
        }
        
        /// <summary>
        /// Registra similitud entre autores (para collaborative filtering)
        /// </summary>
        public void RegisterSimilarity(string author1, string author2)
        {
            if (!similarAuthors.ContainsKey(author1))
                similarAuthors[author1] = new List<string>();
            
            if (!similarAuthors[author1].Contains(author2))
                similarAuthors[author1].Add(author2);
            
            // Bidireccional
            if (!similarAuthors.ContainsKey(author2))
                similarAuthors[author2] = new List<string>();
            
            if (!similarAuthors[author2].Contains(author1))
                similarAuthors[author2].Add(author1);
        }
        
        /// <summary>
        /// Obtiene el autor más descargado
        /// </summary>
        private string GetTopAuthor(UserProfile profile)
        {
            return profile.AuthorFrequency
                .OrderByDescending(kvp => kvp.Value)
                .FirstOrDefault()
                .Key ?? "tus autores favoritos";
        }
        
        /// <summary>
        /// Obtiene estadísticas del motor
        /// </summary>
        public RecommendationStats GetStats()
        {
            const string userId = "default";
            var profile = userProfiles.ContainsKey(userId) ? userProfiles[userId] : null;
            
            return new RecommendationStats
            {
                TotalDownloads = downloadHistory.Count,
                UniqueAuthors = downloadHistory.Select(d => d.Author).Distinct().Count(),
                TopAuthors = profile?.AuthorFrequency
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(5)
                    .Select(kvp => new AuthorCount { Author = kvp.Key, Count = kvp.Value })
                    .ToList() ?? new List<AuthorCount>(),
                TopGenres = profile?.GenreFrequency
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(5)
                    .Select(kvp => new GenreCount { Genre = kvp.Key, Count = kvp.Value })
                    .ToList() ?? new List<GenreCount>(),
                TrendingCount = trendingAuthors.Count
            };
        }
    }
    
    /// <summary>
    /// Perfil de usuario
    /// </summary>
    public class UserProfile
    {
        public Dictionary<string, int> AuthorFrequency { get; set; }
        public Dictionary<string, int> GenreFrequency { get; set; }
        public int TotalDownloads { get; set; }
    }
    
    /// <summary>
    /// Historial de descarga para recomendaciones
    /// </summary>
    public class RecommendationHistoryEntry
    {
        public string Author { get; set; }
        public string Filename { get; set; }
        public string Genre { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    /// <summary>
    /// Popularidad de autor
    /// </summary>
    public class AuthorPopularity
    {
        public string Author { get; set; }
        public int DownloadCount { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
    }
    
    /// <summary>
    /// Recomendación
    /// </summary>
    public class Recommendation
    {
        public string Author { get; set; }
        public double Score { get; set; }
        public string Reason { get; set; }
        public RecommendationType Type { get; set; }
    }
    
    /// <summary>
    /// Tipo de recomendación
    /// </summary>
    public enum RecommendationType
    {
        Collaborative,   // Basado en otros usuarios
        ContentBased,    // Basado en contenido
        Trending         // Trending
    }
    
    /// <summary>
    /// Estadísticas de recomendaciones
    /// </summary>
    public class RecommendationStats
    {
        public int TotalDownloads { get; set; }
        public int UniqueAuthors { get; set; }
        public List<AuthorCount> TopAuthors { get; set; }
        public List<GenreCount> TopGenres { get; set; }
        public int TrendingCount { get; set; }
    }
    
    public class AuthorCount
    {
        public string Author { get; set; }
        public int Count { get; set; }
    }
    
    public class GenreCount
    {
        public string Genre { get; set; }
        public int Count { get; set; }
    }
}
