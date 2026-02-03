using System;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;

namespace SlskDown.Core
{
    /// <summary>
    /// Servicio de caché moderno usando Microsoft.Extensions.Caching.Memory
    /// Reemplaza System.Runtime.Caching con mejor control de memoria
    /// </summary>
    public class ModernCacheService : IDisposable
    {
        private readonly IMemoryCache _cache;
        private readonly MemoryCacheOptions _options;

        public ModernCacheService(long sizeLimitMB = 512)
        {
            _options = new MemoryCacheOptions
            {
                SizeLimit = sizeLimitMB * 1024, // Convertir MB a KB
                CompactionPercentage = 0.25, // Compactar 25% cuando se alcanza el límite
                ExpirationScanFrequency = TimeSpan.FromMinutes(5)
            };

            _cache = new MemoryCache(_options);
        }

        /// <summary>
        /// Obtiene un valor del caché
        /// </summary>
        public T? Get<T>(string key) where T : class
        {
            return _cache.Get<T>(key);
        }

        /// <summary>
        /// Intenta obtener un valor del caché
        /// </summary>
        public bool TryGetValue<T>(string key, out T? value) where T : class
        {
            return _cache.TryGetValue(key, out value);
        }

        /// <summary>
        /// Guarda un valor en el caché con expiración deslizante
        /// </summary>
        public void Set<T>(string key, T value, TimeSpan slidingExpiration, int sizeInKB = 1) where T : class
        {
            var options = new MemoryCacheEntryOptions
            {
                Size = sizeInKB,
                SlidingExpiration = slidingExpiration,
                Priority = CacheItemPriority.Normal
            };

            _cache.Set(key, value, options);
        }

        /// <summary>
        /// Guarda un valor en el caché con expiración absoluta
        /// </summary>
        public void SetAbsolute<T>(string key, T value, TimeSpan absoluteExpiration, int sizeInKB = 1) where T : class
        {
            var options = new MemoryCacheEntryOptions
            {
                Size = sizeInKB,
                AbsoluteExpirationRelativeToNow = absoluteExpiration,
                Priority = CacheItemPriority.Normal
            };

            _cache.Set(key, value, options);
        }

        /// <summary>
        /// Guarda un valor con alta prioridad (no se compacta fácilmente)
        /// </summary>
        public void SetHighPriority<T>(string key, T value, TimeSpan slidingExpiration, int sizeInKB = 1) where T : class
        {
            var options = new MemoryCacheEntryOptions
            {
                Size = sizeInKB,
                SlidingExpiration = slidingExpiration,
                Priority = CacheItemPriority.High
            };

            _cache.Set(key, value, options);
        }

        /// <summary>
        /// Obtiene o crea un valor en el caché
        /// </summary>
        public T GetOrCreate<T>(string key, Func<T> factory, TimeSpan slidingExpiration, int sizeInKB = 1) where T : class
        {
            if (_cache.TryGetValue<T>(key, out var cached) && cached != null)
            {
                return cached;
            }

            var value = factory();
            Set(key, value, slidingExpiration, sizeInKB);
            return value;
        }

        /// <summary>
        /// Obtiene o crea un valor en el caché de forma asíncrona
        /// </summary>
        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan slidingExpiration, int sizeInKB = 1) where T : class
        {
            if (_cache.TryGetValue<T>(key, out var cached) && cached != null)
            {
                return cached;
            }

            var value = await factory();
            Set(key, value, slidingExpiration, sizeInKB);
            return value;
        }

        /// <summary>
        /// Elimina un valor del caché
        /// </summary>
        public void Remove(string key)
        {
            _cache.Remove(key);
        }

        /// <summary>
        /// Limpia todo el caché
        /// </summary>
        public void Clear()
        {
            if (_cache is MemoryCache memCache)
            {
                memCache.Compact(1.0); // Compactar 100%
            }
        }

        /// <summary>
        /// Compacta el caché manualmente
        /// </summary>
        public void Compact(double percentage = 0.25)
        {
            if (_cache is MemoryCache memCache)
            {
                memCache.Compact(percentage);
            }
        }

        public void Dispose()
        {
            if (_cache is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    /// <summary>
    /// Caché especializado para resultados de búsqueda
    /// </summary>
    public class SearchResultsCache
    {
        private readonly ModernCacheService _cache;
        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromHours(24);

        public SearchResultsCache(long sizeLimitMB = 256)
        {
            _cache = new ModernCacheService(sizeLimitMB);
        }

        public List<SearchResultItem>? GetResults(string query)
        {
            return _cache.Get<List<SearchResultItem>>(GetKey(query));
        }

        public void SaveResults(string query, List<SearchResultItem> results, int estimatedSizeKB = 10)
        {
            _cache.Set(GetKey(query), results, DefaultExpiration, estimatedSizeKB);
        }

        public void InvalidateQuery(string query)
        {
            _cache.Remove(GetKey(query));
        }

        public void Clear()
        {
            _cache.Clear();
        }

        private static string GetKey(string query)
        {
            return $"search:{query.ToLowerInvariant()}";
        }
    }

    /// <summary>
    /// Caché especializado para información de usuarios
    /// </summary>
    public class UserInfoCache
    {
        private readonly ModernCacheService _cache;
        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(30);

        public UserInfoCache(long sizeLimitMB = 64)
        {
            _cache = new ModernCacheService(sizeLimitMB);
        }

        public T? GetUserInfo<T>(string username) where T : class
        {
            return _cache.Get<T>(GetKey(username));
        }

        public void SaveUserInfo<T>(string username, T info) where T : class
        {
            _cache.Set(GetKey(username), info, DefaultExpiration, 1);
        }

        public void InvalidateUser(string username)
        {
            _cache.Remove(GetKey(username));
        }

        private static string GetKey(string username)
        {
            return $"user:{username.ToLowerInvariant()}";
        }
    }
}
