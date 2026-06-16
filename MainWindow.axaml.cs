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
using LanCopy.Services;

namespace LanCopy;

public partial class MainWindow : Window
{
    private readonly FileServer _server = new();
    private static Loc L => Loc.Instance;
    private PeerDiscovery? _discovery; // Feature 12: UDP auto-descubrimiento
    private TrayIcon? _tray;            // idea-tray: icono de bandeja
    private bool _reallyExit;          // true cuando se cierra de verdad (no a bandeja)
    private LanClient? _client;        // upload client
    private LanClient? _clientDown;   // Feature 2: cliente separado para download simultáneo
    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private readonly SemaphoreSlim _clientLockDown = new(1, 1);

    private string _localPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private string _remotePath = "";

    private int _isUploading;    // Feature 2: bidireccional — separado de downloading
    private int _isDownloading;
    private int _isTransferring => (_isUploading | _isDownloading); // compat: 1 si cualquiera activo
    private CancellationTokenSource _uploadCts = new();
    private CancellationTokenSource _downloadCts = new();
    private CancellationTokenSource _transferCts { get => _isUploading == 1 ? _uploadCts : _downloadCts; set { } }
    private readonly SemaphoreSlim _pauseSemaphore = new(1, 1); // Feature 1: pausa
    private int _isPaused; // 0=no, 1=pausado

    // Feature 13: límite de transferencias paralelas (máx 4 simultáneos)
    private readonly SemaphoreSlim _transferSemaphore = new(4, 4);

    private readonly BulkObservableCollection<FileEntry> _localItems = new();
    private List<FileEntry> _localItemsAll = new(); // Feature 9: filtro
    private List<FileEntry> _remoteItemsAll = new(); // Feature 6: sort remoto
    private readonly BulkObservableCollection<FileEntry> _remoteItems = new();
    private readonly ObservableCollection<TransferRecord> _history = new();

    // Feature 3: perfiles de conexión
    private List<ConnectionProfile> _profiles = new();

    // Feature 6: ordenación de columnas
    private string _localSortField = "name";
    private bool _localSortAsc = true;
    private string _remoteSortField = "name";
    private bool _remoteSortAsc = true;

    // Feature 7: sparkline de velocidad (últimos 10 valores en MB/s)
    private readonly Queue<double> _speedHistory = new();
    private const int SparklineLen = 10;

    // Feature 8: watch folder
    private FileSystemWatcher? _watcher;
    private bool _watcherActive;
    private System.Timers.Timer? _watchDebounce;
    private readonly object _watchLock = new();

    // Filtro local: debounce para no re-ordenar la lista entera en cada tecla.
    private Avalonia.Threading.DispatcherTimer? _localFilterDebounce;

    // Feature 9: TLS + compresión toggles
    private bool _tlsEnabled = true; // SEGURIDAD: TLS activado por defecto (cifrado silencioso)
    private bool _advancedMode;      // UX: interfaz avanzada oculta por defecto
    private bool _welcomeShown;      // UX: true tras mostrar el asistente la primera vez
    private bool _restrictShareRoot = true; // SEGURIDAD: confina peers a carpeta compartida
    private bool _readOnly; // SEGURIDAD: si true, el servidor rechaza put/delete/rename
    private bool _requireApproval; // SEGURIDAD: pedir consentimiento antes de aceptar ficheros
    private bool _compressEnabled;
    private string _theme = "Dark"; // tema UI: Dark|Light

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LanCopy", "settings.json");

    private static readonly string QueuePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LanCopy", "queue.json"); // Feature 3: cola persistente

    private static readonly string HistoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LanCopy", "history.json"); // historial persistente entre sesiones

    // Cached brushes — evita SolidColorBrush.Parse en cada SetConnStatus (#11)
    private static readonly SolidColorBrush BrushConnected = SolidColorBrush.Parse("#28A745");
    private static readonly SolidColorBrush BrushError = SolidColorBrush.Parse("#FF6B6B");
    private static readonly SolidColorBrush BrushConnecting = SolidColorBrush.Parse("#FFD700");

    // Cached controls — evita FindControl<T>() en hot paths (#23)
    private TextBlock? _txtStatus;
    private TextBlock? _txtSpeed;
    private TextBlock? _txtLocalPath;
    private TextBlock? _txtRemotePath;
    private TextBlock? _txtConnStatus;
    private ProgressBar? _progressBar;
    private Expander? _historyExpander;
    private WindowNotificationManager? _notifManager; // Feature 7: toast
    private ProgressWindow? _progressWin;       // UX: ventana de progreso para procesos largos
    private DispatcherTimer? _statusBlinkTimer; // UX: parpadeo del mensaje de estado que requiere atencion
    private bool _statusBlinkOn;

