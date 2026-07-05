using System.Collections.Generic;
using System.Net;
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

    [Theory]
    [InlineData("192.168.1.34", "255.255.255.0", "192.168.1.255")]
    [InlineData("10.0.8.20", "255.255.240.0", "10.0.15.255")]
    [InlineData("172.16.4.10", "255.255.252.0", "172.16.7.255")]
    public void CalculateBroadcastAddress_UsesInterfaceSubnetMask(string ip, string mask, string expected)
    {
        var actual = PeerDiscovery.CalculateBroadcastAddress(IPAddress.Parse(ip), IPAddress.Parse(mask));
        Assert.Equal(expected, actual.ToString());
    }
    [Fact]
    public void GetPeers_ReturnsEmptyWhenNoPeersDiscovered()
    {
        // PeerDiscovery with no network activity should return empty peer list immediately.
        var discovery = new PeerDiscovery("127.0.0.1", 19999) { StealthMode = true };
        var peers = discovery.GetPeers();
        Assert.Empty(peers);
        discovery.Dispose();
    }
}
