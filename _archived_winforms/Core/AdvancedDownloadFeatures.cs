using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using SlskDown.Models;

namespace SlskDown.Core
{
    /// <summary>
    /// MEJORA #8: Auto-Retry Inteligente con Backoff Exponencial
    /// </summary>
    public class IntelligentRetryManager
    {
        private readonly Dictionary<string, RetryState> retryStates = new Dictionary<string, RetryState>();
        private readonly object lockObj = new object();
        private static readonly int[] BackoffSeconds = { 30, 60, 300, 900 }; // 30s, 1min, 5min, 15min

        public DateTime? GetNextRetryTime(string fileKey, int attemptNumber)
        {
            lock (lockObj)
            {
                if (!retryStates.ContainsKey(fileKey))
                {
                    retryStates[fileKey] = new RetryState();
                }

                var state = retryStates[fileKey];
                var backoffIndex = Math.Min(attemptNumber - 1, BackoffSeconds.Length - 1);
                var delaySeconds = BackoffSeconds[backoffIndex];

                // Añadir jitter aleatorio (±20%)
                var jitter = new Random().Next(-20, 21) / 100.0;
                delaySeconds = (int)(delaySeconds * (1 + jitter));

                // Evitar horas pico (12:00-14:00 y 19:00-22:00)
                var nextRetry = DateTime.Now.AddSeconds(delaySeconds);
                if (IsPeakHour(nextRetry))
                {
                    nextRetry = nextRetry.AddHours(2);
                }

                state.NextRetryTime = nextRetry;
                state.AttemptCount = attemptNumber;
                return nextRetry;
            }
        }

        public bool CanRetryNow(string fileKey)
        {
            lock (lockObj)
            {
                if (!retryStates.ContainsKey(fileKey))
                    return true;

                var state = retryStates[fileKey];
                return state.NextRetryTime == null || DateTime.Now >= state.NextRetryTime;
            }
        }

        private bool IsPeakHour(DateTime time)
        {
            var hour = time.Hour;
            return (hour >= 12 && hour < 14) || (hour >= 19 && hour < 22);
        }

        public void ResetRetry(string fileKey)
        {
            lock (lockObj)
            {
                retryStates.Remove(fileKey);
            }
        }

        private class RetryState
        {
            public DateTime? NextRetryTime { get; set; }
            public int AttemptCount { get; set; }
        }
    }

    /// <summary>
    /// MEJORA #9: Sistema de Caché de Metadatos
    /// </summary>
    public class MetadataCache
    {
        private readonly ConcurrentDictionary<string, CachedMetadata> cache = new ConcurrentDictionary<string, CachedMetadata>();
        private readonly TimeSpan cacheExpiration = TimeSpan.FromHours(24);
        private readonly string cacheFile;

        public MetadataCache(string dataDirectory)
        {
            cacheFile = Path.Combine(dataDirectory, "metadata_cache.json");
            LoadCache();
        }

        public void CacheSearchResult(string fileName, List<string> providers, long fileSize)
        {
            var key = GetCacheKey(fileName);
            cache[key] = new CachedMetadata
            {
                FileName = fileName,
                Providers = providers,
                FileSize = fileSize,
                CachedAt = DateTime.UtcNow
            };
        }

        public CachedMetadata GetCachedMetadata(string fileName)
        {
            var key = GetCacheKey(fileName);
            if (cache.TryGetValue(key, out var metadata))
            {
                if (DateTime.UtcNow - metadata.CachedAt < cacheExpiration)
                {
                    return metadata;
                }
                else
                {
                    cache.TryRemove(key, out _);
                }
            }
            return null;
        }

        public void SaveCache()
        {
            try
            {
                var validEntries = cache.Values
                    .Where(m => DateTime.UtcNow - m.CachedAt < cacheExpiration)
                    .ToList();

                var json = JsonSerializer.Serialize(validEntries, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(cacheFile, json);
            }
            catch { }
        }

        private void LoadCache()
        {
            try
            {
                if (File.Exists(cacheFile))
                {
                    var json = File.ReadAllText(cacheFile);
                    var entries = JsonSerializer.Deserialize<List<CachedMetadata>>(json);
                    
                    foreach (var entry in entries)
                    {
                        if (DateTime.UtcNow - entry.CachedAt < cacheExpiration)
                        {
                            var key = GetCacheKey(entry.FileName);
                            cache[key] = entry;
                        }
                    }
                }
            }
            catch { }
        }

        private string GetCacheKey(string fileName)
        {
            return fileName.ToLowerInvariant().Trim();
        }

        public int GetCacheSize() => cache.Count;

        public void ClearExpiredEntries()
        {
            var expiredKeys = cache
                .Where(kvp => DateTime.UtcNow - kvp.Value.CachedAt >= cacheExpiration)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                cache.TryRemove(key, out _);
            }
        }
    }

