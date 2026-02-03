using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using SlskDown.Models;
using DownloadHistoryRecord = SlskDown.Models.DownloadHistoryRecord;

namespace SlskDown.Core
{
    /// <summary>
    /// Gestiona estadísticas, métricas y historial de descargas
    /// </summary>
    public class StatisticsManager
    {
        // Configuración
        private readonly StatisticsManagerConfig config;
        
        // Estadísticas
        private AppStatistics statistics = new AppStatistics();
        private readonly object statsLock = new object();
        
        // Historial de descargas
        private readonly List<DownloadHistoryRecord> downloadHistory = new List<DownloadHistoryRecord>();
        private readonly object historyLock = new object();
        private readonly HashSet<string> downloadHistoryCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Estadísticas de proveedores
        private readonly Dictionary<string, ProviderStats> providerStats = new Dictionary<string, ProviderStats>();
        private readonly object providerStatsLock = new object();
        
        // Callbacks
        public Action<string> OnLog { get; set; }
        
        public StatisticsManager(StatisticsManagerConfig configuration)
        {
            config = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }
        
        #region Estadísticas Generales
        
        /// <summary>
        /// Registra una búsqueda
        /// </summary>
        public void RecordSearch(bool successful, int resultsCount = 0)
        {
            lock (statsLock)
            {
                statistics.TotalSearches++;
                if (successful)
                {
                    statistics.SuccessfulSearches++;
                    statistics.TotalResultsFound += resultsCount;
                }
            }
        }
        
        /// <summary>
        /// Registra una descarga
        /// </summary>
        public void RecordDownload(bool successful, long sizeBytes = 0, TimeSpan? duration = null)
        {
            lock (statsLock)
            {
                statistics.TotalDownloads++;
                if (successful)
                {
                    statistics.SuccessfulDownloads++;
                    statistics.TotalBytesDownloaded += sizeBytes;
                    
                    if (duration.HasValue && duration.Value.TotalSeconds > 0)
                    {
                        double speedBps = sizeBytes / duration.Value.TotalSeconds;
                        statistics.AverageDownloadSpeed = 
                            (statistics.AverageDownloadSpeed * (statistics.SuccessfulDownloads - 1) + speedBps) / 
                            statistics.SuccessfulDownloads;
                    }
                }
            }
        }
        
        /// <summary>
        /// Obtiene snapshot de estadísticas
        /// </summary>
        public AppStatistics GetStatistics()
        {
            lock (statsLock)
            {
                return new AppStatistics
                {
                    TotalSearches = statistics.TotalSearches,
                    SuccessfulSearches = statistics.SuccessfulSearches,
                    TotalDownloads = statistics.TotalDownloads,
                    SuccessfulDownloads = statistics.SuccessfulDownloads,
                    TotalBytesDownloaded = statistics.TotalBytesDownloaded,
                    TotalResultsFound = statistics.TotalResultsFound,
                    AverageDownloadSpeed = statistics.AverageDownloadSpeed,
                    SessionStartTime = statistics.SessionStartTime
                };
            }
        }
        
        /// <summary>
        /// Reinicia estadísticas
        /// </summary>
        public void ResetStatistics()
        {
            lock (statsLock)
            {
                statistics = new AppStatistics
                {
                    SessionStartTime = DateTime.Now
                };
            }
            
            Log("Estadísticas reiniciadas");
        }
        
        #endregion
        
        #region Historial de Descargas
        
