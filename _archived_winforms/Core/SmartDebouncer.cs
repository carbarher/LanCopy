using System;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Debouncer inteligente para optimizar eventos de UI
    /// Previene búsquedas/operaciones innecesarias mientras el usuario escribe
    /// </summary>
    public class SmartDebouncer<T>
    {
        private CancellationTokenSource? _cts;
        private readonly TimeSpan _delay;
        private readonly Func<T, Task> _action;
        private Task? _pendingTask;

        public SmartDebouncer(TimeSpan delay, Func<T, Task> action)
        {
            _delay = delay;
            _action = action;
        }

        /// <summary>
        /// Dispara la acción con debouncing
        /// </summary>
        public async Task TriggerAsync(T value)
        {
            // Cancelar operación anterior
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            var token = _cts.Token;

            try
            {
                // Esperar delay
                await Task.Delay(_delay, token);
                
                // Ejecutar acción
                await _action(value);
            }
            catch (OperationCanceledException)
            {
                // Ignorar cancelación (es esperado)
            }
        }

        /// <summary>
        /// Dispara inmediatamente sin debouncing
        /// </summary>
        public async Task TriggerImmediateAsync(T value)
        {
            _cts?.Cancel();
            await _action(value);
        }

        /// <summary>
        /// Cancela operación pendiente
        /// </summary>
        public void Cancel()
        {
            _cts?.Cancel();
        }
    }

    /// <summary>
    /// Debouncer síncrono para operaciones rápidas
    /// </summary>
    public class SmartDebouncerSync<T>
    {
        private CancellationTokenSource? _cts;
        private readonly TimeSpan _delay;
        private readonly Action<T> _action;

        public SmartDebouncerSync(TimeSpan delay, Action<T> action)
        {
            _delay = delay;
            _action = action;
        }

        public async Task TriggerAsync(T value)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            var token = _cts.Token;

            try
            {
                await Task.Delay(_delay, token);
                _action(value);
            }
            catch (OperationCanceledException)
            {
                // Ignorar
            }
        }

        public void TriggerImmediate(T value)
        {
            _cts?.Cancel();
            _action(value);
        }
    }

    /// <summary>
    /// Throttler para limitar frecuencia de ejecución
    /// </summary>
    public class SmartThrottler<T>
    {
        private readonly TimeSpan _interval;
        private readonly Func<T, Task> _action;
        private DateTime _lastExecution = DateTime.MinValue;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public SmartThrottler(TimeSpan interval, Func<T, Task> action)
        {
            _interval = interval;
            _action = action;
        }

        /// <summary>
        /// Ejecuta acción con throttling
        /// </summary>
        public async Task<bool> TryExecuteAsync(T value)
        {
            await _semaphore.WaitAsync();
            
            try
            {
                var now = DateTime.UtcNow;
                var elapsed = now - _lastExecution;

                if (elapsed >= _interval)
                {
                    _lastExecution = now;
                    await _action(value);
                    return true;
                }

                return false;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Ejecuta acción ignorando throttling
        /// </summary>
        public async Task ExecuteImmediateAsync(T value)
        {
            await _semaphore.WaitAsync();
            
            try
            {
                _lastExecution = DateTime.UtcNow;
                await _action(value);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }

    /// <summary>
    /// Debouncer adaptativo que ajusta el delay según frecuencia de eventos
    /// </summary>
    public class AdaptiveDebouncer<T>
    {
        private CancellationTokenSource? _cts;
        private readonly TimeSpan _minDelay;
        private readonly TimeSpan _maxDelay;
        private readonly Func<T, Task> _action;
        private DateTime _lastTrigger = DateTime.MinValue;
        private TimeSpan _currentDelay;

        public AdaptiveDebouncer(
            TimeSpan minDelay, 
            TimeSpan maxDelay, 
            Func<T, Task> action)
        {
            _minDelay = minDelay;
            _maxDelay = maxDelay;
            _currentDelay = minDelay;
            _action = action;
        }

        public async Task TriggerAsync(T value)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            var token = _cts.Token;

            // Ajustar delay según frecuencia
            var now = DateTime.UtcNow;
            var timeSinceLastTrigger = now - _lastTrigger;
            
            if (timeSinceLastTrigger < TimeSpan.FromMilliseconds(100))
            {
                // Usuario escribiendo rápido, aumentar delay
                _currentDelay = TimeSpan.FromMilliseconds(
                    Math.Min(_currentDelay.TotalMilliseconds * 1.5, _maxDelay.TotalMilliseconds));
            }
            else if (timeSinceLastTrigger > TimeSpan.FromSeconds(2))
            {
                // Usuario pausó, reducir delay
                _currentDelay = _minDelay;
            }

            _lastTrigger = now;

            try
            {
                await Task.Delay(_currentDelay, token);
                await _action(value);
            }
            catch (OperationCanceledException)
            {
                // Ignorar
            }
        }
    }

    /// <summary>
    /// Ejemplos de uso para UI
    /// </summary>
    public class DebouncerExamples
    {
        // Debouncer para búsqueda mientras escribes
        private readonly SmartDebouncer<string> _searchDebouncer;
        
        // Throttler para actualización de progreso
        private readonly SmartThrottler<int> _progressThrottler;
        
        // Debouncer adaptativo para filtros
        private readonly AdaptiveDebouncer<string> _filterDebouncer;

        public DebouncerExamples()
        {
            // Búsqueda: esperar 300ms después de que el usuario deje de escribir
            _searchDebouncer = new SmartDebouncer<string>(
                TimeSpan.FromMilliseconds(300),
                async query => await PerformSearchAsync(query));

            // Progreso: actualizar máximo cada 100ms
            _progressThrottler = new SmartThrottler<int>(
                TimeSpan.FromMilliseconds(100),
                async progress => await UpdateProgressBarAsync(progress));

            // Filtros: delay adaptativo (200ms-1000ms)
            _filterDebouncer = new AdaptiveDebouncer<string>(
                TimeSpan.FromMilliseconds(200),
                TimeSpan.FromMilliseconds(1000),
                async filter => await ApplyFilterAsync(filter));
        }

        /// <summary>
        /// Uso en TextBox.TextChanged
        /// </summary>
        public async void OnSearchTextChanged(object sender, EventArgs e)
        {
            var textBox = (System.Windows.Forms.TextBox)sender;
            await _searchDebouncer.TriggerAsync(textBox.Text);
        }

        /// <summary>
        /// Uso en actualización de progreso
        /// </summary>
        public async void OnProgressChanged(int progress)
        {
            // Solo actualiza si han pasado 100ms desde la última actualización
            await _progressThrottler.TryExecuteAsync(progress);
        }

        /// <summary>
        /// Uso en filtros con delay adaptativo
        /// </summary>
        public async void OnFilterChanged(string filter)
        {
            await _filterDebouncer.TriggerAsync(filter);
        }

        private async Task PerformSearchAsync(string query)
        {
            // Implementación de búsqueda
            await Task.Delay(100);
        }

        private async Task UpdateProgressBarAsync(int progress)
        {
            // Actualizar UI
            await Task.Delay(10);
        }

        private async Task ApplyFilterAsync(string filter)
        {
            // Aplicar filtro
            await Task.Delay(50);
        }
    }

    /// <summary>
    /// Debouncer con cola para procesar todos los valores
    /// </summary>
    public class QueuedDebouncer<T>
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<T> _queue = new();
        private readonly TimeSpan _delay;
        private readonly Func<T[], Task> _batchAction;
        private CancellationTokenSource? _cts;
        private Task? _processingTask;

        public QueuedDebouncer(TimeSpan delay, Func<T[], Task> batchAction)
        {
            _delay = delay;
            _batchAction = batchAction;
        }

        public void Enqueue(T value)
        {
            _queue.Enqueue(value);
            
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            _processingTask = ProcessQueueAsync(_cts.Token);
        }

        private async Task ProcessQueueAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(_delay, token);
                
                var items = new System.Collections.Generic.List<T>();
                while (_queue.TryDequeue(out var item))
                {
                    items.Add(item);
                }

                if (items.Count > 0)
                {
                    await _batchAction(items.ToArray());
                }
            }
            catch (OperationCanceledException)
            {
                // Ignorar
            }
        }
    }
}
