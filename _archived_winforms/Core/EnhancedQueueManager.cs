using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SlskDown.Models;

namespace SlskDown.Core
{
    /// <summary>
    /// Gestor mejorado de cola de descargas con agrupación y acciones en lote
    /// </summary>
    public class EnhancedQueueManager
    {
        private readonly List<DownloadTask> queue;
        private readonly object queueLock = new object();
        private const int LockTimeoutMs = 30000; // 30 segundos timeout
        private readonly Action onQueueChanged;
        
        public EnhancedQueueManager(List<DownloadTask> downloadQueue, Action queueChangedCallback = null)
        {
            queue = downloadQueue;
            onQueueChanged = queueChangedCallback;
        }
        
        /// <summary>
        /// Agrupa descargas por usuario
        /// </summary>
        public Dictionary<string, List<DownloadTask>> GroupByUser()
        {
            lock (queueLock)
            {
                return queue
                    .GroupBy(d => d.Username ?? "Unknown")
                    .ToDictionary(g => g.Key, g => g.ToList());
            }
        }
        
        /// <summary>
        /// Agrupa descargas por carpeta de destino
        /// </summary>
        public Dictionary<string, List<DownloadTask>> GroupByFolder()
        {
            lock (queueLock)
            {
                return queue
                    .GroupBy(d => System.IO.Path.GetDirectoryName(d.LocalPath) ?? "Unknown")
                    .ToDictionary(g => g.Key, g => g.ToList());
            }
        }
        
        /// <summary>
        /// Agrupa descargas por estado
        /// </summary>
        public Dictionary<DownloadStatus, List<DownloadTask>> GroupByStatus()
        {
            lock (queueLock)
            {
                return queue
                    .GroupBy(d => d.Status)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }
        }
        
        /// <summary>
        /// Agrupa descargas por extensión de archivo
        /// </summary>
        public Dictionary<string, List<DownloadTask>> GroupByExtension()
        {
            lock (queueLock)
            {
                return queue
                    .GroupBy(d => System.IO.Path.GetExtension(d.LocalPath)?.ToLower() ?? "Unknown")
                    .ToDictionary(g => g.Key, g => g.ToList());
            }
        }
        
        /// <summary>
        /// Establece prioridad de una descarga
        /// </summary>
        public void SetPriority(string downloadId, DownloadPriority priority)
        {
            if (Monitor.TryEnter(queueLock, LockTimeoutMs))
            {
                try
                {
                    var task = queue.FirstOrDefault(d => d.LocalPath == downloadId);
                    if (task != null)
                    {
                        task.Priority = priority;
                        ReorderByPriority();
                        onQueueChanged?.Invoke();
                    }
                }
                finally
                {
                    Monitor.Exit(queueLock);
                }
            }
            else
            {
                throw new TimeoutException("No se pudo obtener acceso a la cola");
            }
        }
        
        /// <summary>
        /// Reordena la cola por prioridad
        /// </summary>
        private void ReorderByPriority()
        {
            lock (queueLock)
            {
                var ordered = queue
                    .OrderByDescending(d => d.Priority)
                    .ToList();
                
                queue.Clear();
                queue.AddRange(ordered);
            }
        }
        
        /// <summary>
        /// Pausa todas las descargas
        /// </summary>
        public int PauseAll()
        {
            lock (queueLock)
            {
                var count = 0;
                foreach (var task in queue.Where(d => d.Status == DownloadStatus.Downloading))
                {
                    task.Status = DownloadStatus.Paused;
                    count++;
                }
                onQueueChanged?.Invoke();
                return count;
            }
        }
        
        /// <summary>
        /// Reanuda todas las descargas pausadas
        /// </summary>
        public int ResumeAll()
        {
            lock (queueLock)
            {
                var count = 0;
                foreach (var task in queue.Where(d => d.Status == DownloadStatus.Paused))
                {
                    task.Status = DownloadStatus.Queued;
                    count++;
                }
                onQueueChanged?.Invoke();
                return count;
            }
        }
        
        /// <summary>
        /// Cancela todas las descargas de un usuario
        /// </summary>
        public int CancelByUser(string username)
        {
            lock (queueLock)
            {
                var toCancel = queue
                    .Where(d => d.Username.Equals(username, StringComparison.OrdinalIgnoreCase) &&
                               d.Status != DownloadStatus.Completed &&
                               d.Status != DownloadStatus.Failed)
                    .ToList();
                
                foreach (var task in toCancel)
                {
                    task.Status = DownloadStatus.Cancelled;
                }
                
                onQueueChanged?.Invoke();
                return toCancel.Count;
            }
        }
        
