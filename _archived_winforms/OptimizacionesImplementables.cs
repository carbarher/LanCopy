using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SlskDown
{
    /// <summary>
    /// 5 OPTIMIZACIONES ADICIONALES LISTAS PARA IMPLEMENTAR
    /// Copiar estos mÃ©todos a MainForm.cs y reemplazar cÃ³digo existente
    /// </summary>
    public static class OptimizacionesImplementables
    {
        // ==========================================
        // OPTIMIZACIÃ“N #2: Span<T> para Split
        // ==========================================
        
        /// <summary>
        /// Split optimizado con Span<T> - 0 allocations intermedias
        /// Reemplaza: query.Split(',')
        /// </summary>
        public static void SplitSpan(ReadOnlySpan<char> input, char separator, List<string> output)
        {
            output.Clear();
            int start = 0;
            
            for (int i = 0; i <= input.Length; i++)
            {
                if (i == input.Length || input[i] == separator)
                {
                    var part = input.Slice(start, i - start).Trim();
                    if (!part.IsEmpty)
                    {
                        output.Add(part.ToString()); // Solo 1 allocation por parte
                    }
                    start = i + 1;
                }
            }
        }
        
        // ==========================================
        // OPTIMIZACIÃ“N #3: StringBuilder Pool
        // ==========================================
        
        private static readonly ConcurrentBag<StringBuilder> stringBuilderPool = new();
        private const int MAX_POOL_SIZE = 10;
        private const int INITIAL_CAPACITY = 2048;
        
        /// <summary>
        /// Obtener StringBuilder del pool (reutilizable)
        /// </summary>
        public static StringBuilder RentStringBuilder()
        {
            if (stringBuilderPool.TryTake(out var sb))
            {
                sb.Clear();
                return sb;
            }
            return new StringBuilder(INITIAL_CAPACITY);
        }
        
        /// <summary>
        /// Devolver StringBuilder al pool
        /// </summary>
        public static void ReturnStringBuilder(StringBuilder sb)
        {
            if (sb == null) return;
            
            // Solo guardar si el pool no estÃ¡ lleno
            if (stringBuilderPool.Count < MAX_POOL_SIZE)
            {
                sb.Clear();
                if (sb.Capacity > INITIAL_CAPACITY * 4)
                {
                    sb.Capacity = INITIAL_CAPACITY; // Reducir capacidad si creciÃ³ mucho
                }
                stringBuilderPool.Add(sb);
            }
        }
        
        // ==========================================
        // OPTIMIZACIÃ“N #4: Batch Processing ListView
        // ==========================================
        
        /// <summary>
        /// Agregar items a ListView en lotes (5-10x mÃ¡s rÃ¡pido)
        /// Elimina parpadeo y mejora rendimiento
        /// </summary>
        public static void AddResultsBatch(ListView listView, List<ListViewItem> items)
        {
            if (items == null || items.Count == 0) return;
            
            listView.BeginUpdate(); // Suspender redibujado
            try
            {
                const int BATCH_SIZE = 100;
                
                for (int i = 0; i < items.Count; i += BATCH_SIZE)
                {
                    var batch = items.Skip(i).Take(BATCH_SIZE).ToArray();
                    listView.Items.AddRange(batch);
                    
                    // Mantener UI responsive cada 500 items
                    if (i % 500 == 0 && i > 0)
                    {
                        Application.DoEvents();
                    }
                }
            }
            finally
            {
                listView.EndUpdate(); // Redibujar una sola vez
            }
        }
        
        /// <summary>
        /// Limpiar ListView de forma optimizada
        /// </summary>
        public static void ClearListViewOptimized(ListView listView)
        {
            listView.BeginUpdate();
            try
            {
                listView.Items.Clear();
            }
            finally
            {
                listView.EndUpdate();
            }
        }
        
        // ==========================================
        // OPTIMIZACIÃ“N #5: CachÃ© con ExpiraciÃ³n
        // ==========================================
        
        /// <summary>
        /// Entrada de cachÃ© con timestamp
        /// </summary>
        public class CachedValue<T>
        {
            public T Value { get; set; }
            public DateTime CachedAt { get; set; }
            
            public CachedValue(T value)
            {
                Value = value;
                CachedAt = DateTime.Now;
            }
            
            public bool IsExpired(TimeSpan maxAge)
            {
                return DateTime.Now - CachedAt > maxAge;
            }
        }
        
        /// <summary>
        /// CachÃ© con lÃ­mite de tamaÃ±o y expiraciÃ³n automÃ¡tica
        /// </summary>
        public class ExpiringCache<TKey, TValue> where TKey : notnull
        {
            private readonly ConcurrentDictionary<TKey, CachedValue<TValue>> cache = new();
            private readonly TimeSpan expirationTime;
            private readonly int maxSize;
            
            public ExpiringCache(TimeSpan expirationTime, int maxSize = 5000)
            {
                this.expirationTime = expirationTime;
                this.maxSize = maxSize;
            }
            
            public bool TryGet(TKey key, out TValue value)
            {
                if (cache.TryGetValue(key, out var cached))
                {
                    if (!cached.IsExpired(expirationTime))
                    {
                        value = cached.Value;
                        return true;
                    }
                    // Expirado, eliminar
                    cache.TryRemove(key, out _);
                }
                
                value = default!;
                return false;
            }
            
            public void Set(TKey key, TValue value)
            {
                // Limpiar si estÃ¡ lleno
                if (cache.Count >= maxSize)
                {
                    CleanExpired();
                    
                    // Si aÃºn estÃ¡ lleno, eliminar las mÃ¡s antiguas
                    if (cache.Count >= maxSize)
                    {
                        var oldest = cache.OrderBy(kvp => kvp.Value.CachedAt)
                                          .Take(maxSize / 4) // Eliminar 25%
                                          .Select(kvp => kvp.Key)
                                          .ToList();
                        
                        foreach (var oldKey in oldest)
                        {
                            cache.TryRemove(oldKey, out _);
                        }
                    }
                }
                
                cache[key] = new CachedValue<TValue>(value);
            }
            
            public void CleanExpired()
            {
                var expired = cache.Where(kvp => kvp.Value.IsExpired(expirationTime))
                                   .Select(kvp => kvp.Key)
                                   .ToList();
                
                foreach (var key in expired)
                {
                    cache.TryRemove(key, out _);
                }
            }
            
            public int Count => cache.Count;
            
            public void Clear() => cache.Clear();
        }
        
        // ==========================================
        // EJEMPLOS DE USO
        // ==========================================
        
        /// <summary>
        /// EJEMPLO 1: Usar PLINQ en filtrado
        /// </summary>
        public static List<SearchResult> FilterResultsPLINQ(
            IEnumerable<SearchResult> results,
            long minSize,
            long maxSize)
        {
            return results
                .AsParallel() // â† AGREGAR ESTA LÃNEA
                .WithDegreeOfParallelism(Environment.ProcessorCount) // â† Y ESTA
                .Where(r => r.Size >= minSize && r.Size <= maxSize)
                .OrderByDescending(r => r.Size)
                .ToList();
        }
        
        /// <summary>
        /// EJEMPLO 2: Usar Span<T> para split
        /// </summary>
        public static List<string> SplitQueryOptimized(string query)
        {
            var searchTerms = new List<string>();
            SplitSpan(query.AsSpan(), ',', searchTerms);
            return searchTerms;
        }
        
        /// <summary>
        /// EJEMPLO 3: Usar StringBuilder Pool
        /// </summary>
        public static string BuildStatsOptimized(Dictionary<string, int> stats)
        {
            var sb = RentStringBuilder();
            try
            {
                sb.AppendLine("ðŸ“Š ESTADÃSTICAS");
                sb.AppendLine();
                
                foreach (var kvp in stats)
                {
                    sb.Append("â€¢ ").Append(kvp.Key).Append(": ").Append(kvp.Value).AppendLine();
                }
                
                return sb.ToString();
            }
            finally
            {
                ReturnStringBuilder(sb);
            }
        }
        
        /// <summary>
        /// EJEMPLO 4: Usar Batch ListView
        /// </summary>
        public static void AddSearchResultsOptimized(ListView listView, List<SearchResult> results)
        {
            var items = results.Select(r => new ListViewItem(new[]
            {
                r.Username,
                r.Country,
                r.Filename,
                Optimizations.FormatSize(r.Size), // SizeFormatted
                r.Extension,
                r.Bitrate?.ToString() ?? "",
                r.Length?.ToString() ?? "",
                r.Directory // Folder
            })
            {
                Tag = r
            }).ToList();
            
            AddResultsBatch(listView, items);
        }
        
        /// <summary>
        /// EJEMPLO 5: Usar CachÃ© con ExpiraciÃ³n
        /// </summary>
        private static readonly ExpiringCache<string, string> countryCache = 
            new ExpiringCache<string, string>(TimeSpan.FromDays(7), 5000);
        
        public static async Task<string> GetCountryWithCacheOptimized(string username)
        {
            // Verificar cachÃ©
            if (countryCache.TryGet(username, out var country))
            {
                return country;
            }
            
            // Obtener de API (simulado)
            country = await GetCountryFromAPI(username);
            
            // Guardar en cachÃ©
            countryCache.Set(username, country);
            
            return country;
        }
        
        private static async Task<string> GetCountryFromAPI(string username)
        {
            // ImplementaciÃ³n real aquÃ­
            await Task.Delay(100);
            return "Unknown";
        }
    }
    
    // NOTA: SearchResult ya estÃ¡ definido en Models\SearchResult.cs
    // No es necesario duplicar la clase aquÃ­
}

