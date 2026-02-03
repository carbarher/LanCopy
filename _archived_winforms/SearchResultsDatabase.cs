using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using SlskDown.Models;
using SlskDown.UI;

namespace SlskDown
{
    /// <summary>
    /// Base de datos SQLite para almacenar y consultar grandes volúmenes de resultados de búsqueda
    /// Permite manejar millones de resultados sin consumir RAM
    /// </summary>
    public class SearchResultsDatabase : IDisposable
    {
        private SqliteConnection connection;
        private readonly string dbPath;
        private const int BATCH_SIZE = 1000;
        
        public SearchResultsDatabase(string databasePath = null)
        {
            dbPath = databasePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SlskDown",
                "search_cache.db"
            );
            
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
            InitializeDatabase();
        }
        
        private void InitializeDatabase()
        {
            connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            
            // Crear tabla con índices optimizados
            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS SearchResults (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Filename TEXT NOT NULL,
                    Extension TEXT,
                    Username TEXT NOT NULL,
                    Size INTEGER NOT NULL,
                    UploadSpeed INTEGER,
                    FolderPath TEXT,
                    SearchId TEXT,
                    Timestamp INTEGER
                );
                
                CREATE INDEX IF NOT EXISTS idx_filename ON SearchResults(Filename);
                CREATE INDEX IF NOT EXISTS idx_username ON SearchResults(Username);
                CREATE INDEX IF NOT EXISTS idx_extension ON SearchResults(Extension);
                CREATE INDEX IF NOT EXISTS idx_size ON SearchResults(Size);
                CREATE INDEX IF NOT EXISTS idx_searchid ON SearchResults(SearchId);
                
                -- Tabla para metadatos de búsqueda
                CREATE TABLE IF NOT EXISTS SearchMetadata (
                    SearchId TEXT PRIMARY KEY,
                    Query TEXT,
                    ResultCount INTEGER,
                    Timestamp INTEGER
                );
            ";
            command.ExecuteNonQuery();
        }
        
