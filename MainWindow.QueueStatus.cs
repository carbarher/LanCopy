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
    // ── Cola persistente (Feature 3) ─────────────────────────────────────────

    private static List<(string Path, string Dest)> DeduplicateQueuePairs(IEnumerable<(string Path, string Dest)> items)
        => items
            .Where(x => !string.IsNullOrWhiteSpace(x.Path) && !string.IsNullOrWhiteSpace(x.Dest))
            .GroupBy(x => $"{x.Path}|{x.Dest}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .ToList();

    // F3: actualizar el panel visual de cola
    private void UpdateQueuePanel(int pendingCount, IEnumerable<string>? names = null)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var expander = this.FindControl<Avalonia.Controls.Expander>("queueExpander");
            var countLbl = this.FindControl<TextBlock>("txtQueueCount");
            var listBox = this.FindControl<Avalonia.Controls.ListBox>("lstQueue");
            if (expander == null) return;

            expander.IsVisible = pendingCount > 0;
            if (countLbl != null) countLbl.Text = pendingCount > 0 ? $"({pendingCount})" : "";
            if (listBox != null && names != null)
                listBox.ItemsSource = names.ToList();
        });
    }

    private void BtnClearQueue_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ClearQueue();
        UpdateQueuePanel(0);
        SetStatus(L["st.queueCleared"]);
    }

    private static void ClearQueue()
    {
        try { if (File.Exists(QueuePath)) File.Delete(QueuePath); }
        catch (Exception ex) { Log.Warn("queue", "clear-failed", new { error = ex.Message }); }
    }
    private void SaveQueue(List<(FileEntry entry, string destPath)> files, bool isUpload, string ip, int port, int attempt = 0)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(QueuePath)!);
            var uniquePairs = DeduplicateQueuePairs(files.Select(f => (f.entry.FullPath, f.destPath)));
            if (uniquePairs.Count == 0)
            {
                ClearQueue();
                UpdateQueuePanel(0);
                return;
            }
            var item = new Models.QueueItem(
                uniquePairs.Select(x => x.Path).ToArray(),
                uniquePairs.Select(x => x.Dest).ToArray(),
                isUpload, ip, port, DateTime.UtcNow.ToString("O"), attempt);

            var tmp = QueuePath + ".tmp";
            File.WriteAllText(tmp, System.Text.Json.JsonSerializer.Serialize(item));
            File.Move(tmp, QueuePath, overwrite: true);

            // F3: actualizar panel visual de cola
            UpdateQueuePanel(uniquePairs.Count, uniquePairs.Select(x => Path.GetFileName(x.Path)));
        }
        catch (Exception ex)
        {
            Log.Warn("queue", "save-failed", new { error = ex.Message, isUpload, ip, port, attempt, count = files.Count });
        }
    }


    private async Task CheckPendingQueueAsync()
    {
        if (!File.Exists(QueuePath)) return;
        try
        {
            var json = await File.ReadAllTextAsync(QueuePath);
            var item = System.Text.Json.JsonSerializer.Deserialize<Models.QueueItem>(json);
            if (item == null || item.FilePaths.Length == 0 || item.FilePaths.Length != item.DestPaths.Length)
            {
                ClearQueue();
                return;
            }

            var valid = DeduplicateQueuePairs(item.FilePaths
                .Select((p, i) => new { Path = p, Dest = item.DestPaths[i] })
                .Where(x => !string.IsNullOrWhiteSpace(x.Path) && !string.IsNullOrWhiteSpace(x.Dest))
                .Where(x => item.IsUpload ? File.Exists(x.Path) : true)
                .Select(x => (x.Path, x.Dest)))
                .Select(x => new { Path = x.Path, Dest = x.Dest })
                .ToList();
            if (valid.Count == 0) { ClearQueue(); return; }

            // U4: comprobar max intentos ANTES de mostrar el dialogo
            if (item.Attempt >= 3)
            {
                SetStatus(L["st.queueDiscarded"]);
                ClearQueue();
                return;
            }

            // F8/U1: Usar dialogo AXAML que hereda el tema de la app
            var whenTxt = DateTime.TryParse(item.CreatedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var ts)
                ? ts.ToLocalTime().ToString(System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern + " HH:mm") : "?"; // U4: respeta locale
            var dialogMsg = L.Format("queue.body",
                valid.Count,
                item.IsUpload ? L["word.upload"] : L["word.download"],
                $"{item.RemoteIp}:{item.RemotePort}")
                + "\n" + L.Format("queue.savedAt", whenTxt, Math.Max(1, item.Attempt + 1));

            QueueResumeDialog.QueueResumeAction result = QueueResumeDialog.QueueResumeAction.Discard;
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dlg = new QueueResumeDialog(
                    dialogMsg,
                    L["queue.resume"],
                    L["queue.resumeSkipSameSize"],
                    L["queue.discard"]);
                await dlg.ShowDialog(this);
                result = dlg.Result;
            });

            if (result == QueueResumeDialog.QueueResumeAction.Discard) { ClearQueue(); return; }

            // actualizar intento antes de relanzar
            SaveQueue(valid.Select(v => (new FileEntry { Name = Path.GetFileName(v.Path), FullPath = v.Path }, v.Dest)).ToList(), item.IsUpload, item.RemoteIp, item.RemotePort, item.Attempt + 1);

            this.FindControl<TextBox>("txtRemoteIp")!.Text = item.RemoteIp;
            this.FindControl<TextBox>("txtRemotePort")!.Text = item.RemotePort.ToString();
            await ConnectAsync(item.RemoteIp, item.RemotePort);
            if (!await IsConnectedAsync())
            {
                SetStatusAlert(L.Format("st.queueConnectFailedKept", $"{item.RemoteIp}:{item.RemotePort}", L["st.reconnectFailed"]));
                return;
            }

            var queuedFiles = valid.Select(v =>
            {
                long size = 0;
                if (item.IsUpload)
                {
                    try { size = new FileInfo(v.Path).Length; } catch { size = 0; }
                }

                return (
                    new FileEntry
                    {
                        Name = Path.GetFileName(v.Path),
                        FullPath = v.Path,
                        Size = size
                    },
                    v.Dest
                );
            }).ToList();

            var entries = queuedFiles.Select(x => x.Item1).ToList();
            var forceSkipSameSize = result == QueueResumeDialog.QueueResumeAction.ResumeSkipSameSize;
            var startupSkipSet = await CheckOverwriteAsync(
                queuedFiles,
                item.IsUpload,
                forceRemoteProbe: item.IsUpload,
                forcedAction: forceSkipSameSize ? ConfirmDialog.OverwriteAction.SkipSameSize : null);
            if (startupSkipSet == null)
            {
                SetStatusAlert(L["st.queueKeptRetry"]);
                return;
            }

            await TransferAsync(entries, item.IsUpload, queuedFiles, precomputedSkipSet: startupSkipSet);
        }
        catch (Exception ex)
        {
            Log.Warn("queue", "check-pending-failed", new { error = ex.Message });
            try
            {
                var bad = QueuePath + ".bad-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                if (File.Exists(QueuePath)) File.Move(QueuePath, bad, overwrite: true);
            }
            catch (Exception moveEx)
            {
                Log.Warn("queue", "mark-bad-failed", new { error = moveEx.Message });
                ClearQueue();
            }
        }
    }

    // ── UDP Peer Discovery (Feature 12) ──────────────────────────────────────

    private static int ReadSavedLocalPort()
        => StartupSettings.Load(SettingsPath).LocalPort;

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
        if (!NetworkValidation.TryParsePort(box.Text, out var port))
        {
            SetStatus(L["st.localPortInvalid"]);
            box.Text = _server.Port.ToString();
            return;
        }
        if (port == _server.Port) { SetStatus(L.Format("st.alreadyListening", port)); return; }

        try
        {
            // Bug fix: desuscribir antes de recrear para evitar eventos duplicados
            _server.TextReceived -= OnTextReceived;
            _server.DisconnectNoticeReceived -= OnDisconnectNoticeReceived;
            _server.TransferProgress -= OnServerTransferProgress;
            if (_discovery != null) _discovery.PeersChanged -= OnPeersChanged;
            _discovery?.Stop();
            _server.Stop();
            _server.Start(port);
            _server.TransferProgress += OnServerTransferProgress;
            _server.TextReceived += OnTextReceived;
            _server.DisconnectNoticeReceived += OnDisconnectNoticeReceived;
            this.FindControl<TextBlock>("txtMyIp")!.Text = $"{_server.LocalIp}:{_server.Port}";

            _discovery = new PeerDiscovery(_server.LocalIp, _server.Port);
            _discovery.PeersChanged += OnPeersChanged;
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
    private HashSet<string> _knownPeerIps = []; // P3: was List<string> � O(1) Contains

    private void OnPeersChanged()
    {
        UpdatePeersCombo();
        UpdateBroadcastButton();
    }

    private void UpdatePeersCombo()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var combo = this.FindControl<ComboBox>("cmbPeers");
            if (combo == null) return;
            var peers = _discovery?.GetPeers() ?? [];
            var newItems = peers.Select(p => $"{p.Name} ({p.Ip}:{p.Port})").ToList();

            var prev = combo.SelectedItem as string;
            combo.ItemsSource = newItems;

            // Seleccionar peer guardado o el primero disponible
            if (newItems.Count > 0)
            {
                string toSelect;
                if (prev != null && newItems.Contains(prev))
                    toSelect = prev;
                else
                {
                    var savedIp = this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "";
                    toSelect = (!string.IsNullOrEmpty(savedIp) ? newItems.FirstOrDefault(i => i.Contains(savedIp)) : null) ?? newItems[0];
                }
                if (combo.SelectedItem as string != toSelect)
                    combo.SelectedItem = toSelect;
            }

            // AUTO-CONECTAR cuando aparece un peer nuevo y no hay conexion activa
            var newPeers = peers.Where(p => !_knownPeerIps.Contains(p.Ip)).ToList();
            // B8: revisar _isReconnectInProgress para evitar race con watchdog
            if (newPeers.Count > 0 && _client == null && _isReconnectInProgress == 0)
            {
                var savedIp2 = this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "";
                var target = (!string.IsNullOrEmpty(savedIp2)
                    ? peers.FirstOrDefault(p => p.Ip == savedIp2)
                    : null) ?? newPeers[0];
                _ = ConnectAsync(target.Ip, target.Port);
            }

            // Notificar peer nuevo
            foreach (var p in newPeers)
            {
                var msg = L.Format("st.peerDiscovered", p.Name, p.Ip);
                SetStatus(msg);
                _notifManager?.Show(new Notification("LanCopy", msg, NotificationType.Information));
            }
            _knownPeerIps = peers.Select(p => p.Ip).ToHashSet(); // P3: HashSet<string>
        });
    }
    private void CmbPeers_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var combo = (ComboBox)sender!;
        if (combo.SelectedItem is not string item || string.IsNullOrEmpty(item)) return;
        var peers = _discovery?.GetPeers() ?? [];
        var selected = peers.FirstOrDefault(p => item.Contains(p.Ip));
        string ip, port;
        if (selected != null)
        {
            ip = selected.Ip;
            port = selected.Port.ToString();
        }
        else
        {
            // Fallback: parsear IP:puerto directamente del texto "Nombre (IP:Puerto)"
            var m = System.Text.RegularExpressions.Regex.Match(item, @"\((\d+\.\d+\.\d+\.\d+):(\d+)\)");
            if (!m.Success) return;
            ip = m.Groups[1].Value;
            port = m.Groups[2].Value;
        }
        this.FindControl<TextBox>("txtRemoteIp")!.Text = ip;
        this.FindControl<TextBox>("txtRemotePort")!.Text = port;
        SaveSettingsDeferred(ip, port); // P6: debounced - evitar escrituras rapidas
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
        catch (Exception ex)
        {
            Log.Warn("browser", "dragdrop-read-failed", new { error = ex.Message });
        }
        var fileList = files?.ToList();
        if (fileList == null || fileList.Count == 0) return;

        var dropDirs = fileList.OfType<IStorageFolder>().ToList();

        // Si solo hay UNA carpeta y es el unico item -> navegar (comportamiento original)
        if (dropDirs.Count == 1 && fileList.Count == 1)
        {
            var navPath = dropDirs[0].TryGetLocalPath();
            if (navPath != null) { _localPath = navPath; await RefreshLocalAsync(); return; }
        }

        // U6: mezcla de carpetas + archivos -> enviar todo al remoto
        if (_client == null) { SetStatus(L["st.connectBeforeDrag"]); return; }
        var entries = fileList
            .Select(f => (item: f, path: f.TryGetLocalPath()))
            .Where(x => x.path != null)
            .Select(x =>
            {
                if (x.item is IStorageFolder)
                {
                    if (!Directory.Exists(x.path!)) return null;
                    var di = new DirectoryInfo(x.path!);
                    // IsDirectory=true -> ExpandItemsAsync expandira recursivamente
                    return new FileEntry { Name = di.Name, FullPath = di.FullName, IsDirectory = true };
                }
                if (!System.IO.File.Exists(x.path!)) return null;
                var fi = new FileInfo(x.path!);
                return new FileEntry { Name = fi.Name, FullPath = fi.FullName, Size = fi.Length };
            })
            .Where(e => e != null).Select(e => e!)
            .ToList();
        if (entries.Count > 0)
            await TransferAsync(entries, isUpload: true);
    }

    // ── UI helpers ───────────────────────────────────────────────────────────

    // F6: Descargar archivos seleccionados del panel remoto (pull download)
    private async void DownloadSelected_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selected = this.FindControl<ListBox>("remoteList")
            ?.SelectedItems?.OfType<FileEntry>()
            .Where(f => f.Name != ".." && !f.IsDirectory).ToList() ?? [];
        if (selected.Count == 0) { SetStatus(L["st.selectFiles"]); return; }
        if (_client == null) { SetStatus(L["st.noConnection"]); return; }
        await TransferAsync(selected, isUpload: false);
    }

    private void OnRemoteListPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // F6: placeholder para D&D remoto. La descarga real se hace via DownloadSelected_Click.
    }

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
        if (files.Count > 0) parts.Add(L.Format("sel.files", files.Count, FileEntry.FormatSize(files.Sum(f => f.Size))));
        if (dirs.Count > 0) parts.Add(L.Format("sel.folders", dirs.Count));
        SetStatus(L.Format("st.selected", string.Join(", ", parts)));
    }

    private void UpdateLocalPath()
    {
        if (_txtLocalPath != null)
            _txtLocalPath.Text = string.IsNullOrEmpty(_localPath) ? L["path.drives"] : _localPath;
    }

    private void UpdateRemotePath()
    {
        if (_txtRemotePath != null)
            _txtRemotePath.Text = string.IsNullOrEmpty(_remotePath) ? L["path.drives"] : _remotePath;
    }

    private void SetStatus(string text)
    {
        _progressWin?.SetLine(text);
        void DoSet()
        {
            StopStatusBlink();
            if (_txtStatus != null)
            {
                _txtStatus.Text = text;
                _txtStatus.ClearValue(TextBlock.ForegroundProperty);
            }
        }
        if (Dispatcher.UIThread.CheckAccess()) DoSet();
        else Dispatcher.UIThread.Post(DoSet);
    }

    private void SetStatusAlert(string text, string vivid = "#FF3B30", string dim = "#7A201C")
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_txtStatus == null) return;
            StopStatusBlink();
            _txtStatus.Text = text;
            var vb = SolidColorBrush.Parse(vivid);
            var db = SolidColorBrush.Parse(dim);
            _txtStatus.Foreground = vb;
            _statusBlinkOn = true;
            _statusBlinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(550) };
            _statusBlinkTimer.Tick += (_, _) =>
            {
                _statusBlinkOn = !_statusBlinkOn;
                if (_txtStatus != null) _txtStatus.Foreground = _statusBlinkOn ? vb : db;
            };
            _statusBlinkTimer.Start();
        });
    }

    private void StopStatusBlink()
    {
        if (_statusBlinkTimer != null) { _statusBlinkTimer.Stop(); _statusBlinkTimer = null; }
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
        void DoSet()
        {
            var sendEnabled = enabled && _isDownloading == 0;
            var receiveEnabled = enabled && _isUploading == 0;
            var anyCancelable = cancelEnabled || _isUploading == 1 || _isDownloading == 1;
            var send = _btnSend ?? this.FindControl<Button>("btnSend");
            var receive = _btnReceive ?? this.FindControl<Button>("btnReceive");
            var cancel = _btnCancel ?? this.FindControl<Button>("btnCancel");
            var pause = _btnPause ?? this.FindControl<Button>("btnPause");
            var resume = _btnResume ?? this.FindControl<Button>("btnResume");
            if (send != null)
            {
                send.IsEnabled = sendEnabled;
                ToolTip.SetTip(send, (!sendEnabled && _isDownloading != 0) ? L["tip.sendDisabledDownload"] : L["tip.send"]);
            }
            if (receive != null)
            {
                receive.IsEnabled = receiveEnabled;
                ToolTip.SetTip(receive, (!receiveEnabled && _isUploading != 0) ? L["tip.receiveDisabledUpload"] : L["tip.receive"]);
            }
            if (cancel != null) cancel.IsEnabled = anyCancelable;
            if (pause != null) pause.IsEnabled = cancelEnabled;
            if (resume != null) resume.IsEnabled = false;
        }
        if (Dispatcher.UIThread.CheckAccess()) DoSet();
        else Dispatcher.UIThread.Post(DoSet);
    }

}
