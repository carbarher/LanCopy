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
    private bool _debounceInited = false;
    private void StartBrowserAutoRefresh()
    {
        if (!_debounceInited) { InitDebounce(); _debounceInited = true; } // B4: inicializar handler una vez
        if (_browserRefreshTimer != null) return;
        _browserRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _browserRefreshTimer.Tick += BrowserAutoRefresh_Tick;
        _browserRefreshTimer.Start();
    }

    private void StopBrowserAutoRefresh()
    {
        if (_browserRefreshTimer == null) return;
        _browserRefreshTimer.Stop();
        _browserRefreshTimer.Tick -= BrowserAutoRefresh_Tick;
        _browserRefreshTimer = null;
        // P3: detener tambien el watcher local
        StopLocalWatcher();
    }

    // P3: FileSystemWatcher para detectar cambios locales sin polling cada 3s
    private void SetupLocalWatcher(string path)
    {
        lock (_watchLock)
        {
            _watcher?.Dispose();
            _watcher = null;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            try
            {
                _watcher = new FileSystemWatcher(path)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };
                _watcher.Created += OnLocalFileSystemEvent;
                _watcher.Deleted += OnLocalFileSystemEvent;
                _watcher.Renamed += OnLocalFileSystemEvent;
                _watcher.Changed += OnLocalFileSystemEvent;
            }
            catch
            {
                _watcher?.Dispose();
                _watcher = null;
            }
        }
    }

    private void StopLocalWatcher()
    {
        lock (_watchLock)
        {
            _watcher?.Dispose();
            _watcher = null;

            _debounce.Stop(); // B4: usa el campo pre-inicializado
        }
    }

    private readonly System.Timers.Timer _debounce = new(300) { AutoReset = false }; // B4: inicializado en ctor, no en handler

    private void InitDebounce()
    {
        _debounce.Elapsed += (_, _) =>
        {
            // B4: != 0 en lugar de == 1 — mismo bug que el guard del tick principal; el OR puede devolver 3
            if (Volatile.Read(ref _isWindowClosing) == 1 || _isTransferring != 0) return;
            Dispatcher.UIThread.Post(async () =>
            {
                if (Interlocked.CompareExchange(ref _isBrowserAutoRefreshRunning, 1, 0) == 0)
                {
                    try { await RefreshLocalAsync(autoRefresh: true); }
                    finally { Interlocked.Exchange(ref _isBrowserAutoRefreshRunning, 0); }
                }
            });
        };
    }

    private void OnLocalFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        // B4: _debounce inicializado en campo (thread-safe, no race en la creación)
        _debounce.Stop();
        _debounce.Start();
    }

    private async void BrowserAutoRefresh_Tick(object? sender, EventArgs e)
    {
        if (Volatile.Read(ref _isWindowClosing) == 1) return;
        // U1: != 0 en lugar de == 1 — cuando hay upload Y download simultáneos el OR da 3, no 1
        if (_isTransferring != 0 || _connectButtonIsBusy || Volatile.Read(ref _isReconnectInProgress) == 1) return;
        if (Interlocked.CompareExchange(ref _isBrowserAutoRefreshRunning, 1, 0) != 0) return;

        try
        {
            await RefreshLocalAsync(autoRefresh: true);
            await RefreshRemoteAsync(autoRefresh: true);
        }
        catch (Exception ex)
        {
            Log.Error("browser", "auto-refresh-tick", new { error = ex.Message });
        }
        finally
        {
            Interlocked.Exchange(ref _isBrowserAutoRefreshRunning, 0);
        }
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

    // ── Carpetas favoritas (accesos rápidos) ─────────────────────────────────

    private void LocalFavAdd_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_localPath)) return;
        FavoriteFoldersService.Add(_localPath);
        RefreshFavoritesCombo();
        SetStatus(L.Format("st.favAdded", _localPath));
    }

    private void LocalFavRemove_Click(object? sender, RoutedEventArgs e)
    {
        var combo = this.FindControl<ComboBox>("cmbFavorites");
        if (combo?.SelectedItem is not string path) return;
        FavoriteFoldersService.Remove(path);
        RefreshFavoritesCombo();
        SetStatus(L.Format("st.favRemoved", path));
    }

    private async void CmbFavorites_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var combo = (ComboBox)sender!;
        if (combo.SelectedItem is not string path) return;
        _localPath = path;
        await RefreshLocalAsync();
    }

    private void RefreshFavoritesCombo(string? select = null)
    {
        var combo = this.FindControl<ComboBox>("cmbFavorites");
        if (combo == null) return;
        var favs = FavoriteFoldersService.Load();
        combo.ItemsSource = null;
        combo.ItemsSource = favs;
        if (select != null && favs.Contains(select)) combo.SelectedItem = select;
        else combo.SelectedIndex = -1;
    }
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

    private async Task RefreshLocalAsync(bool autoRefresh = false)
    {
        if (!string.IsNullOrEmpty(_localPath))
        {
            LanCopy.Services.ShareRoot.SetRoot(_localPath);
        }
        try
        {
            var entries = await Task.Run(() => GetLocalEntries(_localPath));
            var signature = ComputeEntriesSignature(entries);
            if (autoRefresh && signature == Interlocked.Read(ref _localEntriesSignature)) return;
            Interlocked.Exchange(ref _localEntriesSignature, signature);
            _localItemsAll = entries;
            ApplyLocalFilter(this.FindControl<TextBox>("txtLocalFilter")?.Text?.Trim() ?? "");
            Dispatcher.UIThread.Post(UpdateLocalPath);
        }
        catch (Exception ex) { SetStatus(L.Format("st.localError", ex.Message)); }
    }

    private string _pendingLocalFilter = "";

    private void TxtLocalFilter_TextChanged(object? sender, TextChangedEventArgs e)
    {
        // Debounce 250 ms: filtra solo cuando el usuario deja de teclear (evita re-ordenar
        // la lista completa en cada pulsacion cuando hay miles de ficheros).
        _pendingLocalFilter = ((TextBox)sender!).Text?.Trim() ?? "";
        _localFilterDebounce?.Stop();
        if (_localFilterDebounce == null)
        {
            // Q2: suscribir Tick una sola vez al crear el timer (no en cada keystroke)
            _localFilterDebounce = new Avalonia.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _localFilterDebounce.Tick += OnLocalFilterTick;
        }
        _localFilterDebounce.Start();
    }

    private void OnLocalFilterTick(object? sender, EventArgs e)
    {
        _localFilterDebounce?.Stop();
        ApplyLocalFilter(_pendingLocalFilter);
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
            {
                if (d.Name.StartsWith(".")) continue;
                list.Add(new FileEntry { Name = d.Name, FullPath = d.FullName, IsDirectory = true });
            }
            foreach (var f in di.GetFiles().OrderBy(x => x.Name))
            {
                if (f.Name.StartsWith(".")) continue;
                list.Add(new FileEntry { Name = f.Name, FullPath = f.FullName, Size = f.Length, LastWriteUtcTicks = f.LastWriteTimeUtc.Ticks });
            }
        }
        catch { /* directorio inaccesible - devolver lista parcial */ }

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

    private async Task RefreshRemoteAsync(bool isRetry = false, bool autoRefresh = false)
    {
        // B3: != 0 en lugar de == 1 — OR bit a bit de _isUploading|_isDownloading puede devolver 3 en transferencia bidireccional
        if (_isTransferring != 0 && !isRetry) return;
        if (Volatile.Read(ref _isReconnectInProgress) == 1 && !isRetry) return;

        LanClient? snap;
        await _clientLock.WaitAsync();
        try { snap = _client; }
        finally { _clientLock.Release(); }
        if (snap == null) return;

        try
        {
            var entries = await snap.ListAsync(_remotePath);
            var signature = ComputeEntriesSignature(entries);
            if (autoRefresh && signature == Interlocked.Read(ref _remoteEntriesSignature)) return;
            Interlocked.Exchange(ref _remoteEntriesSignature, signature);
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
                        if (await TryReconnectAsync(ip, port, CancellationToken.None))
                        {
                            await RefreshRemoteAsync(isRetry: true, autoRefresh: autoRefresh);
                            return;
                        }
                    }
                    catch (Exception reconnectEx)
                    {
                        Log.Warn("browser", "refresh-reconnect-failed", new { error = reconnectEx.Message, ip, port });
                    }
                }
            }
            await _clientLock.WaitAsync();
            try { _client?.Dispose(); _client = null; }
            finally { _clientLock.Release(); }
            SetConnStatus(L["conn.disconnectedWord"], BrushError);
            UpdateConnectButton(isConnected: false, isBusy: false);
            UpdateRemoteCreateFolderButton(isConnected: false);
            SetStatus(L.Format("st.remoteError", L[ex.Message]));
        }
    }

    private static long ComputeEntriesSignature(List<FileEntry> entries)
    {
        unchecked
        {
            long hash = 1469598103934665603L;
            foreach (var entry in entries)
            {
                hash = (hash * 1099511628211L) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(entry.Name);
                // P1: excluir FullPath — su ruta absoluta cambia al navegar aunque el contenido sea idéntico
                hash = (hash * 1099511628211L) ^ entry.Size.GetHashCode();
                hash = (hash * 1099511628211L) ^ entry.LastWriteUtcTicks.GetHashCode();
                hash = (hash * 1099511628211L) ^ entry.IsDirectory.GetHashCode();
            }
            return hash;
        }
    }

    // ── Transfer ─────────────────────────────────────────────────────────────

    private async void CopyToRemote(object? sender, RoutedEventArgs e)
    {
        if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
        var items = GetSelectedItems("localList");
        if (items.Count == 0) { SetStatus(L["st.selectFiles"]); return; }
        try
        {
            await TransferAsync(items, isUpload: true);
        }
        catch (OperationCanceledException)
        {
            // TransferAsync already updates status/cancellation state.
        }
        catch (Exception ex)
        {
            Log.Error("transfer", "copy-to-remote-unhandled", new { error = ex.Message });
            SetStatus(L.Format("st.remoteError", ex.Message));
        }
    }

    private async void CopyToLocal(object? sender, RoutedEventArgs e)
    {
        if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
        var items = GetSelectedItems("remoteList");
        if (items.Count == 0) { SetStatus(L["st.selectRemote"]); return; }
        try
        {
            await TransferAsync(items, isUpload: false);
        }
        catch (OperationCanceledException)
        {
            // TransferAsync already updates status/cancellation state.
        }
        catch (Exception ex)
        {
            Log.Error("transfer", "copy-to-local-unhandled", new { error = ex.Message });
            SetStatus(L.Format("st.remoteError", ex.Message));
        }
    }

    private void CancelTransfer_Click(object? sender, RoutedEventArgs e)
    {
        // U2: usar Volatile.Read — los campos son escritos desde background threads con Interlocked
        if (Volatile.Read(ref _isUploading) == 1) _uploadCts.Cancel();
        if (Volatile.Read(ref _isDownloading) == 1) _downloadCts.Cancel();
    }

    private void PauseTransfer_Click(object? sender, RoutedEventArgs e)
    {
        if (Interlocked.CompareExchange(ref _isPaused, 1, 0) == 0)
        {
            // B1: verificar CurrentCount antes de Wait(0) para garantizar el drain
            // (Wait(0) retorna false silenciosamente si el semáforo ya está en 0)
            while (_pauseSemaphore.CurrentCount > 0) _pauseSemaphore.Wait(0);
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

}
