using System;
using System.Collections.Generic;

namespace SlskDown.Services
{
    /// <summary>
    /// Servicio para tracking de estadÃ­sticas de uso
    /// </summary>
    public interface IStatsService
    {
        /// <summary>
        /// Registra una bÃºsqueda realizada
        /// </summary>
        void RecordSearch(string query, int resultsCount);
        
        /// <summary>
        /// Registra una descarga completada
        /// </summary>
        void RecordDownload(string filename, long size, double speedKBps, string username);
        
        /// <summary>
        /// Obtiene estadÃ­sticas globales
        /// </summary>
        AppStats GetStats();
        
        /// <summary>
        /// Guarda estadÃ­sticas a disco
        /// </summary>
        void Save();
        
        /// <summary>
        /// Carga estadÃ­sticas desde disco
        /// </summary>
        void Load();
    }

    /// <summary>
    /// EstadÃ­sticas de la aplicaciÃ³n
    /// </summary>
    public class AppStats
    {
        public int TotalSearches { get; set; }
        public int TotalDownloads { get; set; }
        public long TotalBytesDownloaded { get; set; }
        public double AverageSpeedKBps { get; set; }
        public DateTime FirstUseDate { get; set; }
        public DateTime LastUseDate { get; set; }
        public Dictionary<string, int> TopUsers { get; set; } = new();
        public Dictionary<string, int> TopExtensions { get; set; } = new();
        public List<string> RecentSearches { get; set; } = new();
        public int SearchesToday { get; set; }
        public int DownloadsToday { get; set; }
        public long BytesDownloadedToday { get; set; }
    }
}

