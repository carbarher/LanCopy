// <copyright file="PathCache.cs" company="SlskDown">
//     Caché de paths normalizados para evitar operaciones repetidas
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;

namespace SlskDown.Core
{
    /// <summary>
    /// Caché de paths normalizados para reducir llamadas a Path.GetFullPath().
    /// Inspirado en el normalized_paths cache de Nicotine+.
    /// </summary>
    public class PathCache
    {
        private readonly Dictionary<string, string> _normalizedCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _lowercaseCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _existsCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();
        private int _maxCacheSize = 10000;

        public int MaxCacheSize
        {
            get => _maxCacheSize;
            set => _maxCacheSize = Math.Max(100, value);
        }

        public int CachedCount => _normalizedCache.Count;
        
        /// <summary>
        /// Tamaño total del caché (todas las entradas)
        /// </summary>
        public int CacheSize
        {
            get
            {
                lock (_lock)
                {
                    return _normalizedCache.Count + _lowercaseCache.Count + _existsCache.Count;
                }
            }
        }

        /// <summary>
        /// Obtiene un path normalizado (caché o calcula).
        /// </summary>
        public string GetNormalized(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            lock (_lock)
            {
                if (_normalizedCache.TryGetValue(path, out var normalized))
                    return normalized;

                // Calcular y cachear
                try
                {
                    normalized = Path.GetFullPath(path);
                }
                catch
                {
                    normalized = path; // Fallback si falla
                }

                AddToCache(_normalizedCache, path, normalized);
                return normalized;
            }
        }

        /// <summary>
        /// Obtiene un path en lowercase (caché o calcula).
        /// </summary>
        public string GetLowercase(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            lock (_lock)
            {
                if (_lowercaseCache.TryGetValue(path, out var lowercase))
                    return lowercase;

                lowercase = path.ToLowerInvariant();
                AddToCache(_lowercaseCache, path, lowercase);
                return lowercase;
            }
        }

        /// <summary>
        /// Verifica si un path existe (con caché).
        /// </summary>
        public bool Exists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            lock (_lock)
            {
                if (_existsCache.TryGetValue(path, out var exists))
                    return exists;

                exists = File.Exists(path) || Directory.Exists(path);
                AddToCache(_existsCache, path, exists);
                return exists;
            }
        }

        /// <summary>
        /// Invalida el caché de existencia de un path.
        /// Útil después de crear/eliminar archivos.
        /// </summary>
        public void InvalidateExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            lock (_lock)
            {
                _existsCache.Remove(path);
            }
        }

        /// <summary>
        /// Limpia todos los cachés.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _normalizedCache.Clear();
                _lowercaseCache.Clear();
                _existsCache.Clear();
            }
        }

        /// <summary>
        /// Limpia el caché de existencia (útil después de operaciones de archivos).
        /// </summary>
        public void ClearExistsCache()
        {
            lock (_lock)
            {
                _existsCache.Clear();
            }
        }

        /// <summary>
        /// Obtiene estadísticas del caché.
        /// </summary>
        public (int Normalized, int Lowercase, int Exists) GetStats()
        {
            lock (_lock)
            {
                return (_normalizedCache.Count, _lowercaseCache.Count, _existsCache.Count);
            }
        }

        private void AddToCache<T>(Dictionary<string, T> cache, string key, T value)
        {
            // Si el caché está lleno, limpiar la mitad más antigua
            if (cache.Count >= _maxCacheSize)
            {
                var toRemove = cache.Count / 2;
                var keysToRemove = new List<string>();
                
                foreach (var k in cache.Keys)
                {
                    keysToRemove.Add(k);
                    if (keysToRemove.Count >= toRemove)
                        break;
                }

                foreach (var k in keysToRemove)
                    cache.Remove(k);
            }

            cache[key] = value;
        }
    }
}
