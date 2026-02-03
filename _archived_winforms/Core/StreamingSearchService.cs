using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Servicio de búsqueda con streaming de resultados usando IAsyncEnumerable
    /// Permite actualizar UI conforme llegan resultados sin esperar a que termine toda la búsqueda
    /// </summary>
    public class StreamingSearchService
    {
        private readonly Func<string, CancellationToken, Task<List<SearchResultItem>>> _searchFunc;

        public StreamingSearchService(Func<string, CancellationToken, Task<List<SearchResultItem>>> searchFunc)
        {
            _searchFunc = searchFunc;
        }

        /// <summary>
        /// Busca y retorna resultados conforme van llegando
        /// </summary>
        public async IAsyncEnumerable<SearchResultItem> SearchStreamingAsync(
            string query,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var results = await _searchFunc(query, cancellationToken);
            
            foreach (var result in results)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return result;
            }
        }

        /// <summary>
        /// Busca con filtrado en tiempo real
        /// </summary>
        public async IAsyncEnumerable<SearchResultItem> SearchWithFilterStreamingAsync(
            string query,
            Func<SearchResultItem, bool> filter,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var result in SearchStreamingAsync(query, cancellationToken))
            {
                if (filter(result))
                    yield return result;
            }
        }

        /// <summary>
        /// Busca en múltiples fuentes y combina resultados en tiempo real
        /// </summary>
        public async IAsyncEnumerable<SearchResultItem> SearchMultiSourceStreamingAsync(
            string query,
            IEnumerable<Func<string, CancellationToken, Task<List<SearchResultItem>>>> sources,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var tasks = sources.Select(source => Task.Run(async () => 
            {
                try
                {
                    return await source(query, cancellationToken);
                }
                catch
                {
                    return new List<SearchResultItem>();
                }
            }, cancellationToken)).ToList();

            // Retornar resultados conforme cada fuente completa
            while (tasks.Count > 0)
            {
                var completed = await Task.WhenAny(tasks);
                tasks.Remove(completed);

                var results = await completed;
                foreach (var result in results)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return result;
                }
            }
        }

        /// <summary>
        /// Busca con deduplicación en tiempo real
        /// </summary>
        public async IAsyncEnumerable<SearchResultItem> SearchWithDeduplicationAsync(
            string query,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var seen = new HashSet<string>();

            await foreach (var result in SearchStreamingAsync(query, cancellationToken))
            {
                var key = $"{result.Filename}|{result.Username}|{result.Size}";
                
                if (seen.Add(key))
                    yield return result;
            }
        }

        /// <summary>
        /// Busca con límite de resultados
        /// </summary>
        public async IAsyncEnumerable<SearchResultItem> SearchWithLimitAsync(
            string query,
            int maxResults,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            int count = 0;

            await foreach (var result in SearchStreamingAsync(query, cancellationToken))
            {
                if (count >= maxResults)
                    yield break;

                yield return result;
                count++;
            }
        }

        /// <summary>
        /// Busca con transformación en tiempo real
        /// </summary>
        public async IAsyncEnumerable<T> SearchWithTransformAsync<T>(
            string query,
            Func<SearchResultItem, T> transform,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var result in SearchStreamingAsync(query, cancellationToken))
            {
                yield return transform(result);
            }
        }
    }

    /// <summary>
    /// Extensiones para IAsyncEnumerable
    /// </summary>
    public static class AsyncEnumerableExtensions
    {
        /// <summary>
        /// Convierte IAsyncEnumerable a List de forma eficiente
        /// </summary>
        public static async Task<List<T>> ToListAsync<T>(
            this IAsyncEnumerable<T> source,
            CancellationToken cancellationToken = default)
        {
            var list = new List<T>();
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                list.Add(item);
            }
            return list;
        }

        /// <summary>
        /// Filtra IAsyncEnumerable
        /// </summary>
        public static async IAsyncEnumerable<T> WhereAsync<T>(
            this IAsyncEnumerable<T> source,
            Func<T, bool> predicate,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                if (predicate(item))
                    yield return item;
            }
        }

        /// <summary>
        /// Transforma IAsyncEnumerable
        /// </summary>
        public static async IAsyncEnumerable<TResult> SelectAsync<T, TResult>(
            this IAsyncEnumerable<T> source,
            Func<T, TResult> selector,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                yield return selector(item);
            }
        }

        /// <summary>
        /// Toma los primeros N elementos
        /// </summary>
        public static async IAsyncEnumerable<T> TakeAsync<T>(
            this IAsyncEnumerable<T> source,
            int count,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            int taken = 0;
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                if (taken >= count)
                    yield break;

                yield return item;
                taken++;
            }
        }

        /// <summary>
        /// Agrupa resultados en batches
        /// </summary>
        public static async IAsyncEnumerable<List<T>> BatchAsync<T>(
            this IAsyncEnumerable<T> source,
            int batchSize,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var batch = new List<T>(batchSize);

            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                batch.Add(item);

                if (batch.Count >= batchSize)
                {
                    yield return batch;
                    batch = new List<T>(batchSize);
                }
            }

            if (batch.Count > 0)
                yield return batch;
        }

        /// <summary>
        /// Ejecuta acción por cada elemento sin consumir el stream
        /// </summary>
        public static async IAsyncEnumerable<T> DoAsync<T>(
            this IAsyncEnumerable<T> source,
            Action<T> action,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                action(item);
                yield return item;
            }
        }

        /// <summary>
        /// Cuenta elementos sin consumir memoria
        /// </summary>
        public static async Task<int> CountAsync<T>(
            this IAsyncEnumerable<T> source,
            CancellationToken cancellationToken = default)
        {
            int count = 0;
            await foreach (var _ in source.WithCancellation(cancellationToken))
            {
                count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Ejemplo de uso con UI
    /// </summary>
    public class StreamingSearchExample
    {
        private readonly StreamingSearchService _searchService;

        public StreamingSearchExample(StreamingSearchService searchService)
        {
            _searchService = searchService;
        }

        /// <summary>
        /// Busca y actualiza UI en tiempo real
        /// </summary>
        public async Task SearchAndUpdateUIAsync(
            string query,
            Action<SearchResultItem> addToUI,
            CancellationToken cancellationToken = default)
        {
            var count = 0;

            await foreach (var result in _searchService
                .SearchWithFilterStreamingAsync(query, r => r.Quality > 50, cancellationToken)
                .TakeAsync(1000))
            {
                // Actualizar UI conforme llegan resultados
                addToUI(result);
                count++;

                // Actualizar contador cada 10 resultados
                if (count % 10 == 0)
                {
                    // UpdateStatusLabel($"Encontrados {count} resultados...");
                }
            }

            // UpdateStatusLabel($"Búsqueda completada: {count} resultados");
        }

        /// <summary>
        /// Busca en batches para actualización eficiente de UI
        /// </summary>
        public async Task SearchInBatchesAsync(
            string query,
            Action<List<SearchResultItem>> addBatchToUI,
            CancellationToken cancellationToken = default)
        {
            await foreach (var batch in _searchService
                .SearchStreamingAsync(query, cancellationToken)
                .BatchAsync(100)) // Actualizar UI cada 100 resultados
            {
                addBatchToUI(batch);
            }
        }
    }
}
