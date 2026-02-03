using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SlskDown.Models;
using DownloadStatus = SlskDown.Models.DownloadStatus;

namespace SlskDown.Core
{
    /// <summary>
    /// Gestor de descargas multi-red que coordina descargas desde múltiples redes P2P
    /// Maneja cola unificada, priorización y failover entre redes
    /// </summary>
    public class MultiNetworkDownloadManager
    {
        private readonly Dictionary<string, IDownloadProvider> _downloadProviders = new Dictionary<string, IDownloadProvider>();
        private readonly ConcurrentDictionary<string, ActiveDownload> _activeDownloads = new ConcurrentDictionary<string, ActiveDownload>();
        private readonly ConcurrentQueue<QueuedDownload> _downloadQueue = new ConcurrentQueue<QueuedDownload>();
        private readonly object _lock = new object();
        private readonly int _maxConcurrentDownloads;
        private CancellationTokenSource _processingCts;
        private Task _processingTask;

        public event EventHandler<DownloadProgressEventArgs> DownloadProgress;
        public event EventHandler<DownloadCompletedEventArgs> DownloadCompleted;
        public event EventHandler<DownloadFailedEventArgs> DownloadFailed;

        public int ActiveDownloadCount => _activeDownloads.Count;
        public int QueuedDownloadCount => _downloadQueue.Count;

        public MultiNetworkDownloadManager(int maxConcurrentDownloads = 3)
        {
            _maxConcurrentDownloads = maxConcurrentDownloads;
        }

        /// <summary>
        /// Registra un proveedor de descarga
        /// </summary>
        public void RegisterDownloadProvider(string networkName, IDownloadProvider provider)
        {
            lock (_lock)
            {
                _downloadProviders[networkName] = provider;

                // Suscribirse a eventos
                provider.ProgressChanged += OnProviderProgressChanged;
                provider.DownloadCompleted += OnProviderDownloadCompleted;
                provider.DownloadFailed += OnProviderDownloadFailed;
            }
        }

        /// <summary>
        /// Encola una descarga desde cualquier red disponible
        /// </summary>
        public string QueueDownload(SearchResult result, string destinationPath, string preferredNetwork = null)
        {
            var downloadId = Guid.NewGuid().ToString("N");
            
            var queuedDownload = new QueuedDownload
            {
                DownloadId = downloadId,
                Result = result,
                DestinationPath = destinationPath,
                PreferredNetwork = preferredNetwork ?? result.NetworkSource,
                QueuedTime = DateTime.UtcNow
            };

            _downloadQueue.Enqueue(queuedDownload);

            // Iniciar procesamiento si no está activo
            EnsureProcessingStarted();

            return downloadId;
        }

        /// <summary>
        /// Cancela una descarga
        /// </summary>
        public async Task CancelDownloadAsync(string downloadId)
        {
            if (_activeDownloads.TryGetValue(downloadId, out var activeDownload))
            {
                var provider = _downloadProviders[activeDownload.ProviderName];
                await provider.CancelDownloadAsync(activeDownload.ProviderDownloadId);
                _activeDownloads.TryRemove(downloadId, out _);
            }
        }

        /// <summary>
        /// Obtiene el estado de una descarga
        /// </summary>
        public async Task<DownloadStatusInfo> GetDownloadStatusAsync(string downloadId)
        {
            if (_activeDownloads.TryGetValue(downloadId, out var activeDownload))
            {
                var provider = _downloadProviders[activeDownload.ProviderName];
                return await provider.GetDownloadStatusAsync(activeDownload.ProviderDownloadId);
            }

            return null;
        }

        /// <summary>
        /// Inicia el procesamiento de la cola de descargas
        /// </summary>
        private void EnsureProcessingStarted()
        {
            lock (_lock)
            {
                if (_processingTask == null || _processingTask.IsCompleted)
                {
                    _processingCts = new CancellationTokenSource();
                    _processingTask = Task.Run(() => ProcessDownloadQueueAsync(_processingCts.Token));
                }
            }
        }

