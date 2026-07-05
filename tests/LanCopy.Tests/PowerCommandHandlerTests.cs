using System.Text.Json;
using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public sealed class PowerCommandHandlerTests : IDisposable
{
    private readonly string _shared;
    private readonly FileServer _server;
    private readonly PowerCommandHandler _handler;
    private readonly List<string> _executedPowerActions = new();

    public PowerCommandHandlerTests()
    {
        _shared = Path.Combine(Path.GetTempPath(), "LanCopyPower_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_shared);
        ShareRoot.SetRoot(_shared);
        _server = new FileServer
        {
            RestrictToShareRoot = true,
            TlsEnabled = true,
            RemotePowerEnabled = false,
            RequiredPin = null,
            AuthorizePeerCommand = (_, _) => true,
            ApproveHighRisk = (_, _) => Task.FromResult(true)
        };
        _handler = new PowerCommandHandler(_server, _server.AuthorizePeerCommand, action =>
        {
            _executedPowerActions.Add(action);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task PowerCommand_WithoutPin_IsRejected_WithPowerPinRequired()
    {
        _server.RemotePowerEnabled = true;
        _server.RequiredPin = null; // No PIN configured

        var req = JsonDocument.Parse("""{"cmd":"power","action":"shutdown"}""").RootElement;
        var stream = new MemoryStream();

        var result = await _handler.AuthorizeAsync(req, "127.0.0.1", stream, CancellationToken.None);

        Assert.False(result);
        var payload = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("powerPinRequired", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PowerCommand_WithPowerDisabled_IsRejected_WithPowerDisabled()
    {
        _server.RemotePowerEnabled = false;
        _server.RequiredPin = "1234";

        var req = JsonDocument.Parse("""{"cmd":"power","action":"shutdown"}""").RootElement;
        var stream = new MemoryStream();

        var result = await _handler.AuthorizeAsync(req, "127.0.0.1", stream, CancellationToken.None);

        Assert.False(result);
        var payload = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("powerDisabled", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PowerCommand_WithInvalidAction_IsRejected_WithBadRequest()
    {
        _server.RemotePowerEnabled = true;
        _server.RequiredPin = "1234";

        var req = JsonDocument.Parse("""{"cmd":"power","action":"invalid_action"}""").RootElement;
        var stream = new MemoryStream();

        var result = await _handler.AuthorizeAsync(req, "127.0.0.1", stream, CancellationToken.None);

        Assert.False(result);
        var payload = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("badRequest", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PowerCommand_WithMissingAction_IsRejected()
    {
        _server.RemotePowerEnabled = true;
        _server.RequiredPin = "1234";

        var req = JsonDocument.Parse("""{"cmd":"power"}""").RootElement;
        var stream = new MemoryStream();

        var result = await _handler.AuthorizeAsync(req, "127.0.0.1", stream, CancellationToken.None);

        Assert.False(result);
        var payload = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("badRequest", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PowerCommand_WithoutPeerPermission_IsRejected()
    {
        _server.RemotePowerEnabled = true;
        _server.RequiredPin = "1234";
        // Create a new handler that denies power permission for this peer
        var handler = new PowerCommandHandler(_server, (_, cmd) => cmd != "power", _ => Task.CompletedTask);

        var req = JsonDocument.Parse("""{"cmd":"power","action":"shutdown"}""").RootElement;
        var stream = new MemoryStream();

        var result = await handler.AuthorizeAsync(req, "127.0.0.1", stream, CancellationToken.None);

        Assert.False(result);
        var payload = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("accessDenied", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PowerCommand_RequiresTls_IsRejected_WithoutTls()
    {
        _server.TlsEnabled = false;
        _server.RemotePowerEnabled = true;
        _server.RequiredPin = "1234";

        var req = JsonDocument.Parse("""{"cmd":"power","action":"shutdown"}""").RootElement;
        var stream = new MemoryStream();

        var result = await _handler.AuthorizeAsync(req, "127.0.0.1", stream, CancellationToken.None);

        Assert.False(result);
        var payload = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("tlsRequired", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PowerCommand_ValidShutdown_IsAuthorized()
    {
        _server.TlsEnabled = true;
        _server.RemotePowerEnabled = true;
        _server.RequiredPin = "1234";

        var req = JsonDocument.Parse("""{"cmd":"power","action":"shutdown"}""").RootElement;
        var stream = new MemoryStream();

        var result = await _handler.AuthorizeAsync(req, "127.0.0.1", stream, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task PowerCommand_ValidReboot_IsAuthorized()
    {
        _server.TlsEnabled = true;
        _server.RemotePowerEnabled = true;
        _server.RequiredPin = "1234";

        var req = JsonDocument.Parse("""{"cmd":"power","action":"reboot"}""").RootElement;
        var stream = new MemoryStream();

        var result = await _handler.AuthorizeAsync(req, "127.0.0.1", stream, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task PowerCommand_Handle_EnforcesCooldown()
    {
        _server.TlsEnabled = true;
        _server.RemotePowerEnabled = true;
        _server.RequiredPin = "1234";

        var req = JsonDocument.Parse("""{"cmd":"power","action":"shutdown"}""").RootElement;
        var stream = new MemoryStream();

        // First call should succeed (no cooldown yet)
        var firstStream = new MemoryStream();
        await _handler.HandleAsync(req, firstStream, CancellationToken.None);
        var firstResponse = System.Text.Encoding.UTF8.GetString(firstStream.ToArray());
        Assert.Contains("ok", firstResponse, StringComparison.OrdinalIgnoreCase);

        // Second call immediately after should be on cooldown
        var secondStream = new MemoryStream();
        await _handler.HandleAsync(req, secondStream, CancellationToken.None);
        var secondResponse = System.Text.Encoding.UTF8.GetString(secondStream.ToArray());
        Assert.Contains("cooldown", secondResponse, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PowerCommand_Handle_WithMissingAction_ReturnsBadRequest()
    {
        var req = JsonDocument.Parse("""{"cmd":"power"}""").RootElement;
        var stream = new MemoryStream();

        await _handler.HandleAsync(req, stream, CancellationToken.None);
        var response = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("badRequest", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PowerCommand_Handle_WithInvalidAction_ReturnsBadRequest()
    {
        var req = JsonDocument.Parse("""{"cmd":"power","action":"invalid_action"}""").RootElement;
        var stream = new MemoryStream();

        await _handler.HandleAsync(req, stream, CancellationToken.None);
        var response = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("badRequest", response, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_executedPowerActions);
    }

    [Fact]
    public async Task PowerCommand_ApprovalCallback_CanReject()
    {
        _server.TlsEnabled = true;
        _server.RemotePowerEnabled = true;
        _server.RequiredPin = "1234";
        // Set approval callback to always reject
        _server.ApproveHighRisk = (_, _) => Task.FromResult(false);

        var req = JsonDocument.Parse("""{"cmd":"power","action":"shutdown"}""").RootElement;
        var stream = new MemoryStream();

        var result = await _handler.AuthorizeAsync(req, "127.0.0.1", stream, CancellationToken.None);

        Assert.False(result);
        var payload = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("rejected", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PowerCommand_UsesLatestServerAuthorizer_WhenCachedHandlerExists()
    {
        _server.TlsEnabled = true;
        _server.RemotePowerEnabled = true;
        _server.RequiredPin = "1234";
        _server.AuthorizePeerCommand = (_, _) => true;
        var handler = new PowerCommandHandler(
            _server,
            (host, command) => (_server.AuthorizePeerCommand ?? CommandAuthorizer.IsAllowed)(host, command),
            _ => Task.CompletedTask);

        var req = JsonDocument.Parse("""{"cmd":"power","action":"shutdown"}""").RootElement;
        var firstStream = new MemoryStream();
        Assert.True(await handler.AuthorizeAsync(req, "127.0.0.1", firstStream, CancellationToken.None));

        _server.AuthorizePeerCommand = (_, _) => false;
        var secondStream = new MemoryStream();
        Assert.False(await handler.AuthorizeAsync(req, "127.0.0.1", secondStream, CancellationToken.None));
        var payload = System.Text.Encoding.UTF8.GetString(secondStream.ToArray());
        Assert.Contains("accessDenied", payload, StringComparison.OrdinalIgnoreCase);
    }


    public void Dispose()
    {
        _server?.Stop();
        try { Directory.Delete(_shared, recursive: true); } catch { }
    }
}
