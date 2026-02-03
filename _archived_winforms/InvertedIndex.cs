using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SlskDown
{
    /// <summary>
    /// Índice invertido para búsqueda ultra-rápida (100-1000x más rápido)
    /// Palabra → Lista de documentos que la contienen
    /// </summary>
    public class InvertedIndex<T>
    {
        // Índice: palabra → lista de documentos
        private readonly ConcurrentDictionary<string, HashSet<T>> index;
        
        // Mapeo: documento → palabras (para eliminación eficiente)
        private readonly ConcurrentDictionary<T, HashSet<string>> documentWords;
        
        // Función para extraer texto indexable del documento
        private readonly Func<T, string> textExtractor;
        
        // Configuración
        private readonly bool caseSensitive;
        private readonly int minWordLength;
        
        public InvertedIndex(Func<T, string> textExtractor, bool caseSensitive = false, int minWordLength = 2)
        {
            this.textExtractor = textExtractor ?? throw new ArgumentNullException(nameof(textExtractor));
            this.caseSensitive = caseSensitive;
            this.minWordLength = minWordLength;
            
            index = new ConcurrentDictionary<string, HashSet<T>>(
                Environment.ProcessorCount * 2,
                10000,
                caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase
            );
            
            documentWords = new ConcurrentDictionary<T, HashSet<string>>();
        }
        
        /// <summary>
        /// Agrega un documento al índice
        /// </summary>
        public void Add(T document)
        {
            if (document == null)
                return;
            
            string text = textExtractor(document);
            if (string.IsNullOrWhiteSpace(text))
                return;
            
            // Extraer palabras
            var words = ExtractWords(text);
            
            // Guardar palabras del documento
            documentWords[document] = words;
            
            // Agregar documento a cada palabra
            foreach (var word in words)
            {
                var docs = index.GetOrAdd(word, _ => new HashSet<T>());
                lock (docs)
                {
                    docs.Add(document);
                }
            }
        }
        
        /// <summary>
        /// Agrega múltiples documentos en batch (más eficiente)
        /// </summary>
        public void AddBatch(IEnumerable<T> documents)
        {
            if (documents == null)
                return;
            
            foreach (var doc in documents)
            {
                Add(doc);
            }
        }
        
        /// <summary>
        /// Elimina un documento del índice
        /// </summary>
        public void Remove(T document)
        {
            if (document == null || !documentWords.TryRemove(document, out var words))
                return;
            
            // Eliminar documento de cada palabra
            foreach (var word in words)
            {
                if (index.TryGetValue(word, out var docs))
                {
                    lock (docs)
                    {
                        docs.Remove(document);
                    }
                }
            }
        }
        
        /// <summary>
        /// Busca documentos que contengan TODAS las palabras (AND)
        /// </summary>
        public List<T> SearchAnd(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<T>();
            
            var queryWords = ExtractWords(query);
            if (queryWords.Count == 0)
                return new List<T>();
            
            // Obtener documentos de la primera palabra
            if (!index.TryGetValue(queryWords.First(), out var firstDocs))
                return new List<T>();
            
            HashSet<T> result;
            lock (firstDocs)
            {
                result = new HashSet<T>(firstDocs);
            }
            
            // Intersectar con documentos de las demás palabras
            foreach (var word in queryWords.Skip(1))
            {
                if (!index.TryGetValue(word, out var docs))
                    return new List<T>(); // Si falta una palabra, no hay resultados
                
                lock (docs)
                {
                    result.IntersectWith(docs);
                }
                
                if (result.Count == 0)
                    return new List<T>();
            }
            
            return result.ToList();
        }
        
        /// <summary>
        /// Busca documentos que contengan CUALQUIER palabra (OR)
        /// </summary>
        public List<T> SearchOr(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<T>();
            
            var queryWords = ExtractWords(query);
            if (queryWords.Count == 0)
                return new List<T>();
            
            var result = new HashSet<T>();
            
            foreach (var word in queryWords)
            {
                if (index.TryGetValue(word, out var docs))
                {
                    lock (docs)
                    {
                        result.UnionWith(docs);
                    }
                }
            }
            
            return result.ToList();
        }
        
        /// <summary>
        /// Busca con ranking por relevancia (más palabras coinciden = más relevante)
        /// </summary>
        public List<(T Document, int Score)> SearchRanked(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<(T, int)>();
            
            var queryWords = ExtractWords(query);
            if (queryWords.Count == 0)
                return new List<(T, int)>();
            
            var scores = new Dictionary<T, int>();
            
            foreach (var word in queryWords)
            {
                if (index.TryGetValue(word, out var docs))
                {
                    lock (docs)
                    {
                        foreach (var doc in docs)
                        {
                            scores[doc] = scores.GetValueOrDefault(doc, 0) + 1;
                        }
                    }
                }
            }
            
            return scores
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => (kvp.Key, kvp.Value))
                .ToList();
        }
        
        /// <summary>
        /// Extrae palabras del texto (tokenización)
        /// </summary>
        private HashSet<string> ExtractWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new HashSet<string>();
            
            // Normalizar
            if (!caseSensitive)
                text = text.ToLowerInvariant();
            
            // Extraer palabras (alfanumérico + guiones)
            var words = Regex.Matches(text, @"[\w-]+")
                .Cast<Match>()
                .Select(m => m.Value)
                .Where(w => w.Length >= minWordLength)
                .ToHashSet(caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
            
            return words;
        }
        
        /// <summary>
        /// Limpia el índice completamente
        /// </summary>
        public void Clear()
        {
            index.Clear();
            documentWords.Clear();
        }
        
        /// <summary>
        /// Obtiene estadísticas del índice
        /// </summary>
        public (int UniqueWords, int TotalDocuments, long EstimatedMemoryBytes) GetStats()
        {
            int uniqueWords = index.Count;
            int totalDocs = documentWords.Count;
            
            // Estimación de memoria: ~100 bytes por palabra + ~50 bytes por documento
            long estimatedMemory = (uniqueWords * 100L) + (totalDocs * 50L);
            
            return (uniqueWords, totalDocs, estimatedMemory);
        }
    }
}
