using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SlskDown.Core
{
    public class ActivityReport
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalDownloads { get; set; }
        public int CompletedDownloads { get; set; }
        public int FailedDownloads { get; set; }
        public long TotalBytes { get; set; }
        public int TotalSearches { get; set; }
        public int TotalResults { get; set; }
        public double AverageSpeed { get; set; }
        public Dictionary<string, int> TopAuthors { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> DailyActivity { get; set; } = new Dictionary<string, int>();
    }

    public static class ReportGenerator
    {
        public static string GenerateWeeklyReport(ActivityReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("📊 REPORTE SEMANAL");
            sb.AppendLine($"Período: {report.StartDate:dd/MM/yyyy} - {report.EndDate:dd/MM/yyyy}");
            sb.AppendLine(new string('═', 50));
            sb.AppendLine();

            // Resumen de descargas
            sb.AppendLine("📥 DESCARGAS:");
            sb.AppendLine($"  • Total: {report.TotalDownloads} archivos");
            sb.AppendLine($"  • Completadas: {report.CompletedDownloads} ({GetPercentage(report.CompletedDownloads, report.TotalDownloads):F1}%)");
            sb.AppendLine($"  • Fallidas: {report.FailedDownloads} ({GetPercentage(report.FailedDownloads, report.TotalDownloads):F1}%)");
            sb.AppendLine($"  • Tamaño total: {FormatBytes(report.TotalBytes)}");
            sb.AppendLine($"  • Velocidad promedio: {FormatSpeed(report.AverageSpeed)}");
            sb.AppendLine();

            // Búsquedas
            sb.AppendLine("🔍 BÚSQUEDAS:");
            sb.AppendLine($"  • Total: {report.TotalSearches} búsquedas");
            sb.AppendLine($"  • Resultados: {report.TotalResults} archivos");
            sb.AppendLine($"  • Promedio: {(report.TotalSearches > 0 ? report.TotalResults / report.TotalSearches : 0)} resultados/búsqueda");
            sb.AppendLine($"  • Tasa de éxito: {GetPercentage(report.TotalResults > 0 ? report.TotalSearches : 0, report.TotalSearches):F1}%");
            sb.AppendLine();

            // Top autores
            if (report.TopAuthors.Count > 0)
            {
                sb.AppendLine("⭐ TOP AUTORES:");
                int rank = 1;
                foreach (var author in report.TopAuthors.OrderByDescending(x => x.Value).Take(5))
                {
                    sb.AppendLine($"  {rank}. {author.Key} ({author.Value} archivos)");
                    rank++;
                }
                sb.AppendLine();
            }

            // Actividad diaria
            if (report.DailyActivity.Count > 0)
            {
                sb.AppendLine("📈 ACTIVIDAD DIARIA:");
                sb.AppendLine(ASCIIGraphics.CreateWeeklyChart(report.DailyActivity));
                sb.AppendLine();
            }

            // Tendencias
            sb.AppendLine("📊 TENDENCIAS:");
            sb.AppendLine("  • Día más activo: " + GetMostActiveDay(report.DailyActivity));
            sb.AppendLine("  • Promedio diario: " + (report.TotalDownloads / 7) + " descargas");
            sb.AppendLine();

            return sb.ToString();
        }

        public static string GenerateDailyReport(ActivityReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("📊 REPORTE DIARIO");
            sb.AppendLine($"Fecha: {report.StartDate:dd/MM/yyyy}");
            sb.AppendLine(new string('═', 40));
            sb.AppendLine();

            sb.AppendLine($"📥 Descargas: {report.CompletedDownloads}/{report.TotalDownloads}");
            sb.AppendLine($"💾 Descargado: {FormatBytes(report.TotalBytes)}");
            sb.AppendLine($"🔍 Búsquedas: {report.TotalSearches}");
            sb.AppendLine($"⚡ Velocidad: {FormatSpeed(report.AverageSpeed)}");
            sb.AppendLine();

            if (report.TopAuthors.Count > 0)
            {
                var topAuthor = report.TopAuthors.OrderByDescending(x => x.Value).First();
                sb.AppendLine($"⭐ Autor del día: {topAuthor.Key} ({topAuthor.Value} descargas)");
            }

            return sb.ToString();
        }

        public static string GenerateMonthlyReport(ActivityReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("📊 REPORTE MENSUAL");
            sb.AppendLine($"Mes: {report.StartDate:MMMM yyyy}");
            sb.AppendLine(new string('═', 50));
            sb.AppendLine();

            sb.AppendLine("📈 RESUMEN DEL MES:");
            sb.AppendLine($"  • Descargas totales: {report.TotalDownloads}");
            sb.AppendLine($"  • Tasa de éxito: {GetPercentage(report.CompletedDownloads, report.TotalDownloads):F1}%");
            sb.AppendLine($"  • Datos descargados: {FormatBytes(report.TotalBytes)}");
            sb.AppendLine($"  • Búsquedas realizadas: {report.TotalSearches}");
            sb.AppendLine($"  • Promedio diario: {report.TotalDownloads / 30} descargas");
            sb.AppendLine();

            sb.AppendLine("🏆 TOP 10 AUTORES DEL MES:");
            int rank = 1;
            foreach (var author in report.TopAuthors.OrderByDescending(x => x.Value).Take(10))
            {
                var bar = ASCIIGraphics.CreateProgressBar(author.Value, report.TopAuthors.Values.Max(), 20);
                sb.AppendLine($"  {rank:D2}. {author.Key.PadRight(20)} {bar}");
                rank++;
            }

            return sb.ToString();
        }

        public static string GenerateLibraryAnalysis(List<string> files, Dictionary<string, int> authorCounts)
        {
            var sb = new StringBuilder();
            sb.AppendLine("📚 ANÁLISIS DE BIBLIOTECA");
            sb.AppendLine(new string('═', 50));
            sb.AppendLine();

            // Estadísticas generales
            sb.AppendLine("📊 ESTADÍSTICAS GENERALES:");
            sb.AppendLine($"  • Total de archivos: {files.Count}");
            sb.AppendLine($"  • Autores únicos: {authorCounts.Count}");
            sb.AppendLine($"  • Promedio por autor: {(authorCounts.Count > 0 ? files.Count / authorCounts.Count : 0)} archivos");
            sb.AppendLine();

            // Detección de series
            var series = SeriesDetector.GroupBySeries(files);
            if (series.Count > 0)
            {
                sb.AppendLine($"📖 SERIES DETECTADAS: {series.Count}");
                foreach (var kvp in series.Take(5))
                {
                    var missing = SeriesDetector.FindMissingVolumes(kvp.Value);
                    var status = missing.Count == 0 ? "✅ Completa" : $"⚠️ Faltan {missing.Count}";
                    sb.AppendLine($"  • {kvp.Key} ({kvp.Value.Count} libros) - {status}");
                }
                sb.AppendLine();
            }

            // Distribución por extensión
            var extensions = files.GroupBy(f => System.IO.Path.GetExtension(f).ToLower())
                                 .ToDictionary(g => g.Key, g => g.Count());
            
            sb.AppendLine("📄 FORMATOS:");
            foreach (var ext in extensions.OrderByDescending(x => x.Value))
            {
                var percentage = GetPercentage(ext.Value, files.Count);
                sb.AppendLine($"  • {ext.Key.ToUpper().PadRight(6)} {ASCIIGraphics.CreatePercentageBar(percentage, 20)}");
            }
            sb.AppendLine();

            // Top autores
            sb.AppendLine("⭐ TOP 10 AUTORES:");
            var topAuthors = authorCounts.OrderByDescending(x => x.Value).Take(10);
            foreach (var author in topAuthors)
            {
                var bar = ASCIIGraphics.CreateProgressBar(author.Value, authorCounts.Values.Max(), 25);
                sb.AppendLine($"  • {author.Key.PadRight(20)} {bar}");
            }

            return sb.ToString();
        }

        public static string PredictCompletionTime(int remaining, double averageSpeed)
        {
            if (averageSpeed <= 0 || remaining <= 0)
                return "No se puede estimar el tiempo de finalización.";

            var estimatedSeconds = remaining / averageSpeed;
            var estimatedTime = TimeSpan.FromSeconds(estimatedSeconds);

            var sb = new StringBuilder();
            sb.AppendLine("⏱️ PREDICCIÓN DE FINALIZACIÓN:");
            sb.AppendLine();
            sb.AppendLine($"  • Archivos restantes: {remaining}");
            sb.AppendLine($"  • Velocidad promedio: {FormatSpeed(averageSpeed)}");
            sb.AppendLine($"  • Tiempo estimado: {FormatTimeSpan(estimatedTime)}");
            sb.AppendLine($"  • Finalización aproximada: {DateTime.Now.Add(estimatedTime):HH:mm}");
            sb.AppendLine();
            sb.AppendLine("Factores que pueden afectar:");
            sb.AppendLine("  • Disponibilidad de usuarios");
            sb.AppendLine("  • Velocidad de conexión");
            sb.AppendLine("  • Nuevas descargas agregadas");

            return sb.ToString();
        }

        public static string GenerateComparisonReport(ActivityReport current, ActivityReport previous)
        {
            var sb = new StringBuilder();
            sb.AppendLine("📊 COMPARACIÓN DE PERÍODOS");
            sb.AppendLine(new string('═', 50));
            sb.AppendLine();

            var downloadChange = CalculateChange(current.TotalDownloads, previous.TotalDownloads);
            var searchChange = CalculateChange(current.TotalSearches, previous.TotalSearches);
            var speedChange = CalculateChange(current.AverageSpeed, previous.AverageSpeed);
            var errorChange = CalculateChange(current.FailedDownloads, previous.FailedDownloads);

            sb.AppendLine($"📥 Descargas: {current.TotalDownloads} {FormatChange(downloadChange)}");
            sb.AppendLine($"🔍 Búsquedas: {current.TotalSearches} {FormatChange(searchChange)}");
            sb.AppendLine($"⚡ Velocidad: {FormatSpeed(current.AverageSpeed)} {FormatChange(speedChange)}");
            sb.AppendLine($"❌ Errores: {current.FailedDownloads} {FormatChange(errorChange, true)}");
            sb.AppendLine();

            sb.AppendLine("TENDENCIAS:");
            if (downloadChange > 10)
                sb.AppendLine("  📈 Aumento significativo en descargas");
            else if (downloadChange < -10)
                sb.AppendLine("  📉 Disminución en descargas");
            
            if (errorChange < -20)
                sb.AppendLine("  ✅ Mejora notable en tasa de éxito");
            else if (errorChange > 20)
                sb.AppendLine("  ⚠️ Aumento en errores de descarga");

            return sb.ToString();
        }

        private static double GetPercentage(int value, int total)
        {
            return total > 0 ? (double)value / total * 100 : 0;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }

        private static string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond < 1024) return $"{bytesPerSecond:F0} B/s";
            if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024:F1} KB/s";
            return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
        }

        private static string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalMinutes < 1) return $"{ts.Seconds} segundos";
            if (ts.TotalHours < 1) return $"{ts.Minutes} minutos";
            if (ts.TotalDays < 1) return $"{ts.Hours}h {ts.Minutes}m";
            return $"{(int)ts.TotalDays} días {ts.Hours}h";
        }

        private static string GetMostActiveDay(Dictionary<string, int> dailyActivity)
        {
            if (dailyActivity.Count == 0) return "N/A";
            var max = dailyActivity.OrderByDescending(x => x.Value).First();
            return $"{max.Key} ({max.Value} descargas)";
        }

        private static double CalculateChange(double current, double previous)
        {
            if (previous == 0) return 0;
            return ((current - previous) / previous) * 100;
        }

        private static string FormatChange(double change, bool inverseGood = false)
        {
            var icon = change > 0 ? (inverseGood ? "📉" : "📈") : (inverseGood ? "📈" : "📉");
            var sign = change > 0 ? "+" : "";
            return $"({icon} {sign}{change:F1}%)";
        }
    }
}
