using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SlskDown.Core.AI
{
    public class AIMetrics
    {
        public int TotalQueries { get; set; }
        public int CacheHits { get; set; }
        public int CacheMisses { get; set; }
        public double AverageResponseTime { get; set; }
        public long TotalTokensGenerated { get; set; }
        public Dictionary<string, int> ModelUsage { get; set; } = new Dictionary<string, int>();
        public DateTime SessionStart { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Dashboard de métricas de IA en tiempo real
    /// </summary>
    public class AIMetricsDashboard
    {
        private readonly List<double> responseTimes = new List<double>();
        private readonly Dictionary<string, int> modelUsage = new Dictionary<string, int>();
        private int totalQueries = 0;
        private int cacheHits = 0;
        private int cacheMisses = 0;
        private long totalTokens = 0;
        private DateTime sessionStart = DateTime.Now;

        public void RecordQuery(string model, double responseTimeMs, int tokensGenerated, bool fromCache)
        {
            totalQueries++;
            
            if (fromCache)
                cacheHits++;
            else
                cacheMisses++;

            responseTimes.Add(responseTimeMs);
            totalTokens += tokensGenerated;

            if (!modelUsage.ContainsKey(model))
                modelUsage[model] = 0;
            
            modelUsage[model]++;

            // Mantener solo últimas 100 mediciones
            if (responseTimes.Count > 100)
                responseTimes.RemoveAt(0);
        }

        public AIMetrics GetMetrics()
        {
            return new AIMetrics
            {
                TotalQueries = totalQueries,
                CacheHits = cacheHits,
                CacheMisses = cacheMisses,
                AverageResponseTime = responseTimes.Count > 0 ? responseTimes.Average() : 0,
                TotalTokensGenerated = totalTokens,
                ModelUsage = new Dictionary<string, int>(modelUsage),
                SessionStart = sessionStart
            };
        }

        public string GenerateDashboard()
        {
            var metrics = GetMetrics();
            var sb = new StringBuilder();

            sb.AppendLine("📊 DASHBOARD DE IA");
            sb.AppendLine(new string('═', 50));
            sb.AppendLine();

            // Estadísticas generales
            sb.AppendLine("📈 ESTADÍSTICAS GENERALES:");
            sb.AppendLine($"  • Total consultas: {metrics.TotalQueries:N0}");
            sb.AppendLine($"  • Tiempo de sesión: {FormatTimeSpan(DateTime.Now - metrics.SessionStart)}");
            sb.AppendLine($"  • Tokens generados: {metrics.TotalTokensGenerated:N0}");
            sb.AppendLine();

            // Rendimiento
            sb.AppendLine("⚡ RENDIMIENTO:");
            sb.AppendLine($"  • Tiempo promedio: {metrics.AverageResponseTime:F0} ms");
            sb.AppendLine($"  • Velocidad: {(metrics.TotalTokensGenerated / Math.Max(1, (DateTime.Now - metrics.SessionStart).TotalSeconds)):F1} tokens/s");
            sb.AppendLine();

            // Caché
            var cacheHitRate = metrics.TotalQueries > 0 
                ? (double)metrics.CacheHits / metrics.TotalQueries * 100 
                : 0;
            
            sb.AppendLine("💾 CACHÉ:");
            sb.AppendLine($"  • Hits: {metrics.CacheHits} ({cacheHitRate:F1}%)");
            sb.AppendLine($"  • Misses: {metrics.CacheMisses}");
            sb.AppendLine($"  • Eficiencia: {CreateProgressBar(cacheHitRate, 100, 20)}");
            sb.AppendLine();

            // Uso de modelos
            if (metrics.ModelUsage.Count > 0)
            {
                sb.AppendLine("🤖 USO DE MODELOS:");
                foreach (var kvp in metrics.ModelUsage.OrderByDescending(x => x.Value))
                {
                    var percentage = (double)kvp.Value / metrics.TotalQueries * 100;
                    sb.AppendLine($"  • {kvp.Key}: {kvp.Value} ({percentage:F1}%)");
                }
                sb.AppendLine();
            }

            // Gráfico de velocidad (últimas 10 consultas)
            if (responseTimes.Count > 0)
            {
                sb.AppendLine("📉 VELOCIDAD (últimas consultas):");
                sb.AppendLine($"  {CreateSparkline(responseTimes.TakeLast(20).ToList())}");
                sb.AppendLine($"  Min: {responseTimes.Min():F0}ms | Max: {responseTimes.Max():F0}ms");
            }

            return sb.ToString();
        }

        private string CreateProgressBar(double current, double max, int width)
        {
            var percentage = max > 0 ? current / max : 0;
            var filled = (int)(percentage * width);
            var empty = width - filled;
            return $"[{new string('█', filled)}{new string('░', empty)}] {percentage:P0}";
        }

        private string CreateSparkline(List<double> values)
        {
            if (values.Count == 0) return "";

            var chars = new[] { '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };
            var max = values.Max();
            var min = values.Min();
            var range = max - min;

            if (range == 0) return new string(chars[chars.Length / 2], values.Count);

            var sb = new StringBuilder();
            foreach (var value in values)
            {
                var normalized = (value - min) / range;
                var index = (int)(normalized * (chars.Length - 1));
                sb.Append(chars[index]);
            }

            return sb.ToString();
        }

        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalMinutes < 1) return $"{ts.Seconds}s";
            if (ts.TotalHours < 1) return $"{ts.Minutes}m {ts.Seconds}s";
            if (ts.TotalDays < 1) return $"{ts.Hours}h {ts.Minutes}m";
            return $"{(int)ts.TotalDays}d {ts.Hours}h";
        }

        public void Reset()
        {
            responseTimes.Clear();
            modelUsage.Clear();
            totalQueries = 0;
            cacheHits = 0;
            cacheMisses = 0;
            totalTokens = 0;
            sessionStart = DateTime.Now;
        }
    }
}
