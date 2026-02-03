using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Pipeline paralelo basado en System.Threading.Channels
    /// Procesamiento eficiente producer-consumer con backpressure
    /// </summary>
    public class ChannelPipelineService<TInput, TOutput>
    {
        private readonly Func<TInput, Task<TOutput>> _processor;
        private readonly int _maxConcurrency;
        private readonly int _bufferSize;

        public ChannelPipelineService(
            Func<TInput, Task<TOutput>> processor,
            int maxConcurrency = 4,
            int bufferSize = 100)
        {
            _processor = processor;
            _maxConcurrency = maxConcurrency;
            _bufferSize = bufferSize;
        }

        /// <summary>
        /// Procesa items en pipeline paralelo
        /// </summary>
        public async Task<List<TOutput>> ProcessAsync(
            IEnumerable<TInput> items,
            CancellationToken cancellationToken = default)
        {
            var channel = Channel.CreateBounded<TInput>(new BoundedChannelOptions(_bufferSize)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

            var results = new List<TOutput>();
            var resultsLock = new object();

            // Producer: escribe items al channel
            var producerTask = Task.Run(async () =>
            {
                try
                {
                    foreach (var item in items)
                    {
                        await channel.Writer.WriteAsync(item, cancellationToken);
                    }
                }
                finally
                {
                    channel.Writer.Complete();
                }
            }, cancellationToken);

            // Consumers: procesan items del channel
            var consumerTasks = Enumerable.Range(0, _maxConcurrency)
                .Select(async _ =>
                {
                    await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
                    {
                        var result = await _processor(item);
                        
                        lock (resultsLock)
                        {
                            results.Add(result);
                        }
                    }
                })
                .ToArray();

            // Esperar a que todo termine
            await Task.WhenAll(consumerTasks);
            await producerTask;

            return results;
        }

        /// <summary>
        /// Procesa items con progreso
        /// </summary>
        public async Task<List<TOutput>> ProcessWithProgressAsync(
            IEnumerable<TInput> items,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var itemsList = items.ToList();
            var totalItems = itemsList.Count;
            var processedCount = 0;

            var channel = Channel.CreateBounded<TInput>(new BoundedChannelOptions(_bufferSize)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

            var results = new List<TOutput>();
            var resultsLock = new object();

            // Producer
            var producerTask = Task.Run(async () =>
            {
                try
                {
                    foreach (var item in itemsList)
                    {
                        await channel.Writer.WriteAsync(item, cancellationToken);
                    }
                }
                finally
                {
                    channel.Writer.Complete();
                }
            }, cancellationToken);

            // Consumers
            var consumerTasks = Enumerable.Range(0, _maxConcurrency)
                .Select(async _ =>
                {
                    await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
                    {
                        var result = await _processor(item);
                        
                        lock (resultsLock)
                        {
                            results.Add(result);
                            processedCount++;
                            progress?.Report(processedCount * 100 / totalItems);
                        }
                    }
                })
                .ToArray();

            await Task.WhenAll(consumerTasks);
            await producerTask;

            return results;
        }
    }

    /// <summary>
    /// Pipeline multi-etapa
    /// </summary>
    public class MultiStagePipeline<T1, T2, T3>
    {
        private readonly Func<T1, Task<T2>> _stage1;
        private readonly Func<T2, Task<T3>> _stage2;
        private readonly int _bufferSize;

        public MultiStagePipeline(
            Func<T1, Task<T2>> stage1,
            Func<T2, Task<T3>> stage2,
            int bufferSize = 100)
        {
            _stage1 = stage1;
            _stage2 = stage2;
            _bufferSize = bufferSize;
        }

        /// <summary>
        /// Procesa items a través de 2 etapas en pipeline
        /// </summary>
        public async Task<List<T3>> ProcessAsync(
            IEnumerable<T1> items,
            CancellationToken cancellationToken = default)
        {
            var channel1 = Channel.CreateBounded<T2>(new BoundedChannelOptions(_bufferSize)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

            var results = new List<T3>();
            var resultsLock = new object();

            // Stage 1: T1 -> T2
            var stage1Task = Task.Run(async () =>
            {
                try
                {
                    foreach (var item in items)
                    {
                        var result = await _stage1(item);
                        await channel1.Writer.WriteAsync(result, cancellationToken);
                    }
                }
                finally
                {
                    channel1.Writer.Complete();
                }
            }, cancellationToken);

            // Stage 2: T2 -> T3
            var stage2Task = Task.Run(async () =>
            {
                await foreach (var item in channel1.Reader.ReadAllAsync(cancellationToken))
                {
                    var result = await _stage2(item);
                    
                    lock (resultsLock)
                    {
                        results.Add(result);
                    }
                }
            }, cancellationToken);

            await Task.WhenAll(stage1Task, stage2Task);

            return results;
        }
    }

    /// <summary>
    /// Pipeline de búsqueda optimizado
    /// </summary>
    public class SearchPipeline
    {
        private readonly int _maxConcurrency;

        public SearchPipeline(int maxConcurrency = 4)
        {
            _maxConcurrency = maxConcurrency;
        }

        /// <summary>
        /// Busca múltiples queries en paralelo
        /// </summary>
        public async Task<List<PipelineSearchResult>> SearchMultipleAsync(
            List<string> queries,
            Func<string, Task<List<SearchResultItem>>> searchFunc,
            CancellationToken cancellationToken = default)
        {
            var pipeline = new ChannelPipelineService<string, PipelineSearchResult>(
                async query =>
                {
                    var results = await searchFunc(query);
                    return new PipelineSearchResult
                    {
                        Query = query,
                        Items = results,
                        Timestamp = DateTime.UtcNow
                    };
                },
                _maxConcurrency);

            return await pipeline.ProcessAsync(queries, cancellationToken);
        }

        /// <summary>
        /// Pipeline completo: Buscar -> Filtrar -> Rankear
        /// </summary>
        public async Task<List<SearchResultItem>> SearchFilterRankAsync(
            List<string> queries,
            Func<string, Task<List<SearchResultItem>>> searchFunc,
            Func<SearchResultItem, bool> filter,
            Func<SearchResultItem, double> scorer,
            CancellationToken cancellationToken = default)
        {
            var channel1 = Channel.CreateBounded<List<SearchResultItem>>(
                new BoundedChannelOptions(10) { FullMode = BoundedChannelFullMode.Wait });

            var channel2 = Channel.CreateBounded<List<SearchResultItem>>(
                new BoundedChannelOptions(10) { FullMode = BoundedChannelFullMode.Wait });

            var finalResults = new List<SearchResultItem>();
            var resultsLock = new object();

            // Stage 1: Búsqueda
            var searchTask = Task.Run(async () =>
            {
                try
                {
                    foreach (var query in queries)
                    {
                        var results = await searchFunc(query);
                        await channel1.Writer.WriteAsync(results, cancellationToken);
                    }
                }
                finally
                {
                    channel1.Writer.Complete();
                }
            }, cancellationToken);

            // Stage 2: Filtrado
            var filterTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var results in channel1.Reader.ReadAllAsync(cancellationToken))
                    {
                        var filtered = results.Where(filter).ToList();
                        if (filtered.Count > 0)
                            await channel2.Writer.WriteAsync(filtered, cancellationToken);
                    }
                }
                finally
                {
                    channel2.Writer.Complete();
                }
            }, cancellationToken);

            // Stage 3: Ranking
            var rankTask = Task.Run(async () =>
            {
                await foreach (var results in channel2.Reader.ReadAllAsync(cancellationToken))
                {
                    var ranked = results
                        .Select(r => new { Result = r, Score = scorer(r) })
                        .OrderByDescending(x => x.Score)
                        .Select(x => x.Result)
                        .ToList();

                    lock (resultsLock)
                    {
                        finalResults.AddRange(ranked);
                    }
                }
            }, cancellationToken);

            await Task.WhenAll(searchTask, filterTask, rankTask);

            return finalResults;
        }
    }

    /// <summary>
    /// Pipeline de descargas
    /// </summary>
    public class DownloadPipeline
    {
        private readonly int _maxConcurrentDownloads;

        public DownloadPipeline(int maxConcurrentDownloads = 3)
        {
            _maxConcurrentDownloads = maxConcurrentDownloads;
        }

        /// <summary>
        /// Descarga múltiples archivos en paralelo con límite
        /// </summary>
        public async Task<List<DownloadResult>> DownloadMultipleAsync(
            List<PipelineDownloadTask> tasks,
            Func<PipelineDownloadTask, Task<bool>> downloadFunc,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var channel = Channel.CreateBounded<PipelineDownloadTask>(
                new BoundedChannelOptions(_maxConcurrentDownloads)
                {
                    FullMode = BoundedChannelFullMode.Wait
                });

            var results = new List<DownloadResult>();
            var resultsLock = new object();
            var completedCount = 0;

            // Producer
            var producerTask = Task.Run(async () =>
            {
                try
                {
                    foreach (var task in tasks)
                    {
                        await channel.Writer.WriteAsync(task, cancellationToken);
                    }
                }
                finally
                {
                    channel.Writer.Complete();
                }
            }, cancellationToken);

            // Consumers
            var consumerTasks = Enumerable.Range(0, _maxConcurrentDownloads)
                .Select(async workerId =>
                {
                    await foreach (var task in channel.Reader.ReadAllAsync(cancellationToken))
                    {
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        bool success = false;

                        try
                        {
                            success = await downloadFunc(task);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Download failed: {ex.Message}");
                        }

                        sw.Stop();

                        var result = new DownloadResult
                        {
                            Task = task,
                            Success = success,
                            Duration = sw.Elapsed,
                            WorkerId = workerId
                        };

                        lock (resultsLock)
                        {
                            results.Add(result);
                            completedCount++;

                            progress?.Report(new DownloadProgress
                            {
                                CompletedCount = completedCount,
                                TotalCount = tasks.Count,
                                CurrentFile = task.Filename,
                                Success = success
                            });
                        }
                    }
                })
                .ToArray();

            await Task.WhenAll(consumerTasks);
            await producerTask;

            return results;
        }
    }

    #region DTOs

    public class PipelineSearchResult
    {
        public string Query { get; set; } = "";
        public List<SearchResultItem> Items { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class PipelineDownloadTask
    {
        public string Filename { get; set; } = "";
        public string Url { get; set; } = "";
        public long Size { get; set; }
    }

    public class DownloadResult
    {
        public PipelineDownloadTask Task { get; set; } = new();
        public bool Success { get; set; }
        public TimeSpan Duration { get; set; }
        public int WorkerId { get; set; }
    }

    public class DownloadProgress
    {
        public int CompletedCount { get; set; }
        public int TotalCount { get; set; }
        public string CurrentFile { get; set; } = "";
        public bool Success { get; set; }
    }

    #endregion

    /// <summary>
    /// Benchmark Channel vs Task.WhenAll
    /// </summary>
    public class ChannelBenchmark
    {
        public static async Task RunBenchmarkAsync(int itemCount = 1000)
        {
            var items = Enumerable.Range(0, itemCount).ToList();

            // Benchmark Task.WhenAll (sin límite de concurrencia)
            var sw1 = System.Diagnostics.Stopwatch.StartNew();
            var results1 = await Task.WhenAll(items.Select(async i =>
            {
                await Task.Delay(1);
                return i * 2;
            }));
            sw1.Stop();

            // Benchmark Channel Pipeline (con límite)
            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            var pipeline = new ChannelPipelineService<int, int>(
                async i =>
                {
                    await Task.Delay(1);
                    return i * 2;
                },
                maxConcurrency: 10);
            var results2 = await pipeline.ProcessAsync(items);
            sw2.Stop();

            System.Diagnostics.Debug.WriteLine($"Channel Pipeline Benchmark ({itemCount} items):");
            System.Diagnostics.Debug.WriteLine($"  Task.WhenAll: {sw1.ElapsedMilliseconds}ms");
            System.Diagnostics.Debug.WriteLine($"  Channel:      {sw2.ElapsedMilliseconds}ms");
            System.Diagnostics.Debug.WriteLine($"  Results match: {results1.Length == results2.Count}");
            System.Diagnostics.Debug.WriteLine($"  Benefit: Controlled concurrency + backpressure");
        }
    }
}
