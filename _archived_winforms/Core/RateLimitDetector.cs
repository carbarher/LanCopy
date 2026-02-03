using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Detector inteligente de rate limiting con backoff adaptativo
    /// </summary>
    public class RateLimitDetector
    {
        private readonly Dictionary<string, RateLimitStats> _serverStats = new();
        private readonly object _lock = new object();
        
        // Configuración
        public int BaseDelayMs { get; set; } = 4000; // 4 segundos base
        public int MaxDelayMs { get; set; } = 30000; // 30 segundos máximo
        public int FailureThreshold { get; set; } = 3; // Fallos consecutivos para aumentar
        public int SuccessThreshold { get; set; } = 5; // Éxitos para reducir delay
        
        public class RateLimitStats
        {
            public int ConsecutiveFailures { get; set; }
            public int ConsecutiveSuccesses { get; set; }
            public int CurrentDelayMs { get; set; } = 4000;
            public DateTime LastFailure { get; set; }
            public DateTime LastSuccess { get; set; }
            public bool IsRateLimited { get; set; }
        }
        
        /// <summary>
        /// Registra un éxito y ajusta el delay si es apropiado
        /// </summary>
        public void RegisterSuccess(string server = "default")
        {
            lock (_lock)
            {
                if (!_serverStats.ContainsKey(server))
                    _serverStats[server] = new RateLimitStats();
                
                var stats = _serverStats[server];
                stats.ConsecutiveSuccesses++;
                stats.ConsecutiveFailures = 0;
                stats.LastSuccess = DateTime.UtcNow;
                stats.IsRateLimited = false;
                
                // Reducir delay después de éxitos consecutivos
                if (stats.ConsecutiveSuccesses >= SuccessThreshold)
                {
                    stats.CurrentDelayMs = Math.Max(BaseDelayMs, stats.CurrentDelayMs / 2);
                    stats.ConsecutiveSuccesses = 0;
                }
            }
        }
        
        /// <summary>
        /// Registra un fallo y ajusta el delay según el tipo
        /// </summary>
        public void RegisterFailure(string server = "default", Exception exception = null)
        {
            lock (_lock)
            {
                if (!_serverStats.ContainsKey(server))
                    _serverStats[server] = new RateLimitStats();
                
                var stats = _serverStats[server];
                stats.ConsecutiveFailures++;
                stats.ConsecutiveSuccesses = 0;
                stats.LastFailure = DateTime.UtcNow;
                
                // Detectar patrones de rate limiting
                bool isRateLimitError = IsRateLimitError(exception);
                if (isRateLimitError || stats.ConsecutiveFailures >= FailureThreshold)
                {
                    stats.IsRateLimited = true;
                    // Backoff exponencial
                    stats.CurrentDelayMs = Math.Min(MaxDelayMs, stats.CurrentDelayMs * 2);
                }
            }
        }
        
        /// <summary>
        /// Obtiene el delay actual para un servidor
        /// </summary>
        public async Task<int> GetCurrentDelayAsync(string server = "default")
        {
            lock (_lock)
            {
                if (!_serverStats.ContainsKey(server))
                    _serverStats[server] = new RateLimitStats();
                
                return _serverStats[server].CurrentDelayMs;
            }
        }
        
        /// <summary>
        /// Espera el delay apropiado antes de la próxima operación
        /// </summary>
        public async Task WaitForDelayAsync(string server = "default", CancellationToken cancellationToken = default)
        {
            var delay = await GetCurrentDelayAsync(server);
            
            // Agregar jitter aleatorio para evitar sincronización
            var jitter = new Random().Next(0, delay / 4); // 0-25% de jitter
            var totalDelay = delay + jitter;
            
            await Task.Delay(totalDelay, cancellationToken);
        }
        
        /// <summary>
        /// Verifica si un error indica rate limiting
        /// </summary>
        private bool IsRateLimitError(Exception exception)
        {
            if (exception == null) return false;
            
            var message = exception.Message.ToLowerInvariant();
            return message.Contains("operation was canceled") ||
                   message.Contains("remote connection closed") ||
                   message.Contains("timeout") ||
                   message.Contains("rate limit");
        }
        
        /// <summary>
        /// Obtiene estadísticas actuales
        /// </summary>
        public RateLimitStats GetStats(string server = "default")
        {
            lock (_lock)
            {
                return _serverStats.TryGetValue(server, out var stats) ? stats : new RateLimitStats();
            }
        }
        
        /// <summary>
        /// Reinicia las estadísticas de un servidor
        /// </summary>
        public void ResetStats(string server = "default")
        {
            lock (_lock)
            {
                _serverStats[server] = new RateLimitStats();
            }
        }
    }
}
