// FastPurge.cs - Purga optimizada por lotes con manejo de conexión
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soulseek;

namespace SlskDown
{
    public partial class MainForm
    {
        private int purgeProgress = 0;
        private int purgeTotalAuthors = 0;
        private string purgeProgressFile = "purge_progress.txt";

        /// <summary>
        /// Purga optimizada por lotes con pausas y guardado de progreso
        /// Usa sistema multi-red si está disponible (Soulseek + eMule)
        /// </summary>
        private async Task PurgeAuthorsWithoutResultsOptimized()
        {
            if (allAuthorsData.Count == 0)
            {
                AutoLog("❌ No se puede purgar: lista vacía.");
                return;
            }
            
            // Verificar si hay redes disponibles
            bool hasNetworks = false;
            if (networkOrchestrator != null && networkOrchestrator.GetSearchProviders().Count > 0)
            {
                hasNetworks = true;
                AutoLog($"🌐 Purga multi-red: {networkOrchestrator.GetSearchProviders().Count} redes activas");
            }
            else if (client != null)
            {
                hasNetworks = true;
                AutoLog("🌐 Purga usando solo Soulseek");
            }
            
            if (!hasNetworks)
            {
                AutoLog("❌ No hay redes disponibles para purgar.");
                return;
            }

            var allAuthors = allAuthorsData.Select(a => a.Name).ToList();
            if (allAuthors.Count == 0) return;

            // Cargar progreso si existe
            int startIndex = LoadPurgeProgress();
            if (startIndex > 0)
            {
                AutoLog($"📂 Reanudando purga desde autor {startIndex}/{allAuthors.Count}");
            }

            AutoLog("");
            AutoLog($"🗑️ PURGA OPTIMIZADA: {allAuthors.Count} autores");
            AutoLog($"📦 Procesando en lotes de 100 autores");
            AutoLog($"⏸️ Pausa de 30s cada 500 autores para mantener conexión");
            if (chkAutoSpanishDocuments?.Checked == true)
                AutoLog("🇪🇸 Filtro: Solo documentos en español");
            AutoLog("");

            autoPurgeRunning = true;
            purgeTotalAuthors = allAuthors.Count;
            purgeProgress = startIndex;

            autoSearchCts?.Dispose();
            autoSearchCts = new CancellationTokenSource();
            var cancellationToken = autoSearchCts.Token;

            // Habilitar botón Detener
            if (btnStopAuto != null)
            {
                btnStopAuto.Enabled = true;
                btnStopAuto.BackColor = Color.FromArgb(0, 255, 0);
            }
            if (btnPurge != null) btnPurge.Enabled = false;
            if (btnStartAuto != null) btnStartAuto.Enabled = false;

            var authorsWithFiles = new HashSet<string>();
            const int BATCH_SIZE = 100;
            const int PAUSE_EVERY = 500;
            const int PAUSE_DURATION_SECONDS = 30;
            const int MAX_PARALLEL = 3; // Reducido para no saturar

            try
            {
                for (int batchStart = startIndex; batchStart < allAuthors.Count; batchStart += BATCH_SIZE)
                {
                    if (!autoPurgeRunning || cancellationToken.IsCancellationRequested)
                    {
                        AutoLog("⏹️ Purga detenida por el usuario");
                        break;
                    }

                    // Verificar conexión antes de cada lote
                    if (!IsConnected())
                    {
                        AutoLog("⚠️ Conexión perdida. Esperando reconexión...");
                        await WaitForReconnection(cancellationToken);
                    }

                    int batchEnd = Math.Min(batchStart + BATCH_SIZE, allAuthors.Count);
                    var batch = allAuthors.Skip(batchStart).Take(batchEnd - batchStart).ToList();

                    AutoLog($"📦 Lote {batchStart / BATCH_SIZE + 1}: Procesando autores {batchStart + 1}-{batchEnd} de {allAuthors.Count}");

                    var semaphore = new SemaphoreSlim(MAX_PARALLEL);

                    await Task.WhenAll(batch.Select(async author =>
                    {
                        await semaphore.WaitAsync(cancellationToken);
                        try
                        {
                            if (!autoPurgeRunning || cancellationToken.IsCancellationRequested)
                                return;

                            UpdateAuthorStatus(author, "🔍 Buscando...");

                            bool hasValid = false;
                            int totalFiles = 0;

                            // Usar sistema multi-red si está disponible
                            if (networkOrchestrator != null && networkOrchestrator.GetSearchProviders().Count > 0)
                            {
                                // Búsqueda multi-red
                                var networkRequest = new Core.SearchRequest
                                {
                                    SearchId = Guid.NewGuid().ToString(),
                                    Query = author,
                                    MaxResults = 100,
                                    TimeoutSeconds = 15
                                };

                                var networkResponse = await networkOrchestrator.SearchAsync(
                                    networkRequest, 
                                    networks: null, // Buscar en todas las redes
                                    cancellationToken: cancellationToken
                                );

                                if (networkResponse?.DeduplicatedResults != null)
                                {
                                    totalFiles = networkResponse.DeduplicatedResults.Count;
                                    hasValid = networkResponse.DeduplicatedResults.Any(r =>
                                        IsDocumentFile(r.FileName) &&
                                        (chkAutoSpanishDocuments?.Checked != true || IsSpanishText(r.FileName)));
                                }
                            }
                            else if (client != null)
                            {
                                // Fallback a Soulseek solo
                                var results = await client.SearchAsync(
                                    SearchQuery.FromText(author),
                                    options: new SearchOptions(
                                        searchTimeout: 15000,
                                        maximumPeerQueueLength: 50000,
                                        filterResponses: false,
                                        minimumResponseFileCount: 1,
                                        minimumPeerUploadSpeed: 0
                                    ),
                                    cancellationToken: cancellationToken
                                );

                                var responses = results.Responses;
                                if (responses != null)
                                {
                                    totalFiles = responses.Sum(r => r.Files?.Count ?? 0);
                                    foreach (var response in responses)
                                    {
                                        if (response.Files == null) continue;
                                        if (response.Files.Any(f =>
                                            IsDocumentFile(f.Filename) &&
                                            (chkAutoSpanishDocuments?.Checked != true || IsSpanishText(f.Filename))))
                                        {
                                            hasValid = true;
                                            break;
                                        }
                                    }
                                }
                            }

                            if (hasValid)
                            {
                                lock (authorsWithFiles) authorsWithFiles.Add(author);
                                UpdateAuthorData(author, totalFiles, "✅ Válido", Color.LightGreen);
                            }
                            else
                            {
                                UpdateAuthorData(author, 0, "❌ Sin libros", Color.LightCoral);
                            }

                            Interlocked.Increment(ref purgeProgress);
                            UpdatePurgeProgressLabel();
                        }
                        catch (OperationCanceledException)
                        {
                            UpdateAuthorData(author, 0, "⏹️ Cancelado", Color.Gray);
                        }
                        catch (Exception ex)
                        {
                            AutoLog($"⚠️ Error en {author}: {ex.Message}");
                            UpdateAuthorData(author, 0, "⚠️ Error", Color.Orange);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));

                    // Guardar progreso después de cada lote
                    SavePurgeProgress(batchEnd);

                    // Pausa cada PAUSE_EVERY autores
                    if ((batchEnd % PAUSE_EVERY == 0) && batchEnd < allAuthors.Count)
                    {
                        AutoLog($"⏸️ Pausa de {PAUSE_DURATION_SECONDS}s para mantener conexión estable...");
                        await Task.Delay(PAUSE_DURATION_SECONDS * 1000, cancellationToken);
                        AutoLog("▶️ Reanudando purga...");
                    }
                }

                // Eliminar autores sin archivos
                var toRemove = allAuthorsData.Where(a => !authorsWithFiles.Contains(a.Name)).ToList();
                foreach (var author in toRemove)
                {
                    allAuthorsData.Remove(author);
                }

                AutoLog("");
                AutoLog($"✅ PURGA COMPLETADA");
                AutoLog($"   Total procesados: {purgeProgress}");
                AutoLog($"   Autores válidos: {authorsWithFiles.Count}");
                AutoLog($"   Autores eliminados: {toRemove.Count}");
                AutoLog("");

                SaveAuthorsList();
                RefreshAuthorsList();

                // Limpiar archivo de progreso
                DeletePurgeProgress();
            }
            catch (Exception ex)
            {
                AutoLog($"❌ Error en purga: {ex.Message}");
            }
            finally
            {
                autoPurgeRunning = false;
                if (btnStopAuto != null) btnStopAuto.Enabled = false;
                if (btnPurge != null) btnPurge.Enabled = true;
                if (btnStartAuto != null) btnStartAuto.Enabled = true;
            }
        }

        private bool IsConnected()
        {
            return client != null &&
                   (client.State.HasFlag(SoulseekClientStates.Connected) ||
                    client.State.HasFlag(SoulseekClientStates.LoggedIn));
        }

        private async Task WaitForReconnection(CancellationToken cancellationToken)
        {
            int attempts = 0;
            const int MAX_WAIT_SECONDS = 120; // 2 minutos máximo

            while (!IsConnected() && attempts < MAX_WAIT_SECONDS)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException();

                await Task.Delay(1000, cancellationToken);
                attempts++;

                if (attempts % 10 == 0)
                {
                    AutoLog($"⏳ Esperando reconexión... ({attempts}s)");
                }
            }

            if (!IsConnected())
            {
                throw new Exception("No se pudo reconectar después de 2 minutos");
            }

            AutoLog("✅ Conexión restablecida. Continuando purga...");
        }

        private void UpdatePurgeProgressLabel()
        {
            if (lblPurgeProgress != null && InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    double percentage = (purgeProgress * 100.0) / purgeTotalAuthors;
                    lblPurgeProgress.Text = $"Progreso: {purgeProgress}/{purgeTotalAuthors} ({percentage:F1}%)";
                }));
            }
        }

        private void SavePurgeProgress(int progress)
        {
            try
            {
                System.IO.File.WriteAllText(purgeProgressFile, progress.ToString());
            }
            catch { }
        }

        private int LoadPurgeProgress()
        {
            try
            {
                if (System.IO.File.Exists(purgeProgressFile))
                {
                    string content = System.IO.File.ReadAllText(purgeProgressFile);
                    if (int.TryParse(content, out int progress))
                        return progress;
                }
            }
            catch { }
            return 0;
        }

        private void DeletePurgeProgress()
        {
            try
            {
                if (System.IO.File.Exists(purgeProgressFile))
                    System.IO.File.Delete(purgeProgressFile);
            }
            catch { }
        }
    }
}
