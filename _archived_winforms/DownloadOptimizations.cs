using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown
{
    // ⭐ OPTIMIZACIONES DE DESCARGAS INSPIRADAS EN NICOTINE+ ⭐
    
    // 1. TIMEOUT INTELIGENTE (45 SEGUNDOS)
    // Nicotine+ usa 45s porque algunos clientes tardan hasta 30s en iniciar conexión indirecta
    // - Nicotine+ antiguo: 2s
    // - Soulseek NS: ~20s
    // - soulseeX: ~30s
    public class SmartDownloadTimeout
    {
        private const int CONNECTION_TIMEOUT_SECONDS = 45; // Nicotine+ usa 45s
        private readonly ConcurrentDictionary<string, CancellationTokenSource> activeTimeouts = new ConcurrentDictionary<string, CancellationTokenSource>();
        
        public CancellationToken CreateTimeout(string downloadId)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(CONNECTION_TIMEOUT_SECONDS));
            activeTimeouts[downloadId] = cts;
            return cts.Token;
        }
        
        public void CancelTimeout(string downloadId)
        {
            if (activeTimeouts.TryRemove(downloadId, out var cts))
            {
                try
                {
                    cts?.Cancel();
                    cts?.Dispose();
                }
                catch { }
            }
        }
        
        public void ClearAll()
        {
            foreach (var kvp in activeTimeouts)
            {
                try
                {
                    kvp.Value?.Cancel();
                    kvp.Value?.Dispose();
                }
                catch { }
            }
            activeTimeouts.Clear();
        }
    }
    
    // 2. CÁLCULO PRECISO DE VELOCIDAD (PROMEDIO + INSTANTÁNEA)
    // Nicotine+ calcula velocidad promedio desde inicio + velocidad instantánea del fragmento
    public class AccurateSpeedCalculator
    {
        public double CalculateSpeed(long totalBytes, double elapsedSeconds, long fragmentBytes, double fragmentSeconds)
        {
            // Velocidad promedio desde inicio
            double avgSpeed = totalBytes / Math.Max(1, elapsedSeconds);
            
            // Velocidad instantánea del fragmento actual
            double instantSpeed = fragmentBytes / Math.Max(0.1, fragmentSeconds);
            
            // Si velocidad instantánea es muy baja o 0, usar promedio
            if (instantSpeed <= 0 || instantSpeed < avgSpeed * 0.1)
            {
                return avgSpeed;
            }
            
            return instantSpeed;
        }
        
        public TimeSpan CalculateTimeLeft(long totalSize, long currentBytes, double speed)
        {
            if (speed <= 0 || currentBytes >= totalSize)
                return TimeSpan.Zero;
                
            long remainingBytes = totalSize - currentBytes;
            double secondsLeft = remainingBytes / speed;
            
            return TimeSpan.FromSeconds(Math.Max(0, secondsLeft));
        }
        
        public double CalculateAverageSpeed(long totalBytes, double elapsedSeconds)
        {
            return Math.Max(0, totalBytes / Math.Max(1, elapsedSeconds));
        }
    }
    
    // 3. RETRY AUTOMÁTICO CON BACKOFF EXPONENCIAL
    // Nicotine+ reintenta descargas fallidas con delays progresivos
    public class DownloadRetryManager
    {
        private readonly ConcurrentDictionary<string, FailedDownload> failedDownloads = new ConcurrentDictionary<string, FailedDownload>();
        private const int MAX_RETRIES = 3;
        private static readonly TimeSpan[] BACKOFF_DELAYS = 
        {
            TimeSpan.FromMinutes(1),   // 1er reintento: 1 min
            TimeSpan.FromMinutes(5),   // 2do reintento: 5 min
            TimeSpan.FromMinutes(15)   // 3er reintento: 15 min
        };
        
        public void MarkAsFailed(string username, string virtualPath, string reason)
        {
            var key = $"{username}_{virtualPath}";
            var failed = failedDownloads.AddOrUpdate(key, 
                k => new FailedDownload
                {
                    Username = username,
                    VirtualPath = virtualPath,
                    RetryCount = 1,
                    LastAttempt = DateTime.Now,
                    FailReason = reason
                },
                (k, existing) =>
                {
                    existing.RetryCount++;
                    existing.LastAttempt = DateTime.Now;
                    existing.FailReason = reason;
                    return existing;
                });
        }
        
        public List<FailedDownload> GetRetryableDownloads()
        {
            var now = DateTime.Now;
            var retryable = new List<FailedDownload>();
            
            foreach (var kvp in failedDownloads)
            {
                var failed = kvp.Value;
                
                if (failed.RetryCount > MAX_RETRIES)
                    continue;
                    
                var backoffIndex = Math.Min(failed.RetryCount - 1, BACKOFF_DELAYS.Length - 1);
                var backoffDelay = BACKOFF_DELAYS[backoffIndex];
                
                if (now - failed.LastAttempt >= backoffDelay)
                {
                    retryable.Add(failed);
                }
            }
            
            return retryable;
        }
        
        public void ClearFailed(string username, string virtualPath)
        {
            var key = $"{username}_{virtualPath}";
            failedDownloads.TryRemove(key, out _);
        }
        
        public int GetFailedCount()
        {
            return failedDownloads.Count;
        }
        
        public void Clear()
        {
            failedDownloads.Clear();
        }
        
        // Método adicional requerido por MainForm
        public void RecordFailure(string username, string remotePath, string reason)
        {
            MarkAsFailed(username, remotePath, reason);
        }
    }
    
    public class FailedDownload
    {
        public string Username { get; set; }
        public string VirtualPath { get; set; }
        public int RetryCount { get; set; }
        public DateTime LastAttempt { get; set; }
        public string FailReason { get; set; }
    }
    
    // 4. SISTEMA DE COLA CON LÍMITES POR USUARIO
    // Nicotine+ gestiona colas separadas y limita descargas simultáneas por usuario
    public class DownloadQueueManager
    {
        private readonly ConcurrentDictionary<string, List<QueuedDownload>> queuedByUser = new ConcurrentDictionary<string, List<QueuedDownload>>();
        private readonly ConcurrentDictionary<string, List<QueuedDownload>> activeByUser = new ConcurrentDictionary<string, List<QueuedDownload>>();
        private readonly ConcurrentDictionary<string, long> userQueueSizes = new ConcurrentDictionary<string, long>();
        private readonly object lockObj = new object();
        
        private const long MAX_QUEUE_SIZE_PER_USER = 500L * 1024 * 1024; // 500 MB
        private const int MAX_CONCURRENT_PER_USER = 2;
        
        public bool CanEnqueueForUser(string username, long fileSize)
        {
            var currentSize = userQueueSizes.GetOrAdd(username, 0);
            var activeCount = activeByUser.GetOrAdd(username, new List<QueuedDownload>()).Count;
            
            return currentSize + fileSize <= MAX_QUEUE_SIZE_PER_USER 
                && activeCount < MAX_CONCURRENT_PER_USER;
        }
        
        public void EnqueueDownload(string username, string virtualPath, long fileSize)
        {
            lock (lockObj)
            {
                var download = new QueuedDownload
                {
                    Username = username,
                    VirtualPath = virtualPath,
                    FileSize = fileSize,
                    EnqueuedTime = DateTime.Now
                };
                
                queuedByUser.GetOrAdd(username, new List<QueuedDownload>()).Add(download);
                userQueueSizes.AddOrUpdate(username, fileSize, (k, v) => v + fileSize);
            }
        }
        
        public void MoveToActive(string username, string virtualPath)
        {
            lock (lockObj)
            {
                var queued = queuedByUser.GetOrAdd(username, new List<QueuedDownload>());
                var download = queued.FirstOrDefault(d => d.VirtualPath == virtualPath);
                
                if (download != null)
                {
                    queued.Remove(download);
                    activeByUser.GetOrAdd(username, new List<QueuedDownload>()).Add(download);
                }
            }
        }
        
        public void RemoveFromActive(string username, string virtualPath)
        {
            lock (lockObj)
            {
                var active = activeByUser.GetOrAdd(username, new List<QueuedDownload>());
                var download = active.FirstOrDefault(d => d.VirtualPath == virtualPath);
                
                if (download != null)
                {
                    active.Remove(download);
                    userQueueSizes.AddOrUpdate(username, 0, (k, v) => Math.Max(0, v - download.FileSize));
                }
            }
        }
        
        public int GetQueuedCount(string username)
        {
            return queuedByUser.GetOrAdd(username, new List<QueuedDownload>()).Count;
        }
        
        public int GetActiveCount(string username)
        {
            return activeByUser.GetOrAdd(username, new List<QueuedDownload>()).Count;
        }
        
        public long GetQueueSize(string username)
        {
            return userQueueSizes.GetOrAdd(username, 0);
        }
    }
    
    public class QueuedDownload
    {
        public string Username { get; set; }
        public string VirtualPath { get; set; }
        public long FileSize { get; set; }
        public DateTime EnqueuedTime { get; set; }
    }
    
    // 5. PERSISTENCIA AUTOMÁTICA CADA 3 MINUTOS
    // Nicotine+ guarda estado de descargas con backup automático
    public class DownloadPersistence
    {
        private const int SAVE_INTERVAL_SECONDS = 180; // 3 minutos
        private System.Threading.Timer saveTimer;
        private readonly string downloadsFilePath;
        private readonly Func<List<PersistedDownload>> getDownloadsFunc;
        private bool isEnabled = false;
        
        public DownloadPersistence(string filePath, Func<List<PersistedDownload>> getDownloads)
        {
            downloadsFilePath = filePath;
            getDownloadsFunc = getDownloads;
        }
        
        public void StartAutoSave()
        {
            if (isEnabled)
                return;
                
            isEnabled = true;
            saveTimer = new System.Threading.Timer(SaveDownloads, null, 
                TimeSpan.FromSeconds(SAVE_INTERVAL_SECONDS), 
                TimeSpan.FromSeconds(SAVE_INTERVAL_SECONDS));
        }
        
        public void StopAutoSave()
        {
            isEnabled = false;
            saveTimer?.Dispose();
            saveTimer = null;
        }
        
        private void SaveDownloads(object state)
        {
            if (!isEnabled)
                return;
                
            try
            {
                var downloads = getDownloadsFunc?.Invoke();
                if (downloads == null || downloads.Count == 0)
                    return;
                
                var json = JsonSerializer.Serialize(downloads, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                // Crear backup del archivo anterior
                if (File.Exists(downloadsFilePath))
                {
                    var backupPath = $"{downloadsFilePath}.backup";
                    File.Copy(downloadsFilePath, backupPath, overwrite: true);
                }
                
                File.WriteAllText(downloadsFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving downloads: {ex.Message}");
            }
        }
        
        public void SaveNow()
        {
            SaveDownloads(null);
        }
        
        public List<PersistedDownload> LoadDownloads()
        {
            if (!File.Exists(downloadsFilePath))
                return new List<PersistedDownload>();
                
            try
            {
                var json = File.ReadAllText(downloadsFilePath);
                return JsonSerializer.Deserialize<List<PersistedDownload>>(json) ?? new List<PersistedDownload>();
            }
            catch
            {
                // Intentar cargar backup
                var backupPath = $"{downloadsFilePath}.backup";
                if (File.Exists(backupPath))
                {
                    try
                    {
                        var json = File.ReadAllText(backupPath);
                        return JsonSerializer.Deserialize<List<PersistedDownload>>(json) ?? new List<PersistedDownload>();
                    }
                    catch { }
                }
                return new List<PersistedDownload>();
            }
        }
    }
    
    public class PersistedDownload
    {
        public string Username { get; set; }
        public string VirtualPath { get; set; }
        public string LocalPath { get; set; }
        public string Status { get; set; }
        public long Size { get; set; }
        public long CurrentOffset { get; set; }
        public DateTime? StartTime { get; set; }
    }
    
    // 6. AUTO-CLEAR DE DESCARGAS COMPLETADAS
    // Nicotine+ limpia automáticamente descargas completadas para liberar memoria
    public class AutoClearManager
    {
        public bool AutoClearEnabled { get; set; } = false;
        public int MaxCompletedDownloads { get; set; } = 100;
        private DateTime lastClearCheck = DateTime.Now;
        private readonly TimeSpan checkInterval = TimeSpan.FromMinutes(5);
        
        public List<T> CheckAndClearCompleted<T>(List<T> downloads, Func<T, bool> isCompleted, Func<T, DateTime?> getCompletedTime) where T : class
        {
            if (!AutoClearEnabled)
                return new List<T>();
                
            // Solo verificar cada 5 minutos
            if (DateTime.Now - lastClearCheck < checkInterval)
                return new List<T>();
                
            lastClearCheck = DateTime.Now;
            
            var completed = downloads.Where(isCompleted).ToList();
            
            if (completed.Count <= MaxCompletedDownloads)
                return new List<T>();
            
            var toRemove = completed
                .OrderBy(d => getCompletedTime(d) ?? DateTime.MinValue)
                .Take(completed.Count - MaxCompletedDownloads)
                .ToList();
            
            return toRemove;
        }
    }
    
    // 7. CONSTANTES DE NICOTINE+ PARA DESCARGAS
    public static class NicotineDownloadConstants
    {
        public const int CONNECTION_TIMEOUT_SECONDS = 45;
        public const int MAX_RETRIES = 3;
        public const int SAVE_INTERVAL_SECONDS = 180;
        public const long MAX_QUEUE_SIZE_PER_USER = 500L * 1024 * 1024; // 500 MB
        public const int MAX_CONCURRENT_PER_USER = 2;
        public const int MAX_COMPLETED_DOWNLOADS = 100;
        
        public static readonly TimeSpan[] RETRY_BACKOFF_DELAYS = 
        {
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(15)
        };
    }
}
