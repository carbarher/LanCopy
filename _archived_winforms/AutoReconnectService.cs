using System;
using System.Threading;
using System.Threading.Tasks;
using Soulseek;

namespace SlskDown
{
    /// <summary>
    /// Servicio de reconexión automática sin perder progreso
    /// Detecta desconexiones y reconecta automáticamente
    /// </summary>
    public class AutoReconnectService
    {
        private readonly Func<Task<bool>> reconnectCallback;
        private readonly Action<string> logCallback;
        private CancellationTokenSource monitorCts;
        private bool isMonitoring;
        private ISoulseekClient client;

        public event Action OnReconnecting;
        public event Action OnReconnected;
        public event Action<Exception> OnReconnectFailed;

        public AutoReconnectService(ISoulseekClient soulseekClient, Func<Task<bool>> reconnect, Action<string> log)
        {
            client = soulseekClient;
            reconnectCallback = reconnect;
            logCallback = log;
        }

        /// <summary>
        /// Inicia el monitoreo de conexión
        /// </summary>
        public void Start()
        {
            if (isMonitoring) return;

            isMonitoring = true;
            monitorCts = new CancellationTokenSource();

            Task.Run(() => MonitorConnectionAsync(monitorCts.Token));
            logCallback?.Invoke("🔄 Servicio de reconexión automática iniciado");
        }

        /// <summary>
        /// Detiene el monitoreo de conexión
        /// </summary>
        public void Stop()
        {
            isMonitoring = false;
            monitorCts?.Cancel();
            logCallback?.Invoke("⏹️ Servicio de reconexión automática detenido");
        }

        /// <summary>
        /// Monitorea la conexión y reconecta automáticamente
        /// </summary>
        private async Task MonitorConnectionAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, ct); // Verificar cada 5 segundos

                    if (client?.State != SoulseekClientStates.Connected &&
                        client?.State != SoulseekClientStates.LoggedIn)
                    {
                        logCallback?.Invoke("⚠️ Desconexión detectada - Intentando reconectar...");
                        OnReconnecting?.Invoke();

                        // Intentar reconectar con backoff exponencial
                        bool reconnected = await TryReconnectWithBackoffAsync(ct);

                        if (reconnected)
                        {
                            logCallback?.Invoke("✅ Reconexión exitosa");
                            OnReconnected?.Invoke();
                        }
                        else
                        {
                            logCallback?.Invoke("❌ Reconexión falló después de múltiples intentos");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logCallback?.Invoke($"⚠️ Error en monitoreo de conexión: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Intenta reconectar con backoff exponencial
        /// </summary>
        private async Task<bool> TryReconnectWithBackoffAsync(CancellationToken ct)
        {
            int[] delays = { 2000, 5000, 10000, 20000, 30000 }; // Backoff exponencial

            for (int attempt = 0; attempt < delays.Length; attempt++)
            {
                try
                {
                    if (ct.IsCancellationRequested)
                        return false;

                    logCallback?.Invoke($"🔄 Intento de reconexión {attempt + 1}/{delays.Length}...");

                    bool success = await reconnectCallback();
                    if (success)
                        return true;

                    // Esperar antes del siguiente intento
                    if (attempt < delays.Length - 1)
                    {
                        await Task.Delay(delays[attempt], ct);
                    }
                }
                catch (Exception ex)
                {
                    logCallback?.Invoke($"⚠️ Error en intento {attempt + 1}: {ex.Message}");
                    OnReconnectFailed?.Invoke(ex);
                }
            }

            return false;
        }
    }
}