        public void StoreResults(List<SearchResultItem> items, string searchId = null)
        {
            searchId = searchId ?? Guid.NewGuid().ToString();
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            using var transaction = connection.BeginTransaction();
            
            try
            {
                // Insertar en lotes para máximo rendimiento
                for (int i = 0; i < items.Count; i += BATCH_SIZE)
                {
                    var batch = items.Skip(i).Take(BATCH_SIZE);
                    
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    
                    var sql = "INSERT INTO SearchResults (Filename, Extension, Username, Size, UploadSpeed, FolderPath, SearchId, Timestamp) VALUES ";
                    var values = new List<string>();
                    var parameters = new List<SqliteParameter>();
                    
                    int paramIndex = 0;
                    foreach (var item in batch)
                    {
                        values.Add($"(@fn{paramIndex}, @ext{paramIndex}, @user{paramIndex}, @size{paramIndex}, @speed{paramIndex}, @path{paramIndex}, @sid{paramIndex}, @ts{paramIndex})");
                        
                        parameters.Add(new SqliteParameter($"@fn{paramIndex}", item.Filename));
                        parameters.Add(new SqliteParameter($"@ext{paramIndex}", item.Extension ?? ""));
                        parameters.Add(new SqliteParameter($"@user{paramIndex}", item.Username));
                        parameters.Add(new SqliteParameter($"@size{paramIndex}", item.Size));
                        parameters.Add(new SqliteParameter($"@speed{paramIndex}", item.UploadSpeed));
                        parameters.Add(new SqliteParameter($"@path{paramIndex}", item.FolderPath ?? ""));
                        parameters.Add(new SqliteParameter($"@sid{paramIndex}", searchId));
                        parameters.Add(new SqliteParameter($"@ts{paramIndex}", timestamp));
                        
                        paramIndex++;
                    }
                    
                    command.CommandText = sql + string.Join(", ", values);
                    command.Parameters.AddRange(parameters.ToArray());
                    command.ExecuteNonQuery();
                }
                
                // Guardar metadata
                using var metaCommand = connection.CreateCommand();
                metaCommand.Transaction = transaction;
                metaCommand.CommandText = @"
                    INSERT OR REPLACE INTO SearchMetadata (SearchId, ResultCount, Timestamp)
                    VALUES (@searchId, @count, @timestamp)
                ";
                metaCommand.Parameters.AddWithValue("@searchId", searchId);
                metaCommand.Parameters.AddWithValue("@count", items.Count);
                metaCommand.Parameters.AddWithValue("@timestamp", timestamp);
                metaCommand.ExecuteNonQuery();
                
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        
        public List<SearchResultItem> QueryResults(
            string searchId = null,
            string filenameFilter = null,
            string usernameFilter = null,
            string extensionFilter = null,
            long? minSize = null,
            long? maxSize = null,
            int limit = 10000,
            int offset = 0)
        {
            var results = new List<SearchResultItem>();
            
            using var command = connection.CreateCommand();
            var whereClauses = new List<string>();
            
            if (!string.IsNullOrEmpty(searchId))
            {
                whereClauses.Add("SearchId = @searchId");
                command.Parameters.AddWithValue("@searchId", searchId);
            }
            
            if (!string.IsNullOrEmpty(filenameFilter))
            {
                whereClauses.Add("Filename LIKE @filename");
                command.Parameters.AddWithValue("@filename", $"%{filenameFilter}%");
            }
            
            if (!string.IsNullOrEmpty(usernameFilter))
            {
                whereClauses.Add("Username LIKE @username");
                command.Parameters.AddWithValue("@username", $"%{usernameFilter}%");
            }
            
            if (!string.IsNullOrEmpty(extensionFilter))
            {
                whereClauses.Add("Extension = @extension");
                command.Parameters.AddWithValue("@extension", extensionFilter);
            }
            
            if (minSize.HasValue)
            {
                whereClauses.Add("Size >= @minSize");
                command.Parameters.AddWithValue("@minSize", minSize.Value);
            }
            
            if (maxSize.HasValue)
            {
                whereClauses.Add("Size <= @maxSize");
                command.Parameters.AddWithValue("@maxSize", maxSize.Value);
            }
            
            var whereClause = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";
            
            command.CommandText = $@"
                SELECT Filename, Extension, Username, Size, UploadSpeed, FolderPath
                FROM SearchResults
                {whereClause}
                ORDER BY Size DESC
                LIMIT @limit OFFSET @offset
            ";
            
            command.Parameters.AddWithValue("@limit", limit);
            command.Parameters.AddWithValue("@offset", offset);
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new SearchResultItem
                {
                    Filename = reader.GetString(0),
                    Extension = reader.GetString(1),
                    Username = reader.GetString(2),
                    Size = reader.GetInt64(3),
                    UploadSpeed = reader.GetInt32(4),
                    FolderPath = reader.GetString(5)
                });
            }
            
            return results;
        }
        
        public int GetResultCount(string searchId = null)
        {
            using var command = connection.CreateCommand();
            
            if (!string.IsNullOrEmpty(searchId))
            {
                command.CommandText = "SELECT COUNT(*) FROM SearchResults WHERE SearchId = @searchId";
                command.Parameters.AddWithValue("@searchId", searchId);
            }
            else
            {
                command.CommandText = "SELECT COUNT(*) FROM SearchResults";
            }
            
            return Convert.ToInt32(command.ExecuteScalar());
        }
        
        public void ClearOldResults(int daysToKeep = 7)
        {
            var cutoffTimestamp = DateTimeOffset.UtcNow.AddDays(-daysToKeep).ToUnixTimeSeconds();
            
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM SearchResults WHERE Timestamp < @cutoff";
            command.Parameters.AddWithValue("@cutoff", cutoffTimestamp);
            command.ExecuteNonQuery();
            
            // Limpiar metadata huérfana
            command.CommandText = @"
                DELETE FROM SearchMetadata 
                WHERE SearchId NOT IN (SELECT DISTINCT SearchId FROM SearchResults)
            ";
            command.ExecuteNonQuery();
            
            // Vacuum para recuperar espacio
            command.CommandText = "VACUUM";
            command.ExecuteNonQuery();
        }
        
        public void ClearAll()
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM SearchResults; DELETE FROM SearchMetadata; VACUUM;";
            command.ExecuteNonQuery();
        }
        
        public void Dispose()
        {
            connection?.Dispose();
        }
    }
}
