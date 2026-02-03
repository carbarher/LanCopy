using System;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Interfaz para proveedores de descarga multi-red
    /// Permite descargas uniformes desde cualquier red P2P
    /// </summary>
    public interface IDownloadProvider
    {
        /// <summary>
        /// Nombre del proveedor (Soulseek, eMule, etc.)
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Indica si el proveedor está listo para descargar
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Evento que se dispara cuando cambia el progreso de descarga
        /// </summary>
        event EventHandler<DownloadProgressEventArgs> ProgressChanged;

        /// <summary>
        /// Evento que se dispara cuando se completa una descarga
        /// </summary>
        event EventHandler<DownloadCompletedEventArgs> DownloadCompleted;

        /// <summary>
        /// Evento que se dispara cuando falla una descarga
        /// </summary>
        event EventHandler<DownloadFailedEventArgs> DownloadFailed;

        /// <summary>
        /// Inicia una descarga
        /// </summary>
        Task<DownloadHandle> StartDownloadAsync(
            SearchResult result, 
            string destinationPath, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Pausa una descarga
        /// </summary>
        Task PauseDownloadAsync(string downloadId);

        /// <summary>
        /// Reanuda una descarga pausada
        /// </summary>
        Task ResumeDownloadAsync(string downloadId);

        /// <summary>
        /// Cancela una descarga
        /// </summary>
        Task CancelDownloadAsync(string downloadId);

        /// <summary>
        /// Obtiene el estado actual de una descarga
        /// </summary>
        Task<DownloadStatusInfo> GetDownloadStatusAsync(string downloadId);
    }

    /// <summary>
    /// Handle de descarga que representa una descarga activa
    /// </summary>
    public class DownloadHandle
    {
        public string DownloadId { get; set; }
        public string ProviderName { get; set; }
        public string FileName { get; set; }
        public long TotalBytes { get; set; }
        public string DestinationPath { get; set; }
        public DateTime StartTime { get; set; }
    }

    /// <summary>
    /// Estado de una descarga (información de progreso)
    /// </summary>
    public class DownloadStatusInfo
    {
        public string DownloadId { get; set; }
        public DownloadState State { get; set; }
        public long BytesDownloaded { get; set; }
        public long TotalBytes { get; set; }
        public double Progress => TotalBytes > 0 ? (double)BytesDownloaded / TotalBytes * 100 : 0;
        public double SpeedBytesPerSecond { get; set; }
        public TimeSpan? EstimatedTimeRemaining { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Estados posibles de una descarga
    /// </summary>
    public enum DownloadState
    {
        Queued,
        Connecting,
        Downloading,
        Paused,
        Completed,
        Failed,
        Cancelled
    }

    /// <summary>
    /// Argumentos del evento de progreso de descarga
    /// </summary>
    public class DownloadProgressEventArgs : EventArgs
    {
        public string DownloadId { get; set; }
        public string ProviderName { get; set; }
        public long BytesDownloaded { get; set; }
        public long TotalBytes { get; set; }
        public double Progress { get; set; }
        public double SpeedBytesPerSecond { get; set; }
    }

    /// <summary>
    /// Argumentos del evento de descarga completada
    /// </summary>
    public class DownloadCompletedEventArgs : EventArgs
    {
        public string DownloadId { get; set; }
        public string ProviderName { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long TotalBytes { get; set; }
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Argumentos del evento de descarga fallida
    /// </summary>
    public class DownloadFailedEventArgs : EventArgs
    {
        public string DownloadId { get; set; }
        public string ProviderName { get; set; }
        public string FileName { get; set; }
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
    }
}
