using System;
using System.Collections.Generic;

namespace SlskDown.Core.Optimization
{
    /// <summary>
    /// Caché LRU (Least Recently Used) thread-safe con límite de tamaño
    /// </summary>
    public class LRUCache<TKey, TValue>
    {
        private readonly int maxSize;
        private readonly Dictionary<TKey, LinkedListNode<CacheItem>> cache;
        private readonly LinkedList<CacheItem> lruList;
        private readonly object lockObj = new object();
        
        public int Count => cache.Count;
        public int MaxSize => maxSize;
        
        public LRUCache(int maxSize)
        {
            if (maxSize <= 0)
                throw new ArgumentException("Max size must be positive", nameof(maxSize));
            
            this.maxSize = maxSize;
            cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(maxSize);
            lruList = new LinkedList<CacheItem>();
        }
        
        private class CacheItem
        {
            public TKey Key { get; set; }
            public TValue Value { get; set; }
            public DateTime LastAccess { get; set; }
        }
        
        /// <summary>
        /// Obtiene valor del caché
        /// </summary>
        public bool TryGet(TKey key, out TValue value)
        {
            lock (lockObj)
            {
                if (cache.TryGetValue(key, out var node))
                {
                    // Mover al frente (más reciente)
                    lruList.Remove(node);
                    lruList.AddFirst(node);
                    node.Value.LastAccess = DateTime.Now;
                    value = node.Value.Value;
                    return true;
                }
                
                value = default;
                return false;
            }
        }
        
        /// <summary>
        /// Agrega o actualiza valor en caché
        /// </summary>
        public void AddOrUpdate(TKey key, TValue value)
        {
            lock (lockObj)
            {
                if (cache.TryGetValue(key, out var existingNode))
                {
                    // Actualizar existente
                    existingNode.Value.Value = value;
                    existingNode.Value.LastAccess = DateTime.Now;
                    lruList.Remove(existingNode);
                    lruList.AddFirst(existingNode);
                }
                else
                {
                    // Agregar nuevo
                    if (cache.Count >= maxSize)
                        RemoveOldest();
                    
                    var item = new CacheItem
                    {
                        Key = key,
                        Value = value,
                        LastAccess = DateTime.Now
                    };
                    
                    var node = lruList.AddFirst(item);
                    cache[key] = node;
                }
            }
        }
        
        /// <summary>
        /// Remueve item del caché
        /// </summary>
        public bool Remove(TKey key)
        {
            lock (lockObj)
            {
                if (cache.TryGetValue(key, out var node))
                {
                    lruList.Remove(node);
                    cache.Remove(key);
                    return true;
                }
                return false;
            }
        }
        
        /// <summary>
        /// Limpia el caché
        /// </summary>
        public void Clear()
        {
            lock (lockObj)
            {
                cache.Clear();
                lruList.Clear();
            }
        }
        
        /// <summary>
        /// Remueve items más antiguos que la edad especificada
        /// </summary>
        public int RemoveOlderThan(TimeSpan maxAge)
        {
            lock (lockObj)
            {
                var cutoff = DateTime.Now - maxAge;
                var toRemove = new List<TKey>();
                
                var node = lruList.Last;
                while (node != null)
                {
                    if (node.Value.LastAccess < cutoff)
                    {
                        toRemove.Add(node.Value.Key);
                        node = node.Previous;
                    }
                    else
                    {
                        break; // Lista ordenada, no hay más antiguos
                    }
                }
                
                foreach (var key in toRemove)
                {
                    Remove(key);
                }
                
                return toRemove.Count;
            }
        }
        
        /// <summary>
        /// Obtiene estadísticas del caché
        /// </summary>
        public CacheStats GetStats()
        {
            lock (lockObj)
            {
                return new CacheStats
                {
                    Count = cache.Count,
                    MaxSize = maxSize,
                    UsagePercent = (cache.Count / (double)maxSize) * 100,
                    OldestItemAge = lruList.Last != null 
                        ? DateTime.Now - lruList.Last.Value.LastAccess 
                        : TimeSpan.Zero
                };
            }
        }
        
        private void RemoveOldest()
        {
            var oldest = lruList.Last;
            if (oldest != null)
            {
                cache.Remove(oldest.Value.Key);
                lruList.RemoveLast();
            }
        }
        
        public class CacheStats
        {
            public int Count { get; set; }
            public int MaxSize { get; set; }
            public double UsagePercent { get; set; }
            public TimeSpan OldestItemAge { get; set; }
        }
    }
}
