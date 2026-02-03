using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SlskDown.Pooling
{
    /// <summary>
    /// Pool genérico de objetos para reducir allocaciones y presión en GC
    /// </summary>
    public class ObjectPool<T> where T : class
    {
        private readonly ConcurrentBag<T> _objects = new();
        private readonly Func<T> _objectFactory;
        private readonly Action<T>? _resetAction;
        private readonly int _maxSize;
        private int _currentSize;
        
        public ObjectPool(Func<T> objectFactory, Action<T>? resetAction = null, int maxSize = 100)
        {
            _objectFactory = objectFactory ?? throw new ArgumentNullException(nameof(objectFactory));
            _resetAction = resetAction;
            _maxSize = maxSize;
        }
        
        /// <summary>
        /// Obtiene un objeto del pool o crea uno nuevo
        /// </summary>
        public T Get()
        {
            using (PerformanceMetrics.Instance.Track("ObjectPool.Get"))
            {
                if (_objects.TryTake(out var item))
                {
                    Interlocked.Decrement(ref _currentSize);
                    return item;
                }
                
                return _objectFactory();
            }
        }
        
        /// <summary>
        /// Devuelve un objeto al pool
        /// </summary>
        public void Return(T item)
        {
            if (item == null)
                return;
            
            using (PerformanceMetrics.Instance.Track("ObjectPool.Return"))
            {
                // Resetear el objeto si hay una acción definida
                _resetAction?.Invoke(item);
                
                // Solo agregar al pool si no excede el tamaño máximo
                if (_currentSize < _maxSize)
                {
                    _objects.Add(item);
                    Interlocked.Increment(ref _currentSize);
                }
                // Si el objeto es IDisposable y no lo agregamos al pool, liberarlo
                else if (item is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
        
        /// <summary>
        /// Limpia el pool
        /// </summary>
        public void Clear()
        {
            while (_objects.TryTake(out var item))
            {
                if (item is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            
            _currentSize = 0;
        }
        
        /// <summary>
        /// Obtiene el tamaño actual del pool
        /// </summary>
        public int Count => _currentSize;
    }
    
    /// <summary>
    /// Wrapper para usar objetos del pool con using
    /// </summary>
    public struct PooledObject<T> : IDisposable where T : class
    {
        private readonly ObjectPool<T> _pool;
        private T? _object;
        
        public PooledObject(ObjectPool<T> pool)
        {
            _pool = pool;
            _object = pool.Get();
        }
        
        public T Object => _object ?? throw new ObjectDisposedException(nameof(PooledObject<T>));
        
        public void Dispose()
        {
            if (_object != null)
            {
                _pool.Return(_object);
                _object = null;
            }
        }
    }
    
    /// <summary>
    /// Pools predefinidos para objetos comunes
    /// </summary>
    public static class CommonPools
    {
        /// <summary>
        /// Pool de StringBuilder
        /// </summary>
        public static readonly ObjectPool<System.Text.StringBuilder> StringBuilder = 
            new ObjectPool<System.Text.StringBuilder>(
                () => new System.Text.StringBuilder(256),
                sb => sb.Clear(),
                maxSize: 50);
        
        /// <summary>
        /// Pool de MemoryStream
        /// </summary>
        public static readonly ObjectPool<System.IO.MemoryStream> MemoryStream = 
            new ObjectPool<System.IO.MemoryStream>(
                () => new System.IO.MemoryStream(4096),
                ms => { ms.SetLength(0); ms.Position = 0; },
                maxSize: 20);
        
        /// <summary>
        /// Pool de byte arrays (4KB)
        /// </summary>
        public static readonly ObjectPool<byte[]> ByteArray4K = 
            new ObjectPool<byte[]>(
                () => new byte[4096],
                arr => Array.Clear(arr, 0, arr.Length),
                maxSize: 50);
        
        /// <summary>
        /// Pool de byte arrays (64KB)
        /// </summary>
        public static readonly ObjectPool<byte[]> ByteArray64K = 
            new ObjectPool<byte[]>(
                () => new byte[65536],
                arr => Array.Clear(arr, 0, arr.Length),
                maxSize: 20);
    }
}
