using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soulseek;

namespace SlskDown.Core
{
    /// <summary>
    /// Proveedor de búsqueda para Soulseek que implementa ISearchProvider
    /// Permite búsquedas uniformes junto con otras redes P2P
    /// </summary>
    public class SoulseekSearchProvider : ISearchProvider
    {
        private readonly SoulseekClient _client;
        private readonly Dictionary<string, CancellationTokenSource> _activeSearches = new Dictionary<string, CancellationTokenSource>();
        private readonly object _searchLock = new object();

        public string ProviderName => "Soulseek";

        public bool IsReady => _client?.State.HasFlag(SoulseekClientStates.Connected) == true &&
                               _client?.State.HasFlag(SoulseekClientStates.LoggedIn) == true;

        public event EventHandler<SearchResultsReceivedEventArgs> ResultsReceived;
        public event EventHandler<SearchCompletedEventArgs> SearchCompleted;

        public SoulseekSearchProvider(SoulseekClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
        {
            if (!IsReady)
            {
                return new SearchResponse
                {
                    SearchId = request.SearchId,
                    Status = SearchStatus.Failed,
                    ErrorMessage = "Cliente Soulseek no está conectado"
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
                // Configurar opciones de búsqueda Soulseek
                var searchOptions = new SearchOptions(
                    searchTimeout: (int)request.Timeout.TotalMilliseconds,
                    responseLimit: request.MaxResults,
                    filterResponses: true,
                    minimumResponseFileCount: 1,
                    minimumPeerUploadSpeed: 0
                );

                // Realizar búsqueda
                var (search, responses) = await _client.SearchAsync(
                    SearchQuery.FromText(request.Query),
                    options: searchOptions,
                    cancellationToken: cts.Token
                );

                // Procesar respuestas
                foreach (var searchResponse in responses ?? Enumerable.Empty<Soulseek.SearchResponse>())
                {
                    var convertedResults = ConvertSoulseekResponse(searchResponse, request);

                    // Aplicar filtros
                    var filteredResults = ApplyFilters(convertedResults, request.Filters);

                    // Deduplicar
                    var newResults = filteredResults.Where(r =>
                    {
                        var key = $"{r.Username}_{r.FileName}_{r.SizeBytes}";
                        return seenFiles.Add(key);
                    }).ToList();

                    if (newResults.Any())
                    {
                        results.AddRange(newResults);

                        // Notificar resultados parciales
                        ResultsReceived?.Invoke(this, new SearchResultsReceivedEventArgs
                        {
                            SearchId = request.SearchId,
                            Results = newResults,
                            TotalResultsReceived = results.Count
                        });
                    }

                    // Verificar límite de resultados
                    if (results.Count >= request.MaxResults)
                    {
                        break;
                    }
                }

                response.Results = results;
                response.Status = SearchStatus.Completed;
            }
            catch (OperationCanceledException)
            {
                response.Status = SearchStatus.Cancelled;
                response.Results = results;
            }
            catch (TimeoutException)
            {
                response.Status = SearchStatus.TimedOut;
                response.Results = results;
            }
            catch (Exception ex)
            {
                response.Status = SearchStatus.Failed;
                response.ErrorMessage = ex.Message;
                response.Results = results;
            }
            finally
            {
                response.Duration = DateTime.UtcNow - startTime;

                lock (_searchLock)
                {
                    _activeSearches.Remove(request.SearchId);
                }

                cts.Dispose();

                SearchCompleted?.Invoke(this, new SearchCompletedEventArgs
                {
                    SearchId = request.SearchId,
                    Status = response.Status,
                    TotalResults = results.Count,
                    Duration = response.Duration,
                    ErrorMessage = response.ErrorMessage
                });
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
            }

            cts.Cancel();
            await Task.CompletedTask;
        }

        private List<SearchResult> ConvertSoulseekResponse(Soulseek.SearchResponse slskResponse, SearchRequest request)
        {
            var results = new List<SearchResult>();

            foreach (var file in slskResponse.Files)
            {
                var result = new SearchResult
                {
                    ResultId = Guid.NewGuid().ToString(),
                    FileName = file.Filename,
                    SizeBytes = file.Size,
                    Username = slskResponse.Username,
                    QueueLength = slskResponse.QueueLength,
                    FreeSlots = slskResponse.HasFreeUploadSlot ? 1 : 0,
                    BitRate = file.BitRate,
                    Duration = file.Length,
                    FileExtension = System.IO.Path.GetExtension(file.Filename),
                    NetworkSource = "Soulseek"
                };

                // Añadir metadata adicional
                if (file.SampleRate.HasValue)
                {
                    result.Metadata["SampleRate"] = file.SampleRate.Value;
                }
                if (file.BitDepth.HasValue)
                {
                    result.Metadata["BitDepth"] = file.BitDepth.Value;
                }
                if (file.Code != 0)
                {
                    result.Metadata["Code"] = file.Code;
                }

                results.Add(result);
            }

            return results;
        }

        private List<SearchResult> ApplyFilters(List<SearchResult> results, SearchFilters filters)
        {
            if (filters == null)
            {
                return results;
            }

            var filtered = results.AsEnumerable();

            // Filtro por tamaño mínimo
            if (filters.MinSizeBytes.HasValue)
            {
                filtered = filtered.Where(r => r.SizeBytes >= filters.MinSizeBytes.Value);
            }

            // Filtro por tamaño máximo
            if (filters.MaxSizeBytes.HasValue)
            {
                filtered = filtered.Where(r => r.SizeBytes <= filters.MaxSizeBytes.Value);
            }

            // Filtro por extensiones
            if (filters.FileExtensions != null && filters.FileExtensions.Any())
            {
                var extensions = new HashSet<string>(filters.FileExtensions, StringComparer.OrdinalIgnoreCase);
                filtered = filtered.Where(r => extensions.Contains(r.FileExtension));
            }

            // Filtro por palabras excluidas
            if (filters.ExcludeKeywords != null && filters.ExcludeKeywords.Any())
            {
                foreach (var keyword in filters.ExcludeKeywords)
                {
                    filtered = filtered.Where(r => !r.FileName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                }
            }

            // Filtro por tipo de archivo
            if (filters.FileType.HasValue && filters.FileType.Value != FileType.Any)
            {
                filtered = filtered.Where(r => MatchesFileType(r, filters.FileType.Value));
            }

            // Filtro español (simplificado - busca indicadores comunes)
            if (filters.SpanishOnly)
            {
                filtered = filtered.Where(r =>
                    r.FileName.Contains("spanish", StringComparison.OrdinalIgnoreCase) ||
                    r.FileName.Contains("español", StringComparison.OrdinalIgnoreCase) ||
                    r.FileName.Contains("castellano", StringComparison.OrdinalIgnoreCase) ||
                    r.FileName.Contains("spa", StringComparison.OrdinalIgnoreCase)
                );
            }

            return filtered.ToList();
        }

        private bool MatchesFileType(SearchResult result, FileType fileType)
        {
            var ext = result.FileExtension?.ToLowerInvariant();

            return fileType switch
            {
                FileType.Audio => ext is ".mp3" or ".flac" or ".wav" or ".m4a" or ".ogg" or ".aac",
                FileType.Video => ext is ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".flv",
                FileType.Image => ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp",
                FileType.Document => ext is ".pdf" or ".doc" or ".docx" or ".txt" or ".epub" or ".mobi",
                FileType.Archive => ext is ".zip" or ".rar" or ".7z" or ".tar" or ".gz",
                FileType.Program => ext is ".exe" or ".msi" or ".dmg" or ".deb" or ".rpm",
                _ => true
            };
        }
    }
}
