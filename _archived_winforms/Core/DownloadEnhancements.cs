using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using System.Media;
using SlskDown.Models;

namespace SlskDown.Core
{
    /// <summary>
    /// MEJORA #1: Gestor de persistencia de progreso de descargas
    /// </summary>
    public class DownloadProgressPersistence
    {
        private readonly string progressDir;
        private readonly object lockObj = new object();

        public DownloadProgressPersistence(string dataDirectory)
        {
            progressDir = Path.Combine(dataDirectory, "download_progress");
            Directory.CreateDirectory(progressDir);
        }

        public void SaveProgress(string fileName, long bytesDownloaded, long totalBytes)
        {
            try
            {
                var progressFile = GetProgressFilePath(fileName);
                var progress = new DownloadProgressInfo
                {
                    FileName = fileName,
                    BytesDownloaded = bytesDownloaded,
                    TotalBytes = totalBytes,
                    LastUpdate = DateTime.UtcNow
                };

                lock (lockObj)
                {
                    var json = JsonSerializer.Serialize(progress);
                    File.WriteAllText(progressFile, json);
                }
            }
            catch { }
        }

        public DownloadProgressInfo LoadProgress(string fileName)
        {
            try
            {
                var progressFile = GetProgressFilePath(fileName);
                if (!File.Exists(progressFile))
                    return null;

                lock (lockObj)
                {
                    var json = File.ReadAllText(progressFile);
                    return JsonSerializer.Deserialize<DownloadProgressInfo>(json);
                }
            }
            catch
            {
                return null;
            }
        }

        public void DeleteProgress(string fileName)
        {
            try
            {
                var progressFile = GetProgressFilePath(fileName);
                lock (lockObj)
                {
                    if (File.Exists(progressFile))
                        File.Delete(progressFile);
                }
            }
            catch { }
        }

        private string GetProgressFilePath(string fileName)
        {
            var safeFileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(progressDir, $"{safeFileName}.progress");
        }
    }

    public class DownloadProgressInfo
    {
        public string FileName { get; set; }
        public long BytesDownloaded { get; set; }
        public long TotalBytes { get; set; }
        public DateTime LastUpdate { get; set; }
    }

    /// <summary>
    /// MEJORA #2: Gestor de errores con límite de reintentos por archivo
    /// </summary>
    public class DownloadErrorManager
    {
        private readonly Dictionary<string, int> fileFailureCount = new Dictionary<string, int>();
        private readonly Dictionary<string, List<string>> fileFailureReasons = new Dictionary<string, List<string>>();
        private readonly object lockObj = new object();
        private readonly int maxFailuresPerFile;

        public DownloadErrorManager(int maxFailuresPerFile = 5)
        {
            this.maxFailuresPerFile = maxFailuresPerFile;
        }

        public bool ShouldRetry(string fileKey)
        {
            lock (lockObj)
            {
                if (!fileFailureCount.ContainsKey(fileKey))
                    return true;

                return fileFailureCount[fileKey] < maxFailuresPerFile;
            }
        }

        public void RecordFailure(string fileKey, string reason)
        {
            lock (lockObj)
            {
                if (!fileFailureCount.ContainsKey(fileKey))
                {
                    fileFailureCount[fileKey] = 0;
                    fileFailureReasons[fileKey] = new List<string>();
                }

                fileFailureCount[fileKey]++;
                fileFailureReasons[fileKey].Add($"{DateTime.Now:HH:mm:ss} - {reason}");
            }
        }

        public int GetFailureCount(string fileKey)
        {
            lock (lockObj)
            {
                return fileFailureCount.ContainsKey(fileKey) ? fileFailureCount[fileKey] : 0;
            }
        }

        public List<string> GetFailureReasons(string fileKey)
        {
            lock (lockObj)
            {
                return fileFailureReasons.ContainsKey(fileKey) 
                    ? new List<string>(fileFailureReasons[fileKey]) 
                    : new List<string>();
            }
        }

