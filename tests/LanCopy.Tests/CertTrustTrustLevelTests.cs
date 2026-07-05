using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public sealed class CertTrustTrustLevelTests
{
    [Fact]
    public void UnknownHost_DefaultsToUnknown()
    {
        var host = "test-" + Guid.NewGuid().ToString("N") + ".local";
        Assert.Equal(CertTrust.PeerTrustLevel.Unknown, CertTrust.GetTrustLevel(host));
    }

    [Fact]
    public void SetTrustLevel_ReturnsFalse_ForUnknownHost()
    {
        var host = "test-" + Guid.NewGuid().ToString("N") + ".local";
        Assert.False(CertTrust.SetTrustLevel(host, CertTrust.PeerTrustLevel.Trusted));
    }
}
