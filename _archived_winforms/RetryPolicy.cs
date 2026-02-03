using System;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// Política de reintentos para operaciones de red
    /// </summary>
    public static class RetryPolicy
    {
        /// <summary>
        /// Ejecuta operación con reintentos exponenciales
        /// </summary>
        public static async Task<T> ExecuteWithRetry<T>(
            Func<Task<T>> operation,
            int maxAttempts = 3,
            int initialDelayMs = 1000,
            double backoffMultiplier = 2.0,
            Action<int, Exception> onRetry = null)
        {
            int attempt = 0;
            int delay = initialDelayMs;
            
            while (true)
            {
                attempt++;
                
                try
                {
                    return await operation();
                }
                catch (Exception ex) when (IsRetriableException(ex) && attempt < maxAttempts)
                {
                    onRetry?.Invoke(attempt, ex);
                    
                    await Task.Delay(delay);
                    delay = (int)(delay * backoffMultiplier);
                }
                catch (Exception ex) when (attempt >= maxAttempts)
                {
                    throw new Exception($"Operación falló después de {maxAttempts} intentos", ex);
                }
            }
        }
        
        /// <summary>
        /// Ejecuta operación con reintentos (sin retorno)
        /// </summary>
        public static async Task ExecuteWithRetry(
            Func<Task> operation,
            int maxAttempts = 3,
            int initialDelayMs = 1000,
            double backoffMultiplier = 2.0,
            Action<int, Exception> onRetry = null)
        {
            await ExecuteWithRetry(async () =>
            {
                await operation();
                return true;
            }, maxAttempts, initialDelayMs, backoffMultiplier, onRetry);
        }
        
        /// <summary>
        /// Determina si una excepción es retriable
        /// </summary>
        private static bool IsRetriableException(Exception ex)
        {
            // Errores de red retriables
            if (ex is System.Net.Sockets.SocketException)
                return true;
            
            if (ex is System.Net.WebException)
                return true;
            
            if (ex is TimeoutException)
                return true;
            
            if (ex is System.IO.IOException)
                return true;
            
            // Soulseek exceptions retriables
            if (ex.GetType().Name.Contains("Transfer") && 
                ex.Message.Contains("timed out"))
                return true;
            
            if (ex.Message.Contains("connection") || 
                ex.Message.Contains("network") ||
                ex.Message.Contains("timeout"))
                return true;
            
            return false;
        }
        
        /// <summary>
        /// Circuit breaker para prevenir sobrecarga
        /// </summary>
        public class CircuitBreaker
        {
            private int failureCount = 0;
            private DateTime lastFailureTime = DateTime.MinValue;
            private CircuitState state = CircuitState.Closed;
            private readonly int failureThreshold;
            private readonly TimeSpan resetTimeout;
            
            public CircuitState State => state;
            public int FailureCount => failureCount;
            public DateTime? LastFailureTime => lastFailureTime == DateTime.MinValue ? null : lastFailureTime;
            public DateTime? OpenedAt { get; private set; }
            
            public CircuitBreaker(int failureThreshold = 5, int resetTimeoutSeconds = 60)
            {
                this.failureThreshold = failureThreshold;
                this.resetTimeout = TimeSpan.FromSeconds(resetTimeoutSeconds);
            }
            
            public async Task<T> Execute<T>(Func<Task<T>> operation)
            {
                // Verificar si el circuito debe resetearse
                if (state == CircuitState.Open && 
                    DateTime.Now - lastFailureTime > resetTimeout)
                {
                    state = CircuitState.HalfOpen;
                    failureCount = 0;
                }
                
                // Si está abierto, rechazar inmediatamente
                if (state == CircuitState.Open)
                {
                    throw new Exception($"Circuit breaker abierto. Reintenta en {(resetTimeout - (DateTime.Now - lastFailureTime)).TotalSeconds:F0}s");
                }
                
                try
                {
                    var result = await operation();
                    
                    // Éxito: cerrar circuito
                    if (state == CircuitState.HalfOpen)
                    {
                        state = CircuitState.Closed;
                        failureCount = 0;
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    failureCount++;
                    lastFailureTime = DateTime.Now;
                    
                    // Abrir circuito si se alcanza el threshold
                    if (failureCount >= failureThreshold)
                    {
                        state = CircuitState.Open;
                        OpenedAt = DateTime.Now;
                    }
                    
                    throw;
                }
            }
            
            /// <summary>
            /// Restaura el estado del circuit breaker desde la base de datos
            /// </summary>
            public void RestoreState(int failures, DateTime? lastFailure, CircuitState restoredState, DateTime? openedAt)
            {
                failureCount = failures;
                lastFailureTime = lastFailure ?? DateTime.MinValue;
                state = restoredState;
                OpenedAt = openedAt;
            }
            
            /// <summary>
            /// Registra un fallo manualmente (para testing o casos especiales)
            /// </summary>
            public void RecordFailure()
            {
                failureCount++;
                lastFailureTime = DateTime.Now;
                
                if (failureCount >= failureThreshold)
                {
                    state = CircuitState.Open;
                    OpenedAt = DateTime.Now;
                }
            }
        }
        
        public enum CircuitState
        {
            Closed,   // Normal, permite operaciones
            Open,     // Bloqueado, rechaza operaciones
            HalfOpen  // Probando si puede cerrarse
        }
    }
}
