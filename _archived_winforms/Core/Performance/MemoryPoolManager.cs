using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace SlskDown.Core.Performance
{
    /// <summary>
    /// Memory Pool ultra-optimizado para eliminar GC overhead
    /// </summary>
    public class MemoryPoolManager : IDisposable
    {
        private readonly Dictionary<Type, ConcurrentQueue<object>> _pools;
        private readonly Dictionary<Type, int> _maxPoolSizes;
        private readonly Timer _cleanupTimer;
        private volatile bool _disposed = false;
        private static readonly Lazy<MemoryPoolManager> _instance = new Lazy<MemoryPoolManager>(() => new MemoryPoolManager());

        public static MemoryPoolManager Instance => _instance.Value;

        private MemoryPoolManager()
        {
            _pools = new Dictionary<Type, ConcurrentQueue<object>>();
            _maxPoolSizes = new Dictionary<Type, int>();
            
            // Configurar tamaños máximos por tipo
            ConfigurePoolSizes();
            
            // Iniciar timer de limpieza cada 5 minutos
            _cleanupTimer = new Timer(CleanupPools, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Obtiene un objeto del pool o crea uno nuevo
        /// </summary>
        public T Get<T>() where T : class, new()
        {
            if (_disposed) return new T();

            var type = typeof(T);
            
            if (!_pools.TryGetValue(type, out var pool))
            {
                pool = new ConcurrentQueue<object>();
                _pools[type] = pool;
            }

            if (pool.TryDequeue(out var item))
            {
                return (T)item;
            }

            return new T();
        }

        /// <summary>
        /// Devuelve un objeto al pool para reutilización
        /// </summary>
        public void Return<T>(T item) where T : class
        {
            if (_disposed || item == null) return;

            var type = typeof(T);
            
            if (!_pools.TryGetValue(type, out var pool))
            {
                pool = new ConcurrentQueue<object>();
                _pools[type] = pool;
            }

            // Resetear objeto si es necesario
            if (item is IPoolable poolable)
            {
                poolable.Reset();
            }

            // Verificar tamaño máximo del pool
            if (_maxPoolSizes.TryGetValue(type, out var maxSize) && pool.Count < maxSize)
            {
                pool.Enqueue(item);
            }
        }

        /// <summary>
        /// Obtiene un array del pool
        /// </summary>
        public T[] GetArray<T>(int size)
        {
            if (_disposed) return new T[size];

            var key = $"Array_{typeof(T).Name}_{size}";
            var type = typeof(T[]);
            
            if (!_pools.TryGetValue(type, out var pool))
            {
                pool = new ConcurrentQueue<object>();
                _pools[type] = pool;
            }

            if (pool.TryDequeue(out var item))
            {
                var array = (T[])item;
                Array.Clear(array, 0, array.Length); // Limpiar array
                return array;
            }

            return new T[size];
        }

        /// <summary>
        /// Devuelve un array al pool
        /// </summary>
        public void ReturnArray<T>(T[] array)
        {
            if (_disposed || array == null) return;

            var type = typeof(T[]);
            
            if (!_pools.TryGetValue(type, out var pool))
            {
                pool = new ConcurrentQueue<object>();
                _pools[type] = pool;
            }

            // Limitar pools de arrays grandes
            if (array.Length <= 1000 && pool.Count < 10)
            {
                pool.Enqueue(array);
            }
        }

        /// <summary>
        /// Usa stackalloc para arrays pequeños (performance crítica)
        /// </summary>
        public unsafe void UseStackAlloc<T>(int size, Action<IntPtr> action) where T : unmanaged
        {
            if (size <= 512) // Solo para arrays pequeños
            {
                var ptr = stackalloc T[size];
                action(new IntPtr(ptr));
            }
            else
            {
                // Usar pool para arrays grandes
                var array = GetArray<T>(size);
                try
                {
                    var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
                    try
                    {
                        action(handle.AddrOfPinnedObject());
                    }
                    finally
                    {
                        handle.Free();
                    }
                }
                finally
                {
                    ReturnArray(array);
                }
            }
        }

        /// <summary>
        /// Obtiene buffer para operaciones de red
        /// </summary>
        public byte[] GetNetworkBuffer(int size = 8192)
        {
            return GetArray<byte>(size);
        }

        /// <summary>
        /// Devuelve buffer de red al pool
        /// </summary>
        public void ReturnNetworkBuffer(byte[] buffer)
        {
            ReturnArray(buffer);
        }

        /// <summary>
        /// Obtiene StringBuilder del pool
        /// </summary>
        public System.Text.StringBuilder GetStringBuilder(int capacity = 256)
        {
            var sb = Get<System.Text.StringBuilder>();
            if (sb.Capacity < capacity)
            {
                sb.Capacity = capacity;
            }
            sb.Clear();
            return sb;
        }

        /// <summary>
        /// Devuelve StringBuilder al pool
        /// </summary>
        public void ReturnStringBuilder(System.Text.StringBuilder sb)
        {
            Return(sb);
        }

        /// <summary>
        /// Obtiene List del pool
        /// </summary>
        public List<T> GetList<T>(int capacity = 0)
        {
            var list = Get<List<T>>();
            list.Clear();
            if (capacity > 0 && list.Capacity < capacity)
            {
                list.Capacity = capacity;
            }
            return list;
        }

        /// <summary>
        /// Devuelve List al pool
        /// </summary>
        public void ReturnList<T>(List<T> list)
        {
            Return(list);
        }

        /// <summary>
        /// Obtiene Dictionary del pool
        /// </summary>
        public Dictionary<TKey, TValue> GetDictionary<TKey, TValue>(int capacity = 0)
        {
            var dict = Get<Dictionary<TKey, TValue>>();
            dict.Clear();
            if (capacity > 0)
            {
                // No hay forma directa de cambiar capacidad de Dictionary existente
                // Se creará uno nuevo si se necesita capacidad específica
                if (dict.Count != 0 || capacity > 0)
                {
                    return new Dictionary<TKey, TValue>(capacity);
                }
            }
            return dict;
        }

        /// <summary>
        /// Devuelve Dictionary al pool
        /// </summary>
        public void ReturnDictionary<TKey, TValue>(Dictionary<TKey, TValue> dict)
        {
            Return(dict);
        }

        /// <summary>
        /// Estadísticas del pool
        /// </summary>
        public PoolStatistics GetStatistics()
        {
            var stats = new PoolStatistics();
            
            foreach (var kvp in _pools)
            {
                stats.PoolSizes[kvp.Key.Name] = kvp.Value.Count;
                stats.TotalObjects += kvp.Value.Count;
            }

            return stats;
        }

        /// <summary>
        /// Limpia pools que exceden tamaño máximo
        /// </summary>
        private void CleanupPools(object state)
        {
            if (_disposed) return;

            foreach (var kvp in _pools)
            {
                var pool = kvp.Value;
                var type = kvp.Key;
                
                if (_maxPoolSizes.TryGetValue(type, out var maxSize))
                {
                    while (pool.Count > maxSize && pool.TryDequeue(out _))
                    {
                        // Descartar objetos excedentes
                    }
                }
            }

            // Forzar GC ligero
            GC.Collect(0, GCCollectionMode.Optimized);
        }

        /// <summary>
        /// Configura tamaños máximos de pool por tipo
        /// </summary>
        private void ConfigurePoolSizes()
        {
            _maxPoolSizes[typeof(byte[])] = 100;
            _maxPoolSizes[typeof(char[])] = 50;
            _maxPoolSizes[typeof(int[])] = 50;
            _maxPoolSizes[typeof(string[])] = 25;
            _maxPoolSizes[typeof(System.Text.StringBuilder)] = 20;
            _maxPoolSizes[typeof(List<string>)] = 30;
            _maxPoolSizes[typeof(List<int>)] = 30;
            _maxPoolSizes[typeof(Dictionary<string, string>)] = 20;
            _maxPoolSizes[typeof(Dictionary<string, int>)] = 20;
            _maxPoolSizes[typeof(System.Collections.Generic.HashSet<string>)] = 15;
        }

        /// <summary>
        /// Pre-calienta pools con objetos comunes
        /// </summary>
        public void Warmup()
        {
            // Pre-crear objetos comunes
            for (int i = 0; i < 10; i++)
            {
                Return(new System.Text.StringBuilder());
                Return(new List<string>());
                Return(new List<int>());
                Return(new Dictionary<string, string>());
                ReturnArray(new byte[8192]);
                ReturnArray(new byte[4096]);
                ReturnArray(new byte[1024]);
            }
        }

        /// <summary>
        /// Libera todos los recursos
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _cleanupTimer?.Dispose();
            
            // Limpiar todos los pools
            foreach (var pool in _pools.Values)
            {
                while (pool.TryDequeue(out _))
                {
                    // Descartar todos los objetos
                }
            }
            
            _pools.Clear();
            _maxPoolSizes.Clear();
        }
    }

    /// <summary>
    /// Interfaz para objetos que pueden ser reseteados en el pool
    /// </summary>
    public interface IPoolable
    {
        void Reset();
    }

    /// <summary>
    /// Estadísticas del memory pool
    /// </summary>
    public class PoolStatistics
    {
        public Dictionary<string, int> PoolSizes { get; set; } = new Dictionary<string, int>();
        public int TotalObjects { get; set; }
        public long TotalMemorySaved { get; set; }

        public override string ToString()
        {
            return $"Total Objects: {TotalObjects}, Pools: {PoolSizes.Count}";
        }
    }

    /// <summary>
    /// Extensiones para facilitar el uso del pool
    /// </summary>
    public static class MemoryPoolExtensions
    {
        /// <summary>
        /// Usa un objeto del pool automáticamente
        /// </summary>
        public static TResult Using<T, TResult>(this MemoryPoolManager pool, Func<T, TResult> func) where T : class, new()
        {
            var item = pool.Get<T>();
            try
            {
                return func(item);
            }
            finally
            {
                pool.Return(item);
            }
        }

        /// <summary>
        /// Usa un array del pool automáticamente
        /// </summary>
        public static TResult UsingArray<T, TResult>(this MemoryPoolManager pool, int size, Func<T[], TResult> func)
        {
            var array = pool.GetArray<T>(size);
            try
            {
                return func(array);
            }
            finally
            {
                pool.ReturnArray(array);
            }
        }

        /// <summary>
        /// Usa StringBuilder del pool automáticamente
        /// </summary>
        public static string UsingStringBuilder(this MemoryPoolManager pool, Action<System.Text.StringBuilder> action, int capacity = 256)
        {
            var sb = pool.GetStringBuilder(capacity);
            try
            {
                action(sb);
                return sb.ToString();
            }
            finally
            {
                pool.ReturnStringBuilder(sb);
            }
        }
    }

    /// <summary>
    /// Objetos optimizados para el pool
    /// </summary>
    public class PooledSearchResult : IPoolable
    {
        public string Filename { get; set; }
        public string Author { get; set; }
        public long Size { get; set; }
        public int BitRate { get; set; }
        public string Hash { get; set; }

        public void Reset()
        {
            Filename = null;
            Author = null;
            Size = 0;
            BitRate = 0;
            Hash = null;
        }
    }

    public class PooledBuffer : IPoolable
    {
        public byte[] Data { get; set; }
        public int Length { get; set; }
        public int Position { get; set; }

        public void Reset()
        {
            Length = 0;
            Position = 0;
            if (Data != null)
            {
                Array.Clear(Data, 0, Data.Length);
            }
        }
    }
}
