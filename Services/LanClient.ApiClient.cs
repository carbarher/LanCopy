using System.Text.Json;
using LanCopy.Models;

namespace LanCopy.Services;

public sealed partial class LanClient
{
    // -- LIST --

    public async Task<List<FileEntry>> ListAsync(string path, CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { cmd = "list", path }), ct);
        var line = await Protocol.ReadLineAsync(stream, ct);
        var resp = JsonSerializer.Deserialize<JsonElement>(line);
        EnsureOk(resp);
        if (!resp.TryGetProperty("entries", out var entriesEl))
            throw new InvalidDataException("svc.missingEntries"); // respuesta del servidor no incluye 'entries'
        // O2: entriesEl.Deserialize<T>() opera sobre el UTF-8 original directamente.
        // GetRawText() materializaba el JSON a string UTF-16 para re-parsearlo — doble decodificación.
        return entriesEl.Deserialize<List<FileEntry>>()!;
    }

    // -- LIST RECURSIVE (para transferir carpetas) --

    public async Task<List<FileEntry>> ListRecursiveAsync(string path, CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { cmd = "list", path, recursive = true }), ct);
        var line = await Protocol.ReadLineAsync(stream, ct);
        var resp = JsonSerializer.Deserialize<JsonElement>(line);
        EnsureOk(resp);
        if (!resp.TryGetProperty("entries", out var entriesEl))
            throw new InvalidDataException("svc.missingEntries"); // respuesta del servidor no incluye 'entries'
        // O2: misma mejora que ListAsync — sin GetRawText()
        return entriesEl.Deserialize<List<FileEntry>>()!;
    }

    public sealed record RecursiveListResult(List<FileEntry> Entries, bool Truncated);

    /// <summary>Reads a recursive directory listing in bounded JSON chunks.</summary>
    public async Task<RecursiveListResult> ListRecursiveStreamAsync(string path, CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { cmd = "list_recursive_stream", path }), ct);

        var header = JsonSerializer.Deserialize<JsonElement>(await Protocol.ReadLineAsync(stream, ct));
        EnsureOk(header);
        var entries = new List<FileEntry>();
        while (true)
        {
            var response = JsonSerializer.Deserialize<JsonElement>(await Protocol.ReadLineAsync(stream, ct));
            var status = response.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;
            if (string.Equals(status, "chunk", StringComparison.Ordinal))
            {
                if (response.TryGetProperty("entries", out var chunkEl))
                {
                    var chunk = chunkEl.Deserialize<List<FileEntry>>();
                    if (chunk != null) entries.AddRange(chunk);
                }
                continue;
            }
            if (string.Equals(status, "done", StringComparison.Ordinal))
            {
                var truncated = response.TryGetProperty("truncated", out var truncatedEl)
                    && truncatedEl.ValueKind == JsonValueKind.True;
                return new RecursiveListResult(entries, truncated);
            }
            EnsureOk(response);
            throw new InvalidDataException("svc.invalidListStream");
        }
    }
    // -- CHAT TEXT --

    public async Task SendTextAsync(string text, CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "text", text }), ct);
        var line = await Protocol.ReadLineAsync(stream, ct);
        EnsureOk(JsonSerializer.Deserialize<JsonElement>(line));
    }

    public async Task SendDisconnectNoticeAsync(CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "disconnect_notice" }), ct);
        var line = await Protocol.ReadLineAsync(stream, ct);
        EnsureOk(JsonSerializer.Deserialize<JsonElement>(line));
    }

    // ── DELETE ────────────────────────────────────────────────────────────────────

    public async Task DeleteAsync(string remotePath, CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "delete", path = remotePath }), ct);
        var line = await Protocol.ReadLineAsync(stream, ct);
        EnsureOk(JsonSerializer.Deserialize<JsonElement>(line));
    }

    // ── RENAME ────────────────────────────────────────────────────────────────────

    public async Task RenameAsync(string remotePath, string newName, CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "rename", path = remotePath, newname = newName }), ct);
        var line = await Protocol.ReadLineAsync(stream, ct);
        EnsureOk(JsonSerializer.Deserialize<JsonElement>(line));
    }

    // ── MKDIR ─────────────────────────────────────────────────────────────────────

    public async Task CreateDirectoryAsync(string remotePath, CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "mkdir", path = remotePath }), ct);
        var line = await Protocol.ReadLineAsync(stream, ct);
        EnsureOk(JsonSerializer.Deserialize<JsonElement>(line));
    }

    // ── SHA1/SHA256 ───────────────────────────────────────────────────────────────

    /// <summary>
    /// SHA1 hex lowercase remoto. Devuelve null si no existe o error.
    /// </summary>
    public async Task<string?> GetSha1Async(string remotePath, CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "sha1", path = remotePath }), ct);
        var line = await Protocol.ReadLineAsync(stream, ct);
        var resp = JsonSerializer.Deserialize<JsonElement>(line);
        var status = resp.TryGetProperty("status", out var s) ? s.GetString() : null;
        if (status != "ok") return null;
        return resp.TryGetProperty("sha1", out var sha1El) ? sha1El.GetString() : null;
    }

    /// <summary>
    /// SHA256 hex lowercase remoto. Devuelve null si no existe o peer no soporta comando.
    /// </summary>
    public async Task<string?> GetSha256Async(string remotePath, CancellationToken ct = default)
    {
        try
        {
            var (tcp, stream) = await OpenAsync(ct);
            using var _ = tcp;
            await Protocol.WriteLineAsync(stream,
                JsonSerializer.Serialize(new { cmd = "sha256", path = remotePath }), ct);
            var line = await Protocol.ReadLineAsync(stream, ct);
            var resp = JsonSerializer.Deserialize<JsonElement>(line);
            var status = resp.TryGetProperty("status", out var s) ? s.GetString() : null;
            if (status != "ok") return null;
            return resp.TryGetProperty("sha256", out var sha256El2) ? sha256El2.GetString() : null;
        }
        catch (Exception ex)
        {
            Log.Debug("client", "sha256-query-failed", new { path = remotePath, error = ex.Message });
            return null;
        }
    }

    // ── STAT ──────────────────────────────────────────────────────────────────────

    public async Task<RemoteStat?> GetStatAsync(string remotePath, CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;

        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "stat", path = remotePath }), ct);

        var line = await Protocol.ReadLineAsync(stream, ct);
        var resp = JsonSerializer.Deserialize<JsonElement>(line);
        EnsureOk(resp);

        var exists = resp.TryGetProperty("exists", out var existsEl) && existsEl.GetBoolean();
        if (!exists) return new RemoteStat(false, false, 0, 0);

        var isDirectory = resp.TryGetProperty("isDirectory", out var dirEl) && dirEl.GetBoolean();
        var size = resp.TryGetProperty("size", out var sizeEl) ? sizeEl.GetInt64() : 0L;
        var ticks = resp.TryGetProperty("lastWriteUtcTicks", out var ticksEl) ? ticksEl.GetInt64() : 0L;
        return new RemoteStat(true, isDirectory, size, ticks);
    }

    public async Task<IReadOnlyDictionary<string, RemoteStat>> GetStatsAsync(IEnumerable<string> remotePaths, CancellationToken ct = default)
    {
        var paths = remotePaths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (paths.Length > 256) throw new ArgumentOutOfRangeException(nameof(remotePaths), "A maximum of 256 paths can be checked at once.");
        if (paths.Length == 0) return new Dictionary<string, RemoteStat>(StringComparer.OrdinalIgnoreCase);

        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { cmd = "stat_many", paths }), ct);
        var line = await Protocol.ReadLineAsync(stream, ct);
        var response = JsonSerializer.Deserialize<JsonElement>(line);
        EnsureOk(response);

        var results = new Dictionary<string, RemoteStat>(StringComparer.OrdinalIgnoreCase);
        if (!response.TryGetProperty("results", out var items) || items.ValueKind != JsonValueKind.Array) return results;
        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("path", out var pathEl)) continue;
            var path = pathEl.GetString();
            if (string.IsNullOrWhiteSpace(path)) continue;
            var exists = item.TryGetProperty("exists", out var existsEl) && existsEl.ValueKind == JsonValueKind.True;
            var directory = item.TryGetProperty("isDirectory", out var directoryEl) && directoryEl.ValueKind == JsonValueKind.True;
            var size = item.TryGetProperty("size", out var sizeEl) && sizeEl.TryGetInt64(out var itemSize) ? itemSize : 0L;
            var ticks = item.TryGetProperty("lastWriteUtcTicks", out var ticksEl) && ticksEl.TryGetInt64(out var itemTicks) ? itemTicks : 0L;
            results[path] = new RemoteStat(exists, directory, size, ticks);
        }
        return results;
    }
    public async Task<RemoteCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { cmd = "caps" }), ct);
        var line = await Protocol.ReadLineAsync(stream, ct);
        var resp = JsonSerializer.Deserialize<JsonElement>(line);
        EnsureOk(resp);
        bool? ReadBool(string name) => resp.TryGetProperty(name, out var el) && el.ValueKind is JsonValueKind.True or JsonValueKind.False ? el.GetBoolean() : null;
        var version = resp.TryGetProperty("version", out var versionEl) && versionEl.TryGetInt32(out var advertisedVersion)
            ? advertisedVersion : Protocol.MinSupportedVersion;
        return new RemoteCapabilities(
            version,
            ReadBool("downloadAllowed"),
            ReadBool("uploadAllowed"),
            ReadBool("modifyAllowed"),
            ReadBool("deleteAllowed"));
    }
    // ── HEALTH ───────────────────────────────────────────────────────────────────
    public async Task<RemoteHealth?> GetHealthAsync(CancellationToken ct = default)
    {
        var (tcp, stream) = await OpenAsync(ct);
        using var _ = tcp;
        await Protocol.WriteLineAsync(stream, JsonSerializer.Serialize(new { cmd = "health" }), ct);
        var line = await Protocol.ReadLineAsync(stream, ct);
        var resp = JsonSerializer.Deserialize<JsonElement>(line);
        EnsureOk(resp);

        int GetInt(string name, int def = 0) => resp.TryGetProperty(name, out var el) && el.TryGetInt32(out var n) ? n : def;
        return new RemoteHealth(
            GetInt("connCurrent"),
            GetInt("connLimit"),
            GetInt("perIpLimit"),
            GetInt("activeIps"),
            GetInt("pinFailsTracked"),
            GetInt("hashCacheEntries"),
            GetInt("commandRateTracked"),
            GetInt("commandRateLimit"),
            GetInt("commandRateWindowSeconds"));
    }

    public async Task SendPowerAsync(string action, CancellationToken ct = default)
    {
        using var idleCts = StartIdleTimeout(ct);
        var ioCt = idleCts.Token;

        var (tcp, stream) = await OpenAsync(ioCt);
        using var _ = tcp;

        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "power", action }), ioCt);

        var headerLine = await Protocol.ReadLineAsync(stream, ioCt);
        var header = JsonSerializer.Deserialize<JsonElement>(headerLine);
        EnsureOk(header);
    }

    public async Task<List<FileEntry>> SearchRemoteAsync(string remotePath, string query, CancellationToken ct = default)
    {
        using var idleCts = StartIdleTimeout(ct);
        var ioCt = idleCts.Token;

        var (tcp, stream) = await OpenAsync(ioCt);
        using var _ = tcp;

        await Protocol.WriteLineAsync(stream,
            JsonSerializer.Serialize(new { cmd = "search", path = remotePath, query }), ioCt);

        var headerLine = await Protocol.ReadLineAsync(stream, ioCt);
        var header = JsonSerializer.Deserialize<JsonElement>(headerLine);
        EnsureOk(header);

        // P3/N9: pre-asignar capacidad — servidor limita resultados a 250, GetArrayLength() da el exacto
        var results = new List<FileEntry>(
            header.TryGetProperty("results", out var resEl) && resEl.ValueKind == JsonValueKind.Array
                ? Math.Min(resEl.GetArrayLength(), 250)
                : 0);
        if (resEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in resEl.EnumerateArray())
            {
                // O2: el.Deserialize<FileEntry>() en lugar de el.GetRawText() — sin string intermedia
                var entry = el.Deserialize<FileEntry>();
                if (entry != null) results.Add(entry);
            }
        }
        return results;
    }
}
