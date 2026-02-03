using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SlskDown.Core.AI
{
    public enum ExpertiseLevel
    {
        Beginner,
        Intermediate,
        Expert
    }

    /// <summary>
    /// Asistente inteligente con múltiples capacidades avanzadas
    /// </summary>
    public class SmartAssistant
    {
        private ExpertiseLevel userLevel = ExpertiseLevel.Intermediate;
        private int failedSearchCount = 0;
        private DateTime lastHelpOffer = DateTime.MinValue;
        private Dictionary<string, string> userPreferences = new Dictionary<string, string>();

        // Traducción de autores
        private static readonly Dictionary<string, List<string>> authorTranslations = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Julio Verne"] = new List<string> { "Jules Verne", "J. Verne" },
            ["Isaac Asimov"] = new List<string> { "I. Asimov", "Asimov" },
            ["Arthur C. Clarke"] = new List<string> { "A.C. Clarke", "Clarke" },
            ["Ray Bradbury"] = new List<string> { "R. Bradbury", "Bradbury" },
            ["Philip K. Dick"] = new List<string> { "P.K. Dick", "Dick" },
            ["H.G. Wells"] = new List<string> { "Herbert George Wells", "Wells" }
        };

        public void SetExpertiseLevel(ExpertiseLevel level)
        {
            userLevel = level;
        }

        public string FormatMessage(string message, string messageType = "info")
        {
            if (userLevel == ExpertiseLevel.Beginner)
            {
                // Mensajes simples y claros
                return FormatBeginnerMessage(message, messageType);
            }
            else if (userLevel == ExpertiseLevel.Expert)
            {
                // Mensajes técnicos detallados
                return FormatExpertMessage(message, messageType);
            }
            else
            {
                // Balance entre claridad y detalle
                return message;
            }
        }

        private string FormatBeginnerMessage(string message, string messageType)
        {
            var sb = new StringBuilder();
            
            // Agregar emoji según tipo
            var emoji = messageType switch
            {
                "error" => "❌",
                "success" => "✅",
                "warning" => "⚠️",
                "info" => "ℹ️",
                _ => "💬"
            };

            sb.AppendLine($"{emoji} {message}");

            // Agregar ayuda contextual para principiantes
            if (messageType == "error")
            {
                sb.AppendLine("\n💡 Tip: Escribe 'ayuda' si necesitas asistencia.");
            }

            return sb.ToString();
        }

        private string FormatExpertMessage(string message, string messageType)
        {
            // Incluir detalles técnicos
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            return $"[{timestamp}] {message}";
        }

        public void RecordFailedSearch()
        {
            failedSearchCount++;
        }

        public void ResetFailedSearchCount()
        {
            failedSearchCount = 0;
        }

        public string CheckAndOfferHelp()
        {
            // Ofrecer ayuda si hay múltiples búsquedas fallidas
            if (failedSearchCount >= 3 && (DateTime.Now - lastHelpOffer).TotalMinutes > 5)
            {
                lastHelpOffer = DateTime.Now;
                failedSearchCount = 0;

                return "🤔 Veo que no encuentras resultados. Algunas sugerencias:\n" +
                       "  • Prueba con términos más generales\n" +
                       "  • Verifica la ortografía\n" +
                       "  • Intenta buscar en otro idioma\n" +
                       "  • Usa sinónimos o nombres alternativos\n\n" +
                       "¿Quieres que te ayude a optimizar tu búsqueda?";
            }

            return null;
        }

        public List<string> TranslateAuthorName(string author)
        {
            var variations = new List<string> { author };

            // Buscar traducciones conocidas
            foreach (var kvp in authorTranslations)
            {
                if (kvp.Key.Equals(author, StringComparison.OrdinalIgnoreCase) ||
                    kvp.Value.Any(v => v.Equals(author, StringComparison.OrdinalIgnoreCase)))
                {
                    variations.Add(kvp.Key);
                    variations.AddRange(kvp.Value);
                }
            }

            return variations.Distinct().ToList();
        }

        public string GenerateOptimizedQuery(string originalQuery)
        {
            var optimized = new StringBuilder();
            optimized.AppendLine($"🔍 CONSULTA OPTIMIZADA:\n");
            optimized.AppendLine($"Original: \"{originalQuery}\"\n");

            // Detectar autor y expandir
            var words = originalQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var possibleAuthor = string.Join(" ", words.Take(2));
            var translations = TranslateAuthorName(possibleAuthor);

            if (translations.Count > 1)
            {
                optimized.AppendLine("Variaciones del autor:");
                foreach (var variant in translations)
                {
                    optimized.AppendLine($"  • {variant}");
                }
                optimized.AppendLine();
            }

            // Sugerir sinónimos
            var synonyms = GetSynonyms(originalQuery);
            if (synonyms.Count > 0)
            {
                optimized.AppendLine("Términos relacionados:");
                foreach (var synonym in synonyms)
                {
                    optimized.AppendLine($"  • {synonym}");
                }
            }

            return optimized.ToString();
        }

        private List<string> GetSynonyms(string term)
        {
            var synonymMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["libro"] = new List<string> { "book", "ebook", "novel", "novela" },
                ["ciencia ficción"] = new List<string> { "sci-fi", "scifi", "science fiction", "sf" },
                ["fantasía"] = new List<string> { "fantasy", "fantasia" },
                ["novela"] = new List<string> { "novel", "book", "libro" }
            };

            var results = new List<string>();
            foreach (var kvp in synonymMap)
            {
                if (term.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    results.AddRange(kvp.Value);
                }
            }

            return results.Distinct().ToList();
        }

        public void LearnPreference(string key, string value)
        {
            userPreferences[key] = value;
        }

        public string GetPreference(string key)
        {
            return userPreferences.GetValueOrDefault(key);
        }

        public Dictionary<string, string> GetAllPreferences()
        {
            return new Dictionary<string, string>(userPreferences);
        }

        public string GenerateReadingList(string theme, int count = 10)
        {
            var lists = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["ciencia ficción clásica"] = new List<string>
                {
                    "Fundación - Isaac Asimov",
                    "1984 - George Orwell",
                    "Un Mundo Feliz - Aldous Huxley",
                    "Fahrenheit 451 - Ray Bradbury",
                    "Dune - Frank Herbert",
                    "La Guerra de los Mundos - H.G. Wells",
                    "2001: Odisea del Espacio - Arthur C. Clarke",
                    "¿Sueñan los androides? - Philip K. Dick",
                    "Crónicas Marcianas - Ray Bradbury",
                    "El Fin de la Eternidad - Isaac Asimov"
                },
                ["fantasía épica"] = new List<string>
                {
                    "El Señor de los Anillos - J.R.R. Tolkien",
                    "Canción de Hielo y Fuego - George R.R. Martin",
                    "El Nombre del Viento - Patrick Rothfuss",
                    "La Rueda del Tiempo - Robert Jordan",
                    "Elantris - Brandon Sanderson",
                    "El Hobbit - J.R.R. Tolkien",
                    "Nacidos de la Bruma - Brandon Sanderson",
                    "La Primera Ley - Joe Abercrombie",
                    "Crónicas de la Torre - Laura Gallego",
                    "El Ciclo de la Puerta de la Muerte - Margaret Weis"
                }
            };

            if (lists.TryGetValue(theme, out var books))
            {
                var sb = new StringBuilder();
                sb.AppendLine($"📚 LISTA DE LECTURA: {theme.ToUpper()}\n");
                
                for (int i = 0; i < Math.Min(count, books.Count); i++)
                {
                    sb.AppendLine($"{i + 1}. {books[i]}");
                }

                return sb.ToString();
            }

            return $"No tengo una lista predefinida para '{theme}'. ¿Quieres que busque libros sobre ese tema?";
        }
    }
}
