using System;
using System.Buffers;

namespace SlskDown.Core.Optimization
{
    /// <summary>
    /// Pool de memoria para reutilizar buffers y reducir GC
    /// </summary>
    public static class MemoryPool
    {
        private static readonly ArrayPool<byte> bytePool = ArrayPool<byte>.Shared;
        
        /// <summary>
        /// Alquila buffer de bytes
        /// </summary>
        public static byte[] RentBytes(int minimumLength)
        {
            return bytePool.Rent(minimumLength);
        }
        
        /// <summary>
        /// Devuelve buffer de bytes
        /// </summary>
        public static void ReturnBytes(byte[] buffer, bool clearArray = false)
        {
            if (buffer != null)
            {
                bytePool.Return(buffer, clearArray);
            }
        }
        
        /// <summary>
        /// Ejecuta acción con buffer temporal
        /// </summary>
        public static void UseBuffer(int size, Action<byte[]> action, bool clearOnReturn = false)
        {
            var buffer = RentBytes(size);
            try
            {
                action(buffer);
            }
            finally
            {
                ReturnBytes(buffer, clearOnReturn);
            }
        }
        
        /// <summary>
        /// Ejecuta función con buffer temporal
        /// </summary>
        public static T UseBuffer<T>(int size, Func<byte[], T> func, bool clearOnReturn = false)
        {
            var buffer = RentBytes(size);
            try
            {
                return func(buffer);
            }
            finally
            {
                ReturnBytes(buffer, clearOnReturn);
            }
        }
        
        /// <summary>
        /// Wrapper para gestión automática de buffer
        /// </summary>
        public class BufferLease : IDisposable
        {
            private byte[] buffer;
            private readonly bool clearOnReturn;
            
            public byte[] Buffer => buffer;
            public int Length => buffer?.Length ?? 0;
            
            public BufferLease(int minimumLength, bool clearOnReturn = false)
            {
                buffer = RentBytes(minimumLength);
                this.clearOnReturn = clearOnReturn;
            }
            
            public void Dispose()
            {
                if (buffer != null)
                {
                    ReturnBytes(buffer, clearOnReturn);
                    buffer = null;
                }
            }
        }
        
        /// <summary>
        /// Crea lease de buffer con using
        /// </summary>
        public static BufferLease Lease(int minimumLength, bool clearOnReturn = false)
        {
            return new BufferLease(minimumLength, clearOnReturn);
        }
    }
}
