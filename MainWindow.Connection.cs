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
using LanCopy.Services.UI;
using Polly;

namespace LanCopy;

public partial class MainWindow
{
    // ── Connection ───────────────────────────────────────────────────────────

    private void StartConnectionWatchdog() => _connectionUiService.StartConnectionWatchdog();

    private void StopConnectionWatchdog() => _connectionUiService.StopConnectionWatchdog();

    private async void Connect_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (await IsConnectedAsync()) await DisconnectAsync(notifyPeer: true);
            else await TryConnectAsync();
        }
        catch (Exception ex)
        {
            Log.Warn("conn", "connect-click-unexpected", new { error = ex.Message });
            SetStatus(L[ex.Message]);
        }
    }

    private async void TxtRemoteIp_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        try { await TryConnectAsync(); }
        catch (Exception ex) { Log.Warn("conn", "ip-keydown-unexpected", new { error = ex.Message }); }
    }

    private void CopyIp_Click(object? sender, RoutedEventArgs e)
    {
        var full = this.FindControl<TextBlock>("txtMyIp")?.Text ?? "";
        var ip = full.Contains(':') ? full[..full.IndexOf(':')] : full;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null) _ = clipboard.SetTextAsync(ip);
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
        catch (Exception ex)
        {
            Log.Warn("ui", "copy-pairing-code-failed", new { error = ex.Message });
            SetStatus(L["st.codeError"]);
        }
    }

    private async void ShowTrustedDevices_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            await new TrustedDevicesDialog().ShowDialog(this);
        }
        catch (Exception ex)
        {
            Log.Warn("ui", "show-trusted-devices-failed", new { error = ex.Message });
            SetStatus("Could not open trusted devices.");
        }
    }

    private void ApplyTheme()
    {
        ThemeVariant variant;
        string icon;
        if (string.Equals(_theme, "Light", StringComparison.OrdinalIgnoreCase))
        {
            variant = ThemeVariant.Light;
            icon = "☀️";
        }
        else if (string.Equals(_theme, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            variant = ThemeVariant.Default; // sigue al OS
            icon = "🔄";
        }
        else // Dark
        {
            variant = ThemeVariant.Dark;
            icon = "🌙";
        }
        if (Application.Current != null) Application.Current.RequestedThemeVariant = variant;
        this.RequestedThemeVariant = variant;
        var btn = this.FindControl<Button>("btnTheme");
        if (btn != null) btn.Content = icon;
    }

    private void ToggleTheme_Click(object? sender, RoutedEventArgs e)
    {
        // Ciclo: Dark → Light → Auto → Dark
        _theme = _theme switch
        {
            "Dark" => "Light",
            "Light" => "Auto",
            _ => "Dark"
        };
        ApplyTheme();
        var ip = this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "";
        var port = this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742";
        SaveSettings(ip, port);
        SetStatus(L.Format("st.themeChanged", _theme));
    }

    private async Task TryConnectAsync()
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

        if (!NetworkValidation.TryParsePort(portStr, out var port))
        {
            SetStatus(L["st.invalidPort"]); return;
        }
        var discoveredPeer = _discovery?.GetPeers().FirstOrDefault(p => p.Ip == ip && p.Port == port);
        bool? useTlsOverride = null;
        if (discoveredPeer?.TlsEnabled is bool peerTls && peerTls != _tlsEnabled)
        {
            if (peerTls)
            {
                useTlsOverride = true;
                SetStatus(L["st.tlsAutoUsingSecure"]);
            }
            else
            {
                var allowed = await AskPlaintextCompatibilityAsync($"{ip}:{port}");
                if (!allowed)
                {
                    SetStatus(L["st.tlsCompatibilityCancelled"]);
                    return;
                }
                useTlsOverride = false;
                _plaintextCompatibilityApprovedPeers.Add(PeerSecurityKey(ip, port));
                SetStatus(L["st.tlsAutoUsingCompatible"]);
            }
        }

        ApplyPeerFolderState(ip, portStr);
        SaveSettings(ip, portStr);
        await ConnectAsync(ip, port, useTlsOverride: useTlsOverride);
    }

    private Task ConnectAsync(string ip, int port, bool silent = false, bool? useTlsOverride = null)
        => _connectionUiService.ConnectAsync(ip, port, silent, useTlsOverride: useTlsOverride);

    private async Task<bool> AskPlaintextCompatibilityAsync(string peer)
    {
        var dialog = new TlsCompatibilityDialog(peer);
        _ = dialog.ShowDialog<bool>(this);
        return await dialog.GetResultAsync();
    }

    private Task<bool> IsConnectedAsync() => _connectionUiService.IsConnectedAsync();

    private Task DisconnectAsync(bool silent = false, bool notifyPeer = false) =>
        _connectionUiService.DisconnectAsync(silent, notifyPeer);

    private Task TrySendDisconnectNoticeAsync() => _connectionUiService.TrySendDisconnectNoticeAsync();

    private void OnDisconnectNoticeReceived(string ip) => _connectionUiService.OnDisconnectNoticeReceived(ip);

    private void UpdateConnectButton(bool isConnected, bool isBusy)
    {
        _connectButtonIsConnected = isConnected;
        _connectButtonIsBusy = isBusy;
        Dispatcher.UIThread.Post(() =>
        {
            var btn = this.FindControl<Button>("btnConnect");
            if (btn == null) return;
            btn.Content = isConnected ? L["btn.disconnect"] : L["btn.connect"];
            ToolTip.SetTip(btn, isConnected ? L["tip.disconnect"] : L["tip.connect"]);
            btn.IsEnabled = !isBusy;
        });
    }

    private void UpdateRemoteCreateFolderButton(bool isConnected)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var btn = this.FindControl<Button>("btnRemoteCreateFolder");
            if (btn != null) btn.IsEnabled = isConnected;
        });
    }

    // ── Reconnect ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Intenta reconectar hasta 3 veces con pausa de 2 s entre intentos.
    /// </summary>
    private Task<bool> TryReconnectAsync(string ip, int port, CancellationToken ct) =>
        _connectionUiService.TryReconnectAsync(ip, port, ct);

    bool IConnectionUiHost.IsWindowClosing => Volatile.Read(ref _isWindowClosing) == 1;

    bool IConnectionUiHost.IsTransferInProgress => _isTransferring != 0;

    bool IConnectionUiHost.IsConnectButtonBusy => _connectButtonIsBusy;

    bool IConnectionUiHost.IsWatchFolderActive => _watchFolderActive;

    string IConnectionUiHost.RemotePath
    {
        get => _remotePath;
        set => _remotePath = value;
    }

    string IConnectionUiHost.RemoteIpText
    {
        get => this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "";
        set
        {
            var box = this.FindControl<TextBox>("txtRemoteIp");
            if (box != null) box.Text = value;
        }
    }

    string IConnectionUiHost.RemotePortText
    {
        get => this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742";
        set
        {
            var box = this.FindControl<TextBox>("txtRemotePort");
            if (box != null) box.Text = value;
        }
    }

    void IConnectionUiHost.SaveConnectionSettings(string ip, string portText) => SaveSettings(ip, portText);

    LanClient IConnectionUiHost.CreateLanClient(string ip, int port, bool? useTlsOverride) => MakeClient(ip, port, useTlsOverride);

    async Task<LanClient?> IConnectionUiHost.GetClientAsync(CancellationToken ct)
    {
        await _clientLock.WaitAsync(ct);
        try { return _client; }
        finally { _clientLock.Release(); }
    }

    async Task IConnectionUiHost.ReplaceClientAsync(LanClient client, CancellationToken ct)
    {
        await _clientLock.WaitAsync(ct);
        try
        {
            _client?.Dispose();
            _client = client;
        }
        finally { _clientLock.Release(); }
    }

    async Task<bool> IConnectionUiHost.TryClearClientAsync(LanClient client, CancellationToken ct)
    {
        await _clientLock.WaitAsync(ct);
        try
        {
            if (!ReferenceEquals(_client, client)) return false;
            _client.Dispose();
            _client = null;
            return true;
        }
        finally { _clientLock.Release(); }
    }

    async Task IConnectionUiHost.DisposeClientAsync(CancellationToken ct)
    {
        await _clientLock.WaitAsync(ct);
        try
        {
            _client?.Dispose();
            _client = null;
        }
        finally { _clientLock.Release(); }
    }

    async Task IConnectionUiHost.DisposeDownloadClientAsync(CancellationToken ct)
    {
        await _clientLockDown.WaitAsync(ct);
        try
        {
            _clientDown?.Dispose();
            _clientDown = null;
        }
        finally { _clientLockDown.Release(); }
    }

    void IConnectionUiHost.SetRemoteEntries(List<FileEntry> entries) =>
        Dispatcher.UIThread.Post(() =>
        {
            _remoteItemsAll = entries;
            ApplyRemoteSort();
            UpdateRemotePath();
            RememberCurrentPeerFolders();
        });

    void IConnectionUiHost.ClearRemoteEntries() =>
        Dispatcher.UIThread.Post(() =>
        {
            RememberCurrentPeerFolders(force: true);
            _remotePath = "";
            _remoteItemsAll = [];
            _remoteItems.ReplaceAll([]);
            Interlocked.Exchange(ref _remoteEntriesSignature, 0);
            UpdateRemotePath();
        });

    void IConnectionUiHost.SetStatus(string text) => SetStatus(text);

    void IConnectionUiHost.SetConnectionStatus(string text, SolidColorBrush brush) => SetConnStatus(text, brush);

    void IConnectionUiHost.SetConnectButtonState(bool isConnected, bool isBusy) => UpdateConnectButton(isConnected, isBusy);

    void IConnectionUiHost.SetRemoteCreateFolderEnabled(bool isEnabled) => UpdateRemoteCreateFolderButton(isEnabled);

    void IConnectionUiHost.SetReconnectInProgress(bool isInProgress) =>
        Interlocked.Exchange(ref _isReconnectInProgress, isInProgress ? 1 : 0);

    void IConnectionUiHost.StopWatchFolder() => StopWatch();

    void IConnectionUiHost.CancelUploadTransfers()
    {
        try { _uploadCts.Cancel(); }
        catch (ObjectDisposedException ex) { Log.Debug("conn", "upload-cts-cancel-disposed-on-disconnect", new { error = ex.Message }); }
        catch (Exception ex) { Log.Warn("conn", "upload-cts-cancel-failed", new { error = ex.Message }); }
    }

    void IConnectionUiHost.CancelDownloadTransfers()
    {
        try { _downloadCts.Cancel(); }
        catch (ObjectDisposedException ex) { Log.Debug("conn", "download-cts-cancel-disposed-on-disconnect", new { error = ex.Message }); }
        catch (Exception ex) { Log.Warn("conn", "download-cts-cancel-failed", new { error = ex.Message }); }
    }

}


