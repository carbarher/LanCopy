using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soulseek;

namespace SlskDown
{
    /// <summary>
    /// MainForm - Partial Class para búsquedas
    /// Contiene: search logic, filters, caching, auto-search
    /// </summary>
    public partial class MainForm : Form
    {
        #region Search Execution
        
        /// <summary>
        /// Ejecuta una búsqueda
        /// </summary>
        private async Task<List<SearchResult>> ExecuteSearchAsync(string query, CancellationToken cancellationToken)
        {
            try
            {
                AutoLog($"🔍 Buscando: {query}");
                
                // Verificar caché
                if (searchCache.TryGetValue(query, out var cachedResults))
                {
                    AutoLog($"💾 Resultados desde caché: {cachedResults.Count:N0}");
                    telemetryService?.IncrementCounter("search.cache_hit");
                    return cachedResults;
                }
                
                telemetryService?.IncrementCounter("search.cache_miss");
                
                // Medir tiempo de búsqueda
                using (telemetryService?.MeasureOperation("search"))
                {
                    var results = new List<SearchResult>();
                    
                    var searchOptions = new SearchOptions(
                        searchTimeout: searchTimeout,
                        responseLimit: maxSearchResults,
                        filterResponses: true,
                        minimumResponseFileCount: 1,
                        minimumPeerUploadSpeed: 0
                    );
                    
                    var searchTask = client.SearchAsync(
                        SearchQuery.FromText(query),
                        options: searchOptions,
                        cancellationToken: cancellationToken
                    );
                    
                    await foreach (var response in searchTask.ConfigureAwait(false))
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;
                        
                        // Filtrar resultados
                        var filteredFiles = FilterSearchResponse(response);
                        
                        if (filteredFiles.Any())
                        {
                            results.Add(new SearchResult
                            {
                                Username = response.Username,
                                Files = filteredFiles,
                                FreeUploadSlots = response.FreeUploadSlots,
                                UploadSpeed = response.UploadSpeed,
                                QueueLength = response.QueueLength
                            });
                        }
                    }
                    
                    // Guardar en caché
                    searchCache[query] = results;
                    
                    AutoLog($"✅ Búsqueda completada: {results.Count:N0} usuarios, {results.Sum(r => r.Files.Count):N0} archivos");
                    
                    telemetryService?.IncrementCounter("search.completed");
                    telemetryService?.RecordValue("search.results_count", results.Count);
                    
                    return results;
                }
            }
            catch (OperationCanceledException)
            {
                AutoLog($"⏹️ Búsqueda cancelada: {query}");
                telemetryService?.IncrementCounter("search.cancelled");
                return new List<SearchResult>();
            }
            catch (Exception ex)
            {
                AutoLog($"❌ Error en búsqueda: {ex.Message}");
                telemetryService?.IncrementCounter("search.failed");
                throw;
            }
        }
        
        #endregion
        
        #region Search Filtering
        
