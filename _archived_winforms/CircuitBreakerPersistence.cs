using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.IO;

namespace SlskDown
{
    /// <summary>
    /// Persistencia de circuit breakers en SQLite
    /// Mantiene el estado de usuarios problemáticos entre sesiones
    /// </summary>
    public class CircuitBreakerPersistence
    {
        private readonly string dbPath;
        private readonly string connectionString;
        
        public CircuitBreakerPersistence(string dataDirectory)
        {
            Directory.CreateDirectory(dataDirectory);
            dbPath = Path.Combine(dataDirectory, "circuit_breakers.db");
            connectionString = $"Data Source={dbPath}";
            
            InitializeDatabase();
        }
        
        private void InitializeDatabase()
        {
            using (var conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                
                var createTableCmd = conn.CreateCommand();
                createTableCmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS circuit_breakers (
                        username TEXT PRIMARY KEY,
                        failure_count INTEGER NOT NULL,
                        last_failure DATETIME,
                        state TEXT NOT NULL,
                        opened_at DATETIME,
                        created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                        updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
                    )";
                createTableCmd.ExecuteNonQuery();
                
                // Índice para búsquedas rápidas por estado
                var createIndexCmd = conn.CreateCommand();
                createIndexCmd.CommandText = @"
                    CREATE INDEX IF NOT EXISTS idx_state 
                    ON circuit_breakers(state)";
                createIndexCmd.ExecuteNonQuery();
            }
        }
        
        /// <summary>
        /// Guarda el estado de un circuit breaker
        /// </summary>
        public void SaveCircuitBreaker(string username, RetryPolicy.CircuitBreaker breaker)
        {
            try
            {
                using (var conn = new SqliteConnection(connectionString))
                {
                    conn.Open();
                    
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO circuit_breakers 
                        (username, failure_count, last_failure, state, opened_at, updated_at)
                        VALUES (@username, @failure_count, @last_failure, @state, @opened_at, CURRENT_TIMESTAMP)";
                    
                    cmd.Parameters.AddWithValue("@username", username);
                    cmd.Parameters.AddWithValue("@failure_count", breaker.FailureCount);
                    cmd.Parameters.AddWithValue("@last_failure", 
                        breaker.LastFailureTime.HasValue ? breaker.LastFailureTime.Value : (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@state", breaker.State.ToString());
                    cmd.Parameters.AddWithValue("@opened_at",
                        breaker.OpenedAt.HasValue ? breaker.OpenedAt.Value : (object)DBNull.Value);
                    
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando circuit breaker: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Carga todos los circuit breakers guardados
        /// </summary>
        public Dictionary<string, RetryPolicy.CircuitBreaker> LoadCircuitBreakers()
        {
            var breakers = new Dictionary<string, RetryPolicy.CircuitBreaker>();
            
            try
            {
                using (var conn = new SqliteConnection(connectionString))
                {
                    conn.Open();
                    
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT username, failure_count, last_failure, state, opened_at
                        FROM circuit_breakers
                        WHERE state != 'Closed' OR last_failure > datetime('now', '-1 day')";
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var username = reader.GetString(0);
                            var failureCount = reader.GetInt32(1);
                            var lastFailure = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
                            var state = Enum.Parse<RetryPolicy.CircuitState>(reader.GetString(3));
                            var openedAt = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4);
                            
                            var breaker = new RetryPolicy.CircuitBreaker(
                                failureThreshold: 3,
                                resetTimeoutSeconds: 300
                            );
                            
                            // Restaurar estado
                            breaker.RestoreState(failureCount, lastFailure, state, openedAt);
                            
                            breakers[username] = breaker;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando circuit breakers: {ex.Message}");
            }
            
            return breakers;
        }
        
        /// <summary>
        /// Elimina circuit breakers antiguos (más de 7 días)
        /// </summary>
        public int CleanupOldBreakers()
        {
            try
            {
                using (var conn = new SqliteConnection(connectionString))
                {
                    conn.Open();
                    
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        DELETE FROM circuit_breakers
                        WHERE state = 'Closed' 
                        AND (last_failure IS NULL OR last_failure < datetime('now', '-7 days'))";
                    
                    return cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error limpiando circuit breakers: {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// Obtiene estadísticas de circuit breakers
        /// </summary>
        public (int total, int open, int halfOpen, int closed) GetStatistics()
        {
            try
            {
                using (var conn = new SqliteConnection(connectionString))
                {
                    conn.Open();
                    
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT 
                            COUNT(*) as total,
                            SUM(CASE WHEN state = 'Open' THEN 1 ELSE 0 END) as open,
                            SUM(CASE WHEN state = 'HalfOpen' THEN 1 ELSE 0 END) as half_open,
                            SUM(CASE WHEN state = 'Closed' THEN 1 ELSE 0 END) as closed
                        FROM circuit_breakers";
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return (
                                reader.GetInt32(0),
                                reader.GetInt32(1),
                                reader.GetInt32(2),
                                reader.GetInt32(3)
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo estadísticas: {ex.Message}");
            }
            
            return (0, 0, 0, 0);
        }
        
        /// <summary>
        /// Obtiene los usuarios más problemáticos
        /// </summary>
        public List<(string username, int failures, DateTime? lastFailure)> GetTopProblematicUsers(int limit = 10)
        {
            var users = new List<(string, int, DateTime?)>();
            
            try
            {
                using (var conn = new SqliteConnection(connectionString))
                {
                    conn.Open();
                    
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT username, failure_count, last_failure
                        FROM circuit_breakers
                        WHERE failure_count > 0
                        ORDER BY failure_count DESC, last_failure DESC
                        LIMIT @limit";
                    
                    cmd.Parameters.AddWithValue("@limit", limit);
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            users.Add((
                                reader.GetString(0),
                                reader.GetInt32(1),
                                reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2)
                            ));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo usuarios problemáticos: {ex.Message}");
            }
            
            return users;
        }
    }
}
