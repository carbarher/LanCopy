using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core
{
    public class SemanticConcept
    {
        public string Concept { get; set; }
        public List<string> Keywords { get; set; } = new List<string>();
        public List<string> Authors { get; set; } = new List<string>();
        public List<string> Titles { get; set; } = new List<string>();
    }

    public static class SemanticSearch
    {
        private static readonly Dictionary<string, SemanticConcept> concepts = new()
        {
            ["viajes en el tiempo"] = new SemanticConcept
            {
                Concept = "viajes en el tiempo",
                Keywords = new List<string> { "tiempo", "temporal", "time travel", "paradoja", "cronoviaje" },
                Authors = new List<string> { "H.G. Wells", "Isaac Asimov", "Connie Willis", "Audrey Niffenegger", "Ray Bradbury" },
                Titles = new List<string> { "La máquina del tiempo", "El fin de la eternidad", "Doomsday Book", "La esposa del viajero" }
            },
            ["space opera"] = new SemanticConcept
            {
                Concept = "space opera",
                Keywords = new List<string> { "espacio", "galaxia", "imperio", "nave espacial", "guerra espacial" },
                Authors = new List<string> { "Frank Herbert", "Isaac Asimov", "Dan Simmons", "Iain M. Banks", "Alastair Reynolds" },
                Titles = new List<string> { "Dune", "Fundación", "Hyperion", "Consider Phlebas" }
            },
            ["cyberpunk"] = new SemanticConcept
            {
                Concept = "cyberpunk",
                Keywords = new List<string> { "cibernético", "hacker", "realidad virtual", "distopía", "tecnología" },
                Authors = new List<string> { "William Gibson", "Neal Stephenson", "Philip K. Dick", "Bruce Sterling" },
                Titles = new List<string> { "Neuromante", "Snow Crash", "¿Sueñan los androides?", "Mirrorshades" }
            },
            ["fantasía épica"] = new SemanticConcept
            {
                Concept = "fantasía épica",
                Keywords = new List<string> { "magia", "dragones", "elfos", "reino", "espada", "hechicero" },
                Authors = new List<string> { "J.R.R. Tolkien", "George R.R. Martin", "Brandon Sanderson", "Patrick Rothfuss", "Robert Jordan" },
                Titles = new List<string> { "El Señor de los Anillos", "Canción de Hielo y Fuego", "El Nombre del Viento", "La Rueda del Tiempo" }
            },
            ["distopía"] = new SemanticConcept
            {
                Concept = "distopía",
                Keywords = new List<string> { "futuro oscuro", "totalitarismo", "control", "opresión", "sociedad" },
                Authors = new List<string> { "George Orwell", "Aldous Huxley", "Ray Bradbury", "Margaret Atwood", "Suzanne Collins" },
                Titles = new List<string> { "1984", "Un Mundo Feliz", "Fahrenheit 451", "El Cuento de la Criada" }
            },
            ["inteligencia artificial"] = new SemanticConcept
            {
                Concept = "inteligencia artificial",
                Keywords = new List<string> { "IA", "robot", "androide", "consciencia", "singularidad" },
                Authors = new List<string> { "Isaac Asimov", "Philip K. Dick", "Arthur C. Clarke", "Ian McDonald" },
                Titles = new List<string> { "Yo, Robot", "¿Sueñan los androides?", "2001: Odisea del Espacio" }
            },
            ["primer contacto"] = new SemanticConcept
            {
                Concept = "primer contacto",
                Keywords = new List<string> { "alienígena", "extraterrestre", "contacto", "comunicación", "encuentro" },
                Authors = new List<string> { "Carl Sagan", "Arthur C. Clarke", "Ted Chiang", "Liu Cixin" },
                Titles = new List<string> { "Contacto", "Cita con Rama", "La Historia de tu Vida", "El Problema de los Tres Cuerpos" }
            },
            ["post-apocalíptico"] = new SemanticConcept
            {
                Concept = "post-apocalíptico",
                Keywords = new List<string> { "apocalipsis", "supervivencia", "ruinas", "fin del mundo", "devastación" },
                Authors = new List<string> { "Cormac McCarthy", "Emily St. John Mandel", "Stephen King", "Richard Matheson" },
                Titles = new List<string> { "La Carretera", "Estación Once", "El Misterio de Salem's Lot", "Soy Leyenda" }
            }
        };

        public static SemanticConcept FindConcept(string query)
        {
            var lowerQuery = query.ToLower();

            // Búsqueda exacta
            if (concepts.TryGetValue(lowerQuery, out var concept))
                return concept;

            // Búsqueda por palabras clave
            foreach (var kvp in concepts)
            {
                if (kvp.Value.Keywords.Any(k => lowerQuery.Contains(k.ToLower())))
                    return kvp.Value;
            }

            return null;
        }

        public static List<string> GetRecommendedAuthors(string concept)
        {
            var semanticConcept = FindConcept(concept);
            return semanticConcept?.Authors ?? new List<string>();
        }

        public static List<string> GetRecommendedTitles(string concept)
        {
            var semanticConcept = FindConcept(concept);
            return semanticConcept?.Titles ?? new List<string>();
        }

        public static string GenerateSemanticSearchSummary(string query)
        {
            var concept = FindConcept(query);
            if (concept == null)
                return null;

            var summary = new System.Text.StringBuilder();
            summary.AppendLine($"🔍 Búsqueda semántica: {concept.Concept}\n");
            summary.AppendLine("Autores relacionados:");
            foreach (var author in concept.Authors)
                summary.AppendLine($"  • {author}");
            
            summary.AppendLine("\nObras representativas:");
            foreach (var title in concept.Titles)
                summary.AppendLine($"  • {title}");

            return summary.ToString();
        }

        public static List<string> FindSimilarBooks(string bookTitle)
        {
            // Mapeo simple de libros similares
            var similarBooks = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Dune"] = new List<string> { "Hyperion", "Fundación", "El juego de Ender", "Neuromante" },
                ["Fundación"] = new List<string> { "Dune", "Hyperion", "La Guerra Interminable", "Cita con Rama" },
                ["1984"] = new List<string> { "Un Mundo Feliz", "Fahrenheit 451", "El Cuento de la Criada", "Nosotros" },
                ["El Señor de los Anillos"] = new List<string> { "Canción de Hielo y Fuego", "El Nombre del Viento", "La Rueda del Tiempo" },
                ["Neuromante"] = new List<string> { "Snow Crash", "Dune", "¿Sueñan los androides?", "Altered Carbon" }
            };

            foreach (var kvp in similarBooks)
            {
                if (bookTitle.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }

            return new List<string>();
        }

        public static string GenerateSimilarBooksSummary(string bookTitle)
        {
            var similar = FindSimilarBooks(bookTitle);
            if (similar.Count == 0)
                return null;

            var summary = new System.Text.StringBuilder();
            summary.AppendLine($"📚 Libros similares a '{bookTitle}':\n");
            
            for (int i = 0; i < similar.Count; i++)
            {
                var stars = new string('⭐', 5 - i); // Más estrellas = más similar
                summary.AppendLine($"{stars} {similar[i]}");
            }

            return summary.ToString();
        }
    }

    public class CompoundCommand
    {
        public List<string> Actions { get; set; } = new List<string>();
        public Dictionary<string, string> Filters { get; set; } = new Dictionary<string, string>();
        public string MainAction { get; set; }
    }

    public static class CompoundCommandParser
    {
        public static CompoundCommand Parse(string input)
        {
            var command = new CompoundCommand();
            var lowerInput = input.ToLower();

            // Detectar acciones
            if (lowerInput.Contains("busca") || lowerInput.Contains("search"))
                command.Actions.Add("search");
            
            if (lowerInput.Contains("descarga") || lowerInput.Contains("baja") || lowerInput.Contains("download"))
                command.Actions.Add("download");
            
            if (lowerInput.Contains("filtra") || lowerInput.Contains("filter"))
                command.Actions.Add("filter");

            // Detectar filtros
            if (lowerInput.Contains("solo") || lowerInput.Contains("only"))
            {
                if (lowerInput.Contains("epub"))
                    command.Filters["extension"] = "epub";
                if (lowerInput.Contains("pdf"))
                    command.Filters["extension"] = "pdf";
                if (lowerInput.Contains("español") || lowerInput.Contains("spanish"))
                    command.Filters["language"] = "español";
            }

            // Detectar tamaño
            var sizeMatch = System.Text.RegularExpressions.Regex.Match(input, @"(?:mayor|greater|más|more)\s+(?:de|than|a)\s+(\d+)\s*(mb|kb|gb)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (sizeMatch.Success)
            {
                command.Filters["minSize"] = sizeMatch.Groups[1].Value;
                command.Filters["sizeUnit"] = sizeMatch.Groups[2].Value.ToLower();
            }

            var maxSizeMatch = System.Text.RegularExpressions.Regex.Match(input, @"(?:menor|less|menos)\s+(?:de|than|a)\s+(\d+)\s*(mb|kb|gb)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (maxSizeMatch.Success)
            {
                command.Filters["maxSize"] = maxSizeMatch.Groups[1].Value;
                command.Filters["maxSizeUnit"] = maxSizeMatch.Groups[2].Value.ToLower();
            }

            // Detectar excepciones
            if (lowerInput.Contains("excepto") || lowerInput.Contains("except"))
            {
                var exceptMatch = System.Text.RegularExpressions.Regex.Match(input, @"(?:excepto|except)\s+(.+?)(?:\s+y\s+|\s*$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (exceptMatch.Success)
                    command.Filters["except"] = exceptMatch.Groups[1].Value.Trim();
            }

            command.MainAction = command.Actions.FirstOrDefault() ?? "unknown";
            return command;
        }

        public static string GenerateCommandSummary(CompoundCommand command)
        {
            var summary = new System.Text.StringBuilder();
            summary.AppendLine("🔧 Comando compuesto detectado:\n");
            
            summary.AppendLine("Acciones:");
            foreach (var action in command.Actions)
            {
                var actionName = action switch
                {
                    "search" => "🔍 Buscar",
                    "download" => "⬇️ Descargar",
                    "filter" => "🔎 Filtrar",
                    _ => action
                };
                summary.AppendLine($"  • {actionName}");
            }

            if (command.Filters.Count > 0)
            {
                summary.AppendLine("\nFiltros:");
                foreach (var filter in command.Filters)
                {
                    summary.AppendLine($"  • {filter.Key}: {filter.Value}");
                }
            }

            return summary.ToString();
        }
    }
}
