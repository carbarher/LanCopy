// <copyright file="CachedDatabase.cs" company="SlskDown">
//     Base de datos genérica con caché en memoria y estadísticas
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace SlskDown.Core
{
    /// <summary>
    /// Base de datos genérica con caché en memoria, persistencia en disco y estadísticas.
    /// Wrapper sobre MappedDatabase con funcionalidad de caché tipo Nicotine+.
    /// </summary>
    public class MappedDatabase<TKey, TValue>
    {
        private readonly Dictionary<TKey, CacheEntry<TValue>> _cache = new();
        private readonly string _filePath;
        private readonly long _maxSizeBytes;
        private long _currentSizeBytes = 0;
        
        // Estadísticas
        private long _totalHits = 0;
        private long _totalMisses = 0;
        private long _totalSets = 0;
        private long _totalEvictions = 0;
        
        private class CacheEntry<T>
        {
            public T Value { get; set; }
            public DateTime Timestamp { get; set; }
            public long SizeBytes { get; set; }
        }
        
        public MappedDatabase(string filePath, int maxSizeMB = 100)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _maxSizeBytes = maxSizeMB * 1024L * 1024L;
            
            // Cargar caché desde disco si existe
            LoadFromDisk();
        }
        
        /// <summary>
        /// Intenta obtener un valor del caché
        /// </summary>
        public bool TryGet(TKey key, out TValue value)
        {
            lock (_cache)
            {
                if (_cache.TryGetValue(key, out var entry))
                {
                    _totalHits++;
                    value = entry.Value;
                    return true;
                }
                
                _totalMisses++;
                value = default;
                return false;
            }
        }
        
        /// <summary>
        /// Establece un valor en el caché
        /// </summary>
        public void Set(TKey key, TValue value)
        {
            lock (_cache)
            {
                var json = JsonConvert.SerializeObject(value);
                var sizeBytes = System.Text.Encoding.UTF8.GetByteCount(json);
                
                // Si ya existe, actualizar
                if (_cache.ContainsKey(key))
                {
                    _currentSizeBytes -= _cache[key].SizeBytes;
                    _cache.Remove(key);
                }
                
                // Evict si excede tamaño máximo
                while (_currentSizeBytes + sizeBytes > _maxSizeBytes && _cache.Count > 0)
                {
                    var oldest = _cache.OrderBy(kvp => kvp.Value.Timestamp).First();
                    _currentSizeBytes -= oldest.Value.SizeBytes;
                    _cache.Remove(oldest.Key);
                    _totalEvictions++;
                }
                
                _cache[key] = new CacheEntry<TValue>
                {
                    Value = value,
                    Timestamp = DateTime.UtcNow,
                    SizeBytes = sizeBytes
                };
                
                _currentSizeBytes += sizeBytes;
                _totalSets++;
                
                // Persistir a disco cada 10 sets
                if (_totalSets % 10 == 0)
                {
                    SaveToDisk();
                }
            }
        }
        
        /// <summary>
        /// Obtiene el timestamp de una entrada
        /// </summary>
        public DateTime GetTimestamp(TKey key)
        {
            lock (_cache)
            {
                return _cache.TryGetValue(key, out var entry) ? entry.Timestamp : DateTime.MinValue;
            }
        }
        
        /// <summary>
        /// Obtiene estadísticas del caché
        /// </summary>
        public CacheStats GetStats()
        {
            lock (_cache)
            {
                var total = _totalHits + _totalMisses;
                var hitRate = total > 0 ? (double)_totalHits / total : 0.0;
                
                return new CacheStats
                {
                    TotalEntries = _cache.Count,
                    SizeBytes = _currentSizeBytes,
                    SizeMB = _currentSizeBytes / (1024.0 * 1024.0),
                    MaxSizeMB = _maxSizeBytes / (1024.0 * 1024.0),
                    TotalHits = _totalHits,
                    TotalMisses = _totalMisses,
                    TotalSets = _totalSets,
                    TotalEvictions = _totalEvictions,
                    HitRate = hitRate
                };
            }
        }
        
        /// <summary>
        /// Limpia el caché
        /// </summary>
        public void Clear()
        {
            lock (_cache)
            {
                _cache.Clear();
                _currentSizeBytes = 0;
            }
        }
        
        private void LoadFromDisk()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return;
                
                var json = File.ReadAllText(_filePath);
                var data = JsonConvert.DeserializeObject<Dictionary<TKey, CacheEntry<TValue>>>(json);
                
                if (data != null)
                {
                    foreach (var kvp in data)
                    {
                        _cache[kvp.Key] = kvp.Value;
                        _currentSizeBytes += kvp.Value.SizeBytes;
                    }
                }
            }
            catch
            {
                // Ignorar errores de carga
            }
        }
        
        private void SaveToDisk()
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                
                var json = JsonConvert.SerializeObject(_cache, Formatting.None);
                File.WriteAllText(_filePath, json);
            }
            catch
            {
                // Ignorar errores de guardado
            }
        }
        
        public class CacheStats
        {
            public int TotalEntries { get; set; }
            public long SizeBytes { get; set; }
            public double SizeMB { get; set; }
            public double MaxSizeMB { get; set; }
            public long TotalHits { get; set; }
            public long TotalMisses { get; set; }
            public long TotalSets { get; set; }
            public long TotalEvictions { get; set; }
            public double HitRate { get; set; }
        }
    }
}
