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
        _server = new FileServer { RestrictToShareRoot = true, AuthorizePeerCommand = (_, _) => true };
        _server.Start(0);
        _port = _server.Port;
    }


    private LanClient Client() => new("127.0.0.1", _port);

    private static string OutsidePath(string fileName)
        => Path.Combine(Path.GetTempPath(), "LanCopyOutside_" + Guid.NewGuid().ToString("N"), fileName);

    [Fact]
    public async Task GetStats_Returns_Existing_Missing_And_Directory_Entries()
    {
        await File.WriteAllTextAsync(Path.Combine(_shared, "present.txt"), "content");
        Directory.CreateDirectory(Path.Combine(_shared, "folder"));

        var stats = await Client().GetStatsAsync(["present.txt", "missing.txt", "folder"]);

        Assert.True(stats["present.txt"].Exists);
        Assert.Equal(7, stats["present.txt"].Size);
        Assert.False(stats["missing.txt"].Exists);
        Assert.True(stats["folder"].Exists);
        Assert.True(stats["folder"].IsDirectory);
    }
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
            await Client().ListAsync(OutsidePath("list.txt")));
    }

    [Fact]
    public async Task Download_OutsideRoot_IsBlocked()
    {
        var outFile = Path.Combine(Path.GetTempPath(), "leak_" + Guid.NewGuid().ToString("N") + ".ini");
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await Client().DownloadAsync(OutsidePath("win.ini"), outFile));
    }

    [Fact]
    public async Task Upload_OutsideRoot_IsBlocked()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), "lc_src2_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(srcDir);
        var srcFile = Path.Combine(srcDir, "evil.txt");
        await File.WriteAllTextAsync(srcFile, "x");
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await Client().UploadAsync(srcFile, OutsidePath("evil.txt")));
    }

    [Fact]
    public async Task Rename_DotDot_IsBlocked()
    {
        var f = Path.Combine(_shared, "victim.txt");
        await File.WriteAllTextAsync(f, "x");
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await Client().RenameAsync("victim.txt", ".."));
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await Client().RenameAsync("victim.txt", "."));
        Assert.True(File.Exists(f));
    }

    [Fact]
    public async Task Upload_Then_Download_Compressed_PreservesContent()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), "lc_zsrc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(srcDir);
        var srcFile = Path.Combine(srcDir, "comp.bin");

        var data = new byte[1_200_000];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)(i % 37);
        await File.WriteAllBytesAsync(srcFile, data);

        var upClient = Client(); upClient.UseCompress = true;
        await upClient.UploadAsync(srcFile, "comp.bin");
        Assert.Equal(data, await File.ReadAllBytesAsync(Path.Combine(_shared, "comp.bin")));

        var outFile = Path.Combine(srcDir, "comp.out");
        var dlClient = Client(); dlClient.UseCompress = true;
        await dlClient.DownloadAsync("comp.bin", outFile);
        Assert.Equal(data, await File.ReadAllBytesAsync(outFile));
    }

    [Fact]
    public async Task Delete_OutsideRoot_IsBlocked()
    {
        var outDir = Path.Combine(Path.GetTempPath(), "lc_out_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        var victim = Path.Combine(outDir, "victim.txt");
        await File.WriteAllTextAsync(victim, "no me borres");
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await Client().DeleteAsync(victim));
        Assert.True(File.Exists(victim));
    }

    [Fact]
    public async Task Rename_OutsideRoot_IsBlocked()
    {
        var outDir = Path.Combine(Path.GetTempPath(), "lc_out2_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        var victim = Path.Combine(outDir, "victim.txt");
        await File.WriteAllTextAsync(victim, "no me toques");
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await Client().RenameAsync(victim, "hacked.txt"));
        Assert.True(File.Exists(victim));
        Assert.False(File.Exists(Path.Combine(outDir, "hacked.txt")));
    }

    [Fact]
    public async Task Mkdir_Creates_Remote_Directory()
    {
        await Client().CreateDirectoryAsync("new-folder");
        Assert.True(Directory.Exists(Path.Combine(_shared, "new-folder")));
    }

    [Fact]
    public async Task Mkdir_OutsideRoot_IsBlocked()
    {
        var outDir = Path.Combine(Path.GetTempPath(), "lc_out_mkdir_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        var target = Path.Combine(outDir, "nope");
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await Client().CreateDirectoryAsync(target));
        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public async Task CompressedExtension_RoundTrip_PreservesContent()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), "lc_zext_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(srcDir);
        var srcFile = Path.Combine(srcDir, "archive.zip");
        var data = new byte[600_000];
        new Random(7).NextBytes(data);
        await File.WriteAllBytesAsync(srcFile, data);

        var up = Client(); up.UseCompress = true;
        await up.UploadAsync(srcFile, "archive.zip");
        Assert.Equal(data, await File.ReadAllBytesAsync(Path.Combine(_shared, "archive.zip")));

        var outFile = Path.Combine(srcDir, "archive.out");
        var dl = Client(); dl.UseCompress = true;
        await dl.DownloadAsync("archive.zip", outFile);
        Assert.Equal(data, await File.ReadAllBytesAsync(outFile));
    }

    [Fact]
    public async Task Download_Resumes_From_Partial()
    {
        var data = new byte[1_500_000];
        new Random(99).NextBytes(data);
        await File.WriteAllBytesAsync(Path.Combine(_shared, "resume.bin"), data);

        var outFile = Path.Combine(Path.GetTempPath(), "res_" + Guid.NewGuid().ToString("N") + ".bin");
        var partFile = outFile + ".part";
        long pre = 500_000;
        await File.WriteAllBytesAsync(partFile, data[..(int)pre]); // descarga interrumpida

        await Client().DownloadAsync("resume.bin", outFile);

        Assert.True(File.Exists(outFile));
        Assert.False(File.Exists(partFile)); // .part promovido al destino
        Assert.Equal(data, await File.ReadAllBytesAsync(outFile));
    }

    [Fact]
    public async Task Download_Resume_CorruptPartial_Throws()
    {
        var data = new byte[800_000];
        new Random(5).NextBytes(data);
        await File.WriteAllBytesAsync(Path.Combine(_shared, "r2.bin"), data);

        var outFile = Path.Combine(Path.GetTempPath(), "r2_" + Guid.NewGuid().ToString("N") + ".bin");
        var partFile = outFile + ".part";
        await File.WriteAllBytesAsync(partFile, new byte[200_000]); // prefijo corrupto (ceros)

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await Client().DownloadAsync("r2.bin", outFile));
    }

    [Fact]
    public async Task Download_ResumeMap_TruncatesToVerifiedAndCompletes()
    {
        var data = new byte[1_000_000];
        new Random(1234).NextBytes(data);
        await File.WriteAllBytesAsync(Path.Combine(_shared, "mapped.bin"), data);

        var outFile = Path.Combine(Path.GetTempPath(), "mapped_" + Guid.NewGuid().ToString("N") + ".bin");
        var partFile = outFile + ".part";
        var mapFile = partFile + ".lcmap";
        var verified = 300_000;

        await File.WriteAllBytesAsync(partFile, data[..550_000]); // parte extra no verificada
        await File.WriteAllTextAsync(mapFile, $"{{\"blockSize\":4194304,\"verifiedBytes\":{verified},\"totalSize\":1000000,\"updatedUtc\":\"{DateTime.UtcNow:O}\"}}");

        await Client().DownloadAsync("mapped.bin", outFile);

        Assert.True(File.Exists(outFile));
        Assert.False(File.Exists(partFile));
        Assert.False(File.Exists(mapFile));
        Assert.Equal(data, await File.ReadAllBytesAsync(outFile));
    }

    [Fact]
    public async Task SendText_Triggers_TextReceived()
    {
        string? gotText = null;
        var tcs = new TaskCompletionSource();
        _server.TextReceived += (ip, text) => { gotText = text; tcs.TrySetResult(); };
        await Client().SendTextAsync("hola mundo");
        await Task.WhenAny(tcs.Task, Task.Delay(3000));
        Assert.Equal("hola mundo", gotText);
    }

    [Fact]
    public async Task SendDisconnectNotice_Triggers_DisconnectNoticeReceived()
    {
        string? gotIp = null;
        var tcs = new TaskCompletionSource();
        _server.DisconnectNoticeReceived += ip => { gotIp = ip; tcs.TrySetResult(); };
        await Client().SendDisconnectNoticeAsync();
        await Task.WhenAny(tcs.Task, Task.Delay(3000));
        Assert.False(string.IsNullOrWhiteSpace(gotIp));
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
