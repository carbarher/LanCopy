using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime;

namespace SlskDown.Core.Optimization
{
    /// <summary>
    /// Monitor de rendimiento en tiempo real
    /// Tracking de CPU, memoria, GC, y operaciones
    /// </summary>
    public class PerformanceMonitor
    {
        private readonly ConcurrentDictionary<string, OperationStats> operations = new();
        private readonly Process currentProcess;
        private DateTime lastGCCheck;
        private long lastGCMemory;
        
        public PerformanceMonitor()
        {
            currentProcess = Process.GetCurrentProcess();
            lastGCCheck = DateTime.Now;
            lastGCMemory = GC.GetTotalMemory(false);
        }
        
        private class OperationStats
        {
            public long Count { get; set; }
            public long TotalMs { get; set; }
            public long MinMs { get; set; } = long.MaxValue;
            public long MaxMs { get; set; }
            public DateTime LastExecution { get; set; }
        }
        
        /// <summary>
        /// Mide duración de operación
        /// </summary>
        public IDisposable Measure(string operationName)
        {
            return new OperationMeasurer(this, operationName);
        }
        
        private void RecordOperation(string name, long durationMs)
        {
            operations.AddOrUpdate(name,
                new OperationStats
                {
                    Count = 1,
                    TotalMs = durationMs,
                    MinMs = durationMs,
                    MaxMs = durationMs,
                    LastExecution = DateTime.Now
                },
                (_, stats) =>
                {
                    stats.Count++;
                    stats.TotalMs += durationMs;
                    stats.MinMs = Math.Min(stats.MinMs, durationMs);
                    stats.MaxMs = Math.Max(stats.MaxMs, durationMs);
                    stats.LastExecution = DateTime.Now;
                    return stats;
                });
        }
        
        /// <summary>
        /// Obtiene métricas del sistema
        /// </summary>
        public SystemMetrics GetSystemMetrics()
        {
            currentProcess.Refresh();
            
            var gcInfo = GC.GetGCMemoryInfo();
            var currentMemory = GC.GetTotalMemory(false);
            var timeSinceLastGC = DateTime.Now - lastGCCheck;
            var memoryDelta = currentMemory - lastGCMemory;
            
            lastGCCheck = DateTime.Now;
            lastGCMemory = currentMemory;
            
            return new SystemMetrics
            {
                CpuUsagePercent = GetCpuUsage(),
                WorkingSetMB = currentProcess.WorkingSet64 / 1024.0 / 1024.0,
                PrivateMemoryMB = currentProcess.PrivateMemorySize64 / 1024.0 / 1024.0,
                ManagedMemoryMB = currentMemory / 1024.0 / 1024.0,
                GCGen0Collections = GC.CollectionCount(0),
                GCGen1Collections = GC.CollectionCount(1),
                GCGen2Collections = GC.CollectionCount(2),
                HeapSizeMB = gcInfo.HeapSizeBytes / 1024.0 / 1024.0,
                FragmentedMB = gcInfo.FragmentedBytes / 1024.0 / 1024.0,
                MemoryLoadPercent = gcInfo.MemoryLoadBytes * 100.0 / gcInfo.TotalAvailableMemoryBytes,
                ThreadCount = currentProcess.Threads.Count,
                HandleCount = currentProcess.HandleCount
            };
        }
        
        private double GetCpuUsage()
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var startCpuUsage = currentProcess.TotalProcessorTime;
                
                System.Threading.Thread.Sleep(100);
                
                var endTime = DateTime.UtcNow;
                var endCpuUsage = currentProcess.TotalProcessorTime;
                
                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                
                return cpuUsageTotal * 100;
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// Obtiene estadísticas de operaciones
        /// </summary>
        public OperationReport GetOperationReport()
        {
            var report = new OperationReport();
            
            foreach (var kvp in operations)
            {
                var stats = kvp.Value;
                report.Operations.Add(new OperationInfo
                {
                    Name = kvp.Key,
                    Count = stats.Count,
                    AverageMs = stats.Count > 0 ? stats.TotalMs / (double)stats.Count : 0,
                    MinMs = stats.MinMs == long.MaxValue ? 0 : stats.MinMs,
                    MaxMs = stats.MaxMs,
                    LastExecution = stats.LastExecution
                });
            }
            
            return report;
        }
        
        /// <summary>
        /// Fuerza garbage collection y compacta heap
        /// </summary>
        public void OptimizeMemory()
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Aggressive, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Aggressive, true, true);
        }
        
        /// <summary>
        /// Resetea estadísticas
        /// </summary>
        public void Reset()
        {
            operations.Clear();
        }
        
        private class OperationMeasurer : IDisposable
        {
            private readonly PerformanceMonitor monitor;
            private readonly string operationName;
            private readonly Stopwatch stopwatch;
            
            public OperationMeasurer(PerformanceMonitor monitor, string operationName)
            {
                this.monitor = monitor;
                this.operationName = operationName;
                stopwatch = Stopwatch.StartNew();
            }
            
            public void Dispose()
            {
                stopwatch.Stop();
                monitor.RecordOperation(operationName, stopwatch.ElapsedMilliseconds);
            }
        }
        
        public class SystemMetrics
        {
            public double CpuUsagePercent { get; set; }
            public double WorkingSetMB { get; set; }
            public double PrivateMemoryMB { get; set; }
            public double ManagedMemoryMB { get; set; }
            public int GCGen0Collections { get; set; }
            public int GCGen1Collections { get; set; }
            public int GCGen2Collections { get; set; }
            public double HeapSizeMB { get; set; }
            public double FragmentedMB { get; set; }
            public double MemoryLoadPercent { get; set; }
            public int ThreadCount { get; set; }
            public int HandleCount { get; set; }
        }
        
        public class OperationReport
        {
            public List<OperationInfo> Operations { get; set; } = new();
        }
        
        public class OperationInfo
        {
            public string Name { get; set; }
            public long Count { get; set; }
            public double AverageMs { get; set; }
            public long MinMs { get; set; }
            public long MaxMs { get; set; }
            public DateTime LastExecution { get; set; }
        }
    }
}
