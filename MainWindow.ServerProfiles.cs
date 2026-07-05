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

public partial class MainWindow
{
    // ÔöÇÔöÇ Feature 3: Perfiles de conexi├│n ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ

    private void CmbProfiles_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var combo = this.FindControl<ComboBox>("cmbProfiles");
        if (combo?.SelectedItem is not string name) return;
        var p = ProfileStore.Find(_profiles, name);
        if (p == null) return;
        // Restaura todos los ajustes del perfil (los handlers aplican y persisten); NO conecta.
        this.FindControl<TextBox>("txtRemoteIp")!.Text = p.Ip;
        this.FindControl<TextBox>("txtRemotePort")!.Text = p.Port;
        this.FindControl<TextBox>("txtPin")!.Text = p.Pin;
        var chkT = this.FindControl<CheckBox>("chkTls"); if (chkT != null) chkT.IsChecked = p.Tls;
        var chkC = this.FindControl<CheckBox>("chkCompress"); if (chkC != null) chkC.IsChecked = p.Compress;
        SaveSettings(p.Ip, p.Port); // Persistir perfil seleccionado para restaurarlo al arrancar
        SetStatus(L.Format("st.profileLoaded", name));
    }

    private async void SaveProfile_Click(object? sender, RoutedEventArgs e)
    {
        var ip = this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "";
        var port = this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742";
        if (string.IsNullOrEmpty(ip)) { SetStatus(L["st.enterIpFirst"]); return; }
        try
        {
            var dlg = new InputDialog(L["dlg.profile.title"], L["dlg.profile.prompt"], ip);
            await dlg.ShowDialog(this); // Q4: era _ = dlg.ShowDialog — fire-and-forget descartaba excepciones del diálogo
            var name = await dlg.GetResultAsync();
            if (string.IsNullOrWhiteSpace(name)) return;
            var pin = this.FindControl<TextBox>("txtPin")?.Text?.Trim() ?? "";
            ProfileStore.Upsert(_profiles, new ConnectionProfile(name, ip, port, pin, _tlsEnabled, _compressEnabled));
            SaveSettings(ip, port);
            RefreshProfilesCombo(name);
            SetStatus(L.Format("st.profileSaved", name));
        }
        catch (Exception ex)
        {
            Log.Warn("profiles", "save-profile-unexpected", new { ip, error = ex.Message });
            SetStatus(L[ex.Message]);
        }
    }

    private void DeleteProfile_Click(object? sender, RoutedEventArgs e)
    {
        var combo = this.FindControl<ComboBox>("cmbProfiles");
        if (combo?.SelectedItem is not string name) return;
        ProfileStore.Remove(_profiles, name);
        
        // Limpiar campos UI ya que el perfil seleccionado fue eliminado
        this.FindControl<TextBox>("txtRemoteIp")!.Text = "";
        this.FindControl<TextBox>("txtRemotePort")!.Text = "8742";
        this.FindControl<TextBox>("txtPin")!.Text = "";

        SaveSettings("", "8742");
        RefreshProfilesCombo();
        SetStatus(L.Format("st.profileDeleted", name));
    }

    private void RefreshProfilesCombo(string? select = null)
    {
        var combo = this.FindControl<ComboBox>("cmbProfiles");
        if (combo == null) return;
        var keep = select ?? combo.SelectedItem as string;
        var names = ProfileStore.Names(_profiles).ToList();
        combo.ItemsSource = null;
        combo.ItemsSource = names;
        if (keep != null && names.Contains(keep)) combo.SelectedItem = keep;
    }

    // ÔöÇÔöÇ Feature 6: Ordenaci├│n de columnas ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ

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

    // M12/M16: devuelve List<FileEntry> directamente (FileSorter.Sort ya materializa el resultado)
    // — elimina el .ToList() extra en ApplyLocalFilter y ApplyRemoteSort.
    private static List<FileEntry> SortEntries(IEnumerable<FileEntry> items, string field, bool asc)
        => FileSorter.Sort(items, field, asc);

    // ÔöÇÔöÇ Feature 7: Sparkline de velocidad ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ

    private void UpdateSparkline(double bytesPerSec)
    {
        var mbps = bytesPerSec / (1024.0 * 1024.0);
        string spark;
        lock (_speedHistory)
        {
            if (_speedHistory.Count >= SparklineLen) _speedHistory.Dequeue();
            if (bytesPerSec > 0) _speedHistory.Enqueue(mbps);
            if (_speedHistory.Count == 0) return;
            spark = SpeedSparkline.Render(_speedHistory);
        }

        Dispatcher.UIThread.Post(() =>
        {
            var tb = this.FindControl<TextBlock>("txtSparkline");
            if (tb != null) tb.Text = spark;
        });
    }

    // ÔöÇÔöÇ Feature 8: Watch folder ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ

    private async void WatchFolder_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_watchFolderActive) StopWatch();
            else await StartWatchAsync();
        }
        catch (Exception ex) { Log.Warn("watch", "watch-folder-click-unexpected", new { error = ex.Message }); }
    }

    private async Task StartWatchAsync()
    {
        // U1: snapshot _client bajo _clientLock para evitar TOCTOU con DisconnectAsync
        // (DisconnectAsync adquiere _clientLock y luego nula _client; leer sin lock es carrera)
        bool clientAvailable;
        // B2: ConfigureAwait(true) para continuar en el UI thread despues del await
        // (ConfigureAwait(false) puede correr la continuacion en thread-pool, y SetStatus requiere UI thread)
        await _clientLock.WaitAsync().ConfigureAwait(true);
        try { clientAvailable = _client != null; }
        finally { _clientLock.Release(); }
        if (!clientAvailable) { SetStatus(L["st.connectFirstRemote"]); return; }
        if (string.IsNullOrEmpty(_localPath)) { SetStatus(L["st.navLocalFirst"]); return; }

        try
        {
            _watchFolderWatcher = new FileSystemWatcher(_localPath)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };
            _watchFolderWatcher.Changed += OnWatcherEvent;
            _watchFolderWatcher.Created += OnWatcherEvent;
            // B8: suscribir Error para detectar desbordamiento de buffer o directorio inaccesible.
            // Sin este handler, el watcher deja de funcionar silenciosamente sin notificar al usuario.
            _watchFolderWatcher.Error += (_, eArgs) =>
            {
                Log.Warn("watch", "watcher-error", new { path = _localPath, error = eArgs.GetException().Message });
                Dispatcher.UIThread.Post(() => { SetStatus(L.Format("st.watchError", eArgs.GetException().Message)); StopWatch(); });
            };
            _watchFolderActive = true; // B2: campo separado del watcher de browser

            // U2: pre-alocar el timer una vez — OnWatcherEvent solo llama Stop()+Start() sin alloc
            lock (_watchLock)
            {
                _watchDebounce = new System.Timers.Timer(WatchDebounceMs) { AutoReset = false };
                _watchDebounce.Elapsed += StartWatchDebounceCallback;
            }

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
        if (_watchFolderWatcher != null)
        {
            _watchFolderWatcher.Changed -= OnWatcherEvent;
            _watchFolderWatcher.Created -= OnWatcherEvent;
            _watchFolderWatcher.Dispose(); // B1: era _watcher.Dispose() — bug: watcher seguía activo
            _watchFolderWatcher = null;    // B1: era _watcher = null — nullaba el objeto incorrecto
        }
        lock (_watchLock) { _watchDebounce?.Dispose(); _watchDebounce = null; }
        _watchFolderActive = false; // Q7+B1: era _watcherActive — flag incorrecto

        var btn = this.FindControl<Button>("btnWatch");
        if (btn != null) { btn.Content = L["btn.watch"]; btn.Background = SolidColorBrush.Parse("#795548"); }
        var tb = this.FindControl<TextBlock>("txtWatchStatus");
        if (tb != null) tb.Text = "";
        SetStatus(L["st.watchStopped"]);
    }

    private const int WatchDebounceMs = 700;

    private void OnWatcherEvent(object sender, FileSystemEventArgs e)
    {
        var changedPath = e.FullPath;
        lock (_watchLock)
        {
            if (_watchDebounce == null) return;
            _watchDebounce.Stop();
            _watchDebounce.Start();
            _watchPendingPath = changedPath;
        }
    }

    private volatile string? _watchPendingPath;

    private void StartWatchDebounceCallback(object? sender, System.Timers.ElapsedEventArgs e)
    {
        string? pendingPath;
        lock (_watchLock) { pendingPath = _watchPendingPath; }
        if (pendingPath == null) return;
        _ = Task.Run(async () =>
        {
            if (!File.Exists(pendingPath)) return;
            if (SafeFileOps.ContainsReparsePoint(pendingPath)) return;
            // B3/U1: comprobar conexión antes de transferir
            bool connected;
            await _clientLock.WaitAsync().ConfigureAwait(false);
            try { connected = _client != null; }
            finally { _clientLock.Release(); }
            if (!connected) return;
            try
            {
                // Esperar a que el archivo sea liberado por el proceso que lo está escribiendo
                await WaitForFileUnlockAsync(pendingPath);

                // Comprobar si el watcher se detuvo durante la espera del desbloqueo
                if (!_watchFolderActive) return;

                var fi = new FileInfo(pendingPath);
                var entry = new FileEntry { Name = fi.Name, FullPath = fi.FullName, Size = fi.Length };
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!_watchFolderActive) return;
                    SetStatus(L.Format("st.changeDetected", fi.Name));
                    _ = TransferAsync(new List<FileEntry> { entry }, isUpload: true, silent: true);
                });
            }
            catch (Exception ex)
            {
                Log.Warn("watch", "debounce-error", new { error = ex.Message });
                await Dispatcher.UIThread.InvokeAsync(() => SetStatus(L.Format("st.watchError", ex.Message)));
            }
        });
    }

    private static async Task WaitForFileUnlockAsync(string path)
    {
        for (int i = 0; i < 20; i++)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
                return; // Desbloqueado
            }
            catch (IOException)
            {
                await Task.Delay(500);
            }
        }
    }

    // Feature: Sync de carpetas

    private async void SyncToRemote_Click(object? sender, RoutedEventArgs e)
    {
        try { await SyncAsync(toRemote: true); }
        catch (Exception ex) { Log.Warn("sync", "sync-to-remote-unexpected", new { error = ex.Message }); }
    }
    private async void SyncToLocal_Click(object? sender, RoutedEventArgs e)
    {
        try { await SyncAsync(toRemote: false); }
        catch (Exception ex) { Log.Warn("sync", "sync-to-local-unexpected", new { error = ex.Message }); }
    }

    private async Task SyncAsync(bool toRemote)
    {
        // U1: snapshot _client bajo _clientLock para evitar TOCTOU con DisconnectAsync
        // (DisconnectAsync adquiere _clientLock y luego nula _client; leer sin lock es carrera)
        bool clientAvailable;
        // B2: ConfigureAwait(true) para continuar en el UI thread despues del await
        // (ConfigureAwait(false) puede correr la continuacion en thread-pool, y SetStatus requiere UI thread)
        await _clientLock.WaitAsync().ConfigureAwait(true);
        try { clientAvailable = _client != null; }
        finally { _clientLock.Release(); }
        if (!clientAvailable) { SetStatus(L["st.connectFirstRemote"]); return; }
        if (string.IsNullOrEmpty(_localPath)) { SetStatus(L["st.navLocalFirst"]); return; }
        if (string.IsNullOrEmpty(_remotePath)) { SetStatus(L["st.navRemoteFirst"]); return; }

        SetStatus(L["st.syncFetching"]);
        try
        {
            // Lista remota (soporta recursivo con LastWriteUtcTicks)
            // P3: snapshot del cliente bajo lock, luego soltar ANTES del await de red
            // (ListRecursiveAsync puede tardar segundos; retener el lock bloqueaba disconnect/watchdog)
            LanClient? snap;
            await _clientLock.WaitAsync();
            try { snap = _client; }
            finally { _clientLock.Release(); }
            if (snap == null) { SetStatus(L["st.connectFirstRemote"]); return; }

            List<FileEntry> remoteFiles;
            // C9-FIX: timeout de 60s para ListRecursiveAsync — sin CT se bloqueaba hasta ~30s por
            // KeepAlive TCP si la conexión caía a mitad del listing. 60s es suficiente incluso para
            // directorios con miles de archivos en red LAN (latencia típica < 1ms).
            using var listCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(60));
            remoteFiles = await snap.ListRecursiveAsync(_remotePath, listCts.Token);

            var remoteDict = remoteFiles.ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);

            // Lista local
            var localFiles = await Task.Run(() =>
            {
                if (!Directory.Exists(_localPath)) return new List<FileEntry>();
                // BUG-FIX-B5: SearchOption.AllDirectories seguia symlinks/junctions ANTES de
                // aplicar el filtro ContainsReparsePoint, causando posibles bucles infinitos o
                // escape del directorio local. Usar EnumerationOptions sin seguir reparse points.
                var opts = new System.IO.EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System,
                    ReturnSpecialDirectories = false,
                };
                return Directory.EnumerateFiles(_localPath, "*", opts)
                    .Where(f => !SafeFileOps.ContainsReparsePoint(f))
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
            var localDict = localFiles.ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);

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
                        // Q7: normalizar separadores: el servidor envía '/', Windows usa '\\'
                        FullPath = Path.Combine(_remotePath, rf.Name.Replace('/', System.IO.Path.DirectorySeparatorChar)),
                        Size = rf.Size,
                        LastWriteUtcTicks = rf.LastWriteUtcTicks
                    }).ToList();
            }

            if (toTransfer.Count == 0) { SetStatus(L["st.syncDone"]); return; }

            SetStatus(L.Format("st.syncToTransfer", toTransfer.Count));
            await TransferAsync(toTransfer, isUpload: toRemote, targetIp: snap.Host, targetPort: snap.Port);
        }
        catch (Exception ex) { SetStatus(L.Format("st.syncError", ex.Message)); }
    }

    // ÔöÇÔöÇ Feature 9+2: TLS + compresi├│n ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ

    private void ChkTls_Changed(object? sender, RoutedEventArgs e)
    {
        if (_securityToggleGuard) return;
        _tlsEnabled = (sender as CheckBox)?.IsChecked == true;
        if (_safeModeEnabled && !_tlsEnabled)
        {
            _securityToggleGuard = true;
            try
            {
                _tlsEnabled = true;
                var chkTls = this.FindControl<CheckBox>("chkTls");
                if (chkTls != null) chkTls.IsChecked = true;
            }
            finally { _securityToggleGuard = false; }
            SetStatus(L["security.keepsTls"]);
            return;
        }
        _server.TlsEnabled = _tlsEnabled;
        _discovery?.UpdateTlsEnabled(_tlsEnabled);
        SaveSettings(
            this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "",
            this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742");
        SetStatus(_tlsEnabled ? L["st.tlsOn"] : L["st.tlsOff"]);
        UpdateServerModeBadge();
    }

    private void ChkShareRoot_Changed(object? sender, RoutedEventArgs e)
    {
        if (_securityToggleGuard) return;
        _restrictShareRoot = (sender as CheckBox)?.IsChecked == true;
        if (_safeModeEnabled && !_restrictShareRoot)
        {
            _securityToggleGuard = true;
            try
            {
                _restrictShareRoot = true;
                var chk = this.FindControl<CheckBox>("chkShareRoot");
                if (chk != null) chk.IsChecked = true;
            }
            finally { _securityToggleGuard = false; }
            SetStatus(L["security.keepsSharedFolder"]);
            return;
        }
        _server.RestrictToShareRoot = _restrictShareRoot;
        UpdateFullDiskSessionTimer();
        SaveSettings(
            this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "",
            this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742");
        if (_restrictShareRoot)
        {
            SetStatus(L["st.shareRootOn"]);
            UpdateServerModeBadge();
        }
        else
        {
            SetStatus(L.Format("security.fullDiskEnabled", (int)FullDiskSessionDuration.TotalMinutes));
            UpdateServerModeBadge();
        }
    }

    private void ChkReadOnly_Changed(object? sender, RoutedEventArgs e)
    {
        _readOnly = (sender as CheckBox)?.IsChecked == true;
        _server.ReadOnly = _readOnly;
        SaveSettings(
            this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "",
            this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742");
        SetStatus(_readOnly ? L["st.readOnlyOn"] : L["st.readOnlyOff"]);
        UpdateServerModeBadge();
    }

    private void ChkSafeModeNoDelete_Changed(object? sender, RoutedEventArgs e)
    {
        if (_securityToggleGuard) return;
        _safeModeNoRemoteDelete = (sender as CheckBox)?.IsChecked == true;
        if (_safeModeEnabled && !_safeModeNoRemoteDelete)
        {
            _securityToggleGuard = true;
            try
            {
                _safeModeNoRemoteDelete = true;
                var chk = this.FindControl<CheckBox>("chkSafeModeNoDelete");
                if (chk != null) chk.IsChecked = true;
            }
            finally { _securityToggleGuard = false; }
            SetStatus(L["security.keepsNoDelete"]);
            return;
        }
        _server.SafeModeNoRemoteDelete = _safeModeNoRemoteDelete;
        SaveSettings(
            this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "",
            this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742");
        SetStatus(_safeModeNoRemoteDelete ? L["st.safeModeNoDeleteOn"] : L["st.safeModeNoDeleteOff"]);
        UpdateServerModeBadge();
    }

    private void ChkRequireApproval_Changed(object? sender, RoutedEventArgs e)
    {
        _requireApproval = (sender as CheckBox)?.IsChecked == true;
        _server.ApproveIncoming = _requireApproval ? OnApproveIncomingAsync : null;
        SaveSettings(
            this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "",
            this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742");
        SetStatus(_requireApproval ? L["st.approvalOn"] : L["st.approvalOff"]);
        UpdateServerModeBadge();
    }

    private void ChkRequireHighRiskApproval_Changed(object? sender, RoutedEventArgs e)
    {
        _requireHighRiskApproval = (sender as CheckBox)?.IsChecked != false;
        _server.ApproveHighRisk = _requireHighRiskApproval ? OnApproveHighRiskAsync : null;
        SaveSettings(
            this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "",
            this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742");
        SetStatus(_requireHighRiskApproval ? L["st.highRiskApprovalOn"] : L["st.highRiskApprovalOff"]);
        UpdateServerModeBadge();
    }

    // Llamado por el servidor (hilo de red) antes de aceptar un fichero. Marshala a la UI,
    // muestra el dialogo y aplica un timeout de 60s que rechaza por seguridad si nadie responde.
    // B4+Q4: ConsentDialog ya tiene su propio AutoRejectAsync(60s); no necesitamos WhenAny externo.
    // Además, ShowDialog() solo retorna cuando el diálogo cierra → GetResultAsync() ya está resuelta.
    private async Task<bool> OnApproveIncomingAsync(FileServer.IncomingTransfer info, CancellationToken ct)
    {
        try
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dlg = new ConsentDialog(info.Ip, info.FileName, info.Size);
                await dlg.ShowDialog(this); // bloqueante hasta que el diálogo cierra (por user o auto-reject)
                return await dlg.GetResultAsync(); // B4+Q4: TCS ya resuelta, retorna inmediatamente
            });
        }
        catch { return false; }
    }

    private async Task<bool> OnApproveHighRiskAsync(FileServer.HighRiskCommand info, CancellationToken ct)
    {
        try
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (ct.IsCancellationRequested) return false;

                var target = !string.IsNullOrWhiteSpace(info.Path) ? info.Path
                    : !string.IsNullOrWhiteSpace(info.Action) ? info.Action
                    : L["st.na"];
                var commandLabel = info.Command switch
                {
                    "delete" => L["cmd.highRisk.delete"],
                    "power" => L["cmd.highRisk.power"],
                    "delta_hashes" or "put_delta_blocks" => L["cmd.highRisk.sync"],
                    _ => info.Command
                };
                var message = L.Format("dlg.highRiskApprove.body", commandLabel, info.Ip, target);
                if (!await MessageBox(message, L["dlg.highRiskApprove.title"]))
                    return false;

                var configuredPin = this.FindControl<TextBox>("txtPin")?.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(configuredPin))
                    return true;

                var pinDlg = new InputDialog(L["dlg.highRiskPin.title"], L["dlg.highRiskPin.prompt"], "");
                await pinDlg.ShowDialog(this);
                var entered = await pinDlg.GetResultAsync();
                if (string.IsNullOrWhiteSpace(entered))
                    return false;
                return FixedTimeEquals(entered.Trim(), configuredPin);
            });
        }
        catch (Exception ex)
        {
            Log.Warn("ui", "high-risk-approval-dialog-failed", new { ip = info.Ip, cmd = info.Command, error = ex.Message });
            return false;
        }
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var aa = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aa, bb);
    }

    private void ChkCompress_Changed(object? sender, RoutedEventArgs e)
    {
        _compressEnabled = (sender as CheckBox)?.IsChecked == true;
        SaveSettings(
            this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "",
            this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742");
        SetStatus(_compressEnabled ? L["st.compressOn"] : L["st.compressOff"]);
    }
    private void ChkAutoOpenLinks_Changed(object? sender, RoutedEventArgs e)
    {
        if (_securityToggleGuard) return;
        _autoOpenLinks = (sender as CheckBox)?.IsChecked == true;
        if (_safeModeEnabled && _autoOpenLinks)
        {
            _securityToggleGuard = true;
            try
            {
                _autoOpenLinks = false;
                var chk = this.FindControl<CheckBox>("chkAutoOpenLinks");
                if (chk != null) chk.IsChecked = false;
            }
            finally { _securityToggleGuard = false; }
            SetStatus(L["security.keepsLinksOff"]);
            return;
        }
        SaveSettings(
            this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "",
            this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742");
        SetStatus(_autoOpenLinks ? L["st.autoOpenLinksOn"] : L["st.autoOpenLinksOff"]);
        UpdateServerModeBadge();
    }

    private async void ChkSafeMode_Changed(object? sender, RoutedEventArgs e)
    {
        if (_securityToggleGuard) return;

        var shouldProtect = (sender as CheckBox)?.IsChecked != false;
        if (shouldProtect)
        {
            _safeModeUntilUtc = null;
            _safeModeUntilClose = false;
            _safeModeEnabled = true;
            ApplySafeModePolicy(persist: true, showStatus: true);
            return;
        }

        await StartMoreAccessFromUserAsync();
    }

    private async void DisableSafeModeTemporarily_Click(object? sender, RoutedEventArgs e)
    {
        if (_securityToggleGuard) return;
        await StartMoreAccessFromUserAsync();
    }

    private async Task StartMoreAccessFromUserAsync()
    {
        var dlg = new MoreAccessDurationDialog();
        await dlg.ShowDialog(this);
        var result = await dlg.GetResultAsync();

        switch (result)
        {
            case MoreAccessDurationDialog.MoreAccessDuration.TenMinutes:
                StartMoreAccessSession(TimeSpan.FromMinutes(10), untilClose: false);
                break;
            case MoreAccessDurationDialog.MoreAccessDuration.ThirtyMinutes:
                StartMoreAccessSession(TimeSpan.FromMinutes(30), untilClose: false);
                break;
            case MoreAccessDurationDialog.MoreAccessDuration.UntilClose:
                StartMoreAccessSession(null, untilClose: true);
                break;
            default:
                RestoreSafeModeCheck();
                break;
        }
    }

    private void StartMoreAccessSession(TimeSpan? duration, bool untilClose)
    {
        _safeModeSessionTimer?.Stop();
        _fullDiskSessionTimer?.Stop();
        _fullDiskUntilUtc = null;
        _safeModeUntilClose = untilClose;
        _safeModeUntilUtc = untilClose || duration is null ? null : DateTimeOffset.UtcNow.Add(duration.Value);

        _safeModeEnabled = false;
        _tlsEnabled = true;
        _restrictShareRoot = false;
        _safeModeNoRemoteDelete = true;
        _requireHighRiskApproval = true;
        _remotePowerEnabled = false;
        _autoOpenLinks = false;

        _server.TlsEnabled = true;
        _server.RestrictToShareRoot = false;
        _server.SafeModeNoRemoteDelete = true;
        _server.ApproveHighRisk = OnApproveHighRiskAsync;
        _server.RemotePowerEnabled = false;
        _discovery?.UpdateTlsEnabled(true);

        _securityToggleGuard = true;
        try
        {
            this.FindControl<CheckBox>("chkSafeMode")!.IsChecked = false;
            var chkTls = this.FindControl<CheckBox>("chkTls");
            if (chkTls != null) { chkTls.IsChecked = true; chkTls.IsEnabled = true; }
            var chkShareRoot = this.FindControl<CheckBox>("chkShareRoot");
            if (chkShareRoot != null) { chkShareRoot.IsChecked = false; chkShareRoot.IsEnabled = true; }
            var chkSafeDelete = this.FindControl<CheckBox>("chkSafeModeNoDelete");
            if (chkSafeDelete != null) { chkSafeDelete.IsChecked = true; chkSafeDelete.IsEnabled = true; }
            var chkRisk = this.FindControl<CheckBox>("chkRequireHighRiskApproval");
            if (chkRisk != null) { chkRisk.IsChecked = true; chkRisk.IsEnabled = true; }
            var chkAutoLinks = this.FindControl<CheckBox>("chkAutoOpenLinks");
            if (chkAutoLinks != null) { chkAutoLinks.IsChecked = false; chkAutoLinks.IsEnabled = true; }
        }
        finally
        {
            _securityToggleGuard = false;
        }

        if (!untilClose)
        {
            _safeModeSessionTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _safeModeSessionTimer.Tick -= SafeModeSessionTimer_Tick;
            _safeModeSessionTimer.Tick += SafeModeSessionTimer_Tick;
            _safeModeSessionTimer.Start();
        }

        UpdateServerModeBadge();
        SetStatus(untilClose
            ? L["security.moreAccessUntilClose"]
            : L.Format("security.moreAccessFor", (int)Math.Ceiling(duration!.Value.TotalMinutes)));
    }

    private void RestoreSafeModeCheck()
    {
        _securityToggleGuard = true;
        try
        {
            var chk = this.FindControl<CheckBox>("chkSafeMode");
            if (chk != null) chk.IsChecked = _safeModeEnabled;
        }
        finally
        {
            _securityToggleGuard = false;
        }
    }

    private void SafeModeSessionTimer_Tick(object? sender, EventArgs e)
    {
        if (_safeModeUntilClose)
            return;

        if (_safeModeUntilUtc is not null && DateTimeOffset.UtcNow < _safeModeUntilUtc.Value)
        {
            UpdateServerModeBadge();
            return;
        }

        _safeModeSessionTimer?.Stop();
        _safeModeUntilUtc = null;
        _safeModeUntilClose = false;
        _safeModeEnabled = true;
        ApplySafeModePolicy(persist: true, showStatus: true);
        SetStatus(L["security.restored"]);
    }
    private void ApplySafeModePolicy(bool persist, bool showStatus)
    {
        _securityToggleGuard = true;
        try
        {
            var chkSafeMode = this.FindControl<CheckBox>("chkSafeMode");
            if (chkSafeMode != null) chkSafeMode.IsChecked = _safeModeEnabled;
            if (_safeModeEnabled)
            {
                _safeModeSessionTimer?.Stop();
                _safeModeUntilUtc = null;
                _safeModeUntilClose = false;
                _tlsEnabled = true;
                _restrictShareRoot = true;
                _safeModeNoRemoteDelete = true;
                _requireHighRiskApproval = true;
                _remotePowerEnabled = false;
                _autoOpenLinks = false;

                _server.TlsEnabled = true;
                _server.RestrictToShareRoot = true;
                _server.SafeModeNoRemoteDelete = true;
                _server.ApproveHighRisk = OnApproveHighRiskAsync;
                _server.RemotePowerEnabled = false;

                _fullDiskUntilUtc = null;
                _fullDiskSessionTimer?.Stop();

                var chkTls = this.FindControl<CheckBox>("chkTls");
                if (chkTls != null) { chkTls.IsChecked = true; chkTls.IsEnabled = false; }
                var chkShareRoot = this.FindControl<CheckBox>("chkShareRoot");
                if (chkShareRoot != null) { chkShareRoot.IsChecked = true; chkShareRoot.IsEnabled = false; }
                var chkSafeDelete = this.FindControl<CheckBox>("chkSafeModeNoDelete");
                if (chkSafeDelete != null) { chkSafeDelete.IsChecked = true; chkSafeDelete.IsEnabled = false; }
                var chkRisk = this.FindControl<CheckBox>("chkRequireHighRiskApproval");
                if (chkRisk != null) { chkRisk.IsChecked = true; chkRisk.IsEnabled = false; }
                var chkAutoLinks = this.FindControl<CheckBox>("chkAutoOpenLinks");
                if (chkAutoLinks != null) { chkAutoLinks.IsChecked = false; chkAutoLinks.IsEnabled = false; }
            }
            else
            {
                var chkTls = this.FindControl<CheckBox>("chkTls");
                if (chkTls != null) chkTls.IsEnabled = true;
                var chkShareRoot = this.FindControl<CheckBox>("chkShareRoot");
                if (chkShareRoot != null) chkShareRoot.IsEnabled = true;
                var chkSafeDelete = this.FindControl<CheckBox>("chkSafeModeNoDelete");
                if (chkSafeDelete != null) chkSafeDelete.IsEnabled = true;
                var chkRisk = this.FindControl<CheckBox>("chkRequireHighRiskApproval");
                if (chkRisk != null) chkRisk.IsEnabled = true;
                var chkAutoLinks = this.FindControl<CheckBox>("chkAutoOpenLinks");
                if (chkAutoLinks != null) chkAutoLinks.IsEnabled = true;
                UpdateFullDiskSessionTimer();
            }
        }
        finally
        {
            _securityToggleGuard = false;
        }

        UpdateServerModeBadge();
        if (showStatus)
            SetStatus(_safeModeEnabled ? L["security.safeOn"] : L["security.safeOff"]);
        if (persist)
        {
            SaveSettings(
                this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "",
                this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742");
        }
    }

    private void UpdateFullDiskSessionTimer()
    {
        if (_safeModeEnabled || _restrictShareRoot)
        {
            _fullDiskUntilUtc = null;
            _fullDiskSessionTimer?.Stop();
            return;
        }

        _fullDiskUntilUtc = DateTimeOffset.UtcNow.Add(FullDiskSessionDuration);
        _fullDiskSessionTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _fullDiskSessionTimer.Tick -= FullDiskSessionTimer_Tick;
        _fullDiskSessionTimer.Tick += FullDiskSessionTimer_Tick;
        _fullDiskSessionTimer.Start();
    }

    private void FullDiskSessionTimer_Tick(object? sender, EventArgs e)
    {
        if (_fullDiskUntilUtc is null || DateTimeOffset.UtcNow < _fullDiskUntilUtc.Value)
            return;

        _fullDiskSessionTimer?.Stop();
        _fullDiskUntilUtc = null;
        _restrictShareRoot = true;
        _server.RestrictToShareRoot = true;
        _securityToggleGuard = true;
        try
        {
            var chkShareRoot = this.FindControl<CheckBox>("chkShareRoot");
            if (chkShareRoot != null) chkShareRoot.IsChecked = true;
        }
        finally
        {
            _securityToggleGuard = false;
        }

        SaveSettings(
            this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "",
            this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742");
        SetStatus(L["security.fullDiskExpired"]);
    }
}
