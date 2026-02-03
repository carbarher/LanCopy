using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Servicio de métricas en tiempo real usando System.Diagnostics.Metrics
    /// Proporciona visibilidad completa del rendimiento de la aplicación
    /// </summary>
    public class RealTimeMetricsService
    {
        private static readonly Meter _meter = new("SlskDown", "1.0");
        
        // Contadores
        public static readonly Counter<long> SearchesTotal = 
            _meter.CreateCounter<long>("searches.total", "searches", "Total de búsquedas realizadas");
        
        public static readonly Counter<long> DownloadsTotal = 
            _meter.CreateCounter<long>("downloads.total", "downloads", "Total de descargas");
        
        public static readonly Counter<long> DownloadsCompleted = 
            _meter.CreateCounter<long>("downloads.completed", "downloads", "Descargas completadas");
        
        public static readonly Counter<long> DownloadsFailed = 
            _meter.CreateCounter<long>("downloads.failed", "downloads", "Descargas fallidas");
        
        public static readonly Counter<long> BytesDownloaded = 
            _meter.CreateCounter<long>("bytes.downloaded", "bytes", "Bytes descargados");
        
        // Histogramas (para medir distribuciones)
        public static readonly Histogram<double> SearchDuration = 
            _meter.CreateHistogram<double>("search.duration", "ms", "Duración de búsquedas");
        
        public static readonly Histogram<double> DownloadDuration = 
            _meter.CreateHistogram<double>("download.duration", "ms", "Duración de descargas");
        
        public static readonly Histogram<long> FileSize = 
            _meter.CreateHistogram<long>("file.size", "bytes", "Tamaño de archivos");
        
        public static readonly Histogram<double> DownloadSpeed = 
            _meter.CreateHistogram<double>("download.speed", "MB/s", "Velocidad de descarga");
        
        // Gauges observables (valores actuales)
        private static int _activeDownloads = 0;
        private static int _queuedDownloads = 0;
        private static int _activeSearches = 0;
        private static long _cacheHits = 0;
        private static long _cacheMisses = 0;
        
        public static readonly ObservableGauge<int> ActiveDownloads = 
            _meter.CreateObservableGauge("downloads.active", () => _activeDownloads, "downloads", "Descargas activas");
        
        public static readonly ObservableGauge<int> QueuedDownloads = 
            _meter.CreateObservableGauge("downloads.queued", () => _queuedDownloads, "downloads", "Descargas en cola");
        
        public static readonly ObservableGauge<int> ActiveSearches = 
            _meter.CreateObservableGauge("searches.active", () => _activeSearches, "searches", "Búsquedas activas");
        
        public static readonly ObservableGauge<double> CacheHitRate = 
            _meter.CreateObservableGauge("cache.hit_rate", () => CalculateCacheHitRate(), "ratio", "Tasa de aciertos de caché");

        // Métodos para actualizar gauges
        public static void IncrementActiveDownloads() => Interlocked.Increment(ref _activeDownloads);
        public static void DecrementActiveDownloads() => Interlocked.Decrement(ref _activeDownloads);
        public static void IncrementQueuedDownloads() => Interlocked.Increment(ref _queuedDownloads);
        public static void DecrementQueuedDownloads() => Interlocked.Decrement(ref _queuedDownloads);
        public static void IncrementActiveSearches() => Interlocked.Increment(ref _activeSearches);
        public static void DecrementActiveSearches() => Interlocked.Decrement(ref _activeSearches);
        public static void RecordCacheHit() => Interlocked.Increment(ref _cacheHits);
        public static void RecordCacheMiss() => Interlocked.Increment(ref _cacheMisses);

        private static double CalculateCacheHitRate()
        {
            var total = _cacheHits + _cacheMisses;
            return total > 0 ? (double)_cacheHits / total : 0;
        }

        /// <summary>
        /// Registra una búsqueda
        /// </summary>
        public static void RecordSearch(double durationMs, string searchType = "manual")
        {
            SearchesTotal.Add(1, new KeyValuePair<string, object?>("type", searchType));
            SearchDuration.Record(durationMs, new KeyValuePair<string, object?>("type", searchType));
        }

        /// <summary>
        /// Registra una descarga
        /// </summary>
        public static void RecordDownload(double durationMs, long bytes, bool success, string network = "soulseek")
        {
            if (success)
            {
                DownloadsCompleted.Add(1, new KeyValuePair<string, object?>("network", network));
                BytesDownloaded.Add(bytes, new KeyValuePair<string, object?>("network", network));
                
                var speedMBps = (bytes / 1024.0 / 1024.0) / (durationMs / 1000.0);
                DownloadSpeed.Record(speedMBps, new KeyValuePair<string, object?>("network", network));
            }
            else
            {
                DownloadsFailed.Add(1, new KeyValuePair<string, object?>("network", network));
            }

            DownloadDuration.Record(durationMs, 
                new KeyValuePair<string, object?>("network", network),
                new KeyValuePair<string, object?>("success", success));
            
            FileSize.Record(bytes, new KeyValuePair<string, object?>("network", network));
        }
    }

    /// <summary>
    /// Servicio de profiling automático
    /// </summary>
    public class AutoProfiler
    {
        private readonly ConcurrentDictionary<string, List<long>> _timings = new();
        private readonly ConcurrentDictionary<string, long> _counts = new();

        /// <summary>
        /// Ejecuta y perfila una operación
        /// </summary>
        public async Task<T> ProfileAsync<T>(string operation, Func<Task<T>> func)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                return await func();
            }
            finally
            {
                sw.Stop();
                RecordTiming(operation, sw.ElapsedMilliseconds);
                
                // Alertar si es lento
                if (sw.ElapsedMilliseconds > 1000)
                {
                    Debug.WriteLine($"Operación lenta: {operation} tomó {sw.ElapsedMilliseconds}ms");
                }
            }
        }

        /// <summary>
        /// Ejecuta y perfila una operación síncrona
        /// </summary>
        public T Profile<T>(string operation, Func<T> func)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                return func();
            }
            finally
            {
                sw.Stop();
                RecordTiming(operation, sw.ElapsedMilliseconds);
            }
        }

        private void RecordTiming(string operation, long milliseconds)
        {
            _timings.AddOrUpdate(
                operation,
                _ => new List<long> { milliseconds },
                (_, list) =>
                {
                    lock (list)
                    {
                        list.Add(milliseconds);
                        return list;
                    }
                });

            _counts.AddOrUpdate(operation, 1, (_, count) => count + 1);
        }

        /// <summary>
        /// Obtiene estadísticas de una operación
        /// </summary>
        public OperationStats? GetStats(string operation)
        {
            if (!_timings.TryGetValue(operation, out var timings))
                return null;

            lock (timings)
            {
                if (timings.Count == 0)
                    return null;

                var sorted = timings.OrderBy(t => t).ToList();
                
                return new OperationStats
                {
                    Operation = operation,
                    Count = timings.Count,
                    TotalMs = timings.Sum(),
                    AvgMs = timings.Average(),
                    MinMs = timings.Min(),
                    MaxMs = timings.Max(),
                    P50Ms = sorted[sorted.Count / 2],
                    P95Ms = sorted[(int)(sorted.Count * 0.95)],
                    P99Ms = sorted[(int)(sorted.Count * 0.99)]
                };
            }
        }

        /// <summary>
        /// Obtiene todas las estadísticas
        /// </summary>
        public List<OperationStats> GetAllStats()
        {
            return _timings.Keys
                .Select(op => GetStats(op))
                .Where(s => s != null)
                .OrderByDescending(s => s!.TotalMs)
                .ToList()!;
        }

        /// <summary>
        /// Imprime estadísticas
        /// </summary>
        public void PrintStats()
        {
            Debug.WriteLine("=== Performance Stats ===");
            
            foreach (var stats in GetAllStats())
            {
                Debug.WriteLine($"{stats.Operation}:");
                Debug.WriteLine($"  Count: {stats.Count}");
                Debug.WriteLine($"  Total: {stats.TotalMs}ms");
                Debug.WriteLine($"  Avg:   {stats.AvgMs:F1}ms");
            }
        }

        /// <summary>
        /// Limpia estadísticas
        /// </summary>
        public void Clear()
        {
            _timings.Clear();
            _counts.Clear();
        }
    }

    /// <summary>
    /// Monitor de rendimiento en tiempo real
    /// </summary>
    public class PerformanceMonitor
    {
        private readonly System.Threading.Timer _timer;
        private long _lastBytesDownloaded = 0;
        private DateTime _lastCheck = DateTime.UtcNow;

        public event EventHandler<PerformanceSnapshot>? SnapshotTaken;

        public PerformanceMonitor(TimeSpan interval)
        {
            _timer = new System.Threading.Timer(TakeSnapshot, null, interval, interval);
        }

        private void TakeSnapshot(object? state)
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastCheck).TotalSeconds;

            // Calcular velocidad actual
            var currentBytes = RealTimeMetricsService.BytesDownloaded.GetType()
                .GetField("_value", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(RealTimeMetricsService.BytesDownloaded) as long? ?? 0;

            var bytesThisPeriod = currentBytes - _lastBytesDownloaded;
            var speedMBps = (bytesThisPeriod / 1024.0 / 1024.0) / elapsed;

            var snapshot = new PerformanceSnapshot
            {
                Timestamp = now,
                ActiveDownloads = RealTimeMetricsService.ActiveDownloads.GetType()
                    .GetField("_callback", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(RealTimeMetricsService.ActiveDownloads) is Func<int> callback ? callback() : 0,
                CurrentSpeedMBps = speedMBps,
                TotalBytesDownloaded = currentBytes
            };

            _lastBytesDownloaded = currentBytes;
            _lastCheck = now;

            SnapshotTaken?.Invoke(this, snapshot);
        }

        public void Stop()
        {
            _timer.Dispose();
        }
    }

    #region DTOs

    public class OperationStats
    {
        public string Operation { get; set; } = "";
        public int Count { get; set; }
        public long TotalMs { get; set; }
        public double AvgMs { get; set; }
        public long MinMs { get; set; }
        public long MaxMs { get; set; }
        public long P50Ms { get; set; }
        public long P95Ms { get; set; }
        public long P99Ms { get; set; }
    }

    public class PerformanceSnapshot
    {
        public DateTime Timestamp { get; set; }
        public int ActiveDownloads { get; set; }
        public double CurrentSpeedMBps { get; set; }
        public long TotalBytesDownloaded { get; set; }
    }

    #endregion
}
