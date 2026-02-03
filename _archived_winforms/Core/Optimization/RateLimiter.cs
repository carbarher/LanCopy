using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core.Optimization
{
    /// <summary>
    /// Rate limiter para controlar frecuencia de operaciones
    /// </summary>
    public class RateLimiter
    {
        private readonly SemaphoreSlim semaphore;
        private readonly Queue<DateTime> requests;
        private readonly int maxRequests;
        private readonly TimeSpan timeWindow;
        private readonly object lockObj = new object();
        
        public int MaxRequests => maxRequests;
        public TimeSpan TimeWindow => timeWindow;
        public int CurrentRequests => requests.Count;
        
        public RateLimiter(int maxRequests, TimeSpan timeWindow)
        {
            if (maxRequests <= 0)
                throw new ArgumentException("Max requests must be positive", nameof(maxRequests));
            
            this.maxRequests = maxRequests;
            this.timeWindow = timeWindow;
            this.requests = new Queue<DateTime>(maxRequests);
            this.semaphore = new SemaphoreSlim(1, 1);
        }
        
        public RateLimiter(int maxRequests, int timeWindowSeconds)
            : this(maxRequests, TimeSpan.FromSeconds(timeWindowSeconds))
        {
        }
        
        /// <summary>
        /// Intenta adquirir permiso para ejecutar operación
        /// </summary>
        public async Task<bool> TryAcquireAsync(CancellationToken cancellationToken = default)
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                CleanOldRequests();
                
                if (requests.Count < maxRequests)
                {
                    requests.Enqueue(DateTime.Now);
                    return true;
                }
                
                return false;
            }
            finally
            {
                semaphore.Release();
            }
        }
        
        /// <summary>
        /// Espera hasta poder ejecutar operación (con timeout)
        /// </summary>
        public async Task WaitAsync(CancellationToken cancellationToken = default)
        {
            while (!await TryAcquireAsync(cancellationToken).ConfigureAwait(false))
            {
                // Calcular tiempo de espera hasta que expire la request más antigua
                var waitTime = CalculateWaitTime();
                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        
        /// <summary>
        /// Ejecuta acción con rate limiting
        /// </summary>
        public async Task<T> ExecuteAsync<T>(
            Func<Task<T>> action, 
            CancellationToken cancellationToken = default)
        {
            await WaitAsync(cancellationToken).ConfigureAwait(false);
            return await action().ConfigureAwait(false);
        }
        
        /// <summary>
        /// Ejecuta acción con rate limiting (sin retorno)
        /// </summary>
        public async Task ExecuteAsync(
            Func<Task> action, 
            CancellationToken cancellationToken = default)
        {
            await WaitAsync(cancellationToken).ConfigureAwait(false);
            await action().ConfigureAwait(false);
        }
        
        private void CleanOldRequests()
        {
            var cutoff = DateTime.Now - timeWindow;
            
            while (requests.Count > 0 && requests.Peek() < cutoff)
            {
                requests.Dequeue();
            }
        }
        
        private TimeSpan CalculateWaitTime()
        {
            lock (lockObj)
            {
                if (requests.Count == 0)
                    return TimeSpan.Zero;
                
                var oldest = requests.Peek();
                var expirationTime = oldest + timeWindow;
                var waitTime = expirationTime - DateTime.Now;
                
                return waitTime > TimeSpan.Zero ? waitTime : TimeSpan.Zero;
            }
        }
        
        /// <summary>
        /// Resetea el rate limiter
        /// </summary>
        public async Task ResetAsync()
        {
            await semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                requests.Clear();
            }
            finally
            {
                semaphore.Release();
            }
        }
        
        /// <summary>
        /// Obtiene estadísticas
        /// </summary>
        public RateLimiterStats GetStats()
        {
            lock (lockObj)
            {
                CleanOldRequests();
                
                return new RateLimiterStats
                {
                    CurrentRequests = requests.Count,
                    MaxRequests = maxRequests,
                    UsagePercent = (requests.Count / (double)maxRequests) * 100,
                    TimeUntilNextSlot = CalculateWaitTime()
                };
            }
        }
        
        public class RateLimiterStats
        {
            public int CurrentRequests { get; set; }
            public int MaxRequests { get; set; }
            public double UsagePercent { get; set; }
            public TimeSpan TimeUntilNextSlot { get; set; }
        }
    }
}
