using System;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.ObjectPool;
using SlskDown.Models;

namespace SlskDown
{
    /// <summary>
    /// Object pools para reducir presión en GC
    /// </summary>
    public static class ObjectPools
    {
        /// <summary>
        /// Pool de StringBuilder para construcción de strings
        /// </summary>
        public static readonly ObjectPool<StringBuilder> StringBuilderPool = 
            new DefaultObjectPoolProvider().CreateStringBuilderPool(
                initialCapacity: 1024,
                maximumRetainedCapacity: 4096
            );

        /// <summary>
        /// Pool de buffers de bytes (4KB)
        /// </summary>
        public static readonly ObjectPool<byte[]> SmallBufferPool = 
            new DefaultObjectPool<byte[]>(new ByteArrayPooledObjectPolicy(4096), 128);

        /// <summary>
        /// Pool de buffers de bytes (64KB)
        /// </summary>
        public static readonly ObjectPool<byte[]> LargeBufferPool = 
            new DefaultObjectPool<byte[]>(new ByteArrayPooledObjectPolicy(65536), 32);

        /// <summary>
        /// Pool de listas de strings
        /// </summary>
        public static readonly ObjectPool<System.Collections.Generic.List<string>> StringListPool = 
            new DefaultObjectPool<System.Collections.Generic.List<string>>(
                new StringListPooledObjectPolicy(), 64
            );

        /// <summary>
        /// Pool de diccionarios string-string
        /// </summary>
        public static readonly ObjectPool<System.Collections.Generic.Dictionary<string, string>> StringDictionaryPool = 
            new DefaultObjectPool<System.Collections.Generic.Dictionary<string, string>>(
                new StringDictionaryPooledObjectPolicy(), 32
            );

        /// <summary>
        /// Pool de AutoSearchFileResult para reducir allocaciones
        /// </summary>
        public static readonly ObjectPool<AutoSearchFileResult> FileResultPool = 
            new DefaultObjectPool<AutoSearchFileResult>(
                new FileResultPooledObjectPolicy(), 10000
            );

        /// <summary>
        /// Obtiene un StringBuilder del pool
        /// </summary>
        public static StringBuilder GetStringBuilder()
        {
            return StringBuilderPool.Get();
        }

        /// <summary>
        /// Devuelve un StringBuilder al pool
        /// </summary>
        public static void ReturnStringBuilder(StringBuilder sb)
        {
            StringBuilderPool.Return(sb);
        }

        /// <summary>
        /// Usa un StringBuilder del pool y lo devuelve automáticamente
        /// </summary>
        public static string UseStringBuilder(Action<StringBuilder> action)
        {
            var sb = StringBuilderPool.Get();
            try
            {
                action(sb);
                return sb.ToString();
            }
            finally
            {
                StringBuilderPool.Return(sb);
            }
        }

        /// <summary>
        /// Obtiene un buffer pequeño (4KB) del pool
        /// </summary>
        public static byte[] GetSmallBuffer()
        {
            return SmallBufferPool.Get();
        }

        /// <summary>
        /// Devuelve un buffer pequeño al pool
        /// </summary>
        public static void ReturnSmallBuffer(byte[] buffer)
        {
            SmallBufferPool.Return(buffer);
        }

        /// <summary>
        /// Obtiene un buffer grande (64KB) del pool
        /// </summary>
        public static byte[] GetLargeBuffer()
        {
            return LargeBufferPool.Get();
        }

        /// <summary>
        /// Devuelve un buffer grande al pool
        /// </summary>
        public static void ReturnLargeBuffer(byte[] buffer)
        {
            LargeBufferPool.Return(buffer);
        }

        /// <summary>
        /// Obtiene una lista de strings del pool
        /// </summary>
        public static System.Collections.Generic.List<string> GetStringList()
        {
            return StringListPool.Get();
        }

        /// <summary>
        /// Devuelve una lista de strings al pool
        /// </summary>
        public static void ReturnStringList(System.Collections.Generic.List<string> list)
        {
            StringListPool.Return(list);
        }

        /// <summary>
        /// Obtiene un diccionario del pool
        /// </summary>
        public static System.Collections.Generic.Dictionary<string, string> GetStringDictionary()
        {
            return StringDictionaryPool.Get();
        }

        /// <summary>
        /// Devuelve un diccionario al pool
        /// </summary>
        public static void ReturnStringDictionary(System.Collections.Generic.Dictionary<string, string> dict)
        {
            StringDictionaryPool.Return(dict);
        }

        /// <summary>
        /// Obtiene un AutoSearchFileResult del pool
        /// </summary>
        public static AutoSearchFileResult GetFileResult()
        {
            return FileResultPool.Get();
        }

