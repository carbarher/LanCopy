using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;

namespace SlskDown
{
    public class MusicMetadata
    {
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Title { get; set; }
        public int Year { get; set; }
        public string Genre { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }
    
    public class BookMetadata
    {
        public string Author { get; set; }
        public string Title { get; set; }
        public string Genre { get; set; }
        public int Year { get; set; }
        public double Rating { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }
    
    public class ExternalServicesIntegration
    {
        private HttpClient httpClient;
        private InterestsSystem interestsSystem;
        private Action<string> logAction;
        private Dictionary<string, MusicMetadata> musicCache = new Dictionary<string, MusicMetadata>();
        private Dictionary<string, BookMetadata> bookCache = new Dictionary<string, BookMetadata>();
        
        public ExternalServicesIntegration(InterestsSystem interests, Action<string> logger)
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "SlskDown/1.0");
            interestsSystem = interests;
            logAction = logger;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // MUSICBRAINZ INTEGRATION
        // ═══════════════════════════════════════════════════════════════
        
        public async Task<MusicMetadata> EnrichMusicMetadata(string filename)
        {
            try
            {
                // Verificar caché
                if (musicCache.TryGetValue(filename, out var cached))
                {
                    return cached;
                }
                
                // Extraer información del nombre del archivo
                var info = ParseMusicFilename(filename);
                if (string.IsNullOrEmpty(info.artist) && string.IsNullOrEmpty(info.title))
                {
                    return null;
                }
                
                logAction?.Invoke($"Buscando metadata en MusicBrainz: {filename}");
                
                // Buscar en MusicBrainz
                var query = Uri.EscapeDataString($"{info.artist} {info.title}");
                var url = $"https://musicbrainz.org/ws/2/recording/?query={query}&fmt=json&limit=1";
                
                var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    logAction?.Invoke($"MusicBrainz error: {response.StatusCode}");
                    return null;
                }
                
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(json);
                
                if (!data.TryGetProperty("recordings", out var recordings) || 
                    recordings.GetArrayLength() == 0)
                {
                    return null;
                }
                
                var recording = recordings[0];
                var metadata = new MusicMetadata
                {
                    Title = recording.GetProperty("title").GetString(),
                    Artist = recording.TryGetProperty("artist-credit", out var artistCredit) && 
                            artistCredit.GetArrayLength() > 0
                        ? artistCredit[0].GetProperty("name").GetString()
                        : info.artist,
                    Year = 0,
                    Genre = "",
                    Tags = new List<string>()
                };
                
                // Extraer tags si existen
                if (recording.TryGetProperty("tags", out var tags))
                {
                    foreach (var tag in tags.EnumerateArray())
                    {
                        if (tag.TryGetProperty("name", out var tagName))
                        {
                            metadata.Tags.Add(tagName.GetString());
                        }
                    }
                }
                
                // Agregar a intereses automáticamente
                if (!string.IsNullOrEmpty(metadata.Artist))
                {
                    interestsSystem.AddLikedInterest(metadata.Artist);
                }
                
                foreach (var tag in metadata.Tags.Take(3))
                {
                    interestsSystem.AddLikedInterest(tag);
                }
                
                // Cachear
                musicCache[filename] = metadata;
                
                logAction?.Invoke($"Metadata encontrada: {metadata.Artist} - {metadata.Title}");
                
                // Delay para respetar rate limit de MusicBrainz (1 req/sec)
                await Task.Delay(1000);
                
                return metadata;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error enriqueciendo metadata musical: {ex.Message}");
                return null;
            }
        }
        
        private (string artist, string title) ParseMusicFilename(string filename)
        {
            // Remover extensión
            var name = System.IO.Path.GetFileNameWithoutExtension(filename);
            
            // Patrones comunes: "Artist - Title", "Artist-Title", "Title by Artist"
            if (name.Contains(" - "))
            {
                var parts = name.Split(new[] { " - " }, 2, StringSplitOptions.None);
                return (parts[0].Trim(), parts[1].Trim());
            }
            else if (name.Contains(" by "))
            {
                var parts = name.Split(new[] { " by " }, 2, StringSplitOptions.None);
                return (parts[1].Trim(), parts[0].Trim());
            }
            
            return ("", name);
        }
        
        // ═══════════════════════════════════════════════════════════════
        // GOODREADS INTEGRATION (usando Open Library API como alternativa)
        // ═══════════════════════════════════════════════════════════════
        