        public void ResetFailures(string fileKey)
        {
            lock (lockObj)
            {
                fileFailureCount.Remove(fileKey);
                fileFailureReasons.Remove(fileKey);
            }
        }

        public List<string> GetProblematicFiles()
        {
            lock (lockObj)
            {
                return fileFailureCount
                    .Where(kvp => kvp.Value >= maxFailuresPerFile)
                    .Select(kvp => kvp.Key)
                    .ToList();
            }
        }
    }

    /// <summary>
    /// MEJORA #3: Sistema de notificaciones
    /// </summary>
    public class DownloadNotificationManager
    {
        private readonly bool enableNotifications;
        private readonly bool enableSound;

        public DownloadNotificationManager(bool enableNotifications = true, bool enableSound = false)
        {
            this.enableNotifications = enableNotifications;
            this.enableSound = enableSound;
        }

        public void NotifyDownloadComplete(string fileName, long bytes)
        {
            if (!enableNotifications)
                return;

            try
            {
                var sizeStr = FormatBytes(bytes);
                ShowNotification("Descarga completada", $"{fileName}\n{sizeStr}");

                if (enableSound)
                    SystemSounds.Asterisk.Play();
            }
            catch { }
        }

        public void NotifyQueueComplete(int totalFiles, long totalBytes)
        {
            if (!enableNotifications)
                return;

            try
            {
                var sizeStr = FormatBytes(totalBytes);
                ShowNotification("Cola completada", $"{totalFiles} archivos descargados\n{sizeStr} total");

                if (enableSound)
                    SystemSounds.Exclamation.Play();
            }
            catch { }
        }

        public void NotifyDownloadError(string fileName, string error)
        {
            if (!enableNotifications)
                return;

            try
            {
                ShowNotification("Error de descarga", $"{fileName}\n{error}");
            }
            catch { }
        }

        private void ShowNotification(string title, string message)
        {
            var notifyIcon = new NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Information,
                Visible = true,
                BalloonTipTitle = title,
                BalloonTipText = message
            };

            notifyIcon.ShowBalloonTip(3000);

