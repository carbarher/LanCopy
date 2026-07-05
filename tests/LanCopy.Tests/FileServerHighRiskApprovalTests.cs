using System.Net.Sockets;
using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public sealed class FileServerHighRiskApprovalTests : IDisposable
{
    private readonly string _shared;
    private readonly FileServer _server;
    private readonly int _port;

    public FileServerHighRiskApprovalTests()
    {
        _shared = Path.Combine(Path.GetTempPath(), "LanCopyRisk_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_shared);
        ShareRoot.SetRoot(_shared);
        File.WriteAllText(Path.Combine(_shared, "danger.txt"), "x");
        _server = new FileServer
        {
            RestrictToShareRoot = true,
            TlsEnabled = true,
            AuthorizePeerCommand = (_, _) => true,
            ApproveHighRisk = (_, _) => Task.FromResult(true)
        };
        _server.Start(0);
        _port = _server.Port;
    }

    [Fact]
    public async Task HighRiskApproval_BlocksDelete_WhenNotApproved()
    {
        _server.TlsEnabled = true;
        _server.ApproveHighRisk = (_, _) => Task.FromResult(false);
        var client = new LanClient("127.0.0.1", _port) { UseTls = true };
        await Assert.ThrowsAnyAsync<Exception>(() => client.DeleteAsync("danger.txt"));
        Assert.True(File.Exists(Path.Combine(_shared, "danger.txt")));
    }

    [Fact]
    public async Task HighRiskCommands_AreBlocked_InPlaintext()
    {
        _server.TlsEnabled = false;
        _server.ApproveHighRisk = (_, _) => Task.FromResult(true);
        var client = new LanClient("127.0.0.1", _port) { UseTls = false };
        await Assert.ThrowsAnyAsync<Exception>(() => client.DeleteAsync("danger.txt"));
        Assert.True(File.Exists(Path.Combine(_shared, "danger.txt")));
    }


    public void Dispose()
    {
        _server.Stop();
        try { Directory.Delete(_shared, recursive: true); } catch { }
    }
}
