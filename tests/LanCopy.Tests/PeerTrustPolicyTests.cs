using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public sealed class PeerTrustPolicyTests
{
    [Theory]
    [InlineData(CertTrust.PeerTrustLevel.Unknown, "put")]
    [InlineData(CertTrust.PeerTrustLevel.Unknown, "put_resume")]
    [InlineData(CertTrust.PeerTrustLevel.Unknown, "rename")]
    [InlineData(CertTrust.PeerTrustLevel.Unknown, "mkdir")]
    [InlineData(CertTrust.PeerTrustLevel.Unknown, "delete")]
    [InlineData(CertTrust.PeerTrustLevel.Unknown, "power")]
    [InlineData(CertTrust.PeerTrustLevel.Unknown, "delta_hashes")]
    [InlineData(CertTrust.PeerTrustLevel.Unknown, "put_delta_blocks")]
    [InlineData(CertTrust.PeerTrustLevel.Paired, "delete")]
    [InlineData(CertTrust.PeerTrustLevel.Paired, "power")]
    [InlineData(CertTrust.PeerTrustLevel.Paired, "delta_hashes")]
    [InlineData(CertTrust.PeerTrustLevel.Paired, "put_delta_blocks")]
    public void HighRiskCommands_AreBlocked_WhenNotTrusted(CertTrust.PeerTrustLevel level, string cmd)
    {
        Assert.False(PeerTrustPolicy.IsAllowed(level, cmd));
    }

    [Theory]
    [InlineData(CertTrust.PeerTrustLevel.Trusted, "delete")]
    [InlineData(CertTrust.PeerTrustLevel.Trusted, "power")]
    [InlineData(CertTrust.PeerTrustLevel.Trusted, "delta_hashes")]
    [InlineData(CertTrust.PeerTrustLevel.Trusted, "put_delta_blocks")]
    [InlineData(CertTrust.PeerTrustLevel.OwnerDevice, "delete")]
    [InlineData(CertTrust.PeerTrustLevel.OwnerDevice, "power")]
    [InlineData(CertTrust.PeerTrustLevel.OwnerDevice, "delta_hashes")]
    [InlineData(CertTrust.PeerTrustLevel.OwnerDevice, "put_delta_blocks")]
    public void HighRiskCommands_AreAllowed_WhenTrustedOrOwner(CertTrust.PeerTrustLevel level, string cmd)
    {
        Assert.True(PeerTrustPolicy.IsAllowed(level, cmd));
    }

    [Theory]
    [InlineData("list")]
    [InlineData("get")]
    [InlineData("search")]
    [InlineData("stat")]
    [InlineData("put")]
    [InlineData("rename")]
    [InlineData("mkdir")]
    [InlineData("text")]
    public void PairedPeers_AllowNonHighRiskCommands_ToReachPermissionLayer(string cmd)
    {
        Assert.True(PeerTrustPolicy.IsAllowed(CertTrust.PeerTrustLevel.Paired, cmd));
    }

    [Theory]
    [InlineData("caps")]
    [InlineData("health")]
    [InlineData("disconnect_notice")]
    [InlineData("text")]
    public void UnknownPeers_OnlyGetMinimalCommands(string cmd)
    {
        Assert.True(PeerTrustPolicy.IsAllowed(CertTrust.PeerTrustLevel.Unknown, cmd));
    }

    [Theory]
    [InlineData("list")]
    [InlineData("get")]
    [InlineData("search")]
    [InlineData("stat")]
    [InlineData("put")]
    [InlineData("delete")]
    public void UnknownPeers_BlockEverythingElse(string cmd)
    {
        Assert.False(PeerTrustPolicy.IsAllowed(CertTrust.PeerTrustLevel.Unknown, cmd));
    }
}
