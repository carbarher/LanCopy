using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public sealed class PeerPermissionStoreTests
{
    [Fact]
    public void DefaultPermissions_AreSafe()
    {
        var store = new PeerPermissionStore(Path.Combine(Path.GetTempPath(), "LanCopyPerms_" + Guid.NewGuid().ToString("N") + ".json"));
        var p = store.Get("192.168.1.50");
        Assert.True(p.Browse);
        Assert.True(p.Download);
        Assert.False(p.Upload);
        Assert.False(p.Modify);
        Assert.False(p.Delete);
        Assert.False(p.Sync);
        Assert.False(p.Clipboard);
        Assert.False(p.Power);
    }

    [Fact]
    public void SavingPermissions_StoresUpdateTime()
    {
        var path = Path.Combine(Path.GetTempPath(), "LanCopyPerms_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new PeerPermissionStore(path);
            store.Set("192.168.1.51", new PeerPermissionStore.Permissions(Upload: true));
            var updated = store.GetLastUpdatedUtc("192.168.1.51");
            Assert.NotNull(updated);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void LegacyDictionaryFormat_IsMigrated()
    {
        var path = Path.Combine(Path.GetTempPath(), "LanCopyPerms_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, """
            {
              "192.168.1.99": {
                "browse": true,
                "download": true,
                "upload": true,
                "modify": false,
                "delete": false,
                "sync": false,
                "clipboard": false,
                "power": false
              }
            }
            """);

            var store = new PeerPermissionStore(path);
            var p = store.Get("192.168.1.99");
            Assert.True(p.Upload);
            Assert.True(p.Browse);
            Assert.True(p.Download);
            Assert.False(p.Power);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void LegacySnapshotFormat_IsMigrated()
    {
        var path = Path.Combine(Path.GetTempPath(), "LanCopyPerms_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, """
            {
              "hosts": {
                "192.168.1.100": {
                  "browse": true,
                  "download": true,
                  "upload": false,
                  "modify": true,
                  "delete": false,
                  "sync": false,
                  "clipboard": false,
                  "power": false
                }
              }
            }
            """);

            var store = new PeerPermissionStore(path);
            var p = store.Get("192.168.1.100");
            Assert.True(p.Modify);
            Assert.True(p.Browse);
            Assert.True(p.Download);
            Assert.False(p.Delete);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void CorruptFile_FallsBackToSafeDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "LanCopyPerms_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, "{ not-json");
            var store = new PeerPermissionStore(path);
            var p = store.Get("192.168.1.200");
            Assert.True(p.Browse);
            Assert.True(p.Download);
            Assert.False(p.Upload);
            Assert.False(p.Delete);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ResetPermissions_ReturnsSafeDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "LanCopyPerms_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new PeerPermissionStore(path);
            store.Set("192.168.1.201", new PeerPermissionStore.Permissions(
                Browse: true,
                Download: true,
                Upload: true,
                Modify: true,
                Delete: true,
                Sync: true,
                Clipboard: true,
                Power: true));

            store.Set("192.168.1.201", new PeerPermissionStore.Permissions());

            var p = store.Get("192.168.1.201");
            Assert.True(p.Browse);
            Assert.True(p.Download);
            Assert.False(p.Upload);
            Assert.False(p.Modify);
            Assert.False(p.Delete);
            Assert.False(p.Sync);
            Assert.False(p.Clipboard);
            Assert.False(p.Power);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void LegacyPermissionsWithoutDangerousFields_DoNotEnableThem()
    {
        var path = Path.Combine(Path.GetTempPath(), "LanCopyPerms_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, """
            {
              "192.168.1.202": {
                "browse": true,
                "download": true
              }
            }
            """);

            var store = new PeerPermissionStore(path);
            var p = store.Get("192.168.1.202");
            Assert.True(p.Browse);
            Assert.True(p.Download);
            Assert.False(p.Upload);
            Assert.False(p.Delete);
            Assert.False(p.Power);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