            Task.Delay(4000).ContinueWith(_ =>
            {
                try
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                }
                catch { }
            });
        }

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

    /// <summary>
    /// MEJORA #4: Limitador de velocidad de descarga
    /// </summary>
    public class DownloadSpeedLimiter
    {
        private readonly int maxSpeedKBps;
        private readonly bool enableScheduled;
        private readonly int nightStart;
        private readonly int nightEnd;
        private DateTime lastCheck = DateTime.MinValue;
        private long bytesThisSecond = 0;
        private readonly object lockObj = new object();

        public DownloadSpeedLimiter(int maxSpeedKBps = 0, bool enableScheduled = false, int nightStart = 22, int nightEnd = 8)
        {
            this.maxSpeedKBps = maxSpeedKBps;
            this.enableScheduled = enableScheduled;
            this.nightStart = nightStart;
            this.nightEnd = nightEnd;
        }

        public async Task ThrottleIfNeeded(int bytesRead)
        {
            if (maxSpeedKBps <= 0)
                return;

            var currentLimit = GetCurrentSpeedLimit();
            if (currentLimit <= 0)
                return;

            lock (lockObj)
            {
                var now = DateTime.UtcNow;
                if ((now - lastCheck).TotalSeconds >= 1)
                {
                    lastCheck = now;
                    bytesThisSecond = 0;
                }

                bytesThisSecond += bytesRead;
            }

            var maxBytesPerSecond = currentLimit * 1024;
            if (bytesThisSecond > maxBytesPerSecond)
            {
                var delayMs = (int)((bytesThisSecond - maxBytesPerSecond) * 1000.0 / maxBytesPerSecond);
                if (delayMs > 0 && delayMs < 5000)
                    await Task.Delay(delayMs);
            }
        }

        private int GetCurrentSpeedLimit()
        {
            if (!enableScheduled)
                return maxSpeedKBps;

            var hour = DateTime.Now.Hour;
            var isNightTime = (nightStart < nightEnd)
                ? (hour >= nightStart && hour < nightEnd)
                : (hour >= nightStart || hour < nightEnd);

            return isNightTime ? 0 : maxSpeedKBps;
        }
    }

    /// <summary>
    /// MEJORA #5: Búsqueda automática de fuentes alternativas
    /// </summary>
    public class AlternativeSourceFinder
    {
        private readonly int minSpeedKBps;
        private readonly int searchDelaySeconds;
        private readonly Dictionary<string, DateTime> lastSearchTime = new Dictionary<string, DateTime>();
        private readonly object lockObj = new object();

        public AlternativeSourceFinder(int minSpeedKBps = 50, int searchDelaySeconds = 30)
        {
            this.minSpeedKBps = minSpeedKBps;
            this.searchDelaySeconds = searchDelaySeconds;
        }

        public bool ShouldSearchAlternative(string fileKey, double currentSpeedKBps)
        {
            if (currentSpeedKBps >= minSpeedKBps)
                return false;

            lock (lockObj)
            {
                if (!lastSearchTime.ContainsKey(fileKey))
                {
                    lastSearchTime[fileKey] = DateTime.UtcNow;
                    return true;
                }

                var elapsed = (DateTime.UtcNow - lastSearchTime[fileKey]).TotalSeconds;
                if (elapsed >= searchDelaySeconds)
                {
                    lastSearchTime[fileKey] = DateTime.UtcNow;
                    return true;
                }

                return false;
            }
        }

        public void ResetSearchTime(string fileKey)
        {
            lock (lockObj)
            {
                lastSearchTime.Remove(fileKey);
            }
        }
    }

    /// <summary>
    /// MEJORA #6: Estadísticas detalladas de descargas
    /// </summary>
    public class DownloadStatisticsTracker
    {
        private readonly string statsFile;
        private readonly int historyDays;
        private readonly object lockObj = new object();
        private DownloadStatistics stats;

        public DownloadStatisticsTracker(string dataDirectory, int historyDays = 30)
        {
            this.historyDays = historyDays;
            statsFile = Path.Combine(dataDirectory, "download_stats.json");
            LoadStats();
        }

        public void RecordDownload(string fileName, long bytes, TimeSpan duration, double avgSpeedKBps)
        {
            lock (lockObj)
            {
                var entry = new DownloadStatEntry
                {
                    FileName = fileName,
                    Bytes = bytes,
                    Duration = duration,
                    AvgSpeedKBps = avgSpeedKBps,
                    Timestamp = DateTime.UtcNow
                };

                stats.Entries.Add(entry);
                CleanupOldEntries();
                SaveStats();
            }
        }

        public DownloadStatsSummary GetSummary(int days = 7)
        {
            lock (lockObj)
            {
                var cutoff = DateTime.UtcNow.AddDays(-days);
                var recent = stats.Entries.Where(e => e.Timestamp >= cutoff).ToList();

                return new DownloadStatsSummary
                {
                    TotalFiles = recent.Count,
                    TotalBytes = recent.Sum(e => e.Bytes),
                    AvgSpeedKBps = recent.Any() ? recent.Average(e => e.AvgSpeedKBps) : 0,
                    TotalDuration = TimeSpan.FromSeconds(recent.Sum(e => e.Duration.TotalSeconds)),
                    DailyBreakdown = recent.GroupBy(e => e.Timestamp.Date)
                        .Select(g => new DailyStats
                        {
                            Date = g.Key,
                            Files = g.Count(),
                            Bytes = g.Sum(e => e.Bytes)
                        }).ToList()
                };
            }
        }

        private void LoadStats()
        {
            try
            {
                if (File.Exists(statsFile))
                {
                    var json = File.ReadAllText(statsFile);
                    stats = JsonSerializer.Deserialize<DownloadStatistics>(json) ?? new DownloadStatistics();
                }
                else
                {
                    stats = new DownloadStatistics();
                }
            }
            catch
            {
                stats = new DownloadStatistics();
            }
        }

        private void SaveStats()
        {
            try
            {
                var json = JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(statsFile, json);
            }
            catch { }
        }

        private void CleanupOldEntries()
        {
            var cutoff = DateTime.UtcNow.AddDays(-historyDays);
            stats.Entries.RemoveAll(e => e.Timestamp < cutoff);
        }
    }

    public class DownloadStatistics
    {
        public List<DownloadStatEntry> Entries { get; set; } = new List<DownloadStatEntry>();
    }

    public class DownloadStatEntry
    {
        public string FileName { get; set; }
        public long Bytes { get; set; }
        public TimeSpan Duration { get; set; }
        public double AvgSpeedKBps { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DownloadStatsSummary
    {
        public int TotalFiles { get; set; }
        public long TotalBytes { get; set; }
        public double AvgSpeedKBps { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public List<DailyStats> DailyBreakdown { get; set; }
    }

    public class DailyStats
    {
        public DateTime Date { get; set; }
        public int Files { get; set; }
        public long Bytes { get; set; }
    }

    /// <summary>
    /// MEJORA #7: Filtros y ordenamiento de cola
    /// </summary>
    public class DownloadQueueFilter
    {
        public enum SortBy
        {
            Name,
            Size,
            Status,
            DateAdded,
            Priority
        }

        public enum FilterBy
        {
            All,
            Queued,
            Downloading,
            Paused,
            Error,
            Completed
        }

        public static List<DownloadTask> ApplyFilter(List<DownloadTask> queue, FilterBy filter)
        {
            return filter switch
            {
                FilterBy.Queued => queue.Where(t => t.Status == DownloadStatus.Queued).ToList(),
                FilterBy.Downloading => queue.Where(t => t.Status == DownloadStatus.Downloading).ToList(),
                FilterBy.Paused => queue.Where(t => t.Status == DownloadStatus.Paused).ToList(),
                FilterBy.Error => queue.Where(t => t.Status == DownloadStatus.Failed || 
                                                    t.Status == DownloadStatus.ConnectionTimeout ||
                                                    t.Status == DownloadStatus.RemoteFileError).ToList(),
                FilterBy.Completed => queue.Where(t => t.Status == DownloadStatus.Completed).ToList(),
                _ => queue
            };
        }

        public static List<DownloadTask> ApplySort(List<DownloadTask> queue, SortBy sortBy, bool ascending = true)
        {
            var sorted = sortBy switch
            {
                SortBy.Name => ascending 
                    ? queue.OrderBy(t => t.File.FileName)
                    : queue.OrderByDescending(t => t.File.FileName),
                SortBy.Size => ascending
                    ? queue.OrderBy(t => t.File.SizeBytes)
                    : queue.OrderByDescending(t => t.File.SizeBytes),
                SortBy.Status => ascending
                    ? queue.OrderBy(t => t.Status)
                    : queue.OrderByDescending(t => t.Status),
                SortBy.DateAdded => ascending
                    ? queue.OrderBy(t => t.StartedAt ?? DateTime.MaxValue)
                    : queue.OrderByDescending(t => t.StartedAt ?? DateTime.MinValue),
                SortBy.Priority => ascending
                    ? queue.OrderBy(t => t.Priority)
                    : queue.OrderByDescending(t => t.Priority),
                _ => queue.AsEnumerable()
            };

            return sorted.ToList();
        }

        public static List<DownloadTask> SearchQueue(List<DownloadTask> queue, string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return queue;

            var term = searchTerm.ToLowerInvariant();
            return queue.Where(t => 
                t.File.FileName.ToLowerInvariant().Contains(term) ||
                t.File.Username.ToLowerInvariant().Contains(term) ||
                (t.File.Author?.ToLowerInvariant().Contains(term) ?? false)
            ).ToList();
        }
    }
}
