using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace SlskDown
{
    /// <summary>
    /// Caché persistente de hashes SHA256 para evitar re-validar archivos
    /// </summary>
    public class FileHashCache : IDisposable
    {
        private readonly string dbPath;
        private readonly ConcurrentDictionary<string, CacheEntry> memoryCache;
        private readonly object dbLock = new object();
        private bool disposed = false;

        public class CacheEntry
        {
            public string FilePath { get; set; } = "";
            public string Hash { get; set; } = "";
            public long FileSize { get; set; }
            public DateTime LastModified { get; set; }
            public bool IsValid { get; set; }
            public DateTime CachedAt { get; set; }
        }

        public FileHashCache(string databasePath)
        {
            dbPath = databasePath;
            memoryCache = new ConcurrentDictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
            InitializeDatabase();
            LoadCacheFromDatabase();
        }

        private void InitializeDatabase()
        {
            lock (dbLock)
            {
                using var connection = new SqliteConnection($"Data Source={dbPath}");
                connection.Open();

                var createTableCmd = connection.CreateCommand();
                createTableCmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS FileHashCache (
                        FilePath TEXT PRIMARY KEY,
                        Hash TEXT NOT NULL,
                        FileSize INTEGER NOT NULL,
                        LastModified TEXT NOT NULL,
                        IsValid INTEGER NOT NULL,
                        CachedAt TEXT NOT NULL
                    );
                    CREATE INDEX IF NOT EXISTS idx_hash ON FileHashCache(Hash);
                    CREATE INDEX IF NOT EXISTS idx_cached_at ON FileHashCache(CachedAt);
                ";
                createTableCmd.ExecuteNonQuery();
            }
        }

        private void LoadCacheFromDatabase()
        {
            try
            {
                lock (dbLock)
                {
                    using var connection = new SqliteConnection($"Data Source={dbPath}");
                    connection.Open();

                    var selectCmd = connection.CreateCommand();
                    selectCmd.CommandText = "SELECT FilePath, Hash, FileSize, LastModified, IsValid, CachedAt FROM FileHashCache";

                    using var reader = selectCmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var entry = new CacheEntry
                        {
                            FilePath = reader.GetString(0),
                            Hash = reader.GetString(1),
                            FileSize = reader.GetInt64(2),
                            LastModified = DateTime.Parse(reader.GetString(3)),
                            IsValid = reader.GetInt32(4) == 1,
                            CachedAt = DateTime.Parse(reader.GetString(5))
                        };

                        memoryCache.TryAdd(entry.FilePath, entry);
                    }
                }
            }
            catch
            {
                // Si falla la carga, continuar con caché vacío
            }
        }

        /// <summary>
        /// Calcula SHA256 de un archivo
        /// </summary>
        public static string ComputeFileHash(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = sha256.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Calcula SHA256 de un archivo de forma asíncrona
        /// </summary>
        public static async Task<string> ComputeFileHashAsync(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = await sha256.ComputeHashAsync(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Verifica si un archivo está en caché y es válido
        /// </summary>
        public bool TryGetValidationResult(string filePath, out bool isValid)
        {
            isValid = false;

            if (!File.Exists(filePath))
                return false;

            if (!memoryCache.TryGetValue(filePath, out var entry))
                return false;

            var fileInfo = new FileInfo(filePath);

            // Verificar si el archivo cambió
            if (fileInfo.Length != entry.FileSize || 
                fileInfo.LastWriteTimeUtc != entry.LastModified)
            {
                // Archivo modificado, invalidar caché
                memoryCache.TryRemove(filePath, out _);
                return false;
            }

            isValid = entry.IsValid;
            return true;
        }

        /// <summary>
        /// Guarda resultado de validación en caché
        /// </summary>
        public void SetValidationResult(string filePath, bool isValid)
        {
            if (!File.Exists(filePath))
                return;

            var fileInfo = new FileInfo(filePath);
            var hash = ComputeFileHash(filePath);

            var entry = new CacheEntry
            {
                FilePath = filePath,
                Hash = hash,
                FileSize = fileInfo.Length,
                LastModified = fileInfo.LastWriteTimeUtc,
                IsValid = isValid,
                CachedAt = DateTime.UtcNow
            };

            memoryCache[filePath] = entry;

            // Guardar en base de datos de forma asíncrona
            Task.Run(() => SaveToDatabase(entry));
        }

        /// <summary>
        /// Guarda resultado de validación en caché (versión async)
        /// </summary>
        public async Task SetValidationResultAsync(string filePath, bool isValid)
        {
            if (!File.Exists(filePath))
                return;

            var fileInfo = new FileInfo(filePath);
            var hash = await ComputeFileHashAsync(filePath);

            var entry = new CacheEntry
            {
                FilePath = filePath,
                Hash = hash,
                FileSize = fileInfo.Length,
                LastModified = fileInfo.LastWriteTimeUtc,
                IsValid = isValid,
                CachedAt = DateTime.UtcNow
            };

            memoryCache[filePath] = entry;
            await SaveToDatabaseAsync(entry);
        }

        private void SaveToDatabase(CacheEntry entry)
        {
            try
            {
                lock (dbLock)
                {
                    using var connection = new SqliteConnection($"Data Source={dbPath}");
                    connection.Open();

                    var insertCmd = connection.CreateCommand();
                    insertCmd.CommandText = @"
                        INSERT OR REPLACE INTO FileHashCache 
                        (FilePath, Hash, FileSize, LastModified, IsValid, CachedAt)
                        VALUES (@FilePath, @Hash, @FileSize, @LastModified, @IsValid, @CachedAt)
                    ";

                    insertCmd.Parameters.AddWithValue("@FilePath", entry.FilePath);
                    insertCmd.Parameters.AddWithValue("@Hash", entry.Hash);
                    insertCmd.Parameters.AddWithValue("@FileSize", entry.FileSize);
                    insertCmd.Parameters.AddWithValue("@LastModified", entry.LastModified.ToString("o"));
                    insertCmd.Parameters.AddWithValue("@IsValid", entry.IsValid ? 1 : 0);
                    insertCmd.Parameters.AddWithValue("@CachedAt", entry.CachedAt.ToString("o"));

                    insertCmd.ExecuteNonQuery();
                }
            }
            catch
            {
                // Error guardando en DB, continuar con caché en memoria
            }
        }

        private async Task SaveToDatabaseAsync(CacheEntry entry)
        {
            await Task.Run(() => SaveToDatabase(entry));
        }

        /// <summary>
        /// Busca archivos duplicados por hash
        /// </summary>
        public List<List<string>> FindDuplicates()
        {
            var duplicates = memoryCache.Values
                .Where(e => e.IsValid)
                .GroupBy(e => e.Hash)
                .Where(g => g.Count() > 1)
                .Select(g => g.Select(e => e.FilePath).ToList())
                .ToList();

            return duplicates;
        }

        /// <summary>
        /// Busca archivos duplicados por hash (solo válidos)
        /// </summary>
        public Dictionary<string, List<string>> FindDuplicatesByHash()
        {
            return memoryCache.Values
                .Where(e => e.IsValid)
                .GroupBy(e => e.Hash)
                .Where(g => g.Count() > 1)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.FilePath).ToList()
                );
        }

        /// <summary>
        /// Limpia entradas antiguas (más de 30 días)
        /// </summary>
        public int CleanupOldEntries(int daysOld = 30)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
            var oldEntries = memoryCache
                .Where(kvp => kvp.Value.CachedAt < cutoffDate)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in oldEntries)
            {
                memoryCache.TryRemove(key, out _);
            }

            // Limpiar base de datos
            try
            {
                lock (dbLock)
                {
                    using var connection = new SqliteConnection($"Data Source={dbPath}");
                    connection.Open();

                    var deleteCmd = connection.CreateCommand();
                    deleteCmd.CommandText = "DELETE FROM FileHashCache WHERE CachedAt < @CutoffDate";
                    deleteCmd.Parameters.AddWithValue("@CutoffDate", cutoffDate.ToString("o"));
                    deleteCmd.ExecuteNonQuery();
                }
            }
            catch
            {
                // Error limpiando DB
            }

            return oldEntries.Count;
        }

        /// <summary>
        /// Invalida entrada del caché
        /// </summary>
        public void Invalidate(string filePath)
        {
            memoryCache.TryRemove(filePath, out _);

            try
            {
                lock (dbLock)
                {
                    using var connection = new SqliteConnection($"Data Source={dbPath}");
                    connection.Open();

                    var deleteCmd = connection.CreateCommand();
                    deleteCmd.CommandText = "DELETE FROM FileHashCache WHERE FilePath = @FilePath";
                    deleteCmd.Parameters.AddWithValue("@FilePath", filePath);
                    deleteCmd.ExecuteNonQuery();
                }
            }
            catch
            {
                // Error eliminando de DB
            }
        }

        /// <summary>
        /// Limpia todo el caché
        /// </summary>
        public void Clear()
        {
            memoryCache.Clear();

            try
            {
                lock (dbLock)
                {
                    using var connection = new SqliteConnection($"Data Source={dbPath}");
                    connection.Open();

                    var deleteCmd = connection.CreateCommand();
                    deleteCmd.CommandText = "DELETE FROM FileHashCache";
                    deleteCmd.ExecuteNonQuery();
                }
            }
            catch
            {
                // Error limpiando DB
            }
        }

        /// <summary>
        /// Obtiene estadísticas del caché
        /// </summary>
        public (int TotalEntries, int ValidFiles, int InvalidFiles, long TotalSize) GetStats()
        {
            var validFiles = memoryCache.Values.Count(e => e.IsValid);
            var invalidFiles = memoryCache.Values.Count(e => !e.IsValid);
            var totalSize = memoryCache.Values.Sum(e => e.FileSize);

            return (memoryCache.Count, validFiles, invalidFiles, totalSize);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                // Guardar caché en memoria a base de datos antes de cerrar
                disposed = true;
            }
        }
    }
}
