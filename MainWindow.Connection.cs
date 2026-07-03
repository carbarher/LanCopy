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
using Polly;

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
        if (_connectButtonIsBusy || _isTransferring != 0 || Volatile.Read(ref _isReconnectInProgress) == 1) return;
        if (Interlocked.CompareExchange(ref _isConnectionProbeRunning, 1, 0) != 0) return;

        try
        {
            LanClient? snap;
            await _clientLock.WaitAsync();
            try { snap = _client; }
            finally { _clientLock.Release(); }
            if (snap == null) return;

            // U4: usar GetHealthAsync como keepalive (no enumera directorio, solo consulta métricas)
            using var probeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            _ = await snap.GetHealthAsync(probeCts.Token);
        }
        catch (Exception ex)
        {
            Log.Debug("conn", "watchdog-health-probe-failed", new { error = ex.Message });
            if (Volatile.Read(ref _isWindowClosing) == 1 || _isTransferring != 0) return;
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
        LanClient newClient;
        try { _client?.Dispose(); _client = newClient = MakeClient(ip, port); }
        finally { _clientLock.Release(); }

        try
        {
            // C9-FIX: añadir timeout explícito de 15s para que ConnectAsync falle rápido si el
            // servidor no responde. Sin CT, ListAsync solo dependía del KeepAlive TCP (~30s),
            // lo que dejaba la UI bloqueada en "conectando..." sin posibilidad de cancelación.
            using var connectCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
            var entries = await newClient.ListAsync(_remotePath, connectCts.Token);
            Dispatcher.UIThread.Post(() => { _remoteItemsAll = entries; ApplyRemoteSort(); UpdateRemotePath(); });
            SetConnStatus(L["conn.connectedWord"], BrushConnected);
            UpdateConnectButton(isConnected: true, isBusy: false);
            UpdateRemoteCreateFolderButton(isConnected: true);
            if (!silent)
            {
                var emojiId = newClient.RemoteCertificate != null
                    ? $"  🔒 {CertTrust.EmojiFingerprint(newClient.RemoteCertificate)}"
                    : "";
                SetStatus(L.Format("st.connected", $"{ip}:{port}") + emojiId);
            }
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrEmpty(_remotePath) &&
                (ex.Message.Contains("svc.accessDenied") || ex.Message.Contains("svc.outsideShare") || ex.Message.Contains("svc.invalidPath") || ex.Message.Contains("svc.sysProtected")))
            {
                try
                {
                    _remotePath = "";
                    // C9-FIX: mismo timeout de 15s en el fallback a ruta raíz
                    using var fallbackCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
                    var entries = await newClient.ListAsync("", fallbackCts.Token);
                    Dispatcher.UIThread.Post(() => { _remoteItemsAll = entries; ApplyRemoteSort(); UpdateRemotePath(); });
                    SetConnStatus(L["conn.connectedWord"], BrushConnected);
                    UpdateConnectButton(isConnected: true, isBusy: false);
                    UpdateRemoteCreateFolderButton(isConnected: true);
                    if (!silent) SetStatus(L.Format("st.connected", $"{ip}:{port}"));
                    return;
                }
                catch (Exception fallbackEx)
                {
                    Log.Warn("ui", "connect-fallback-root-list-failed", new { ip, port, error = fallbackEx.Message });
                }
            }

            await _clientLock.WaitAsync();
            try
            {
                // Evita clobber de una conexión más nueva si hubo intentos solapados.
                if (ReferenceEquals(_client, newClient))
                {
                    _client.Dispose();
                    _client = null;
                }
            }
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
        try { _uploadCts.Cancel(); }
        catch (ObjectDisposedException ex) { Log.Debug("conn", "upload-cts-cancel-disposed-on-disconnect", new { error = ex.Message }); }
        try { _downloadCts.Cancel(); }
        catch (ObjectDisposedException ex) { Log.Debug("conn", "download-cts-cancel-disposed-on-disconnect", new { error = ex.Message }); }
        if (_watchFolderActive) Dispatcher.UIThread.Post(StopWatch);

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
            Interlocked.Exchange(ref _remoteEntriesSignature, 0);
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
            // C9-FIX: timeout de 3s — best-effort notice; si el peer no responde en 3s no vale la pena esperar.
            // Sin CT, bloqueaba ~30s por KeepAlive TCP durante el cierre de la app.
            using var disconnectCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
            await cli.SendDisconnectNoticeAsync(disconnectCts.Token);
        }
        catch (Exception ex)
        {
            Log.Debug("ui", "disconnect-notice-send-failed", new { ip, port, error = ex.Message });
        }
    }

    private void OnDisconnectNoticeReceived(string ip)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                var remoteIp = this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "";
                if (!string.Equals(remoteIp, ip, StringComparison.OrdinalIgnoreCase)) return;
                if (!await IsConnectedAsync()) return;

                await DisconnectAsync(silent: true);
                SetStatus(L["st.disconnected"]);
            }
            catch (Exception ex)
            {
                Log.Warn("conn", "disconnect-notice-handler-failed", new { ip, error = ex.Message });
            }
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
        if (Interlocked.CompareExchange(ref _isReconnectInProgress, 1, 0) != 0)
            return await WaitReconnectWindowAsync(ct);
        SetConnStatus(L["conn.reconnecting"], BrushConnecting);
        var retryPolicy = Policy
            .Handle<Exception>(ex => ex is not OperationCanceledException)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt =>
                {
                    var jitter = Random.Shared.Next(150, 550);
                    return TimeSpan.FromMilliseconds(Math.Min(5000, (int)(Math.Pow(2, attempt) * 350) + jitter));
                },
                onRetryAsync: (ex, _, attempt, _) =>
                {
                    SetStatus(L.Format("st.reconnecting", attempt));
                    return Task.CompletedTask;
                });

        try
        {
            await retryPolicy.ExecuteAsync(async token =>
            {
                token.ThrowIfCancellationRequested();
                var tempClient = MakeClient(ip, port);
                try
                {
                    // C11-FIX: timeout de 8s por intento de reconexión — sin CT propio, cada ListAsync
                    // esperaba hasta ~30s (KeepAlive TCP). Con 3 retries → 90s de bloqueo potencial.
                    using var retryCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    retryCts.CancelAfter(TimeSpan.FromSeconds(8));
                    _ = await tempClient.ListAsync("", retryCts.Token);
                    await _clientLock.WaitAsync(token);
                    try
                    {
                        _client?.Dispose();
                        _client = tempClient;
                        tempClient = null;
                    }
                    finally { _clientLock.Release(); }
                }
                finally
                {
                    tempClient?.Dispose();
                }
            }, ct);

            SetConnStatus(L["conn.reconnectedWord"], BrushConnected);
            UpdateConnectButton(isConnected: true, isBusy: false);
            UpdateRemoteCreateFolderButton(isConnected: true);
            SetStatus(L["st.reconnected"]);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            Log.Warn("conn", "reconnect-failed", new { ip, port, error = ex.Message });
            SetConnStatus(L["conn.disconnectedWord"], BrushError);
            UpdateConnectButton(isConnected: false, isBusy: false);
            UpdateRemoteCreateFolderButton(isConnected: false);
            SetStatus(L["st.reconnectFailed"]);

            await _clientLock.WaitAsync();
            try { _client?.Dispose(); _client = null; }
            finally { _clientLock.Release(); }

            return false;
        }
        finally
        {
            Interlocked.Exchange(ref _isReconnectInProgress, 0);
        }
    }

    private async Task<bool> WaitReconnectWindowAsync(CancellationToken ct)
    {
        const int maxChecks = 15;
        for (int i = 0; i < maxChecks; i++)
        {
            if (ct.IsCancellationRequested) return false;
            if (Volatile.Read(ref _isReconnectInProgress) == 0) return await IsConnectedAsync();
            try { await Task.Delay(200, ct); }
            catch (OperationCanceledException) { return false; }
        }
        return await IsConnectedAsync();
    }

}
