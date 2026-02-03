using System;
using System.Collections.Concurrent;

namespace SlskDown.Core.Optimization
{
    /// <summary>
    /// Pool de strings para reducir allocations de strings duplicados
    /// Útil para usernames, paths, etc. que se repiten frecuentemente
    /// </summary>
    public class StringPool
    {
        private readonly ConcurrentDictionary<string, string> pool;
        private readonly int maxSize;
        
        public int Count => pool.Count;
        
        public StringPool(int maxSize = 10000)
        {
            this.maxSize = maxSize;
            pool = new ConcurrentDictionary<string, string>();
        }
        
        /// <summary>
        /// Obtiene string internado del pool
        /// </summary>
        public string GetOrAdd(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            
            // Si el pool está lleno, no agregar más
            if (pool.Count >= maxSize && !pool.ContainsKey(value))
                return value;
            
            return pool.GetOrAdd(value, value);
        }
        
        /// <summary>
        /// Limpia el pool
        /// </summary>
        public void Clear()
        {
            pool.Clear();
        }
        
        /// <summary>
        /// Pool global para usernames
        /// </summary>
        public static StringPool Usernames { get; } = new StringPool(5000);
        
        /// <summary>
        /// Pool global para paths
        /// </summary>
        public static StringPool Paths { get; } = new StringPool(10000);
        
        /// <summary>
        /// Pool global para extensiones de archivo
        /// </summary>
        public static StringPool Extensions { get; } = new StringPool(100);
    }
}
