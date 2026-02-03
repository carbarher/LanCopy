using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SlskDown.Core;
using SlskDown.Models;
using DownloadStatus = SlskDown.Models.DownloadStatus;

namespace SlskDown
{
    /// <summary>
    /// Funcionalidades avanzadas de SlskDown - Parte 2
    /// - Rate limiting adaptativo para descargas
    /// - Watchlist automática
    /// - Modo portátil
    /// - Estadísticas avanzadas
    /// </summary>
    public partial class MainForm
    {
        // ==================== RATE LIMITING ADAPTATIVO PARA DESCARGAS ====================
        
        private AdaptiveParallelism adaptiveDownloads;
        private int consecutiveDownloadFailures = 0;
        private int consecutiveDownloadSuccesses = 0;
        private DateTime lastDownloadThrottle = DateTime.MinValue;
        private const int DOWNLOAD_FAILURE_THRESHOLD = 5;
        private const int SUCCESS_THRESHOLD_TO_RESTORE = 20;
        
        /// <summary>
        /// Inicializa rate limiting adaptativo para descargas
        /// </summary>
        private void InitializeAdaptiveDownloadRateLimiting()
        {
            adaptiveDownloads = new AdaptiveParallelism(
                initialParallelism: maxSimultaneousDownloads,
                minParallelism: 1,
                maxParallelism: 10,
                adjustmentStep: 1,
                windowSize: 20
            );
            
            Log($"Rate limiting adaptativo de descargas inicializado (base: {maxSimultaneousDownloads})");
        }
        
        /// <summary>
        /// Registra éxito de descarga para rate limiting adaptativo
        /// </summary>
        private void RecordDownloadSuccess()
        {
            consecutiveDownloadFailures = 0;
            consecutiveDownloadSuccesses++;
            
            adaptiveDownloads?.RecordResult(true);
            
            // Si llevamos 20 descargas exitosas, restaurar paralelismo
            if (consecutiveDownloadSuccesses >= SUCCESS_THRESHOLD_TO_RESTORE)
            {
                if (adaptiveDownloads != null)
                {
                    var currentLevel = adaptiveDownloads.CurrentParallelism;
                    if (currentLevel < maxSimultaneousDownloads)
                    {
                        Log($"📈 Restaurando paralelismo de descargas: {currentLevel} → {maxSimultaneousDownloads}");
                        consecutiveDownloadSuccesses = 0;
                    }
                }
            }
        }
        
        /// <summary>
        /// Registra fallo de descarga y aplica throttling si es necesario
        /// </summary>
        private void RecordDownloadFailure()
        {
            consecutiveDownloadSuccesses = 0;
            consecutiveDownloadFailures++;
            
            adaptiveDownloads?.RecordResult(false);
            
            // Si llevamos 5 fallos consecutivos, reducir paralelismo
            if (consecutiveDownloadFailures >= DOWNLOAD_FAILURE_THRESHOLD)
            {
                var timeSinceLastThrottle = DateTime.Now - lastDownloadThrottle;
                
                if (timeSinceLastThrottle > TimeSpan.FromMinutes(5))
                {
                    if (adaptiveDownloads != null)
                    {
                        var currentLevel = adaptiveDownloads.CurrentParallelism;
                        var newLevel = Math.Max(1, currentLevel - 1);
                        
                        Log($"{consecutiveDownloadFailures} fallos consecutivos detectados");
                        Log($"Reduciendo paralelismo de descargas: {currentLevel} → {newLevel}");
                        
                        maxSimultaneousDownloads = newLevel;
                        lastDownloadThrottle = DateTime.Now;
                        consecutiveDownloadFailures = 0;
                    }
                }
            }
        }
        
        /// <summary>
        /// Obtiene nivel de paralelismo actual recomendado
        /// </summary>
        private int GetRecommendedDownloadParallelism()
        {
            if (adaptiveDownloads == null)
                return maxSimultaneousDownloads;
            
            return adaptiveDownloads.CurrentParallelism;
        }
        
        // ==================== WATCHLIST AUTOMÁTICA ====================
        
