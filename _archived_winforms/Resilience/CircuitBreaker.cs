using System;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Resilience
{
    /// <summary>
    /// Circuit Breaker para prevenir cascadas de fallos
    /// </summary>
    public class CircuitBreaker
    {
        private readonly int _failureThreshold;
        private readonly TimeSpan _timeout;
        private readonly TimeSpan _resetTimeout;
        
        private int _failureCount;
        private DateTime _lastFailureTime;
        private CircuitState _state = CircuitState.Closed;
        private readonly object _lock = new();
        
        public CircuitBreaker(
            int failureThreshold = 5,
            TimeSpan? timeout = null,
            TimeSpan? resetTimeout = null)
        {
            _failureThreshold = failureThreshold;
            _timeout = timeout ?? TimeSpan.FromSeconds(30);
            _resetTimeout = resetTimeout ?? TimeSpan.FromMinutes(1);
        }
        
        /// <summary>
        /// Estado actual del circuit breaker
        /// </summary>
        public CircuitState State
        {
            get
            {
                lock (_lock)
                {
                    // Si está abierto y ha pasado el timeout, intentar medio abierto
                    if (_state == CircuitState.Open && 
                        DateTime.UtcNow - _lastFailureTime > _resetTimeout)
                    {
                        _state = CircuitState.HalfOpen;
                        Logging.Logger.Instance.Info("Circuit breaker: Open -> HalfOpen");
                    }
                    
                    return _state;
                }
            }
        }
        
        /// <summary>
        /// Ejecuta una acción a través del circuit breaker
        /// </summary>
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
        {
            if (State == CircuitState.Open)
            {
                throw new CircuitBreakerOpenException("Circuit breaker is open");
            }
            
            try
            {
                using (PerformanceMetrics.Instance.Track("CircuitBreaker.Execute"))
                {
                    // Timeout para la operación
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(_timeout);
                    
                    var result = await action();
                    
                    OnSuccess();
                    return result;
                }
            }
            catch (Exception ex)
            {
                OnFailure(ex);
                throw;
            }
        }
        
        /// <summary>
        /// Ejecuta una acción sin retorno
        /// </summary>
        public async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(async () =>
            {
                await action();
                return true;
            }, cancellationToken);
        }
        
        /// <summary>
        /// Maneja un éxito
        /// </summary>
        private void OnSuccess()
        {
            lock (_lock)
            {
                if (_state == CircuitState.HalfOpen)
                {
                    _state = CircuitState.Closed;
                    _failureCount = 0;
                    Logging.Logger.Instance.Info("Circuit breaker: HalfOpen -> Closed");
                }
                else if (_state == CircuitState.Closed)
                {
                    _failureCount = 0;
                }
            }
        }
        
        /// <summary>
        /// Maneja un fallo
        /// </summary>
        private void OnFailure(Exception ex)
        {
            lock (_lock)
            {
                _failureCount++;
                _lastFailureTime = DateTime.UtcNow;
                
                if (_state == CircuitState.HalfOpen)
                {
                    _state = CircuitState.Open;
                    Logging.Logger.Instance.Warning($"Circuit breaker: HalfOpen -> Open (failure)", ex);
                }
                else if (_failureCount >= _failureThreshold)
                {
                    _state = CircuitState.Open;
                    Logging.Logger.Instance.Warning(
                        $"Circuit breaker: Closed -> Open (threshold {_failureThreshold} reached)", ex);
                }
            }
        }
        
        /// <summary>
        /// Resetea manualmente el circuit breaker
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _state = CircuitState.Closed;
                _failureCount = 0;
                Logging.Logger.Instance.Info("Circuit breaker: Manual reset");
            }
        }
        
        /// <summary>
        /// Obtiene estadísticas del circuit breaker
        /// </summary>
        public CircuitBreakerStats GetStats()
        {
            lock (_lock)
            {
                return new CircuitBreakerStats
                {
                    State = _state,
                    FailureCount = _failureCount,
                    LastFailureTime = _lastFailureTime,
                    TimeUntilReset = _state == CircuitState.Open 
                        ? _resetTimeout - (DateTime.UtcNow - _lastFailureTime)
                        : TimeSpan.Zero
                };
            }
        }
    }
    
    /// <summary>
    /// Estados del circuit breaker
    /// </summary>
    public enum CircuitState
    {
        /// <summary>
        /// Cerrado - operaciones normales
        /// </summary>
        Closed,
        
        /// <summary>
        /// Abierto - rechaza todas las operaciones
        /// </summary>
        Open,
        
        /// <summary>
        /// Medio abierto - permite una operación de prueba
        /// </summary>
        HalfOpen
    }
    
    /// <summary>
    /// Estadísticas del circuit breaker
    /// </summary>
    public class CircuitBreakerStats
    {
        public CircuitState State { get; set; }
        public int FailureCount { get; set; }
        public DateTime LastFailureTime { get; set; }
        public TimeSpan TimeUntilReset { get; set; }
        
        public override string ToString()
        {
            return $"State: {State}, Failures: {FailureCount}, " +
                   $"Reset in: {TimeUntilReset.TotalSeconds:F0}s";
        }
    }
    
    /// <summary>
    /// Excepción lanzada cuando el circuit breaker está abierto
    /// </summary>
    public class CircuitBreakerOpenException : Exception
    {
        public CircuitBreakerOpenException(string message) : base(message)
        {
        }
    }
}
