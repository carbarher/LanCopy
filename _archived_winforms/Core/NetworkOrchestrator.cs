using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Orquestador que gestiona múltiples redes P2P (Soulseek)
    /// Coordina búsquedas paralelas, deduplicación y priorización de fuentes
    /// </summary>
    public class NetworkOrchestrator : IDisposable
    {
        private readonly Dictionary<string, INetworkClient> _clients = new Dictionary<string, INetworkClient>();
        private readonly Dictionary<string, ISearchProvider> _searchProviders = new Dictionary<string, ISearchProvider>();
        private readonly PersistentSearchCache _cache = new PersistentSearchCache();
        private readonly object _lock = new object();

        public event EventHandler<NetworkStatusEventArgs> NetworkStatusChanged;
        public event EventHandler<MultiNetworkSearchResultsEventArgs> SearchResultsReceived;
        public event EventHandler<PartialSearchResultsEventArgs> PartialResultsReceived;

        /// <summary>
        /// Caché compartido de resultados multi-red
        /// </summary>
        public PersistentSearchCache Cache => _cache;

        /// <summary>
        /// Registra un cliente de red
        /// </summary>
        public void RegisterClient(string networkName, INetworkClient client)
        {
            lock (_lock)
            {
                if (_clients.ContainsKey(networkName))
                {
                    throw new InvalidOperationException($"Red '{networkName}' ya está registrada");
                }

                _clients[networkName] = client;
                client.StateChanged += (s, e) => OnNetworkStateChanged(networkName, e);
            }
        }

        /// <summary>
        /// Registra un proveedor de búsqueda
        /// </summary>
        public void RegisterSearchProvider(string networkName, ISearchProvider provider)
        {
            lock (_lock)
            {
                if (_searchProviders.ContainsKey(networkName))
                {
                    throw new InvalidOperationException($"Proveedor de búsqueda '{networkName}' ya está registrado");
                }

                _searchProviders[networkName] = provider;

                provider.ResultsReceived += (s, e) => OnSearchResultsReceived(networkName, e);
                provider.SearchCompleted += (s, e) => OnSearchCompleted(networkName, e);
            }
        }

        /// <summary>
        /// Obtiene todos los clientes registrados
        /// </summary>
        public IReadOnlyDictionary<string, INetworkClient> GetClients()
        {
            lock (_lock)
            {
                return new Dictionary<string, INetworkClient>(_clients);
            }
        }

        /// <summary>
        /// Obtiene un cliente específico por nombre de red
        /// </summary>
        public INetworkClient GetClient(string networkName)
        {
            lock (_lock)
            {
                return _clients.TryGetValue(networkName, out var client) ? client : null;
            }
        }

        /// <summary>
        /// Obtiene todos los proveedores de búsqueda registrados
        /// </summary>
        public IReadOnlyDictionary<string, ISearchProvider> GetSearchProviders()
        {
            lock (_lock)
            {
                return new Dictionary<string, ISearchProvider>(_searchProviders);
            }
        }

        /// <summary>
        /// Realiza búsqueda en múltiples redes en paralelo
        /// </summary>
        public async Task<MultiNetworkSearchResponse> SearchAsync(
            SearchRequest request,
            IEnumerable<string> networks = null,
            CancellationToken cancellationToken = default)
        {
            // Determinar en qué redes buscar ANTES de consultar caché
            var targetNetworks = networks?.ToList() ?? _searchProviders.Keys.ToList();
            
            // Intentar obtener del caché primero
            if (true)
            {
                var cachedResults = _cache.Get(request.Query);
                if (cachedResults != null && cachedResults.Any())
                {
                    // IMPORTANTE: Filtrar resultados de caché por redes habilitadas
                    var filteredResults = cachedResults
                        .Where(r => targetNetworks.Contains(r.NetworkSource))
                        .ToList();
                    
                    if (filteredResults.Any())
                    {
                        return new MultiNetworkSearchResponse
                        {
                            SearchId = request.SearchId,
                            StartTime = DateTime.UtcNow,
                            EndTime = DateTime.UtcNow,
                            TotalDuration = TimeSpan.Zero,
                            DeduplicatedResults = filteredResults,
                            AllResults = filteredResults,
                            FromCache = true
                        };
                    }
                }
            }

            var response = new MultiNetworkSearchResponse
            {
                SearchId = request.SearchId,
                StartTime = DateTime.UtcNow,
                FromCache = false
            };
            var searchTasks = new List<Task<(string network, SearchResponse response)>>();

            // Ejecutar búsquedas EN PARALELO
            foreach (var networkName in targetNetworks)
            {
                if (!_searchProviders.TryGetValue(networkName, out var provider))
                {
                    continue;
                }

                if (!provider.IsReady)
                {
                    response.FailedNetworks.Add(networkName, "Proveedor no está listo");
                    continue;
                }

                // Lanzar búsqueda en paralelo
                var task = Task.Run(async () =>
                {
                    try
                    {
                        var networkResponse = await provider.SearchAsync(request, cancellationToken);
                        return (networkName, networkResponse);
                    }
                    catch (Exception ex)
                    {
                        return (networkName, new SearchResponse
                        {
                            SearchId = request.SearchId,
                            Status = SearchStatus.Failed,
                            ErrorMessage = ex.Message
                        });
                    }
                }, cancellationToken);

                searchTasks.Add(task);
            }

            // Procesar resultados a medida que llegan (resultados parciales)
            var completedTasks = new HashSet<Task<(string network, SearchResponse response)>>();
            
            while (searchTasks.Count > completedTasks.Count)
            {
                var completedTask = await Task.WhenAny(searchTasks.Where(t => !completedTasks.Contains(t)));
                completedTasks.Add(completedTask);
                
                var (networkName, networkResponse) = await completedTask;
                
                // Notificar resultados parciales inmediatamente
                if (networkResponse.Status == SearchStatus.Completed && networkResponse.Results.Any())
                {
                    PartialResultsReceived?.Invoke(this, new PartialSearchResultsEventArgs
                    {
                        SearchId = request.SearchId,
                        NetworkName = networkName,
                        Results = networkResponse.Results,
                        IsComplete = false
                    });
                }
            }
            
            var results = await Task.WhenAll(searchTasks);

            // Consolidar resultados
            foreach (var (networkName, networkResponse) in results)
            {
                response.NetworkResponses[networkName] = networkResponse;

                if (networkResponse.Status == SearchStatus.Completed)
                {
                    response.AllResults.AddRange(networkResponse.Results);
                }
                else if (networkResponse.Status == SearchStatus.Failed)
                {
                    response.FailedNetworks[networkName] = networkResponse.ErrorMessage;
                }
            }

            // Deduplicar resultados
            response.DeduplicatedResults = DeduplicateResults(response.AllResults);

            // Guardar en caché si hay resultados
            if (response.DeduplicatedResults.Any())
            {
                _cache.Set(request.Query, response.DeduplicatedResults);
            }

            response.EndTime = DateTime.UtcNow;
            response.TotalDuration = response.EndTime - response.StartTime;

            return response;
        }

        /// <summary>
        /// Deduplica resultados de múltiples redes
        /// Prioriza por disponibilidad, velocidad y fuentes
        /// </summary>
        private List<SearchResult> DeduplicateResults(List<SearchResult> results)
        {
            // Agrupar por nombre de archivo (normalizado)
            var groups = results.GroupBy(r => NormalizeFileName(r.FileName));

            var deduplicated = new List<SearchResult>();

            foreach (var group in groups)
            {
                // Si hay múltiples fuentes del mismo archivo, elegir la mejor
                var best = group.OrderByDescending(r => CalculateResultScore(r)).First();

                // Añadir metadata de fuentes alternativas
                var alternativeSources = group.Where(r => r != best).ToList();
                if (alternativeSources.Any())
                {
                    best.Metadata["AlternativeSources"] = alternativeSources.Count;
                    best.Metadata["Networks"] = string.Join(", ", group.Select(r => r.NetworkSource).Distinct());
                }

                deduplicated.Add(best);
            }

            return deduplicated;
        }

        /// <summary>
        /// Normaliza nombre de archivo para comparación
        /// </summary>
        private string NormalizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return string.Empty;
            }

            // Remover extensión, espacios, guiones, puntos
            var normalized = System.IO.Path.GetFileNameWithoutExtension(fileName);
            normalized = normalized.Replace(" ", "").Replace("-", "").Replace("_", "").Replace(".", "");
            return normalized.ToLowerInvariant();
        }

        /// <summary>
        /// Calcula puntuación de un resultado para priorización
        /// Mayor puntuación = mejor resultado
        /// </summary>
        private double CalculateResultScore(SearchResult result)
        {
            double score = 0;

            // Priorizar por slots libres
            if (result.FreeSlots.HasValue && result.FreeSlots.Value > 0)
            {
                score += 100;
            }

            // Penalizar por longitud de cola
            if (result.QueueLength > 0)
            {
                score -= result.QueueLength * 2;
            }

            // Priorizar bitrate alto (para audio)
            if (result.BitRate.HasValue)
            {
                score += result.BitRate.Value / 1000.0; // Convertir a Kbps
            }

            // Priorizar Soulseek
            if (result.NetworkSource == "Soulseek")
            {
                score += 50;
            }

            return score;
        }

        /// <summary>
        /// Obtiene estadísticas consolidadas de todas las redes
        /// </summary>
        public MultiNetworkStatistics GetStatistics()
        {
            var stats = new MultiNetworkStatistics();

            lock (_lock)
            {
                foreach (var (networkName, client) in _clients)
                {
                    var networkStats = client.GetStatistics();
                    stats.NetworkStats[networkName] = networkStats;

                    stats.TotalBytesDownloaded += networkStats.TotalBytesDownloaded;
                    stats.TotalBytesUploaded += networkStats.TotalBytesUploaded;
                    stats.TotalActiveDownloads += networkStats.ActiveDownloads;
                    stats.TotalConnectedPeers += networkStats.ConnectedPeers;
                }
            }

            return stats;
        }

        private void OnNetworkStateChanged(string networkName, NetworkStateChangedEventArgs e)
        {
            NetworkStatusChanged?.Invoke(this, new NetworkStatusEventArgs
            {
                NetworkName = networkName,
                State = e.CurrentState,
                Message = e.Message,
                Error = e.Error
            });
        }

        private void OnSearchResultsReceived(string networkName, SearchResultsReceivedEventArgs e)
        {
            SearchResultsReceived?.Invoke(this, new MultiNetworkSearchResultsEventArgs
            {
                NetworkName = networkName,
                SearchId = e.SearchId,
                Results = e.Results,
                TotalResultsReceived = e.TotalResultsReceived
            });
        }

        private void OnSearchCompleted(string networkName, SearchCompletedEventArgs e)
        {
            // Opcional: Notificar cuando una red específica completa su búsqueda
        }

        public void Dispose()
        {
            lock (_lock)
            {
                foreach (var client in _clients.Values)
                {
                    client?.Dispose();
                }
                _clients.Clear();
                _searchProviders.Clear();
            }
        }
    }

    /// <summary>
    /// Respuesta de búsqueda multi-red
    /// </summary>
    public class MultiNetworkSearchResponse
    {
        public string SearchId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan TotalDuration { get; set; }

        public Dictionary<string, SearchResponse> NetworkResponses { get; set; } = new Dictionary<string, SearchResponse>();
        public Dictionary<string, string> FailedNetworks { get; set; } = new Dictionary<string, string>();

        public List<SearchResult> AllResults { get; set; } = new List<SearchResult>();
        public List<SearchResult> DeduplicatedResults { get; set; } = new List<SearchResult>();

        public int TotalResults => AllResults.Count;
        public int UniqueResults => DeduplicatedResults.Count;
        
        /// <summary>
        /// Indica si los resultados provienen del caché
        /// </summary>
        public bool FromCache { get; set; }
    }

    /// <summary>
    /// Estadísticas consolidadas de múltiples redes
    /// </summary>
    public class MultiNetworkStatistics
    {
        public Dictionary<string, NetworkStatistics> NetworkStats { get; set; } = new Dictionary<string, NetworkStatistics>();

        public long TotalBytesDownloaded { get; set; }
        public long TotalBytesUploaded { get; set; }
        public int TotalActiveDownloads { get; set; }
        public int TotalConnectedPeers { get; set; }
    }

    /// <summary>
    /// Evento de cambio de estado de red
    /// </summary>
    public class NetworkStatusEventArgs : EventArgs
    {
        public string NetworkName { get; set; }
        public NetworkConnectionState State { get; set; }
        public string Message { get; set; }
        public Exception Error { get; set; }
    }

    /// <summary>
    /// Evento de resultados de búsqueda multi-red
    /// </summary>
    public class MultiNetworkSearchResultsEventArgs : EventArgs
    {
        public string NetworkName { get; set; }
        public string SearchId { get; set; }
        public List<SearchResult> Results { get; set; }
        public int TotalResultsReceived { get; set; }
    }
}
