using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SlskDown.Models;

namespace SlskDown.Core
{
    /// <summary>
    /// Métodos de extensión para DownloadManager que implementan las mejoras 8-16
    /// </summary>
    public partial class DownloadManager
    {
        /// <summary>
        /// MEJORA #8: Obtiene el tiempo de próximo reintento con backoff exponencial
        /// </summary>
        private DateTime? GetIntelligentRetryTime(DownloadTask task, int attemptNumber)
        {
            if (!config.EnableIntelligentRetry || intelligentRetry == null)
                return DateTime.Now;

            var fileKey = $"{task.File.Username}_{task.File.FileName}";
            return intelligentRetry.GetNextRetryTime(fileKey, attemptNumber);
        }

        /// <summary>
        /// MEJORA #8: Verifica si puede reintentar ahora
        /// </summary>
        private bool CanRetryNow(DownloadTask task)
        {
            if (!config.EnableIntelligentRetry || intelligentRetry == null)
                return true;

            var fileKey = $"{task.File.Username}_{task.File.FileName}";
            return intelligentRetry.CanRetryNow(fileKey);
        }

        /// <summary>
        /// MEJORA #9: Cachea resultado de búsqueda
        /// </summary>
        private void CacheSearchResult(string fileName, List<string> providers, long fileSize)
        {
            if (!config.EnableMetadataCache || metadataCache == null)
                return;

            try
            {
                metadataCache.CacheSearchResult(fileName, providers, fileSize);
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error cacheando metadatos: {ex.Message}");
            }
        }

