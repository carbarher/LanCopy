using System;
using System.Net.Http;
using System.Threading.Tasks;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace SlskDown.Core
{
    /// <summary>
    /// Servicio de resilience usando Polly para retry policies y circuit breakers
    /// Mejora la estabilidad de operaciones de red y I/O
    /// </summary>
    public static class ResilienceService
    {
        // Circuit breaker para operaciones HTTP
        private static readonly AsyncCircuitBreakerPolicy _httpCircuitBreaker = 
            Policy.Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (exception, duration) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"Circuit breaker opened: {exception.Message}");
                    },
                    onReset: () =>
                    {
                        System.Diagnostics.Debug.WriteLine("Circuit breaker reset");
                    }
                );

        // Retry policy con backoff exponencial
        private static readonly AsyncRetryPolicy _retryPolicy = 
            Policy.Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"Retry {retryCount} after {timeSpan.TotalSeconds}s: {exception.Message}");
                    }
                );

        // Policy combinado: retry + circuit breaker
        private static readonly AsyncPolicy _combinedHttpPolicy = 
            Policy.WrapAsync(_retryPolicy, _httpCircuitBreaker);

        /// <summary>
        /// Ejecuta una operación HTTP con retry y circuit breaker
        /// </summary>
        public static async Task<T> ExecuteHttpAsync<T>(Func<Task<T>> operation)
        {
            return await _combinedHttpPolicy.ExecuteAsync(operation);
        }

        /// <summary>
        /// Ejecuta una operación HTTP con timeout
        /// </summary>
        public static async Task<T> ExecuteHttpWithTimeoutAsync<T>(Func<Task<T>> operation, TimeSpan timeout)
        {
            var timeoutPolicy = Policy.TimeoutAsync(timeout, TimeoutStrategy.Pessimistic);
            var combined = Policy.WrapAsync(_combinedHttpPolicy, timeoutPolicy);
            return await combined.ExecuteAsync(operation);
        }

        /// <summary>
        /// Crea un retry policy personalizado
        /// </summary>
        public static AsyncRetryPolicy CreateRetryPolicy(int retryCount, TimeSpan baseDelay)
        {
            return Policy.Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: retryCount,
                    sleepDurationProvider: attempt => baseDelay * Math.Pow(2, attempt - 1)
                );
        }

        /// <summary>
        /// Crea un circuit breaker personalizado
        /// </summary>
        public static AsyncCircuitBreakerPolicy CreateCircuitBreaker(int exceptionsAllowed, TimeSpan breakDuration)
        {
            return Policy.Handle<Exception>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: exceptionsAllowed,
                    durationOfBreak: breakDuration
                );
        }
    }

    /// <summary>
    /// Políticas de resilience específicas para Soulseek
    /// </summary>
    public static class SoulseekResiliencePolicy
    {
        // Retry para búsquedas (más agresivo)
        public static readonly AsyncRetryPolicy SearchRetryPolicy = 
            Policy.Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: 2,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(attempt * 2)
                );

        // Retry para descargas (más tolerante)
        public static readonly AsyncRetryPolicy DownloadRetryPolicy = 
            Policy.Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: 5,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))
                );

        // Circuit breaker para conexiones
        public static readonly AsyncCircuitBreakerPolicy ConnectionCircuitBreaker = 
            Policy.Handle<Exception>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 3,
                    durationOfBreak: TimeSpan.FromMinutes(1)
                );

        /// <summary>
        /// Ejecuta una búsqueda con retry
        /// </summary>
        public static async Task<T> ExecuteSearchAsync<T>(Func<Task<T>> searchOperation)
        {
            return await SearchRetryPolicy.ExecuteAsync(searchOperation);
        }

        /// <summary>
        /// Ejecuta una descarga con retry y circuit breaker
        /// </summary>
        public static async Task<T> ExecuteDownloadAsync<T>(Func<Task<T>> downloadOperation)
        {
            var combined = Policy.WrapAsync(DownloadRetryPolicy, ConnectionCircuitBreaker);
            return await combined.ExecuteAsync(downloadOperation);
        }
    }

    /// <summary>
    /// Builder para crear políticas personalizadas
    /// </summary>
    public class ResiliencePolicyBuilder
    {
        private int _retryCount = 3;
        private TimeSpan _baseDelay = TimeSpan.FromSeconds(1);
        private int _circuitBreakerThreshold = 5;
        private TimeSpan _circuitBreakerDuration = TimeSpan.FromSeconds(30);
        private TimeSpan? _timeout;

        public ResiliencePolicyBuilder WithRetry(int count, TimeSpan baseDelay)
        {
            _retryCount = count;
            _baseDelay = baseDelay;
            return this;
        }

        public ResiliencePolicyBuilder WithCircuitBreaker(int threshold, TimeSpan duration)
        {
            _circuitBreakerThreshold = threshold;
            _circuitBreakerDuration = duration;
            return this;
        }

        public ResiliencePolicyBuilder WithTimeout(TimeSpan timeout)
        {
            _timeout = timeout;
            return this;
        }

        public AsyncPolicy Build()
        {
            var retry = Policy.Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: _retryCount,
                    sleepDurationProvider: attempt => _baseDelay * Math.Pow(2, attempt - 1)
                );

            var circuitBreaker = Policy.Handle<Exception>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: _circuitBreakerThreshold,
                    durationOfBreak: _circuitBreakerDuration
                );

            var combined = Policy.WrapAsync(retry, circuitBreaker);

            if (_timeout.HasValue)
            {
                var timeoutPolicy = Policy.TimeoutAsync(_timeout.Value, TimeoutStrategy.Pessimistic);
                combined = Policy.WrapAsync(combined, timeoutPolicy);
            }

            return combined;
        }
    }
}
