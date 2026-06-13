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
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
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
    private static readonly char[] SparkChars = { '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };

    // Feature 8: watch folder
    private FileSystemWatcher? _watcher;
    private bool _watcherActive;
    private System.Timers.Timer? _watchDebounce;

    // Feature 9: TLS + compresión toggles
    private bool _tlsEnabled;
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
            _server.RestrictToShareRoot = _restrictShareRoot;
            _server.ReadOnly = _readOnly;
            _server.ApproveIncoming = _requireApproval ? OnApproveIncomingAsync : null;
            _server.Start(savedLocalPort);
            _server.TransferProgress += OnServerTransferProgress;
            { var lpBox = this.FindControl<TextBox>("txtLocalPort"); if (lpBox != null) lpBox.Text = _server.Port.ToString(); }
            this.FindControl<TextBlock>("txtMyIp")!.Text = $"{_server.LocalIp}:{_server.Port}";
            SetStatus(L.Format("st.serverActive", $"{_server.LocalIp}:{_server.Port}"));

            // Feature 12: iniciar auto-descubrimiento UDP
            _discovery = new PeerDiscovery(_server.LocalIp, _server.Port);
            _discovery.PeersChanged += UpdatePeersCombo;
            _discovery.Start();
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
        Closing += (_, _) =>
        {
            try { SaveSettings(this.FindControl<TextBox>("txtRemoteIp")?.Text ?? "", this.FindControl<TextBox>("txtRemotePort")?.Text ?? "8742"); } catch { }
            _server.Stop(); _discovery?.Stop(); _client?.Dispose(); _clientDown?.Dispose(); _uploadCts.Cancel(); _downloadCts.Cancel();
        };
        Opened += OnWindowOpened;
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        // Deferir inicialización secundaria para que la ventana pinte primero.
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        await LoadSettingsAsync();

        // No bloquear primer render: refresco local y cola pendiente arrancan sin bloquear Opened.
        LoadHistory();
        _ = RefreshLocalAsync();
        _ = CheckPendingQueueAsync();
    }

    // ── Connection ───────────────────────────────────────────────────────────

    private void Connect_Click(object? sender, RoutedEventArgs e) => TryConnect();

    private void TxtRemoteIp_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) TryConnect();
    }

    private void CopyIp_Click(object? sender, RoutedEventArgs e)
    {
        var full = this.FindControl<TextBlock>("txtMyIp")!.Text ?? "";
        var ip = full.Contains(':') ? full[..full.IndexOf(':')] : full;
        _ = TopLevel.GetTopLevel(this)!.Clipboard!.SetTextAsync(ip);
        SetStatus(L["st.ipCopied"]);
    }

    private async void CopyPairingCode_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var code = PairingCode.Encode(_server.LocalIp, _server.Port);
            var top = TopLevel.GetTopLevel(this);
            if (top?.Clipboard != null) await top.Clipboard.SetTextAsync(code);
            SetStatus(L.Format("st.codeCopied", code));
        }
        catch { SetStatus(L["st.codeError"]); }
    }

    private void ApplyTheme()
    {
        var variant = string.Equals(_theme, "Light", StringComparison.OrdinalIgnoreCase)
            ? ThemeVariant.Light : ThemeVariant.Dark;
        if (Application.Current != null) Application.Current.RequestedThemeVariant = variant;
        this.RequestedThemeVariant = variant;
    }

    private void ToggleTheme_Click(object? sender, RoutedEventArgs e)
    {
        _theme = string.Equals(_theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
        ApplyTheme();
        var ip = this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "";
        var port = this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742";
        SaveSettings(ip, port);
        SetStatus(L.Format("st.themeChanged", _theme));
    }

    private void TryConnect()
    {
        var ip = this.FindControl<TextBox>("txtRemoteIp")!.Text?.Trim() ?? "";
        var portStr = this.FindControl<TextBox>("txtRemotePort")!.Text?.Trim() ?? "8742";
        if (string.IsNullOrEmpty(ip)) { SetStatus(L["st.enterIp"]); return; }

        // Si el usuario pego un codigo de emparejamiento en el campo IP, decodificarlo.
        if (PairingCode.TryDecode(ip, out var pcIp, out var pcPort))
        {
            ip = pcIp;
            portStr = pcPort.ToString();
            this.FindControl<TextBox>("txtRemoteIp")!.Text = ip;
            this.FindControl<TextBox>("txtRemotePort")!.Text = portStr;
        }

        if (!int.TryParse(portStr, out var port) || port < 1 || port > 65535)
        {
            SetStatus(L["st.invalidPort"]); return;
        }
        SaveSettings(ip, portStr);
        _ = ConnectAsync(ip, port);
    }

    private async Task ConnectAsync(string ip, int port, bool silent = false)
    {
        if (!silent)
        {
            SetStatus(L.Format("st.connecting", $"{ip}:{port}"));
            SetConnStatus(L["conn.connecting"], BrushConnecting);
        }

        await _clientLock.WaitAsync();
        try { _client?.Dispose(); _client = MakeClient(ip, port); }
        finally { _clientLock.Release(); }

        try
        {
            var entries = await _client.ListAsync(_remotePath);
            Dispatcher.UIThread.Post(() => { _remoteItemsAll = entries; ApplyRemoteSort(); UpdateRemotePath(); });
            SetConnStatus(L["conn.connectedWord"], BrushConnected);
            if (!silent) SetStatus(L.Format("st.connected", $"{ip}:{port}"));
        }
        catch (Exception ex)
        {
            await _clientLock.WaitAsync();
            try { _client?.Dispose(); _client = null; }
            finally { _clientLock.Release(); }
            SetConnStatus(L["conn.error"], BrushError);
            if (!silent) SetStatus(L.Format("st.connectFailed", $"{ip}:{port}", ex.Message));
        }
    }

    // ── Reconnect ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Intenta reconectar hasta 3 veces con pausa de 2 s entre intentos.
    /// </summary>
    private async Task<bool> TryReconnectAsync(string ip, int port, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(ip) || port < 1) return false;
        SetConnStatus(L["conn.reconnecting"], BrushConnecting);

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            if (ct.IsCancellationRequested) return false;
            try
            {
                await _clientLock.WaitAsync(ct);
                try { _client?.Dispose(); _client = MakeClient(ip, port); }
                finally { _clientLock.Release(); }

                // Ping: verifica que la conexión funciona
                await _clientLock.WaitAsync(ct);
                var snap = _client;
                _clientLock.Release();
                _ = await snap!.ListAsync("");

                SetConnStatus(L["conn.reconnectedWord"], BrushConnected);
                SetStatus(L["st.reconnected"]);
                return true;
            }
            catch (OperationCanceledException) { return false; }
            catch
            {
                if (attempt < 3)
                {
                    SetStatus(L.Format("st.reconnecting", attempt));
                    try { await Task.Delay(2000, ct); } catch { return false; }
                }
            }
        }

        SetConnStatus(L["conn.disconnectedWord"], BrushError);
        SetStatus(L["st.reconnectFailed"]);
        return false;
    }

    // ── Local navigation ─────────────────────────────────────────────────────

    private async void LocalList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        var list = (ListBox)sender!;
        if (list.SelectedItem is FileEntry { IsDirectory: true } item)
        {
            _localPath = item.FullPath;
            await RefreshLocalAsync();
        }
    }

    private void LocalList_SelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        ShowSelectionStatus((ListBox)sender!);

    private async void LocalGoUp(object? sender, RoutedEventArgs e)
    {
        var parent = Directory.GetParent(_localPath)?.FullName;
        _localPath = parent ?? "";
        await RefreshLocalAsync();
    }

    private async void LocalGoHome(object? sender, RoutedEventArgs e)
    {
        _localPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        await RefreshLocalAsync();
    }

    private async void RefreshLocal_Click(object? sender, RoutedEventArgs e) =>
        await RefreshLocalAsync();

    private async Task RefreshLocalAsync()
    {
        try
        {
            var entries = await Task.Run(() => GetLocalEntries(_localPath));
            _localItemsAll = entries;
            ApplyLocalFilter(this.FindControl<TextBox>("txtLocalFilter")?.Text?.Trim() ?? "");
            Dispatcher.UIThread.Post(UpdateLocalPath);
        }
        catch (Exception ex) { SetStatus(L.Format("st.localError", ex.Message)); }
    }

    private void TxtLocalFilter_TextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyLocalFilter(((TextBox)sender!).Text?.Trim() ?? "");
    }

    private void ApplyLocalFilter(string filter)
    {
        var filtered = string.IsNullOrEmpty(filter)
            ? _localItemsAll
            : _localItemsAll.Where(f => f.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        var sorted = SortEntries(filtered, _localSortField, _localSortAsc).ToList();
        Dispatcher.UIThread.Post(() => _localItems.ReplaceAll(sorted));
    }

    private void ApplyRemoteSort()
    {
        var sorted = SortEntries(_remoteItemsAll, _remoteSortField, _remoteSortAsc).ToList();
        _remoteItems.ReplaceAll(sorted);
    }

    private static List<FileEntry> GetLocalEntries(string path)
    {
        var list = new List<FileEntry>();

        if (string.IsNullOrWhiteSpace(path))
        {
            foreach (var d in DriveInfo.GetDrives())
                list.Add(new FileEntry { Name = d.Name, FullPath = d.Name, IsDirectory = true });
            return list;
        }

        var parent = Directory.GetParent(path)?.FullName;
        if (parent != null)
            list.Add(new FileEntry { Name = "..", FullPath = parent, IsDirectory = true });

        try
        {
            var di = new DirectoryInfo(path);
            foreach (var d in di.GetDirectories().OrderBy(x => x.Name))
                list.Add(new FileEntry { Name = d.Name, FullPath = d.FullName, IsDirectory = true });
            foreach (var f in di.GetFiles().OrderBy(x => x.Name))
                list.Add(new FileEntry { Name = f.Name, FullPath = f.FullName, Size = f.Length });
        }
        catch { }

        return list;
    }

    // ── Remote navigation ────────────────────────────────────────────────────

    private async void RemoteList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_client == null) return;
        var list = (ListBox)sender!;
        if (list.SelectedItem is FileEntry { IsDirectory: true } item)
        {
            _remotePath = item.FullPath;
            await RefreshRemoteAsync();
        }
    }

    private void RemoteList_SelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        ShowSelectionStatus((ListBox)sender!);

    private async void RemoteGoUp(object? sender, RoutedEventArgs e)
    {
        if (_client == null) return;
        var parent = Path.GetDirectoryName(_remotePath.TrimEnd('\\', '/'));
        _remotePath = parent ?? "";
        await RefreshRemoteAsync();
    }

    private async void RemoteGoHome(object? sender, RoutedEventArgs e)
    {
        if (_client == null) return;
        _remotePath = "";
        await RefreshRemoteAsync();
    }

    private async void RefreshRemote_Click(object? sender, RoutedEventArgs e)
    {
        if (_client == null) { SetStatus(L["st.notConnected"]); return; }
        await RefreshRemoteAsync();
    }

    private async Task RefreshRemoteAsync(bool isRetry = false)
    {
        // No interrumpir transferencia activa (#13)
        if (_isTransferring == 1 && !isRetry) return;

        await _clientLock.WaitAsync();
        var snap = _client;
        _clientLock.Release();
        if (snap == null) return;

        try
        {
            var entries = await snap.ListAsync(_remotePath);
            Dispatcher.UIThread.Post(() => { _remoteItemsAll = entries; ApplyRemoteSort(); UpdateRemotePath(); });
        }
        catch (Exception ex)
        {
            if (!isRetry)
            {
                var ip = this.FindControl<TextBox>("txtRemoteIp")!.Text?.Trim() ?? "";
                var portStr = this.FindControl<TextBox>("txtRemotePort")!.Text?.Trim() ?? "8742";
                if (!string.IsNullOrEmpty(ip) && int.TryParse(portStr, out var port))
                {
                    try
                    {
                        await _clientLock.WaitAsync();
                        try { _client?.Dispose(); _client = MakeClient(ip, port); }
                        finally { _clientLock.Release(); }
                        await RefreshRemoteAsync(isRetry: true);
                        // Mostrar "Reconectado" tanto en status como en badge (#19)
                        SetStatus(L["st.reconnected"]);
                        SetConnStatus(L["conn.reconnectedWord"], BrushConnected);
                        return;
                    }
                    catch { }
                }
            }
            await _clientLock.WaitAsync();
            try { _client?.Dispose(); _client = null; }
            finally { _clientLock.Release(); }
            SetConnStatus(L["conn.disconnectedWord"], BrushError);
            SetStatus(L.Format("st.remoteError", ex.Message));
        }
    }

    // ── Transfer ─────────────────────────────────────────────────────────────

    private async void CopyToRemote(object? sender, RoutedEventArgs e)
    {
        if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
        var items = GetSelectedItems("localList");
        if (items.Count == 0) { SetStatus(L["st.selectFiles"]); return; }
        await TransferAsync(items, isUpload: true);
    }

    private async void CopyToLocal(object? sender, RoutedEventArgs e)
    {
        if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
        var items = GetSelectedItems("remoteList");
        if (items.Count == 0) { SetStatus(L["st.selectRemote"]); return; }
        await TransferAsync(items, isUpload: false);
    }

    private void CancelTransfer_Click(object? sender, RoutedEventArgs e)
    {
        if (_isUploading == 1) _uploadCts.Cancel();
        if (_isDownloading == 1) _downloadCts.Cancel();
    }

    private void PauseTransfer_Click(object? sender, RoutedEventArgs e)
    {
        if (Interlocked.CompareExchange(ref _isPaused, 1, 0) == 0)
        {
            _pauseSemaphore.Wait(0); // drain el semáforo → bloquear en siguiente WaitAsync
            Dispatcher.UIThread.Post(() =>
            {
                var bp = this.FindControl<Button>("btnPause");
                var br = this.FindControl<Button>("btnResume");
                if (bp != null) bp.IsEnabled = false;
                if (br != null) br.IsEnabled = true;
            });
            SetStatus(L["st.paused"]);
        }
    }

    private void ResumeTransfer_Click(object? sender, RoutedEventArgs e)
    {
        if (Interlocked.CompareExchange(ref _isPaused, 0, 1) == 1)
        {
            _pauseSemaphore.Release();
            Dispatcher.UIThread.Post(() =>
            {
                var bp = this.FindControl<Button>("btnPause");
                var br = this.FindControl<Button>("btnResume");
                if (bp != null) bp.IsEnabled = true;
                if (br != null) br.IsEnabled = false;
            });
            SetStatus(L["st.resumed"]);
        }
    }

    private List<FileEntry> GetSelectedItems(string listName) =>
        this.FindControl<ListBox>(listName)!
            .SelectedItems?.OfType<FileEntry>()
            .Where(f => f.Name != "..")
            .ToList() ?? [];

    private async Task TransferAsync(List<FileEntry> items, bool isUpload)
    {
        ref int isFlag = ref (isUpload ? ref _isUploading : ref _isDownloading);
        if (Interlocked.CompareExchange(ref isFlag, 1, 0) == 1) return;

        CancellationTokenSource cts;
        if (isUpload) { _uploadCts = new CancellationTokenSource(); cts = _uploadCts; }
        else { _downloadCts = new CancellationTokenSource(); cts = _downloadCts; }
        var ct = cts.Token;

        // Capturar IP/puerto en hilo UI antes de entrar en async
        var remoteIp = this.FindControl<TextBox>("txtRemoteIp")!.Text?.Trim() ?? "";
        var portStr = this.FindControl<TextBox>("txtRemotePort")!.Text?.Trim() ?? "8742";
        int.TryParse(portStr, out var remotePort);

        SetTransferButtonsEnabled(false, cancelEnabled: true);
        try
        {
            var fileList = await ExpandItemsAsync(items, isUpload, ct);
            if (fileList == null || ct.IsCancellationRequested) return;

            // Feature 3: guardar cola persistente al iniciar
            SaveQueue(fileList, isUpload, remoteIp, remotePort);

            var totalBytes = fileList.Sum(x => x.entry.Size);
            var arrow = isUpload ? ">>" : "<<";
            SetStatus(L.Format("st.filesTotal", $"{arrow} {fileList.Count}", fileList.Count > 1 ? L["word.files"] : L["word.file"], FileEntry.FormatSize(totalBytes)));

            var skipSet = await CheckOverwriteAsync(fileList, isUpload);
            if (skipSet == null) return;

            int ok = 0, skip = 0;
            var doneSync = new { Bytes = 0L };
            long totalTransfer = fileList
                .Where((_, i) => !skipSet.Contains(i))
                .Sum(x => x.entry.Size);

            var failedFiles = new System.Collections.Concurrent.ConcurrentBag<(FileEntry entry, string destPath)>();
            var lockDoneBytes = new object();
            long doneBytes = 0; // actualizada dentro de lockDoneBytes

            // -- Pase 1: transferencias paralelas (máx 4 simultáneas) --------
            var transferTasks = new List<Task>();
            for (int fi = 0; fi < fileList.Count; fi++)
            {
                if (ct.IsCancellationRequested) break;
                if (skipSet.Contains(fi)) { skip++; continue; }

                var (entry, destPath) = fileList[fi];
                var taskFi = fi; // capture para closure

                var task = ParallelTransferFileAsync(
                    entry, destPath, isUpload, taskFi, fileList.Count,
                    totalTransfer, arrow, failedFiles, lockDoneBytes, remoteIp, remotePort, ct);
                transferTasks.Add(task);
            }

            // Esperar a que todas las transferencias terminen (max 4 simultáneas)
            if (transferTasks.Count > 0)
                await Task.WhenAll(transferTasks);

            // Contar OK y actualizar doneBytes final
            lock (lockDoneBytes)
                doneBytes = totalTransfer - failedFiles.Sum(x => (long)x.entry.Size);
            ok = fileList.Count - skip - failedFiles.Count;

            // -- Pase 2: reintentar archivos fallidos -------------------------
            if (failedFiles.Count > 0 && !ct.IsCancellationRequested)
            {
                SetStatus(L.Format("st.retrying", failedFiles.Count));
                try { await Task.Delay(2000, ct); } catch { goto cleanup; }

                bool reconnected2 = await TryReconnectAsync(remoteIp, remotePort, ct);
                if (!reconnected2) goto cleanup;

                var retryBag = new System.Collections.Concurrent.ConcurrentBag<(FileEntry entry, string destPath)>();
                var retryTasks = new List<Task>();
                int ri = 0;
                foreach (var (entry, destPath) in failedFiles)
                {
                    if (ct.IsCancellationRequested) break;

                    var taskRi = ri++;
                    var task = ParallelTransferFileAsync(
                        entry, destPath, isUpload, taskRi, failedFiles.Count,
                        totalTransfer, $"↺{arrow}", retryBag, lockDoneBytes, remoteIp, remotePort, ct);
                    retryTasks.Add(task);
                }

                if (retryTasks.Count > 0)
                    await Task.WhenAll(retryTasks);

                // Re-contar qué pasó
                ok += failedFiles.Count - retryBag.Count;
                failedFiles.Clear();
                foreach (var f in retryBag) failedFiles.Add(f);

                if (failedFiles.Count > 0)
                {
                    var names = string.Join(", ", failedFiles.Select(x => x.entry.Name).Take(3));
                    var extra = failedFiles.Count > 3 ? $" +{failedFiles.Count - 3}" : "";
                    AddHistory($"Sin completar ({failedFiles.Count}): {names}{extra}", "#FF6B6B");
                }
                else
                {
                    AddHistory($"↺ Reintento OK: {failedFiles.Count} archivo(s)", "#FFD700");
                }

                lock (lockDoneBytes)
                    doneBytes = totalTransfer - failedFiles.Sum(x => (long)x.entry.Size);
            }

        cleanup:
            Dispatcher.UIThread.Post(() =>
            {
                if (_progressBar != null) _progressBar.Value = 0;
                if (_txtSpeed != null) _txtSpeed.Text = "";
                UpdateSparkline(0);
            });

            var msg = skip > 0
                ? $"{ok} copiados, {skip} omitidos de {fileList.Count}"
                : $"{ok} / {fileList.Count} archivos  ({FileEntry.FormatSize(doneBytes)})";
            SetStatus(msg);
            if (ok > 0) AddHistory($"{arrow} {ok} archivo{(ok > 1 ? "s" : "")} ({FileEntry.FormatSize(doneBytes)})", "#28A745");

            if (isUpload) await RefreshRemoteAsync();
            else await RefreshLocalAsync();
        }
        finally
        {
            // Resetear pausa por si cancelaron mientras pausado
            if (Interlocked.Exchange(ref _isPaused, 0) == 1)
                try { _pauseSemaphore.Release(); } catch { }
            SetTransferButtonsEnabled(true, cancelEnabled: false);
            ref int flagEnd = ref (isUpload ? ref _isUploading : ref _isDownloading);
            Interlocked.Exchange(ref flagEnd, 0);
            ClearQueue(); // Feature 3: transferencia completada (o cancelada)
        }
    }

    /// <summary>
    /// Transfiere un único archivo. Devuelve true si éxito, false si error o cancelado.
    /// </summary>
    // Progreso del lado servidor (cuando el OTRO PC inicia la copia): recepcion 'put' / envio 'get'.
    private long _srvLastDone;
    private readonly Stopwatch _srvSw = new();

    private void OnServerTransferProgress(FileServer.TransferProgressInfo info)
    {
        if (info.Done <= 0 || info.Done < _srvLastDone) { _srvSw.Restart(); _srvLastDone = 0; }
        var elapsed = _srvSw.Elapsed.TotalSeconds;
        var speed = elapsed > 0.01 ? (info.Done - _srvLastDone) / elapsed : 0;
        _srvLastDone = info.Done;
        _srvSw.Restart();

        var pct = info.Total > 0 ? info.Done * 100.0 / info.Total : 0.0;
        var arrow = info.Receiving ? "\u2B07" : "\u2B06";
        var verb = info.Receiving ? L["verb.receiving"] : L["verb.sending"];

        Dispatcher.UIThread.Post(() =>
        {
            if (_progressBar != null) _progressBar.Value = pct;
            if (_txtSpeed != null) _txtSpeed.Text = $"{FileEntry.FormatSize((long)speed)}/s";
            UpdateSparkline(speed);
        });
        SetStatus($"{arrow} {verb} {info.FileName}  " +
                  $"{FileEntry.FormatSize(info.Done)}/{FileEntry.FormatSize(info.Total)}  " +
                  $"@ {FileEntry.FormatSize((long)speed)}/s");
    }
    private async Task<bool> DoTransferFileAsync(
        FileEntry entry, string destPath, bool isUpload,
        int fi, int total, long fileStart, long totalTransfer,
        string arrow, CancellationToken ct)
    {
        var lastDone = 0L;
        var sw = Stopwatch.StartNew();

        var prog = new Progress<(long done, long total)>(v =>
        {
            var elapsed = sw.Elapsed.TotalSeconds;
            var speed = elapsed > 0.01 ? (v.done - lastDone) / elapsed : 0;
            lastDone = v.done;
            sw.Restart();

            var pct = totalTransfer > 0 ? (fileStart + v.done) * 100.0 / totalTransfer : 0.0;
            Dispatcher.UIThread.Post(() =>
            {
                if (_progressBar != null) _progressBar.Value = pct;
                if (_txtSpeed != null) _txtSpeed.Text = $"{FileEntry.FormatSize((long)speed)}/s";
                UpdateSparkline(speed);
            });
            SetStatus($"{arrow} [{fi + 1}/{total}] {entry.Name}  " +
                      $"{FileEntry.FormatSize(v.done)}/{FileEntry.FormatSize(v.total)}  " +
                      $"@ {FileEntry.FormatSize((long)speed)}/s");
        });

        var throttled = new ThrottledProgress<(long, long)>(prog, intervalMs: 200);

        // Feature 2: upload usa _client/_clientLock, download usa _clientDown/_clientLockDown
        var clientLock = isUpload ? _clientLock : _clientLockDown;

        try
        {
            await clientLock.WaitAsync(ct);
            LanClient? snap;
            if (isUpload)
            {
                if (_client == null) { clientLock.Release(); return false; }
                snap = _client;
            }
            else
            {
                if (_clientDown == null)
                {
                    // Crear cliente download si no existe (primera vez o reconexión)
                    var remoteIp2 = this.FindControl<TextBox>("txtRemoteIp")!.Text?.Trim() ?? "";
                    var portStr2 = this.FindControl<TextBox>("txtRemotePort")!.Text?.Trim() ?? "8742";
                    int.TryParse(portStr2, out var remotePort2);
                    _clientDown = new LanClient(remoteIp2, remotePort2);
                }
                snap = _clientDown;
            }
            clientLock.Release();

            if (isUpload)
                await snap.UploadAsync(entry.FullPath, destPath, throttled, ct);
            else
                await snap.DownloadAsync(entry.FullPath, destPath, throttled, ct);

            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LanCopy] {entry.Name}: {ex.Message}");
            // Invalidar cliente para forzar reconexión
            if (isUpload) { await _clientLock.WaitAsync(ct); _client?.Dispose(); _client = null; _clientLock.Release(); }
            else { await _clientLockDown.WaitAsync(ct); _clientDown?.Dispose(); _clientDown = null; _clientLockDown.Release(); }
            return false;
        }
    }

    // Feature 13: transferencia paralela con límite de 4 simultáneas
    private async Task ParallelTransferFileAsync(
        FileEntry entry, string destPath, bool isUpload,
        int fi, int total, long totalTransfer, string arrow,
        System.Collections.Concurrent.ConcurrentBag<(FileEntry, string)> failedBag,
        object lockDoneBytes,
        string remoteIp, int remotePort, CancellationToken ct)
    {
        // Feature 1: punto de pausa
        await _pauseSemaphore.WaitAsync(ct);
        if (!ct.IsCancellationRequested) _pauseSemaphore.Release();
        if (ct.IsCancellationRequested) return;

        // Limitar a máx 4 simultáneas
        await _transferSemaphore.WaitAsync(ct);
        try
        {
            long fileStart = 0L;
            lock (lockDoneBytes)
            {
                // Calcular doneBytes actual desde totalTransfer - failedBag
                var failed = failedBag.Sum(x => (long)x.Item1.Size);
                fileStart = totalTransfer - failed;
            }

            bool fileOk = await DoTransferFileAsync(
                entry, destPath, isUpload, fi, total, fileStart, totalTransfer, arrow, ct);

            if (!fileOk && !ct.IsCancellationRequested)
            {
                SetStatus(L.Format("st.errorReconnecting", entry.Name));
                bool reconnected = await TryReconnectAsync(remoteIp, remotePort, ct);
                if (reconnected)
                    fileOk = await DoTransferFileAsync(
                        entry, destPath, isUpload, fi, total, fileStart, totalTransfer, arrow, ct);
            }

            if (fileOk)
            {
                lock (lockDoneBytes)
                {
                    var failed = failedBag.Sum(x => (long)x.Item1.Size);
                    var doneNow = totalTransfer - failed;
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_progressBar != null)
                            _progressBar.Value = totalTransfer > 0 ? doneNow * 100.0 / totalTransfer : 100;
                    });
                }
            }
            else if (!ct.IsCancellationRequested)
            {
                failedBag.Add((entry, destPath));
            }
        }
        finally
        {
            _transferSemaphore.Release();
        }
    }

    // Throttle Report() en el hilo de background — evita flood del dispatcher (#9)
    private sealed class ThrottledProgress<T> : IProgress<T>
    {
        private readonly IProgress<T> _inner;
        private readonly long _intervalMs;
        private long _lastMs;

        public ThrottledProgress(IProgress<T> inner, int intervalMs = 200)
        {
            _inner = inner;
            _intervalMs = intervalMs;
        }

        public void Report(T value)
        {
            var now = Environment.TickCount64;
            if (now - Interlocked.Read(ref _lastMs) < _intervalMs) return;
            Interlocked.Exchange(ref _lastMs, now);
            _inner.Report(value);
        }
    }

    private async Task<List<(FileEntry entry, string destPath)>?> ExpandItemsAsync(
        List<FileEntry> items, bool isUpload, CancellationToken ct)
    {
        var result = new List<(FileEntry, string)>();
        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) return null;

            if (!item.IsDirectory)
            {
                var dest = isUpload
                    ? Path.Combine(_remotePath, item.Name)
                    : Path.Combine(_localPath, item.Name);
                result.Add((item, dest));
            }
            else
            {
                if (isUpload)
                {
                    try
                    {
                        foreach (var f in Directory.EnumerateFiles(item.FullPath, "*", SearchOption.AllDirectories))
                        {
                            var rel = Path.GetRelativePath(item.FullPath, f);
                            var dest = Path.Combine(_remotePath, item.Name, rel);
                            var fii = new FileInfo(f);
                            // Tag="dir" → saltar overwrite check (subcarpetas remotas desconocidas) (#3)
                            result.Add((new FileEntry { Name = fii.Name, FullPath = f, Size = fii.Length, Tag = "dir" }, dest));
                        }
                    }
                    catch { }
                }
                else
                {
                    try
                    {
                        await _clientLock.WaitAsync(ct);
                        var snap = _client;
                        _clientLock.Release();
                        if (snap == null) continue;

                        var remoteFiles = await snap.ListRecursiveAsync(item.FullPath, ct);
                        foreach (var rf in remoteFiles)
                        {
                            var dest = Path.Combine(_localPath, item.Name, rf.Name);
                            result.Add((new FileEntry { Name = rf.Name, FullPath = rf.FullPath, Size = rf.Size, Tag = "dir" }, dest));
                        }
                    }
                    catch (Exception ex) { SetStatus(L.Format("st.errorListing", item.Name, ex.Message)); }
                }
            }
        }
        return result;
    }

    private async Task<HashSet<int>?> CheckOverwriteAsync(
        List<(FileEntry entry, string destPath)> files, bool isUpload)
    {
        var conflicts = new List<int>();
        for (int i = 0; i < files.Count; i++)
        {
            var (entry, dest) = files[i];
            // Items dentro de carpetas expandidas: Tag="dir" → no se puede detectar conflicto (#3)
            if (entry.Tag == "dir") continue;

            bool exists = isUpload
                ? _remoteItems.Any(r => !r.IsDirectory && string.Equals(r.FullPath, dest, StringComparison.OrdinalIgnoreCase))
                : File.Exists(dest);
            if (exists) conflicts.Add(i);
        }

        if (conflicts.Count == 0) return [];

        var dlg = new ConfirmDialog(conflicts.Count, files[conflicts[0]].entry.Name);
        await dlg.ShowDialog(this); // await correcto: espera cierre del dialogo (#1)
        var action = await dlg.GetResultAsync();

        if (action == ConfirmDialog.OverwriteAction.Rename)
        {
            // Renombrar destino con sufijo (2), (3)... para archivos en conflicto
            foreach (var i in conflicts)
            {
                var (entry, dest) = files[i];
                var renamed = MakeUniqueDest(dest);
                files[i] = (entry, renamed);
            }
            return [];
        }

        return action switch
        {
            ConfirmDialog.OverwriteAction.OverwriteAll => [],
            ConfirmDialog.OverwriteAction.SkipAll => [.. conflicts],
            _ => null
        };
    }

    private static string MakeUniqueDest(string dest)
    {
        if (!File.Exists(dest)) return dest;
        var dir = Path.GetDirectoryName(dest) ?? "";
        var name = Path.GetFileNameWithoutExtension(dest);
        var ext = Path.GetExtension(dest);
        for (int n = 2; n < 1000; n++)
        {
            var candidate = Path.Combine(dir, $"{name} ({n}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
        return dest;
    }

    private void AddHistory(string text, string color)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _history.Insert(0, new TransferRecord
            {
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Text = text,
                Color = color
            });
            while (_history.Count > 50) _history.RemoveAt(_history.Count - 1);

            // Auto-expand historial al primer item (#21)
            if (_historyExpander != null && !_historyExpander.IsExpanded)
                _historyExpander.IsExpanded = true;

            // Feature 7: toast notificación al completar
            if (_notifManager != null && color == "#28A745")
                _notifManager.Show(new Notification("LanCopy", text, NotificationType.Success));

            SaveHistory();
        });
    }

    private void SaveHistory()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath)!);
            var snapshot = _history.Take(50).ToList();
            File.WriteAllText(HistoryPath, JsonSerializer.Serialize(snapshot));
        }
        catch { /* historial es best-effort */ }
    }

    private void LoadHistory()
    {
        try
        {
            if (!File.Exists(HistoryPath)) return;
            var items = JsonSerializer.Deserialize<List<TransferRecord>>(File.ReadAllText(HistoryPath));
            if (items == null) return;
            Dispatcher.UIThread.Post(() =>
            {
                _history.Clear();
                foreach (var it in items.Take(50)) _history.Add(it);
                if (_historyExpander != null && _history.Count > 0)
                    _historyExpander.IsExpanded = true;
            });
        }
        catch { /* historial corrupto: ignorar */ }
    }

    // ── Settings ─────────────────────────────────────────────────────────────

    private async Task LoadSettingsAsync()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var json = await File.ReadAllTextAsync(SettingsPath);
            var doc = JsonSerializer.Deserialize<JsonElement>(json);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (doc.TryGetProperty("remoteIp", out var ip)) this.FindControl<TextBox>("txtRemoteIp")!.Text = ip.GetString();
                if (doc.TryGetProperty("remotePort", out var port)) this.FindControl<TextBox>("txtRemotePort")!.Text = port.GetString();
                if (doc.TryGetProperty("localPort", out var lport))
                {
                    var lpTxt = lport.ValueKind == JsonValueKind.Number ? lport.GetInt32().ToString() : lport.GetString();
                    var lpc = this.FindControl<TextBox>("txtLocalPort"); if (lpc != null && !string.IsNullOrEmpty(lpTxt)) lpc.Text = lpTxt;
                }
            });
            if (doc.TryGetProperty("pin", out var pin))
            {
                var pinVal = pin.GetString() ?? "";
                await Dispatcher.UIThread.InvokeAsync(() => this.FindControl<TextBox>("txtPin")!.Text = pinVal);
                _server.RequiredPin = string.IsNullOrEmpty(pinVal) ? null : pinVal;
            }
            // Feature 9: TLS
            if (doc.TryGetProperty("tlsEnabled", out var tls))
            {
                _tlsEnabled = tls.GetBoolean();
                _server.TlsEnabled = _tlsEnabled;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var chk = this.FindControl<CheckBox>("chkTls");
                    if (chk != null) chk.IsChecked = _tlsEnabled;
                });
            }
            // SEGURIDAD: confinamiento a carpeta compartida
            if (doc.TryGetProperty("restrictShareRoot", out var rsr))
            {
                _restrictShareRoot = rsr.GetBoolean();
                _server.RestrictToShareRoot = _restrictShareRoot;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var chk = this.FindControl<CheckBox>("chkShareRoot");
                    if (chk != null) chk.IsChecked = _restrictShareRoot;
                });
            }
            // SEGURIDAD: modo solo lectura
            if (doc.TryGetProperty("readOnly", out var roEl))
            {
                _readOnly = roEl.GetBoolean();
                _server.ReadOnly = _readOnly;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var chk = this.FindControl<CheckBox>("chkReadOnly");
                    if (chk != null) chk.IsChecked = _readOnly;
                });
            }
            // SEGURIDAD: consentimiento del receptor
            if (doc.TryGetProperty("requireApproval", out var raEl))
            {
                _requireApproval = raEl.GetBoolean();
                _server.ApproveIncoming = _requireApproval ? OnApproveIncomingAsync : null;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var chk = this.FindControl<CheckBox>("chkRequireApproval");
                    if (chk != null) chk.IsChecked = _requireApproval;
                });
            }
            // Feature 2: compresión
            if (doc.TryGetProperty("compressEnabled", out var comp))
            {
                _compressEnabled = comp.GetBoolean();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var chk = this.FindControl<CheckBox>("chkCompress");
                    if (chk != null) chk.IsChecked = _compressEnabled;
                });
            }
            // Tema UI
            if (doc.TryGetProperty("theme", out var themeEl))
            {
                _theme = themeEl.GetString() ?? "Dark";
                await Dispatcher.UIThread.InvokeAsync(ApplyTheme);
            }
            // Geometria de ventana persistente
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    if (doc.TryGetProperty("winW", out var wEl) && doc.TryGetProperty("winH", out var hEl))
                    {
                        var w = wEl.GetDouble(); var h = hEl.GetDouble();
                        if (w >= MinWidth && h >= MinHeight && w < 10000 && h < 10000) { Width = w; Height = h; }
                    }
                    if (doc.TryGetProperty("winX", out var xEl) && doc.TryGetProperty("winY", out var yEl))
                    {
                        var x = xEl.GetInt32(); var y = yEl.GetInt32();
                        if (x > -32000 && y > -32000 && x < 32000 && y < 32000) Position = new PixelPoint(x, y);
                    }
                    if (doc.TryGetProperty("winMax", out var mEl) && mEl.GetBoolean())
                        WindowState = WindowState.Maximized;
                }
                catch { }
            });
            // Feature 3: perfiles
            if (doc.TryGetProperty("profiles", out var profilesEl))
            {
                _profiles = JsonSerializer.Deserialize<List<ConnectionProfile>>(profilesEl.GetRawText()) ?? new();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Al iniciar, selecciona el perfil que coincida con la conexion restaurada.
                    var curIp = this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "";
                    var curPort = this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "";
                    var match = _profiles.FirstOrDefault(p => p.Ip == curIp && p.Port == curPort)?.Name;
                    RefreshProfilesCombo(match);
                });
            }
        }
        catch { }
    }

    private void SaveSettings(string ip, string port)
    {
        try
        {
            var pin = this.FindControl<TextBox>("txtPin")?.Text?.Trim() ?? "";
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new
            {
                remoteIp = ip,
                remotePort = port,
                localPort = this.FindControl<TextBox>("txtLocalPort")?.Text?.Trim() ?? "8742",
                pin,
                tlsEnabled = _tlsEnabled,
                restrictShareRoot = _restrictShareRoot,
                readOnly = _readOnly,
                requireApproval = _requireApproval,
                compressEnabled = _compressEnabled,
                language = L.Current,
                theme = _theme,
                winW = this.Width,
                winH = this.Height,
                winX = this.Position.X,
                winY = this.Position.Y,
                winMax = this.WindowState == WindowState.Maximized,
                profiles = _profiles
            }));
        }
        catch { }
    }

    private void CmbLang_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox cmb) return;
        var idx = cmb.SelectedIndex;
        if (idx < 0 || idx >= Loc.Available.Count) return;
        var code = Loc.Available[idx].Code;
        if (code == L.Current) return;
        L.SetLanguage(code);
        this.FlowDirection = L.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }
    private void TxtPin_TextChanged(object? sender, TextChangedEventArgs e)
    {
        var pin = ((TextBox)sender!).Text?.Trim() ?? "";
        _server.RequiredPin = string.IsNullOrEmpty(pin) ? null : pin;
    }

    private LanClient MakeClient(string ip, int port)
    {
        var pin = this.FindControl<TextBox>("txtPin")?.Text?.Trim() ?? "";
        return new LanClient(ip, port)
        {
            Pin = string.IsNullOrEmpty(pin) ? null : pin,
            UseTls = _tlsEnabled,
            UseCompress = _compressEnabled
        };
    }

    // ── Cola persistente (Feature 3) ─────────────────────────────────────────

    private void SaveQueue(List<(FileEntry entry, string destPath)> files, bool isUpload, string ip, int port)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(QueuePath)!);
            var item = new Models.QueueItem(
                files.Select(f => f.entry.FullPath).ToArray(),
                files.Select(f => f.destPath).ToArray(),
                isUpload, ip, port);
            File.WriteAllText(QueuePath, System.Text.Json.JsonSerializer.Serialize(item));
        }
        catch { /* no bloquear transferencia por error de cola */ }
    }

    private static void ClearQueue()
    {
        try { if (File.Exists(QueuePath)) File.Delete(QueuePath); } catch { }
    }

    private async Task CheckPendingQueueAsync()
    {
        if (!File.Exists(QueuePath)) return;
        try
        {
            var json = await File.ReadAllTextAsync(QueuePath);
            var item = System.Text.Json.JsonSerializer.Deserialize<Models.QueueItem>(json);
            if (item == null || item.FilePaths.Length == 0) { ClearQueue(); return; }

            var result = await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dlg = new Avalonia.Controls.Window
                {
                    Title = "Cola pendiente",
                    Width = 440,
                    Height = 160,
                    Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2D2D30")),
                    WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner
                };
                var msg = new TextBlock
                {
                    Text = $"Hay {item.FilePaths.Length} archivo(s) en cola de la sesión anterior ({(item.IsUpload ? "subida" : "bajada")} a {item.RemoteIp}:{item.RemotePort}).\n¿Reanudar la transferencia?",
                    Foreground = Avalonia.Media.Brushes.White,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Avalonia.Thickness(16, 16, 16, 8)
                };
                var resume = new Avalonia.Controls.Button { Content = "Reanudar", Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#28A745")), Foreground = Avalonia.Media.Brushes.White, Padding = new Avalonia.Thickness(16, 6), Margin = new Avalonia.Thickness(8, 0) };
                var discard = new Avalonia.Controls.Button { Content = "Descartar", Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#6C757D")), Foreground = Avalonia.Media.Brushes.White, Padding = new Avalonia.Thickness(16, 6) };
                bool ok = false;
                resume.Click += (_, _) => { ok = true; dlg.Close(); };
                discard.Click += (_, _) => { ok = false; dlg.Close(); };
                var row = new Avalonia.Controls.StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Children = { resume, discard } };
                dlg.Content = new Avalonia.Controls.StackPanel { Children = { msg, row } };
                await dlg.ShowDialog(this);
                return ok;
            });

            if (!result) { ClearQueue(); return; }

            // Restaurar IP/puerto y lanzar transferencia
            this.FindControl<TextBox>("txtRemoteIp")!.Text = item.RemoteIp;
            this.FindControl<TextBox>("txtRemotePort")!.Text = item.RemotePort.ToString();
            await ConnectAsync(item.RemoteIp, item.RemotePort);

            var entries = item.FilePaths.Select((p, i) =>
                new FileEntry { Name = Path.GetFileName(p), FullPath = p }).ToList();
            _ = TransferAsync(entries, item.IsUpload);
        }
        catch { ClearQueue(); }
    }

    // ── UDP Peer Discovery (Feature 12) ──────────────────────────────────────

    private static int ReadSavedLocalPort()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return 8742;
            var doc = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(SettingsPath));
            if (doc.TryGetProperty("localPort", out var lp))
            {
                if (lp.ValueKind == JsonValueKind.Number && lp.TryGetInt32(out var n) && n is >= 1 and <= 65535) return n;
                if (lp.ValueKind == JsonValueKind.String && int.TryParse(lp.GetString(), out var s) && s is >= 1 and <= 65535) return s;
            }
        }
        catch { }
        return 8742;
    }

    private void TxtLocalPort_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) ApplyLocalPort();
    }

    private void ApplyLocalPort_Click(object? sender, RoutedEventArgs e) => ApplyLocalPort();

    // Reinicia el servidor (y el descubrimiento) en el nuevo puerto local indicado por el usuario.
    private void ApplyLocalPort()
    {
        var box = this.FindControl<TextBox>("txtLocalPort");
        if (box == null) return;
        if (!int.TryParse(box.Text?.Trim(), out var port) || port < 1 || port > 65535)
        {
            SetStatus(L["st.localPortInvalid"]);
            box.Text = _server.Port.ToString();
            return;
        }
        if (port == _server.Port) { SetStatus(L.Format("st.alreadyListening", port)); return; }

        try
        {
            _discovery?.Stop();
            _server.TransferProgress -= OnServerTransferProgress;
            _server.Stop();
            _server.Start(port);
            _server.TransferProgress += OnServerTransferProgress;
            this.FindControl<TextBlock>("txtMyIp")!.Text = $"{_server.LocalIp}:{_server.Port}";

            _discovery = new PeerDiscovery(_server.LocalIp, _server.Port);
            _discovery.PeersChanged += UpdatePeersCombo;
            _discovery.Start();

            SetStatus(L.Format("st.serverRestarted", $"{_server.LocalIp}:{_server.Port}"));
            SaveSettings(
                this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "",
                this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742");
        }
        catch (Exception ex)
        {
            SetStatus(L.Format("st.portChangeFailed", ex.Message));
        }
    }
    private void UpdatePeersCombo()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var combo = this.FindControl<ComboBox>("cmbPeers");
            if (combo == null) return;
            var peers = _discovery?.GetPeers() ?? [];
            combo.ItemsSource = peers.Select(p => $"{p.Name} ({p.Ip}:{p.Port})").ToList();
        });
    }

    private void CmbPeers_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var combo = (ComboBox)sender!;
        if (combo.SelectedItem is not string item) return;
        var peers = _discovery?.GetPeers() ?? [];
        var selected = peers.FirstOrDefault(p => item.Contains(p.Ip));
        if (selected == null) return;
        this.FindControl<TextBox>("txtRemoteIp")!.Text = selected.Ip;
        this.FindControl<TextBox>("txtRemotePort")!.Text = selected.Port.ToString();
    }

    // ── Drag & Drop (Feature 6) ───────────────────────────────────────────

    private void OnLocalDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Copy; // Accept all drops; filter in OnLocalDrop
        e.Handled = true;
    }

    private async void OnLocalDrop(object? sender, DragEventArgs e)
    {
        IEnumerable<IStorageItem>? files = null;
        try
        {
            if (e.DataTransfer is IAsyncDataTransfer asyncTransfer)
                files = await asyncTransfer.TryGetFilesAsync();
        }
        catch { }
        var fileList = files?.ToList();
        if (fileList == null || fileList.Count == 0) return;

        // Si hay directorios, navegar al primero; de lo contrario iniciar upload
        var dirs = fileList.OfType<IStorageFolder>().ToList();
        if (dirs.Count > 0 && fileList.Count == 1)
        {
            var path = dirs[0].TryGetLocalPath();
            if (path != null) { _localPath = path; await RefreshLocalAsync(); return; }
        }

        // Convertir a FileEntry y enviar al remoto si hay cliente
        if (_client == null) { SetStatus(L["st.connectBeforeDrag"]); return; }
        var entries = fileList
            .Select(f => f.TryGetLocalPath())
            .Where(p => p != null && System.IO.File.Exists(p))
            .Select(p => { var fi = new FileInfo(p!); return new FileEntry { Name = fi.Name, FullPath = fi.FullName, Size = fi.Length }; })
            .ToList();
        if (entries.Count > 0)
            await TransferAsync(entries, isUpload: true);
    }

    // ── UI helpers ───────────────────────────────────────────────────────────

    private void ShowSelectionStatus(ListBox list)
    {
        var selected = list.SelectedItems?.OfType<FileEntry>()
                           .Where(f => f.Name != "..").ToList() ?? [];
        if (selected.Count == 0) return;

        if (selected.Count == 1 && selected[0].IsDirectory)
        {
            SetStatus(L.Format("st.folderDoubleClick", selected[0].Name));
            return;
        }
        var files = selected.Where(f => !f.IsDirectory).ToList();
        var dirs = selected.Where(f => f.IsDirectory).ToList();
        var parts = new List<string>();
        if (files.Count > 0) parts.Add($"{files.Count} archivo{(files.Count > 1 ? "s" : "")} ({FileEntry.FormatSize(files.Sum(f => f.Size))})");
        if (dirs.Count > 0) parts.Add($"{dirs.Count} carpeta{(dirs.Count > 1 ? "s" : "")}");
        SetStatus(L.Format("st.selected", string.Join(", ", parts)));
    }

    private void UpdateLocalPath()
    {
        if (_txtLocalPath != null)
            _txtLocalPath.Text = string.IsNullOrEmpty(_localPath) ? "(unidades)" : _localPath;
    }

    private void UpdateRemotePath()
    {
        if (_txtRemotePath != null)
            _txtRemotePath.Text = string.IsNullOrEmpty(_remotePath) ? "(unidades)" : _remotePath;
    }

    private void SetStatus(string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_txtStatus != null) _txtStatus.Text = text;
        });
    }

    private void SetConnStatus(string text, SolidColorBrush brush)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_txtConnStatus == null) return;
            _txtConnStatus.Text = text;
            _txtConnStatus.Foreground = brush;
        });
    }

    private void SetTransferButtonsEnabled(bool enabled, bool cancelEnabled)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Con bidireccional: send/receive habilitados solo si el contrario inactivo
            var sendEnabled = enabled && _isDownloading == 0;
            var receiveEnabled = enabled && _isUploading == 0;
            var anyCancelable = cancelEnabled || _isUploading == 1 || _isDownloading == 1;

            var send = this.FindControl<Button>("btnSend");
            var receive = this.FindControl<Button>("btnReceive");
            var cancel = this.FindControl<Button>("btnCancel");
            var pause = this.FindControl<Button>("btnPause");
            var resume = this.FindControl<Button>("btnResume");
            if (send != null) send.IsEnabled = sendEnabled;
            if (receive != null) receive.IsEnabled = receiveEnabled;
            if (cancel != null) cancel.IsEnabled = anyCancelable;
            if (pause != null) pause.IsEnabled = cancelEnabled;
            if (resume != null) resume.IsEnabled = false;

            // ══ Context menus — Local ═════════════════════════════════════════════════════
        });
    }

    // ══ Context menus — Local ═════════════════════════════════════════════════════

    private void LocalCtx_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var items = GetSelectedItems("localList");
        bool any = items.Count > 0;
        bool single = items.Count == 1;
        bool anyInvalid = items.Any(x => !SafeFileOps.TryValidateMutationPath(x.FullPath, out _, out _));

        this.FindControl<MenuItem>("ctxLocalSend")!.IsEnabled = any && _client != null;
        this.FindControl<MenuItem>("ctxLocalRename")!.IsEnabled = single && !anyInvalid;
        this.FindControl<MenuItem>("ctxLocalDelete")!.IsEnabled = any;
        this.FindControl<MenuItem>("ctxLocalVerify")!.IsEnabled = any && !items.Any(x => x.IsDirectory) && _client != null;
    }

    private async void LocalCtx_Send(object? sender, RoutedEventArgs e)
    {
        if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
        var items = GetSelectedItems("localList");
        if (items.Count == 0) return;
        await TransferAsync(items, isUpload: true);
    }

    private async void LocalCtx_Rename(object? sender, RoutedEventArgs e)
    {
        var items = GetSelectedItems("localList");
        if (items.Count != 1) return;
        var item = items[0];

        if (!SafeFileOps.TryValidateMutationPath(item.FullPath, out var sourcePath, out var reason))
        {
            SetStatus(L.Format("st.blocked", reason));
            SafeFileOps.Audit("rename", item.FullPath, "blocked", reason);
            return;
        }

        var dlg = new InputDialog(L["dlg.rename.title"], L["dlg.rename.prompt"], item.Name);
        _ = dlg.ShowDialog(this);
        var newName = await dlg.GetResultAsync();
        if (string.IsNullOrWhiteSpace(newName) || newName == item.Name) return;
        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { SetStatus(L["st.invalidName"]); return; }

        if (SafeFileOps.IsOnCooldown($"local-rename:{sourcePath}", 2))
        {
            SetStatus(L["st.cooldown"]);
            SafeFileOps.Audit("rename", sourcePath, "blocked", "cooldown");
            return;
        }

        try
        {
            var dir = Path.GetDirectoryName(sourcePath)!;
            var destPath = Path.Combine(dir, newName);
            if (!SafeFileOps.TryValidateMutationPath(destPath, out _, out var destReason, requireExists: false))
            {
                SetStatus(L.Format("st.destBlocked", destReason));
                SafeFileOps.Audit("rename", sourcePath, "blocked", $"dest:{destReason}");
                return;
            }

            if (item.IsDirectory) Directory.Move(sourcePath, destPath);
            else File.Move(sourcePath, destPath);

            SafeFileOps.Audit("rename", sourcePath, "ok", $"to:{destPath}");
            await RefreshLocalAsync();
        }
        catch (Exception ex)
        {
            SafeFileOps.Audit("rename", sourcePath, "error", ex.Message);
            SetStatus(L.Format("st.renameError", ex.Message));
        }
    }

    private async void LocalCtx_Delete(object? sender, RoutedEventArgs e)
    {
        var items = GetSelectedItems("localList");
        if (items.Count == 0) return;

        var allowed = new List<(FileEntry item, string path)>();
        var blocked = new List<string>();

        foreach (var item in items)
        {
            if (SafeFileOps.TryValidateMutationPath(item.FullPath, out var normalized, out var reason))
                allowed.Add((item, normalized));
            else
            {
                blocked.Add($"{item.Name}: {reason}");
                SafeFileOps.Audit("delete", item.FullPath, "blocked", reason);
            }
        }

        if (allowed.Count == 0)
        {
            await ShowInfoDialog("Borrado bloqueado", string.Join(Environment.NewLine, blocked));
            return;
        }

        var msg = $"Mover {allowed.Count} elemento(s) a papelera segura ($LanCopyTrash)?";
        if (!await MessageBox(msg, "Confirmar borrado")) return;

        if (SafeFileOps.IsHighRiskDelete(allowed.Select(x => x.path).ToList()))
        {
            var confirmDlg = new InputDialog(L["dlg.hard.title"], L.Format("dlg.hard.prompt", SafeFileOps.HardConfirmToken), "");
            _ = confirmDlg.ShowDialog(this);
            var token = await confirmDlg.GetResultAsync();
            if (!string.Equals(token, SafeFileOps.HardConfirmToken, StringComparison.Ordinal))
            {
                SetStatus(L["st.deleteCancelled"]);
                return;
            }
        }

        int ok = 0, cooldown = 0, err = 0;
        var lines = new List<string>();
        foreach (var (item, path) in allowed)
        {
            var key = $"local-delete:{path}";
            if (SafeFileOps.IsOnCooldown(key, 2))
            {
                cooldown++;
                lines.Add($"⏳ {item.Name} — cooldown");
                SafeFileOps.Audit("delete", path, "blocked", "cooldown");
                continue;
            }

            if (SafeFileOps.TryMoveToTrash(path, out var moved, out var moveErr))
            {
                ok++;
                lines.Add($"🗑️ {item.Name} -> {moved}");
                SafeFileOps.Audit("delete", path, "ok", $"trash:{moved}");
            }
            else
            {
                err++;
                lines.Add($"⚠️ {item.Name} — {moveErr}");
                SafeFileOps.Audit("delete", path, "error", moveErr);
            }
        }

        if (blocked.Count > 0) lines.InsertRange(0, blocked.Select(x => $"🔒 {x}"));

        await RefreshLocalAsync();
        await ShowInfoDialog("Resumen borrado local", $"OK:{ok}  Bloq:{blocked.Count + cooldown}  Err:{err}" + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, lines));
        SetStatus(L.Format("st.deleteLocalResult", ok, blocked.Count + cooldown, err));
    }

    private async void LocalCtx_Verify(object? sender, RoutedEventArgs e)
    {
        if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
        var items = GetSelectedItems("localList").Where(x => !x.IsDirectory).ToList();
        if (items.Count == 0) return;

        SetStatus(L["st.verifying"]);
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();
        var ct = CancellationToken.None;
        var toleranceTicks = TimeSpan.FromSeconds(2).Ticks;

        // Paralelo: max 4 simultáneos (evita saturar cliente)
        var tasks = items.Select(item => VerifyLocalItemAsync(item, toleranceTicks, results, ct)).ToList();
        await Task.WhenAll(tasks);

        var sortedResults = results.OrderBy(r => r).ToList();
        var text = string.Join(Environment.NewLine, sortedResults);
        await ShowInfoDialog("Verificación local -> remoto", text.TrimEnd());
    }

    private async Task VerifyLocalItemAsync(FileEntry item, long toleranceTicks,
        System.Collections.Concurrent.ConcurrentBag<string> results, CancellationToken ct)
    {
        try
        {
            var remotePath = string.IsNullOrEmpty(_remotePath)
                ? item.Name
                : Path.Combine(_remotePath, item.Name).Replace('\\', '/');

            var fi = new FileInfo(item.FullPath);

            await _clientLock.WaitAsync(ct);
            LanClient.RemoteStat? stat;
            try { stat = await _client!.GetStatAsync(remotePath, ct); }
            finally { _clientLock.Release(); }

            if (stat is null || !stat.Exists)
            {
                results.Add($"❓ {item.Name} — no existe en remoto");
                return;
            }
            if (stat.IsDirectory)
            {
                results.Add($"⚠️ {item.Name} — remoto es directorio");
                return;
            }

            var sizeMatch = fi.Length == stat.Size;
            var timeMatch = Math.Abs(fi.LastWriteTimeUtc.Ticks - stat.LastWriteUtcTicks) <= toleranceTicks;
            if (!sizeMatch || !timeMatch)
            {
                results.Add($"❌ {item.Name} — diferencia rápida (size/time)");
                return;
            }

            await using var fs = File.OpenRead(item.FullPath);
            var localSha256 = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();

            await _clientLock.WaitAsync(ct);
            string? remoteHash;
            bool usingSha256;
            try
            {
                remoteHash = await _client!.GetSha256Async(remotePath, ct);
                usingSha256 = !string.IsNullOrWhiteSpace(remoteHash);
                if (!usingSha256) remoteHash = await _client.GetSha1Async(remotePath, ct);
            }
            finally { _clientLock.Release(); }

            if (remoteHash == null) results.Add($"❓ {item.Name} — hash remoto no disponible");
            else if (usingSha256 && string.Equals(localSha256, remoteHash, StringComparison.OrdinalIgnoreCase))
                results.Add($"✅ {item.Name} — idéntico (SHA256)");
            else if (!usingSha256)
            {
                fs.Seek(0, SeekOrigin.Begin);
                var localSha1 = Convert.ToHexString(await SHA1.HashDataAsync(fs, ct)).ToLowerInvariant();
                if (string.Equals(localSha1, remoteHash, StringComparison.OrdinalIgnoreCase))
                    results.Add($"✅ {item.Name} — idéntico (SHA1 compat)");
                else results.Add($"❌ {item.Name} — hash diferente");
            }
            else results.Add($"❌ {item.Name} — hash diferente");
        }
        catch (Exception ex)
        {
            results.Add($"⚠️ {item.Name} — error: {ex.Message}");
        }
    }

    private async void LocalCtx_CopyPath(object? sender, RoutedEventArgs e)
    {
        var items = GetSelectedItems("localList");
        if (items.Count == 0) return;
        var text = string.Join(Environment.NewLine, items.Select(x => x.FullPath));
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null) await clipboard.SetTextAsync(text);
        SetStatus(L["st.pathsCopied"]);
    }

    // ══ Context menus — Remote ════════════════════════════════════════════════════

    private void RemoteCtx_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var items = GetSelectedItems("remoteList");
        bool any = items.Count > 0;
        bool single = items.Count == 1;
        bool connected = _client != null;

        this.FindControl<MenuItem>("ctxRemoteReceive")!.IsEnabled = any && connected;
        this.FindControl<MenuItem>("ctxRemoteRename")!.IsEnabled = single && connected;
        this.FindControl<MenuItem>("ctxRemoteDelete")!.IsEnabled = any && connected;
        this.FindControl<MenuItem>("ctxRemoteVerify")!.IsEnabled = any && !items.Any(x => x.IsDirectory) && connected;
    }

    private async void RemoteCtx_Receive(object? sender, RoutedEventArgs e)
    {
        if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
        var items = GetSelectedItems("remoteList");
        if (items.Count == 0) return;
        await TransferAsync(items, isUpload: false);
    }

    private async void RemoteCtx_Rename(object? sender, RoutedEventArgs e)
    {
        if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
        var items = GetSelectedItems("remoteList");
        if (items.Count != 1) return;
        var item = items[0];

        var dlg = new InputDialog(L["dlg.rename.titleRemote"], L["dlg.rename.prompt"], item.Name);
        _ = dlg.ShowDialog(this);
        var newName = await dlg.GetResultAsync();
        if (string.IsNullOrWhiteSpace(newName) || newName == item.Name) return;
        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { SetStatus(L["st.invalidName"]); return; }

        try
        {
            await _clientLock.WaitAsync();
            try { await _client!.RenameAsync(item.FullPath, newName); }
            finally { _clientLock.Release(); }

            SafeFileOps.Audit("remote-rename", item.FullPath, "ok", $"to:{newName}");
            await RefreshRemoteAsync();
        }
        catch (Exception ex)
        {
            SafeFileOps.Audit("remote-rename", item.FullPath, "error", ex.Message);
            SetStatus(L.Format("st.renameError", ex.Message));
        }
    }

    private async void RemoteCtx_Delete(object? sender, RoutedEventArgs e)
    {
        if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
        var items = GetSelectedItems("remoteList");
        if (items.Count == 0) return;

        if (!await MessageBox($"Mover {items.Count} elemento(s) remotos a papelera segura?", "Confirmar borrado remoto")) return;

        if (items.Count >= 20 || items.Any(x => x.IsDirectory))
        {
            var confirmDlg = new InputDialog(L["dlg.hard.title"], L.Format("dlg.hard.prompt", SafeFileOps.HardConfirmToken), "");
            _ = confirmDlg.ShowDialog(this);
            var token = await confirmDlg.GetResultAsync();
            if (!string.Equals(token, SafeFileOps.HardConfirmToken, StringComparison.Ordinal))
            {
                SetStatus(L["st.deleteCancelledRemote"]);
                return;
            }
        }

        int ok = 0, err = 0;
        var lines = new List<string>();

        foreach (var item in items)
        {
            try
            {
                await _clientLock.WaitAsync();
                try { await _client!.DeleteAsync(item.FullPath); }
                finally { _clientLock.Release(); }

                ok++;
                lines.Add($"🗑️ {item.Name} — enviado a papelera remota");
                SafeFileOps.Audit("remote-delete", item.FullPath, "ok", "trash-remote");
            }
            catch (Exception ex)
            {
                err++;
                lines.Add($"⚠️ {item.Name} — {ex.Message}");
                SafeFileOps.Audit("remote-delete", item.FullPath, "error", ex.Message);
            }
        }

        await RefreshRemoteAsync();
        await ShowInfoDialog("Resumen borrado remoto", $"OK:{ok}  Err:{err}" + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, lines));
        SetStatus(L.Format("st.deleteRemoteResult", ok, err));
    }

    private async void RemoteCtx_Verify(object? sender, RoutedEventArgs e)
    {
        if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
        var items = GetSelectedItems("remoteList").Where(x => !x.IsDirectory).ToList();
        if (items.Count == 0) return;

        SetStatus(L["st.verifying"]);
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();
        var ct = CancellationToken.None;
        var toleranceTicks = TimeSpan.FromSeconds(2).Ticks;

        // Paralelo: max 4 simultáneos
        var tasks = items.Select(item => VerifyRemoteItemAsync(item, toleranceTicks, results, ct)).ToList();
        await Task.WhenAll(tasks);

        var sortedResults = results.OrderBy(r => r).ToList();
        var text = string.Join(Environment.NewLine, sortedResults);
        await ShowInfoDialog("Verificación remoto -> local", text.TrimEnd());
    }

    private async Task VerifyRemoteItemAsync(FileEntry item, long toleranceTicks,
        System.Collections.Concurrent.ConcurrentBag<string> results, CancellationToken ct)
    {
        try
        {
            var localPath = Path.Combine(_localPath, item.Name);

            await _clientLock.WaitAsync(ct);
            LanClient.RemoteStat? stat;
            try { stat = await _client!.GetStatAsync(item.FullPath, ct); }
            finally { _clientLock.Release(); }

            if (stat is null || !stat.Exists) { results.Add($"❓ {item.Name} — no disponible en remoto"); return; }
            if (stat.IsDirectory) { results.Add($"⚠️ {item.Name} — remoto es directorio"); return; }
            if (!File.Exists(localPath)) { results.Add($"❓ {item.Name} — no existe localmente"); return; }

            var fi = new FileInfo(localPath);
            var sizeMatch = fi.Length == stat.Size;
            var timeMatch = Math.Abs(fi.LastWriteTimeUtc.Ticks - stat.LastWriteUtcTicks) <= toleranceTicks;
            if (!sizeMatch || !timeMatch)
            {
                results.Add($"❌ {item.Name} — diferencia rápida (size/time)");
                return;
            }

            await using var fs = File.OpenRead(localPath);
            var localSha256 = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();

            await _clientLock.WaitAsync(ct);
            string? remoteHash;
            bool usingSha256;
            try
            {
                remoteHash = await _client!.GetSha256Async(item.FullPath, ct);
                usingSha256 = !string.IsNullOrWhiteSpace(remoteHash);
                if (!usingSha256) remoteHash = await _client.GetSha1Async(item.FullPath, ct);
            }
            finally { _clientLock.Release(); }

            if (remoteHash == null) results.Add($"❓ {item.Name} — hash remoto no disponible");
            else if (usingSha256 && string.Equals(localSha256, remoteHash, StringComparison.OrdinalIgnoreCase))
                results.Add($"✅ {item.Name} — idéntico (SHA256)");
            else if (!usingSha256)
            {
                fs.Seek(0, SeekOrigin.Begin);
                var localSha1 = Convert.ToHexString(await SHA1.HashDataAsync(fs, ct)).ToLowerInvariant();
                if (string.Equals(localSha1, remoteHash, StringComparison.OrdinalIgnoreCase))
                    results.Add($"✅ {item.Name} — idéntico (SHA1 compat)");
                else results.Add($"❌ {item.Name} — hash diferente");
            }
            else results.Add($"❌ {item.Name} — hash diferente");
        }
        catch (Exception ex)
        {
            results.Add($"⚠️ {item.Name} — error: {ex.Message}");
        }
    }

    private async void RemoteCtx_CopyPath(object? sender, RoutedEventArgs e)
    {
        var items = GetSelectedItems("remoteList");
        if (items.Count == 0) return;
        var text = string.Join(Environment.NewLine, items.Select(x => x.FullPath));
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null) await clipboard.SetTextAsync(text);
        SetStatus(L["st.pathsCopied"]);
    }

    // ══ Helpers UI ════════════════════════════════════════════════════════════════

    private async Task<bool> MessageBox(string message, string title = "Confirmar")
    {
        var tcs = new TaskCompletionSource<bool>();
        var dlg = new Window
        {
            Title = title,
            Width = 420,
            Height = 160,
            CanResize = false,
            Background = SolidColorBrush.Parse("#2D2D30"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 14 };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = SolidColorBrush.Parse("#CCCCCC"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        });
        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        var btnNo = new Button { Content = "Cancelar", Background = SolidColorBrush.Parse("#3E3E42"), Foreground = Brushes.White, Padding = new Thickness(10, 5) };
        var btnYes = new Button { Content = "Aceptar", Background = SolidColorBrush.Parse("#C0392B"), Foreground = Brushes.White, Padding = new Thickness(10, 5) };
        btnNo.Click += (_, _) => { tcs.TrySetResult(false); dlg.Close(); };
        btnYes.Click += (_, _) => { tcs.TrySetResult(true); dlg.Close(); };
        btns.Children.Add(btnNo);
        btns.Children.Add(btnYes);
        panel.Children.Add(btns);
        dlg.Content = panel;
        dlg.Closing += (_, _) => tcs.TrySetResult(false);
        await dlg.ShowDialog(this);
        return await tcs.Task;
    }

    private async Task ShowInfoDialog(string title, string content)
    {
        var dlg = new Window
        {
            Title = title,
            Width = 560,
            Height = 360,
            Background = SolidColorBrush.Parse("#2D2D30"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 14 };
        var scroll = new ScrollViewer { MaxHeight = 260 };
        scroll.Content = new TextBlock
        {
            Text = content,
            Foreground = SolidColorBrush.Parse("#CCCCCC"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas,Courier New,monospace")
        };
        panel.Children.Add(scroll);
        var btnClose = new Button
        {
            Content = "Cerrar",
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = SolidColorBrush.Parse("#007ACC"),
            Foreground = Brushes.White,
            Padding = new Thickness(12, 5)
        };
        btnClose.Click += (_, _) => dlg.Close();
        panel.Children.Add(btnClose);
        dlg.Content = panel;
        await dlg.ShowDialog(this);
    }

    // ── Feature 3: Perfiles de conexión ─────────────────────────────────────

    private void CmbProfiles_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var combo = this.FindControl<ComboBox>("cmbProfiles");
        if (combo?.SelectedItem is not string name) return;
        var p = _profiles.FirstOrDefault(x => x.Name == name);
        if (p == null) return;
        // Restaura todos los ajustes del perfil (los handlers aplican y persisten); NO conecta.
        this.FindControl<TextBox>("txtRemoteIp")!.Text = p.Ip;
        this.FindControl<TextBox>("txtRemotePort")!.Text = p.Port;
        this.FindControl<TextBox>("txtPin")!.Text = p.Pin;
        var chkT = this.FindControl<CheckBox>("chkTls"); if (chkT != null) chkT.IsChecked = p.Tls;
        var chkC = this.FindControl<CheckBox>("chkCompress"); if (chkC != null) chkC.IsChecked = p.Compress;
        SetStatus(L.Format("st.profileLoaded", name));
    }

    private async void SaveProfile_Click(object? sender, RoutedEventArgs e)
    {
        var ip = this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "";
        var port = this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742";
        if (string.IsNullOrEmpty(ip)) { SetStatus(L["st.enterIpFirst"]); return; }
        var dlg = new InputDialog(L["dlg.profile.title"], L["dlg.profile.prompt"], ip);
        _ = dlg.ShowDialog(this);
        var name = await dlg.GetResultAsync();
        if (string.IsNullOrWhiteSpace(name)) return;
        var pin = this.FindControl<TextBox>("txtPin")?.Text?.Trim() ?? "";
        _profiles.RemoveAll(p => p.Name == name);
        _profiles.Add(new ConnectionProfile(name, ip, port, pin, _tlsEnabled, _compressEnabled));
        SaveSettings(ip, port);
        RefreshProfilesCombo(name);
        SetStatus(L.Format("st.profileSaved", name));
    }

    private void DeleteProfile_Click(object? sender, RoutedEventArgs e)
    {
        var combo = this.FindControl<ComboBox>("cmbProfiles");
        if (combo?.SelectedItem is not string name) return;
        _profiles.RemoveAll(p => p.Name == name);
        SaveSettings(
            this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "",
            this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742");
        RefreshProfilesCombo();
        SetStatus(L.Format("st.profileDeleted", name));
    }

    private void RefreshProfilesCombo(string? select = null)
    {
        var combo = this.FindControl<ComboBox>("cmbProfiles");
        if (combo == null) return;
        var keep = select ?? combo.SelectedItem as string;
        var names = _profiles.Select(p => p.Name).ToList();
        combo.ItemsSource = null;
        combo.ItemsSource = names;
        if (keep != null && names.Contains(keep)) combo.SelectedItem = keep;
    }

    // ── Feature 6: Ordenación de columnas ────────────────────────────────────

    private void LocalSortName_Click(object? sender, RoutedEventArgs e) => SetLocalSort("name");
    private void LocalSortSize_Click(object? sender, RoutedEventArgs e) => SetLocalSort("size");
    private void LocalSortDate_Click(object? sender, RoutedEventArgs e) => SetLocalSort("date");
    private void RemoteSortName_Click(object? sender, RoutedEventArgs e) => SetRemoteSort("name");
    private void RemoteSortSize_Click(object? sender, RoutedEventArgs e) => SetRemoteSort("size");
    private void RemoteSortDate_Click(object? sender, RoutedEventArgs e) => SetRemoteSort("date");

    private void SetLocalSort(string field)
    {
        if (_localSortField == field) _localSortAsc = !_localSortAsc;
        else { _localSortField = field; _localSortAsc = true; }
        ApplyLocalFilter(this.FindControl<TextBox>("txtLocalFilter")?.Text?.Trim() ?? "");
    }

    private void SetRemoteSort(string field)
    {
        if (_remoteSortField == field) _remoteSortAsc = !_remoteSortAsc;
        else { _remoteSortField = field; _remoteSortAsc = true; }
        ApplyRemoteSort();
    }

    private static IEnumerable<FileEntry> SortEntries(IEnumerable<FileEntry> items, string field, bool asc)
    {
        var dotdot = items.Where(f => f.Name == "..").ToList();
        var dirs = items.Where(f => f.IsDirectory && f.Name != "..").ToList();
        var files = items.Where(f => !f.IsDirectory).ToList();

        IEnumerable<FileEntry> SortGroup(List<FileEntry> g) => field switch
        {
            "size" => asc ? g.OrderBy(f => f.Size) : g.OrderByDescending(f => f.Size),
            "date" => asc ? g.OrderBy(f => f.LastWriteUtcTicks) : g.OrderByDescending(f => f.LastWriteUtcTicks),
            _ => asc ? g.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                          : g.OrderByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase),
        };
        return dotdot.Concat(SortGroup(dirs)).Concat(SortGroup(files));
    }

    // ── Feature 7: Sparkline de velocidad ────────────────────────────────────

    private void UpdateSparkline(double bytesPerSec)
    {
        var mbps = bytesPerSec / (1024.0 * 1024.0);
        if (_speedHistory.Count >= SparklineLen) _speedHistory.Dequeue();
        if (bytesPerSec > 0) _speedHistory.Enqueue(mbps);
        if (_speedHistory.Count == 0) return;

        var max = _speedHistory.Max();
        var spark = max <= 0
            ? ""
            : string.Concat(_speedHistory.Select(v => SparkChars[(int)Math.Min(7, v / max * 7)]));

        Dispatcher.UIThread.Post(() =>
        {
            var tb = this.FindControl<TextBlock>("txtSparkline");
            if (tb != null) tb.Text = spark;
        });
    }

    // ── Feature 8: Watch folder ───────────────────────────────────────────────

    private void WatchFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (_watcherActive) StopWatch();
        else StartWatch();
    }

    private void StartWatch()
    {
        if (_client == null) { SetStatus(L["st.connectFirstRemote"]); return; }
        if (string.IsNullOrEmpty(_localPath)) { SetStatus(L["st.navLocalFirst"]); return; }

        try
        {
            _watcher = new FileSystemWatcher(_localPath)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };
            _watcher.Changed += OnWatcherEvent;
            _watcher.Created += OnWatcherEvent;
            _watcherActive = true;

            var btn = this.FindControl<Button>("btnWatch");
            if (btn != null) { btn.Content = L["btn.watchStop"]; btn.Background = SolidColorBrush.Parse("#C0392B"); }
            var tb = this.FindControl<TextBlock>("txtWatchStatus");
            if (tb != null) tb.Text = L["label.watching"];
            SetStatus(L.Format("st.watching", _localPath));
        }
        catch (Exception ex)
        {
            SetStatus(L.Format("st.watchError", ex.Message));
        }
    }

    private void StopWatch()
    {
        _watcher?.Dispose();
        _watcher = null;
        _watchDebounce?.Dispose();
        _watchDebounce = null;
        _watcherActive = false;

        var btn = this.FindControl<Button>("btnWatch");
        if (btn != null) { btn.Content = L["btn.watch"]; btn.Background = SolidColorBrush.Parse("#795548"); }
        var tb = this.FindControl<TextBlock>("txtWatchStatus");
        if (tb != null) tb.Text = "";
        SetStatus(L["st.watchStopped"]);
    }

    private void OnWatcherEvent(object sender, FileSystemEventArgs e)
    {
        // Debounce 700 ms — resetear timer con cada evento
        _watchDebounce?.Dispose();
        _watchDebounce = new System.Timers.Timer(700) { AutoReset = false };
        _watchDebounce.Elapsed += async (_, _) =>
        {
            if (!File.Exists(e.FullPath)) return;
            try
            {
                var fi = new FileInfo(e.FullPath);
                var entry = new FileEntry { Name = fi.Name, FullPath = fi.FullName, Size = fi.Length };
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SetStatus(L.Format("st.changeDetected", fi.Name));
                    _ = TransferAsync(new List<FileEntry> { entry }, isUpload: true);
                });
            }
            catch { }
        };
        _watchDebounce.Start();
    }

    // ── Feature 1: Sync de carpetas ───────────────────────────────────────────

    private async void SyncToRemote_Click(object? sender, RoutedEventArgs e) => await SyncAsync(toRemote: true);
    private async void SyncToLocal_Click(object? sender, RoutedEventArgs e) => await SyncAsync(toRemote: false);

    private async Task SyncAsync(bool toRemote)
    {
        if (_client == null) { SetStatus(L["st.connectFirstRemote"]); return; }
        if (string.IsNullOrEmpty(_localPath)) { SetStatus(L["st.navLocalFirst"]); return; }
        if (string.IsNullOrEmpty(_remotePath)) { SetStatus(L["st.navRemoteFirst"]); return; }

        SetStatus(L["st.syncFetching"]);
        try
        {
            // Lista remota (soporta recursivo con LastWriteUtcTicks)
            var snap = _client;
            List<FileEntry> remoteFiles;
            await _clientLock.WaitAsync();
            try { remoteFiles = await snap.ListRecursiveAsync(_remotePath); }
            finally { _clientLock.Release(); }

            var remoteDict = remoteFiles.ToDictionary(f => f.Name, f => f);

            // Lista local
            var localFiles = await Task.Run(() =>
            {
                if (!Directory.Exists(_localPath)) return new List<FileEntry>();
                return Directory.EnumerateFiles(_localPath, "*", SearchOption.AllDirectories)
                    .Select(f =>
                    {
                        var fi = new FileInfo(f);
                        return new FileEntry
                        {
                            Name = Path.GetRelativePath(_localPath, f).Replace('\\', '/'),
                            FullPath = f,
                            Size = fi.Length,
                            LastWriteUtcTicks = fi.LastWriteTimeUtc.Ticks
                        };
                    }).ToList();
            });
            var localDict = localFiles.ToDictionary(f => f.Name, f => f);

            static bool IsDiff(FileEntry a, FileEntry b) =>
                a.Size != b.Size || Math.Abs(a.LastWriteUtcTicks - b.LastWriteUtcTicks) > TimeSpan.TicksPerSecond;

            List<FileEntry> toTransfer;
            if (toRemote)
            {
                toTransfer = localFiles
                    .Where(lf => !remoteDict.TryGetValue(lf.Name, out var rf) || IsDiff(lf, rf))
                    .ToList();
            }
            else
            {
                toTransfer = remoteFiles
                    .Where(rf => !localDict.TryGetValue(rf.Name, out var lf) || IsDiff(rf, lf))
                    .Select(rf => new FileEntry
                    {
                        Name = rf.Name,
                        FullPath = Path.Combine(_remotePath, rf.Name.Replace('/', Path.DirectorySeparatorChar)),
                        Size = rf.Size,
                        LastWriteUtcTicks = rf.LastWriteUtcTicks
                    }).ToList();
            }

            if (toTransfer.Count == 0) { SetStatus(L["st.syncDone"]); return; }

            SetStatus(L.Format("st.syncToTransfer", toTransfer.Count));
            await TransferAsync(toTransfer, isUpload: toRemote);
        }
        catch (Exception ex) { SetStatus(L.Format("st.syncError", ex.Message)); }
    }

    // ── Feature 9+2: TLS + compresión ────────────────────────────────────────

    private void ChkTls_Changed(object? sender, RoutedEventArgs e)
    {
        _tlsEnabled = (sender as CheckBox)?.IsChecked == true;
        _server.TlsEnabled = _tlsEnabled;
        SaveSettings(
            this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "",
            this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742");
        SetStatus(_tlsEnabled ? L["st.tlsOn"] : L["st.tlsOff"]);
    }

    private void ChkShareRoot_Changed(object? sender, RoutedEventArgs e)
    {
        _restrictShareRoot = (sender as CheckBox)?.IsChecked == true;
        _server.RestrictToShareRoot = _restrictShareRoot;
        SaveSettings(
            this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "",
            this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742");
        SetStatus(_restrictShareRoot ? L["st.shareRootOn"] : L["st.shareRootOff"]);
    }

    private void ChkReadOnly_Changed(object? sender, RoutedEventArgs e)
    {
        _readOnly = (sender as CheckBox)?.IsChecked == true;
        _server.ReadOnly = _readOnly;
        SaveSettings(
            this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "",
            this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742");
        SetStatus(_readOnly ? L["st.readOnlyOn"] : L["st.readOnlyOff"]);
    }

    private void ChkRequireApproval_Changed(object? sender, RoutedEventArgs e)
    {
        _requireApproval = (sender as CheckBox)?.IsChecked == true;
        _server.ApproveIncoming = _requireApproval ? OnApproveIncomingAsync : null;
        SaveSettings(
            this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "",
            this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742");
        SetStatus(_requireApproval ? L["st.approvalOn"] : L["st.approvalOff"]);
    }

    // Llamado por el servidor (hilo de red) antes de aceptar un fichero. Marshala a la UI,
    // muestra el dialogo y aplica un timeout de 60s que rechaza por seguridad si nadie responde.
    private async Task<bool> OnApproveIncomingAsync(FileServer.IncomingTransfer info, CancellationToken ct)
    {
        try
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dlg = new ConsentDialog(info.Ip, info.FileName, info.Size);
                _ = dlg.ShowDialog(this);
                var result = dlg.GetResultAsync();
                var winner = await Task.WhenAny(result, Task.Delay(TimeSpan.FromSeconds(60), ct));
                if (winner != result) { try { dlg.Close(); } catch { } return false; }
                return await result;
            });
        }
        catch { return false; }
    }

    private void ChkCompress_Changed(object? sender, RoutedEventArgs e)
    {
        _compressEnabled = (sender as CheckBox)?.IsChecked == true;
        SaveSettings(
            this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "",
            this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742");
        SetStatus(_compressEnabled ? L["st.compressOn"] : L["st.compressOff"]);
    }
}

// ── Modelo: perfil de conexión (Feature 3) ────────────────────────────────────
internal record ConnectionProfile(string Name, string Ip, string Port, string Pin = "", bool Tls = false, bool Compress = false);
