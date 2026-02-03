using System;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Circuit Breaker unificado para toda la aplicación
    /// Previene cascadas de fallos y protege contra servicios caídos
    /// </summary>
    public class UnifiedCircuitBreaker
    {
        private int failureCount = 0;
        private DateTime circuitOpenedAt = DateTime.MinValue;
        private CircuitState state = CircuitState.Closed;
        private readonly object stateLock = new object();
        
        private readonly int failureThreshold;
        private readonly TimeSpan openTimeout;
        private readonly TimeSpan halfOpenTimeout;
        
        public CircuitState State
        {
            get
            {
                lock (stateLock)
                {
                    return state;
                }
            }
        }
        
        public int FailureCount
        {
            get
            {
                lock (stateLock)
                {
                    return failureCount;
                }
            }
        }
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="failureThreshold">Número de fallos antes de abrir el circuito</param>
        /// <param name="openTimeoutSeconds">Segundos que el circuito permanece abierto</param>
        /// <param name="halfOpenTimeoutSeconds">Segundos en estado half-open antes de cerrar</param>
        public UnifiedCircuitBreaker(int failureThreshold = 5, int openTimeoutSeconds = 300, int halfOpenTimeoutSeconds = 60)
        {
            this.failureThreshold = failureThreshold;
            this.openTimeout = TimeSpan.FromSeconds(openTimeoutSeconds);
            this.halfOpenTimeout = TimeSpan.FromSeconds(halfOpenTimeoutSeconds);
        }
        
        /// <summary>
        /// Verifica si se permite una operación
        /// </summary>
        public bool AllowRequest()
        {
            lock (stateLock)
            {
                if (state == CircuitState.Closed)
                {
                    return true;
                }
                
                if (state == CircuitState.Open)
                {
                    // Verificar si es tiempo de intentar cerrar el circuito
                    if (DateTime.UtcNow - circuitOpenedAt >= openTimeout)
                    {
                        state = CircuitState.HalfOpen;
                        return true;
                    }
                    
                    return false;
                }
                
                // HalfOpen: permitir 1 intento para probar
                return true;
            }
        }
        
        /// <summary>
        /// Registra una operación exitosa
        /// </summary>
        public void RecordSuccess()
        {
            lock (stateLock)
            {
                failureCount = 0;
                
                if (state == CircuitState.HalfOpen)
                {
                    state = CircuitState.Closed;
                }
            }
        }
        
        /// <summary>
        /// Registra una operación fallida
        /// </summary>
        public void RecordFailure()
        {
            lock (stateLock)
            {
                failureCount++;
                
                if (state == CircuitState.HalfOpen)
                {
                    // Si falla en HalfOpen, volver a Open
                    state = CircuitState.Open;
                    circuitOpenedAt = DateTime.UtcNow;
                }
                else if (failureCount >= failureThreshold)
                {
                    state = CircuitState.Open;
                    circuitOpenedAt = DateTime.UtcNow;
                }
            }
        }
        
        /// <summary>
        /// Ejecuta una operación a través del circuit breaker
        /// </summary>
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
        {
            if (!AllowRequest())
            {
                throw new CircuitBreakerOpenException("Circuit breaker is open");
            }
            
            try
            {
                var result = await action();
                RecordSuccess();
                return result;
            }
            catch (Exception)
            {
                RecordFailure();
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
        /// Resetea el circuit breaker manualmente
        /// </summary>
        public void Reset()
        {
            lock (stateLock)
            {
                failureCount = 0;
                state = CircuitState.Closed;
                circuitOpenedAt = DateTime.MinValue;
            }
        }
        
        /// <summary>
        /// Obtiene el tiempo restante hasta que el circuito intente cerrarse
        /// </summary>
        public TimeSpan GetTimeUntilRetry()
        {
            lock (stateLock)
            {
                if (state != CircuitState.Open)
                    return TimeSpan.Zero;
                
                var elapsed = DateTime.UtcNow - circuitOpenedAt;
                var remaining = openTimeout - elapsed;
                
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }
        
        /// <summary>
        /// Obtiene información del estado actual
        /// </summary>
        public string GetStatusInfo()
        {
            lock (stateLock)
            {
                var info = $"Estado: {state}, Fallos: {failureCount}/{failureThreshold}";
                
                if (state == CircuitState.Open)
                {
                    var remaining = GetTimeUntilRetry();
                    info += $", Reintento en: {remaining.TotalSeconds:F0}s";
                }
                
                return info;
            }
        }
    }
    
    public enum CircuitState
    {
        /// <summary>
        /// Circuito cerrado - operaciones normales
        /// </summary>
        Closed,
        
        /// <summary>
        /// Circuito abierto - bloqueando operaciones
        /// </summary>
        Open,
        
        /// <summary>
        /// Medio abierto - probando si el servicio se recuperó
        /// </summary>
        HalfOpen
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
