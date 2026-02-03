using System;
using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace SlskDown.Core.Optimization
{
    /// <summary>
    /// Gestor de object pools para reutilizar objetos costosos
    /// Reduce GC pressure y mejora rendimiento
    /// </summary>
    public static class ObjectPoolManager
    {
        // Pool de StringBuilders
        private static readonly ObjectPool<StringBuilder> stringBuilderPool = 
            new DefaultObjectPoolProvider().CreateStringBuilderPool(1024, 4096);
        
        // Pool de byte arrays
        private static readonly DefaultObjectPool<byte[]> smallBufferPool = 
            new DefaultObjectPool<byte[]>(new BufferPoolPolicy(4096), 100);
        
        private static readonly DefaultObjectPool<byte[]> mediumBufferPool = 
            new DefaultObjectPool<byte[]>(new BufferPoolPolicy(65536), 50);
        
        private static readonly DefaultObjectPool<byte[]> largeBufferPool = 
            new DefaultObjectPool<byte[]>(new BufferPoolPolicy(262144), 20);
        
        /// <summary>
        /// Obtiene StringBuilder del pool
        /// </summary>
        public static StringBuilder GetStringBuilder()
        {
            return stringBuilderPool.Get();
        }
        
        /// <summary>
        /// Devuelve StringBuilder al pool
        /// </summary>
        public static void ReturnStringBuilder(StringBuilder sb)
        {
            stringBuilderPool.Return(sb);
        }
        
        /// <summary>
        /// Usa StringBuilder con using automático
        /// </summary>
        public static StringBuilderLease LeaseStringBuilder()
        {
            return new StringBuilderLease(stringBuilderPool);
        }
        
        /// <summary>
        /// Obtiene buffer pequeño (4KB)
        /// </summary>
        public static byte[] GetSmallBuffer()
        {
            return smallBufferPool.Get();
        }
        
        /// <summary>
        /// Devuelve buffer pequeño
        /// </summary>
        public static void ReturnSmallBuffer(byte[] buffer)
        {
            Array.Clear(buffer, 0, buffer.Length);
            smallBufferPool.Return(buffer);
        }
        
        /// <summary>
        /// Obtiene buffer mediano (64KB)
        /// </summary>
        public static byte[] GetMediumBuffer()
        {
            return mediumBufferPool.Get();
        }
        
        /// <summary>
        /// Devuelve buffer mediano
        /// </summary>
        public static void ReturnMediumBuffer(byte[] buffer)
        {
            Array.Clear(buffer, 0, buffer.Length);
            mediumBufferPool.Return(buffer);
        }
        
        /// <summary>
        /// Obtiene buffer grande (256KB)
        /// </summary>
        public static byte[] GetLargeBuffer()
        {
            return largeBufferPool.Get();
        }
        
        /// <summary>
        /// Devuelve buffer grande
        /// </summary>
        public static void ReturnLargeBuffer(byte[] buffer)
        {
            Array.Clear(buffer, 0, buffer.Length);
            largeBufferPool.Return(buffer);
        }
        
        /// <summary>
        /// Usa buffer con using automático
        /// </summary>
        public static BufferLease LeaseBuffer(BufferSize size)
        {
            return new BufferLease(size);
        }
        
        public enum BufferSize
        {
            Small = 4096,
            Medium = 65536,
            Large = 262144
        }
        
        // Policy para crear buffers
        private class BufferPoolPolicy : IPooledObjectPolicy<byte[]>
        {
            private readonly int size;
            
            public BufferPoolPolicy(int size)
            {
                this.size = size;
            }
            
            public byte[] Create()
            {
                return new byte[size];
            }
            
            public bool Return(byte[] obj)
            {
                return obj.Length == size;
            }
        }
        
        // Lease para StringBuilder con IDisposable
        public class StringBuilderLease : IDisposable
        {
            private readonly ObjectPool<StringBuilder> pool;
            private StringBuilder sb;
            
            public StringBuilder StringBuilder => sb;
            
            public StringBuilderLease(ObjectPool<StringBuilder> pool)
            {
                this.pool = pool;
                sb = pool.Get();
            }
            
            public void Dispose()
            {
                if (sb != null)
                {
                    pool.Return(sb);
                    sb = null;
                }
            }
        }
        
        // Lease para buffers con IDisposable
        public class BufferLease : IDisposable
        {
            private byte[] buffer;
            private readonly BufferSize size;
            
            public byte[] Buffer => buffer;
            public int Length => buffer?.Length ?? 0;
            
            public BufferLease(BufferSize size)
            {
                this.size = size;
                buffer = size switch
                {
                    BufferSize.Small => GetSmallBuffer(),
                    BufferSize.Medium => GetMediumBuffer(),
                    BufferSize.Large => GetLargeBuffer(),
                    _ => throw new ArgumentException("Invalid buffer size")
                };
            }
            
            public void Dispose()
            {
                if (buffer != null)
                {
                    switch (size)
                    {
                        case BufferSize.Small:
                            ReturnSmallBuffer(buffer);
                            break;
                        case BufferSize.Medium:
                            ReturnMediumBuffer(buffer);
                            break;
                        case BufferSize.Large:
                            ReturnLargeBuffer(buffer);
                            break;
                    }
                    buffer = null;
                }
            }
        }
    }
}
