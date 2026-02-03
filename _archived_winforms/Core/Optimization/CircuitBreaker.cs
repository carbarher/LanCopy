using System;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core.Optimization
{
    /// <summary>
    /// Circuit breaker pattern para proteger servicios externos
    /// </summary>
    public class CircuitBreaker
    {
        private int failureCount;
        private DateTime lastFailureTime;
        private CircuitState state = CircuitState.Closed;
        private readonly int failureThreshold;
        private readonly TimeSpan openTimeout;
        private readonly TimeSpan halfOpenTimeout;
        private readonly object lockObj = new object();
        
        public CircuitState State => state;
        public int FailureCount => failureCount;
        
        public CircuitBreaker(
            int failureThreshold = 5,
            TimeSpan? openTimeout = null,
            TimeSpan? halfOpenTimeout = null)
        {
            this.failureThreshold = failureThreshold;
            this.openTimeout = openTimeout ?? TimeSpan.FromMinutes(1);
            this.halfOpenTimeout = halfOpenTimeout ?? TimeSpan.FromSeconds(30);
        }
        
        public enum CircuitState
        {
            Closed,    // Normal operation
            Open,      // Failing, rejecting requests
            HalfOpen   // Testing if service recovered
        }
        
        /// <summary>
        /// Ejecuta acción con circuit breaker
        /// </summary>
        public async Task<T> ExecuteAsync<T>(
            Func<Task<T>> action,
            CancellationToken cancellationToken = default)
        {
            CheckState();
            
            if (state == CircuitState.Open)
            {
                throw new CircuitBreakerOpenException(
                    $"Circuit breaker is open. Last failure: {lastFailureTime}");
            }
            
            try
            {
                var result = await action().ConfigureAwait(false);
                OnSuccess();
                return result;
            }
            catch (Exception ex)
            {
                OnFailure(ex);
                throw;
            }
        }
        
        /// <summary>
        /// Ejecuta acción sin retorno
        /// </summary>
        public async Task ExecuteAsync(
            Func<Task> action,
            CancellationToken cancellationToken = default)
        {
            CheckState();
            
            if (state == CircuitState.Open)
            {
                throw new CircuitBreakerOpenException(
                    $"Circuit breaker is open. Last failure: {lastFailureTime}");
            }
            
            try
            {
                await action().ConfigureAwait(false);
                OnSuccess();
            }
            catch (Exception ex)
            {
                OnFailure(ex);
                throw;
            }
        }
        
        /// <summary>
        /// Intenta ejecutar sin lanzar excepción si está abierto
        /// </summary>
        public async Task<(bool Success, T Result)> TryExecuteAsync<T>(
            Func<Task<T>> action,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await ExecuteAsync(action, cancellationToken).ConfigureAwait(false);
                return (true, result);
            }
            catch (CircuitBreakerOpenException)
            {
                return (false, default);
            }
        }
        
        private void CheckState()
        {
            lock (lockObj)
            {
                if (state == CircuitState.Open)
                {
                    var timeSinceFailure = DateTime.Now - lastFailureTime;
                    
                    if (timeSinceFailure >= openTimeout)
                    {
                        state = CircuitState.HalfOpen;
                        failureCount = 0;
                    }
                }
                else if (state == CircuitState.HalfOpen)
                {
                    var timeSinceFailure = DateTime.Now - lastFailureTime;
                    
                    if (timeSinceFailure >= halfOpenTimeout)
                    {
                        state = CircuitState.Closed;
                        failureCount = 0;
                    }
                }
            }
        }
        
        private void OnSuccess()
        {
            lock (lockObj)
            {
                if (state == CircuitState.HalfOpen)
                {
                    state = CircuitState.Closed;
                }
                
                failureCount = 0;
            }
        }
        
        private void OnFailure(Exception ex)
        {
            lock (lockObj)
            {
                failureCount++;
                lastFailureTime = DateTime.Now;
                
                if (state == CircuitState.HalfOpen)
                {
                    state = CircuitState.Open;
                }
                else if (failureCount >= failureThreshold)
                {
                    state = CircuitState.Open;
                }
            }
        }
        
        /// <summary>
        /// Resetea manualmente el circuit breaker
        /// </summary>
        public void Reset()
        {
            lock (lockObj)
            {
                state = CircuitState.Closed;
                failureCount = 0;
            }
        }
        
        /// <summary>
        /// Obtiene estadísticas
        /// </summary>
        public CircuitBreakerStats GetStats()
        {
            lock (lockObj)
            {
                return new CircuitBreakerStats
                {
                    State = state,
                    FailureCount = failureCount,
                    LastFailureTime = lastFailureTime,
                    TimeSinceLastFailure = lastFailureTime != default 
                        ? DateTime.Now - lastFailureTime 
                        : TimeSpan.Zero
                };
            }
        }
        
        public class CircuitBreakerStats
        {
            public CircuitState State { get; set; }
            public int FailureCount { get; set; }
            public DateTime LastFailureTime { get; set; }
            public TimeSpan TimeSinceLastFailure { get; set; }
        }
    }
    
    public class CircuitBreakerOpenException : Exception
    {
        public CircuitBreakerOpenException(string message) : base(message) { }
    }
}
