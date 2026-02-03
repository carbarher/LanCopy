using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Interfaz común para clientes de redes P2P (Soulseek)
    /// Permite orquestar múltiples redes de forma uniforme
    /// </summary>
    public interface INetworkClient : IDisposable
    {
        /// <summary>
        /// Nombre de la red (ej: "Soulseek")
        /// </summary>
        string NetworkName { get; }

        /// <summary>
        /// Estado actual de la conexión
        /// </summary>
        NetworkConnectionState State { get; }

        /// <summary>
        /// Evento disparado cuando cambia el estado de conexión
        /// </summary>
        event EventHandler<NetworkStateChangedEventArgs> StateChanged;

        /// <summary>
        /// Conectar a la red
        /// </summary>
        Task ConnectAsync(NetworkCredentials credentials, CancellationToken cancellationToken = default);

        /// <summary>
        /// Desconectar de la red
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Verificar si está conectado y operativo
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Obtener estadísticas de la red
        /// </summary>
        NetworkStatistics GetStatistics();
    }

    /// <summary>
    /// Estados posibles de conexión a una red P2P
    /// </summary>
    public enum NetworkConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        LoggedIn,
        Reconnecting,
        Failed
    }

    /// <summary>
    /// Credenciales para autenticación en la red
    /// </summary>
    public class NetworkCredentials
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Server { get; set; }
        public int Port { get; set; }
        public Dictionary<string, object> AdditionalSettings { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Argumentos del evento de cambio de estado
    /// </summary>
    public class NetworkStateChangedEventArgs : EventArgs
    {
        public NetworkConnectionState PreviousState { get; set; }
        public NetworkConnectionState CurrentState { get; set; }
        public string Message { get; set; }
        public Exception Error { get; set; }
    }

    /// <summary>
    /// Estadísticas de una red P2P
    /// </summary>
    public class NetworkStatistics
    {
        public long TotalBytesUploaded { get; set; }
        public long TotalBytesDownloaded { get; set; }
        public int ActiveUploads { get; set; }
        public int ActiveDownloads { get; set; }
        public int QueuedDownloads { get; set; }
        public int ConnectedPeers { get; set; }
        public TimeSpan Uptime { get; set; }
        public DateTime LastConnected { get; set; }
    }
}
