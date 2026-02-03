using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core.AI
{
    public enum UserIntention
    {
        Search,
        Download,
        GetStatus,
        GetHelp,
        Configure,
        Analyze,
        Compare,
        AskQuestion,
        FollowUp,
        Clarification,
        Unknown
    }

    public class DetectedIntention
    {
        public UserIntention Primary { get; set; }
        public List<UserIntention> Secondary { get; set; } = new List<UserIntention>();
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
        public double Confidence { get; set; }
        public bool NeedsClarification { get; set; }
        public List<string> ClarificationQuestions { get; set; } = new List<string>();
    }

    /// <summary>
    /// Detector de intenciones complejas del usuario
    /// </summary>
    public class IntentionDetector
    {
        public static DetectedIntention DetectIntention(string message, List<string> conversationHistory = null)
        {
            var intention = new DetectedIntention { Confidence = 0.5 };
            var lower = message.ToLower();

            // Detectar búsqueda
            if (ContainsAny(lower, "busca", "search", "encuentra", "find", "quiero libros"))
            {
                intention.Primary = UserIntention.Search;
                intention.Confidence = 0.9;
                ExtractSearchParameters(message, intention);
            }
            // Detectar descarga
            else if (ContainsAny(lower, "descarga", "download", "bájate", "baja"))
            {
                intention.Primary = UserIntention.Download;
                intention.Confidence = 0.9;
                ExtractDownloadParameters(message, intention);
            }
            // Detectar estado
            else if (ContainsAny(lower, "estado", "status", "cómo va", "progreso"))
            {
                intention.Primary = UserIntention.GetStatus;
                intention.Confidence = 1.0;
            }
            // Detectar ayuda
            else if (ContainsAny(lower, "ayuda", "help", "cómo", "how"))
            {
                intention.Primary = UserIntention.GetHelp;
                intention.Confidence = 0.9;
            }
            // Detectar análisis
            else if (ContainsAny(lower, "analiza", "analyze", "compara", "compare", "muestra", "show"))
            {
                intention.Primary = UserIntention.Analyze;
                intention.Confidence = 0.8;
            }
            // Detectar seguimiento
            else if (ContainsAny(lower, "también", "además", "y", "otro", "más"))
            {
                intention.Primary = UserIntention.FollowUp;
                intention.Confidence = 0.7;
                intention.NeedsClarification = true;
                intention.ClarificationQuestions.Add("¿Te refieres a continuar con la acción anterior?");
            }
            // Detectar pregunta
            else if (lower.Contains("?") || lower.StartsWith("qué") || lower.StartsWith("por qué"))
            {
                intention.Primary = UserIntention.AskQuestion;
                intention.Confidence = 0.8;
            }
            else
            {
                intention.Primary = UserIntention.Unknown;
                intention.Confidence = 0.3;
                intention.NeedsClarification = true;
                intention.ClarificationQuestions.Add("No estoy seguro de qué quieres hacer. ¿Podrías ser más específico?");
            }

            // Detectar intenciones secundarias
            DetectSecondaryIntentions(lower, intention);

            return intention;
        }

        private static void ExtractSearchParameters(string message, DetectedIntention intention)
        {
            var lower = message.ToLower();

            // Extraer autor
            var authorPatterns = new[] { "de ", "by ", "autor ", "author " };
            foreach (var pattern in authorPatterns)
            {
                var index = lower.IndexOf(pattern);
                if (index >= 0)
                {
                    var afterPattern = message.Substring(index + pattern.Length);
                    var author = afterPattern.Split(new[] { ' ', ',', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (!string.IsNullOrEmpty(author))
                        intention.Parameters["author"] = author;
                }
            }

            // Extraer formato
            var formats = new[] { "epub", "pdf", "mobi", "azw3" };
            foreach (var format in formats)
            {
                if (lower.Contains(format))
                    intention.Parameters["format"] = format;
            }

            // Extraer idioma
            if (lower.Contains("español") || lower.Contains("spanish"))
                intention.Parameters["language"] = "español";
            else if (lower.Contains("inglés") || lower.Contains("english"))
                intention.Parameters["language"] = "inglés";
        }

        private static void ExtractDownloadParameters(string message, DetectedIntention intention)
        {
            ExtractSearchParameters(message, intention); // Mismos parámetros

            // Detectar si es descarga masiva
            if (ContainsAny(message.ToLower(), "todo", "all", "todos", "completo"))
                intention.Parameters["mass_download"] = "true";
        }

        private static void DetectSecondaryIntentions(string lower, DetectedIntention intention)
        {
            // Detectar filtrado como intención secundaria
            if (ContainsAny(lower, "solo", "only", "únicamente", "filtra"))
                intention.Secondary.Add(UserIntention.Configure);

            // Detectar comparación como secundaria
            if (ContainsAny(lower, "mejor", "best", "compara", "compare"))
                intention.Secondary.Add(UserIntention.Compare);
        }

        private static bool ContainsAny(string text, params string[] keywords)
        {
            return keywords.Any(k => text.Contains(k));
        }

        public static string GenerateIntentionSummary(DetectedIntention intention)
        {
            var summary = $"🎯 Intención detectada: {GetIntentionName(intention.Primary)} (confianza: {intention.Confidence:P0})\n";

            if (intention.Parameters.Count > 0)
            {
                summary += "\n📋 Parámetros:\n";
                foreach (var param in intention.Parameters)
                {
                    summary += $"  • {param.Key}: {param.Value}\n";
                }
            }

            if (intention.Secondary.Count > 0)
            {
                summary += $"\n🔄 Intenciones adicionales: {string.Join(", ", intention.Secondary.Select(GetIntentionName))}\n";
            }

            if (intention.NeedsClarification)
            {
                summary += "\n❓ Necesito aclaración:\n";
                foreach (var question in intention.ClarificationQuestions)
                {
                    summary += $"  • {question}\n";
                }
            }

            return summary;
        }

        private static string GetIntentionName(UserIntention intention)
        {
            return intention switch
            {
                UserIntention.Search => "Búsqueda",
                UserIntention.Download => "Descarga",
                UserIntention.GetStatus => "Consultar estado",
                UserIntention.GetHelp => "Solicitar ayuda",
                UserIntention.Configure => "Configurar",
                UserIntention.Analyze => "Analizar",
                UserIntention.Compare => "Comparar",
                UserIntention.AskQuestion => "Pregunta",
                UserIntention.FollowUp => "Seguimiento",
                UserIntention.Clarification => "Aclaración",
                _ => "Desconocida"
            };
        }
    }
}
