using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace SlskDown.Core.AI
{
    /// <summary>
    /// Generador de resúmenes de libros usando IA
    /// </summary>
    public class BookSummarizer
    {
        private readonly OllamaClient ollama;

        public event Action<string> OnLog;

        public BookSummarizer(OllamaClient ollama)
        {
            this.ollama = ollama;
        }

        /// <summary>
        /// Obtiene resumen completo de un libro
        /// </summary>
        public async Task<BookSummary> GetSummaryAsync(string bookTitle, string author = null)
        {
            try
            {
                Log($"📝 Generando resumen: {bookTitle}");

                var prompt = $@"
Libro: ""{bookTitle}""
{(author != null ? $"Autor: {author}" : "")}

Genera información completa en formato JSON:
{{
  ""title"": ""Título completo"",
  ""author"": ""Autor completo"",
  ""summary"": ""Resumen en 50 palabras"",
  ""themes"": [""tema1"", ""tema2"", ""tema3""],
  ""style"": ""Estilo literario"",
  ""audience"": ""Público recomendado"",
  ""similarBooks"": [
    {{""title"": ""Libro similar 1"", ""author"": ""Autor""}},
    {{""title"": ""Libro similar 2"", ""author"": ""Autor""}}
  ],
  ""year"": 1967,
  ""pages"": 471,
  ""rating"": 9.5
}}
";

                var response = await ollama.GetCompletionAsync(prompt, temperature: 0.5);
                
                if (string.IsNullOrEmpty(response))
                    return null;

                var summary = ParseSummary(response);
                Log($"✅ Resumen generado: {summary.Summary?.Substring(0, Math.Min(50, summary.Summary?.Length ?? 0))}...");
                
                return summary;
            }
            catch (Exception ex)
            {
                Log($"❌ Error en GetSummaryAsync: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Genera descripción corta para vista previa
        /// </summary>
        public async Task<string> GetQuickDescriptionAsync(string bookTitle)
        {
            try
            {
                var prompt = $@"
Describe en UNA línea (máximo 20 palabras) de qué trata: ""{bookTitle}""
";

                var response = await ollama.GetCompletionAsync(prompt, temperature: 0.3);
                return response?.Trim();
            }
            catch (Exception ex)
            {
                Log($"❌ Error en GetQuickDescriptionAsync: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Analiza si un libro es apropiado para un usuario
        /// </summary>
        public async Task<ContentRating> AnalyzeContentAsync(string bookTitle, string userAge = null)
        {
            try
            {
                var prompt = $@"
Libro: ""{bookTitle}""
{(userAge != null ? $"Edad del usuario: {userAge}" : "")}

Analiza el contenido en formato JSON:
{{
  ""ageRating"": ""13+"",
  ""contentWarnings"": [""violencia"", ""lenguaje adulto""],
  ""appropriate"": true,
  ""reasoning"": ""Explicación breve""
}}
";

                var response = await ollama.GetCompletionAsync(prompt, temperature: 0.3);
                
                if (string.IsNullOrEmpty(response))
                    return null;

                return ParseContentRating(response);
            }
            catch (Exception ex)
            {
                Log($"❌ Error en AnalyzeContentAsync: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Compara dos libros
        /// </summary>
        public async Task<BookComparison> CompareBooksAsync(string book1, string book2)
        {
            try
            {
                var prompt = $@"
Compara estos dos libros:
1. ""{book1}""
2. ""{book2}""

Formato JSON:
{{
  ""similarities"": [""similitud1"", ""similitud2""],
  ""differences"": [""diferencia1"", ""diferencia2""],
  ""recommendation"": ""Cuál leer primero y por qué""
}}
";

                var response = await ollama.GetCompletionAsync(prompt, temperature: 0.5);
                
                if (string.IsNullOrEmpty(response))
                    return null;

                return ParseComparison(response);
            }
            catch (Exception ex)
            {
                Log($"❌ Error en CompareBooksAsync: {ex.Message}");
                return null;
            }
        }

        private BookSummary ParseSummary(string jsonResponse)
        {
            try
            {
                var startIndex = jsonResponse.IndexOf('{');
                var endIndex = jsonResponse.LastIndexOf('}');
                
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    var jsonText = jsonResponse.Substring(startIndex, endIndex - startIndex + 1);
                    return JsonSerializer.Deserialize<BookSummary>(jsonText);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private ContentRating ParseContentRating(string jsonResponse)
        {
            try
            {
                var startIndex = jsonResponse.IndexOf('{');
                var endIndex = jsonResponse.LastIndexOf('}');
                
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    var jsonText = jsonResponse.Substring(startIndex, endIndex - startIndex + 1);
                    return JsonSerializer.Deserialize<ContentRating>(jsonText);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private BookComparison ParseComparison(string jsonResponse)
        {
            try
            {
                var startIndex = jsonResponse.IndexOf('{');
                var endIndex = jsonResponse.LastIndexOf('}');
                
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    var jsonText = jsonResponse.Substring(startIndex, endIndex - startIndex + 1);
                    return JsonSerializer.Deserialize<BookComparison>(jsonText);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
    }

    public class BookSummary
    {
        public string Title { get; set; }
        public string Author { get; set; }
        public string Summary { get; set; }
        public string[] Themes { get; set; }
        public string Style { get; set; }
        public string Audience { get; set; }
        public SimilarBook[] SimilarBooks { get; set; }
        public int Year { get; set; }
        public int Pages { get; set; }
        public double Rating { get; set; }
    }

    public class SimilarBook
    {
        public string Title { get; set; }
        public string Author { get; set; }
    }

    public class ContentRating
    {
        public string AgeRating { get; set; }
        public string[] ContentWarnings { get; set; }
        public bool Appropriate { get; set; }
        public string Reasoning { get; set; }
    }

    public class BookComparison
    {
        public string[] Similarities { get; set; }
        public string[] Differences { get; set; }
        public string Recommendation { get; set; }
    }
}
