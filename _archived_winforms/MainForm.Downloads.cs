using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soulseek;

namespace SlskDown
{
    /// <summary>
    /// MainForm - Partial Class para gestión de descargas
    /// Contiene: download manager, queue, retry logic, progress tracking
    /// </summary>
    public partial class MainForm : Form
    {
        #region Download Queue Management
        
        /// <summary>
        /// Agrega una descarga a la cola
        /// </summary>
        private async Task<bool> QueueDownloadAsync(string username, string filename, long fileSize)
        {
            try
            {
                // Verificar si ya existe en la cola
                var added = downloadQueueService.TryAdd(
                    task,
                    d => d.Username.Equals(username, StringComparison.OrdinalIgnoreCase) &&
                         d.Filename.Equals(filename, StringComparison.OrdinalIgnoreCase),
                    comparison: (a, b) => a.QueuedTime.CompareTo(b.QueuedTime));

                if (!added)
                {
                    AutoLog($"⚠️ Descarga duplicada ignorada: {filename}");
                    return false;
                }
                
                // Verificar con Bloom Filter
                string fileKey = $"{username}|{filename}";
                if (downloadedFilesBloomFilter?.Contains(fileKey) == true)
                {
                    AutoLog($"🔍 Bloom Filter: Archivo posiblemente descargado - {filename}");
                    
                    // Verificar en disco para confirmar
                    string localPath = Path.Combine(downloadDirectory, filename);
                    if (File.Exists(localPath))
                    {
                        AutoLog($"✅ Archivo ya existe en disco - Omitido");
                        return false;
                    }
                }
                
                // Crear tarea de descarga
                var task = new DownloadTask
                {
                    Username = username,
                    Filename = filename,
                    Size = fileSize,
                    Status = DownloadStatus.Queued,
                    QueuedTime = DateTime.Now,
                    RetryCount = 0,
                    AutoRetryEnabled = true
                };
                
                AutoLog($"➕ Agregado a cola: {filename} ({FormatFileSize(fileSize)})");
                
                // Actualizar UI
                await UpdateDownloadListView();
                
                return true;
            }
            catch (Exception ex)
            {
                AutoLog($"❌ Error agregando a cola: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Procesa la cola de descargas
        /// </summary>
        private async Task ProcessDownloadQueue()
        {
            var runningTasks = new List<Task>();

            try
            {
                while (downloadManagerRunning)
                {
                    try
                    {
                        int currentLimit = Math.Max(1, maxSimultaneousDownloads);

                        runningTasks.RemoveAll(t => t.IsCompleted);

                        while (downloadManagerRunning && runningTasks.Count < currentLimit)
                        {
                            DownloadTask? nextTask = null;

                            nextTask = downloadQueueService.WithQueueLock(list =>
                                list.Where(t => t.Status == DownloadStatus.Queued || t.Status == DownloadStatus.Failed)
                                    .OrderByDescending(t => t.Priority)
                                    .ThenBy(t => t.QueuedTime)
                                    .FirstOrDefault());

                            if (nextTask == null)
                                break;

                            var downloadJob = ProcessSingleDownload(nextTask);
                            runningTasks.Add(downloadJob);
                        }

                        if (runningTasks.Count > 0)
                        {
                            var completedTask = await Task.WhenAny(runningTasks);
                            runningTasks.Remove(completedTask);

                            try
                            {
                                await completedTask;
                            }
                            catch (Exception completedEx)
                            {
                                AutoLog($"❌ Error en descarga paralela: {completedEx.Message}");
                            }
                        }
                        else
                        {
                            await Task.Delay(500);
                        }
                    }
                    catch (Exception loopEx)
                    {
                        AutoLog($"❌ Error procesando cola: {loopEx.Message}");
                        await Task.Delay(2000);
                    }
                }
            }
            finally
            {
                if (runningTasks.Count > 0)
                {
                    try
                    {
                        await Task.WhenAll(runningTasks);
                    }
                    catch (Exception finalEx)
                    {
                        AutoLog($"⚠️ Error finalizando descargas pendientes: {finalEx.Message}");
                    }
                }
            }
        }
        
        /// <summary>
        /// Procesa una descarga individual
        /// </summary>
        private async Task ProcessSingleDownload(DownloadTask task)
        {
            try
            {
                task.Status = DownloadStatus.Downloading;
                task.StartTime = DateTime.Now;
                
                AutoLog($"⬇️ Descargando: {task.Filename} de {task.Username}");
                
                // Actualizar UI
                await UpdateDownloadListView();
                
                // Realizar descarga
                var localPath = Path.Combine(downloadDirectory, task.Filename);
                
                var options = new TransferOptions(
                    stateChanged: (args) => OnDownloadStateChanged(task, args),
                    progressUpdated: (args) => OnDownloadProgressUpdated(task, args)
                );
                
                await client.DownloadAsync(
                    task.Username,
                    task.Filename,
                    localPath,
                    options: options,
                    cancellationToken: downloadCancellationTokenSource.Token
                );
                
                // Descarga completada
                task.Status = DownloadStatus.Completed;
                task.CompletedTime = DateTime.Now;
                task.Progress = 100;
                MarkDownloadStatsDirty();

                AutoLog($"✅ Descarga completada: {task.Filename}");

                // Verificar tamaño final
                if (task.TargetSize.HasValue && task.Size != task.TargetSize.Value)
                {
                    AutoLog($"⚠️ Tamaño final no coincide: {task.Filename} ({task.Size} != {task.TargetSize})");
                }

                // Agregar a Bloom Filter
                string fileKey = $"{task.Username}|{task.Filename}";
                downloadedFilesBloomFilter?.Add(fileKey);

                
                task.LocalPath = localPath;


                if (File.Exists(localPath))
                {
                    try
                    {
                        bool isSpanish = IsSpanishFileByContent(localPath);
                        task.IsSpanish = isSpanish;

                        if (!isSpanish)
                        {
                            AutoLog($"🚫 {task.Filename}: detectado como NO español tras analizar el contenido. Moviendo a carpeta de revisión.");

                            string quarantineDir = Path.Combine(downloadDir, "_NoEspanol");
                            Directory.CreateDirectory(quarantineDir);

                            string previousPath = localPath;
                            string targetPath = Path.Combine(quarantineDir, task.Filename);
                            int counter = 1;
                            while (File.Exists(targetPath))
                            {
                                string nameWithoutExt = Path.GetFileNameWithoutExtension(task.Filename);
                                string ext = Path.GetExtension(task.Filename);
                                targetPath = Path.Combine(quarantineDir, $"{nameWithoutExt}_{counter}{ext}");
                                counter++;
                            }

                            File.Move(localPath, targetPath);
                            task.LocalPath = targetPath;
                            AutoLog($"📁 Archivo movido a: {targetPath}");

                            var detectorSource = RustCore.IsAvailable() ? "rust" : "fallback";
                            AppendQuarantineLog("moved_to_quarantine", task.Filename, previousPath, targetPath, detectorSource);
                            
                            _ = UpdateQuarantineStatsAsync();
                        }
                    }
                    catch (Exception detectionEx)
                    {
                        AutoLog($"⚠️ Error analizando o moviendo el archivo {task.Filename}: {detectionEx.Message}");
                    }
                }

                // Incrementar telemetría
                telemetryService?.IncrementCounter("downloads.completed");
                telemetryService?.RecordValue("download.size_mb", task.Size / 1024.0 / 1024.0);
                
            }
            catch (OperationCanceledException)
            {
                task.Status = DownloadStatus.Cancelled;
                AutoLog($"⏹️ Cancelado: {task.Filename}");
                MarkDownloadStatsDirty();
            }
            catch (Exception ex)
            {
                task.Status = DownloadStatus.Failed;
                task.ErrorMessage = ex.Message;
                task.RetryCount++;
                
                AutoLog($"❌ Error descargando {task.Filename}: {ex.Message}");
                RegisterDownloadError($"{task.Filename}: {ex.Message}");
                
                // Incrementar telemetría
                telemetryService?.IncrementCounter("downloads.failed");
                
                // Reintentar si está habilitado
                if (task.AutoRetryEnabled && task.RetryCount < maxRetries)
                {
                    AutoLog($"🔄 Reintentando en {retryDelaySeconds}s (intento {task.RetryCount}/{maxRetries})");
                    await Task.Delay(retryDelaySeconds * 1000);
                    task.Status = DownloadStatus.Queued;
                }
                else
                {
                    MarkDownloadStatsDirty();
                }
            }
            finally
            {
                MarkDownloadStatsDirty();
                await UpdateDownloadListView();
            }
        }
        
        #endregion
        
        #region Download Progress Tracking
        
        private void OnDownloadStateChanged(DownloadTask task, TransferStateChangedEventArgs args)
        {
            SafeBeginInvoke(() =>
            {
                AutoLog($"📊 {task.Filename}: {args.PreviousState} → {args.State}");
            });
        }
        
        private void OnDownloadProgressUpdated(DownloadTask task, TransferProgressUpdatedEventArgs args)
        {
            task.Progress = args.PercentComplete;
            task.BytesDownloaded = args.BytesTransferred;
            
            // Calcular velocidad
            if (task.StartTime.HasValue)
            {
                var elapsed = DateTime.Now - task.StartTime.Value;
                if (elapsed.TotalSeconds > 0)
                {
                    var bytesPerSecond = args.BytesTransferred / elapsed.TotalSeconds;
                    task.Speed = bytesPerSecond;
                    task.SpeedMBps = bytesPerSecond / (1024.0 * 1024.0);
                }
            }
            
            // Actualizar UI cada 500ms para evitar sobrecarga
            var now = DateTime.Now;
            if ((now - task.LastUIUpdate).TotalMilliseconds > 500)
            {
                task.LastUIUpdate = now;
                SafeBeginInvoke(() => UpdateDownloadListView());
                MarkDownloadStatsDirty();
            }
        }
        
        #endregion
        
        #region Download Statistics
        
        /// <summary>
        /// Obtiene estadísticas de descargas
        /// </summary>
        private DownloadStatistics GetDownloadStatistics()
        {
            return downloadQueueService.WithQueueLock(list =>
            {
                var stats = new DownloadStatistics
                {
                    TotalDownloads = list.Count,
                    CompletedDownloads = list.Count(d => d.Status == DownloadStatus.Completed),
                    FailedDownloads = list.Count(d => d.Status == DownloadStatus.Failed),
                    ActiveDownloads = list.Count(d => d.Status == DownloadStatus.Downloading),
                    QueuedDownloads = list.Count(d => d.Status == DownloadStatus.Queued),
                    TotalBytesDownloaded = list.Where(d => d.Status == DownloadStatus.Completed).Sum(d => d.Size),
                    AverageSpeed = list.Where(d => d.Speed > 0).Average(d => (double?)d.Speed) ?? 0
                };

                return stats;
            });
        }
        
        #endregion
    }
    
    /// <summary>
    /// Estadísticas de descargas
    /// </summary>
    public class DownloadStatistics
    {
        public int TotalDownloads { get; set; }
        public int CompletedDownloads { get; set; }
        public int FailedDownloads { get; set; }
        public int ActiveDownloads { get; set; }
        public int QueuedDownloads { get; set; }
        public long TotalBytesDownloaded { get; set; }
        public double AverageSpeed { get; set; }
        
        public double SuccessRate => TotalDownloads > 0 
            ? (double)CompletedDownloads / TotalDownloads * 100 
            : 0;
    }
}
