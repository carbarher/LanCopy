using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SlskDown.AI
{
    /// <summary>
    /// FUNCIONALIDAD #2: Sistema de Recomendaciones Inteligentes
    /// </summary>
    public class AIRecommender
    {
        private readonly OllamaClient ollamaClient;
        private readonly bool enabled;

        public AIRecommender(OllamaClient client, bool enabled = true)
        {
            this.ollamaClient = client;
            this.enabled = enabled;
        }

        /// <summary>
        /// Genera recomendaciones basadas en historial de descargas
        /// </summary>
        public async Task<List<Recommendation>> GetRecommendationsAsync(List<string> downloadHistory, int maxRecommendations = 10)
        {
            if (!enabled || downloadHistory.Count == 0)
                return new List<Recommendation>();

            try
            {
                var prompt = BuildRecommendationPrompt(downloadHistory, maxRecommendations);
                var response = await ollamaClient.GenerateAsync(prompt);
                return ParseRecommendations(response);
            }
            catch (Exception ex)
            {
                return new List<Recommendation> 
                { 
                    new Recommendation { Error = ex.Message } 
                };
            }
        }

        /// <summary>
        /// Recomienda archivos similares a uno específico
        /// </summary>
        public async Task<List<Recommendation>> GetSimilarFilesAsync(string fileName, string author = null, int maxResults = 5)
        {
            if (!enabled)
                return new List<Recommendation>();

            try
            {
                var prompt = $@"Basándote en este archivo:
Nombre: {fileName}
{(author != null ? $"Autor: {author}" : "")}

Recomienda {maxResults} archivos similares que un usuario podría estar interesado en descargar.
Para cada recomendación proporciona:
- Título/nombre del archivo
- Autor (si aplica)
- Razón de la recomendación
- Puntuación de similitud (0-10)

Formato de respuesta:
TITULO: [título]
AUTOR: [autor]
RAZON: [razón]
SCORE: [puntuación]
---";

                var response = await ollamaClient.GenerateAsync(prompt);
                return ParseRecommendations(response);
            }
            catch
            {
                return new List<Recommendation>();
            }
        }

        /// <summary>
        /// Sugiere autores relacionados
        /// </summary>
        public async Task<List<string>> SuggestRelatedAuthorsAsync(List<string> currentAuthors, int maxSuggestions = 5)
        {
            if (!enabled || currentAuthors.Count == 0)
                return new List<string>();

            try
            {
                var prompt = $@"Basándote en estos autores que el usuario ha descargado:
{string.Join(", ", currentAuthors)}

Sugiere {maxSuggestions} autores similares o relacionados que podrían interesarle.
Responde SOLO con los nombres de los autores, uno por línea.";

                var response = await ollamaClient.GenerateAsync(prompt);
                return response.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Take(maxSuggestions)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Detecta patrones de interés del usuario
        /// </summary>
        public async Task<UserInterestProfile> AnalyzeUserInterestsAsync(List<string> downloadHistory)
        {
            if (!enabled || downloadHistory.Count == 0)
                return new UserInterestProfile();

            try
            {
                var prompt = $@"Analiza este historial de descargas y detecta los patrones de interés del usuario:

{string.Join("\n", downloadHistory.Take(50))}

Identifica:
1. Géneros principales (máximo 5)
2. Temas recurrentes (máximo 5)
3. Autores favoritos (máximo 5)
4. Idiomas preferidos
5. Nivel de complejidad preferido (básico, intermedio, avanzado)

Formato:
GENEROS: [género1, género2, ...]
TEMAS: [tema1, tema2, ...]
AUTORES: [autor1, autor2, ...]
IDIOMAS: [idioma1, idioma2, ...]
NIVEL: [nivel]";

                var response = await ollamaClient.GenerateAsync(prompt);
                return ParseUserProfile(response);
            }
            catch
            {
                return new UserInterestProfile();
            }
        }

        private string BuildRecommendationPrompt(List<string> history, int maxRecommendations)
        {
            var recentHistory = history.TakeLast(20).ToList();
            return $@"Basándote en este historial de descargas recientes del usuario:

{string.Join("\n", recentHistory)}

Genera {maxRecommendations} recomendaciones personalizadas de archivos que podrían interesarle.
Para cada recomendación proporciona:
- Título/nombre sugerido
- Autor (si aplica)
- Razón de la recomendación
- Puntuación de relevancia (0-10)

Formato de respuesta:
TITULO: [título]
AUTOR: [autor]
RAZON: [razón]
SCORE: [puntuación]
---";
        }

        private List<Recommendation> ParseRecommendations(string response)
        {
            var recommendations = new List<Recommendation>();
            var blocks = response.Split("---", StringSplitOptions.RemoveEmptyEntries);

            foreach (var block in blocks)
            {
                var rec = new Recommendation();
                var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    if (line.StartsWith("TITULO:", StringComparison.OrdinalIgnoreCase))
                        rec.Title = line.Substring(7).Trim();
                    else if (line.StartsWith("AUTOR:", StringComparison.OrdinalIgnoreCase))
                        rec.Author = line.Substring(6).Trim();
                    else if (line.StartsWith("RAZON:", StringComparison.OrdinalIgnoreCase))
                        rec.Reason = line.Substring(6).Trim();
                    else if (line.StartsWith("SCORE:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (double.TryParse(line.Substring(6).Trim(), out var score))
                            rec.RelevanceScore = score;
                    }
                }

                if (!string.IsNullOrEmpty(rec.Title))
                    recommendations.Add(rec);
            }

            return recommendations.OrderByDescending(r => r.RelevanceScore).ToList();
        }

        private UserInterestProfile ParseUserProfile(string response)
        {
            var profile = new UserInterestProfile();
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.StartsWith("GENEROS:", StringComparison.OrdinalIgnoreCase))
                {
                    profile.PreferredGenres = line.Substring(8).Split(',')
                        .Select(g => g.Trim())
                        .Where(g => !string.IsNullOrWhiteSpace(g))
                        .ToList();
                }
                else if (line.StartsWith("TEMAS:", StringComparison.OrdinalIgnoreCase))
                {
                    profile.RecurringThemes = line.Substring(6).Split(',')
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .ToList();
                }
                else if (line.StartsWith("AUTORES:", StringComparison.OrdinalIgnoreCase))
                {
                    profile.FavoriteAuthors = line.Substring(8).Split(',')
                        .Select(a => a.Trim())
                        .Where(a => !string.IsNullOrWhiteSpace(a))
                        .ToList();
                }
                else if (line.StartsWith("IDIOMAS:", StringComparison.OrdinalIgnoreCase))
                {
                    profile.PreferredLanguages = line.Substring(8).Split(',')
                        .Select(l => l.Trim())
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .ToList();
                }
                else if (line.StartsWith("NIVEL:", StringComparison.OrdinalIgnoreCase))
                {
                    profile.ComplexityLevel = line.Substring(6).Trim();
                }
            }

            return profile;
        }
    }

    public class Recommendation
    {
        public string Title { get; set; }
        public string Author { get; set; }
        public string Reason { get; set; }
        public double RelevanceScore { get; set; }
        public string Error { get; set; }
    }

    public class UserInterestProfile
    {
        public List<string> PreferredGenres { get; set; } = new List<string>();
        public List<string> RecurringThemes { get; set; } = new List<string>();
        public List<string> FavoriteAuthors { get; set; } = new List<string>();
        public List<string> PreferredLanguages { get; set; } = new List<string>();
        public string ComplexityLevel { get; set; }
    }
}
