using System;
using System.Collections.Generic;

namespace SlskDown.Core.Events
{
    /// <summary>
    /// Mensajes de eventos de red inspirados en Nicotine+
    /// </summary>
    
    // Eventos de servidor
    public class ServerConnectedMessage
    {
        public string ServerAddress { get; set; }
        public DateTime ConnectedAt { get; set; }
    }

    public class ServerDisconnectedMessage
    {
        public string Reason { get; set; }
        public bool WasManual { get; set; }
        public DateTime DisconnectedAt { get; set; }
    }

    public class ServerLoginMessage
    {
        public bool Success { get; set; }
        public string Username { get; set; }
        public string ErrorMessage { get; set; }
    }

    // Eventos de peer
    public class PeerConnectedMessage
    {
        public string Username { get; set; }
        public string ConnectionType { get; set; }
        public string RemoteEndpoint { get; set; }
        public DateTime ConnectedAt { get; set; }
    }

    public class PeerDisconnectedMessage
    {
        public string Username { get; set; }
        public string ConnectionType { get; set; }
        public string Reason { get; set; }
        public DateTime DisconnectedAt { get; set; }
    }

    public class PeerConnectionErrorMessage
    {
        public string Username { get; set; }
        public string ConnectionType { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsOffline { get; set; }
    }

    // Eventos de transferencia
    public class TransferRequestMessage
    {
        public string Username { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string Direction { get; set; } // "Download" o "Upload"
    }

    public class NetworkTransferStartedMessage
    {
        public string TransferId { get; set; }
        public string Username { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public DateTime StartedAt { get; set; }
    }

    public class NetworkTransferProgressMessage
    {
        public string TransferId { get; set; }
        public string Username { get; set; }
        public string FileName { get; set; }
        public long BytesTransferred { get; set; }
        public long TotalBytes { get; set; }
        public double Speed { get; set; }
        public double Progress { get; set; }
    }

    public class NetworkTransferCompletedMessage
    {
        public string TransferId { get; set; }
        public string Username { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public DateTime CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public double AverageSpeed { get; set; }
    }

    public class NetworkTransferFailedMessage
    {
        public string TransferId { get; set; }
        public string Username { get; set; }
        public string FileName { get; set; }
        public string ErrorMessage { get; set; }
        public string FailureReason { get; set; }
        public DateTime FailedAt { get; set; }
    }

    public class TransferAbortedMessage
    {
        public string TransferId { get; set; }
        public string Username { get; set; }
        public string FileName { get; set; }
        public string Reason { get; set; }
        public DateTime AbortedAt { get; set; }
    }

    // Eventos de búsqueda
    public class SearchStartedMessage
    {
        public string SearchId { get; set; }
        public string Query { get; set; }
        public DateTime StartedAt { get; set; }
    }

    public class SearchResultsMessage
    {
        public string SearchId { get; set; }
        public string Query { get; set; }
        public string Username { get; set; }
        public int ResultCount { get; set; }
        public List<SearchResultItem> Results { get; set; }
    }

    public class SearchResultItem
    {
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string Extension { get; set; }
        public Dictionary<string, object> Attributes { get; set; }
        public string Source { get; set; } // "Soulseek", "eMule", etc.
    }

    public class SearchCompletedMessage
    {
        public string SearchId { get; set; }
        public string Query { get; set; }
        public int TotalResults { get; set; }
        public int UniqueUsers { get; set; }
        public DateTime CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
    }

    // Eventos de usuario
    public class UserStatusChangedMessage
    {
        public string Username { get; set; }
        public string Status { get; set; } // "Online", "Offline", "Away"
        public DateTime ChangedAt { get; set; }
    }

    public class UserStatsMessage
    {
        public string Username { get; set; }
        public int AverageSpeed { get; set; }
        public long UploadCount { get; set; }
        public int SharedFileCount { get; set; }
        public int SharedFolderCount { get; set; }
    }

    // Eventos de cola
    public class QueuePositionMessage
    {
        public string Username { get; set; }
        public string FileName { get; set; }
        public int Position { get; set; }
    }

    public class QueueSlotAvailableMessage
    {
        public string Username { get; set; }
        public DateTime AvailableAt { get; set; }
    }

    // Eventos de red distribuida
    public class DistributedParentConnectedMessage
    {
        public string ParentUsername { get; set; }
        public string RemoteEndpoint { get; set; }
        public DateTime ConnectedAt { get; set; }
    }

    public class DistributedParentDisconnectedMessage
    {
        public string ParentUsername { get; set; }
        public string Reason { get; set; }
        public DateTime DisconnectedAt { get; set; }
    }

    public class DistributedSearchMessage
    {
        public string Query { get; set; }
        public string OriginUser { get; set; }
        public int SearchToken { get; set; }
    }
}
