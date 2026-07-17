using LanCopy.Services.UI;
using Xunit;

namespace LanCopy.Tests;

public sealed class ConnectionRecoveryPolicyTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void RecoveryRequiresConsecutiveFailures(int failures, bool expected)
        => Assert.Equal(expected, ConnectionRecoveryPolicy.ShouldAttemptRecovery(failures));

    [Theory]
    [InlineData(2, true)]
    [InlineData(10, true)]
    [InlineData(30, true)]
    [InlineData(121, false)]
    public void ShortAndMediumOutagesKeepTheSession(int seconds, bool expected)
        => Assert.Equal(expected, ConnectionRecoveryPolicy.ShouldKeepSession(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void ReconnectDelaysGrowAndRemainBounded()
    {
        var previous = TimeSpan.Zero;
        for (var attempt = 1; attempt <= ConnectionRecoveryPolicy.ReconnectRetryCount; attempt++)
        {
            var delay = ConnectionRecoveryPolicy.GetReconnectDelay(attempt);
            Assert.True(delay > previous);
            Assert.True(delay < TimeSpan.FromSeconds(16));
            previous = delay;
        }
    }
}
