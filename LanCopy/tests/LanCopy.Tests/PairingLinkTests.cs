using System.Threading.Tasks;
using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public class PairingLinkTests
{
    [Fact]
    public void Build_Then_Parse_RoundTrips()
    {
        var link = PairingLink.Build("192.168.1.50", 8742, "1234");
        var p = PairingLink.TryParse(link);
        Assert.NotNull(p);
        Assert.Equal("192.168.1.50", p!.Value.Ip);
        Assert.Equal(8742, p.Value.Port);
        Assert.Equal("1234", p.Value.Pin);
    }

    [Fact]
    public void Parse_IpPort_Simple()
    {
        var p = PairingLink.TryParse("10.0.0.7:9000");
        Assert.NotNull(p);
        Assert.Equal("10.0.0.7", p!.Value.Ip);
        Assert.Equal(9000, p.Value.Port);
        Assert.Null(p.Value.Pin);
    }

    [Fact]
    public void Parse_PlainIp_DefaultsPort()
    {
        var p = PairingLink.TryParse("10.0.0.7");
        Assert.NotNull(p);
        Assert.Equal(8742, p!.Value.Port);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a link / with spaces")]
    public void Parse_Junk_ReturnsNull(string input)
    {
        Assert.Null(PairingLink.TryParse(input));
    }

    [Fact]
    public async Task RateLimiter_Unlimited_NoDelay()
    {
        var rl = new RateLimiter { BytesPerSecond = 0 };
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await rl.ThrottleAsync(10_000_000, default);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 100);
    }

    [Fact]
    public void RateLimiter_NegativeClampsToZero()
    {
        var rl = new RateLimiter { BytesPerSecond = -5 };
        Assert.Equal(0, rl.BytesPerSecond);
    }
}
