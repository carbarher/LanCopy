using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SlskDown
{
    /// <summary>
    /// Detecta series de libros y encuentra libros faltantes
    /// </summary>
    public class SeriesDetector
    {
        public class BookInSeries
        {
            public string FileName { get; set; }
            public string SeriesName { get; set; }
            public int BookNumber { get; set; }
            public string Author { get; set; }
            public bool IsDownloaded { get; set; }
        }

        public class SeriesInfo
        {
            public string SeriesName { get; set; }
            public string Author { get; set; }
            public List<BookInSeries> Books { get; set; } = new List<BookInSeries>();
            public List<int> MissingNumbers { get; set; } = new List<int>();
            public int TotalBooks { get; set; }
            public int DownloadedBooks { get; set; }
            public double CompletionPercentage => TotalBooks > 0 ? (DownloadedBooks * 100.0 / TotalBooks) : 0;
            public bool IsComplete => MissingNumbers.Count == 0 && TotalBooks > 0;
        }

        // Patrones para detectar números en títulos de series
        private static readonly Regex[] SeriesPatterns = new[]
        {
            // "Libro 1", "Book 1", "Tomo 1", "Vol 1", "Volume 1"
            new Regex(@"(?:libro|book|tomo|vol(?:ume)?)\s*(\d+)", RegexOptions.IgnoreCase),
            
            // "#1", "No. 1", "Nº 1"
            new Regex(@"(?:#|no\.?|nº)\s*(\d+)", RegexOptions.IgnoreCase),
            
            // "(1)", "[1]", "- 1 -"
            new Regex(@"[\(\[\-]\s*(\d+)\s*[\)\]\-]", RegexOptions.IgnoreCase),
            
            // "01 -", "1 -", "001 -" al inicio
            new Regex(@"^(\d{1,3})\s*[-\.]", RegexOptions.IgnoreCase),
            
            // "Parte 1", "Part 1"
            new Regex(@"(?:parte|part)\s*(\d+)", RegexOptions.IgnoreCase),
            
            // Números romanos: "I", "II", "III", etc.
            new Regex(@"\b([IVXLCDM]{1,5})\b(?!\w)", RegexOptions.IgnoreCase)
        };

        private static readonly Dictionary<string, int> RomanNumerals = new Dictionary<string, int>
        {
            {"I", 1}, {"II", 2}, {"III", 3}, {"IV", 4}, {"V", 5},
            {"VI", 6}, {"VII", 7}, {"VIII", 8}, {"IX", 9}, {"X", 10},
            {"XI", 11}, {"XII", 12}, {"XIII", 13}, {"XIV", 14}, {"XV", 15},
            {"XVI", 16}, {"XVII", 17}, {"XVIII", 18}, {"XIX", 19}, {"XX", 20}
        };

        /// <summary>
        /// Detecta series en una lista de archivos
        /// </summary>
        public static List<SeriesInfo> DetectSeries(List<string> fileNames, string author = null)
        {
            var booksBySeries = new Dictionary<string, List<BookInSeries>>();

            foreach (var fileName in fileNames)
            {
                var bookInfo = ExtractBookInfo(fileName, author);
                if (bookInfo != null)
                {
                    var key = $"{bookInfo.Author}_{bookInfo.SeriesName}";
                    if (!booksBySeries.ContainsKey(key))
                    {
                        booksBySeries[key] = new List<BookInSeries>();
                    }
                    booksBySeries[key].Add(bookInfo);
                }
            }

            var seriesList = new List<SeriesInfo>();

            foreach (var kvp in booksBySeries)
            {
                var books = kvp.Value.OrderBy(b => b.BookNumber).ToList();
                
                // Solo considerar como serie si tiene al menos 2 libros
                if (books.Count < 2)
                    continue;

                var seriesInfo = new SeriesInfo
                {
                    SeriesName = books[0].SeriesName,
                    Author = books[0].Author,
                    Books = books,
                    DownloadedBooks = books.Count(b => b.IsDownloaded)
                };

                // Detectar números faltantes
                var existingNumbers = books.Select(b => b.BookNumber).OrderBy(n => n).ToList();
                var minNumber = existingNumbers.Min();
                var maxNumber = existingNumbers.Max();

                seriesInfo.TotalBooks = maxNumber - minNumber + 1;

                for (int i = minNumber; i <= maxNumber; i++)
                {
                    if (!existingNumbers.Contains(i))
                    {
                        seriesInfo.MissingNumbers.Add(i);
                    }
                }

                seriesList.Add(seriesInfo);
            }

            return seriesList.OrderByDescending(s => s.Books.Count).ToList();
        }

        /// <summary>
        /// Extrae información de un libro (serie, número, autor)
        /// </summary>
        private static BookInSeries ExtractBookInfo(string fileName, string defaultAuthor)
        {
            // Intentar extraer autor del nombre del archivo
            var author = ExtractAuthor(fileName) ?? defaultAuthor ?? "Desconocido";

            // Intentar extraer número de libro
            int? bookNumber = null;
            foreach (var pattern in SeriesPatterns)
            {
                var match = pattern.Match(fileName);
                if (match.Success)
                {
                    var numberStr = match.Groups[1].Value;
                    
                    // Intentar convertir número romano
                    if (RomanNumerals.TryGetValue(numberStr.ToUpperInvariant(), out int romanValue))
                    {
                        bookNumber = romanValue;
                        break;
                    }
                    
                    // Intentar convertir número arábigo
                    if (int.TryParse(numberStr, out int arabicValue))
                    {
                        bookNumber = arabicValue;
                        break;
                    }
                }
            }

            if (!bookNumber.HasValue)
                return null;

            // Extraer nombre de la serie (remover número y extensión)
            var seriesName = ExtractSeriesName(fileName, bookNumber.Value);

            return new BookInSeries
            {
                FileName = fileName,
                SeriesName = seriesName,
                BookNumber = bookNumber.Value,
                Author = author,
                IsDownloaded = true // Por defecto, asumimos que está descargado
            };
        }

        /// <summary>
        /// Extrae el nombre de la serie del nombre del archivo
        /// </summary>
        private static string ExtractSeriesName(string fileName, int bookNumber)
        {
            // Remover extensión
            var nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fileName);

            // Remover todas las variantes del número
            foreach (var pattern in SeriesPatterns)
            {
                nameWithoutExt = pattern.Replace(nameWithoutExt, "");
            }

            // Limpiar espacios y caracteres especiales
            nameWithoutExt = Regex.Replace(nameWithoutExt, @"\s+", " ").Trim();
            nameWithoutExt = Regex.Replace(nameWithoutExt, @"[-_\.\(\)\[\]]+", " ").Trim();

            return nameWithoutExt;
        }

        /// <summary>
        /// Intenta extraer el nombre del autor del nombre del archivo
        /// </summary>
        private static string ExtractAuthor(string fileName)
        {
            // Patrón común: "Autor - Título"
            var match = Regex.Match(fileName, @"^([^-]+)\s*-\s*(.+)");
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            // Patrón: "Autor_Título"
            match = Regex.Match(fileName, @"^([^_]+)_(.+)");
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            return null;
        }

        /// <summary>
        /// Genera términos de búsqueda para libros faltantes
        /// </summary>
        public static List<string> GenerateSearchTerms(SeriesInfo series)
        {
            var searchTerms = new List<string>();

            foreach (var missingNumber in series.MissingNumbers)
            {
                // Variante 1: "Autor Serie Libro N"
                searchTerms.Add($"{series.Author} {series.SeriesName} libro {missingNumber}");
                
                // Variante 2: "Autor Serie #N"
                searchTerms.Add($"{series.Author} {series.SeriesName} #{missingNumber}");
                
                // Variante 3: "Autor Serie N"
                searchTerms.Add($"{series.Author} {series.SeriesName} {missingNumber}");
                
                // Variante 4: "Autor Serie Vol N"
                searchTerms.Add($"{series.Author} {series.SeriesName} vol {missingNumber}");
            }

            return searchTerms;
        }

        /// <summary>
        /// Formatea la información de una serie para mostrar
        /// </summary>
        public static string FormatSeriesInfo(SeriesInfo series)
        {
            var status = series.IsComplete ? "✅ COMPLETA" : $"⚠️ {series.MissingNumbers.Count} faltante(s)";
            var missing = series.MissingNumbers.Count > 0 
                ? $" - Faltan: {string.Join(", ", series.MissingNumbers)}" 
                : "";
            
            return $"{series.Author} - {series.SeriesName} ({series.DownloadedBooks}/{series.TotalBooks}) {status}{missing}";
        }
    }
}
