using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core
{
    /// <summary>
    /// Gestor de slots dinámicos que se ajusta según carga y rendimiento
    /// </summary>
    public class DynamicSlotManager
    {
        private int currentUploadSlots;
        private int currentDownloadSlots;
        private readonly int maxUploadSlots;
        private readonly int maxDownloadSlots;
        private readonly int minUploadSlots;
        private readonly int minDownloadSlots;
        
        private readonly List<PerformanceSample> samples;
        private readonly int maxSamples = 20;
        private bool isMonitoring;
        private CancellationTokenSource monitoringCancellation;
        
        public int CurrentUploadSlots => currentUploadSlots;
        public int CurrentDownloadSlots => currentDownloadSlots;
        
        public DynamicSlotManager(
            int initialUploadSlots = 5,
            int maxUploadSlots = 20,
            int minUploadSlots = 2,
            int initialDownloadSlots = 10,
            int maxDownloadSlots = 500,
            int minDownloadSlots = 5)
        {
            this.currentUploadSlots = initialUploadSlots;
            this.currentDownloadSlots = initialDownloadSlots;
            this.maxUploadSlots = maxUploadSlots;
            this.maxDownloadSlots = maxDownloadSlots;
            this.minUploadSlots = minUploadSlots;
            this.minDownloadSlots = minDownloadSlots;
            
            samples = new List<PerformanceSample>();
        }
        
        public class PerformanceSample
        {
            public DateTime Timestamp { get; set; }
            public int ActiveUploads { get; set; }
            public int ActiveDownloads { get; set; }
            public double AvgUploadSpeed { get; set; }
            public double AvgDownloadSpeed { get; set; }
            public double CpuUsage { get; set; }
            public long MemoryUsageMB { get; set; }
        }
        
        /// <summary>
        /// Registra muestra de rendimiento
        /// </summary>
        public void RecordSample(
            int activeUploads,
            int activeDownloads,
            double avgUploadSpeed,
            double avgDownloadSpeed,
            double cpuUsage = 0,
            long memoryUsageMB = 0)
        {
            samples.Add(new PerformanceSample
            {
                Timestamp = DateTime.Now,
                ActiveUploads = activeUploads,
                ActiveDownloads = activeDownloads,
                AvgUploadSpeed = avgUploadSpeed,
                AvgDownloadSpeed = avgDownloadSpeed,
                CpuUsage = cpuUsage,
                MemoryUsageMB = memoryUsageMB
            });
            
            // Mantener solo las últimas N muestras
            if (samples.Count > maxSamples)
                samples.RemoveAt(0);
        }
        
        /// <summary>
        /// Ajusta slots basado en rendimiento
        /// </summary>
        public (int uploadSlots, int downloadSlots) AdjustSlots()
        {
            if (samples.Count < 5)
                return (currentUploadSlots, currentDownloadSlots);
            
            var recentSamples = samples.TakeLast(10).ToList();
            
            // Calcular promedios
            var avgUploadSpeed = recentSamples.Average(s => s.AvgUploadSpeed);
            var avgDownloadSpeed = recentSamples.Average(s => s.AvgDownloadSpeed);
            var avgCpu = recentSamples.Average(s => s.CpuUsage);
            var avgMemory = recentSamples.Average(s => s.MemoryUsageMB);
            
            // Ajustar uploads
            if (avgCpu < 50 && avgMemory < 1024 && avgUploadSpeed > 1.0)
            {
                // Sistema tiene recursos, aumentar slots
                currentUploadSlots = Math.Min(currentUploadSlots + 1, maxUploadSlots);
            }
            else if (avgCpu > 80 || avgMemory > 2048 || avgUploadSpeed < 0.5)
            {
                // Sistema sobrecargado, reducir slots
                currentUploadSlots = Math.Max(currentUploadSlots - 1, minUploadSlots);
            }
            
            // Ajustar downloads
            if (avgCpu < 60 && avgMemory < 1536 && avgDownloadSpeed > 2.0)
            {
                currentDownloadSlots = Math.Min(currentDownloadSlots + 5, maxDownloadSlots);
            }
            else if (avgCpu > 85 || avgMemory > 3072 || avgDownloadSpeed < 1.0)
            {
                currentDownloadSlots = Math.Max(currentDownloadSlots - 5, minDownloadSlots);
            }
            
            return (currentUploadSlots, currentDownloadSlots);
        }
        
        /// <summary>
        /// Verifica si hay slots disponibles
        /// </summary>
        public bool HasUploadSlot(int currentActive)
        {
            return currentActive < currentUploadSlots;
        }
        
        public bool HasDownloadSlot(int currentActive)
        {
            return currentActive < currentDownloadSlots;
        }
        
        /// <summary>
        /// Obtiene estadísticas
        /// </summary>
        public object GetStats()
        {
            if (samples.Count == 0)
                return new { currentUploadSlots, currentDownloadSlots };
            
            var recent = samples.TakeLast(10).ToList();
            
            return new
            {
                currentUploadSlots,
                currentDownloadSlots,
                maxUploadSlots,
                maxDownloadSlots,
                avgUploadSpeed = recent.Average(s => s.AvgUploadSpeed),
                avgDownloadSpeed = recent.Average(s => s.AvgDownloadSpeed),
                avgCpuUsage = recent.Average(s => s.CpuUsage),
                avgMemoryMB = recent.Average(s => s.MemoryUsageMB),
                samplesCollected = samples.Count
            };
        }
        
        /// <summary>
        /// Fuerza un número específico de slots
        /// </summary>
        public void SetSlots(int uploadSlots, int downloadSlots)
        {
            currentUploadSlots = Math.Clamp(uploadSlots, minUploadSlots, maxUploadSlots);
            currentDownloadSlots = Math.Clamp(downloadSlots, minDownloadSlots, maxDownloadSlots);
        }
        
        /// <summary>
        /// Inicia monitoreo automático
        /// </summary>
        public void StartMonitoring(int intervalSeconds = 60)
        {
            if (isMonitoring)
                return;
            
            isMonitoring = true;
            monitoringCancellation = new CancellationTokenSource();
            
            Task.Run(async () =>
            {
                while (isMonitoring && !monitoringCancellation.Token.IsCancellationRequested)
                {
                    try
                    {
                        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                        {
                            await Task.Run(() => AdjustSlots(), cts.Token).ConfigureAwait(false);
                        }
                        await Task.Delay(intervalSeconds * 1000, monitoringCancellation.Token).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        // Timeout en AdjustSlots, continuar
                    }
                    catch
                    {
                        // Continuar monitoreando
                    }
                }
            }, monitoringCancellation.Token).ConfigureAwait(false);
        }
    }
}
