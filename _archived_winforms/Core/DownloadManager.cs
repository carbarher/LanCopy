using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Soulseek;
using SlskDown.Models;
using DownloadStatus = SlskDown.Models.DownloadStatus;

// INTEGRACIÓN NICOTINE+: Componentes mejorados
using SlskDown.Core.Configuration;
using SlskDown.Core.Statistics;
using SlskDown.Core.Queue;
using SlskDown.Core.Events;
using SlskDown.Core.Protocol;
using SlskDown.Core.Prioritization;
using SlskDown.Core.Users;
using SlskDown.Core.Retry;
using SlskDown.Core.Files;

namespace SlskDown.Core
{
    /// <summary>
    /// Gestiona la cola de descargas, reintentos y búsqueda de proveedores alternativos
    /// </summary>
    public partial class DownloadManager
    {
        // Configuración
        private readonly DownloadManagerConfig config;
        
        // INTEGRACIÓN NICOTINE+: Componentes mejorados (base)
        private readonly TransferConfiguration transferConfig;
        private readonly TransferStatistics transferStats;
        private readonly UserQueueManager queueManager;
        private readonly NetworkEventBus eventBus;
        private readonly SlskDown.Core.Protocol.SoulseekConnectionPool connectionPool;
        
        // Callback para métricas
        public Action<long> OnFileDownloaded { get; set; }
        
        // INTEGRACIÓN NICOTINE+: Componentes avanzados
        private readonly DynamicDownloadPrioritizer prioritizer;
        private readonly UserBanManager banManager;
        private readonly IntelligentRetryStrategy retryStrategy;
        private readonly PartialFileManager partialManager;
        
        // Estado
        private readonly List<DownloadTask> downloadQueue;
        private readonly object downloadQueueLock;
        private readonly DownloadQueueService downloadQueueService;
        private readonly SemaphoreSlim saveQueueSemaphore = new SemaphoreSlim(1, 1);
        private System.Threading.Timer queuePositionTimer;
        private System.Threading.Timer retryConnectionTimer;
        private System.Threading.Timer retryIoTimer;
        private System.Threading.Timer cleanupTimer;
        private System.Threading.Timer stallWatchdogTimer;
        private System.Threading.Timer periodicSaveTimer;  // Nicotine+ inspired: save every 3 minutes
        private bool isRunning = false;
        private bool allowSavingTransfers = false;  // Nicotine+ inspired: prevent saves during shutdown
        private bool transfersModified = false;     // Nicotine+ inspired: only save if modified
        
        // Estadísticas y tracking
        private readonly Dictionary<string, SlskDown.Models.ProviderStats> providerStats = new Dictionary<string, SlskDown.Models.ProviderStats>();
        private readonly object providerStatsLock = new object();
        private readonly Dictionary<string, int> downloadRetryCount = new Dictionary<string, int>();
        private readonly object retryCountLock = new object();
        private QueuePrioritizationStrategy queueStrategy = QueuePrioritizationStrategy.Balanced;
        private readonly SemaphoreSlim saveStateSemaphore = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim statePersistenceSemaphore = new SemaphoreSlim(1, 1);
        private volatile bool stateSaveScheduled = false;

        private readonly object providerRetryBudgetLock = new object();
        private readonly Dictionary<string, Queue<DateTime>> providerRetryBudgetFailures =
            new Dictionary<string, Queue<DateTime>>(StringComparer.OrdinalIgnoreCase);
        private const int PROVIDER_RETRY_BUDGET_WINDOW_MINUTES = 15;
        private const int PROVIDER_RETRY_BUDGET_MAX_FAILURES = 10;
        private const int PROVIDER_RETRY_BUDGET_COOLDOWN_MINUTES = 15;

        private const long MIN_FREE_DISK_BYTES = 1L * 1024 * 1024 * 1024; // 1 GiB
        private static readonly TimeSpan DISK_SPACE_DEFER_DELAY = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan INCOMPLETE_PARTIAL_MAX_AGE = TimeSpan.FromDays(30);
        private static readonly TimeSpan INCOMPLETE_ZERO_BYTE_MAX_AGE = TimeSpan.FromDays(1);

        private readonly Dictionary<string, Dictionary<string, int>> fileProviderSuccessCounts =
            new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        private readonly object fileProviderLock = new object();
        
        // Blacklist temporal de proveedores con tiempo de espera progresivo
        private readonly Dictionary<string, (int failures, DateTime lastFail, DateTime blockedUntil)> providerBlacklist = 
            new Dictionary<string, (int failures, DateTime lastFail, DateTime blockedUntil)>();
        private readonly object blacklistLock = new object();

        private readonly Dictionary<string, DateTime> providerCooldownUntil = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DownloadFailureReason> providerCooldownReason = new Dictionary<string, DownloadFailureReason>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> providerConsecutiveFailures = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, int> providerCircuitFailures = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> providerCircuitOpenUntilUtc = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> providerHalfOpenInFlight = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object circuitLock = new object();
        private static readonly Random jitterRng = new Random();
        
        // MEJORA: Tracking de tareas activas para espera graceful
        private readonly Dictionary<DownloadTask, Task> activeDownloadTasks = new Dictionary<DownloadTask, Task>();
        private readonly object activeTasksLock = new object();
        
        // MEJORAS #1-7: Nuevas funcionalidades
        private readonly DownloadProgressPersistence progressPersistence;
        private readonly DownloadErrorManager errorManager;
        private readonly DownloadNotificationManager notificationManager;
        private readonly DownloadSpeedLimiter speedLimiter;
        private readonly AlternativeSourceFinder sourceFinder;
        private readonly DownloadStatisticsTracker statsTracker;
        private System.Threading.Timer progressSaveTimer;
        
        // MEJORAS #8-16: Funcionalidades avanzadas
        private readonly IntelligentRetryManager intelligentRetry;
        private readonly MetadataCache metadataCache;
        private readonly AggressiveDownloadMode aggressiveMode;
        private readonly FastSearchIndex searchIndex;
        private readonly ProviderPrefetchManager prefetchManager;
        private readonly RealtimeDashboard dashboard;

        private readonly Dictionary<DownloadTask, (long lastBytes, DateTime lastProgressUtc)> stallTracker = new Dictionary<DownloadTask, (long lastBytes, DateTime lastProgressUtc)>();
        private readonly object stallTrackerLock = new object();

        private int EffectiveProviderBlacklistThreshold => Math.Max(1, config?.ProviderBlacklistThreshold ?? 3);
        private double EffectiveProviderBlacklistHours => Math.Max(0.1, config?.ProviderBlacklistHours ?? 1);
        private int EffectiveMaxTotalAttempts => Math.Max(1, config?.MaxTotalAttempts ?? 15);
        private const int STATE_SAVE_DELAY_MS = 1000;
        private const int MAX_ADAPTIVE_PER_PROVIDER_CAP = 2;

        private static long GetAvailableDiskBytes(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return 0;
                }

                var root = Path.GetPathRoot(path);
                if (string.IsNullOrWhiteSpace(root))
                {
                    return 0;
                }

