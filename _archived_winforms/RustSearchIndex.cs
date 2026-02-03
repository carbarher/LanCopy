using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SlskDown.Core
{
    /// <summary>
    /// Índice de búsqueda full-text ultra-rápido con Rust
    /// Perfecto para indexar y buscar en miles de archivos/autores
    /// </summary>
    public class RustSearchIndex : IDisposable
    {
        private const string DLL_NAME = "slskdown_core.dll";
        private int _indexId = -1;
        private bool _disposed = false;

        // ==================== FFI IMPORTS ====================

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int create_search_index();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int index_add_document(
            int index_id,
            int doc_id,
            [MarshalAs(UnmanagedType.LPStr)] string text
        );

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr index_search(
            int index_id,
            [MarshalAs(UnmanagedType.LPStr)] string query
        );

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int index_clear(int index_id);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int index_destroy(int index_id);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr fuzzy_search(
            int index_id,
            [MarshalAs(UnmanagedType.LPStr)] string query,
            int max_distance
        );

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr ranked_search(
            int index_id,
            [MarshalAs(UnmanagedType.LPStr)] string query,
            int top_n
        );

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void free_rust_string(IntPtr ptr);

        // ==================== ESTRUCTURAS ====================

        public class ScoredResult
        {
            [JsonProperty("doc_id")]
            public int DocId { get; set; }

            [JsonProperty("score")]
            public double Score { get; set; }

            [JsonProperty("snippet")]
            public string Snippet { get; set; } = "";
        }

        // ==================== CONSTRUCTOR ====================

        public RustSearchIndex()
        {
            if (!IsRustAvailable())
            {
                throw new InvalidOperationException("Rust library not available");
            }

            try
            {
                _indexId = create_search_index();
                if (_indexId < 0)
                {
                    throw new InvalidOperationException("Failed to create search index");
                }
            }
            catch (DllNotFoundException)
            {
                throw new InvalidOperationException("slskdown_core.dll not found");
            }
        }

        // ==================== HELPERS ====================

        private static string? PtrToStringAndFree(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return null;

            try
            {
                return Marshal.PtrToStringAnsi(ptr);
            }
            finally
            {
                free_rust_string(ptr);
            }
        }

        private static bool _rustAvailable = true;

        public static bool IsRustAvailable()
        {
            if (!_rustAvailable) return false;

            try
            {
                int testId = create_search_index();
                if (testId >= 0)
                {
                    index_destroy(testId);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                if (ex is DllNotFoundException || ex is EntryPointNotFoundException || ex is BadImageFormatException)
                {
                    _rustAvailable = false;
                }
                return false;
            }
        }

        // ==================== API PÚBLICA ====================

        /// <summary>
        /// Agrega un documento al índice
        /// </summary>
        /// <param name="docId">ID único del documento</param>
        /// <param name="text">Texto a indexar</param>
        public void AddDocument(int docId, string text)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(text))
                return;

            index_add_document(_indexId, docId, text);
        }

        /// <summary>
        /// Busca en el índice
        /// Retorna IDs de documentos que coinciden con TODOS los términos de búsqueda (AND)
        /// </summary>
        public List<int> Search(string query)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(query))
                return new List<int>();

            try
            {
                IntPtr ptr = index_search(_indexId, query);
                string? json = PtrToStringAndFree(ptr);

                if (json == null)
                    return new List<int>();

                return JsonConvert.DeserializeObject<List<int>>(json)
                    ?? new List<int>();
            }
            catch
            {
                return new List<int>();
            }
        }

        /// <summary>
        /// Búsqueda fuzzy (tolerante a errores tipográficos)
        /// </summary>
        /// <param name="query">Consulta de búsqueda</param>
        /// <param name="maxDistance">Distancia de Levenshtein máxima (1-3 recomendado)</param>
        /// <returns>Lista de (DocId, Distance) ordenada por relevancia</returns>
        public List<(int DocId, int Distance)> FuzzySearch(string query, int maxDistance = 2)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(query))
                return new List<(int, int)>();

            try
            {
                IntPtr ptr = fuzzy_search(_indexId, query, maxDistance);
                string? json = PtrToStringAndFree(ptr);

                if (json == null)
                    return new List<(int, int)>();

                var rawResults = JsonConvert.DeserializeObject<List<List<int>>>(json);
                if (rawResults == null)
                    return new List<(int, int)>();

                var results = new List<(int, int)>();
                foreach (var item in rawResults)
                {
                    if (item.Count >= 2)
                    {
                        results.Add((item[0], item[1]));
                    }
                }

                return results;
            }
            catch
            {
                return new List<(int, int)>();
            }
        }

        /// <summary>
        /// Búsqueda con ranking por relevancia (TF-IDF simplificado)
        /// </summary>
        /// <param name="query">Consulta de búsqueda</param>
        /// <param name="topN">Número máximo de resultados</param>
        /// <returns>Lista de resultados con score y snippet</returns>
        public List<ScoredResult> RankedSearch(string query, int topN = 10)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(query))
                return new List<ScoredResult>();

            try
            {
                IntPtr ptr = ranked_search(_indexId, query, topN);
                string? json = PtrToStringAndFree(ptr);

                if (json == null)
                    return new List<ScoredResult>();

                return JsonConvert.DeserializeObject<List<ScoredResult>>(json)
                    ?? new List<ScoredResult>();
            }
            catch
            {
                return new List<ScoredResult>();
            }
        }

        /// <summary>
        /// Limpia todos los documentos del índice
        /// </summary>
        public void Clear()
        {
            ThrowIfDisposed();
            index_clear(_indexId);
        }

        /// <summary>
        /// Indexa múltiples documentos en batch
        /// </summary>
        public void AddDocumentsBatch(Dictionary<int, string> documents)
        {
            ThrowIfDisposed();

            foreach (var kvp in documents)
            {
                AddDocument(kvp.Key, kvp.Value);
            }
        }

        // ==================== DISPOSE ====================

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RustSearchIndex));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_indexId >= 0)
            {
                try
                {
                    index_destroy(_indexId);
                }
                catch { }
                
                _indexId = -1;
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~RustSearchIndex()
        {
            Dispose();
        }
    }

    /// <summary>
    /// Helper para crear índices de autores y archivos fácilmente
    /// </summary>
    public static class SearchIndexHelpers
    {
        /// <summary>
        /// Crea índice de autores para búsqueda rápida
        /// </summary>
        public static RustSearchIndex CreateAuthorIndex(List<string> authors)
        {
            if (!RustSearchIndex.IsRustAvailable())
                throw new InvalidOperationException("Rust not available");

            var index = new RustSearchIndex();
            
            for (int i = 0; i < authors.Count; i++)
            {
                index.AddDocument(i, authors[i]);
            }

            return index;
        }

        /// <summary>
        /// Crea índice de archivos para búsqueda rápida
        /// </summary>
        public static RustSearchIndex CreateFileIndex(List<string> filePaths)
        {
            if (!RustSearchIndex.IsRustAvailable())
                throw new InvalidOperationException("Rust not available");

            var index = new RustSearchIndex();
            
            for (int i = 0; i < filePaths.Count; i++)
            {
                string fileName = System.IO.Path.GetFileNameWithoutExtension(filePaths[i]);
                index.AddDocument(i, fileName);
            }

            return index;
        }

        /// <summary>
        /// Busca autor permitiendo errores tipográficos
        /// </summary>
        public static List<string> FindSimilarAuthors(
            RustSearchIndex authorIndex,
            List<string> allAuthors,
            string query,
            int maxResults = 10
        )
        {
            // Primero buscar coincidencia exacta
            var exactMatches = authorIndex.Search(query);
            if (exactMatches.Count > 0)
            {
                return exactMatches
                    .Take(maxResults)
                    .Select(id => id < allAuthors.Count ? allAuthors[id] : "")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }

            // Si no hay coincidencia exacta, buscar fuzzy
            var fuzzyResults = authorIndex.FuzzySearch(query, maxDistance: 2);
            
            return fuzzyResults
                .Take(maxResults)
                .Select(result => result.DocId < allAuthors.Count ? allAuthors[result.DocId] : "")
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }
    }
}
