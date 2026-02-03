using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SlskDown.Core
{
    public static class ASCIIGraphics
    {
        public static string CreateProgressBar(int current, int total, int width = 40)
        {
            if (total == 0) return "[" + new string('░', width) + "] 0%";

            var percentage = (double)current / total;
            var filled = (int)(percentage * width);
            var empty = width - filled;

            var bar = new StringBuilder();
            bar.Append('[');
            bar.Append(new string('█', filled));
            bar.Append(new string('░', empty));
            bar.Append($"] {percentage:P0}");

            return bar.ToString();
        }

        public static string CreateBarChart(Dictionary<string, int> data, int maxWidth = 40, bool showValues = true)
        {
            if (data == null || data.Count == 0)
                return "Sin datos";

            var maxValue = data.Values.Max();
            var result = new StringBuilder();

            foreach (var kvp in data.OrderByDescending(x => x.Value))
            {
                var barLength = maxValue > 0 ? (int)((double)kvp.Value / maxValue * maxWidth) : 0;
                var bar = new string('█', barLength) + new string('░', maxWidth - barLength);
                
                var label = kvp.Key.Length > 15 ? kvp.Key.Substring(0, 12) + "..." : kvp.Key.PadRight(15);
                var value = showValues ? $" {kvp.Value}" : "";
                
                result.AppendLine($"{label} {bar}{value}");
            }

            return result.ToString();
        }

        public static string CreateTable(List<string> headers, List<List<string>> rows)
        {
            if (headers == null || headers.Count == 0 || rows == null || rows.Count == 0)
                return "Sin datos";

            // Calcular anchos de columna
            var columnWidths = new int[headers.Count];
            for (int i = 0; i < headers.Count; i++)
            {
                columnWidths[i] = headers[i].Length;
                foreach (var row in rows)
                {
                    if (i < row.Count)
                        columnWidths[i] = Math.Max(columnWidths[i], row[i].Length);
                }
            }

            var result = new StringBuilder();

            // Línea superior
            result.Append("┌");
            for (int i = 0; i < headers.Count; i++)
            {
                result.Append(new string('─', columnWidths[i] + 2));
                if (i < headers.Count - 1)
                    result.Append("┬");
            }
            result.AppendLine("┐");

            // Headers
            result.Append("│");
            for (int i = 0; i < headers.Count; i++)
            {
                result.Append($" {headers[i].PadRight(columnWidths[i])} │");
            }
            result.AppendLine();

            // Línea separadora
            result.Append("├");
            for (int i = 0; i < headers.Count; i++)
            {
                result.Append(new string('─', columnWidths[i] + 2));
                if (i < headers.Count - 1)
                    result.Append("┼");
            }
            result.AppendLine("┤");

            // Filas
            foreach (var row in rows)
            {
                result.Append("│");
                for (int i = 0; i < headers.Count; i++)
                {
                    var cell = i < row.Count ? row[i] : "";
                    result.Append($" {cell.PadRight(columnWidths[i])} │");
                }
                result.AppendLine();
            }

            // Línea inferior
            result.Append("└");
            for (int i = 0; i < headers.Count; i++)
            {
                result.Append(new string('─', columnWidths[i] + 2));
                if (i < headers.Count - 1)
                    result.Append("┴");
            }
            result.AppendLine("┘");

            return result.ToString();
        }

        public static string CreateSparkline(List<int> values, int height = 8)
        {
            if (values == null || values.Count == 0)
                return "Sin datos";

            var chars = new[] { '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };
            var max = values.Max();
            var min = values.Min();
            var range = max - min;

            if (range == 0)
                return new string(chars[chars.Length / 2], values.Count);

            var result = new StringBuilder();
            foreach (var value in values)
            {
                var normalized = (double)(value - min) / range;
                var index = (int)(normalized * (chars.Length - 1));
                result.Append(chars[index]);
            }

            return result.ToString();
        }

        public static string CreateDownloadStatus(string filename, int percentage, double speed, string status)
        {
            var statusIcon = status switch
            {
                "Downloading" => "⬇️",
                "Completed" => "✅",
                "Failed" => "❌",
                "Paused" => "⏸️",
                "Pending" => "⏳",
                _ => "❓"
            };

            var progressBar = CreateProgressBar(percentage, 100, 30);
            var speedStr = FormatSpeed(speed);
            var name = filename.Length > 40 ? filename.Substring(0, 37) + "..." : filename;

            return $"{statusIcon} {name}\n    {progressBar} {speedStr}";
        }

        public static string CreateMultiDownloadStatus(List<(string filename, int percentage, double speed, string status)> downloads)
        {
            var result = new StringBuilder();
            result.AppendLine("📥 DESCARGAS ACTIVAS:\n");

            foreach (var download in downloads)
            {
                result.AppendLine(CreateDownloadStatus(download.filename, download.percentage, download.speed, download.status));
                result.AppendLine();
            }

            return result.ToString();
        }

        public static string CreateWeeklyChart(Dictionary<string, int> dayData)
        {
            var days = new[] { "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom" };
            var maxValue = dayData.Values.Max();
            var result = new StringBuilder();

            result.AppendLine("📊 ACTIVIDAD SEMANAL:\n");

            foreach (var day in days)
            {
                var value = dayData.ContainsKey(day) ? dayData[day] : 0;
                var barLength = maxValue > 0 ? (int)((double)value / maxValue * 40) : 0;
                var bar = new string('█', barLength) + new string('░', 40 - barLength);
                var peak = value == maxValue && value > 0 ? " ⭐" : "";
                
                result.AppendLine($"{day} {bar} {value}{peak}");
            }

            return result.ToString();
        }

        public static string CreateStatBox(string title, string value, string subtitle)
        {
            var width = Math.Max(Math.Max(title.Length, value.Length), subtitle.Length) + 4;
            var result = new StringBuilder();

            result.AppendLine("┌" + new string('─', width) + "┐");
            result.AppendLine($"│ {title.PadRight(width - 2)} │");
            result.AppendLine("├" + new string('─', width) + "┤");
            result.AppendLine($"│ {value.PadRight(width - 2)} │");
            result.AppendLine($"│ {subtitle.PadRight(width - 2)} │");
            result.AppendLine("└" + new string('─', width) + "┘");

            return result.ToString();
        }

        public static string CreateSeriesProgress(string seriesName, List<(int volume, bool downloaded)> volumes)
        {
            var result = new StringBuilder();
            result.AppendLine($"📚 Serie: {seriesName}\n");

            foreach (var vol in volumes.OrderBy(v => v.volume))
            {
                var icon = vol.downloaded ? "✅" : "❌";
                var status = vol.downloaded ? "Descargado" : "Faltante";
                result.AppendLine($"{icon} Volumen {vol.volume}: {status}");
            }

            var downloaded = volumes.Count(v => v.downloaded);
            var total = volumes.Count;
            var percentage = total > 0 ? (double)downloaded / total * 100 : 0;

            result.AppendLine($"\nProgreso: {CreateProgressBar(downloaded, total, 30)}");
            result.AppendLine($"Completado: {downloaded}/{total} ({percentage:F0}%)");

            return result.ToString();
        }

        private static string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond < 1024) return $"{bytesPerSecond:F0} B/s";
            if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024:F1} KB/s";
            return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
        }

        public static string CreateLoadingAnimation(int frame)
        {
            var frames = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
            return frames[frame % frames.Length];
        }

        public static string CreatePercentageBar(double percentage, int width = 20)
        {
            var filled = (int)(percentage / 100.0 * width);
            var empty = width - filled;
            return $"[{new string('█', filled)}{new string('░', empty)}] {percentage:F1}%";
        }
    }
}
