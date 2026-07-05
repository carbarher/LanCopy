using System.Text.Json;

namespace LanCopy.Services;

public sealed class PeerPermissionStore
{
    public static PeerPermissionStore Shared { get; } = new();

    public sealed record Permissions(
        bool Browse = true,
        bool Download = true,
        bool Upload = false,
        bool Modify = false,
        bool Delete = false,
        bool Sync = false,
        bool Power = false,
        DateTimeOffset? UpdatedUtc = null);

    private sealed class Snapshot
    {
        public Dictionary<string, Permissions> Hosts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly object _lock = new();
    private readonly string _path;
    private Dictionary<string, Permissions>? _cache;

    public PeerPermissionStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LanCopy",
            "peer-permissions.json");
    }

    public Permissions Get(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return new Permissions();
        lock (_lock)
        {
            var map = LoadLocked();
            return map.TryGetValue(host.Trim(), out var p) ? p : new Permissions();
        }
    }

    public void Set(string host, Permissions permissions)
    {
        if (string.IsNullOrWhiteSpace(host))
            return;
        lock (_lock)
        {
            var map = LoadLocked();
            map[host.Trim()] = permissions with { UpdatedUtc = permissions.UpdatedUtc ?? DateTimeOffset.UtcNow };
            SaveLocked(map);
        }
    }

    public DateTimeOffset? GetLastUpdatedUtc(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return null;
        lock (_lock)
        {
            var map = LoadLocked();
            return map.TryGetValue(host.Trim(), out var p) ? p.UpdatedUtc : null;
        }
    }

    public bool Remove(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;
        lock (_lock)
        {
            var map = LoadLocked();
            var removed = map.Remove(host.Trim());
            if (removed) SaveLocked(map);
            return removed;
        }
    }

    private Dictionary<string, Permissions> LoadLocked()
    {
        if (_cache != null)
            return _cache;
        try
        {
            if (!File.Exists(_path))
                return _cache = new Dictionary<string, Permissions>(StringComparer.OrdinalIgnoreCase);
            using var fs = File.OpenRead(_path);
            using var doc = JsonDocument.Parse(fs);
            _cache = LoadFromJson(doc.RootElement);
            return _cache;
        }
        catch (Exception ex)
        {
            Log.Warn("peer-permissions", "load-failed", new { error = ex.Message });
            return _cache = new Dictionary<string, Permissions>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static Dictionary<string, Permissions> LoadFromJson(JsonElement root)
    {
        var map = new Dictionary<string, Permissions>(StringComparer.OrdinalIgnoreCase);
        if (root.ValueKind != JsonValueKind.Object)
            return map;

        if (root.TryGetProperty("hosts", out var hosts) && hosts.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in hosts.EnumerateObject())
            {
                if (TryReadPermissions(prop.Value, out var permissions))
                    map[prop.Name] = permissions;
            }
            return map;
        }

        foreach (var prop in root.EnumerateObject())
        {
            if (TryReadPermissions(prop.Value, out var permissions))
                map[prop.Name] = permissions;
        }
        return map;
    }

    private static bool TryReadPermissions(JsonElement el, out Permissions permissions)
    {
        try
        {
            permissions = JsonSerializer.Deserialize<Permissions>(
                el.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Permissions();
            return true;
        }
        catch
        {
            permissions = new Permissions();
            return false;
        }
    }

    private void SaveLocked(Dictionary<string, Permissions> map)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var tmp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var snap = new Snapshot { Hosts = map };
            File.WriteAllText(tmp, JsonSerializer.Serialize(snap, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, _path, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Warn("peer-permissions", "save-failed", new { error = ex.Message });
        }
    }
}
