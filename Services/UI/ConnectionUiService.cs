using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using LanCopy.Localization;
using LanCopy.Models;
using LanCopy.Services;
using Polly;

namespace LanCopy.Services.UI;

internal interface IConnectionUiHost
{
    bool IsWindowClosing { get; }
    bool IsTransferInProgress { get; }
    bool IsConnectButtonBusy { get; }
    bool IsWatchFolderActive { get; }
    string RemotePath { get; set; }
    string RemoteIpText { get; set; }
    string RemotePortText { get; set; }
    void SaveConnectionSettings(string ip, string portText);
    LanClient CreateLanClient(string ip, int port, bool? useTlsOverride = null);
    Task<LanClient?> GetClientAsync(CancellationToken ct = default);
    Task ReplaceClientAsync(LanClient client, CancellationToken ct = default);
    Task<bool> TryClearClientAsync(LanClient client, CancellationToken ct = default);
    Task DisposeClientAsync(CancellationToken ct = default);
    Task DisposeDownloadClientAsync(CancellationToken ct = default);
    void SetRemoteEntries(List<FileEntry> entries);
    void ClearRemoteEntries();
    void SetStatus(string text);
    void SetConnectionStatus(string text, SolidColorBrush brush);
    void SetConnectButtonState(bool isConnected, bool isBusy);
    void SetRemoteCreateFolderEnabled(bool isEnabled);
    void SetReconnectInProgress(bool isInProgress);
    void StopWatchFolder();
    void CancelUploadTransfers();
    void CancelDownloadTransfers();
}

internal sealed class ConnectionUiService
{
    private static readonly SolidColorBrush BrushConnected = SolidColorBrush.Parse("#28A745");
    private static readonly SolidColorBrush BrushError = SolidColorBrush.Parse("#FF6B6B");
    private static readonly SolidColorBrush BrushConnecting = SolidColorBrush.Parse("#FFD700");
    private static Loc L => Loc.Instance;

    private readonly IConnectionUiHost _host;
    private DispatcherTimer? _connectionWatchdogTimer;
    private int _isConnectionProbeRunning;
    private int _isReconnectInProgress;

    public ConnectionUiService(IConnectionUiHost host)
    {
        _host = host;
    }

    public void StartConnectionWatchdog()
    {
        if (_connectionWatchdogTimer != null) return;
        _connectionWatchdogTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _connectionWatchdogTimer.Tick += ConnectionWatchdog_Tick;
        _connectionWatchdogTimer.Start();
    }

    public void StopConnectionWatchdog()
    {
        if (_connectionWatchdogTimer == null) return;
        _connectionWatchdogTimer.Stop();
        _connectionWatchdogTimer.Tick -= ConnectionWatchdog_Tick;
        _connectionWatchdogTimer = null;
    }

    public async Task<bool> IsConnectedAsync(CancellationToken ct = default)
        => await _host.GetClientAsync(ct) != null;

