using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SlskDown.Core
{
    public class SeriesInfo
    {
        public string SeriesName { get; set; }
        public int? VolumeNumber { get; set; }
        public string OriginalFilename { get; set; }
        public List<string> RelatedFiles { get; set; } = new List<string>();
    }

    public static class SeriesDetector
    {
        private static readonly Regex[] SeriesPatterns = new[]
        {
            // Patrones en español
            new Regex(@"(?<series>.+?)\s*[-–—]\s*(?:Libro|Tomo|Volumen|Vol\.?|Parte)\s*(?<num>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"(?<series>.+?)\s*\((?:Libro|Tomo|Volumen|Vol\.?|Parte)\s*(?<num>\d+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"(?<series>.+?)\s*(?<num>\d+)\s*(?:de\s*\d+)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            
            // Patrones en inglés
            new Regex(@"(?<series>.+?)\s*[-–—]\s*(?:Book|Volume|Vol\.?|Part)\s*(?<num>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"(?<series>.+?)\s*\((?:Book|Volume|Vol\.?|Part)\s*(?<num>\d+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            
            // Patrones con números romanos
            new Regex(@"(?<series>.+?)\s*[-–—]\s*(?<num>[IVX]+)(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            
            // Patrones con #
            new Regex(@"(?<series>.+?)\s*#(?<num>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            
            // Patrones de sagas conocidas
            new Regex(@"(?<series>Fundación|Foundation)\s*(?:y\s+Imperio|e\s+Imperio|and\s+Empire)?.*?(?<num>\d+)?", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"(?<series>Harry\s+Potter)\s*(?:y|and)\s*(?:la|el|the)\s*(?<title>.+?)(?:\s*[-–—]\s*(?<num>\d+))?", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };

        private static readonly Dictionary<string, int> RomanNumerals = new()
        {
            ["I"] = 1, ["II"] = 2, ["III"] = 3, ["IV"] = 4, ["V"] = 5,
            ["VI"] = 6, ["VII"] = 7, ["VIII"] = 8, ["IX"] = 9, ["X"] = 10,
            ["XI"] = 11, ["XII"] = 12, ["XIII"] = 13, ["XIV"] = 14, ["XV"] = 15
        };

        public static bool IsSeries(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return false;

            var cleanName = CleanFilename(filename);
            return SeriesPatterns.Any(pattern => pattern.IsMatch(cleanName));
        }

        public static SeriesInfo DetectSeries(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return null;

            var cleanName = CleanFilename(filename);

            foreach (var pattern in SeriesPatterns)
            {
                var match = pattern.Match(cleanName);
                if (match.Success)
                {
                    var seriesName = match.Groups["series"].Value.Trim();
                    var volumeStr = match.Groups["num"].Value;
                    
                    int? volumeNumber = null;
                    if (!string.IsNullOrEmpty(volumeStr))
                    {
                        if (int.TryParse(volumeStr, out var num))
                        {
                            volumeNumber = num;
                        }
                        else if (RomanNumerals.TryGetValue(volumeStr.ToUpper(), out var romanNum))
                        {
                            volumeNumber = romanNum;
                        }
                    }

                    return new SeriesInfo
                    {
                        SeriesName = seriesName,
                        VolumeNumber = volumeNumber,
                        OriginalFilename = filename
                    };
                }
            }

            return null;
        }

        public static string DetectSeriesName(string filename)
        {
            var info = DetectSeries(filename);
            return info?.SeriesName;
        }

        public static int? DetectVolumeNumber(string filename)
        {
            var info = DetectSeries(filename);
            return info?.VolumeNumber;
        }

        public static Dictionary<string, List<SeriesInfo>> GroupBySeries(List<string> files)
        {
            if (files == null || files.Count == 0)
                return new Dictionary<string, List<SeriesInfo>>();

            var seriesGroups = new Dictionary<string, List<SeriesInfo>>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                var seriesInfo = DetectSeries(file);
                if (seriesInfo != null)
                {
                    if (!seriesGroups.ContainsKey(seriesInfo.SeriesName))
                        seriesGroups[seriesInfo.SeriesName] = new List<SeriesInfo>();

                    seriesGroups[seriesInfo.SeriesName].Add(seriesInfo);
                }
            }

            // Ordenar cada serie por número de volumen
            foreach (var series in seriesGroups.Values)
            {
                series.Sort((a, b) =>
                {
                    if (a.VolumeNumber.HasValue && b.VolumeNumber.HasValue)
                        return a.VolumeNumber.Value.CompareTo(b.VolumeNumber.Value);
                    if (a.VolumeNumber.HasValue)
                        return -1;
                    if (b.VolumeNumber.HasValue)
                        return 1;
                    return string.Compare(a.OriginalFilename, b.OriginalFilename, StringComparison.OrdinalIgnoreCase);
                });
            }

            return seriesGroups;
        }

        public static List<string> FindMissingVolumes(List<SeriesInfo> series)
        {
            if (series == null || series.Count == 0)
                return new List<string>();

            var volumes = series.Where(s => s.VolumeNumber.HasValue)
                               .Select(s => s.VolumeNumber.Value)
                               .OrderBy(v => v)
                               .ToList();

            if (volumes.Count == 0)
                return new List<string>();

            var missing = new List<string>();
            var min = volumes.Min();
            var max = volumes.Max();

            for (int i = min; i <= max; i++)
            {
                if (!volumes.Contains(i))
                {
                    missing.Add($"Volumen {i}");
                }
            }

            return missing;
        }

        public static string GenerateSeriesSummary(string seriesName, List<SeriesInfo> books)
        {
            if (books == null || books.Count == 0)
                return "";

            var summary = new System.Text.StringBuilder();
            summary.AppendLine($"📚 Serie: {seriesName}");
            summary.AppendLine($"Total de libros: {books.Count}");

            var missing = FindMissingVolumes(books);
            if (missing.Count > 0)
            {
                summary.AppendLine($"⚠️ Volúmenes faltantes: {string.Join(", ", missing)}");
            }
            else if (books.All(b => b.VolumeNumber.HasValue))
            {
                summary.AppendLine("✅ Serie completa");
            }

            summary.AppendLine("\nLibros encontrados:");
            foreach (var book in books)
            {
                var volumeInfo = book.VolumeNumber.HasValue ? $"Vol. {book.VolumeNumber}" : "Sin número";
                summary.AppendLine($"  • {volumeInfo}: {book.OriginalFilename}");
            }

            return summary.ToString();
        }

        private static string CleanFilename(string filename)
        {
            // Remover extensión
            var name = System.IO.Path.GetFileNameWithoutExtension(filename);
            
            // Remover caracteres especiales comunes
            name = name.Replace("_", " ")
                      .Replace(".", " ")
                      .Replace("[", "")
                      .Replace("]", "")
                      .Replace("(", " (")
                      .Replace(")", ") ");

            // Normalizar espacios
            name = Regex.Replace(name, @"\s+", " ").Trim();

            return name;
        }
    }
}