    public MainWindow()
    {
        InitializeComponent();

        _txtStatus = this.FindControl<TextBlock>("txtStatus");
        _txtSpeed = this.FindControl<TextBlock>("txtSpeed");
        _txtLocalPath = this.FindControl<TextBlock>("txtLocalPath");
        _txtRemotePath = this.FindControl<TextBlock>("txtRemotePath");
        _txtConnStatus = this.FindControl<TextBlock>("txtConnStatus");
        _progressBar = this.FindControl<ProgressBar>("transferProgress");
        _historyExpander = this.FindControl<Expander>("historyExpander");
        _notifManager = new WindowNotificationManager(TopLevel.GetTopLevel(this)!)
        {
            Position = NotificationPosition.BottomRight,
            MaxItems = 3
        };

        this.FindControl<ListBox>("localList")!.ItemsSource = _localItems;
        this.FindControl<ListBox>("remoteList")!.ItemsSource = _remoteItems;
        this.FindControl<ListBox>("historyList")!.ItemsSource = _history;

        // Feature 6: drag & drop desde explorador de Windows al panel local
        var localList = this.FindControl<ListBox>("localList")!;
        DragDrop.SetAllowDrop(localList, true);
        localList.AddHandler(DragDrop.DropEvent, OnLocalDrop);
        localList.AddHandler(DragDrop.DragOverEvent, OnLocalDragOver);

        try
        {
            var savedLocalPort = ReadSavedLocalPort();
            _server.TlsEnabled = _tlsEnabled;
            _server.RestrictToShareRoot = _restrictShareRoot;
            _server.ReadOnly = _readOnly;
            _server.ApproveIncoming = _requireApproval ? OnApproveIncomingAsync : null;
            _server.Start(savedLocalPort);
            _server.TransferProgress += OnServerTransferProgress;
            _server.TextReceived += OnTextReceived;
            { var lpBox = this.FindControl<TextBox>("txtLocalPort"); if (lpBox != null) lpBox.Text = _server.Port.ToString(); }
            this.FindControl<TextBlock>("txtMyIp")!.Text = $"{_server.LocalIp}:{_server.Port}";
            SetStatus(L.Format("st.serverActive", $"{_server.LocalIp}:{_server.Port}"));

            // Feature 12: iniciar auto-descubrimiento UDP
            _discovery = new PeerDiscovery(_server.LocalIp, _server.Port);
            _discovery.PeersChanged += UpdatePeersCombo;
            _discovery.Start();

            SetupTray(); // idea-tray
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
        Closing += (_, ev) =>
        {
            // idea-tray: cerrar la ventana solo la oculta; el servidor sigue recibiendo en segundo plano.
            if (!_reallyExit && _tray != null)
            {
                ev.Cancel = true;
                Hide();
                SetStatus(L["st.minimizedToTray"]);
                return;
            }
            try { SaveSettings(this.FindControl<TextBox>("txtRemoteIp")?.Text ?? "", this.FindControl<TextBox>("txtRemotePort")?.Text ?? "8742"); } catch { }
            _server.Stop(); _discovery?.Stop(); _client?.Dispose(); _clientDown?.Dispose(); _uploadCts.Cancel(); _downloadCts.Cancel();
            try { if (_tray != null) _tray.IsVisible = false; } catch { }
        };
        Opened += OnWindowOpened;
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        // Deferir inicialización secundaria para que la ventana pinte primero.
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        await LoadSettingsAsync();

        // UX: primer uso -> mostrar asistente de bienvenida para usuarios sin red.
        if (!_welcomeShown)
        {
            _welcomeShown = true;
            try { await new WelcomeDialog().ShowDialog(this); } catch { }
            try { SaveSettings(this.FindControl<TextBox>("txtRemoteIp")?.Text ?? "", this.FindControl<TextBox>("txtRemotePort")?.Text ?? "8742"); } catch { }
        }

        // No bloquear primer render: refresco local y cola pendiente arrancan sin bloquear Opened.
        LoadHistory();
        _ = ProcessLaunchArgsAsync();
        _ = CheckPendingQueueAsync();
    }

    // idea-sendto: si la app se abre con rutas de archivo (desde "Enviar a"), navegar a su
    // carpeta y pre-seleccionarlos para enviarlos tras conectar.
    private async Task ProcessLaunchArgsAsync()
    {
        string[] argv;
        try { argv = Environment.GetCommandLineArgs(); } catch { argv = Array.Empty<string>(); }
        var files = argv.Skip(1).Where(a => !string.IsNullOrWhiteSpace(a) && File.Exists(a)).ToList();
        if (files.Count == 0) { await RefreshLocalAsync(); return; }

        _localPath = Path.GetDirectoryName(files[0]) ?? _localPath;
        await RefreshLocalAsync();
        try
        {
            var list = this.FindControl<ListBox>("localList");
            if (list != null)
            {
                var wanted = new HashSet<string>(files.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
                list.SelectedItems?.Clear();
                foreach (var it in _localItems.Where(x => wanted.Contains(x.FullPath)))
                    list.SelectedItems?.Add(it);
            }
        }
        catch { }
        SetStatus(L.Format("st.sendtoReady", files.Count));
    }

}

// ── Modelo: perfil de conexión (Feature 3) ────────────────────────────────────
internal record ConnectionProfile(string Name, string Ip, string Port, string Pin = "", bool Tls = false, bool Compress = false);
