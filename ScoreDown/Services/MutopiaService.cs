using HtmlAgilityPack;
using ScoreDown.Infrastructure;
using ScoreDown.Models;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using HtmlDoc = HtmlAgilityPack.HtmlDocument;

namespace ScoreDown.Services;

public class MutopiaService
{
    private readonly HttpClient _http;
    private const string SearchUrl = "https://www.mutopiaproject.org/cgibin/make-table.cgi";
    private static readonly Regex s_nextStartAtRegex = new(@"[?&]startat=(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public MutopiaService()
    {
        _http = HttpClientProvider.GetDefault();
    }

    public async Task<List<PartituraItem>> SearchAsync(
        string query,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report($"🔍 Buscando '{query}' en Mutopia Project...");

        var items = await FetchPagedItemsAsync(
            BuildSearchUrl(query, startAt: 0),
            ct,
            progress,
            modeLabel: "Mutopia búsqueda").ConfigureAwait(false);

        if (items.Count == 0)
        {
            var fallback = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(query.ToLowerInvariant());
            if (!string.Equals(fallback, query, StringComparison.Ordinal))
            {
                items = await FetchPagedItemsAsync(
                    BuildSearchUrl(fallback, startAt: 0),
                    ct,
                    progress,
                    modeLabel: "Mutopia búsqueda").ConfigureAwait(false);
            }
        }

        if (items.Count == 0)
        {
            progress?.Report("⚠️ Sin resultados en Mutopia Project");
            return [];
        }

        progress?.Report($"✅ {items.Count} obras encontradas en Mutopia");
        return items;
    }

    /// <summary>
    /// Streams the full Mutopia catalog (~2400 obras) in a single HTTP request.
    /// Files (PDF/MIDI) are included. Calls <paramref name="onItem"/> for each work found.
    /// </summary>
    public async Task FetchAllAsync(
        Action<PartituraItem> onItem,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report("📚 Descargando catálogo completo de Mutopia Project...");
        var items = await FetchPagedItemsAsync(
            $"{SearchUrl}?output=html&startat=0",
            ct,
            progress,
            modeLabel: "Mutopia catálogo").ConfigureAwait(false);

        if (items.Count == 0)
        {
            progress?.Report("⚠️ Sin resultados en Mutopia");
            return;
        }

        int count = 0;
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            onItem(item);
            count++;
            if (count % 200 == 0)
                progress?.Report($"📚 Mutopia: {count} obras encontradas...");
        }

        progress?.Report($"✅ Mutopia: {count} obras en catálogo");
    }

    private async Task<List<PartituraItem>> FetchPagedItemsAsync(
        string firstUrl,
        CancellationToken ct,
        IProgress<string>? progress,
        string modeLabel)
    {
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenWorks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<PartituraItem>();
        var nextUrl = firstUrl;
        int page = 0;

        while (!string.IsNullOrWhiteSpace(nextUrl))
        {
            ct.ThrowIfCancellationRequested();
            page++;

            var html = await FetchHtmlAsync(nextUrl, ct).ConfigureAwait(false);
            if (html is null)
                return [];

            if (html.Contains("no matches were found", StringComparison.OrdinalIgnoreCase))
                return [];

            var doc = new HtmlDoc();
            doc.LoadHtml(html);
            var pageItems = ParseItems(doc, seenWorks);

            if (pageItems.Count == 0)
                break;

            items.AddRange(pageItems);

            var discoveredNextUrl = ExtractNextPageUrl(doc);
            if (string.IsNullOrWhiteSpace(discoveredNextUrl) || !seenUrls.Add(discoveredNextUrl))
                break;

            nextUrl = discoveredNextUrl;
            if (page % 10 == 0)
                progress?.Report($"📚 {modeLabel}: {items.Count} obras acumuladas...");
        }

        return items;
    }

    private List<PartituraItem> ParseItems(HtmlDoc doc, HashSet<string> seenWorks)
    {
        var tables = doc.DocumentNode.SelectNodes("//table[contains(@class,'result-table')]");
        if (tables is null || tables.Count == 0)
            return [];

        var items = new List<PartituraItem>();
        foreach (var table in tables)
        {
            var headerTds = table.SelectNodes(".//tr[1]/td");
            if (headerTds is null || headerTds.Count < 2) continue;

            var title = HtmlEntity.DeEntitize(headerTds[0].InnerText.Trim());
            if (string.IsNullOrWhiteSpace(title) || title == "\u00a0") continue;

            var composerRaw = HtmlEntity.DeEntitize(headerTds[1].InnerText.Trim());
            var composer = Regex.Replace(composerRaw, @"^\s*by\s+", "", RegexOptions.IgnoreCase);
            composer = Regex.Replace(composer, @"\s*\(\d{4}[^)]*\)\s*$", "").Trim();

            var files = new List<PartituraFile>();
            var links = table.SelectNodes(".//a[@href]");
            if (links != null)
            {
                foreach (var link in links)
                {
                    var href = link.GetAttributeValue("href", "");
                    if (string.IsNullOrWhiteSpace(href)) continue;
                    if (href.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                        files.Add(new PartituraFile { Format = "PDF", DownloadUrl = href, FileName = SanitizeFileName(Path.GetFileName(href)) });
                    else if (href.EndsWith(".mid", StringComparison.OrdinalIgnoreCase))
                        files.Add(new PartituraFile { Format = "MIDI", DownloadUrl = href, FileName = SanitizeFileName(Path.GetFileName(href)) });
                }
            }

            files = files
                .GroupBy(f => f.DownloadUrl, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            if (files.Count == 0) continue;

            var key = $"{composer}\n{title}";
            if (!seenWorks.Add(key))
                continue;

            items.Add(new PartituraItem
            {
                Title = title,
                Composer = composer,
                PageUrl = string.Empty,
                Files = files,
                Source = "Mutopia"
            });
        }

        return items;
    }

    private static string BuildSearchUrl(string query, int startAt)
    {
        var encoded = HttpUtility.UrlEncode(query);
        return $"{SearchUrl}?searchingfor={encoded}&output=html&startat={startAt}";
    }

    private static string? ExtractNextPageUrl(HtmlDoc doc)
    {
        var links = doc.DocumentNode.SelectNodes("//a[@href]");
        if (links is null) return null;

        foreach (var link in links)
        {
            var href = link.GetAttributeValue("href", "");
            var text = HtmlEntity.DeEntitize(link.InnerText.Trim());
            if (!text.Contains("Next", StringComparison.OrdinalIgnoreCase))
                continue;

            var match = s_nextStartAtRegex.Match(href);
            if (!match.Success)
                continue;

            return href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? href
                : $"{SearchUrl}?{href.Split('?', 2).ElementAtOrDefault(1)}";
        }

        return null;
    }

    private static readonly HashSet<char> s_invalidFileNameChars =
        new(Path.GetInvalidFileNameChars());

    private static string SanitizeFileName(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(s_invalidFileNameChars.Contains(c) ? '_' : c);
        return sb.ToString();
    }

    private Task<string?> FetchHtmlAsync(string url, CancellationToken ct)
        => HttpHelper.FetchHtmlAsync(_http, url, maxRetries: 3, ct);
}
