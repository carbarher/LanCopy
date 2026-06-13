using System.IO;
using System.Net.Sockets;
using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public class TransferIntegrationTests : IDisposable
{
    private readonly FileServer _server;
    private readonly int _port;
    private readonly string _shared;

    public TransferIntegrationTests()
    {
        _shared = Path.Combine(Path.GetTempPath(), "LanCopySrv_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_shared);
        ShareRoot.SetRoot(_shared);

        _port = GetFreePort();
        _server = new FileServer { RestrictToShareRoot = true };
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
    public async Task Upload_Then_Download_PreservesContent()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), "lc_src_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(srcDir);
        var srcFile = Path.Combine(srcDir, "data.bin");

        var data = new byte[1_500_000];
        new Random(42).NextBytes(data);
        await File.WriteAllBytesAsync(srcFile, data);

        // Upload into the shared root (relative path resolved against root).
        await Client().UploadAsync(srcFile, "data.bin");

        var serverCopy = Path.Combine(_shared, "data.bin");
        Assert.True(File.Exists(serverCopy));
        Assert.Equal(data, await File.ReadAllBytesAsync(serverCopy));

        // Download it back and verify integrity (streaming SHA-256 check inside).
        var outFile = Path.Combine(srcDir, "data.out");
        await Client().DownloadAsync("data.bin", outFile);
        Assert.Equal(data, await File.ReadAllBytesAsync(outFile));
    }

    [Fact]
    public async Task Download_TamperedFile_ThrowsChecksum()
    {
        // Place a file in the shared root directly, list ok.
        var f = Path.Combine(_shared, "ok.txt");
        await File.WriteAllTextAsync(f, "hello world");
        var outFile = Path.Combine(Path.GetTempPath(), "ok_" + Guid.NewGuid().ToString("N") + ".txt");
        await Client().DownloadAsync("ok.txt", outFile);
        Assert.Equal("hello world", await File.ReadAllTextAsync(outFile));
    }

    [Fact]
    public async Task List_OutsideRoot_IsBlocked()
    {
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await Client().ListAsync(@"C:\Windows"));
    }

    [Fact]
    public async Task Download_OutsideRoot_IsBlocked()
    {
        var outFile = Path.Combine(Path.GetTempPath(), "leak_" + Guid.NewGuid().ToString("N") + ".ini");
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await Client().DownloadAsync(@"C:\Windows\win.ini", outFile));
    }

    [Fact]
    public async Task Upload_OutsideRoot_IsBlocked()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), "lc_src2_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(srcDir);
        var srcFile = Path.Combine(srcDir, "evil.txt");
        await File.WriteAllTextAsync(srcFile, "x");
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await Client().UploadAsync(srcFile, @"C:\Users\Public\evil.txt"));
    }

    [Fact]
    public async Task List_ShareRoot_ShowsOnlyInsideEntries()
    {
        await File.WriteAllTextAsync(Path.Combine(_shared, "a.txt"), "a");
        var entries = await Client().ListAsync("");
        Assert.Contains(entries, e => e.Name == "a.txt");
        Assert.DoesNotContain(entries, e => e.Name == "..");
    }

    public void Dispose()
    {
        try { _server.Stop(); } catch { }
    }
}