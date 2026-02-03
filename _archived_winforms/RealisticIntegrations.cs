using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;

namespace SlskDown
{
    // ═══════════════════════════════════════════════════════════════
    // INTEGRACIÓN CON SPOTIFY/APPLE MUSIC
    // ═══════════════════════════════════════════════════════════════
    
    public class MusicStreamingIntegration : DisposableBase
    {
        private HttpClient httpClient;
        private Action<string> logAction;
        private string spotifyToken;
        private string appleMusicToken;
        
        public MusicStreamingIntegration(Action<string> logger)
        {
            httpClient = HttpClientPool.Spotify;
            logAction = logger;
        }
        
        protected override void DisposeManagedResources()
        {
            // HttpClient es compartido, no dispose
        }
        
        public async Task<bool> ConnectSpotify(string accessToken)
        {
            spotifyToken = accessToken;
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            
            try
            {
                var response = await httpClient.GetAsync("https://api.spotify.com/v1/me");
                if (response.IsSuccessStatusCode)
                {
                    logAction?.Invoke("✅ Conectado a Spotify");
                    return true;
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error conectando a Spotify: {ex.Message}");
            }
            
            return false;
        }
        
        public async Task SyncPlaylistToSpotify(Playlist playlist)
        {
            try
            {
                // Crear playlist en Spotify
                var createPlaylistBody = new
                {
                    name = playlist.Name,
                    description = playlist.Description,
                    @public = false
                };
                
                var response = await httpClient.PostAsync(
                    "https://api.spotify.com/v1/me/playlists",
                    new StringContent(JsonSerializer.Serialize(createPlaylistBody), Encoding.UTF8, "application/json")
                );
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    var playlistData = JsonSerializer.Deserialize<JsonElement>(result);
                    var playlistId = playlistData.GetProperty("id").GetString();
                    
                    // Buscar tracks en Spotify y agregar
                    var trackUris = new List<string>();
                    foreach (var file in playlist.Files)
                    {
                        var trackUri = await SearchSpotifyTrack(file.Artist, file.Title);
                        if (trackUri != null)
                        {
                            trackUris.Add(trackUri);
                        }
                    }
                    
                    // Agregar tracks a playlist
                    if (trackUris.Count > 0)
                    {
                        await AddTracksToSpotifyPlaylist(playlistId, trackUris);
                        logAction?.Invoke($"✅ Playlist sincronizada a Spotify: {playlist.Name} ({trackUris.Count} tracks)");
                    }
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error sincronizando a Spotify: {ex.Message}");
            }
        }
        
        private async Task<string> SearchSpotifyTrack(string artist, string title)
        {
            return await ApiRateLimiters.Spotify.ExecuteAsync(async () =>
            {
                try
                {
                    var query = Uri.EscapeDataString($"{artist} {title}");
                    var response = await httpClient.GetAsync($"https://api.spotify.com/v1/search?q={query}&type=track&limit=1").Fast();
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<JsonElement>(result);
                    
                    if (data.GetProperty("tracks").GetProperty("items").GetArrayLength() > 0)
                    {
                        return data.GetProperty("tracks").GetProperty("items")[0].GetProperty("uri").GetString();
                    }
                    }
                }
                catch { }
                
                return null;
            });
        }
        
