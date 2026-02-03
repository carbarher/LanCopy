using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown
{
    /// <summary>
    /// CachÃ© inteligente de bÃºsquedas para evitar bÃºsquedas duplicadas
    /// </summary>
    public class SearchCache
    {
        private class CacheEntry
        {
            public string Query { get; set; }
            public List<SearchResult> Results { get; set; }
            public DateTime Timestamp { get; set; }
            public int HitCount { get; set; }
        }

        private readonly Dictionary<string, CacheEntry> _cache = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();
        private readonly int _maxEntries;
        private readonly TimeSpan _maxAge;

        public SearchCache(int maxEntries = 50, int maxAgeMinutes = 30)
        {
            _maxEntries = maxEntries;
            _maxAge = TimeSpan.FromMinutes(maxAgeMinutes);
        }

        /// <summary>
        /// Intenta obtener resultados del cachÃ©
        /// </summary>
        public bool TryGet(string query, out List<SearchResult> results)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(query, out var entry))
                {
                    // Verificar si no estÃ¡ expirado
                    if (DateTime.Now - entry.Timestamp < _maxAge)
                    {
                        entry.HitCount++;
                        results = new List<SearchResult>(entry.Results);
                        return true;
                    }
                    else
                    {
                        // Expirado, eliminar
                        _cache.Remove(query);
                    }
                }

                results = null;
                return false;
            }
        }

        /// <summary>
        /// Agrega resultados al cachÃ©
        /// </summary>
        public void Add(string query, List<SearchResult> results)
        {
            lock (_lock)
            {
                // Limpiar entradas antiguas si estÃ¡ lleno
                if (_cache.Count >= _maxEntries)
                {
                    CleanupOldEntries();
                }

                _cache[query] = new CacheEntry
                {
                    Query = query,
                    Results = new List<SearchResult>(results),
                    Timestamp = DateTime.Now,
                    HitCount = 0
                };
            }
        }

        /// <summary>
        /// Limpia entradas antiguas o menos usadas
        /// </summary>
        private void CleanupOldEntries()
        {
            var now = DateTime.Now;
            
            // Eliminar entradas expiradas
            var expired = _cache.Where(kvp => now - kvp.Value.Timestamp >= _maxAge)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expired)
            {
                _cache.Remove(key);
            }

            // Si aÃºn estÃ¡ lleno, eliminar las menos usadas
            if (_cache.Count >= _maxEntries)
            {
                var leastUsed = _cache.OrderBy(kvp => kvp.Value.HitCount)
                    .Take(_maxEntries / 4)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in leastUsed)
                {
                    _cache.Remove(key);
                }
            }
        }

        /// <summary>
        /// Limpia todo el cachÃ©
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
            }
        }

        /// <summary>
        /// Obtiene estadÃ­sticas del cachÃ©
        /// </summary>
        public (int entries, int totalHits, double avgAge) GetStats()
        {
            lock (_lock)
            {
                if (_cache.Count == 0)
                    return (0, 0, 0);

                int entries = _cache.Count;
                int totalHits = _cache.Sum(kvp => kvp.Value.HitCount);
                double avgAge = _cache.Average(kvp => (DateTime.Now - kvp.Value.Timestamp).TotalMinutes);

                return (entries, totalHits, avgAge);
            }
        }

        /// <summary>
        /// Obtiene las bÃºsquedas mÃ¡s populares
        /// </summary>
        public List<(string query, int hits)> GetTopQueries(int count = 10)
        {
            lock (_lock)
            {
                return _cache
                    .OrderByDescending(kvp => kvp.Value.HitCount)
                    .Take(count)
                    .Select(kvp => (kvp.Value.Query, kvp.Value.HitCount))
                    .ToList();
            }
        }
    }
}

