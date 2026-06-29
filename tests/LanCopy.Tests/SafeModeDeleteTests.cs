using System.IO;
using System.Net.Sockets;
using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public class SafeModeDeleteTests : IDisposable
{
    private readonly FileServer _server;
    private readonly int _port;
    private readonly string _shared;

    public SafeModeDeleteTests()
    {
        _shared = Path.Combine(Path.GetTempPath(), "LanCopySafe_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_shared);
        ShareRoot.SetRoot(_shared);

        _port = GetFreePort();
        _server = new FileServer { RestrictToShareRoot = true, ReadOnly = false, SafeModeNoRemoteDelete = true };
        _server.Start(_port);
    }

    private static int GetFreePort()
    {
        var l = new TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private LanClient Client() => new("127.0.0.1", _port);

    [Fact]
    public async Task SafeMode_BlocksDeleteOnly()
    {
        var f = Path.Combine(_shared, "keep.txt");
        await File.WriteAllTextAsync(f, "keep");

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await Client().DeleteAsync("keep.txt"));

        Assert.True(File.Exists(f));
    }

    [Fact]
    public async Task SafeMode_AllowsUpload()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), "lc_safe_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(srcDir);
        var srcFile = Path.Combine(srcDir, "x.txt");
        await File.WriteAllTextAsync(srcFile, "data");

        await Client().UploadAsync(srcFile, "x.txt");

        var dest = Path.Combine(_shared, "x.txt");
        Assert.True(File.Exists(dest));
        Assert.Equal("data", await File.ReadAllTextAsync(dest));
    }

    public void Dispose()
    {
        try { _server.Stop(); } catch { }
    }
}
