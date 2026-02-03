using System;
using System.Collections.Generic;
using System.Linq;
using SlskDown.Core;

namespace SlskDown.Utils
{
    /// <summary>
    /// Cache de búsquedas recientes para evitar búsquedas duplicadas
    /// Búsquedas instantáneas para queries repetidas en 15 minutos
    /// </summary>
    public class SearchCache
    {
        private readonly LRUCache<string, CachedSearch> _cache;
        
        private class CachedSearch
        {
            public List<SearchResult> Results { get; set; }
            public DateTime SearchTime { get; set; }
            public int ResultCount { get; set; }
            public TimeSpan SearchDuration { get; set; }
        }

        public SearchCache(int capacity = 100, TimeSpan? ttl = null)
        {
            _cache = new LRUCache<string, CachedSearch>(
                capacity, 
                ttl ?? TimeSpan.FromMinutes(15)
            );
        }

        /// <summary>
        /// Intenta obtener resultados del cache
        /// </summary>
        public bool TryGetCached(string query, out List<SearchResult> results, out TimeSpan searchDuration)
        {
            var normalizedQuery = NormalizeQuery(query);
            
            if (_cache.TryGet(normalizedQuery, out var cached))
            {
                results = cached.Results;
                searchDuration = cached.SearchDuration;
                return true;
            }

            results = null;
            searchDuration = TimeSpan.Zero;
            return false;
        }

        /// <summary>
        /// Guarda resultados en cache
        /// </summary>
        public void CacheResults(string query, List<SearchResult> results, TimeSpan searchDuration)
        {
            var normalizedQuery = NormalizeQuery(query);
            
            var cached = new CachedSearch
            {
                Results = results.ToList(), // Clonar para evitar modificaciones
                SearchTime = DateTime.Now,
                ResultCount = results.Count,
                SearchDuration = searchDuration
            };

            _cache.Add(normalizedQuery, cached);
        }

        /// <summary>
        /// Invalida cache para una query específica
        /// </summary>
        public void Invalidate(string query)
        {
            var normalizedQuery = NormalizeQuery(query);
            _cache.Remove(normalizedQuery);
        }

        /// <summary>
        /// Limpia todo el cache
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Obtiene estadísticas del cache
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            var stats = _cache.GetStats();
            return new CacheStatistics
            {
                TotalEntries = stats.TotalEntries,
                Capacity = stats.Capacity,
                HitRate = CalculateHitRate(),
                AverageSearchTime = CalculateAverageSearchTime()
            };
        }

        private string NormalizeQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return string.Empty;

            // Normalizar: lowercase, trim, remover espacios múltiples
            return string.Join(" ", 
                query.ToLowerInvariant()
                     .Trim()
                     .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            );
        }

        private double _totalRequests = 0;
        private double _cacheHits = 0;

        internal void RecordHit()
        {
            _totalRequests++;
            _cacheHits++;
        }

        internal void RecordMiss()
        {
            _totalRequests++;
        }

        private double CalculateHitRate()
        {
            return _totalRequests > 0 ? (_cacheHits / _totalRequests) * 100 : 0;
        }

        private TimeSpan CalculateAverageSearchTime()
        {
            // Implementar si se necesita tracking detallado
            return TimeSpan.Zero;
        }
    }

    public class CacheStatistics
    {
        public int TotalEntries { get; set; }
        public int Capacity { get; set; }
        public double HitRate { get; set; }
        public TimeSpan AverageSearchTime { get; set; }
    }
}