        /// <summary>
        /// Cancela todas las descargas con un estado específico
        /// </summary>
        public int CancelByStatus(DownloadStatus status)
        {
            lock (queueLock)
            {
                var toCancel = queue.Where(d => d.Status == status).ToList();
                
                foreach (var task in toCancel)
                {
                    task.Status = DownloadStatus.Cancelled;
                }
                
                onQueueChanged?.Invoke();
                return toCancel.Count;
            }
        }
        
        /// <summary>
        /// Elimina descargas completadas
        /// </summary>
        public int RemoveCompleted()
        {
            lock (queueLock)
            {
                var count = queue.RemoveAll(d => d.Status == DownloadStatus.Completed);
                onQueueChanged?.Invoke();
                return count;
            }
        }
        
        /// <summary>
        /// Elimina descargas fallidas
        /// </summary>
        public int RemoveFailed()
        {
            lock (queueLock)
            {
                var count = queue.RemoveAll(d => d.Status == DownloadStatus.Failed);
                onQueueChanged?.Invoke();
                return count;
            }
        }
        
        /// <summary>
        /// Reintenta todas las descargas fallidas
        /// </summary>
        public int RetryAllFailed()
        {
            lock (queueLock)
            {
                var count = 0;
                foreach (var task in queue.Where(d => d.Status == DownloadStatus.Failed))
                {
                    task.Status = DownloadStatus.Queued;
                    task.RetryCount = 0;
                    task.ErrorMessage = null;
                    count++;
                }
                onQueueChanged?.Invoke();
                return count;
            }
        }
        
        /// <summary>
        /// Mueve una descarga a una posición específica
        /// </summary>
        public bool MoveToPosition(string downloadId, int newPosition)
        {
            lock (queueLock)
            {
                var task = queue.FirstOrDefault(d => d.LocalPath == downloadId);
                if (task == null || newPosition < 0 || newPosition >= queue.Count)
                    return false;
                
                queue.Remove(task);
                queue.Insert(newPosition, task);
                onQueueChanged?.Invoke();
                return true;
            }
        }
        
        /// <summary>
        /// Obtiene estadísticas de la cola
        /// </summary>
        public QueueStatistics GetStatistics()
        {
            lock (queueLock)
            {
                return new QueueStatistics
                {
                    Total = queue.Count,
                    Queued = queue.Count(d => d.Status == DownloadStatus.Queued),
                    InProgress = queue.Count(d => d.Status == DownloadStatus.Downloading),
                    Paused = queue.Count(d => d.Status == DownloadStatus.Paused),
                    Completed = queue.Count(d => d.Status == DownloadStatus.Completed),
                    Failed = queue.Count(d => d.Status == DownloadStatus.Failed),
                    Cancelled = queue.Count(d => d.Status == DownloadStatus.Cancelled),
                    TotalSize = queue.Sum(d => (long)d.FileSize),
                    DownloadedSize = queue.Sum(d => d.BytesDownloaded),
                    AverageSpeed = queue.Where(d => d.SpeedMBps > 0).Average(d => (double?)d.SpeedMBps) ?? 0,
                    EstimatedTimeRemaining = TimeSpan.Zero
                };
            }
        }
        
        /// <summary>
        /// Filtra descargas por criterios
        /// </summary>
        public List<DownloadTask> Filter(Func<DownloadTask, bool> predicate)
        {
            lock (queueLock)
            {
                return queue.Where(predicate).ToList();
            }
        }
    }
    
    /// <summary>
    /// Estadísticas de la cola de descargas
    /// </summary>
    public class QueueStatistics
    {
        public int Total { get; set; }
        public int Queued { get; set; }
        public int InProgress { get; set; }
        public int Paused { get; set; }
        public int Completed { get; set; }
        public int Failed { get; set; }
        public int Cancelled { get; set; }
        public long TotalSize { get; set; }
        public long DownloadedSize { get; set; }
        public double AverageSpeed { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
        
        public double ProgressPercentage => 
            TotalSize > 0 ? (DownloadedSize / (double)TotalSize) * 100 : 0;
    }
}
