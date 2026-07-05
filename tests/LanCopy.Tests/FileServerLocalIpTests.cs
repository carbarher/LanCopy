using System.Net;
using System.Net.NetworkInformation;
using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public sealed class FileServerLocalIpTests
{
    [Fact]
    public void ScoreLocalIpCandidate_PrefersPhysicalEthernetWithGatewayOverHyperVPrivateAddress()
    {
        var physical = FileServer.ScoreLocalIpCandidate(
            IPAddress.Parse("192.168.1.34"),
            NetworkInterfaceType.Ethernet,
            hasGateway: true,
            adapterName: "Ethernet",
            adapterDescription: "Intel(R) Ethernet Connection");

        var virtualAdapter = FileServer.ScoreLocalIpCandidate(
            IPAddress.Parse("172.24.208.1"),
            NetworkInterfaceType.Ethernet,
            hasGateway: false,
            adapterName: "vEthernet (Ethernet)",
            adapterDescription: "Hyper-V Virtual Ethernet Adapter");

        Assert.True(physical > virtualAdapter);
    }

    [Fact]
    public void ScoreLocalIpCandidate_DemotesApipaAddresses()
    {
        var apipa = FileServer.ScoreLocalIpCandidate(
            IPAddress.Parse("169.254.3.84"),
            NetworkInterfaceType.Wireless80211,
            hasGateway: false,
            adapterName: "Wi-Fi",
            adapterDescription: "Wireless Adapter");

        var lan = FileServer.ScoreLocalIpCandidate(
            IPAddress.Parse("192.168.1.34"),
            NetworkInterfaceType.Ethernet,
            hasGateway: true,
            adapterName: "Ethernet",
            adapterDescription: "Intel(R) Ethernet Connection");

        Assert.True(lan > apipa);
    }
}