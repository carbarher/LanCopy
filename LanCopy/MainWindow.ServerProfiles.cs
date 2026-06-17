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
    // ── Feature 3: Perfiles de conexión ─────────────────────────────────────

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
        ProfileStore.Upsert(_profiles, new ConnectionProfile(name, ip, port, pin, _tlsEnabled, _compressEnabled));
        SaveSettings(ip, port);
        RefreshProfilesCombo(name);
        SetStatus(L.Format("st.profileSaved", name));
    }

    private void DeleteProfile_Click(object? sender, RoutedEventArgs e)
    {
        var combo = this.FindControl<ComboBox>("cmbProfiles");
        if (combo?.SelectedItem is not string name) return;
        ProfileStore.Remove(_profiles, name);
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
        var names = ProfileStore.Names(_profiles).ToList();
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
        => FileSorter.Sort(items, field, asc);

    // ── Feature 7: Sparkline de velocidad ────────────────────────────────────

    private void UpdateSparkline(double bytesPerSec)
    {
        var mbps = bytesPerSec / (1024.0 * 1024.0);
        if (_speedHistory.Count >= SparklineLen) _speedHistory.Dequeue();
        if (bytesPerSec > 0) _speedHistory.Enqueue(mbps);
        if (_speedHistory.Count == 0) return;

        var spark = SpeedSparkline.Render(_speedHistory);

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
        if (_watcher != null)
        {
            _watcher.Changed -= OnWatcherEvent;
            _watcher.Created -= OnWatcherEvent;
            _watcher.Dispose();
        }
        _watcher = null;
        lock (_watchLock) { _watchDebounce?.Dispose(); _watchDebounce = null; }
        _watcherActive = false;

        var btn = this.FindControl<Button>("btnWatch");
        if (btn != null) { btn.Content = L["btn.watch"]; btn.Background = SolidColorBrush.Parse("#795548"); }
        var tb = this.FindControl<TextBlock>("txtWatchStatus");
        if (tb != null) tb.Text = "";
        SetStatus(L["st.watchStopped"]);
    }

    private void OnWatcherEvent(object sender, FileSystemEventArgs e)
    {
        // FileSystemWatcher dispara en hilos del pool: serializar el reinicio del debounce.
        var changedPath = e.FullPath;
        lock (_watchLock)
        {
            _watchDebounce?.Dispose();
            var timer = new System.Timers.Timer(700) { AutoReset = false };
            // Bug fix: no async void en Timer.Elapsed — las excepciones no capturadas crashean.
            // Usamos Task.Run para que el async sea manejable.
            timer.Elapsed += (_, _) =>
            {
                _ = Task.Run(async () =>
                {
                    if (!File.Exists(changedPath)) return;
                    // SEGURIDAD: no transferir ficheros bajo junctions (podrían ser externos a la carpeta watched).
                    if (SafeFileOps.ContainsReparsePoint(changedPath)) return;
                    try
                    {
                        var fi = new FileInfo(changedPath);
                        var entry = new FileEntry { Name = fi.Name, FullPath = fi.FullName, Size = fi.Length };
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            SetStatus(L.Format("st.changeDetected", fi.Name));
                            _ = TransferAsync(new List<FileEntry> { entry }, isUpload: true);
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Watch] {ex.Message}");
                        SetStatus(L.Format("st.watchError", ex.Message));
                    }
                });
            };
            _watchDebounce = timer;
            timer.Start();
        }
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

            var remoteDict = remoteFiles.ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);

            // Lista local
            var localFiles = await Task.Run(() =>
            {
                if (!Directory.Exists(_localPath)) return new List<FileEntry>();
                return Directory.EnumerateFiles(_localPath, "*", SearchOption.AllDirectories)
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


