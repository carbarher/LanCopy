// OptimizedPurge_50K.cs - Purga ultra-optimizada para 50,000+ autores
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soulseek;

namespace SlskDown
{
    public partial class MainForm
    {
        // Variables para optimización de UI durante purga (50K)
        private DateTime lastPurge50KUIUpdate = DateTime.MinValue;
        private const int PURGE_50K_UI_UPDATE_THROTTLE_MS = 1000; // Actualizar UI cada 1 segundo máximo
        private ConcurrentQueue<(string author, int filesCount, string status, Color? color)> pendingPurge50KUIUpdates = new ConcurrentQueue<(string, int, string, Color?)>();
        private System.Threading.Timer purge50KUIUpdateTimer;
        
        /// <summary>
        /// Purga ultra-optimizada para 50K+ autores
        /// - Actualizaciones UI en batch cada 1 segundo
        /// - Procesamiento paralelo agresivo
        /// - Cache de búsquedas
        /// - Eliminación diferida
        /// </summary>
        private async Task PurgeAuthorsWithoutResults_50K()
        {
            if (allAuthorsData.Count == 0)
            {
                AutoLog("❌ No se puede purgar: No hay autores cargados.");
                return;
            }
            
            if (client == null || (!client.State.HasFlag(SoulseekClientStates.Connected) && !client.State.HasFlag(SoulseekClientStates.LoggedIn)))
            {
                AutoLog("❌ No se puede purgar: No estás conectado a Soulseek.");
                return;
            }

            var allAuthors = allAuthorsData.Select(a => a.Name).ToList();
            if (allAuthors.Count == 0) return;

            // Pre-filtrar autores inválidos
            var originalCount = allAuthors.Count;
            allAuthors = allAuthors
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Where(a => a.Length >= 2 && a.Length <= 100)
                .Where(a => !a.All(char.IsDigit))
                .Where(a => a.Any(char.IsLetter))
                .ToList();
            
            var filtered = originalCount - allAuthors.Count;
            if (filtered > 0)
                AutoLog($"🗑️ Pre-filtrados {filtered} autores inválidos");

            // Calcular configuración óptima (CONSERVADORA para evitar desconexiones)
            int batchSize = Math.Min(500, allAuthors.Count / 20); // Lotes más pequeños
            int totalBatches = (int)Math.Ceiling((double)allAuthors.Count / batchSize);
            int maxParallel = 4; // CONSERVADOR: 4 búsquedas simultáneas para evitar saturar el servidor

            AutoLog("═══════════════════════════════════════");
            AutoLog($"🧹 PURGA ULTRA-OPTIMIZADA (50K+)");
            AutoLog($"📚 {allAuthors.Count:N0} autores");
            AutoLog($"📦 {totalBatches} lotes de {batchSize} autores");
            AutoLog($"⚡ Paralelismo: {maxParallel} búsquedas simultáneas");
            AutoLog($"🎨 Actualizaciones UI: Batch cada 1 segundo");
            if (chkAutoSpanishDocuments?.Checked == true) AutoLog("🇪🇸 Filtro: Solo documentos en español");
            AutoLog($"💾 Caché: {searchCache.Count:N0} autores en caché");
            AutoLog("═══════════════════════════════════════");

            autoPurgeRunning = true;
            autoSearchCts?.Dispose();
            autoSearchCts = new CancellationTokenSource();
            var cancellationToken = autoSearchCts.Token;
            
            // Iniciar timer para actualizaciones UI en batch
            StartPurgeUIUpdateTimer();
            
            // NO deshabilitar actualizaciones del ListView para ver progreso en tiempo real
            // El timer se encargará de actualizar en batch cada segundo
            
            if (btnStopAuto != null)
            {
                btnStopAuto.Enabled = true;
                btnStopAuto.BackColor = Color.FromArgb(0, 200, 0);
            }
            if (btnPurge != null) btnPurge.Enabled = false;
            if (btnStartAuto != null) btnStartAuto.Enabled = false;

            var authorsWithFiles = new ConcurrentBag<string>();
            var processedCount = 0;
            var validCount = 0;
            var cacheHits = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                for (int batch = 1; batch <= totalBatches; batch++)
                {
                    if (cancellationToken.IsCancellationRequested || !autoPurgeRunning)
                    {
                        AutoLog($"⏹️ Purga cancelada en lote {batch}/{totalBatches}");
                        break;
                    }

                    var batchStart = (batch - 1) * batchSize;
                    var batchAuthors = allAuthors.Skip(batchStart).Take(batchSize).ToList();
                    var batchSw = System.Diagnostics.Stopwatch.StartNew();

                    var progress = processedCount * 100.0 / allAuthors.Count;
                    var elapsed = sw.Elapsed;
                    var rate = processedCount > 0 ? processedCount / elapsed.TotalSeconds : 0;
                    var remaining = rate > 0 ? TimeSpan.FromSeconds((allAuthors.Count - processedCount) / rate) : TimeSpan.Zero;
                    
                    AutoLog("");
                    AutoLog($"📦 ═══ LOTE {batch}/{totalBatches} ═══");
                    AutoLog($"📊 Progreso: {processedCount:N0}/{allAuthors.Count:N0} ({progress:F1}%)");
                    AutoLog($"⏱️ Tiempo: {elapsed:hh\\:mm\\:ss} | Restante: ~{remaining:hh\\:mm\\:ss}");
                    AutoLog($"⚡ Velocidad: {rate:F1} autores/seg | 💾 Cache hits: {cacheHits:N0}");

                    // Procesar lote con máximo paralelismo
                    var semaphore = new SemaphoreSlim(maxParallel);
                    AutoLog($"🔓 Semáforo creado con {maxParallel} slots para {batchAuthors.Count} autores");

                    var tasks = batchAuthors.Select(async author =>
                    {
                        if (cancellationToken.IsCancellationRequested || !autoPurgeRunning)
                            return;
                        
                        // Esperar mientras esté pausado por desconexión
                        while (autoPurgePausedByDisconnection && autoPurgeRunning)
                        {
                            await Task.Delay(1000, cancellationToken);
                        }
                        
                        if (cancellationToken.IsCancellationRequested || !autoPurgeRunning)
                            return;
                        
                        await semaphore.WaitAsync(cancellationToken);
                        
                        try
                        {
                            // Verificar caché primero
                            if (searchCache.TryGetValue(author, out var cachedResult))
                            {
                                Interlocked.Increment(ref cacheHits);
                                
                                if (cachedResult.HasValidFiles)
                                {
                                    authorsWithFiles.Add(author);
                                    Interlocked.Increment(ref validCount);
                                    AutoLog($"   💾 {author}: {cachedResult.FilesCount} archivos (caché) → ✅ VÁLIDO");
                                    QueueUIUpdate(author, cachedResult.FilesCount, "✅ Válido (caché)", Color.LightGreen);
                                }
                                else
                                {
                                    AutoLog($"   💾 {author}: {cachedResult.FilesCount} archivos (caché) → ❌ ELIMINADO");
                                    QueueUIUpdate(author, 0, "❌ Eliminado (caché)", Color.LightCoral);
                                }
                                
                                Interlocked.Increment(ref processedCount);
                                semaphore.Release(); // CRÍTICO: Liberar semáforo antes de return
                                return;
                            }

                            // Búsqueda real
                            AutoLog($"   🔍 Buscando: {author}...");
                            QueueUIUpdate(author, 0, "🔍 Buscando...", null);

                            // Delay para evitar flood (250ms entre búsquedas = ~4 búsquedas/seg con 4 paralelas = 16 búsquedas/seg total)
                            await Task.Delay(250, cancellationToken);

                            var results = await client.SearchAsync(
                                SearchQuery.FromText(author),
                                options: new SearchOptions(
                                    searchTimeout: 3000,  // Reducido a 3000ms para mayor velocidad
                                    responseLimit: 3000,  // Reducido de 5000 a 3000
                                    fileLimit: 10000,     // Reducido de 30000 a 10000
                                    filterResponses: false,
                                    minimumResponseFileCount: 1,
                                    minimumPeerUploadSpeed: 0
                                ),
                                cancellationToken: cancellationToken
                            );

                            var responses = results.Responses;
                            var responsesCount = responses?.Count ?? 0;
                            var filesCount = responses?.Sum(r => r.Files?.Count ?? 0) ?? 0;
                            bool hasValid = false;
                            int documentsFound = 0;
                            int validDocuments = 0;

                            if (responses != null)
                            {
                                bool checkSpanish = chkAutoSpanishDocuments?.Checked == true;
                                
                                foreach (var response in responses)
                                {
                                    if (response.Files == null) continue;
                                    
                                    foreach (var file in response.Files)
                                    {
                                        if (file.Size == 0) continue;
                                        if (IsGarbageFile(file.Filename)) continue;
                                        if (!IsDocumentFile(file.Filename)) continue;
                                        
                                        documentsFound++;
                                        
                                        if (checkSpanish)
                                        {
                                            if (IsSpanishText(file.Filename))
                                            {
                                                validDocuments++;
                                                hasValid = true;
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            validDocuments++;
                                            hasValid = true;
                                            break;
                                        }
                                    }
                                    
                                    if (hasValid) break;
                                }
                            }

                            // Cachear resultado
                            searchCache[author] = new SearchCacheEntry
                            {
                                Timestamp = DateTime.Now,
                                FilesCount = filesCount,
                                HasValidFiles = hasValid
                            };

                            // Log detallado de resultados
                            if (hasValid)
                            {
                                authorsWithFiles.Add(author);
                                Interlocked.Increment(ref validCount);
                                AutoLog($"      ✅ {author}: {responsesCount} respuestas, {filesCount} archivos, {documentsFound} documentos, {validDocuments} válidos → VÁLIDO");
                                QueueUIUpdate(author, filesCount, "✅ Válido", Color.LightGreen);
                            }
                            else
                            {
                                AutoLog($"      ❌ {author}: {responsesCount} respuestas, {filesCount} archivos, {documentsFound} documentos, {validDocuments} válidos → ELIMINADO");
                                QueueUIUpdate(author, filesCount, "❌ Eliminado", Color.LightCoral);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            AutoLog($"      ⏹️ {author}: Búsqueda cancelada");
                            QueueUIUpdate(author, 0, "⏹️ Cancelado", Color.Gray);
                        }
                        catch (Exception ex)
                        {
                            AutoLog($"      ⚠️ {author}: Error - {ex.Message}");
                            QueueUIUpdate(author, 0, "⚠️ Error", Color.Orange);
                        }
                        finally
                        {
                            semaphore.Release();
                            Interlocked.Increment(ref processedCount);
                        }
                    }).ToList();
                    
                    await Task.WhenAll(tasks);
                    
                    batchSw.Stop();

                    // Estadísticas del lote
                    var batchValidCount = batchAuthors.Count(a => authorsWithFiles.Contains(a));
                    var batchInvalidCount = batchAuthors.Count - batchValidCount;
                    var batchRate = batchAuthors.Count / batchSw.Elapsed.TotalSeconds;
                    
                    AutoLog($"✅ Lote completado en {batchSw.Elapsed:mm\\:ss}:");
                    AutoLog($"   ✓ Válidos: {batchValidCount} | ✗ Eliminados: {batchInvalidCount}");
                    AutoLog($"   ⚡ Velocidad lote: {batchRate:F1} autores/seg");

                    // Actualizar progreso
                    if (lblPurgeProgress != null)
                    {
                        SafeBeginInvoke(() =>
                        {
                            var pct = processedCount * 100.0 / allAuthors.Count;
                            var elapsed = sw.Elapsed;
                            var rate = processedCount / elapsed.TotalSeconds;
                            var remaining = rate > 0 ? TimeSpan.FromSeconds((allAuthors.Count - processedCount) / rate) : TimeSpan.Zero;
                            
                            lblPurgeProgress.Text = $"⏳ {processedCount:N0}/{allAuthors.Count:N0} ({pct:F1}%) | ⏱️ {remaining:hh\\:mm\\:ss} restante";
                            lblPurgeProgress.Visible = true;
                        });
                    }
                    
                    // Guardar progreso cada 5 lotes
                    if (batch % 5 == 0 || batch == totalBatches)
                    {
                        AutoLog($"💾 Guardando progreso (lote {batch}/{totalBatches})...");
                        SaveSearchCache();
                    }
                }

                // Procesar actualizaciones UI pendientes
                AutoLog("");
                AutoLog("🔄 Procesando actualizaciones UI pendientes...");
                FlushPendingUIUpdates();
                
                // Eliminar autores sin archivos (optimizado)
                AutoLog("");
                AutoLog("🗑️ Eliminando autores sin archivos...");
                
                var authorsWithFilesSet = new HashSet<string>(authorsWithFiles);
                var toRemove = allAuthorsData.Where(a => !authorsWithFilesSet.Contains(a.Name)).ToList();
                
                AutoLog($"   📋 Autores a eliminar: {toRemove.Count:N0}");
                
                var removeSw = System.Diagnostics.Stopwatch.StartNew();
                
                // Eliminación en paralelo para grandes volúmenes
                if (toRemove.Count > 1000)
                {
                    AutoLog($"   ⚡ Eliminación paralela activada (>{toRemove.Count:N0} autores)");
                    Parallel.ForEach(toRemove, author =>
                    {
                        allAuthorsData.Remove(author);
                        authorIndex.Remove(author.Name);
                    });
                }
                else
                {
                    AutoLog($"   📝 Eliminación secuencial ({toRemove.Count:N0} autores)");
                    foreach (var author in toRemove)
                    {
                        allAuthorsData.Remove(author);
                        authorIndex.Remove(author.Name);
                    }
                }
                
                removeSw.Stop();
                AutoLog($"   ✅ Eliminación completada en {removeSw.ElapsedMilliseconds}ms");
                
                sw.Stop();

                filteredAuthorsData = new List<AuthorData>(allAuthorsData);

                AutoLog("");
                AutoLog("═══════════════════════════════════════");
                AutoLog($"✅ PURGA COMPLETADA");
                AutoLog("");
                AutoLog($"📊 RESULTADOS:");
                AutoLog($"   ✓ Autores válidos: {authorsWithFiles.Count:N0}");
                AutoLog($"   ✗ Autores eliminados: {toRemove.Count:N0}");
                AutoLog($"   📝 Total procesados: {processedCount:N0}");
                AutoLog("");
                AutoLog($"⚡ RENDIMIENTO:");
                AutoLog($"   ⏱️ Tiempo total: {sw.Elapsed:hh\\:mm\\:ss}");
                AutoLog($"   🚀 Velocidad promedio: {(processedCount / sw.Elapsed.TotalSeconds):F1} autores/seg");
                AutoLog($"   💾 Cache hits: {cacheHits:N0} ({(cacheHits * 100.0 / processedCount):F1}%)");
                AutoLog($"   🔍 Búsquedas en Soulseek: {(processedCount - cacheHits):N0}");
                AutoLog("");
                AutoLog($"💾 CACHÉ:");
                AutoLog($"   📦 Entradas totales: {searchCache.Count:N0}");
                AutoLog($"   ✅ Tasa de aciertos: {(cacheHits * 100.0 / processedCount):F1}%");
                AutoLog("═══════════════════════════════════════");
            }
            finally
            {
                // Detener timer de actualizaciones UI
                StopPurgeUIUpdateTimer();
                
                // Marcar purga como no activa ANTES de refrescar UI
                autoPurgeRunning = false;
                
                // Refrescar ListView final con manejo de errores
                SafeBeginInvoke(() =>
                {
                    try
                    {
                        if (lvAutoAuthors != null && lvAutoAuthors.IsHandleCreated)
                        {
                            // Ya no hay BeginUpdate activo, solo refrescar
                            RefreshAuthorsListView();
                            UpdateAuthorCount();
                        }
                    }
                    catch (Exception ex)
                    {
                        AutoLog($"⚠️ Error al refrescar ListView: {ex.Message}");
                    }
                    
                    // Restaurar botones
                    if (btnStopAuto != null && btnStopAuto.IsHandleCreated)
                    {
                        btnStopAuto.Enabled = false;
                        btnStopAuto.BackColor = Color.FromArgb(150, 0, 0);
                    }
                    if (btnPurge != null && btnPurge.IsHandleCreated)
                        btnPurge.Enabled = true;
                    if (btnStartAuto != null && btnStartAuto.IsHandleCreated)
                        btnStartAuto.Enabled = true;
                    if (lblPurgeProgress != null && lblPurgeProgress.IsHandleCreated)
                        lblPurgeProgress.Visible = false;
                });
                
                SaveSearchCache();
            }
        }
        
        /// <summary>
        /// Encola actualización de UI para procesamiento en batch
        /// </summary>
        private void QueueUIUpdate(string author, int filesCount, string status, Color? color)
        {
            pendingPurge50KUIUpdates.Enqueue((author, filesCount, status, color));
        }
        
        /// <summary>
        /// Inicia timer para procesar actualizaciones UI en batch
        /// </summary>
        private void StartPurgeUIUpdateTimer()
        {
            purge50KUIUpdateTimer?.Dispose();
            purge50KUIUpdateTimer = new System.Threading.Timer(_ =>
            {
                FlushPendingUIUpdates();
            }, null, 1000, 1000); // Cada 1 segundo
        }
        
        /// <summary>
        /// Detiene timer de actualizaciones UI
        /// </summary>
        private void StopPurgeUIUpdateTimer()
        {
            purge50KUIUpdateTimer?.Dispose();
            purge50KUIUpdateTimer = null;
        }
        
        /// <summary>
        /// Procesa todas las actualizaciones UI pendientes en batch
        /// </summary>
        private void FlushPendingUIUpdates()
        {
            if (pendingPurge50KUIUpdates.IsEmpty)
                return;
            
            var updates = new List<(string author, int filesCount, string status, Color? color)>();
            
            // Extraer todas las actualizaciones pendientes
            while (pendingPurge50KUIUpdates.TryDequeue(out var update))
            {
                updates.Add(update);
            }
            
            if (updates.Count == 0)
                return;
            
            AutoLog($"🔄 FlushPendingUIUpdates: {updates.Count} actualizaciones pendientes");
            
            // Aplicar actualizaciones en batch en UI thread
            SafeBeginInvoke(() =>
            {
                try
                {
                    // Validar que el ListView existe y tiene handle
                    if (lvAutoAuthors == null || !lvAutoAuthors.IsHandleCreated)
                    {
                        AutoLog($"⚠️ FlushPendingUIUpdates: ListView no disponible (null={lvAutoAuthors == null}, handle={lvAutoAuthors?.IsHandleCreated})");
                        return;
                    }
                    
                    AutoLog($"✅ FlushPendingUIUpdates: Aplicando {updates.Count} actualizaciones a UI");
                    
                    // Deshabilitar redibujado durante actualizaciones en batch
                    lvAutoAuthors.BeginUpdate();
                    
                    // Aplicar todas las actualizaciones
                    int applied = 0;
                    foreach (var (author, filesCount, status, color) in updates)
                    {
                        if (authorIndex != null && authorIndex.TryGetValue(author, out var authorData))
                        {
                            authorData.FilesCount = filesCount;
                            authorData.Status = status;
                            if (color.HasValue)
                                authorData.ForeColor = color.Value;
                            applied++;
                        }
                    }
                    
                    AutoLog($"✅ FlushPendingUIUpdates: {applied}/{updates.Count} actualizaciones aplicadas");
                    
                    // CRÍTICO: Refrescar ListView para mostrar cambios
                    // Como NO está en VirtualMode, necesitamos actualizar los items manualmente
                    foreach (var (author, filesCount, status, color) in updates)
                    {
                        if (authorIndex != null && authorIndex.TryGetValue(author, out var authorData))
                        {
                            // Buscar el item en el ListView
                            var index = filteredAuthorsData.IndexOf(authorData);
                            if (index >= 0 && index < lvAutoAuthors.Items.Count)
                            {
                                var item = lvAutoAuthors.Items[index];
                                item.SubItems[1].Text = filesCount.ToString();
                                item.SubItems[2].Text = status;
                                if (color.HasValue)
                                    item.ForeColor = color.Value;
                            }
                        }
                    }
                    
                    // Reactivar redibujado DESPUÉS de todas las actualizaciones
                    lvAutoAuthors.EndUpdate();
                    
                    AutoLog($"✅ FlushPendingUIUpdates: Completado - {lvAutoAuthors.Items.Count} items en ListView");
                }
                catch (Exception ex)
                {
                    AutoLog($"⚠️ Error en FlushPendingUIUpdates: {ex.Message}");
                    AutoLog($"   Stack: {ex.StackTrace}");
                }
            });
        }
    }
}
