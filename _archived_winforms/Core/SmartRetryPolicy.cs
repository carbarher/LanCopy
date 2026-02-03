using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Política de reintentos inteligente con detección de patrones
    /// </summary>
    public class SmartRetryPolicy
    {
        private readonly RateLimitDetector _rateLimitDetector;
        private readonly Dictionary<string, RetryStats> _operationStats = new();
        private readonly object _lock = new();
        
        public SmartRetryPolicy(RateLimitDetector rateLimitDetector)
        {
            _rateLimitDetector = rateLimitDetector;
        }
        
        public class RetryStats
        {
            public int TotalAttempts { get; set; }
            public int ConsecutiveFailures { get; set; }
            public int ConsecutiveSuccesses { get; set; }
            public DateTime LastAttempt { get; set; }
            public DateTime LastSuccess { get; set; }
            public TimeSpan AverageDelay { get; set; }
            public bool IsInCircuitBreaker { get; set; }
            public DateTime CircuitBreakerOpenedAt { get; set; }
        }
        
        /// <summary>
        /// Ejecuta una operación con política de reintentos inteligente
        /// </summary>
        public async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            string operationName = "default",
            int maxRetries = 3,
            CancellationToken cancellationToken = default)
        {
            var stats = GetOrCreateStats(operationName);
            var attempt = 0;
            
            while (attempt <= maxRetries)
            {
                try
                {
                    // Verificar circuit breaker
                    if (stats.IsInCircuitBreaker)
                    {
                        if (ShouldResetCircuitBreaker(stats))
                        {
                            stats.IsInCircuitBreaker = false;
                            Console.WriteLine($"Circuit breaker reset for {operationName}");
                        }
                        else
                        {
                            throw new CircuitBreakerOpenException($"Circuit breaker open for {operationName}");
                        }
                    }
                    
                    // Esperar delay adaptativo
                    if (attempt > 0)
                    {
                        var delay = CalculateAdaptiveDelay(stats, attempt);
                        Console.WriteLine($"Retry {attempt}/{maxRetries} for {operationName} - waiting {delay}ms");
                        await Task.Delay(delay, cancellationToken);
                    }
                    
                    // Ejecutar operación
                    var result = await operation();
                    
                    // Registrar éxito
                    RegisterSuccess(stats);
                    _rateLimitDetector.RegisterSuccess(operationName);
                    
                    return result;
                }
                catch (Exception ex)
                {
                    attempt++;
                    stats.TotalAttempts++;
                    stats.ConsecutiveFailures++;
                    stats.ConsecutiveSuccesses = 0;
                    stats.LastAttempt = DateTime.UtcNow;
                    
                    // Registrar fallo
                    _rateLimitDetector.RegisterFailure(operationName, ex);
                    
                    // Verificar si debemos reintentar
                    if (attempt > maxRetries || !ShouldRetry(ex))
                    {
                        // Activar circuit breaker si hay muchos fallos
                        if (stats.ConsecutiveFailures >= 5)
                        {
                            stats.IsInCircuitBreaker = true;
                            stats.CircuitBreakerOpenedAt = DateTime.UtcNow;
                            Console.WriteLine($"Circuit breaker opened for {operationName}");
                        }
                        
                        throw;
                    }
                    
                    Console.WriteLine($"Attempt {attempt-1} failed for {operationName}: {ex.Message}");
                }
            }
            
            throw new MaximumRetriesExceededException($"Maximum retries exceeded for {operationName}");
        }
        
        /// <summary>
        /// Calcula delay adaptativo basado en estadísticas
        /// </summary>
        private int CalculateAdaptiveDelay(RetryStats stats, int attempt)
        {
            // Base delay con backoff exponencial
            var baseDelay = Math.Min(30000, 2000 * (int)Math.Pow(2, attempt - 1));
            
            // Ajustar según tasa de éxito reciente
            var successRate = stats.TotalAttempts > 0 
                ? (double)stats.ConsecutiveSuccesses / stats.TotalAttempts 
                : 0.5;
            
            if (successRate < 0.3) // Baja tasa de éxito
                baseDelay = (int)(baseDelay * 1.5);
            else if (successRate > 0.8) // Alta tasa de éxito
                baseDelay = (int)(baseDelay * 0.7);
            
            // Agregar jitter para evitar sincronización
            var jitter = new Random().Next(0, baseDelay / 4);
            
            return baseDelay + jitter;
        }
        
        /// <summary>
        /// Determina si una excepción debe ser reintentada
        /// </summary>
        private bool ShouldRetry(Exception exception)
        {
            if (exception == null) return false;
            
            var message = exception.Message.ToLowerInvariant();
            
            // No reintentar en estos casos
            var noRetryMessages = new[]
            {
                "authentication failed",
                "invalid credentials",
                "file not found",
                "access denied",
                "user not found"
            };
            
            foreach (var noRetry in noRetryMessages)
            {
                if (message.Contains(noRetry))
                    return false;
            }
            
            // Sí reintentar en estos casos
            var retryMessages = new[]
            {
                "timeout",
                "connection",
                "network",
                "temporary failure",
                "operation was canceled",
                "remote connection closed"
            };
            
            foreach (var retry in retryMessages)
            {
                if (message.Contains(retry))
                    return true;
            }
            
            // Por defecto, reintentar excepciones de red
            return exception is System.Net.WebException ||
                   exception is System.Net.Sockets.SocketException ||
                   exception is System.TimeoutException;
        }
        
        /// <summary>
        /// Determina si se debe resetear el circuit breaker
        /// </summary>
        private bool ShouldResetCircuitBreaker(RetryStats stats)
        {
            // Resetear después de 5 minutos
            return DateTime.UtcNow - stats.CircuitBreakerOpenedAt > TimeSpan.FromMinutes(5);
        }
        
        /// <summary>
        /// Registra un éxito en las estadísticas
        /// </summary>
        private void RegisterSuccess(RetryStats stats)
        {
            stats.ConsecutiveSuccesses++;
            stats.ConsecutiveFailures = 0;
            stats.LastSuccess = DateTime.UtcNow;
            stats.LastAttempt = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Obtiene o crea estadísticas para una operación
        /// </summary>
        private RetryStats GetOrCreateStats(string operationName)
        {
            lock (_lock)
            {
                if (!_operationStats.ContainsKey(operationName))
                    _operationStats[operationName] = new RetryStats();
                
                return _operationStats[operationName];
            }
        }
        
        /// <summary>
        /// Obtiene estadísticas de una operación
        /// </summary>
        public RetryStats GetStats(string operationName)
        {
            lock (_lock)
            {
                return _operationStats.TryGetValue(operationName, out var stats) ? stats : new RetryStats();
            }
        }
        
        /// <summary>
        /// Reinicia las estadísticas de una operación
        /// </summary>
        public void ResetStats(string operationName)
        {
            lock (_lock)
            {
                _operationStats[operationName] = new RetryStats();
            }
        }
        
        /// <summary>
        /// Obtiene un resumen de todas las operaciones
        /// </summary>
        public Dictionary<string, RetryStats> GetAllStats()
        {
            lock (_lock)
            {
                return new Dictionary<string, RetryStats>(_operationStats);
            }
        }
    }
    
    /// <summary>
    /// Excepción cuando el circuit breaker está abierto
    /// </summary>
    public class CircuitBreakerOpenException : Exception
    {
        public CircuitBreakerOpenException(string message) : base(message) { }
    }
    
    /// <summary>
    /// Excepción cuando se excede el máximo de reintentos
    /// </summary>
    public class MaximumRetriesExceededException : Exception
    {
        public MaximumRetriesExceededException(string message) : base(message) { }
    }
}
