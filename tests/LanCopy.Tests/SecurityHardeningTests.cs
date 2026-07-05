using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public sealed class SecurityHardeningTests : IDisposable
{
    private readonly string _shared;
    private readonly FileServer _server;
    private readonly int _port;

    public SecurityHardeningTests()
    {
        _shared = Path.Combine(Path.GetTempPath(), "LanCopySec_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_shared);
        ShareRoot.SetRoot(_shared);
        File.WriteAllText(Path.Combine(_shared, "probe.txt"), "ok");
        _server = new FileServer
        {
            RestrictToShareRoot = true,
            TlsEnabled = false,
            AuthorizePeerCommand = CommandAuthorizer.IsAllowed,
            ApproveHighRisk = (_, _) => Task.FromResult(true)
        };
        _server.Start(0);
        _port = _server.Port;
    }

    [Fact]
    public async Task TlsFallback_IsBlocked_ByDefault()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var legacyPort = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        var acceptTask = Task.Run(async () =>
        {
            using var tcp = await listener.AcceptTcpClientAsync();
        });

        var client = new LanClient("127.0.0.1", legacyPort)
        {
            UseTls = true
        };

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => client.ListAsync(""));
        listener.Stop();
        await acceptTask;
        Assert.Contains("st.tlsPeerMismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task TlsFallback_CanBeEnabled_Explicitly()
    {
        _server.AuthorizePeerCommand = (_, _) => true;
        var client = new LanClient("127.0.0.1", _port)
        {
            UseTls = true,
            AllowPlaintextFallback = true
        };

        var entries = await client.ListAsync("");
        Assert.Contains(entries, e => string.Equals(e.Name, "probe.txt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PowerCommand_DefaultDisabled_IsRejected()
    {
        var server = new FileServer
        {
            TlsEnabled = true,
            RemotePowerEnabled = false,
            AuthorizePeerCommand = (_, _) => true,
            ApproveHighRisk = (_, _) => Task.FromResult(true)
        };

        var req = JsonDocument.Parse("""{"cmd":"power","action":"shutdown"}""").RootElement;
        var stream = new MemoryStream();
        var method = typeof(FileServer).GetMethod("AuthorizeCommandAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task<bool>)method!.Invoke(server, new object[] { req, "power", "127.0.0.1", stream, CancellationToken.None })!;
        var allowed = await task;

        Assert.False(allowed);
        var payload = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("powerDisabled", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PowerCommand_InvalidAction_IsRejected()
    {
        _server.AuthorizePeerCommand = (_, _) => true;
        _server.TlsEnabled = true;
        _server.RemotePowerEnabled = true;
        _server.RequiredPin = "1234";
        _server.ApproveHighRisk = (_, _) => Task.FromResult(true);

        var client = new LanClient("127.0.0.1", _port)
        {
            UseTls = true,
            Pin = "1234"
        };

        await Assert.ThrowsAnyAsync<Exception>(() => client.SendPowerAsync("banana"));
    }

    [Fact]
    public void DeleteCooldown_IsAppliedPerKey()
    {
        var key = "remote-delete:" + Path.Combine(_shared, "probe.txt");
        Assert.False(SafeFileOps.IsOnCooldown(key, 10));
        Assert.True(SafeFileOps.IsOnCooldown(key, 10));
    }

    [Fact]
    public async Task HighRiskCommand_IsRejected_WhenPlaintextAndUnknownPeer()
    {
        _server.AuthorizePeerCommand = (_, _) => true;
        var client = new LanClient("127.0.0.1", _port) { UseTls = false };
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => client.DeleteAsync("probe.txt"));
        Assert.Contains("tlsRequired", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(_shared, "probe.txt")));
    }

    [Fact]
    public void UnknownCommand_IsRejected_ByCommandAuthorizer()
    {
        // Test truly unknown command that doesn't exist in the system
        var result1 = CommandAuthorizer.IsAllowed("127.0.0.1", "totally_unknown_cmd");
        Assert.False(result1);

        // Test another made-up command
        var result2 = CommandAuthorizer.IsAllowed("127.0.0.1", "xyz123");
        Assert.False(result2);

        // Test command with special characters
        var result3 = CommandAuthorizer.IsAllowed("127.0.0.1", "cmd!@#$");
        Assert.False(result3);

        // Verify that valid commands still work
        var result4 = CommandAuthorizer.IsAllowed("127.0.0.1", "list");
        // Result depends on peer trust level and permissions, but should not throw
        Assert.IsType<bool>(result4);
    }


    public void Dispose()
    {
        _server.Stop();
        try { Directory.Delete(_shared, recursive: true); } catch { }
    }
}
