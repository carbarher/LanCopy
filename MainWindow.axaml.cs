using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
using System.Text.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using QRCoder;
using Avalonia.Styling;
using Avalonia.Threading;
using LanCopy.Models;
using LanCopy.Localization;
using LanCopy.Dialogs;
using LanCopy.Services;
using LanCopy.Services.UI;

namespace LanCopy;

public partial class MainWindow : Window, IConnectionUiHost, ITransferUiHost
{
    // Constantes de validación
    private const int MinPinLength = 4;
    private const int MaxPinLength = 128;
    private const int SettingsSchemaVersion = 1;
    private readonly FileServer _server = new();
    private static Loc L => Loc.Instance;
    private PeerDiscovery? _discovery; // Feature 12: UDP auto-descubrimiento
    private TrayIcon? _tray;            // idea-tray: icono de bandeja
    private LanClient? _client;        // upload client
    private LanClient? _clientDown;   // Feature 2: cliente separado para download simultáneo
    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private readonly SemaphoreSlim _clientLockDown = new(1, 1);

    private string _localPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private string _remotePath = "";
    private readonly Dictionary<string, PeerFolderState> _peerFolders = new(StringComparer.OrdinalIgnoreCase);

    private int _isUploading;    // Feature 2: bidireccional — separado de downloading
    private int _isDownloading;
    private int _isPreparingFolder;
    // U4+Q1: usar Volatile.Read — _isUploading y _isDownloading son escritos desde hilos distintos
    private int _isTransferring => (Volatile.Read(ref _isUploading) | Volatile.Read(ref _isDownloading) | Volatile.Read(ref _isPreparingFolder));
    private CancellationTokenSource _uploadCts = new();
    private CancellationTokenSource _downloadCts = new();
    private readonly SemaphoreSlim _pauseSemaphore = new(1, 1); // Feature 1: pausa
    private int _isPaused; // 0=no, 1=pausado

    // Una conexión por lote es compatible con PCs lentos y versiones anteriores.
    private const int AutomaticFileConcurrency = 1;
    private int _maxParallel = AutomaticFileConcurrency;
    private SemaphoreSlim _transferSemaphore = new(AutomaticFileConcurrency, AutomaticFileConcurrency);
    private readonly object _semLock = new(); // Q5: protege swap atómico de _transferSemaphore
    private LanClient.RemoteCapabilities? _remoteCapabilities;

    private readonly BulkObservableCollection<FileEntry> _localItems = new();
    private List<FileEntry> _localItemsAll = new(); // Feature 9: filtro
    private List<FileEntry> _remoteItemsAll = new(); // Feature 6: sort remoto
    private readonly BulkObservableCollection<FileEntry> _remoteItems = new();
        // Feature 3: perfiles de conexión
    private List<ConnectionProfile> _profiles = new();
    // Feature 6: ordenación de columnas
    private string _localSortField = "name";
    private bool _localSortAsc = true;
    private string _remoteSortField = "name";
    private bool _remoteSortAsc = true;

    // Feature 7: sparkline de velocidad (últimos 10 valores en MB/s)
    private readonly Queue<double> _speedHistory = new();
    // F3: historial de textos recibidos desde peers
    private readonly System.Collections.ObjectModel.ObservableCollection<ChatMessage> _chatMessages = new();
    private ChatWindow? _chatWindow;
    private const int MaxChatHistoryMessages = 500;
    private string? _chatTargetIp;
    private int _chatTargetPort = NetworkValidation.DefaultPort;
    private bool _sortSmallestFirst = false; // F7: enviar archivos pequenos primero
    private const int SparklineLen = 10;

    // Feature 8: watch folder
    private FileSystemWatcher? _watcher;
    private FileSystemWatcher? _watchFolderWatcher; // B1: watch-folder watcher separado
    private bool _watchFolderActive; // B2: flag separado para watch-folder (vs browser watcher)
    private System.Timers.Timer? _watchDebounce;
    private readonly object _watchLock = new();

