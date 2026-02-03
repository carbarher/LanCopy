using System;
using System.Threading;
using System.Threading.Tasks;
using Soulseek;

namespace SlskDown.Core
{
    /// <summary>
    /// Gestiona la conexión a Soulseek con reconexión automática y circuit breaker
    /// </summary>
    public class ConnectionManager
    {
        // Configuración
        private readonly ConnectionManagerConfig config;
        
        // Cliente Soulseek
        private ISoulseekClient client;
        
        // Estado de conexión
        private bool isConnected = false;
        private bool isConnecting = false;
        private readonly SemaphoreSlim connectionSemaphore = new SemaphoreSlim(1, 1);
        
        // Reconexión automática
        private bool autoReconnectEnabled = false;
        private CancellationTokenSource reconnectCts;
        
        // Circuit Breaker
        private int consecutiveFailures = 0;
        private DateTime circuitOpenedAt = DateTime.MinValue;
        private const int CIRCUIT_BREAKER_THRESHOLD = 5;
        private const int CIRCUIT_BREAKER_TIMEOUT_MINUTES = 5;
        
        // Callbacks
        public Action<string> OnLog { get; set; }
        public Action OnConnected { get; set; }
        public Action OnDisconnected { get; set; }
        public Action<string> OnConnectionFailed { get; set; }
        
        public ConnectionManager(ConnectionManagerConfig configuration)
        {
            config = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }
        
        /// <summary>
        /// Configura el cliente Soulseek
        /// </summary>
        public void SetClient(ISoulseekClient soulseekClient)
        {
            client = soulseekClient ?? throw new ArgumentNullException(nameof(soulseekClient));
            
            // Suscribirse a eventos del cliente
            client.StateChanged += OnClientStateChanged;
            client.ServerInfoReceived += OnServerInfoReceived;
        }
        
        /// <summary>
        /// Estado actual de la conexión
        /// </summary>
        public bool IsConnected => isConnected && client?.State == SoulseekClientStates.Connected;
        
        /// <summary>
        /// Conecta al servidor de Soulseek
        /// </summary>
        public async Task<bool> ConnectAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                Log("❌ Usuario o contraseña vacíos");
                return false;
            }
            
            // Verificar circuit breaker
            if (IsCircuitOpen())
            {
                var timeRemaining = CIRCUIT_BREAKER_TIMEOUT_MINUTES - (DateTime.Now - circuitOpenedAt).TotalMinutes;
                Log($"⚠️ Circuit breaker abierto. Espera {timeRemaining:F1} minutos");
                return false;
            }
            
            await connectionSemaphore.WaitAsync();
            
