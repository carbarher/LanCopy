using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core
{
    /// <summary>
    /// Caché compartido para resultados de búsqueda multi-red
    /// Optimiza búsquedas repetidas y deduplica resultados entre redes
    /// </summary>
    public class MultiNetworkCache
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new ConcurrentDictionary<string, CacheEntry>();
        private readonly TimeSpan _defaultExpiration;
        private readonly int _maxEntries;

        public MultiNetworkCache(TimeSpan? defaultExpiration = null, int maxEntries = 1000)
        {
            _defaultExpiration = defaultExpiration ?? TimeSpan.FromMinutes(30);
            _maxEntries = maxEntries;
        }

        /// <summary>
        /// Obtiene resultados del caché si existen y no han expirado
        /// </summary>
        public List<SearchResult> Get(string query)
        {
            var normalizedQuery = NormalizeQuery(query);
            
            if (_cache.TryGetValue(normalizedQuery, out var entry))
            {
                if (DateTime.UtcNow - entry.Timestamp < _defaultExpiration)
                {
                    entry.HitCount++;
                    return entry.Results.ToList(); // Retornar copia
                }
                else
                {
                    // Expirado, remover
                    _cache.TryRemove(normalizedQuery, out _);
                }
            }

            return null;
        }

        /// <summary>
        /// Guarda resultados en el caché
        /// </summary>
        public void Set(string query, List<SearchResult> results)
        {
            var normalizedQuery = NormalizeQuery(query);

            // Evitar crecimiento infinito
            if (_cache.Count >= _maxEntries)
            {
                EvictOldestEntries();
            }

            var entry = new CacheEntry
            {
                Query = query,
                Results = results,
                Timestamp = DateTime.UtcNow,
                HitCount = 0
            };

            _cache[normalizedQuery] = entry;
        }

        /// <summary>
        /// Agrega resultados de una red adicional a una búsqueda existente
        /// </summary>
        public void Merge(string query, List<SearchResult> newResults)
        {
            var normalizedQuery = NormalizeQuery(query);

            if (_cache.TryGetValue(normalizedQuery, out var entry))
            {
                // Deduplicar por hash o nombre+tamaño
                var existingHashes = new HashSet<string>(
                    entry.Results
                        .Where(r => !string.IsNullOrEmpty(r.FileHash))
                        .Select(r => r.FileHash)
                );

                var existingFiles = new HashSet<string>(
                    entry.Results
                        .Where(r => string.IsNullOrEmpty(r.FileHash))
                        .Select(r => $"{NormalizeFileName(r.FileName)}_{r.SizeBytes}")
                );

                foreach (var result in newResults)
                {
                    bool isDuplicate = false;

                    // Verificar por hash
                    if (!string.IsNullOrEmpty(result.FileHash))
                    {
                        isDuplicate = !existingHashes.Add(result.FileHash);
                    }
                    // Verificar por nombre+tamaño
                    else
                    {
                        var key = $"{NormalizeFileName(result.FileName)}_{result.SizeBytes}";
                        isDuplicate = !existingFiles.Add(key);
                    }

                    if (!isDuplicate)
                    {
                        entry.Results.Add(result);
                    }
                }

                entry.Timestamp = DateTime.UtcNow; // Actualizar timestamp
            }
            else
            {
                // No existe en caché, crear nueva entrada
                Set(query, newResults);
            }
        }

        /// <summary>
        /// Limpia entradas expiradas
        /// </summary>
        public void CleanExpired()
        {
            var now = DateTime.UtcNow;
            var expiredKeys = _cache
                .Where(kvp => now - kvp.Value.Timestamp >= _defaultExpiration)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// Obtiene estadísticas del caché
        /// </summary>
        public MultiNetworkCacheStatistics GetStatistics()
        {
            return new MultiNetworkCacheStatistics
            {
                TotalEntries = _cache.Count,
                TotalResults = _cache.Values.Sum(e => e.Results.Count),
                TotalHits = _cache.Values.Sum(e => e.HitCount),
                AverageResultsPerQuery = _cache.Count > 0 ? _cache.Values.Average(e => e.Results.Count) : 0,
                OldestEntry = _cache.Values.Any() ? _cache.Values.Min(e => e.Timestamp) : DateTime.UtcNow,
                NewestEntry = _cache.Values.Any() ? _cache.Values.Max(e => e.Timestamp) : DateTime.UtcNow
            };
        }

        /// <summary>
        /// Limpia todo el caché
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Normaliza query para búsqueda en caché (case-insensitive, sin espacios extras)
        /// </summary>
        private string NormalizeQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return string.Empty;

            return string.Join(" ", query.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>
        /// Normaliza nombre de archivo para comparación
        /// </summary>
        private string NormalizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return string.Empty;

            var normalized = System.IO.Path.GetFileNameWithoutExtension(fileName);
            normalized = normalized.Replace(" ", "").Replace("-", "").Replace("_", "").Replace(".", "");
            return normalized.ToLowerInvariant();
        }

        /// <summary>
        /// Elimina las entradas más antiguas y menos usadas
        /// </summary>
        private void EvictOldestEntries()
        {
            var toRemove = _cache
                .OrderBy(kvp => kvp.Value.HitCount) // Menos usadas primero
                .ThenBy(kvp => kvp.Value.Timestamp) // Más antiguas primero
                .Take(_maxEntries / 10) // Remover 10%
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in toRemove)
            {
                _cache.TryRemove(key, out _);
            }
        }

        private class CacheEntry
        {
            public string Query { get; set; }
            public List<SearchResult> Results { get; set; }
            public DateTime Timestamp { get; set; }
            public int HitCount { get; set; }
        }
    }

    /// <summary>
    /// Estadísticas del caché multi-red
    /// </summary>
    public class MultiNetworkCacheStatistics
    {
        public int TotalEntries { get; set; }
        public int TotalResults { get; set; }
        public int TotalHits { get; set; }
        public double AverageResultsPerQuery { get; set; }
        public DateTime OldestEntry { get; set; }
        public DateTime NewestEntry { get; set; }

        public override string ToString()
        {
            return $"Caché: {TotalEntries} queries, {TotalResults} resultados, {TotalHits} hits, " +
                   $"promedio {AverageResultsPerQuery:F1} resultados/query";
        }
    }
}
