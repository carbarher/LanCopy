using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Interfaz para proveedores de búsqueda en redes P2P
    /// Abstrae las operaciones de búsqueda independientemente de la red
    /// </summary>
    public interface ISearchProvider
    {
        /// <summary>
        /// Nombre del proveedor (ej: "Soulseek")
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Indica si el proveedor está listo para realizar búsquedas
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Realizar una búsqueda
        /// </summary>
        Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancelar una búsqueda en progreso
        /// </summary>
        Task CancelSearchAsync(string searchId);

        /// <summary>
        /// Evento disparado cuando se reciben resultados de búsqueda
        /// </summary>
        event EventHandler<SearchResultsReceivedEventArgs> ResultsReceived;

        /// <summary>
        /// Evento disparado cuando una búsqueda se completa
        /// </summary>
        event EventHandler<SearchCompletedEventArgs> SearchCompleted;
    }

    /// <summary>
    /// Solicitud de búsqueda
    /// </summary>
    public class SearchRequest
    {
        public string SearchId { get; set; } = Guid.NewGuid().ToString();
        public string Query { get; set; }
        public SearchFilters Filters { get; set; } = new SearchFilters();
        public int MaxResults { get; set; } = 1000;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Filtros de búsqueda
    /// </summary>
    public class SearchFilters
    {
        public long? MinSizeBytes { get; set; }
        public long? MaxSizeBytes { get; set; }
        public List<string> FileExtensions { get; set; } = new List<string>();
        public List<string> ExcludeKeywords { get; set; } = new List<string>();
        public FileType? FileType { get; set; }
        public bool SpanishOnly { get; set; }
    }

    /// <summary>
    /// Tipos de archivo para filtrado
    /// </summary>
    public enum FileType
    {
        Any,
        Audio,
        Video,
        Image,
        Document,
        Archive,
        Program
    }

    /// <summary>
    /// Respuesta de búsqueda
    /// </summary>
    public class SearchResponse
    {
        public string SearchId { get; set; }
        public List<SearchResult> Results { get; set; } = new List<SearchResult>();
        public SearchStatus Status { get; set; }
        public string ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Estado de una búsqueda
    /// </summary>
    public enum SearchStatus
    {
        InProgress,
        Completed,
        Cancelled,
        Failed,
        TimedOut
    }

    /// <summary>
    /// Resultado individual de búsqueda
    /// </summary>
    public class SearchResult
    {
        public string ResultId { get; set; }
        public string FileName { get; set; }
        public long SizeBytes { get; set; }
        public string Username { get; set; }
        public int QueueLength { get; set; }
        public int? FreeSlots { get; set; }
        public long? BitRate { get; set; }
        public int? Duration { get; set; }
        public string FileExtension { get; set; }
        public string FileHash { get; set; }
        public string NetworkSource { get; set; } // "Soulseek", "eMule", etc.
        public bool IsLocked { get; set; } // Indica si el archivo está bloqueado
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Argumentos del evento de resultados recibidos
    /// </summary>
    public class SearchResultsReceivedEventArgs : EventArgs
    {
        public string SearchId { get; set; }
        public List<SearchResult> Results { get; set; }
        public int TotalResultsReceived { get; set; }
    }

    /// <summary>
    /// Argumentos del evento de búsqueda completada
    /// </summary>
    public class SearchCompletedEventArgs : EventArgs
    {
        public string SearchId { get; set; }
        public SearchStatus Status { get; set; }
        public int TotalResults { get; set; }
        public TimeSpan Duration { get; set; }
        public string ErrorMessage { get; set; }
    }
}
