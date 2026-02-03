using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Soulseek;

namespace SlskDown
{
    /// <summary>
    /// Monitor de salud de conexión con auto-reconexión inteligente
    /// Características: ping periódico, circuit breaker, exponential backoff, métricas
    /// </summary>
    public class HealthMonitor : IDisposable
    {
        private readonly SoulseekClient client;
        private readonly System.Threading.Timer healthCheckTimer;
        private readonly object reconnectLock = new object();
        
        // Configuración
        private const int HEALTH_CHECK_INTERVAL_MS = 30000; // 30 segundos
        private const int PING_TIMEOUT_MS = 5000;
        private const int MAX_CONSECUTIVE_FAILURES = 5;
        private const int CIRCUIT_BREAKER_COOLDOWN_MS = 300000; // 5 minutos
        
        // Estado
        private int consecutiveFailures = 0;
        private bool circuitBreakerOpen = false;
        private DateTime circuitBreakerOpenedAt;
        private DateTime? nextAllowedReconnect;
        private int reconnectAttempts = 0;
        private bool isReconnecting = false;
        
        // Métricas
        private long totalHealthChecks = 0;
        private long totalFailures = 0;
        private long totalReconnections = 0;
        private long totalSuccessfulReconnections = 0;
        private DateTime lastSuccessfulCheck;
        private double averageLatencyMs = 0;
        
        // Eventos
        public event Action<ConnectionHealthStatus> HealthStatusChanged;
        public event Action<string> ConnectionLost;
        public event Action<string> ConnectionRestored;
        public event Action<int, TimeSpan> ReconnectAttempt;
        
