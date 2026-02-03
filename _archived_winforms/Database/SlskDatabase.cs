using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using SlskDown.Database.Models;
using SlskDown;

namespace SlskDown.Database
{
    /// <summary>
    /// Clase principal para gestión de la base de datos SQLite
    /// </summary>
    public class SlskDatabase : IDisposable
    {
        private readonly string connectionString;
        private SqliteConnection connection;
        private readonly object lockObj = new object();
        
        // Sistema de batch writes para mejor rendimiento
        private readonly Queue<(string sql, object parameters)> batchQueue = new Queue<(string, object)>();
        private readonly object batchLock = new object();
        private const int BATCH_SIZE = 100;

        public SlskDatabase(string dbPath = "slskdown.db")
        {
            connectionString = $"Data Source={dbPath}";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            connection = new SqliteConnection(connectionString);
            connection.Open();

            connection.Execute("PRAGMA foreign_keys = ON;");
            connection.Execute("PRAGMA journal_mode = WAL;");
            connection.Execute("PRAGMA synchronous = NORMAL;");
            connection.Execute("PRAGMA temp_store = MEMORY;");
            connection.Execute("PRAGMA busy_timeout = 5000;");

            CreateTables();
            EnsureSchemaUpgrades();
        }

        private void CreateTables()
        {
            var sql = @"
                -- Tabla de descargas
                CREATE TABLE IF NOT EXISTS Downloads (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FileName TEXT NOT NULL,
                    Author TEXT,
                    Username TEXT NOT NULL,
                    SizeBytes INTEGER NOT NULL,
                    Status TEXT NOT NULL,
                    DownloadedAt DATETIME NOT NULL,
                    FilePath TEXT,
                    Speed REAL,
                    Language TEXT,
                    MD5Hash TEXT,
                    SHA1Hash TEXT
                );
                CREATE INDEX IF NOT EXISTS idx_downloads_author ON Downloads(Author);
                CREATE INDEX IF NOT EXISTS idx_downloads_username ON Downloads(Username);
                CREATE INDEX IF NOT EXISTS idx_downloads_date ON Downloads(DownloadedAt);
                CREATE INDEX IF NOT EXISTS idx_downloads_status ON Downloads(Status);
                CREATE INDEX IF NOT EXISTS idx_downloads_md5 ON Downloads(MD5Hash);
                CREATE INDEX IF NOT EXISTS idx_downloads_size ON Downloads(SizeBytes);
                CREATE INDEX IF NOT EXISTS idx_downloads_filename ON Downloads(FileName);

                -- Tabla de caché de búsquedas
                CREATE TABLE IF NOT EXISTS SearchCache (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Query TEXT NOT NULL,
                    Username TEXT NOT NULL,
                    FileName TEXT NOT NULL,
                    Size INTEGER NOT NULL,
                    FilePath TEXT,
                    Speed REAL,
                    CachedAt DATETIME NOT NULL,
                    ExpiresAt DATETIME NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_searchcache_query ON SearchCache(Query, ExpiresAt);
                CREATE INDEX IF NOT EXISTS idx_searchcache_expires ON SearchCache(ExpiresAt);
                CREATE INDEX IF NOT EXISTS idx_searchcache_query_order ON SearchCache(Query, ExpiresAt, Speed DESC, Size ASC);

                -- Tabla de puntuación de fuentes
                CREATE TABLE IF NOT EXISTS SourceRatings (
                    Username TEXT PRIMARY KEY,
                    AverageSpeed REAL NOT NULL DEFAULT 0,
                    SuccessRate REAL NOT NULL DEFAULT 0,
                    TotalDownloads INTEGER NOT NULL DEFAULT 0,
                    FailedDownloads INTEGER NOT NULL DEFAULT 0,
                    DisconnectionCount INTEGER NOT NULL DEFAULT 0,
                    LastSeen DATETIME NOT NULL,
                    Score REAL NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS idx_sourceratings_score ON SourceRatings(Score DESC);

                -- Tabla de listas de autores
                CREATE TABLE IF NOT EXISTS Authors (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE,
                    Source TEXT,
                    IsCanonical INTEGER NOT NULL DEFAULT 0,
                    Priority INTEGER NOT NULL DEFAULT 0,
                    LastSearched DATETIME,
                    AddedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    NormalizedName TEXT
                );
                CREATE INDEX IF NOT EXISTS idx_authors_name ON Authors(Name COLLATE NOCASE);
                -- Nota: idx_authors_normalized se crea en EnsureAuthorsSchema() para soportar upgrades

                -- Tabla de watchlist
                CREATE TABLE IF NOT EXISTS Watchlist (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Author TEXT NOT NULL UNIQUE,
                    Enabled INTEGER NOT NULL DEFAULT 1,
                    AutoDownload INTEGER NOT NULL DEFAULT 0,
                    NotifyOnNew INTEGER NOT NULL DEFAULT 1,
                    SearchIntervalHours REAL NOT NULL DEFAULT 24,
                    LastSearch DATETIME NOT NULL,
                    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
                CREATE INDEX IF NOT EXISTS idx_watchlist_enabled ON Watchlist(Enabled);
                CREATE INDEX IF NOT EXISTS idx_watchlist_lastsearch ON Watchlist(LastSearch);

                -- Tabla de hashes de archivos (para detección de duplicados)
                CREATE TABLE IF NOT EXISTS FileHashes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FilePath TEXT NOT NULL UNIQUE,
                    FileName TEXT NOT NULL,
                    Size INTEGER NOT NULL,
                    MD5Hash TEXT,
                    SHA1Hash TEXT,
                    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    LastModified DATETIME NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_filehashes_size ON FileHashes(Size);
                CREATE INDEX IF NOT EXISTS idx_filehashes_md5 ON FileHashes(MD5Hash);
                CREATE INDEX IF NOT EXISTS idx_filehashes_sha1 ON FileHashes(SHA1Hash);

                -- MEJORA: Tabla de checksums SHA256 para verificación de integridad
                CREATE TABLE IF NOT EXISTS FileChecksums (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FilePath TEXT NOT NULL,
                    FileName TEXT NOT NULL,
                    SizeBytes INTEGER NOT NULL,
                    SHA256Checksum TEXT NOT NULL,
                    VerifiedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    LastModified DATETIME NOT NULL,
                    IsValid INTEGER NOT NULL DEFAULT 1,
                    DownloadId INTEGER,
                    FOREIGN KEY (DownloadId) REFERENCES Downloads(Id)
                );
                CREATE UNIQUE INDEX IF NOT EXISTS idx_checksums_path_modified ON FileChecksums(FilePath, LastModified);
                CREATE INDEX IF NOT EXISTS idx_checksums_sha256 ON FileChecksums(SHA256Checksum);
                CREATE INDEX IF NOT EXISTS idx_checksums_verified ON FileChecksums(VerifiedAt);
                CREATE INDEX IF NOT EXISTS idx_checksums_download ON FileChecksums(DownloadId);
            ";

            connection.Execute(sql);
        }

        private void EnsureSchemaUpgrades()
        {
            EnsureAuthorsSchema();
        }

        private void EnsureAuthorsSchema()
        {
            try
            {
                var columnNames = connection
                    .Query<string>("SELECT name FROM pragma_table_info('Authors');")
                    .Select(name => name.ToLowerInvariant())
                    .ToHashSet();

                if (!columnNames.Contains("iscanonical"))
                {
                    connection.Execute("ALTER TABLE Authors ADD COLUMN IsCanonical INTEGER NOT NULL DEFAULT 0;");
                }

                if (!columnNames.Contains("priority"))
                {
                    connection.Execute("ALTER TABLE Authors ADD COLUMN Priority INTEGER NOT NULL DEFAULT 0;");
                }

                if (!columnNames.Contains("lastsearched"))
                {
                    connection.Execute("ALTER TABLE Authors ADD COLUMN LastSearched DATETIME;");
                }

                if (!columnNames.Contains("addedat"))
                {
                    connection.Execute("ALTER TABLE Authors ADD COLUMN AddedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP;");
                }

                if (!columnNames.Contains("normalizedname"))
                {
                    connection.Execute("ALTER TABLE Authors ADD COLUMN NormalizedName TEXT;");
                }

                // Reforzar índices clave
                connection.Execute("CREATE INDEX IF NOT EXISTS idx_authors_name ON Authors(Name COLLATE NOCASE);");
                connection.Execute("CREATE INDEX IF NOT EXISTS idx_authors_canonical ON Authors(IsCanonical, Priority DESC);");
                connection.Execute("CREATE INDEX IF NOT EXISTS idx_authors_lastsearched ON Authors(LastSearched);");
                connection.Execute("CREATE INDEX IF NOT EXISTS idx_authors_normalized ON Authors(NormalizedName COLLATE NOCASE);");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("No se pudo actualizar el esquema de la tabla Authors.", ex);
            }
        }

        // ===== OPERACIONES DE DESCARGAS =====

        public async Task<int> GetAuthorCountAsync()
        {
            const string sql = "SELECT COUNT(*) FROM Authors";
            return await connection.ExecuteScalarAsync<int>(sql);
        }

        public async Task BulkInsertAuthorsAsync(
            IEnumerable<string> authors,
            string source,
            Func<string, bool>? isCanonicalFunc = null,
            Func<string, int>? priorityFunc = null)
        {
            if (authors == null)
            {
                return;
            }

            const string sql = @"
                INSERT INTO Authors (Name, Source, IsCanonical, Priority, AddedAt, NormalizedName)
                VALUES (@Name, @Source, @IsCanonical, @Priority, @AddedAt, @NormalizedName)
                ON CONFLICT(Name) DO UPDATE SET
                    IsCanonical = excluded.IsCanonical,
                    Priority = excluded.Priority,
                    NormalizedName = excluded.NormalizedName;";

            var authorList = authors
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .ToList();

            if (authorList.Count == 0)
            {
                return;
            }

            var normalizedList = SlskDown.Core.AuthorNormalizer.NormalizeBatch(authorList) ?? new List<string>();

            var timestamp = DateTime.UtcNow;
            var canonicalEvaluator = isCanonicalFunc;
            var priorityEvaluator = priorityFunc;

            var records = authorList
                .Select((trimmed, index) => new
                {
                    Name = trimmed,
                    Source = source,
                    IsCanonical = canonicalEvaluator?.Invoke(trimmed) == true ? 1 : 0,
                    Priority = priorityEvaluator != null ? priorityEvaluator(trimmed) : 0,
                    AddedAt = timestamp,
                    NormalizedName = index < normalizedList.Count
                        ? normalizedList[index]
                        : AuthorNormalizer.Normalize(trimmed)
                });

            using var transaction = connection.BeginTransaction();
            try
            {
                await connection.ExecuteAsync(sql, records, transaction);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<List<string>> GetAllAuthorsAsync()
        {
            const string sql = @"
                SELECT Name FROM Authors
                ORDER BY IsCanonical DESC, Priority DESC, Name COLLATE NOCASE";
            var result = await connection.QueryAsync<string>(sql);
            return result.ToList();
        }

        public async Task EnsureNormalizedNamesAsync()
        {
            const string selectSql = @"
                SELECT Id, Name FROM Authors
                WHERE NormalizedName IS NULL OR NormalizedName = ''";

            var pending = (await connection.QueryAsync<(long Id, string Name)>(selectSql))
                .Where(row => !string.IsNullOrWhiteSpace(row.Name))
                .ToList();

            if (pending.Count == 0)
            {
                return;
            }

            var normalized = SlskDown.Core.AuthorNormalizer.NormalizeBatch(pending.Select(p => p.Name)) ?? new List<string>();

            const string updateSql = @"UPDATE Authors SET NormalizedName = @NormalizedName WHERE Id = @Id";

            using var transaction = connection.BeginTransaction();
            try
            {
                for (int i = 0; i < pending.Count; i++)
                {
                    var normalizedValue = i < normalized.Count
                        ? normalized[i]
                        : AuthorNormalizer.Normalize(pending[i].Name);

                    await connection.ExecuteAsync(
                        updateSql,
                        new { Id = pending[i].Id, NormalizedName = normalizedValue },
                        transaction);
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<List<(string Name, string NormalizedName)>> GetAuthorsWithNormalizedAsync()
        {
            const string sql = @"
                SELECT Name, COALESCE(NormalizedName, '') AS NormalizedName
                FROM Authors
                ORDER BY IsCanonical DESC, Priority DESC, Name COLLATE NOCASE";

            var result = await connection.QueryAsync<(string Name, string NormalizedName)>(sql);
            return result.ToList();
        }

        public async Task UpdateAuthorCanonicalStatusAsync(string authorName, bool isCanonical, int priority)
        {
            const string sql = @"
                UPDATE Authors
                SET IsCanonical = @IsCanonical, Priority = @Priority
                WHERE Name = @Name COLLATE NOCASE";
            await connection.ExecuteAsync(sql, new
            {
                Name = authorName,
                IsCanonical = isCanonical ? 1 : 0,
                Priority = priority
            });
        }

        public async Task UpdateAuthorLastSearchedAsync(string authorName)
        {
            const string sql = @"
                UPDATE Authors
                SET LastSearched = @LastSearched
                WHERE Name = @Name COLLATE NOCASE";
            await connection.ExecuteAsync(sql, new
            {
                Name = authorName,
                LastSearched = DateTime.UtcNow
            });
        }

        public async Task<int> GetCanonicalAuthorCountAsync()
        {
            return await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM Authors WHERE IsCanonical = 1");
        }

        public async Task<(string Username, double Score)> GetTopProviderAsync()
        {
            var sql = "SELECT Username, Score FROM SourceRatings ORDER BY Score DESC LIMIT 1";
            return await connection.QueryFirstOrDefaultAsync<(string Username, double Score)>(sql);
        }

        public async Task ClearAuthorsAsync()
        {
            const string sql = "DELETE FROM Authors";
            await connection.ExecuteAsync(sql);
        }

        public async Task ExecuteAsync(string sql, object? parameters = null)
        {
            await connection.ExecuteAsync(sql, parameters);
        }

        public async Task<long> InsertDownloadAsync(DownloadRecord download)
        {
            var sql = @"
                INSERT INTO Downloads (FileName, Author, Username, SizeBytes, Status, DownloadedAt, FilePath, Speed, Language, MD5Hash, SHA1Hash)
                VALUES (@FileName, @Author, @Username, @SizeBytes, @Status, @DownloadedAt, @FilePath, @Speed, @Language, @MD5Hash, @SHA1Hash);
                SELECT last_insert_rowid();
            ";
            return await connection.ExecuteScalarAsync<long>(sql, download);
        }

        public async Task<List<DownloadRecord>> GetDownloadsAsync(int limit = 100, int offset = 0)
        {
            var sql = "SELECT * FROM Downloads ORDER BY DownloadedAt DESC LIMIT @Limit OFFSET @Offset";
            var result = await connection.QueryAsync<DownloadRecord>(sql, new { Limit = limit, Offset = offset });
            return result.ToList();
        }

        public async Task<List<DownloadRecord>> GetDownloadsByAuthorAsync(string author)
        {
            var sql = "SELECT * FROM Downloads WHERE Author = @Author ORDER BY DownloadedAt DESC";
            var result = await connection.QueryAsync<DownloadRecord>(sql, new { Author = author });
            return result.ToList();
        }

        public async Task<DownloadRecord> FindDownloadByHashAsync(string md5Hash)
        {
            var sql = "SELECT * FROM Downloads WHERE MD5Hash = @MD5Hash LIMIT 1";
            return await connection.QueryFirstOrDefaultAsync<DownloadRecord>(sql, new { MD5Hash = md5Hash });
        }

        public async Task<List<DownloadRecord>> FindDownloadsBySizeAsync(long size)
        {
            var sql = "SELECT * FROM Downloads WHERE SizeBytes = @Size";
            var result = await connection.QueryAsync<DownloadRecord>(sql, new { Size = size });
            return result.ToList();
        }

        public async Task<List<DownloadRecord>> GetDownloadsByFileNameAsync(string fileName)
        {
            var sql = "SELECT * FROM Downloads WHERE FileName = @FileName";
            var result = await connection.QueryAsync<DownloadRecord>(sql, new { FileName = fileName });
            return result.ToList();
        }

        // ===== OPERACIONES DE CACHÉ DE BÚSQUEDA =====

        public async Task CacheSearchResultsAsync(string query, List<SearchCacheEntry> results)
        {
            var sql = @"
                INSERT OR REPLACE INTO SearchCache (Query, Username, FileName, Size, FilePath, Speed, CachedAt, ExpiresAt)
                VALUES (@Query, @Username, @FileName, @Size, @FilePath, @Speed, @CachedAt, @ExpiresAt)
            ";

            if (results == null || results.Count == 0)
            {
                return;
            }

            var normalizedQuery = (query ?? string.Empty).Trim().ToLowerInvariant();
            foreach (var item in results)
            {
                item.Query = normalizedQuery;
            }

            using var transaction = connection.BeginTransaction();
            try
            {
                await connection.ExecuteAsync(sql, results, transaction);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<List<SearchCacheEntry>> GetCachedSearchResultsAsync(string query)
        {
            var sql = @"
                SELECT * FROM SearchCache 
                WHERE Query = @Query AND ExpiresAt > @Now
                ORDER BY Speed DESC, Size ASC
            ";
            var normalizedQuery = (query ?? string.Empty).Trim().ToLowerInvariant();
            var result = await connection.QueryAsync<SearchCacheEntry>(sql, new { Query = normalizedQuery, Now = DateTime.UtcNow });
            return result.ToList();
        }

        public async Task<bool> HasCachedResultsAsync(string query)
        {
            var sql = "SELECT COUNT(*) FROM SearchCache WHERE Query = @Query AND ExpiresAt > @Now";
            var normalizedQuery = (query ?? string.Empty).Trim().ToLowerInvariant();
            var count = await connection.ExecuteScalarAsync<int>(sql, new { Query = normalizedQuery, Now = DateTime.UtcNow });
            return count > 0;
        }

        public async Task CleanExpiredCacheAsync()
        {
            var sql = "DELETE FROM SearchCache WHERE ExpiresAt < @Now";
            await connection.ExecuteAsync(sql, new { Now = DateTime.UtcNow });
        }

        // ===== OPERACIONES DE PUNTUACIÓN DE FUENTES =====

        public async Task UpsertSourceRatingAsync(SourceRating rating)
        {
            var sql = @"
                INSERT OR REPLACE INTO SourceRatings 
                (Username, AverageSpeed, SuccessRate, TotalDownloads, FailedDownloads, DisconnectionCount, LastSeen, Score)
                VALUES (@Username, @AverageSpeed, @SuccessRate, @TotalDownloads, @FailedDownloads, @DisconnectionCount, @LastSeen, @Score)
            ";
            await connection.ExecuteAsync(sql, rating);
        }

        public async Task<SourceRating> GetSourceRatingAsync(string username)
        {
            var sql = "SELECT * FROM SourceRatings WHERE Username = @Username";
            return await connection.QueryFirstOrDefaultAsync<SourceRating>(sql, new { Username = username });
        }

        public async Task<List<SourceRating>> GetTopSourcesAsync(int limit = 10)
        {
            var sql = "SELECT * FROM SourceRatings ORDER BY Score DESC LIMIT @Limit";
            var result = await connection.QueryAsync<SourceRating>(sql, new { Limit = limit });
            return result.ToList();
        }

        public async Task UpdateSourceStatsAsync(string username, bool success, double speed, bool disconnected)
        {
            var rating = await GetSourceRatingAsync(username) ?? new SourceRating
            {
                Username = username,
                TotalDownloads = 0,
                FailedDownloads = 0,
                AverageSpeed = 0,
                DisconnectionCount = 0,
                LastSeen = DateTime.UtcNow
            };

            rating.TotalDownloads++;
            if (!success) rating.FailedDownloads++;
            if (disconnected) rating.DisconnectionCount++;
            rating.LastSeen = DateTime.UtcNow;

            // Calcular velocidad promedio (media móvil)
            rating.AverageSpeed = (rating.AverageSpeed * (rating.TotalDownloads - 1) + speed) / rating.TotalDownloads;

            // Calcular tasa de éxito
            rating.SuccessRate = (rating.TotalDownloads - rating.FailedDownloads) / (double)rating.TotalDownloads;

            // Calcular score
            rating.Score = CalculateSourceScore(rating);

            await UpsertSourceRatingAsync(rating);
        }

        private double CalculateSourceScore(SourceRating rating)
        {
            var speedScore = Math.Min(rating.AverageSpeed / 5.0, 1.0) * 40;
            var successScore = rating.SuccessRate * 30;
            var reliabilityScore = (1.0 - Math.Min(rating.DisconnectionCount / (double)Math.Max(rating.TotalDownloads, 1), 1.0)) * 20;
            var recencyScore = CalculateRecencyScore(rating.LastSeen) * 10;

            return speedScore + successScore + reliabilityScore + recencyScore;
        }

        private double CalculateRecencyScore(DateTime lastSeen)
        {
            var daysSince = (DateTime.UtcNow - lastSeen).TotalDays;
            if (daysSince < 1) return 1.0;
            if (daysSince < 7) return 0.8;
            if (daysSince < 30) return 0.5;
            return 0.2;
        }

        // ===== OPERACIONES DE WATCHLIST =====

        public async Task<long> AddToWatchlistAsync(WatchlistEntry entry)
        {
            var sql = @"
                INSERT INTO Watchlist (Author, Enabled, AutoDownload, NotifyOnNew, SearchIntervalHours, LastSearch, CreatedAt)
                VALUES (@Author, @Enabled, @AutoDownload, @NotifyOnNew, @SearchIntervalHours, @LastSearch, @CreatedAt);
                SELECT last_insert_rowid();
            ";
            return await connection.ExecuteScalarAsync<long>(sql, entry);
        }

        public async Task<List<WatchlistEntry>> GetWatchlistAsync()
        {
            var sql = "SELECT * FROM Watchlist ORDER BY Author";
            var result = await connection.QueryAsync<WatchlistEntry>(sql);
            return result.ToList();
        }

        public async Task<List<WatchlistEntry>> GetActiveWatchlistAsync()
        {
            var sql = @"
                SELECT * FROM Watchlist 
                WHERE Enabled = 1 
                AND datetime(LastSearch, '+' || SearchIntervalHours || ' hours') < datetime('now')
                ORDER BY LastSearch ASC
            ";
            var result = await connection.QueryAsync<WatchlistEntry>(sql);
            return result.ToList();
        }

        public async Task UpdateWatchlistLastSearchAsync(long id)
        {
            var sql = "UPDATE Watchlist SET LastSearch = @Now WHERE Id = @Id";
            await connection.ExecuteAsync(sql, new { Id = id, Now = DateTime.UtcNow });
        }

        public async Task RemoveFromWatchlistAsync(long id)
        {
            var sql = "DELETE FROM Watchlist WHERE Id = @Id";
            await connection.ExecuteAsync(sql, new { Id = id });
        }

        // ===== OPERACIONES DE HASHES DE ARCHIVOS =====

        public async Task<long> InsertFileHashAsync(string filePath, string fileName, long size, string md5Hash, string sha1Hash)
        {
            var sql = @"
                INSERT OR REPLACE INTO FileHashes (FilePath, FileName, Size, MD5Hash, SHA1Hash, LastModified)
                VALUES (@FilePath, @FileName, @Size, @MD5Hash, @SHA1Hash, @LastModified);
                SELECT last_insert_rowid();
            ";
            return await connection.ExecuteScalarAsync<long>(sql, new
            {
                FilePath = filePath,
                FileName = fileName,
                Size = size,
                MD5Hash = md5Hash,
                SHA1Hash = sha1Hash,
                LastModified = DateTime.UtcNow
            });
        }

        public async Task<dynamic> FindFileByHashAsync(string md5Hash)
        {
            var sql = "SELECT * FROM FileHashes WHERE MD5Hash = @MD5Hash LIMIT 1";
            return await connection.QueryFirstOrDefaultAsync(sql, new { MD5Hash = md5Hash });
        }

        public async Task<List<dynamic>> FindFilesBySizeAsync(long size)
        {
            var sql = "SELECT * FROM FileHashes WHERE Size = @Size";
            var result = await connection.QueryAsync(sql, new { Size = size });
            return result.ToList();
        }

        // ===== ESTADÍSTICAS =====

        public async Task<Dictionary<string, object>> GetStatisticsAsync()
        {
            var stats = new Dictionary<string, object>();

            // Total de descargas
            stats["TotalDownloads"] = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Downloads");

            // Total de GB descargados
            var totalBytes = await connection.ExecuteScalarAsync<long>("SELECT IFNULL(SUM(SizeBytes), 0) FROM Downloads WHERE Status = 'Completed'");
            stats["TotalGB"] = totalBytes / (1024.0 * 1024.0 * 1024.0);

            // Velocidad promedio
            stats["AverageSpeed"] = await connection.ExecuteScalarAsync<double>("SELECT IFNULL(AVG(Speed), 0) FROM Downloads WHERE Speed IS NOT NULL");

            // Tasa de éxito
            var completed = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Downloads WHERE Status = 'Completed'");
            var total = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Downloads");
            stats["SuccessRate"] = total > 0 ? (completed / (double)total) * 100 : 0;

            // Top 5 autores
            var topAuthors = await connection.QueryAsync<dynamic>(@"
                SELECT Author, COUNT(*) as Count 
                FROM Downloads 
                WHERE Author IS NOT NULL 
                GROUP BY Author 
                ORDER BY Count DESC 
                LIMIT 5
            ");
            stats["TopAuthors"] = topAuthors.ToList();

            return stats;
        }

        // ===== BATCH WRITES =====
        
        /// <summary>
        /// Agrega una operación de escritura a la cola de batch
        /// </summary>
        public void QueueBatchWrite(string sql, object parameters)
        {
            lock (batchLock)
            {
                batchQueue.Enqueue((sql, parameters));
            }
        }
        
        /// <summary>
        /// Ejecuta todas las escrituras pendientes en un solo batch
        /// </summary>
        public async Task FlushBatchWritesAsync()
        {
            List<(string sql, object parameters)> batch;
            
            lock (batchLock)
            {
                if (batchQueue.Count == 0)
                    return;
                    
                // Tomar hasta BATCH_SIZE operaciones
                batch = new List<(string, object)>();
                while (batchQueue.Count > 0 && batch.Count < BATCH_SIZE)
                {
                    batch.Add(batchQueue.Dequeue());
                }
            }
            
            if (batch.Count == 0)
                return;
            
            // Ejecutar batch en una transacción para máximo rendimiento
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    foreach (var (sql, parameters) in batch)
                    {
                        await connection.ExecuteAsync(sql, parameters, transaction);
                    }
                    
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
        
        /// <summary>
        /// Obtiene el número de operaciones pendientes en la cola
        /// </summary>
        public int GetPendingBatchCount()
        {
            lock (batchLock)
            {
                return batchQueue.Count;
            }
        }

        // ===== OPERACIONES DE CHECKSUMS =====

        /// <summary>
        /// Guarda o actualiza el checksum de un archivo
        /// </summary>
        public async Task<long> SaveChecksumAsync(string filePath, string fileName, long sizeBytes, string sha256Checksum, long? downloadId = null)
        {
            if (!File.Exists(filePath))
                return -1;

            var fileInfo = new FileInfo(filePath);
            var lastModified = fileInfo.LastWriteTimeUtc;

            var sql = @"
                INSERT INTO FileChecksums (FilePath, FileName, SizeBytes, SHA256Checksum, VerifiedAt, LastModified, IsValid, DownloadId)
                VALUES (@FilePath, @FileName, @SizeBytes, @SHA256Checksum, @VerifiedAt, @LastModified, 1, @DownloadId)
                ON CONFLICT(FilePath, LastModified) DO UPDATE SET
                    SHA256Checksum = @SHA256Checksum,
                    VerifiedAt = @VerifiedAt,
                    IsValid = 1;
                SELECT last_insert_rowid();
            ";

            return await connection.ExecuteScalarAsync<long>(sql, new
            {
                FilePath = filePath,
                FileName = fileName,
                SizeBytes = sizeBytes,
                SHA256Checksum = sha256Checksum,
                VerifiedAt = DateTime.UtcNow,
                LastModified = lastModified,
                DownloadId = downloadId
            });
        }

        /// <summary>
        /// Obtiene el checksum guardado de un archivo (si existe y es válido)
        /// </summary>
        public async Task<string> GetChecksumAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            var fileInfo = new FileInfo(filePath);
            var lastModified = fileInfo.LastWriteTimeUtc;

            var sql = @"
                SELECT SHA256Checksum 
                FROM FileChecksums 
                WHERE FilePath = @FilePath 
                  AND LastModified = @LastModified 
                  AND IsValid = 1
                ORDER BY VerifiedAt DESC 
                LIMIT 1
            ";

            return await connection.QueryFirstOrDefaultAsync<string>(sql, new
            {
                FilePath = filePath,
                LastModified = lastModified
            });
        }

        /// <summary>
        /// Verifica si un archivo tiene un checksum válido guardado
        /// </summary>
        public async Task<bool> HasValidChecksumAsync(string filePath)
        {
            var checksum = await GetChecksumAsync(filePath);
            return !string.IsNullOrEmpty(checksum);
        }

        /// <summary>
        /// Marca un checksum como inválido (archivo corrupto)
        /// </summary>
        public async Task MarkChecksumInvalidAsync(string filePath)
        {
            var sql = @"
                UPDATE FileChecksums 
                SET IsValid = 0 
                WHERE FilePath = @FilePath
            ";

            await connection.ExecuteAsync(sql, new { FilePath = filePath });
        }

        /// <summary>
        /// Elimina checksums de archivos que ya no existen
        /// </summary>
        public async Task<int> CleanupOrphanedChecksumsAsync()
        {
            var sql = "SELECT FilePath FROM FileChecksums";
            var allPaths = await connection.QueryAsync<string>(sql);

            var orphanedPaths = allPaths.Where(path => !File.Exists(path)).ToList();

            if (orphanedPaths.Count == 0)
                return 0;

            var deleteSql = "DELETE FROM FileChecksums WHERE FilePath = @FilePath";
            return await connection.ExecuteAsync(deleteSql, orphanedPaths.Select(p => new { FilePath = p }));
        }

        /// <summary>
        /// Obtiene estadísticas de checksums
        /// </summary>
        public async Task<(int total, int valid, int invalid, long oldestDays)> GetChecksumStatsAsync()
        {
            var sql = @"
                SELECT 
                    COUNT(*) as Total,
                    SUM(CASE WHEN IsValid = 1 THEN 1 ELSE 0 END) as Valid,
                    SUM(CASE WHEN IsValid = 0 THEN 1 ELSE 0 END) as Invalid,
                    CAST((julianday('now') - julianday(MIN(VerifiedAt))) AS INTEGER) as OldestDays
                FROM FileChecksums
            ";

            var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql);
            return (
                total: result?.Total ?? 0,
                valid: result?.Valid ?? 0,
                invalid: result?.Invalid ?? 0,
                oldestDays: result?.OldestDays ?? 0
            );
        }

        // ===== UTILIDADES =====

        public async Task VacuumAsync()
        {
            await connection.ExecuteAsync("VACUUM");
        }

        public async Task<long> GetDatabaseSizeAsync()
        {
            var dbPath = connection.DataSource;
            return await Task.Run(() => new FileInfo(dbPath).Length);
        }

        public void Dispose()
        {
            // Flush cualquier operación pendiente antes de cerrar (sin bloquear)
            try
            {
                FlushBatchWritesAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch { }
            
            connection?.Close();
            connection?.Dispose();
        }
    }
}
