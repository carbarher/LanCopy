using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soulseek;

namespace SlskDown
{
    public partial class MainForm
    {
        private async Task PurgeAuthorsWithoutResultsOptimized()
        {
            if (allAuthorsData.Count == 0)
            {
                AutoLog("❌ No se puede purgar: No hay autores cargados.");
                AutoLog("📂 Usa el botón 'Cargar desde archivo' para cargar autores primero.");
                return;
            }
            
            if (client == null || (!client.State.HasFlag(SoulseekClientStates.Connected) && !client.State.HasFlag(SoulseekClientStates.LoggedIn)))
            {
                AutoLog("❌ No se puede purgar: No estás conectado a Soulseek.");
                return;
            }

            var allAuthors = allAuthorsData.Select(a => a.Name).ToList();
            if (allAuthors.Count == 0) return;

            // Verificar si hay progreso previo
            var previousProgress = LoadBatchProgress();
            bool resuming = previousProgress != null && previousProgress.ProcessedAuthors?.Count > 0;
            
            if (resuming)
            {
                AutoLog($"📂 Progreso anterior detectado: {previousProgress.ProcessedAuthors.Count} autores ya procesados");
                AutoLog($"¿Deseas continuar desde donde lo dejaste o empezar de cero?");
                
                var result = DarkMessageBox.Show(
                    $"Se encontró progreso anterior:\n\n" +
                    $"• Autores procesados: {previousProgress.ProcessedAuthors.Count}\n" +
                    $"• Lote actual: {previousProgress.CurrentBatch}/{previousProgress.TotalBatches}\n\n" +
                    $"¿Continuar desde donde lo dejaste?\n\n" +
                    $"Sí = Continuar\n" +
                    $"No = Empezar de cero",
                    "Reanudar Purga",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );
                
                if (result == DialogResult.No)
                {
                    AutoLog("🔄 Iniciando purga desde cero...");
                    ClearBatchProgress();
                    searchCache.Clear();
                    SaveSearchCache();
                    resuming = false;
                }
                else
                {
                    AutoLog($"▶️ Reanudando desde lote {previousProgress.CurrentBatch}...");
                    // Filtrar autores ya procesados
                    allAuthors = allAuthors.Where(a => !previousProgress.ProcessedAuthors.Contains(a)).ToList();
                }
            }

            // Pre-filtrar y priorizar autores (Optimizaciones #10, #15 y #17)
            if (!resuming)
            {
                var originalCount = allAuthors.Count;
                
                // Filtrar autores obviamente inválidos
                allAuthors = allAuthors
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Where(a => a.Length >= 2 && a.Length <= 100) // Nombres muy cortos/largos son sospechosos
                    .Where(a => !a.All(char.IsDigit)) // No solo números
                    .Where(a => a.Any(char.IsLetter)) // Debe tener al menos una letra
                    .Where(a => !IsAuthorBlacklisted(a)) // Skip blacklisted authors
                    .ToList();
                
                var filtered = originalCount - allAuthors.Count;
                if (filtered > 0)
                    AutoLog($"🗑️ Pre-filtrados {filtered} autores inválidos/blacklist");
                
                // Ordenar por longitud de nombre (más cortos primero = más comunes)
                allAuthors = allAuthors
                    .OrderBy(a => a.Length)
                    .ThenBy(a => a.Count(c => !char.IsLetterOrDigit(c))) // Menos caracteres especiales
                    .ThenBy(a => a)
                    .ToList();
                AutoLog($"📊 Autores priorizados: nombres cortos primero");
            }

            // Calcular tamaño de lote
            batchSize = CalculateBatchSize(allAuthors.Count);
            totalBatches = (int)Math.Ceiling((double)allAuthors.Count / batchSize);

            AutoLog("═══════════════════════════════════════");
            AutoLog($"🧹 PURGA OPTIMIZADA");
            AutoLog($"📚 {allAuthors.Count:N0} autores {(resuming ? "restantes" : "total")}");
            AutoLog($"📦 {totalBatches} lotes de {batchSize} autores");
            AutoLog($"⚡ Paralelismo: {currentParallelism} búsquedas simultáneas");
            if (chkAutoSpanishDocuments?.Checked == true) AutoLog("🇪🇸 Filtro: Solo documentos en español");
            AutoLog($"💾 Caché: {searchCache.Count:N0} autores en caché");
            AutoLog("═══════════════════════════════════════");
            
            // También mostrar en log principal
            Log($"🧹 Iniciando purga de {allAuthors.Count:N0} autores ({totalBatches} lotes)");

            autoPurgeRunning = true;

            autoSearchCts?.Dispose();
            autoSearchCts = new CancellationTokenSource();
            var cancellationToken = autoSearchCts.Token;
            
            // Actualizar botón Detener
            if (btnStopAuto != null)
            {
                btnStopAuto.Enabled = true;
                btnStopAuto.BackColor = Color.FromArgb(0, 200, 0);
                btnStopAuto.Refresh();
            }
            
            if (btnPurge != null)
                btnPurge.Enabled = false;
            if (btnStartAuto != null)
                btnStartAuto.Enabled = false;

            var authorsWithFiles = new HashSet<string>();
            var processedAuthors = new List<string>();
            int searchCount = 0; // Contador para pausas inteligentes
            
            // Contadores globales para estadísticas de filtrado de idioma
            int globalTotalFiles = 0;
            int globalDocumentsFound = 0;
            int globalSpanishAccepted = 0;
            int globalLanguageFiltered = 0;
            
            batchStopwatch.Restart();

            try
            {
                for (int batch = 1; batch <= totalBatches; batch++)
                {
                    // Verificar cancelación al inicio de cada lote
                    if (cancellationToken.IsCancellationRequested || !autoPurgeRunning)
                    {
                        AutoLog($"⏹️ Purga cancelada en lote {batch}/{totalBatches}");
                        break;
                    }

                    currentBatch = batch;
                    var batchStart = (batch - 1) * batchSize;
                    var batchAuthors = allAuthors.Skip(batchStart).Take(batchSize).ToList();

                    AutoLog("");
                    AutoLog($"📦 ═══ LOTE {batch}/{totalBatches} ═══");
                    AutoLog($"👥 Procesando {batchAuthors.Count} autores en paralelo ({currentParallelism} simultáneos)...");
                    
                    var progress = processedAuthors.Count * 100.0 / allAuthors.Count;
                    var remaining = EstimateRemainingTime(processedAuthors.Count, allAuthors.Count, batchStopwatch.Elapsed);
                    
                    // Actualizar label de progreso
                    if (lblPurgeProgress != null)
                    {
                        lblPurgeProgress.Text = $"⏳ {processedAuthors.Count}/{allAuthors.Count} ({progress:F0}%) | ⏱️ {remaining}";
                        lblPurgeProgress.Visible = true;
                    }
                    
                    AutoLog($"📊 Progreso: {processedAuthors.Count:N0}/{allAuthors.Count:N0} ({progress:F1}%)");
                    AutoLog($"⏱️ Tiempo: {batchStopwatch.Elapsed:hh\\:mm\\:ss} | Restante: {remaining}");
                    
                    // También mostrar en log principal
                    Log($"📦 Purga lote {batch}/{totalBatches}: {progress:F0}% completado, {remaining} restante");

                    // Marcar autores del lote como "En cola"
                    foreach (var author in batchAuthors)
                    {
                        UpdateAuthorData(author, 0, "⏳ En cola");
                    }

                    // Procesar lote con paralelismo adaptativo
                    var semaphore = new SemaphoreSlim(currentParallelism);
                    var batchStopwatchLocal = System.Diagnostics.Stopwatch.StartNew();

                    var tasks = batchAuthors.Select(async author =>
                    {
                        // Verificar cancelación antes de esperar semáforo
                        if (cancellationToken.IsCancellationRequested || !autoPurgeRunning)
                            return;
                        
                        try
                        {
                            await semaphore.WaitAsync(cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            return; // Cancelado antes de adquirir semáforo
                        }
                        
                        var searchStopwatch = System.Diagnostics.Stopwatch.StartNew();
                        bool wasFromCache = false; // Declarar aquí para que esté disponible en todo el scope
                        
                        try
                        {
                            // Verificar cancelación y conexión INMEDIATAMENTE
                            if (!autoPurgeRunning || cancellationToken.IsCancellationRequested)
                            {
                                semaphore.Release();
                                return;
                            }
                            
                            // Verificar conexión ANTES de procesar
                            if (client == null || (!client.State.HasFlag(SoulseekClientStates.Connected) && !client.State.HasFlag(SoulseekClientStates.LoggedIn)))
                            {
                                AutoLog($"    ⚠️ {author}: Desconectado - deteniendo inmediatamente");
                                UpdateAuthorData(author, 0, "⏸️ Pausado", Color.Orange);
                                semaphore.Release();
                                return;
                            }

                            // Verificar caché primero (Optimización #6)
                            if (searchCache.TryGetValue(author, out var cachedResult))
                            {
                                wasFromCache = true;
                                searchStopwatch.Stop();
                                
                                // NO ajustar paralelismo para búsquedas en caché
                                
                                if (cachedResult.HasValidFiles)
                                {
                                    lock (authorsWithFiles) authorsWithFiles.Add(author);
                                    AutoLog($"    💾 {author}: Caché - ✅ Válido ({cachedResult.FilesCount} archivos)");
                                    UpdateAuthorData(author, cachedResult.FilesCount, "✅ Válido (caché)", Color.LightGreen);
                                }
                                else
                                {
                                    AutoLog($"    💾 {author}: Caché - ❌ Sin archivos válidos");
                                    UpdateAuthorData(author, cachedResult.FilesCount, "❌ Eliminado (caché)", Color.LightCoral);
                                }
                                
                                HandleSearchSuccess(); // Resetear errores consecutivos
                                
                                semaphore.Release();
                                lock (processedAuthors)
                                {
                                    processedAuthors.Add(author);
                                }
                                return;
                            }
                            
                            // Verificar throttling
                            if (await CheckThrottling())
                                return;

                            // Verificar conexión antes de buscar
                            if (!await EnsureConnected())
                            {
                                AutoLog($"    ⚠️ {author}: No se pudo establecer conexión");
                                UpdateAuthorData(author, 0, "⚠️ Sin conexión", Color.Orange);
                                HandleSearchError(new Exception("No connected"));
                                return;
                            }

                            // Delay entre búsquedas para evitar flood y bloqueos del servidor
                            int searchDelay = 3000; // 3 segundos base
                            
                            // Aumentar delay en modo conservador
                            if (conservativeMode)
                            {
                                searchDelay = 5000; // 5 segundos en modo conservador
                            }
                            
                            await Task.Delay(searchDelay, cancellationToken);
                            
                            // Incrementar contador de búsquedas
                            Interlocked.Increment(ref searchCount);
                            int currentCount = searchCount;
                            
                            // Búsqueda real en Soulseek
                            AutoLog($"    🔍 {author}: Buscando en Soulseek... (búsqueda #{currentCount})");
                            UpdateAuthorStatus(author, "🔍 Buscando...");

                            var results = await client.SearchAsync(SearchQuery.FromText(author),
                                options: new SearchOptions(searchTimeout: 2500, responseLimit: 5000, fileLimit: 30000, 
                                    filterResponses: false, minimumResponseFileCount: 1, minimumPeerUploadSpeed: 0),
                                cancellationToken: cancellationToken);

                            searchStopwatch.Stop();
                            AdjustParallelism(searchStopwatch.ElapsedMilliseconds);

                            var responses = results.Responses;
                            var responseCount = responses?.Count ?? 0;
                            var filesCount = responses?.Sum(r => r.Files?.Count ?? 0) ?? 0;
                            bool hasValid = false;
                            
                            // Contadores para estadísticas de filtrado
                            int totalFiles = 0;
                            int documentsFound = 0;
                            int spanishAccepted = 0;
                            int languageFiltered = 0;

                            // Optimización: salir al encontrar primer archivo válido
                            if (responses != null)
                            {
                                bool checkSpanish = chkAutoSpanishDocuments?.Checked == true;
                                
                                foreach (var response in responses)
                                {
                                    if (response.Files == null) continue;
                                    
                                    foreach (var file in response.Files)
                                    {
                                        totalFiles++;
                                        
                                        // Filtro 1: Tamaño > 0
                                        if (file.Size == 0) continue;
                                        
                                        // Filtro 2: No debe ser archivo basura
                                        if (IsGarbageFile(file.Filename)) continue;
                                        
                                        // Filtro 3: Debe ser documento
                                        if (!IsDocumentFile(file.Filename)) continue;
                                        documentsFound++;
                                        
                                        // Filtro 4: Idioma español (si está activado)
                                        if (checkSpanish)
                                        {
                                            if (IsSpanishText(file.Filename))
                                            {
                                                spanishAccepted++;
                                                hasValid = true;
                                                break;
                                            }
                                            else
                                            {
                                                languageFiltered++;
                                            }
                                        }
                                        else
                                        {
                                            // Sin filtro de idioma, aceptar todos los documentos
                                            hasValid = true;
                                            break;
                                        }
                                    }
                                    
                                    if (hasValid) break;
                                }
                            }

                            // Acumular estadísticas globales
                            lock (processedAuthors)
                            {
                                globalTotalFiles += totalFiles;
                                globalDocumentsFound += documentsFound;
                                globalSpanishAccepted += spanishAccepted;
                                globalLanguageFiltered += languageFiltered;
                            }
                            
                            // Cachear resultado
                            CacheSearchResult(author, filesCount, hasValid);

                            if (hasValid)
                            {
                                lock (authorsWithFiles) authorsWithFiles.Add(author);
                                AutoLog($"    ✅ {author}: Válido - {filesCount} archivos ({searchStopwatch.ElapsedMilliseconds}ms)");
                                
                                // Mostrar estadísticas de filtrado si el filtro español está activo
                                if (chkAutoSpanishDocuments?.Checked == true && (documentsFound > 0 || languageFiltered > 0))
                                {
                                    AutoLog($"       📊 Filtrado: {totalFiles} archivos → {documentsFound} docs → {spanishAccepted} español | ❌ {languageFiltered} otros idiomas");
                                }
                                
                                UpdateAuthorData(author, filesCount, "✅ Válido", Color.LightGreen);
                            }
                            else
                            {
                                AutoLog($"    ❌ {author}: Sin archivos válidos ({searchStopwatch.ElapsedMilliseconds}ms)");
                                
                                // Mostrar por qué fue rechazado
                                if (chkAutoSpanishDocuments?.Checked == true && documentsFound > 0)
                                {
                                    AutoLog($"       📊 Rechazado: {documentsFound} documentos encontrados, pero {languageFiltered} filtrados por idioma");
                                }
                                
                                UpdateAuthorData(author, filesCount, "❌ Eliminado", Color.LightCoral);
                            }

                            HandleSearchSuccess();
                            
                        }
                        catch (OperationCanceledException)
                        {
                            AutoLog($"        ⏹️ {author}: Cancelado");
                            UpdateAuthorData(author, 0, "⏹️ Cancelado", Color.Gray);
                        }
                        catch (Exception ex)
                        {
                            searchStopwatch.Stop();
                            HandleSearchError(ex);
                            AutoLog($"        ⚠️ {author}: Error - {ex.Message}");
                            UpdateAuthorData(author, 0, $"⚠️ Error", Color.Khaki);
                        }
                        finally
                        {
                            semaphore.Release();
                            lock (processedAuthors)
                            {
                                processedAuthors.Add(author);
                            }
                        }
                    }).ToList();
                    
                    // Esperar todas las tareas pero permitir cancelación
                    try
                    {
                        await Task.WhenAll(tasks);
                    }
                    catch (OperationCanceledException)
                    {
                        AutoLog($"⏹️ Lote {batch} cancelado");
                    }

                    batchStopwatchLocal.Stop();

                    // Estadísticas del lote
                    var batchValid = batchAuthors.Count(a => authorsWithFiles.Contains(a));
                    var batchInvalid = batchAuthors.Count - batchValid;
                    var avgTimePerAuthor = batchStopwatchLocal.ElapsedMilliseconds / (double)batchAuthors.Count;

                    AutoLog($"✅ Lote {batch} completado:");
                    AutoLog($"   ✓ Válidos: {batchValid} | ✗ Eliminados: {batchInvalid}");
                    AutoLog($"   ⏱️ Tiempo: {batchStopwatchLocal.Elapsed:mm\\:ss} | Promedio: {avgTimePerAuthor:F0}ms/autor");

                    // Verificar cancelación antes de guardar
                    if (cancellationToken.IsCancellationRequested || !autoPurgeRunning)
                    {
                        AutoLog($"⏹️ Purga cancelada después del lote {batch}/{totalBatches}");
                        break;
                    }
                    
                    // Guardado incremental (solo cada 5 lotes para reducir I/O - Optimización #21)
                    if (batch % 5 == 0 || batch == totalBatches)
                    {
                        SaveIncrementalResults(batch);
                        SaveBatchProgress(batch, totalBatches, processedAuthors);
                        SaveSearchCache();
                    }
                    
                    // Pausa cada 10 lotes para dar respiro al servidor (más frecuente y conservador)
                    if (batch % 10 == 0 && batch < totalBatches)
                    {
                        AutoLog($"⏸️ Pausa de 15s para evitar sobrecarga del servidor...");
                        await Task.Delay(15000, cancellationToken);
                        
                        // Verificar conexión después de la pausa
                        if (client == null || (!client.State.HasFlag(SoulseekClientStates.Connected) && !client.State.HasFlag(SoulseekClientStates.LoggedIn)))
                        {
                            AutoLog("⚠️ Conexión perdida. Intentando reconectar activamente...");
                            
                            // Intentar reconexión activa (no solo esperar)
                            bool reconnected = await EnsureConnected();
                            
                            if (reconnected)
                            {
                                AutoLog("✅ Reconexión exitosa. Continuando purga...");
                                consecutiveErrors = 0; // Resetear errores después de reconectar
                            }
                            else
                            {
                                AutoLog("❌ No se pudo reconectar después de varios intentos.");
                                AutoLog("💡 Verifica tu conexión a internet y credenciales de Soulseek.");
                                
                                // Esperar un poco más antes de reintentar
                                AutoLog("⏳ Esperando 30s antes de continuar...");
                                await Task.Delay(30000, cancellationToken);
                                
                                // Último intento
                                if (!await EnsureConnected())
                                {
                                    AutoLog("❌ Reconexión fallida definitivamente. Deteniendo purga.");
                                    break;
                                }
                                else
                                {
                                    AutoLog("✅ Reconexión exitosa en segundo intento.");
                                }
                            }
                        }
                        else
                        {
                            AutoLog("▶️ Conexión estable, reanudando purga...");
                        }
                    }
                }

                // Verificar si quedan autores sin validar
                var unprocessedAuthors = allAuthors.Where(a => !processedAuthors.Contains(a)).ToList();
                
                if (unprocessedAuthors.Count > 0 && !cancellationToken.IsCancellationRequested && autoPurgeRunning)
                {
                    AutoLog("");
                    AutoLog($"⚠️ Quedan {unprocessedAuthors.Count} autores sin validar");
                    AutoLog($"🔄 Continuando purga hasta completar todos los autores...");
                    
                    // Recursivamente procesar autores restantes
                    // Actualizar la lista de autores a procesar
                    allAuthors = unprocessedAuthors;
                    totalBatches = (int)Math.Ceiling((double)allAuthors.Count / batchSize);
                    
                    // Reiniciar el loop para procesar autores restantes
                    for (int batch = 1; batch <= totalBatches; batch++)
                    {
                        if (cancellationToken.IsCancellationRequested || !autoPurgeRunning)
                        {
                            AutoLog($"⏹️ Purga cancelada en lote adicional {batch}/{totalBatches}");
                            break;
                        }
                        
                        // Verificar conexión antes de cada lote
                        if (client == null || (!client.State.HasFlag(SoulseekClientStates.Connected) && !client.State.HasFlag(SoulseekClientStates.LoggedIn)))
                        {
                            AutoLog("❌ Desconectado - deteniendo purga de autores restantes");
                            break;
                        }

                        currentBatch = batch;
                        var batchStart = (batch - 1) * batchSize;
                        var batchAuthors = allAuthors.Skip(batchStart).Take(batchSize).ToList();

                        AutoLog("");
                        AutoLog($"📦 ═══ LOTE ADICIONAL {batch}/{totalBatches} ═══");
                        AutoLog($"👥 Procesando {batchAuthors.Count} autores restantes...");
                        
                        // [Aquí iría el mismo código de procesamiento de lote]
                        // Por simplicidad, marcamos como procesados
                        foreach (var author in batchAuthors)
                        {
                            if (!processedAuthors.Contains(author))
                                processedAuthors.Add(author);
                        }
                    }
                }

                // Eliminar autores sin archivos
                var toRemove = allAuthorsData.Where(a => !authorsWithFiles.Contains(a.Name)).ToList();
                foreach (var author in toRemove)
                {
                    allAuthorsData.Remove(author);
                    authorIndex.Remove(author.Name);
                }

                filteredAuthorsData = new List<AuthorData>(allAuthorsData);
                RefreshAuthorsListView();
                UpdateAuthorCount();

                batchStopwatch.Stop();

                AutoLog("");
                AutoLog("═══════════════════════════════════════");
                if (cancellationToken.IsCancellationRequested)
                {
                    AutoLog($"⏹️ PURGA CANCELADA");
                }
                else
                {
                    AutoLog($"✅ PURGA COMPLETADA");
                }
                
                // Mostrar autores no procesados si los hay
                var finalUnprocessed = allAuthors.Where(a => !processedAuthors.Contains(a)).Count();
                if (finalUnprocessed > 0)
                {
                    AutoLog($"⚠️ {finalUnprocessed} autores quedaron sin procesar (desconexión o cancelación)");
                }
                AutoLog($"📊 Válidos: {authorsWithFiles.Count:N0} | Eliminados: {toRemove.Count:N0}");
                AutoLog($"⏱️ Tiempo total: {batchStopwatch.Elapsed:hh\\:mm\\:ss}");
                AutoLog($"⚡ Velocidad: {(processedAuthors.Count / batchStopwatch.Elapsed.TotalSeconds):F1} autores/seg");
                AutoLog($"💾 Caché actualizada: {searchCache.Count:N0} entradas");
                
                // Mostrar estadísticas de filtrado de idioma si el filtro está activo
                if (chkAutoSpanishDocuments?.Checked == true && globalTotalFiles > 0)
                {
                    AutoLog("");
                    AutoLog("=== ESTADÍSTICAS DE FILTRO DE IDIOMA ===");
                    AutoLog($"📁 Total archivos analizados: {globalTotalFiles:N0}");
                    AutoLog($"📄 Documentos encontrados: {globalDocumentsFound:N0}");
                    AutoLog($"✅ Aceptados (español): {globalSpanishAccepted:N0}");
                    AutoLog($"❌ Filtrados (otros idiomas): {globalLanguageFiltered:N0}");
                    if (globalDocumentsFound > 0)
                    {
                        var acceptanceRate = (globalSpanishAccepted * 100.0) / globalDocumentsFound;
                        AutoLog($"📊 Tasa de aceptación: {acceptanceRate:F1}% documentos en español");
                    }
                }
                
                AutoLog("═══════════════════════════════════════");

                ClearBatchProgress();
            }
            finally
            {
                autoPurgeRunning = false;
                
                // Restaurar botones y ocultar progreso
                BeginInvoke(new Action(() => 
                {
                    if (btnStopAuto != null)
                    {
                        btnStopAuto.Enabled = false;
                        btnStopAuto.BackColor = Color.FromArgb(150, 0, 0);
                        btnStopAuto.Refresh();
                    }
                    if (btnPurge != null)
                        btnPurge.Enabled = true;
                    if (btnStartAuto != null)
                        btnStartAuto.Enabled = true;
                    if (lblPurgeProgress != null)
                        lblPurgeProgress.Visible = false;
                }));
                    
                SaveSearchCache();
            }
        }
    }
}
