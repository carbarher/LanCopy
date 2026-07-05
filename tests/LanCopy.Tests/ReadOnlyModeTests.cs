using System.IO;
using System.Net.Sockets;
using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public class ReadOnlyModeTests : IDisposable
{
    private readonly FileServer _server;
    private readonly int _port;
    private readonly string _shared;

    public ReadOnlyModeTests()
    {
        _shared = Path.Combine(Path.GetTempPath(), "LanCopyRO_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_shared);
        ShareRoot.SetRoot(_shared);
        _server = new FileServer { RestrictToShareRoot = true, ReadOnly = true, AuthorizePeerCommand = (_, _) => true };
        _server.Start(0);
        _port = _server.Port;
    }


    private LanClient Client() => new("127.0.0.1", _port);

    [Fact]
    public async Task ReadOnly_BlocksUpload()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), "lc_ro_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(srcDir);
        var srcFile = Path.Combine(srcDir, "x.txt");
        await File.WriteAllTextAsync(srcFile, "data");

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await Client().UploadAsync(srcFile, "x.txt"));

        Assert.False(File.Exists(Path.Combine(_shared, "x.txt")));
    }

    [Fact]
    public async Task ReadOnly_BlocksDelete()
    {
        var f = Path.Combine(_shared, "keep.txt");
        await File.WriteAllTextAsync(f, "keep");
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await Client().DeleteAsync("keep.txt"));
        Assert.True(File.Exists(f));
    }

    [Fact]
    public async Task ReadOnly_BlocksMkdir()
    {
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await Client().CreateDirectoryAsync("cant-create"));
        Assert.False(Directory.Exists(Path.Combine(_shared, "cant-create")));
    }

    [Fact]
    public async Task ReadOnly_StillAllowsDownload()
    {
        var f = Path.Combine(_shared, "r.txt");
        await File.WriteAllTextAsync(f, "readable");
        var outFile = Path.Combine(Path.GetTempPath(), "ro_out_" + Guid.NewGuid().ToString("N") + ".txt");
        await Client().DownloadAsync("r.txt", outFile);
        Assert.Equal("readable", await File.ReadAllTextAsync(outFile));
    }

    public void Dispose()
    {
        try { _server.Stop(); } catch { }
    }
}