    // Filtro local: debounce para no re-ordenar la lista entera en cada tecla.
    private Avalonia.Threading.DispatcherTimer? _localFilterDebounce;
    // M3: debounce 150ms para RefreshRemoteAsync — evita ráfagas TCP cuando operaciones en cadena disparan
    //     múltiples refrescos en < 150ms (ej: crear carpeta + renombrar + upload)
    private CancellationTokenSource? _remoteRefreshDebounce;

    // Feature 9: TLS + compresión toggles
    private bool _tlsEnabled = true; // SEGURIDAD: TLS activado por defecto (cifrado silencioso)
    private readonly HashSet<string> _plaintextCompatibilityApprovedPeers = new(StringComparer.OrdinalIgnoreCase);
    private bool _advancedMode;      // UX: interfaz avanzada oculta por defecto
    private bool _welcomeShown;      // UX: true tras mostrar el asistente la primera vez
    private bool _restrictShareRoot = true; // SEGURIDAD: confina peers a carpeta compartida
    private bool _readOnly; // SEGURIDAD: si true, el servidor rechaza put/delete/rename
    private bool _safeModeEnabled = true; // SEGURIDAD: política global de defaults seguros
    private bool _safeModeNoRemoteDelete; // SEGURIDAD: bloquea solo el borrado remoto
    private bool _remotePowerEnabled; // SEGURIDAD: comandos de apagado remoto desactivados por defecto
    private bool _requireApproval; // SEGURIDAD: pedir consentimiento antes de aceptar ficheros
    private bool _requireHighRiskApproval = true; // SEGURIDAD: confirmar localmente comandos remotos de alto riesgo
    private bool _compressEnabled;
    private bool _autoOpenLinks;
    private DispatcherTimer? _fullDiskSessionTimer;
    private DispatcherTimer? _safeModeSessionTimer;
    private DateTimeOffset? _fullDiskUntilUtc;
    private DateTimeOffset? _safeModeUntilUtc;
    private bool _safeModeUntilClose;
    private bool _securityToggleGuard;
    private static readonly TimeSpan FullDiskSessionDuration = TimeSpan.FromMinutes(10);
    private int _bandwidthLimitMbps;
    private string _theme = "Auto"; // tema UI: Dark|Light|Auto


    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LanCopy", "settings.json");

    private static readonly string QueuePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LanCopy", "queue.json");

    // Cached brushes — evita SolidColorBrush.Parse en cada SetConnStatus (#11)
    private static readonly SolidColorBrush BrushConnected = SolidColorBrush.Parse("#28A745");
    private static readonly SolidColorBrush BrushError = SolidColorBrush.Parse("#FF6B6B");
    private static readonly SolidColorBrush BrushConnecting = SolidColorBrush.Parse("#FFD700");

    // Cached controls — evita FindControl<T>() en hot paths (#23)
    private TextBlock? _txtStatus;
    private Button? _btnSend;
    private Button? _btnReceive;
    private Button? _btnCancel;
    private Button? _btnPause;
    private Button? _btnResume;
    // F2: verificacion checksum tras cada download
    private bool _checksumEnabled = false;
    private TextBlock? _txtSpeed;
    private TextBlock? _txtProgressPercent;
    private TextBlock? _txtLocalPath;
    private TextBlock? _txtRemotePath;
    private TextBlock? _txtConnStatus;
    private ProgressBar? _progressBar;
        private WindowNotificationManager? _notifManager; // Feature 7: toast
    private ProgressWindow? _progressWin;       // UX: ventana de progreso para procesos largos
    private DispatcherTimer? _statusBlinkTimer; // UX: parpadeo del mensaje de estado que requiere atencion
    private DispatcherTimer? _browserRefreshTimer;
    private bool _statusBlinkOn;
    private bool _connectButtonIsConnected;
    private bool _connectButtonIsBusy;
    private int _isBrowserAutoRefreshRunning;
    private int _isReconnectInProgress;
    private long _localEntriesSignature;
    private long _remoteEntriesSignature;
    private int _isWindowClosing;
    private readonly ConnectionUiService _connectionUiService;
    private readonly TransferUiService _transferUiService;
 
