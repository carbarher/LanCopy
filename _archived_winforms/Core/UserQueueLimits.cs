using System;
using System.Collections.Generic;
using System.Linq;
using SlskDown.Models;

namespace SlskDown.Core
{
    /// <summary>
    /// Sistema de límites de cola por usuario (inspirado en slskd/Nicotine+)
    /// </summary>
    public class UserQueueLimits
    {
        private readonly Dictionary<string, UserLimitStats> userStats;
        private readonly object statsLock = new object();
        
        public int MaxQueuedFilesPerUser { get; set; } = 500;
        public long MaxQueuedMegabytesPerUser { get; set; } = 5000;
        public int MaxDailyFilesPerUser { get; set; } = 1000;
        public long MaxDailyMegabytesPerUser { get; set; } = 10000;
        public int MaxWeeklyFilesPerUser { get; set; } = 5000;
        public long MaxWeeklyMegabytesPerUser { get; set; } = 50000;
        
        public UserQueueLimits()
        {
            userStats = new Dictionary<string, UserLimitStats>();
        }
        
        public class UserLimitStats
        {
            public string Username { get; set; }
            public int QueuedFiles { get; set; }
            public long QueuedMegabytes { get; set; }
            public int DailyFiles { get; set; }
            public long DailyMegabytes { get; set; }
            public int WeeklyFiles { get; set; }
            public long WeeklyMegabytes { get; set; }
            public DateTime LastReset { get; set; } = DateTime.Now;
            public DateTime LastWeeklyReset { get; set; } = DateTime.Now;
        }
        
        public enum LimitCheckResult
        {
            Allowed,
            QueuedFilesExceeded,
            QueuedMegabytesExceeded,
            DailyFilesExceeded,
            DailyMegabytesExceeded,
            WeeklyFilesExceeded,
            WeeklyMegabytesExceeded
        }
        
        /// <summary>
        /// Verifica si un usuario puede encolar un archivo
        /// </summary>
        public LimitCheckResult CanEnqueue(string username, long fileSizeBytes)
        {
            lock (statsLock)
            {
                var stats = GetOrCreateStats(username);
                ResetIfNeeded(stats);
                
                var fileSizeMB = fileSizeBytes / (1024 * 1024);
                
                // Verificar límites de cola
                if (stats.QueuedFiles >= MaxQueuedFilesPerUser)
                    return LimitCheckResult.QueuedFilesExceeded;
                
                if (stats.QueuedMegabytes + fileSizeMB > MaxQueuedMegabytesPerUser)
                    return LimitCheckResult.QueuedMegabytesExceeded;
                
                // Verificar límites diarios
                if (stats.DailyFiles >= MaxDailyFilesPerUser)
                    return LimitCheckResult.DailyFilesExceeded;
                
                if (stats.DailyMegabytes + fileSizeMB > MaxDailyMegabytesPerUser)
                    return LimitCheckResult.DailyMegabytesExceeded;
                
                // Verificar límites semanales
                if (stats.WeeklyFiles >= MaxWeeklyFilesPerUser)
                    return LimitCheckResult.WeeklyFilesExceeded;
                
                if (stats.WeeklyMegabytes + fileSizeMB > MaxWeeklyMegabytesPerUser)
                    return LimitCheckResult.WeeklyMegabytesExceeded;
                
                return LimitCheckResult.Allowed;
            }
        }
        
        /// <summary>
        /// Registra que un archivo fue encolado
        /// </summary>
        public void RecordEnqueue(string username, long fileSizeBytes)
        {
            lock (statsLock)
            {
                var stats = GetOrCreateStats(username);
                var fileSizeMB = fileSizeBytes / (1024 * 1024);
                
                stats.QueuedFiles++;
                stats.QueuedMegabytes += fileSizeMB;
                stats.DailyFiles++;
                stats.DailyMegabytes += fileSizeMB;
                stats.WeeklyFiles++;
                stats.WeeklyMegabytes += fileSizeMB;
            }
        }
        
        /// <summary>
        /// Registra que un archivo completó su descarga
        /// </summary>
        public void RecordComplete(string username, long fileSizeBytes)
        {
            lock (statsLock)
            {
                var stats = GetOrCreateStats(username);
                var fileSizeMB = fileSizeBytes / (1024 * 1024);
                
                stats.QueuedFiles = Math.Max(0, stats.QueuedFiles - 1);
                stats.QueuedMegabytes = Math.Max(0, stats.QueuedMegabytes - fileSizeMB);
            }
        }
        
        /// <summary>
        /// Registra que un archivo fue cancelado
        /// </summary>
        public void RecordCancel(string username, long fileSizeBytes)
        {
            RecordComplete(username, fileSizeBytes);
        }
        
        /// <summary>
        /// Obtiene mensaje de rechazo apropiado
        /// </summary>
        public string GetRejectionMessage(LimitCheckResult result)
        {
            return result switch
            {
                LimitCheckResult.QueuedFilesExceeded => "Too many files",
                LimitCheckResult.QueuedMegabytesExceeded => "Too many megabytes",
                LimitCheckResult.DailyFilesExceeded => "Too many files today",
                LimitCheckResult.DailyMegabytesExceeded => "Too many megabytes today",
                LimitCheckResult.WeeklyFilesExceeded => "Too many files this week",
                LimitCheckResult.WeeklyMegabytesExceeded => "Too many megabytes this week",
                _ => "Allowed"
            };
        }
        
        /// <summary>
        /// Obtiene estadísticas de un usuario
        /// </summary>
        public UserLimitStats GetStats(string username)
        {
            lock (statsLock)
            {
                return GetOrCreateStats(username);
            }
        }
        
        /// <summary>
        /// Resetea contadores si es necesario
        /// </summary>
        private void ResetIfNeeded(UserLimitStats stats)
        {
            var now = DateTime.Now;
            
            // Reset diario (cada 24 horas)
            if ((now - stats.LastReset).TotalHours >= 24)
            {
                stats.DailyFiles = 0;
                stats.DailyMegabytes = 0;
                stats.LastReset = now;
            }
            
            // Reset semanal (cada 7 días)
            if ((now - stats.LastWeeklyReset).TotalDays >= 7)
            {
                stats.WeeklyFiles = 0;
                stats.WeeklyMegabytes = 0;
                stats.LastWeeklyReset = now;
            }
        }
        
        private UserLimitStats GetOrCreateStats(string username)
        {
            if (!userStats.ContainsKey(username))
            {
                userStats[username] = new UserLimitStats { Username = username };
            }
            return userStats[username];
        }
        
        /// <summary>
        /// Obtiene top usuarios por uso
        /// </summary>
        public List<UserLimitStats> GetTopUsers(int count = 10)
        {
            lock (statsLock)
            {
                return userStats.Values
                    .OrderByDescending(s => s.QueuedFiles)
                    .Take(count)
                    .ToList();
            }
        }
        
        /// <summary>
        /// Limpia estadísticas antiguas
        /// </summary>
        public void CleanupOldStats(int daysOld = 30)
        {
            lock (statsLock)
            {
                var cutoff = DateTime.Now.AddDays(-daysOld);
                var toRemove = userStats
                    .Where(kvp => kvp.Value.LastReset < cutoff && kvp.Value.QueuedFiles == 0)
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var key in toRemove)
                {
                    userStats.Remove(key);
                }
            }
        }
    }
}
