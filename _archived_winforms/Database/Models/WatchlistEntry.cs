using System;

namespace SlskDown.Database.Models
{
    /// <summary>
    /// Entrada de la lista de vigilancia de autores
    /// </summary>
    public class WatchlistEntry
    {
        public long Id { get; set; }
        public string Author { get; set; }
        public bool Enabled { get; set; }
        public bool AutoDownload { get; set; }
        public bool NotifyOnNew { get; set; }
        public double SearchIntervalHours { get; set; }
        public DateTime LastSearch { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
