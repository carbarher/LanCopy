using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using LanCopy.Models;
using System.Text;

namespace LanCopy.Services;

public sealed partial class FileServer
{
    // Feature 9: capabilities — anuncia soporte de compresión y TLS al cliente
    private async Task HandleCapsAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new
            {
                status = "ok",
                compress = true,
                tls = TlsEnabled,
                version = Protocol.Version,
                minVersion = Protocol.MinSupportedVersion,
                readOnly = ReadOnly,
                safeModeNoRemoteDelete = SafeModeNoRemoteDelete,
                allowRemoteHardDelete = AllowRemoteHardDelete,
                text = true
            }), ct);
    }

    private async Task HandleHealthAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        // M8: usar _activeIpCount O(1) en lugar de _perIp.Count(predicate) LINQ O(n)
        var activeIps = Interlocked.CompareExchange(ref _activeIpCount, 0, 0);
        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new
        {
            status = "ok",
            connCurrent = MaxConnections - _connLimit.CurrentCount,
            connLimit = MaxConnections,
            perIpLimit = MaxPerIp,
            activeIps,
            pinFailsTracked = _pinFails.Count,
            hashCacheEntries = _sha256Cache.Count,
            commandRateTracked = _cmdRate.Count,
            commandRateLimit = CommandRateLimit,
            commandRateWindowSeconds = CommandRateWindowSeconds
        }), ct);
    }

    // Chat: recibe un texto corto (<=256 KB) y lo notifica a la UI.
    private const int MaxTextBytes = 256 * 1024;

    private async Task HandleTextAsync(JsonElement req, Stream stream, string ip, CancellationToken ct)
    {
        var text = req.TryGetProperty("text", out var tEl) ? tEl.GetString() ?? "" : "";
        if (Encoding.UTF8.GetByteCount(text) > MaxTextBytes)
        {
            await Protocol.WriteErrorAsync(stream, "svc.textTooLong", ct);
            return;
        }

        var safeBuilder = new System.Text.StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            var v = rune.Value;
            if (v < 0x20 && v != 0x0A && v != 0x0D && v != 0x09) continue;
            if (v >= 0xE000 && v <= 0xF8FF) continue;
            if (v == 0x202E || v == 0x202D) continue;
            if (v == 0x200F || v == 0x200E) continue;
            if (v == 0x2066 || v == 0x2067 || v == 0x2068) continue;
            if (v == 0x2028 || v == 0x2029) continue;
            if (v == 0xFEFF) continue;
            if (v == 0x200B || v == 0x200C || v == 0x200D) continue;
            if (v == 0x202A || v == 0x202B || v == 0x202C) continue;
            if (v == 0x2069) continue;
            safeBuilder.Append(rune);
        }

        var safeText = safeBuilder.ToString();
        try { TextReceived?.Invoke(ip, safeText); }
        catch (Exception ex) { Log.Warn("server", "text-received-handler-error", new { ip, error = ex.Message }); }
        await Protocol.WriteOkAsync(stream, ct);
    }

    private async Task HandleDisconnectNoticeAsync(Stream stream, string ip, CancellationToken ct)
    {
        try { DisconnectNoticeReceived?.Invoke(ip); }
        catch (Exception ex) { Log.Warn("server", "disconnect-notice-handler-error", new { ip, error = ex.Message }); }
        await Protocol.WriteOkAsync(stream, ct);
    }

    private async Task HandleSearchAsync(JsonElement req, Stream stream, CancellationToken ct)
    {
        if (!TryGetStringProperty(req, "path", out var reqPath)) { await WriteBadRequestAsync(stream, ct); return; }
        if (!TryGuardRead(reqPath, out var basePath, out var gReason))
        {
            await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "error", error = gReason }), ct);
            return;
        }
        if (!TryGetStringProperty(req, "query", out var query) || string.IsNullOrWhiteSpace(query))
        {
            await WriteBadRequestAsync(stream, ct);
            return;
        }

        if (!Directory.Exists(basePath))
        {
            await Protocol.WriteErrorAsync(stream, "svc.dirNotFound", ct);
            return;
        }

        var results = new List<FileEntry>();
        try
        {
            var options = new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint, RecurseSubdirectories = true };
            var dirInfo = new DirectoryInfo(basePath);

            foreach (var f in dirInfo.EnumerateFileSystemInfos("*", options))
            {
                if (f.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    var isDir = f is DirectoryInfo;
                    var size = isDir ? 0 : ((FileInfo)f).Length;
                    results.Add(new FileEntry
                    {
                        Name = Path.GetRelativePath(basePath, f.FullName).Replace('\\', '/'),
                        FullPath = f.FullName,
                        IsDirectory = isDir,
                        Size = size,
                        LastWriteUtcTicks = f.LastWriteTimeUtc.Ticks
                    });

                    if (results.Count >= 250) break;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn("server", "search-failed", new { path = basePath, query, error = ex.Message });
        }

        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { status = "ok", results }), ct);
    }
}