        /// <summary>
        /// Filtra archivos de una respuesta de búsqueda
        /// </summary>
        private List<Soulseek.File> FilterSearchResponse(SearchResponse response)
        {
            var files = response.Files.AsEnumerable();
            
            // Filtro de tamaño mínimo
            if (minFileSize > 0)
            {
                files = files.Where(f => f.Size >= minFileSize * 1024 * 1024);
            }
            
            // Filtro de extensiones permitidas
            if (allowedExtensions?.Any() == true)
            {
                files = files.Where(f => allowedExtensions.Any(ext => 
                    f.Filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
            }
            
            // Filtro de archivos basura
            files = files.Where(f => !IsGarbageFile(f.Filename));
            
            // Filtro de español (si está habilitado)
            if (filterSpanishOnly)
            {
                files = files.Where(f => IsSpanishFile(f.Filename));
            }
            
            // Filtro de blacklist
            if (blacklist?.Contains(response.Username) == true)
            {
                return new List<Soulseek.File>();
            }
            
            return files.ToList();
        }
        
        /// <summary>
        /// Verifica si un archivo es basura
        /// </summary>
        private bool IsGarbageFile(string filename)
        {
            var garbagePatterns = new[]
            {
                ".tmp", ".part", ".crdownload", ".download",
                "thumbs.db", "desktop.ini", ".ds_store"
            };
            
            var lowerFilename = filename.ToLowerInvariant();
            return garbagePatterns.Any(pattern => lowerFilename.Contains(pattern));
        }
        
        /// <summary>
        /// Verifica si un archivo es en español (heurística)
        /// </summary>
        private bool IsSpanishFile(string filename)
        {
            var spanishKeywords = new[]
            {
                "español", "spanish", "castellano", "latino", "latam",
                "es-", "spa", "[es]", "(es)", "_es_"
            };
            
            var lowerFilename = filename.ToLowerInvariant();
            return spanishKeywords.Any(keyword => lowerFilename.Contains(keyword));
        }
        
        #endregion
        
        #region Auto-Search
        
        /// <summary>
        /// Ejecuta búsqueda automática para múltiples autores
        /// </summary>
        private async Task ExecuteAutoSearchAsync(List<string> authors, CancellationToken cancellationToken)
        {
            try
            {
                autoSearchRunning = true;
                int processed = 0;
                int total = authors.Count;
                
                AutoLog($"🚀 Iniciando búsqueda automática: {total} autores");
                
                // Usar paralelismo adaptativo
                var adaptiveParallelism = EnsureAdaptiveAutoSearchInitialized();
                int parallelism = adaptiveParallelism != null
                    ? adaptiveParallelism.GetOptimalParallelism()
                    : maxParallelAutoSearches;
                var semaphore = new SemaphoreSlim(parallelism, parallelism);
                var tasks = new List<Task>();
                
                foreach (var author in authors)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    
                    await semaphore.WaitAsync(cancellationToken);
                    
                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            var results = await ExecuteSearchAsync(author, cancellationToken);
                            
                            // Registrar éxito/fallo para paralelismo adaptativo
                            bool success = results.Any();
                            adaptiveParallelism?.RecordResult(success);
                            
                            processed++;
                            
                            SafeBeginInvoke(() =>
                            {
                                lblPurgeProgress.Text = $"Progreso: {processed}/{total} ({processed * 100 / total}%)";
                            });
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, cancellationToken);
                    
                    tasks.Add(task);
                }
                
                await Task.WhenAll(tasks);
                
                AutoLog($"✅ Búsqueda automática completada: {processed}/{total} autores procesados");

                // Persistir estado de cierre para evitar reanudaciones no deseadas
                SaveAutoSearchState(
                    wasRunning: false,
                    currentIndex: 0,
                    authors: Array.Empty<string>(),
                    completedPasses: 0);

                ClearAutoSearchState();
            }
            catch (OperationCanceledException)
            {
                AutoLog("⏹️ Búsqueda automática cancelada");
                SaveAutoSearchState(
                    wasRunning: false,
                    currentIndex: 0,
                    authors: Array.Empty<string>(),
                    completedPasses: 0);
                ClearAutoSearchState();
            }
            catch (Exception ex)
            {
                AutoLog($"❌ Error en búsqueda automática: {ex.Message}");
                SaveAutoSearchState(
                    wasRunning: false,
                    currentIndex: 0,
                    authors: Array.Empty<string>(),
                    completedPasses: 0);
                ClearAutoSearchState();
            }
            finally
            {
                autoSearchRunning = false;
                SaveAutoSearchState(
                    wasRunning: false,
                    currentIndex: 0,
                    authors: Array.Empty<string>(),
                    completedPasses: 0);

                ClearAutoSearchState();
            }
        }
        
        #endregion
        
        #region Search Cache Management
        
        /// <summary>
        /// Limpia caché de búsquedas antiguas
        /// </summary>
        private void CleanSearchCache()
        {
            try
            {
                var cutoffTime = DateTime.Now.AddHours(-24);
                var keysToRemove = searchCache
                    .Where(kvp => kvp.Value.Any() && 
                           kvp.Value.First().Timestamp < cutoffTime)
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var key in keysToRemove)
                {
                    searchCache.TryRemove(key, out _);
                }
                
                if (keysToRemove.Any())
                {
                    AutoLog($"🗑️ Limpieza de caché: {keysToRemove.Count} búsquedas antiguas eliminadas");
                }
            }
            catch (Exception ex)
            {
                AutoLog($"⚠️ Error limpiando caché: {ex.Message}");
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Resultado de búsqueda
    /// </summary>
    public class SearchResult
    {
        public string Username { get; set; }
        public List<Soulseek.File> Files { get; set; }
        public int FreeUploadSlots { get; set; }
        public int UploadSpeed { get; set; }
        public int QueueLength { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
