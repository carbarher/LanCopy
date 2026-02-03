using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// Sistema de cache inteligente para evitar bÃºsquedas repetitivas
    /// </summary>
    public partial class MainForm
    {
        // Cache de resultados
        private static readonly ConcurrentDictionary<string, CachedSearchResult> searchCache = new();
        private static readonly TimeSpan cacheExpiry = TimeSpan.FromHours(2);
        private static readonly string cacheFile = @"c:\p2p\SlskDown\search_cache.json";
        
        // EstadÃ­sticas de cache
        private static int cacheHits = 0;
        private static int cacheMisses = 0;
        private static int cacheEvictions = 0;
        
        /// <summary>
        /// Resultado de bÃºsqueda cacheado
        /// </summary>
        public struct CachedSearchResult
        {
            public string Author { get; set; }
            public int TotalFiles { get; set; }
            public int SpanishFiles { get; set; }
            public int Responses { get; set; }
            public DateTime CachedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
            public bool HasResults => TotalFiles > 0;
            public string SearchHash { get; set; }
        }
        
        /// <summary>
        /// Inicializar sistema de cache
        /// </summary>
        private void InitializeSmartCache()
        {
            try
            {
                LoadSearchCache();
                StartCacheCleanupTimer();
                
                Console.WriteLine("[SmartCache] ðŸ—„ï¸ Sistema de cache inteligente inicializado");
                Console.WriteLine($"[SmartCache] ðŸ“Š Cache cargado: {searchCache.Count} resultados");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SmartCache] âŒ Error inicializando cache: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Verificar si resultado estÃ¡ en cache
        /// </summary>
        private bool TryGetCachedResult(string author, out CachedSearchResult result)
        {
            try
            {
                var key = GenerateCacheKey(author);
                
                if (searchCache.TryGetValue(key, out result))
                {
                    // Verificar si no ha expirado
                    if (DateTime.Now < result.ExpiresAt)
                    {
                        cacheHits++;
                        
                        Console.WriteLine($"[SmartCache] ðŸŽ¯ CACHE HIT: {author} ({result.TotalFiles} archivos)");
                        return true;
                    }
                    else
                    {
                        // Expirado - remover
                        searchCache.TryRemove(key, out _);
                        cacheEvictions++;
                        
                        Console.WriteLine($"[SmartCache] â° CACHE EXPIRADO: {author}");
                    }
                }
                
                cacheMisses++;
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SmartCache] âŒ Error verificando cache: {ex.Message}");
                cacheMisses++;
                result = default;
                return false;
            }
        }
        
        /// <summary>
        /// Agregar resultado al cache
        /// </summary>
        private void AddToCache(string author, int totalFiles, int spanishFiles, int responses)
        {
            try
            {
                var key = GenerateCacheKey(author);
                
                var result = new CachedSearchResult
                {
                    Author = author,
                    TotalFiles = totalFiles,
                    SpanishFiles = spanishFiles,
                    Responses = responses,
                    CachedAt = DateTime.Now,
                    ExpiresAt = DateTime.Now.Add(cacheExpiry),
                    SearchHash = GenerateSearchHash(author)
                };
                
                searchCache[key] = result;
                
                Console.WriteLine($"[SmartCache] ðŸ’¾ CACHE ADD: {author} ({totalFiles} archivos, expira en {cacheExpiry.TotalMinutes:F0}min)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SmartCache] âŒ Error agregando al cache: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Generar clave Ãºnica para cache
        /// </summary>
        private string GenerateCacheKey(string author)
        {
            return author.ToLower().Trim();
        }
        
        /// <summary>
        /// Generar hash de bÃºsqueda para detecciÃ³n de duplicados
        /// </summary>
        private string GenerateSearchHash(string author)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(author.ToLower().Trim()));
            return Convert.ToBase64String(hash)[..8];
        }
        
        /// <summary>
        /// Cargar cache desde archivo
        /// </summary>
        private void LoadSearchCache()
        {
            try
            {
                if (File.Exists(cacheFile))
                {
                    var json = File.ReadAllText(cacheFile);
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, CachedSearchResult>>(json);
                    
                    if (loaded != null)
                    {
                        searchCache.Clear();
                        
                        // Filtrar resultados expirados
                        var now = DateTime.Now;
                        foreach (var kvp in loaded)
                        {
                            if (now < kvp.Value.ExpiresAt)
                            {
                                searchCache[kvp.Key] = kvp.Value;
                            }
                        }
                        
                        Console.WriteLine($"[SmartCache] ðŸ“‚ Cache cargado: {searchCache.Count}/{loaded.Count} resultados vÃ¡lidos");
                    }
                }
                else
                {
                    Console.WriteLine("[SmartCache] â„¹ï¸ No existe archivo de cache - iniciando vacÃ­o");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SmartCache] âŒ Error cargando cache: {ex.Message}");
                searchCache.Clear();
            }
        }
        
        /// <summary>
        /// Guardar cache a archivo
        /// </summary>
        private void SaveSearchCache()
        {
            try
            {
                var json = JsonSerializer.Serialize(searchCache, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(cacheFile, json);
                
                Console.WriteLine($"[SmartCache] ðŸ’¾ Cache guardado: {searchCache.Count} resultados");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SmartCache] âŒ Error guardando cache: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Iniciar timer de limpieza de cache
        /// </summary>
        private void StartCacheCleanupTimer()
        {
            var cleanupTimer = new System.Timers.Timer
            {
                Interval = TimeSpan.FromMinutes(30).TotalMilliseconds, // Limpieza cada 30 minutos
                AutoReset = true
            };
            
            cleanupTimer.Elapsed += (s, e) => CleanupExpiredCache();
            cleanupTimer.Start();
            
            Console.WriteLine("[SmartCache] â° Timer de limpieza iniciado (30min)");
        }
        
        /// <summary>
        /// Limpiar resultados expirados del cache
        /// </summary>
        private void CleanupExpiredCache()
        {
            try
            {
                var now = DateTime.Now;
                var expiredKeys = new List<string>();
                
                foreach (var kvp in searchCache)
                {
                    if (now >= kvp.Value.ExpiresAt)
                    {
                        expiredKeys.Add(kvp.Key);
                    }
                }
                
                foreach (var key in expiredKeys)
                {
                    searchCache.TryRemove(key, out _);
                    cacheEvictions++;
                }
                
                if (expiredKeys.Count > 0)
                {
                    Console.WriteLine($"[SmartCache] ðŸ§¹ Limpieza: {expiredKeys.Count} resultados expirados eliminados");
                    SaveSearchCache();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SmartCache] âŒ Error en limpieza de cache: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtener estadÃ­sticas del cache
        /// </summary>
        private CacheStatistics GetCacheStatistics()
        {
            var total = searchCache.Count;
            var expired = searchCache.Values.Count(r => DateTime.Now >= r.ExpiresAt);
            var valid = total - expired;
            
            var hitRate = (cacheHits + cacheMisses) > 0 ? (double)cacheHits / (cacheHits + cacheMisses) : 0;
            
            return new CacheStatistics
            {
                TotalEntries = total,
                ValidEntries = valid,
                ExpiredEntries = expired,
                CacheHits = cacheHits,
                CacheMisses = cacheMisses,
                HitRate = hitRate,
                Evictions = cacheEvictions,
                CacheSizeBytes = GetCacheSizeBytes()
            };
        }
        
        /// <summary>
        /// Obtener tamaÃ±o del cache en bytes
        /// </summary>
        private long GetCacheSizeBytes()
        {
            try
            {
                if (File.Exists(cacheFile))
                {
                    return new FileInfo(cacheFile).Length;
                }
            }
            catch { }
            
            return 0;
        }
        
        /// <summary>
        /// EstadÃ­sticas del cache
        /// </summary>
        public struct CacheStatistics
        {
            public int TotalEntries { get; set; }
            public int ValidEntries { get; set; }
            public int ExpiredEntries { get; set; }
            public int CacheHits { get; set; }
            public int CacheMisses { get; set; }
            public double HitRate { get; set; }
            public int Evictions { get; set; }
            public long CacheSizeBytes { get; set; }
        }
        
        /// <summary>
        /// Mostrar reporte del cache
        /// </summary>
        private void ShowCacheReport()
        {
            try
            {
                var stats = GetCacheStatistics();
                
                var report = $"""
ðŸ“Š REPORTE DE CACHE INTELIGENTE
========================================
ðŸ“ˆ EstadÃ­sticas de Rendimiento:
â”œâ”€â”€ Entradas totales: {stats.TotalEntries}
â”œâ”€â”€ Entradas vÃ¡lidas: {stats.ValidEntries}
â”œâ”€â”€ Entradas expiradas: {stats.ExpiredEntries}
â”œâ”€â”€ Cache hits: {stats.CacheHits}
â”œâ”€â”€ Cache misses: {stats.CacheMisses}
â”œâ”€â”€ Tasa de aciertos: {stats.HitRate:P1}
â”œâ”€â”€ Evicciones: {stats.Evictions}
â””â”€â”€ TamaÃ±o en disco: {FormatBytes(stats.CacheSizeBytes)}

âš¡ Optimizaciones Aplicadas:
â”œâ”€â”€ ExpiraciÃ³n: {cacheExpiry.TotalHours:F0} horas
â”œâ”€â”€ Limpieza automÃ¡tica: 30 minutos
â”œâ”€â”€ Hash SHA256 para deduplicaciÃ³n
â””â”€â”€ Persistencia en JSON

ðŸ’¾ Archivo: search_cache.json
ðŸ”„ Ãšltima actualizaciÃ³n: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
""";
                
                Console.WriteLine(report);
                MessageBox.Show(report, "Reporte de Cache Inteligente", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SmartCache] âŒ Error generando reporte: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Limpiar todo el cache
        /// </summary>
        private void ClearCache()
        {
            try
            {
                searchCache.Clear();
                cacheHits = 0;
                cacheMisses = 0;
                cacheEvictions = 0;
                
                if (File.Exists(cacheFile))
                {
                    File.Delete(cacheFile);
                }
                
                Console.WriteLine("[SmartCache] ðŸ§¹ Cache limpiado completamente");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SmartCache] âŒ Error limpiando cache: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Formatear bytes para legibilidad
        /// </summary>
        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
        
        /// <summary>
        /// Optimizar cache para bÃºsqueda masiva
        /// </summary>
        private void OptimizeCacheForBatch()
        {
            try
            {
                // Reducir tiempo de expiraciÃ³n para mayor frescura
                cacheExpiry = TimeSpan.FromHours(1);
                
                Console.WriteLine("[SmartCache] ðŸš€ Cache optimizado para bÃºsqueda masiva:");
                Console.WriteLine($"  ExpiraciÃ³n reducida a: {cacheExpiry.TotalMinutes:F0} minutos");
                Console.WriteLine($"  Entradas actuales: {searchCache.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SmartCache] âŒ Error optimizando cache: {ex.Message}");
            }
        }
    }
}

