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

    private void SaveQueue(List<(FileEntry entry, string destPath)> files, bool isUpload, string ip, int port, int attempt = 0)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(QueuePath)!);
            var item = new Models.QueueItem(
                files.Select(f => f.entry.FullPath).ToArray(),
                files.Select(f => f.destPath).ToArray(),
                isUpload, ip, port, DateTime.UtcNow.ToString("O"), attempt);

            var tmp = QueuePath + ".tmp";
            File.WriteAllText(tmp, System.Text.Json.JsonSerializer.Serialize(item));
            File.Move(tmp, QueuePath, overwrite: true);
        }
        catch
        {
            // no bloquear transferencia por error de cola
        }
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
            if (item == null || item.FilePaths.Length == 0 || item.FilePaths.Length != item.DestPaths.Length)
            {
                ClearQueue();
                return;
            }

            var valid = item.FilePaths
                .Select((p, i) => new { Path = p, Dest = item.DestPaths[i] })
                .Where(x => !string.IsNullOrWhiteSpace(x.Path) && !string.IsNullOrWhiteSpace(x.Dest))
                .Where(x => item.IsUpload ? File.Exists(x.Path) : true)
                .ToList();
            if (valid.Count == 0) { ClearQueue(); return; }

            var result = await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dlg = new Avalonia.Controls.Window
                {
                    Title = L["queue.title"],
                    Width = 460,
                    Height = 180,
                    Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2D2D30")),
                    WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner
                };
                var whenTxt = DateTime.TryParse(item.CreatedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var ts)
                    ? ts.ToLocalTime().ToString("dd/MM HH:mm") : "?";
                var msg = new TextBlock
                {
                    Text = L.Format("queue.body", valid.Count, item.IsUpload ? L["word.upload"] : L["word.download"], $"{item.RemoteIp}:{item.RemotePort}") + $"\n(guardada: {whenTxt}, intento #{Math.Max(1, item.Attempt + 1)})",
                    Foreground = Avalonia.Media.Brushes.White,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Avalonia.Thickness(16, 16, 16, 8)
                };
                var resume = new Avalonia.Controls.Button { Content = L["queue.resume"], Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#28A745")), Foreground = Avalonia.Media.Brushes.White, Padding = new Avalonia.Thickness(16, 6), Margin = new Avalonia.Thickness(8, 0) };
                var discard = new Avalonia.Controls.Button { Content = L["queue.discard"], Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#6C757D")), Foreground = Avalonia.Media.Brushes.White, Padding = new Avalonia.Thickness(16, 6) };
                bool ok = false;
                resume.Click += (_, _) => { ok = true; dlg.Close(); };
                discard.Click += (_, _) => { ok = false; dlg.Close(); };
                var row = new Avalonia.Controls.StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Children = { resume, discard } };
                dlg.Content = new Avalonia.Controls.StackPanel { Children = { msg, row } };
                await dlg.ShowDialog(this);
                return ok;
            });

            if (!result) { ClearQueue(); return; }

            if (item.Attempt >= 3)
            {
                SetStatus("Cola pendiente descartada: superó el máximo de reintentos");
                ClearQueue();
                return;
            }

            // actualizar intento antes de relanzar
            SaveQueue(valid.Select(v => (new FileEntry { Name = Path.GetFileName(v.Path), FullPath = v.Path }, v.Dest)).ToList(), item.IsUpload, item.RemoteIp, item.RemotePort, item.Attempt + 1);

            this.FindControl<TextBox>("txtRemoteIp")!.Text = item.RemoteIp;
            this.FindControl<TextBox>("txtRemotePort")!.Text = item.RemotePort.ToString();
            await ConnectAsync(item.RemoteIp, item.RemotePort);

            var entries = valid.Select(v => new FileEntry { Name = Path.GetFileName(v.Path), FullPath = v.Path }).ToList();
            _ = TransferAsync(entries, item.IsUpload);
        }
        catch
        {
            try
            {
                var bad = QueuePath + ".bad-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                if (File.Exists(QueuePath)) File.Move(QueuePath, bad, overwrite: true);
            }
            catch { ClearQueue(); }
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
            if (_discovery != null) _discovery.PeersChanged -= UpdatePeersCombo;
            _discovery?.Stop();
            _server.Stop();
            _server.Start(port);
            _server.TransferProgress += OnServerTransferProgress;
            _server.TextReceived += OnTextReceived;
            _server.DisconnectNoticeReceived += OnDisconnectNoticeReceived;
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
    private List<string> _knownPeerIps = [];

    private void UpdatePeersCombo()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var combo = this.FindControl<ComboBox>("cmbPeers");
            if (combo == null) return;
            var peers = _discovery?.GetPeers() ?? [];
            combo.ItemsSource = peers.Select(p => $"{p.Name} ({p.Ip}:{p.Port})").ToList();

            // Notificar cuando aparece un nuevo peer
            var newPeers = peers.Where(p => !_knownPeerIps.Contains(p.Ip)).ToList();
            foreach (var p in newPeers)
            {
                var msg = $"🖥 {p.Name} ({p.Ip}) disponible en la red";
                SetStatus(msg);
                _notifManager?.Show(new Notification("LanCopy", msg, NotificationType.Information));
            }
            _knownPeerIps = peers.Select(p => p.Ip).ToList();
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
        Dispatcher.UIThread.Post(() =>
        {
            StopStatusBlink();
            if (_txtStatus != null)
            {
                _txtStatus.Text = text;
                _txtStatus.ClearValue(TextBlock.ForegroundProperty); // vuelve al color normal del tema
            }
        });
    }

    // UX: mensaje que requiere lectura del usuario -> color llamativo parpadeante
    // (alterna entre el color vivo y su version apagada). Cualquier SetStatus normal lo detiene.
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

}
