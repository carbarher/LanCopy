using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown
{
    /// <summary>
    /// Cache LRU para validación de archivos (evita I/O repetido)
    /// </summary>
    public class FileValidationCache
    {
        private class CacheEntry
        {
            public bool Exists { get; set; }
            public long Size { get; set; }
            public DateTime LastModified { get; set; }
            public DateTime CachedAt { get; set; }
        }
        
        private readonly ConcurrentDictionary<string, CacheEntry> cache;
        private readonly LinkedList<string> lruList;
        private readonly object lruLock = new object();
        
        private readonly int maxCacheSize;
        private readonly TimeSpan cacheExpiration;
        
        public FileValidationCache(int maxCacheSize = 10000, int expirationSeconds = 300)
        {
            this.maxCacheSize = maxCacheSize;
            this.cacheExpiration = TimeSpan.FromSeconds(expirationSeconds);
            
            cache = new ConcurrentDictionary<string, CacheEntry>(
                Environment.ProcessorCount * 2,
                maxCacheSize
            );
            
            lruList = new LinkedList<string>();
        }
        
        /// <summary>
        /// Obtiene información cacheada del archivo
        /// </summary>
        public bool TryGet(string filePath, out bool exists, out long size)
        {
            exists = false;
            size = 0;
            
            if (string.IsNullOrWhiteSpace(filePath))
                return false;
            
            if (!cache.TryGetValue(filePath, out var entry))
                return false;
            
            // Verificar expiración
            if (DateTime.UtcNow - entry.CachedAt > cacheExpiration)
            {
                cache.TryRemove(filePath, out _);
                return false;
            }
            
            // Actualizar LRU
            lock (lruLock)
            {
                lruList.Remove(filePath);
                lruList.AddFirst(filePath);
            }
            
            exists = entry.Exists;
            size = entry.Size;
            return true;
        }
        
        /// <summary>
        /// Cachea información del archivo
        /// </summary>
        public void Set(string filePath, bool exists, long size, DateTime lastModified)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;
            
            var entry = new CacheEntry
            {
                Exists = exists,
                Size = size,
                LastModified = lastModified,
                CachedAt = DateTime.UtcNow
            };
            
            cache[filePath] = entry;
            
            // Actualizar LRU
            lock (lruLock)
            {
                lruList.Remove(filePath);
                lruList.AddFirst(filePath);
                
                // Evict si excede tamaño
                while (lruList.Count > maxCacheSize)
                {
                    var oldest = lruList.Last.Value;
                    lruList.RemoveLast();
                    cache.TryRemove(oldest, out _);
                }
            }
        }
        
        /// <summary>
        /// Invalida entrada del cache
        /// </summary>
        public void Invalidate(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;
            
            cache.TryRemove(filePath, out _);
            
            lock (lruLock)
            {
                lruList.Remove(filePath);
            }
        }
        
        /// <summary>
        /// Limpia el cache completamente
        /// </summary>
        public void Clear()
        {
            cache.Clear();
            lock (lruLock)
            {
                lruList.Clear();
            }
        }
        
        /// <summary>
        /// Limpia entradas expiradas
        /// </summary>
        public int CleanupExpired()
        {
            var now = DateTime.UtcNow;
            var expired = cache
                .Where(kvp => now - kvp.Value.CachedAt > cacheExpiration)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in expired)
            {
                cache.TryRemove(key, out _);
                lock (lruLock)
                {
                    lruList.Remove(key);
                }
            }
            
            return expired.Count;
        }
        
        /// <summary>
        /// Obtiene estadísticas del cache
        /// </summary>
        public (int Count, int MaxSize, double HitRate) GetStats()
        {
            return (cache.Count, maxCacheSize, 0.0); // TODO: track hit rate
        }
    }
}
