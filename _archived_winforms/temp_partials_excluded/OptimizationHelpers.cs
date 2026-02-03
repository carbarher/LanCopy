using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Soulseek;
using SlskDown.Models;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;
using IODirectory = System.IO.Directory;

namespace SlskDown
{
    public partial class MainForm
    {
        // ============================================
        // CACHÉ DE BÚSQUEDAS (24H TTL)
        // ============================================
        
        private void LoadSearchCache()
        {
            try
            {
                if (IOFile.Exists(searchCachePath))
                {
                    var json = IOFile.ReadAllText(searchCachePath);
                    searchCache = JsonSerializer.Deserialize<Dictionary<string, SearchCacheEntry>>(json) 
                        ?? new Dictionary<string, SearchCacheEntry>(StringComparer.OrdinalIgnoreCase);
                    
                    // Limpiar entradas expiradas (>24h)
                    var expired = searchCache.Where(kvp => (DateTime.Now - kvp.Value.Timestamp).TotalHours > 24).Select(kvp => kvp.Key).ToList();
                    foreach (var key in expired)
                        searchCache.Remove(key);
                    
                    AutoLog($"📦 Caché cargada: {searchCache.Count} búsquedas recientes");
                }
            }
            catch (Exception ex)
            {
                AutoLog($"⚠️ Error al cargar caché: {ex.Message}");
                searchCache = new Dictionary<string, SearchCacheEntry>(StringComparer.OrdinalIgnoreCase);
            }
        }
        
        private void SaveSearchCache()
        {
            try
            {
                var json = JsonSerializer.Serialize(searchCache, new JsonSerializerOptions { WriteIndented = true });
                IOFile.WriteAllText(searchCachePath, json);
            }
            catch (Exception ex)
            {
                AutoLog($"⚠️ Error al guardar caché: {ex.Message}");
            }
        }
        
        private bool TryGetCachedSearch(string author, out SearchCacheEntry entry)
        {
            if (searchCache.TryGetValue(author, out entry))
            {
                // Verificar si no ha expirado (24h)
                if ((DateTime.Now - entry.Timestamp).TotalHours < 24)
                {
                    return true;
                }
                // Expirado, eliminar
                searchCache.Remove(author);
            }
            entry = null;
            return false;
        }
        
        private void CacheSearchResult(string author, int filesCount, bool hasValidFiles)
        {
            searchCache[author] = new SearchCacheEntry
            {
                Timestamp = DateTime.Now,
                FilesCount = filesCount,
                HasValidFiles = hasValidFiles
            };
        }
        
        // ============================================
        // PROCESAMIENTO POR LOTES
        // ============================================
        
        private class BatchProgress
        {
            public int CurrentBatch { get; set; }
            public int TotalBatches { get; set; }
            public List<string> ProcessedAuthors { get; set; } = new List<string>();
            public DateTime StartTime { get; set; }
        }
        
        private void SaveBatchProgress(int batch, int total, List<string> processed)
        {
            try
            {
                var progress = new BatchProgress
                {
                    CurrentBatch = batch,
                    TotalBatches = total,
                    ProcessedAuthors = processed,
                    StartTime = batchStopwatch.Elapsed.TotalSeconds > 0 ? DateTime.Now.AddSeconds(-batchStopwatch.Elapsed.TotalSeconds) : DateTime.Now
                };
                
                var json = JsonSerializer.Serialize(progress, new JsonSerializerOptions { WriteIndented = true });
                IOFile.WriteAllText(progressPath, json);
            }
            catch (Exception ex)
            {
                AutoLog($"⚠️ Error al guardar progreso: {ex.Message}");
            }
        }
        
        private BatchProgress LoadBatchProgress()
        {
            try
            {
                if (IOFile.Exists(progressPath))
                {
                    var json = IOFile.ReadAllText(progressPath);
                    return JsonSerializer.Deserialize<BatchProgress>(json);
                }
            }
            catch (Exception ex)
            {
                AutoLog($"⚠️ Error al cargar progreso: {ex.Message}");
            }
            return null;
        }
        
        private void ClearBatchProgress()
        {
            try
            {
                if (IOFile.Exists(progressPath))
                    IOFile.Delete(progressPath);
            }
            catch { }
        }
        
