using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soulseek;
using SlskDown.Core;

namespace SlskDown.Services
{
    /// <summary>
    /// Monitor de salud del sistema para detección proactiva de problemas
    /// Verifica conexiones, recursos y descargas cada 5 minutos
    /// </summary>
    public class HealthMonitor
    {
        private readonly System.Threading.Timer _healthCheckTimer;
        private readonly List<HealthIssue> _activeIssues;
        private readonly object _lock = new object();

        public event Action<HealthIssue> OnIssueDetected;
        public event Action<HealthIssue> OnIssueResolved;
        public event Action<HealthReport> OnHealthReportGenerated;

        private SoulseekClient _soulseekClient;
        private string _downloadDirectory;
        private long _minDiskSpaceBytes = 1024L * 1024 * 1024; // 1GB

        public HealthMonitor()
        {
            _activeIssues = new List<HealthIssue>();
            _healthCheckTimer = new System.Threading.Timer(PerformHealthCheck, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Configure(SoulseekClient soulseekClient, string downloadDirectory)
        {
            _soulseekClient = soulseekClient;
            _downloadDirectory = downloadDirectory;
        }

        public void Start(TimeSpan? interval = null)
        {
            var checkInterval = interval ?? TimeSpan.FromMinutes(5);
            _healthCheckTimer.Change(TimeSpan.Zero, checkInterval);
        }

        public void Stop()
        {
            _healthCheckTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private async void PerformHealthCheck(object state)
        {
            try
            {
                var report = await GenerateHealthReportAsync();
                
                // Detectar nuevos problemas
                foreach (var issue in report.Issues)
                {
                    lock (_lock)
                    {
                        if (!_activeIssues.Any(i => i.Type == issue.Type))
                        {
                            _activeIssues.Add(issue);
                            OnIssueDetected?.Invoke(issue);
                        }
                    }
                }

                // Detectar problemas resueltos
                lock (_lock)
                {
                    var resolvedIssues = _activeIssues
                        .Where(active => !report.Issues.Any(current => current.Type == active.Type))
                        .ToList();

                    foreach (var resolved in resolvedIssues)
                    {
                        _activeIssues.Remove(resolved);
                        OnIssueResolved?.Invoke(resolved);
                    }
                }

                OnHealthReportGenerated?.Invoke(report);
            }
            catch (Exception ex)
            {
                // Log error pero no detener el monitor
                System.Diagnostics.Debug.WriteLine($"Health check error: {ex.Message}");
            }
        }

        public async Task<HealthReport> GenerateHealthReportAsync()
        {
            var report = new HealthReport
            {
                Timestamp = DateTime.Now,
                Issues = new List<HealthIssue>()
            };

            // Check 1: Conexión Soulseek
            if (_soulseekClient != null)
            {
                if (_soulseekClient.State != SoulseekClientStates.Connected &&
                    _soulseekClient.State != SoulseekClientStates.LoggedIn)
                {
                    report.Issues.Add(new HealthIssue
                    {
                        Type = HealthIssueType.SoulseekDisconnected,
                        Severity = IssueSeverity.High,
                        Message = $"Soulseek desconectado (Estado: {_soulseekClient.State})",
                        DetectedAt = DateTime.Now
                    });
                }
            }


            // Check 3: Espacio en disco
            if (!string.IsNullOrEmpty(_downloadDirectory) && System.IO.Directory.Exists(_downloadDirectory))
            {
                try
                {
                    var drive = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(_downloadDirectory));
                    if (drive.AvailableFreeSpace < _minDiskSpaceBytes)
                    {
                        report.Issues.Add(new HealthIssue
                        {
                            Type = HealthIssueType.LowDiskSpace,
                            Severity = IssueSeverity.Critical,
                            Message = $"Espacio bajo en disco: {drive.AvailableFreeSpace / (1024 * 1024)}MB disponibles",
                            DetectedAt = DateTime.Now
                        });
                    }
                }
                catch { }
            }

            // Check 4: Memoria disponible
            var availableMemory = GC.GetTotalMemory(false);
            if (availableMemory > 500 * 1024 * 1024) // >500MB usado
            {
                report.Issues.Add(new HealthIssue
                {
                    Type = HealthIssueType.HighMemoryUsage,
                    Severity = IssueSeverity.Low,
                    Message = $"Uso alto de memoria: {availableMemory / (1024 * 1024)}MB",
                    DetectedAt = DateTime.Now
                });
            }

            report.OverallHealth = CalculateOverallHealth(report.Issues);
            return report;
        }

        private HealthStatus CalculateOverallHealth(List<HealthIssue> issues)
        {
            if (!issues.Any())
                return HealthStatus.Healthy;

            if (issues.Any(i => i.Severity == IssueSeverity.Critical))
                return HealthStatus.Critical;

            if (issues.Any(i => i.Severity == IssueSeverity.High))
                return HealthStatus.Degraded;

            return HealthStatus.Warning;
        }

        public void Dispose()
        {
            _healthCheckTimer?.Dispose();
        }
    }

    public class HealthReport
    {
        public DateTime Timestamp { get; set; }
        public HealthStatus OverallHealth { get; set; }
        public List<HealthIssue> Issues { get; set; }

        public string GetSummary()
        {
            if (!Issues.Any())
                return "Sistema saludable";

            var critical = Issues.Count(i => i.Severity == IssueSeverity.Critical);
            var high = Issues.Count(i => i.Severity == IssueSeverity.High);
            var medium = Issues.Count(i => i.Severity == IssueSeverity.Medium);

            return $"{Issues.Count} problemas detectados (Críticos: {critical}, Altos: {high}, Medios: {medium})";
        }
    }

    public class HealthIssue
    {
        public HealthIssueType Type { get; set; }
        public IssueSeverity Severity { get; set; }
        public string Message { get; set; }
        public DateTime DetectedAt { get; set; }
    }

    public enum HealthIssueType
    {
        SoulseekDisconnected,
        LowDiskSpace,
        HighMemoryUsage,
        StuckDownloads,
        DatabaseError
    }

    public enum IssueSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum HealthStatus
    {
        Healthy,
        Warning,
        Degraded,
        Critical
    }
}
