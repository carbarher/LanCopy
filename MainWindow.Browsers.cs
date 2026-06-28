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

    private string _pendingLocalFilter = "";

    private void TxtLocalFilter_TextChanged(object? sender, TextChangedEventArgs e)
    {
        // Debounce 250 ms: filtra solo cuando el usuario deja de teclear (evita re-ordenar
        // la lista completa en cada pulsacion cuando hay miles de ficheros).
        _pendingLocalFilter = ((TextBox)sender!).Text?.Trim() ?? "";
        _localFilterDebounce?.Stop();
        _localFilterDebounce ??= new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _localFilterDebounce.Tick -= OnLocalFilterTick;
        _localFilterDebounce.Tick += OnLocalFilterTick;
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
                list.Add(new FileEntry { Name = d.Name, FullPath = d.FullName, IsDirectory = true });
            foreach (var f in di.GetFiles().OrderBy(x => x.Name))
                list.Add(new FileEntry { Name = f.Name, FullPath = f.FullName, Size = f.Length, LastWriteUtcTicks = f.LastWriteTimeUtc.Ticks });
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

        LanClient? snap;
        await _clientLock.WaitAsync();
        try { snap = _client; }
        finally { _clientLock.Release(); }
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
                        UpdateConnectButton(isConnected: true, isBusy: false);
                        UpdateRemoteCreateFolderButton(isConnected: true);
                        return;
                    }
                    catch { }
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

}
