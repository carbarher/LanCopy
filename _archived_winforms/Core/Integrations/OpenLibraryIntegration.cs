using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SlskDown.Core.Integrations
{
    /// <summary>
    /// Integración con OpenLibrary para metadata de libros
    /// </summary>
    public class OpenLibraryIntegration
    {
        private readonly HttpClient httpClient;
        private const string BASE_URL = "https://openlibrary.org";

        public event Action<string> OnLog;

        public OpenLibraryIntegration()
        {
            httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            httpClient.DefaultRequestHeaders.Add("User-Agent", "SlskDown/1.0");
        }

        /// <summary>
        /// Busca libros por título
        /// </summary>
        public async Task<List<OpenLibraryBook>> SearchByTitleAsync(string title, int limit = 10)
        {
            try
            {
                var url = $"{BASE_URL}/search.json?title={Uri.EscapeDataString(title)}&limit={limit}";
                var response = await httpClient.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<OpenLibrarySearchResponse>(response);

                if (result?.Docs == null)
                    return new List<OpenLibraryBook>();

                return result.Docs.Select(doc => new OpenLibraryBook
                {
                    Title = doc.Title,
                    Author = doc.AuthorName?.FirstOrDefault(),
                    AuthorKey = doc.AuthorKey?.FirstOrDefault(),
                    FirstPublishYear = doc.FirstPublishYear,
                    ISBN = doc.ISBN?.FirstOrDefault(),
                    CoverEditionKey = doc.CoverEditionKey,
                    Publisher = doc.Publisher?.FirstOrDefault(),
                    Language = doc.Language?.FirstOrDefault(),
                    NumberOfPages = doc.NumberOfPagesMedian
                }).ToList();
            }
            catch (Exception ex)
            {
                Log($"Error buscando en OpenLibrary: {ex.Message}");
                return new List<OpenLibraryBook>();
            }
        }

        /// <summary>
        /// Busca libros por autor
        /// </summary>
        public async Task<List<OpenLibraryBook>> SearchByAuthorAsync(string author, int limit = 10)
        {
            try
            {
                var url = $"{BASE_URL}/search.json?author={Uri.EscapeDataString(author)}&limit={limit}";
                var response = await httpClient.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<OpenLibrarySearchResponse>(response);

                if (result?.Docs == null)
                    return new List<OpenLibraryBook>();

                return result.Docs.Select(doc => new OpenLibraryBook
                {
                    Title = doc.Title,
                    Author = doc.AuthorName?.FirstOrDefault(),
                    AuthorKey = doc.AuthorKey?.FirstOrDefault(),
                    FirstPublishYear = doc.FirstPublishYear,
                    ISBN = doc.ISBN?.FirstOrDefault(),
                    CoverEditionKey = doc.CoverEditionKey,
                    Publisher = doc.Publisher?.FirstOrDefault(),
                    Language = doc.Language?.FirstOrDefault(),
                    NumberOfPages = doc.NumberOfPagesMedian
                }).ToList();
            }
            catch (Exception ex)
            {
                Log($"Error buscando por autor: {ex.Message}");
                return new List<OpenLibraryBook>();
            }
        }

        /// <summary>
        /// Obtiene información detallada de un libro por ISBN
        /// </summary>
        public async Task<OpenLibraryBook> GetBookByISBNAsync(string isbn)
        {
            try
            {
                var url = $"{BASE_URL}/isbn/{isbn}.json";
                var response = await httpClient.GetStringAsync(url);
                var work = JsonSerializer.Deserialize<OpenLibraryWork>(response);

                if (work == null)
                    return null;

                return new OpenLibraryBook
                {
                    Title = work.Title,
                    ISBN = isbn,
                    Publisher = work.Publishers?.FirstOrDefault(),
                    FirstPublishYear = work.PublishDate != null ? ExtractYear(work.PublishDate) : null,
                    NumberOfPages = work.NumberOfPages
                };
            }
            catch (Exception ex)
            {
                Log($"Error obteniendo libro por ISBN: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Obtiene URL de la portada de un libro
        /// </summary>
        public string GetCoverUrl(string coverEditionKey, string size = "M")
        {
            if (string.IsNullOrEmpty(coverEditionKey))
                return null;

            // Tamaños: S (small), M (medium), L (large)
            return $"https://covers.openlibrary.org/b/olid/{coverEditionKey}-{size}.jpg";
        }

        /// <summary>
        /// Obtiene URL de la portada por ISBN
        /// </summary>
        public string GetCoverUrlByISBN(string isbn, string size = "M")
        {
            if (string.IsNullOrEmpty(isbn))
                return null;

            return $"https://covers.openlibrary.org/b/isbn/{isbn}-{size}.jpg";
        }

        /// <summary>
        /// Obtiene información de un autor
        /// </summary>
        public async Task<OpenLibraryAuthor> GetAuthorAsync(string authorKey)
        {
            try
            {
                var url = $"{BASE_URL}/authors/{authorKey}.json";
                var response = await httpClient.GetStringAsync(url);
                var author = JsonSerializer.Deserialize<OpenLibraryAuthor>(response);

                return author;
            }
            catch (Exception ex)
            {
                Log($"Error obteniendo autor: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Obtiene obras de un autor
        /// </summary>
        public async Task<List<OpenLibraryBook>> GetAuthorWorksAsync(string authorKey, int limit = 50)
        {
            try
            {
                var url = $"{BASE_URL}/authors/{authorKey}/works.json?limit={limit}";
                var response = await httpClient.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<OpenLibraryWorksResponse>(response);

                if (result?.Entries == null)
                    return new List<OpenLibraryBook>();

                return result.Entries.Select(entry => new OpenLibraryBook
                {
                    Title = entry.Title,
                    FirstPublishYear = entry.FirstPublishYear
                }).ToList();
            }
            catch (Exception ex)
            {
                Log($"Error obteniendo obras del autor: {ex.Message}");
                return new List<OpenLibraryBook>();
            }
        }

        /// <summary>
        /// Busca libros relacionados
        /// </summary>
        public async Task<List<OpenLibraryBook>> GetRelatedBooksAsync(string title, string author)
        {
            try
            {
                // Buscar por autor para encontrar otros libros
                var books = await SearchByAuthorAsync(author, 20);
                
                // Filtrar el libro actual
                return books.Where(b => !b.Title.Equals(title, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            catch (Exception ex)
            {
                Log($"Error obteniendo libros relacionados: {ex.Message}");
                return new List<OpenLibraryBook>();
            }
        }

        private int? ExtractYear(string publishDate)
        {
            if (string.IsNullOrEmpty(publishDate))
                return null;

            // Intentar extraer año de diferentes formatos
            var parts = publishDate.Split(' ', '-', '/');
            foreach (var part in parts)
            {
                if (int.TryParse(part, out var year) && year >= 1000 && year <= DateTime.Now.Year)
                {
                    return year;
                }
            }

            return null;
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
    }

    // Modelos de respuesta de OpenLibrary
    public class OpenLibrarySearchResponse
    {
        public int NumFound { get; set; }
        public List<OpenLibraryDoc> Docs { get; set; }
    }

    public class OpenLibraryDoc
    {
        public string Title { get; set; }
        public List<string> AuthorName { get; set; }
        public List<string> AuthorKey { get; set; }
        public int? FirstPublishYear { get; set; }
        public List<string> ISBN { get; set; }
        public string CoverEditionKey { get; set; }
        public List<string> Publisher { get; set; }
        public List<string> Language { get; set; }
        public int? NumberOfPagesMedian { get; set; }
    }

    public class OpenLibraryWork
    {
        public string Title { get; set; }
        public List<string> Publishers { get; set; }
        public string PublishDate { get; set; }
        public int? NumberOfPages { get; set; }
    }

    public class OpenLibraryWorksResponse
    {
        public List<OpenLibraryWorkEntry> Entries { get; set; }
    }

    public class OpenLibraryWorkEntry
    {
        public string Title { get; set; }
        public int? FirstPublishYear { get; set; }
    }

    public class OpenLibraryBook
    {
        public string Title { get; set; }
        public string Author { get; set; }
        public string AuthorKey { get; set; }
        public int? FirstPublishYear { get; set; }
        public string ISBN { get; set; }
        public string CoverEditionKey { get; set; }
        public string Publisher { get; set; }
        public string Language { get; set; }
        public int? NumberOfPages { get; set; }

        public string CoverUrl => !string.IsNullOrEmpty(CoverEditionKey)
            ? $"https://covers.openlibrary.org/b/olid/{CoverEditionKey}-M.jpg"
            : !string.IsNullOrEmpty(ISBN)
                ? $"https://covers.openlibrary.org/b/isbn/{ISBN}-M.jpg"
                : null;
    }

    public class OpenLibraryAuthor
    {
        public string Name { get; set; }
        public string Bio { get; set; }
        public string BirthDate { get; set; }
        public string DeathDate { get; set; }
        public string PersonalName { get; set; }
    }
}
