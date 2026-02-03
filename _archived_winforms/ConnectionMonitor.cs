using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// Monitor de conexión de red para detección proactiva de problemas
    /// </summary>
    public class ConnectionMonitor
    {
        private System.Threading.Timer pingTimer;
        private bool isNetworkAvailable = true;
        private DateTime lastSuccessfulPing = DateTime.Now;
        private int consecutiveFailures = 0;
        private const int MAX_CONSECUTIVE_FAILURES = 3;
        
        public event Action<bool> NetworkStatusChanged;
        public event Action ConnectionLost;
        public event Action ConnectionRestored;
        
        public bool IsNetworkAvailable => isNetworkAvailable;
        public DateTime LastSuccessfulPing => lastSuccessfulPing;
        public int ConsecutiveFailures => consecutiveFailures;
        
        /// <summary>
        /// Inicia monitoreo de conexión
        /// </summary>
        public void Start(int intervalSeconds = 30)
        {
            // Verificar disponibilidad inicial
            isNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();
            
            // Timer para verificación periódica
            pingTimer = new System.Threading.Timer(async _ => await CheckConnection(), null, 
                TimeSpan.Zero, TimeSpan.FromSeconds(intervalSeconds));
        }
        
        /// <summary>
        /// Detiene monitoreo
        /// </summary>
        public void Stop()
        {
            pingTimer?.Dispose();
            pingTimer = null;
        }
        
        /// <summary>
        /// Verifica conexión a internet
        /// </summary>
        private async Task CheckConnection()
        {
            try
            {
                // Verificar interfaz de red
                bool networkAvailable = NetworkInterface.GetIsNetworkAvailable();
                
                if (!networkAvailable)
                {
                    HandleConnectionFailure("No hay interfaces de red disponibles");
                    return;
                }
                
                // Ping a DNS público (Google)
                using var ping = new Ping();
                var reply = await ping.SendPingAsync("8.8.8.8", 5000);
                
                if (reply.Status == IPStatus.Success)
                {
                    HandleConnectionSuccess();
                }
                else
                {
                    HandleConnectionFailure($"Ping falló: {reply.Status}");
                }
            }
            catch (PingException ex)
            {
                HandleConnectionFailure($"Error de ping: {ex.Message}");
            }
            catch (Exception ex)
            {
                HandleConnectionFailure($"Error verificando conexión: {ex.Message}");
            }
        }
        
        private void HandleConnectionSuccess()
        {
            lastSuccessfulPing = DateTime.Now;
            
            if (consecutiveFailures > 0)
            {
                consecutiveFailures = 0;
                
                // Conexión restaurada
                if (!isNetworkAvailable)
                {
                    isNetworkAvailable = true;
                    NetworkStatusChanged?.Invoke(true);
                    ConnectionRestored?.Invoke();
                }
            }
            else if (!isNetworkAvailable)
            {
                isNetworkAvailable = true;
                NetworkStatusChanged?.Invoke(true);
            }
        }
        
        private void HandleConnectionFailure(string reason)
        {
            consecutiveFailures++;
            
            if (consecutiveFailures >= MAX_CONSECUTIVE_FAILURES && isNetworkAvailable)
            {
                // Conexión perdida
                isNetworkAvailable = false;
                NetworkStatusChanged?.Invoke(false);
                ConnectionLost?.Invoke();
            }
        }
        
        /// <summary>
        /// Verifica si hay conexión a un host específico
        /// </summary>
        public async Task<bool> CanReachHost(string host, int timeoutMs = 5000)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(host, timeoutMs);
                return reply.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Obtiene latencia a un host
        /// </summary>
        public async Task<long> GetLatency(string host)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(host, 5000);
                
                if (reply.Status == IPStatus.Success)
                    return reply.RoundtripTime;
                
                return -1;
            }
            catch
            {
                return -1;
            }
        }
        
        /// <summary>
        /// Verifica calidad de conexión
        /// </summary>
        public async Task<ConnectionQuality> GetConnectionQuality()
        {
            var latency = await GetLatency("8.8.8.8");
            
            if (latency < 0)
                return ConnectionQuality.None;
            else if (latency < 50)
                return ConnectionQuality.Excellent;
            else if (latency < 100)
                return ConnectionQuality.Good;
            else if (latency < 200)
                return ConnectionQuality.Fair;
            else
                return ConnectionQuality.Poor;
        }
    }
    
    public enum ConnectionQuality
    {
        None,
        Poor,
        Fair,
        Good,
        Excellent
    }
}