            try
            {
                if (isConnecting)
                {
                    Log("⚠️ Ya hay una conexión en progreso");
                    return false;
                }
                
                if (IsConnected)
                {
                    Log("✅ Ya está conectado");
                    return true;
                }
                
                isConnecting = true;
                Log($"🔌 Conectando como: {username}");
                
                // Intentar conexión con reintentos
                for (int attempt = 1; attempt <= config.MaxConnectionAttempts; attempt++)
                {
                    try
                    {
                        Log($"🔄 Intento {attempt}/{config.MaxConnectionAttempts}...");
                        
                        await client.ConnectAsync(username, password);
                        
                        // Éxito
                        isConnected = true;
                        consecutiveFailures = 0;
                        Log($"✅ Conectado exitosamente (intento {attempt})");
                        OnConnected?.Invoke();
                        
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Log($"❌ Intento {attempt} falló: {ex.Message}");
                        
                        if (attempt < config.MaxConnectionAttempts)
                        {
                            int delay = GetBackoffDelay(attempt);
                            Log($"⏳ Esperando {delay}ms antes del siguiente intento...");
                            await Task.Delay(delay);
                        }
                    }
                }
                
                // Todos los intentos fallaron
                consecutiveFailures++;
                
                if (consecutiveFailures >= CIRCUIT_BREAKER_THRESHOLD)
                {
                    circuitOpenedAt = DateTime.Now;
                    Log($"🔴 Circuit breaker abierto después de {consecutiveFailures} fallos");
                }
                
                string errorMsg = $"No se pudo conectar después de {config.MaxConnectionAttempts} intentos";
                Log($"❌ {errorMsg}");
                OnConnectionFailed?.Invoke(errorMsg);
                
                return false;
            }
            finally
            {
                isConnecting = false;
                connectionSemaphore.Release();
            }
        }
        
        /// <summary>
        /// Desconecta del servidor
        /// </summary>
        public async Task DisconnectAsync()
        {
            await connectionSemaphore.WaitAsync();
            
            try
            {
                if (client != null && IsConnected)
                {
                    Log("🔌 Desconectando...");
                    // ERROR: await client.DisconnectAsync();
                    isConnected = false;
                    Log("✅ Desconectado");
                    OnDisconnected?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error al desconectar: {ex.Message}");
            }
            finally
            {
                connectionSemaphore.Release();
            }
        }
        
        /// <summary>
        /// Habilita reconexión automática
        /// </summary>
        public void EnableAutoReconnect(string username, string password)
        {
            if (autoReconnectEnabled) return;
            
            autoReconnectEnabled = true;
            reconnectCts = new CancellationTokenSource();
            
            _ = Task.Run(async () => await AutoReconnectLoop(username, password, reconnectCts.Token));
            
            Log("✅ Reconexión automática habilitada");
        }
        
        /// <summary>
        /// Deshabilita reconexión automática
        /// </summary>
        public void DisableAutoReconnect()
        {
            if (!autoReconnectEnabled) return;
            
            autoReconnectEnabled = false;
            reconnectCts?.Cancel();
            
            Log("⏹️ Reconexión automática deshabilitada");
        }
        
        /// <summary>
        /// Loop de reconexión automática
        /// </summary>
        private async Task AutoReconnectLoop(string username, string password, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && autoReconnectEnabled)
            {
                try
                {
                    await Task.Delay(config.ReconnectCheckInterval, ct);
                    
                    if (!IsConnected && !isConnecting)
                    {
                        Log("🔄 Reconexión automática...");
                        await ConnectAsync(username, password);
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log($"⚠️ Error en reconexión automática: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Verifica si el circuit breaker está abierto
        /// </summary>
        private bool IsCircuitOpen()
        {
            if (circuitOpenedAt == DateTime.MinValue)
                return false;
            
            var elapsed = DateTime.Now - circuitOpenedAt;
            
            if (elapsed.TotalMinutes >= CIRCUIT_BREAKER_TIMEOUT_MINUTES)
            {
                // Cerrar circuit breaker
                circuitOpenedAt = DateTime.MinValue;
                consecutiveFailures = 0;
                Log("🟢 Circuit breaker cerrado");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Calcula delay con exponential backoff
        /// </summary>
        private int GetBackoffDelay(int attempt)
        {
            // Exponential backoff: 3s, 6s, 12s, 24s, 48s
            int baseDelay = config.BaseReconnectDelay;
            int delay = baseDelay * (int)Math.Pow(2, attempt - 1);
            
            // Cap máximo
            return Math.Min(delay, config.MaxReconnectDelay);
        }
        
        /// <summary>
        /// Maneja cambios de estado del cliente
        /// </summary>
        private void OnClientStateChanged(object sender, SoulseekClientStateChangedEventArgs e)
        {
            Log($"📡 Estado: {e.PreviousState} → {e.State}");
            
            if (e.State == SoulseekClientStates.Connected)
            {
                isConnected = true;
                consecutiveFailures = 0;
                OnConnected?.Invoke();
            }
            else if (e.State == SoulseekClientStates.Disconnected)
            {
                isConnected = false;
                OnDisconnected?.Invoke();
            }
        }
        
        /// <summary>
        /// Maneja información del servidor
        /// </summary>
        private void OnServerInfoReceived(object sender, ServerInfo info)
        {
            if (info != null)
            {
                Log($"📊 Servidor: {info.ParentMinSpeed} KB/s min, {info.WishlistInterval}s wishlist");
            }
        }
        
        /// <summary>
        /// Reinicia el circuit breaker manualmente
        /// </summary>
        public void ResetCircuitBreaker()
        {
            circuitOpenedAt = DateTime.MinValue;
            consecutiveFailures = 0;
            Log("🔄 Circuit breaker reiniciado manualmente");
        }
        
        /// <summary>
        /// Obtiene estadísticas de conexión
        /// </summary>
        public ConnectionStats GetStats()
        {
            return new ConnectionStats
            {
                IsConnected = IsConnected,
                IsConnecting = isConnecting,
                ConsecutiveFailures = consecutiveFailures,
                IsCircuitOpen = IsCircuitOpen(),
                AutoReconnectEnabled = autoReconnectEnabled
            };
        }
        
        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
        
        public void Dispose()
        {
            DisableAutoReconnect();
            reconnectCts?.Dispose();
            connectionSemaphore?.Dispose();
        }
    }
    
    /// <summary>
    /// Configuración del ConnectionManager
    /// </summary>
    public class ConnectionManagerConfig
    {
        public int MaxConnectionAttempts { get; set; } = 5;
        public int BaseReconnectDelay { get; set; } = 3000; // 3 segundos
        public int MaxReconnectDelay { get; set; } = 60000; // 60 segundos
        public int ReconnectCheckInterval { get; set; } = 30000; // 30 segundos
    }
    
    /// <summary>
    /// Estadísticas de conexión
    /// </summary>
    public class ConnectionStats
    {
        public bool IsConnected { get; set; }
        public bool IsConnecting { get; set; }
        public int ConsecutiveFailures { get; set; }
        public bool IsCircuitOpen { get; set; }
        public bool AutoReconnectEnabled { get; set; }
    }
}