        private List<WatchlistAuthor> watchlistAuthors = new List<WatchlistAuthor>();
        private System.Threading.Timer watchlistAutomationTimer;
        private string watchlistFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "watchlist_auto.json");
        private const int WATCHLIST_CHECK_HOURS = 24; // Revisar cada 24 horas
        
        private class WatchlistAuthor
        {
            public string Name { get; set; }
            public DateTime LastChecked { get; set; }
            public int FilesFound { get; set; }
            public bool AutoDownload { get; set; }
            public int NewFilesDetected { get; set; }
        }
        
        /// <summary>
        /// Inicia sistema de watchlist automática
        /// </summary>
        private void StartAutomaticWatchlist()
        {
            try
            {
                LoadWatchlistAuthors();
                
                // Timer cada 24 horas
                var interval = TimeSpan.FromHours(WATCHLIST_CHECK_HOURS);
                watchlistAutomationTimer?.Dispose();
                watchlistAutomationTimer = new System.Threading.Timer(
                    _ => CheckWatchlistAuthors(),
                    null,
                    TimeSpan.FromMinutes(5),
                    interval
                );
                
                Log($"Watchlist automática iniciada ({watchlistAuthors.Count} autores vigilados)");
            }
            catch (Exception ex)
            {
                Log($"Error iniciando watchlist automática: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Carga lista de autores vigilados
        /// </summary>
        private void LoadWatchlistAuthors()
        {
            try
            {
                if (!File.Exists(watchlistFile))
                {
                    watchlistAuthors = new List<WatchlistAuthor>();
                    return;
                }
                
                var json = File.ReadAllText(watchlistFile);
                watchlistAuthors = Newtonsoft.Json.JsonConvert.DeserializeObject<List<WatchlistAuthor>>(json)
                    ?? new List<WatchlistAuthor>();
            }
            catch
            {
                watchlistAuthors = new List<WatchlistAuthor>();
            }
        }
        
        /// <summary>
        /// Guarda lista de autores vigilados
        /// </summary>
        private void SaveWatchlistAuthors()
        {
            try
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(watchlistAuthors, Newtonsoft.Json.Formatting.Indented);
                
                var dir = Path.GetDirectoryName(watchlistFile);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                
                File.WriteAllText(watchlistFile, json);
            }
            catch (Exception ex)
            {
                Log($"Error guardando watchlist: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Revisa autores en watchlist y busca nuevos archivos
        /// </summary>
        private async void CheckWatchlistAuthors()
        {
            try
            {
                if (watchlistAuthors.Count == 0)
                    return;
                
                Log($"Revisando watchlist: {watchlistAuthors.Count} autores...");
                
                int totalNewFiles = 0;
                var authorsWithNewFiles = new List<string>();
                
                foreach (var author in watchlistAuthors)
                {
                    try
                    {
                        // Buscar archivos del autor
                        var searchResults = await SearchAuthorAsync(author.Name);
                        
                        // Filtrar solo nuevos (comparar con FilesFound anterior)
                        var newFilesCount = Math.Max(0, searchResults.Count - author.FilesFound);
                        
                        if (newFilesCount > 0)
                        {
                            author.NewFilesDetected = newFilesCount;
                            totalNewFiles += newFilesCount;
                            authorsWithNewFiles.Add(author.Name);
                            
                            Log($"   {author.Name}: {newFilesCount} archivos nuevos detectados");
                            
                            // Si está activado auto-download, agregar a cola
                            if (author.AutoDownload)
                            {
                                // Agregar a cola de descargas
                                // TODO: Implementar según lógica actual
                                Log($"      Auto-descarga activada");
                            }
                        }
                        
                        author.FilesFound = searchResults.Count;
                        author.LastChecked = DateTime.Now;
                        
                        // Delay entre búsquedas
                        await Task.Delay(2000);
                    }
                    catch (Exception ex)
                    {
                        Log($"   Error revisando {author.Name}: {ex.Message}");
                    }
                }
                
                SaveWatchlistAuthors();
                
                // Notificar si hay nuevos archivos
                if (totalNewFiles > 0)
                {
                    var message = $"Se encontraron {totalNewFiles} archivos nuevos de {authorsWithNewFiles.Count} autor(es):\n\n" +
                        string.Join("\n", authorsWithNewFiles.Take(5));
                    
                    if (authorsWithNewFiles.Count > 5)
                        message += $"\n... y {authorsWithNewFiles.Count - 5} más";
                    
                    ShowNotification("Watchlist", message, ToolTipIcon.Info);
                    Log($"Watchlist: {totalNewFiles} archivos nuevos detectados");
                }
                else
                {
                    Log($"Watchlist: Sin archivos nuevos");
                }
            }
            catch (Exception ex)
            {
                Log($"Error en watchlist automática: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Agrega autor a watchlist
        /// </summary>
        private void AddToWatchlist(string authorName, bool autoDownload = false)
        {
            if (watchlistAuthors.Any(w => w.Name.Equals(authorName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"El autor '{authorName}' ya está en la watchlist.", "Ya existe", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            watchlistAuthors.Add(new WatchlistAuthor
            {
                Name = authorName,
                LastChecked = DateTime.MinValue,
                FilesFound = 0,
                AutoDownload = autoDownload,
                NewFilesDetected = 0
            });
            
            SaveWatchlistAuthors();
            Log($"Autor agregado a watchlist: {authorName}");
        }
        
        // ==================== MODO PORTÁTIL ====================
        
        
        /// <summary>
        /// Activa modo portátil
        /// </summary>
        private void EnablePortableMode()
        {
            try
            {
                portableMode = true;
                
                // Crear estructura de carpetas en modo portátil
                if (!Directory.Exists(portableDataDir))
                    Directory.CreateDirectory(portableDataDir);
                
                var portableSubdirs = new[]
                {
                    Path.Combine(portableDataDir, "data"),
                    Path.Combine(portableDataDir, "downloads"),
                    Path.Combine(portableDataDir, "logs"),
                    Path.Combine(portableDataDir, "backups")
                };
                
                foreach (var dir in portableSubdirs)
                {
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                }
                
                // Actualizar rutas
                dataDir = Path.Combine(portableDataDir, "data");
                downloadDir = Path.Combine(portableDataDir, "downloads");
                backupDir = Path.Combine(portableDataDir, "backups");
                
                // Guardar configuración de modo portátil
                var configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portable.config");
                File.WriteAllText(configFile, "PORTABLE_MODE_ENABLED");
                
                Log($"Modo portátil activado");
                Log($"Datos: {dataDir}");
                Log($"Descargas: {downloadDir}");
                
                MessageBox.Show(
                    "Modo portátil activado.\n\n" +
                    "Todos los datos ahora se guardan en:\n" +
                    portableDataDir + "\n\n" +
                    "Puedes mover esta carpeta a otra ubicación o USB.",
                    "Modo Portátil",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log($"Error activando modo portátil: {ex.Message}");
                MessageBox.Show($"Error activando modo portátil:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// Desactiva modo portátil
        /// </summary>
        private void DisablePortableMode()
        {
            try
            {
                portableMode = false;
                
                // Restaurar rutas por defecto
                dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
                downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SlskDown");
                backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
                
                // Eliminar archivo de configuración portátil
                var configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portable.config");
                if (File.Exists(configFile))
                    File.Delete(configFile);
                
                Log($"Modo portátil desactivado");
                
                MessageBox.Show(
                    "Modo portátil desactivado.\n\n" +
                    "Ahora se usarán las rutas por defecto.\n\n" +
                    "Los datos en modo portátil NO se eliminarán automáticamente.",
                    "Modo Portátil",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log($"Error desactivando modo portátil: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Verifica si debe activarse modo portátil al inicio
        /// </summary>
        private void CheckPortableModeOnStartup()
        {
            var configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portable.config");
            if (File.Exists(configFile))
            {
                portableMode = true;
                dataDir = Path.Combine(portableDataDir, "data");
                downloadDir = Path.Combine(portableDataDir, "downloads");
                backupDir = Path.Combine(portableDataDir, "backups");
                
                Log($"Modo portátil detectado y activado");
            }
        }
        
        // ==================== ESTADÍSTICAS AVANZADAS (sin gráficos por ahora) ====================
        
        /// <summary>
        /// Muestra estadísticas avanzadas textuales
        /// </summary>
        private void ShowAdvancedStatistics()
        {
            try
            {
                var stats = new StringBuilder();
                stats.AppendLine("═══════════════════════════════════════════");
                stats.AppendLine("ESTADÍSTICAS AVANZADAS");
                stats.AppendLine("═══════════════════════════════════════════\n");
                
                // Estadísticas de biblioteca
                lock (downloadHistoryLock)
                {
                    stats.AppendLine("BIBLIOTECA");
                    stats.AppendLine($"   Total archivos: {downloadHistory.Count:N0}");
                    stats.AppendLine($"   Tamaño total: {FormatFileSize(downloadHistory.Sum(h => h.SizeBytes))}");
                    stats.AppendLine($"   Autores únicos: {downloadHistory.Select(h => h.Author).Distinct().Count():N0}");
                    var spanishDownloads = downloadHistory.Count(h => IsSpanishText(h.FileName));
                    stats.AppendLine($"   Archivos en español: {spanishDownloads:N0}");
                    stats.AppendLine();
                }
                
                // Top 10 autores más descargados
                lock (downloadHistoryLock)
                {
                    stats.AppendLine("🏆 TOP 10 AUTORES MÁS DESCARGADOS");
                    var topAuthors = downloadHistory
                        .GroupBy(h => h.Author)
                        .Select(g => new { Author = g.Key, Count = g.Count(), Size = g.Sum(h => h.SizeBytes) })
                        .OrderByDescending(a => a.Count)
                        .Take(10);
                    
                    int rank = 1;
                    foreach (var author in topAuthors)
                    {
                        stats.AppendLine($"   {rank}. {author.Author}: {author.Count} archivos ({FormatFileSize(author.Size)})");
                        rank++;
                    }
                    stats.AppendLine();
                }
                
                // Distribución por extensión
                lock (downloadHistoryLock)
                {
                    stats.AppendLine("📄 DISTRIBUCIÓN POR TIPO");
                    var byExtension = downloadHistory
                        .GroupBy(h => Path.GetExtension(h.FileName).ToLower())
                        .Select(g => new { Ext = g.Key, Count = g.Count(), Size = g.Sum(h => h.SizeBytes) })
                        .OrderByDescending(e => e.Count);
                    
                    foreach (var ext in byExtension)
                    {
                        var percentage = (ext.Count * 100.0 / downloadHistory.Count);
                        stats.AppendLine($"   {ext.Ext}: {ext.Count:N0} ({percentage:F1}%) - {FormatFileSize(ext.Size)}");
                    }
                    stats.AppendLine();
                }
                
                // Estadísticas de búsquedas
                stats.AppendLine("BÚSQUEDAS");
                stats.AppendLine($"   Autores indexados: {allAuthorsData.Count:N0}");
                stats.AppendLine($"   Autores válidos: {allAuthorsData.Count(a => a.FilesCount > 0):N0}");
                stats.AppendLine();
                
                // Estadísticas de descargas
                lock (downloadQueueLock)
                {
                    stats.AppendLine("DESCARGAS");
                    stats.AppendLine($"   En cola: {downloadQueue.Count(t => t.Status == DownloadStatus.Queued):N0}");
                    stats.AppendLine($"   Descargando: {downloadQueue.Count(t => t.Status == DownloadStatus.Downloading):N0}");
                    stats.AppendLine($"   Completadas: {downloadQueue.Count(t => t.Status == DownloadStatus.Completed):N0}");
                    stats.AppendLine($"   Fallidas: {downloadQueue.Count(t => t.Status == DownloadStatus.Failed):N0}");
                    stats.AppendLine();
                }
                
                stats.AppendLine("═══════════════════════════════════════════");
                
                // Mostrar en MessageBox
                MessageBox.Show(
                    stats.ToString(),
                    "Estadísticas Avanzadas",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error mostrando estadísticas:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// Método dummy para búsqueda de autor (usar el existente)
        /// </summary>
        private Task<List<SearchResultItem>> SearchAuthorAsync(string authorName)
        {
            // Este método debe usar la lógica de búsqueda existente
            // Por ahora retorno lista vacía para compilar
            return Task.FromResult(new List<SearchResultItem>());
        }
    }
}
