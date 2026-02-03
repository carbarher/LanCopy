using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Proveedor de búsqueda para eMule/ed2k que implementa ISearchProvider
    /// Permite búsquedas uniformes junto con otras redes P2P
    /// </summary>
    public class EmuleSearchProvider : ISearchProvider
    {
        private readonly IEmuleClient _emuleClient;
        private readonly Dictionary<string, CancellationTokenSource> _activeSearches = new Dictionary<string, CancellationTokenSource>();
        private readonly Dictionary<string, List<EmuleSearchResult>> _pendingResults = new Dictionary<string, List<EmuleSearchResult>>();
        private readonly Dictionary<string, TaskCompletionSource<bool>> _resultWaiters = new Dictionary<string, TaskCompletionSource<bool>>();
        private readonly object _searchLock = new object();
        private readonly SemaphoreSlim _searchSemaphore = new SemaphoreSlim(1, 1); // Solo 1 búsqueda a la vez

        public string ProviderName => "eMule";

        public bool IsReady => _emuleClient?.IsConnected ?? false;

        public event EventHandler<SearchResultsReceivedEventArgs> ResultsReceived;
        public event EventHandler<SearchCompletedEventArgs> SearchCompleted;

        public EmuleSearchProvider(IEmuleClient emuleClient)
        {
            _emuleClient = emuleClient ?? throw new ArgumentNullException(nameof(emuleClient));
            
            // Suscribirse a los resultados del cliente eMule
            _emuleClient.SearchResultsReceived += OnEmuleSearchResultsReceived;
        }
        
        private void OnEmuleSearchResultsReceived(object sender, EmuleSearchResultsEventArgs e)
        {
            var logMsg = $"📥 eMule Provider: Evento recibido - {e.Results?.Count ?? 0} resultados para SearchId={e.SearchId}";
            System.Diagnostics.Debug.WriteLine(logMsg);
            _emuleClient.GetType().GetMethod("Log", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(_emuleClient, new object[] { logMsg });
            
            TaskCompletionSource<bool> waiter = null;
            
            // Los resultados se almacenarán temporalmente para ser procesados
            lock (_searchLock)
            {
                if (!_pendingResults.ContainsKey(e.SearchId))
                {
                    _pendingResults[e.SearchId] = new List<EmuleSearchResult>();
                }
                _pendingResults[e.SearchId].AddRange(e.Results);
                var totalMsg = $"📥 eMule Provider: Total pendientes para {e.SearchId}: {_pendingResults[e.SearchId].Count}";
                System.Diagnostics.Debug.WriteLine(totalMsg);
                _emuleClient.GetType().GetMethod("Log", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(_emuleClient, new object[] { totalMsg });
                
                // Señalizar que los resultados han llegado
                if (_resultWaiters.TryGetValue(e.SearchId, out waiter))
                {
                    _resultWaiters.Remove(e.SearchId);
                }
            }
            
            // Completar la tarea fuera del lock
            waiter?.TrySetResult(true);
        }

        public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
        {
            if (!IsReady)
            {
                return new SearchResponse
                {
                    SearchId = request.SearchId,
                    Status = SearchStatus.Failed,
                    ErrorMessage = "Cliente eMule no está conectado"
                };
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            lock (_searchLock)
            {
                _activeSearches[request.SearchId] = cts;
            }

            var response = new SearchResponse
            {
                SearchId = request.SearchId,
                Status = SearchStatus.InProgress
            };

            var startTime = DateTime.UtcNow;
            var results = new List<SearchResult>();
            var seenFiles = new HashSet<string>();

            try
            {
                var logMsg = $"⏳ Esperando turno para buscar '{request.Query}' en eMule...";
                System.Diagnostics.Debug.WriteLine($"[eMule Provider] {logMsg}");
                _emuleClient.GetType().GetMethod("Log", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(_emuleClient, new object[] { logMsg });
                
                // IMPORTANTE: eMule WebServer solo puede manejar UNA búsqueda a la vez
                // Esperar turno para ejecutar la búsqueda
                await _searchSemaphore.WaitAsync(cts.Token);
                
                try
                {
                    var startMsg = $"Iniciando búsqueda de '{request.Query}' en eMule";
                    System.Diagnostics.Debug.WriteLine($"[eMule Provider] {startMsg}");
                    _emuleClient.GetType().GetMethod("Log", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(_emuleClient, new object[] { startMsg });
                    
                    // Crear TaskCompletionSource para esperar los resultados
                    var waiter = new TaskCompletionSource<bool>();
                    
                    // Realizar búsqueda en eMule
                    var searchId = await _emuleClient.SearchAsync(request.Query, cts.Token);
                    System.Diagnostics.Debug.WriteLine($"[eMule Provider] Búsqueda enviada, SearchId={searchId}");
                
                // Registrar el waiter antes de esperar
                lock (_searchLock)
                {
                    _resultWaiters[searchId] = waiter;
                }
                
                // AJUSTE: eMule es LENTO, usar timeout de 60 segundos (6x más que antes)
                var waitTask = waiter.Task;
                var timeoutTask = Task.Delay(60000, cts.Token);
                var completedTask = await Task.WhenAny(waitTask, timeoutTask);
                
                var msg = completedTask == waitTask 
                    ? "✅ eMule Provider: Resultados recibidos vía evento" 
                    : "⏱️ eMule Provider: Timeout esperando resultados";
                _emuleClient.GetType().GetMethod("Log", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(_emuleClient, new object[] { msg });
                
                // Obtener resultados recibidos del evento
                List<EmuleSearchResult> emuleResults;
                lock (_searchLock)
                {
                    // Limpiar waiter si aún existe
                    _resultWaiters.Remove(searchId);
                    
                    if (_pendingResults.TryGetValue(searchId, out var pendingList))
                    {
                        emuleResults = new List<EmuleSearchResult>(pendingList);
                        _pendingResults.Remove(searchId);
                        var recMsg = $"📥 eMule Provider: Recuperados {emuleResults.Count} resultados del diccionario";
                        System.Diagnostics.Debug.WriteLine(recMsg);
                        _emuleClient.GetType().GetMethod("Log", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(_emuleClient, new object[] { recMsg });
                    }
                    else
                    {
                        emuleResults = new List<EmuleSearchResult>();
                        var warnMsg = $"⚠️ eMule Provider: No se encontraron resultados en el diccionario para SearchId={searchId}";
                        System.Diagnostics.Debug.WriteLine(warnMsg);
                        _emuleClient.GetType().GetMethod("Log", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(_emuleClient, new object[] { warnMsg });
                    }
                }

                // Procesar resultados
                foreach (var emuleResult in emuleResults)
                {
                    var convertedResult = ConvertEmuleResult(emuleResult);

                    // Deduplicar
                    var key = $"{emuleResult.FileHash}_{convertedResult.SizeBytes}";
                    if (!seenFiles.Add(key))
                    {
                        continue;
                    }

                    results.Add(convertedResult);

                    // Notificar resultados parciales cada 10 archivos
                    if (results.Count % 10 == 0)
                    {
                        ResultsReceived?.Invoke(this, new SearchResultsReceivedEventArgs
                        {
                            SearchId = request.SearchId,
                            Results = new List<SearchResult>(results),
                            TotalResultsReceived = results.Count
                        });
                    }
                }

                    response.Results = results;
                    response.Status = SearchStatus.Completed;
                    response.Duration = DateTime.UtcNow - startTime;

                    System.Diagnostics.Debug.WriteLine($"[eMule Provider] Búsqueda completada: {results.Count} resultados finales");

                    // Notificar finalización
                    SearchCompleted?.Invoke(this, new SearchCompletedEventArgs
                    {
                        SearchId = request.SearchId,
                        Status = SearchStatus.Completed,
                        TotalResults = results.Count,
                        Duration = response.Duration
                    });
                }
                finally
                {
                    // Liberar el semáforo para permitir la siguiente búsqueda
                    _searchSemaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                response.Status = SearchStatus.Cancelled;
                response.ErrorMessage = "Búsqueda cancelada";
            }
            catch (TimeoutException)
            {
                response.Status = SearchStatus.TimedOut;
                response.ErrorMessage = "Búsqueda expiró";
            }
            catch (Exception ex)
            {
                response.Status = SearchStatus.Failed;
                response.ErrorMessage = ex.Message;
            }
            finally
            {
                lock (_searchLock)
                {
                    _activeSearches.Remove(request.SearchId);
                }
                cts.Dispose();
            }

            return response;
        }

        public async Task CancelSearchAsync(string searchId)
        {
            CancellationTokenSource cts;

            lock (_searchLock)
            {
                if (!_activeSearches.TryGetValue(searchId, out cts))
                {
                    return;
                }
                _activeSearches.Remove(searchId);
            }

            cts.Cancel();
            cts.Dispose();

            await Task.CompletedTask;
        }

        private SearchResult ConvertEmuleResult(EmuleSearchResult emuleResult)
        {
            return new SearchResult
            {
                ResultId = Guid.NewGuid().ToString(),
                FileName = emuleResult.FileName,
                SizeBytes = emuleResult.FileSize,
                Username = "eMule",
                QueueLength = 0,
                FreeSlots = emuleResult.SourceCount > 0 ? emuleResult.SourceCount : (int?)null,
                BitRate = null,
                Duration = null,
                FileExtension = System.IO.Path.GetExtension(emuleResult.FileName),
                FileHash = emuleResult.FileHash,
                NetworkSource = "eMule",
                Metadata = new Dictionary<string, object>
                {
                    ["SourceCount"] = emuleResult.SourceCount,
                    ["CompleteSourceCount"] = emuleResult.CompleteSourceCount,
                    ["FileType"] = emuleResult.FileType
                }
            };
        }

    }
}
