using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SlskDown
{
    public class BookMetadata
    {
        public string Title { get; set; }
        public string Author { get; set; }
        public string Synopsis { get; set; }
        public string BackCover { get; set; }
        public List<string> Keywords { get; set; } = new List<string>();
        public string Genre { get; set; }
        public string Publisher { get; set; }
        public string PublishDate { get; set; }
        public int PageCount { get; set; }
        public string ISBN { get; set; }
        public string CoverUrl { get; set; }
        public double Rating { get; set; }
        public int RatingCount { get; set; }
        public string Language { get; set; }
        public List<string> Categories { get; set; } = new List<string>();
        public string Source { get; set; } // De dónde vino la metadata
        public double Quality { get; set; } // 0-1, qué tan completa está
    }

    public class BookMetadataService
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private const int TIMEOUT_SECONDS = 10;

        static BookMetadataService()
        {
            httpClient.Timeout = TimeSpan.FromSeconds(TIMEOUT_SECONDS);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "SlskDown/1.0");
        }

        /// <summary>
        /// Busca metadata en múltiples fuentes públicas
        /// </summary>
        public async Task<BookMetadata> SearchMetadata(string title, string author = null)
        {
            if (string.IsNullOrWhiteSpace(title))
                return null;

            // Intentar en orden de calidad
            var tasks = new List<Task<BookMetadata>>
            {
                SearchGoogleBooks(title, author),
                SearchOpenLibrary(title, author)
            };

            // Esperar a que alguna responda
            while (tasks.Count > 0)
            {
                var completedTask = await Task.WhenAny(tasks);
                tasks.Remove(completedTask);

                var result = await completedTask;
                if (result != null && result.Quality > 0.5)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Google Books API - La mejor fuente de metadata
        /// </summary>
        private async Task<BookMetadata> SearchGoogleBooks(string title, string author)
        {
            try
            {
                var query = author != null 
                    ? $"{title}+inauthor:{author}" 
                    : title;
                
                var url = $"https://www.googleapis.com/books/v1/volumes?q={Uri.EscapeDataString(query)}&maxResults=1";
                
                var response = await httpClient.GetStringAsync(url);
                var json = JsonDocument.Parse(response);

                if (!json.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
                    return null;

                var book = items[0].GetProperty("volumeInfo");
                
                var metadata = new BookMetadata
                {
                    Source = "Google Books",
                    Title = GetJsonString(book, "title"),
                    Author = GetJsonArrayString(book, "authors"),
                    Synopsis = GetJsonString(book, "description"),
                    Publisher = GetJsonString(book, "publisher"),
                    PublishDate = GetJsonString(book, "publishedDate"),
                    PageCount = GetJsonInt(book, "pageCount"),
                    Language = GetJsonString(book, "language"),
                    Categories = GetJsonArray(book, "categories")
                };

                // ISBN
                if (book.TryGetProperty("industryIdentifiers", out var identifiers))
                {
                    foreach (var id in identifiers.EnumerateArray())
                    {
                        var type = GetJsonString(id, "type");
                        if (type == "ISBN_13" || type == "ISBN_10")
                        {
                            metadata.ISBN = GetJsonString(id, "identifier");
                            break;
                        }
                    }
                }

                // Cover URL
                if (book.TryGetProperty("imageLinks", out var images))
                {
                    metadata.CoverUrl = GetJsonString(images, "thumbnail") 
                        ?? GetJsonString(images, "smallThumbnail");
                }

                // Rating
                metadata.Rating = GetJsonDouble(book, "averageRating");
                metadata.RatingCount = GetJsonInt(book, "ratingsCount");

                // Keywords de categorías
                metadata.Keywords = metadata.Categories.Take(5).ToList();

                // Generar contraportada si hay sinopsis
                if (!string.IsNullOrEmpty(metadata.Synopsis))
                {
                    metadata.BackCover = GenerateBackCover(metadata);
                }

                // Calcular calidad
                metadata.Quality = CalculateQuality(metadata);

                return metadata;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en Google Books: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Open Library API - Alternativa gratuita
        /// </summary>
        private async Task<BookMetadata> SearchOpenLibrary(string title, string author)
        {
            try
            {
                var query = author != null 
                    ? $"{title} {author}" 
                    : title;
                
                var url = $"https://openlibrary.org/search.json?q={Uri.EscapeDataString(query)}&limit=1";
                
                var response = await httpClient.GetStringAsync(url);
                var json = JsonDocument.Parse(response);

                if (!json.RootElement.TryGetProperty("docs", out var docs) || docs.GetArrayLength() == 0)
                    return null;

                var book = docs[0];
                
                var metadata = new BookMetadata
                {
                    Source = "Open Library",
                    Title = GetJsonString(book, "title"),
                    Author = GetJsonArrayString(book, "author_name"),
                    Publisher = GetJsonArrayString(book, "publisher"),
                    PublishDate = GetJsonString(book, "first_publish_year"),
                    ISBN = GetJsonArrayString(book, "isbn"),
                    Language = GetJsonArrayString(book, "language")
                };

                // Cover URL
                if (book.TryGetProperty("cover_i", out var coverId))
                {
                    var coverIdValue = coverId.GetInt64();
                    metadata.CoverUrl = $"https://covers.openlibrary.org/b/id/{coverIdValue}-L.jpg";
                }

                // Subjects como keywords
                metadata.Keywords = GetJsonArray(book, "subject").Take(5).ToList();
                metadata.Categories = GetJsonArray(book, "subject").Take(3).ToList();

                // Calcular calidad
                metadata.Quality = CalculateQuality(metadata);

                return metadata;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en Open Library: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Genera una contraportada atractiva basándose en la sinopsis
        /// </summary>
        private string GenerateBackCover(BookMetadata metadata)
        {
            var synopsis = metadata.Synopsis;
            
            // Acortar si es muy largo
            if (synopsis.Length > 500)
            {
                synopsis = synopsis.Substring(0, 497) + "...";
            }

            var backCover = synopsis + "\n\n";

            // Agregar info del autor si existe
            if (!string.IsNullOrEmpty(metadata.Author))
            {
                backCover += $"— {metadata.Author}\n\n";
            }

            // Agregar rating si existe
            if (metadata.Rating > 0)
            {
                var stars = new string('⭐', (int)Math.Round(metadata.Rating));
                backCover += $"{stars} {metadata.Rating:F1}/5";
                
                if (metadata.RatingCount > 0)
                {
                    backCover += $" ({metadata.RatingCount:N0} valoraciones)";
                }
                backCover += "\n\n";
            }

            // Agregar categorías
            if (metadata.Categories.Any())
            {
                backCover += $"Género: {string.Join(", ", metadata.Categories.Take(3))}\n";
            }

            return backCover.Trim();
        }

        /// <summary>
        /// Calcula qué tan completa está la metadata (0-1)
        /// </summary>
        private double CalculateQuality(BookMetadata metadata)
        {
            double score = 0;
            
            if (!string.IsNullOrEmpty(metadata.Title)) score += 0.2;
            if (!string.IsNullOrEmpty(metadata.Author)) score += 0.2;
            if (!string.IsNullOrEmpty(metadata.Synopsis)) score += 0.3;
            if (!string.IsNullOrEmpty(metadata.CoverUrl)) score += 0.1;
            if (metadata.Rating > 0) score += 0.1;
            if (metadata.Categories.Any()) score += 0.1;

            return score;
        }

        // Helpers para extraer datos del JSON
        private string GetJsonString(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private int GetJsonInt(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
                ? value.GetInt32()
                : 0;
        }

        private double GetJsonDouble(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
                ? value.GetDouble()
                : 0;
        }

        private string GetJsonArrayString(JsonElement element, string property)
        {
            if (element.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array)
            {
                var items = array.EnumerateArray().Select(e => e.GetString()).Where(s => !string.IsNullOrEmpty(s));
                return string.Join(", ", items);
            }
            return null;
        }

        private List<string> GetJsonArray(JsonElement element, string property)
        {
            if (element.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array)
            {
                return array.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }
            return new List<string>();
        }
    }
}
