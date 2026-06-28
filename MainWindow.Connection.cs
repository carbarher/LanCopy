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
    // ── Connection ───────────────────────────────────────────────────────────

    private void StartConnectionWatchdog()
    {
        if (_connectionWatchdogTimer != null) return;
        _connectionWatchdogTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _connectionWatchdogTimer.Tick += ConnectionWatchdog_Tick;
        _connectionWatchdogTimer.Start();
    }

    private void StopConnectionWatchdog()
    {
        if (_connectionWatchdogTimer == null) return;
        _connectionWatchdogTimer.Stop();
        _connectionWatchdogTimer.Tick -= ConnectionWatchdog_Tick;
        _connectionWatchdogTimer = null;
    }

    private async void ConnectionWatchdog_Tick(object? sender, EventArgs e)
    {
        if (Volatile.Read(ref _isWindowClosing) == 1) return;
        if (_connectButtonIsBusy || _isTransferring == 1) return;
        if (Interlocked.CompareExchange(ref _isConnectionProbeRunning, 1, 0) != 0) return;

        try
        {
            LanClient? snap;
            await _clientLock.WaitAsync();
            try { snap = _client; }
            finally { _clientLock.Release(); }
            if (snap == null) return;

            using var probeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            _ = await snap.ListAsync("", probeCts.Token);
        }
        catch (Exception)
        {
            if (Volatile.Read(ref _isWindowClosing) == 1 || _isTransferring == 1) return;
            await DisconnectAsync(silent: true);
            SetStatus(L["st.disconnected"]);
        }
        finally
        {
            Interlocked.Exchange(ref _isConnectionProbeRunning, 0);
        }
    }

    private async void Connect_Click(object? sender, RoutedEventArgs e)
    {
        if (await IsConnectedAsync()) await DisconnectAsync(notifyPeer: true);
        else await TryConnectAsync();
    }

    private async void TxtRemoteIp_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await TryConnectAsync();
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
        SaveSettings(ip, portStr);
        await ConnectAsync(ip, port);
    }

    private async Task ConnectAsync(string ip, int port, bool silent = false)
    {
        UpdateConnectButton(isConnected: false, isBusy: true);
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
            UpdateConnectButton(isConnected: true, isBusy: false);
            UpdateRemoteCreateFolderButton(isConnected: true);
            if (!silent) SetStatus(L.Format("st.connected", $"{ip}:{port}"));
        }
        catch (Exception ex)
        {
            await _clientLock.WaitAsync();
            try { _client?.Dispose(); _client = null; }
            finally { _clientLock.Release(); }
            SetConnStatus(L["conn.error"], BrushError);
            UpdateConnectButton(isConnected: false, isBusy: false);
            UpdateRemoteCreateFolderButton(isConnected: false);
            if (!silent) SetStatus(L.Format("st.connectFailed", $"{ip}:{port}", L[ex.Message]));
        }
    }

    private async Task<bool> IsConnectedAsync()
    {
        await _clientLock.WaitAsync();
        try { return _client != null; }
        finally { _clientLock.Release(); }
    }

    private async Task DisconnectAsync(bool silent = false, bool notifyPeer = false)
    {
        UpdateConnectButton(isConnected: true, isBusy: true);
        if (notifyPeer) await TrySendDisconnectNoticeAsync();
        _uploadCts.Cancel();
        _downloadCts.Cancel();

        await _clientLock.WaitAsync();
        try { _client?.Dispose(); _client = null; }
        finally { _clientLock.Release(); }

        await _clientLockDown.WaitAsync();
        try { _clientDown?.Dispose(); _clientDown = null; }
        finally { _clientLockDown.Release(); }

        Dispatcher.UIThread.Post(() =>
        {
            _remotePath = "";
            _remoteItemsAll = [];
            _remoteItems.ReplaceAll([]);
            UpdateRemotePath();
        });

        SetConnStatus(L["conn.disconnectedWord"], BrushError);
        UpdateConnectButton(isConnected: false, isBusy: false);
        UpdateRemoteCreateFolderButton(isConnected: false);
        if (!silent) SetStatus(L["st.disconnected"]);
    }

    private async Task TrySendDisconnectNoticeAsync()
    {
        var ip = this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "";
        var portText = this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742";
        if (string.IsNullOrWhiteSpace(ip) || !int.TryParse(portText, out var port)) return;

        try
        {
            using var cli = MakeClient(ip, port);
            await cli.SendDisconnectNoticeAsync();
        }
        catch { }
    }

    private void OnDisconnectNoticeReceived(string ip)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            var remoteIp = this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "";
            if (!string.Equals(remoteIp, ip, StringComparison.OrdinalIgnoreCase)) return;
            if (!await IsConnectedAsync()) return;

            await DisconnectAsync(silent: true);
            SetStatus(L["st.disconnected"]);
        });
    }

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
                LanClient? snap;
                await _clientLock.WaitAsync(ct);
                try { snap = _client; }
                finally { _clientLock.Release(); }
                _ = await snap!.ListAsync("");

                SetConnStatus(L["conn.reconnectedWord"], BrushConnected);
                UpdateConnectButton(isConnected: true, isBusy: false);
                UpdateRemoteCreateFolderButton(isConnected: true);
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
        UpdateConnectButton(isConnected: false, isBusy: false);
        UpdateRemoteCreateFolderButton(isConnected: false);
        SetStatus(L["st.reconnectFailed"]);
        return false;
    }

}
