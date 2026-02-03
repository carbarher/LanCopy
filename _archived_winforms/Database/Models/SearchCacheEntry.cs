using System;

namespace SlskDown.Database.Models
{
    /// <summary>
    /// Entrada de caché de búsqueda
    /// </summary>
    public class SearchCacheEntry
    {
        public long Id { get; set; }
        public string Query { get; set; }
        public string Username { get; set; }
        public string FileName { get; set; }
        public long Size { get; set; }
        public string FilePath { get; set; }
        public double? Speed { get; set; }
        public DateTime CachedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
