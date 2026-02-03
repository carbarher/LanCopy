using System;
using System.Threading;
using System.Threading.Tasks;
using Soulseek;

namespace SlskDown.Core
{
    /// <summary>
    /// Adaptador que envuelve SoulseekClient para implementar INetworkClient
    /// Permite tratar Soulseek de forma uniforme con otras redes P2P
    /// </summary>
    public class SoulseekClientAdapter : INetworkClient
    {
        private readonly SoulseekClient _client;
        private NetworkConnectionState _state = NetworkConnectionState.Disconnected;
        private readonly object _stateLock = new object();
        private DateTime _connectedAt;
        private NetworkStatistics _stats = new NetworkStatistics();

        public string NetworkName => "Soulseek";

        public NetworkConnectionState State
        {
            get { lock (_stateLock) { return _state; } }
            private set
            {
                NetworkConnectionState oldState;
                lock (_stateLock)
                {
                    oldState = _state;
                    _state = value;
                }
                StateChanged?.Invoke(this, new NetworkStateChangedEventArgs
                {
                    PreviousState = oldState,
                    CurrentState = value
                });
            }
        }

        public bool IsConnected => _client?.State.HasFlag(SoulseekClientStates.Connected) == true &&
                                   _client?.State.HasFlag(SoulseekClientStates.LoggedIn) == true;

        public event EventHandler<NetworkStateChangedEventArgs> StateChanged;

        public SoulseekClientAdapter(SoulseekClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));

            // Suscribirse a eventos del cliente Soulseek
            _client.StateChanged += OnSoulseekStateChanged;
            _client.Disconnected += OnSoulseekDisconnected;
        }

        public async Task ConnectAsync(NetworkCredentials credentials, CancellationToken cancellationToken = default)
        {
            if (IsConnected)
            {
                throw new InvalidOperationException("Ya está conectado a Soulseek");
            }

            State = NetworkConnectionState.Connecting;

            try
            {
                await _client.ConnectAsync(
                    credentials.Username,
                    credentials.Password,
                    cancellationToken
                );

                _connectedAt = DateTime.UtcNow;
                State = NetworkConnectionState.LoggedIn;
            }
            catch (Exception ex)
            {
                State = NetworkConnectionState.Failed;
                throw new Exception($"Error conectando a Soulseek: {ex.Message}", ex);
            }
        }

        public async Task DisconnectAsync()
        {
            if (!IsConnected)
            {
                return;
            }

            try
            {
                _client.Disconnect();
                State = NetworkConnectionState.Disconnected;
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error desconectando de Soulseek: {ex.Message}", ex);
            }
        }

        public NetworkStatistics GetStatistics()
        {
            _stats.Uptime = IsConnected ? DateTime.UtcNow - _connectedAt : TimeSpan.Zero;
            _stats.LastConnected = _connectedAt;

            // Obtener estadísticas del cliente Soulseek si están disponibles
            // TODO: Mapear estadísticas específicas de Soulseek cuando estén expuestas

            return _stats;
        }

        private void OnSoulseekStateChanged(object sender, SoulseekClientStateChangedEventArgs e)
        {
            // Mapear estados de Soulseek a estados genéricos
            if (e.State.HasFlag(SoulseekClientStates.Connected) && e.State.HasFlag(SoulseekClientStates.LoggedIn))
            {
                State = NetworkConnectionState.LoggedIn;
            }
            else if (e.State.HasFlag(SoulseekClientStates.Connected))
            {
                State = NetworkConnectionState.Connected;
            }
            else if (e.State == SoulseekClientStates.Disconnected)
            {
                State = NetworkConnectionState.Disconnected;
            }
        }

        private void OnSoulseekDisconnected(object sender, SoulseekClientDisconnectedEventArgs e)
        {
            State = NetworkConnectionState.Disconnected;

            StateChanged?.Invoke(this, new NetworkStateChangedEventArgs
            {
                PreviousState = NetworkConnectionState.Connected,
                CurrentState = NetworkConnectionState.Disconnected,
                Message = e.Message,
                Error = e.Exception
            });
        }

        public void Dispose()
        {
            _client.StateChanged -= OnSoulseekStateChanged;
            _client.Disconnected -= OnSoulseekDisconnected;
            // No dispose del cliente, ya que es gestionado externamente
        }

        /// <summary>
        /// Obtiene el cliente Soulseek subyacente para operaciones específicas
        /// </summary>
        public SoulseekClient GetUnderlyingClient() => _client;
    }
}
