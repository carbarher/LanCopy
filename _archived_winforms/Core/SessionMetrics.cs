using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;

namespace SlskDown.Core
{
    /// <summary>
    /// Gestiona métricas y estadísticas de la sesión actual
    /// </summary>
    public class SessionMetrics
    {
        // Métricas de descargas
        public int TotalDownloads { get; private set; }
        public int CompletedDownloads { get; private set; }
        public int FailedDownloads { get; private set; }
        public long TotalBytesDownloaded { get; private set; }
        public double AverageDownloadSpeed { get; private set; } // KB/s
        
        // Métricas de búsquedas
        public int TotalSearches { get; private set; }
        public int SuccessfulSearches { get; private set; }
        public int FailedSearches { get; private set; }
        public double AverageSearchTime { get; private set; } // segundos
        
        // Métricas de purga
        public int AuthorsProcessed { get; private set; }
        public int AuthorsWithFiles { get; private set; }
        public int AuthorsWithoutFiles { get; private set; }
        public double AuthorsPerHour { get; private set; }
        
        // Métricas de conexión
        public int ConnectionAttempts { get; private set; }
        public int SuccessfulConnections { get; private set; }
        public int Disconnections { get; private set; }
        public TimeSpan TotalConnectedTime { get; private set; }
        public double AverageLatency { get; private set; } // ms
        
        // Tiempos
        public DateTime SessionStart { get; private set; }
        public DateTime? SessionEnd { get; private set; }
        public TimeSpan SessionDuration => SessionEnd.HasValue 
            ? SessionEnd.Value - SessionStart 
            : DateTime.UtcNow - SessionStart;
        
        // Historial para cálculos
        private readonly List<double> _downloadSpeeds = new List<double>();
        private readonly List<double> _searchTimes = new List<double>();
        private readonly List<double> _latencies = new List<double>();
        private DateTime? _lastConnectionTime;
        private DateTime? _lastDisconnectionTime;
        
        public SessionMetrics()
        {
            SessionStart = DateTime.UtcNow;
        }
        
        // === MÉTODOS DE REGISTRO ===
        
        public void RecordDownloadStarted()
        {
            TotalDownloads++;
        }
        
        public void RecordDownloadCompleted(long bytes, double speedKBps)
        {
            CompletedDownloads++;
            TotalBytesDownloaded += bytes;
            
            if (speedKBps > 0)
            {
                _downloadSpeeds.Add(speedKBps);
                AverageDownloadSpeed = _downloadSpeeds.Average();
            }
        }
        
        public void RecordDownloadFailed()
        {
            FailedDownloads++;
        }
        
        public void RecordSearchStarted()
        {
            TotalSearches++;
        }
        
        public void RecordSearchCompleted(double durationSeconds, bool success)
        {
            if (success)
            {
                SuccessfulSearches++;
                _searchTimes.Add(durationSeconds);
                AverageSearchTime = _searchTimes.Average();
            }
            else
            {
                FailedSearches++;
            }
        }
        
        public void RecordAuthorProcessed(bool hasFiles)
        {
            AuthorsProcessed++;
            if (hasFiles)
                AuthorsWithFiles++;
            else
                AuthorsWithoutFiles++;
            
            // Calcular autores por hora
            var hours = SessionDuration.TotalHours;
            if (hours > 0)
                AuthorsPerHour = AuthorsProcessed / hours;
        }
        
        public void RecordConnectionAttempt()
        {
            ConnectionAttempts++;
        }
        
        public void RecordConnectionSuccess(double latencyMs)
        {
            SuccessfulConnections++;
            _lastConnectionTime = DateTime.UtcNow;
            
            if (latencyMs > 0)
            {
                _latencies.Add(latencyMs);
                AverageLatency = _latencies.Average();
            }
        }
        
        public void RecordDisconnection()
        {
            Disconnections++;
            _lastDisconnectionTime = DateTime.UtcNow;
            
            // Acumular tiempo conectado
            if (_lastConnectionTime.HasValue && _lastDisconnectionTime.HasValue)
            {
                var connectedDuration = _lastDisconnectionTime.Value - _lastConnectionTime.Value;
                if (connectedDuration.TotalSeconds > 0)
                    TotalConnectedTime += connectedDuration;
            }
        }
        
        public void RecordConnectionHealthCheck(bool success)
        {
            // Registrar health check para estadísticas
            if (success)
            {
                // Health check exitoso - conexión saludable
                _lastConnectionTime = DateTime.UtcNow;
            }
            else
            {
                // Health check fallido - posible problema de conexión
                Disconnections++;
            }
        }
        
        public void EndSession()
        {
            SessionEnd = DateTime.UtcNow;
        }
        
        // === CÁLCULOS Y ESTADÍSTICAS ===
        
        public double GetSuccessRate()
        {
            if (TotalDownloads == 0) return 0;
            return (double)CompletedDownloads / TotalDownloads * 100;
        }
        
        public double GetSearchSuccessRate()
        {
            if (TotalSearches == 0) return 0;
            return (double)SuccessfulSearches / TotalSearches * 100;
        }
        
        public double GetConnectionUptime()
        {
            if (SessionDuration.TotalSeconds == 0) return 0;
            return TotalConnectedTime.TotalSeconds / SessionDuration.TotalSeconds * 100;
        }
        
