using System.IO;
using System.Text.Json;
using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public class StartupSettingsTests
{
    private static string TempFile()
        => Path.Combine(Path.GetTempPath(), "LanCopySS_" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public void Load_MissingFile_ReturnsSecureDefaults()
    {
        var cfg = StartupSettings.Load(TempFile());
        Assert.Equal(StartupSettings.DefaultLocalPort, cfg.LocalPort);
        Assert.Null(cfg.RequiredPin);
        Assert.True(cfg.TlsEnabled);
        Assert.True(cfg.RestrictShareRoot);
        Assert.False(cfg.ReadOnly);
        Assert.False(cfg.RequireApproval);
        Assert.True(cfg.RequireHighRiskApproval);
        Assert.False(cfg.RemotePowerEnabled);
        Assert.True(cfg.SafeModeEnabled);
        Assert.True(cfg.SafeModeNoRemoteDelete);
    }

    [Fact]
    public void Load_RestoresPinBeforeServerStart()
    {
        var p = TempFile();
        try
        {
            var json = JsonSerializer.Serialize(new
            {
                localPort = 9001,
                pin = "secret-pin",
                tlsEnabled = false,
                restrictShareRoot = true,
                readOnly = true,
                requireApproval = true,
                requireHighRiskApproval = false,
                remotePowerEnabled = true,
                safeModeEnabled = false,
                safeModeNoRemoteDelete = true,
            });
            File.WriteAllText(p, json);

            var cfg = StartupSettings.Load(p);
            Assert.Equal(9001, cfg.LocalPort);
            Assert.Equal("secret-pin", cfg.RequiredPin);
            Assert.False(cfg.TlsEnabled);
            Assert.True(cfg.RestrictShareRoot);
            Assert.True(cfg.ReadOnly);
            Assert.True(cfg.RequireApproval);
            Assert.False(cfg.RequireHighRiskApproval);
            Assert.True(cfg.RemotePowerEnabled);
            Assert.False(cfg.SafeModeEnabled);
            Assert.True(cfg.SafeModeNoRemoteDelete);
        }
        finally
        {
            File.Delete(p);
        }
    }


    [Fact]
    public void Load_TemporaryMoreAccess_RestartsProtected()
    {
        var p = TempFile();
        try
        {
            var json = JsonSerializer.Serialize(new
            {
                tlsEnabled = true,
                restrictShareRoot = false,
                requireHighRiskApproval = true,
                remotePowerEnabled = false,
                safeModeEnabled = false,
                safeModeNoRemoteDelete = true
            });
            File.WriteAllText(p, json);

            var cfg = StartupSettings.Load(p);
            Assert.True(cfg.SafeModeEnabled);
            Assert.True(cfg.TlsEnabled);
            Assert.True(cfg.RestrictShareRoot);
            Assert.True(cfg.RequireHighRiskApproval);
            Assert.False(cfg.RemotePowerEnabled);
            Assert.True(cfg.SafeModeNoRemoteDelete);
        }
        finally
        {
            File.Delete(p);
        }
    }
    [Fact]
    public void Load_EmptyPin_IsNull()
    {
        var p = TempFile();
        try
        {
            File.WriteAllText(p, JsonSerializer.Serialize(new { pin = "   " }));
            Assert.Null(StartupSettings.Load(p).RequiredPin);
        }
        finally
        {
            File.Delete(p);
        }
    }

    [Fact]
    public void Load_SafeModeEnabled_EnforcesSecureServerDefaults()
    {
        var p = TempFile();
        try
        {
            var json = JsonSerializer.Serialize(new
            {
                tlsEnabled = false,
                restrictShareRoot = true,
                requireHighRiskApproval = false,
                remotePowerEnabled = true,
                safeModeEnabled = true,
                safeModeNoRemoteDelete = false
            });
            File.WriteAllText(p, json);

            var cfg = StartupSettings.Load(p);
            Assert.True(cfg.SafeModeEnabled);
            Assert.True(cfg.TlsEnabled);
            Assert.True(cfg.RestrictShareRoot);
            Assert.True(cfg.RequireHighRiskApproval);
            Assert.False(cfg.RemotePowerEnabled);
            Assert.True(cfg.SafeModeNoRemoteDelete);
        }
        finally
        {
            File.Delete(p);
        }
    }
}

