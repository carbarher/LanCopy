using System;
using System.Threading.Tasks;

namespace SlskDown.Core.Optimization
{
    /// <summary>
    /// Extensiones para facilitar uso de optimizaciones
    /// </summary>
    public static class OptimizationExtensions
    {
        private static readonly MetricsCollector globalMetrics = new MetricsCollector();
        
        /// <summary>
        /// Obtiene instancia global de métricas
        /// </summary>
        public static MetricsCollector GlobalMetrics => globalMetrics;
        
        /// <summary>
        /// Ejecuta con medición de duración
        /// </summary>
        public static async Task<T> WithMetricsAsync<T>(
            this Task<T> task,
            string metricName)
        {
            using (globalMetrics.MeasureDuration(metricName))
            {
                return await task.ConfigureAwait(false);
            }
        }
        
        /// <summary>
        /// Ejecuta con medición de duración (sin retorno)
        /// </summary>
        public static async Task WithMetricsAsync(
            this Task task,
            string metricName)
        {
            using (globalMetrics.MeasureDuration(metricName))
            {
                await task.ConfigureAwait(false);
            }
        }
        
        /// <summary>
        /// Ejecuta con rate limiting
        /// </summary>
        public static async Task<T> WithRateLimitAsync<T>(
            this Task<T> task,
            RateLimiter rateLimiter)
        {
            await rateLimiter.WaitAsync().ConfigureAwait(false);
            return await task.ConfigureAwait(false);
        }
        
        /// <summary>
        /// Ejecuta con circuit breaker
        /// </summary>
        public static async Task<T> WithCircuitBreakerAsync<T>(
            this Func<Task<T>> func,
            CircuitBreaker circuitBreaker)
        {
            return await circuitBreaker.ExecuteAsync(func).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Incrementa contador de métrica
        /// </summary>
        public static void IncrementMetric(string name, long value = 1)
        {
            globalMetrics.IncrementCounter(name, value);
        }
        
        /// <summary>
        /// Registra valor en métrica
        /// </summary>
        public static void RecordMetric(string name, double value)
        {
            globalMetrics.RecordValue(name, value);
        }
        
        /// <summary>
        /// Establece gauge
        /// </summary>
        public static void SetGauge(string name, double value)
        {
            globalMetrics.SetGauge(name, value);
        }
    }
}
