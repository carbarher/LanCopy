using System;
using System.Net.Http;
using System.Threading.Tasks;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace SlskDown.Core.Optimization
{
    /// <summary>
    /// Políticas de resiliencia con Polly para operaciones críticas
    /// Retry, Circuit Breaker, Timeout combinados
    /// </summary>
    public static class ResiliencePolicy
    {
        /// <summary>
        /// Política para HTTP requests con retry y circuit breaker
        /// </summary>
        public static AsyncRetryPolicy<HttpResponseMessage> HttpRetryPolicy { get; } = 
            Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .OrResult(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        // Log retry
                    });
        
        /// <summary>
        /// Circuit breaker para HTTP requests
        /// </summary>
        public static AsyncCircuitBreakerPolicy<HttpResponseMessage> HttpCircuitBreakerPolicy { get; } =
            Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500)
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (outcome, duration) =>
                    {
                        // Log circuit break
                    },
                    onReset: () =>
                    {
                        // Log circuit reset
                    });
        
        /// <summary>
        /// Política de timeout
        /// </summary>
        public static AsyncTimeoutPolicy TimeoutPolicy(int seconds) =>
            Policy.TimeoutAsync(TimeSpan.FromSeconds(seconds));
        
        /// <summary>
        /// Política combinada: Retry + Circuit Breaker
        /// </summary>
        public static IAsyncPolicy<HttpResponseMessage> HttpFullPolicy { get; } =
            HttpCircuitBreakerPolicy.WrapAsync(HttpRetryPolicy);
        
        /// <summary>
        /// Política para operaciones de base de datos
        /// </summary>
        public static AsyncRetryPolicy DatabaseRetryPolicy { get; } =
            Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(100 * attempt),
                    onRetry: (exception, timespan, retryCount, context) =>
                    {
                        // Log retry
                    });
        
        /// <summary>
        /// Política para operaciones de archivo
        /// </summary>
        public static AsyncRetryPolicy FileRetryPolicy { get; } =
            Policy
                .Handle<System.IO.IOException>()
                .WaitAndRetryAsync(
                    retryCount: 5,
                    sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(50 * attempt),
                    onRetry: (exception, timespan, retryCount, context) =>
                    {
                        // Log retry
                    });
        
        /// <summary>
        /// Ejecuta acción con retry automático
        /// </summary>
        public static async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> action,
            int maxRetries = 3,
            int delayMs = 100)
        {
            var policy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: maxRetries,
                    sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(delayMs * attempt)
                );
            
            return await policy.ExecuteAsync(action).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Ejecuta acción con timeout
        /// </summary>
        public static async Task<T> ExecuteWithTimeoutAsync<T>(
            Func<Task<T>> action,
            int timeoutSeconds = 30)
        {
            var policy = Policy.TimeoutAsync<T>(TimeSpan.FromSeconds(timeoutSeconds));
            return await policy.ExecuteAsync(action).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Ejecuta acción con retry + timeout
        /// </summary>
        public static async Task<T> ExecuteWithRetryAndTimeoutAsync<T>(
            Func<Task<T>> action,
            int maxRetries = 3,
            int timeoutSeconds = 30)
        {
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(maxRetries, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            
            return await retryPolicy.ExecuteAsync(async () =>
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                return await action().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Crea política personalizada de retry
        /// </summary>
        public static AsyncRetryPolicy CreateCustomRetryPolicy(
            int retryCount,
            Func<int, TimeSpan> sleepDurationProvider,
            Action<Exception, TimeSpan, int> onRetry = null)
        {
            var policy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: retryCount,
                    sleepDurationProvider: sleepDurationProvider
                );
            
            if (onRetry != null)
            {
                policy = Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(
                        retryCount: retryCount,
                        sleepDurationProvider: sleepDurationProvider,
                        onRetry: (ex, ts, count, ctx) => onRetry(ex, ts, count)
                    );
            }
            
            return policy;
        }
    }
}