        private int CalculateBatchSize(int totalAuthors)
        {
            if (totalAuthors < 5000)
                return 500;
            else if (totalAuthors < 20000)
                return 1000;
            else
                return 2000;
        }
        
        // ============================================
        // PARALELISMO ADAPTATIVO
        // ============================================
        
        private void AdjustParallelism(long searchTimeMs)
        {
            // Si el ajuste adaptativo está deshabilitado, mantener máximo paralelismo
            if (!enableAdaptiveParallelism)
            {
                if (currentParallelism < maxParallelism)
                {
                    currentParallelism = maxParallelism;
                }
                return;
            }
            
            // Ignorar búsquedas extremadamente lentas (probablemente timeouts)
            if (searchTimeMs > 10000)
                return;
            
            recentSearchTimes.Enqueue(searchTimeMs);
            
            // Mantener solo las últimas 30 búsquedas para mejor promedio
            while (recentSearchTimes.Count > 30)
                recentSearchTimes.Dequeue();
            
            // Ajustar cada 20 búsquedas (no tan frecuente)
            if (recentSearchTimes.Count >= 20 && recentSearchTimes.Count % 20 == 0)
            {
                var avgTime = recentSearchTimes.Average();
                
                // Si búsquedas rápidas (<3s), aumentar paralelismo agresivamente
                if (avgTime < 3000 && currentParallelism < maxParallelism)
                {
                    currentParallelism = Math.Min(currentParallelism + 2, maxParallelism);
                    AutoLog($"⚡ Paralelismo aumentado a {currentParallelism} (búsquedas rápidas: {avgTime:F0}ms)");
                }
                // Solo reducir si REALMENTE lentas (>8s) y con muchos errores
                else if (avgTime > 8000 && currentParallelism > minParallelism && consecutiveErrors > 5)
                {
                    currentParallelism--;
                    AutoLog($"🐌 Paralelismo reducido a {currentParallelism} (búsquedas lentas: {avgTime:F0}ms)");
                }
            }
        }
        
        // ============================================
        // THROTTLING ADAPTATIVO
        // ============================================
        
        private async Task<bool> CheckThrottling()
        {
            if (isThrottled)
            {
                var elapsed = (DateTime.Now - lastThrottleTime).TotalSeconds;
                if (elapsed < 30) // Esperar 30s si estamos throttled
                {
                    await Task.Delay(1000);
                    return true;
                }
                else
                {
                    isThrottled = false;
                    consecutiveErrors = 0;
                    AutoLog("✅ Throttling desactivado, reanudando...");
                }
            }
            return false;
        }
        
        private void HandleSearchError(Exception ex)
        {
            consecutiveErrors++;
            
            // Si hay muchos errores consecutivos, activar throttling (reducido de 5 a 2)
            if (consecutiveErrors >= 2)
            {
                isThrottled = true;
                lastThrottleTime = DateTime.Now;
                AutoLog($"⚠️ THROTTLING ACTIVADO: {consecutiveErrors} errores consecutivos. Pausando 30s...");
                
                // Reducir paralelismo también
                if (currentParallelism > minParallelism)
                {
                    currentParallelism = minParallelism;
                    AutoLog($"🔽 Paralelismo reducido a mínimo: {currentParallelism}");
                }
            }
        }
        
        private void HandleSearchSuccess()
        {
            // Resetear contador de errores en éxito
            if (consecutiveErrors > 0)
            {
                consecutiveErrors = Math.Max(0, consecutiveErrors - 1);
            }
        }
        
        // ============================================
        // GESTIÓN DE CONEXIÓN
        // ============================================
        
