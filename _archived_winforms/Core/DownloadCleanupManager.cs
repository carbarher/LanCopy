using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SlskDown.Models;
using DownloadHistoryRecord = SlskDown.Models.DownloadHistoryRecord;

namespace SlskDown.Core
{
    /// <summary>
    /// MEJORA #11 (Nicotine+ 3.3.0): Clear Deleted Downloads
    /// Limpia automáticamente descargas cuyo archivo ya no existe en disco
    /// </summary>
    public class DownloadCleanupManager
    {
        private readonly Action<string> log;
        
        public DownloadCleanupManager(Action<string> log)
        {
            this.log = log;
        }

        /// <summary>
        /// Limpia descargas completadas cuyos archivos fueron eliminados del disco
        /// </summary>
        public CleanupResult ClearDeletedDownloads(List<DownloadHistoryRecord> downloadHistory)
        {
            var result = new CleanupResult();
            var toRemove = new List<DownloadHistoryRecord>();

            foreach (var download in downloadHistory)
            {
                // Solo verificar descargas completadas
                if (download.Status != "Completed")
                    continue;

                // Verificar si el archivo existe
                if (!File.Exists(download.FileName))
                {
                    toRemove.Add(download);
                    result.DeletedFiles.Add(download.FileName);
                    result.FreedSpace += download.SizeBytes;
                }
            }

            // Remover de la lista
            foreach (var download in toRemove)
            {
                downloadHistory.Remove(download);
            }

            result.RemovedCount = toRemove.Count;
            
            if (result.RemovedCount > 0)
            {
                log?.Invoke($"🗑️ Limpiados {result.RemovedCount} archivos eliminados del historial");
                log?.Invoke($"   💾 Espacio liberado: {FormatFileSize(result.FreedSpace)}");
            }
            else
            {
                log?.Invoke("✅ No hay archivos eliminados en el historial");
            }

            return result;
        }

        /// <summary>
        /// Limpia descargas fallidas antiguas (más de X días)
        /// </summary>
        public CleanupResult ClearOldFailedDownloads(List<DownloadHistoryRecord> downloadHistory, int daysOld = 30)
        {
            var result = new CleanupResult();
            var cutoffDate = DateTime.Now.AddDays(-daysOld);
            var toRemove = new List<DownloadHistoryRecord>();

            foreach (var download in downloadHistory)
            {
                // Solo descargas fallidas o canceladas
                if (download.Status != "Failed" && 
                    download.Status != "Cancelled")
                    continue;

                // Verificar fecha
                if (download.CompletedAt < cutoffDate)
                {
                    toRemove.Add(download);
                    result.DeletedFiles.Add(download.FileName);
                }
            }

            // Remover de la lista
            foreach (var download in toRemove)
            {
                downloadHistory.Remove(download);
            }

            result.RemovedCount = toRemove.Count;
            
            if (result.RemovedCount > 0)
            {
                log?.Invoke($"🗑️ Limpiados {result.RemovedCount} fallos antiguos (>{daysOld} días)");
            }

            return result;
        }

        /// <summary>
        /// Limpia duplicados en el historial (mismo archivo, mismo usuario)
        /// </summary>
        public CleanupResult ClearDuplicateDownloads(List<DownloadHistoryRecord> downloadHistory)
        {
            var result = new CleanupResult();
            var seen = new HashSet<string>();
            var toRemove = new List<DownloadHistoryRecord>();

            // Ordenar por fecha (más reciente primero)
            var sorted = downloadHistory.OrderByDescending(d => d.CompletedAt).ToList();

            foreach (var download in sorted)
            {
                var key = $"{download.Username}|{download.FileName}";
                
                if (seen.Contains(key))
                {
                    // Duplicado encontrado
                    toRemove.Add(download);
                }
                else
                {
                    seen.Add(key);
                }
            }

            // Remover duplicados
            foreach (var download in toRemove)
            {
                downloadHistory.Remove(download);
            }

            result.RemovedCount = toRemove.Count;
            
            if (result.RemovedCount > 0)
            {
                log?.Invoke($"🗑️ Limpiados {result.RemovedCount} duplicados del historial");
            }

            return result;
        }

        /// <summary>
        /// Limpieza completa: archivos eliminados + fallos antiguos + duplicados
        /// </summary>
        public CleanupResult FullCleanup(List<DownloadHistoryRecord> downloadHistory, int daysOld = 30)
        {
            var totalResult = new CleanupResult();

            log?.Invoke("🧹 Iniciando limpieza completa del historial...");

            // 1. Archivos eliminados
            var deleted = ClearDeletedDownloads(downloadHistory);
            totalResult.Merge(deleted);

            // 2. Fallos antiguos
            var oldFailed = ClearOldFailedDownloads(downloadHistory, daysOld);
            totalResult.Merge(oldFailed);

            // 3. Duplicados
            var duplicates = ClearDuplicateDownloads(downloadHistory);
            totalResult.Merge(duplicates);

            log?.Invoke($"✅ Limpieza completa: {totalResult.RemovedCount} entradas eliminadas");
            
            return totalResult;
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        public class CleanupResult
        {
            public int RemovedCount { get; set; }
            public long FreedSpace { get; set; }
            public List<string> DeletedFiles { get; set; } = new List<string>();

            public void Merge(CleanupResult other)
            {
                RemovedCount += other.RemovedCount;
                FreedSpace += other.FreedSpace;
                DeletedFiles.AddRange(other.DeletedFiles);
            }
        }
    }
}
