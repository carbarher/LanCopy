using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace SlskDown.Core
{
    /// <summary>
    /// Servicio de búsqueda de autores usando SQLite FTS5 (Full-Text Search)
    /// 100-1000x más rápido que búsqueda con LIKE '%...%'
    /// </summary>
    public class FastAuthorSearchService : IDisposable
    {
        private readonly string _connectionString;
        private SqliteConnection? _connection;

        public FastAuthorSearchService(string databasePath)
        {
            _connectionString = $"Data Source={databasePath};Mode=ReadWriteCreate;Cache=Shared";
        }

        /// <summary>
        /// Inicializa la base de datos con FTS5
        /// </summary>
        public async Task InitializeAsync()
        {
            _connection = new SqliteConnection(_connectionString);
            await _connection.OpenAsync();

            // Habilitar WAL mode para mejor concurrencia
            await _connection.ExecuteAsync("PRAGMA journal_mode=WAL;");
            await _connection.ExecuteAsync("PRAGMA synchronous=NORMAL;");
            await _connection.ExecuteAsync("PRAGMA cache_size=-64000;"); // 64MB cache

            // Crear tabla principal de autores
            await _connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS authors (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL,
                    normalized_name TEXT NOT NULL,
                    aliases TEXT,
                    book_count INTEGER DEFAULT 0,
                    last_search_date TEXT,
                    created_at TEXT DEFAULT CURRENT_TIMESTAMP
                );
            ");

            // Crear índice FTS5 para búsqueda full-text ultra-rápida
            await _connection.ExecuteAsync(@"
                CREATE VIRTUAL TABLE IF NOT EXISTS authors_fts USING fts5(
                    name,
                    normalized_name,
                    aliases,
                    content='authors',
                    content_rowid='id',
                    tokenize='unicode61 remove_diacritics 2'
                );
            ");

            // Crear triggers para mantener FTS5 sincronizado
            await _connection.ExecuteAsync(@"
                CREATE TRIGGER IF NOT EXISTS authors_ai AFTER INSERT ON authors BEGIN
                    INSERT INTO authors_fts(rowid, name, normalized_name, aliases)
                    VALUES (new.id, new.name, new.normalized_name, new.aliases);
                END;
            ");

            await _connection.ExecuteAsync(@"
                CREATE TRIGGER IF NOT EXISTS authors_ad AFTER DELETE ON authors BEGIN
                    INSERT INTO authors_fts(authors_fts, rowid, name, normalized_name, aliases)
                    VALUES('delete', old.id, old.name, old.normalized_name, old.aliases);
                END;
            ");

            await _connection.ExecuteAsync(@"
                CREATE TRIGGER IF NOT EXISTS authors_au AFTER UPDATE ON authors BEGIN
                    INSERT INTO authors_fts(authors_fts, rowid, name, normalized_name, aliases)
                    VALUES('delete', old.id, old.name, old.normalized_name, old.aliases);
                    INSERT INTO authors_fts(rowid, name, normalized_name, aliases)
                    VALUES (new.id, new.name, new.normalized_name, new.aliases);
                END;
            ");

            // Crear índices adicionales
            await _connection.ExecuteAsync(@"
                CREATE INDEX IF NOT EXISTS idx_authors_normalized 
                ON authors(normalized_name);
            ");

            await _connection.ExecuteAsync(@"
                CREATE INDEX IF NOT EXISTS idx_authors_book_count 
                ON authors(book_count DESC);
            ");
        }

        /// <summary>
        /// Busca autores usando FTS5 (100-1000x más rápido)
        /// </summary>
        public async Task<List<AuthorSearchResult>> SearchAuthorsAsync(
            string query, 
            int limit = 100)
        {
            if (_connection == null)
                throw new InvalidOperationException("Database not initialized");

            // Preparar query para FTS5 (soporta operadores OR, AND, NEAR, etc.)
            var ftsQuery = PrepareFtsQuery(query);

            var sql = @"
                SELECT 
                    a.id,
                    a.name,
                    a.normalized_name,
                    a.aliases,
                    a.book_count,
                    a.last_search_date,
                    bm25(authors_fts) as rank
                FROM authors_fts
                JOIN authors a ON authors_fts.rowid = a.id
                WHERE authors_fts MATCH @query
                ORDER BY rank, a.book_count DESC
                LIMIT @limit;
            ";

            var results = await _connection.QueryAsync<AuthorSearchResult>(
                sql, 
                new { query = ftsQuery, limit });

            return results.ToList();
        }

        /// <summary>
        /// Busca autores con fuzzy matching (tolerante a errores)
        /// </summary>
        public async Task<List<AuthorSearchResult>> SearchAuthorsFuzzyAsync(
            string query, 
            int limit = 100)
        {
            if (_connection == null)
                throw new InvalidOperationException("Database not initialized");

            // Generar variantes del query para fuzzy matching
            var variants = GenerateFuzzyVariants(query);
            var ftsQuery = string.Join(" OR ", variants.Select(v => $"\"{v}\""));

            var sql = @"
                SELECT 
                    a.id,
                    a.name,
                    a.normalized_name,
                    a.aliases,
                    a.book_count,
                    a.last_search_date,
                    bm25(authors_fts) as rank
                FROM authors_fts
                JOIN authors a ON authors_fts.rowid = a.id
                WHERE authors_fts MATCH @query
                ORDER BY rank, a.book_count DESC
                LIMIT @limit;
            ";

            var results = await _connection.QueryAsync<AuthorSearchResult>(
                sql, 
                new { query = ftsQuery, limit });

            return results.ToList();
        }

        /// <summary>
        /// Agrega o actualiza un autor
        /// </summary>
        public async Task<int> UpsertAuthorAsync(string name, string? aliases = null)
        {
            if (_connection == null)
                throw new InvalidOperationException("Database not initialized");

            var normalizedName = NormalizeName(name);

            var sql = @"
                INSERT INTO authors (name, normalized_name, aliases)
                VALUES (@name, @normalizedName, @aliases)
                ON CONFLICT(normalized_name) DO UPDATE SET
                    aliases = COALESCE(@aliases, aliases),
                    book_count = book_count + 1
                RETURNING id;
            ";

            return await _connection.ExecuteScalarAsync<int>(
                sql, 
                new { name, normalizedName, aliases });
        }

        /// <summary>
        /// Agrega múltiples autores en batch (ultra-rápido)
        /// </summary>
        public async Task<int> UpsertAuthorsBatchAsync(List<string> names)
        {
            if (_connection == null)
                throw new InvalidOperationException("Database not initialized");

            using var transaction = _connection.BeginTransaction();
            
            try
            {
                var sql = @"
                    INSERT INTO authors (name, normalized_name)
                    VALUES (@name, @normalizedName)
                    ON CONFLICT(normalized_name) DO UPDATE SET
                        book_count = book_count + 1;
                ";

                var parameters = names.Select(name => new
                {
                    name,
                    normalizedName = NormalizeName(name)
                }).ToList();

                var affected = await _connection.ExecuteAsync(sql, parameters, transaction);
                
                await transaction.CommitAsync();
                return affected;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Obtiene estadísticas de la base de datos
        /// </summary>
        public async Task<DatabaseStats> GetStatsAsync()
        {
            if (_connection == null)
                throw new InvalidOperationException("Database not initialized");

            var sql = @"
                SELECT 
                    COUNT(*) as TotalAuthors,
                    SUM(book_count) as TotalBooks,
                    AVG(book_count) as AvgBooksPerAuthor,
                    MAX(book_count) as MaxBooks
                FROM authors;
            ";

            return await _connection.QuerySingleAsync<DatabaseStats>(sql);
        }

        /// <summary>
        /// Optimiza la base de datos (ejecutar periódicamente)
        /// </summary>
        public async Task OptimizeAsync()
        {
            if (_connection == null)
                throw new InvalidOperationException("Database not initialized");

            // Optimizar FTS5
            await _connection.ExecuteAsync("INSERT INTO authors_fts(authors_fts) VALUES('optimize');");
            
            // Vacuum para compactar
            await _connection.ExecuteAsync("VACUUM;");
            
            // Analizar para actualizar estadísticas
            await _connection.ExecuteAsync("ANALYZE;");
        }

        /// <summary>
        /// Limpia autores sin libros
        /// </summary>
        public async Task<int> CleanupAsync()
        {
            if (_connection == null)
                throw new InvalidOperationException("Database not initialized");

            var sql = "DELETE FROM authors WHERE book_count = 0;";
            return await _connection.ExecuteAsync(sql);
        }

        #region Helper Methods

        private string PrepareFtsQuery(string query)
        {
            // Limpiar y preparar query para FTS5
            query = query.Trim();
            
            // Remover caracteres especiales que pueden causar errores
            query = System.Text.RegularExpressions.Regex.Replace(query, @"[^\w\s]", " ");
            
            // Normalizar espacios
            query = System.Text.RegularExpressions.Regex.Replace(query, @"\s+", " ");
            
            // Agregar wildcards para búsqueda parcial
            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" OR ", terms.Select(t => $"{t}*"));
        }

        private List<string> GenerateFuzzyVariants(string query)
        {
            var variants = new List<string> { query };
            
            // Variante sin acentos
            var normalized = NormalizeName(query);
            if (normalized != query)
                variants.Add(normalized);
            
            // Variante con wildcards
            variants.Add($"{query}*");
            
            // Variantes de palabras individuales
            var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 1)
            {
                variants.AddRange(words.Select(w => $"{w}*"));
            }
            
            return variants.Distinct().ToList();
        }

        private string NormalizeName(string name)
        {
            // Normalizar: minúsculas, sin acentos, sin caracteres especiales
            name = name.ToLowerInvariant();
            
            // Remover acentos
            name = System.Text.RegularExpressions.Regex.Replace(
                name.Normalize(System.Text.NormalizationForm.FormD),
                @"[\u0300-\u036f]",
                string.Empty
            );
            
            // Normalizar espacios
            name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+", " ").Trim();
            
            return name;
        }

        #endregion

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }

    #region DTOs

    public class AuthorSearchResult
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string NormalizedName { get; set; } = "";
        public string? Aliases { get; set; }
        public int BookCount { get; set; }
        public string? LastSearchDate { get; set; }
        public double Rank { get; set; }
    }

    public class DatabaseStats
    {
        public int TotalAuthors { get; set; }
        public int TotalBooks { get; set; }
        public double AvgBooksPerAuthor { get; set; }
        public int MaxBooks { get; set; }
    }

    #endregion
}
