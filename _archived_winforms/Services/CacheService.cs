using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SlskDown.Services
{
    /// <summary>
    /// Implementación simple de caché en memoria con expiración
    /// </summary>
    public class CacheService : ICacheService
    {
        private class CacheEntry
        {
            public object? Value { get; set; }
            public DateTime ExpiresAt { get; set; }
        }

        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
        private readonly System.Threading.Timer _cleanupTimer;

        public CacheService()
        {
            // Limpiar entradas expiradas cada 60 segundos
            _cleanupTimer = new System.Threading.Timer(
                CleanupExpiredEntries,
                null,
                TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(60)
            );
        }

        public void Set<T>(string key, T value, TimeSpan? expiration = null)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            _cache[key] = new CacheEntry
            {
                Value = value,
                ExpiresAt = DateTime.UtcNow.Add(expiration ?? TimeSpan.FromHours(1))
            };
        }

        public T? Get<T>(string key)
        {
            if (TryGet<T>(key, out var value))
                return value;
            return default;
        }

        public bool Contains(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            if (_cache.TryGetValue(key, out var entry))
            {
                if (DateTime.UtcNow < entry.ExpiresAt)
                    return true;
                else
                    _cache.TryRemove(key, out _);
            }
            return false;
        }

        public bool TryGet<T>(string key, out T? value)
        {
            value = default;

            if (string.IsNullOrEmpty(key))
                return false;

            if (_cache.TryGetValue(key, out var entry))
            {
                if (DateTime.UtcNow < entry.ExpiresAt)
                {
                    value = (T?)entry.Value;
                    return true;
                }
                else
                {
                    // Entrada expirada, remover
                    _cache.TryRemove(key, out _);
                }
            }

            return false;
        }

        public void Remove(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            _cache.TryRemove(key, out _);
        }

        public void Clear()
        {
            _cache.Clear();
        }

        private void CleanupExpiredEntries(object? state)
        {
            var now = DateTime.UtcNow;
            var keysToRemove = new List<string>();

            foreach (var kvp in _cache)
            {
                if (now >= kvp.Value.ExpiresAt)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }
    }
}

