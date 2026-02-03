using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SlskDown.Core
{
    /// <summary>
    /// MEJORA #4 y #5: Mejoras de búsqueda inspiradas en Nicotine+ 3.3.0
    /// - Generic File Type Filters
    /// - Phrase Searching con comillas
    /// </summary>
    public static class SearchEnhancements
    {
        // MEJORA #4: Generic File Type Categories
        public enum FileTypeCategory
        {
            All,
            Audio,      // .mp3, .flac, .wav, .ogg, .m4a, .aac, .opus, .wma
            Image,      // .jpg, .png, .gif, .bmp, .svg, .webp, .tiff, .ico
            Video,      // .mp4, .mkv, .avi, .mov, .wmv, .flv, .webm, .m4v
            Text,       // .txt, .pdf, .doc, .docx, .epub, .mobi, .azw, .azw3
            Archive,    // .zip, .rar, .7z, .tar, .gz, .bz2, .xz
            Executable  // .exe, .dll, .msi, .app, .dmg, .deb, .rpm
        }

        private static readonly Dictionary<FileTypeCategory, HashSet<string>> FileTypeExtensions = new()
        {
            [FileTypeCategory.Audio] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".mp3", ".flac", ".wav", ".ogg", ".m4a", ".aac", ".opus", ".wma", 
                ".ape", ".alac", ".aiff", ".dsf", ".dff", ".mka"
            },
            [FileTypeCategory.Image] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg", ".webp", ".tiff", 
                ".tif", ".ico", ".heic", ".heif", ".raw", ".cr2", ".nef"
            },
            [FileTypeCategory.Video] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v",
                ".mpg", ".mpeg", ".3gp", ".ogv", ".ts", ".m2ts"
            },
            [FileTypeCategory.Text] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".txt", ".pdf", ".doc", ".docx", ".epub", ".mobi", ".azw", ".azw3",
                ".rtf", ".odt", ".md", ".html", ".htm", ".xml", ".json"
            },
            [FileTypeCategory.Archive] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".lz", ".lzma",
                ".cab", ".iso", ".dmg"
            },
            [FileTypeCategory.Executable] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".exe", ".dll", ".msi", ".app", ".dmg", ".deb", ".rpm", ".apk",
                ".bat", ".sh", ".cmd", ".com"
            }
        };

        /// <summary>
        /// Determina la categoría de un archivo por su extensión
        /// </summary>
        public static FileTypeCategory GetFileTypeCategory(string filename)
        {
            var ext = System.IO.Path.GetExtension(filename);
            if (string.IsNullOrEmpty(ext)) return FileTypeCategory.All;

            foreach (var kvp in FileTypeExtensions)
            {
                if (kvp.Value.Contains(ext))
                    return kvp.Key;
            }

            return FileTypeCategory.All;
        }

        /// <summary>
        /// Verifica si un archivo pertenece a una categoría específica
        /// </summary>
        public static bool IsFileType(string filename, FileTypeCategory category)
        {
            if (category == FileTypeCategory.All) return true;
            return GetFileTypeCategory(filename) == category;
        }

        /// <summary>
        /// Filtra una lista de archivos por categoría
        /// </summary>
        public static IEnumerable<T> FilterByFileType<T>(
            IEnumerable<T> files, 
            FileTypeCategory category,
            Func<T, string> filenameSelector)
        {
            if (category == FileTypeCategory.All)
                return files;

            return files.Where(f => IsFileType(filenameSelector(f), category));
        }

        // MEJORA #5: Phrase Searching con comillas
        public class SearchQuery
        {
            public List<string> ExactPhrases { get; set; } = new();
            public List<string> Keywords { get; set; } = new();
            public FileTypeCategory FileType { get; set; } = FileTypeCategory.All;

            public bool IsEmpty => ExactPhrases.Count == 0 && Keywords.Count == 0;

            public override string ToString()
            {
                var parts = new List<string>();
                
                if (ExactPhrases.Count > 0)
                    parts.Add($"Frases: {string.Join(", ", ExactPhrases.Select(p => $"\"{p}\""))}");
                
                if (Keywords.Count > 0)
                    parts.Add($"Palabras: {string.Join(", ", Keywords)}");
                
                if (FileType != FileTypeCategory.All)
                    parts.Add($"Tipo: {FileType}");

                return string.Join(" | ", parts);
            }
        }

        private static readonly Regex QuotedPhraseRegex = new Regex(
            "\"([^\"]+)\"", 
            RegexOptions.Compiled
        );

        /// <summary>
        /// Parsea una query de búsqueda con soporte para frases exactas entre comillas
        /// Ejemplo: "pink floyd" dark side → busca frase exacta "pink floyd" Y palabras "dark" y "side"
        /// </summary>
        public static SearchQuery ParseSearchQuery(string query)
        {
            var result = new SearchQuery();

            if (string.IsNullOrWhiteSpace(query))
                return result;

            // Extraer frases entre comillas
            var matches = QuotedPhraseRegex.Matches(query);
            foreach (Match match in matches)
            {
                var phrase = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(phrase))
                    result.ExactPhrases.Add(phrase);
            }

            // Remover frases del query y obtener palabras sueltas
            var remainingQuery = QuotedPhraseRegex.Replace(query, " ");
            var words = remainingQuery
                .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim())
                .Where(w => !string.IsNullOrEmpty(w))
                .ToList();

            result.Keywords.AddRange(words);

            return result;
        }

        /// <summary>
        /// Verifica si un texto coincide con una query de búsqueda
        /// </summary>
        public static bool MatchesQuery(string text, SearchQuery query)
        {
            if (query.IsEmpty) return true;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var textLower = text.ToLowerInvariant();

            // Todas las frases exactas deben estar presentes
            foreach (var phrase in query.ExactPhrases)
            {
                if (!textLower.Contains(phrase.ToLowerInvariant()))
                    return false;
            }

            // Todas las palabras clave deben estar presentes
            foreach (var keyword in query.Keywords)
            {
                if (!textLower.Contains(keyword.ToLowerInvariant()))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Filtra una lista de archivos por query de búsqueda
        /// </summary>
        public static IEnumerable<T> FilterByQuery<T>(
            IEnumerable<T> files,
            SearchQuery query,
            Func<T, string> textSelector)
        {
            if (query.IsEmpty)
                return files;

            return files.Where(f => MatchesQuery(textSelector(f), query));
        }

        // MEJORA #4: Helpers para UI
        public static string GetFileTypeCategoryDisplayName(FileTypeCategory category)
        {
            return category switch
            {
                FileTypeCategory.All => "Todos",
                FileTypeCategory.Audio => "Audio",
                FileTypeCategory.Image => "Imagen",
                FileTypeCategory.Video => "Video",
                FileTypeCategory.Text => "Texto",
                FileTypeCategory.Archive => "Archivo",
                FileTypeCategory.Executable => "Ejecutable",
                _ => "Desconocido"
            };
        }

        public static string GetFileTypeCategoryIcon(FileTypeCategory category)
        {
            return category switch
            {
                FileTypeCategory.Audio => "[Audio]",
                FileTypeCategory.Image => "[Imagen]",
                FileTypeCategory.Video => "[Video]",
                FileTypeCategory.Text => "[Texto]",
                FileTypeCategory.Archive => "[Archivo]",
                FileTypeCategory.Executable => "[Ejecutable]",
                _ => "[Archivo]"
            };
        }
    }
}