    public MainWindow() : this(null)
    {
    }

    public MainWindow(string[]? startupArgs)
    {
        InitializeComponent();

        _txtStatus = this.FindControl<TextBlock>("txtStatus");
        _txtSpeed = this.FindControl<TextBlock>("txtSpeed");
        _txtProgressPercent = this.FindControl<TextBlock>("txtProgressPercent");
        _txtLocalPath = this.FindControl<TextBlock>("txtLocalPath");
        _txtRemotePath = this.FindControl<TextBlock>("txtRemotePath");
        _txtConnStatus = this.FindControl<TextBlock>("txtConnStatus");
        _progressBar = this.FindControl<ProgressBar>("transferProgress");
                _btnSend    = this.FindControl<Button>("btnSend");
        _btnReceive = this.FindControl<Button>("btnReceive");
        _btnCancel  = this.FindControl<Button>("btnCancel");
        _btnPause   = this.FindControl<Button>("btnPause");
        _btnResume  = this.FindControl<Button>("btnResume");
        _connectionUiService = new ConnectionUiService(this);
        _transferUiService = new TransferUiService(this);
        InitializeTransferUi();
        _notifManager = new WindowNotificationManager(TopLevel.GetTopLevel(this)!)
        {
            Position = NotificationPosition.BottomRight,
            MaxItems = 3
        };

        this.FindControl<DataGrid>("localList")!.ItemsSource = _localItems;
        this.FindControl<DataGrid>("remoteList")!.ItemsSource = _remoteItems;

        // Feature 6: drag & drop desde explorador de Windows al panel local
        var localList = this.FindControl<DataGrid>("localList")!;
        DragDrop.SetAllowDrop(localList, true);
        localList.AddHandler(DragDrop.DropEvent, OnLocalDrop);
        localList.AddHandler(DragDrop.DragOverEvent, OnLocalDragOver);

        try
        {
            // Aplicar PIN/TLS/confinamiento ANTES de escuchar: LoadSettingsAsync llega tarde.
            var startup = StartupSettings.Load(SettingsPath);
            _tlsEnabled = startup.TlsEnabled;
            _restrictShareRoot = startup.RestrictShareRoot;
            _readOnly = startup.ReadOnly;
            _safeModeEnabled = startup.SafeModeEnabled;
            _safeModeNoRemoteDelete = startup.SafeModeNoRemoteDelete;
            _remotePowerEnabled = startup.RemotePowerEnabled;
            _welcomeShown = startup.WelcomeShown;
            _requireApproval = startup.RequireApproval;
            _requireHighRiskApproval = startup.RequireHighRiskApproval;
            _server.TlsEnabled = _tlsEnabled;
            _server.RestrictToShareRoot = _restrictShareRoot;
            _server.ReadOnly = _readOnly;
            _server.SafeModeNoRemoteDelete = _safeModeNoRemoteDelete;
            _server.RemotePowerEnabled = _remotePowerEnabled;
            _server.RequiredPin = startup.RequiredPin;
            _server.ApproveIncoming = _requireApproval ? OnApproveIncomingAsync : null;
            _server.ApproveHighRisk = _requireHighRiskApproval ? OnApproveHighRiskAsync : null;
            _server.AuthorizePeerCommand = CommandAuthorizer.IsAllowed;
            ApplySafeModePolicy(persist: false, showStatus: false);
            { var chkSafe = this.FindControl<CheckBox>("chkSafeModeNoDelete"); if (chkSafe != null) chkSafe.IsChecked = _safeModeNoRemoteDelete; }
            { var chkRisk = this.FindControl<CheckBox>("chkRequireHighRiskApproval"); if (chkRisk != null) chkRisk.IsChecked = _requireHighRiskApproval; }
            _server.Start(startup.LocalPort);
            _server.TransferProgress += OnServerTransferProgress;
            _server.TextReceived += OnTextReceived;
            _server.DisconnectNoticeReceived += OnDisconnectNoticeReceived;
            { var lpBox = this.FindControl<TextBox>("txtLocalPort"); if (lpBox != null) lpBox.Text = _server.Port.ToString(); }
            this.FindControl<TextBlock>("txtMyIp")!.Text = $"{_server.LocalIp}:{_server.Port}";
            SetStatus(L.Format("st.serverActive", $"{_server.LocalIp}:{_server.Port}"));

            // Feature 12: iniciar auto-descubrimiento UDP
            _discovery = new PeerDiscovery(_server.LocalIp, _server.Port, _tlsEnabled);
            _discovery.PeersChanged += OnPeersChanged; // v5-F7 merged
            _discovery.Start();

            SetupTray(); // idea-tray
            if (startupArgs != null && startupArgs.Length > 0)
            {
                _ = ProcessStartupArgsAsync(startupArgs);
            }
        }
        catch (Exception ex)
        {
            SetStatus(L.Format("st.serverError", ex.Message));
        }

        var cmbLang = this.FindControl<ComboBox>("cmbLang");
        if (cmbLang != null)
        {
            cmbLang.ItemsSource = Loc.Available.Select(a => a.Native).ToList();
            int li = 0;
            for (int i = 0; i < Loc.Available.Count; i++) if (Loc.Available[i].Code == L.Current) { li = i; break; }
            cmbLang.SelectedIndex = li;
        }
        this.FlowDirection = L.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        ApplyUiMode(); // UX: aplica modo simple/avanzado al arrancar
        Closing += (_, _) => ShutdownAppResources();

        Opened += OnWindowOpened;
        StartConnectionWatchdog();
        StartBrowserAutoRefresh();
    }

