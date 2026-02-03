using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SlskDown.Core.AI
{
    public class SearchFilter
    {
        public List<string> Extensions { get; set; } = new List<string>();
        public string Language { get; set; }
        public long? MinSize { get; set; }
        public long? MaxSize { get; set; }
        public List<string> Authors { get; set; } = new List<string>();
        public List<string> ExcludeTerms { get; set; } = new List<string>();
        public DateTime? AfterDate { get; set; }
        public int? MinQuality { get; set; }
    }

    /// <summary>
    /// Generador de filtros complejos desde lenguaje natural
    /// </summary>
    public class ComplexFilterGenerator
    {
        public static SearchFilter ParseNaturalLanguageFilter(string input)
        {
            var filter = new SearchFilter();
            var lower = input.ToLower();

            // Detectar extensiones
            var extensionPatterns = new[] { "epub", "pdf", "mobi", "azw3", "txt", "doc", "docx" };
            foreach (var ext in extensionPatterns)
            {
                if (lower.Contains(ext))
                    filter.Extensions.Add(ext);
            }

            // Detectar idioma
            if (lower.Contains("español") || lower.Contains("spanish") || lower.Contains("castellano"))
                filter.Language = "español";
            else if (lower.Contains("inglés") || lower.Contains("english"))
                filter.Language = "inglés";
            else if (lower.Contains("francés") || lower.Contains("french"))
                filter.Language = "francés";

            // Detectar tamaño mínimo
            var minSizeMatch = Regex.Match(lower, @"(?:mayor|más|min|mínimo|minimum|greater|more)\s+(?:de|than|a)?\s*(\d+)\s*(mb|kb|gb)?");
            if (minSizeMatch.Success)
            {
                var value = long.Parse(minSizeMatch.Groups[1].Value);
                var unit = minSizeMatch.Groups[2].Value.ToLower();
                
                filter.MinSize = unit switch
                {
                    "kb" => value * 1024,
                    "mb" => value * 1024 * 1024,
                    "gb" => value * 1024 * 1024 * 1024,
                    _ => value * 1024 * 1024 // Default MB
                };
            }

            // Detectar tamaño máximo
            var maxSizeMatch = Regex.Match(lower, @"(?:menor|menos|max|máximo|maximum|less|smaller)\s+(?:de|than|a)?\s*(\d+)\s*(mb|kb|gb)?");
            if (maxSizeMatch.Success)
            {
                var value = long.Parse(maxSizeMatch.Groups[1].Value);
                var unit = maxSizeMatch.Groups[2].Value.ToLower();
                
                filter.MaxSize = unit switch
                {
                    "kb" => value * 1024,
                    "mb" => value * 1024 * 1024,
                    "gb" => value * 1024 * 1024 * 1024,
                    _ => value * 1024 * 1024
                };
            }

            // Detectar autores
            var authorMatch = Regex.Match(input, @"(?:de|by|autor|author):\s*([^,\n]+)", RegexOptions.IgnoreCase);
            if (authorMatch.Success)
            {
                filter.Authors.Add(authorMatch.Groups[1].Value.Trim());
            }

            // Detectar múltiples autores
            var authorsMatch = Regex.Match(input, @"(?:autores|authors):\s*([^\n]+)", RegexOptions.IgnoreCase);
            if (authorsMatch.Success)
            {
                var authors = authorsMatch.Groups[1].Value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                filter.Authors.AddRange(authors.Select(a => a.Trim()));
            }

            // Detectar exclusiones
            var excludeMatch = Regex.Match(input, @"(?:excepto|except|sin|without|no):\s*([^\n]+)", RegexOptions.IgnoreCase);
            if (excludeMatch.Success)
            {
                var terms = excludeMatch.Groups[1].Value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                filter.ExcludeTerms.AddRange(terms.Select(t => t.Trim()));
            }

            // Detectar fecha
            if (lower.Contains("últimos") || lower.Contains("last") || lower.Contains("recent"))
            {
                var yearMatch = Regex.Match(lower, @"(\d+)\s*(?:años|years|año|year)");
                if (yearMatch.Success)
                {
                    var years = int.Parse(yearMatch.Groups[1].Value);
                    filter.AfterDate = DateTime.Now.AddYears(-years);
                }
            }

            // Detectar calidad
            if (lower.Contains("alta calidad") || lower.Contains("high quality") || lower.Contains("mejor"))
                filter.MinQuality = 7;
            else if (lower.Contains("calidad media") || lower.Contains("medium quality"))
                filter.MinQuality = 5;

            return filter;
        }

        public static string GenerateFilterDescription(SearchFilter filter)
        {
            var parts = new List<string>();

            if (filter.Extensions.Count > 0)
                parts.Add($"Formato: {string.Join(", ", filter.Extensions.Select(e => e.ToUpper()))}");

            if (!string.IsNullOrEmpty(filter.Language))
                parts.Add($"Idioma: {filter.Language}");

            if (filter.MinSize.HasValue)
                parts.Add($"Tamaño mínimo: {FormatSize(filter.MinSize.Value)}");

            if (filter.MaxSize.HasValue)
                parts.Add($"Tamaño máximo: {FormatSize(filter.MaxSize.Value)}");

            if (filter.Authors.Count > 0)
                parts.Add($"Autores: {string.Join(", ", filter.Authors)}");

            if (filter.ExcludeTerms.Count > 0)
                parts.Add($"Excluir: {string.Join(", ", filter.ExcludeTerms)}");

            if (filter.AfterDate.HasValue)
                parts.Add($"Después de: {filter.AfterDate.Value:yyyy}");

            if (filter.MinQuality.HasValue)
                parts.Add($"Calidad mínima: {filter.MinQuality}/10");

            return parts.Count > 0 
                ? "🔍 FILTROS APLICADOS:\n  • " + string.Join("\n  • ", parts)
                : "Sin filtros aplicados";
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }

        public static bool MatchesFilter(dynamic file, SearchFilter filter)
        {
            // Verificar extensión
            if (filter.Extensions.Count > 0)
            {
                var ext = System.IO.Path.GetExtension(file.FileName?.ToString() ?? "").TrimStart('.').ToLower();
                if (!filter.Extensions.Contains(ext))
                    return false;
            }

            // Verificar tamaño
            long fileSize = file.SizeBytes ?? file.Size ?? 0;
            if (filter.MinSize.HasValue && fileSize < filter.MinSize.Value)
                return false;
            if (filter.MaxSize.HasValue && fileSize > filter.MaxSize.Value)
                return false;

            // Verificar autor
            if (filter.Authors.Count > 0)
            {
                var username = file.Username?.ToString() ?? "";
                if (!filter.Authors.Any(a => username.Contains(a, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }

            // Verificar exclusiones
            if (filter.ExcludeTerms.Count > 0)
            {
                var filename = file.FileName?.ToString() ?? "";
                if (filter.ExcludeTerms.Any(t => filename.Contains(t, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }

            return true;
        }
    }
}
