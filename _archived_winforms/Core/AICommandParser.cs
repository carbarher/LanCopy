using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SlskDown.Core
{
    /// <summary>
    /// Parser de comandos de IA para ejecutar acciones en SlskDown
    /// </summary>
    public class AICommandParser
    {
        public enum CommandType
        {
            None,
            Search,
            SearchAndDownload,
            AddAuthor,
            MassSearch,
            MassSearchAndDownload
        }

        public class ParsedCommand
        {
            public CommandType Type { get; set; }
            public string Query { get; set; }
            public string Author { get; set; }
            public List<string> Authors { get; set; } = new List<string>();
            public bool SpanishOnly { get; set; }
            public string Extension { get; set; }
            public bool AutoDownload { get; set; }
            public int MaxResults { get; set; } = 100;
            public string RawInput { get; set; }
        }

        private static readonly Dictionary<string, string[]> CommandPatterns = new()
        {
            ["download"] = new[] { "baja", "bajate", "descarga", "descargame", "download", "get" },
            ["search"] = new[] { "busca", "buscame", "encuentra", "search", "find" },
            ["add_author"] = new[] { "agrega", "añade", "add", "agregar autor", "añadir autor" },
            ["all_works"] = new[] { "todas las obras", "todo", "all works", "complete works", "obra completa" },
            ["spanish"] = new[] { "español", "castellano", "spanish", "en español", "en castellano" },
            ["english"] = new[] { "inglés", "english", "en inglés" }
        };

        public static ParsedCommand Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new ParsedCommand { Type = CommandType.None };

            var command = new ParsedCommand
            {
                RawInput = input,
                MaxResults = 100,
                SpanishOnly = false,
                AutoDownload = false
            };

            var lowerInput = input.ToLower();

            // Detectar tipo de comando
            bool isDownload = ContainsAny(lowerInput, CommandPatterns["download"]);
            bool isSearch = ContainsAny(lowerInput, CommandPatterns["search"]);
            bool isAllWorks = ContainsAny(lowerInput, CommandPatterns["all_works"]);

            // Detectar idioma
            command.SpanishOnly = ContainsAny(lowerInput, CommandPatterns["spanish"]);

            // Detectar extensión
            command.Extension = ExtractExtension(lowerInput);

            // Extraer autor(es)
            var multiAuthorMatch = Regex.Match(input, @"(?:autores?|escritores?)\s*:?\s*([\w\s\.,y]+?)(?:\s+en|\s+formato|$)", RegexOptions.IgnoreCase);
            if (multiAuthorMatch.Success)
            {
                var authorsText = multiAuthorMatch.Groups[1].Value;
                var authorsList = authorsText.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(a => a.Trim().Replace(" y ", "").Replace(" and ", "").Trim())
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .ToList();

                if (authorsList.Count > 1)
                {
                    command.Authors = authorsList;
                    command.Query = string.Join(", ", authorsList);
                }
                else if (authorsList.Count == 1)
                {
                    command.Author = authorsList[0];
                    command.Query = command.Author;
                }
            }
            else
            {
                // Detectar autor individual
                var authorMatch = Regex.Match(input, @"(?:de|autor|autora|escritor)\s+([\w\s\.]+?)(?:\s+en|\s+formato|$)", RegexOptions.IgnoreCase);
                if (authorMatch.Success)
                {
                    command.Author = authorMatch.Groups[1].Value.Trim();
                    command.Query = command.Author;
                }
            }

            // Determinar tipo de comando
            if (command.Authors.Count > 1)
            {
                // Búsqueda masiva de múltiples autores
                if (isDownload)
                {
                    command.Type = CommandType.MassSearchAndDownload;
                    command.AutoDownload = true;
                }
                else if (isSearch)
                {
                    command.Type = CommandType.MassSearch;
                }
            }
            else if (isDownload)
            {
                command.Type = CommandType.SearchAndDownload;
                command.AutoDownload = true;
            }
            else if (isSearch)
            {
                command.Type = CommandType.Search;
            }
            else if (ContainsAny(lowerInput, CommandPatterns["add_author"]))
            {
                command.Type = CommandType.AddAuthor;
            }

            return command;
        }

        private static bool ContainsAny(string text, string[] patterns)
        {
            return patterns.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
        }

        private static string ExtractAuthor(string input)
        {
            // Patrones para extraer nombres de autores
            var patterns = new[]
            {
                @"(?:obras?\s+de|works?\s+of|by)\s+([A-ZÁÉÍÓÚÑa-záéíóúñ\s\.]+?)(?:\s+en|\s+in|$)",
                @"(?:baja|descarga|busca)\s+(?:todas?\s+las?\s+obras?\s+de\s+)?([A-ZÁÉÍÓÚÑa-záéíóúñ\s\.]+?)(?:\s+en|\s+in|$)",
                @"([A-ZÁÉÍÓÚÑa-záéíóúñ][A-ZÁÉÍÓÚÑa-záéíóúñ\s\.]+?)(?:\s+en\s+español|\s+en\s+inglés|$)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var author = match.Groups[1].Value.Trim();

                    // Limpiar palabras comunes que no son parte del nombre
                    var stopWords = new[] { "todas", "las", "obras", "de", "en", "español", "inglés", "all", "works", "of", "in", "spanish", "english" };
                    var words = author.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Where(w => !stopWords.Contains(w.ToLower()))
                        .ToArray();

                    if (words.Length > 0)
                        return string.Join(" ", words);
                }
            }

            return null;
        }

        private static string ExtractExtension(string input)
        {
            var extensions = new[] { "epub", "pdf", "mobi", "azw3", "txt", "doc", "docx" };

            foreach (var ext in extensions)
            {
                if (input.Contains(ext, StringComparison.OrdinalIgnoreCase))
                    return ext;
            }

            return null;
        }

        public static string GenerateConfirmationMessage(ParsedCommand command)
        {
            if (command.Type == CommandType.None)
                return null;

            var message = new System.Text.StringBuilder();

            switch (command.Type)
            {
                case CommandType.MassSearch:
                    message.AppendLine($"🔍 Búsqueda masiva de {command.Authors.Count} autores:");
                    foreach (var author in command.Authors)
                        message.AppendLine($"   • {author}");
                    break;

                case CommandType.MassSearchAndDownload:
                    message.AppendLine($"🔍 Buscar y descargar obras de {command.Authors.Count} autores:");
                    foreach (var author in command.Authors)
                        message.AppendLine($"   • {author}");
                    break;

                case CommandType.SearchAndDownload:
                    message.AppendLine($"🔍 Buscar y descargar obras de: {command.Author ?? command.Query}");
                    break;

                case CommandType.Search:
                    message.AppendLine($"🔍 Buscar obras de: {command.Author ?? command.Query}");
                    break;

                case CommandType.AddAuthor:
                    message.AppendLine($"➕ Agregar autor: {command.Author}");
                    break;
            }

            if (command.SpanishOnly)
                message.AppendLine("🇪🇸 Solo en español");

            if (!string.IsNullOrEmpty(command.Extension))
                message.AppendLine($"📄 Formato: {command.Extension.ToUpper()}");

            if (command.AutoDownload)
                message.AppendLine("⬇️ Descarga automática activada");

            message.AppendLine($"📊 Máximo: {command.MaxResults} resultados");

            return message.ToString();
        }

        public static string GenerateSystemPrompt()
        {
            return @"Eres un asistente de IA integrado en SlskDown, una aplicación para buscar y descargar libros.

COMANDOS QUE PUEDES EJECUTAR:
1. Búsqueda y descarga: ""bájate todas las obras de [autor] en español""
2. Solo búsqueda: ""busca libros de [autor]""
3. Agregar autor: ""agrega a [autor] a la lista de búsqueda automática""

EJEMPLOS:
- ""bájate todas las obras de Julio Verne en español""
- ""descarga los libros de Isaac Asimov en formato epub""
- ""busca novelas de Gabriel García Márquez""
- ""agrega a Jorge Luis Borges a la lista""

Cuando detectes un comando:
1. Confirma lo que entendiste
2. Pregunta si debe ejecutarse
3. Informa sobre el progreso

Para preguntas generales sobre literatura, responde normalmente sin ejecutar comandos.";
        }
    }
}