                var drive = new DriveInfo(root);
                return drive.AvailableFreeSpace;
            }
            catch
            {
                return 0;
            }
        }

        private bool TryDeferTaskDueToDiskSpace(DownloadTask task, out string reason)
        {
            reason = string.Empty;
            if (task?.File == null || task.File.SizeBytes <= 0)
            {
                return false;
            }

            var folder = !string.IsNullOrWhiteSpace(config?.IncompleteDownloadsDirectory)
                ? config.IncompleteDownloadsDirectory
                : config?.DownloadDirectory;

            var freeBytes = GetAvailableDiskBytes(folder);
            if (freeBytes <= 0)
            {
                return false;
            }

            long existingBytes = 0;
            try
            {
                if (!string.IsNullOrWhiteSpace(task.LocalPath) && System.IO.File.Exists(task.LocalPath))
                {
                    existingBytes = new FileInfo(task.LocalPath).Length;
                }
            }
            catch
            {
                existingBytes = 0;
            }

            var remainingBytes = Math.Max(0L, task.File.SizeBytes - existingBytes);
            var requiredBytes = remainingBytes + MIN_FREE_DISK_BYTES;

            if (freeBytes >= requiredBytes)
            {
                return false;
            }

            reason = $"sin espacio en disco (libre {freeBytes / (1024.0 * 1024 * 1024):F2} GiB, requerido ~{requiredBytes / (1024.0 * 1024 * 1024):F2} GiB)";
            var reasonText = reason;

            var deferredUtc = DateTime.UtcNow.Add(DISK_SPACE_DEFER_DELAY);
            downloadQueueService.Update(list =>
            {
                if (!list.Contains(task))
                {
                    return false;
                }

                task.Status = DownloadStatus.Queued;
                task.IsScheduled = true;
                task.ScheduledAt = deferredUtc;
                if (string.IsNullOrWhiteSpace(task.ErrorMessage))
                {
                    task.ErrorMessage = reasonText;
                }
                return true;
            });

            return true;
        }

        private void RegisterRetryBudgetFailure(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            var nowUtc = DateTime.UtcNow;
            var cutoff = nowUtc.AddMinutes(-PROVIDER_RETRY_BUDGET_WINDOW_MINUTES);

            lock (providerRetryBudgetLock)
            {
                if (!providerRetryBudgetFailures.TryGetValue(username, out var q))
                {
                    q = new Queue<DateTime>();
                    providerRetryBudgetFailures[username] = q;
                }

                q.Enqueue(nowUtc);
                while (q.Count > 0 && q.Peek() < cutoff)
                {
                    q.Dequeue();
                }

                if (q.Count < PROVIDER_RETRY_BUDGET_MAX_FAILURES)
                {
                    return;
                }
            }

            lock (blacklistLock)
            {
                var until = nowUtc.AddMinutes(PROVIDER_RETRY_BUDGET_COOLDOWN_MINUTES);
                if (providerCooldownUntil.TryGetValue(username, out var existing) && existing > nowUtc)
                {
                    until = MaxUtc(until, existing);
                }
                providerCooldownUntil[username] = until;
                providerCooldownReason[username] = DownloadFailureReason.Connection;
                Log($"🧯 Retry budget: cooldown extra para {username} hasta {until.ToLocalTime():HH:mm} ({PROVIDER_RETRY_BUDGET_MAX_FAILURES}+ fallos/{PROVIDER_RETRY_BUDGET_WINDOW_MINUTES}m)");
            }
        }

        private void ClearRetryBudgetForProvider(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            lock (providerRetryBudgetLock)
            {
                providerRetryBudgetFailures.Remove(username);
            }
        }

        private static DateTime MaxUtc(DateTime aUtc, DateTime bUtc)
        {
            return aUtc >= bUtc ? aUtc : bUtc;
        }

        private TimeSpan ComputeRetryBackoffDelay(int retryCount)
        {
            var initialMs = Math.Max(100, config.RetryBackoffMilliseconds);
            var maxSeconds = Math.Max(1, config.RetryBackoffMaxSeconds);
            var jitterRatio = Math.Max(0.0, Math.Min(0.5, config.RetryBackoffJitterRatio));

            var exponent = Math.Min(Math.Max(0, retryCount), 8);
            var delayMs = initialMs * Math.Pow(2, exponent);
            delayMs = Math.Min(delayMs, maxSeconds * 1000.0);

            var jitter = 1.0;
            lock (jitterRng)
            {
                jitter = 1.0 + ((jitterRng.NextDouble() * 2.0) - 1.0) * jitterRatio;
            }

            delayMs = Math.Max(0, delayMs * jitter);
            return TimeSpan.FromMilliseconds(delayMs);
        }

        private DateTime ComputeNextEligibleUtc(DownloadTask task, TimeSpan baseDelay)
        {
            var nowUtc = DateTime.UtcNow;
            var nextUtc = nowUtc.Add(baseDelay);

            try
            {
                if (task?.File != null && !string.IsNullOrWhiteSpace(task.File.Username))
                {
                    if (TryGetCooldownInfo(task.File.Username, out _, out var cooldownRemaining))
                    {
                        nextUtc = MaxUtc(nextUtc, nowUtc.Add(cooldownRemaining));
                    }

                    if (IsProviderCircuitOpen(task.File.Username, out var circuitRemaining))
                    {
                        nextUtc = MaxUtc(nextUtc, nowUtc.Add(circuitRemaining));
                    }
                }
            }
            catch
            {
            }

            return DateTime.SpecifyKind(nextUtc, DateTimeKind.Utc);
        }

        private static bool IsEligibleToStartQueuedTask(DownloadTask task, DateTime nowUtc)
        {
            if (task == null)
            {
                return false;
            }

            if (task.Status != DownloadStatus.Queued)
            {
                return false;
            }

            if (!task.IsScheduled || !task.ScheduledAt.HasValue)
            {
                return true;
            }

            var scheduledUtc = NormalizeToUtc(task.ScheduledAt.Value);
            return scheduledUtc <= nowUtc;
        }

        // Callbacks
        public Action<string> OnLog { get; set; }
        public Action<DownloadTask, string> OnDownloadStatusUpdate { get; set; }
        public Action OnQueueChanged { get; set; }
        public Func<DownloadTask, Task> OnDownloadFile { get; set; }
        public Func<DownloadTask, Task<SearchAlternativesResult>> OnSearchAlternatives { get; set; }
        public Func<DownloadTask, Task> OnRequestQueuePosition { get; set; }
        public Action<int> OnApplyDownloadSpeedLimit { get; set; }
        public Func<DownloadTask, Task> OnPostDownloadAction { get; set; }

        public DownloadManagerConfig Config => config;
        
        public DownloadManager(
            DownloadManagerConfig configuration,
            List<DownloadTask> sharedQueue = null,
            object sharedQueueLock = null,
            DownloadQueueService queueService = null)
        {
            config = configuration ?? throw new ArgumentNullException(nameof(configuration));
            downloadQueue = sharedQueue ?? new List<DownloadTask>();
            downloadQueueLock = sharedQueueLock ?? new object();
            downloadQueueService = queueService ?? new DownloadQueueService(downloadQueue, downloadQueueLock);
            queueStrategy = configuration.QueueStrategy;

            // INTEGRACIÓN NICOTINE+: Inicializar componentes mejorados (base)
            transferConfig = TransferConfiguration.CreateDefault();
            transferStats = new TransferStatistics();
            queueManager = new UserQueueManager(defaultQueueLimit: 50);
            eventBus = new NetworkEventBus();
            connectionPool = new SlskDown.Core.Protocol.SoulseekConnectionPool(
                connectionTimeout: TimeSpan.FromSeconds(30),
                idleTimeout: TimeSpan.FromMinutes(5)
            );
            
            // INTEGRACIÓN NICOTINE+: Inicializar componentes avanzados
            prioritizer = new DynamicDownloadPrioritizer(transferStats, username => true);
            banManager = new UserBanManager(new BanConfig
            {
                MaxFailures = 5,
                TimeWindow = TimeSpan.FromHours(1),
                BanDuration = TimeSpan.FromHours(24)
            });
            retryStrategy = new IntelligentRetryStrategy(new RetryConfig
            {
                BaseDelay = TimeSpan.FromMinutes(1),
                MaxDelay = TimeSpan.FromHours(1),
                MaxRetries = 5
            });
            partialManager = new PartialFileManager();
            
            // MEJORAS #1-7: Inicializar nuevas funcionalidades
            var dataDir = config.DownloadDirectory ?? Environment.CurrentDirectory;
            try
            {
                progressPersistence = new DownloadProgressPersistence(dataDir);
                errorManager = new DownloadErrorManager(config.MaxFailuresPerFile);
                notificationManager = new DownloadNotificationManager(config.EnableNotifications, config.EnableSoundOnComplete);
                speedLimiter = new DownloadSpeedLimiter(
                    config.MaxDownloadSpeedKBps,
                    config.EnableScheduledSpeed,
                    config.NightSpeedStartHour,
                    config.NightSpeedEndHour
                );
                sourceFinder = new AlternativeSourceFinder(config.MinDownloadSpeedKBps, config.SourceSearchDelaySeconds);
                statsTracker = new DownloadStatisticsTracker(dataDir, config.StatsHistoryDays);
                
                // MEJORAS #8-16: Inicializar funcionalidades avanzadas
                intelligentRetry = new IntelligentRetryManager();
                metadataCache = new MetadataCache(dataDir);
                aggressiveMode = new AggressiveDownloadMode(config.MaxSimultaneousDownloads, config.AggressiveModeMaxDownloads);
                searchIndex = new FastSearchIndex();
                prefetchManager = new ProviderPrefetchManager();
                dashboard = new RealtimeDashboard();
                
                Log("Funcionalidades avanzadas inicializadas correctamente");
            }
            catch (Exception ex)
            {
                Log($"Error inicializando funcionalidades avanzadas: {ex.Message}");
                // Continuar sin las funcionalidades avanzadas
            }

            queuePositionTimer = CreateTimer("QueuePositionTimer", OnQueuePositionTimer, config.QueuePositionRefreshIntervalSeconds);
            retryConnectionTimer = CreateTimer("RetryConnectionTimer", OnRetryConnectionTimer, config.RetryConnectionIntervalSeconds);
            retryIoTimer = CreateTimer("RetryIoTimer", OnRetryIoTimer, config.RetryIoIntervalSeconds);
            cleanupTimer = CreateTimer("CleanupTimer", OnCleanupTimer, config.CleanupIntervalSeconds);
            stallWatchdogTimer = CreateTimer("StallWatchdogTimer", OnStallWatchdogTimer, config.StallWatchdogIntervalSeconds);
        }
        
        /// <summary>
        /// Inicia el gestor de descargas
        /// </summary>
        public void Start()
        {
            if (isRunning) return;
            
            isRunning = true;
            allowSavingTransfers = true;
            transfersModified = false;
            
            // Iniciar timer de guardado periódico (cada 3 minutos como Nicotine+)
            periodicSaveTimer = new System.Threading.Timer(
                _ => OnPeriodicSaveTimer(),
                null,
                TimeSpan.FromMinutes(3),
                TimeSpan.FromMinutes(3)
            );
            
            // MEJORA #1: Iniciar timer de guardado de progreso
            if (config.EnableProgressPersistence)
            {
                progressSaveTimer = new System.Threading.Timer(
                    _ => SaveAllProgress(),
                    null,
                    TimeSpan.FromSeconds(config.ProgressSaveIntervalSeconds),
                    TimeSpan.FromSeconds(config.ProgressSaveIntervalSeconds)
                );
            }
            
            ResetTimers();
            _ = Task.Run(async () => await LoadProviderStateAsync());
            _ = Task.Run(async () => await LoadFileProviderPreferencesAsync());
            Task.Run(async () => await ProcessQueueLoop());
            Log("Download Manager iniciado (guardado periódico cada 3 min)");
        }

        public bool IsRunning => isRunning;

        /// <summary>
        /// Detiene el gestor de descargas
        /// </summary>
        public void Stop()
        {
            isRunning = false;
            allowSavingTransfers = false;
            
            // INTEGRACIÓN NICOTINE+: Guardar estado de componentes avanzados
            _ = Task.Run(async () =>
            {
                try
                {
                    var dataDir = config.DownloadDirectory ?? Environment.CurrentDirectory;
                    await banManager.SaveToFileAsync(Path.Combine(dataDir, "banned_users.json"));
                }
                catch (Exception ex)
                {
                    Log($"Error guardando bans: {ex.Message}");
                }
            });
            
            queuePositionTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            retryConnectionTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            retryIoTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            cleanupTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            stallWatchdogTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            progressSaveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            periodicSaveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            periodicSaveTimer?.Dispose();
            periodicSaveTimer = null;

            lock (stallTrackerLock)
            {
                stallTracker.Clear();
            }
            
            Log("Download Manager detenido");
        }
        
        /// <summary>
        /// MEJORA: Espera gracefully a que las descargas activas terminen
        /// </summary>
        public async Task WaitForActiveDownloadsAsync(int timeoutSeconds = 30)
        {
            Task[] activeTasks;
            lock (activeTasksLock)
            {
                activeTasks = activeDownloadTasks.Values.ToArray();
            }
            
            if (activeTasks.Length == 0)
            {
                Log("No hay descargas activas que esperar");
                return;
            }
            
            Log($"Esperando a que {activeTasks.Length} descargas activas terminen (timeout: {timeoutSeconds}s)...");
            
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                await Task.WhenAll(activeTasks).WaitAsync(cts.Token);
                Log($"Todas las descargas activas han terminado");
            }
            catch (OperationCanceledException)
            {
                Log($"Timeout esperando descargas activas ({timeoutSeconds}s)");
            }
            catch (Exception ex)
            {
                Log($"Error esperando descargas: {ex.Message}");
            }
        }

        private async Task LoadFileProviderPreferencesAsync()
        {
            if (string.IsNullOrWhiteSpace(config?.FileProviderPreferencesPath))
            {
                return;
            }

            var path = config.FileProviderPreferencesPath;
            try
            {
                if (!System.IO.File.Exists(path))
                {
                    return;
                }

                var json = await System.IO.File.ReadAllTextAsync(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                var items = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (items == null)
                {
                    return;
                }

                lock (fileProviderLock)
                {
                    fileProviderSuccessCounts.Clear();
                    foreach (var kvp in items)
                    {
                        if (string.IsNullOrWhiteSpace(kvp.Key) || kvp.Value == null)
                        {
                            continue;
                        }
                        fileProviderSuccessCounts[kvp.Key] = new Dictionary<string, int>(kvp.Value, StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error cargando preferencias por archivo: {ex.Message}");
            }
        }

        private void RecordFileProviderSuccess(string fileKey, string username)
        {
            if (string.IsNullOrWhiteSpace(fileKey) || string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            lock (fileProviderLock)
            {
                if (!fileProviderSuccessCounts.TryGetValue(fileKey, out var counts))
                {
                    counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    fileProviderSuccessCounts[fileKey] = counts;
                }

                counts.TryGetValue(username, out var current);
                counts[username] = current + 1;
            }

            ScheduleStateSave();
        }

        private List<string> GetPreferredProvidersForFile(string fileKey, int maxEntries = 5)
        {
            if (string.IsNullOrWhiteSpace(fileKey))
            {
                return new List<string>();
            }

            lock (fileProviderLock)
            {
                if (!fileProviderSuccessCounts.TryGetValue(fileKey, out var counts) || counts.Count == 0)
                {
                    return new List<string>();
                }

                return counts
                    .OrderByDescending(kvp => kvp.Value)
                    .ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                    .Take(Math.Max(1, maxEntries))
                    .Select(kvp => kvp.Key)
                    .ToList();
            }
        }
        
        /// <summary>
        /// MEJORA: Obtiene el número de descargas activas en ejecución
        /// </summary>
        public int GetActiveDownloadsInProgressCount()
        {
            lock (activeTasksLock)
            {
                return activeDownloadTasks.Count;
            }
        }
        
        /// <summary>
        /// Agrega una tarea a la cola de descargas
        /// </summary>
        public bool AddToQueue(DownloadTask task, bool prioritizeBySize = false)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            
            int queueCount = downloadQueueService.Update(list =>
            {
                bool alreadyQueued = list.Any(t =>
                    t.File.FileName.Equals(task.File.FileName, StringComparison.OrdinalIgnoreCase) &&
                    t.File.Username.Equals(task.File.Username, StringComparison.OrdinalIgnoreCase));

                if (alreadyQueued)
                {
                    return -1;
                }

                if (prioritizeBySize)
                {
                    int insertIndex = list.Count;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var existing = list[i];
                        if (existing.Status == DownloadStatus.Queued && existing.File.SizeBytes > task.File.SizeBytes)
                        {
                            insertIndex = i;
                            break;
                        }
                    }
                    list.Insert(insertIndex, task);
                }
                else
                {
                    list.Add(task);
                }

                if (queueStrategy != QueuePrioritizationStrategy.Manual)
                {
                    ApplyQueuePrioritization(list);
                }

                return list.Count;
            });

            if (queueCount == -1)
            {
                return false;
            }

            OnQueueChanged?.Invoke();
            Log($"➕ Agregado a cola: {task.File.FileName} ({queueCount} en cola)");
            MarkTransfersModified();  // Nicotine+ inspired: mark for periodic save
            ScheduleQueueSave();
            return true;
        }

        public void NotifyQueueChanged()
        {
            OnQueueChanged?.Invoke();
        }
        
        public bool PauseTask(DownloadTask task)
        {
            if (task == null) return false;

            bool paused = downloadQueueService.Update(list =>
            {
                if (!list.Contains(task))
                {
                    return false;
                }

                if (task.Status == DownloadStatus.Downloading || task.Status == DownloadStatus.Queued)
                {
                    bool shouldCancel = task.Status == DownloadStatus.Downloading;
                    task.Status = DownloadStatus.Paused;
                    if (shouldCancel)
                    {
                        try { task.CancellationToken?.Cancel(); } catch { }
                    }

                    return true;
                }

                return false;
            });

            if (paused)
            {
                OnDownloadStatusUpdate?.Invoke(task, "Pausado");
                OnQueueChanged?.Invoke();
                ScheduleQueueSave();
            }

            return paused;
        }

        public bool ResumeTask(DownloadTask task)
        {
            if (task == null) return false;

            bool resumed = downloadQueueService.Update(list =>
            {
                if (!list.Contains(task))
                {
                    return false;
                }

                if (task.Status == DownloadStatus.Paused)
                {
                    task.Status = DownloadStatus.Queued;
                    task.CancellationToken = null;
                    return true;
                }

                return false;
            });

            if (resumed)
            {
                OnDownloadStatusUpdate?.Invoke(task, "⏳ Pendiente");
                OnQueueChanged?.Invoke();
                ScheduleQueueSave();
            }

            return resumed;
        }

        public bool CancelTask(DownloadTask task, bool removeFromQueue = false)
        {
            if (task == null) return false;

            bool cancelled = downloadQueueService.Update(list =>
            {
                if (!list.Contains(task))
                {
                    return false;
                }

                task.Status = DownloadStatus.Cancelled;
                task.EndTime = DateTime.Now;

                if (removeFromQueue)
                {
                    list.Remove(task);
                }

                return true;
            });

            if (cancelled)
            {
                try { task.CancellationToken?.Cancel(); } catch { }
                OnDownloadStatusUpdate?.Invoke(task, "Cancelado");
                OnQueueChanged?.Invoke();
                ScheduleQueueSave();
            }

            return cancelled;
        }

        public IReadOnlyList<DownloadTask> ClearCompletedTasks()
        {
            var removed = downloadQueueService.Update(list =>
            {
                var tasks = list
                    .Where(t => t.Status == DownloadStatus.Completed || t.Status == DownloadStatus.Cancelled)
                    .ToList();

                if (tasks.Count > 0)
                {
                    foreach (var task in tasks)
                    {
                        list.Remove(task);
                    }
                }

                return tasks;
            });

            if (removed.Count > 0)
            {
                OnQueueChanged?.Invoke();
                ScheduleQueueSave();
            }

            return removed;
        }

        private void ScheduleQueueSave()
        {
            _ = SaveQueueAsync();
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
            catch { }
        }

        /// <summary>
        /// Obtiene todas las tareas en la cola
        /// </summary>
        public List<DownloadTask> GetQueueSnapshot()
        {
            return downloadQueueService.WithQueueLock(list => new List<DownloadTask>(list));
        }

        public async Task SaveQueueAsync()
        {
            if (string.IsNullOrWhiteSpace(config?.QueuePersistencePath))
                return;

            if (!await saveQueueSemaphore.WaitAsync(0))
                return;

            try
            {
                var entries = downloadQueueService.WithQueueLock(list =>
                    list
                        .Where(t =>
                            t.Status == DownloadStatus.Queued ||
                            t.Status == DownloadStatus.Paused ||
                            t.Status == DownloadStatus.Downloading ||
                            t.Status == DownloadStatus.GettingStatus)
                        .Select(t => new DownloadQueuePersistenceEntry(t))
                        .ToList());

                string queuePath = config.QueuePersistencePath;
                string gzipPath = queuePath + ".gz";

                if (entries.Count == 0)
                {
                    TryDeleteFile(queuePath);
                    TryDeleteFile(gzipPath);
                    return;
                }

                var dir = System.IO.Path.GetDirectoryName(queuePath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = false });
                var jsonBytes = Encoding.UTF8.GetBytes(json);

                var gzipTempPath = gzipPath + ".tmp";

                try
                {
                    using (var outputStream = new System.IO.FileStream(gzipTempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal))
                    {
                        await gzipStream.WriteAsync(jsonBytes, 0, jsonBytes.Length);
                    }

                    System.IO.File.Move(gzipTempPath, gzipPath, true);
                }
                catch
                {
                    TryDeleteFile(gzipTempPath);
                    throw;
                }

                TryDeleteFile(queuePath);

                Log($"Cola guardada: {entries.Count} tareas");
            }
            catch (Exception ex)
            {
                Log($"Error guardando cola: {ex.Message}");
            }
            finally
            {
                saveQueueSemaphore.Release();
            }
        }

        public async Task<IReadOnlyList<DownloadTask>> LoadQueueAsync()
        {
            if (string.IsNullOrWhiteSpace(config?.QueuePersistencePath))
                return Array.Empty<DownloadTask>();

            string queuePath = config.QueuePersistencePath;
            string gzipPath = queuePath + ".gz";

            try
            {
                string json = null;

                if (System.IO.File.Exists(gzipPath))
                {
                    using (var inputStream = new System.IO.FileStream(gzipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
                    using (var reader = new StreamReader(gzipStream, Encoding.UTF8))
                    {
                        json = await reader.ReadToEndAsync();
                    }
                }
                else if (System.IO.File.Exists(queuePath))
                {
                    json = await System.IO.File.ReadAllTextAsync(queuePath, Encoding.UTF8);
                }

                if (string.IsNullOrWhiteSpace(json))
                    return Array.Empty<DownloadTask>();

                var items = JsonSerializer.Deserialize<List<DownloadQueuePersistenceEntry>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (items == null || items.Count == 0)
                    return Array.Empty<DownloadTask>();

                var loadedTasks = new List<DownloadTask>(items.Count);

                foreach (var entry in items)
                {
                    if (entry?.File == null || string.IsNullOrWhiteSpace(entry.LocalPath))
                        continue;

                    try
                    {
                        if (!string.IsNullOrWhiteSpace(entry.TargetPath) && System.IO.File.Exists(entry.TargetPath))
                        {
                            var finfo = new FileInfo(entry.TargetPath);
                            if (entry.File.SizeBytes > 0 && Math.Abs(finfo.Length - entry.File.SizeBytes) <= 1024)
                            {
                                Log($"Descarga omitida (archivo ya existe): {entry.File.FileName}");
                                continue;
                            }
                        }
                    }
                    catch
                    {
                    }

                    var status = entry.Status;
                    if (status == DownloadStatus.Downloading || status == DownloadStatus.GettingStatus)
                    {
                        status = DownloadStatus.Queued;
                    }

                    var task = entry.RestoreTask(status);

                    try
                    {
                        if (!string.IsNullOrWhiteSpace(task.LocalPath) && System.IO.File.Exists(task.LocalPath) && task.File?.SizeBytes > 0)
                        {
                            var finfo = new FileInfo(task.LocalPath);
                            var age = DateTime.UtcNow - finfo.LastWriteTimeUtc;
                            if (finfo.Length == 0 && age >= INCOMPLETE_ZERO_BYTE_MAX_AGE)
                            {
                                TryDeleteFile(task.LocalPath);
                                task.BytesDownloaded = 0;
                                task.ProgressPercent = 0;
                            }
                            else if (age >= INCOMPLETE_PARTIAL_MAX_AGE)
                            {
                                TryDeleteFile(task.LocalPath);
                                task.BytesDownloaded = 0;
                                task.ProgressPercent = 0;
                            }
                            else
                            {
                                task.BytesDownloaded = finfo.Length;
                                var pct = (finfo.Length / (double)task.File.SizeBytes) * 100.0;
                                task.ProgressPercent = Math.Min(99.0, Math.Max(0.0, pct));
                            }
                        }
                    }
                    catch
                    {
                    }

                    loadedTasks.Add(task);
                }

                int added = downloadQueueService.Update(list =>
                {
                    int addedCount = 0;
                    foreach (var task in loadedTasks)
                    {
                        bool alreadyQueued = list.Any(t =>
                            t.File.FileName.Equals(task.File.FileName, StringComparison.OrdinalIgnoreCase) &&
                            t.File.Username.Equals(task.File.Username, StringComparison.OrdinalIgnoreCase));

                        if (!alreadyQueued)
                        {
                            list.Add(task);
                            addedCount++;
                        }
                    }
                    return addedCount;
                });

                if (added > 0)
                {
                    OnQueueChanged?.Invoke();
                    ScheduleQueueSave();
                }

                TryDeleteFile(queuePath);
                TryDeleteFile(gzipPath);

                Log($"Cola restaurada desde disco: {loadedTasks.Count} tareas");

                return loadedTasks;
            }
            catch (Exception ex)
            {
                Log($" Error cargando cola: {ex.Message}");
                return Array.Empty<DownloadTask>();
            }
        }

        private class DownloadQueuePersistenceEntry
        {
            public DownloadQueuePersistenceEntry()
            {
            }

            public DownloadQueuePersistenceEntry(DownloadTask task)
            {
                if (task == null)
                    return;

                File = task.File;
                LocalPath = task.LocalPath;
                TargetPath = task.TargetPath;
                Status = task.Status;
                BytesDownloaded = task.BytesDownloaded;
                ProgressPercent = task.ProgressPercent;
                RetryCount = task.RetryCount;
                MaxRetries = task.MaxRetries;
                LastRetryTime = task.LastRetryTime;
                LastKnownQueuePosition = task.QueuePosition;
                LastErrorMessage = task.ErrorMessage;
                LastFailureAt = task.FinalFailureTime;
                StartedAt = task.StartTime;
                SlowDownloadChecks = task.SlowDownloadChecks;
                IsScheduled = task.IsScheduled;
                ScheduledAt = task.ScheduledAt;
            }

            public AutoSearchFileResult File { get; set; }
            public string LocalPath { get; set; }
            public string TargetPath { get; set; }
            public DownloadStatus Status { get; set; }
            public long BytesDownloaded { get; set; }
            public double ProgressPercent { get; set; }
            public int RetryCount { get; set; }
            public int MaxRetries { get; set; }
            public DateTime? LastRetryTime { get; set; }
            public int LastKnownQueuePosition { get; set; }
            public string LastErrorMessage { get; set; }
            public DateTime? LastFailureAt { get; set; }
            public DateTime? StartedAt { get; set; }
            public int SlowDownloadChecks { get; set; }
            public bool IsScheduled { get; set; }
            public DateTime? ScheduledAt { get; set; }

            public DownloadTask RestoreTask(DownloadStatus status)
            {
                return new DownloadTask
                {
                    File = File,
                    LocalPath = LocalPath,
                    TargetPath = TargetPath,
                    Status = status,
                    BytesDownloaded = BytesDownloaded,
                    ProgressPercent = ProgressPercent,
                    RetryCount = RetryCount,
                    MaxRetries = MaxRetries,
                    LastRetryTime = LastRetryTime,
                    QueuePosition = LastKnownQueuePosition,
                    ErrorMessage = LastErrorMessage,
                    FinalFailureTime = LastFailureAt,
                    StartTime = StartedAt,
                    SlowDownloadChecks = SlowDownloadChecks,
                    IsScheduled = IsScheduled,
                    ScheduledAt = ScheduledAt
                };
            }
        }

        /// <summary>
        /// Limpia la blacklist temporal de proveedores
        /// </summary>
        public void ClearBlacklist()
        {
            lock (blacklistLock)
            {
                int count = providerBlacklist.Count;
                providerBlacklist.Clear();
                providerCooldownUntil.Clear();
                providerCooldownReason.Clear();
                providerConsecutiveFailures.Clear();

                lock (circuitLock)
                {
                    providerCircuitFailures.Clear();
                    providerCircuitOpenUntilUtc.Clear();
                    providerHalfOpenInFlight.Clear();
                }
                Log($"Blacklist limpiada: {count} proveedores liberados");
            }
            ScheduleStateSave();
        }

        private async Task LoadProviderStateAsync()
        {
            if (string.IsNullOrWhiteSpace(config?.ProviderBlacklistPath))
            {
                return;
            }

            var path = config.ProviderBlacklistPath;
            try
            {
                if (!System.IO.File.Exists(path))
                {
                    return;
                }

                var json = await System.IO.File.ReadAllTextAsync(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                var items = JsonSerializer.Deserialize<Dictionary<string, ProviderBlacklistEntry>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (items == null)
                {
                    return;
                }

                var nowUtc = DateTime.UtcNow;
                lock (blacklistLock)
                {
                    providerBlacklist.Clear();
                    providerCooldownUntil.Clear();
                    providerCooldownReason.Clear();
                    providerConsecutiveFailures.Clear();

                    lock (circuitLock)
                    {
                        providerCircuitFailures.Clear();
                        providerCircuitOpenUntilUtc.Clear();
                        providerHalfOpenInFlight.Clear();
                    }

                    foreach (var kvp in items)
                    {
                        if (string.IsNullOrWhiteSpace(kvp.Key))
                        {
                            continue;
                        }

                        var lastFailUtc = NormalizeToUtc(kvp.Value.LastFail);
                        var blockedUntilUtc = kvp.Value.CircuitOpenUntilUtc.HasValue ? NormalizeToUtc(kvp.Value.CircuitOpenUntilUtc.Value) : lastFailUtc;
                        providerBlacklist[kvp.Key] = (kvp.Value.Failures, lastFailUtc, blockedUntilUtc);

                        if (kvp.Value.CooldownUntilUtc.HasValue)
                        {
                            var untilUtc = NormalizeToUtc(kvp.Value.CooldownUntilUtc.Value);
                            if (untilUtc > nowUtc)
                            {
                                providerCooldownUntil[kvp.Key] = untilUtc;
                                if (kvp.Value.CooldownReason.HasValue)
                                {
                                    providerCooldownReason[kvp.Key] = kvp.Value.CooldownReason.Value;
                                }
                            }
                        }

                        if (kvp.Value.CircuitOpenUntilUtc.HasValue)
                        {
                            var untilUtc = NormalizeToUtc(kvp.Value.CircuitOpenUntilUtc.Value);
                            if (untilUtc > nowUtc)
                            {
                                lock (circuitLock)
                                {
                                    providerCircuitOpenUntilUtc[kvp.Key] = untilUtc;
                                }
                            }
                        }
                    }
                }

                lock (providerRetryBudgetLock)
                {
                    providerRetryBudgetFailures.Clear();
                    var cutoff = nowUtc.AddMinutes(-PROVIDER_RETRY_BUDGET_WINDOW_MINUTES);
                    foreach (var kvp in items)
                    {
                        var list = kvp.Value?.RetryBudgetFailuresUtc;
                        if (list == null || list.Count == 0)
                        {
                            continue;
                        }

                        var q = new Queue<DateTime>();
                        foreach (var dt in list)
                        {
                            var utc = NormalizeToUtc(dt);
                            if (utc >= cutoff && utc <= nowUtc)
                            {
                                q.Enqueue(utc);
                            }
                        }

                        if (q.Count > 0)
                        {
                            providerRetryBudgetFailures[kvp.Key] = q;
                        }
                    }
                }

                Log($"Blacklist/cooldowns cargados: {items.Count}");
            }
            catch (Exception ex)
            {
                Log($"Error cargando blacklist/cooldowns: {ex.Message}");
            }
        }

        private static DateTime NormalizeToUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
            {
                return value;
            }

            if (value.Kind == DateTimeKind.Local)
            {
                return value.ToUniversalTime();
            }

            return DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime();
        }

        private int GetActiveDownloadsCount() =>
            downloadQueueService.WithQueueLock(list =>
                list.Count(t =>
                    t.Status == DownloadStatus.Downloading ||
                    t.Status == DownloadStatus.GettingStatus));
        
        /// <summary>
        /// Obtiene snapshot de la blacklist
        /// </summary>
        public Dictionary<string, (int failures, DateTime lastFail, DateTime blockedUntil)> GetBlacklistSnapshot()
        {
            lock (blacklistLock)
            {
                return new Dictionary<string, (int failures, DateTime lastFail, DateTime blockedUntil)>(providerBlacklist);
            }
        }

        public Dictionary<string, (DateTime untilUtc, DownloadFailureReason reason)> GetCooldownSnapshot()
        {
            lock (blacklistLock)
            {
                var snapshot = new Dictionary<string, (DateTime untilUtc, DownloadFailureReason reason)>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in providerCooldownUntil)
                {
                    var reason = providerCooldownReason.TryGetValue(kvp.Key, out var r)
                        ? r
                        : DownloadFailureReason.Unknown;
                    snapshot[kvp.Key] = (kvp.Value, reason);
                }

                return snapshot;
            }
        }

        public sealed class ProviderHealthEntry
        {
            public string Username { get; set; }
            public int ActiveDownloads { get; set; }
            public bool IsBlacklisted { get; set; }
            public int BlacklistFailures { get; set; }
            public DateTime? BlacklistLastFailUtc { get; set; }
            public TimeSpan? CooldownRemaining { get; set; }
            public DownloadFailureReason? CooldownReason { get; set; }
            public TimeSpan? CircuitRemaining { get; set; }
            public bool HalfOpenInFlight { get; set; }
        }

        public List<ProviderHealthEntry> GetProviderHealthSnapshot(int maxEntries = 300)
        {
            var queueSnapshot = GetQueueSnapshot();
            var activeCounts = queueSnapshot
                .Where(t => t.Status == DownloadStatus.Downloading || t.Status == DownloadStatus.GettingStatus)
                .Where(t => t.File != null && !string.IsNullOrWhiteSpace(t.File.Username))
                .GroupBy(t => t.File.Username, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            Dictionary<string, (int failures, DateTime lastFail, DateTime blockedUntil)> blacklist;
            Dictionary<string, (DateTime untilUtc, DownloadFailureReason reason)> cooldown;
            Dictionary<string, DateTime> circuit;
            HashSet<string> halfOpen;

            lock (blacklistLock)
            {
                blacklist = new Dictionary<string, (int failures, DateTime lastFail, DateTime blockedUntil)>(providerBlacklist, StringComparer.OrdinalIgnoreCase);
                cooldown = GetCooldownSnapshot();
            }

            lock (circuitLock)
            {
                circuit = new Dictionary<string, DateTime>(providerCircuitOpenUntilUtc, StringComparer.OrdinalIgnoreCase);
                halfOpen = new HashSet<string>(providerHalfOpenInFlight, StringComparer.OrdinalIgnoreCase);
            }

            var users = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var u in activeCounts.Keys) users.Add(u);
            foreach (var u in blacklist.Keys) users.Add(u);
            foreach (var u in cooldown.Keys) users.Add(u);
            foreach (var u in circuit.Keys) users.Add(u);
            foreach (var u in halfOpen) users.Add(u);

            var nowUtc = DateTime.UtcNow;
            var list = new List<ProviderHealthEntry>(Math.Min(maxEntries, users.Count));
            foreach (var username in users)
            {
                if (list.Count >= maxEntries)
                {
                    break;
                }

                activeCounts.TryGetValue(username, out var active);
                blacklist.TryGetValue(username, out var bl);
                cooldown.TryGetValue(username, out var cd);
                circuit.TryGetValue(username, out var circuitUntil);

                TimeSpan? circuitRemaining = null;
                if (circuitUntil != default && circuitUntil > nowUtc)
                {
                    circuitRemaining = circuitUntil - nowUtc;
                }

                var entry = new ProviderHealthEntry
                {
                    Username = username,
                    ActiveDownloads = active,
                    IsBlacklisted = IsProviderBlacklisted(username),
                    BlacklistFailures = bl.failures,
                    BlacklistLastFailUtc = bl.lastFail == default ? null : bl.lastFail,
                    CooldownRemaining = cd.untilUtc == default ? null : (cd.untilUtc > nowUtc ? cd.untilUtc - nowUtc : null),
                    CooldownReason = cd.untilUtc == default ? null : cd.reason,
                    CircuitRemaining = circuitRemaining,
                    HalfOpenInFlight = halfOpen.Contains(username)
                };

                list.Add(entry);
            }

            return list
                .OrderByDescending(x => x.ActiveDownloads)
                .ThenByDescending(x => x.IsBlacklisted)
                .ThenByDescending(x => x.CooldownRemaining.HasValue)
                .ThenByDescending(x => x.CircuitRemaining.HasValue)
                .ThenBy(x => x.Username, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public bool TryGetCooldownInfo(string username, out DownloadFailureReason reason, out TimeSpan remaining)
        {
            reason = DownloadFailureReason.Unknown;
            remaining = TimeSpan.Zero;

            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            lock (blacklistLock)
            {
                if (!providerCooldownUntil.TryGetValue(username, out var untilUtc))
                {
                    return false;
                }

                var nowUtc = DateTime.UtcNow;
                if (untilUtc <= nowUtc)
                {
                    providerCooldownUntil.Remove(username);
                    providerCooldownReason.Remove(username);
                    return false;
                }

                reason = providerCooldownReason.TryGetValue(username, out var r) ? r : DownloadFailureReason.Unknown;
                remaining = untilUtc - nowUtc;
                return true;
            }
        }
        
        /// <summary>
        /// Verifica si un proveedor está en blacklist
        /// </summary>
        public bool IsProviderBlacklisted(string username)
        {
            if (string.IsNullOrEmpty(username)) return false;
            
            lock (blacklistLock)
            {
                if (providerCooldownUntil.TryGetValue(username, out var until))
                {
                    if (DateTime.UtcNow < until)
                    {
                        return true;
                    }

                    providerCooldownUntil.Remove(username);
                    providerCooldownReason.Remove(username);
                }

                if (!providerBlacklist.TryGetValue(username, out var data))
                    return false;
                
                // Verificar si ha expirado
                if ((DateTime.UtcNow - data.lastFail).TotalHours >= EffectiveProviderBlacklistHours)
                {
                    providerBlacklist.Remove(username);
                    ScheduleStateSave();
                    return false;
                }
                
                return data.failures >= EffectiveProviderBlacklistThreshold;
            }
        }

        private bool IsProviderCircuitOpen(string username, out TimeSpan remaining)
        {
            remaining = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            lock (circuitLock)
            {
                if (!providerCircuitOpenUntilUtc.TryGetValue(username, out var untilUtc))
                {
                    return false;
                }

                var nowUtc = DateTime.UtcNow;
                if (untilUtc <= nowUtc)
                {
                    providerCircuitOpenUntilUtc.Remove(username);
                    return false;
                }

                remaining = untilUtc - nowUtc;
                return true;
            }
        }

        private bool TryReserveHalfOpenTrial(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            lock (circuitLock)
            {
                if (providerHalfOpenInFlight.Contains(username))
                {
                    return false;
                }

                providerHalfOpenInFlight.Add(username);
                return true;
            }
        }

        private void ReleaseHalfOpenTrial(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            lock (circuitLock)
            {
                providerHalfOpenInFlight.Remove(username);
            }
        }

        private bool ShouldBlockProviderForStart(string username, out string blockReason)
        {
            blockReason = string.Empty;
            if (string.IsNullOrWhiteSpace(username))
            {
                return true;
            }

            if (IsProviderBlacklisted(username))
            {
                blockReason = "blacklist/cooldown";
                return true;
            }

            if (IsProviderCircuitOpen(username, out var remaining))
            {
                blockReason = $"circuit_open({remaining.TotalMinutes:F0}m)";
                return true;
            }

            return false;
        }

        private bool TryEnterProviderForStart(string username, out string blockReason, out bool halfOpenReserved)
        {
            blockReason = string.Empty;
            halfOpenReserved = false;

            if (string.IsNullOrWhiteSpace(username))
            {
                blockReason = "invalid_username";
                return false;
            }

            if (IsProviderBlacklisted(username))
            {
                blockReason = "blacklist/cooldown";
                return false;
            }

            lock (circuitLock)
            {
                if (providerHalfOpenInFlight.Contains(username))
                {
                    blockReason = "half_open_busy";
                    return false;
                }

                if (providerCircuitOpenUntilUtc.TryGetValue(username, out var untilUtc))
                {
                    var nowUtc = DateTime.UtcNow;
                    if (untilUtc > nowUtc)
                    {
                        blockReason = $"circuit_open({(untilUtc - nowUtc).TotalMinutes:F0}m)";
                        return false;
                    }

                    // Open expired -> Half-open: allow exactly 1 in-flight trial.
                    providerCircuitOpenUntilUtc.Remove(username);

                    providerHalfOpenInFlight.Add(username);
                    halfOpenReserved = true;
                    return true;
                }
            }

            return true;
        }

        private void RecordProviderSuccess(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            ClearRetryBudgetForProvider(username);

            lock (blacklistLock)
            {
                providerConsecutiveFailures.Remove(username);
            }

            lock (circuitLock)
            {
                providerCircuitFailures.Remove(username);
                providerCircuitOpenUntilUtc.Remove(username);
                providerHalfOpenInFlight.Remove(username);
            }
        }
        
        /// <summary>
        /// Registra un fallo de proveedor
        /// </summary>
        public void RecordProviderFailure(string username, DownloadFailureReason reason = DownloadFailureReason.Unknown)
        {
            if (string.IsNullOrEmpty(username)) return;

            RegisterRetryBudgetFailure(username);
            
            lock (blacklistLock)
            {
                int newFailures = 1;
                DateTime blockedUntil = DateTime.UtcNow;
                
                if (providerBlacklist.TryGetValue(username, out var data))
                {
                    // Si ya pasó el tiempo de bloqueo, resetear contador
                    if (DateTime.UtcNow >= data.blockedUntil)
                    {
                        newFailures = 1;
                    }
                    else
                    {
                        newFailures = data.failures + 1;
                    }
                }
                
                // Calcular tiempo de bloqueo progresivo: 1h, 3h, 6h, 12h, 24h
                int blockHours = newFailures switch
                {
                    <= 10 => 1,
                    <= 30 => 3,
                    <= 60 => 6,
                    <= 100 => 12,
                    _ => 24
                };
                
                blockedUntil = DateTime.UtcNow.AddHours(blockHours);
                providerBlacklist[username] = (newFailures, DateTime.UtcNow, blockedUntil);

                var consecutive = providerConsecutiveFailures.TryGetValue(username, out var cf) ? cf + 1 : 1;
                providerConsecutiveFailures[username] = consecutive;

                var now = DateTime.UtcNow;
                TimeSpan? cooldown = reason switch
                {
                    DownloadFailureReason.QueueFull => TimeSpan.FromMinutes(30),
                    DownloadFailureReason.QuotaExceeded => TimeSpan.FromHours(2),
                    DownloadFailureReason.Banned => TimeSpan.FromHours(24),
                    DownloadFailureReason.Timeout => TimeSpan.FromMinutes(15),
                    DownloadFailureReason.Connection => TimeSpan.FromMinutes(10),
                    _ => null
                };

                if (cooldown.HasValue)
                {
                    var multiplier = Math.Pow(2, Math.Min(consecutive - 1, 5));
                    var seconds = cooldown.Value.TotalSeconds * multiplier;
                    seconds = Math.Min(seconds, TimeSpan.FromMinutes(config.MaxProviderCooldownMinutes).TotalSeconds);
                    var jitterRatio = Math.Max(0.0, Math.Min(0.5, config.ProviderCooldownJitterRatio));
                    var jitter = 1.0;
                    lock (jitterRng)
                    {
                        jitter = 1.0 + ((jitterRng.NextDouble() * 2.0) - 1.0) * jitterRatio;
                    }
                    seconds = Math.Max(1, seconds * jitter);

                    var until = now.Add(TimeSpan.FromSeconds(seconds));
                    providerCooldownUntil[username] = until;
                    providerCooldownReason[username] = reason;
                    Log($"⏳ Cooldown proveedor: {username} ({reason}) hasta {until.ToLocalTime():HH:mm}");
                }

                if (newFailures >= EffectiveProviderBlacklistThreshold)
                {
                    Log($"⛔ Proveedor bloqueado temporalmente: {username} ({newFailures} fallos, umbral {EffectiveProviderBlacklistThreshold})");
                }
            }

            lock (circuitLock)
            {
                providerHalfOpenInFlight.Remove(username);

                var failures = providerCircuitFailures.TryGetValue(username, out var f) ? f + 1 : 1;
                providerCircuitFailures[username] = failures;

                if (failures >= config.CircuitBreakerFailureThreshold)
                {
                    var openUntil = DateTime.UtcNow.AddSeconds(Math.Max(10, config.CircuitBreakerOpenSeconds));
                    providerCircuitOpenUntilUtc[username] = openUntil;
                    providerCircuitFailures[username] = 0;
                    Log($"🧯 Circuit abierto para {username} hasta {openUntil.ToLocalTime():HH:mm:ss}");
                }
            }
            ScheduleStateSave();
        }

        private void RemoveTaskInternal(DownloadTask task, bool logRemoval = false)
        {
            if (task == null) return;

            downloadQueueService.Update(list => list.Remove(task));

            if (logRemoval)
            {
                Log($"Tarea eliminada de la cola: {task.File.FileName}");
            }

            OnQueueChanged?.Invoke();
            ScheduleQueueSave();
        }

        public void RemoveFromQueue(DownloadTask task)
        {
            RemoveTaskInternal(task, logRemoval: true);
        }

        public void RecordProviderSuccess(string username, long sizeBytes, TimeSpan duration)
        {
            if (string.IsNullOrWhiteSpace(username)) return;

            lock (providerStatsLock)
            {
                if (!providerStats.TryGetValue(username, out var stats))
                {
                    stats = new SlskDown.Models.ProviderStats { Username = username };
                    providerStats[username] = stats;
                }

                stats.TotalDownloads++;
                stats.SuccessfulDownloads++;
                stats.TotalBytesDownloaded += sizeBytes;
                stats.LastDownloadDate = DateTime.Now;

                if (duration.TotalSeconds > 0)
                {
                    double speedBps = sizeBytes / duration.TotalSeconds;
                    stats.AverageSpeed = stats.SuccessfulDownloads == 1
                        ? speedBps
                        : (stats.AverageSpeed * (stats.SuccessfulDownloads - 1) + speedBps) / stats.SuccessfulDownloads;
                }
            }

            ScheduleStateSave();
        }

        /// <summary>
        /// Busca proveedor alternativo para una tarea fallida
        /// </summary>
        public async Task<SearchAlternativesResult> TryFindAlternativeProviderAsync(DownloadTask failedTask)
        {
            if (failedTask == null)
            {
                return new SearchAlternativesResult
                {
                    Success = false,
                    FailureReason = "Tarea nula"
                };
            }

            string fileKey = $"{failedTask.File.FileName}_{failedTask.File.SizeBytes}";

            lock (retryCountLock)
            {
                if (!downloadRetryCount.ContainsKey(fileKey))
                {
                    downloadRetryCount[fileKey] = 0;
                }
            }

            try
            {
                if (OnSearchAlternatives == null)
                {
                    Log("OnSearchAlternatives no está configurado");
                    return new SearchAlternativesResult
                    {
                        Success = false,
                        FailureReason = "Callback no configurado"
                    };
                }

                var searchResult = await OnSearchAlternatives(failedTask);
                if (searchResult == null || !searchResult.Success)
                {
                    Log("Sin alternativas válidas (callback no encontró candidatos)");
                    RemoveTaskInternal(failedTask);
                    return new SearchAlternativesResult
                    {
                        Success = false,
                        FailureReason = searchResult?.FailureReason ?? "Sin resultados"
                    };
                }

                var candidates = new List<AutoSearchFileResult>();
                if (searchResult.Candidates != null)
                {
                    candidates.AddRange(searchResult.Candidates);
                }
                if (searchResult.Alternative != null)
                {
                    candidates.Add(searchResult.Alternative);
                }

                candidates = candidates
                    .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Username))
                    .GroupBy(c => c.Username, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                if (candidates.Count == 0)
                {
                    Log("Sin alternativas válidas (lista vacía)");
                    RemoveTaskInternal(failedTask);
                    return new SearchAlternativesResult
                    {
                        Success = false,
                        FailureReason = "Sin candidatos"
                    };
                }

                var preferred = GetPreferredProvidersForFile(fileKey, maxEntries: 6);
                var preferredRank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < preferred.Count; i++)
                {
                    preferredRank[preferred[i]] = i;
                }

                AutoSearchFileResult selected = candidates
                    .Where(c => !ShouldBlockProviderForStart(c.Username, out _))
                    .OrderBy(c => preferredRank.TryGetValue(c.Username, out var rank) ? rank : int.MaxValue)
                    .ThenByDescending(c => GetProviderScore(c.Username))
                    .FirstOrDefault();

                selected ??= candidates.First();

                failedTask.File = selected;
                failedTask.Status = DownloadStatus.Queued;
                failedTask.ErrorMessage = null;

                Log($"Alternativa encontrada: {failedTask.File.Username}");
                return new SearchAlternativesResult
                {
                    Success = true,
                    Alternative = selected,
                    Candidates = candidates
                };
            }
            catch (Exception ex)
            {
                Log($"Error buscando alternativa: {ex.Message}");
                return new SearchAlternativesResult
                {
                    Success = false,
                    FailureReason = ex.Message
                };
            }
        }
        
        /// <summary>
        /// Loop principal de procesamiento de cola
        /// </summary>
        private async Task ProcessQueueLoop()
        {
            while (isRunning)
            {
                try
                {
                    await ProcessQueue();
                    await Task.Delay(config.ProcessQueueIntervalMs);
                }
                catch (Exception ex)
                {
                    Log($"Error en loop de descargas: {ex.Message}");
                    await Task.Delay(config.ProcessQueueErrorDelayMs);
                }
            }
        }

        private int GetEffectivePerProviderCap(string username, int baseCap)
        {
            var safeBaseCap = Math.Max(1, baseCap);
            if (string.IsNullOrWhiteSpace(username))
            {
                return safeBaseCap;
            }

            SlskDown.Models.ProviderStats statsSnapshot = null;
            lock (providerStatsLock)
            {
                if (providerStats.TryGetValue(username, out var stats))
                {
                    statsSnapshot = stats;
                }
            }

            if (statsSnapshot == null || statsSnapshot.TotalDownloads < 5)
            {
                return safeBaseCap;
            }

            var total = Math.Max(1, statsSnapshot.TotalDownloads);
            var successRate = statsSnapshot.SuccessfulDownloads / (double)total;
            var avgSpeedMBps = statsSnapshot.AverageSpeed / (1024.0 * 1024.0);

            if (successRate >= 0.80 && avgSpeedMBps >= 1.0)
            {
                return Math.Min(MAX_ADAPTIVE_PER_PROVIDER_CAP, Math.Max(safeBaseCap, 2));
            }

            if (successRate <= 0.30 && statsSnapshot.FailedDownloads >= 3)
            {
                return 1;
            }

            return safeBaseCap;
        }

        private async Task ProcessQueue()
        {
            var (reprioritized, pendingTasks) = downloadQueueService.Update(list =>
            {
                // DIAGNÓSTICO: Log de estado de la cola
                var queuedCount = list.Count(t => t.Status == DownloadStatus.Queued);
                var downloadingCount = list.Count(t => t.Status == DownloadStatus.Downloading);
                var gettingStatusCount = list.Count(t => t.Status == DownloadStatus.GettingStatus);
                var totalCount = list.Count;
                
                if (totalCount > 0 && queuedCount > 0)
                {
                    Log($"Cola: {totalCount} total | {queuedCount} en cola | {downloadingCount} descargando | {gettingStatusCount} verificando");
                }
                
                bool reprioritizedInner = false;
                if (queueStrategy != QueuePrioritizationStrategy.Manual)
                {
                    reprioritizedInner = ApplyQueuePrioritization(list);
                }

                int activeCount = list.Count(t =>
                    t.Status == DownloadStatus.Downloading ||
                    t.Status == DownloadStatus.GettingStatus);
                if (activeCount >= config.MaxSimultaneousDownloads)
                {
                    Log($"Máximo de descargas simultáneas alcanzado ({activeCount}/{config.MaxSimultaneousDownloads})");
                    return (reprioritizedInner, pending: new List<DownloadTask>());
                }

                int slotsAvailable = config.MaxSimultaneousDownloads - activeCount;
                var perProviderCap = Math.Max(1, config.MaxSimultaneousDownloadsPerProvider);
                var activeCountsByUser = list
                    .Where(t => t.Status == DownloadStatus.Downloading || t.Status == DownloadStatus.GettingStatus)
                    .Where(t => t.File != null && !string.IsNullOrWhiteSpace(t.File.Username))
                    .GroupBy(t => t.File.Username, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

                // AUTO scheduling: round-robin por proveedor para evitar atascarse con una sola cola
                var providerOrder = new List<string>();
                var queuedByProvider = new Dictionary<string, Queue<DownloadTask>>(StringComparer.OrdinalIgnoreCase);
                var eligibleCount = 0;
                var notEligibleCount = 0;
                var noFileCount = 0;
                
                foreach (var task in list)
                {
                    var nowUtc = DateTime.UtcNow;
                    if (!IsEligibleToStartQueuedTask(task, nowUtc))
                    {
                        notEligibleCount++;
                        continue;
                    }

                    if (task.File == null || string.IsNullOrWhiteSpace(task.File.Username))
                    {
                        noFileCount++;
                        continue;
                    }
                    
                    eligibleCount++;

                    // Verificar si el proveedor está en blacklist temporal
                    bool isBlacklisted = false;
                    lock (blacklistLock)
                    {
                        if (providerBlacklist.TryGetValue(task.File.Username, out var data))
                        {
                            if (data.failures >= EffectiveProviderBlacklistThreshold && 
                                DateTime.Now < data.blockedUntil)
                            {
                                isBlacklisted = true;
                            }
                        }
                    }
                    
                    // Si está bloqueado, posponer la tarea
                    if (isBlacklisted)
                    {
                        continue;
                    }

                    if (task.IsScheduled)
                    {
                        task.IsScheduled = false;
                        task.ScheduledAt = null;
                    }

                    var username = task.File.Username;
                    if (!queuedByProvider.TryGetValue(username, out var q))
                    {
                        q = new Queue<DownloadTask>();
                        queuedByProvider[username] = q;
                        providerOrder.Add(username);
                    }
                    q.Enqueue(task);
                }

                var pending = new List<DownloadTask>(slotsAvailable);
                
                // DIAGNÓSTICO: Log de tareas elegibles
                if (queuedCount > 0)
                {
                    Log($"Elegibilidad: {eligibleCount} elegibles | {notEligibleCount} no elegibles | {noFileCount} sin archivo | {queuedByProvider.Count} proveedores");
                }
                
                if (queuedByProvider.Count == 0)
                {
                    if (queuedCount > 0)
                    {
                        Log($"Hay {queuedCount} tareas en cola pero ninguna es elegible para iniciar");
                    }
                    return (reprioritizedInner, pending);
                }
                
                // INTEGRACIÓN NICOTINE+: Reordenar proveedores por prioridad dinámica
                var allEligibleTasks = queuedByProvider.Values.SelectMany(q => q).ToList();
                var prioritizedTasks = prioritizer.ReorderByPriority(allEligibleTasks);
                
                // Reconstruir queuedByProvider con orden priorizado
                queuedByProvider.Clear();
                providerOrder.Clear();
                foreach (var task in prioritizedTasks)
                {
                    var username = task.File.Username;
                    if (!queuedByProvider.TryGetValue(username, out var q))
                    {
                        q = new Queue<DownloadTask>();
                        queuedByProvider[username] = q;
                        providerOrder.Add(username);
                    }
                    q.Enqueue(task);
                }

                while (pending.Count < slotsAvailable)
                {
                    var progressed = false;
                    foreach (var username in providerOrder)
                    {
                        if (pending.Count >= slotsAvailable)
                        {
                            break;
                        }

                        if (!queuedByProvider.TryGetValue(username, out var q) || q.Count == 0)
                        {
                            continue;
                        }

                        activeCountsByUser.TryGetValue(username, out var activeForUser);
                        var effectivePerProviderCap = GetEffectivePerProviderCap(username, perProviderCap);
                        if (activeForUser >= effectivePerProviderCap)
                        {
                            continue;
                        }

                        // Intentar reservar el proveedor antes de sacar la tarea de la cola
                        if (!TryEnterProviderForStart(username, out _, out _))
                        {
                            continue;
                        }

                        var task = q.Dequeue();
                        pending.Add(task);
                        activeCountsByUser[username] = activeForUser + 1;
                        progressed = true;
                    }

                    if (!progressed)
                    {
                        break;
                    }
                }

                foreach (var task in pending)
                {
                    task.Status = DownloadStatus.GettingStatus;
                    task.StartTime ??= DateTime.Now;
                    task.LastFailureReason = DownloadFailureReason.Unknown;
                }

                return (reprioritizedInner, pending);
            });

            if (reprioritized)
            {
                OnQueueChanged?.Invoke();
                ScheduleQueueSave();
            }

            // DIAGNÓSTICO: Log de tareas a procesar
            if (pendingTasks.Count > 0)
            {
                Log($"Iniciando {pendingTasks.Count} descarga(s)");
            }

            foreach (var task in pendingTasks)
            {
                if (!isRunning)
                {
                    break;
                }

                if (ShouldBlockProviderForStart(task.File.Username, out var reason))
                {
                    Log($"⛔ Proveedor bloqueado ({reason}): {task.File.Username}");
                    task.Status = DownloadStatus.Queued;
                    await TryFindAlternativeProviderAsync(task);
                    ReleaseHalfOpenTrial(task.File.Username);
                    continue;
                }

                if (config.DownloadSpeedLimitKiB > 0)
                {
                    OnApplyDownloadSpeedLimit?.Invoke(config.DownloadSpeedLimitKiB);
                }

                if (TryDeferTaskDueToDiskSpace(task, out var diskReason))
                {
                    Log($"💾 Descarga pospuesta por disco: {task.File?.FileName} -> {diskReason}");
                    OnQueueChanged?.Invoke();
                    ScheduleQueueSave();
                    ReleaseHalfOpenTrial(task.File.Username);
                    continue;
                }

                if (OnDownloadFile != null)
                {
                    var downloadTask = Task.Run(async () =>
                    {
                        try
                        {
                            // INTEGRACIÓN NICOTINE+: Verificar límite de cola del usuario
                            if (queueManager != null && !queueManager.CanQueueTransfer(task.File.Username))
                            {
                                task.Status = DownloadStatus.UserQueueFull;
                                task.ErrorMessage = "Cola del usuario llena";
                                Log($"⛔ [Nicotine+] Cola llena para {task.File.Username}");
                                return;
                            }
                            
                            // INTEGRACIÓN NICOTINE+: Incrementar contador de cola
                            queueManager?.IncrementQueueSize(task.File.Username);
                            
                            try
                            {
                                // INTEGRACIÓN NICOTINE+: Registrar inicio en estadísticas
                                transferStats?.RecordTransferStart(task.File.Username, task.File.Network ?? "Soulseek");
                                
                                // INTEGRACIÓN NICOTINE+: Publicar evento de inicio
                                eventBus?.Publish(new TransferStartedMessage
                                {
                                    FileName = task.File.FileName,
                                    Username = task.File.Username,
                                    FileSize = task.File.SizeBytes
                                });
                                
                                task.StartedAt = DateTime.UtcNow;
                                
                                MarkTaskProgress(task, DateTime.UtcNow);
                                await OnDownloadFile(task);

                                if (task.Status == DownloadStatus.Completed)
                                {
                                    // INTEGRACIÓN NICOTINE+: Completar descarga con PartialFileManager
                                    try
                                    {
                                        await partialManager.CompleteDownloadAsync(task.LocalPath);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log($"⚠️ [PartialFile] Error completando descarga: {ex.Message}");
                                    }
                                    
                                    var duration = TimeSpan.Zero;
                                    try
                                    {
                                        if (task.StartTime.HasValue)
                                        {
                                            var end = task.EndTime ?? DateTime.Now;
                                            duration = end - task.StartTime.Value;
                                        }
                                    }
                                    catch
                                    {
                                    }

                                    // INTEGRACIÓN NICOTINE+: Registrar éxito en estadísticas
                                    transferStats?.RecordTransferSuccess(
                                        task.File.Username,
                                        task.File.Network ?? "Soulseek",
                                        task.File.SizeBytes,
                                        duration
                                    );
                                    
                                    // Registrar en métricas
                                    try
                                    {
                                        OnFileDownloaded?.Invoke(task.File.SizeBytes);
                                    }
                                    catch
                                    {
                                    }
                                    
                                    // INTEGRACIÓN NICOTINE+: Publicar evento de completado
                                    eventBus?.Publish(new TransferCompletedMessage
                                    {
                                        FileName = task.File.FileName,
                                        BytesTransferred = task.File.SizeBytes,
                                        Duration = duration
                                    });

                                    try
                                    {
                                        RecordProviderSuccess(task.File.Username, task.File?.SizeBytes ?? 0, duration);
                                    }
                                    catch
                                    {
                                        RecordProviderSuccess(task.File.Username);
                                    }

                                    try
                                    {
                                        var fileKey = $"{task.File.FileName}_{task.File.SizeBytes}";
                                        RecordFileProviderSuccess(fileKey, task.File.Username);
                                    }
                                    catch
                                    {
                                    }
                                }
                            }
                            finally
                            {
                                // INTEGRACIÓN NICOTINE+: Decrementar contador de cola
                                queueManager?.DecrementQueueSize(task.File.Username);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"❌ Error en descarga: {ex.Message}");
                            if (task.Status != DownloadStatus.Failed)
                            {
                                task.Status = DownloadStatus.Failed;
                            }

                            if (string.IsNullOrWhiteSpace(task.ErrorMessage))
                            {
                                task.ErrorMessage = ex.Message;
                            }

                            if (task.LastFailureReason == DownloadFailureReason.Unknown)
                            {
                                task.LastFailureReason = DownloadFailureClassifier.FromException(ex);
                            }
                            
                            // INTEGRACIÓN NICOTINE+: Registrar fallo en estadísticas
                            transferStats?.RecordTransferFailure(
                                task.File.Username,
                                task.File.Network ?? "Soulseek",
                                task.LastFailureReason.ToString()
                            );
                            
                            // INTEGRACIÓN NICOTINE+: Publicar evento de fallo
                            eventBus?.Publish(new TransferFailedMessage
                            {
                                FileName = task.File.FileName,
                                ErrorMessage = task.ErrorMessage,
                                Reason = Models.TransferFailureReason.Unknown
                            });
                            
                            // INTEGRACIÓN NICOTINE+: Registrar fallo para auto-ban
                            banManager.RecordFailure(task.File.Username, task.ErrorMessage);
                            
                            RecordProviderFailure(task.File.Username, task.LastFailureReason);
                            task.LastRetryTime = DateTime.UtcNow;
                            task.RetryCount++;

                            // INTEGRACIÓN NICOTINE+: Usar IntelligentRetryStrategy
                            if (!retryStrategy.ShouldRetry(task))
                            {
                                task.AutoRetryEnabled = false;
                                task.FinalFailureTime = DateTime.UtcNow;
                                task.IsScheduled = false;
                                task.ScheduledAt = null;
                                var retryInfo = retryStrategy.GetRetryInfo(task);
                                Log($"⛔ No reintentar: {task.File.FileName} - {retryInfo.RecommendedAction}");
                                return;
                            }

                            // Buscar alternativa ANTES de programar el siguiente intento
                            await TryFindAlternativeProviderAsync(task);

                            // INTEGRACIÓN NICOTINE+: Calcular delay con estrategia inteligente
                            var intelligentDelay = retryStrategy.CalculateRetryDelay(task);
                            task.RetryAt = DateTime.UtcNow + intelligentDelay;
                            Log($"🔄 Retry inteligente en {intelligentDelay.TotalMinutes:F0} minutos para {task.File.FileName}");
                            
                            var baseDelay = intelligentDelay;
                            var nextEligibleUtc = DateTime.UtcNow + baseDelay;
                            var scheduled = downloadQueueService.Update(list =>
                            {
                                if (!list.Contains(task))
                                {
                                    return false;
                                }

                                task.IsScheduled = true;
                                task.ScheduledAt = nextEligibleUtc;
                                task.Status = DownloadStatus.Queued;
                                return true;
                            });

                            if (scheduled)
                            {
                                Log($"⏳ Reintento programado: {task.File.FileName} en {(nextEligibleUtc - DateTime.UtcNow).TotalSeconds:F0}s ({task.LastFailureReason})");
                            }
                        }
                        finally
                        {
                            ReleaseHalfOpenTrial(task.File?.Username);
                            lock (activeTasksLock)
                            {
                                activeDownloadTasks.Remove(task);
                            }

                            lock (stallTrackerLock)
                            {
                                stallTracker.Remove(task);
                            }
                        }
                    });

                    lock (activeTasksLock)
                    {
                        activeDownloadTasks[task] = downloadTask;
                    }
                }
            }
        }

        public bool ApplyQueuePrioritization()
        {
            bool changed = downloadQueueService.Update(list => ApplyQueuePrioritization(list));

            if (!changed)
                return false;

            Log($"🎯 Priorización aplicada con estrategia {queueStrategy} ({DateTime.Now:HH:mm:ss})");
            OnQueueChanged?.Invoke();
            ScheduleQueueSave();
            return true;
        }

        private bool ApplyQueuePrioritization(IList<DownloadTask> queue)
        {
            if (queueStrategy == QueuePrioritizationStrategy.Manual)
                return false;

            var pendingTasks = queue.Where(t => t.Status == DownloadStatus.Queued).ToList();
            if (pendingTasks.Count <= 1)
                return false;

            var sortedTasks = queueStrategy switch
            {
                QueuePrioritizationStrategy.FastestFirst => pendingTasks
                    .OrderByDescending(t => GetProviderScore(t.File.Username))
                    .ThenBy(t => t.File.SizeBytes)
                    .ToList(),
                QueuePrioritizationStrategy.SmallestFirst => pendingTasks
                    .OrderBy(t => t.File.SizeBytes)
                    .ThenByDescending(t => GetProviderScore(t.File.Username))
                    .ToList(),
                QueuePrioritizationStrategy.LargestFirst => pendingTasks
                    .OrderByDescending(t => t.File.SizeBytes)
                    .ThenByDescending(t => GetProviderScore(t.File.Username))
                    .ToList(),
                QueuePrioritizationStrategy.Balanced => pendingTasks
                    .OrderBy(t => CalculateBalancedScore(t))
                    .ThenByDescending(t => GetProviderScore(t.File.Username))
                    .ToList(),
                _ => pendingTasks
            };

            bool changed = !pendingTasks.SequenceEqual(sortedTasks);
            if (!changed)
                return false;

            var reordered = new List<DownloadTask>(queue.Count);
            int pendingIndex = 0;
            foreach (var task in queue)
            {
                if (task.Status == DownloadStatus.Queued)
                {
                    reordered.Add(sortedTasks[pendingIndex++]);
                }
                else
                {
                    reordered.Add(task);
                }
            }

            queue.Clear();
            foreach (var task in reordered)
            {
                queue.Add(task);
            }

            return true;
        }

        private double CalculateBalancedScore(DownloadTask task)
        {
            double sizeMb = Math.Max(task.File.SizeBytes / (1024.0 * 1024.0), 0.1);
            double providerScore = GetProviderScore(task.File.Username);
            double retryPenalty = task.RetryCount * 2;

            return sizeMb / (providerScore + 1) + retryPenalty;
        }

        public SlskDown.Models.ProviderStats GetProviderStats(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return null;
            }

            lock (providerStatsLock)
            {
                if (!providerStats.TryGetValue(username, out var stats))
                {
                    return null;
                }

                return new SlskDown.Models.ProviderStats
                {
                    Username = stats.Username,
                    TotalDownloads = stats.TotalDownloads,
                    SuccessfulDownloads = stats.SuccessfulDownloads,
                    FailedDownloads = stats.FailedDownloads,
                    AverageSpeed = stats.AverageSpeed,
                    TotalBytesDownloaded = stats.TotalBytesDownloaded,
                    LastDownload = stats.LastDownload,
                    LastDownloadDate = stats.LastDownloadDate
                };
            }
        }

        public bool TryGetBlacklistInfo(string username, out int failures, out double hoursRemaining)
        {
            failures = 0;
            hoursRemaining = 0;

            if (string.IsNullOrWhiteSpace(username))
                return false;

            lock (blacklistLock)
            {
                if (!providerBlacklist.TryGetValue(username, out var data))
                    return false;

                var elapsedHours = (DateTime.UtcNow - data.lastFail).TotalHours;
                if (elapsedHours >= EffectiveProviderBlacklistHours)
                {
                    providerBlacklist.Remove(username);
                    ScheduleStateSave();
                    return false;
                }

                failures = data.failures;
                hoursRemaining = Math.Max(0, EffectiveProviderBlacklistHours - elapsedHours);
                return true;
            }
        }

        public double GetProviderScore(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return 0;

            lock (providerStatsLock)
            {
                return providerStats.TryGetValue(username, out var stats) ? stats.QualityScore : 0;
            }
        }

        private System.Threading.Timer CreateTimer(string name, System.Threading.TimerCallback callback, int intervalSeconds)
        {
            if (intervalSeconds <= 0)
                return null;

            return new System.Threading.Timer(callback, null, Timeout.Infinite, Timeout.Infinite);
        }

        private void ResetTimers()
        {
            queuePositionTimer?.Change(TimeSpan.FromSeconds(config.QueuePositionRefreshIntervalSeconds),
                TimeSpan.FromSeconds(config.QueuePositionRefreshIntervalSeconds));
            retryConnectionTimer?.Change(TimeSpan.FromSeconds(config.RetryConnectionIntervalSeconds),
                TimeSpan.FromSeconds(config.RetryConnectionIntervalSeconds));
            retryIoTimer?.Change(TimeSpan.FromSeconds(config.RetryIoIntervalSeconds),
                TimeSpan.FromSeconds(config.RetryIoIntervalSeconds));
            cleanupTimer?.Change(TimeSpan.FromSeconds(config.CleanupIntervalSeconds),
                TimeSpan.FromSeconds(config.CleanupIntervalSeconds));

            var watchdogIntervalSeconds = Math.Max(5, config.StallWatchdogIntervalSeconds);
            stallWatchdogTimer?.Change(TimeSpan.FromSeconds(watchdogIntervalSeconds),
                TimeSpan.FromSeconds(watchdogIntervalSeconds));
        }

        private void MarkTaskProgress(DownloadTask task, DateTime nowUtc)
        {
            if (task == null)
            {
                return;
            }

            lock (stallTrackerLock)
            {
                stallTracker[task] = (task.BytesDownloaded, nowUtc);
            }
        }

        private void OnStallWatchdogTimer(object state)
        {
            if (!isRunning)
            {
                return;
            }

            var nowUtc = DateTime.UtcNow;
            var stalledTasks = new List<DownloadTask>();
            var stallThreshold = TimeSpan.FromSeconds(Math.Max(30, config.StallNoProgressSeconds));

            downloadQueueService.WithQueueLock(list =>
            {
                foreach (var task in list)
                {
                    if (task == null || task.Status != DownloadStatus.Downloading)
                    {
                        continue;
                    }

                    if (task.File == null || string.IsNullOrWhiteSpace(task.File.Username))
                    {
                        continue;
                    }

                    (long lastBytes, DateTime lastProgressUtc) entry;
                    var hasEntry = false;
                    lock (stallTrackerLock)
                    {
                        hasEntry = stallTracker.TryGetValue(task, out entry);
                        if (!hasEntry)
                        {
                            stallTracker[task] = (task.BytesDownloaded, nowUtc);
                            continue;
                        }
                    }

                    if (task.BytesDownloaded > entry.lastBytes)
                    {
                        MarkTaskProgress(task, nowUtc);
                        continue;
                    }

                    var effectiveThreshold = stallThreshold;

                    if ((nowUtc - entry.lastProgressUtc) >= effectiveThreshold)
                    {
                        stalledTasks.Add(task);
                    }
                }

                return 0;
            });

            foreach (var task in stalledTasks)
            {
                try
                {
                    HandleStalledDownload(task, nowUtc);
                }
                catch (Exception ex)
                {
                    Log($"⚠️ Error en watchdog de atascos: {ex.Message}");
                }
            }
        }

        private void HandleStalledDownload(DownloadTask task, DateTime nowUtc)
        {
            if (task == null)
            {
                return;
            }

            var updated = downloadQueueService.Update(list =>
            {
                if (!list.Contains(task))
                {
                    return false;
                }

                if (task.Status != DownloadStatus.Downloading)
                {
                    return false;
                }

                task.Status = DownloadStatus.Failed;
                task.LastFailureReason = DownloadFailureReason.Timeout;
                task.ErrorMessage ??= "Stalled: sin progreso durante 5 minutos";
                task.LastRetryTime = nowUtc;
                return true;
            });

            if (!updated)
            {
                return;
            }

            lock (stallTrackerLock)
            {
                stallTracker.Remove(task);
            }

            Log($"⏱️ Watchdog: descarga atascada, cancelando: {task.File?.FileName}");
            try { task.CancellationToken?.Cancel(); } catch { }
            OnQueueChanged?.Invoke();
        }

        private async void OnQueuePositionTimer(object state)
        {
            if (!isRunning || OnRequestQueuePosition == null)
                return;

            var queued = GetQueueSnapshot().Where(t => t.Status == DownloadStatus.Queued).ToList();
            foreach (var task in queued)
            {
                try
                {
                    await OnRequestQueuePosition(task);
                }
                catch (Exception ex)
                {
                    Log($"⚠️ Error consultando posición de cola: {ex.Message}");
                }
            }
        }

        private async void OnRetryConnectionTimer(object state)
        {
            if (!isRunning)
                return;

            var toRetry = GetQueueSnapshot()
                .Where(t => DownloadRetryCoordinator.ShouldRetryConnection(t, config))
                .ToList();

            foreach (var task in toRetry)
            {
                await RetryTaskAsync(task, DownloadFailureReason.Connection);
            }
        }

        private async void OnRetryIoTimer(object state)
        {
            if (!isRunning)
                return;

            var toRetry = GetQueueSnapshot()
                .Where(t => DownloadRetryCoordinator.ShouldRetryIo(t, config))
                .ToList();

            foreach (var task in toRetry)
            {
                await RetryTaskAsync(task, DownloadFailureReason.FileIo);
            }
        }

        private async void OnCleanupTimer(object state)
        {
            try
            {
                CleanupIncompleteDownloads();
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error limpiando temporales: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        private async Task RetryTaskAsync(DownloadTask task, DownloadFailureReason reason)
        {
            if (task == null || !task.AutoRetryEnabled)
                return;

            if (task.RetryCount >= EffectiveMaxTotalAttempts)
            {
                task.AutoRetryEnabled = false;
                task.FinalFailureTime = DateTime.UtcNow;
                task.IsScheduled = false;
                task.ScheduledAt = null;
                return;
            }

            bool updated = downloadQueueService.Update(list =>
            {
                if (!list.Contains(task))
                {
                    return false;
                }

                task.Status = DownloadStatus.Queued;
                task.RetryCount++;

                if (task.RetryCount >= EffectiveMaxTotalAttempts)
                {
                    task.AutoRetryEnabled = false;
                    task.FinalFailureTime = DateTime.UtcNow;
                    task.Status = DownloadStatus.Failed;
                    task.IsScheduled = false;
                    task.ScheduledAt = null;
                    return true;
                }

                task.LastRetryTime = DateTime.UtcNow;
                task.ErrorMessage = null;

                var baseDelay = ComputeRetryBackoffDelay(task.RetryCount);
                var nextEligibleUtc = ComputeNextEligibleUtc(task, baseDelay);
                task.IsScheduled = true;
                task.ScheduledAt = nextEligibleUtc;
                return true;
            });

            if (!updated)
            {
                return;
            }

            Log($"🔄 Reintento programado tras {reason}: {task.File.FileName}");
            OnQueueChanged?.Invoke();
            await Task.CompletedTask;
        }

        private void CleanupIncompleteDownloads()
        {
            if (string.IsNullOrWhiteSpace(config.IncompleteDownloadsDirectory))
                return;

            var folder = config.IncompleteDownloadsDirectory;
            if (!System.IO.Directory.Exists(folder))
                return;

            var activePaths = downloadQueueService.WithQueueLock(list =>
            {
                var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var task in list)
                {
                    if (string.IsNullOrWhiteSpace(task.LocalPath))
                    {
                        continue;
                    }

                    try
                    {
                        if (System.IO.File.Exists(task.LocalPath))
                        {
                            paths.Add(task.LocalPath);
                        }
                    }
                    catch
                    {
                    }
                }

                return paths;
            });

            foreach (var file in System.IO.Directory.GetFiles(folder))
            {
                if (activePaths.Contains(file))
                {
                    try
                    {
                        var finfo = new FileInfo(file);
                        var age = DateTime.UtcNow - finfo.LastWriteTimeUtc;
                        if (finfo.Length == 0 && age >= INCOMPLETE_ZERO_BYTE_MAX_AGE)
                        {
                            System.IO.File.Delete(file);
                            Log($"🧹 Eliminado temporal 0 bytes obsoleto: {Path.GetFileName(file)}");
                        }
                    }
                    catch
                    {
                    }
                    continue;
                }

                try
                {
                    System.IO.File.Delete(file);
                    Log($"🧹 Eliminado temporal obsoleto: {Path.GetFileName(file)}");
                }
                catch (Exception ex)
                {
                    Log($"⚠️ No se pudo eliminar temporal {file}: {ex.Message}");
                }
            }
        }

        #region Periodic Saving (Nicotine+ inspired)

        /// <summary>
        /// Marca las transferencias como modificadas para guardado periódico
        /// </summary>
        private void MarkTransfersModified()
        {
            transfersModified = true;
        }

        /// <summary>
        /// Timer callback para guardado periódico (cada 3 minutos)
        /// Inspirado en Nicotine+ para reducir I/O
        /// </summary>
        private void OnPeriodicSaveTimer()
        {
            try
            {
                if (!allowSavingTransfers)
                {
                    Log("⏭️ Guardado periódico omitido (shutdown en progreso)");
                    return;
                }

                if (!transfersModified)
                {
                    Log("⏭️ Guardado periódico omitido (sin cambios)");
                    return;
                }

                Log("💾 Guardado periódico iniciado...");
                SaveStateAsync().GetAwaiter().GetResult();
                transfersModified = false;
                Log("✅ Guardado periódico completado");
            }
            catch (Exception ex)
            {
                Log($"❌ Error en guardado periódico: {ex.Message}");
            }
        }

        #endregion

        private void Log(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            try
            {
                OnLog?.Invoke(message);
            }
            catch
            {
                // Ignorar errores de logging para no afectar flujo principal
            }
        }

        private void ScheduleStateSave()
        {
            if (stateSaveScheduled)
            {
                return;
            }

            stateSaveScheduled = true;
            Task.Run(async () =>
            {
                await Task.Delay(STATE_SAVE_DELAY_MS);

                if (!await statePersistenceSemaphore.WaitAsync(0))
                {
                    stateSaveScheduled = false;
                    return;
                }

                try
                {
                    await SaveStateAsync();
                }
                catch (Exception ex)
                {
                    Log($"❌ Error guardando estado: {ex.Message}");
                }
                finally
                {
                    stateSaveScheduled = false;
                    statePersistenceSemaphore.Release();
                }
            });
        }

        private async Task SaveStateAsync()
        {
            if (!await saveStateSemaphore.WaitAsync(0))
            {
                return;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(config.ProviderStatsPath))
                {
                    await SaveProviderStatsAsync();
                }

                if (!string.IsNullOrWhiteSpace(config.ProviderBlacklistPath))
                {
                    await SaveProviderBlacklistAsync();
                }

                if (!string.IsNullOrWhiteSpace(config.FileProviderPreferencesPath))
                {
                    await SaveFileProviderPreferencesAsync();
                }
            }
            finally
            {
                saveStateSemaphore.Release();
            }
        }

        private async Task SaveFileProviderPreferencesAsync()
        {
            var path = config.FileProviderPreferencesPath;
            try
            {
                Dictionary<string, Dictionary<string, int>> snapshot;
                lock (fileProviderLock)
                {
                    snapshot = fileProviderSuccessCounts.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new Dictionary<string, int>(kvp.Value, StringComparer.OrdinalIgnoreCase),
                        StringComparer.OrdinalIgnoreCase);
                }

                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await System.IO.File.WriteAllTextAsync(path, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Log($"❌ Error guardando preferencias por archivo: {ex.Message}");
            }
        }

        private async Task SaveProviderStatsAsync()
        {
            var path = config.ProviderStatsPath;
            try
            {
                var snapshot = new List<SlskDown.Models.ProviderStats>();
                lock (providerStatsLock)
                {
                    snapshot.AddRange(providerStats.Values.Select(stats => new SlskDown.Models.ProviderStats
                    {
                        Username = stats.Username,
                        TotalDownloads = stats.TotalDownloads,
                        SuccessfulDownloads = stats.SuccessfulDownloads,
                        FailedDownloads = stats.FailedDownloads,
                        AverageSpeed = stats.AverageSpeed,
                        TotalBytesDownloaded = stats.TotalBytesDownloaded,
                        LastDownload = stats.LastDownload,
                        LastDownloadDate = stats.LastDownloadDate
                    }));
                }

                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await System.IO.File.WriteAllTextAsync(path, json, Encoding.UTF8);
                Log($"💾 Estadísticas de proveedores guardadas ({snapshot.Count})");
            }
            catch (Exception ex)
            {
                Log($"❌ Error guardando estadísticas de proveedores: {ex.Message}");
            }
        }

        private async Task SaveProviderBlacklistAsync()
        {
            var path = config.ProviderBlacklistPath;
            try
            {
                Dictionary<string, (int failures, DateTime lastFail, DateTime blockedUntil)> snapshot;
                Dictionary<string, DateTime> cooldownSnapshot;
                Dictionary<string, DownloadFailureReason> reasonSnapshot;
                Dictionary<string, DateTime> circuitSnapshot;
                Dictionary<string, List<DateTime>> retryBudgetSnapshot;
                lock (blacklistLock)
                {
                    snapshot = new Dictionary<string, (int failures, DateTime lastFail, DateTime blockedUntil)>(providerBlacklist);
                    cooldownSnapshot = new Dictionary<string, DateTime>(providerCooldownUntil, StringComparer.OrdinalIgnoreCase);
                    reasonSnapshot = new Dictionary<string, DownloadFailureReason>(providerCooldownReason, StringComparer.OrdinalIgnoreCase);
                }

                lock (circuitLock)
                {
                    circuitSnapshot = new Dictionary<string, DateTime>(providerCircuitOpenUntilUtc, StringComparer.OrdinalIgnoreCase);
                }

                lock (providerRetryBudgetLock)
                {
                    retryBudgetSnapshot = providerRetryBudgetFailures.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.ToList(),
                        StringComparer.OrdinalIgnoreCase);
                }

                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                var payload = snapshot.ToDictionary(
                    kvp => kvp.Key,
                    kvp =>
                    {
                        var entry = new ProviderBlacklistEntry { Failures = kvp.Value.failures, LastFail = kvp.Value.lastFail };
                        if (cooldownSnapshot.TryGetValue(kvp.Key, out var untilUtc))
                        {
                            entry.CooldownUntilUtc = untilUtc;
                            if (reasonSnapshot.TryGetValue(kvp.Key, out var reason))
                            {
                                entry.CooldownReason = reason;
                            }
                        }

                        if (circuitSnapshot.TryGetValue(kvp.Key, out var openUntilUtc))
                        {
                            entry.CircuitOpenUntilUtc = openUntilUtc;
                        }

                        if (retryBudgetSnapshot.TryGetValue(kvp.Key, out var failures))
                        {
                            entry.RetryBudgetFailuresUtc = failures;
                        }

                        return entry;
                    });

                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await System.IO.File.WriteAllTextAsync(path, json, Encoding.UTF8);
                Log($"💾 Blacklist de proveedores guardada ({snapshot.Count})");
            }
            catch (Exception ex)
            {
                Log($"❌ Error guardando blacklist de proveedores: {ex.Message}");
            }
        }

        private sealed class ProviderBlacklistEntry
        {
            public int Failures { get; set; }
            public DateTime LastFail { get; set; }
            public DateTime? CooldownUntilUtc { get; set; }
            public DownloadFailureReason? CooldownReason { get; set; }
            public DateTime? CircuitOpenUntilUtc { get; set; }
            public List<DateTime> RetryBudgetFailuresUtc { get; set; }
        }

        #region Transfer Rejection Parsing (Nicotine+ inspired)

        /// <summary>
        /// Parsea razones de rechazo de transferencia según protocolo Soulseek
        /// Inspirado en Nicotine+ para diagnóstico preciso
        /// </summary>
        public static DownloadStatus ParseTransferRejection(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return DownloadStatus.Failed;

            reason = reason.Trim();

            // Razones estándar del protocolo Soulseek
            return reason switch
            {
                "Banned" => DownloadStatus.Filtered,
                "Cancelled" => DownloadStatus.Cancelled,
                "Complete" => DownloadStatus.Completed,
                "File not shared." or "File not shared" => DownloadStatus.RemoteFileError,
                "File read error." => DownloadStatus.RemoteFileError,
                "Pending shutdown." => DownloadStatus.Failed,
                "Queued" => DownloadStatus.Queued,
                "Too many files" => DownloadStatus.UserQueueFull,
                "Too many megabytes" => DownloadStatus.UserQuotaExceeded,
                
                // Razones deprecated pero aún en uso
                "Blocked country" => DownloadStatus.Filtered,
                "Disallowed extension" => DownloadStatus.Filtered,
                "Remote file error" => DownloadStatus.RemoteFileError,
                
                // Patrones parciales
                _ when reason.Contains("User limit", StringComparison.OrdinalIgnoreCase) && 
                       reason.Contains("megabytes", StringComparison.OrdinalIgnoreCase) 
                    => DownloadStatus.UserQuotaExceeded,
                    
                _ when reason.Contains("User limit", StringComparison.OrdinalIgnoreCase) && 
                       reason.Contains("files", StringComparison.OrdinalIgnoreCase) 
                    => DownloadStatus.UserQueueFull,
                    
                _ when reason.Contains("not shared", StringComparison.OrdinalIgnoreCase) 
                    => DownloadStatus.RemoteFileError,
                    
                _ when reason.Contains("banned", StringComparison.OrdinalIgnoreCase) 
                    => DownloadStatus.Filtered,
                    
                _ => DownloadStatus.Failed
            };
        }

        /// <summary>
        /// Determina si una descarga debe reintentarse según su estado
        /// Inspirado en Nicotine+ para reintentos inteligentes
        /// </summary>
        public static bool ShouldRetryDownload(DownloadStatus status)
        {
            return status switch
            {
                // Reintentar: problemas temporales
                DownloadStatus.UserQueueFull => true,        // Cola llena, reintentar más tarde
                DownloadStatus.UserQuotaExceeded => true,    // Cuota excedida, reintentar más tarde
                DownloadStatus.ConnectionTimeout => true,    // Timeout, reintentar
                DownloadStatus.ConnectionClosed => true,     // Conexión cerrada, reintentar
                DownloadStatus.UserLoggedOff => true,        // Usuario offline, reintentar cuando vuelva
                DownloadStatus.Failed => true,               // Fallo genérico, reintentar
                DownloadStatus.LocalFileError => true,       // Error I/O local, reintentar
                
                // NO reintentar: problemas permanentes
                DownloadStatus.RemoteFileError => false,     // Archivo no compartido o error lectura
                DownloadStatus.Filtered => false,            // Filtrado/banned, no reintentar
                DownloadStatus.Cancelled => false,           // Cancelado por usuario
                DownloadStatus.Completed => false,           // Ya completado
                
                // Estados intermedios
                DownloadStatus.Queued => false,              // Ya en cola
                DownloadStatus.GettingStatus => false,       // Verificando estado
                DownloadStatus.Downloading => false,         // Descargando activamente
                DownloadStatus.Paused => false,              // Pausado manualmente
                
                _ => false
            };
        }

        /// <summary>
        /// Calcula delay de reintento según tipo de error (exponential backoff)
        /// </summary>
        public static TimeSpan GetRetryDelay(DownloadStatus status, int retryCount)
        {
            var baseDelay = status switch
            {
                // Delays cortos para problemas de conexión
                DownloadStatus.ConnectionTimeout => TimeSpan.FromSeconds(5),
                DownloadStatus.ConnectionClosed => TimeSpan.FromSeconds(5),
                
                // Delays medios para cola/cuota
                DownloadStatus.UserQueueFull => TimeSpan.FromMinutes(2),
                DownloadStatus.UserQuotaExceeded => TimeSpan.FromMinutes(5),
                
                // Delays largos para usuario offline
                DownloadStatus.UserLoggedOff => TimeSpan.FromMinutes(10),
                
                // Delay estándar para otros
                _ => TimeSpan.FromSeconds(30)
            };

            // Exponential backoff: delay * 2^retryCount (máximo 1 hora)
            var exponentialDelay = baseDelay * Math.Pow(2, Math.Min(retryCount, 5));
            return TimeSpan.FromSeconds(Math.Min(exponentialDelay.TotalSeconds, 3600));
        }

        #endregion

    }

    /// <summary>
    /// Configuración del DownloadManager
    /// </summary>
    public class DownloadManagerConfig
    {
        public int MaxSimultaneousDownloads { get; set; } = 3;
        public int MaxSimultaneousDownloadsPerProvider { get; set; } = 1;
        public int MaxAlternativeRetries { get; set; } = 3;
        public int MaxRetries { get; set; } = 3;
        public int MaxTotalAttempts { get; set; } = 15;
        public string DownloadDirectory { get; set; }
        public bool OrganizeByAuthor { get; set; } = true;
        public QueuePrioritizationStrategy QueueStrategy { get; set; } = QueuePrioritizationStrategy.Balanced;
        public string QueuePersistencePath { get; set; }
        public int ProviderBlacklistThreshold { get; set; } = 3;
        public double ProviderBlacklistHours { get; set; } = 1;
        public int QueuePositionRefreshIntervalSeconds { get; set; } = 300;
        public int RetryConnectionIntervalSeconds { get; set; } = 180;
        public int RetryIoIntervalSeconds { get; set; } = 900;
        public int CleanupIntervalSeconds { get; set; } = 600;
        public int RetryBackoffMilliseconds { get; set; } = 2000;
        public int RetryBackoffMaxSeconds { get; set; } = 900;
        public double RetryBackoffJitterRatio { get; set; } = 0.20;
        public int MaxProviderCooldownMinutes { get; set; } = 240;
        public double ProviderCooldownJitterRatio { get; set; } = 0.20;
        public int CircuitBreakerFailureThreshold { get; set; } = 5;
        public int CircuitBreakerOpenSeconds { get; set; } = 120;
        public string ProviderStatsPath { get; set; }
        public string ProviderBlacklistPath { get; set; }
        public string FileProviderPreferencesPath { get; set; }
        public string QueuePersistenceLegacyPath { get; set; }
        public string QueuePersistenceBackupPath { get; set; }
        public string IncompleteDownloadsDirectory { get; set; }
        public int DownloadSpeedLimitKiB { get; set; }
        public string PostDownloadCommand { get; set; }
        public string DownloadFilterPattern { get; set; }
        public int ProcessQueueIntervalMs { get; set; } = 1000;
        public int ProcessQueueErrorDelayMs { get; set; } = 5000;

        public int StallNoProgressSeconds { get; set; } = 300;
        public int StallWatchdogIntervalSeconds { get; set; } = 30;
        
        // MEJORA #1: Persistencia de progreso
        public bool EnableProgressPersistence { get; set; } = true;
        public int ProgressSaveIntervalSeconds { get; set; } = 30;
        
        // MEJORA #2: Gestión de errores mejorada
        public int MaxFailuresPerFile { get; set; } = 5;
        public bool MoveFailedToEnd { get; set; } = true;
        
        // MEJORA #3: Notificaciones
        public bool EnableNotifications { get; set; } = true;
        public bool EnableSoundOnComplete { get; set; } = false;
        
        // MEJORA #4: Límite de velocidad
        public int MaxDownloadSpeedKBps { get; set; } = 0; // 0 = sin límite
        public bool EnableScheduledSpeed { get; set; } = false;
        public int NightSpeedStartHour { get; set; } = 22; // 10 PM
        public int NightSpeedEndHour { get; set; } = 8; // 8 AM
        
        // MEJORA #5: Búsqueda automática de fuentes alternativas
        public bool EnableAutoSourceSearch { get; set; } = true;
        public int MinDownloadSpeedKBps { get; set; } = 50; // Cambiar fuente si < 50 KB/s
        public int SourceSearchDelaySeconds { get; set; } = 30;
        
        // MEJORA #6: Estadísticas mejoradas
        public bool EnableDetailedStats { get; set; } = true;
        public int StatsHistoryDays { get; set; } = 30;
        
        // MEJORA #7: Filtros de cola
        public bool EnableQueueFiltering { get; set; } = true;
        
        // MEJORA #8: Auto-Retry Inteligente
        public bool EnableIntelligentRetry { get; set; } = true;
        
        // MEJORA #9: Caché de Metadatos
        public bool EnableMetadataCache { get; set; } = true;
        
        // MEJORA #10: Modo Descarga Agresiva
        public bool EnableAggressiveMode { get; set; } = false;
        public int AggressiveModeMaxDownloads { get; set; } = 10;
        
        // MEJORA #11: Compresión de Cola
        public bool EnableQueueCompression { get; set; } = true;
        
        // MEJORA #12: Índice de Búsqueda Rápida
        public bool EnableFastSearchIndex { get; set; } = true;
        
        // MEJORA #13: Prefetch de Proveedores
        public bool EnableProviderPrefetch { get; set; } = true;
        public int PrefetchTopProvidersCount { get; set; } = 5;
        
        // MEJORA #14: Dashboard en Tiempo Real
        public bool EnableRealtimeDashboard { get; set; } = true;
        
        // MEJORA #16: Exportar/Importar Cola
        public bool EnableQueueExportImport { get; set; } = true;
    }

    public static class DownloadFailureClassifier
    {
        public static DownloadFailureReason FromException(Exception ex)
        {
            if (ex == null)
                return DownloadFailureReason.Unknown;

            var message = ex.Message ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(message))
            {
                var status = DownloadManager.ParseTransferRejection(message);
                var fromStatus = status switch
                {
                    DownloadStatus.UserQueueFull => DownloadFailureReason.QueueFull,
                    DownloadStatus.UserQuotaExceeded => DownloadFailureReason.QuotaExceeded,
                    DownloadStatus.Filtered => DownloadFailureReason.Banned,
                    DownloadStatus.RemoteFileError => DownloadFailureReason.FileNotShared,
                    DownloadStatus.ConnectionTimeout => DownloadFailureReason.Timeout,
                    DownloadStatus.ConnectionClosed => DownloadFailureReason.Connection,
                    DownloadStatus.UserLoggedOff => DownloadFailureReason.Connection,
                    _ => DownloadFailureReason.Unknown
                };

                if (fromStatus != DownloadFailureReason.Unknown)
                {
                    return fromStatus;
                }

                if (message.Contains("too many files", StringComparison.OrdinalIgnoreCase))
                    return DownloadFailureReason.QueueFull;

                if (message.Contains("too many megabytes", StringComparison.OrdinalIgnoreCase))
                    return DownloadFailureReason.QuotaExceeded;

                if (message.Contains("not shared", StringComparison.OrdinalIgnoreCase) || message.Contains("file read error", StringComparison.OrdinalIgnoreCase))
                    return DownloadFailureReason.FileNotShared;

                if (message.Contains("banned", StringComparison.OrdinalIgnoreCase) || message.Contains("blocked country", StringComparison.OrdinalIgnoreCase))
                    return DownloadFailureReason.Banned;

                if (message.Contains("pending shutdown", StringComparison.OrdinalIgnoreCase))
                    return DownloadFailureReason.PendingShutdown;

                if (message.Contains("timeout", StringComparison.OrdinalIgnoreCase) || message.Contains("timed out", StringComparison.OrdinalIgnoreCase))
                    return DownloadFailureReason.Timeout;

                if (message.Contains("appears to be offline", StringComparison.OrdinalIgnoreCase))
                    return DownloadFailureReason.Connection;
            }

            if (ex is IOException)
                return DownloadFailureReason.FileIo;

            if (ex is OperationCanceledException)
                return DownloadFailureReason.UserCancelled;

            return DownloadFailureReason.Unknown;
        }
    }

    public static class DownloadRetryCoordinator
    {
        public static bool ShouldRetryConnection(DownloadTask task, DownloadManagerConfig config)
        {
            if (task == null || config == null)
                return false;

            if (task.Status != DownloadStatus.Failed)
                return false;

            if (task.LastFailureReason != DownloadFailureReason.Connection)
                return false;

            return ShouldRetry(task, config.RetryConnectionIntervalSeconds);
        }

        public static bool ShouldRetryIo(DownloadTask task, DownloadManagerConfig config)
        {
            if (task == null || config == null)
                return false;

            if (task.Status != DownloadStatus.Failed)
                return false;

            if (task.LastFailureReason != DownloadFailureReason.FileIo)
                return false;

            return ShouldRetry(task, config.RetryIoIntervalSeconds);
        }

        // INTEGRACIÓN NICOTINE+: Métodos auxiliares para componentes mejorados
        // TEMPORALMENTE COMENTADOS: Requieren campos de instancia que no existen (banManager, partialManager, eventBus)
        /*
        public TransferConfiguration LoadOrCreateTransferConfiguration()
        {
            var configPath = Path.Combine(config.DataDirectory, "transfer_config.json");
            
            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var loadedConfig = JsonSerializer.Deserialize<TransferConfiguration>(json);
                    Log("✅ Configuración de transferencias cargada desde archivo");
                    return loadedConfig;
                }
                catch (Exception ex)
                {
                    Log($"⚠️ Error cargando configuración de transferencias: {ex.Message}. Usando preset optimizado.");
                }
            }
            
            // Usar preset optimizado por defecto
            var defaultConfig = TransferConfiguration.CreateSpeedOptimized();
            Log("✅ Usando configuración de transferencias optimizada por defecto");
            return defaultConfig;
        }

        public bool IsUserOnlineFunc(string username)
        {
            // TODO: Implementar verificación real de estado online
            // Por ahora retorna true para todos
            return true;
        }
        
        public void SetupAdvancedComponentEvents()
        {
            // Eventos del BanManager
            banManager.OnUserBanned += (username, reason) =>
            {
                Log($"🚫 [Auto-Ban] Usuario baneado: {username} - {reason}");
            };
            
            banManager.OnUserUnbanned += (username) =>
            {
                Log($"✅ [Auto-Ban] Usuario desbaneado: {username}");
            };
            
            banManager.OnLog += (message) =>
            {
                Log($"[BanManager] {message}");
            };
            
            // Eventos del PartialFileManager
            partialManager.OnLog += (message) =>
            {
                Log($"[PartialFile] {message}");
            };
        }
        
        public void SetupNicotineEventHandlers()
        {
            // Eventos de inicio de transferencia
            eventBus.Subscribe<TransferStartedMessage>(msg => 
            {
                Log($"🚀 [Nicotine+] Iniciada: {msg.FileName} desde {msg.Username}");
            });
            
            // Eventos de progreso
            eventBus.Subscribe<TransferProgressMessage>(msg => 
            {
                // Solo log cada 10% de progreso para no saturar
                if (msg.Progress % 10 < 1)
                {
                    Log($"📊 [Nicotine+] {msg.FileName}: {msg.Progress:F1}% @ {msg.Speed:F2} MB/s");
                }
            });
            
            // Eventos de completado
            eventBus.Subscribe<TransferCompletedMessage>(msg => 
            {
                Log($"✅ [Nicotine+] Completada: {msg.FileName} ({msg.BytesTransferred:N0} bytes en {msg.Duration.TotalSeconds:F1}s)");
            });
            
            // Eventos de fallo
            eventBus.Subscribe<TransferFailedMessage>(msg => 
            {
                Log($"❌ [Nicotine+] Fallida: {msg.FileName} - {msg.ErrorMessage}");
            });
            
            // Eventos de cancelación
            eventBus.Subscribe<TransferCancelledMessage>(msg => 
            {
                Log($"⏹️ [Nicotine+] Cancelada: {msg.FileName} por {msg.Username}");
            });
        }
        */

        // INTEGRACIÓN NICOTINE+: Métodos públicos para componentes avanzados
        // NOTA: Métodos temporalmente eliminados por incompatibilidad con tipos anidados
        // Se restaurarán cuando los componentes Nicotine+ estén completamente implementados

        public static bool ShouldRetry(DownloadTask task, int intervalSeconds)
        {
            if (!task.LastRetryTime.HasValue)
                return true;

            var lastRetryUtc = DateTime.SpecifyKind(task.LastRetryTime.Value, DateTimeKind.Utc);
            return (DateTime.UtcNow - lastRetryUtc).TotalSeconds >= intervalSeconds;
        }
    }

    public class DownloadQueuePersistenceEntry
    {
        public DownloadTask Task { get; set; }
        public DateTime CreatedAt { get; set; }
        public int RetryCount { get; set; }
    }
}
