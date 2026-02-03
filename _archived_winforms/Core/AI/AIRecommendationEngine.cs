using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SlskDown.Models;

namespace SlskDown.Core.AI
{
    /// <summary>
    /// Motor de recomendaciones inteligentes basado en IA
    /// </summary>
    public class AIRecommendationEngine
    {
        private readonly OllamaClient ollama;
        private readonly Func<string, Task<List<SearchResult>>> searchFunction;

        public event Action<string> OnLog;

        public AIRecommendationEngine(OllamaClient ollama, Func<string, Task<List<SearchResult>>> searchFunction)
        {
            this.ollama = ollama;
            this.searchFunction = searchFunction;
        }

        /// <summary>
        /// Obtiene recomendaciones basadas en un libro descargado
        /// </summary>
        public async Task<List<BookRecommendation>> GetRecommendationsAsync(string bookTitle, string author = null)
        {
            try
            {
                Log($"🤖 Generando recomendaciones para: {bookTitle}");

                var prompt = $@"
Usuario descargó: ""{bookTitle}"" {(author != null ? $"de {author}" : "")}

Recomienda 5 libros similares en español.
Para cada libro proporciona:
- Título
- Autor
- Razón breve (máximo 15 palabras)

Formato JSON:
[
  {{""title"": ""Título"", ""author"": ""Autor"", ""reason"": ""Razón""}}
]
";

                var response = await ollama.GetCompletionAsync(prompt, temperature: 0.7);
                
                if (string.IsNullOrEmpty(response))
                    return new List<BookRecommendation>();

                // Parsear respuesta JSON
                var recommendations = ParseRecommendations(response);

                // Buscar cada recomendación en Soulseek
                foreach (var rec in recommendations)
                {
                    Log($"🔍 Buscando: {rec.Title} - {rec.Author}");
                    
                    var searchQuery = $"{rec.Author} {rec.Title}";
                    var results = await searchFunction(searchQuery);

                    rec.Available = results.Any();
                    rec.ResultCount = results.Count;
                    
                    if (results.Any())
                    {
                        rec.BestResult = results
                            .OrderByDescending(r => r.FileSize)
                            .ThenByDescending(r => r.UploadSpeed)
                            .FirstOrDefault();
                    }
                }

                Log($"✅ {recommendations.Count} recomendaciones generadas");
                return recommendations;
            }
            catch (Exception ex)
            {
                Log($"❌ Error en GetRecommendationsAsync: {ex.Message}");
                return new List<BookRecommendation>();
            }
        }

        /// <summary>
        /// Obtiene recomendaciones basadas en historial de descargas
        /// </summary>
        public async Task<List<BookRecommendation>> GetRecommendationsFromHistoryAsync(List<string> downloadHistory)
        {
            try
            {
                var recentBooks = downloadHistory.Take(10).ToList();
                var booksText = string.Join(", ", recentBooks);

                var prompt = $@"
Usuario ha descargado recientemente:
{booksText}

Basándote en sus gustos, recomienda 5 libros nuevos que probablemente le interesen.
Formato JSON:
[
  {{""title"": ""Título"", ""author"": ""Autor"", ""reason"": ""Razón""}}
]
";

                var response = await ollama.GetCompletionAsync(prompt, temperature: 0.8);
                
                if (string.IsNullOrEmpty(response))
                    return new List<BookRecommendation>();

                var recommendations = ParseRecommendations(response);

                // Buscar disponibilidad
                foreach (var rec in recommendations)
                {
                    var results = await searchFunction($"{rec.Author} {rec.Title}");
                    rec.Available = results.Any();
                    rec.ResultCount = results.Count;
                    rec.BestResult = results.FirstOrDefault();
                }

                return recommendations;
            }
            catch (Exception ex)
            {
                Log($"❌ Error en GetRecommendationsFromHistoryAsync: {ex.Message}");
                return new List<BookRecommendation>();
            }
        }

        private List<BookRecommendation> ParseRecommendations(string jsonResponse)
        {
            try
            {
                // Extraer JSON si está dentro de texto
                var startIndex = jsonResponse.IndexOf('[');
                var endIndex = jsonResponse.LastIndexOf(']');
                
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    var jsonText = jsonResponse.Substring(startIndex, endIndex - startIndex + 1);
                    var items = JsonSerializer.Deserialize<List<RecommendationItem>>(jsonText);
                    
                    return items.Select(item => new BookRecommendation
                    {
                        Title = item.title,
                        Author = item.author,
                        Reason = item.reason
                    }).ToList();
                }

                return new List<BookRecommendation>();
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error parseando recomendaciones: {ex.Message}");
                return new List<BookRecommendation>();
            }
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }

        private class RecommendationItem
        {
            public string title { get; set; }
            public string author { get; set; }
            public string reason { get; set; }
        }
    }

    public class BookRecommendation
    {
        public string Title { get; set; }
        public string Author { get; set; }
        public string Reason { get; set; }
        public bool Available { get; set; }
        public int ResultCount { get; set; }
        public SearchResult BestResult { get; set; }
    }
}
