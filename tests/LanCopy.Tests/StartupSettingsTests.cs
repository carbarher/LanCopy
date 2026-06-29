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
        Assert.False(cfg.SafeModeNoRemoteDelete);
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
                restrictShareRoot = false,
                readOnly = true,
                requireApproval = true,
                safeModeNoRemoteDelete = true,
            });
            File.WriteAllText(p, json);

            var cfg = StartupSettings.Load(p);
            Assert.Equal(9001, cfg.LocalPort);
            Assert.Equal("secret-pin", cfg.RequiredPin);
            Assert.False(cfg.TlsEnabled);
            Assert.False(cfg.RestrictShareRoot);
            Assert.True(cfg.ReadOnly);
            Assert.True(cfg.RequireApproval);
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
}
