using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Soulseek;

namespace SlskDown.Core
{
    /// <summary>
    /// Búsquedas correlacionadas - encuentra usuarios con gustos similares
    /// </summary>
    public class CorrelatedSearch
    {
        private readonly SoulseekClient client;
        private readonly Dictionary<string, UserProfile> profileCache;
        private readonly object cacheLock = new object();
        private const int MaxCacheSize = 1000;
        
        public CorrelatedSearch(SoulseekClient soulseekClient)
        {
            client = soulseekClient;
            profileCache = new Dictionary<string, UserProfile>();
        }
        
        /// <summary>
        /// Perfil de usuario con sus archivos compartidos
        /// </summary>
        public class UserProfile
        {
            public string Username { get; set; }
            public HashSet<string> FileNames { get; set; } = new HashSet<string>();
            public HashSet<string> Artists { get; set; } = new HashSet<string>();
            public HashSet<string> Albums { get; set; } = new HashSet<string>();
            public int TotalFiles { get; set; }
            public DateTime LastUpdated { get; set; }
        }
        
        /// <summary>
        /// Resultado de similitud entre usuarios
        /// </summary>
        public class SimilarityResult
        {
            public string Username { get; set; }
            public double SimilarityScore { get; set; } // 0.0 - 1.0
            public int CommonFiles { get; set; }
            public List<string> CommonArtists { get; set; } = new List<string>();
            public List<string> CommonAlbums { get; set; } = new List<string>();
        }
        
        /// <summary>
        /// Obtiene el perfil de un usuario (con caché)
        /// </summary>
        public async Task<UserProfile> GetUserProfile(string username)
        {
            lock (cacheLock)
            {
                if (profileCache.ContainsKey(username))
                {
                    var cached = profileCache[username];
                    if (DateTime.Now - cached.LastUpdated < TimeSpan.FromHours(24))
                        return cached;
                }
            }
            
            try
            {
                var browse = await client.BrowseAsync(username);
                var profile = new UserProfile
                {
                    Username = username,
                    LastUpdated = DateTime.Now
                };
                
                foreach (var dir in browse.Directories)
                {
                    foreach (var file in dir.Files)
                    {
                        profile.FileNames.Add(file.Filename.ToLower());
                        profile.TotalFiles++;
                        
                        // Extraer artista y álbum del path
                        var parts = dir.Name.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            profile.Artists.Add(parts[0].ToLower());
                            if (parts.Length >= 3)
                                profile.Albums.Add(parts[1].ToLower());
                        }
                    }
                }
                
                lock (cacheLock)
                {
                    // Limitar tamaño del caché
                    if (profileCache.Count >= MaxCacheSize && !profileCache.ContainsKey(username))
                    {
                        var oldest = profileCache.OrderBy(kvp => kvp.Value.LastUpdated).First().Key;
                        profileCache.Remove(oldest);
                    }
                    profileCache[username] = profile;
                }
                
                return profile;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Calcula similitud entre dos usuarios
        /// </summary>
        public SimilarityResult CalculateSimilarity(UserProfile user1, UserProfile user2)
        {
            if (user1 == null || user2 == null)
                return null;
            
            // Similitud basada en artistas comunes (más peso)
            var commonArtists = user1.Artists.Intersect(user2.Artists).ToList();
            var artistScore = commonArtists.Count / (double)Math.Max(user1.Artists.Count, 1);
            
            // Similitud basada en álbumes comunes
            var commonAlbums = user1.Albums.Intersect(user2.Albums).ToList();
            var albumScore = commonAlbums.Count / (double)Math.Max(user1.Albums.Count, 1);
            
            // Similitud basada en archivos comunes
            var commonFiles = user1.FileNames.Intersect(user2.FileNames).Count();
            var fileScore = commonFiles / (double)Math.Max(user1.FileNames.Count, 1);
            
            // Score ponderado (artistas 50%, álbumes 30%, archivos 20%)
            var totalScore = (artistScore * 0.5) + (albumScore * 0.3) + (fileScore * 0.2);
            
            return new SimilarityResult
            {
                Username = user2.Username,
                SimilarityScore = totalScore,
                CommonFiles = commonFiles,
                CommonArtists = commonArtists,
                CommonAlbums = commonAlbums
            };
        }
        
        /// <summary>
        /// Encuentra usuarios similares a un usuario base
        /// </summary>
        public async Task<List<SimilarityResult>> FindSimilarUsers(
            string baseUsername, 
            List<string> candidateUsers,
            double minSimilarity = 0.3,
            int maxResults = 10)
        {
            var baseProfile = await GetUserProfile(baseUsername);
            if (baseProfile == null)
                return new List<SimilarityResult>();
            
            var results = new List<SimilarityResult>();
            
            foreach (var candidate in candidateUsers)
            {
                if (candidate.Equals(baseUsername, StringComparison.OrdinalIgnoreCase))
                    continue;
                
                var candidateProfile = await GetUserProfile(candidate);
                if (candidateProfile == null)
                    continue;
                
                var similarity = CalculateSimilarity(baseProfile, candidateProfile);
                if (similarity != null && similarity.SimilarityScore >= minSimilarity)
                {
                    results.Add(similarity);
                }
            }
            
            return results
                .OrderByDescending(r => r.SimilarityScore)
                .Take(maxResults)
                .ToList();
        }
        
        /// <summary>
        /// Busca en usuarios similares
        /// </summary>
        public async Task<List<Soulseek.File>> SearchInSimilarUsers(
            string query,
            string baseUsername,
            List<string> candidateUsers,
            double minSimilarity = 0.3)
        {
            var similarUsers = await FindSimilarUsers(baseUsername, candidateUsers, minSimilarity);
            var results = new List<Soulseek.File>();
            
            foreach (var similar in similarUsers)
            {
                try
                {
                    var searchResponse = await client.SearchAsync(
                        SearchQuery.FromText(query),
                        options: new SearchOptions(
                            filterResponses: false,
                            responseLimit: 100
                        )
                    );
                    
                    // Filtrar solo resultados del usuario similar
                    var userResults = searchResponse.Responses
                        .Where(r => r.Username.Equals(similar.Username, StringComparison.OrdinalIgnoreCase))
                        .SelectMany(r => r.Files);
                    
                    results.AddRange(userResults);
                }
                catch
                {
                    // Continuar con el siguiente usuario
                }
            }
            
            return results;
        }
        
        /// <summary>
        /// Limpia perfiles antiguos del caché
        /// </summary>
        public void CleanupCache(TimeSpan maxAge)
        {
            lock (cacheLock)
            {
                var cutoff = DateTime.Now - maxAge;
                var toRemove = profileCache
                    .Where(kvp => kvp.Value.LastUpdated < cutoff)
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var key in toRemove)
                {
                    profileCache.Remove(key);
                }
            }
        }
        
        /// <summary>
        /// Obtiene estadísticas del caché
        /// </summary>
        public object GetCacheStats()
        {
            lock (cacheLock)
            {
                return new
                {
                    cachedProfiles = profileCache.Count,
                    maxSize = MaxCacheSize,
                    usagePercent = (profileCache.Count / (double)MaxCacheSize) * 100
                };
            }
        }
    }
}
