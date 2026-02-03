using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SlskDown.Core;

namespace SlskDown.EMule
{
    /// <summary>
    /// Proveedor de búsqueda para eMule/ed2k
    /// Implementa ISearchProvider para búsquedas en la red eMule
    /// </summary>
    public class EMuleSearchProvider : ISearchProvider
    {
        private readonly INetworkClient _client;
        private readonly SlskDown.Core.EmuleWebClient _coreClient;
        private readonly EMuleClient _ecClient;
        private readonly Dictionary<string, SearchRequest> _activeSearches = new Dictionary<string, SearchRequest>();
        private readonly object _searchLock = new object();

        public string ProviderName => "eMule";

        public bool IsReady => _client?.IsConnected ?? _coreClient?.IsConnected ?? _ecClient?.IsConnected ?? false;

        public event EventHandler<SearchResultsReceivedEventArgs> ResultsReceived;
        public event EventHandler<SearchCompletedEventArgs> SearchCompleted;

        public EMuleSearchProvider(INetworkClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            
            // Si el cliente es EMuleClient (EC), guardarlo
            if (client is EMuleClient ecClient)
            {
                _ecClient = ecClient;
            }
        }

        public EMuleSearchProvider(SlskDown.Core.EmuleWebClient coreClient)
        {
            _coreClient = coreClient ?? throw new ArgumentNullException(nameof(coreClient));
            
            // Suscribirse al evento de resultados del cliente Core
            if (_coreClient != null)
            {
                _coreClient.SearchResultsReceived += OnCoreClientSearchResultsReceived;
            }
        }
        
        public EMuleSearchProvider(EMuleClient ecClient)
        {
            _ecClient = ecClient ?? throw new ArgumentNullException(nameof(ecClient));
        }
        
        private void OnCoreClientSearchResultsReceived(object sender, SlskDown.Core.EmuleSearchResultsEventArgs e)
        {
            // Convertir EmuleSearchResult a Core.SearchResult
            var coreResults = e.Results.Select(r => new Core.SearchResult
            {
                ResultId = r.FileHash,
                FileName = r.FileName,
                SizeBytes = r.FileSize,
                FileExtension = r.FileType,
                FileHash = r.FileHash,
                Username = "eMule",
                NetworkSource = "eMule",
                QueueLength = 0,
                FreeSlots = r.SourceCount,
                Metadata = new Dictionary<string, object>
                {
                    { "SourceCount", r.SourceCount },
                    { "CompleteSourceCount", r.CompleteSourceCount }
                }
            }).ToList();
            
            // Disparar evento de resultados recibidos
            ResultsReceived?.Invoke(this, new SearchResultsReceivedEventArgs
            {
                SearchId = e.SearchId,
                Results = coreResults
            });
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

            lock (_searchLock)
            {
                _activeSearches[request.SearchId] = request;
            }

            var response = new SearchResponse
            {
                SearchId = request.SearchId,
                Status = SearchStatus.InProgress,
                Results = new List<Core.SearchResult>()
            };

            var startTime = DateTime.UtcNow;

            try
            {
                // Prioridad 1: EMuleClient (EC) - Mejor control, puede detener búsquedas anteriores
                if (_ecClient != null)
                {
                    var emuleResults = await _ecClient.SearchAsync(request.Query);
                    
                    // Convertir EmuleSearchResult a Core.SearchResult
                    response.Results = emuleResults.Select(r => new Core.SearchResult
                    {
                        ResultId = r.FileHash,
                        FileName = r.FileName,
                        SizeBytes = r.FileSize,
                        FileExtension = r.FileType,
                        FileHash = r.FileHash,
                        Username = "eMule",
                        NetworkSource = "eMule",
                        QueueLength = 0,
                        FreeSlots = r.SourceCount,
                        Metadata = new Dictionary<string, object>
                        {
                            { "SourceCount", r.SourceCount },
                            { "CompleteSourceCount", r.CompleteSourceCount }
                        }
                    }).ToList();
                    
                    response.Status = SearchStatus.Completed;
                }
                // Prioridad 2: Core.EmuleWebClient (WebServer) - Limitación: acumula historial
                else if (_coreClient != null)
                {
                    // Crear TaskCompletionSource para esperar los resultados del evento
                    var tcs = new TaskCompletionSource<List<Core.SearchResult>>();
                    
                    // Handler temporal para capturar los resultados
                    EventHandler<SlskDown.Core.EmuleSearchResultsEventArgs> handler = null;
                    handler = (s, e) =>
                    {
                        var results = e.Results.Select(r => new Core.SearchResult
                        {
                            ResultId = r.FileHash,
                            FileName = r.FileName,
                            SizeBytes = r.FileSize,
                            FileExtension = r.FileType,
                            FileHash = r.FileHash,
                            Username = "eMule",
                            NetworkSource = "eMule",
                            QueueLength = 0,
                            FreeSlots = r.SourceCount,
                            Metadata = new Dictionary<string, object>
                            {
                                { "SourceCount", r.SourceCount },
                                { "CompleteSourceCount", r.CompleteSourceCount }
                            }
                        }).ToList();
                        
                        tcs.TrySetResult(results);
                        _coreClient.SearchResultsReceived -= handler;
                    };
                    
                    _coreClient.SearchResultsReceived += handler;
                    
                    try
                    {
                        // Iniciar búsqueda
                        var searchId = await _coreClient.SearchAsync(request.Query, cancellationToken);
                        
                        // Esperar resultados con timeout
                        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(35), cancellationToken);
                        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
                        
                        if (completedTask == tcs.Task)
                        {
                            response.Results = await tcs.Task;
                            response.Status = SearchStatus.Completed;
                        }
                        else
                        {
                            response.Status = SearchStatus.TimedOut;
                            response.ErrorMessage = "Timeout esperando resultados de eMule";
                        }
                    }
                    finally
                    {
                        _coreClient.SearchResultsReceived -= handler;
                    }
                }
                // Prioridad 3: Fallback al cliente antiguo
                else
                {
                    var webClient = _client as EMuleWebClient;
                    if (webClient != null)
                    {
                        var results = await webClient.SearchAsync(request.Query);
                        response.Results = results.ToList();
                        response.Status = SearchStatus.Completed;
                    }
                    else
                    {
                        response.Status = SearchStatus.Failed;
                        response.ErrorMessage = "Cliente eMule no soporta búsquedas";
                    }
                }
            }
            catch (OperationCanceledException)
            {
                response.Status = SearchStatus.Cancelled;
            }
            catch (Exception ex)
            {
                response.Status = SearchStatus.Failed;
                response.ErrorMessage = ex.Message;
            }
            finally
            {
                response.Duration = DateTime.UtcNow - startTime;

                lock (_searchLock)
                {
                    _activeSearches.Remove(request.SearchId);
                }

                SearchCompleted?.Invoke(this, new SearchCompletedEventArgs
                {
                    SearchId = request.SearchId,
                    Status = response.Status,
                    TotalResults = response.Results.Count,
                    Duration = response.Duration,
                    ErrorMessage = response.ErrorMessage
                });
            }

            return response;
        }

        public async Task CancelSearchAsync(string searchId)
        {
            lock (_searchLock)
            {
                _activeSearches.Remove(searchId);
            }
            await Task.CompletedTask;
        }
    }
}
