using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;

namespace SlskDown
{
    /// <summary>
    /// Sistema de métricas de performance para monitorear operaciones
    /// </summary>
    public class PerformanceMetrics
    {
        private static readonly Lazy<PerformanceMetrics> _instance = 
            new(() => new PerformanceMetrics());
        
        public static PerformanceMetrics Instance => _instance.Value;
        
        private readonly ConcurrentDictionary<string, MetricData> _metrics = new();
        private readonly Stopwatch _uptime = Stopwatch.StartNew();
        
        private PerformanceMetrics() { }
        
        /// <summary>
        /// Inicia el tracking de una operación
        /// </summary>
        public IDisposable Track(string operationName)
        {
            return new OperationTracker(this, operationName);
        }
        
        /// <summary>
        /// Registra una métrica
        /// </summary>
        internal void RecordMetric(string name, long durationMs)
        {
            _metrics.AddOrUpdate(
                name,
                _ => new MetricData { Name = name, Count = 1, TotalMs = durationMs, MinMs = durationMs, MaxMs = durationMs },
                (_, existing) =>
                {
                    existing.Count++;
                    existing.TotalMs += durationMs;
                    existing.MinMs = Math.Min(existing.MinMs, durationMs);
                    existing.MaxMs = Math.Max(existing.MaxMs, durationMs);
                    return existing;
                });
        }
        
        /// <summary>
        /// Obtiene estadísticas de una operación
        /// </summary>
        public MetricStats? GetStats(string operationName)
        {
            if (!_metrics.TryGetValue(operationName, out var data))
                return null;
            
            return new MetricStats
            {
                Name = data.Name,
                Count = data.Count,
                AverageMs = data.Count > 0 ? data.TotalMs / (double)data.Count : 0,
                MinMs = data.MinMs,
                MaxMs = data.MaxMs,
                TotalMs = data.TotalMs
            };
        }
        
        /// <summary>
        /// Obtiene todas las métricas
        /// </summary>
        public MetricStats[] GetAllStats()
        {
            return _metrics.Values
                .Select(d => new MetricStats
                {
                    Name = d.Name,
                    Count = d.Count,
                    AverageMs = d.Count > 0 ? d.TotalMs / (double)d.Count : 0,
                    MinMs = d.MinMs,
                    MaxMs = d.MaxMs,
                    TotalMs = d.TotalMs
                })
                .OrderByDescending(s => s.TotalMs)
                .ToArray();
        }
        
        /// <summary>
        /// Limpia todas las métricas
        /// </summary>
        public void Clear()
        {
            _metrics.Clear();
        }
        
        /// <summary>
        /// Obtiene el tiempo de actividad
        /// </summary>
        public TimeSpan GetUptime() => _uptime.Elapsed;
        
        /// <summary>
        /// Genera un reporte de métricas
        /// </summary>
        public string GenerateReport()
        {
            var stats = GetAllStats();
            var report = new System.Text.StringBuilder();
            
            report.AppendLine("=== PERFORMANCE METRICS ===");
            report.AppendLine($"Uptime: {GetUptime():hh\\:mm\\:ss}");
            report.AppendLine($"Total Operations: {stats.Sum(s => s.Count)}");
            report.AppendLine();
            
            report.AppendLine("Top Operations by Total Time:");
            foreach (var stat in stats.Take(10))
            {
                report.AppendLine($"  {stat.Name}:");
                report.AppendLine($"    Count: {stat.Count}");
                report.AppendLine($"    Avg: {stat.AverageMs:F2}ms");
                report.AppendLine($"    Min: {stat.MinMs}ms");
                report.AppendLine($"    Max: {stat.MaxMs}ms");
                report.AppendLine($"    Total: {stat.TotalMs}ms");
            }
            
            return report.ToString();
        }
        
        /// <summary>
        /// Tracker de operación individual
        /// </summary>
        private class OperationTracker : IDisposable
        {
            private readonly PerformanceMetrics _metrics;
            private readonly string _operationName;
            private readonly Stopwatch _stopwatch;
            
            public OperationTracker(PerformanceMetrics metrics, string operationName)
            {
                _metrics = metrics;
                _operationName = operationName;
                _stopwatch = Stopwatch.StartNew();
            }
            
            public void Dispose()
            {
                _stopwatch.Stop();
                _metrics.RecordMetric(_operationName, _stopwatch.ElapsedMilliseconds);
            }
        }
        
        /// <summary>
        /// Datos internos de métrica
        /// </summary>
        private class MetricData
        {
            public string Name { get; set; } = string.Empty;
            public long Count { get; set; }
            public long TotalMs { get; set; }
            public long MinMs { get; set; }
            public long MaxMs { get; set; }
        }
    }
    
    /// <summary>
    /// Estadísticas de una métrica
    /// </summary>
    public class MetricStats
    {
        public string Name { get; set; } = string.Empty;
        public long Count { get; set; }
        public double AverageMs { get; set; }
        public long MinMs { get; set; }
        public long MaxMs { get; set; }
        public long TotalMs { get; set; }
        
        public override string ToString()
        {
            return $"{Name}: {Count} calls, avg {AverageMs:F2}ms (min {MinMs}ms, max {MaxMs}ms)";
        }
    }
}
