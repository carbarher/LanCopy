using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace SlskDown.Telemetry
{
    /// <summary>
    /// Sistema de health checks para monitorear el estado de la aplicación
    /// </summary>
    public class HealthCheckService
    {
        private static readonly Lazy<HealthCheckService> _instance = 
            new(() => new HealthCheckService());
        
        public static HealthCheckService Instance => _instance.Value;
        
        private readonly List<IHealthCheck> _healthChecks = new();
        private readonly Process _currentProcess;
        
        private HealthCheckService()
        {
            _currentProcess = Process.GetCurrentProcess();
            
            // Registrar health checks por defecto
            RegisterDefaultHealthChecks();
        }
        
        /// <summary>
        /// Registra los health checks por defecto
        /// </summary>
        private void RegisterDefaultHealthChecks()
        {
            Register(new MemoryHealthCheck());
            Register(new DiskSpaceHealthCheck());
            Register(new ThreadPoolHealthCheck());
        }
        
        /// <summary>
        /// Registra un health check
        /// </summary>
        public void Register(IHealthCheck healthCheck)
        {
            if (!_healthChecks.Contains(healthCheck))
            {
                _healthChecks.Add(healthCheck);
            }
        }
        
        /// <summary>
        /// Ejecuta todos los health checks
        /// </summary>
        public async Task<HealthCheckReport> CheckHealthAsync()
        {
            var results = new List<HealthCheckResult>();
            
            foreach (var check in _healthChecks)
            {
                try
                {
                    var result = await check.CheckHealthAsync();
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    results.Add(new HealthCheckResult
                    {
                        Name = check.Name,
                        Status = HealthStatus.Unhealthy,
                        Description = $"Health check failed: {ex.Message}",
                        Exception = ex
                    });
                }
            }
            
            var overallStatus = DetermineOverallStatus(results);
            
            return new HealthCheckReport
            {
                Status = overallStatus,
                Results = results,
                Timestamp = DateTime.UtcNow,
                Duration = TimeSpan.Zero // Se puede mejorar con tracking
            };
        }
        
        /// <summary>
        /// Determina el estado general basado en los resultados
        /// </summary>
        private HealthStatus DetermineOverallStatus(List<HealthCheckResult> results)
        {
            if (results.Any(r => r.Status == HealthStatus.Unhealthy))
                return HealthStatus.Unhealthy;
            
            if (results.Any(r => r.Status == HealthStatus.Degraded))
                return HealthStatus.Degraded;
            
            return HealthStatus.Healthy;
        }
        
        /// <summary>
        /// Obtiene métricas del sistema
        /// </summary>
        public SystemMetrics GetSystemMetrics()
        {
            _currentProcess.Refresh();
            
            return new SystemMetrics
            {
                WorkingSetMB = _currentProcess.WorkingSet64 / 1024.0 / 1024.0,
                PrivateMemoryMB = _currentProcess.PrivateMemorySize64 / 1024.0 / 1024.0,
                CpuTimeSeconds = _currentProcess.TotalProcessorTime.TotalSeconds,
                ThreadCount = _currentProcess.Threads.Count,
                HandleCount = _currentProcess.HandleCount,
                Uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()
            };
        }
    }
    
    /// <summary>
    /// Interfaz para health checks
    /// </summary>
    public interface IHealthCheck
    {
        string Name { get; }
        Task<HealthCheckResult> CheckHealthAsync();
    }
    
    /// <summary>
    /// Health check de memoria
    /// </summary>
    public class MemoryHealthCheck : IHealthCheck
    {
        public string Name => "Memory";
        
        public Task<HealthCheckResult> CheckHealthAsync()
        {
            var process = Process.GetCurrentProcess();
            var workingSetMB = process.WorkingSet64 / 1024.0 / 1024.0;
            
            HealthStatus status;
            string description;
            
            if (workingSetMB > 1000) // > 1GB
            {
                status = HealthStatus.Unhealthy;
                description = $"High memory usage: {workingSetMB:F2} MB";
            }
            else if (workingSetMB > 500) // > 500MB
            {
                status = HealthStatus.Degraded;
                description = $"Elevated memory usage: {workingSetMB:F2} MB";
            }
            else
            {
                status = HealthStatus.Healthy;
                description = $"Memory usage: {workingSetMB:F2} MB";
            }
            
            return Task.FromResult(new HealthCheckResult
            {
                Name = Name,
                Status = status,
                Description = description,
                Data = new Dictionary<string, object>
                {
                    ["WorkingSetMB"] = workingSetMB,
                    ["GCTotalMemoryMB"] = GC.GetTotalMemory(false) / 1024.0 / 1024.0
                }
            });
        }
    }
    
    /// <summary>
    /// Health check de espacio en disco
    /// </summary>
    public class DiskSpaceHealthCheck : IHealthCheck
    {
        public string Name => "DiskSpace";
        
        public Task<HealthCheckResult> CheckHealthAsync()
        {
            var drive = new System.IO.DriveInfo(AppDomain.CurrentDomain.BaseDirectory);
            var freeSpaceGB = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
            var totalSpaceGB = drive.TotalSize / 1024.0 / 1024.0 / 1024.0;
            var freePercentage = (freeSpaceGB / totalSpaceGB) * 100;
            
            HealthStatus status;
            string description;
            
            if (freePercentage < 5)
            {
                status = HealthStatus.Unhealthy;
                description = $"Critical disk space: {freeSpaceGB:F2} GB ({freePercentage:F1}%)";
            }
            else if (freePercentage < 10)
            {
                status = HealthStatus.Degraded;
                description = $"Low disk space: {freeSpaceGB:F2} GB ({freePercentage:F1}%)";
            }
            else
            {
                status = HealthStatus.Healthy;
                description = $"Disk space: {freeSpaceGB:F2} GB ({freePercentage:F1}%)";
            }
            
            return Task.FromResult(new HealthCheckResult
            {
                Name = Name,
                Status = status,
                Description = description,
                Data = new Dictionary<string, object>
                {
                    ["FreeSpaceGB"] = freeSpaceGB,
                    ["TotalSpaceGB"] = totalSpaceGB,
                    ["FreePercentage"] = freePercentage
                }
            });
        }
    }
    
    /// <summary>
    /// Health check del thread pool
    /// </summary>
    public class ThreadPoolHealthCheck : IHealthCheck
    {
        public string Name => "ThreadPool";
        
        public Task<HealthCheckResult> CheckHealthAsync()
        {
            System.Threading.ThreadPool.GetAvailableThreads(out int workerThreads, out int ioThreads);
            System.Threading.ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxIoThreads);
            
            var workerUsagePercent = ((maxWorkerThreads - workerThreads) / (double)maxWorkerThreads) * 100;
            
            HealthStatus status;
            string description;
            
            if (workerUsagePercent > 90)
            {
                status = HealthStatus.Unhealthy;
                description = $"Thread pool exhaustion: {workerUsagePercent:F1}% used";
            }
            else if (workerUsagePercent > 70)
            {
                status = HealthStatus.Degraded;
                description = $"High thread pool usage: {workerUsagePercent:F1}% used";
            }
            else
            {
                status = HealthStatus.Healthy;
                description = $"Thread pool usage: {workerUsagePercent:F1}% used";
            }
            
            return Task.FromResult(new HealthCheckResult
            {
                Name = Name,
                Status = status,
                Description = description,
                Data = new Dictionary<string, object>
                {
                    ["AvailableWorkerThreads"] = workerThreads,
                    ["MaxWorkerThreads"] = maxWorkerThreads,
                    ["UsagePercent"] = workerUsagePercent
                }
            });
        }
    }
    
    /// <summary>
    /// Estados de salud
    /// </summary>
    public enum HealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy
    }
    
    /// <summary>
    /// Resultado de un health check
    /// </summary>
    public class HealthCheckResult
    {
        public string Name { get; set; } = string.Empty;
        public HealthStatus Status { get; set; }
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object>? Data { get; set; }
        public Exception? Exception { get; set; }
    }
    
    /// <summary>
    /// Reporte de health checks
    /// </summary>
    public class HealthCheckReport
    {
        public HealthStatus Status { get; set; }
        public List<HealthCheckResult> Results { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public TimeSpan Duration { get; set; }
        
        public override string ToString()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine($"=== HEALTH CHECK REPORT ===");
            report.AppendLine($"Status: {Status}");
            report.AppendLine($"Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();
            
            foreach (var result in Results)
            {
                report.AppendLine($"[{result.Status}] {result.Name}: {result.Description}");
            }
            
            return report.ToString();
        }
    }
    
    /// <summary>
    /// Métricas del sistema
    /// </summary>
    public class SystemMetrics
    {
        public double WorkingSetMB { get; set; }
        public double PrivateMemoryMB { get; set; }
        public double CpuTimeSeconds { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public TimeSpan Uptime { get; set; }
        
        public override string ToString()
        {
            return $"Memory: {WorkingSetMB:F2} MB, Threads: {ThreadCount}, Uptime: {Uptime:hh\\:mm\\:ss}";
        }
    }
}
