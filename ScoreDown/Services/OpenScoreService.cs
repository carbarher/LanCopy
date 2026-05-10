using ScoreDown.Infrastructure;
using ScoreDown.Models;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ScoreDown.Services;

/// <summary>
/// Searches OpenScore on GitHub (CC0-licensed MusicXML scores).
/// Uses the Git tree API with in-process caching to avoid repeated API calls.
/// </summary>
public class OpenScoreService
{
    private readonly HttpClient _http;

    private const string LiederTreeUrl =
        "https://api.github.com/repos/OpenScore/Lieder/git/trees/main?recursive=1";
    private const string LiederRawBase =
        "https://raw.githubusercontent.com/OpenScore/Lieder/main/";

    private List<TreeEntry>? _cachedPaths;
    private List<PartituraItem>? _cachedItems;   // built from _cachedPaths; invalidated together
    private DateTimeOffset _cacheExpiry = DateTimeOffset.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);
    private readonly SemaphoreSlim _fetchLock = new(1, 1);  // one in-flight fetch at a time

    public OpenScoreService()
    {
        _http = HttpClientProvider.GetDefault();
    }

    /// <summary>
    /// Fetches all MXL entries from OpenScore Lieder and calls <paramref name="onItem"/> for each.
    /// Used by the catalog download flow (equivalent to SearchAsync with no filter).
    /// </summary>
    public async Task FetchAllAsync(
        Action<PartituraItem> onItem,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report("🔍 Cargando índice OpenScore Lieder...");
        var items = await GetCachedItemsAsync(progress, ct).ConfigureAwait(false);
        if (items is null)
        {
            progress?.Report("⚠️ No se pudo obtener índice de OpenScore");
            return;
        }

        progress?.Report($"📥 Enviando {items.Count} obras de OpenScore al catálogo...");
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            onItem(item);
        }
        progress?.Report($"✅ {items.Count} obras de OpenScore catalogadas");
    }

    public async Task<List<PartituraItem>> SearchAsync(
        string query,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report("🔍 Cargando índice OpenScore Lieder...");
        var items = await GetCachedItemsAsync(progress, ct).ConfigureAwait(false);
        if (items is null)
        {
            progress?.Report("⚠️ No se pudo obtener índice de OpenScore");
            return [];
        }

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var results = items
            .Where(item => terms.All(t =>
                item.Title.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                item.Composer.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        progress?.Report(results.Count == 0
            ? "⚠️ Sin resultados en OpenScore"
            : $"✅ {results.Count} obras encontradas en OpenScore");
        return results;
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    /// <summary>Returns the cached item list, fetching and building once if stale.</summary>
    private async Task<List<PartituraItem>?> GetCachedItemsAsync(IProgress<string>? progress, CancellationToken ct)
    {
        // Fast path — no lock needed when both are non-null and fresh.
        if (_cachedItems is not null && DateTimeOffset.UtcNow < _cacheExpiry)
            return _cachedItems;

        await _fetchLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Re-check after acquiring lock in case another thread just populated.
            if (_cachedItems is not null && DateTimeOffset.UtcNow < _cacheExpiry)
                return _cachedItems;

            var json = await _http.GetStringAsync(LiederTreeUrl, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("tree", out var treeEl))
            {
                progress?.Report("⚠️ OpenScore: respuesta de GitHub no contiene 'tree' (¿rate limit 403/429?)");
                return null;
            }

            // Warn if GitHub truncated the tree (>100 k blobs).
            if (doc.RootElement.TryGetProperty("truncated", out var trunc) && trunc.GetBoolean())
                progress?.Report("⚠️ OpenScore: árbol truncado por GitHub; algunas obras pueden faltar");

            var entries = new List<TreeEntry>(4096);
            foreach (var node in treeEl.EnumerateArray())
            {
                if (!node.TryGetProperty("type", out var t) || t.GetString() != "blob") continue;
                if (!node.TryGetProperty("path", out var p)) continue;

                long size = 0;
                if (node.TryGetProperty("size", out var s)) size = s.GetInt64();

                var path = p.GetString() ?? "";
                if (path.EndsWith(".mxl", StringComparison.OrdinalIgnoreCase))
                    entries.Add(new TreeEntry(path, size));
            }

            _cachedPaths = entries;
            _cachedItems = BuildItems(entries);
            _cacheExpiry = DateTimeOffset.UtcNow.Add(CacheTtl);
            return _cachedItems;
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            progress?.Report($"⚠️ OpenScore: error HTTP al obtener índice — {ex.Message}");
            return null;
        }
        catch
        {
            return null;
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    private static List<PartituraItem> BuildItems(List<TreeEntry> mxlPaths)
    {
        // Path format: scores/{Composer}/{Work}/{Song}/filename.mxl (5 parts)
        var grouped = mxlPaths
            .Select(e => new { Entry = e, Parts = e.Path.Split('/') })
            .Where(x => x.Parts.Length >= 5 && x.Parts[0] == "scores")
            .GroupBy(x => $"{x.Parts[1]}/{x.Parts[2]}/{x.Parts[3]}")
            .ToList();

        var items = new List<PartituraItem>(grouped.Count);

        foreach (var group in grouped)
        {
            var first = group.First();
            var parts = first.Parts;

            var composerRaw = parts[1];
            var workRaw = parts[2];
            var songRaw = parts[3];

            var composer = FormatComposer(composerRaw);
            var work = FormatTitle(workRaw);
            var song = FormatSongTitle(songRaw);
            var title = string.IsNullOrEmpty(song) ? work : $"{work}: {song}";

            var pageUrl = "https://github.com/OpenScore/Lieder/tree/main/scores/" +
                          Uri.EscapeDataString(composerRaw) + "/" +
                          Uri.EscapeDataString(workRaw) + "/" +
                          Uri.EscapeDataString(songRaw);

            var files = group.Select(x =>
            {
                // Commas are safe in URI path segments (RFC 3986 sub-delims) and are
                // used verbatim in GitHub raw URLs; EscapeDataString would percent-encode
                // them unnecessarily. All other unsafe chars are still encoded correctly.
                var rawUrl = LiederRawBase + string.Join("/",
                    x.Parts.Select(EscapePathSegment));
                return new PartituraFile
                {
                    Format = "MXL",
                    DownloadUrl = rawUrl,
                    FileName = x.Parts[^1],
                    SizeBytes = x.Entry.Size,
                    SourcePageUrl = pageUrl
                };
            }).ToList();

            items.Add(new PartituraItem
            {
                Title = title,
                Composer = composer,
                PageUrl = pageUrl,
                Source = "OpenScore",
                Files = files
            });
        }

        return items;
    }

    private static string EscapePathSegment(string s) =>
        // Commas appear in composer/work directory names (e.g. "Schubert,_Franz").
        // They are valid path segment characters in RFC 3986 and GitHub raw serves them fine unencoded.
        Uri.EscapeDataString(s).Replace("%2C", ",", StringComparison.Ordinal);

    private static string FormatComposer(string raw)
    {
        // "Schubert,_Franz" → "Franz Schubert"
        // "Bach,_Johann_Christian" → "Johann Christian Bach"
        var normalized = raw.Replace('_', ' ');
        var comma = normalized.IndexOf(',');
        if (comma > 0)
        {
            var surname = normalized[..comma].Trim();
            var given = normalized[(comma + 1)..].Trim();
            return string.IsNullOrEmpty(given) ? surname : $"{given} {surname}";
        }
        return normalized;
    }

    private static string FormatTitle(string raw)
        // "Winterreise,_D.911" → "Winterreise, D.911"
        => raw.Replace('_', ' ');

    private static readonly Regex s_leadingNum = new(@"^\d+\s+", RegexOptions.Compiled);

    private static string FormatSongTitle(string raw)
    {
        // "1_Gute_Nacht" → "Gute Nacht";  "_" or empty → ""
        if (raw is "_" or "") return "";
        var s = raw.Replace('_', ' ').Trim();
        return s_leadingNum.Replace(s, "");
    }

    private record TreeEntry(string Path, long Size);
}
