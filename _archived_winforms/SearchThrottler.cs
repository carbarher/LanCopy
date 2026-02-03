using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// Throttling inteligente de bÃºsquedas para evitar saturar el servidor
    /// </summary>
    public class SearchThrottler
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly Queue<DateTime> _searchTimestamps = new Queue<DateTime>();
        private readonly int _maxSearchesPerMinute;
        private readonly TimeSpan _minDelayBetweenSearches;

        public SearchThrottler(int maxSearchesPerMinute = 8, int minDelayMs = 500)
        {
            _maxSearchesPerMinute = maxSearchesPerMinute;
            _minDelayBetweenSearches = TimeSpan.FromMilliseconds(minDelayMs);
            _semaphore = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Espera hasta que sea seguro hacer una bÃºsqueda
        /// </summary>
        public async Task WaitForSearchSlotAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                // Limpiar timestamps antiguos (>1 minuto)
                var oneMinuteAgo = DateTime.Now.AddMinutes(-1);
                while (_searchTimestamps.Count > 0 && _searchTimestamps.Peek() < oneMinuteAgo)
                    {
                        _searchTimestamps.Dequeue();
                    }

                    // Si hemos alcanzado el lÃ­mite, esperar
                    if (_searchTimestamps.Count >= _maxSearchesPerMinute)
                    {
                        var oldestTimestamp = _searchTimestamps.Peek();
                        var waitTime = oldestTimestamp.AddMinutes(1) - DateTime.Now;
                        
                        if (waitTime > TimeSpan.Zero)
                        {
                            await Task.Delay(waitTime, cancellationToken);
                        }

                        // Limpiar de nuevo despuÃ©s de esperar
                        oneMinuteAgo = DateTime.Now.AddMinutes(-1);
                        while (_searchTimestamps.Count > 0 && _searchTimestamps.Peek() < oneMinuteAgo)
                        {
                            _searchTimestamps.Dequeue();
                        }
                    }

                    // Esperar delay mÃ­nimo desde Ãºltima bÃºsqueda
                    if (_searchTimestamps.Count > 0)
                    {
                        var lastSearch = _searchTimestamps.ToArray()[_searchTimestamps.Count - 1];
                        var timeSinceLastSearch = DateTime.Now - lastSearch;
                        
                        if (timeSinceLastSearch < _minDelayBetweenSearches)
                        {
                            var remainingDelay = _minDelayBetweenSearches - timeSinceLastSearch;
                            await Task.Delay(remainingDelay, cancellationToken);
                        }
                    }

                // Registrar esta bÃºsqueda
                _searchTimestamps.Enqueue(DateTime.Now);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Obtiene el nÃºmero de bÃºsquedas en el Ãºltimo minuto
        /// </summary>
        public int GetSearchesInLastMinute()
        {
            _semaphore.Wait();
            try
            {
                var oneMinuteAgo = DateTime.Now.AddMinutes(-1);
                while (_searchTimestamps.Count > 0 && _searchTimestamps.Peek() < oneMinuteAgo)
                {
                    _searchTimestamps.Dequeue();
                }
                return _searchTimestamps.Count;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Obtiene el tiempo estimado de espera
        /// </summary>
        public TimeSpan GetEstimatedWaitTime()
        {
            _semaphore.Wait();
            try
            {
                if (_searchTimestamps.Count < _maxSearchesPerMinute)
                    return TimeSpan.Zero;

                var oldestTimestamp = _searchTimestamps.Peek();
                var waitTime = oldestTimestamp.AddMinutes(1) - DateTime.Now;
                
                return waitTime > TimeSpan.Zero ? waitTime : TimeSpan.Zero;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Verifica si se puede hacer una bÃºsqueda ahora
        /// </summary>
        public bool CanSearchNow()
        {
            return GetSearchesInLastMinute() < _maxSearchesPerMinute && 
                   GetEstimatedWaitTime() == TimeSpan.Zero;
        }

        /// <summary>
        /// Obtiene estadÃ­sticas del throttler
        /// </summary>
        public (int searchesLastMinute, int maxPerMinute, TimeSpan estimatedWait) GetStats()
        {
            return (GetSearchesInLastMinute(), _maxSearchesPerMinute, GetEstimatedWaitTime());
        }

        /// <summary>
        /// Resetea el throttler
        /// </summary>
        public void Reset()
        {
            _semaphore.Wait();
            try
            {
                _searchTimestamps.Clear();
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}

