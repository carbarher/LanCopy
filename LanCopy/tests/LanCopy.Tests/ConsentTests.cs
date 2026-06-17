using System.IO;
using System.Net.Sockets;
using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public class ConsentTests : IDisposable
{
    private readonly FileServer _server;
    private readonly int _port;
    private readonly string _shared;
    private bool _approve = true;

    public ConsentTests()
    {
        _shared = Path.Combine(Path.GetTempPath(), "LanCopyConsent_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_shared);
        ShareRoot.SetRoot(_shared);

        _port = GetFreePort();
        _server = new FileServer { RestrictToShareRoot = true };
        _server.ApproveIncoming = (info, ct) => Task.FromResult(_approve);
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

    private async Task<string> MakeSrcAsync(int bytes)
    {
        var dir = Path.Combine(Path.GetTempPath(), "lc_cs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var f = Path.Combine(dir, "f.bin");
        var data = new byte[bytes];
        new Random(7).NextBytes(data);
        await File.WriteAllBytesAsync(f, data);
        return f;
    }

    [Fact]
    public async Task Rejected_Upload_GivesCleanError_AndNoFile()
    {
        _approve = false;
        var src = await MakeSrcAsync(800_000); // big enough to exercise body drain
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await Client().UploadAsync(src, "f.bin"));
        // El mensaje del servidor debe llegar limpio (no un reset de socket).
        Assert.Equal("svc.rejected", ex.Message);
        Assert.False(File.Exists(Path.Combine(_shared, "f.bin")));
    }

    [Fact]
    public async Task Approved_Upload_Succeeds()
    {
        _approve = true;
        var src = await MakeSrcAsync(500_000);
        await Client().UploadAsync(src, "f.bin");
        Assert.True(File.Exists(Path.Combine(_shared, "f.bin")));
        Assert.Equal(await File.ReadAllBytesAsync(src), await File.ReadAllBytesAsync(Path.Combine(_shared, "f.bin")));
    }

    public void Dispose()
    {
        try { _server.Stop(); } catch { }
    }
}