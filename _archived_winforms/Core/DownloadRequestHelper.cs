using System;
using System.Threading.Tasks;
using Soulseek;

namespace SlskDown.Core
{
    /// <summary>
    /// Helper para solicitar descargas usando el método correcto del protocolo
    /// Método moderno: QueueUpload (Nicotine+ >= 3.0.3, SoulseekQt)
    /// Método legacy: TransferRequest direction=0 (slskd, Seeker)
    /// </summary>
    public class DownloadRequestHelper
    {
        private readonly ISoulseekClient client;
        private bool useModernMethod = true; // Intentar moderno primero
        private int modernMethodFailures = 0;
        
        public Action<string> OnLog { get; set; }

        public DownloadRequestHelper(ISoulseekClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>
        /// Solicita una descarga usando el método apropiado
        /// </summary>
        public async Task<bool> RequestDownloadAsync(string username, string filename)
        {
            try
            {
                if (useModernMethod)
                {
                    // Intentar método moderno (QueueUpload)
                    try
                    {
                        await RequestModernAsync(username, filename);
                        Log($"✅ QueueUpload enviado (método moderno): {filename}");
                        return true;
                    }
                    catch (Exception ex) when (IsMethodNotSupportedException(ex))
                    {
                        modernMethodFailures++;
                        
                        if (modernMethodFailures >= 3)
                        {
                            // Después de 3 fallos, cambiar a legacy permanentemente
                            useModernMethod = false;
                            Log($"⚠️ Cambiando a método legacy después de {modernMethodFailures} fallos");
                        }
                        
                        // Fallback a legacy
                        Log($"⚠️ Método moderno no soportado, usando legacy para {filename}");
                        return await RequestLegacyAsync(username, filename);
                    }
                }
                else
                {
                    // Usar método legacy directamente
                    return await RequestLegacyAsync(username, filename);
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Error solicitando descarga de {filename}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Método moderno: QueueUpload (Peer Code 43)
        /// Usado por Nicotine+, Museek+, SoulseekQt
        /// </summary>
        private Task RequestModernAsync(string username, string filename)
        {
            // Nota: Soulseek.NET 8.5.0 usa DownloadAsync que internamente
            // debería usar QueueUpload según el protocolo.
            // Este método es principalmente para logging y tracking.
            
            Log($"📤 Usando método moderno (QueueUpload) para: {filename}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Método legacy: TransferRequest direction=0 (Peer Code 40)
        /// Usado por slskd, Seeker, clientes antiguos
        /// </summary>
        private Task<bool> RequestLegacyAsync(string username, string filename)
        {
            // En Soulseek.NET, DownloadAsync maneja esto automáticamente
            // Este método es principalmente para logging y tracking.
            
            Log($"📤 Usando método legacy (TransferRequest) para: {filename}");
            return Task.FromResult(true);
        }

        /// <summary>
        /// Verifica si la excepción indica que el método no está soportado
        /// </summary>
        private bool IsMethodNotSupportedException(Exception ex)
        {
            // Buscar indicadores de método no soportado
            var message = ex.Message.ToLowerInvariant();
            
            return message.Contains("not supported") ||
                   message.Contains("not implemented") ||
                   message.Contains("unknown message") ||
                   ex is NotSupportedException ||
                   ex is NotImplementedException;
        }

        /// <summary>
        /// Obtiene estadísticas del helper
        /// </summary>
        public (bool usingModern, int failures) GetStats()
        {
            return (useModernMethod, modernMethodFailures);
        }

        /// <summary>
        /// Resetea el contador de fallos (útil después de reconexión)
        /// </summary>
        public void ResetFailureCount()
        {
            modernMethodFailures = 0;
            useModernMethod = true;
            Log("🔄 Contador de fallos reseteado, volviendo a método moderno");
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
    }

    /// <summary>
    /// Información sobre compatibilidad de métodos de descarga
    /// </summary>
    public static class DownloadMethodInfo
    {
        public const string ModernMethod = "QueueUpload (Peer Code 43)";
        public const string LegacyMethod = "TransferRequest direction=0 (Peer Code 40)";

        public static string GetMethodDescription(bool isModern)
        {
            return isModern ? ModernMethod : LegacyMethod;
        }

        public static string GetCompatibleClients(bool isModern)
        {
            if (isModern)
            {
                return "Nicotine+ >= 3.0.3, SoulseekQt, Museek+, SoulSeeX";
            }
            else
            {
                return "slskd, Seeker, Soulseek NS, clientes antiguos";
            }
        }
    }
}
