using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SlskDown.Models;

namespace SlskDown.Core
{
    /// <summary>
    /// Cache persistente de resultados de búsqueda usando SQLite
    /// </summary>
    public class SearchCacheManager : IDisposable
    {
        private readonly string _dbPath;
        private readonly SqliteConnection _connection;
        private readonly TimeSpan _defaultTtl = TimeSpan.FromHours(24);
        
        public SearchCacheManager(string dbPath = null)
        {
            _dbPath = dbPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "search_cache.db");
            
            // Crear directorio si no existe
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath));
            
            _connection = new SqliteConnection($"Data Source={_dbPath}");
            _connection.Open();
            
            InitializeDatabase();
        }
        
        /// <summary>
        /// Inicializa la base de datos y crea tablas si no existen
        /// </summary>
        private void InitializeDatabase()
        {
            var createTableCmd = _connection.CreateCommand();
            createTableCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS search_cache (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    query TEXT NOT NULL,
                    network TEXT NOT NULL,
                    results_json TEXT NOT NULL,
                    result_count INTEGER NOT NULL,
                    created_at TEXT NOT NULL,
                    expires_at TEXT NOT NULL,
                    UNIQUE(query, network)
                );
                
                CREATE INDEX IF NOT EXISTS idx_search_cache_query ON search_cache(query);
                CREATE INDEX IF NOT EXISTS idx_search_cache_expires_at ON search_cache(expires_at);
            ";
            createTableCmd.ExecuteNonQuery();
        }
        
        /// <summary>
        /// Guarda resultados de búsqueda en cache
        /// </summary>
        public async Task CacheResultsAsync(string query, string network, List<SearchResultItem> results, TimeSpan? ttl = null)
        {
            var effectiveTtl = ttl ?? _defaultTtl;
            var expiresAt = DateTime.UtcNow.Add(effectiveTtl);
            
            try
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO search_cache 
                    (query, network, results_json, result_count, created_at, expires_at)
                    VALUES (@query, @network, @results_json, @result_count, @created_at, @expires_at)
                ";
                
                cmd.Parameters.AddWithValue("@query", query.ToLowerInvariant());
                cmd.Parameters.AddWithValue("@network", network);
                cmd.Parameters.AddWithValue("@results_json", System.Text.Json.JsonSerializer.Serialize(results));
                cmd.Parameters.AddWithValue("@result_count", results.Count);
                cmd.Parameters.AddWithValue("@created_at", DateTime.UtcNow.ToString("O"));
                cmd.Parameters.AddWithValue("@expires_at", expiresAt.ToString("O"));
                
                await cmd.ExecuteNonQueryAsync();
                
                Console.WriteLine($"💾 Cached {results.Count} results for '{query}' on {network}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error caching results: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtiene resultados cacheados para una consulta
        /// </summary>
        public async Task<List<SearchResultItem>> GetCachedResultsAsync(string query, string network)
        {
            try
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT results_json, expires_at 
                    FROM search_cache 
                    WHERE query = @query AND network = @network
                ";
                
                cmd.Parameters.AddWithValue("@query", query.ToLowerInvariant());
                cmd.Parameters.AddWithValue("@network", network);
                
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var expiresAt = DateTime.Parse(reader.GetString(1));
                    if (expiresAt > DateTime.UtcNow)
                    {
                        var resultsJson = reader.GetString(0);
                        var results = System.Text.Json.JsonSerializer.Deserialize<List<SearchResultItem>>(resultsJson);
                        
                        Console.WriteLine($"Retrieved {results?.Count ?? 0} cached results for '{query}' on {network}");
                        return results ?? new List<SearchResultItem>();
                    }
                    else
                    {
                        // Cache expirado, eliminar
                        await DeleteExpiredEntryAsync(query, network);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving cached results: {ex.Message}");
            }
            
            return new List<SearchResultItem>();
        }
        
        /// <summary>
        /// Verifica si hay resultados cacheados válidos
        /// </summary>
        public async Task<bool> HasValidCacheAsync(string query, string network)
        {
            try
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT COUNT(*) 
                    FROM search_cache 
                    WHERE query = @query AND network = @network AND expires_at > @now
                ";
                
                cmd.Parameters.AddWithValue("@query", query.ToLowerInvariant());
                cmd.Parameters.AddWithValue("@network", network);
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
                
                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return count > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking cache validity: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Elimina entradas expiradas del cache
        /// </summary>
        public async Task<int> CleanupExpiredAsync()
        {
            try
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = @"
                    DELETE FROM search_cache 
                    WHERE expires_at < @now
                ";
                
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
                
                var deleted = await cmd.ExecuteNonQueryAsync();
                
                if (deleted > 0)
                    Console.WriteLine($"Cleaned up {deleted} expired cache entries");
                
                return deleted;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning up cache: {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// Elimina una entrada específica del cache
        /// </summary>
        private async Task DeleteExpiredEntryAsync(string query, string network)
        {
            try
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = @"
                    DELETE FROM search_cache 
                    WHERE query = @query AND network = @network
                ";
                
                cmd.Parameters.AddWithValue("@query", query.ToLowerInvariant());
                cmd.Parameters.AddWithValue("@network", network);
                
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting expired entry: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Limpia todo el cache
        /// </summary>
        public async Task ClearAllAsync()
        {
            try
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = "DELETE FROM search_cache";
                await cmd.ExecuteNonQueryAsync();
                
                Console.WriteLine("Cleared all search cache");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing cache: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtiene estadísticas del cache
        /// </summary>
        public async Task<CacheStats> GetStatsAsync()
        {
            try
            {
                var stats = new CacheStats();
                
                // Total entries
                var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM search_cache";
                stats.TotalEntries = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                
                // Valid entries
                cmd.CommandText = @"
                    SELECT COUNT(*) 
                    FROM search_cache 
                    WHERE expires_at > @now
                ";
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
                stats.ValidEntries = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                
                // Expired entries
                stats.ExpiredEntries = stats.TotalEntries - stats.ValidEntries;
                
                // Total results cached
                cmd.CommandText = "SELECT SUM(result_count) FROM search_cache WHERE expires_at > @now";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
                var totalResults = await cmd.ExecuteScalarAsync();
                stats.TotalResultsCached = totalResults != DBNull.Value ? Convert.ToInt32(totalResults) : 0;
                
                // Oldest entry
                cmd.CommandText = @"
                    SELECT MIN(created_at) 
                    FROM search_cache 
                    WHERE expires_at > @now
                ";
                var oldest = await cmd.ExecuteScalarAsync();
                if (oldest != DBNull.Value)
                    stats.OldestEntry = DateTime.Parse(oldest.ToString());
                
                // Newest entry
                cmd.CommandText = @"
                    SELECT MAX(created_at) 
                    FROM search_cache 
                    WHERE expires_at > @now
                ";
                var newest = await cmd.ExecuteScalarAsync();
                if (newest != DBNull.Value)
                    stats.NewestEntry = DateTime.Parse(newest.ToString());
                
                return stats;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting cache stats: {ex.Message}");
                return new CacheStats();
            }
        }
        
        /// <summary>
        /// Optimiza la base de datos
        /// </summary>
        public async Task OptimizeAsync()
        {
            try
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = "VACUUM";
                await cmd.ExecuteNonQueryAsync();
                
                Console.WriteLine("🔧 Search cache optimized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error optimizing cache: {ex.Message}");
            }
        }
        
        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
    
    /// <summary>
    /// Estadísticas del cache de búsqueda
    /// </summary>
    public class CacheStats
    {
        public int TotalEntries { get; set; }
        public int ValidEntries { get; set; }
        public int ExpiredEntries { get; set; }
        public int TotalResultsCached { get; set; }
        public DateTime? OldestEntry { get; set; }
        public DateTime? NewestEntry { get; set; }
        
        public override string ToString()
        {
            return $"Entries: {ValidEntries}/{TotalEntries} | " +
                   $"Results: {TotalResultsCached:N0} | " +
                   $"Age: {(OldestEntry.HasValue ? (DateTime.UtcNow - OldestEntry.Value).TotalHours.ToString("F1") + "h ago" : "N/A")} | " +
                   $"Size: {ValidEntries * 1024:N0} bytes (est.)";
        }
    }
}
