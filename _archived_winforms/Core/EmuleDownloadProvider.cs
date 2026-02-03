using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Proveedor de descargas para eMule/ed2k
    /// Implementa IDownloadProvider para integración con NetworkOrchestrator
    /// </summary>
    public class EmuleDownloadProvider : IDownloadProvider
    {
        private readonly EmuleClient _client;
        private bool _isReady;

        public string ProviderName => "eMule";
        public bool IsReady => _isReady && _client.IsConnected;

        public event EventHandler<DownloadProgressEventArgs> ProgressChanged;
        public event EventHandler<DownloadCompletedEventArgs> DownloadCompleted;
        public event EventHandler<DownloadFailedEventArgs> DownloadFailed;

        public EmuleDownloadProvider(EmuleClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            
            // Suscribirse a eventos del cliente
            _client.StateChanged += OnClientStateChanged;
        }

        /// <summary>
        /// Inicializa el proveedor conectándose a eMule
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _client.ConnectAsync(cancellationToken);
                _isReady = true;
            }
            catch (Exception ex)
            {
                _isReady = false;
                throw new InvalidOperationException($"Error inicializando eMule: {ex.Message}", ex);
            }
        }

        public async Task<DownloadHandle> StartDownloadAsync(
            SearchResult result, 
            string destinationPath, 
            CancellationToken cancellationToken = default)
        {
            if (!IsReady)
            {
                throw new InvalidOperationException("eMule no está listo");
            }

            try
            {
                // Convertir SearchResult a EmuleSearchResult
                var emuleResult = new EmuleSearchResult
                {
                    FileHash = result.FileHash,
                    FileName = result.FileName,
                    FileSize = result.SizeBytes,
                    FileType = Path.GetExtension(result.FileName).TrimStart('.')
                };

                var download = await _client.DownloadAsync(emuleResult, destinationPath, cancellationToken);

                return new DownloadHandle
                {
                    DownloadId = download.DownloadId,
                    ProviderName = ProviderName,
                    FileName = download.FileName,
                    TotalBytes = download.FileSize,
                    DestinationPath = destinationPath,
                    StartTime = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                DownloadFailed?.Invoke(this, new DownloadFailedEventArgs
                {
                    ProviderName = ProviderName,
                    FileName = result.FileName,
                    ErrorMessage = ex.Message,
                    Exception = ex
                });
                throw;
            }
        }

        public Task PauseDownloadAsync(string downloadId)
        {
            // eMule no soporta pausa directamente, se implementaría deteniendo y guardando estado
            return Task.CompletedTask;
        }

        public Task ResumeDownloadAsync(string downloadId)
        {
            // eMule maneja reintentos automáticamente
            return Task.CompletedTask;
        }

        public Task CancelDownloadAsync(string downloadId)
        {
            // Enviar comando de cancelación a eMule
            return Task.CompletedTask;
        }

        public Task<DownloadStatusInfo> GetDownloadStatusAsync(string downloadId)
        {
            // Consultar estado de descarga en eMule
            // Por ahora retornar estado simulado
            return Task.FromResult(new DownloadStatusInfo
            {
                DownloadId = downloadId,
                State = DownloadState.Downloading,
                BytesDownloaded = 0,
                TotalBytes = 0,
                SpeedBytesPerSecond = 0
            });
        }

        private void OnClientStateChanged(object sender, EmuleConnectionStateChangedEventArgs e)
        {
            _isReady = e.CurrentState == EmuleConnectionState.Connected;
        }

        public void Dispose()
        {
            _client.StateChanged -= OnClientStateChanged;
            _client.DisconnectAsync().Wait();
        }
    }
}
