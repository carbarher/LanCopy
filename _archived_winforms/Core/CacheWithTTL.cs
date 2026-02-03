using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core
{
    /// <summary>
    /// Caché genérico con Time-To-Live (estilo Nicotine+)
    /// </summary>
    public class CacheWithTTL<TKey, TValue>
    {
        private class CacheEntry
        {
            public TValue Value { get; set; }
            public DateTime ExpiresAt { get; set; }
            public DateTime CreatedAt { get; set; }
        }
        
        private readonly Dictionary<TKey, CacheEntry> cache = new Dictionary<TKey, CacheEntry>();
        private readonly TimeSpan defaultTTL;
        private readonly object lockObj = new object();
        private readonly int maxEntries;
        
        public CacheWithTTL(TimeSpan defaultTTL, int maxEntries = 1000)
        {
            this.defaultTTL = defaultTTL;
            this.maxEntries = maxEntries;
        }
        
        public void Set(TKey key, TValue value, TimeSpan? ttl = null)
        {
            lock (lockObj)
            {
                // Limpiar si está lleno
                if (cache.Count >= maxEntries && !cache.ContainsKey(key))
                {
                    var oldest = cache.OrderBy(kvp => kvp.Value.CreatedAt).First();
                    cache.Remove(oldest.Key);
                }
                
                cache[key] = new CacheEntry
                {
                    Value = value,
                    ExpiresAt = DateTime.Now + (ttl ?? defaultTTL),
                    CreatedAt = DateTime.Now
                };
            }
        }
        
        public bool TryGet(TKey key, out TValue value)
        {
            lock (lockObj)
            {
                if (cache.ContainsKey(key))
                {
                    var entry = cache[key];
                    if (DateTime.Now < entry.ExpiresAt)
                    {
                        value = entry.Value;
                        return true;
                    }
                    else
                    {
                        cache.Remove(key);
                    }
                }
                
                value = default;
                return false;
            }
        }
        
        public void Remove(TKey key)
        {
            lock (lockObj)
            {
                cache.Remove(key);
            }
        }
        
        public void Clear()
        {
            lock (lockObj)
            {
                cache.Clear();
            }
        }
        
        public int CleanupExpired()
        {
            lock (lockObj)
            {
                var expiredKeys = cache.Where(kvp => DateTime.Now >= kvp.Value.ExpiresAt)
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var key in expiredKeys)
                    cache.Remove(key);
                
                return expiredKeys.Count;
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
        
        public Dictionary<string, object> GetStats()
        {
            lock (lockObj)
            {
                var now = DateTime.Now;
                var expired = cache.Count(kvp => now >= kvp.Value.ExpiresAt);
                
                return new Dictionary<string, object>
                {
                    ["TotalEntries"] = cache.Count,
                    ["ExpiredEntries"] = expired,
                    ["ValidEntries"] = cache.Count - expired,
                    ["MaxEntries"] = maxEntries,
                    ["DefaultTTL"] = defaultTTL.TotalSeconds
                };
            }
        }
    }
}
