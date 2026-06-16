using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public class PairingCodeTests
{
    [Theory]
    [InlineData("192.168.1.34", 8742)]
    [InlineData("10.0.0.1", 1)]
    [InlineData("172.16.5.9", 65535)]
    [InlineData("127.0.0.1", 8742)]
    public void RoundTrip_Works(string ip, int port)
    {
        var code = PairingCode.Encode(ip, port);
        Assert.True(PairingCode.TryDecode(code, out var ip2, out var port2));
        Assert.Equal(ip, ip2);
        Assert.Equal(port, port2);
    }

    [Fact]
    public void Decode_ToleratesCase_AndMissingDash()
    {
        var code = PairingCode.Encode("192.168.1.34", 8742);
        var noisy = code.Replace("-", "").ToLowerInvariant();
        Assert.True(PairingCode.TryDecode(noisy, out var ip, out var port));
        Assert.Equal("192.168.1.34", ip);
        Assert.Equal(8742, port);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-code")]
    [InlineData("ABC")]
    public void Decode_InvalidInput_ReturnsFalse(string bad)
    {
        Assert.False(PairingCode.TryDecode(bad, out _, out _));
    }

    [Fact]
    public void Encode_HasExpectedShape()
    {
        var code = PairingCode.Encode("192.168.1.34", 8742);
        Assert.Contains("-", code);
        Assert.Equal(11, code.Length);
    }
}