        public TimeSpan GetEstimatedTimeRemaining(int remainingItems, double itemsPerHour)
        {
            if (itemsPerHour <= 0) return TimeSpan.Zero;
            var hoursRemaining = remainingItems / itemsPerHour;
            return TimeSpan.FromHours(hoursRemaining);
        }
        
        public string GetFormattedSpeed()
        {
            if (AverageDownloadSpeed < 1024)
                return $"{AverageDownloadSpeed:F1} KB/s";
            return $"{AverageDownloadSpeed / 1024:F2} MB/s";
        }
        
        public string GetFormattedSize()
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;
            
            if (TotalBytesDownloaded < KB)
                return $"{TotalBytesDownloaded} B";
            if (TotalBytesDownloaded < MB)
                return $"{TotalBytesDownloaded / (double)KB:F2} KB";
            if (TotalBytesDownloaded < GB)
                return $"{TotalBytesDownloaded / (double)MB:F2} MB";
            return $"{TotalBytesDownloaded / (double)GB:F2} GB";
        }
        
        // === EXPORTAR ===
        
        public string GetSummary()
        {
            return $@"
═══════════════════════════════════════════════════════
                    RESUMEN DE SESIÓN
═══════════════════════════════════════════════════════

DURACIÓN
   Inicio:           {SessionStart:yyyy-MM-dd HH:mm:ss}
   Duración:         {SessionDuration.Hours}h {SessionDuration.Minutes}m {SessionDuration.Seconds}s
   Tiempo conectado: {TotalConnectedTime.Hours}h {TotalConnectedTime.Minutes}m ({GetConnectionUptime():F1}%)

DESCARGAS
   Total:            {TotalDownloads}
   Completadas:      {CompletedDownloads} ({GetSuccessRate():F1}%)
   Fallidas:         {FailedDownloads}
   Datos:            {GetFormattedSize()}
   Velocidad media:  {GetFormattedSpeed()}

BÚSQUEDAS
   Total:            {TotalSearches}
   Exitosas:         {SuccessfulSearches} ({GetSearchSuccessRate():F1}%)
   Fallidas:         {FailedSearches}
   Tiempo medio:     {AverageSearchTime:F2}s

AUTORES (PURGA)
   Procesados:       {AuthorsProcessed}
   Con archivos:     {AuthorsWithFiles}
   Sin archivos:     {AuthorsWithoutFiles}
   Velocidad:        {AuthorsPerHour:F1} autores/hora

CONEXIÓN
   Intentos:         {ConnectionAttempts}
   Exitosos:         {SuccessfulConnections}
   Desconexiones:    {Disconnections}
   Latencia media:   {AverageLatency:F0}ms

═══════════════════════════════════════════════════════
";
        }
        
        public void ExportToJson(string filePath)
        {
            var data = new
            {
                SessionStart,
                SessionEnd,
                SessionDuration = SessionDuration.ToString(),
                Downloads = new
                {
                    TotalDownloads,
                    CompletedDownloads,
                    FailedDownloads,
                    TotalBytesDownloaded,
                    AverageDownloadSpeed,
                    SuccessRate = GetSuccessRate()
                },
                Searches = new
                {
                    TotalSearches,
                    SuccessfulSearches,
                    FailedSearches,
                    AverageSearchTime,
                    SuccessRate = GetSearchSuccessRate()
                },
                Purge = new
                {
                    AuthorsProcessed,
                    AuthorsWithFiles,
                    AuthorsWithoutFiles,
                    AuthorsPerHour
                },
                Connection = new
                {
                    ConnectionAttempts,
                    SuccessfulConnections,
                    Disconnections,
                    TotalConnectedTime = TotalConnectedTime.ToString(),
                    AverageLatency,
                    Uptime = GetConnectionUptime()
                }
            };
            
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }
        
        public void ExportToCsv(string filePath)
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Métrica,Valor");
            csv.AppendLine($"Inicio de sesión,{SessionStart:yyyy-MM-dd HH:mm:ss}");
            csv.AppendLine($"Duración (horas),{SessionDuration.TotalHours:F2}");
            csv.AppendLine($"Descargas totales,{TotalDownloads}");
            csv.AppendLine($"Descargas completadas,{CompletedDownloads}");
            csv.AppendLine($"Descargas fallidas,{FailedDownloads}");
            csv.AppendLine($"Tasa de éxito (%),{GetSuccessRate():F2}");
            csv.AppendLine($"Bytes descargados,{TotalBytesDownloaded}");
            csv.AppendLine($"Velocidad media (KB/s),{AverageDownloadSpeed:F2}");
            csv.AppendLine($"Búsquedas totales,{TotalSearches}");
            csv.AppendLine($"Búsquedas exitosas,{SuccessfulSearches}");
            csv.AppendLine($"Tiempo medio de búsqueda (s),{AverageSearchTime:F2}");
            csv.AppendLine($"Autores procesados,{AuthorsProcessed}");
            csv.AppendLine($"Autores con archivos,{AuthorsWithFiles}");
            csv.AppendLine($"Autores por hora,{AuthorsPerHour:F2}");
            csv.AppendLine($"Intentos de conexión,{ConnectionAttempts}");
            csv.AppendLine($"Conexiones exitosas,{SuccessfulConnections}");
            csv.AppendLine($"Desconexiones,{Disconnections}");
            csv.AppendLine($"Latencia media (ms),{AverageLatency:F0}");
            csv.AppendLine($"Uptime (%),{GetConnectionUptime():F2}");
            
            File.WriteAllText(filePath, csv.ToString());
        }
    }
}
