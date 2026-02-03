using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Soulseek;

namespace SlskDown.Services
{
    /// <summary>
    /// Servicio de recomendaciones basado en el protocolo Soulseek
    /// Server Code 54: Recommendations
    /// Server Code 50: SimilarRecommendations
    /// </summary>
    public class RecommendationService
    {
        private readonly ISoulseekClient client;
        private readonly List<Recommendation> cachedRecommendations = new();
        private DateTime lastRecommendationsFetch = DateTime.MinValue;
        private readonly TimeSpan cacheExpiration = TimeSpan.FromHours(1);

        public Action<string> OnLog { get; set; }

        public class Recommendation
        {
            public string Item { get; set; }
            public int Score { get; set; }
            public RecommendationType Type { get; set; }
            public DateTime FetchedAt { get; set; }

            public string DisplayText => $"{Item} (score: {Score})";
        }

        public enum RecommendationType
        {
            Global,         // Recomendaciones globales del servidor
            Similar,        // Artistas/items similares
            UserBased       // Basado en descargas del usuario
        }

        public RecommendationService(ISoulseekClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>
        /// Obtiene recomendaciones globales del servidor
        /// Basadas en las descargas del usuario
        /// </summary>
        public async Task<List<Recommendation>> GetRecommendationsAsync(bool forceRefresh = false)
        {
            try
            {
                // Usar caché si es reciente
                if (!forceRefresh && 
                    cachedRecommendations.Count > 0 && 
                    (DateTime.Now - lastRecommendationsFetch) < cacheExpiration)
                {
                    Log($"📋 Usando recomendaciones en caché ({cachedRecommendations.Count} items)");
                    return cachedRecommendations.ToList();
                }

                Log("Obteniendo recomendaciones del servidor...");

                // Nota: Verificar si Soulseek.NET tiene método GetRecommendations
                // Si no existe, este es un placeholder para implementación futura
                
                // Por ahora, retornar lista vacía con mensaje
                Log("API de recomendaciones no disponible en Soulseek.NET actual");
                Log("Implementación futura: Server Code 54 (Recommendations)");
                
                return new List<Recommendation>();

                // Implementación futura cuando esté disponible:
                /*
                var serverRecommendations = await client.GetRecommendationsAsync();
                
                cachedRecommendations.Clear();
                foreach (var (item, score) in serverRecommendations)
                {
                    cachedRecommendations.Add(new Recommendation
                    {
                        Item = item,
                        Score = score,
                        Type = RecommendationType.Global,
                        FetchedAt = DateTime.Now
                    });
                }

                lastRecommendationsFetch = DateTime.Now;
                Log($"Obtenidas {cachedRecommendations.Count} recomendaciones");
                
                return cachedRecommendations.ToList();
                */
            }
            catch (Exception ex)
            {
                Log($"Error obteniendo recomendaciones: {ex.Message}");
                return new List<Recommendation>();
            }
        }

        /// <summary>
        /// Obtiene items similares a uno dado (ej: artistas similares)
        /// </summary>
        public async Task<List<Recommendation>> GetSimilarItemsAsync(string item)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(item))
                    return new List<Recommendation>();

                Log($"Buscando items similares a: {item}");

                // Nota: Verificar si Soulseek.NET tiene método GetSimilarRecommendations
                // Si no existe, este es un placeholder para implementación futura
                
                Log("API de similares no disponible en Soulseek.NET actual");
                Log("Implementación futura: Server Code 50 (SimilarRecommendations)");
                
                return new List<Recommendation>();

                // Implementación futura cuando esté disponible:
                /*
                var similarItems = await client.GetSimilarRecommendationsAsync(item);
                
                var recommendations = new List<Recommendation>();
                foreach (var (similarItem, score) in similarItems)
                {
                    recommendations.Add(new Recommendation
                    {
                        Item = similarItem,
                        Score = score,
                        Type = RecommendationType.Similar,
                        FetchedAt = DateTime.Now
                    });
                }

                Log($"Encontrados {recommendations.Count} items similares");
                return recommendations;
                */
            }
            catch (Exception ex)
            {
                Log($"Error obteniendo items similares: {ex.Message}");
                return new List<Recommendation>();
            }
        }

        /// <summary>
        /// Genera recomendaciones locales basadas en historial de descargas
        /// (Alternativa mientras no esté disponible la API del servidor)
        /// </summary>
        public List<Recommendation> GetLocalRecommendations(List<string> downloadedArtists, int maxResults = 10)
        {
            try
            {
                if (downloadedArtists == null || downloadedArtists.Count == 0)
                    return new List<Recommendation>();

                Log($"Generando recomendaciones locales desde {downloadedArtists.Count} artistas...");

                // Contar frecuencia de artistas
                var artistFrequency = downloadedArtists
                    .GroupBy(a => a, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new Recommendation
                    {
                        Item = g.Key,
                        Score = g.Count(),
                        Type = RecommendationType.UserBased,
                        FetchedAt = DateTime.Now
                    })
                    .OrderByDescending(r => r.Score)
                    .Take(maxResults)
                    .ToList();

                Log($"Generadas {artistFrequency.Count} recomendaciones locales");
                return artistFrequency;
            }
            catch (Exception ex)
            {
                Log($"Error generando recomendaciones locales: {ex.Message}");
                return new List<Recommendation>();
            }
        }

        /// <summary>
        /// Limpia la caché de recomendaciones
        /// </summary>
        public void ClearCache()
        {
            cachedRecommendations.Clear();
            lastRecommendationsFetch = DateTime.MinValue;
            Log("Caché de recomendaciones limpiada");
        }

        /// <summary>
        /// Obtiene estadísticas del servicio
        /// </summary>
        public (int cached, DateTime lastFetch, bool isStale) GetStats()
        {
            var isStale = (DateTime.Now - lastRecommendationsFetch) > cacheExpiration;
            return (cachedRecommendations.Count, lastRecommendationsFetch, isStale);
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
    }

    /// <summary>
    /// Helper para agregar "Me gusta" e "Intereses" al perfil
    /// (Usado por el servidor para generar recomendaciones)
    /// </summary>
    public class UserInterestsManager
    {
        private readonly ISoulseekClient client;
        private readonly HashSet<string> likes = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> hates = new(StringComparer.OrdinalIgnoreCase);

        public Action<string> OnLog { get; set; }

        public UserInterestsManager(ISoulseekClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>
        /// Agrega un item a "Me gusta"
        /// Server Code 51: AddThingILike
        /// </summary>
        public async Task<bool> AddLikeAsync(string item)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(item) || likes.Contains(item))
                    return false;

                // Nota: Verificar si Soulseek.NET tiene método AddThingILike
                // await client.AddThingILikeAsync(item);
                
                likes.Add(item);
                OnLog?.Invoke($"👍 Agregado a Me gusta: {item}");
                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Error agregando Me gusta: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Agrega un item a "No me gusta"
        /// Server Code 117: AddThingIHate
        /// </summary>
        public async Task<bool> AddHateAsync(string item)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(item) || hates.Contains(item))
                    return false;

                // Nota: Verificar si Soulseek.NET tiene método AddThingIHate
                // await client.AddThingIHateAsync(item);
                
                hates.Add(item);
                OnLog?.Invoke($"👎 Agregado a No me gusta: {item}");
                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Error agregando No me gusta: {ex.Message}");
                return false;
            }
        }

        public IReadOnlySet<string> GetLikes() => likes;
        public IReadOnlySet<string> GetHates() => hates;
    }
}
