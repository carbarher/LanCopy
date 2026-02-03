using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SlskDown.Services
{
    /// <summary>
    /// ImplementaciÃ³n del servicio de estadÃ­sticas
    /// </summary>
    public class StatsService : IStatsService
    {
        private readonly string _statsPath;
        private readonly ILoggingService? _logger;
        private AppStats _stats;
        private DateTime _lastSaveDate;

        public StatsService(ILoggingService? logger = null)
        {
            _logger = logger;
            _statsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_stats.json");
            _stats = new AppStats
            {
                FirstUseDate = DateTime.Now,
                LastUseDate = DateTime.Now
            };
            _lastSaveDate = DateTime.Now.Date;
            Load();
        }

        public void RecordSearch(string query, int resultsCount)
        {
            _stats.TotalSearches++;
            _stats.LastUseDate = DateTime.Now;
            
            // Actualizar estadÃ­sticas del dÃ­a
            if (DateTime.Now.Date != _lastSaveDate)
            {
                _stats.SearchesToday = 0;
                _lastSaveDate = DateTime.Now.Date;
            }
            _stats.SearchesToday++;
            
            // Agregar a bÃºsquedas recientes (mÃ¡ximo 20)
            if (!string.IsNullOrWhiteSpace(query))
            {
                _stats.RecentSearches.Remove(query); // Remover si ya existe
                _stats.RecentSearches.Insert(0, query);
                if (_stats.RecentSearches.Count > 20)
                    _stats.RecentSearches = _stats.RecentSearches.Take(20).ToList();
            }
            
            _logger?.Debug($"BÃºsqueda registrada: '{query}' ({resultsCount} resultados)");
            
            // Auto-guardar cada 10 bÃºsquedas
            if (_stats.TotalSearches % 10 == 0)
                Save();
        }

        public void RecordDownload(string filename, long size, double speedKBps, string username)
        {
            _stats.TotalDownloads++;
            _stats.TotalBytesDownloaded += size;
            _stats.LastUseDate = DateTime.Now;
            
            // Actualizar estadÃ­sticas del dÃ­a
            if (DateTime.Now.Date != _lastSaveDate)
            {
                _stats.DownloadsToday = 0;
                _stats.BytesDownloadedToday = 0;
                _lastSaveDate = DateTime.Now.Date;
            }
            _stats.DownloadsToday++;
            _stats.BytesDownloadedToday += size;
            
            // Calcular velocidad promedio
            if (_stats.TotalDownloads == 1)
            {
                _stats.AverageSpeedKBps = speedKBps;
            }
            else
            {
                // Promedio mÃ³vil
                _stats.AverageSpeedKBps = (_stats.AverageSpeedKBps * (_stats.TotalDownloads - 1) + speedKBps) / _stats.TotalDownloads;
            }
            
            // Top usuarios
            if (!string.IsNullOrWhiteSpace(username))
            {
                if (_stats.TopUsers.ContainsKey(username))
                    _stats.TopUsers[username]++;
                else
                    _stats.TopUsers[username] = 1;
            }
            
            // Top extensiones
            string ext = Path.GetExtension(filename)?.ToLower() ?? "";
            if (!string.IsNullOrWhiteSpace(ext))
            {
                if (_stats.TopExtensions.ContainsKey(ext))
                    _stats.TopExtensions[ext]++;
                else
                    _stats.TopExtensions[ext] = 1;
            }
            
            _logger?.Debug($"Descarga registrada: {Path.GetFileName(filename)} ({FormatSize(size)}, {speedKBps:F1} KB/s)");
            
            // Auto-guardar cada 5 descargas
            if (_stats.TotalDownloads % 5 == 0)
                Save();
        }

        public AppStats GetStats()
        {
            // Actualizar fecha de Ãºltimo uso
            _stats.LastUseDate = DateTime.Now;
            
            // Verificar si cambiÃ³ el dÃ­a
            if (DateTime.Now.Date != _lastSaveDate)
            {
                _stats.SearchesToday = 0;
                _stats.DownloadsToday = 0;
                _stats.BytesDownloadedToday = 0;
                _lastSaveDate = DateTime.Now.Date;
            }
            
            return _stats;
        }

        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                
                var json = JsonSerializer.Serialize(_stats, options);
                File.WriteAllText(_statsPath, json);
                
                _logger?.Debug($"EstadÃ­sticas guardadas: {_stats.TotalSearches} bÃºsquedas, {_stats.TotalDownloads} descargas");
            }
            catch (Exception ex)
            {
                _logger?.Error("Error guardando estadÃ­sticas", ex);
            }
        }

        public void Load()
        {
            try
            {
                if (File.Exists(_statsPath))
                {
                    var json = File.ReadAllText(_statsPath);
                    var loaded = JsonSerializer.Deserialize<AppStats>(json);
                    
                    if (loaded != null)
                    {
                        _stats = loaded;
                        _stats.LastUseDate = DateTime.Now;
                        
                        // Verificar si cambiÃ³ el dÃ­a
                        if (DateTime.Now.Date != _lastSaveDate)
                        {
                            _stats.SearchesToday = 0;
                            _stats.DownloadsToday = 0;
                            _stats.BytesDownloadedToday = 0;
                        }
                        
                        _logger?.Info($"EstadÃ­sticas cargadas: {_stats.TotalSearches} bÃºsquedas, {_stats.TotalDownloads} descargas");
                    }
                }
                else
                {
                    _logger?.Info("No hay estadÃ­sticas previas, iniciando nuevo tracking");
                }
            }
            catch (Exception ex)
            {
                _logger?.Error("Error cargando estadÃ­sticas", ex);
                _stats = new AppStats
                {
                    FirstUseDate = DateTime.Now,
                    LastUseDate = DateTime.Now
                };
            }
        }

        private string FormatSize(long bytes)
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
}

