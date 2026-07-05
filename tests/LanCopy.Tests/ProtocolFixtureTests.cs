using System.Net.Sockets;
using System.Text.Json;
using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public sealed class ProtocolFixtureTests : IDisposable
{
    private readonly FileServer _server;
    private readonly int _port;
    private readonly string _shared;

    public ProtocolFixtureTests()
    {
        _shared = Path.Combine(Path.GetTempPath(), "LanCopyProto_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_shared);
        ShareRoot.SetRoot(_shared);
        _server = new FileServer
        {
            RestrictToShareRoot = true,
            TlsEnabled = false,
            AuthorizePeerCommand = (_, _) => true
        };
        _server.Start(0);
        _port = _server.Port;
    }

    public void Dispose()
    {
        try { _server.Stop(); } catch { }
        try { Directory.Delete(_shared, recursive: true); } catch { }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task ProtocolFixtures_MatchExpectedShape(string fixturePath, string expectedStatus, string expectedError)
    {
        var requestJson = await File.ReadAllTextAsync(fixturePath);
        var response = await RoundTripAsync(requestJson);

        Assert.Equal(expectedStatus, response.GetProperty("status").GetString());
        if (expectedStatus == "error")
        {
            Assert.Equal(expectedError, response.GetProperty("error").GetString());
            return;
        }

        Assert.Equal("ok", response.GetProperty("status").GetString());
    }

    public static IEnumerable<object[]> Fixtures()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "fixtures", "protocol", "v1");
        yield return new object[] { Path.Combine(root, "caps.json"), "ok", "" };
        yield return new object[] { Path.Combine(root, "health.json"), "ok", "" };
        yield return new object[] { Path.Combine(root, "unknown.json"), "error", "svc.unknownCmd" };
    }

    private async Task<JsonElement> RoundTripAsync(string requestJson)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync("127.0.0.1", _port);
        await using var stream = tcp.GetStream();
        await Protocol.WriteLineAsync(stream, requestJson, CancellationToken.None);
        var line = await Protocol.ReadLineAsync(stream, CancellationToken.None);
        return JsonSerializer.Deserialize<JsonElement>(line);
    }

}
