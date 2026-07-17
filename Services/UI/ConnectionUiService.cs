using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
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
    private int _lastReconnectSucceeded;
    private int _consecutiveProbeFailures;
    private int _networkRecoveryScheduled;
    private int _networkHandlersAttached;
    private long _outageStartedUtcTicks;

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
        if (Interlocked.Exchange(ref _networkHandlersAttached, 1) == 0)
        {
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        }
    }

    public void StopConnectionWatchdog()
    {
        if (_connectionWatchdogTimer == null) return;
        _connectionWatchdogTimer.Stop();
        _connectionWatchdogTimer.Tick -= ConnectionWatchdog_Tick;
        _connectionWatchdogTimer = null;
        Interlocked.Exchange(ref _consecutiveProbeFailures, 0);
        if (Interlocked.Exchange(ref _networkHandlersAttached, 0) == 1)
        {
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        }
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
            MarkRecovered("connect");
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
                    MarkRecovered("connect-root-fallback");
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
        Interlocked.Exchange(ref _outageStartedUtcTicks, 0);
        Interlocked.Exchange(ref _consecutiveProbeFailures, 0);
        Interlocked.Exchange(ref _lastReconnectSucceeded, 0);
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

        Interlocked.Exchange(ref _lastReconnectSucceeded, 0);
        MarkOutage("reconnect-requested");
        _host.SetReconnectInProgress(true);
        _host.SetConnectionStatus(L["conn.reconnecting"], BrushConnecting);
        _host.SetConnectButtonState(isConnected: false, isBusy: true);
        var retryPolicy = Policy
            .Handle<Exception>(ex => ex is not OperationCanceledException)
            .WaitAndRetryAsync(
                retryCount: ConnectionRecoveryPolicy.ReconnectRetryCount,
                sleepDurationProvider: ConnectionRecoveryPolicy.GetReconnectDelay,
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

            Interlocked.Exchange(ref _consecutiveProbeFailures, 0);
            Interlocked.Exchange(ref _lastReconnectSucceeded, 1);
            MarkRecovered("reconnect");
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
            Log.Warn("conn", "reconnect-failed", new { ip, port, error = ex.ToString() });
            Interlocked.Exchange(ref _lastReconnectSucceeded, 0);
            _host.SetConnectionStatus(L["conn.disconnectedWord"], BrushError);
            _host.SetConnectButtonState(isConnected: false, isBusy: false);
            _host.SetRemoteCreateFolderEnabled(isEnabled: false);
            _host.SetStatus(L["st.reconnectFailed"]);
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

            using var probeCts = new CancellationTokenSource(ConnectionRecoveryPolicy.ProbeTimeout);
            _ = await snap.GetHealthAsync(probeCts.Token);
            Interlocked.Exchange(ref _consecutiveProbeFailures, 0);
        }
        catch (Exception ex)
        {
            if (_host.IsWindowClosing || _host.IsTransferInProgress) return;
            MarkOutage("health-probe", ex);
            var failures = Interlocked.Increment(ref _consecutiveProbeFailures);
            Log.Debug("conn", "watchdog-health-probe-failed", new { failures, threshold = ConnectionRecoveryPolicy.ProbeFailureThreshold, error = ex.Message });
            if (!ConnectionRecoveryPolicy.ShouldAttemptRecovery(failures)) return;

            Interlocked.Exchange(ref _consecutiveProbeFailures, 0);
            var ip = _host.RemoteIpText.Trim();
            if (!int.TryParse(_host.RemotePortText.Trim(), out var port) || string.IsNullOrWhiteSpace(ip)) return;
            _host.SetStatus(L["st.autoReconnecting"]);
            _ = await TryReconnectAsync(ip, port, CancellationToken.None);
        }
        finally
        {
            Interlocked.Exchange(ref _isConnectionProbeRunning, 0);
        }
    }

    private async Task<bool> WaitReconnectWindowAsync(CancellationToken ct)
    {
        const int maxChecks = 300; // hasta 60 s, cubre la ventana completa de reconexión
        for (int i = 0; i < maxChecks; i++)
        {
            if (ct.IsCancellationRequested) return false;
            if (Volatile.Read(ref _isReconnectInProgress) == 0) return Volatile.Read(ref _lastReconnectSucceeded) == 1;
            try { await Task.Delay(200, ct); }
            catch (OperationCanceledException) { return false; }
        }

        return Volatile.Read(ref _lastReconnectSucceeded) == 1;
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (!e.IsAvailable) { MarkOutage("network-unavailable"); return; }
        ScheduleNetworkRecovery("network-available");
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e) => ScheduleNetworkRecovery("network-address-changed");

    private void ScheduleNetworkRecovery(string reason)
    {
        if (_host.IsWindowClosing || Interlocked.CompareExchange(ref _networkRecoveryScheduled, 1, 0) != 0) return;
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await Task.Delay(1500);
                if (_host.IsWindowClosing || _host.IsTransferInProgress || await _host.GetClientAsync() == null) return;
                var ip = _host.RemoteIpText.Trim();
                if (!int.TryParse(_host.RemotePortText.Trim(), out var port) || string.IsNullOrWhiteSpace(ip)) return;
                Log.Info("conn", "network-change-recovery", new { reason, ip, port });
                _ = await TryReconnectAsync(ip, port, CancellationToken.None);
            }
            catch (Exception ex) { Log.Debug("conn", "network-change-recovery-failed", new { reason, error = ex.Message }); }
            finally { Interlocked.Exchange(ref _networkRecoveryScheduled, 0); }
        });
    }

    private void MarkOutage(string reason, Exception? ex = null)
    {
        var now = DateTimeOffset.UtcNow.UtcTicks;
        if (Interlocked.CompareExchange(ref _outageStartedUtcTicks, now, 0) == 0)
            Log.Warn("conn", "outage-started", new { reason, error = ex?.Message });
    }

    private TimeSpan GetOutageDuration()
    {
        var started = Interlocked.Read(ref _outageStartedUtcTicks);
        return started == 0 ? TimeSpan.Zero : TimeSpan.FromTicks(Math.Max(0, DateTimeOffset.UtcNow.UtcTicks - started));
    }

    private void MarkRecovered(string reason)
    {
        var started = Interlocked.Exchange(ref _outageStartedUtcTicks, 0);
        if (started == 0) return;
        var durationMs = Math.Max(0, TimeSpan.FromTicks(DateTimeOffset.UtcNow.UtcTicks - started).TotalMilliseconds);
        Log.Info("conn", "outage-recovered", new { reason, durationMs });
    }
}

