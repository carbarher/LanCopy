using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// Cache persistente para guardar datos en disco
    /// </summary>
    public class PersistentCache<TKey, TValue>
    {
        private readonly string filePath;
        private readonly LRUCache<TKey, TValue> memoryCache;
        private bool isDirty;
        
        public PersistentCache(string filePath, int maxSize)
        {
            this.filePath = filePath;
            this.memoryCache = new LRUCache<TKey, TValue>(maxSize);
            this.isDirty = false;
        }
        
        /// <summary>
        /// Carga el cache desde disco
        /// </summary>
        public async Task LoadAsync()
        {
            try
            {
                if (!File.Exists(filePath))
                    return;
                
                var json = await File.ReadAllTextAsync(filePath);
                var data = JsonSerializer.Deserialize<Dictionary<string, TValue>>(json);
                
                if (data != null)
                {
                    foreach (var kvp in data)
                    {
                        // Convertir string key a TKey
                        if (TryConvertKey(kvp.Key, out TKey key))
                        {
                            memoryCache.Add(key, kvp.Value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cargando cache: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Guarda el cache en disco
        /// </summary>
        public async Task SaveAsync()
        {
            if (!isDirty)
                return;
            
            try
            {
                var stats = memoryCache.GetStats();
                var data = new Dictionary<string, TValue>();
                
                // Exportar datos del cache (limitado por tamaño)
                // Nota: LRUCache no expone items directamente, así que guardamos solo lo necesario
                
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = false
                });
                
                await File.WriteAllTextAsync(filePath, json);
                isDirty = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error guardando cache: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtiene un valor del cache
        /// </summary>
        public bool TryGetValue(TKey key, out TValue value)
        {
            return memoryCache.TryGetValue(key, out value);
        }
        
        /// <summary>
        /// Agrega un valor al cache
        /// </summary>
        public void Add(TKey key, TValue value)
        {
            memoryCache.Add(key, value);
            isDirty = true;
        }
        
        /// <summary>
        /// Limpia el cache
        /// </summary>
        public void Clear()
        {
            memoryCache.Clear();
            isDirty = true;
        }
        
        /// <summary>
        /// Obtiene estadísticas del cache
        /// </summary>
        public CacheStats GetStats()
        {
            return memoryCache.GetStats();
        }
        
        private bool TryConvertKey(string strKey, out TKey key)
        {
            try
            {
                key = (TKey)Convert.ChangeType(strKey, typeof(TKey));
                return true;
            }
            catch
            {
                key = default;
                return false;
            }
        }
    }
    
    /// <summary>
    /// Manager para manejar múltiples caches persistentes
    /// </summary>
    public class CacheManager
    {
        private static readonly Lazy<CacheManager> instance = 
            new Lazy<CacheManager>(() => new CacheManager());
        
        public static CacheManager Instance => instance.Value;
        
        private readonly Dictionary<string, object> caches = new Dictionary<string, object>();
        private readonly string cacheDirectory;
        
        private CacheManager()
        {
            cacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SlskDown",
                "Cache"
            );
            
            Directory.CreateDirectory(cacheDirectory);
        }
        
        /// <summary>
        /// Obtiene o crea un cache persistente
        /// </summary>
        public PersistentCache<TKey, TValue> GetCache<TKey, TValue>(string name, int maxSize = 10000)
        {
            if (caches.TryGetValue(name, out var existingCache))
            {
                return (PersistentCache<TKey, TValue>)existingCache;
            }
            
            var filePath = Path.Combine(cacheDirectory, $"{name}.json");
            var cache = new PersistentCache<TKey, TValue>(filePath, maxSize);
            caches[name] = cache;
            
            return cache;
        }
        
        /// <summary>
        /// Carga todos los caches
        /// </summary>
        public async Task LoadAllAsync()
        {
            var tasks = new List<Task>();
            
            foreach (var cache in caches.Values)
            {
                if (cache is PersistentCache<string, bool> stringBoolCache)
                {
                    tasks.Add(stringBoolCache.LoadAsync());
                }
            }
            
            await Task.WhenAll(tasks);
        }
        
        /// <summary>
        /// Guarda todos los caches
        /// </summary>
        public async Task SaveAllAsync()
        {
            var tasks = new List<Task>();
            
            foreach (var cache in caches.Values)
            {
                if (cache is PersistentCache<string, bool> stringBoolCache)
                {
                    tasks.Add(stringBoolCache.SaveAsync());
                }
            }
            
            await Task.WhenAll(tasks);
        }
        
        /// <summary>
        /// Obtiene el tamaño total de los caches en disco
        /// </summary>
        public long GetTotalCacheSize()
        {
            long totalSize = 0;
            
            if (Directory.Exists(cacheDirectory))
            {
                foreach (var file in Directory.GetFiles(cacheDirectory, "*.json"))
                {
                    totalSize += new FileInfo(file).Length;
                }
            }
            
            return totalSize;
        }
        
        /// <summary>
        /// Limpia todos los caches en disco
        /// </summary>
        public void ClearAllCaches()
        {
            if (Directory.Exists(cacheDirectory))
            {
                foreach (var file in Directory.GetFiles(cacheDirectory, "*.json"))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deleting cache file {file}: {ex.Message}");
                    }
                }
            }
            
            foreach (var cache in caches.Values)
            {
                if (cache is PersistentCache<string, bool> stringBoolCache)
                {
                    stringBoolCache.Clear();
                }
            }
        }
    }
}