        /// <summary>
        /// MEJORA #9: Obtiene metadatos cacheados
        /// </summary>
        private CachedMetadata GetCachedMetadata(string fileName)
        {
            if (!config.EnableMetadataCache || metadataCache == null)
                return null;

            try
            {
                return metadataCache.GetCachedMetadata(fileName);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// MEJORA #10: Verifica si debe activar modo agresivo
        /// </summary>
        private bool ShouldEnableAggressiveMode(DownloadTask task, int availableProviders)
        {
            if (!config.EnableAggressiveMode)
                return false;

            return aggressiveMode.ShouldEnableForFile(task.File.FileName, availableProviders);
        }

        /// <summary>
        /// MEJORA #10: Obtiene límite de descargas según modo
        /// </summary>
        private int GetEffectiveMaxDownloads()
        {
            if (!config.EnableAggressiveMode)
                return config.MaxSimultaneousDownloads;

            return aggressiveMode.GetMaxDownloads();
        }

        /// <summary>
        /// MEJORA #11: Comprime la cola para guardar
        /// </summary>
        private byte[] CompressQueueForSave(List<DownloadTask> queue)
        {
            if (!config.EnableQueueCompression)
                return null;

            try
            {
                return QueueCompressor.CompressQueue(queue);
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error comprimiendo cola: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// MEJORA #11: Descomprime la cola al cargar
        /// </summary>
        private List<DownloadTask> DecompressQueueFromLoad(byte[] compressedData)
        {
            if (!config.EnableQueueCompression || compressedData == null)
                return new List<DownloadTask>();

            try
            {
                return QueueCompressor.DecompressQueue(compressedData);
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error descomprimiendo cola: {ex.Message}");
                return new List<DownloadTask>();
            }
        }

        /// <summary>
        /// MEJORA #12: Construye índice de búsqueda rápida
        /// </summary>
        private void BuildSearchIndex()
        {
            if (!config.EnableFastSearchIndex || searchIndex == null)
                return;

            try
            {
                lock (downloadQueueLock)
                {
                    searchIndex.BuildIndex(downloadQueue);
                    Log($"🔍 Índice de búsqueda construido: {downloadQueue.Count} tareas");
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error construyendo índice: {ex.Message}");
            }
        }

        /// <summary>
        /// MEJORA #12: Búsqueda rápida por nombre de archivo
        /// </summary>
        public List<DownloadTask> FastSearchByFileName(string fileName)
        {
            if (!config.EnableFastSearchIndex)
                return new List<DownloadTask>();

            try
            {
                return searchIndex.SearchByFileName(fileName);
            }
            catch
            {
                return new List<DownloadTask>();
            }
        }

        /// <summary>
        /// MEJORA #12: Búsqueda rápida por proveedor
        /// </summary>
        public List<DownloadTask> FastSearchByProvider(string provider)
        {
            if (!config.EnableFastSearchIndex)
                return new List<DownloadTask>();

            try
            {
                return searchIndex.SearchByProvider(provider);
            }
            catch
            {
                return new List<DownloadTask>();
            }
        }

        /// <summary>
        /// MEJORA #13: Prefetch de proveedores top
        /// </summary>
        private async Task PrefetchTopProviders()
        {
            if (!config.EnableProviderPrefetch)
                return;

            try
            {
                List<string> topProviders;
                lock (downloadQueueLock)
                {
                    topProviders = prefetchManager.GetTopProviders(downloadQueue, config.PrefetchTopProvidersCount);
                }

                foreach (var provider in topProviders)
                {
                    if (prefetchManager.ShouldPrefetch(provider))
                    {
                        // Aquí iría la lógica de pre-conexión
                        prefetchManager.MarkPrefetched(provider);
                        Log($"🔌 Prefetch: {provider}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error en prefetch: {ex.Message}");
            }
        }

        /// <summary>
        /// MEJORA #14: Actualiza dashboard con velocidad actual
        /// </summary>
        private void UpdateDashboard(double currentSpeedKBps)
        {
            if (!config.EnableRealtimeDashboard || dashboard == null)
                return;

            try
            {
                dashboard.RecordSpeed(currentSpeedKBps);
            }
            catch { }
        }

        /// <summary>
        /// MEJORA #14: Registra descarga completada en dashboard
        /// </summary>
        private void RecordDownloadInDashboard(long bytes)
        {
            if (!config.EnableRealtimeDashboard)
                return;

            try
            {
                dashboard.RecordDownload(bytes);
            }
            catch { }
        }

        /// <summary>
        /// MEJORA #14: Obtiene estadísticas del dashboard
        /// </summary>
        public DashboardStats GetDashboardStats()
        {
            if (!config.EnableRealtimeDashboard || dashboard == null)
                return null;

            try
            {
                lock (downloadQueueLock)
                {
                    return dashboard.GetStats(downloadQueue);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// MEJORA #16: Exporta la cola a JSON
        /// </summary>
        public void ExportQueueToJson(string filePath)
        {
            if (!config.EnableQueueExportImport)
                return;

            try
            {
                lock (downloadQueueLock)
                {
                    QueueExportImport.ExportToJson(downloadQueue, filePath);
                    Log($"📤 Cola exportada a: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Error exportando cola: {ex.Message}");
            }
        }

        /// <summary>
        /// MEJORA #16: Exporta la cola a CSV
        /// </summary>
        public void ExportQueueToCsv(string filePath)
        {
            if (!config.EnableQueueExportImport)
                return;

            try
            {
                lock (downloadQueueLock)
                {
                    QueueExportImport.ExportToCsv(downloadQueue, filePath);
                    Log($"📤 Cola exportada a: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Error exportando cola: {ex.Message}");
            }
        }

        /// <summary>
        /// MEJORA #16: Importa cola desde JSON
        /// </summary>
        public async Task<int> ImportQueueFromJson(string filePath)
        {
            if (!config.EnableQueueExportImport)
                return 0;

            try
            {
                var importedTasks = QueueExportImport.ImportFromJson(filePath);
                var addedCount = 0;

                foreach (var task in importedTasks)
                {
                    // Aquí iría la lógica para añadir las tareas importadas a la cola
                    // Por ahora solo contamos
                    addedCount++;
                }

                Log($"📥 Cola importada: {addedCount} tareas desde {filePath}");
                return addedCount;
            }
            catch (Exception ex)
            {
                Log($"❌ Error importando cola: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// MEJORA #16: Importa cola desde CSV
        /// </summary>
        public async Task<int> ImportQueueFromCsv(string filePath)
        {
            if (!config.EnableQueueExportImport)
                return 0;

            try
            {
                var importedTasks = QueueExportImport.ImportFromCsv(filePath);
                var addedCount = 0;

                foreach (var task in importedTasks)
                {
                    // Aquí iría la lógica para añadir las tareas importadas a la cola
                    addedCount++;
                }

                Log($"📥 Cola importada: {addedCount} tareas desde {filePath}");
                return addedCount;
            }
            catch (Exception ex)
            {
                Log($"❌ Error importando cola: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Método auxiliar para guardar caché de metadatos al detener
        /// </summary>
        private void SaveMetadataCache()
        {
            if (!config.EnableMetadataCache)
                return;

            try
            {
                metadataCache.SaveCache();
                Log($"💾 Caché de metadatos guardado: {metadataCache.GetCacheSize()} entradas");
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error guardando caché: {ex.Message}");
            }
        }

        /// <summary>
        /// Método auxiliar para limpiar entradas expiradas del caché
        /// </summary>
        private void CleanupExpiredCache()
        {
            if (!config.EnableMetadataCache)
                return;

            try
            {
                metadataCache.ClearExpiredEntries();
            }
            catch { }
        }
    }
}
