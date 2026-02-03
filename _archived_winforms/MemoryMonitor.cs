using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// Monitor de memoria con alertas y limpieza automÃ¡tica
    /// </summary>
    public class MemoryMonitor : IDisposable
    {
        private readonly long _warningThresholdMB;
        private readonly long _criticalThresholdMB;
        private readonly System.Threading.Timer _monitorTimer;
        private long _peakMemoryMB;
        private int _gcCollections;
        private DateTime _lastGC;

        public event EventHandler<MemoryWarningEventArgs> MemoryWarning;
        public event EventHandler<MemoryWarningEventArgs> MemoryCritical;

        public MemoryMonitor(long warningThresholdMB = 500, long criticalThresholdMB = 1000, int checkIntervalSeconds = 30)
        {
            _warningThresholdMB = warningThresholdMB;
            _criticalThresholdMB = criticalThresholdMB;
            _lastGC = DateTime.Now;

            _monitorTimer = new System.Threading.Timer(CheckMemory, null, TimeSpan.Zero, TimeSpan.FromSeconds(checkIntervalSeconds));
        }

        private void CheckMemory(object state)
        {
            var currentMemoryMB = GetCurrentMemoryMB();
            
            // Actualizar pico
            if (currentMemoryMB > _peakMemoryMB)
            {
                _peakMemoryMB = currentMemoryMB;
            }

            // Verificar umbrales
            if (currentMemoryMB >= _criticalThresholdMB)
            {
                OnMemoryCritical(new MemoryWarningEventArgs
                {
                    CurrentMemoryMB = currentMemoryMB,
                    ThresholdMB = _criticalThresholdMB,
                    Level = MemoryWarningLevel.Critical
                });

                // GC agresivo en nivel crÃ­tico
                ForceGarbageCollection(aggressive: true);
            }
            else if (currentMemoryMB >= _warningThresholdMB)
            {
                OnMemoryWarning(new MemoryWarningEventArgs
                {
                    CurrentMemoryMB = currentMemoryMB,
                    ThresholdMB = _warningThresholdMB,
                    Level = MemoryWarningLevel.Warning
                });

                // GC suave en warning
                ForceGarbageCollection(aggressive: false);
            }
        }

        /// <summary>
        /// Obtiene la memoria actual en MB
        /// </summary>
        public long GetCurrentMemoryMB()
        {
            return GC.GetTotalMemory(false) / (1024 * 1024);
        }

        /// <summary>
        /// Fuerza una recolecciÃ³n de basura
        /// </summary>
        public void ForceGarbageCollection(bool aggressive = false)
        {
            var timeSinceLastGC = DateTime.Now - _lastGC;
            
            // No hacer GC muy frecuentemente (mÃ­nimo 30 segundos)
            if (timeSinceLastGC.TotalSeconds < 30)
                return;

            if (aggressive)
            {
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);
            }
            else
            {
                GC.Collect(1, GCCollectionMode.Optimized, false);
            }

            _gcCollections++;
            _lastGC = DateTime.Now;
        }

        /// <summary>
        /// Obtiene estadÃ­sticas de memoria
        /// </summary>
        public MemoryStats GetStats()
        {
            var process = Process.GetCurrentProcess();
            
            return new MemoryStats
            {
                CurrentMemoryMB = GetCurrentMemoryMB(),
                PeakMemoryMB = _peakMemoryMB,
                WorkingSetMB = process.WorkingSet64 / (1024 * 1024),
                PrivateMemoryMB = process.PrivateMemorySize64 / (1024 * 1024),
                GCCollections = _gcCollections,
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
                LastGC = _lastGC
            };
        }

        /// <summary>
        /// Obtiene recomendaciones de optimizaciÃ³n
        /// </summary>
        public string GetOptimizationRecommendations()
        {
            var stats = GetStats();
            var recommendations = new System.Text.StringBuilder();

            if (stats.CurrentMemoryMB > _warningThresholdMB)
            {
                recommendations.AppendLine("âš ï¸ Memoria alta:");
                recommendations.AppendLine("  â€¢ Considera limpiar resultados antiguos");
                recommendations.AppendLine("  â€¢ Reduce el lÃ­mite de resultados por bÃºsqueda");
            }

            if (stats.Gen2Collections > 10)
            {
                recommendations.AppendLine("âš ï¸ Muchas colecciones Gen2:");
                recommendations.AppendLine("  â€¢ Hay objetos de larga duraciÃ³n");
                recommendations.AppendLine("  â€¢ Considera usar pools de objetos");
            }

            var gen0Rate = stats.Gen0Collections / Math.Max(1, stats.Gen2Collections);
            if (gen0Rate < 10)
            {
                recommendations.AppendLine("âš ï¸ Ratio Gen0/Gen2 bajo:");
                recommendations.AppendLine("  â€¢ Muchos objetos sobreviven a Gen0");
                recommendations.AppendLine("  â€¢ Revisa allocaciones grandes");
            }

            return recommendations.Length > 0 ? recommendations.ToString() : "âœ… Memoria en buen estado";
        }

        protected virtual void OnMemoryWarning(MemoryWarningEventArgs e)
        {
            MemoryWarning?.Invoke(this, e);
        }

        protected virtual void OnMemoryCritical(MemoryWarningEventArgs e)
        {
            MemoryCritical?.Invoke(this, e);
        }

        public void Dispose()
        {
            _monitorTimer?.Dispose();
        }
    }

    public class MemoryWarningEventArgs : EventArgs
    {
        public long CurrentMemoryMB { get; set; }
        public long ThresholdMB { get; set; }
        public MemoryWarningLevel Level { get; set; }
    }

    public enum MemoryWarningLevel
    {
        Normal,
        Warning,
        Critical
    }

    public class MemoryStats
    {
        public long CurrentMemoryMB { get; set; }
        public long PeakMemoryMB { get; set; }
        public long WorkingSetMB { get; set; }
        public long PrivateMemoryMB { get; set; }
        public int GCCollections { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        public DateTime LastGC { get; set; }
    }
}

