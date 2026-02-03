using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Cola de descargas en background con gestión inteligente
    /// Inspirado en Seeker Android background downloads
    /// </summary>
    public class BackgroundDownloadQueue
    {
        private readonly Queue<DownloadJob> pendingJobs;
        private readonly List<DownloadJob> activeJobs;
        private readonly List<DownloadJob> completedJobs;
        private readonly object queueLock = new object();
        private CancellationTokenSource cts;
        private Task workerTask;
        private bool isRunning;
        
        public int MaxConcurrentDownloads { get; set; } = 3;
        public bool PauseOnLowBattery { get; set; } = true;
        public bool PauseOnMeteredConnection { get; set; } = false;
        public int RetryAttempts { get; set; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(1);
        
        public event Action<DownloadJob> OnJobStarted;
        public event Action<DownloadJob> OnJobCompleted;
        public event Action<DownloadJob> OnJobFailed;
        public event Action<DownloadJob, double> OnJobProgress;
        
        public int PendingCount => pendingJobs.Count;
        public int ActiveCount => activeJobs.Count;
        public int CompletedCount => completedJobs.Count;
        
        public BackgroundDownloadQueue()
        {
            pendingJobs = new Queue<DownloadJob>();
            activeJobs = new List<DownloadJob>();
            completedJobs = new List<DownloadJob>();
        }
        
        public class DownloadJob
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string Filename { get; set; }
            public string Username { get; set; }
            public string RemotePath { get; set; }
            public string LocalPath { get; set; }
            public long Size { get; set; }
            public JobStatus Status { get; set; }
            public int RetryCount { get; set; }
            public DateTime CreatedAt { get; set; } = DateTime.Now;
            public DateTime? StartedAt { get; set; }
            public DateTime? CompletedAt { get; set; }
            public double Progress { get; set; }
            public string ErrorMessage { get; set; }
            public Func<DownloadJob, CancellationToken, Task<bool>> DownloadFunc { get; set; }
        }
        
        public enum JobStatus
        {
            Pending,
            Active,
            Completed,
            Failed,
            Cancelled,
            Paused
        }
        
        /// <summary>
        /// Inicia el procesador de cola
        /// </summary>
        public void Start()
        {
            if (isRunning) return;
            
            isRunning = true;
            cts = new CancellationTokenSource();
            workerTask = Task.Run(() => ProcessQueue(cts.Token));
        }
        
        /// <summary>
        /// Detiene el procesador de cola
        /// </summary>
        public async Task Stop()
        {
            if (!isRunning) return;
            
            isRunning = false;
            cts?.Cancel();
            
            if (workerTask != null)
                await workerTask;
        }
        
        /// <summary>
        /// Agrega trabajo a la cola
        /// </summary>
        public void Enqueue(DownloadJob job)
        {
            lock (queueLock)
            {
                pendingJobs.Enqueue(job);
                job.Status = JobStatus.Pending;
            }
        }
        
        /// <summary>
        /// Procesa la cola de descargas
        /// </summary>
        private async Task ProcessQueue(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                DownloadJob job = null;
                
                try
                {
                    lock (queueLock)
                    {
                        if (pendingJobs.Count > 0 && activeJobs.Count < MaxConcurrentDownloads)
                        {
                            job = pendingJobs.Dequeue();
                            activeJobs.Add(job);
                        }
                    }
                    
                    if (job != null)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                                {
                                    cts.CancelAfter(TimeSpan.FromHours(2)); // Timeout de 2 horas por job
                                    await ProcessJob(job, cts.Token).ConfigureAwait(false);
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                job.Status = JobStatus.Failed;
                                job.ErrorMessage = "Timeout: descarga cancelada tras 2 horas";
                            }
                            finally
                            {
                                lock (queueLock)
                                {
                                    activeJobs.Remove(job);
                                }
                            }
                        }, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception)
                {
                    // Continuar procesando
                    await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        
        /// <summary>
        /// Procesa un trabajo individual
        /// </summary>
        private async Task ProcessJob(DownloadJob job, CancellationToken cancellationToken)
        {
            job.Status = JobStatus.Active;
            job.StartedAt = DateTime.Now;
            OnJobStarted?.Invoke(job);
            
            bool success = false;
            
            for (int attempt = 0; attempt <= RetryAttempts; attempt++)
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        job.Status = JobStatus.Cancelled;
                        break;
                    }
                    
                    // Ejecutar descarga
                    success = await job.DownloadFunc(job, cancellationToken);
                    
                    if (success)
                    {
                        job.Status = JobStatus.Completed;
                        job.CompletedAt = DateTime.Now;
                        job.Progress = 100;
                        break;
                    }
                    else if (attempt < RetryAttempts)
                    {
                        job.RetryCount++;
                        await Task.Delay(RetryDelay, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    job.ErrorMessage = ex.Message;
                    
                    if (attempt < RetryAttempts)
                    {
                        job.RetryCount++;
                        await Task.Delay(RetryDelay, cancellationToken);
                    }
                }
            }
            
            // Finalizar trabajo
            lock (queueLock)
            {
                activeJobs.Remove(job);
                completedJobs.Add(job);
            }
            
            if (success)
            {
                OnJobCompleted?.Invoke(job);
            }
            else
            {
                job.Status = JobStatus.Failed;
                OnJobFailed?.Invoke(job);
            }
        }
        
        /// <summary>
        /// Pausa todas las descargas
        /// </summary>
        public void PauseAll()
        {
            lock (queueLock)
            {
                foreach (var job in activeJobs)
                {
                    job.Status = JobStatus.Paused;
                }
            }
        }
        
        /// <summary>
        /// Reanuda todas las descargas
        /// </summary>
        public void ResumeAll()
        {
            lock (queueLock)
            {
                foreach (var job in activeJobs.Where(j => j.Status == JobStatus.Paused))
                {
                    job.Status = JobStatus.Active;
                }
            }
        }
        
        /// <summary>
        /// Cancela un trabajo específico
        /// </summary>
        public void CancelJob(string jobId)
        {
            lock (queueLock)
            {
                var job = activeJobs.FirstOrDefault(j => j.Id == jobId) 
                       ?? pendingJobs.FirstOrDefault(j => j.Id == jobId);
                
                if (job != null)
                {
                    job.Status = JobStatus.Cancelled;
                }
            }
        }
        
        /// <summary>
        /// Limpia trabajos completados
        /// </summary>
        public void ClearCompleted()
        {
            lock (queueLock)
            {
                completedJobs.Clear();
            }
        }
        
        /// <summary>
        /// Obtiene estadísticas
        /// </summary>
        public object GetStats()
        {
            lock (queueLock)
            {
                return new
                {
                    pending = pendingJobs.Count,
                    active = activeJobs.Count,
                    completed = completedJobs.Count(j => j.Status == JobStatus.Completed),
                    failed = completedJobs.Count(j => j.Status == JobStatus.Failed),
                    totalSize = completedJobs.Where(j => j.Status == JobStatus.Completed).Sum(j => j.Size),
                    avgTime = completedJobs
                        .Where(j => j.Status == JobStatus.Completed && j.StartedAt.HasValue && j.CompletedAt.HasValue)
                        .Average(j => (j.CompletedAt.Value - j.StartedAt.Value).TotalSeconds)
                };
            }
        }
    }
}