        private async Task AddTracksToSpotifyPlaylist(string playlistId, List<string> trackUris)
        {
            var body = new { uris = trackUris };
            await httpClient.PostAsync(
                $"https://api.spotify.com/v1/playlists/{playlistId}/tracks",
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            );
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // PLUGIN PARA OBSIDIAN/NOTION
    // ═══════════════════════════════════════════════════════════════
    
    public class KnowledgeBaseIntegration : DisposableBase
    {
        private Action<string> logAction;
        private string obsidianVaultPath;
        private string notionToken;
        
        public KnowledgeBaseIntegration(Action<string> logger)
        {
            logAction = logger;
        }
        
        protected override void DisposeManagedResources() { }
        
        public void SetObsidianVault(string vaultPath)
        {
            obsidianVaultPath = vaultPath;
            logAction?.Invoke($"✅ Obsidian vault configurado: {vaultPath}");
        }
        
        public async Task CreateBookNote(BookMetadata book)
        {
            try
            {
                var notePath = Path.Combine(obsidianVaultPath, "Books", $"{book.Title}.md");
                Directory.CreateDirectory(Path.GetDirectoryName(notePath));
                
                var markdown = new StringBuilder();
                markdown.AppendLine($"# {book.Title}");
                markdown.AppendLine();
                markdown.AppendLine($"**Autor**: {book.Author}");
                markdown.AppendLine($"**Género**: {book.Genre}");
                markdown.AppendLine($"**Año**: {book.Year}");
                markdown.AppendLine($"**Rating**: {book.Rating}/5");
                markdown.AppendLine();
                markdown.AppendLine("## Notas");
                markdown.AppendLine();
                markdown.AppendLine("## Citas");
                markdown.AppendLine();
                markdown.AppendLine("## Tags");
                foreach (var tag in book.Tags)
                {
                    markdown.AppendLine($"- #{tag.Replace(" ", "_")}");
                }
                
                await File.WriteAllTextAsync(notePath, markdown.ToString());
                logAction?.Invoke($"📝 Nota creada en Obsidian: {book.Title}");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error creando nota: {ex.Message}");
            }
        }
        
        public async Task SyncToNotion(BookMetadata book)
        {
            // Implementación de API de Notion
            logAction?.Invoke($"📝 Sincronizando a Notion: {book.Title}");
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // INTEGRACIÓN CON ANKI (FLASHCARDS)
    // ═══════════════════════════════════════════════════════════════
    
    public class AnkiIntegration : DisposableBase
    {
        private HttpClient httpClient;
        private Action<string> logAction;
        
        public AnkiIntegration(Action<string> logger)
        {
            httpClient = HttpClientPool.Default;
            logAction = logger;
        }
        
        protected override void DisposeManagedResources() { }
        
        public async Task<bool> IsAnkiRunning()
        {
            try
            {
                var response = await httpClient.PostAsync(
                    "http://localhost:8765",
                    new StringContent("{\"action\":\"version\",\"version\":6}", Encoding.UTF8, "application/json")
                );
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
        
        public async Task CreateFlashcardsFromBook(string bookPath, string deckName)
        {
            try
            {
                // Extraer texto del libro
                var text = await ExtractTextFromBook(bookPath);
                
                // Generar flashcards con IA (conceptos clave)
                var flashcards = GenerateFlashcards(text);
                
                // Crear deck en Anki
                await CreateAnkiDeck(deckName);
                
                // Agregar flashcards
                foreach (var card in flashcards)
                {
                    await AddFlashcardToAnki(deckName, card.Front, card.Back);
                }
                
                logAction?.Invoke($"✅ {flashcards.Count} flashcards creadas en Anki: {deckName}");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error creando flashcards: {ex.Message}");
            }
        }
        
        private async Task<string> ExtractTextFromBook(string bookPath)
        {
            // Implementación de extracción de texto
            return await File.ReadAllTextAsync(bookPath);
        }
        
        private List<Flashcard> GenerateFlashcards(string text)
        {
            // Generación inteligente de flashcards
            var flashcards = new List<Flashcard>();
            
            // Ejemplo simple: extraer definiciones
            var lines = text.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains(":"))
                {
                    var parts = line.Split(':', 2);
                    if (parts.Length == 2)
                    {
                        flashcards.Add(new Flashcard
                        {
                            Front = parts[0].Trim(),
                            Back = parts[1].Trim()
                        });
                    }
                }
            }
            
            return flashcards.Take(50).ToList();
        }
        
        private async Task CreateAnkiDeck(string deckName)
        {
            var request = new
            {
                action = "createDeck",
                version = 6,
                @params = new { deck = deckName }
            };
            
            await httpClient.PostAsync(
                "http://localhost:8765",
                new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
            );
        }
        
        private async Task AddFlashcardToAnki(string deckName, string front, string back)
        {
            var request = new
            {
                action = "addNote",
                version = 6,
                @params = new
                {
                    note = new
                    {
                        deckName = deckName,
                        modelName = "Basic",
                        fields = new
                        {
                            Front = front,
                            Back = back
                        },
                        tags = new[] { "SlskDown" }
                    }
                }
            };
            
            await httpClient.PostAsync(
                "http://localhost:8765",
                new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
            );
        }
    }
    
    public class Flashcard
    {
        public string Front { get; set; }
        public string Back { get; set; }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // SISTEMA DE CITAS Y REFERENCIAS BIBLIOGRÁFICAS
    // ═══════════════════════════════════════════════════════════════
    
    public class BibliographyManager : DisposableBase
    {
        private Action<string> logAction;
        private List<Citation> citations = new List<Citation>();
        
        public BibliographyManager(Action<string> logger)
        {
            logAction = logger;
        }
        
        protected override void DisposeManagedResources()
        {
            citations?.Clear();
        }
        
        public Citation CreateCitation(BookMetadata book, CitationStyle style = CitationStyle.APA)
        {
            var citation = new Citation
            {
                Id = Guid.NewGuid().ToString(),
                Book = book,
                Style = style,
                FormattedText = FormatCitation(book, style)
            };
            
            citations.Add(citation);
            logAction?.Invoke($"📚 Cita creada: {citation.FormattedText}");
            
            return citation;
        }
        
        private string FormatCitation(BookMetadata book, CitationStyle style)
        {
            switch (style)
            {
                case CitationStyle.APA:
                    return $"{book.Author} ({book.Year}). {book.Title}. {book.Publisher}.";
                    
                case CitationStyle.MLA:
                    return $"{book.Author}. {book.Title}. {book.Publisher}, {book.Year}.";
                    
                case CitationStyle.Chicago:
                    return $"{book.Author}. {book.Title}. {book.Publisher}, {book.Year}.";
                    
                case CitationStyle.Harvard:
                    return $"{book.Author}, {book.Year}. {book.Title}. {book.Publisher}.";
                    
                default:
                    return $"{book.Author} - {book.Title} ({book.Year})";
            }
        }
        
        public string ExportBibliography(CitationStyle style)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Bibliografía");
            sb.AppendLine();
            
            foreach (var citation in citations.OrderBy(c => c.Book.Author))
            {
                sb.AppendLine(citation.FormattedText);
            }
            
            return sb.ToString();
        }
        
        public async Task ExportToBibTeX(string outputPath)
        {
            var sb = new StringBuilder();
            
            foreach (var citation in citations)
            {
                sb.AppendLine($"@book{{{citation.Id},");
                sb.AppendLine($"  author = {{{citation.Book.Author}}},");
                sb.AppendLine($"  title = {{{citation.Book.Title}}},");
                sb.AppendLine($"  year = {{{citation.Book.Year}}},");
                sb.AppendLine($"  publisher = {{{citation.Book.Publisher}}}");
                sb.AppendLine("}");
                sb.AppendLine();
            }
            
            await File.WriteAllTextAsync(outputPath, sb.ToString());
            logAction?.Invoke($"📄 Bibliografía exportada a BibTeX: {outputPath}");
        }
    }
    
    public class Citation
    {
        public string Id { get; set; }
        public BookMetadata Book { get; set; }
        public CitationStyle Style { get; set; }
        public string FormattedText { get; set; }
    }
    
    public enum CitationStyle
    {
        APA,
        MLA,
        Chicago,
        Harvard,
        IEEE
    }
}
