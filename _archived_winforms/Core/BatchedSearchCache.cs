using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace SlskDown.Core
{
    /// <summary>
    /// Caché de búsquedas con batching para SQLite
    /// 10-100x más rápido que writes individuales
    /// </summary>
    public class BatchedSearchCache : IDisposable
    {
        private readonly Channel<CacheOperation> _channel;
        private readonly Task _processingTask;
        private readonly SqliteConnection _connection;
        private readonly int _batchSize;
        private readonly TimeSpan _batchTimeout;
        private bool _disposed;

        public BatchedSearchCache(string dbPath = null, int batchSize = 100, int batchTimeoutMs = 1000)
        {
            dbPath ??= System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SlskDown",
                "search_cache_batched.db"
            );

            var directory = System.IO.Path.GetDirectoryName(dbPath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            _connection = new SqliteConnection($"Data Source={dbPath}");
            _connection.Open();
            
            InitializeDatabase();

            _batchSize = batchSize;
            _batchTimeout = TimeSpan.FromMilliseconds(batchTimeoutMs);
            _channel = Channel.CreateUnbounded<CacheOperation>();
            _processingTask = Task.Run(ProcessBatchesAsync);
        }

        private void InitializeDatabase()
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS search_cache (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    query_normalized TEXT NOT NULL,
                    network_source TEXT NOT NULL,
                    results_json TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    expires_at TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_cache_query_network 
                ON search_cache(query_normalized, network_source);

                CREATE INDEX IF NOT EXISTS idx_cache_expires 
                ON search_cache(expires_at) 
                WHERE datetime(expires_at) > datetime('now');
            ";
            cmd.ExecuteNonQuery();
        }

        private async Task ProcessBatchesAsync()
        {
            var batch = new List<CacheOperation>(_batchSize);
            var timer = new System.Diagnostics.Stopwatch();

            while (!_disposed)
            {
                try
                {
                    batch.Clear();
                    timer.Restart();

                    // Recolectar operaciones hasta alcanzar batch size o timeout
                    while (batch.Count < _batchSize && timer.Elapsed < _batchTimeout)
                    {
                        if (await _channel.Reader.WaitToReadAsync())
                        {
                            while (batch.Count < _batchSize && _channel.Reader.TryRead(out var op))
                            {
                                batch.Add(op);
                            }
                        }

                        if (batch.Count == 0)
                            break;
                    }

                    if (batch.Count == 0)
                        continue;

                    // Procesar batch en una transacción
                    await ProcessBatchAsync(batch);
                }
                catch (Exception ex)
                {
                    // Log error pero continuar procesando
                    Console.WriteLine($"Error processing cache batch: {ex.Message}");
                }
            }
        }

        private async Task ProcessBatchAsync(List<CacheOperation> batch)
        {
            using var transaction = _connection.BeginTransaction();
            try
            {
                foreach (var op in batch)
                {
                    switch (op.Type)
                    {
                        case OperationType.Set:
                            await SetInternalAsync(op.Key, op.NetworkSource, op.Value, op.Ttl);
                            break;

                        case OperationType.Delete:
                            await DeleteInternalAsync(op.Key, op.NetworkSource);
                            break;

                        case OperationType.Clear:
                            await ClearInternalAsync();
                            break;
                    }
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private async Task SetInternalAsync(string key, string networkSource, List<SearchResult> results, TimeSpan ttl)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(results);
            var now = DateTime.UtcNow;
            var expires = now.Add(ttl);

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO search_cache 
                (query_normalized, network_source, results_json, created_at, expires_at)
                VALUES (@query, @network, @json, @created, @expires)
            ";
            cmd.Parameters.AddWithValue("@query", key);
            cmd.Parameters.AddWithValue("@network", networkSource);
            cmd.Parameters.AddWithValue("@json", json);
            cmd.Parameters.AddWithValue("@created", now.ToString("O"));
            cmd.Parameters.AddWithValue("@expires", expires.ToString("O"));

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task DeleteInternalAsync(string key, string networkSource)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM search_cache WHERE query_normalized = @query AND network_source = @network";
            cmd.Parameters.AddWithValue("@query", key);
            cmd.Parameters.AddWithValue("@network", networkSource);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task ClearInternalAsync()
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM search_cache";
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Obtiene resultados del caché (operación síncrona)
        /// </summary>
        public List<SearchResult> Get(string query, string networkSource = "Soulseek")
        {
            var normalized = NormalizeQuery(query);

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                SELECT results_json 
                FROM search_cache 
                WHERE query_normalized = @query 
                AND network_source = @network
                AND datetime(expires_at) > datetime('now')
                LIMIT 1
            ";
            cmd.Parameters.AddWithValue("@query", normalized);
            cmd.Parameters.AddWithValue("@network", networkSource);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var json = reader.GetString(0);
                return System.Text.Json.JsonSerializer.Deserialize<List<SearchResult>>(json);
            }

            return null;
        }

        /// <summary>
        /// Guarda resultados en el caché (operación asíncrona batched)
        /// </summary>
        public async Task SetAsync(string query, List<SearchResult> results, string networkSource = "Soulseek", TimeSpan? ttl = null)
        {
            var normalized = NormalizeQuery(query);
            ttl ??= TimeSpan.FromDays(7);

            await _channel.Writer.WriteAsync(new CacheOperation
            {
                Type = OperationType.Set,
                Key = normalized,
                NetworkSource = networkSource,
                Value = results,
                Ttl = ttl.Value
            });
        }

        /// <summary>
        /// Elimina entrada del caché
        /// </summary>
        public async Task DeleteAsync(string query, string networkSource = "Soulseek")
        {
            var normalized = NormalizeQuery(query);

            await _channel.Writer.WriteAsync(new CacheOperation
            {
                Type = OperationType.Delete,
                Key = normalized,
                NetworkSource = networkSource
            });
        }

        /// <summary>
        /// Limpia todo el caché
        /// </summary>
        public async Task ClearAsync()
        {
            await _channel.Writer.WriteAsync(new CacheOperation
            {
                Type = OperationType.Clear
            });
        }

        private string NormalizeQuery(string query)
        {
            return query?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _channel.Writer.Complete();
            _processingTask.Wait(5000);
            _connection?.Dispose();
        }
    }

    internal class CacheOperation
    {
        public OperationType Type { get; set; }
        public string Key { get; set; }
        public string NetworkSource { get; set; }
        public List<SearchResult> Value { get; set; }
        public TimeSpan Ttl { get; set; }
    }

    internal enum OperationType
    {
        Set,
        Delete,
        Clear
    }
}
