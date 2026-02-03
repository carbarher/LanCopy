using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Soulseek;

namespace SlskDown
{
    /// <summary>
    /// MainForm - Partial class para operaciones de descarga
    /// </summary>
    public partial class MainForm
    {
        // Cola de descargas con prioridad
        private readonly ConcurrentQueue<DownloadTask> _downloadQueue = new();
        private readonly ConcurrentDictionary<string, DownloadProgress> _activeDownloads = new();
        private readonly SemaphoreSlim _downloadSemaphore = new(3); // Máximo 3 descargas simultáneas
        
        /// <summary>
        /// Agrega una descarga a la cola
        /// </summary>
        private void QueueDownload(string username, string filename, long fileSize)
        {
            var task = new DownloadTask
            {
                Username = username,
                Filename = filename,
                FileSize = fileSize,
                QueuedAt = DateTime.UtcNow,
                Priority = CalculateDownloadPriority(filename, fileSize)
            };
            
            _downloadQueue.Enqueue(task);
            Log($"📥 Descarga en cola: {Path.GetFileName(filename)} ({FormatFileSize(fileSize)})");
            
            // Iniciar procesamiento de cola si no está corriendo
            _ = ProcessDownloadQueueAsync();
        }
        
        /// <summary>
        /// Procesa la cola de descargas
        /// </summary>
        private async Task ProcessDownloadQueueAsync()
        {
            while (_downloadQueue.TryDequeue(out var task))
            {
                await _downloadSemaphore.WaitAsync();
                
                try
                {
                    await DownloadFileAsync(task);
                }
                finally
                {
                    _downloadSemaphore.Release();
                }
            }
        }
        
        /// <summary>
        /// Descarga un archivo
        /// </summary>
        private async Task DownloadFileAsync(DownloadTask task)
        {
            var fileName = Path.GetFileName(task.Filename);
            var progress = new DownloadProgress
            {
                FileName = fileName,
                TotalBytes = task.FileSize,
                StartTime = DateTime.UtcNow
            };
            
            _activeDownloads.TryAdd(fileName, progress);
            
            try
            {
                Log($"⬇️ Descargando: {fileName}");
                
                // Preparar ruta local
                var localPath = Path.Combine(downloadDir, fileName);
                System.IO.Directory.CreateDirectory(downloadDir);
                
                // Agregar a lista de descargas en UI
                ListViewItem downloadItem = null;
                SafeInvoke(() =>
                {
                    downloadItem = new ListViewItem(fileName);
                    downloadItem.SubItems.Add(task.Username);
                    downloadItem.SubItems.Add("0%");
                    downloadItem.SubItems.Add("Descargando...");
                    downloadItem.SubItems.Add(FormatSize(task.FileSize));
                    downloadItem.SubItems.Add("");
                    AddItemWithAutoScroll(lvDownloads, downloadItem);
                });
                
                // Realizar la descarga usando el cliente de Soulseek
                if (client != null && client.State == SoulseekClientStates.Connected)
                {
                    await client.DownloadAsync(
                        task.Username,
                        task.Filename,
                        () => Task.FromResult<Stream>(new FileStream(localPath, FileMode.Create)),
                        task.FileSize,
                        options: new TransferOptions(
                            progressUpdated: (args) =>
                            {
                                var percent = (int)((args.Transfer.BytesTransferred / (double)task.FileSize) * 100);
                                progress.BytesDownloaded = args.Transfer.BytesTransferred;
                                
                                SafeBeginInvoke(() =>
                                {
                                    if (downloadItem != null)
                                    {
                                        downloadItem.SubItems[2].Text = $"{percent}%";
                                        downloadItem.SubItems[3].Text = $"Descargando... {FormatSize(args.Transfer.BytesTransferred)} / {FormatSize(task.FileSize)}";
                                    }
                                });
                            }
                        )
                    );
                    
                    // Actualizar UI al completar
                    SafeInvoke(() =>
                    {
                        if (downloadItem != null)
                        {
                            downloadItem.SubItems[2].Text = "100%";
                            downloadItem.SubItems[3].Text = "✓ Completado";
                            downloadItem.ForeColor = Color.LimeGreen;
                        }
                    });
                    
                    Log($"✅ Completado: {fileName}");
                    
                    // Actualizar estadísticas
                    totalDownloads++;
                    SaveStats();
                }
                else
                {
                    throw new Exception("Cliente no conectado");
                }
            }
            catch (OperationCanceledException)
            {
                Log($"⏸️ Cancelado: {fileName}");
            }
            catch (Exception ex)
            {
                Log($"❌ Error descargando {fileName}: {ex.Message}");
                
                // Reintentar si es apropiado
                if (ShouldRetry(task))
                {
                    task.RetryCount++;
                    _downloadQueue.Enqueue(task);
                    Log($"🔄 Reintentando {fileName} ({task.RetryCount}/3)");
                }
            }
            finally
            {
                _activeDownloads.TryRemove(fileName, out _);
            }
        }
        
