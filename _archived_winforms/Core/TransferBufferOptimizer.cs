using System;
using System.Net.Sockets;
using SlskDown.Core.Optimization;

namespace SlskDown.Core
{
    /// <summary>
    /// Optimizador de buffers para transferencias (inspirado en slskd)
    /// </summary>
    public class TransferBufferOptimizer
    {
        /// <summary>
        /// Alquila buffer del pool
        /// </summary>
        public static byte[] RentBuffer(int size)
        {
            return MemoryPool.RentBytes(size);
        }
        
        /// <summary>
        /// Devuelve buffer al pool
        /// </summary>
        public static void ReturnBuffer(byte[] buffer, bool clear = false)
        {
            MemoryPool.ReturnBytes(buffer, clear);
        }
        
        // Tamaños de buffer optimizados basados en slskd
        public const int DEFAULT_READ_BUFFER = 16384;      // 16 KB
        public const int DEFAULT_WRITE_BUFFER = 16384;     // 16 KB
        public const int DEFAULT_TRANSFER_BUFFER = 262144; // 256 KB
        public const int DEFAULT_WRITE_QUEUE = 250;
        
        // Timeouts optimizados
        public const int DEFAULT_CONNECT_TIMEOUT = 10000;    // 10 segundos
        public const int DEFAULT_INACTIVITY_TIMEOUT = 15000; // 15 segundos
        public const int DEFAULT_TRANSFER_TIMEOUT = 30000;   // 30 segundos (mínimo)
        
        /// <summary>
        /// Configura socket para transferencias óptimas
        /// </summary>
        public static void ConfigureSocket(Socket socket, bool isTransfer = false)
        {
            if (socket == null) return;
            
            try
            {
                // Buffer sizes
                if (isTransfer)
                {
                    socket.SendBufferSize = DEFAULT_TRANSFER_BUFFER;
                    socket.ReceiveBufferSize = DEFAULT_TRANSFER_BUFFER;
                }
                else
                {
                    socket.SendBufferSize = DEFAULT_WRITE_BUFFER;
                    socket.ReceiveBufferSize = DEFAULT_READ_BUFFER;
                }
                
                // TCP optimizations
                socket.NoDelay = true; // Disable Nagle's algorithm
                
                // Timeouts
                socket.SendTimeout = isTransfer ? DEFAULT_TRANSFER_TIMEOUT : DEFAULT_CONNECT_TIMEOUT;
                socket.ReceiveTimeout = isTransfer ? DEFAULT_TRANSFER_TIMEOUT : DEFAULT_INACTIVITY_TIMEOUT;
                
                // Keep-alive (importante para transferencias largas)
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                
                // Linger para cerrar conexiones limpiamente
                socket.LingerState = new LingerOption(true, 2);
            }
            catch
            {
                // Ignorar errores de configuración
            }
        }
        
        /// <summary>
        /// Calcula tamaño de buffer óptimo basado en velocidad de conexión
        /// </summary>
        public static int CalculateOptimalBufferSize(double speedMBps)
        {
            // Para velocidades altas, usar buffers más grandes
            if (speedMBps > 10)
                return 524288; // 512 KB
            else if (speedMBps > 5)
                return DEFAULT_TRANSFER_BUFFER; // 256 KB
            else if (speedMBps > 1)
                return 131072; // 128 KB
            else
                return 65536; // 64 KB
        }
        
        /// <summary>
        /// Configuración de buffer adaptativa
        /// </summary>
        public class AdaptiveBufferConfig
        {
            public int ReadBuffer { get; set; } = DEFAULT_READ_BUFFER;
            public int WriteBuffer { get; set; } = DEFAULT_WRITE_BUFFER;
            public int TransferBuffer { get; set; } = DEFAULT_TRANSFER_BUFFER;
            public int WriteQueueLimit { get; set; } = DEFAULT_WRITE_QUEUE;
            
            /// <summary>
            /// Ajusta buffers basado en rendimiento
            /// </summary>
            public void AdjustForPerformance(double avgSpeedMBps, long memoryAvailableMB)
            {
                // Si hay poca memoria, reducir buffers
                if (memoryAvailableMB < 512)
                {
                    TransferBuffer = 65536; // 64 KB
                    WriteQueueLimit = 50;
                }
                // Si hay mucha memoria y velocidad alta, aumentar
                else if (memoryAvailableMB > 2048 && avgSpeedMBps > 5)
                {
                    TransferBuffer = 524288; // 512 KB
                    WriteQueueLimit = 500;
                }
                else
                {
                    TransferBuffer = CalculateOptimalBufferSize(avgSpeedMBps);
                }
            }
        }
        
        /// <summary>
        /// Estadísticas de rendimiento de buffers
        /// </summary>
        public class BufferStats
        {
            public long TotalBytesRead { get; set; }
            public long TotalBytesWritten { get; set; }
            public int ReadOperations { get; set; }
            public int WriteOperations { get; set; }
            public DateTime StartTime { get; set; } = DateTime.Now;
            
            public double AverageReadSize => ReadOperations > 0 ? TotalBytesRead / (double)ReadOperations : 0;
            public double AverageWriteSize => WriteOperations > 0 ? TotalBytesWritten / (double)WriteOperations : 0;
            public double ThroughputMBps
            {
                get
                {
                    var elapsed = (DateTime.Now - StartTime).TotalSeconds;
                    if (elapsed <= 0) return 0;
                    return (TotalBytesRead + TotalBytesWritten) / elapsed / (1024 * 1024);
                }
            }
        }
    }
}