        /// <summary>
        /// Agrega una descarga al historial
        /// </summary>
        public void AddToHistory(DownloadHistoryRecord download)
        {
            if (download == null) return;
            
            string key = $"{download.FileName}_{download.SizeBytes}";
            
            lock (historyLock)
            {
                if (!downloadHistoryCache.Contains(key))
                {
                    downloadHistory.Add(download);
                    downloadHistoryCache.Add(key);
                    
                    // Limitar tamaño del historial
                    if (downloadHistory.Count > config.MaxHistoryItems)
                    {
                        var toRemove = downloadHistory
                            .OrderBy(h => h.CompletedAt)
                            .Take(downloadHistory.Count - config.MaxHistoryItems)
                            .ToList();
                        
                        foreach (var item in toRemove)
                        {
                            downloadHistory.Remove(item);
                            downloadHistoryCache.Remove($"{item.FileName}_{item.SizeBytes}");
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Verifica si un archivo ya fue descargado
        /// </summary>
        public bool IsInHistory(string fileName, long sizeBytes)
        {
            string key = $"{fileName}_{sizeBytes}";
            
            lock (historyLock)
            {
                return downloadHistoryCache.Contains(key);
            }
        }
        
        /// <summary>
        /// Obtiene el historial completo
        /// </summary>
        public List<DownloadHistoryRecord> GetHistory()
        {
            lock (historyLock)
            {
                return new List<DownloadHistoryRecord>(downloadHistory);
            }
        }
        
        /// <summary>
        /// Obtiene historial filtrado por fecha
        /// </summary>
        public List<DownloadHistoryRecord> GetHistoryByDate(DateTime startDate, DateTime endDate)
        {
            lock (historyLock)
            {
                return downloadHistory
                    .Where(h => h.CompletedAt >= startDate && h.CompletedAt <= endDate)
                    .OrderByDescending(h => h.CompletedAt)
                    .ToList();
            }
        }
        
        /// <summary>
        /// Limpia el historial
        /// </summary>
        public void ClearHistory()
        {
            lock (historyLock)
            {
                int count = downloadHistory.Count;
                downloadHistory.Clear();
                downloadHistoryCache.Clear();
                Log($"Historial limpiado: {count} elementos eliminados");
            }
        }
        
        #endregion
        
        #region Estadísticas de Proveedores
        
        /// <summary>
        /// Registra una descarga exitosa de un proveedor
        /// </summary>
        public void RecordProviderSuccess(string username, long sizeBytes, TimeSpan duration)
        {
            if (string.IsNullOrEmpty(username)) return;
            
            lock (providerStatsLock)
            {
                if (!providerStats.ContainsKey(username))
                {
                    providerStats[username] = new ProviderStats { Username = username };
                }
                
                var stats = providerStats[username];
                stats.TotalDownloads++;
                stats.SuccessfulDownloads++;
                stats.TotalBytesDownloaded += sizeBytes;
                stats.LastDownloadDate = DateTime.Now;
                
                if (duration.TotalSeconds > 0)
                {
                    double speedBps = sizeBytes / duration.TotalSeconds;
                    stats.AverageSpeed = 
                        (stats.AverageSpeed * (stats.SuccessfulDownloads - 1) + speedBps) / 
                        stats.SuccessfulDownloads;
                }
            }
        }
        
        /// <summary>
        /// Registra una descarga fallida de un proveedor
        /// </summary>
        public void RecordProviderFailure(string username)
        {
            if (string.IsNullOrEmpty(username)) return;
            
            lock (providerStatsLock)
            {
                if (!providerStats.ContainsKey(username))
                {
                    providerStats[username] = new ProviderStats { Username = username };
                }
                
                var stats = providerStats[username];
                stats.TotalDownloads++;
                stats.FailedDownloads++;
            }
        }
        
        /// <summary>
        /// Obtiene estadísticas de un proveedor
        /// </summary>
        public ProviderStats GetProviderStats(string username)
        {
            if (string.IsNullOrEmpty(username)) return null;
            
            lock (providerStatsLock)
            {
                return providerStats.TryGetValue(username, out var stats) ? stats : null;
            }
        }
        
        /// <summary>
        /// Obtiene los mejores proveedores
        /// </summary>
        public List<ProviderStats> GetTopProviders(int count = 10)
        {
            lock (providerStatsLock)
            {
                return providerStats.Values
                    .Where(p => p.SuccessfulDownloads > 0)
                    .OrderByDescending(p => p.SuccessRate)
                    .ThenByDescending(p => p.AverageSpeed)
                    .Take(count)
                    .ToList();
            }
        }
        
        /// <summary>
        /// Limpia estadísticas de proveedores
        /// </summary>
        public void ClearProviderStats()
        {
            lock (providerStatsLock)
            {
                int count = providerStats.Count;
                providerStats.Clear();
                Log($"Estadísticas de proveedores limpiadas: {count} proveedores");
            }
        }
        
        #endregion
        
        #region Persistencia
        
        /// <summary>
        /// Guarda el historial en disco
        /// </summary>
        public async System.Threading.Tasks.Task SaveHistoryAsync()
        {
            try
            {
                List<DownloadHistoryRecord> snapshot;
                lock (historyLock)
                {
                    snapshot = new List<DownloadHistoryRecord>(downloadHistory);
                }
                
                string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                await File.WriteAllTextAsync(config.HistoryFilePath, json);
                Log($"Historial guardado: {snapshot.Count} elementos");
            }
            catch (Exception ex)
            {
                Log($"Error guardando historial: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Carga el historial desde disco
        /// </summary>
        public async System.Threading.Tasks.Task LoadHistoryAsync()
        {
            try
            {
                if (!File.Exists(config.HistoryFilePath))
                {
                    Log("No hay historial previo");
                    return;
                }
                
                string json = await File.ReadAllTextAsync(config.HistoryFilePath);
                var loaded = JsonSerializer.Deserialize<List<DownloadHistoryRecord>>(json);
                
                if (loaded != null)
                {
                    lock (historyLock)
                    {
                        downloadHistory.Clear();
                        downloadHistoryCache.Clear();
                        
                        foreach (var item in loaded)
                        {
                            downloadHistory.Add(item);
                            downloadHistoryCache.Add($"{item.FileName}_{item.SizeBytes}");
                        }
                    }
                    
                    Log($"Historial cargado: {loaded.Count} elementos");
                }
            }
            catch (Exception ex)
            {
                Log($"Error cargando historial: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Guarda estadísticas de proveedores
        /// </summary>
        public async System.Threading.Tasks.Task SaveProviderStatsAsync()
        {
            try
            {
                Dictionary<string, ProviderStats> snapshot;
                lock (providerStatsLock)
                {
                    snapshot = new Dictionary<string, ProviderStats>(providerStats);
                }
                
                string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                await File.WriteAllTextAsync(config.ProviderStatsFilePath, json);
                Log($"Estadísticas de proveedores guardadas: {snapshot.Count} proveedores");
            }
            catch (Exception ex)
            {
                Log($"Error guardando estadísticas: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Carga estadísticas de proveedores
        /// </summary>
        public async System.Threading.Tasks.Task LoadProviderStatsAsync()
        {
            try
            {
                if (!File.Exists(config.ProviderStatsFilePath))
                {
                    Log("No hay estadísticas previas");
                    return;
                }
                
                string json = await File.ReadAllTextAsync(config.ProviderStatsFilePath);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, ProviderStats>>(json);
                
                if (loaded != null)
                {
                    lock (providerStatsLock)
                    {
                        providerStats.Clear();
                        foreach (var kvp in loaded)
                        {
                            providerStats[kvp.Key] = kvp.Value;
                        }
                    }
                    
                    Log($"Estadísticas cargadas: {loaded.Count} proveedores");
                }
            }
            catch (Exception ex)
            {
                Log($"Error cargando estadísticas: {ex.Message}");
            }
        }
        
        #endregion
        
        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
    }
    
    /// <summary>
    /// Configuración del StatisticsManager
    /// </summary>
    public class StatisticsManagerConfig
    {
        public string HistoryFilePath { get; set; }
        public string ProviderStatsFilePath { get; set; }
        public int MaxHistoryItems { get; set; } = 10000;
    }
}
