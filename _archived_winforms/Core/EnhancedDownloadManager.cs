using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Soulseek;
using SlskDown.Models;
using SlskDown.Core.Configuration;
using SlskDown.Core.Statistics;
using SlskDown.Core.Queue;
using SlskDown.Core.Events;
using SlskDown.Core.Transfers;
using SlskDown.Core.Protocol;

namespace SlskDown.Core
{
    /// <summary>
    /// DownloadManager mejorado con todas las optimizaciones de Nicotine+
    /// Ejemplo de integración completa de los 9 componentes
    /// </summary>
    public class EnhancedDownloadManager : IDisposable
    {
        // === COMPONENTES DE NICOTINE+ ===
        private readonly TransferConfiguration transferConfig;
        private readonly TransferStatistics transferStats;
        private readonly UserQueueManager queueManager;
        private readonly NetworkEventBus eventBus;
        private readonly SoulseekConnectionPool connectionPool;

        // === CONFIGURACIÓN ORIGINAL ===
        private readonly DownloadManagerConfig config;
        private readonly ISoulseekClient soulseekClient;
        private readonly Action<string> logger;

        // === ESTADO ===
        private readonly List<DownloadTask> downloadQueue;
        private readonly object downloadQueueLock = new object();
        private readonly Dictionary<DownloadTask, Task> activeDownloads;
        private readonly object activeDownloadsLock = new object();
        private bool isRunning = false;
        private bool isDisposed = false;

        // === TIMERS ===
        private System.Threading.Timer retryTimer;
        private System.Threading.Timer cleanupTimer;
        private System.Threading.Timer statsTimer;

        public EnhancedDownloadManager(
            DownloadManagerConfig config,
            ISoulseekClient soulseekClient,
            Action<string> logger = null)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.soulseekClient = soulseekClient ?? throw new ArgumentNullException(nameof(soulseekClient));
            this.logger = logger ?? (msg => Console.WriteLine(msg));

            // Inicializar estructuras de datos
            this.downloadQueue = new List<DownloadTask>();
            this.activeDownloads = new Dictionary<DownloadTask, Task>();

            // === INICIALIZAR COMPONENTES DE NICOTINE+ ===
            
            // 1. Configuración granular
            this.transferConfig = LoadOrCreateTransferConfiguration();
            Log($"TransferConfiguration cargada: {transferConfig.MaxParallelDownloads} descargas paralelas");

            // 2. Estadísticas detalladas
            this.transferStats = new TransferStatistics();
            Log($"TransferStatistics inicializado");

            // 3. Gestor de colas por usuario
            this.queueManager = new UserQueueManager(defaultQueueLimit: 50);
            Log($"UserQueueManager inicializado (límite por defecto: 50)");

            // 4. Sistema de eventos desacoplado
            this.eventBus = new NetworkEventBus();
            SetupEventHandlers();
            Log($"NetworkEventBus inicializado");

            // 5. Pool de conexiones
            this.connectionPool = new SoulseekConnectionPool(
                maxConnectionsPerUser: 3,
                idleTimeout: transferConfig.ConnectionPoolIdleTimeout
            );
            Log($"SoulseekConnectionPool inicializado");

            // Inicializar timers
            InitializeTimers();
        }

