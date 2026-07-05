using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LanCopy.Models;

namespace LanCopy.Services;

public sealed partial class LanClient
{
    /// <summary>
    /// Performs PIN-based authentication with the server if required.
    /// If server requires PIN (via Feature 10), sends PIN in auth request and validates response.
    /// This is a security measure to prevent unauthorized access.
    /// </summary>
    private async Task AuthenticateWithPinAsync(Stream stream, CancellationToken ct)
    {
        // If no PIN is configured, skip authentication
        if (string.IsNullOrEmpty(Pin))
            return;

        // Send auth request with PIN to server
        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { cmd = "auth", pin = Pin }), ct);
        
        // Read and validate server's response
        var ackLine = await Protocol.ReadLineAsync(stream, ct);
        var ack = JsonSerializer.Deserialize<JsonElement>(ackLine);
        
        // Ensure server accepted the authentication (status = "ok")
        EnsureOk(ack);
    }
}
