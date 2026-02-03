using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace SlskDown.Database
{
    /// <summary>
    /// Helper para batch inserts optimizados en SQLite
    /// 100-1000x más rápido que inserts individuales
    /// </summary>
    public class BatchInsertHelper
    {
        private readonly string _connectionString;

        public BatchInsertHelper(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Inserta múltiples registros en una sola transacción
        /// </summary>
        public async Task<int> BatchInsertAsync<T>(
            string tableName,
            IEnumerable<T> items,
            Func<T, Dictionary<string, object>> mapToColumns,
            int batchSize = 1000)
        {
            if (!items.Any())
                return 0;

            int totalInserted = 0;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Procesar en lotes
            var batches = items
                .Select((item, index) => new { item, index })
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.Select(x => x.item).ToList());

            foreach (var batch in batches)
            {
                using var transaction = connection.BeginTransaction();
                try
                {
                    foreach (var item in batch)
                    {
                        var columns = mapToColumns(item);
                        await InsertSingleAsync(connection, transaction, tableName, columns);
                        totalInserted++;
                    }

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            return totalInserted;
        }

        /// <summary>
        /// Inserta resultados de búsqueda en batch
        /// </summary>
        public async Task<int> BatchInsertSearchResultsAsync(
            IEnumerable<Core.SearchResult> results,
            string searchQuery,
            DateTime searchDate)
        {
            return await BatchInsertAsync(
                "SearchResults",
                results,
                result => new Dictionary<string, object>
                {
                    ["SearchQuery"] = searchQuery,
                    ["SearchDate"] = searchDate,
                    ["FileName"] = result.FileName,
                    ["SizeBytes"] = result.SizeBytes,
                    ["FileHash"] = result.FileHash ?? "",
                    ["NetworkSource"] = result.NetworkSource ?? "",
                    ["Username"] = result.Username ?? "",
                    ["BitRate"] = result.BitRate ?? 0,
                    ["SampleRate"] = 0,
                    ["BitDepth"] = 0
                }
            );
        }

        /// <summary>
        /// Optimiza la base de datos (VACUUM + ANALYZE)
        /// </summary>
        public async Task OptimizeDatabaseAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // VACUUM recupera espacio no usado
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "VACUUM";
                await cmd.ExecuteNonQueryAsync();
            }

            // ANALYZE actualiza estadísticas para mejor query planning
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "ANALYZE";
                await cmd.ExecuteNonQueryAsync();
            }

            // PRAGMA optimize
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA optimize";
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Crea índices optimizados si no existen
        /// </summary>
        public async Task CreateOptimizedIndexesAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var indexes = new[]
            {
                // Índice para búsquedas por fecha
                "CREATE INDEX IF NOT EXISTS idx_searches_date ON Searches(SearchDate DESC)",
                
                // Índice para descargas por estado y fecha
                "CREATE INDEX IF NOT EXISTS idx_downloads_status_date ON Downloads(Status, CreatedAt DESC)",
                
                // Índice para resultados por query
                "CREATE INDEX IF NOT EXISTS idx_searchresults_query ON SearchResults(SearchQuery)",
                
                // Índice para archivos por hash
                "CREATE INDEX IF NOT EXISTS idx_files_hash ON Files(FileHash)",
                
                // Índice compuesto para búsquedas frecuentes
                "CREATE INDEX IF NOT EXISTS idx_downloads_user_status ON Downloads(Username, Status)"
            };

            foreach (var indexSql in indexes)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = indexSql;
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task InsertSingleAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string tableName,
            Dictionary<string, object> columns)
        {
            var columnNames = string.Join(", ", columns.Keys);
            var paramNames = string.Join(", ", columns.Keys.Select(k => $"@{k}"));
            
            var sql = $"INSERT INTO {tableName} ({columnNames}) VALUES ({paramNames})";

            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;

            foreach (var kvp in columns)
            {
                cmd.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value ?? DBNull.Value);
            }

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Obtiene estadísticas de la base de datos
        /// </summary>
        public async Task<DatabaseStats> GetDatabaseStatsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var stats = new DatabaseStats();

            // Tamaño de la base de datos
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA page_count";
                var pageCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                
                cmd.CommandText = "PRAGMA page_size";
                var pageSize = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                
                stats.SizeBytes = pageCount * pageSize;
            }

            // Número de tablas
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table'";
                stats.TableCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            // Número de índices
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='index'";
                stats.IndexCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            return stats;
        }
    }

    public class DatabaseStats
    {
        public long SizeBytes { get; set; }
        public int TableCount { get; set; }
        public int IndexCount { get; set; }

        public string SizeMB => $"{SizeBytes / (1024.0 * 1024.0):F2} MB";
    }
}
