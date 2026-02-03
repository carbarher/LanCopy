using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace SlskDown
{
    /// <summary>
    /// Helper para configurar TCP Keep-Alive en sockets
    /// Basado en mejores prácticas para Soulseek
    /// </summary>
    public static class TcpKeepAliveHelper
    {
        /// <summary>
        /// Configura TCP Keep-Alive con valores óptimos para Soulseek
        /// </summary>
        /// <param name="socket">Socket a configurar</param>
        /// <param name="keepAliveTime">Tiempo antes del primer keep-alive (default: 60s)</param>
        /// <param name="keepAliveInterval">Intervalo entre probes (default: 10s)</param>
        public static void ConfigureKeepAlive(Socket socket, int keepAliveTime = 60000, int keepAliveInterval = 10000)
        {
            if (socket == null)
                throw new ArgumentNullException(nameof(socket));
            
            try
            {
                // Habilitar keep-alive
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                
                // Configurar intervalos personalizados (Windows)
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    ConfigureWindowsKeepAlive(socket, keepAliveTime, keepAliveInterval);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    ConfigureLinuxKeepAlive(socket, keepAliveTime / 1000, keepAliveInterval / 1000);
                }
                // macOS usa valores del sistema por defecto
            }
            catch (Exception ex)
            {
                // Log pero no fallar - keep-alive es opcional
                Console.WriteLine($"⚠️ No se pudo configurar TCP keep-alive: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Configura keep-alive en Windows usando IOControl
        /// </summary>
        private static void ConfigureWindowsKeepAlive(Socket socket, int keepAliveTime, int keepAliveInterval)
        {
            // Estructura: [onoff (4 bytes), keepalivetime (4 bytes), keepaliveinterval (4 bytes)]
            byte[] keepAliveValues = new byte[12];
            
            // onoff = 1 (habilitar)
            Buffer.BlockCopy(BitConverter.GetBytes(1u), 0, keepAliveValues, 0, 4);
            
            // keepalivetime en milisegundos
            Buffer.BlockCopy(BitConverter.GetBytes((uint)keepAliveTime), 0, keepAliveValues, 4, 4);
            
            // keepaliveinterval en milisegundos
            Buffer.BlockCopy(BitConverter.GetBytes((uint)keepAliveInterval), 0, keepAliveValues, 8, 4);
            
            socket.IOControl(IOControlCode.KeepAliveValues, keepAliveValues, null);
        }
        
        /// <summary>
        /// Configura keep-alive en Linux usando SetSocketOption
        /// </summary>
        private static void ConfigureLinuxKeepAlive(Socket socket, int keepAliveTimeSec, int keepAliveIntervalSec)
        {
            const int TCP_KEEPIDLE = 4;   // Tiempo antes del primer probe
            const int TCP_KEEPINTVL = 5;  // Intervalo entre probes
            const int TCP_KEEPCNT = 6;    // Número de probes
            
            try
            {
                socket.SetSocketOption(SocketOptionLevel.Tcp, (SocketOptionName)TCP_KEEPIDLE, keepAliveTimeSec);
                socket.SetSocketOption(SocketOptionLevel.Tcp, (SocketOptionName)TCP_KEEPINTVL, keepAliveIntervalSec);
                socket.SetSocketOption(SocketOptionLevel.Tcp, (SocketOptionName)TCP_KEEPCNT, 10);
            }
            catch
            {
                // Algunos sistemas Linux pueden no soportar estas opciones
            }
        }
        
        /// <summary>
        /// Obtiene la configuración recomendada según el escenario
        /// </summary>
        public static (int keepAliveTime, int keepAliveInterval) GetRecommendedSettings(KeepAliveScenario scenario)
        {
            return scenario switch
            {
                KeepAliveScenario.Normal => (60000, 10000),      // 60s, 10s
                KeepAliveScenario.Aggressive => (30000, 5000),   // 30s, 5s (stress test)
                KeepAliveScenario.Conservative => (120000, 30000), // 120s, 30s (evitar bans)
                _ => (60000, 10000)
            };
        }
    }
    
    public enum KeepAliveScenario
    {
        Normal,         // Uso 24/7 normal
        Aggressive,     // Stress test, detección rápida
        Conservative    // Evitar bans, menos tráfico
    }
}
