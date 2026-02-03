using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SlskDown.Models;

namespace SlskDown.Services
{
    /// <summary>
    /// ImplementaciÃ³n del servicio de rastreo de descargas simuladas
    /// </summary>
    public class DownloadTrackingService : IDownloadTrackingService
    {
        private readonly string _downloadedFilePath;
        private readonly HashSet<string> _downloadedKeys;
        private readonly List<DownloadedFile> _downloadedFiles;
        private readonly object _lock = new object();
        private readonly ILoggingService? _logger;

        public DownloadTrackingService(ILoggingService? logger = null)
        {
            _logger = logger;
            _downloadedFilePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "downloaded_files.json"
            );
            
            _downloadedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _downloadedFiles = new List<DownloadedFile>();
            
            LoadDownloadedFiles();
        }

        /// <summary>
        /// Carga el archivo de descargas desde disco
        /// </summary>
        private void LoadDownloadedFiles()
        {
            try
            {
                Console.WriteLine($"[DownloadTracking] LoadDownloadedFiles() - Ruta: {_downloadedFilePath}");
                Console.WriteLine($"[DownloadTracking] LoadDownloadedFiles() - Existe: {File.Exists(_downloadedFilePath)}");
                
                if (File.Exists(_downloadedFilePath))
                {
                    var json = File.ReadAllText(_downloadedFilePath);
                    Console.WriteLine($"[DownloadTracking] JSON contenido: {json.Substring(0, Math.Min(100, json.Length))}...");
                    
                    var files = JsonSerializer.Deserialize<List<DownloadedFile>>(json);
                    
                    if (files != null)
                    {
                        lock (_lock)
                        {
                            _downloadedFiles.Clear();
                            _downloadedKeys.Clear();
                            
                            foreach (var file in files)
                            {
                                _downloadedFiles.Add(file);
                                _downloadedKeys.Add(file.GetKey());
                            }
                        }
                        
                        _logger?.Info($"Cargados {files.Count} archivos descargados del historial");
                        Console.WriteLine($"[DownloadTracking] Cargados {files.Count} archivos");
                    }
                }
                else
                {
                    Console.WriteLine("[DownloadTracking] Archivo no existe - iniciando con historial vacÃ­o");
                }
            }
            catch (Exception ex)
            {
                _logger?.Error("Error cargando historial de descargas", ex);
                Console.WriteLine($"[DownloadTracking] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Guarda el archivo de descargas a disco
        /// </summary>
        private void SaveDownloadedFiles()
        {
            try
            {
                lock (_lock)
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };
                    
                    var json = JsonSerializer.Serialize(_downloadedFiles, options);
                    File.WriteAllText(_downloadedFilePath, json);
                }
            }
            catch (Exception ex)
            {
                _logger?.Error("Error guardando historial de descargas", ex);
            }
        }

        /// <summary>
        /// Marca un archivo como descargado
        /// </summary>
        public void MarkAsDownloaded(string filename, string username, long size, string author)
        {
            var file = new DownloadedFile
            {
                Filename = filename,
                Username = username,
                Size = size,
                Author = author,
                DownloadedDate = DateTime.Now
            };
            
            var key = file.GetKey();
            
            lock (_lock)
            {
                if (!_downloadedKeys.Contains(key))
                {
                    _downloadedFiles.Add(file);
                    _downloadedKeys.Add(key);
                    
                    _logger?.Info($"Marcado como descargado: {Path.GetFileName(filename)} de {author}");
                    
                    // Guardar cada 10 archivos para no saturar el disco
                    if (_downloadedFiles.Count % 10 == 0)
                    {
                        SaveDownloadedFiles();
                    }
                }
            }
        }

        /// <summary>
        /// Verifica si un archivo ya fue descargado (solo por nombre y tamaÃ±o)
        /// </summary>
        public bool IsAlreadyDownloaded(string filename, string username, long size)
        {
            // Usar solo el nombre del archivo sin path
            var filenameOnly = Path.GetFileName(filename);
            var key = $"{filenameOnly}_{size}".ToLower();
            
            lock (_lock)
            {
                bool result = _downloadedKeys.Contains(key);
                
                // Solo loggear los primeros 50 para no saturar
                if (_downloadedKeys.Count <= 50 || _downloadedKeys.Count % 100 == 0)
                {
                    Console.WriteLine($"[DownloadTracking] IsAlreadyDownloaded('{filenameOnly}') = {result} (Total en memoria: {_downloadedKeys.Count})");
                }
                
                return result;
            }
        }

        /// <summary>
        /// Obtiene todos los archivos descargados
        /// </summary>
        public List<DownloadedFile> GetAllDownloaded()
        {
            lock (_lock)
            {
                return new List<DownloadedFile>(_downloadedFiles);
            }
        }

        /// <summary>
        /// Obtiene archivos descargados de un autor especÃ­fico
        /// </summary>
        public List<DownloadedFile> GetDownloadedByAuthor(string author)
        {
            lock (_lock)
            {
                return _downloadedFiles
                    .Where(f => f.Author.Equals(author, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        /// <summary>
        /// Limpia archivos descargados mÃ¡s antiguos que X dÃ­as
        /// </summary>
        public int CleanupOldDownloads(int daysOld)
        {
            var cutoffDate = DateTime.Now.AddDays(-daysOld);
            int removed = 0;
            
            lock (_lock)
            {
                var toRemove = _downloadedFiles
                    .Where(f => f.DownloadedDate < cutoffDate)
                    .ToList();
                
                foreach (var file in toRemove)
                {
                    _downloadedFiles.Remove(file);
                    _downloadedKeys.Remove(file.GetKey());
                    removed++;
                }
                
                if (removed > 0)
                {
                    SaveDownloadedFiles();
                    _logger?.Info($"Limpiados {removed} archivos antiguos del historial");
                }
            }
            
            return removed;
        }

        /// <summary>
        /// Obtiene estadÃ­sticas de descargas
        /// </summary>
        public (int total, int today, Dictionary<string, int> byAuthor) GetStats()
        {
            lock (_lock)
            {
                var total = _downloadedFiles.Count;
                var today = _downloadedFiles.Count(f => f.DownloadedDate.Date == DateTime.Now.Date);
                
                var byAuthor = _downloadedFiles
                    .GroupBy(f => f.Author)
                    .ToDictionary(
                        g => g.Key, 
                        g => g.Count(),
                        StringComparer.OrdinalIgnoreCase
                    );
                
                return (total, today, byAuthor);
            }
        }

        /// <summary>
        /// Guarda los cambios pendientes
        /// </summary>
        public void Flush()
        {
            SaveDownloadedFiles();
        }

        /// <summary>
        /// Limpia completamente el historial en memoria y recarga desde disco
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                int filesBefore = _downloadedFiles.Count;
                int keysBefore = _downloadedKeys.Count;
                
                _downloadedFiles.Clear();
                _downloadedKeys.Clear();
                
                _logger?.Info($"Historial limpiado en memoria - Archivos: {filesBefore} â†’ 0, Keys: {keysBefore} â†’ 0");
                Console.WriteLine($"[DownloadTracking] Clear() - Antes: {filesBefore} archivos, {keysBefore} keys");
            }
            
            // Recargar desde disco (deberÃ­a estar vacÃ­o)
            LoadDownloadedFiles();
            
            lock (_lock)
            {
                int filesAfter = _downloadedFiles.Count;
                int keysAfter = _downloadedKeys.Count;
                _logger?.Info($"DespuÃ©s de recargar - Archivos: {filesAfter}, Keys: {keysAfter}");
                Console.WriteLine($"[DownloadTracking] DespuÃ©s de recargar - Archivos: {filesAfter}, Keys: {keysAfter}");
            }
        }
    }
}

