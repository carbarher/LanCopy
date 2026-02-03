using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core
{
    public class NetworkStatisticsExtended
    {
        public TimeSpan Uptime { get; set; }
        public DateTime ConnectedSince { get; set; }
        public int Reconnections { get; set; }
        
        public int TotalSearches { get; set; }
        public int SuccessfulSearches { get; set; }
        public int AverageResultsPerSearch { get; set; }
        public TimeSpan AverageSearchTime { get; set; }
        
        public long TotalBytesDownloaded { get; set; }
        public long TotalBytesUploaded { get; set; }
        public int CompletedDownloads { get; set; }
        public int FailedDownloads { get; set; }
        public double AverageDownloadSpeed { get; set; }
        
        public Dictionary<string, NetworkSpecificStats> ByNetwork { get; set; }
        
        public List<DataPoint> DownloadSpeedHistory { get; set; }
        public List<DataPoint> SearchActivityHistory { get; set; }

        public NetworkStatisticsExtended()
        {
            ByNetwork = new Dictionary<string, NetworkSpecificStats>();
            DownloadSpeedHistory = new List<DataPoint>();
            SearchActivityHistory = new List<DataPoint>();
            ConnectedSince = DateTime.Now;
        }

        public double SuccessRate => TotalSearches > 0 ? SuccessfulSearches / (double)TotalSearches : 0;
        public double DownloadSuccessRate => (CompletedDownloads + FailedDownloads) > 0 
            ? CompletedDownloads / (double)(CompletedDownloads + FailedDownloads) 
            : 0;

        public void RecordSearch(string networkName, int resultCount, TimeSpan duration, bool success = true)
        {
            TotalSearches++;
            if (success) SuccessfulSearches++;
            
            if (!ByNetwork.ContainsKey(networkName))
            {
                ByNetwork[networkName] = new NetworkSpecificStats { NetworkName = networkName };
            }
            
            ByNetwork[networkName].RecordSearch(resultCount, duration);
            
            var totalResults = ByNetwork.Values.Sum(n => n.ResultCount);
            AverageResultsPerSearch = TotalSearches > 0 ? (int)(totalResults / TotalSearches) : 0;
            
            var totalDuration = ByNetwork.Values.Sum(n => n.TotalSearchTime.TotalMilliseconds);
            AverageSearchTime = TotalSearches > 0 
                ? TimeSpan.FromMilliseconds(totalDuration / TotalSearches) 
                : TimeSpan.Zero;
            
            SearchActivityHistory.Add(new DataPoint
            {
                Timestamp = DateTime.Now,
                Value = resultCount
            });
            
            if (SearchActivityHistory.Count > 1000)
            {
                SearchActivityHistory.RemoveAt(0);
            }
        }

        public void RecordDownload(string networkName, long bytes, TimeSpan duration, bool success = true)
        {
            if (success)
            {
                CompletedDownloads++;
                TotalBytesDownloaded += bytes;
            }
            else
            {
                FailedDownloads++;
            }
            
            if (!ByNetwork.ContainsKey(networkName))
            {
                ByNetwork[networkName] = new NetworkSpecificStats { NetworkName = networkName };
            }
            
            ByNetwork[networkName].RecordDownload(bytes, success);
            
            var speed = duration.TotalSeconds > 0 ? bytes / duration.TotalSeconds : 0;
            AverageDownloadSpeed = (AverageDownloadSpeed * (CompletedDownloads - 1) + speed) / CompletedDownloads;
            
            DownloadSpeedHistory.Add(new DataPoint
            {
                Timestamp = DateTime.Now,
                Value = speed
            });
            
            if (DownloadSpeedHistory.Count > 1000)
            {
                DownloadSpeedHistory.RemoveAt(0);
            }
        }

        public void RecordReconnection()
        {
            Reconnections++;
        }

        public void UpdateUptime()
        {
            Uptime = DateTime.Now - ConnectedSince;
        }

        public string GetSummary()
        {
            UpdateUptime();
            
            var summary = "=== ESTADÍSTICAS GENERALES ===\n";
            summary += $"Tiempo activo: {FormatTimeSpan(Uptime)}\n";
            summary += $"Reconexiones: {Reconnections}\n\n";
            
            summary += "=== BÚSQUEDAS ===\n";
            summary += $"Total: {TotalSearches} ({SuccessfulSearches} exitosas, {SuccessRate:P0})\n";
            summary += $"Promedio resultados/búsqueda: {AverageResultsPerSearch}\n";
            summary += $"Tiempo promedio: {FormatTimeSpan(AverageSearchTime)}\n\n";
            
            summary += "=== DESCARGAS ===\n";
            summary += $"Completadas: {CompletedDownloads}\n";
            summary += $"Fallidas: {FailedDownloads}\n";
            summary += $"Tasa de éxito: {DownloadSuccessRate:P0}\n";
            summary += $"Total descargado: {FormatBytes(TotalBytesDownloaded)}\n";
            summary += $"Velocidad promedio: {FormatBytes((long)AverageDownloadSpeed)}/s\n\n";
            
            if (ByNetwork.Any())
            {
                summary += "=== POR RED ===\n";
                foreach (var kvp in ByNetwork.OrderByDescending(x => x.Value.SearchCount))
                {
                    summary += $"\n{kvp.Key}:\n";
                    summary += kvp.Value.GetSummary();
                }
            }
            
            return summary;
        }

        public string GetCompactSummary()
        {
            UpdateUptime();
            return $"{FormatTimeSpan(Uptime)} | " +
                   $"{TotalSearches} búsquedas ({SuccessRate:P0}) | " +
                   $"📥 {CompletedDownloads} descargas | " +
                   $"💾 {FormatBytes(TotalBytesDownloaded)}";
        }

        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalDays >= 1)
                return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1)
                return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
            return $"{ts.Seconds}s";
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

        public class NetworkSpecificStats
        {
            public string NetworkName { get; set; }
            public int SearchCount { get; set; }
            public int ResultCount { get; set; }
            public int DownloadCount { get; set; }
            public long BytesDownloaded { get; set; }
            public TimeSpan TotalSearchTime { get; set; }
            public int SuccessfulSearches { get; set; }
            
            public double SuccessRate => SearchCount > 0 ? SuccessfulSearches / (double)SearchCount : 0;
            public double AverageResultsPerSearch => SearchCount > 0 ? ResultCount / (double)SearchCount : 0;

            public void RecordSearch(int resultCount, TimeSpan duration)
            {
                SearchCount++;
                ResultCount += resultCount;
                TotalSearchTime += duration;
                if (resultCount > 0) SuccessfulSearches++;
            }

            public void RecordDownload(long bytes, bool success)
            {
                if (success)
                {
                    DownloadCount++;
                    BytesDownloaded += bytes;
                }
            }

            public string GetSummary()
            {
                var avgTime = SearchCount > 0 
                    ? TimeSpan.FromMilliseconds(TotalSearchTime.TotalMilliseconds / SearchCount) 
                    : TimeSpan.Zero;
                
                return $"  Búsquedas: {SearchCount} ({SuccessRate:P0} éxito)\n" +
                       $"  Resultados: {ResultCount} (promedio: {AverageResultsPerSearch:F1})\n" +
                       $"  Descargas: {DownloadCount}\n" +
                       $"  Descargado: {FormatBytes(BytesDownloaded)}\n" +
                       $"  Tiempo promedio: {FormatTimeSpan(avgTime)}\n";
            }

            private string FormatTimeSpan(TimeSpan ts)
            {
                if (ts.TotalMinutes >= 1)
                    return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
                return $"{ts.TotalSeconds:F1}s";
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
        }

        public class DataPoint
        {
            public DateTime Timestamp { get; set; }
            public double Value { get; set; }
        }
    }

    public class StatisticsManagerExtended
    {
        private readonly NetworkStatisticsExtended _stats;
        private readonly string _statsFilePath;
        private readonly Action<string> _onLog;
        private System.Threading.Timer _autoSaveTimer;

        public StatisticsManagerExtended(string statsFilePath, Action<string> onLog = null)
        {
            _stats = new NetworkStatisticsExtended();
            _statsFilePath = statsFilePath;
            _onLog = onLog;
            
            _autoSaveTimer = new System.Threading.Timer(
                _ => SaveAsync().Wait(),
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5)
            );
        }

        public NetworkStatisticsExtended Stats => _stats;

        public async System.Threading.Tasks.Task LoadAsync()
        {
            if (!System.IO.File.Exists(_statsFilePath))
            {
                _onLog?.Invoke("[Stats] No hay estadísticas previas");
                return;
            }

            try
            {
                var json = await System.IO.File.ReadAllTextAsync(_statsFilePath);
                var loaded = System.Text.Json.JsonSerializer.Deserialize<NetworkStatisticsExtended>(json);
                
                if (loaded != null)
                {
                    _stats.TotalSearches = loaded.TotalSearches;
                    _stats.SuccessfulSearches = loaded.SuccessfulSearches;
                    _stats.TotalBytesDownloaded = loaded.TotalBytesDownloaded;
                    _stats.CompletedDownloads = loaded.CompletedDownloads;
                    _stats.FailedDownloads = loaded.FailedDownloads;
                    _stats.Reconnections = loaded.Reconnections;
                    _stats.ByNetwork = loaded.ByNetwork ?? new Dictionary<string, NetworkStatisticsExtended.NetworkSpecificStats>();
                    
                    _onLog?.Invoke($"[Stats] Estadísticas cargadas: {_stats.TotalSearches} búsquedas, {_stats.CompletedDownloads} descargas");
                }
            }
            catch (Exception ex)
            {
                _onLog?.Invoke($"[Stats] Error cargando estadísticas: {ex.Message}");
            }
        }

        public async System.Threading.Tasks.Task SaveAsync()
        {
            try
            {
                var directory = System.IO.Path.GetDirectoryName(_statsFilePath);
                if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                var options = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                };
                
                var json = System.Text.Json.JsonSerializer.Serialize(_stats, options);
                await System.IO.File.WriteAllTextAsync(_statsFilePath, json);
                
                _onLog?.Invoke($"[Stats] Estadísticas guardadas");
            }
            catch (Exception ex)
            {
                _onLog?.Invoke($"[Stats] Error guardando estadísticas: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _autoSaveTimer?.Dispose();
            SaveAsync().Wait();
        }
    }
}
