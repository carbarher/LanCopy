using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public sealed class CommandAuthorizerTests
{
    [Theory]
    [InlineData("put")]
    [InlineData("put_resume")]
    [InlineData("rename")]
    [InlineData("mkdir")]
    [InlineData("delete")]
    [InlineData("text")]
    [InlineData("clipboard/text")]
    [InlineData("delta_hashes")]
    [InlineData("put_delta_blocks")]
    [InlineData("power")]
    public void NewPeer_CannotUseDangerousCommands(string cmd)
    {
        var host = "peer-" + Guid.NewGuid().ToString("N") + ".local";
        Assert.False(CommandAuthorizer.IsAllowed(host, cmd));
    }

    [Theory]
    [InlineData("caps")]
    [InlineData("health")]
    [InlineData("disconnect_notice")]
    public void NewPeer_CanUseMinimalSafeCommands(string cmd)
    {
        var host = "peer-" + Guid.NewGuid().ToString("N") + ".local";
        Assert.True(CommandAuthorizer.IsAllowed(host, cmd));
    }

    [Theory]
    [InlineData("list")]
    [InlineData("get")]
    [InlineData("search")]
    [InlineData("stat")]
    [InlineData("put")]
    public void UnknownPeer_KnownRestrictedCommands_AreBlocked(string cmd)
    {
        var host = "peer-" + Guid.NewGuid().ToString("N") + ".local";
        Assert.False(CommandAuthorizer.IsAllowed(host, cmd));
    }

    [Fact]
    public void TrustedPeer_StillRequiresExplicitPermission()
    {
        var trusted = CertTrust.PeerTrustLevel.Trusted;
        var safe = new PeerPermissionStore.Permissions(Browse: true, Download: true);
        var locked = new PeerPermissionStore.Permissions(Browse: true, Download: true);

        Assert.True(CommandAuthorizer.IsAllowed(trusted, safe, "list"));
        Assert.False(CommandAuthorizer.IsAllowed(trusted, locked, "put"));
        Assert.False(CommandAuthorizer.IsAllowed(trusted, locked, "delete"));
        Assert.False(CommandAuthorizer.IsAllowed(trusted, locked, "power"));
        Assert.False(CommandAuthorizer.IsAllowed(trusted, locked, "delta_hashes"));
        Assert.True(CommandAuthorizer.IsAllowed(trusted, locked, "text"));
        Assert.False(CommandAuthorizer.IsAllowed(trusted, locked, "clipboard/text"));
    }

    [Fact]
    public void AdvancedTrustedPreset_AllowsDangerousCommandsWhenEnabled()
    {
        var trusted = CertTrust.PeerTrustLevel.Trusted;
        var advanced = new PeerPermissionStore.Permissions(
            Browse: true,
            Download: true,
            Upload: true,
            Modify: true,
            Delete: true,
            Sync: true,
            Clipboard: true,
            Power: true);

        Assert.True(CommandAuthorizer.IsAllowed(trusted, advanced, "put"));
        Assert.True(CommandAuthorizer.IsAllowed(trusted, advanced, "delete"));
        Assert.True(CommandAuthorizer.IsAllowed(trusted, advanced, "power"));
        Assert.True(CommandAuthorizer.IsAllowed(trusted, advanced, "delta_hashes"));
    }
}


