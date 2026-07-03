using System;

namespace LanCopy.Services;

/// <summary>
/// Centralised timeout, retry and configuration constants.
/// Every magic number that was previously scattered across the codebase
/// lives here so that tuning behaviour is a single-file change.
/// </summary>
internal static class TransferOptions
{
    // ───────────────────────── Connection ─────────────────────────

    /// <summary>Maximum concurrent connections the server will accept.</summary>
    public const int MaxConnections = 64;

    /// <summary>Default per-IP connection cap.</summary>
    public const int MaxPerIpDefault = 8;

    /// <summary>TCP keep-alive: seconds of idle before the first probe.</summary>
    public const int KeepAliveIdleSeconds = 15;

    /// <summary>TCP keep-alive: seconds between successive probes.</summary>
    public const int KeepAliveIntervalSeconds = 5;

    /// <summary>TCP keep-alive: number of unacknowledged probes before drop.</summary>
    public const int KeepAliveRetryCount = 3;

    // ───────────────────────── Transfer ───────────────────────────

    /// <summary>Idle timeout applied to transfers below <see cref="LargeTransferThresholdBytes"/>.</summary>
    public static readonly TimeSpan TransferIdleTimeoutSmall = TimeSpan.FromSeconds(60);

    /// <summary>Idle timeout applied to transfers at or above <see cref="LargeTransferThresholdBytes"/>.</summary>
    public static readonly TimeSpan TransferIdleTimeoutLarge = TimeSpan.FromSeconds(180);

    /// <summary>Byte threshold that distinguishes "small" from "large" transfers (1 GB).</summary>
    public const long LargeTransferThresholdBytes = 1L * 1024 * 1024 * 1024;

    /// <summary>Maximum number of retry passes for failed files in a transfer batch.</summary>
    public const int MaxRetryPasses = 4;

    /// <summary>Minimum interval (ms) between transfer progress reports.</summary>
    public const int TransferProgressIntervalMs = 200;

    /// <summary>Interval at which the transfer-status UI pulses updates.</summary>
    public static readonly TimeSpan TransferUiPulseInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>Timeout for a synchronous stat request during sync.</summary>
    public static readonly TimeSpan SyncStatTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Timeout for sending clipboard content to a peer.</summary>
    public static readonly TimeSpan ClipboardSendTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Per-peer timeout when broadcasting a file/folder to all peers.</summary>
    public static readonly TimeSpan BroadcastPerPeerTimeout = TimeSpan.FromMinutes(2);

    // ───────────────────────── Security / Rate-Limiting ───────────

    /// <summary>Rolling window (seconds) for the command-rate limiter.</summary>
    public const int CommandRateWindowSeconds = 10;

    /// <summary>Maximum commands allowed within <see cref="CommandRateWindowSeconds"/>.</summary>
    public const int CommandRateLimit = 120;

    /// <summary>Consecutive PIN failures before back-off kicks in.</summary>
    public const int PinMaxFails = 5;

    /// <summary>Initial back-off delay (ms) after too many PIN failures.</summary>
    public const int PinBackoffMs = 30_000;

    /// <summary>Maximum back-off delay (ms) for PIN failures.</summary>
    public const int PinMaxBackoffMs = 10 * 60_000;

    /// <summary>Maximum throttle delay (ms) the generic rate limiter will impose.</summary>
    public const int RateLimiterMaxThrottleMs = 60_000;

    // ───────────────────────── Discovery ──────────────────────────

    /// <summary>Interval (ms) between UDP broadcast announcements.</summary>
    public const int BroadcastIntervalMs = 5_000;

    /// <summary>Interval (ms) between checks for expired peers.</summary>
    public const int ExpireCheckIntervalMs = 10_000;

    /// <summary>Time (ms) after which a peer with no broadcast is considered stale.</summary>
    public const int PeerStaleMs = 25_000;

    // ───────────────────────── UI / Watchdog ──────────────────────

    /// <summary>How often the connection watchdog fires.</summary>
    public static readonly TimeSpan WatchdogInterval = TimeSpan.FromSeconds(4);

    /// <summary>Timeout for a single health-probe round-trip.</summary>
    public static readonly TimeSpan HealthProbeTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Timeout for a health probe while a transfer is active.</summary>
    public static readonly TimeSpan TransferHealthTimeout = TimeSpan.FromSeconds(3);

    /// <summary>Number of reconnect attempts before giving up.</summary>
    public const int ReconnectRetryCount = 3;

    /// <summary>Base delay (ms) for exponential reconnect back-off.</summary>
    public const int ReconnectBaseDelayMs = 350;

    /// <summary>Maximum delay (ms) for exponential reconnect back-off.</summary>
    public const int ReconnectMaxDelayMs = 5_000;

    /// <summary>Maximum polling iterations while waiting for a reconnect to settle.</summary>
    public const int WaitReconnectMaxChecks = 15;

    /// <summary>Delay (ms) between reconnect-wait polling iterations.</summary>
    public const int WaitReconnectDelayMs = 200;

    /// <summary>Auto-refresh interval for the remote browser view.</summary>
    public static readonly TimeSpan BrowserAutoRefreshInterval = TimeSpan.FromSeconds(3);

    /// <summary>Debounce delay (ms) for file-system watcher events in the browser.</summary>
    public const int WatcherDebounceMs = 300;

    // ───────────────────────── Protocol ───────────────────────────

    /// <summary>Size (bytes) of the read/write buffer used by the protocol layer (512 KB).</summary>
    public const int ProtocolBufferSize = 512 * 1024;

    /// <summary>Maximum length (bytes) of a single protocol text line (1 MB).</summary>
    public const int ProtocolMaxLineBytes = 1024 * 1024;

    // ───────────────────────── Logging / Audit ────────────────────

    /// <summary>Grace period (ms) for the logger to flush on shutdown.</summary>
    public const int LogShutdownTimeoutMs = 2_000;

    /// <summary>Number of days to retain audit log entries.</summary>
    public const int AuditRetentionDays = 90;
}
