using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// Auto-tuning inteligente de parámetros basado en métricas
    /// Ajusta automáticamente maxParallelDownloads, timeouts, etc.
    /// </summary>
    public class AutoTuner
    {
        private readonly PerformanceMonitor monitor;
        private readonly System.Threading.Timer tuningTimer;
        private readonly Func<bool> isConnectedCheck;
        
        // Parámetros ajustables
        private int currentParallelDownloads;
        private int currentTimeout;
        private int currentBatchSize;
        
        // Rangos permitidos
        private readonly int minParallelDownloads = 1;
        private readonly int maxParallelDownloads = 30; // Aumentado de 20 a 30 para más velocidad
        private readonly int minTimeout = 10000;
        private readonly int maxTimeout = 120000;
        private readonly int minBatchSize = 100;
        private readonly int maxBatchSize = 2000;
        
        // Historial de ajustes
        private readonly ConcurrentQueue<TuningEvent> tuningHistory;
        private readonly int maxHistorySize = 100;
        
        // Métricas para decisiones
        private readonly ConcurrentQueue<PerformanceMetrics> metricsHistory;
        private readonly int metricsWindowSize = 20;
        
        // Eventos
        public event Action<TuningEvent> OnParameterAdjusted;
        
        public AutoTuner(PerformanceMonitor monitor, int initialParallelDownloads = 3, Func<bool> isConnectedCheck = null)
        {
            this.monitor = monitor;
            this.isConnectedCheck = isConnectedCheck ?? (() => true); // Default: siempre true si no se proporciona
            currentParallelDownloads = initialParallelDownloads;
            currentTimeout = 30000;
            currentBatchSize = 500;
            
            tuningHistory = new ConcurrentQueue<TuningEvent>();
            metricsHistory = new ConcurrentQueue<PerformanceMetrics>();
            
            // Timer para ajustar cada 30 segundos
            tuningTimer = new System.Threading.Timer(PerformTuning, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }
        
        /// <summary>
        /// Realiza ajuste automático de parámetros
        /// </summary>
        private void PerformTuning(object state)
        {
            try
            {
                // No ajustar si no está conectado
                if (!isConnectedCheck())
                {
                    return;
                }
                
                // Capturar métricas actuales
                var snapshot = monitor.GetCurrentSnapshot();
                var metrics = new PerformanceMetrics
                {
                    Timestamp = DateTime.UtcNow,
                    MemoryMB = snapshot.MemoryMB,
                    CpuPercent = snapshot.CpuPercent,
                    DownloadsPerMinute = CalculateDownloadsPerMinute(),
                    ErrorRate = CalculateErrorRate()
                };
                
                metricsHistory.Enqueue(metrics);
                while (metricsHistory.Count > metricsWindowSize)
                {
                    metricsHistory.TryDequeue(out _);
                }
                
                // Ajustar parámetros basado en métricas
                TuneParallelDownloads(metrics);
                TuneTimeout(metrics);
                TuneBatchSize(metrics);
            }
            catch
            {
                // Ignorar errores en tuning
            }
        }
        
        /// <summary>
        /// Ajusta maxParallelDownloads basado en CPU y RAM
        /// </summary>
        private void TuneParallelDownloads(PerformanceMetrics metrics)
        {
            var avgMetrics = GetAverageMetrics();
            
            // Si CPU y RAM están bajos, aumentar paralelismo
            if (avgMetrics.CpuPercent < 50 && avgMetrics.MemoryMB < 500)
            {
                if (currentParallelDownloads < maxParallelDownloads)
                {
                    var newValue = Math.Min(currentParallelDownloads + 1, maxParallelDownloads);
                    RecordTuning("maxParallelDownloads", currentParallelDownloads, newValue, 
                        $"CPU y RAM bajos ({avgMetrics.CpuPercent:F1}%, {avgMetrics.MemoryMB} MB)");
                    currentParallelDownloads = newValue;
                }
            }
            // Si CPU o RAM están altos, reducir paralelismo
            else if (avgMetrics.CpuPercent > 80 || avgMetrics.MemoryMB > 800)
            {
                if (currentParallelDownloads > minParallelDownloads)
                {
                    var newValue = Math.Max(currentParallelDownloads - 1, minParallelDownloads);
                    RecordTuning("maxParallelDownloads", currentParallelDownloads, newValue,
                        $"CPU o RAM altos ({avgMetrics.CpuPercent:F1}%, {avgMetrics.MemoryMB} MB)");
                    currentParallelDownloads = newValue;
                }
            }
        }
        
        /// <summary>
        /// Ajusta timeout basado en tasa de errores
        /// </summary>
        private void TuneTimeout(PerformanceMetrics metrics)
        {
            var avgMetrics = GetAverageMetrics();
            
            // Si tasa de errores es alta, aumentar timeout
            if (avgMetrics.ErrorRate > 0.3) // >30% errores
            {
                if (currentTimeout < maxTimeout)
                {
                    var newValue = Math.Min((int)(currentTimeout * 1.5), maxTimeout);
                    RecordTuning("timeout", currentTimeout, newValue,
                        $"Tasa de errores alta ({avgMetrics.ErrorRate:P0})");
                    currentTimeout = newValue;
                }
            }
            // Si tasa de errores es baja, reducir timeout
            else if (avgMetrics.ErrorRate < 0.05) // <5% errores
            {
                if (currentTimeout > minTimeout)
                {
                    var newValue = Math.Max((int)(currentTimeout * 0.8), minTimeout);
                    RecordTuning("timeout", currentTimeout, newValue,
                        $"Tasa de errores baja ({avgMetrics.ErrorRate:P0})");
                    currentTimeout = newValue;
                }
            }
        }
        
        /// <summary>
        /// Ajusta batch size basado en memoria
        /// </summary>
        private void TuneBatchSize(PerformanceMetrics metrics)
        {
            var avgMetrics = GetAverageMetrics();
            
            // Si memoria está baja, aumentar batch size
            if (avgMetrics.MemoryMB < 300)
            {
                if (currentBatchSize < maxBatchSize)
                {
                    var newValue = Math.Min(currentBatchSize + 100, maxBatchSize);
                    RecordTuning("batchSize", currentBatchSize, newValue,
                        $"Memoria baja ({avgMetrics.MemoryMB} MB)");
                    currentBatchSize = newValue;
                }
            }
            // Si memoria está alta, reducir batch size
            else if (avgMetrics.MemoryMB > 700)
            {
                if (currentBatchSize > minBatchSize)
                {
                    var newValue = Math.Max(currentBatchSize - 100, minBatchSize);
                    RecordTuning("batchSize", currentBatchSize, newValue,
                        $"Memoria alta ({avgMetrics.MemoryMB} MB)");
                    currentBatchSize = newValue;
                }
            }
        }
        
        /// <summary>
        /// Registra evento de tuning
        /// </summary>
        private void RecordTuning(string parameter, int oldValue, int newValue, string reason)
        {
            var tuningEvent = new TuningEvent
            {
                Timestamp = DateTime.UtcNow,
                Parameter = parameter,
                OldValue = oldValue,
                NewValue = newValue,
                Reason = reason
            };
            
            tuningHistory.Enqueue(tuningEvent);
            while (tuningHistory.Count > maxHistorySize)
            {
                tuningHistory.TryDequeue(out _);
            }
            
            OnParameterAdjusted?.Invoke(tuningEvent);
        }
        
        /// <summary>
        /// Calcula métricas promedio de la ventana
        /// </summary>
        private PerformanceMetrics GetAverageMetrics()
        {
            var metrics = metricsHistory.ToList();
            if (metrics.Count == 0)
            {
                return new PerformanceMetrics();
            }
            
            return new PerformanceMetrics
            {
                MemoryMB = (long)metrics.Average(m => m.MemoryMB),
                CpuPercent = metrics.Average(m => m.CpuPercent),
                DownloadsPerMinute = metrics.Average(m => m.DownloadsPerMinute),
                ErrorRate = metrics.Average(m => m.ErrorRate)
            };
        }
        
        /// <summary>
        /// Calcula descargas por minuto
        /// </summary>
        private double CalculateDownloadsPerMinute()
        {
            var snapshots = monitor.GetAllSnapshots();
            if (snapshots.Count < 2)
                return 0;
            
            var recent = snapshots.TakeLast(12).ToList(); // Últimos 60 segundos (5s * 12)
            if (recent.Count < 2)
                return 0;
            
            var first = recent.First();
            var last = recent.Last();
            var downloads = last.TotalDownloads - first.TotalDownloads;
            var minutes = (last.Timestamp - first.Timestamp).TotalMinutes;
            
            return minutes > 0 ? downloads / minutes : 0;
        }
        
        /// <summary>
        /// Calcula tasa de errores
        /// </summary>
        private double CalculateErrorRate()
        {
            var snapshot = monitor.GetCurrentSnapshot();
            var total = snapshot.TotalSearches + snapshot.TotalDownloads;
            
            return total > 0 ? (double)snapshot.TotalErrors / total : 0;
        }
        
        /// <summary>
        /// Obtiene parámetros actuales
        /// </summary>
        public TuningParameters GetCurrentParameters()
        {
            return new TuningParameters
            {
                ParallelDownloads = currentParallelDownloads,
                Timeout = currentTimeout,
                BatchSize = currentBatchSize
            };
        }
        
        /// <summary>
        /// Obtiene historial de ajustes
        /// </summary>
        public List<TuningEvent> GetTuningHistory()
        {
            return tuningHistory.ToList();
        }
        
        /// <summary>
        /// Genera reporte de tuning
        /// </summary>
        public string GenerateReport()
        {
            var current = GetCurrentParameters();
            var avgMetrics = GetAverageMetrics();
            var recentTuning = tuningHistory.TakeLast(10).ToList();
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("🎯 REPORTE DE AUTO-TUNING\n");
            sb.AppendLine("═══════════════════════════════════════\n");
            
            sb.AppendLine("⚙️ PARÁMETROS ACTUALES:");
            sb.AppendLine($"   • Descargas paralelas: {current.ParallelDownloads}");
            sb.AppendLine($"   • Timeout: {current.Timeout / 1000}s");
            sb.AppendLine($"   • Batch size: {current.BatchSize}\n");
            
            sb.AppendLine("📊 MÉTRICAS PROMEDIO:");
            sb.AppendLine($"   • CPU: {avgMetrics.CpuPercent:F1}%");
            sb.AppendLine($"   • RAM: {avgMetrics.MemoryMB} MB");
            sb.AppendLine($"   • Descargas/min: {avgMetrics.DownloadsPerMinute:F1}");
            sb.AppendLine($"   • Tasa de errores: {avgMetrics.ErrorRate:P1}\n");
            
            if (recentTuning.Count > 0)
            {
                sb.AppendLine("📝 AJUSTES RECIENTES:");
                foreach (var tuning in recentTuning)
                {
                    sb.AppendLine($"   • {tuning.Timestamp:HH:mm:ss} - {tuning.Parameter}: {tuning.OldValue} → {tuning.NewValue}");
                    sb.AppendLine($"     Razón: {tuning.Reason}");
                }
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Habilita/deshabilita auto-tuning
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                tuningTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(30));
            }
            else
            {
                tuningTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }
        
        public void Dispose()
        {
            tuningTimer?.Dispose();
        }
    }
    
    /// <summary>
    /// Métricas de rendimiento
    /// </summary>
    public class PerformanceMetrics
    {
        public DateTime Timestamp { get; set; }
        public long MemoryMB { get; set; }
        public double CpuPercent { get; set; }
        public double DownloadsPerMinute { get; set; }
        public double ErrorRate { get; set; }
    }
    
    /// <summary>
    /// Evento de ajuste de parámetro
    /// </summary>
    public class TuningEvent
    {
        public DateTime Timestamp { get; set; }
        public string Parameter { get; set; }
        public int OldValue { get; set; }
        public int NewValue { get; set; }
        public string Reason { get; set; }
    }
    
    /// <summary>
    /// Parámetros ajustables
    /// </summary>
    public class TuningParameters
    {
        public int ParallelDownloads { get; set; }
        public int Timeout { get; set; }
        public int BatchSize { get; set; }
    }
}
