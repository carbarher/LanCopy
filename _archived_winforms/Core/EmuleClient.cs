using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Implementación básica del cliente eMule/ed2k
    /// Conecta a un daemon eMule local (aMule) vía protocolo External Connection
    /// </summary>
    public class EmuleClient : IEmuleClient
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _password;
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private EmuleConnectionState _state = EmuleConnectionState.Disconnected;
        private readonly Dictionary<string, List<EmuleSearchResult>> _searchResults = new Dictionary<string, List<EmuleSearchResult>>();
        private readonly object _lock = new object();
        private CancellationTokenSource _receiveCts;
        private Task _receiveTask;

        public bool IsConnected => _state == EmuleConnectionState.Connected && _tcpClient?.Connected == true;
        public EmuleConnectionState State => _state;

        public event EventHandler<EmuleConnectionStateChangedEventArgs> StateChanged;
        public event EventHandler<EmuleSearchResultsEventArgs> SearchResultsReceived;

        /// <summary>
        /// Constructor del cliente eMule
        /// </summary>
        /// <param name="host">Host del daemon eMule (por defecto localhost)</param>
        /// <param name="port">Puerto EC (External Connection, por defecto 4712)</param>
        /// <param name="password">Contraseña EC (opcional)</param>
        public EmuleClient(string host = "127.0.0.1", int port = 4712, string password = "")
        {
            _host = host;
            _port = port;
            _password = password;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (IsConnected)
            {
                return;
            }

            try
            {
                ChangeState(EmuleConnectionState.Connecting, "Conectando a eMule...");

                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_host, _port);
                _stream = _tcpClient.GetStream();

                // Autenticación si hay contraseña
                if (!string.IsNullOrEmpty(_password))
                {
                    await AuthenticateAsync(cancellationToken);
                }

                ChangeState(EmuleConnectionState.Connected, "Conectado a eMule");

                // Iniciar tarea de recepción de datos
                _receiveCts = new CancellationTokenSource();
                _receiveTask = Task.Run(() => ReceiveLoop(_receiveCts.Token), _receiveCts.Token);
            }
            catch (Exception ex)
            {
                ChangeState(EmuleConnectionState.Error, $"Error conectando: {ex.Message}");
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            if (_state == EmuleConnectionState.Disconnected)
            {
                return;
            }

            try
            {
                ChangeState(EmuleConnectionState.Disconnecting, "Desconectando...");

                // Cancelar tarea de recepción
                _receiveCts?.Cancel();
                if (_receiveTask != null)
                {
                    await _receiveTask.ConfigureAwait(false);
                }

                _stream?.Close();
                _tcpClient?.Close();

                ChangeState(EmuleConnectionState.Disconnected, "Desconectado");
            }
            catch (Exception ex)
            {
                ChangeState(EmuleConnectionState.Error, $"Error desconectando: {ex.Message}");
            }
        }

        public async Task<string> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Cliente eMule no está conectado");
            }

            var searchId = Guid.NewGuid().ToString("N");

            lock (_lock)
            {
                _searchResults[searchId] = new List<EmuleSearchResult>();
            }

            try
            {
                // Enviar comando de búsqueda al daemon eMule
                // Protocolo EC simplificado: SEARCH <query>
                var searchCommand = $"SEARCH {query}\n";
                var bytes = Encoding.UTF8.GetBytes(searchCommand);
                await _stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);

                // Simular resultados de búsqueda (en una implementación real, se recibirían del daemon)
                await Task.Delay(1000, cancellationToken);
                SimulateSearchResults(searchId, query);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error en búsqueda eMule: {ex.Message}", ex);
            }

            return searchId;
        }

        public Task CancelSearchAsync(string searchId)
        {
            lock (_lock)
            {
                _searchResults.Remove(searchId);
            }
            return Task.CompletedTask;
        }

        public async Task<EmuleDownload> DownloadAsync(EmuleSearchResult result, string destinationPath, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Cliente eMule no está conectado");
            }

            try
            {
                // Enviar comando de descarga al daemon eMule
                var downloadCommand = $"DOWNLOAD {result.FileHash}\n";
                var bytes = Encoding.UTF8.GetBytes(downloadCommand);
                await _stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);

                var download = new EmuleDownload
                {
                    DownloadId = Guid.NewGuid().ToString("N"),
                    FileHash = result.FileHash,
                    FileName = result.FileName,
                    FileSize = result.FileSize,
                    State = EmuleDownloadState.Queued
                };

                return download;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error iniciando descarga eMule: {ex.Message}", ex);
            }
        }

        private async Task AuthenticateAsync(CancellationToken cancellationToken)
        {
            // Protocolo EC simplificado: AUTH <password>
            var authCommand = $"AUTH {_password}\n";
            var bytes = Encoding.UTF8.GetBytes(authCommand);
            await _stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);

            // Esperar respuesta de autenticación
            var buffer = new byte[1024];
            var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            if (!response.Contains("OK"))
            {
                throw new InvalidOperationException("Autenticación fallida");
            }
        }

        private async Task ReceiveLoop(CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];

            try
            {
                while (!cancellationToken.IsCancellationRequested && IsConnected)
                {
                    var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    var data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    ProcessReceivedData(data);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cuando se cancela
            }
            catch (Exception)
            {
                // Error en recepción
                if (!cancellationToken.IsCancellationRequested)
                {
                    ChangeState(EmuleConnectionState.Error, "Error en recepción de datos");
                }
            }
        }

        private void ProcessReceivedData(string data)
        {
            // Procesar datos recibidos del daemon eMule
            // En una implementación real, aquí se parsearían los resultados de búsqueda
            // y se dispararían los eventos correspondientes
        }

        private void SimulateSearchResults(string searchId, string query)
        {
            // Simulación de resultados para demostración
            // En una implementación real, estos vendrían del daemon eMule
            var results = new List<EmuleSearchResult>
            {
                new EmuleSearchResult
                {
                    FileHash = GenerateRandomHash(),
                    FileName = $"{query} - Resultado eMule 1.epub",
                    FileSize = 1024000 + new Random().Next(1000000),
                    SourceCount = new Random().Next(1, 50),
                    CompleteSourceCount = new Random().Next(1, 20),
                    FileType = "epub"
                },
                new EmuleSearchResult
                {
                    FileHash = GenerateRandomHash(),
                    FileName = $"{query} - Resultado eMule 2.pdf",
                    FileSize = 2048000 + new Random().Next(1000000),
                    SourceCount = new Random().Next(1, 50),
                    CompleteSourceCount = new Random().Next(1, 20),
                    FileType = "pdf"
                }
            };

            lock (_lock)
            {
                if (_searchResults.ContainsKey(searchId))
                {
                    _searchResults[searchId].AddRange(results);
                }
            }

            // Disparar evento de resultados recibidos
            SearchResultsReceived?.Invoke(this, new EmuleSearchResultsEventArgs
            {
                SearchId = searchId,
                Results = results
            });
        }

        private string GenerateRandomHash()
        {
            var random = new Random();
            var hash = new byte[16];
            random.NextBytes(hash);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        private void ChangeState(EmuleConnectionState newState, string message)
        {
            var previousState = _state;
            _state = newState;

            StateChanged?.Invoke(this, new EmuleConnectionStateChangedEventArgs
            {
                PreviousState = previousState,
                CurrentState = newState,
                Message = message
            });
        }
    }
}
