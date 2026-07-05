using System.Net.Sockets;
using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public sealed class FileServerAuthorizationTests : IDisposable
{
    private readonly string _shared;
    private readonly FileServer _server;
    private readonly int _port;

    public FileServerAuthorizationTests()
    {
        _shared = Path.Combine(Path.GetTempPath(), "LanCopyAuth_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_shared);
        ShareRoot.SetRoot(_shared);
        File.WriteAllText(Path.Combine(_shared, "deny.txt"), "x");
        _server = new FileServer
        {
            RestrictToShareRoot = true,
            TlsEnabled = false,
            AuthorizePeerCommand = (_, cmd) => !string.Equals(cmd, "delete", StringComparison.Ordinal)
        };
        _server.Start(0);
        _port = _server.Port;
    }

    [Fact]
    public async Task AuthorizationPolicy_BlocksDeniedCommand()
    {
        var client = new LanClient("127.0.0.1", _port) { UseTls = false };
        await Assert.ThrowsAnyAsync<Exception>(() => client.DeleteAsync("deny.txt"));
        Assert.True(File.Exists(Path.Combine(_shared, "deny.txt")));
    }


    public void Dispose()
    {
        _server.Stop();
        try { Directory.Delete(_shared, recursive: true); } catch { }
    }
}