    private void ShutdownAppResources()
    {
        if (Interlocked.Exchange(ref _isWindowClosing, 1) == 1) return;

        try { SaveSettings(this.FindControl<TextBox>("txtRemoteIp")?.Text ?? "", this.FindControl<TextBox>("txtRemotePort")?.Text ?? "8742"); }
        catch (Exception ex) { Log.Warn("ui", "save-settings-on-close-failed", new { error = ex.Message }); }

        StopConnectionWatchdog();
        StopBrowserAutoRefresh();
        StopStatusBlink();
        if (_watchFolderActive) StopWatch();
        _transferUiService.Shutdown();
        try { _fullDiskSessionTimer?.Stop(); _fullDiskSessionTimer = null; } catch (Exception ex) { Log.Debug("ui", "stop-full-disk-timer-failed", new { error = ex.Message }); }
        try { _safeModeSessionTimer?.Stop(); _safeModeSessionTimer = null; } catch (Exception ex) { Log.Debug("ui", "stop-safe-mode-timer-failed", new { error = ex.Message }); }
        try { _localFilterDebounce?.Stop(); _localFilterDebounce = null; } catch (Exception ex) { Log.Debug("ui", "stop-local-filter-debounce-failed", new { error = ex.Message }); }
        try { _saveDebounce?.Stop(); _saveDebounce = null; } catch (Exception ex) { Log.Debug("ui", "stop-save-debounce-failed", new { error = ex.Message }); }
        try { _remoteRefreshDebounce?.Cancel(); _remoteRefreshDebounce?.Dispose(); _remoteRefreshDebounce = null; } catch (Exception ex) { Log.Debug("ui", "cancel-remote-refresh-debounce-failed", new { error = ex.Message }); }
        try { _watchDebounce?.Stop(); _watchDebounce?.Dispose(); _watchDebounce = null; } catch (Exception ex) { Log.Debug("ui", "dispose-watch-debounce-failed", new { error = ex.Message }); }
        try { _watcher?.Dispose(); _watcher = null; } catch (Exception ex) { Log.Debug("ui", "dispose-local-watcher-failed", new { error = ex.Message }); }
        try { _watchFolderWatcher?.Dispose(); _watchFolderWatcher = null; } catch (Exception ex) { Log.Debug("ui", "dispose-watch-folder-watcher-failed", new { error = ex.Message }); }

        try { _uploadCts.Cancel(); } catch (Exception ex) { Log.Debug("ui", "cancel-upload-cts-on-close-failed", new { error = ex.Message }); }
        try { _downloadCts.Cancel(); } catch (Exception ex) { Log.Debug("ui", "cancel-download-cts-on-close-failed", new { error = ex.Message }); }

        try { _server.Stop(); } catch (Exception ex) { Log.Debug("ui", "server-stop-on-close-failed", new { error = ex.Message }); }
        try { _discovery?.Stop(); _discovery = null; } catch (Exception ex) { Log.Debug("ui", "discovery-stop-on-close-failed", new { error = ex.Message }); }
        try { _client?.Dispose(); _client = null; } catch (Exception ex) { Log.Debug("ui", "client-dispose-on-close-failed", new { error = ex.Message }); }
        try { _clientDown?.Dispose(); _clientDown = null; } catch (Exception ex) { Log.Debug("ui", "download-client-dispose-on-close-failed", new { error = ex.Message }); }
        try { _chatWindow?.Close(); _chatWindow = null; } catch (Exception ex) { Log.Debug("ui", "close-chat-window-on-exit", new { error = ex.Message }); }
        try { _uploadCts.Dispose(); } catch (Exception ex) { Log.Debug("ui", "upload-cts-dispose-on-close-failed", new { error = ex.Message }); }
        try { _downloadCts.Dispose(); } catch (Exception ex) { Log.Debug("ui", "download-cts-dispose-on-close-failed", new { error = ex.Message }); }

        try
        {
            if (_tray != null)
            {
                _tray.IsVisible = false;
                if (_tray is IDisposable disposableTray) disposableTray.Dispose();
                _tray = null;
            }
        }
        catch (Exception ex) { Log.Debug("ui", "dispose-tray-on-close-failed", new { error = ex.Message }); }

        Log.Shutdown(2000);
    }
    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        try
        {
        // Deferir inicialización secundaria para que la ventana pinte primero.
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        await LoadSettingsAsync();

        // Las versiones anteriores podían dejar una cola en disco. Nunca se reanuda sola.
        ClearQueue();
        UpdateQueuePanel(0);

        // Título con versión: gestionar en código porque {l:Tr} es un Observable binding
        // que sobreescribiría cualquier asignación directa a Title. Suscribirse a LanguageChanged
        // para actualizar también cuando el usuario cambia el idioma.
        var _ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        if (_ver != null) Log.Info("app", "started", new { version = $"{_ver.Major}.{_ver.Minor}.{_ver.Build}" });
        var _verSuffix = _ver != null ? $"  v{_ver.Major}.{_ver.Minor}.{_ver.Build}" : "";
        void UpdateTitle() => Dispatcher.UIThread.Post(() =>
            Title = Localization.Loc.Instance["app.title"] + _verSuffix);
        
        // Esperar un instante para que el backend de Avalonia asiente la ventana y no pise el título
        _ = Task.Delay(250).ContinueWith(_ => UpdateTitle(), TaskScheduler.FromCurrentSynchronizationContext());
        Localization.Loc.Instance.LanguageChanged += UpdateTitle;
        Localization.Loc.Instance.LanguageChanged += () => Dispatcher.UIThread.Post(RefreshDynamicTranslations);
        RefreshDynamicTranslations();

        // UX: primer uso -> mostrar asistente de bienvenida para usuarios sin red.
        if (!_welcomeShown)
        {
            _welcomeShown = true;
            try { await new WelcomeDialog().ShowDialog(this); }
            catch (Exception ex) { Log.Warn("ui", "show-welcome-dialog-on-open-failed", new { error = ex.Message }); }
            try { SaveSettings(this.FindControl<TextBox>("txtRemoteIp")?.Text ?? "", this.FindControl<TextBox>("txtRemotePort")?.Text ?? "8742"); }
            catch (Exception ex) { Log.Warn("ui", "save-settings-after-welcome-on-open-failed", new { error = ex.Message }); }
        }

        // Auto-conectar al arrancar si hay IP/puerto guardados
        var autoIp = this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "";
        var autoPortStr = this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742";
        if (!string.IsNullOrEmpty(autoIp) && NetworkValidation.TryParsePort(autoPortStr, out var autoPort))
        {
            ApplyPeerFolderState(autoIp, autoPortStr);
            SetStatus(L.Format("st.connecting", $"{autoIp}:{autoPortStr}"));
            _ = ConnectAsync(autoIp, autoPort);
        }

        // F1: slider de paralelismo
        var slider = this.FindControl<Slider>("sldParallel"); // U2: nombre correcto segun AXAML
        var lblPar = this.FindControl<TextBlock>("txtParallelValue");
        if (slider != null)
        {
            slider.Value = _maxParallel;
            if (lblPar != null) lblPar.Text = _maxParallel.ToString();
            slider.PropertyChanged += (_, e) =>
            {
                if (e.Property.Name != "Value") return;
                if (e.NewValue is not double v) return;
                var n = Math.Clamp((int)v, 1, 8);
                _maxParallel = n;
                if (lblPar != null) lblPar.Text = n.ToString();
                // M3-FIX: solo intercambiar _transferSemaphore cuando NO hay transferencia activa.
                // Sin este guard, el slider puede disponer el semaforo mientras ParallelTransferFileAsync
                // lo retiene, causando ObjectDisposedException. Igual que Parallel_Changed en Features.cs.
                lock (_semLock)
                {
                    if (_isTransferring == 0)
                    {
                        var old = _transferSemaphore;
                        _transferSemaphore = new SemaphoreSlim(n, n);
                        old.Dispose();
                    }
                    // Si hay transferencia activa, _maxParallel ya fue actualizado para la proxima.
                }
            };
        }

        // El arranque nunca reanuda transferencias: primero deben completarse conexión y autorizaciones.
        RefreshFavoritesCombo();
        _ = ProcessLaunchArgsAsync();

        }
        catch (Exception ex) { Log.Warn("ui", "window-opened-unhandled", new { error = ex.Message }); }
    }

    // idea-sendto: si la app se abre con rutas de archivo (desde "Enviar a"), navegar a su
    // carpeta y pre-seleccionarlos para enviarlos tras conectar.
    private async Task ProcessLaunchArgsAsync()
    {
        string[] argv;
        try { argv = Environment.GetCommandLineArgs(); }
        catch (Exception ex)
        {
            Log.Warn("ui", "read-command-line-args-failed", new { error = ex.Message });
            argv = Array.Empty<string>();
        }
        var files = argv.Skip(1).Where(a => !string.IsNullOrWhiteSpace(a) && File.Exists(a)).ToList();
        if (files.Count == 0) { await RefreshLocalAsync(); return; }

        _localPath = Path.GetDirectoryName(files[0]) ?? _localPath;
        await RefreshLocalAsync();
        try
        {
            var list = this.FindControl<DataGrid>("localList");
            if (list != null)
            {
                var wanted = new HashSet<string>(files.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
                list.SelectedItems?.Clear();
                foreach (var it in _localItems.Where(x => wanted.Contains(x.FullPath)))
                    list.SelectedItems?.Add(it);
            }
        }
        catch (Exception ex) { Log.Warn("ui", "preselect-launch-files-failed", new { count = files.Count, error = ex.Message }); }
        SetStatus(L.Format("st.sendtoReady", files.Count));
    }

}

// ── Modelo: perfil de conexión (Feature 3) ────────────────────────────────────
internal record ConnectionProfile(string Name, string Ip, string Port, string Pin = "", bool Tls = false, bool Compress = false);
internal record PeerFolderState(string LocalPath = "", string RemotePath = "");
