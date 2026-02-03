using System;
using System.Threading;
using System.Threading.Tasks;
using Soulseek;

namespace SlskDown.Core
{
    /// <summary>
    /// MEJORA #1: Health Check Periódico
    /// Verifica la salud de la conexión cada 5 minutos cuando está idle
    /// Detecta desconexiones silenciosas y reconecta proactivamente
    /// </summary>
    public class HealthCheckService : IDisposable
    {
        private readonly SoulseekClient _client;
        private readonly Action<string> _logAction;
        private readonly Func<Task> _reconnectAction;
        private System.Threading.Timer _healthCheckTimer;
        private DateTime _lastActivityTime;
        private DateTime _lastHealthCheck;
        private bool _isDisposed;
        
        // Configuración
        private readonly TimeSpan _healthCheckInterval = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _idleThreshold = TimeSpan.FromMinutes(2);
        private readonly TimeSpan _pingTimeout = TimeSpan.FromSeconds(10);
        
        // Estadísticas
        public int TotalHealthChecks { get; private set; }
        public int FailedHealthChecks { get; private set; }
        public int SuccessfulReconnections { get; private set; }
        public DateTime? LastSuccessfulCheck { get; private set; }
        public DateTime? LastFailedCheck { get; private set; }
        
        public HealthCheckService(
            SoulseekClient client, 
            Action<string> logAction, 
            Func<Task> reconnectAction)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));
            _reconnectAction = reconnectAction ?? throw new ArgumentNullException(nameof(reconnectAction));
            
            _lastActivityTime = DateTime.UtcNow;
            _lastHealthCheck = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Inicia el servicio de health check
        /// </summary>
        public void Start()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(HealthCheckService));
            
            // Timer que ejecuta cada minuto para verificar si debe hacer health check
            _healthCheckTimer = new System.Threading.Timer(
                async _ => await PerformHealthCheckIfNeeded(),
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(1)
            );
            
            _logAction("Health Check Service iniciado (intervalo: 5 min)");
        }
        
        /// <summary>
        /// Detiene el servicio de health check
        /// </summary>
        public void Stop()
        {
            _healthCheckTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _logAction("Health Check Service detenido");
        }
        
        /// <summary>
        /// Notifica actividad reciente (descarga, búsqueda, etc.)
        /// </summary>
        public void NotifyActivity()
        {
            _lastActivityTime = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Realiza health check si es necesario
        /// </summary>
        private async Task PerformHealthCheckIfNeeded()
        {
            try
            {
                if (_isDisposed) return;
                
                var now = DateTime.UtcNow;
                var timeSinceLastCheck = now - _lastHealthCheck;
                var timeSinceLastActivity = now - _lastActivityTime;
                
                // Solo hacer health check si:
                // 1. Han pasado 5+ minutos desde el último check
                // 2. No ha habido actividad en los últimos 2 minutos (idle)
                if (timeSinceLastCheck < _healthCheckInterval)
                    return;
                
                if (timeSinceLastActivity < _idleThreshold)
                {
                    // Hay actividad reciente, posponer health check
                    _lastHealthCheck = now;
                    return;
                }
                
                // Realizar health check
                await PerformHealthCheck();
            }
            catch (Exception ex)
            {
                _logAction($"Error en health check timer: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Realiza el health check real
        /// </summary>
        private async Task PerformHealthCheck()
        {
            if (_isDisposed) return;
            
            _lastHealthCheck = DateTime.UtcNow;
            TotalHealthChecks++;
            
            try
            {
                // Verificar estado del cliente
                if (_client == null)
                {
                    _logAction("Health Check: Cliente es null");
                    await HandleFailedHealthCheck("Cliente null");
                    return;
                }
                
                // Verificar si está conectado
                if (!_client.State.HasFlag(SoulseekClientStates.Connected) ||
                    !_client.State.HasFlag(SoulseekClientStates.LoggedIn))
                {
                    _logAction("Health Check: Cliente desconectado");
                    await HandleFailedHealthCheck("Desconectado");
                    return;
                }
                
                // Hacer ping al servidor (búsqueda dummy con timeout)
                using var cts = new CancellationTokenSource(_pingTimeout);
                
                try
                {
                    // Intentar obtener info del servidor como ping
                    var searchTask = _client.SearchAsync(
                        SearchQuery.FromText($"healthcheck_{Guid.NewGuid():N}"),
                        options: new SearchOptions(
                            searchTimeout: 5000,
                            responseLimit: 1,
                            filterResponses: false
                        ),
                        cancellationToken: cts.Token
                    );
                    
                    await searchTask;
                    
                    // Si llegamos aquí, la conexión está viva
                    LastSuccessfulCheck = DateTime.UtcNow;
                    _logAction($"Health Check OK (idle: {(DateTime.UtcNow - _lastActivityTime).TotalMinutes:F1} min)");
                }
                catch (OperationCanceledException)
                {
                    // Timeout en el ping
                    _logAction("Health Check: Timeout en ping");
                    await HandleFailedHealthCheck("Timeout");
                }
                catch (Exception ex)
                {
                    // Error en el ping
                    _logAction($"Health Check: Error en ping - {ex.Message}");
                    await HandleFailedHealthCheck(ex.Message);
                }
            }
            catch (Exception ex)
            {
                _logAction($"Health Check: Error general - {ex.Message}");
                FailedHealthChecks++;
                LastFailedCheck = DateTime.UtcNow;
            }
        }
        
        /// <summary>
        /// Maneja un health check fallido
        /// </summary>
        private async Task HandleFailedHealthCheck(string reason)
        {
            FailedHealthChecks++;
            LastFailedCheck = DateTime.UtcNow;
            
            _logAction($"Health Check falló ({reason}), iniciando reconexión proactiva...");
            
            try
            {
                await _reconnectAction();
                SuccessfulReconnections++;
                _logAction("Reconexión proactiva exitosa");
            }
            catch (Exception ex)
            {
                _logAction($"Error en reconexión proactiva: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtiene estadísticas del servicio
        /// </summary>
        public HealthCheckStats GetStats()
        {
            return new HealthCheckStats
            {
                TotalChecks = TotalHealthChecks,
                FailedChecks = FailedHealthChecks,
                SuccessRate = TotalHealthChecks > 0 
                    ? (double)(TotalHealthChecks - FailedHealthChecks) / TotalHealthChecks * 100 
                    : 100,
                SuccessfulReconnections = SuccessfulReconnections,
                LastSuccessfulCheck = LastSuccessfulCheck,
                LastFailedCheck = LastFailedCheck,
                MinutesSinceLastActivity = (DateTime.UtcNow - _lastActivityTime).TotalMinutes,
                MinutesSinceLastCheck = (DateTime.UtcNow - _lastHealthCheck).TotalMinutes
            };
        }
        
        public void Dispose()
        {
            if (_isDisposed) return;
            
            _isDisposed = true;
            _healthCheckTimer?.Dispose();
            _healthCheckTimer = null;
            
            _logAction("🛑 Health Check Service disposed");
        }
    }
    
    /// <summary>
    /// Estadísticas del servicio de health check
    /// </summary>
    public class HealthCheckStats
    {
        public int TotalChecks { get; set; }
        public int FailedChecks { get; set; }
        public double SuccessRate { get; set; }
        public int SuccessfulReconnections { get; set; }
        public DateTime? LastSuccessfulCheck { get; set; }
        public DateTime? LastFailedCheck { get; set; }
        public double MinutesSinceLastActivity { get; set; }
        public double MinutesSinceLastCheck { get; set; }
    }
}
