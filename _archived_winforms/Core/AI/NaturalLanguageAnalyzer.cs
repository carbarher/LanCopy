using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SlskDown.Core.AI
{
    /// <summary>
    /// Análisis de resultados en lenguaje natural
    /// </summary>
    public class NaturalLanguageAnalyzer
    {
        public static string AnalyzeSearchResults(List<dynamic> results)
        {
            if (results == null || results.Count == 0)
                return "No se encontraron resultados.";

            var sb = new StringBuilder();
            sb.AppendLine($"📊 ANÁLISIS DE {results.Count} RESULTADOS:\n");

            // Análisis por formato
            var byFormat = results.GroupBy(r => GetFileExtension(r.FileName))
                                 .OrderByDescending(g => g.Count())
                                 .ToList();

            if (byFormat.Count > 0)
            {
                sb.AppendLine("📄 POR FORMATO:");
                foreach (var group in byFormat.Take(5))
                {
                    var percentage = (double)group.Count() / results.Count * 100;
                    sb.AppendLine($"  • {group.Key.ToUpper()}: {group.Count()} archivos ({percentage:F1}%)");
                }
                sb.AppendLine();
            }

            // Análisis por tamaño
            var avgSize = results.Average(r => GetFileSize(r));
            var minSize = results.Min(r => GetFileSize(r));
            var maxSize = results.Max(r => GetFileSize(r));

            sb.AppendLine("💾 POR TAMAÑO:");
            sb.AppendLine($"  • Promedio: {FormatSize((long)avgSize)}");
            sb.AppendLine($"  • Más pequeño: {FormatSize((long)minSize)}");
            sb.AppendLine($"  • Más grande: {FormatSize((long)maxSize)}");
            sb.AppendLine();

            // Recomendación
            var bestFormat = byFormat.FirstOrDefault();
            if (bestFormat != null)
            {
                sb.AppendLine("💡 RECOMENDACIÓN:");
                sb.AppendLine($"  La mayoría son {bestFormat.Key.ToUpper()}. Este formato es el más común.");
            }

            return sb.ToString();
        }

        public static string CompareFiles(List<dynamic> files, string criteria = "quality")
        {
            if (files == null || files.Count < 2)
                return "Se necesitan al menos 2 archivos para comparar.";

            var sb = new StringBuilder();
            sb.AppendLine($"🔍 COMPARACIÓN DE {files.Count} ARCHIVOS:\n");

            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var size = GetFileSize(file);
                var format = GetFileExtension(file.FileName);
                var score = CalculateQualityScore(file);

                sb.AppendLine($"📄 OPCIÓN {i + 1}:");
                sb.AppendLine($"  • Tamaño: {FormatSize((long)size)}");
                sb.AppendLine($"  • Formato: {format.ToUpper()}");
                sb.AppendLine($"  • Puntuación: {score}/10");
                sb.AppendLine();
            }

            // Recomendar mejor opción
            var bestIndex = 0;
            var bestScore = 0.0;
            for (int i = 0; i < files.Count; i++)
            {
                var score = CalculateQualityScore(files[i]);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            sb.AppendLine($"✅ RECOMENDACIÓN: Opción {bestIndex + 1}");
            sb.AppendLine($"   Mejor puntuación de calidad ({bestScore:F1}/10)");

            return sb.ToString();
        }

        private static double CalculateQualityScore(dynamic file)
        {
            double score = 5.0; // Base score

            var format = GetFileExtension(file.FileName).ToLower();
            var size = GetFileSize(file);

            // Bonus por formato
            if (format == "epub") score += 2.0;
            else if (format == "pdf") score += 1.5;
            else if (format == "mobi") score += 1.0;

            // Bonus por tamaño razonable (1-10 MB)
            if (size >= 1_000_000 && size <= 10_000_000) score += 1.5;
            else if (size > 10_000_000 && size <= 50_000_000) score += 1.0;

            // Penalización por archivos muy pequeños (posible baja calidad)
            if (size < 500_000) score -= 2.0;

            return Math.Max(0, Math.Min(10, score));
        }

        private static string GetFileExtension(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return "unknown";
            var ext = System.IO.Path.GetExtension(filename);
            return string.IsNullOrEmpty(ext) ? "unknown" : ext.TrimStart('.');
        }

        private static long GetFileSize(dynamic file)
        {
            try
            {
                if (file.SizeBytes != null) return (long)file.SizeBytes;
                if (file.Size != null) return (long)file.Size;
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }

        public static string GenerateNarrativeSummary(Dictionary<string, int> stats)
        {
            var sb = new StringBuilder();
            sb.AppendLine("📖 RESUMEN DE TU ACTIVIDAD:\n");

            var totalDownloads = stats.GetValueOrDefault("downloads", 0);
            var totalSearches = stats.GetValueOrDefault("searches", 0);

            if (totalDownloads == 0 && totalSearches == 0)
            {
                sb.AppendLine("Aún no has realizado ninguna actividad. ¡Empieza buscando algo!");
                return sb.ToString();
            }

            // Narrativa personalizada
            if (totalDownloads > 50)
                sb.AppendLine("¡Eres un usuario muy activo! 🌟");
            else if (totalDownloads > 20)
                sb.AppendLine("Estás construyendo una buena biblioteca. 📚");
            else if (totalDownloads > 0)
                sb.AppendLine("Estás comenzando tu colección. 🚀");

            sb.AppendLine($"\nHas descargado {totalDownloads} archivos y realizado {totalSearches} búsquedas.");

            // Análisis de patrones
            var avgPerSearch = totalSearches > 0 ? (double)totalDownloads / totalSearches : 0;
            if (avgPerSearch > 5)
                sb.AppendLine("Eres muy selectivo, descargas muchos archivos por búsqueda.");
            else if (avgPerSearch > 2)
                sb.AppendLine("Tienes un buen balance entre búsquedas y descargas.");
            else if (avgPerSearch > 0)
                sb.AppendLine("Exploras mucho antes de descargar, ¡excelente!");

            return sb.ToString();
        }
    }
}