        private TransferConfiguration LoadOrCreateTransferConfiguration()
        {
            var configPath = Path.Combine(config.DataDirectory, "transfer_config.json");

            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var loaded = System.Text.Json.JsonSerializer.Deserialize<TransferConfiguration>(json);
                    Log($"Configuración cargada desde: {configPath}");
                    return loaded;
                }
                catch (Exception ex)
                {
                    Log($"Error cargando configuración: {ex.Message}");
                }
            }

            // Usar configuración optimizada para velocidad por defecto
            var defaultConfig = TransferConfiguration.CreateSpeedOptimized();
            defaultConfig.DownloadDirectory = config.DownloadDirectory;
            defaultConfig.IncompleteDirectory = config.IncompleteDownloadsDirectory;
            
            return defaultConfig;
        }

        private void SetupEventHandlers()
        {
            // Suscribirse a eventos propios para logging
            eventBus.Subscribe<TransferStartedMessage>(msg =>
            {
                Log($"Iniciada: {msg.FileName} desde {msg.Username}");
            });

            eventBus.Subscribe<TransferCompletedMessage>(msg =>
            {
                Log($"Completada: {msg.FileName} ({FormatSpeed(msg.AverageSpeed)} promedio)");
            });

            eventBus.Subscribe<TransferFailedMessage>(msg =>
            {
                Log($"Fallida: {msg.FileName} - {msg.ErrorMessage}");
            });

            eventBus.HandlerError += (sender, args) =>
            {
                Log($"Error en handler de evento {args.MessageType.Name}: {args.Exception.Message}");
            };
        }

        private void InitializeTimers()
        {
            // Timer de reintentos (cada 30 segundos)
            retryTimer = new System.Threading.Timer(_ => ProcessRetries(), null, 
                TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

            // Timer de limpieza (cada 5 minutos)
            cleanupTimer = new System.Threading.Timer(_ => PerformCleanup(), null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            // Timer de estadísticas (cada minuto)
            statsTimer = new System.Threading.Timer(_ => LogStatistics(), null,
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// Inicia el gestor de descargas
        /// </summary>
        public void Start()
        {
            if (isRunning)
                return;

            isRunning = true;
            Log("EnhancedDownloadManager iniciado");

            // Iniciar procesamiento de cola
            Task.Run(() => ProcessQueueLoop());
        }

        /// <summary>
        /// Detiene el gestor de descargas
        /// </summary>
        public async Task StopAsync()
        {
            if (!isRunning)
                return;

            isRunning = false;
            Log("Deteniendo EnhancedDownloadManager...");

            // Esperar a que terminen las descargas activas
            Task[] activeTasks;
            lock (activeDownloadsLock)
            {
                activeTasks = activeDownloads.Values.ToArray();
            }

            await Task.WhenAll(activeTasks);
            Log("EnhancedDownloadManager detenido");
        }

        /// <summary>
        /// Agrega una tarea a la cola de descargas
        /// </summary>
        public bool AddToQueue(DownloadTask task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            // MEJORA NICOTINE+: Verificar límite de cola del usuario
            if (!queueManager.CanQueueTransfer(task.Username))
            {
                var limit = queueManager.GetQueueLimit(task.Username);
                var current = queueManager.GetQueueSize(task.Username);

                Log($"Cola de {task.Username} llena ({current}/{limit})");

                task.Status = DownloadStatus.QueueFull;
                task.ErrorMessage = $"Cola del usuario llena ({current}/{limit})";
                return false;
            }

            // MEJORA NICOTINE+: Validar archivo parcial si existe
            if (!string.IsNullOrEmpty(task.LocalPath) && File.Exists(task.LocalPath))
            {
                if (!TransferCleanup.ValidatePartialFile(task, Log))
                {
                    Log($"Archivo parcial corrupto, reiniciando: {task.FileName}");
                }
            }

            lock (downloadQueueLock)
            {
                downloadQueue.Add(task);
                queueManager.IncrementQueueSize(task.Username);
            }

            Log($"Agregado a cola: {task.FileName} (cola de {task.Username}: {queueManager.GetQueueSize(task.Username)})");
            return true;
        }

        /// <summary>
        /// Bucle principal de procesamiento de cola
        /// </summary>
        private async Task ProcessQueueLoop()
        {
            while (isRunning)
            {
                try
                {
                    // Obtener siguiente tarea
                    DownloadTask nextTask = null;
                    lock (downloadQueueLock)
                    {
                        // Verificar si hay espacio para más descargas
                        int activeCount;
                        lock (activeDownloadsLock)
                        {
                            activeCount = activeDownloads.Count;
                        }

                        if (activeCount < transferConfig.MaxParallelDownloads)
                        {
                            // Buscar tarea lista para descargar
                            nextTask = downloadQueue
                                .Where(t => t.Status == DownloadStatus.Queued || t.Status == DownloadStatus.Paused)
                                .Where(t => !t.IsScheduled || t.ScheduledAt <= DateTime.UtcNow)
                                .OrderBy(t => t.QueuePosition ?? int.MaxValue)
                                .FirstOrDefault();

                            if (nextTask != null)
                            {
                                downloadQueue.Remove(nextTask);
                            }
                        }
                    }

                    if (nextTask != null)
                    {
                        // Iniciar descarga
                        var downloadTask = Task.Run(() => DownloadFileAsync(nextTask));
                        
                        lock (activeDownloadsLock)
                        {
                            activeDownloads[nextTask] = downloadTask;
                        }
                    }
                    else
                    {
                        // No hay tareas, esperar
                        await Task.Delay(1000);
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error en ProcessQueueLoop: {ex.Message}");
                    await Task.Delay(5000);
                }
            }
        }

        /// <summary>
        /// Descarga un archivo con todas las mejoras de Nicotine+
        /// </summary>
        private async Task DownloadFileAsync(DownloadTask task)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // MEJORA NICOTINE+: Registrar inicio en estadísticas
                transferStats.RecordTransferStart(task.Username, task.File?.Network ?? "Soulseek");

                // MEJORA NICOTINE+: Publicar evento de inicio
                eventBus.Publish(new TransferStartedMessage
                {
                    TransferId = task.Id,
                    Username = task.Username,
                    FileName = task.FileName,
                    FileSize = task.File?.SizeBytes ?? 0,
                    StartedAt = startTime
                });

                task.Status = DownloadStatus.Downloading;
                task.StartedAt = startTime;

                // Determinar timeout según red
                var timeout = task.File?.Network == "eMule"
                    ? transferConfig.EMuleStallTimeout
                    : transferConfig.StallTimeout;

                Log($"Descargando: {task.FileName} (timeout: {timeout.TotalMinutes:F0} min)");

                // Realizar descarga con progreso
                await PerformDownloadWithProgress(task, timeout);

                // Descarga exitosa
                var duration = DateTime.UtcNow - startTime;
                task.Status = DownloadStatus.Completed;
                task.CompletedAt = DateTime.UtcNow;

                // MEJORA NICOTINE+: Registrar éxito en estadísticas
                transferStats.RecordTransferSuccess(
                    task.Username,
                    task.File?.Network ?? "Soulseek",
                    task.File?.SizeBytes ?? 0,
                    duration
                );

                // MEJORA NICOTINE+: Publicar evento de completado
                eventBus.Publish(new TransferCompletedMessage
                {
                    TransferId = task.Id,
                    Username = task.Username,
                    FileName = task.FileName,
                    FileSize = task.File?.SizeBytes ?? 0,
                    CompletedAt = DateTime.UtcNow,
                    Duration = duration,
                    AverageSpeed = (task.File?.SizeBytes ?? 0) / duration.TotalSeconds
                });

                Log($"Completada: {task.FileName} en {FormatDuration(duration)}");
            }
            catch (Exception ex)
            {
                // MEJORA NICOTINE+: Clasificar error automáticamente
                var error = TransferError.FromException(ex);

                task.Status = MapFailureReasonToDownloadStatus(error.Reason);
                task.ErrorMessage = error.GetUserFriendlyMessage();

                // MEJORA NICOTINE+: Registrar fallo en estadísticas
                transferStats.RecordTransferFailure(
                    task.Username,
                    task.File?.Network ?? "Soulseek",
                    error.Reason.ToString()
                );

                // MEJORA NICOTINE+: Publicar evento de fallo
                eventBus.Publish(new TransferFailedMessage
                {
                    TransferId = task.Id,
                    Username = task.Username,
                    FileName = task.FileName,
                    ErrorMessage = error.Message,
                    FailureReason = error.Reason.ToString(),
                    FailedAt = DateTime.UtcNow
                });

                // Decidir si reintentar
                if (error.IsRetryable && task.RetryCount < transferConfig.MaxRetries)
                {
                    task.Status = DownloadStatus.Queued;
                    task.IsScheduled = true;
                    task.ScheduledAt = DateTime.UtcNow.Add(error.SuggestedRetryDelay);
                    task.RetryCount++;

                    Log($"Reintento programado en {error.SuggestedRetryDelay.TotalMinutes:F0} min (intento {task.RetryCount}/{transferConfig.MaxRetries})");

                    // Volver a agregar a cola
                    lock (downloadQueueLock)
                    {
                        downloadQueue.Add(task);
                    }
                }
                else
                {
                    Log($"Error no retryable: {error.Reason} - {error.Message}");

                    // MEJORA NICOTINE+: Cleanup robusto
                    await TransferCleanup.AbortTransferAsync(
                        task,
                        TransferStatus.Aborted,
                        error.Message,
                        Log
                    );
                }
            }
            finally
            {
                // MEJORA NICOTINE+: Liberar espacio en cola del usuario
                queueManager.DecrementQueueSize(task.Username);

                // Remover de descargas activas
                lock (activeDownloadsLock)
                {
                    activeDownloads.Remove(task);
                }
            }
        }

        private async Task PerformDownloadWithProgress(DownloadTask task, TimeSpan timeout)
        {
            // Simulación de descarga con progreso
            // En implementación real, usar soulseekClient.DownloadAsync con callbacks

            var totalBytes = task.File?.SizeBytes ?? 1000000;
            var bytesDownloaded = 0L;
            var lastUpdate = DateTime.UtcNow;

            while (bytesDownloaded < totalBytes)
            {
                // Simular descarga de chunk
                await Task.Delay(100);
                var chunkSize = Math.Min(8192, totalBytes - bytesDownloaded);
                bytesDownloaded += chunkSize;

                // Calcular velocidad
                var elapsed = DateTime.UtcNow - lastUpdate;
                var speed = elapsed.TotalSeconds > 0 ? chunkSize / elapsed.TotalSeconds : 0;

                // MEJORA NICOTINE+: Actualizar estadísticas de progreso
                transferStats.UpdateProgress(
                    task.Username,
                    task.File?.Network ?? "Soulseek",
                    bytesDownloaded,
                    bytesDownloaded - chunkSize,
                    speed
                );

                // MEJORA NICOTINE+: Publicar evento de progreso
                if (DateTime.UtcNow - lastUpdate > TimeSpan.FromSeconds(1))
                {
                    eventBus.Publish(new TransferProgressMessage
                    {
                        TransferId = task.Id,
                        Username = task.Username,
                        FileName = task.FileName,
                        BytesTransferred = bytesDownloaded,
                        TotalBytes = totalBytes,
                        Speed = speed,
                        Progress = (double)bytesDownloaded / totalBytes * 100
                    });

                    lastUpdate = DateTime.UtcNow;
                }
            }
        }

        private DownloadStatus MapFailureReasonToDownloadStatus(TransferFailureReason reason)
        {
            return reason switch
            {
                TransferFailureReason.ConnectionTimeout => DownloadStatus.Failed,
                TransferFailureReason.UserLoggedOff => DownloadStatus.UserOffline,
                TransferFailureReason.QueueFull => DownloadStatus.QueueFull,
                TransferFailureReason.DiskFull => DownloadStatus.Failed,
                _ => DownloadStatus.Failed
            };
        }

        private void ProcessRetries()
        {
            if (!isRunning)
                return;

            lock (downloadQueueLock)
            {
                var now = DateTime.UtcNow;
                var readyForRetry = downloadQueue
                    .Where(t => t.IsScheduled && t.ScheduledAt <= now)
                    .ToList();

                foreach (var task in readyForRetry)
                {
                    task.IsScheduled = false;
                    task.ScheduledAt = null;
                    Log($"Reintentando: {task.FileName}");
                }
            }
        }

        private void PerformCleanup()
        {
            if (!isRunning)
                return;

            try
            {
                // MEJORA NICOTINE+: Limpiar conexiones idle del pool
                connectionPool.CleanupIdleConnections();

                // MEJORA NICOTINE+: Limpiar archivos temporales
                TransferCleanup.CleanupTemporaryFiles(config.DownloadDirectory, Log);

                // MEJORA NICOTINE+: Limpiar usuarios inactivos del queue manager
                queueManager.CleanupInactiveUsers(TimeSpan.FromHours(1));

                Log("Limpieza periódica completada");
            }
            catch (Exception ex)
            {
                Log($"Error en limpieza: {ex.Message}");
            }
        }

        private void LogStatistics()
        {
            if (!isRunning)
                return;

            try
            {
                // MEJORA NICOTINE+: Obtener y mostrar estadísticas
                var globalStats = transferStats.GetGlobalStats();
                var poolStats = connectionPool.GetStatistics();
                var queueStats = queueManager.GetStatistics();

                Log($"Estadísticas:");
                Log($"   Transferencias: {globalStats.TotalTransfers} ({globalStats.SuccessRate:P1} éxito)");
                Log($"   Velocidad promedio: {FormatSpeed(globalStats.AverageSpeed)}");
                Log($"   Pool: {poolStats.ActiveConnections} activas, {poolStats.IdleConnections} idle (hit rate: {poolStats.HitRate:P1})");
                Log($"   Colas: {queueStats.TotalUsers} usuarios, {queueStats.TotalQueuedTransfers} en cola");
            }
            catch (Exception ex)
            {
                Log($"Error obteniendo estadísticas: {ex.Message}");
            }
        }

        private string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond <= 0)
                return "0 KB/s";

            const double KB = 1024;
            const double MB = KB * 1024;

            if (bytesPerSecond >= MB)
                return $"{bytesPerSecond / MB:F2} MB/s";

            return $"{bytesPerSecond / KB:F2} KB/s";
        }

        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalSeconds < 60)
                return $"{duration.TotalSeconds:F0}s";

            if (duration.TotalMinutes < 60)
                return $"{duration.TotalMinutes:F1}m";

            return $"{duration.TotalHours:F1}h";
        }

        private void Log(string message)
        {
            logger?.Invoke($"[EnhancedDM] {message}");
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;

            // Detener timers
            retryTimer?.Dispose();
            cleanupTimer?.Dispose();
            statsTimer?.Dispose();

            // Limpiar componentes
            connectionPool?.Dispose();
            eventBus?.Dispose();

            Log("🔌 EnhancedDownloadManager disposed");
        }
    }
}
