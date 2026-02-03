using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using SlskDown.Core.Optimization;

namespace SlskDown.Core
{
    /// <summary>
    /// Integración con Last.fm para recomendaciones musicales
    /// </summary>
    public class LastFmIntegration
    {
        private readonly string apiKey;
        private readonly HttpClient httpClient;
        private readonly Action<string> logFunc;
        private const string BaseUrl = "http://ws.audioscrobbler.com/2.0/";
        
        public LastFmIntegration(string lastFmApiKey = null, Action<string> log = null)
        {
            apiKey = lastFmApiKey ?? "demo_key"; // Usar demo key si no se proporciona
            httpClient = OptimizedHttpClient.Instance;
            logFunc = log;
        }
        
        /// <summary>
        /// Artista recomendado
        /// </summary>
        public class RecommendedArtist
        {
            public string Name { get; set; }
            public int Playcount { get; set; }
            public int Listeners { get; set; }
            public string Url { get; set; }
            public List<string> Tags { get; set; } = new List<string>();
        }
        
        /// <summary>
        /// Álbum recomendado
        /// </summary>
        public class RecommendedAlbum
        {
            public string Name { get; set; }
            public string Artist { get; set; }
            public int Playcount { get; set; }
            public string Url { get; set; }
        }
        
        /// <summary>
        /// Obtiene artistas similares
        /// </summary>
        public async Task<List<RecommendedArtist>> GetSimilarArtists(string artistName, int limit = 10)
        {
            try
            {
                var url = $"{BaseUrl}?method=artist.getsimilar&artist={Uri.EscapeDataString(artistName)}&api_key={apiKey}&format=json&limit={limit}";
                
                logFunc?.Invoke($"Consultando Last.fm: artistas similares a {artistName}");
                
                var response = await httpClient.GetStringAsync(url);
                var json = JsonDocument.Parse(response);
                
                var artists = new List<RecommendedArtist>();
                
                if (json.RootElement.TryGetProperty("similarartists", out var similar))
                {
                    if (similar.TryGetProperty("artist", out var artistArray))
                    {
                        foreach (var artist in artistArray.EnumerateArray())
                        {
                            artists.Add(new RecommendedArtist
                            {
                                Name = artist.GetProperty("name").GetString(),
                                Url = artist.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : ""
                            });
                        }
                    }
                }
                
                logFunc?.Invoke($"Encontrados {artists.Count} artistas similares");
                return artists;
            }
            catch (Exception ex)
            {
                logFunc?.Invoke($"Error consultando Last.fm: {ex.Message}");
                return new List<RecommendedArtist>();
            }
        }
        
        /// <summary>
        /// Obtiene top álbumes de un artista
        /// </summary>
        public async Task<List<RecommendedAlbum>> GetTopAlbums(string artistName, int limit = 10)
        {
            try
            {
                var url = $"{BaseUrl}?method=artist.gettopalbums&artist={Uri.EscapeDataString(artistName)}&api_key={apiKey}&format=json&limit={limit}";
                
                logFunc?.Invoke($"Consultando Last.fm: top álbumes de {artistName}");
                
                var response = await httpClient.GetStringAsync(url);
                var json = JsonDocument.Parse(response);
                
                var albums = new List<RecommendedAlbum>();
                
                if (json.RootElement.TryGetProperty("topalbums", out var top))
                {
                    if (top.TryGetProperty("album", out var albumArray))
                    {
                        foreach (var album in albumArray.EnumerateArray())
                        {
                            albums.Add(new RecommendedAlbum
                            {
                                Name = album.GetProperty("name").GetString(),
                                Artist = artistName,
                                Playcount = album.TryGetProperty("playcount", out var pc) ? 
                                    int.Parse(pc.GetString()) : 0,
                                Url = album.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : ""
                            });
                        }
                    }
                }
                
                logFunc?.Invoke($"Encontrados {albums.Count} álbumes");
                return albums;
            }
            catch (Exception ex)
            {
                logFunc?.Invoke($"Error consultando Last.fm: {ex.Message}");
                return new List<RecommendedAlbum>();
            }
        }
        
