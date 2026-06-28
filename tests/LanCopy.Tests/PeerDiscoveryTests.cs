using System.Collections.Generic;
using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public class PeerDiscoveryTests
{
    [Fact]
    public void IsSameInstanceId_MatchesOnlyExactNonEmptyValues()
    {
        const string current = "abc123";
        Assert.True(PeerDiscovery.IsSameInstanceId("abc123", current));
        Assert.False(PeerDiscovery.IsSameInstanceId("ABC123", current));
        Assert.False(PeerDiscovery.IsSameInstanceId("", current));
        Assert.False(PeerDiscovery.IsSameInstanceId(null, current));
    }

    [Fact]
    public void IsLocalIpv4Address_DetectsOnlyKnownLocalIpv4()
    {
        var localIps = new HashSet<string>(new[] { "127.0.0.1", "192.168.1.25" });
        Assert.True(PeerDiscovery.IsLocalIpv4Address("192.168.1.25", localIps));
        Assert.False(PeerDiscovery.IsLocalIpv4Address("192.168.1.99", localIps));
        Assert.False(PeerDiscovery.IsLocalIpv4Address("::1", localIps));
    }

    [Fact]
    public void GetLocalIpv4Addresses_IncludesPrimaryLocalIpAndLoopback()
    {
        var ips = PeerDiscovery.GetLocalIpv4Addresses("10.0.0.8");
        Assert.Contains("10.0.0.8", ips);
        Assert.Contains("127.0.0.1", ips);
    }
}