        private async Task<bool> EnsureConnected()
        {
            // Verificar si ya estamos conectados
            if (client != null && 
                (client.State.HasFlag(SoulseekClientStates.Connected) || 
                 client.State.HasFlag(SoulseekClientStates.LoggedIn)))
            {
                return true;
            }
            
            // Intentar reconectar
            AutoLog("⚠️ Conexión perdida, intentando reconectar...");
            
            try
            {
                // Llamar al método de reconexión del MainForm
                await CheckAndReconnect();
                
                // Esperar un momento para que la conexión se establezca
                await Task.Delay(2000);
                
                // Verificar si la reconexión fue exitosa
                if (client != null && 
                    (client.State.HasFlag(SoulseekClientStates.Connected) || 
                     client.State.HasFlag(SoulseekClientStates.LoggedIn)))
                {
                    AutoLog("✅ Reconexión exitosa");
                    return true;
                }
                else
                {
                    AutoLog("❌ Reconexión fallida");
                    return false;
                }
            }
            catch (Exception ex)
            {
                AutoLog($"❌ Error durante reconexión: {ex.Message}");
                return false;
            }
        }
        
        // ============================================
        // PRIORIZACIÓN INTELIGENTE
        // ============================================
        
        private List<string> PrioritizeAuthors(List<string> authors)
        {
            // Ordenar autores por prioridad:
            // 1. Autores con caché que tenían archivos válidos
            // 2. Autores sin caché
            // 3. Autores con caché que no tenían archivos
            
            var withValidCache = new List<string>();
            var withoutCache = new List<string>();
            var withInvalidCache = new List<string>();
            
            foreach (var author in authors)
            {
                if (TryGetCachedSearch(author, out var entry))
                {
                    if (entry.HasValidFiles)
                        withValidCache.Add(author);
                    else
                        withInvalidCache.Add(author);
                }
                else
                {
                    withoutCache.Add(author);
                }
            }
            
            var prioritized = new List<string>();
            prioritized.AddRange(withValidCache);
            prioritized.AddRange(withoutCache);
            prioritized.AddRange(withInvalidCache);
            
            if (withValidCache.Count > 0)
                AutoLog($"🎯 Priorizados {withValidCache.Count} autores con historial válido");
            
            return prioritized;
        }
        
        // ============================================
        // GUARDADO INCREMENTAL
        // ============================================
        
        private void SaveIncrementalResults(int batchNumber)
        {
            try
            {
                var batchFile = Path.Combine(dataDir, $"auto_search_batch_{batchNumber:D3}.csv");
                SaveAutoResultsToCsv(batchFile);
                AutoLog($"💾 Lote {batchNumber} guardado: {batchFile}");
            }
            catch (Exception ex)
            {
                AutoLog($"⚠️ Error al guardar lote {batchNumber}: {ex.Message}");
            }
        }
        
        private void SaveAutoResultsToCsv(string filePath = null)
        {
            try
            {
                var targetPath = filePath ?? autoResultsCsvPath;
                List<AutoSearchFileResult> snapshot;
                lock (autoSearchResultsLock)
                {
                    snapshot = autoSearchResults.ToList();
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Author,Username,FileName,Directory,SizeBytes,SizeReadable,IsSpanish,IsDocument,Timestamp");

                foreach (var result in snapshot)
                {
                    sb.AppendLine($"\"{result.Author}\",\"{result.Username}\",\"{result.FileName}\",\"{result.Directory}\",{result.SizeBytes},\"{result.SizeReadable}\",{result.IsSpanish},{result.IsDocument},{result.Timestamp:yyyy-MM-dd HH:mm:ss}");
                }

                IOFile.WriteAllText(targetPath, sb.ToString());
            }
            catch (Exception ex)
            {
                AutoLog($"❌ Error al guardar CSV: {ex.Message}");
            }
        }
        
        // ============================================
        // ESTIMACIÓN DE TIEMPO
        // ============================================
        
        private string EstimateRemainingTime(int processed, int total, TimeSpan elapsed)
        {
            if (processed == 0) return "Calculando...";
            
            var avgTimePerAuthor = elapsed.TotalSeconds / processed;
            var remaining = total - processed;
            var estimatedSeconds = avgTimePerAuthor * remaining;
            
            var ts = TimeSpan.FromSeconds(estimatedSeconds);
            
            if (ts.TotalHours >= 1)
                return $"{ts.Hours}h {ts.Minutes}m";
            else if (ts.TotalMinutes >= 1)
                return $"{ts.Minutes}m {ts.Seconds}s";
            else
                return $"{ts.Seconds}s";
        }
    }
}