        /// <summary>
        /// Devuelve un AutoSearchFileResult al pool
        /// </summary>
        public static void ReturnFileResult(AutoSearchFileResult file)
        {
            FileResultPool.Return(file);
        }
    }

    /// <summary>
    /// Policy para pool de byte arrays
    /// </summary>
    internal class ByteArrayPooledObjectPolicy : IPooledObjectPolicy<byte[]>
    {
        private readonly int size;

        public ByteArrayPooledObjectPolicy(int size)
        {
            this.size = size;
        }

        public byte[] Create()
        {
            return new byte[size];
        }

        public bool Return(byte[] obj)
        {
            if (obj == null || obj.Length != size)
                return false;

            Array.Clear(obj, 0, obj.Length);
            return true;
        }
    }

    /// <summary>
    /// Policy para pool de listas de strings
    /// </summary>
    internal class StringListPooledObjectPolicy : IPooledObjectPolicy<System.Collections.Generic.List<string>>
    {
        public System.Collections.Generic.List<string> Create()
        {
            return new System.Collections.Generic.List<string>(32);
        }

        public bool Return(System.Collections.Generic.List<string> obj)
        {
            if (obj == null)
                return false;

            obj.Clear();
            return true;
        }
    }

    /// <summary>
    /// Policy para pool de diccionarios
    /// </summary>
    internal class StringDictionaryPooledObjectPolicy : IPooledObjectPolicy<System.Collections.Generic.Dictionary<string, string>>
    {
        public System.Collections.Generic.Dictionary<string, string> Create()
        {
            return new System.Collections.Generic.Dictionary<string, string>(32, StringComparer.OrdinalIgnoreCase);
        }

        public bool Return(System.Collections.Generic.Dictionary<string, string> obj)
        {
            if (obj == null)
                return false;

            obj.Clear();
            return true;
        }
    }

    /// <summary>
    /// Policy para pool de AutoSearchFileResult
    /// </summary>
    internal class FileResultPooledObjectPolicy : IPooledObjectPolicy<AutoSearchFileResult>
    {
        public AutoSearchFileResult Create()
        {
            return new AutoSearchFileResult();
        }

        public bool Return(AutoSearchFileResult obj)
        {
            if (obj == null)
                return false;

            obj.FileName = null;
            obj.Author = null;
            obj.Username = null;
            obj.SizeBytes = 0;
            obj.IsSpanish = false;
            return true;
        }
    }

    /// <summary>
    /// Extensiones para usar pools con using
    /// </summary>
    public static class ObjectPoolExtensions
    {
        /// <summary>
        /// Usa un StringBuilder del pool con using
        /// </summary>
        public static PooledStringBuilder RentStringBuilder(this ObjectPool<StringBuilder> pool)
        {
            return new PooledStringBuilder(pool);
        }

        /// <summary>
        /// Usa un buffer del pool con using
        /// </summary>
        public static PooledBuffer RentBuffer(this ObjectPool<byte[]> pool)
        {
            return new PooledBuffer(pool);
        }

        /// <summary>
        /// Usa una lista del pool con using
        /// </summary>
        public static PooledList RentList(this ObjectPool<System.Collections.Generic.List<string>> pool)
        {
            return new PooledList(pool);
        }
    }

    /// <summary>
    /// Wrapper para StringBuilder del pool con IDisposable
    /// </summary>
    public struct PooledStringBuilder : IDisposable
    {
        private readonly ObjectPool<StringBuilder> pool;
        public StringBuilder Value { get; }

        public PooledStringBuilder(ObjectPool<StringBuilder> pool)
        {
            this.pool = pool;
            Value = pool.Get();
        }

        public void Dispose()
        {
            pool.Return(Value);
        }

        public override string ToString() => Value.ToString();
    }

    /// <summary>
    /// Wrapper para buffer del pool con IDisposable
    /// </summary>
    public struct PooledBuffer : IDisposable
    {
        private readonly ObjectPool<byte[]> pool;
        public byte[] Value { get; }

        public PooledBuffer(ObjectPool<byte[]> pool)
        {
            this.pool = pool;
            Value = pool.Get();
        }

        public void Dispose()
        {
            pool.Return(Value);
        }
    }

    /// <summary>
    /// Wrapper para lista del pool con IDisposable
    /// </summary>
    public struct PooledList : IDisposable
    {
        private readonly ObjectPool<System.Collections.Generic.List<string>> pool;
        public System.Collections.Generic.List<string> Value { get; }

        public PooledList(ObjectPool<System.Collections.Generic.List<string>> pool)
        {
            this.pool = pool;
            Value = pool.Get();
        }

        public void Dispose()
        {
            pool.Return(Value);
        }
    }
}
