using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SlskDown.Core
{
    /// <summary>
    /// Dashboard de estadísticas multi-red con métricas detalladas
    /// Proporciona visualización y análisis de rendimiento por red
    /// </summary>
    public class MultiNetworkStatsDashboard
    {
        private readonly Dictionary<string, NetworkMetrics> _networkMetrics = new Dictionary<string, NetworkMetrics>();
        private readonly List<SearchHistoryEntry> _searchHistory = new List<SearchHistoryEntry>();
        private readonly List<DownloadHistoryEntry> _downloadHistory = new List<DownloadHistoryEntry>();
        private readonly object _lock = new object();
        private readonly int _maxHistoryEntries;

        public MultiNetworkStatsDashboard(int maxHistoryEntries = 1000)
        {
            _maxHistoryEntries = maxHistoryEntries;
        }

        /// <summary>
        /// Registra una búsqueda completada
        /// </summary>
        public void RecordSearch(string networkName, int resultCount, TimeSpan duration, bool fromCache)
        {
            lock (_lock)
            {
                var metrics = GetOrCreateMetrics(networkName);
                metrics.TotalSearches++;
                metrics.TotalResults += resultCount;
                metrics.TotalSearchDuration += duration;
                
                if (fromCache)
                    metrics.CachedSearches++;

                if (resultCount > 0)
                    metrics.SuccessfulSearches++;

                _searchHistory.Add(new SearchHistoryEntry
                {
                    NetworkName = networkName,
                    Timestamp = DateTime.UtcNow,
                    ResultCount = resultCount,
                    Duration = duration,
                    FromCache = fromCache
                });

                TrimHistory(_searchHistory);
            }
        }

        /// <summary>
        /// Registra una descarga completada
        /// </summary>
        public void RecordDownload(string networkName, long bytes, TimeSpan duration, bool success)
        {
            lock (_lock)
            {
                var metrics = GetOrCreateMetrics(networkName);
                metrics.TotalDownloads++;
                
                if (success)
                {
                    metrics.SuccessfulDownloads++;
                    metrics.TotalBytesDownloaded += bytes;
                    metrics.TotalDownloadDuration += duration;
                }
                else
                {
                    metrics.FailedDownloads++;
                }

                _downloadHistory.Add(new DownloadHistoryEntry
                {
                    NetworkName = networkName,
                    Timestamp = DateTime.UtcNow,
                    Bytes = bytes,
                    Duration = duration,
                    Success = success
                });

                TrimHistory(_downloadHistory);
            }
        }

        /// <summary>
        /// Obtiene métricas de una red específica
        /// </summary>
        public NetworkMetrics GetMetrics(string networkName)
        {
            lock (_lock)
            {
                return _networkMetrics.TryGetValue(networkName, out var metrics) 
                    ? metrics.Clone() 
                    : new NetworkMetrics { NetworkName = networkName };
            }
        }

        /// <summary>
        /// Obtiene métricas de todas las redes
        /// </summary>
        public Dictionary<string, NetworkMetrics> GetAllMetrics()
        {
            lock (_lock)
            {
                return _networkMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone());
            }
        }

        /// <summary>
        /// Obtiene estadísticas comparativas entre redes
        /// </summary>
        public ComparativeStats GetComparativeStats()
        {
            lock (_lock)
            {
                var stats = new ComparativeStats();

                foreach (var kvp in _networkMetrics)
                {
                    var network = kvp.Key;
                    var metrics = kvp.Value;

                    // Red con más resultados
                    if (metrics.TotalResults > (stats.MostResultsNetwork?.TotalResults ?? 0))
                    {
                        stats.MostResultsNetwork = metrics.Clone();
                    }

                    // Red más rápida (búsquedas)
                    var avgSearchTime = metrics.AverageSearchDuration;
                    if (avgSearchTime > TimeSpan.Zero && 
                        (stats.FastestSearchNetwork == null || avgSearchTime < stats.FastestSearchNetwork.AverageSearchDuration))
                    {
                        stats.FastestSearchNetwork = metrics.Clone();
                    }

                    // Red más rápida (descargas)
                    var avgSpeed = metrics.AverageDownloadSpeed;
                    if (avgSpeed > (stats.FastestDownloadNetwork?.AverageDownloadSpeed ?? 0))
                    {
                        stats.FastestDownloadNetwork = metrics.Clone();
                    }

                    // Red más confiable
                    var reliability = metrics.DownloadSuccessRate;
                    if (reliability > (stats.MostReliableNetwork?.DownloadSuccessRate ?? 0))
                    {
                        stats.MostReliableNetwork = metrics.Clone();
                    }
                }

                return stats;
            }
        }

        /// <summary>
        /// Obtiene historial de búsquedas recientes
        /// </summary>
        public List<SearchHistoryEntry> GetSearchHistory(int count = 50)
        {
            lock (_lock)
            {
                return _searchHistory.OrderByDescending(e => e.Timestamp).Take(count).ToList();
            }
        }

        /// <summary>
        /// Obtiene historial de descargas recientes
        /// </summary>
        public List<DownloadHistoryEntry> GetDownloadHistory(int count = 50)
        {
            lock (_lock)
            {
                return _downloadHistory.OrderByDescending(e => e.Timestamp).Take(count).ToList();
            }
        }

        /// <summary>
        /// Genera reporte de texto formateado
        /// </summary>
        public string GenerateTextReport()
        {
            lock (_lock)
            {
                var sb = new StringBuilder();
                sb.AppendLine("═══════════════════════════════════════════════════════");
                sb.AppendLine("       DASHBOARD DE ESTADÍSTICAS MULTI-RED");
                sb.AppendLine("═══════════════════════════════════════════════════════");
                sb.AppendLine();

                foreach (var kvp in _networkMetrics.OrderByDescending(k => k.Value.TotalResults))
                {
                    var network = kvp.Key;
                    var metrics = kvp.Value;

                    sb.AppendLine($"🌐 {network}");
                    sb.AppendLine($"   Búsquedas: {metrics.TotalSearches} ({metrics.SuccessfulSearches} exitosas, {metrics.CachedSearches} en caché)");
                    sb.AppendLine($"   Resultados: {metrics.TotalResults:N0} ({metrics.AverageResultsPerSearch:F1} promedio)");
                    sb.AppendLine($"   Tiempo búsqueda: {metrics.AverageSearchDuration.TotalSeconds:F2}s promedio");
                    sb.AppendLine($"   Descargas: {metrics.TotalDownloads} ({metrics.SuccessfulDownloads} exitosas, {metrics.FailedDownloads} fallidas)");
                    sb.AppendLine($"   Tasa éxito: {metrics.DownloadSuccessRate:F1}%");
                    sb.AppendLine($"   Datos descargados: {FormatBytes(metrics.TotalBytesDownloaded)}");
                    sb.AppendLine($"   Velocidad promedio: {FormatSpeed(metrics.AverageDownloadSpeed)}");
                    sb.AppendLine();
                }

                var comparative = GetComparativeStats();
                sb.AppendLine("📊 COMPARATIVA");
                sb.AppendLine($"   Más resultados: {comparative.MostResultsNetwork?.NetworkName ?? "N/A"}");
                sb.AppendLine($"   Búsqueda más rápida: {comparative.FastestSearchNetwork?.NetworkName ?? "N/A"}");
                sb.AppendLine($"   Descarga más rápida: {comparative.FastestDownloadNetwork?.NetworkName ?? "N/A"}");
                sb.AppendLine($"   Más confiable: {comparative.MostReliableNetwork?.NetworkName ?? "N/A"}");
                sb.AppendLine("═══════════════════════════════════════════════════════");

                return sb.ToString();
            }
        }

        /// <summary>
        /// Resetea todas las estadísticas
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _networkMetrics.Clear();
                _searchHistory.Clear();
                _downloadHistory.Clear();
            }
        }

        private NetworkMetrics GetOrCreateMetrics(string networkName)
        {
            if (!_networkMetrics.TryGetValue(networkName, out var metrics))
            {
                metrics = new NetworkMetrics { NetworkName = networkName };
                _networkMetrics[networkName] = metrics;
            }
            return metrics;
        }

        private void TrimHistory<T>(List<T> history)
        {
            if (history.Count > _maxHistoryEntries)
            {
                history.RemoveRange(0, history.Count - _maxHistoryEntries);
            }
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
            return $"{len:F2} {sizes[order]}";
        }

        private string FormatSpeed(double bytesPerSecond)
        {
            return $"{FormatBytes((long)bytesPerSecond)}/s";
        }
    }

    /// <summary>
    /// Métricas de una red específica
    /// </summary>
    public class NetworkMetrics
    {
        public string NetworkName { get; set; }
        
        // Métricas de búsqueda
        public int TotalSearches { get; set; }
        public int SuccessfulSearches { get; set; }
        public int CachedSearches { get; set; }
        public int TotalResults { get; set; }
        public TimeSpan TotalSearchDuration { get; set; }

        // Métricas de descarga
        public int TotalDownloads { get; set; }
        public int SuccessfulDownloads { get; set; }
        public int FailedDownloads { get; set; }
        public long TotalBytesDownloaded { get; set; }
        public TimeSpan TotalDownloadDuration { get; set; }

        // Métricas calculadas
        public double AverageResultsPerSearch => TotalSearches > 0 ? (double)TotalResults / TotalSearches : 0;
        public TimeSpan AverageSearchDuration => TotalSearches > 0 ? TimeSpan.FromTicks(TotalSearchDuration.Ticks / TotalSearches) : TimeSpan.Zero;
        public double DownloadSuccessRate => TotalDownloads > 0 ? (double)SuccessfulDownloads / TotalDownloads * 100 : 0;
        public double AverageDownloadSpeed => TotalDownloadDuration.TotalSeconds > 0 ? TotalBytesDownloaded / TotalDownloadDuration.TotalSeconds : 0;

        public NetworkMetrics Clone()
        {
            return (NetworkMetrics)MemberwiseClone();
        }
    }

    /// <summary>
    /// Estadísticas comparativas entre redes
    /// </summary>
    public class ComparativeStats
    {
        public NetworkMetrics MostResultsNetwork { get; set; }
        public NetworkMetrics FastestSearchNetwork { get; set; }
        public NetworkMetrics FastestDownloadNetwork { get; set; }
        public NetworkMetrics MostReliableNetwork { get; set; }
    }

    /// <summary>
    /// Entrada de historial de búsqueda
    /// </summary>
    public class SearchHistoryEntry
    {
        public string NetworkName { get; set; }
        public DateTime Timestamp { get; set; }
        public int ResultCount { get; set; }
        public TimeSpan Duration { get; set; }
        public bool FromCache { get; set; }
    }

    /// <summary>
    /// Entrada de historial de descarga
    /// </summary>
    public class DownloadHistoryEntry
    {
        public string NetworkName { get; set; }
        public DateTime Timestamp { get; set; }
        public long Bytes { get; set; }
        public TimeSpan Duration { get; set; }
        public bool Success { get; set; }
        public double Speed => Duration.TotalSeconds > 0 ? Bytes / Duration.TotalSeconds : 0;
    }
}