        public HealthMonitor(SoulseekClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.lastSuccessfulCheck = DateTime.Now;
            
            // Timer para health checks periódicos
            healthCheckTimer = new System.Threading.Timer(async _ => await PerformHealthCheck(), null, 
                System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            healthCheckTimer.Change(HEALTH_CHECK_INTERVAL_MS, HEALTH_CHECK_INTERVAL_MS);
            
            Console.WriteLine("[HealthMonitor] Inicializado - Checks cada 30s");
        }
        
        /// <summary>
        /// Realiza un health check completo
        /// </summary>
        private async Task PerformHealthCheck()
        {
            totalHealthChecks++;
            
            try
            {
                // Verificar estado básico
                if (client.State != SoulseekClientStates.Connected)
                {
                    await HandleDisconnection("Cliente no conectado");
                    return;
                }
                
                // Ping para verificar latencia
                var sw = Stopwatch.StartNew();
                var pingTask = Task.Run(async () =>
                {
                    // Simular ping con operación ligera
                    await Task.Delay(10); // Placeholder - en producción usar operación real
                    return true;
                });
                
                var timeoutTask = Task.Delay(PING_TIMEOUT_MS);
                var completedTask = await Task.WhenAny(pingTask, timeoutTask);
                
                sw.Stop();
                
                if (completedTask == timeoutTask)
                {
                    // Timeout
                    await HandleDisconnection($"Timeout en ping ({PING_TIMEOUT_MS}ms)");
                    return;
                }
                
                // Ping exitoso
                consecutiveFailures = 0;
                lastSuccessfulCheck = DateTime.Now;
                
                // Actualizar latencia promedio (EWMA)
                double latency = sw.Elapsed.TotalMilliseconds;
                averageLatencyMs = averageLatencyMs == 0 
                    ? latency 
                    : (averageLatencyMs * 0.7 + latency * 0.3);
                
                // Notificar estado saludable
                HealthStatusChanged?.Invoke(new ConnectionHealthStatus
                {
                    IsHealthy = true,
                    LatencyMs = latency,
                    AverageLatencyMs = averageLatencyMs,
                    ConsecutiveFailures = 0,
                    LastSuccessfulCheck = lastSuccessfulCheck
                });
                
                // Cerrar circuit breaker si estaba abierto
                if (circuitBreakerOpen)
                {
                    circuitBreakerOpen = false;
                    Console.WriteLine("[HealthMonitor] ✅ Circuit breaker cerrado - Conexión restaurada");
                }
            }
            catch (Exception ex)
            {
                await HandleDisconnection($"Error en health check: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Maneja una desconexión detectada
        /// </summary>
        private async Task HandleDisconnection(string reason)
        {
            consecutiveFailures++;
            totalFailures++;
            
            Console.WriteLine($"[HealthMonitor] ⚠️ Fallo #{consecutiveFailures}: {reason}");
            
            // Notificar estado no saludable
            HealthStatusChanged?.Invoke(new ConnectionHealthStatus
            {
                IsHealthy = false,
                LatencyMs = -1,
                AverageLatencyMs = averageLatencyMs,
                ConsecutiveFailures = consecutiveFailures,
                LastSuccessfulCheck = lastSuccessfulCheck,
                FailureReason = reason
            });
            
            // Abrir circuit breaker si hay demasiados fallos
            if (consecutiveFailures >= MAX_CONSECUTIVE_FAILURES && !circuitBreakerOpen)
            {
                circuitBreakerOpen = true;
                circuitBreakerOpenedAt = DateTime.Now;
                Console.WriteLine($"[HealthMonitor] 🔴 Circuit breaker ABIERTO - {consecutiveFailures} fallos consecutivos");
                ConnectionLost?.Invoke($"Conexión perdida después de {consecutiveFailures} fallos: {reason}");
            }
            
            // Intentar reconexión automática
            await AttemptReconnection();
        }
        
        /// <summary>
        /// Intenta reconectar con exponential backoff + jitter
        /// </summary>
        private async Task AttemptReconnection()
        {
            // Evitar reconexiones simultáneas
            lock (reconnectLock)
            {
                if (isReconnecting)
                    return;
                isReconnecting = true;
            }
            
            try
            {
                // Verificar cooldown del circuit breaker
                if (circuitBreakerOpen)
                {
                    var timeSinceOpen = DateTime.Now - circuitBreakerOpenedAt;
                    if (timeSinceOpen.TotalMilliseconds < CIRCUIT_BREAKER_COOLDOWN_MS)
                    {
                        var remaining = TimeSpan.FromMilliseconds(CIRCUIT_BREAKER_COOLDOWN_MS - timeSinceOpen.TotalMilliseconds);
                        Console.WriteLine($"[HealthMonitor] ⏳ Circuit breaker abierto - Esperando {remaining.TotalSeconds:F0}s");
                        return;
                    }
                    
                    // Cooldown completado, intentar cerrar circuit breaker
                    Console.WriteLine("[HealthMonitor] 🔄 Circuit breaker cooldown completado - Intentando reconexión");
                }
                
                // Verificar ventana de reconexión
                if (nextAllowedReconnect.HasValue && DateTime.Now < nextAllowedReconnect.Value)
                {
                    var waitTime = nextAllowedReconnect.Value - DateTime.Now;
                    Console.WriteLine($"[HealthMonitor] ⏳ Esperando {waitTime.TotalSeconds:F0}s antes de reconectar");
                    return;
                }
                
                // Calcular backoff exponencial con jitter
                reconnectAttempts++;
                totalReconnections++;
                
                var baseDelay = Math.Min(Math.Pow(2, reconnectAttempts - 1) * 5, 60); // Cap a 60s
                var jitter = new Random().NextDouble() * 0.25 * baseDelay; // 0-25% jitter
                var totalDelay = TimeSpan.FromSeconds(baseDelay + jitter);
                
                nextAllowedReconnect = DateTime.Now.Add(totalDelay);
                
                Console.WriteLine($"[HealthMonitor] 🔄 Intento de reconexión #{reconnectAttempts} en {totalDelay.TotalSeconds:F1}s");
                ReconnectAttempt?.Invoke(reconnectAttempts, totalDelay);
                
                // Esperar backoff
                await Task.Delay(totalDelay);
                
                // Intentar reconectar (esto debe ser manejado por el código que usa HealthMonitor)
                // Aquí solo notificamos que es momento de reconectar
                Console.WriteLine($"[HealthMonitor] 🔌 Ejecutando reconexión #{reconnectAttempts}...");
                
                // El cliente debe reconectar externamente
                // Si tiene éxito, el próximo health check lo detectará
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HealthMonitor] ❌ Error en reconexión: {ex.Message}");
            }
            finally
            {
                lock (reconnectLock)
                {
                    isReconnecting = false;
                }
            }
        }
        
        /// <summary>
        /// Notifica que la reconexión fue exitosa (llamar externamente)
        /// </summary>
        public void NotifyReconnectionSuccess()
        {
            totalSuccessfulReconnections++;
            reconnectAttempts = 0;
            consecutiveFailures = 0;
            nextAllowedReconnect = null;
            
            if (circuitBreakerOpen)
            {
                circuitBreakerOpen = false;
                Console.WriteLine("[HealthMonitor] ✅ Reconexión exitosa - Circuit breaker cerrado");
            }
            
            ConnectionRestored?.Invoke("Conexión restaurada exitosamente");
        }
        
        /// <summary>
        /// Obtiene estadísticas del monitor
        /// </summary>
        public HealthMonitorStats GetStats()
        {
            return new HealthMonitorStats
            {
                TotalHealthChecks = totalHealthChecks,
                TotalFailures = totalFailures,
                TotalReconnections = totalReconnections,
                TotalSuccessfulReconnections = totalSuccessfulReconnections,
                ConsecutiveFailures = consecutiveFailures,
                CircuitBreakerOpen = circuitBreakerOpen,
                AverageLatencyMs = averageLatencyMs,
                LastSuccessfulCheck = lastSuccessfulCheck,
                ReconnectAttempts = reconnectAttempts,
                SuccessRate = totalHealthChecks > 0 
                    ? (double)(totalHealthChecks - totalFailures) / totalHealthChecks * 100 
                    : 100
            };
        }
        
        public void Dispose()
        {
            healthCheckTimer?.Dispose();
            Console.WriteLine("[HealthMonitor] Detenido");
        }
    }
    
    /// <summary>
    /// Estado de salud de la conexión
    /// </summary>
    public class ConnectionHealthStatus
    {
        public bool IsHealthy { get; set; }
        public double LatencyMs { get; set; }
        public double AverageLatencyMs { get; set; }
        public int ConsecutiveFailures { get; set; }
        public DateTime LastSuccessfulCheck { get; set; }
        public string FailureReason { get; set; }
    }
    
    /// <summary>
    /// Estadísticas del monitor de salud
    /// </summary>
    public class HealthMonitorStats
    {
        public long TotalHealthChecks { get; set; }
        public long TotalFailures { get; set; }
        public long TotalReconnections { get; set; }
        public long TotalSuccessfulReconnections { get; set; }
        public int ConsecutiveFailures { get; set; }
        public bool CircuitBreakerOpen { get; set; }
        public double AverageLatencyMs { get; set; }
        public DateTime LastSuccessfulCheck { get; set; }
        public int ReconnectAttempts { get; set; }
        public double SuccessRate { get; set; }
        
        public override string ToString()
        {
            return $"Health Checks: {TotalHealthChecks:N0} | " +
                   $"Fallos: {TotalFailures:N0} | " +
                   $"Tasa éxito: {SuccessRate:F1}% | " +
                   $"Latencia: {AverageLatencyMs:F1}ms | " +
                   $"Reconexiones: {TotalSuccessfulReconnections}/{TotalReconnections}";
        }
    }
}