        /// <summary>
        /// Obtiene recomendaciones basadas en múltiples artistas
        /// </summary>
        public async Task<List<string>> GetRecommendations(List<string> artistNames, int maxResults = 20)
        {
            var recommendations = new HashSet<string>();
            
            logFunc?.Invoke($"Generando recomendaciones basadas en {artistNames.Count} artistas");
            
            foreach (var artist in artistNames.Take(5)) // Limitar a 5 para no saturar API
            {
                var similar = await GetSimilarArtists(artist, 5);
                foreach (var rec in similar)
                {
                    if (!artistNames.Contains(rec.Name, StringComparer.OrdinalIgnoreCase))
                        recommendations.Add(rec.Name);
                }
                
                await Task.Delay(200); // Rate limiting
            }
            
            var result = recommendations.Take(maxResults).ToList();
            logFunc?.Invoke($"Generadas {result.Count} recomendaciones únicas");
            
            return result;
        }
        
        /// <summary>
        /// Busca artista por nombre
        /// </summary>
        public async Task<List<RecommendedArtist>> SearchArtist(string query, int limit = 10)
        {
            try
            {
                var url = $"{BaseUrl}?method=artist.search&artist={Uri.EscapeDataString(query)}&api_key={apiKey}&format=json&limit={limit}";
                
                var response = await httpClient.GetStringAsync(url);
                var json = JsonDocument.Parse(response);
                
                var artists = new List<RecommendedArtist>();
                
                if (json.RootElement.TryGetProperty("results", out var results))
                {
                    if (results.TryGetProperty("artistmatches", out var matches))
                    {
                        if (matches.TryGetProperty("artist", out var artistArray))
                        {
                            foreach (var artist in artistArray.EnumerateArray())
                            {
                                artists.Add(new RecommendedArtist
                                {
                                    Name = artist.GetProperty("name").GetString(),
                                    Listeners = artist.TryGetProperty("listeners", out var l) ? 
                                        int.Parse(l.GetString()) : 0,
                                    Url = artist.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : ""
                                });
                            }
                        }
                    }
                }
                
                return artists;
            }
            catch (Exception ex)
            {
                logFunc?.Invoke($"Error buscando artista: {ex.Message}");
                return new List<RecommendedArtist>();
            }
        }
        
        /// <summary>
        /// Obtiene tags/géneros de un artista
        /// </summary>
        public async Task<List<string>> GetArtistTags(string artistName)
        {
            try
            {
                var url = $"{BaseUrl}?method=artist.gettoptags&artist={Uri.EscapeDataString(artistName)}&api_key={apiKey}&format=json";
                
                var response = await httpClient.GetStringAsync(url);
                var json = JsonDocument.Parse(response);
                
                var tags = new List<string>();
                
                if (json.RootElement.TryGetProperty("toptags", out var top))
                {
                    if (top.TryGetProperty("tag", out var tagArray))
                    {
                        foreach (var tag in tagArray.EnumerateArray().Take(10))
                        {
                            tags.Add(tag.GetProperty("name").GetString());
                        }
                    }
                }
                
                return tags;
            }
            catch
            {
                return new List<string>();
            }
        }
        
        /// <summary>
        /// Genera términos de búsqueda optimizados para Soulseek
        /// </summary>
        public List<string> GenerateSearchTerms(RecommendedArtist artist, RecommendedAlbum album = null)
        {
            var terms = new List<string>();
            
            if (album != null)
            {
                // Búsquedas específicas de álbum
                terms.Add($"{artist.Name} {album.Name}");
                terms.Add($"\"{artist.Name}\" \"{album.Name}\"");
                terms.Add($"{album.Name} {artist.Name} flac");
                terms.Add($"{album.Name} {artist.Name} 320");
            }
            else
            {
                // Búsquedas generales del artista
                terms.Add(artist.Name);
                terms.Add($"\"{artist.Name}\"");
                terms.Add($"{artist.Name} discography");
                terms.Add($"{artist.Name} flac");
            }
            
            return terms;
        }
    }
}
