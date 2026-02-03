using System.Collections.Generic;
using SlskDown.Models;

namespace SlskDown.Services
{
    /// <summary>
    /// Servicio para rastrear archivos ya descargados (simulados)
    /// </summary>
    public interface IDownloadTrackingService
    {
        /// <summary>
        /// Marca un archivo como descargado
        /// </summary>
        void MarkAsDownloaded(string filename, string username, long size, string author);
        
        /// <summary>
        /// Verifica si un archivo ya fue descargado
        /// </summary>
        bool IsAlreadyDownloaded(string filename, string username, long size);
        
        /// <summary>
        /// Obtiene todos los archivos descargados
        /// </summary>
        List<DownloadedFile> GetAllDownloaded();
        
        /// <summary>
        /// Obtiene archivos descargados de un autor especÃ­fico
        /// </summary>
        List<DownloadedFile> GetDownloadedByAuthor(string author);
        
        /// <summary>
        /// Limpia archivos descargados mÃ¡s antiguos que X dÃ­as
        /// </summary>
        int CleanupOldDownloads(int daysOld);
        
        /// <summary>
        /// Obtiene estadÃ­sticas de descargas
        /// </summary>
        (int total, int today, Dictionary<string, int> byAuthor) GetStats();
        
        /// <summary>
        /// Limpia completamente el historial en memoria y recarga desde disco
        /// </summary>
        void Clear();
    }
}

