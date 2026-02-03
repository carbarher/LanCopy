using System;

namespace SlskDown
{
    // Métricas de conexión
    public class ConnectionMetrics
    {
        public int TotalConnections { get; set; }
        public int FailedConnections { get; set; }
        public int TimeoutConnections { get; set; }
        public int SuccessfulReconnections { get; set; }
        public TimeSpan AverageConnectionTime { get; set; }
        public DateTime LastConnectionLost { get; set; }
        public int ConsecutiveFailures { get; set; }
        
        public double SuccessRate => TotalConnections > 0 
            ? (1 - (double)FailedConnections / TotalConnections) * 100 
            : 0;
    }

    // Métricas de descarga
    public class DownloadMetrics
    {
        public int TotalDownloads { get; set; }
        public int CompletedDownloads { get; set; }
        public int FailedDownloads { get; set; }
        public int AutoRetriedDownloads { get; set; }
        public long TotalBytesDownloaded { get; set; }
        public double AverageSpeed { get; set; }
        public TimeSpan TotalDownloadTime { get; set; }
        
        public double CompletionRate => TotalDownloads > 0 
            ? (double)CompletedDownloads / TotalDownloads * 100 
            : 0;
    }

    // Prioridad de descargas
    public enum DownloadPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }
}