        /// <summary>
        /// Calcula la prioridad de descarga
        /// </summary>
        private int CalculateDownloadPriority(string filename, long fileSize)
        {
            int priority = 0;
            
            // Archivos más pequeños tienen mayor prioridad
            if (fileSize < 1024 * 1024) // < 1MB
                priority += 10;
            else if (fileSize < 10 * 1024 * 1024) // < 10MB
                priority += 5;
            
            // Ciertos tipos de archivo tienen mayor prioridad
            var ext = Path.GetExtension(filename).ToLowerInvariant();
            if (ext == ".epub" || ext == ".pdf")
                priority += 5;
            
            return priority;
        }
        
        /// <summary>
        /// Determina si se debe reintentar una descarga
        /// </summary>
        private bool ShouldRetry(DownloadTask task)
        {
            return task.RetryCount < 3;
        }
        
        /// <summary>
        /// Cancela una descarga activa
        /// </summary>
        private void CancelDownload(string fileName)
        {
            if (activeDownloads.TryGetValue(fileName, out var cts))
            {
                cts.Cancel();
                Log($"⏹️ Cancelando: {fileName}");
            }
        }
        
        /// <summary>
        /// Cancela todas las descargas
        /// </summary>
        private void CancelAllDownloads()
        {
            foreach (var kvp in activeDownloads)
            {
                kvp.Value.Cancel();
            }
            
            Log("⏹️ Cancelando todas las descargas...");
        }
        
        /// <summary>
        /// Obtiene estadísticas de descargas
        /// </summary>
        private DownloadStats GetDownloadStats()
        {
            var stats = new DownloadStats
            {
                ActiveDownloads = _activeDownloads.Count,
                QueuedDownloads = _downloadQueue.Count,
                TotalBytesDownloaded = _activeDownloads.Values.Sum(p => p.BytesDownloaded),
                AverageSpeed = CalculateAverageSpeed()
            };
            
            return stats;
        }
        
        /// <summary>
        /// Calcula la velocidad promedio de descarga
        /// </summary>
        private double CalculateAverageSpeed()
        {
            if (_activeDownloads.IsEmpty)
                return 0;
            
            var speeds = _activeDownloads.Values
                .Select(p => p.BytesDownloaded / (DateTime.UtcNow - p.StartTime).TotalSeconds)
                .Where(s => !double.IsNaN(s) && !double.IsInfinity(s));
            
            return speeds.Any() ? speeds.Average() : 0;
        }
    }
    
    /// <summary>
    /// Tarea de descarga
    /// </summary>
    internal class DownloadTask
    {
        public string Username { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime QueuedAt { get; set; }
        public int Priority { get; set; }
        public int RetryCount { get; set; }
    }
    
    /// <summary>
    /// Progreso de descarga
    /// </summary>
    internal class DownloadProgress
    {
        public string FileName { get; set; } = string.Empty;
        public long TotalBytes { get; set; }
        public long BytesDownloaded { get; set; }
        public DateTime StartTime { get; set; }
        public double PercentComplete => TotalBytes > 0 ? (BytesDownloaded * 100.0 / TotalBytes) : 0;
    }
    
    /// <summary>
    /// Estadísticas de descargas
    /// </summary>
    internal class DownloadStats
    {
        public int ActiveDownloads { get; set; }
        public int QueuedDownloads { get; set; }
        public long TotalBytesDownloaded { get; set; }
        public double AverageSpeed { get; set; }
    }
}
