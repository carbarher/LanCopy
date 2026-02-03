using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown
{
    /// <summary>
    /// Cache LRU (Least Recently Used) thread-safe con límite de tamaño
    /// </summary>
    public class LRUCache<TKey, TValue>
    {
        private readonly int maxSize;
        private readonly Dictionary<TKey, LinkedListNode<CacheItem>> cache;
        private readonly LinkedList<CacheItem> lruList;
        private readonly object lockObj = new object();
        
        // Estadísticas
        private long hits;
        private long misses;
        private long evictions;
        
        public LRUCache(int maxSize)
        {
            this.maxSize = maxSize;
            this.cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(maxSize);
            this.lruList = new LinkedList<CacheItem>();
        }
        
        public bool TryGetValue(TKey key, out TValue value)
        {
            lock (lockObj)
            {
                if (cache.TryGetValue(key, out var node))
                {
                    // Mover al frente (más recientemente usado)
                    lruList.Remove(node);
                    lruList.AddFirst(node);
                    
                    value = node.Value.Value;
                    hits++;
                    return true;
                }
                
                value = default;
                misses++;
                return false;
            }
        }
        
        public void Add(TKey key, TValue value)
        {
            lock (lockObj)
            {
                if (cache.TryGetValue(key, out var existingNode))
                {
                    // Actualizar valor existente y mover al frente
                    existingNode.Value.Value = value;
                    lruList.Remove(existingNode);
                    lruList.AddFirst(existingNode);
                    return;
                }
                
                // Verificar si necesitamos evictar
                if (cache.Count >= maxSize)
                {
                    // Remover el menos recientemente usado (último)
                    var lruNode = lruList.Last;
                    if (lruNode != null)
                    {
                        cache.Remove(lruNode.Value.Key);
                        lruList.RemoveLast();
                        evictions++;
                    }
                }
                
                // Agregar nuevo item al frente
                var newItem = new CacheItem { Key = key, Value = value };
                var newNode = new LinkedListNode<CacheItem>(newItem);
                lruList.AddFirst(newNode);
                cache[key] = newNode;
            }
        }
        
        public void Clear()
        {
            lock (lockObj)
            {
                cache.Clear();
                lruList.Clear();
                hits = 0;
                misses = 0;
                evictions = 0;
            }
        }
        
        public int Count
        {
            get
            {
                lock (lockObj)
                {
                    return cache.Count;
                }
            }
        }
        
        public CacheStats GetStats()
        {
            lock (lockObj)
            {
                long total = hits + misses;
                double hitRate = total > 0 ? (double)hits / total * 100 : 0;
                
                return new CacheStats
                {
                    Size = cache.Count,
                    MaxSize = maxSize,
                    Hits = hits,
                    Misses = misses,
                    Evictions = evictions,
                    HitRate = hitRate
                };
            }
        }
        
        private class CacheItem
        {
            public TKey Key { get; set; }
            public TValue Value { get; set; }
        }
    }
    
    public class CacheStats
    {
        public int Size { get; set; }
        public int MaxSize { get; set; }
        public long Hits { get; set; }
        public long Misses { get; set; }
        public long Evictions { get; set; }
        public double HitRate { get; set; }
        
        public override string ToString()
        {
            return $"Cache: {Size}/{MaxSize} items | Hit rate: {HitRate:F1}% | Evictions: {Evictions:N0}";
        }
    }
}
