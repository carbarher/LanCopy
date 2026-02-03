using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown
{
    /// <summary>
    /// Monitor de salud de conexión con métricas de latencia y timeouts
    /// </summary>
    public class ConnectionHealthMonitor
    {
        private int consecutiveTimeouts = 0;
        private int consecutiveKeepAliveFailures = 0;
        private readonly Queue<TimeSpan> latencySamples = new Queue<TimeSpan>(20);
        private readonly Queue<DateTime> timeoutHistory = new Queue<DateTime>(50);
        private readonly object metricsLock = new object();
        
        private DateTime lastSuccessfulOperation = DateTime.UtcNow;
        private DateTime lastHealthCheck = DateTime.UtcNow;
        
        // Umbrales configurables
        private readonly int timeoutThreshold;
        private readonly int keepAliveThreshold;
        private readonly TimeSpan degradedLatencyThreshold;
        private readonly TimeSpan criticalLatencyThreshold;
        
        public ConnectionHealthMonitor(
            int timeoutThreshold = 3,
            int keepAliveThreshold = 3,
            int degradedLatencyMs = 5000,
            int criticalLatencyMs = 10000)
        {
            this.timeoutThreshold = timeoutThreshold;
            this.keepAliveThreshold = keepAliveThreshold;
            this.degradedLatencyThreshold = TimeSpan.FromMilliseconds(degradedLatencyMs);
            this.criticalLatencyThreshold = TimeSpan.FromMilliseconds(criticalLatencyMs);
        }
        
        /// <summary>
        /// Verifica el estado de salud de la conexión
        /// </summary>
        public ConnectionHealth CheckHealth()
        {
            lock (metricsLock)
            {
                lastHealthCheck = DateTime.UtcNow;
                
                // Crítico: múltiples timeouts consecutivos
                if (consecutiveTimeouts >= timeoutThreshold)
                    return ConnectionHealth.Critical;
                
                // Crítico: múltiples fallos de keep-alive
                if (consecutiveKeepAliveFailures >= keepAliveThreshold)
                    return ConnectionHealth.Critical;
                
                // Crítico: sin operaciones exitosas en 5 minutos
                if (DateTime.UtcNow - lastSuccessfulOperation > TimeSpan.FromMinutes(5))
                    return ConnectionHealth.Critical;
                
                // Degradado: latencia alta
                if (latencySamples.Count >= 5)
                {
                    var avgLatency = TimeSpan.FromMilliseconds(
                        latencySamples.Average(ts => ts.TotalMilliseconds)
                    );
                    
                    if (avgLatency >= criticalLatencyThreshold)
                        return ConnectionHealth.Critical;
                    
                    if (avgLatency >= degradedLatencyThreshold)
                        return ConnectionHealth.Degraded;
                }
                
                // Degradado: timeouts recientes (no consecutivos)
                if (consecutiveTimeouts > 0 || timeoutHistory.Count > 5)
                    return ConnectionHealth.Degraded;
                
                return ConnectionHealth.Healthy;
            }
        }
        
        /// <summary>
        /// Registra un timeout
        /// </summary>
        public void RecordTimeout()
        {
            lock (metricsLock)
            {
                consecutiveTimeouts++;
                timeoutHistory.Enqueue(DateTime.UtcNow);
                
                // Mantener solo últimos 50 timeouts
                while (timeoutHistory.Count > 50)
                    timeoutHistory.Dequeue();
            }
        }
        
        /// <summary>
        /// Registra un fallo de keep-alive
        /// </summary>
        public void RecordKeepAliveFailure()
        {
            lock (metricsLock)
            {
                consecutiveKeepAliveFailures++;
            }
        }
        
        /// <summary>
        /// Registra una operación exitosa con su latencia
        /// </summary>
        public void RecordSuccess(TimeSpan latency)
        {
            lock (metricsLock)
            {
                consecutiveTimeouts = 0;
                consecutiveKeepAliveFailures = 0;
                lastSuccessfulOperation = DateTime.UtcNow;
                
                latencySamples.Enqueue(latency);
                
                // Mantener solo últimas 20 muestras
                while (latencySamples.Count > 20)
                    latencySamples.Dequeue();
            }
        }
        
        /// <summary>
        /// Obtiene métricas detalladas
        /// </summary>
        public ConnectionMetrics GetMetrics()
        {
            lock (metricsLock)
            {
                var metrics = new ConnectionMetrics
                {
                    Health = CheckHealth(),
                    ConsecutiveTimeouts = consecutiveTimeouts,
                    ConsecutiveKeepAliveFailures = consecutiveKeepAliveFailures,
                    LastSuccessfulOperation = lastSuccessfulOperation,
                    TimeSinceLastSuccess = DateTime.UtcNow - lastSuccessfulOperation
                };
                
                if (latencySamples.Count > 0)
                {
                    metrics.AverageLatency = TimeSpan.FromMilliseconds(
                        latencySamples.Average(ts => ts.TotalMilliseconds)
                    );
                    metrics.MinLatency = TimeSpan.FromMilliseconds(
                        latencySamples.Min(ts => ts.TotalMilliseconds)
                    );
                    metrics.MaxLatency = TimeSpan.FromMilliseconds(
                        latencySamples.Max(ts => ts.TotalMilliseconds)
                    );
                    metrics.LatencySampleCount = latencySamples.Count;
                }
                
                // Calcular tasa de timeouts (últimos 5 minutos)
                var recentTimeouts = timeoutHistory.Count(t => 
                    DateTime.UtcNow - t < TimeSpan.FromMinutes(5)
                );
                metrics.RecentTimeoutCount = recentTimeouts;
                
                return metrics;
            }
        }
        
        /// <summary>
        /// Resetea todas las métricas
        /// </summary>
        public void Reset()
        {
            lock (metricsLock)
            {
                consecutiveTimeouts = 0;
                consecutiveKeepAliveFailures = 0;
                latencySamples.Clear();
                timeoutHistory.Clear();
                lastSuccessfulOperation = DateTime.UtcNow;
                lastHealthCheck = DateTime.UtcNow;
            }
        }
        
        /// <summary>
        /// Obtiene un resumen legible del estado
        /// </summary>
        public string GetStatusSummary()
        {
            var metrics = GetMetrics();
            var summary = $"Salud: {metrics.Health}";
            
            if (metrics.AverageLatency != TimeSpan.Zero)
            {
                summary += $", Latencia: {metrics.AverageLatency.TotalMilliseconds:F0}ms";
            }
            
            if (metrics.ConsecutiveTimeouts > 0)
            {
                summary += $", Timeouts: {metrics.ConsecutiveTimeouts}";
            }
            
            if (metrics.TimeSinceLastSuccess > TimeSpan.FromMinutes(1))
            {
                summary += $", Última operación: {metrics.TimeSinceLastSuccess.TotalSeconds:F0}s";
            }
            
            return summary;
        }
    }
    
    public enum ConnectionHealth
    {
        /// <summary>
        /// Conexión saludable - operaciones normales
        /// </summary>
        Healthy,
        
        /// <summary>
        /// Conexión degradada - latencia alta o timeouts ocasionales
        /// </summary>
        Degraded,
        
        /// <summary>
        /// Conexión crítica - múltiples fallos, requiere reconexión
        /// </summary>
        Critical
    }
    
    public class ConnectionMetrics
    {
        public ConnectionHealth Health { get; set; }
        public int ConsecutiveTimeouts { get; set; }
        public int ConsecutiveKeepAliveFailures { get; set; }
        public DateTime LastSuccessfulOperation { get; set; }
        public TimeSpan TimeSinceLastSuccess { get; set; }
        public TimeSpan AverageLatency { get; set; }
        public TimeSpan MinLatency { get; set; }
        public TimeSpan MaxLatency { get; set; }
        public int LatencySampleCount { get; set; }
        public int RecentTimeoutCount { get; set; }
    }
}