    public class CachedMetadata
    {
        public string FileName { get; set; }
        public List<string> Providers { get; set; }
        public long FileSize { get; set; }
        public DateTime CachedAt { get; set; }
    }

    /// <summary>
    /// MEJORA #10: Modo "Descarga Agresiva"
    /// </summary>
    public class AggressiveDownloadMode
    {
        private bool isEnabled = false;
        private readonly int normalMaxDownloads;
        private readonly int aggressiveMaxDownloads;

        public AggressiveDownloadMode(int normalMax, int aggressiveMax)
        {
            normalMaxDownloads = normalMax;
            aggressiveMaxDownloads = aggressiveMax;
        }

        public bool IsEnabled => isEnabled;

        public void Enable()
        {
            isEnabled = true;
        }

        public void Disable()
        {
            isEnabled = false;
        }

        public int GetMaxDownloads()
        {
            return isEnabled ? aggressiveMaxDownloads : normalMaxDownloads;
        }

        public bool ShouldEnableForFile(string fileName, int availableProviders)
        {
            // Activar modo agresivo si hay pocos proveedores (< 3)
            return availableProviders < 3;
        }
    }

    /// <summary>
    /// MEJORA #11: Compresión de Cola en Memoria
    /// </summary>
    public class QueueCompressor
    {
        public static byte[] CompressQueue(List<DownloadTask> queue)
        {
            try
            {
                var json = JsonSerializer.Serialize(queue);
                var bytes = Encoding.UTF8.GetBytes(json);

                using (var outputStream = new MemoryStream())
                {
                    using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal))
                    {
                        gzipStream.Write(bytes, 0, bytes.Length);
                    }
                    return outputStream.ToArray();
                }
            }
            catch
            {
                return null;
            }
        }

        public static List<DownloadTask> DecompressQueue(byte[] compressedData)
        {
            try
            {
                using (var inputStream = new MemoryStream(compressedData))
                using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
                using (var outputStream = new MemoryStream())
                {
                    gzipStream.CopyTo(outputStream);
                    var bytes = outputStream.ToArray();
                    var json = Encoding.UTF8.GetString(bytes);
                    return JsonSerializer.Deserialize<List<DownloadTask>>(json);
                }
            }
            catch
            {
                return new List<DownloadTask>();
            }
        }

        public static long GetCompressionRatio(List<DownloadTask> queue)
        {
            var json = JsonSerializer.Serialize(queue);
            var originalSize = Encoding.UTF8.GetByteCount(json);
            var compressed = CompressQueue(queue);
            return compressed != null ? (long)((1.0 - (double)compressed.Length / originalSize) * 100) : 0;
        }
    }

    /// <summary>
    /// MEJORA #12: Índice de Búsqueda Rápida
    /// </summary>
    public class FastSearchIndex
    {
        private readonly Dictionary<string, List<DownloadTask>> fileNameIndex = new Dictionary<string, List<DownloadTask>>();
        private readonly Dictionary<string, List<DownloadTask>> providerIndex = new Dictionary<string, List<DownloadTask>>();
        private readonly object lockObj = new object();

        public void BuildIndex(List<DownloadTask> queue)
        {
            lock (lockObj)
            {
                fileNameIndex.Clear();
                providerIndex.Clear();

                foreach (var task in queue)
                {
                    // Índice por nombre de archivo
                    var fileKey = task.File.FileName.ToLowerInvariant();
                    if (!fileNameIndex.ContainsKey(fileKey))
                        fileNameIndex[fileKey] = new List<DownloadTask>();
                    fileNameIndex[fileKey].Add(task);

                    // Índice por proveedor
                    var providerKey = task.File.Username.ToLowerInvariant();
                    if (!providerIndex.ContainsKey(providerKey))
                        providerIndex[providerKey] = new List<DownloadTask>();
                    providerIndex[providerKey].Add(task);
                }
            }
        }

        public List<DownloadTask> SearchByFileName(string fileName)
        {
            lock (lockObj)
            {
                var key = fileName.ToLowerInvariant();
                return fileNameIndex.ContainsKey(key) ? new List<DownloadTask>(fileNameIndex[key]) : new List<DownloadTask>();
            }
        }

        public List<DownloadTask> SearchByProvider(string provider)
        {
            lock (lockObj)
            {
                var key = provider.ToLowerInvariant();
                return providerIndex.ContainsKey(key) ? new List<DownloadTask>(providerIndex[key]) : new List<DownloadTask>();
            }
        }

        public List<DownloadTask> SearchByPartialName(string partialName)
        {
            lock (lockObj)
            {
                var searchTerm = partialName.ToLowerInvariant();
                return fileNameIndex
                    .Where(kvp => kvp.Key.Contains(searchTerm))
                    .SelectMany(kvp => kvp.Value)
                    .Distinct()
                    .ToList();
            }
        }

        public void UpdateTask(DownloadTask task)
        {
            lock (lockObj)
            {
                // Reconstruir índices para esta tarea
                var fileKey = task.File.FileName.ToLowerInvariant();
                var providerKey = task.File.Username.ToLowerInvariant();

                if (fileNameIndex.ContainsKey(fileKey))
                {
                    fileNameIndex[fileKey].RemoveAll(t => t == task);
                    fileNameIndex[fileKey].Add(task);
                }

                if (providerIndex.ContainsKey(providerKey))
                {
                    providerIndex[providerKey].RemoveAll(t => t == task);
                    providerIndex[providerKey].Add(task);
                }
            }
        }
    }

    /// <summary>
    /// MEJORA #13: Prefetch de Proveedores
    /// </summary>
    public class ProviderPrefetchManager
    {
        private readonly HashSet<string> prefetchedProviders = new HashSet<string>();
        private readonly Dictionary<string, DateTime> lastPrefetchTime = new Dictionary<string, DateTime>();
        private readonly object lockObj = new object();
        private readonly TimeSpan prefetchCooldown = TimeSpan.FromMinutes(5);

        public bool ShouldPrefetch(string provider)
        {
            lock (lockObj)
            {
                if (prefetchedProviders.Contains(provider))
                {
                    if (lastPrefetchTime.TryGetValue(provider, out var lastTime))
                    {
                        return DateTime.UtcNow - lastTime > prefetchCooldown;
                    }
                }
                return true;
            }
        }

        public void MarkPrefetched(string provider)
        {
            lock (lockObj)
            {
                prefetchedProviders.Add(provider);
                lastPrefetchTime[provider] = DateTime.UtcNow;
            }
        }

        public void ClearPrefetch(string provider)
        {
            lock (lockObj)
            {
                prefetchedProviders.Remove(provider);
                lastPrefetchTime.Remove(provider);
            }
        }

        public List<string> GetTopProviders(List<DownloadTask> queue, int count = 5)
        {
            return queue
                .GroupBy(t => t.File.Username)
                .OrderByDescending(g => g.Count())
                .Take(count)
                .Select(g => g.Key)
                .ToList();
        }
    }

    /// <summary>
    /// MEJORA #14: Dashboard de Estadísticas en Tiempo Real
    /// </summary>
    public class RealtimeDashboard
    {
        private readonly Queue<SpeedSample> speedHistory = new Queue<SpeedSample>();
        private readonly int maxSamples = 60; // 60 segundos de historial
        private long totalBytesDownloadedToday = 0;
        private int filesCompletedToday = 0;
        private DateTime lastResetDate = DateTime.Today;
        private readonly object lockObj = new object();

        public void RecordSpeed(double speedKBps)
        {
            lock (lockObj)
            {
                speedHistory.Enqueue(new SpeedSample
                {
                    SpeedKBps = speedKBps,
                    Timestamp = DateTime.Now
                });

                while (speedHistory.Count > maxSamples)
                {
                    speedHistory.Dequeue();
                }
            }
        }

        public void RecordDownload(long bytes)
        {
            lock (lockObj)
            {
                CheckDailyReset();
                totalBytesDownloadedToday += bytes;
                filesCompletedToday++;
            }
        }

        public DashboardStats GetStats(List<DownloadTask> queue)
        {
            lock (lockObj)
            {
                CheckDailyReset();

                var currentSpeed = speedHistory.Any() ? speedHistory.Last().SpeedKBps : 0;
                var avgSpeed = speedHistory.Any() ? speedHistory.Average(s => s.SpeedKBps) : 0;
                var peakSpeed = speedHistory.Any() ? speedHistory.Max(s => s.SpeedKBps) : 0;

                var activeDownloads = queue.Count(t => t.Status == DownloadStatus.Downloading);
                var queuedDownloads = queue.Count(t => t.Status == DownloadStatus.Queued);
                var totalPending = activeDownloads + queuedDownloads;

                var totalBytesRemaining = queue
                    .Where(t => t.Status == DownloadStatus.Queued || t.Status == DownloadStatus.Downloading)
                    .Sum(t => t.File.SizeBytes - t.BytesDownloaded);

                var eta = avgSpeed > 0 ? TimeSpan.FromSeconds(totalBytesRemaining / (avgSpeed * 1024)) : TimeSpan.Zero;

                var topProviders = queue
                    .Where(t => t.Status == DownloadStatus.Downloading)
                    .GroupBy(t => t.File.Username)
                    .Select(g => new ProviderSpeed
                    {
                        Provider = g.Key,
                        Speed = g.Sum(t => t.SpeedMBps * 1024) // Convertir MB/s a KB/s
                    })
                    .OrderByDescending(p => p.Speed)
                    .Take(5)
                    .ToList();

                return new DashboardStats
                {
                    CurrentSpeedKBps = currentSpeed,
                    AverageSpeedKBps = avgSpeed,
                    PeakSpeedKBps = peakSpeed,
                    ActiveDownloads = activeDownloads,
                    QueuedDownloads = queuedDownloads,
                    FilesCompletedToday = filesCompletedToday,
                    BytesDownloadedToday = totalBytesDownloadedToday,
                    EstimatedTimeRemaining = eta,
                    TopProviders = topProviders,
                    SpeedHistory = speedHistory.ToList()
                };
            }
        }

        private void CheckDailyReset()
        {
            if (DateTime.Today > lastResetDate)
            {
                totalBytesDownloadedToday = 0;
                filesCompletedToday = 0;
                lastResetDate = DateTime.Today;
            }
        }
    }

    public class SpeedSample
    {
        public double SpeedKBps { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DashboardStats
    {
        public double CurrentSpeedKBps { get; set; }
        public double AverageSpeedKBps { get; set; }
        public double PeakSpeedKBps { get; set; }
        public int ActiveDownloads { get; set; }
        public int QueuedDownloads { get; set; }
        public int FilesCompletedToday { get; set; }
        public long BytesDownloadedToday { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
        public List<ProviderSpeed> TopProviders { get; set; }
        public List<SpeedSample> SpeedHistory { get; set; }
    }

    public class ProviderSpeed
    {
        public string Provider { get; set; }
        public double Speed { get; set; }
    }

    /// <summary>
    /// MEJORA #16: Exportar/Importar Cola
    /// </summary>
    public class QueueExportImport
    {
        public static void ExportToJson(List<DownloadTask> queue, string filePath)
        {
            var exportData = queue.Select(t => new ExportedTask
            {
                FileName = t.File.FileName,
                Username = t.File.Username,
                SizeBytes = t.File.SizeBytes,
                Author = t.File.Author,
                Status = t.Status.ToString()
            }).ToList();

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        public static void ExportToCsv(List<DownloadTask> queue, string filePath)
        {
            var csv = new StringBuilder();
            csv.AppendLine("FileName,Username,SizeBytes,Author,Status");

            foreach (var task in queue)
            {
                csv.AppendLine($"\"{task.File.FileName}\",\"{task.File.Username}\",{task.File.SizeBytes},\"{task.File.Author}\",{task.Status}");
            }

            File.WriteAllText(filePath, csv.ToString());
        }

        public static List<ExportedTask> ImportFromJson(string filePath)
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<ExportedTask>>(json);
        }

        public static List<ExportedTask> ImportFromCsv(string filePath)
        {
            var tasks = new List<ExportedTask>();
            var lines = File.ReadAllLines(filePath);

            for (int i = 1; i < lines.Length; i++) // Skip header
            {
                var parts = lines[i].Split(',');
                if (parts.Length >= 5)
                {
                    tasks.Add(new ExportedTask
                    {
                        FileName = parts[0].Trim('"'),
                        Username = parts[1].Trim('"'),
                        SizeBytes = long.Parse(parts[2]),
                        Author = parts[3].Trim('"'),
                        Status = parts[4]
                    });
                }
            }

            return tasks;
        }
    }

    public class ExportedTask
    {
        public string FileName { get; set; }
        public string Username { get; set; }
        public long SizeBytes { get; set; }
        public string Author { get; set; }
        public string Status { get; set; }
    }
}
