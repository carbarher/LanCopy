using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SlskDown.Core
{
    /// <summary>
    /// Caché persistente de resultados de búsqueda usando SQLite
    /// Permite búsquedas instantáneas para queries comunes
    /// </summary>
    public class PersistentSearchCache : IDisposable
    {
        private readonly string _dbPath;
        private readonly SQLiteConnection _connection;
        private readonly TimeSpan _defaultTtl;

        public PersistentSearchCache(string dbPath = null, TimeSpan? ttl = null)
        {
            _dbPath = dbPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SlskDown",
                "search_cache.db"
            );

            _defaultTtl = ttl ?? TimeSpan.FromDays(7);

            // Crear directorio si no existe
            var directory = Path.GetDirectoryName(_dbPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Conectar a base de datos
            _connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
            _connection.Open();

            InitializeDatabase();
        }

        /// <summary>
        /// Inicializa la estructura de la base de datos
        /// </summary>
        private void InitializeDatabase()
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS search_cache (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        query_normalized TEXT NOT NULL,
                        query_original TEXT NOT NULL,
                        network_source TEXT NOT NULL,
                        results_json TEXT NOT NULL,
                        result_count INTEGER NOT NULL,
                        created_at TEXT NOT NULL,
                        expires_at TEXT NOT NULL,
                        hit_count INTEGER DEFAULT 0,
                        last_accessed TEXT
                    );

                    CREATE INDEX IF NOT EXISTS idx_query_network 
                    ON search_cache(query_normalized, network_source);

                    CREATE INDEX IF NOT EXISTS idx_expires 
                    ON search_cache(expires_at);

                    CREATE TABLE IF NOT EXISTS cache_stats (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        total_queries INTEGER DEFAULT 0,
                        cache_hits INTEGER DEFAULT 0,
                        cache_misses INTEGER DEFAULT 0,
                        total_results_cached INTEGER DEFAULT 0,
                        last_cleanup TEXT
                    );

                    INSERT OR IGNORE INTO cache_stats (id) VALUES (1);
                ";
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Obtiene resultados del caché
        /// </summary>
        public List<SearchResult> Get(string query, string networkSource = null)
        {
            var normalized = NormalizeQuery(query);

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT results_json, id 
                    FROM search_cache 
                    WHERE query_normalized = @query 
                    AND (@network IS NULL OR network_source = @network)
                    AND datetime(expires_at) > datetime('now')
                    ORDER BY created_at DESC
                    LIMIT 1
                ";
                cmd.Parameters.AddWithValue("@query", normalized);
                cmd.Parameters.AddWithValue("@network", networkSource ?? (object)DBNull.Value);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var json = reader.GetString(0);
                        var cacheId = reader.GetInt64(1);

                        // Actualizar estadísticas de acceso
                        UpdateAccessStats(cacheId, true);

                        return JsonSerializer.Deserialize<List<SearchResult>>(json);
                    }
                }
            }

            UpdateAccessStats(0, false);
            return null;
        }

        /// <summary>
        /// Guarda resultados en el caché
        /// </summary>
        public void Set(string query, List<SearchResult> results, string networkSource = "All", TimeSpan? ttl = null)
        {
            if (results == null || !results.Any())
                return;

            var normalized = NormalizeQuery(query);
            var expiresAt = DateTime.UtcNow.Add(ttl ?? _defaultTtl);
            var json = JsonSerializer.Serialize(results);

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO search_cache 
                    (query_normalized, query_original, network_source, results_json, result_count, created_at, expires_at)
                    VALUES (@normalized, @original, @network, @json, @count, @created, @expires)
                ";
                cmd.Parameters.AddWithValue("@normalized", normalized);
                cmd.Parameters.AddWithValue("@original", query);
                cmd.Parameters.AddWithValue("@network", networkSource);
                cmd.Parameters.AddWithValue("@json", json);
                cmd.Parameters.AddWithValue("@count", results.Count);
                cmd.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("O"));
                cmd.Parameters.AddWithValue("@expires", expiresAt.ToString("O"));

                cmd.ExecuteNonQuery();
            }

            // Actualizar estadísticas
            UpdateCacheStats(results.Count);
        }

        /// <summary>
        /// Normaliza query para búsqueda consistente
        /// </summary>
        private string NormalizeQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return string.Empty;

            return query.ToLowerInvariant().Trim();
        }

        /// <summary>
        /// Actualiza estadísticas de acceso
        /// </summary>
        private void UpdateAccessStats(long cacheId, bool isHit)
        {
            using (var cmd = _connection.CreateCommand())
            {
                if (isHit && cacheId > 0)
                {
                    cmd.CommandText = @"
                        UPDATE search_cache 
                        SET hit_count = hit_count + 1, last_accessed = @now
                        WHERE id = @id
                    ";
                    cmd.Parameters.AddWithValue("@id", cacheId);
                    cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
                    cmd.ExecuteNonQuery();
                }

                cmd.CommandText = @"
                    UPDATE cache_stats 
                    SET total_queries = total_queries + 1,
                        cache_hits = cache_hits + @hit,
                        cache_misses = cache_misses + @miss
                    WHERE id = 1
                ";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@hit", isHit ? 1 : 0);
                cmd.Parameters.AddWithValue("@miss", isHit ? 0 : 1);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Actualiza estadísticas del caché
        /// </summary>
        private void UpdateCacheStats(int resultCount)
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    UPDATE cache_stats 
                    SET total_results_cached = total_results_cached + @count
                    WHERE id = 1
                ";
                cmd.Parameters.AddWithValue("@count", resultCount);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Limpia entradas expiradas del caché
        /// </summary>
        public int CleanupExpired()
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    DELETE FROM search_cache 
                    WHERE datetime(expires_at) <= datetime('now')
                ";
                var deleted = cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    UPDATE cache_stats 
                    SET last_cleanup = @now
                    WHERE id = 1
                ";
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
                cmd.ExecuteNonQuery();

                return deleted;
            }
        }

        /// <summary>
        /// Obtiene estadísticas del caché
        /// </summary>
        public SearchCacheStatistics GetStatistics()
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT total_queries, cache_hits, cache_misses, total_results_cached, last_cleanup
                    FROM cache_stats WHERE id = 1
                ";

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var stats = new SearchCacheStatistics
                        {
                            TotalQueries = reader.GetInt32(0),
                            CacheHits = reader.GetInt32(1),
                            CacheMisses = reader.GetInt32(2),
                            TotalResultsCached = reader.GetInt32(3)
                        };

                        if (!reader.IsDBNull(4))
                        {
                            stats.LastCleanup = DateTime.Parse(reader.GetString(4));
                        }

                        stats.HitRate = stats.TotalQueries > 0 
                            ? (double)stats.CacheHits / stats.TotalQueries * 100 
                            : 0;

                        return stats;
                    }
                }
            }

            return new SearchCacheStatistics();
        }

        /// <summary>
        /// Limpia todo el caché
        /// </summary>
        public void Clear()
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM search_cache";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    UPDATE cache_stats 
                    SET total_queries = 0, cache_hits = 0, cache_misses = 0, 
                        total_results_cached = 0, last_cleanup = @now
                    WHERE id = 1
                ";
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
                cmd.ExecuteNonQuery();
            }
        }

        public void Dispose()
        {
            _connection?.Close();
            _connection?.Dispose();
        }
    }

    /// <summary>
    /// Estadísticas del caché de búsquedas
    /// </summary>
    public class SearchCacheStatistics
    {
        public int TotalQueries { get; set; }
        public int CacheHits { get; set; }
        public int CacheMisses { get; set; }
        public int TotalResultsCached { get; set; }
        public double HitRate { get; set; }
        public DateTime? LastCleanup { get; set; }
    }
}
