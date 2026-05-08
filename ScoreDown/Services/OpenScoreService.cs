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
    private DateTimeOffset _cacheExpiry = DateTimeOffset.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

    public OpenScoreService()
    {
        _http = HttpClientProvider.GetDefault();
    }

    public async Task<List<PartituraItem>> SearchAsync(
        string query,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report("🔍 Cargando índice OpenScore Lieder...");

        var paths = await GetCachedPathsAsync(ct).ConfigureAwait(false);
        if (paths is null)
        {
            progress?.Report("⚠️ No se pudo obtener índice de OpenScore");
            return [];
        }

        progress?.Report($"🔍 Buscando '{query}' en OpenScore...");

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var matching = paths
            .Where(p => p.Path.EndsWith(".mxl", StringComparison.OrdinalIgnoreCase))
            .Where(p => terms.All(t =>
                p.SearchKey.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (matching.Count == 0)
        {
            progress?.Report("⚠️ Sin resultados en OpenScore");
            return [];
        }

        var items = BuildItems(matching);
        progress?.Report($"✅ {items.Count} obras encontradas en OpenScore");
        return items;
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    private async Task<List<TreeEntry>?> GetCachedPathsAsync(CancellationToken ct)
    {
        if (_cachedPaths is not null && DateTimeOffset.UtcNow < _cacheExpiry)
            return _cachedPaths;

        try
        {
            var json = await _http.GetStringAsync(LiederTreeUrl, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("tree", out var treeEl))
                return null;

            var entries = new List<TreeEntry>(4096);
            foreach (var item in treeEl.EnumerateArray())
            {
                if (!item.TryGetProperty("type", out var t) || t.GetString() != "blob") continue;
                if (!item.TryGetProperty("path", out var p)) continue;

                long size = 0;
                if (item.TryGetProperty("size", out var s)) size = s.GetInt64();

                var path = p.GetString() ?? "";
                entries.Add(new TreeEntry(path, size));
            }

            _cachedPaths = entries;
            _cacheExpiry = DateTimeOffset.UtcNow.Add(CacheTtl);
            return entries;
        }
        catch
        {
            return null;
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
                var rawUrl = LiederRawBase + string.Join("/",
                    x.Parts.Select(Uri.EscapeDataString));
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

    private record TreeEntry
    {
        public string Path { get; }
        public long Size { get; }
        // Pre-built search key (underscores replaced for query matching)
        public string SearchKey { get; }

        public TreeEntry(string path, long size)
        {
            Path = path;
            Size = size;
            SearchKey = path.Replace('_', ' ').Replace(',', ' ');
        }
    }
}