        /// <summary>
        /// Procesa la cola de descargas
        /// </summary>
        private async Task ProcessDownloadQueueAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Esperar si ya hay demasiadas descargas activas
                    while (_activeDownloads.Count >= _maxConcurrentDownloads)
                    {
                        await Task.Delay(1000, cancellationToken);
                    }

                    // Intentar obtener siguiente descarga de la cola
                    if (!_downloadQueue.TryDequeue(out var queuedDownload))
                    {
                        await Task.Delay(500, cancellationToken);
                        continue;
                    }

                    // Iniciar descarga
                    await StartDownloadAsync(queuedDownload, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Log error pero continuar procesando
                    Console.WriteLine($"Error procesando cola de descargas: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Inicia una descarga desde el proveedor apropiado
        /// </summary>
        private async Task StartDownloadAsync(QueuedDownload queuedDownload, CancellationToken cancellationToken)
        {
            // Intentar con red preferida primero
            var networks = new List<string>();
            if (!string.IsNullOrEmpty(queuedDownload.PreferredNetwork))
            {
                networks.Add(queuedDownload.PreferredNetwork);
            }

            // Agregar otras redes como fallback
            networks.AddRange(_downloadProviders.Keys.Where(n => n != queuedDownload.PreferredNetwork));

            Exception lastException = null;

            foreach (var networkName in networks)
            {
                if (!_downloadProviders.TryGetValue(networkName, out var provider))
                {
                    continue;
                }

                if (!provider.IsReady)
                {
                    continue;
                }

                try
                {
                    var handle = await provider.StartDownloadAsync(
                        queuedDownload.Result,
                        queuedDownload.DestinationPath,
                        cancellationToken
                    );

                    // Registrar descarga activa
                    var activeDownload = new ActiveDownload
                    {
                        DownloadId = queuedDownload.DownloadId,
                        ProviderName = networkName,
                        ProviderDownloadId = handle.DownloadId,
                        Result = queuedDownload.Result,
                        StartTime = DateTime.UtcNow
                    };

                    _activeDownloads.TryAdd(queuedDownload.DownloadId, activeDownload);
                    return; // Éxito
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    // Intentar con siguiente red
                }
            }

            // Todas las redes fallaron
            DownloadFailed?.Invoke(this, new DownloadFailedEventArgs
            {
                DownloadId = queuedDownload.DownloadId,
                ProviderName = queuedDownload.PreferredNetwork,
                FileName = queuedDownload.Result.FileName,
                ErrorMessage = $"No se pudo iniciar descarga en ninguna red: {lastException?.Message}",
                Exception = lastException
            });
        }

        private void OnProviderProgressChanged(object sender, DownloadProgressEventArgs e)
        {
            DownloadProgress?.Invoke(this, e);
        }

        private void OnProviderDownloadCompleted(object sender, DownloadCompletedEventArgs e)
        {
            // Remover de descargas activas
            var activeDownload = _activeDownloads.Values.FirstOrDefault(d => d.ProviderDownloadId == e.DownloadId);
            if (activeDownload != null)
            {
                _activeDownloads.TryRemove(activeDownload.DownloadId, out _);
            }

            DownloadCompleted?.Invoke(this, e);
        }

        private void OnProviderDownloadFailed(object sender, DownloadFailedEventArgs e)
        {
            // Remover de descargas activas
            var activeDownload = _activeDownloads.Values.FirstOrDefault(d => d.ProviderDownloadId == e.DownloadId);
            if (activeDownload != null)
            {
                _activeDownloads.TryRemove(activeDownload.DownloadId, out _);
            }

            DownloadFailed?.Invoke(this, e);
        }

        /// <summary>
        /// Detiene el procesamiento de descargas
        /// </summary>
        public void Stop()
        {
            _processingCts?.Cancel();
        }

        private class QueuedDownload
        {
            public string DownloadId { get; set; }
            public SearchResult Result { get; set; }
            public string DestinationPath { get; set; }
            public string PreferredNetwork { get; set; }
            public DateTime QueuedTime { get; set; }
        }

        private class ActiveDownload
        {
            public string DownloadId { get; set; }
            public string ProviderName { get; set; }
            public string ProviderDownloadId { get; set; }
            public SearchResult Result { get; set; }
            public DateTime StartTime { get; set; }
        }
    }
}
