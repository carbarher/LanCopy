using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// Procesamiento paralelo de bÃºsquedas de autores
    /// </summary>
    public class ParallelAuthorSearch
    {
        private readonly int _maxConcurrency;
        private readonly SemaphoreSlim _semaphore;
        private static readonly ConcurrentDictionary<string, Task> activeAuthorSearches = new();

        public ParallelAuthorSearch(int maxConcurrency = 5)
        {
            _maxConcurrency = maxConcurrency;
            _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        }

        /// <summary>
        /// Procesa mÃºltiples autores en paralelo con lÃ­mite de concurrencia
        /// </summary>
        public async Task ProcessAuthorsAsync(
            List<string> authors,
            Func<string, int, int, Task> processAuthorFunc,
            CancellationToken cancellationToken = default)
        {
            var tasks = new List<Task>();
            int currentIndex = 0;

            foreach (var author in authors)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                currentIndex++;
                int localIndex = currentIndex;
                string localAuthor = author;

                var task = ProcessAuthorWithSemaphoreAsync(
                    localAuthor,
                    localIndex,
                    authors.Count,
                    processAuthorFunc,
                    cancellationToken
                );

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
        }

        private async Task ProcessAuthorWithSemaphoreAsync(
            string author,
            int index,
            int total,
            Func<string, int, int, Task> processFunc,
            CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                await processFunc(author, index, total);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            _semaphore?.Dispose();
        }
    }

    /// <summary>
    /// CachÃ© de bÃºsqueda de paÃ­ses con batch loading
    /// </summary>
    public class CountryCacheBatch
    {
        private readonly Dictionary<string, string> _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();
        private readonly ICountryCacheService _cacheService;

        public CountryCacheBatch(ICountryCacheService cacheService)
        {
            _cacheService = cacheService;
        }

        /// <summary>
        /// Obtiene paÃ­ses para mÃºltiples usuarios en una sola operaciÃ³n
        /// </summary>
        public async Task<Dictionary<string, string>> GetCountriesBatchAsync(IEnumerable<string> usernames)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var toFetch = new List<string>();

            // Verificar cachÃ© primero
            lock (_lock)
            {
                foreach (var username in usernames)
                {
                    if (_cache.TryGetValue(username, out var country))
                    {
                        result[username] = country;
                    }
                    else
                    {
                        toFetch.Add(username);
                    }
                }
            }

            // Fetch los que faltan (en paralelo)
            if (toFetch.Count > 0)
            {
                var fetchTasks = toFetch.Select(async username =>
                {
                    var country = await _cacheService.GetCountryAsync(username);
                    return (username, country);
                });

                var fetched = await Task.WhenAll(fetchTasks);

                lock (_lock)
                {
                    foreach (var (username, country) in fetched)
                    {
                        _cache[username] = country;
                        result[username] = country;
                    }
                }
            }

            return result;
        }

        public void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
            }
        }
    }

}

