using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core.Async
{
    /// <summary>
    /// Pipeline asíncrono reactivo para procesamiento de alto rendimiento
    /// </summary>
    public class ReactivePipeline<T> : IDisposable where T : class
    {
        private readonly Channel<T> _channel;
        private readonly Subject<T> _subject;
        private readonly List<Func<T, Task<T>>> _processors;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _processingTask;
        private readonly SemaphoreSlim _semaphore;
        private volatile bool _disposed = false;

        public int QueueSize => _channel.Reader.Count;
        public bool IsCompleted => _channel.Reader.Completion.IsCompleted;
        public IObservable<T> Output => _subject.AsObservable();

        public ReactivePipeline(int capacity = 1000, int maxConcurrency = Environment.ProcessorCount * 2)
        {
            var options = new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            };

            _channel = Channel.CreateBounded<T>(options);
            _subject = new Subject<T>();
            _processors = new List<Func<T, Task<T>>>();
            _cancellationTokenSource = new CancellationTokenSource();
            _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            _processingTask = StartProcessing(_cancellationTokenSource.Token);
        }

        /// <summary>
        /// Agrega un procesador al pipeline
        /// </summary>
        public ReactivePipeline<T> AddProcessor(Func<T, Task<T>> processor)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ReactivePipeline<T>));
            _processors.Add(processor);
            return this;
        }

        /// <summary>
        /// Agrega un procesador síncrono al pipeline
        /// </summary>
        public ReactivePipeline<T> AddProcessor(Func<T, T> processor)
        {
            return AddProcessor(item => Task.FromResult(processor(item)));
        }

        /// <summary>
        /// Agrega un procesador con filtro (puede descartar items)
        /// </summary>
        public ReactivePipeline<T> AddFilter(Func<T, bool> filter)
        {
            return AddProcessor(async item =>
            {
                await Task.Yield();
                return filter(item) ? item : null;
            });
        }

        /// <summary>
        /// Agrega un procesador con filtro asíncrono
        /// </summary>
        public ReactivePipeline<T> AddFilter(Func<T, Task<bool>> filter)
        {
            return AddProcessor(async item =>
            {
                return await filter(item) ? item : null;
            });
        }

        /// <summary>
        /// Envía un item al pipeline
        /// </summary>
        public async Task<bool> EnqueueAsync(T item)
        {
            if (_disposed) return false;

            try
            {
                return await _channel.Writer.WaitToWriteAsync(_cancellationTokenSource.Token)
                    && _channel.Writer.TryWrite(item);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        /// <summary>
        /// Envía múltiples items al pipeline
        /// </summary>
        public async Task<int> EnqueueBatchAsync(IEnumerable<T> items)
        {
            if (_disposed) return 0;

            int count = 0;
            foreach (var item in items)
            {
                if (await EnqueueAsync(item))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Procesa items concurrentemente
        /// </summary>
        private async Task StartProcessing(CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();

            while (!cancellationToken.IsCancellationRequested && await _channel.Reader.WaitToReadAsync(cancellationToken))
            {
                await _semaphore.WaitAsync(cancellationToken);
                
                var task = Task.Run(async () =>
                {
                    try
                    {
                        while (_channel.Reader.TryRead(out var item) && !cancellationToken.IsCancellationRequested)
                        {
                            var processedItem = await ProcessItem(item);
                            if (processedItem != null)
                            {
                                _subject.OnNext(processedItem);
                            }
                        }
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }, cancellationToken);

                tasks.Add(task);

                // Limpiar tareas completadas
                tasks.RemoveAll(t => t.IsCompleted);
            }

            // Esperar todas las tareas restantes
            await Task.WhenAll(tasks);
            _subject.OnCompleted();
        }

        /// <summary>
        /// Procesa un item a través de todos los procesadores
        /// </summary>
        private async Task<T> ProcessItem(T item)
        {
            if (item == null) return null;

            T current = item;
            
            foreach (var processor in _processors)
            {
                try
                {
                    current = await processor(current);
                    if (current == null) return null; // Item filtrado
                }
                catch (Exception ex)
                {
                    // Log error pero continuar con siguiente item
                    System.Diagnostics.Debug.WriteLine($"Error en procesador: {ex.Message}");
                    return null;
                }
            }

            return current;
        }

        /// <summary>
        /// Crea un pipeline de búsqueda reactivo
        /// </summary>
        public static ReactivePipeline<SearchRequest> CreateSearchPipeline()
        {
            return new ReactivePipeline<SearchRequest>(capacity: 500, maxConcurrency: 10)
                .AddProcessor(request => ValidateSearchRequest(request))
                .AddProcessor(request => ApplyRateLimiting(request))
                .AddProcessor(request => ExecuteSearch(request))
                .AddProcessor(request => ProcessSearchResults(request));
        }

        /// <summary>
        /// Crea un pipeline de descarga reactivo
        /// </summary>
        public static ReactivePipeline<DownloadRequest> CreateDownloadPipeline()
        {
            return new ReactivePipeline<DownloadRequest>(capacity: 1000, maxConcurrency: 20)
                .AddProcessor(request => ValidateDownloadRequest(request))
                .AddProcessor(request => CheckAvailability(request))
                .AddProcessor(request => QueueDownload(request))
                .AddProcessor(request => MonitorDownload(request));
        }

        /// <summary>
        /// Operación buffer para procesar lotes
        /// </summary>
        public IObservable<IList<T>> Buffer(TimeSpan timeSpan, int count)
        {
            return _subject
                .Buffer(timeSpan, count)
                .Where(list => list.Count > 0);
        }

        /// <summary>
        /// Operación throttle para limitar tasa de salida
        /// </summary>
        public IObservable<T> Throttle(TimeSpan dueTime)
        {
            return _subject.Throttle(dueTime);
        }

        /// <summary>
        /// Operación debounce para evitar duplicados
        /// </summary>
        public IObservable<T> Debounce(TimeSpan dueTime)
        {
            return _subject.Debounce(dueTime);
        }

        /// <summary>
        /// Operación distinct para eliminar duplicados
        /// </summary>
        public IObservable<T> DistinctUntilChanged()
        {
            return _subject.DistinctUntilChanged();
        }

        /// <summary>
        /// Filtra items por condición
        /// </summary>
        public IObservable<T> Where(Func<T, bool> predicate)
        {
            return _subject.Where(predicate);
        }

        /// <summary>
        /// Transforma items
        /// </summary>
        public IObservable<TResult> Select<TResult>(Func<T, TResult> selector)
        {
            return _subject.Select(selector);
        }

        /// <summary>
        /// Procesa items en paralelo
        /// </summary>
        public IObservable<TResult> SelectMany<TResult>(Func<T, IObservable<TResult>> selector)
        {
            return _subject.SelectMany(selector);
        }

        /// <summary>
        /// Libera recursos
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _cancellationTokenSource.Cancel();
            _channel.Writer.Complete();
            
            try
            {
                _processingTask.Wait(TimeSpan.FromSeconds(30));
            }
            catch (AggregateException)
            {
                // Ignorar errores de cancelación
            }

            _subject?.Dispose();
            _cancellationTokenSource?.Dispose();
            _semaphore?.Dispose();
        }

        #region Procesadores Específicos

        private static async Task<SearchRequest> ValidateSearchRequest(SearchRequest request)
        {
            await Task.Yield();
            
            if (string.IsNullOrWhiteSpace(request.Query))
                return null;
                
            if (request.Query.Length < 2)
                return null;

            return request;
        }

        private static async Task<SearchRequest> ApplyRateLimiting(SearchRequest request)
        {
            // Implementar rate limiting aquí
            await Task.Delay(100); // Simular delay
            return request;
        }

        private static async Task<SearchRequest> ExecuteSearch(SearchRequest request)
        {
            // Simular búsqueda
            await Task.Delay(500);
            request.Results = new List<string> { $"result1_{request.Query}", $"result2_{request.Query}" };
            return request;
        }

        private static async Task<SearchRequest> ProcessSearchResults(SearchRequest request)
        {
            // Procesar resultados
            await Task.Yield();
            request.ProcessedAt = DateTime.UtcNow;
            return request;
        }

        private static async Task<DownloadRequest> ValidateDownloadRequest(DownloadRequest request)
        {
            await Task.Yield();
            
            if (string.IsNullOrWhiteSpace(request.Filename))
                return null;

            return request;
        }

        private static async Task<DownloadRequest> CheckAvailability(DownloadRequest request)
        {
            await Task.Delay(200);
            request.IsAvailable = true;
            return request;
        }

        private static async Task<DownloadRequest> QueueDownload(DownloadRequest request)
        {
            await Task.Yield();
            request.QueuedAt = DateTime.UtcNow;
            return request;
        }

        private static async Task<DownloadRequest> MonitorDownload(DownloadRequest request)
        {
            await Task.Delay(1000);
            request.CompletedAt = DateTime.UtcNow;
            return request;
        }

        #endregion
    }

    /// <summary>
    /// Channel-based communications para alta concurrencia
    /// </summary>
    public class ChannelCommunicator<T> : IDisposable where T : class
    {
        private readonly Channel<T> _channel;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task[] _consumerTasks;
        private readonly Func<T, Task> _messageHandler;
        private volatile bool _disposed = false;

        public int QueueSize => _channel.Reader.Count;
        public bool IsCompleted => _channel.Reader.Completion.IsCompleted;

        public ChannelCommunicator(
            Func<T, Task> messageHandler,
            int capacity = 1000,
            int consumerCount = Environment.ProcessorCount)
        {
            var options = new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            };

            _channel = Channel.CreateBounded<T>(options);
            _cancellationTokenSource = new CancellationTokenSource();
            _messageHandler = messageHandler;

            _consumerTasks = new Task[consumerCount];
            for (int i = 0; i < consumerCount; i++)
            {
                _consumerTasks[i] = StartConsumer(i, _cancellationTokenSource.Token);
            }
        }

        /// <summary>
        /// Envía mensaje asíncronamente
        /// </summary>
        public async Task<bool> SendAsync(T message)
        {
            if (_disposed) return false;

            try
            {
                return await _channel.Writer.WaitToWriteAsync(_cancellationTokenSource.Token)
                    && _channel.Writer.TryWrite(message);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        /// <summary>
        /// Inicia consumidor de mensajes
        /// </summary>
        private async Task StartConsumer(int consumerId, CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var message in _channel.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        await _messageHandler(message);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Consumer {consumerId} error: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when shutting down
            }
        }

        /// <summary>
        /// Detiene comunicación
        /// </summary>
        public async Task StopAsync()
        {
            if (_disposed) return;

            _disposed = true;
            _cancellationTokenSource.Cancel();
            _channel.Writer.Complete();

            await Task.WhenAll(_consumerTasks);
        }

        public void Dispose()
        {
            if (_disposed) return;

            StopAsync().GetAwaiter().GetResult();
            _cancellationTokenSource.Dispose();
        }
    }

    /// <summary>
    /// Lock-free data structures para máximo rendimiento
    /// </summary>
    public class LockFreeCollection<T> where T : class
    {
        private readonly ConcurrentQueue<T> _queue;
        private volatile int _count;

        public int Count => _count;

        public LockFreeCollection()
        {
            _queue = new ConcurrentQueue<T>();
        }

        /// <summary>
        /// Agrega item sin locks
        /// </summary>
        public void Add(T item)
        {
            if (item == null) return;
            
            _queue.Enqueue(item);
            Interlocked.Increment(ref _count);
        }

        /// <summary>
        /// Intenta remover item sin locks
        /// </summary>
        public bool TryTake(out T item)
        {
            if (_queue.TryDequeue(out item))
            {
                Interlocked.Decrement(ref _count);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Limpia colección
        /// </summary>
        public void Clear()
        {
            while (_queue.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _count);
            }
        }

        /// <summary>
        /// Convierte a array (snapshot)
        /// </summary>
        public T[] ToArray()
        {
            return _queue.ToArray();
        }
    }

    /// <summary>
    /// Lock-free counter para estadísticas
    /// </summary>
    public class LockFreeCounter
    {
        private long _value;

        public long Value => _value;

        public LockFreeCounter(long initialValue = 0)
        {
            _value = initialValue;
        }

        /// <summary>
        /// Incrementa atómicamente
        /// </summary>
        public long Increment()
        {
            return Interlocked.Increment(ref _value);
        }

        /// <summary>
        /// Incrementa por delta atómicamente
        /// </summary>
        public long Add(long delta)
        {
            return Interlocked.Add(ref _value, delta);
        }

        /// <summary>
        /// Decrementa atómicamente
        /// </summary>
        public long Decrement()
        {
            return Interlocked.Decrement(ref _value);
        }

        /// <summary>
        /// Establece valor atómicamente
        /// </summary>
        public void Set(long value)
        {
            Interlocked.Exchange(ref _value, value);
        }

        /// <summary>
        /// Resetea a cero
        /// </summary>
        public void Reset()
        {
            Interlocked.Exchange(ref _value, 0);
        }
    }

    #region Modelos

    public class SearchRequest
    {
        public string Query { get; set; }
        public List<string> Results { get; set; } = new List<string>();
        public DateTime ProcessedAt { get; set; }
    }

    public class DownloadRequest
    {
        public string Filename { get; set; }
        public bool IsAvailable { get; set; }
        public DateTime QueuedAt { get; set; }
        public DateTime CompletedAt { get; set; }
    }

    #endregion
}
