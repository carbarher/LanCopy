using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Interfaz para cliente eMule/ed2k
    /// Define las operaciones básicas para interactuar con la red eMule
    /// </summary>
    public interface IEmuleClient
    {
        /// <summary>
        /// Indica si el cliente está conectado a la red eMule
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Estado actual del cliente
        /// </summary>
        EmuleConnectionState State { get; }

        /// <summary>
        /// Evento que se dispara cuando cambia el estado de conexión
        /// </summary>
        event EventHandler<EmuleConnectionStateChangedEventArgs> StateChanged;

        /// <summary>
        /// Evento que se dispara cuando se reciben resultados de búsqueda
        /// </summary>
        event EventHandler<EmuleSearchResultsEventArgs> SearchResultsReceived;

        /// <summary>
        /// Conecta al cliente eMule
        /// </summary>
        Task ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Desconecta del cliente eMule
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Realiza una búsqueda en la red eMule
        /// </summary>
        Task<string> SearchAsync(string query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancela una búsqueda activa
        /// </summary>
        Task CancelSearchAsync(string searchId);

        /// <summary>
        /// Descarga un archivo de la red eMule
        /// </summary>
        Task<EmuleDownload> DownloadAsync(EmuleSearchResult result, string destinationPath, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Estados de conexión del cliente eMule
    /// </summary>
    public enum EmuleConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Disconnecting,
        Error
    }

    /// <summary>
    /// Argumentos del evento de cambio de estado
    /// </summary>
    public class EmuleConnectionStateChangedEventArgs : EventArgs
    {
        public EmuleConnectionState PreviousState { get; set; }
        public EmuleConnectionState CurrentState { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Argumentos del evento de resultados de búsqueda
    /// </summary>
    public class EmuleSearchResultsEventArgs : EventArgs
    {
        public string SearchId { get; set; }
        public List<EmuleSearchResult> Results { get; set; }
    }

    /// <summary>
    /// Resultado de búsqueda de eMule
    /// </summary>
    public class EmuleSearchResult
    {
        public string FileHash { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public int SourceCount { get; set; }
        public int CompleteSourceCount { get; set; }
        public string FileType { get; set; }
        public string Ed2kLink { get; set; }
        public int Sources { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Información de descarga de eMule
    /// </summary>
    public class EmuleDownload
    {
        public string DownloadId { get; set; }
        public string FileHash { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public long BytesDownloaded { get; set; }
        public EmuleDownloadState State { get; set; }
        public double Progress => FileSize > 0 ? (double)BytesDownloaded / FileSize * 100 : 0;
    }

    /// <summary>
    /// Estados de descarga de eMule
    /// </summary>
    public enum EmuleDownloadState
    {
        Queued,
        Downloading,
        Paused,
        Completed,
        Error,
        Cancelled
    }
}
