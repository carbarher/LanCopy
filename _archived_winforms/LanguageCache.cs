using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

namespace SlskDown
{
    /// <summary>
    /// MEJORA #2: Caché persistente SQLite para verificación de idioma
    /// Evita re-verificar archivos entre sesiones, acelerando búsquedas significativamente
    /// </summary>
    public class LanguageCache : IDisposable
    {
        private readonly string dbPath;
        private SQLiteConnection connection;
        private readonly object lockObj = new object();

        public LanguageCache(string databasePath = null)
        {
            dbPath = databasePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SlskDown",
                "language_cache.db"
            );

            // Crear directorio si no existe
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath));

            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            lock (lockObj)
            {
                connection = new SQLiteConnection($"Data Source={dbPath};Version=3;");
                connection.Open();

                // Crear tabla si no existe
                string createTableQuery = @"
                    CREATE TABLE IF NOT EXISTS language_verification (
                        file_key TEXT PRIMARY KEY,
                        is_spanish INTEGER NOT NULL,
                        verified_at INTEGER NOT NULL,
                        file_size INTEGER,
                        verification_type TEXT
                    );
                    
                    CREATE INDEX IF NOT EXISTS idx_verified_at ON language_verification(verified_at);
                ";

                using (var cmd = new SQLiteCommand(createTableQuery, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Obtiene el resultado de verificación de idioma desde la caché
        /// </summary>
        /// <param name="fileKey">Clave única del archivo (username|filename)</param>
        /// <param name="maxAgeHours">Edad máxima de la entrada en horas (default: 720 = 30 días)</param>
        /// <returns>True si es español, False si no lo es, null si no está en caché o expiró</returns>
        public bool? Get(string fileKey, int maxAgeHours = 720)
        {
            lock (lockObj)
            {
                try
                {
                    long minTimestamp = DateTimeOffset.UtcNow.AddHours(-maxAgeHours).ToUnixTimeSeconds();

                    string query = @"
                        SELECT is_spanish 
                        FROM language_verification 
                        WHERE file_key = @key AND verified_at >= @minTimestamp
                    ";

                    using (var cmd = new SQLiteCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@key", fileKey);
                        cmd.Parameters.AddWithValue("@minTimestamp", minTimestamp);

                        var result = cmd.ExecuteScalar();
                        if (result != null)
                        {
                            return Convert.ToInt32(result) == 1;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading from cache: {ex.Message}");
                }

                return null;
            }
        }

        /// <summary>
        /// Guarda un resultado de verificación de idioma en la caché
        /// </summary>
        public void Set(string fileKey, bool isSpanish, long fileSize = 0, string verificationType = "content")
        {
            lock (lockObj)
            {
                try
                {
                    long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    string query = @"
                        INSERT OR REPLACE INTO language_verification 
                        (file_key, is_spanish, verified_at, file_size, verification_type)
                        VALUES (@key, @isSpanish, @timestamp, @fileSize, @verificationType)
                    ";

                    using (var cmd = new SQLiteCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@key", fileKey);
                        cmd.Parameters.AddWithValue("@isSpanish", isSpanish ? 1 : 0);
                        cmd.Parameters.AddWithValue("@timestamp", timestamp);
                        cmd.Parameters.AddWithValue("@fileSize", fileSize);
                        cmd.Parameters.AddWithValue("@verificationType", verificationType);

                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing to cache: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Limpia entradas antiguas de la caché
        /// </summary>
        public int CleanOldEntries(int maxAgeHours = 720)
        {
            lock (lockObj)
            {
                try
                {
                    long minTimestamp = DateTimeOffset.UtcNow.AddHours(-maxAgeHours).ToUnixTimeSeconds();

                    string query = "DELETE FROM language_verification WHERE verified_at < @minTimestamp";

                    using (var cmd = new SQLiteCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@minTimestamp", minTimestamp);
                        return cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error cleaning cache: {ex.Message}");
                    return 0;
                }
            }
        }

        /// <summary>
        /// Obtiene estadísticas de la caché
        /// </summary>
        public CacheStats GetStats()
        {
            lock (lockObj)
            {
                try
                {
                    var stats = new CacheStats();

                    string query = @"
                        SELECT 
                            COUNT(*) as total,
                            SUM(CASE WHEN is_spanish = 1 THEN 1 ELSE 0 END) as spanish_count,
                            SUM(CASE WHEN is_spanish = 0 THEN 1 ELSE 0 END) as non_spanish_count
                        FROM language_verification
                    ";

                    using (var cmd = new SQLiteCommand(query, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            stats.TotalEntries = reader.GetInt32(0);
                            stats.SpanishCount = reader.GetInt32(1);
                            stats.NonSpanishCount = reader.GetInt32(2);
                        }
                    }

                    return stats;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting stats: {ex.Message}");
                    return new CacheStats();
                }
            }
        }

        public void Dispose()
        {
            lock (lockObj)
            {
                connection?.Close();
                connection?.Dispose();
            }
        }

        public class CacheStats
        {
            public int TotalEntries { get; set; }
            public int SpanishCount { get; set; }
            public int NonSpanishCount { get; set; }
        }
    }
}
