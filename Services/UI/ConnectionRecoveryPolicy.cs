namespace LanCopy.Services.UI;

internal static class ConnectionRecoveryPolicy
{
    internal const int ProbeFailureThreshold = 3;
    internal const int ReconnectRetryCount = 6;
    internal static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan MaximumRecoverableOutage = TimeSpan.FromMinutes(2);

    internal static bool ShouldAttemptRecovery(int consecutiveFailures)
        => consecutiveFailures >= ProbeFailureThreshold;

    internal static bool ShouldKeepSession(TimeSpan outageDuration)
        => outageDuration <= MaximumRecoverableOutage;

    internal static TimeSpan GetReconnectDelay(int attempt)
    {
        var seconds = attempt switch
        {
            <= 1 => 1,
            2 => 2,
            3 => 4,
            4 => 8,
            5 => 12,
            _ => 15
        };
        return TimeSpan.FromSeconds(seconds) + TimeSpan.FromMilliseconds(Random.Shared.Next(100, 500));
    }
}
