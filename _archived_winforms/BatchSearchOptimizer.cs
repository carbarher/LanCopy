using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// Sistema de bÃºsqueda por lotes para mÃ¡xima eficiencia
    /// </summary>
    public partial class MainForm
    {
        // ConfiguraciÃ³n de lotes
        private static int batchSize = 10; // Autores por lote
        private static int batchTimeoutSecs = 45; // Timeout por lote
        private static bool batchModeEnabled = true;
        
        /// <summary>
        /// Procesar autores en lotes optimizados
        /// </summary>
        private async Task ProcessBatchSearchAsync(List<string> authors)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var totalBatches = (int)Math.Ceiling((double)authors.Count / batchSize);
                
                Console.WriteLine($"[BatchSearch] ðŸš€ Iniciando bÃºsqueda por lotes:");
                Console.WriteLine($"  Autores totales: {authors.Count}");
                Console.WriteLine($"  Lotes: {totalBatches} ({batchSize} autores/lote)");
                Console.WriteLine($"  Timeout por lote: {batchTimeoutSecs}s");
                
                // Mostrar inicio en UI
                this.Invoke((MethodInvoker)delegate
                {
                    authorSearchLog.AppendText($"\r\nðŸš€ BÃšSQUEDA POR LOTES ACTIVADA\r\n");
                    authorSearchLog.AppendText($"ðŸ“¦ Autores: {authors.Count} | Lotes: {totalBatches} | TamaÃ±o lote: {batchSize}\r\n");
                    authorSearchLog.AppendText($"â±ï¸ Timeout por lote: {batchTimeoutSecs}s | Modo: {(batchModeEnabled ? "Optimizado" : "Normal")}\r\n");
                    authorSearchLog.AppendText($"â° Inicio: {DateTime.Now:HH:mm:ss}\r\n");
                    authorSearchLog.AppendText($"========================================\r\n");
                    authorSearchLog.SelectionStart = authorSearchLog.Text.Length;
                    authorSearchLog.ScrollToCaret();
                });
                
                var totalFilesFound = 0;
                var totalSpanishFiles = 0;
                var successfulBatches = 0;
                var failedBatches = 0;
                
                // Procesar lotes secuencialmente
                for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
                {
                    var batchAuthors = authors.Skip(batchIndex * batchSize).Take(batchSize).ToList();
                    
                    this.Invoke((MethodInvoker)delegate
                    {
                        authorSearchLog.AppendText($"\r\nðŸ“¦ LOTE {batchIndex + 1}/{totalBatches}: {string.Join(", ", batchAuthors)}\r\n");
                        authorSearchLog.AppendText($"â° {DateTime.Now:HH:mm:ss}\r\n");
                        authorSearchLog.SelectionStart = authorSearchLog.Text.Length;
                        authorSearchLog.ScrollToCaret();
                    });
                    
                    var batchResult = await ProcessSingleBatchAsync(batchAuthors, batchIndex + 1);
                    
                    if (batchResult.Success)
                    {
                        successfulBatches++;
                        totalFilesFound += batchResult.FilesFound;
                        totalSpanishFiles += batchResult.SpanishFiles;
                        
                        this.Invoke((MethodInvoker)delegate
                        {
                            authorSearchLog.AppendText($"  âœ… Lote completado: {batchResult.FilesFound} archivos ({batchResult.SearchTime.TotalSeconds:F1}s)\r\n");
                        });
                    }
                    else
                    {
                        failedBatches++;
                        
                        this.Invoke((MethodInvoker)delegate
                        {
                            authorSearchLog.AppendText($"  âŒ Lote fallido: {batchResult.Error}\r\n");
                        });
                    }
                    
                    // PequeÃ±a pausa entre lotes para no sobrecargar
                    if (batchIndex < totalBatches - 1)
                    {
                        await Task.Delay(1000);
                    }
                }
                
                // Resumen final
                stopwatch.Stop();
                
                this.Invoke((MethodInvoker)delegate
                {
                    authorSearchLog.AppendText($"\r\nðŸ“Š RESUMEN DE BÃšSQUEDA POR LOTES\r\n");
                    authorSearchLog.AppendText($"========================================\r\n");
                    authorSearchLog.AppendText($"â±ï¸ Tiempo total: {stopwatch.Elapsed:mm\\:ss}\r\n");
                    authorSearchLog.AppendText($"ðŸ“¦ Lotes exitosos: {successfulBatches}/{totalBatches}\r\n");
                    authorSearchLog.AppendText($"ðŸ“¦ Lotes fallidos: {failedBatches}/{totalBatches}\r\n");
                    authorSearchLog.AppendText($"ðŸ“ Archivos encontrados: {totalFilesFound}\r\n");
                    authorSearchLog.AppendText($"ðŸ“– Archivos vÃ¡lidos: {totalSpanishFiles}\r\n");
                    authorSearchLog.AppendText($"âš¡ Velocidad promedio: {(totalFilesFound / Math.Max(1, stopwatch.Elapsed.TotalMinutes)):F1} archivos/minuto\r\n");
                    authorSearchLog.AppendText($"ðŸ“ˆ Eficiencia: {(successfulBatches * 100 / Math.Max(1, totalBatches))}%\r\n");
                    authorSearchLog.SelectionStart = authorSearchLog.Text.Length;
                    authorSearchLog.ScrollToCaret();
                });
                
                Console.WriteLine($"[BatchSearch] âœ… BÃºsqueda por lotes completada:");
                Console.WriteLine($"  Tiempo: {stopwatch.Elapsed:mm\\:ss}");
                Console.WriteLine($"  Archivos: {totalFilesFound}");
                Console.WriteLine($"  Eficiencia: {successfulBatches * 100 / totalBatches}%");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BatchSearch] âŒ Error en bÃºsqueda por lotes: {ex.Message}");
                
                this.Invoke((MethodInvoker)delegate
                {
                    authorSearchLog.AppendText($"âŒ Error en bÃºsqueda por lotes: {ex.Message}\r\n");
                });
            }
        }
        
        /// <summary>
        /// Procesar un lote individual de autores
        /// </summary>
        private async Task<BatchResult> ProcessSingleBatchAsync(List<string> batchAuthors, int batchNumber)
        {
            var result = new BatchResult { Success = false };
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var batchFilesFound = 0;
                var batchSpanishFiles = 0;
                
                // Usar timeout adaptativo para el lote
                var batchTimeout = TimeSpan.FromSeconds(batchTimeoutSecs);
                
                using var cts = new System.Threading.CancellationTokenSource(batchTimeout);
                
                // Crear tareas para todos los autores del lote
                var authorTasks = batchAuthors.Select(author => SearchAuthorWithTimeoutAsync(author, cts.Token)).ToArray();
                
                // Esperar a que todas las bÃºsquedas del lote completen
                var completedTasks = await Task.WhenAll(authorTasks);
                
                // Procesar resultados del lote
                foreach (var taskResult in completedTasks)
                {
                    if (taskResult.Success)
                    {
                        batchFilesFound += taskResult.FilesFound;
                        batchSpanishFiles += taskResult.SpanishFiles;
                        
                        // Actualizar cache
                        AddToCache(taskResult.Author, taskResult.FilesFound, taskResult.SpanishFiles, 0);
                        
                        // Actualizar timeouts adaptativos
                        UpdateTimeoutStats(taskResult.Author, true, taskResult.SearchTime.TotalSeconds, taskResult.FilesFound);
                    }
                    else
                    {
                        // Actualizar timeouts para autores fallidos
                        UpdateTimeoutStats(taskResult.Author, false, batchTimeoutSecs, 0);
                    }
                }
                
                stopwatch.Stop();
                
                result = new BatchResult
                {
                    Success = true,
                    FilesFound = batchFilesFound,
                    SpanishFiles = batchSpanishFiles,
                    SearchTime = stopwatch.Elapsed,
                    BatchNumber = batchNumber,
                    AuthorsProcessed = batchAuthors.Count
                };
                
                Console.WriteLine($"[BatchSearch] ðŸ“¦ Lote {batchNumber}: {batchFilesFound} archivos en {stopwatch.Elapsed.TotalSeconds:F1}s");
            }
            catch (OperationCanceledException)
            {
                result.Error = $"Timeout del lote ({batchTimeoutSecs}s)";
                Console.WriteLine($"[BatchSearch] â° Lote {batchNumber}: Timeout alcanzado");
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                Console.WriteLine($"[BatchSearch] âŒ Lote {batchNumber}: {ex.Message}");
            }
            
            return result;
        }
        
        /// <summary>
        /// BÃºsqueda de autor con timeout y optimizaciones
        /// </summary>
        private async Task<AuthorSearchResult> SearchAuthorWithTimeoutAsync(string author, System.Threading.CancellationToken cancellationToken)
        {
            var result = new AuthorSearchResult { Author = author };
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Verificar cache primero
                if (TryGetCachedResult(author, out var cachedResult))
                {
                    stopwatch.Stop();
                    
                    return new AuthorSearchResult
                    {
                        Author = author,
                        TotalFiles = cachedResult.TotalFiles,
                        SpanishFiles = cachedResult.SpanishFiles,
                        Responses = cachedResult.Responses,
                        Success = true,
                        SearchTime = stopwatch.Elapsed,
                        FromCache = true
                    };
                }
                
                // Obtener timeout optimizado para este autor
                var optimalTimeout = GetOptimalTimeout(author);
                
                // Realizar bÃºsqueda real
                var searchResult = await PerformOptimizedAuthorSearchAsync(author, optimalTimeout, cancellationToken);
                
                stopwatch.Stop();
                
                result = new AuthorSearchResult
                {
                    Author = author,
                    TotalFiles = searchResult.TotalFiles,
                    SpanishFiles = searchResult.SpanishFiles,
                    Responses = searchResult.Responses,
                    Success = searchResult.Success,
                    Error = searchResult.Error,
                    SearchTime = stopwatch.Elapsed,
                    FromCache = false
                };
                
                // Agregar al cache si tuvo Ã©xito
                if (result.Success && result.TotalFiles > 0)
                {
                    AddToCache(author, result.TotalFiles, result.SpanishFiles, result.Responses);
                }
            }
            catch (OperationCanceledException)
            {
                result.Error = "BÃºsqueda cancelada por timeout";
                result.Success = false;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.Success = false;
            }
            
            return result;
        }
        
        /// <summary>
        /// BÃºsqueda optimizada de autor
        /// </summary>
        private async Task<AdaptiveTimeout.AuthorSearchResult> PerformOptimizedAuthorSearchAsync(string author, int timeoutSecs, System.Threading.CancellationToken cancellationToken)
        {
            var result = new AdaptiveTimeout.AuthorSearchResult { Author = author };
            
            try
            {
                if (client?.State != SoulseekClientStates.Connected && client?.State != SoulseekClientStates.LoggedIn)
                {
                    throw new InvalidOperationException("No conectado a Soulseek");
                }
                
                var searchQuery = SearchQuery.FromText(author);
                var timeout = timeoutSecs * 1000;
                
                // Opciones optimizadas para velocidad
                var searchOptions = new SearchOptions(
                    searchTimeout: timeout,
                    responseLimit: 30, // Reducido para velocidad
                    fileLimit: 100     // Reducido para velocidad
                );
                
                int spanishFiles = 0;
                int totalFiles = 0;
                int responses = 0;
                
                // Procesar respuestas con cancelaciÃ³n
                var responsesTask = client.SearchAsync(searchQuery, searchOptions, cancellationToken);
                
                await foreach (var response in responsesTask.WithCancellation(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    responses++;
                    
                    foreach (var file in response.Files)
                    {
                        totalFiles++;
                        
                        // Filtro rÃ¡pido
                        if (IsSpanishFile(file.Filename))
                        {
                            spanishFiles++;
                        }
                    }
                    
                    // LÃ­mite temprano para velocidad
                    if (responses >= 20) break;
                }
                
                result = new AdaptiveTimeout.AuthorSearchResult
                {
                    Author = author,
                    TotalFiles = totalFiles,
                    SpanishFiles = spanishFiles,
                    Responses = responses,
                    Success = true
                };
            }
            catch (OperationCanceledException)
            {
                result = new AdaptiveTimeout.AuthorSearchResult
                {
                    Author = author,
                    Success = false,
                    Error = "BÃºsqueda cancelada"
                };
            }
            catch (Exception ex)
            {
                result = new AdaptiveTimeout.AuthorSearchResult
                {
                    Author = author,
                    Success = false,
                    Error = ex.Message
                };
            }
            
            return result;
        }
        
        /// <summary>
        /// Resultado de procesamiento de lote
        /// </summary>
        public struct BatchResult
        {
            public bool Success { get; set; }
            public int FilesFound { get; set; }
            public int SpanishFiles { get; set; }
            public TimeSpan SearchTime { get; set; }
            public int BatchNumber { get; set; }
            public int AuthorsProcessed { get; set; }
            public string Error { get; set; }
        }
        
        /// <summary>
        /// Resultado extendido de bÃºsqueda de autor
        /// </summary>
        public struct AuthorSearchResult
        {
            public string Author { get; set; }
            public int TotalFiles { get; set; }
            public int SpanishFiles { get; set; }
            public int Responses { get; set; }
            public bool Success { get; set; }
            public string Error { get; set; }
            public TimeSpan SearchTime { get; set; }
            public bool FromCache { get; set; }
        }
        
        /// <summary>
        /// Configurar tamaÃ±o de lote Ã³ptimo
        /// </summary>
        private void ConfigureBatchSize()
        {
            try
            {
                // Ajustar basado en rendimiento del sistema
                var memoryMB = GC.GetTotalMemory(false) / 1024 / 1024;
                
                if (memoryMB < 100)
                {
                    batchSize = 5; // Sistema con poca memoria
                }
                else if (memoryMB < 500)
                {
                    batchSize = 10; // Sistema normal
                }
                else
                {
                    batchSize = 20; // Sistema con mucha memoria
                }
                
                Console.WriteLine($"[BatchSearch] ðŸ“Š TamaÃ±o de lote configurado: {batchSize} (memoria: {memoryMB}MB)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BatchSearch] âŒ Error configurando lote: {ex.Message}");
                batchSize = 10; // Valor por defecto seguro
            }
        }
        
        /// <summary>
        /// Optimizar parÃ¡metros para mÃ¡xima velocidad
        /// </summary>
        private void OptimizeForMaximumSpeed()
        {
            try
            {
                batchModeEnabled = true;
                batchSize = 15; // Lotes mÃ¡s grandes
                batchTimeoutSecs = 30; // Timeout mÃ¡s agresivo
                
                Console.WriteLine($"[BatchSearch] ðŸš€ OptimizaciÃ³n mÃ¡xima velocidad activada:");
                Console.WriteLine($"  Lotes: {batchSize} autores");
                Console.WriteLine($"  Timeout: {batchTimeoutSecs}s por lote");
                Console.WriteLine($"  Modo: Optimizado");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BatchSearch] âŒ Error en optimizaciÃ³n: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Mostrar estadÃ­sticas de lotes
        /// </summary>
        private void ShowBatchStatistics()
        {
            var stats = $"""
ðŸ“Š ESTADÃSTICAS DE BÃšSQUEDA POR LOTES
========================================
âš™ï¸ ConfiguraciÃ³n Actual:
â”œâ”€â”€ TamaÃ±o de lote: {batchSize} autores
â”œâ”€â”€ Timeout por lote: {batchTimeoutSecs}s
â”œâ”€â”€ Modo optimizado: {(batchModeEnabled ? "SÃ­" : "No")}
â””â”€â”€ Memoria disponible: {GC.GetTotalMemory(false) / 1024 / 1024}MB

ðŸš€ Optimizaciones Aplicadas:
â”œâ”€â”€ Procesamiento paralelo por lote
â”œâ”€â”€ Timeout adaptativo por autor
â”œâ”€â”€ Cache inteligente de resultados
â”œâ”€â”€ CancelaciÃ³n temprana de lotes
â””â”€â”€ LÃ­mites de respuesta agresivos

ðŸ’¡ Recomendaciones:
â”œâ”€â”€ Usar lotes de 10-15 autores para balance velocidad/estabilidad
â”œâ”€â”€ Timeout de 30s para mayorÃ­a de casos
â”œâ”€â”€ Activar cache para bÃºsquedas repetitivas
â””â”€â”€ Monitorizar uso de memoria
""";
            
            Console.WriteLine(stats);
            MessageBox.Show(stats, "EstadÃ­sticas de BÃºsqueda por Lotes", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}

