using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// Servicio de base de datos SQLite para SlskDown
    /// </summary>
    public class DatabaseService
    {
        private readonly string connectionString;
        private readonly string dbPath;
        
        public DatabaseService(string dbDirectory = @"c:\p2p\SlskDown\")
        {
            dbPath = Path.Combine(dbDirectory, "slskdown.db");
            connectionString = $"Data Source={dbPath};Version=3;";
            
            InitializeDatabase();
        }
        
        /// <summary>
        /// Estructuras para datos
        /// </summary>
        public struct SearchRecord
        {
            public int Id { get; set; }
            public string Query { get; set; }
            public DateTime Timestamp { get; set; }
            public int ResultsCount { get; set; }
            public int SearchTimeMs { get; set; }
            public string Username { get; set; }
            public bool IsAutoSearch { get; set; }
        }
        
        public struct DownloadRecord
        {
            public int Id { get; set; }
            public string Username { get; set; }
            public string Filename { get; set; }
            public long Size { get; set; }
            public string Bitrate { get; set; }
            public string Country { get; set; }
            public DateTime StartedAt { get; set; }
            public DateTime? CompletedAt { get; set; }
            public long BytesDownloaded { get; set; }
            public double Speed { get; set; }
            public string Status { get; set; }
            public string LocalPath { get; set; }
        }
        
        public struct UserStats
        {
            public string Username { get; set; }
            public int DownloadsCompleted { get; set; }
            public int DownloadsFailed { get; set; }
            public long TotalBytes { get; set; }
            public double AverageSpeed { get; set; }
            public DateTime FirstSeen { get; set; }
            public DateTime LastSeen { get; set; }
            public string Country { get; set; }
            public bool IsBlacklisted { get; set; }
        }
        
        /// <summary>
        /// Inicializar base de datos y crear tablas
        /// </summary>
        private void InitializeDatabase()
        {
            try
            {
                Console.WriteLine("[Database] ðŸ—„ï¸ Inicializando base de datos SQLite");
                
                // Crear directorio si no existe
                var directory = Path.GetDirectoryName(dbPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();
                
                // Crear tablas
                CreateTables(connection);
                
                // Crear Ã­ndices
                CreateIndexes(connection);
                
                Console.WriteLine("[Database] âœ… Base de datos inicializada exitosamente");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Database] âŒ Error inicializando base de datos: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Crear tablas de la base de datos
        /// </summary>
        private void CreateTables(SQLiteConnection connection)
        {
            var commands = new[]
            {
                @"
                CREATE TABLE IF NOT EXISTS searches (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    query TEXT NOT NULL,
                    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                    results_count INTEGER DEFAULT 0,
                    search_time_ms INTEGER DEFAULT 0,
                    username TEXT DEFAULT 'carbar',
                    is_auto_search BOOLEAN DEFAULT 0
                )",
                
                @"
                CREATE TABLE IF NOT EXISTS downloads (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    username TEXT NOT NULL,
                    filename TEXT NOT NULL,
                    size INTEGER DEFAULT 0,
                    bitrate TEXT,
                    country TEXT,
                    started_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    completed_at DATETIME,
                    bytes_downloaded INTEGER DEFAULT 0,
                    speed REAL DEFAULT 0,
                    status TEXT DEFAULT 'pending',
                    local_path TEXT,
                    search_id INTEGER,
                    FOREIGN KEY (search_id) REFERENCES searches (id)
                )",
                
                @"
                CREATE TABLE IF NOT EXISTS user_stats (
                    username TEXT PRIMARY KEY,
                    downloads_completed INTEGER DEFAULT 0,
                    downloads_failed INTEGER DEFAULT 0,
                    total_bytes INTEGER DEFAULT 0,
                    average_speed REAL DEFAULT 0,
                    first_seen DATETIME DEFAULT CURRENT_TIMESTAMP,
                    last_seen DATETIME DEFAULT CURRENT_TIMESTAMP,
                    country TEXT,
                    is_blacklisted BOOLEAN DEFAULT 0
                )",
                
                @"
                CREATE TABLE IF NOT EXISTS file_stats (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    filename TEXT NOT NULL,
                    artist TEXT,
                    title TEXT,
                    extension TEXT,
                    size INTEGER DEFAULT 0,
                    bitrate TEXT,
                    duration INTEGER,
                    download_count INTEGER DEFAULT 0,
                    first_downloaded DATETIME DEFAULT CURRENT_TIMESTAMP,
                    last_downloaded DATETIME DEFAULT CURRENT_TIMESTAMP
                )",
                
                @"
                CREATE TABLE IF NOT EXISTS daily_stats (
                    date DATE PRIMARY KEY,
                    searches_count INTEGER DEFAULT 0,
                    downloads_count INTEGER DEFAULT 0,
                    bytes_downloaded INTEGER DEFAULT 0,
                    unique_users INTEGER DEFAULT 0,
                    active_time_minutes INTEGER DEFAULT 0
                )",
                
                @"
                CREATE TABLE IF NOT EXISTS blacklist (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    username TEXT NOT NULL UNIQUE,
                    reason TEXT,
                    blacklisted_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    blacklisted_by TEXT DEFAULT 'user'
                )",
                
                @"
                CREATE TABLE IF NOT EXISTS watchlist (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    term TEXT NOT NULL UNIQUE,
                    added_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    last_searched DATETIME,
                    search_count INTEGER DEFAULT 0,
                    is_active BOOLEAN DEFAULT 1
                )"
            };
            
            foreach (var command in commands)
            {
                using var cmd = new SQLiteCommand(command, connection);
                cmd.ExecuteNonQuery();
            }
        }
        
        /// <summary>
        /// Crear Ã­ndices para optimizar consultas
        /// </summary>
        private void CreateIndexes(SQLiteConnection connection)
        {
            var indexes = new[]
            {
                "CREATE INDEX IF NOT EXISTS idx_searches_timestamp ON searches(timestamp)",
                "CREATE INDEX IF NOT EXISTS idx_searches_query ON searches(query)",
                "CREATE INDEX IF NOT EXISTS idx_downloads_username ON downloads(username)",
                "CREATE INDEX IF NOT EXISTS idx_downloads_status ON downloads(status)",
                "CREATE INDEX IF NOT EXISTS idx_downloads_started_at ON downloads(started_at)",
                "CREATE INDEX IF NOT EXISTS idx_file_stats_artist ON file_stats(artist)",
                "CREATE INDEX IF NOT EXISTS idx_file_stats_extension ON file_stats(extension)",
                "CREATE INDEX IF NOT EXISTS idx_user_stats_country ON user_stats(country)",
                "CREATE INDEX IF NOT EXISTS idx_blacklist_username ON blacklist(username)",
                "CREATE INDEX IF NOT EXISTS idx_watchlist_term ON watchlist(term)"
            };
            
            foreach (var index in indexes)
            {
                using var cmd = new SQLiteCommand(index, connection);
                cmd.ExecuteNonQuery();
            }
            
            Console.WriteLine("[Database] ðŸ“Š Ãndices creados exitosamente");
        }
        
        /// <summary>
        /// Registrar bÃºsqueda en base de datos
        /// </summary>
        public async Task<int> RecordSearchAsync(string query, int resultsCount, int searchTimeMs, bool isAutoSearch = false)
        {
            try
            {
                using var connection = new SQLiteConnection(connectionString);
                await connection.OpenAsync();
                
                var command = @"
                    INSERT INTO searches (query, results_count, search_time_ms, is_auto_search)
                    VALUES (@query, @resultsCount, @searchTimeMs, @isAutoSearch);
                    SELECT last_insert_rowid();";
                
                using var cmd = new SQLiteCommand(command, connection);
                cmd.Parameters.AddWithValue("@query", query);
                cmd.Parameters.AddWithValue("@resultsCount", resultsCount);
                cmd.Parameters.AddWithValue("@searchTimeMs", searchTimeMs);
                cmd.Parameters.AddWithValue("@isAutoSearch", isAutoSearch);
                
                var id = (long?)await cmd.ExecuteScalarAsync() ?? 0;
                
                // Actualizar estadÃ­sticas diarias
                await UpdateDailyStatsAsync();
                
                Console.WriteLine($"[Database] ðŸ” BÃºsqueda registrada: {query} (ID: {id})");
                return (int)id;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Database] âŒ Error registrando bÃºsqueda: {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// Registrar descarga en base de datos
        /// </summary>
        public async Task<int> RecordDownloadAsync(string username, string filename, long size, string bitrate, string country, int searchId = 0)
        {
            try
            {
                using var connection = new SQLiteConnection(connectionString);
                await connection.OpenAsync();
                
                var command = @"
                    INSERT INTO downloads (username, filename, size, bitrate, country, search_id)
                    VALUES (@username, @filename, @size, @bitrate, @country, @searchId);
                    SELECT last_insert_rowid();";
                
                using var cmd = new SQLiteCommand(command, connection);
                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.AddWithValue("@filename", filename);
                cmd.Parameters.AddWithValue("@size", size);
                cmd.Parameters.AddWithValue("@bitrate", bitrate);
                cmd.Parameters.AddWithValue("@country", country);
                cmd.Parameters.AddWithValue("@searchId", searchId);
                
                var id = (long?)await cmd.ExecuteScalarAsync() ?? 0;
                
                // Actualizar estadÃ­sticas de usuario
                await UpdateUserStatsAsync(username, country);
                
                // Actualizar estadÃ­sticas de archivo
                await UpdateFileStatsAsync(filename, size, bitrate);
                
                Console.WriteLine($"[Database] ðŸ“¥ Descarga registrada: {filename} (ID: {id})");
                return (int)id;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Database] âŒ Error registrando descarga: {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// Actualizar estado de descarga
        /// </summary>
        public async Task UpdateDownloadStatusAsync(int downloadId, string status, long bytesDownloaded = 0, double speed = 0, string? localPath = null)
        {
            try
            {
                using var connection = new SQLiteConnection(connectionString);
                await connection.OpenAsync();
                
                var command = @"
                    UPDATE downloads 
                    SET status = @status, bytes_downloaded = @bytesDownloaded, speed = @speed, local_path = @localPath
                    WHERE id = @id";
                
                using var cmd = new SQLiteCommand(command, connection);
                cmd.Parameters.AddWithValue("@status", status);
                cmd.Parameters.AddWithValue("@bytesDownloaded", bytesDownloaded);
                cmd.Parameters.AddWithValue("@speed", speed);
                cmd.Parameters.AddWithValue("@localPath", localPath ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@id", downloadId);
                
                await cmd.ExecuteNonQueryAsync();
                
                // Si se completÃ³, actualizar timestamp
                if (status == "completed")
                {
                    var completeCmd = "UPDATE downloads SET completed_at = CURRENT_TIMESTAMP WHERE id = @id";
                    using var completeCmdObj = new SQLiteCommand(completeCmd, connection);
                    completeCmdObj.Parameters.AddWithValue("@id", downloadId);
                    await completeCmdObj.ExecuteNonQueryAsync();
                }
                
                Console.WriteLine($"[Database] ðŸ“Š Descarga {downloadId} actualizada: {status}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Database] âŒ Error actualizando descarga: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtener historial de bÃºsquedas
        /// </summary>
        public async Task<List<SearchRecord>> GetSearchHistoryAsync(int limit = 100, DateTime? startDate = null)
        {
            var searches = new List<SearchRecord>();
            
            try
            {
                using var connection = new SQLiteConnection(connectionString);
                await connection.OpenAsync();
                
                var command = @"
                    SELECT id, query, timestamp, results_count, search_time_ms, username, is_auto_search
                    FROM searches
                    WHERE (@startDate IS NULL OR timestamp >= @startDate)
                    ORDER BY timestamp DESC
                    LIMIT @limit";
                
                using var cmd = new SQLiteCommand(command, connection);
                cmd.Parameters.AddWithValue("@startDate", startDate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@limit", limit);
                
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    searches.Add(new SearchRecord
                    {
                        Id = reader.GetInt32(0),
                        Query = reader.GetString(1),
                        Timestamp = reader.GetDateTime(2),
                        ResultsCount = reader.GetInt32(3),
                        SearchTimeMs = reader.GetInt32(4),
                        Username = reader.GetString(5),
                        IsAutoSearch = reader.GetBoolean(6)
                    });
                }
                
                Console.WriteLine($"[Database] ðŸ“‹ Obtenidas {searches.Count} bÃºsquedas del historial");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Database] âŒ Error obteniendo historial: {ex.Message}");
            }
            
            return searches;
        }
        
        /// <summary>
        /// Obtener estadÃ­sticas de usuario
        /// </summary>
        public async Task<UserStats> GetUserStatsAsync(string username)
        {
            var stats = new UserStats { Username = username };
            
            try
            {
                using var connection = new SQLiteConnection(connectionString);
                await connection.OpenAsync();
                
                var command = @"
                    SELECT username, downloads_completed, downloads_failed, total_bytes, average_speed, 
                           first_seen, last_seen, country, is_blacklisted
                    FROM user_stats
                    WHERE username = @username";
                
                using var cmd = new SQLiteCommand(command, connection);
                cmd.Parameters.AddWithValue("@username", username);
                
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    stats.DownloadsCompleted = reader.GetInt32(1);
                    stats.DownloadsFailed = reader.GetInt32(2);
                    stats.TotalBytes = reader.GetInt64(3);
                    stats.AverageSpeed = reader.GetDouble(4);
                    stats.FirstSeen = reader.GetDateTime(5);
                    stats.LastSeen = reader.GetDateTime(6);
                    stats.Country = reader.GetString(7);
                    stats.IsBlacklisted = reader.GetBoolean(8);
                }
                
                Console.WriteLine($"[Database] ðŸ“Š EstadÃ­sticas obtenidas para usuario: {username}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Database] âŒ Error obteniendo estadÃ­sticas: {ex.Message}");
            }
            
            return stats;
        }
        
        /// <summary>
        /// Agregar usuario a blacklist
        /// </summary>
        public async Task<bool> BlacklistUserAsync(string username, string reason = "")
        {
            try
            {
                using var connection = new SQLiteConnection(connectionString);
                await connection.OpenAsync();
                
                var command = @"
                    INSERT OR REPLACE INTO blacklist (username, reason)
                    VALUES (@username, @reason)";
                
                using var cmd = new SQLiteCommand(command, connection);
                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.AddWithValue("@reason", reason);
                
                await cmd.ExecuteNonQueryAsync();
                
                // Actualizar estadÃ­sticas de usuario
                var updateCmd = "UPDATE user_stats SET is_blacklisted = 1 WHERE username = @username";
                using var updateCmdObj = new SQLiteCommand(updateCmd, connection);
                updateCmdObj.Parameters.AddWithValue("@username", username);
                await updateCmdObj.ExecuteNonQueryAsync();
                
                Console.WriteLine($"[Database] ðŸš« Usuario bloqueado: {username}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Database] âŒ Error bloqueando usuario: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Obtener usuarios blacklisteados
        /// </summary>
        public async Task<List<string>> GetBlacklistedUsersAsync()
        {
            var users = new List<string>();
            
            try
            {
                using var connection = new SQLiteConnection(connectionString);
                await connection.OpenAsync();
                
                var command = "SELECT username FROM blacklist ORDER BY blacklisted_at DESC";
                
                using var cmd = new SQLiteCommand(command, connection);
                using var reader = await cmd.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    users.Add(reader.GetString(0));
                }
                
                Console.WriteLine($"[Database] ðŸš« Obtenidos {users.Count} usuarios bloqueados");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Database] âŒ Error obteniendo blacklist: {ex.Message}");
            }
            
            return users;
        }
        
        /// <summary>
        /// Limpiar registros antiguos
        /// </summary>
        public async Task<bool> CleanupOldRecordsAsync(int daysToKeep = 30)
        {
            try
            {
                using var connection = new SQLiteConnection(connectionString);
                await connection.OpenAsync();
                
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                
                var commands = new[]
                {
                    ("DELETE FROM searches WHERE timestamp < @cutoffDate", "bÃºsquedas"),
                    ("DELETE FROM downloads WHERE started_at < @cutoffDate", "descargas"),
                    ("DELETE FROM daily_stats WHERE date < @cutoffDate", "estadÃ­sticas diarias")
                };
                
                int totalDeleted = 0;
                
                foreach (var (command, description) in commands)
                {
                    using var cmd = new SQLiteCommand(command, connection);
                    cmd.Parameters.AddWithValue("@cutoffDate", cutoffDate);
                    var deleted = await cmd.ExecuteNonQueryAsync();
                    totalDeleted += deleted;
                    Console.WriteLine($"[Database] ðŸ§¹ Eliminados {deleted} registros de {description}");
                }
                
                // Optimizar base de datos
                using var vacuumCmd = new SQLiteCommand("VACUUM", connection);
                await vacuumCmd.ExecuteNonQueryAsync();
                
                Console.WriteLine($"[Database] âœ… Limpieza completada: {totalDeleted} registros eliminados");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Database] âŒ Error en limpieza: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Obtener tamaÃ±o de base de datos
        /// </summary>
        public long GetDatabaseSize()
        {
            try
            {
                if (File.Exists(dbPath))
                {
                    return new FileInfo(dbPath).Length;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Database] âŒ Error obteniendo tamaÃ±o: {ex.Message}");
            }
            
            return 0;
        }
        
        /// <summary>
        /// Exportar base de datos a JSON
        /// </summary>
        public async Task<bool> ExportToJsonAsync(string filePath)
        {
            try
            {
                var exportData = new
                {
                    exported_at = DateTime.Now,
                    searches = await GetSearchHistoryAsync(1000),
                    blacklisted_users = await GetBlacklistedUsersAsync(),
                    database_size = GetDatabaseSize(),
                    version = "2.0"
                };
                
                var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);
                
                Console.WriteLine($"[Database] ðŸ“¤ Base de datos exportada a: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Database] âŒ Error exportando a JSON: {ex.Message}");
                return false;
            }
        }
        
        // MÃ©todos auxiliares
        private async Task UpdateDailyStatsAsync()
        {
            // Implementar actualizaciÃ³n de estadÃ­sticas diarias
        }
        
        private async Task UpdateUserStatsAsync(string username, string country)
        {
            // Implementar actualizaciÃ³n de estadÃ­sticas de usuario
        }
        
        private async Task UpdateFileStatsAsync(string filename, long size, string bitrate)
        {
            // Implementar actualizaciÃ³n de estadÃ­sticas de archivo
        }
    }
}