    public async Task ConnectAsync(string ip, int port, bool silent = false, CancellationToken ct = default, bool? useTlsOverride = null)
    {
        _host.SetConnectButtonState(isConnected: false, isBusy: true);
        if (!silent)
        {
            _host.SetStatus(L.Format("st.connecting", $"{ip}:{port}"));
            _host.SetConnectionStatus(L["conn.connecting"], BrushConnecting);
        }

        var newClient = _host.CreateLanClient(ip, port, useTlsOverride);
        await _host.ReplaceClientAsync(newClient, ct);

        try
        {
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(TimeSpan.FromSeconds(15));
            var entries = await newClient.ListAsync(_host.RemotePath, connectCts.Token);
            _host.SetRemoteEntries(entries);
            _host.SetConnectionStatus(L["conn.connectedWord"], BrushConnected);
            _host.SetConnectButtonState(isConnected: true, isBusy: false);
            _host.SetRemoteCreateFolderEnabled(isEnabled: true);
            if (!silent)
            {
                var cert = newClient.RemoteCertificate;
                if (cert != null)
                {
                    var fp = CertTrust.ShortFingerprint(CertTrust.Fingerprint(cert));
                    var emoji = CertTrust.EmojiFingerprint(cert);
                    _host.SetStatus(L.Format("st.connectedTrusted", $"{ip}:{port}", fp, emoji));
                }
                else
                {
                    _host.SetStatus(L.Format("st.connected", $"{ip}:{port}"));
                }
            }
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrEmpty(_host.RemotePath) &&
                (ex.Message.Contains("svc.accessDenied") || ex.Message.Contains("svc.outsideShare") || ex.Message.Contains("svc.invalidPath") || ex.Message.Contains("svc.sysProtected")))
            {
                try
                {
                    _host.RemotePath = "";
                    using var fallbackCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    fallbackCts.CancelAfter(TimeSpan.FromSeconds(15));
                    var entries = await newClient.ListAsync("", fallbackCts.Token);
                    _host.SetRemoteEntries(entries);
                    _host.SetConnectionStatus(L["conn.connectedWord"], BrushConnected);
                    _host.SetConnectButtonState(isConnected: true, isBusy: false);
                    _host.SetRemoteCreateFolderEnabled(isEnabled: true);
                    if (!silent) _host.SetStatus(L.Format("st.connected", $"{ip}:{port}"));
                    return;
                }
                catch (Exception fallbackEx)
                {
                    Log.Warn("ui", "connect-fallback-root-list-failed", new { ip, port, error = fallbackEx.Message });
                }
            }

            await _host.TryClearClientAsync(newClient, ct);
            _host.SetConnectionStatus(L["conn.error"], BrushError);
            _host.SetConnectButtonState(isConnected: false, isBusy: false);
            _host.SetRemoteCreateFolderEnabled(isEnabled: false);
            if (!silent)
            {
                if (string.Equals(ex.Message, "st.identityChanged", StringComparison.Ordinal))
                {
                    _host.SetStatus(L["st.identityChanged"]);
                }
                else if (string.Equals(ex.Message, "st.certRejected", StringComparison.Ordinal))
                {
                    _host.SetStatus(L["st.certRejected"]);
                }
                else
                {
                    _host.SetStatus(L.Format("st.connectFailedWithHint", $"{ip}:{port}", L[ex.Message]));
                }
            }
        }
    }

    public async Task DisconnectAsync(bool silent = false, bool notifyPeer = false, CancellationToken ct = default)
    {
        _host.SetConnectButtonState(isConnected: true, isBusy: true);
        if (notifyPeer) await TrySendDisconnectNoticeAsync(ct);
        _host.CancelUploadTransfers();
        _host.CancelDownloadTransfers();
        if (_host.IsWatchFolderActive) Dispatcher.UIThread.Post(_host.StopWatchFolder);

        await _host.DisposeClientAsync(ct);
        await _host.DisposeDownloadClientAsync(ct);

        _host.ClearRemoteEntries();
        _host.SetConnectionStatus(L["conn.disconnectedWord"], BrushError);
        _host.SetConnectButtonState(isConnected: false, isBusy: false);
        _host.SetRemoteCreateFolderEnabled(isEnabled: false);
        if (!silent) _host.SetStatus(L["st.disconnected"]);
    }

    public async Task TrySendDisconnectNoticeAsync(CancellationToken ct = default)
    {
        var ip = _host.RemoteIpText.Trim();
        var portText = _host.RemotePortText.Trim();
        if (string.IsNullOrWhiteSpace(ip) || !int.TryParse(portText, out var port)) return;

        try
        {
            using var cli = _host.CreateLanClient(ip, port);
            using var disconnectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            disconnectCts.CancelAfter(TimeSpan.FromSeconds(3));
            await cli.SendDisconnectNoticeAsync(disconnectCts.Token);
        }
        catch (Exception ex)
        {
            Log.Debug("ui", "disconnect-notice-send-failed", new { ip, port, error = ex.Message });
        }
    }

    public void OnDisconnectNoticeReceived(string ip)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                var remoteIp = _host.RemoteIpText.Trim();
                if (!string.Equals(remoteIp, ip, StringComparison.OrdinalIgnoreCase)) return;
                if (!await IsConnectedAsync()) return;

                await DisconnectAsync(silent: true);
                _host.SetStatus(L["st.disconnected"]);
            }
            catch (Exception ex)
            {
                Log.Warn("conn", "disconnect-notice-handler-failed", new { ip, error = ex.Message });
            }
        });
    }

    public async Task<bool> TryReconnectAsync(string ip, int port, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(ip) || port < 1) return false;
        if (Interlocked.CompareExchange(ref _isReconnectInProgress, 1, 0) != 0)
            return await WaitReconnectWindowAsync(ct);

        _host.SetReconnectInProgress(true);
        _host.SetConnectionStatus(L["conn.reconnecting"], BrushConnecting);
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
                    _host.SetStatus(L.Format("st.reconnecting", attempt));
                    return Task.CompletedTask;
                });

        try
        {
            await retryPolicy.ExecuteAsync(async token =>
            {
                token.ThrowIfCancellationRequested();
                var tempClient = _host.CreateLanClient(ip, port);
                try
                {
                    using var retryCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    retryCts.CancelAfter(TimeSpan.FromSeconds(8));
                    _ = await tempClient.ListAsync("", retryCts.Token);
                    await _host.ReplaceClientAsync(tempClient, token);
                    tempClient = null;
                }
                finally
                {
                    tempClient?.Dispose();
                }
            }, ct);

            _host.SetConnectionStatus(L["conn.reconnectedWord"], BrushConnected);
            _host.SetConnectButtonState(isConnected: true, isBusy: false);
            _host.SetRemoteCreateFolderEnabled(isEnabled: true);
            _host.SetStatus(L["st.reconnected"]);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            Log.Warn("conn", "reconnect-failed", new { ip, port, error = ex.Message });
            _host.SetConnectionStatus(L["conn.disconnectedWord"], BrushError);
            _host.SetConnectButtonState(isConnected: false, isBusy: false);
            _host.SetRemoteCreateFolderEnabled(isEnabled: false);
            _host.SetStatus(L["st.reconnectFailed"]);
            await _host.DisposeClientAsync(ct);
            return false;
        }
        finally
        {
            Interlocked.Exchange(ref _isReconnectInProgress, 0);
            _host.SetReconnectInProgress(false);
        }
    }

    public async Task RecoverFromTransferStallAsync(CancellationToken ct = default)
    {
        _host.SetStatus(L["st.autoReconnecting"]);
        _host.CancelUploadTransfers();
        _host.CancelDownloadTransfers();
        await _host.DisposeClientAsync(ct);
        await _host.DisposeDownloadClientAsync(ct);
    }

    private async void ConnectionWatchdog_Tick(object? sender, EventArgs e)
    {
        if (_host.IsWindowClosing) return;
        if (_host.IsConnectButtonBusy || _host.IsTransferInProgress || Volatile.Read(ref _isReconnectInProgress) == 1) return;
        if (Interlocked.CompareExchange(ref _isConnectionProbeRunning, 1, 0) != 0) return;

        try
        {
            var snap = await _host.GetClientAsync();
            if (snap == null) return;

            using var probeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            _ = await snap.GetHealthAsync(probeCts.Token);
        }
        catch (Exception ex)
        {
            Log.Debug("conn", "watchdog-health-probe-failed", new { error = ex.Message });
            if (_host.IsWindowClosing || _host.IsTransferInProgress) return;
            await DisconnectAsync(silent: true);
            _host.SetStatus(L["st.disconnected"]);
        }
        finally
        {
            Interlocked.Exchange(ref _isConnectionProbeRunning, 0);
        }
    }

    private async Task<bool> WaitReconnectWindowAsync(CancellationToken ct)
    {
        const int maxChecks = 15;
        for (int i = 0; i < maxChecks; i++)
        {
            if (ct.IsCancellationRequested) return false;
            if (Volatile.Read(ref _isReconnectInProgress) == 0) return await IsConnectedAsync(ct);
            try { await Task.Delay(200, ct); }
            catch (OperationCanceledException) { return false; }
        }

        return await IsConnectedAsync(ct);
    }
}

