using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SlskDown.Models;

namespace SlskDown.Core
{
    /// <summary>
    /// Métodos de extensión para DownloadManager que implementan las 7 mejoras
    /// </summary>
    public partial class DownloadManager
    {
        /// <summary>
        /// MEJORA #1: Guarda el progreso de todas las descargas activas
        /// </summary>
        private void SaveAllProgress()
        {
            if (!config.EnableProgressPersistence || progressPersistence == null)
                return;

            try
            {
                lock (downloadQueueLock)
                {
                    var activeDownloads = downloadQueue
                        .Where(t => t.Status == DownloadStatus.Downloading && t.BytesDownloaded > 0)
                        .ToList();

                    foreach (var task in activeDownloads)
                    {
                        progressPersistence.SaveProgress(
                            task.File.FileName,
                            task.BytesDownloaded,
                            task.File.SizeBytes
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error guardando progreso: {ex.Message}");
            }
        }

        /// <summary>
        /// MEJORA #1: Carga el progreso guardado de una descarga
        /// </summary>
        private long LoadProgress(string fileName)
        {
            if (!config.EnableProgressPersistence || progressPersistence == null)
                return 0;

            try
            {
                var progress = progressPersistence.LoadProgress(fileName);
                if (progress != null && progress.BytesDownloaded > 0)
                {
                    Log($"📥 Reanudando descarga: {fileName} desde {FormatBytes(progress.BytesDownloaded)}");
                    return progress.BytesDownloaded;
                }
            }
            catch { }

            return 0;
        }

        /// <summary>
        /// MEJORA #2: Verifica si un archivo debe reintentar la descarga
        /// </summary>
        private bool ShouldRetryDownload(DownloadTask task, string reason)
        {
            if (errorManager == null)
                return true;
                
            var fileKey = $"{task.File.Username}_{task.File.FileName}";
            
            if (!errorManager.ShouldRetry(fileKey))
            {
                Log($"❌ Archivo alcanzó límite de reintentos: {task.File.FileName}");
                
                // MEJORA #3: Notificar error
                notificationManager.NotifyDownloadError(task.File.FileName, "Límite de reintentos alcanzado");
                
                return false;
            }

            errorManager.RecordFailure(fileKey, reason);
            var failureCount = errorManager.GetFailureCount(fileKey);
            Log($"⚠️ Reintento {failureCount}/{config.MaxFailuresPerFile}: {task.File.FileName} - {reason}");

            // MEJORA #2: Mover al final de la cola si está configurado
            if (config.MoveFailedToEnd && failureCount >= 2)
            {
                MoveTaskToEndOfQueue(task);
            }

            return true;
        }

        /// <summary>
        /// MEJORA #2: Mueve una tarea al final de la cola
        /// </summary>
        private void MoveTaskToEndOfQueue(DownloadTask task)
        {
            lock (downloadQueueLock)
            {
                if (downloadQueue.Remove(task))
                {
                    downloadQueue.Add(task);
                    Log($"🔄 Tarea movida al final de la cola: {task.File.FileName}");
                }
            }
        }

        /// <summary>
        /// MEJORA #3: Notifica cuando se completa una descarga
        /// </summary>
        private void NotifyDownloadComplete(DownloadTask task, TimeSpan duration, double avgSpeedKBps)
        {
            if (!config.EnableNotifications || notificationManager == null)
                return;

            try
            {
                // Notificar descarga individual
                notificationManager.NotifyDownloadComplete(task.File.FileName, task.File.SizeBytes);

                // MEJORA #6: Registrar estadísticas
                if (config.EnableDetailedStats)
                {
                    statsTracker.RecordDownload(
                        task.File.FileName,
                        task.File.SizeBytes,
                        duration,
                        avgSpeedKBps
                    );
                }

                // Verificar si la cola está completa
                lock (downloadQueueLock)
                {
                    var pendingTasks = downloadQueue
                        .Where(t => t.Status == DownloadStatus.Queued || 
                                   t.Status == DownloadStatus.Downloading ||
                                   t.Status == DownloadStatus.Paused)
                        .ToList();

                    if (pendingTasks.Count == 0)
                    {
                        var completedTasks = downloadQueue
                            .Where(t => t.Status == DownloadStatus.Completed)
                            .ToList();

                        if (completedTasks.Count > 0)
                        {
                            var totalBytes = completedTasks.Sum(t => t.File.SizeBytes);
                            notificationManager.NotifyQueueComplete(completedTasks.Count, totalBytes);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error en notificación: {ex.Message}");
            }
        }

        /// <summary>
        /// MEJORA #4: Aplica limitación de velocidad durante la descarga
        /// </summary>
        private async Task ApplySpeedLimit(int bytesRead)
        {
            if (config.MaxDownloadSpeedKBps <= 0 || speedLimiter == null)
                return;

            try
            {
                await speedLimiter.ThrottleIfNeeded(bytesRead);
            }
            catch { }
        }

        /// <summary>
        /// MEJORA #5: Verifica si debe buscar fuente alternativa
        /// </summary>
        private bool ShouldSearchAlternativeSource(DownloadTask task, double currentSpeedKBps)
        {
            if (!config.EnableAutoSourceSearch || sourceFinder == null)
                return false;

            var fileKey = $"{task.File.Username}_{task.File.FileName}";
            return sourceFinder.ShouldSearchAlternative(fileKey, currentSpeedKBps);
        }

        /// <summary>
        /// MEJORA #6: Obtiene resumen de estadísticas
        /// </summary>
        public DownloadStatsSummary GetStatisticsSummary(int days = 7)
        {
            if (!config.EnableDetailedStats || statsTracker == null)
                return null;

            try
            {
                return statsTracker.GetSummary(days);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// MEJORA #7: Aplica filtros y ordenamiento a la cola
        /// </summary>
        public List<DownloadTask> GetFilteredQueue(
            DownloadQueueFilter.FilterBy filter = DownloadQueueFilter.FilterBy.All,
            DownloadQueueFilter.SortBy sortBy = DownloadQueueFilter.SortBy.DateAdded,
            bool ascending = true,
            string searchTerm = null)
        {
            if (!config.EnableQueueFiltering)
                return GetQueueSnapshot();

            lock (downloadQueueLock)
            {
                var queue = new List<DownloadTask>(downloadQueue);

                // Aplicar filtro
                queue = DownloadQueueFilter.ApplyFilter(queue, filter);

                // Aplicar búsqueda
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    queue = DownloadQueueFilter.SearchQueue(queue, searchTerm);
                }

                // Aplicar ordenamiento
                queue = DownloadQueueFilter.ApplySort(queue, sortBy, ascending);

                return queue;
            }
        }

        /// <summary>
        /// MEJORA #2: Obtiene lista de archivos problemáticos
        /// </summary>
        public List<string> GetProblematicFiles()
        {
            return errorManager.GetProblematicFiles();
        }

        /// <summary>
        /// MEJORA #2: Reinicia contador de fallos para un archivo
        /// </summary>
        public void ResetFileFailures(string fileName, string username)
        {
            var fileKey = $"{username}_{fileName}";
            errorManager.ResetFailures(fileKey);
            Log($"🔄 Reiniciado contador de fallos: {fileName}");
        }

        /// <summary>
        /// Formatea bytes a formato legible
        /// </summary>
        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
