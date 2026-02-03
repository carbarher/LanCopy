using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soulseek;
using SlskDown.Core.Services.Search;

namespace SlskDown.Core.Services.Search
{
    /// <summary>
    /// Implementación por defecto del flujo de búsqueda sobre Soulseek.
    /// Encapsula rate limiting, filtrado y caché.
    /// </summary>
    public sealed class SearchWorkflow : ISearchWorkflow
    {
        private readonly SoulseekClient client;
        private readonly ISearchFilterService filterService;
        private readonly ISearchRateLimiter rateLimiter;
        private readonly SearchCacheService? cacheService;
        private readonly Func<IReadOnlyList<SearchResultItem>, IReadOnlyList<SearchResultItem>>? postProcessor;
        private readonly Action<string>? logger;

        public SearchWorkflow(
            SoulseekClient client,
            ISearchFilterService filterService,
            ISearchRateLimiter rateLimiter,
            SearchCacheService? cacheService = null,
            Func<IReadOnlyList<SearchResultItem>, IReadOnlyList<SearchResultItem>>? postProcessor = null,
            Action<string>? logger = null)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.filterService = filterService ?? throw new ArgumentNullException(nameof(filterService));
            this.rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
            this.cacheService = cacheService;
            this.postProcessor = postProcessor;
            this.logger = logger;
        }

        public async Task<SearchWorkflowResult> ExecuteAsync(SearchWorkflowRequest request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return new SearchWorkflowResult();
            }

            // Intentar leer desde caché primero.
            if (cacheService != null && request.UseCache &&
                cacheService.TryGet(request.Query, out var cachedItems) && cachedItems.Any())
            {
                logger?.Invoke($"Cache hit para '{request.Query}' ({cachedItems.Count} items)");
                return new SearchWorkflowResult
                {
                    Items = cachedItems,
                    FromCache = true,
                    Stats = new SearchWorkflowStats
                    {
                        TotalFilesAccepted = cachedItems.Count,
                        Duration = TimeSpan.Zero
                    }
                };
            }

            var stopwatch = Stopwatch.StartNew();
            var items = new List<SearchResultItem>();
            var statsBuilder = new StatsBuilder();

            try
            {
                await rateLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
                logger?.Invoke($"Buscando '{request.Query}' (timeout: {request.Timeout.TotalSeconds:F0}s, limit: {request.ResponseLimit})");

                var searchOptions = new SearchOptions(
                    searchTimeout: (int)Math.Min(int.MaxValue, request.Timeout.TotalMilliseconds),
                    responseLimit: request.ResponseLimit,
                    fileLimit: request.FileLimit,
                    filterResponses: true,
                    minimumResponseFileCount: 1,
                    minimumPeerUploadSpeed: 0);

                try
                {
                    var searchTask = client.SearchAsync(
                        SearchQuery.FromText(request.Query),
                        options: searchOptions,
                        cancellationToken: cancellationToken);

                    var (_, responses) = await searchTask.ConfigureAwait(false);
                    foreach (Soulseek.SearchResponse response in responses)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        statsBuilder.ResponsesProcessed++;

                        var filterResult = filterService.FilterResponse(response, request.Filters);

                        statsBuilder.FilesProcessed += filterResult.TotalFilesEvaluated;
                        statsBuilder.FilesFilteredBySize += filterResult.FilteredBySize;
                        statsBuilder.FilesFilteredByExtension += filterResult.FilteredByExtension;
                        statsBuilder.FilesFilteredBySpanish += filterResult.FilteredBySpanish;
                        statsBuilder.FilesFilteredByBlacklist += filterResult.FilteredByBlacklist;

                        if (filterResult.AcceptedFiles.Count == 0)
                        {
                            continue;
                        }

                        foreach (var file in filterResult.AcceptedFiles)
                        {
                            var item = ConvertToItem(response, file);
                            items.Add(item);
                        }
                    }

                    rateLimiter.RecordSuccess();
                }
                catch (OperationCanceledException)
                {
                    rateLimiter.RecordFailure();
                    throw;
                }
                catch (TimeoutException ex)
                {
                    rateLimiter.RecordFailure();
                    logger?.Invoke($"Timeout buscando '{request.Query}': {ex.Message}");
                    return BuildResult(items, statsBuilder, stopwatch.Elapsed, timedOut: true);
                }
                catch (Exception ex)
                {
                    rateLimiter.RecordFailure();
                    logger?.Invoke($"Error en búsqueda '{request.Query}': {ex.Message}");
                    throw;
                }

                statsBuilder.TotalFilesAccepted = items.Count;

                IReadOnlyList<SearchResultItem> finalItems = items;
                if (postProcessor != null && finalItems.Count > 0)
                {
                    finalItems = postProcessor(finalItems);
                }

                if (cacheService != null && request.UseCache && finalItems.Count > 0)
                {
                    cacheService.Store(request.Query, finalItems.ToList());
                }

                stopwatch.Stop();

                return new SearchWorkflowResult
                {
                    Items = finalItems,
                    Stats = statsBuilder.Build(stopwatch.Elapsed)
                };
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                return BuildResult(items, statsBuilder, stopwatch.Elapsed, cancelled: true);
            }
        }

        private SearchWorkflowResult BuildResult(
            IReadOnlyList<SearchResultItem> items,
            StatsBuilder statsBuilder,
            TimeSpan duration,
            bool cancelled = false,
            bool timedOut = false)
        {
            statsBuilder.TotalFilesAccepted = items.Count;

            return new SearchWorkflowResult
            {
                Items = items,
                Cancelled = cancelled,
                TimedOut = timedOut,
                Stats = statsBuilder.Build(duration)
            };
        }

        private static SearchResultItem ConvertToItem(Soulseek.SearchResponse response, Soulseek.File file)
        {
            var extension = System.IO.Path.GetExtension(file.Filename)?.ToLowerInvariant() ?? string.Empty;
            var folder = System.IO.Path.GetDirectoryName(file.Filename) ?? string.Empty;

            return new SearchResultItem
            {
                Username = response.Username,
                Filename = System.IO.Path.GetFileName(file.Filename),
                Size = file.Size,
                Extension = extension,
                FolderPath = folder,
                Bitrate = file.BitRate ?? 0,
                Length = file.Length ?? 0,
                UploadSpeed = response.UploadSpeed,
                QueueLength = response.QueueLength,
                FreeUploadSlots = 0,
                AddedAt = DateTime.UtcNow,
                IsDownloaded = false,
                IsQueued = false,
                QualityScore = 0,
                Network = "Soulseek",
                Author = string.Empty
            };
        }

        private sealed class StatsBuilder
        {
            public int ResponsesProcessed { get; set; }
            public int FilesProcessed { get; set; }
            public int FilesFilteredBySize { get; set; }
            public int FilesFilteredByExtension { get; set; }
            public int FilesFilteredBySpanish { get; set; }
            public int FilesFilteredByBlacklist { get; set; }
            public int TotalFilesAccepted { get; set; }

            public SearchWorkflowStats Build(TimeSpan duration) => new SearchWorkflowStats
            {
                ResponsesProcessed = ResponsesProcessed,
                FilesProcessed = FilesProcessed,
                FilesFilteredBySize = FilesFilteredBySize,
                FilesFilteredByExtension = FilesFilteredByExtension,
                FilesFilteredBySpanish = FilesFilteredBySpanish,
                FilesFilteredByBlacklist = FilesFilteredByBlacklist,
                TotalFilesAccepted = TotalFilesAccepted,
                Duration = duration
            };
        }
    }
}
