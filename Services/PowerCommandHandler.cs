using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LanCopy.Services;

/// <summary>
/// Handles remote power commands (shutdown, reboot) with security checks and PIN validation.
/// This class encapsulates all power-related command logic to simplify FileServer.
/// </summary>
public sealed class PowerCommandHandler
{
    private readonly FileServer _server;
    private readonly Func<string, string, bool> _authorizePeerCommand;
    private readonly Func<string, Task> _executePowerAction;

    public PowerCommandHandler(
        FileServer server,
        Func<string, string, bool>? authorizePeerCommand = null,
        Func<string, Task>? executePowerAction = null)
    {
        _server = server;
        _authorizePeerCommand = authorizePeerCommand ?? CommandAuthorizer.IsAllowed;
        _executePowerAction = executePowerAction ?? ExecutePowerActionAsync;
    }

    /// <summary>
    /// Authorizes a power command request before processing.
    /// Returns true if the command is authorized, false otherwise.
    /// </summary>
    public async Task<bool> AuthorizeAsync(
        JsonElement req,
        string ip,
        Stream stream,
        CancellationToken ct)
    {
        if (!_authorizePeerCommand(ip, "power"))
        {
            SafeFileOps.Audit("power", ip, "blocked", "peer-policy-denied", "remote");
            await Protocol.WriteErrorAsync(stream, "svc.accessDenied", ct);
            return false;
        }

        if (!_server.TlsEnabled)
        {
            SafeFileOps.Audit("power", ip, "blocked", "tlsRequired", "remote");
            await Protocol.WriteErrorAsync(stream, "svc.tlsRequired", ct);
            return false;
        }

        if (!_server.RemotePowerEnabled)
        {
            SafeFileOps.Audit("power", ip, "blocked", "power.disabled", "remote");
            await Protocol.WriteErrorAsync(stream, "svc.powerDisabled", ct);
            return false;
        }

        if (string.IsNullOrWhiteSpace(_server.RequiredPin))
        {
            SafeFileOps.Audit("power", ip, "blocked", "pin-required", "remote");
            await Protocol.WriteErrorAsync(stream, "svc.powerPinRequired", ct);
            return false;
        }

        if (!TryGetPowerAction(req, out var action))
        {
            SafeFileOps.Audit("power", ip, "blocked", "power.invalid-action", "remote");
            await Protocol.WriteErrorAsync(stream, "svc.badRequest", ct);
            return false;
        }

        if (!await ApproveHighRiskCommandAsync("power", ip, action, stream, ct))
            return false;

        return true;
    }

    /// <summary>
    /// Processes a power command request after authorization has passed.
    /// Validates cooldown, sends acknowledgment, then executes the power action in background.
    /// </summary>
    public async Task HandleAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        if (!TryGetPowerAction(req, out var action))
        {
            await Protocol.WriteErrorAsync(stream, "svc.badRequest", ct);
            return;
        }

        var actionKey = $"remote-power:{action}";
        if (SafeFileOps.IsOnCooldown(actionKey, 30))
        {
            await Protocol.WriteLineAsync(stream,
                System.Text.Json.JsonSerializer.Serialize(new { status = "error", error = "svc.cooldown" }), ct);
            SafeFileOps.Audit("power", action, "blocked", "cooldown", "remote");
            return;
        }

        await Protocol.WriteOkAsync(stream, ct);
        SafeFileOps.Audit("power", action, "ok", "power.approved", "remote");

        _ = Task.Run(async () =>
        {
            await Task.Delay(1000);
            await _executePowerAction(action);
        });
    }

    private static Task ExecutePowerActionAsync(string action)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var cmd = action == "reboot" ? "/r /f /t 0" : "/s /f /t 0";
                Process.Start(new ProcessStartInfo("shutdown", cmd) { CreateNoWindow = true, UseShellExecute = false });
            }
            else if (OperatingSystem.IsLinux())
            {
                var cmd = action == "reboot" ? "systemctl reboot" : "systemctl poweroff";
                Process.Start(new ProcessStartInfo("sh", $"-c \"{cmd}\"") { CreateNoWindow = true, UseShellExecute = false });
            }
            else
            {
                var cmd = action == "reboot" ? "reboot" : "shutdown -h now";
                Process.Start(new ProcessStartInfo("sh", $"-c \"{cmd}\"") { CreateNoWindow = true, UseShellExecute = false });
            }
        }
        catch (Exception ex)
        {
            Log.Warn("server", "power-action-failed", new { action, error = ex.Message });
        }

        return Task.CompletedTask;
    }

    private async Task<bool> ApproveHighRiskCommandAsync(
        string cmd,
        string ip,
        string? action,
        Stream stream,
        CancellationToken ct)
    {
        if (_server.ApproveHighRisk is not { } approve)
            return true;

        bool ok;
        try
        {
            ok = await approve(new FileServer.HighRiskCommand(ip, cmd, null, action), ct);
        }
        catch (Exception ex)
        {
            Log.Warn("server", "power-approve-callback-failed", new { ip, error = ex.Message });
            ok = false;
        }

        if (ok)
        {
            SafeFileOps.Audit(cmd, ip, "ok", $"{cmd}.approved", "remote");
            return true;
        }

        SafeFileOps.Audit(cmd, ip, "blocked", $"{cmd}.rejected", "remote");
        await Protocol.WriteErrorAsync(stream, "svc.rejected", ct);
        return false;
    }

    private static bool TryGetStringProperty(JsonElement req, string name, out string value)
    {
        value = "";
        if (!req.TryGetProperty(name, out var el)) return false;
        value = el.GetString() ?? "";
        return true;
    }

    private static bool TryGetPowerAction(JsonElement req, out string action)
    {
        if (!TryGetStringProperty(req, "action", out action))
            return false;

        return string.Equals(action, "shutdown", StringComparison.Ordinal) ||
               string.Equals(action, "reboot", StringComparison.Ordinal);
    }
}