        public async Task<BookMetadata> EnrichBookMetadata(string filename)
        {
            try
            {
                // Verificar caché
                if (bookCache.TryGetValue(filename, out var cached))
                {
                    return cached;
                }
                
                // Extraer información del nombre del archivo
                var info = ParseBookFilename(filename);
                if (string.IsNullOrEmpty(info.author) && string.IsNullOrEmpty(info.title))
                {
                    return null;
                }
                
                logAction?.Invoke($"📚 Buscando metadata en Open Library: {filename}");
                
                // Buscar en Open Library (alternativa a Goodreads)
                var query = Uri.EscapeDataString($"{info.author} {info.title}");
                var url = $"https://openlibrary.org/search.json?q={query}&limit=1";
                
                var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    logAction?.Invoke($"Open Library error: {response.StatusCode}");
                    return null;
                }
                
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(json);
                
                if (!data.TryGetProperty("docs", out var docs) || 
                    docs.GetArrayLength() == 0)
                {
                    return null;
                }
                
                var book = docs[0];
                var metadata = new BookMetadata
                {
                    Title = book.TryGetProperty("title", out var title) 
                        ? title.GetString() 
                        : info.title,
                    Author = book.TryGetProperty("author_name", out var authors) && 
                            authors.GetArrayLength() > 0
                        ? authors[0].GetString()
                        : info.author,
                    Year = book.TryGetProperty("first_publish_year", out var year)
                        ? year.GetInt32()
                        : 0,
                    Genre = "",
                    Rating = 0,
                    Tags = new List<string>()
                };
                
                // Extraer subjects (géneros/tags)
                if (book.TryGetProperty("subject", out var subjects))
                {
                    foreach (var subject in subjects.EnumerateArray().Take(5))
                    {
                        metadata.Tags.Add(subject.GetString());
                    }
                }
                
                // Agregar a intereses automáticamente
                if (!string.IsNullOrEmpty(metadata.Author))
                {
                    interestsSystem.AddLikedInterest(metadata.Author);
                }
                
                foreach (var tag in metadata.Tags.Take(3))
                {
                    interestsSystem.AddLikedInterest(tag);
                }
                
                // Cachear
                bookCache[filename] = metadata;
                
                logAction?.Invoke($"Metadata encontrada: {metadata.Author} - {metadata.Title}");
                
                return metadata;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error enriqueciendo metadata de libro: {ex.Message}");
                return null;
            }
        }
        
        private (string author, string title) ParseBookFilename(string filename)
        {
            // Remover extensión
            var name = System.IO.Path.GetFileNameWithoutExtension(filename);
            
            // Patrones comunes: "Author - Title", "Title by Author", "Title (Author)"
            if (name.Contains(" - "))
            {
                var parts = name.Split(new[] { " - " }, 2, StringSplitOptions.None);
                return (parts[0].Trim(), parts[1].Trim());
            }
            else if (name.Contains(" by "))
            {
                var parts = name.Split(new[] { " by " }, 2, StringSplitOptions.None);
                return (parts[1].Trim(), parts[0].Trim());
            }
            else if (name.Contains("(") && name.Contains(")"))
            {
                var titleEnd = name.IndexOf("(");
                var authorStart = name.IndexOf("(") + 1;
                var authorEnd = name.IndexOf(")");
                
                var title = name.Substring(0, titleEnd).Trim();
                var author = name.Substring(authorStart, authorEnd - authorStart).Trim();
                
                return (author, title);
            }
            
            return ("", name);
        }
        
        // ═══════════════════════════════════════════════════════════════
        // BATCH PROCESSING
        // ═══════════════════════════════════════════════════════════════
        
        public async Task EnrichMultipleFiles(List<string> filenames)
        {
            int enriched = 0;
            
            foreach (var filename in filenames)
            {
                var ext = System.IO.Path.GetExtension(filename).ToLowerInvariant();
                
                if (IsMusicFile(ext))
                {
                    var metadata = await EnrichMusicMetadata(filename);
                    if (metadata != null) enriched++;
                }
                else if (IsBookFile(ext))
                {
                    var metadata = await EnrichBookMetadata(filename);
                    if (metadata != null) enriched++;
                }
                
                // Delay entre archivos
                await Task.Delay(500);
            }
            
            logAction?.Invoke($"Enriquecimiento completado: {enriched}/{filenames.Count} archivos");
        }
        
        private bool IsMusicFile(string ext)
        {
            var musicExts = new[] { ".mp3", ".flac", ".m4a", ".ogg", ".wav", ".aac" };
            return musicExts.Contains(ext);
        }
        
        private bool IsBookFile(string ext)
        {
            var bookExts = new[] { ".epub", ".mobi", ".pdf", ".azw3", ".fb2" };
            return bookExts.Contains(ext);
        }
        
        // ═══════════════════════════════════════════════════════════════
        // ESTADÍSTICAS
        // ═══════════════════════════════════════════════════════════════
        
        public Dictionary<string, object> GetStats()
        {
            return new Dictionary<string, object>
            {
                { "MusicCacheSize", musicCache.Count },
                { "BookCacheSize", bookCache.Count },
                { "TotalEnriched", musicCache.Count + bookCache.Count }
            };
        }
        
        public void ClearCache()
        {
            musicCache.Clear();
            bookCache.Clear();
            logAction?.Invoke("Caché de metadata externa limpiado");
        }
    }
}
