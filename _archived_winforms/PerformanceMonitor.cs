using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// Monitor de rendimiento en tiempo real con métricas y export
    /// </summary>
    public class PerformanceMonitor
    {
        private readonly ConcurrentQueue<PerformanceSnapshot> snapshots;
        private readonly System.Threading.Timer metricsTimer;
        private readonly Process currentProcess;
        private readonly PerformanceCounter cpuCounter;
        private readonly int maxSnapshots;
        
        // Métricas acumuladas
        private long totalSearches;
        private long totalDownloads;
        private long totalBytesDownloaded;
        private long totalErrors;
        private readonly ConcurrentDictionary<string, long> operationTimes;
        
        // Alertas
        public event Action<PerformanceAlert> OnAlert;
        
        // Thresholds para alertas
        private const long HIGH_MEMORY_MB = 1000;
        private const double HIGH_CPU_PERCENT = 80.0;
        private const long LOW_DISK_SPACE_MB = 1000;
        
        public PerformanceMonitor(int maxSnapshots = 1000)
        {
            this.maxSnapshots = maxSnapshots;
            snapshots = new ConcurrentQueue<PerformanceSnapshot>();
            operationTimes = new ConcurrentDictionary<string, long>();
            currentProcess = Process.GetCurrentProcess();
            
            try
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            }
            catch
            {
                // CPU counter no disponible en algunos sistemas
            }
            
            // Timer para capturar métricas cada 5 segundos
            metricsTimer = new System.Threading.Timer(CaptureMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }
        
        /// <summary>
        /// Captura snapshot de métricas actuales
        /// </summary>
        private void CaptureMetrics(object state)
        {
            try
            {
                var snapshot = new PerformanceSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    MemoryMB = currentProcess.WorkingSet64 / 1024 / 1024,
                    CpuPercent = cpuCounter?.NextValue() ?? 0,
                    ThreadCount = currentProcess.Threads.Count,
                    HandleCount = currentProcess.HandleCount,
                    TotalSearches = totalSearches,
                    TotalDownloads = totalDownloads,
                    TotalBytesDownloaded = totalBytesDownloaded,
                    TotalErrors = totalErrors
                };
                
                snapshots.Enqueue(snapshot);
                
                // Mantener solo los últimos N snapshots
                while (snapshots.Count > maxSnapshots)
                {
                    snapshots.TryDequeue(out _);
                }
                
                // Verificar alertas
                CheckAlerts(snapshot);
            }
            catch
            {
                // Ignorar errores en captura de métricas
            }
        }
        
        /// <summary>
        /// Verifica condiciones de alerta
        /// </summary>
        private void CheckAlerts(PerformanceSnapshot snapshot)
        {
            // Alerta de memoria alta
            if (snapshot.MemoryMB > HIGH_MEMORY_MB)
            {
                OnAlert?.Invoke(new PerformanceAlert
                {
                    Type = AlertType.HighMemory,
                    Message = $"Uso de memoria alto: {snapshot.MemoryMB} MB",
                    Severity = AlertSeverity.Warning,
                    Value = snapshot.MemoryMB
                });
            }
            
            // Alerta de CPU alto
            if (snapshot.CpuPercent > HIGH_CPU_PERCENT)
            {
                OnAlert?.Invoke(new PerformanceAlert
                {
                    Type = AlertType.HighCpu,
                    Message = $"Uso de CPU alto: {snapshot.CpuPercent:F1}%",
                    Severity = AlertSeverity.Warning,
                    Value = snapshot.CpuPercent
                });
            }
            
            // Alerta de espacio en disco bajo
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory));
                var freeSpaceMB = drive.AvailableFreeSpace / 1024 / 1024;
                
                if (freeSpaceMB < LOW_DISK_SPACE_MB)
                {
                    OnAlert?.Invoke(new PerformanceAlert
                    {
                        Type = AlertType.LowDiskSpace,
                        Message = $"Espacio en disco bajo: {freeSpaceMB} MB",
                        Severity = AlertSeverity.Critical,
                        Value = freeSpaceMB
                    });
                }
            }
            catch { }
        }
        
        /// <summary>
        /// Registra tiempo de una operación
        /// </summary>
        public void RecordOperation(string operationName, long milliseconds)
        {
            operationTimes.AddOrUpdate(operationName, milliseconds, (key, old) => (old + milliseconds) / 2);
        }
        
        /// <summary>
        /// Mide tiempo de ejecución de una operación
        /// </summary>
        public async Task<T> MeasureAsync<T>(string operationName, Func<Task<T>> operation)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                return await operation();
            }
            finally
            {
                sw.Stop();
                RecordOperation(operationName, sw.ElapsedMilliseconds);
            }
        }
        
        /// <summary>
        /// Incrementa contador de búsquedas
        /// </summary>
        public void IncrementSearches() => Interlocked.Increment(ref totalSearches);
        
        /// <summary>
        /// Incrementa contador de descargas
        /// </summary>
        public void IncrementDownloads() => Interlocked.Increment(ref totalDownloads);
        
        /// <summary>
        /// Incrementa bytes descargados
        /// </summary>
        public void AddBytesDownloaded(long bytes) => Interlocked.Add(ref totalBytesDownloaded, bytes);
        
        /// <summary>
        /// Incrementa contador de errores
        /// </summary>
        public void IncrementErrors() => Interlocked.Increment(ref totalErrors);
        
        /// <summary>
        /// Obtiene snapshot actual
        /// </summary>
        public PerformanceSnapshot GetCurrentSnapshot()
        {
            return snapshots.LastOrDefault() ?? new PerformanceSnapshot();
        }
        
        /// <summary>
        /// Obtiene todos los snapshots
        /// </summary>
        public List<PerformanceSnapshot> GetAllSnapshots()
        {
            return snapshots.ToList();
        }
        
        /// <summary>
        /// Obtiene estadísticas de operaciones
        /// </summary>
        public Dictionary<string, long> GetOperationStats()
        {
            return operationTimes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        
        /// <summary>
        /// Genera reporte de rendimiento
        /// </summary>
        public string GenerateReport()
        {
            var current = GetCurrentSnapshot();
            var sb = new StringBuilder();
            
            sb.AppendLine("📊 REPORTE DE RENDIMIENTO");
            sb.AppendLine("═══════════════════════════════════════\n");
            
            sb.AppendLine("💻 SISTEMA:");
            sb.AppendLine($"   • Memoria: {current.MemoryMB} MB");
            sb.AppendLine($"   • CPU: {current.CpuPercent:F1}%");
            sb.AppendLine($"   • Threads: {current.ThreadCount}");
            sb.AppendLine($"   • Handles: {current.HandleCount}\n");
            
            sb.AppendLine("📈 ACTIVIDAD:");
            sb.AppendLine($"   • Búsquedas: {current.TotalSearches:N0}");
            sb.AppendLine($"   • Descargas: {current.TotalDownloads:N0}");
            sb.AppendLine($"   • Bytes: {FormatBytes(current.TotalBytesDownloaded)}");
            sb.AppendLine($"   • Errores: {current.TotalErrors:N0}\n");
            
            sb.AppendLine("⏱️ OPERACIONES (tiempo promedio):");
            foreach (var op in operationTimes.OrderByDescending(kvp => kvp.Value))
            {
                sb.AppendLine($"   • {op.Key}: {op.Value} ms");
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Exporta métricas a CSV
        /// </summary>
        public void ExportToCSV(string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,MemoryMB,CpuPercent,ThreadCount,HandleCount,TotalSearches,TotalDownloads,TotalBytesDownloaded,TotalErrors");
            
            foreach (var snapshot in snapshots)
            {
                sb.AppendLine($"{snapshot.Timestamp:O},{snapshot.MemoryMB},{snapshot.CpuPercent:F2},{snapshot.ThreadCount},{snapshot.HandleCount},{snapshot.TotalSearches},{snapshot.TotalDownloads},{snapshot.TotalBytesDownloaded},{snapshot.TotalErrors}");
            }
            
            File.WriteAllText(filePath, sb.ToString());
        }
        
        /// <summary>
        /// Exporta métricas a JSON
        /// </summary>
        public void ExportToJSON(string filePath)
        {
            var data = new
            {
                ExportDate = DateTime.UtcNow,
                Snapshots = snapshots.ToList(),
                OperationStats = operationTimes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            File.WriteAllText(filePath, json);
        }
        
        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
        
        public void Dispose()
        {
            metricsTimer?.Dispose();
            cpuCounter?.Dispose();
        }
    }
    
    /// <summary>
    /// Snapshot de métricas en un momento dado
    /// </summary>
    public class PerformanceSnapshot
    {
        public DateTime Timestamp { get; set; }
        public long MemoryMB { get; set; }
        public double CpuPercent { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public long TotalSearches { get; set; }
        public long TotalDownloads { get; set; }
        public long TotalBytesDownloaded { get; set; }
        public long TotalErrors { get; set; }
    }
    
    /// <summary>
    /// Alerta de rendimiento
    /// </summary>
    public class PerformanceAlert
    {
        public AlertType Type { get; set; }
        public string Message { get; set; }
        public AlertSeverity Severity { get; set; }
        public double Value { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
    
    public enum AlertType
    {
        HighMemory,
        HighCpu,
        LowDiskSpace,
        HighErrorRate,
        SlowOperation
    }
    
    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }
}
