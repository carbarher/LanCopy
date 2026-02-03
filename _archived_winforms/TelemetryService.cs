using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// Servicio de telemetría para monitoreo de rendimiento y uso
    /// Métricas: búsquedas, descargas, errores, latencia, throughput
    /// </summary>
    public class TelemetryService : IDisposable
    {
        private readonly string metricsDirectory;
        private readonly ConcurrentDictionary<string, MetricCounter> counters;
        private readonly ConcurrentDictionary<string, MetricHistogram> histograms;
        private readonly ConcurrentQueue<TelemetryEvent> eventQueue;
        private readonly System.Threading.Timer flushTimer;
        private readonly Stopwatch uptime;
        
        // Configuración
        private const int FLUSH_INTERVAL_MS = 60000; // 1 minuto
        private const int MAX_EVENTS_IN_MEMORY = 10000;
        
        public TelemetryService(string metricsDirectory)
        {
            this.metricsDirectory = metricsDirectory;
            this.counters = new ConcurrentDictionary<string, MetricCounter>();
            this.histograms = new ConcurrentDictionary<string, MetricHistogram>();
            this.eventQueue = new ConcurrentQueue<TelemetryEvent>();
            this.uptime = Stopwatch.StartNew();
            
            Directory.CreateDirectory(metricsDirectory);
            
            // Timer para flush periódico
            System.Threading.Timer timer = new System.Threading.Timer(_ => FlushMetrics(), null, FLUSH_INTERVAL_MS, FLUSH_INTERVAL_MS);
            this.flushTimer = timer;
            
            Console.WriteLine($"[Telemetry] Inicializado: {metricsDirectory}");
        }
        
        #region Counters
        
        /// <summary>
        /// Incrementa un contador
        /// </summary>
        public void IncrementCounter(string name, long value = 1)
        {
            var counter = counters.GetOrAdd(name, _ => new MetricCounter(name));
            counter.Increment(value);
        }
        
        /// <summary>
        /// Obtiene valor de contador
        /// </summary>
        public long GetCounter(string name)
        {
            return counters.TryGetValue(name, out var counter) ? counter.Value : 0;
        }
        
        #endregion
        
        #region Histograms
        
        /// <summary>
        /// Registra un valor en histograma (para latencias, tamaños, etc.)
        /// </summary>
        public void RecordValue(string name, double value)
        {
            var histogram = histograms.GetOrAdd(name, _ => new MetricHistogram(name));
            histogram.Record(value);
        }
        
        /// <summary>
        /// Obtiene estadísticas de histograma
        /// </summary>
        public HistogramStats GetHistogramStats(string name)
        {
            return histograms.TryGetValue(name, out var histogram) 
                ? histogram.GetStats() 
                : new HistogramStats();
        }
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Registra un evento de telemetría
        /// </summary>
        public void RecordEvent(string eventType, string message, Dictionary<string, object> metadata = null)
        {
            if (eventQueue.Count >= MAX_EVENTS_IN_MEMORY)
            {
                // Descartar eventos antiguos si hay demasiados
                eventQueue.TryDequeue(out _);
            }
            
            var evt = new TelemetryEvent
            {
                Timestamp = DateTime.Now,
                EventType = eventType,
                Message = message,
                Metadata = metadata ?? new Dictionary<string, object>()
            };
            
            eventQueue.Enqueue(evt);
        }
        
        #endregion
        
        #region Performance Tracking
        
        /// <summary>
        /// Mide el tiempo de ejecución de una operación
        /// </summary>
        public IDisposable MeasureOperation(string operationName)
        {
            return new OperationTimer(this, operationName);
        }
        
        private class OperationTimer : IDisposable
        {
            private readonly TelemetryService telemetry;
            private readonly string operationName;
            private readonly Stopwatch stopwatch;
            
            public OperationTimer(TelemetryService telemetry, string operationName)
            {
                this.telemetry = telemetry;
                this.operationName = operationName;
                this.stopwatch = Stopwatch.StartNew();
            }
            
            public void Dispose()
            {
                stopwatch.Stop();
                telemetry.RecordValue($"{operationName}.duration_ms", stopwatch.Elapsed.TotalMilliseconds);
                telemetry.IncrementCounter($"{operationName}.count");
            }
        }
        
        #endregion
        
        #region Reporting
        
        /// <summary>
        /// Obtiene reporte completo de métricas
        /// </summary>
        public string GetMetricsReport()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("═══════════════════════════════════════════════════════");
            sb.AppendLine("📊 TELEMETRÍA - REPORTE DE MÉTRICAS");
            sb.AppendLine("═══════════════════════════════════════════════════════");
            sb.AppendLine($"⏱️  Uptime: {FormatTimeSpan(uptime.Elapsed)}");
            sb.AppendLine($"📅 Generado: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            
            // Contadores
            if (counters.Any())
            {
                sb.AppendLine("📈 CONTADORES:");
                sb.AppendLine("───────────────────────────────────────────────────────");
                foreach (var kvp in counters.OrderBy(x => x.Key))
                {
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value.Value:N0}");
                }
                sb.AppendLine();
            }
            
            // Histogramas
            if (histograms.Any())
            {
                sb.AppendLine("📊 HISTOGRAMAS:");
                sb.AppendLine("───────────────────────────────────────────────────────");
                foreach (var kvp in histograms.OrderBy(x => x.Key))
                {
                    var stats = kvp.Value.GetStats();
                    sb.AppendLine($"  {kvp.Key}:");
                    sb.AppendLine($"    Count: {stats.Count:N0}");
                    sb.AppendLine($"    Min: {stats.Min:F2}");
                    sb.AppendLine($"    Max: {stats.Max:F2}");
                    sb.AppendLine($"    Avg: {stats.Average:F2}");
                    sb.AppendLine($"    P50: {stats.P50:F2}");
                    sb.AppendLine($"    P95: {stats.P95:F2}");
                    sb.AppendLine($"    P99: {stats.P99:F2}");
                }
                sb.AppendLine();
            }
            
            // Eventos recientes
            var recentEvents = eventQueue.TakeLast(10).ToList();
            if (recentEvents.Any())
            {
                sb.AppendLine("🔔 EVENTOS RECIENTES (últimos 10):");
                sb.AppendLine("───────────────────────────────────────────────────────");
                foreach (var evt in recentEvents)
                {
                    sb.AppendLine($"  [{evt.Timestamp:HH:mm:ss}] {evt.EventType}: {evt.Message}");
                }
            }
            
            sb.AppendLine("═══════════════════════════════════════════════════════");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Exporta métricas a JSON
        /// </summary>
        public async Task ExportMetricsAsync(string filePath)
        {
            var data = new
            {
                timestamp = DateTime.Now,
                uptime_seconds = uptime.Elapsed.TotalSeconds,
                counters = counters.ToDictionary(x => x.Key, x => x.Value.Value),
                histograms = histograms.ToDictionary(x => x.Key, x => x.Value.GetStats()),
                events = eventQueue.ToList()
            };
            
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }
        
        #endregion
        
        #region Persistence
        
        /// <summary>
        /// Guarda métricas en disco
        /// </summary>
        private void FlushMetrics()
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var filePath = Path.Combine(metricsDirectory, $"metrics_{timestamp}.json");
                
                ExportMetricsAsync(filePath).Wait();
                
                // Limpiar archivos antiguos (mantener últimos 7 días)
                CleanOldMetrics();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Telemetry] Error guardando métricas: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Limpia métricas antiguas
        /// </summary>
        private void CleanOldMetrics()
        {
            try
            {
                var files = Directory.GetFiles(metricsDirectory, "metrics_*.json");
                var cutoffDate = DateTime.Now.AddDays(-7);
                
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch { }
        }
        
        #endregion
        
        #region Helpers
        
        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalDays >= 1)
                return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";
            if (ts.TotalMinutes >= 1)
                return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
            return $"{ts.Seconds}s";
        }
        
        #endregion
        
        public void Dispose()
        {
            flushTimer?.Dispose();
            FlushMetrics();
        }
    }
    
    #region Metric Classes
    
    /// <summary>
    /// Contador de métricas
    /// </summary>
    public class MetricCounter
    {
        public string Name { get; }
        private long value;
        
        public long Value => value;
        
        public MetricCounter(string name)
        {
            Name = name;
        }
        
        public void Increment(long delta = 1)
        {
            Interlocked.Add(ref value, delta);
        }
    }
    
    /// <summary>
    /// Histograma de métricas
    /// </summary>
    public class MetricHistogram
    {
        public string Name { get; }
        private readonly ConcurrentBag<double> values;
        
        public MetricHistogram(string name)
        {
            Name = name;
            values = new ConcurrentBag<double>();
        }
        
        public void Record(double value)
        {
            values.Add(value);
        }
        
        public HistogramStats GetStats()
        {
            var sorted = values.OrderBy(x => x).ToList();
            
            if (sorted.Count == 0)
                return new HistogramStats();
            
            return new HistogramStats
            {
                Count = sorted.Count,
                Min = sorted.First(),
                Max = sorted.Last(),
                Average = sorted.Average(),
                P50 = GetPercentile(sorted, 0.50),
                P95 = GetPercentile(sorted, 0.95),
                P99 = GetPercentile(sorted, 0.99)
            };
        }
        
        private double GetPercentile(List<double> sorted, double percentile)
        {
            if (sorted.Count == 0) return 0;
            
            var index = (int)Math.Ceiling(sorted.Count * percentile) - 1;
            index = Math.Max(0, Math.Min(sorted.Count - 1, index));
            
            return sorted[index];
        }
    }
    
    /// <summary>
    /// Estadísticas de histograma
    /// </summary>
    public class HistogramStats
    {
        public int Count { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double Average { get; set; }
        public double P50 { get; set; }
        public double P95 { get; set; }
        public double P99 { get; set; }
    }
    
    /// <summary>
    /// Evento de telemetría
    /// </summary>
    public class TelemetryEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; }
        public string Message { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }
    
    #endregion
}
