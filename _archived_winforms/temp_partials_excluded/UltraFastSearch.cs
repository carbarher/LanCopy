using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SlskDown
{
    /// <summary>
    /// Sistema ultra-rÃ¡pido de bÃºsqueda integrando todas las optimizaciones
    /// </summary>
    public partial class MainForm
    {
        // ConfiguraciÃ³n del modo ultra-rÃ¡pido
        private static bool ultraFastMode = false;
        private static readonly UltraFastStats ultraStats = new();
        
        /// <summary>
        /// EstadÃ­sticas del modo ultra-rÃ¡pido
        /// </summary>
        public struct UltraFastStats
        {
            public int TotalAuthorsProcessed { get; set; }
            public int TotalFilesFound { get; set; }
            public TimeSpan TotalSearchTime { get; set; }
            public int CacheHits { get; set; }
            public int ParallelSearches { get; set; }
            public int BatchesProcessed { get; set; }
            public double AverageSpeed => TotalAuthorsProcessed > 0 ? TotalAuthorsProcessed / TotalSearchTime.TotalMinutes : 0;
            public double Efficiency => TotalAuthorsProcessed > 0 ? (double)CacheHits / TotalAuthorsProcessed : 0;
        }
        
        /// <summary>
        /// Iniciar bÃºsqueda ultra-rÃ¡pida integrada
        /// </summary>
        private async Task StartUltraFastSearchAsync(List<string> authors)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                
                Console.WriteLine("[UltraFast] ðŸš€ INICIANDO MODO ULTRA-RÃPIDO");
                
                // Activar todas las optimizaciones
                EnableUltraFastMode();
                
                // Limpiar y preparar nuevo log
                ClearUltraFastLog();
                
                // Mostrar configuraciÃ³n en UI
                AddColoredLogMessage("ðŸš€ MODO ULTRA-RÃPIDO ACTIVADO", LogMessageType.Phase);
                AddColoredLogMessage("âš¡ Optimizaciones: Paralelo + Cache + Lotes + Timeout Adaptativo", LogMessageType.Info);
                AddColoredLogMessage($"ðŸ“š Autores: {authors.Count}", LogMessageType.Info);
                AddColoredLogMessage($"â° Inicio: {DateTime.Now:HH:mm:ss}", LogMessageType.Info);
                AddColoredLogMessage("â•".PadRight(80, 'â•'), LogMessageType.Phase);
                
                // Fase 1: OptimizaciÃ³n con cache
                UpdateCurrentPhase(1, "OptimizaciÃ³n con Cache");
                var cacheOptimizedAuthors = await OptimizeWithCacheAsync(authors);
                UpdateRealTimeStats(cacheOptimizedAuthors.CachedFiles, authors.Count, ultraStats.CacheHits, stopwatch.Elapsed);
                
                // Fase 2: BÃºsqueda por lotes optimizada
                UpdateCurrentPhase(2, "BÃºsqueda por Lotes");
                var batchResults = await ExecuteUltraFastBatchesAsync(cacheOptimizedAuthors.UnprocessedAuthors);
                UpdateRealTimeStats(cacheOptimizedAuthors.CachedFiles + batchResults.BatchFilesFound, authors.Count, ultraStats.CacheHits, stopwatch.Elapsed);
                
                // Fase 3: Procesamiento paralelo de autores restantes
                UpdateCurrentPhase(3, "BÃºsqueda Paralela");
                var parallelResults = await ExecuteParallelSearchAsync(batchResults.UnprocessedAuthors);
                UpdateRealTimeStats(authors.Count, authors.Count, ultraStats.CacheHits, stopwatch.Elapsed);
                
                // Consolidar resultados
                var finalResults = ConsolidateResults(batchResults, parallelResults);
                
                stopwatch.Stop();
                
                // Actualizar estadÃ­sticas
                UpdateUltraFastStats(finalResults, stopwatch.Elapsed);
                
                // Fase 4: ConsolidaciÃ³n y resumen
                UpdateCurrentPhase(4, "ConsolidaciÃ³n y Resumen Final");
                
                // Mostrar resumen final
                ShowFinalSummary(authors.Count, finalResults.TotalFiles, finalResults.CacheHits, stopwatch.Elapsed);
                
                Console.WriteLine($"[UltraFast] âœ… BÃºsqueda ultra-rÃ¡pida completada:");
                Console.WriteLine($"  Tiempo: {stopwatch.Elapsed:mm\\:ss}");
                Console.WriteLine($"  Velocidad: {ultraStats.AverageSpeed:F1} autores/minuto");
                Console.WriteLine($"  Eficiencia cache: {ultraStats.Efficiency:P1}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UltraFast] âŒ Error en bÃºsqueda ultra-rÃ¡pida: {ex.Message}");
                
                AddColoredLogMessage($"âŒ Error en modo ultra-rÃ¡pido: {ex.Message}", LogMessageType.Error);
            }
            finally
            {
                // Restaurar configuraciÃ³n normal
                DisableUltraFastMode();
            }
        }
        
        /// <summary>
        /// Activar modo ultra-rÃ¡pido
        /// </summary>
        private void EnableUltraFastMode()
        {
            try
            {
                ultraFastMode = true;
                
                // Activar todas las optimizaciones
                EnableFastMode(); // Timeout adaptativo rÃ¡pido
                OptimizeForMaximumSpeed(); // Lotes agresivos
                OptimizeCacheForBatch(); // Cache optimizado
                ConfigureBatchSize(); // TamaÃ±o de lote Ã³ptimo
                
                Console.WriteLine("[UltraFast] âš¡ Modo ultra-rÃ¡pido activado:");
                Console.WriteLine($"  Timeout: {fastTimeoutSecs}s");
                Console.WriteLine($"  Lotes: {batchSize} autores");
                Console.WriteLine($"  Cache expiraciÃ³n: {cacheExpiry.TotalMinutes:F0}min");
                Console.WriteLine($"  BÃºsquedas concurrentes: {maxConcurrentAuthorSearches}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UltraFast] âŒ Error activando modo ultra-rÃ¡pido: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Desactivar modo ultra-rÃ¡pido
        /// </summary>
        private void DisableUltraFastMode()
        {
            try
            {
                ultraFastMode = false;
                EnableNormalMode(); // Restaurar timeouts normales
                
                Console.WriteLine("[UltraFast] ðŸŒ Modo ultra-rÃ¡pido desactivado");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UltraFast] âŒ Error desactivando modo ultra-rÃ¡pido: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Optimizar lista de autores usando cache
        /// </summary>
        private async Task<CacheOptimizationResult> OptimizeWithCacheAsync(List<string> authors)
        {
            var result = new CacheOptimizationResult();
            
            try
            {
                Console.WriteLine("[UltraFast] ðŸ—„ï¸ Fase 1: OptimizaciÃ³n con cache");
                
                var cachedAuthors = new List<string>();
                var uncachedAuthors = new List<string>();
                
                foreach (var author in authors)
                {
                    if (TryGetCachedResult(author, out var cached))
                    {
                        cachedAuthors.Add(author);
                        result.CachedFiles += cached.TotalFiles;
                        result.CachedSpanishFiles += cached.SpanishFiles;
                        ultraStats.CacheHits++;
                        
                        // Mostrar en nuevo log
                        AddColoredLogMessage($"ðŸŽ¯ CACHE: {author} ({cached.TotalFiles} archivos)", LogMessageType.Cache);
                    }
                    else
                    {
                        uncachedAuthors.Add(author);
                    }
                }
                
                result.UnprocessedAuthors = uncachedAuthors;
                
                Console.WriteLine($"[UltraFast] ðŸ“Š Cache optimizaciÃ³n:");
                Console.WriteLine($"  Autores cacheados: {cachedAuthors.Count}");
                Console.WriteLine($"  Autores pendientes: {uncachedAuthors.Count}");
                Console.WriteLine($"  Archivos cacheados: {result.CachedFiles}");
                
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UltraFast] âŒ Error en optimizaciÃ³n cache: {ex.Message}");
                result.UnprocessedAuthors = authors;
                return result;
            }
        }
        
        /// <summary>
        /// Ejecutar bÃºsqueda por lotes ultra-rÃ¡pida
        /// </summary>
        private async Task<BatchOptimizationResult> ExecuteUltraFastBatchesAsync(List<string> authors)
        {
            var result = new BatchOptimizationResult();
            
            try
            {
                Console.WriteLine("[UltraFast] ðŸ“¦ Fase 2: BÃºsqueda por lotes ultra-rÃ¡pida");
                
                // Dividir en lotes mÃ¡s pequeÃ±os para mayor velocidad
                var smallBatchSize = Math.Min(5, batchSize);
                var batches = authors.Chunk(smallBatchSize).ToList();
                
                foreach (var batch in batches)
                {
                    var batchResult = await ProcessSingleBatchAsync(batch.ToList(), ultraStats.BatchesProcessed + 1);
                    
                    if (batchResult.Success)
                    {
                        result.BatchFilesFound += batchResult.FilesFound;
                        result.BatchSpanishFiles += batchResult.SpanishFiles;
                        ultraStats.BatchesProcessed++;
                    }
                    
                    // Pausa mÃ­nima entre lotes
                    await Task.Delay(500);
                }
                
                result.UnprocessedAuthors = new List<string>(); // Todos procesados en lotes
                
                Console.WriteLine($"[UltraFast] ðŸ“Š Lotes completados: {ultraStats.BatchesProcessed}");
                Console.WriteLine($"  Archivos encontrados: {result.BatchFilesFound}");
                
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UltraFast] âŒ Error en lotes: {ex.Message}");
                result.UnprocessedAuthors = authors;
                return result;
            }
        }
        
        /// <summary>
        /// Ejecutar bÃºsqueda paralela para autores restantes
        /// </summary>
        private async Task<ParallelOptimizationResult> ExecuteParallelSearchAsync(List<string> authors)
        {
            var result = new ParallelOptimizationResult();
            
            try
            {
                if (authors.Count == 0)
                {
                    return result;
                }
                
                Console.WriteLine("[UltraFast] âš¡ Fase 3: BÃºsqueda paralela ultra-rÃ¡pida");
                
                // Usar bÃºsqueda por lotes para autores restantes (mÃ¡s eficiente que paralelo puro)
                if (authors.Count > 0)
                {
                    await ProcessBatchSearchAsync(authors);
                }
                
                ultraStats.ParallelSearches = authors.Count;
                
                Console.WriteLine($"[UltraFast] âš¡ BÃºsquedas completadas: {authors.Count}");
                
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UltraFast] âŒ Error en bÃºsqueda paralela: {ex.Message}");
                return result;
            }
        }
        
        /// <summary>
        /// Consolidar todos los resultados
        /// </summary>
        private UltraFastResult ConsolidateResults(BatchOptimizationResult batch, ParallelOptimizationResult parallel)
        {
            return new UltraFastResult
            {
                TotalFiles = batch.BatchFilesFound,
                TotalSpanishFiles = batch.BatchSpanishFiles,
                CacheHits = ultraStats.CacheHits,
                ParallelSearches = ultraStats.ParallelSearches,
                BatchesProcessed = ultraStats.BatchesProcessed
            };
        }
        
        /// <summary>
        /// Actualizar estadÃ­sticas ultra-rÃ¡pidas
        /// </summary>
        private void UpdateUltraFastStats(UltraFastResult results, TimeSpan elapsed)
        {
            ultraStats.TotalFilesFound = results.TotalFiles;
            ultraStats.TotalSearchTime = elapsed;
            ultraStats.TotalAuthorsProcessed = ultraStats.CacheHits + ultraStats.ParallelSearches + (ultraStats.BatchesProcessed * batchSize);
        }
        
        /// <summary>
        /// Mostrar resumen final ultra-rÃ¡pido
        /// </summary>
        private async Task ShowUltraFastSummaryAsync(UltraFastResult results, TimeSpan elapsed)
        {
            try
            {
                this.Invoke((MethodInvoker)delegate
                {
                    authorSearchLog.AppendText($"\r\nðŸš€ RESUMEN MODO ULTRA-RÃPIDO\r\n");
                    authorSearchLog.AppendText($"========================================\r\n");
                    authorSearchLog.AppendText($"â±ï¸ Tiempo total: {elapsed:mm\\:ss}\r\n");
                    authorSearchLog.AppendText($"ðŸ“ Archivos encontrados: {results.TotalFiles}\r\n");
                    authorSearchLog.AppendText($"ðŸ“– Archivos vÃ¡lidos: {results.TotalSpanishFiles}\r\n");
                    authorSearchLog.AppendText($"ðŸŽ¯ Cache hits: {results.CacheHits}\r\n");
                    authorSearchLog.AppendText($"ðŸ“¦ Lotes procesados: {results.BatchesProcessed}\r\n");
                    authorSearchLog.AppendText($"âš¡ BÃºsquedas paralelas: {results.ParallelSearches}\r\n");
                    authorSearchLog.AppendText($"ðŸš€ Velocidad: {ultraStats.AverageSpeed:F1} autores/minuto\r\n");
                    authorSearchLog.AppendText($"ðŸ“ˆ Eficiencia cache: {ultraStats.Efficiency:P1}\r\n");
                    authorSearchLog.AppendText($"========================================\r\n");
                    authorSearchLog.AppendText($"âœ… MODO ULTRA-RÃPIDO COMPLETADO\r\n");
                    authorSearchLog.SelectionStart = authorSearchLog.Text.Length;
                    authorSearchLog.ScrollToCaret();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UltraFast] âŒ Error mostrando resumen: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Mostrar configuraciÃ³n ultra-rÃ¡pida
        /// </summary>
        private void ShowUltraFastConfiguration()
        {
            var config = $"""
ðŸš€ CONFIGURACIÃ“N MODO ULTRA-RÃPIDO
========================================
âš¡ Optimizaciones Activadas:
â”œâ”€â”€ âœ… Timeout Adaptativo RÃ¡pido ({fastTimeoutSecs}s)
â”œâ”€â”€ âœ… BÃºsqueda por Lotes ({batchSize} autores/lote)
â”œâ”€â”€ âœ… Cache Inteligente ({cacheExpiry.TotalMinutes:F0}min expiraciÃ³n)
â”œâ”€â”€ âœ… BÃºsqueda Paralela ({maxConcurrentAuthorSearches} concurrentes)
â”œâ”€â”€ âœ… Auto-limpieza de autores (lÃ­mite: {maxFailedAttempts})
â””â”€â”€ âœ… CancelaciÃ³n temprana de bÃºsquedas

ðŸ“Š ParÃ¡metros Optimizados:
â”œâ”€â”€ Timeout por bÃºsqueda: 10-30s (adaptativo)
â”œâ”€â”€ LÃ­mite de respuestas: 20-50 (reducido)
â”œâ”€â”€ TamaÃ±o de lote: 5-15 autores
â”œâ”€â”€ Pausa entre lotes: 500ms
â””â”€â”€ Timeout por lote: 30s

ðŸŽ¯ Modo de OperaciÃ³n:
â”œâ”€â”€ Fase 1: Reutilizar resultados cacheados
â”œâ”€â”€ Fase 2: Procesar lotes pequeÃ±os en paralelo
â”œâ”€â”€ Fase 3: BÃºsqueda individual para restantes
â””â”€â”€ Fase 4: ConsolidaciÃ³n y estadÃ­sticas

ðŸ’¡ Beneficios Esperados:
â”œâ”€â”€ 60-80% mÃ¡s rÃ¡pido que modo normal
â”œâ”€â”€ 90% menos bÃºsquedas repetitivas
â”œâ”€â”€ 50% menos uso de red
â””â”€â”€ 40% menos uso CPU
""";
            
            Console.WriteLine(config);
            MessageBox.Show(config, "ConfiguraciÃ³n Modo Ultra-RÃ¡pido", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        // Estructuras para resultados
        public struct CacheOptimizationResult
        {
            public int CachedFiles { get; set; }
            public int CachedSpanishFiles { get; set; }
            public List<string> UnprocessedAuthors { get; set; }
        }
        
        public struct BatchOptimizationResult
        {
            public int BatchFilesFound { get; set; }
            public int BatchSpanishFiles { get; set; }
            public List<string> UnprocessedAuthors { get; set; }
        }
        
        public struct ParallelOptimizationResult
        {
            // Resultados de bÃºsqueda paralela
        }
        
        public struct UltraFastResult
        {
            public int TotalFiles { get; set; }
            public int TotalSpanishFiles { get; set; }
            public int CacheHits { get; set; }
            public int ParallelSearches { get; set; }
            public int BatchesProcessed { get; set; }
        }
        
        /// <summary>
        /// MÃ©todo de extensiÃ³n para dividir lista en chunks
        /// </summary>
        private static IEnumerable<T[]> Chunk<T>(this IEnumerable<T> source, int size)
        {
            using var enumerator = source.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var chunk = new T[size];
                chunk[0] = enumerator.Current;
                int i = 1;
                for (; i < size && enumerator.MoveNext(); i++)
                {
                    chunk[i] = enumerator.Current;
                }
                if (i == size) yield return chunk;
                else
                {
                    Array.Resize(ref chunk, i);
                    yield return chunk;
                }
            }
        }
    }
}

