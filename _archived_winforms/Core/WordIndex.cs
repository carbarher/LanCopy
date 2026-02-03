// <copyright file="WordIndex.cs" company="SlskDown">
//     Índice invertido de palabras para búsquedas O(1)
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SlskDown.Core
{
    /// <summary>
    /// Índice invertido de palabras para búsquedas ultrarrápidas.
    /// Inspirado en el word_index de Nicotine+.
    /// </summary>
    public class WordIndex
    {
        private readonly Dictionary<string, HashSet<int>> _index = new();
        private readonly Dictionary<int, string> _idToPath = new();
        private int _nextId = 0;

        /// <summary>
        /// Número de palabras únicas indexadas
        /// </summary>
        public int WordCount => _index.Count;
        
        /// <summary>
        /// Número de documentos indexados
        /// </summary>
        public int DocumentCount => _idToPath.Count;

        /// <summary>
        /// Agrega un path al índice.
        /// </summary>
        public int Add(string path)
        {
            var id = _nextId++;
            _idToPath[id] = path;

            var words = TokenizePath(path);
            foreach (var word in words)
            {
                if (!_index.ContainsKey(word))
                    _index[word] = new HashSet<int>();
                
                _index[word].Add(id);
            }

            return id;
        }

        /// <summary>
        /// Busca paths que contengan todas las palabras.
        /// </summary>
        public List<string> Search(string query)
        {
            var words = TokenizeQuery(query);
            if (words.Count == 0)
                return new List<string>();

            // Obtener IDs que contienen la primera palabra
            if (!_index.TryGetValue(words[0], out var resultIds))
                return new List<string>();

            var results = new HashSet<int>(resultIds);

            // Intersectar con IDs de las demás palabras
            for (int i = 1; i < words.Count; i++)
            {
                if (!_index.TryGetValue(words[i], out var wordIds))
                    return new List<string>(); // Si falta una palabra, no hay resultados

                results.IntersectWith(wordIds);
                
                if (results.Count == 0)
                    return new List<string>();
            }

            // Convertir IDs a paths
            return results.Select(id => _idToPath[id]).ToList();
        }

        /// <summary>
        /// Busca IDs que contengan todas las palabras (más eficiente que Search).
        /// </summary>
        public List<int> SearchIds(string query)
        {
            var words = TokenizeQuery(query);
            if (words.Count == 0)
                return new List<int>();

            // Obtener IDs que contienen la primera palabra
            if (!_index.TryGetValue(words[0], out var resultIds))
                return new List<int>();

            var results = new HashSet<int>(resultIds);

            // Intersectar con IDs de las demás palabras
            for (int i = 1; i < words.Count; i++)
            {
                if (!_index.TryGetValue(words[i], out var wordIds))
                    return new List<int>(); // Si falta una palabra, no hay resultados

                results.IntersectWith(wordIds);
                
                if (results.Count == 0)
                    return new List<int>();
            }

            return results.ToList();
        }

        /// <summary>
        /// Agrega un path con un ID específico (útil para sincronizar con índices externos).
        /// </summary>
        public void AddWithId(int id, string path)
        {
            _idToPath[id] = path;
            if (id >= _nextId)
                _nextId = id + 1;

            var words = TokenizePath(path);
            foreach (var word in words)
            {
                if (!_index.ContainsKey(word))
                    _index[word] = new HashSet<int>();
                
                _index[word].Add(id);
            }
        }

        /// <summary>
        /// Busca paths que contengan al menos una palabra.
        /// </summary>
        public List<string> SearchAny(string query)
        {
            var words = TokenizeQuery(query);
            if (words.Count == 0)
                return new List<string>();

            var results = new HashSet<int>();

            foreach (var word in words)
            {
                if (_index.TryGetValue(word, out var wordIds))
                    results.UnionWith(wordIds);
            }

            return results.Select(id => _idToPath[id]).ToList();
        }

        /// <summary>
        /// Elimina un path del índice.
        /// </summary>
        public void Remove(int id)
        {
            if (!_idToPath.TryGetValue(id, out var path))
                return;

            var words = TokenizePath(path);
            foreach (var word in words)
            {
                if (_index.TryGetValue(word, out var ids))
                {
                    ids.Remove(id);
                    if (ids.Count == 0)
                        _index.Remove(word);
                }
            }

            _idToPath.Remove(id);
        }

        /// <summary>
        /// Limpia todo el índice.
        /// </summary>
        public void Clear()
        {
            _index.Clear();
            _idToPath.Clear();
            _nextId = 0;
        }

        /// <summary>
        /// Obtiene estadísticas del índice.
        /// </summary>
        public (int TotalPaths, int TotalWords, int AvgPathsPerWord) GetStats()
        {
            var totalPaths = _idToPath.Count;
            var totalWords = _index.Count;
            var avgPathsPerWord = totalWords > 0 
                ? (int)_index.Values.Average(ids => ids.Count) 
                : 0;

            return (totalPaths, totalWords, avgPathsPerWord);
        }

        /// <summary>
        /// Tokeniza un path en palabras individuales.
        /// Similar a Nicotine+ pero adaptado para Windows.
        /// </summary>
        private List<string> TokenizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return new List<string>();

            // Normalizar a lowercase
            var normalized = path.ToLowerInvariant();

            // Reemplazar separadores y caracteres especiales por espacios
            var sb = new StringBuilder(normalized);
            foreach (var ch in new[] { '\\', '/', '_', '-', '.', '(', ')', '[', ']', '{', '}' })
                sb.Replace(ch, ' ');

            // Split y filtrar
            return sb.ToString()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 2) // Ignorar palabras de 1 letra
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Tokeniza una query de búsqueda.
        /// </summary>
        private List<string> TokenizeQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<string>();

            return query.ToLowerInvariant()
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 2)
                .ToList();
        }
    }
}
