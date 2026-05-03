using HtmlAgilityPack;
using ScoreDown.Infrastructure;
using ScoreDown.Models;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using HtmlDoc = HtmlAgilityPack.HtmlDocument;

namespace ScoreDown.Services;

public class CpdlService
{
    private readonly HttpClient _http;
    private string? _manualCookieHeader;
    private string? _manualUserAgent;
    private const string BaseUrl = "https://www.cpdl.org";
    private static readonly string[] AllowedFormats = ["PDF", "MIDI", "XML", "MXL"];
    private static readonly string[] LikelyWorkCategoryPrefixes =
    [
        "Category:Music by ",
        "Category:Compositions by ",
        "Category:Works by ",
        "Category:Masses by ",
        "Category:Motets by ",
        "Category:Anthems by ",
        "Category:Hymns by ",
        "Category:Sacred songs by ",
        "Category:Psalm settings by ",
        "Category:Magnificats by ",
        "Category:Nunc dimittis settings by ",
    ];

    public CpdlService()
    {
        _http = HttpClientProvider.GetDefault();
    }

    public bool HasManualSession => !string.IsNullOrWhiteSpace(_manualCookieHeader);

    public (string? Cookie, string? UserAgent) GetSessionHeaders() =>
        (_manualCookieHeader, _manualUserAgent);

    public void SetManualSession(string? cookieHeader, string? userAgent = null)
    {
        _manualCookieHeader = string.IsNullOrWhiteSpace(cookieHeader) ? null : cookieHeader.Trim();
        _manualUserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim();
    }

    public async Task<List<PartituraItem>> SearchAsync(
        string query,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report($"🔍 Buscando '{query}' en CPDL...");

        var encoded = HttpUtility.UrlEncode(query);
        var searchUrl = $"{BaseUrl}/wiki/index.php/Special:Search?search={encoded}";
        var html = await FetchHtmlAsync(searchUrl, ct).ConfigureAwait(false);
        if (html is null)
        {
            progress?.Report("❌ No se pudo conectar con CPDL");
            return [];
        }

        var doc = new HtmlDoc();
        doc.LoadHtml(html);

        var resultNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'mw-search-result-heading')]//a[@href]")
            ?? doc.DocumentNode.SelectNodes("//ul[contains(@class,'mw-search-results')]//a[@href]");

        if (resultNodes is null || resultNodes.Count == 0)
        {
            progress?.Report("⚠️ Sin resultados en CPDL");
            return [];
        }

        var items = new List<PartituraItem>();
        int index = 0;
        foreach (var node in resultNodes.Take(12))
        {
            ct.ThrowIfCancellationRequested();
            index++;

            var href = node.GetAttributeValue("href", "");
            var title = HtmlEntity.DeEntitize(node.InnerText.Trim());
            if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(title)) continue;
            if (!href.StartsWith("/wiki/", StringComparison.OrdinalIgnoreCase)) continue;
            if (href.Contains("Special:", StringComparison.OrdinalIgnoreCase)) continue;

            var pageUrl = href.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? href : BaseUrl + href;
            progress?.Report($"📋 CPDL [{index}/{Math.Min(resultNodes.Count, 12)}] {title}");

            var item = await LoadWorkPageAsync(title, pageUrl, ct).ConfigureAwait(false);
            if (item is not null && item.Files.Count > 0)
                items.Add(item);
        }

        progress?.Report($"✅ {items.Count} obras encontradas en CPDL");
        return items;
    }

    private async Task<PartituraItem?> LoadWorkPageAsync(string title, string pageUrl, CancellationToken ct)
    {
        try
        {
            var html = await FetchHtmlAsync(pageUrl, ct).ConfigureAwait(false);
            if (html is null) return null;

            var doc = new HtmlDoc();
            doc.LoadHtml(html);

            var files = ExtractFiles(doc, title);
            if (files.Count == 0) return null;

            return new PartituraItem
            {
                Title = CleanTitle(title),
                Composer = ExtractComposer(doc, title),
                PageUrl = pageUrl,
                Files = files,
                Source = "CPDL"
            };
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractComposer(HtmlDoc doc, string title)
    {
        var cats = doc.DocumentNode.SelectNodes("//div[@id='mw-normal-catlinks']//a");
        if (cats != null)
        {
            foreach (var cat in cats)
            {
                var text = HtmlEntity.DeEntitize(cat.InnerText.Trim());
                if (text.StartsWith("Music by ", StringComparison.OrdinalIgnoreCase))
                    return text[9..].Trim();
                if (text.StartsWith("Compositions by ", StringComparison.OrdinalIgnoreCase))
                    return text[16..].Trim();
            }
        }

        var comma = title.IndexOf(',');
        return comma > 0 ? title[..comma].Trim() : string.Empty;
    }

    private static List<PartituraFile> ExtractFiles(HtmlDoc doc, string baseTitle)
    {
        var files = new List<PartituraFile>();
        var links = doc.DocumentNode.SelectNodes("//a[@href]");
        if (links is null) return files;

        foreach (var link in links)
        {
            var href = link.GetAttributeValue("href", "");
            var ext = GetExtension(href)
                ?? GetExtension(link.InnerText)
                ?? GetExtension(link.GetAttributeValue("title", ""))
                ?? GetExtension(link.ParentNode?.InnerText ?? string.Empty);
            if (ext is null) continue;

            if (!LooksLikeDownloadLink(href, link)) continue;

            var absolute = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? href
                : href.StartsWith("/") ? BaseUrl + href : $"{BaseUrl}/{href.TrimStart('/')}";

            var fileName = Path.GetFileName(href.Split('?')[0]);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = $"{CleanTitle(baseTitle)}.{ext.ToLowerInvariant()}";

            if (!fileName.Contains('.', StringComparison.Ordinal))
                fileName = $"{fileName}.{ext.ToLowerInvariant()}";

            if (files.Any(f => string.Equals(f.DownloadUrl, absolute, StringComparison.OrdinalIgnoreCase)))
                continue;

            files.Add(new PartituraFile
            {
                Format = ext,
                DownloadUrl = absolute,
                FileName = SanitizeFileName(fileName)
            });
        }

        return files;
    }

    private static bool LooksLikeDownloadLink(string href, HtmlNode link)
    {
        if (string.IsNullOrWhiteSpace(href)) return false;

        if (href.Contains("/wiki/images/", StringComparison.OrdinalIgnoreCase) ||
            href.Contains("Special:Redirect/file", StringComparison.OrdinalIgnoreCase) ||
            href.Contains("title=File:", StringComparison.OrdinalIgnoreCase) ||
            href.Contains("/wiki/File:", StringComparison.OrdinalIgnoreCase) ||
            href.Contains("/images/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var title = link.GetAttributeValue("title", "");
        if (title.StartsWith("File:", StringComparison.OrdinalIgnoreCase))
            return true;

        var cssClass = link.GetAttributeValue("class", "");
        if (cssClass.Contains("internal", StringComparison.OrdinalIgnoreCase) ||
            cssClass.Contains("mediafile", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return GetExtension(href) is not null;
    }

    private static string? GetExtension(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var ext = Path.GetExtension(text.Split('?')[0]).TrimStart('.').ToUpperInvariant();
        if (Array.IndexOf(AllowedFormats, ext) >= 0) return ext;

        var upper = text.ToUpperInvariant();
        foreach (var format in AllowedFormats)
            if (upper == format || upper.Contains('.' + format))
                return format;

        return null;
    }

    private static readonly HashSet<char> s_invalidFileNameChars = new(Path.GetInvalidFileNameChars());

    private static string SanitizeFileName(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(s_invalidFileNameChars.Contains(c) ? '_' : c);
        return sb.ToString();
    }

    private static string CleanTitle(string title) => title.Replace("  ", " ").Trim();

    private const string ApiUrl = "https://www.cpdl.org/wiki/api.php";
    private static readonly Regex s_composerInTitle =
        new(@"\(([^,)]+),\s*([^)]+)\)\s*$", RegexOptions.Compiled);
    private static readonly Regex s_composerSuffix =
        new(@"\s*\([^,)]+,\s*[^)]+\)\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Streams the full CPDL catalog via MediaWiki allpages API.
    /// Items have empty Files — call <see cref="LoadFilesAsync"/> before downloading.
    /// </summary>
    public async Task FetchAllAsync(
        Action<PartituraItem> onItem,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report("📋 Listando páginas de CPDL via API...");
        string? continueToken = null;
        int total = 0;
        int filteredOut = 0;
        bool blockedOrUnavailable = false;

        do
        {
            ct.ThrowIfCancellationRequested();
            var url = $"{ApiUrl}?action=query&list=allpages&aplimit=500&apnamespace=0&apfilterredir=nonredirects&format=json";
            if (continueToken != null)
                url += $"&apcontinue={HttpUtility.UrlEncode(continueToken)}";

            var json = await FetchJsonAsync(url, ct).ConfigureAwait(false);
            if (json is null)
            {
                blockedOrUnavailable = true;
                break;
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch
            {
                blockedOrUnavailable = true;
                break;
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("query", out var query) &&
                    query.TryGetProperty("allpages", out var pages))
                {
                    var batch = new List<(int PageId, string Title)>();
                    foreach (var page in pages.EnumerateArray())
                    {
                        var rawTitle = page.GetProperty("title").GetString() ?? "";
                        if (string.IsNullOrEmpty(rawTitle) || rawTitle.Contains(':')) continue;

                        int pageId = 0;
                        if (page.TryGetProperty("pageid", out var pageIdEl) && pageIdEl.TryGetInt32(out var parsedPageId))
                            pageId = parsedPageId;

                        batch.Add((pageId, rawTitle));
                    }

                    var allowedPageIds = await FetchLikelyWorkPageIdsAsync(batch, ct).ConfigureAwait(false);

                    foreach (var entry in batch)
                    {
                        var rawTitle = entry.Title;

                        if (allowedPageIds is not null && entry.PageId > 0 && !allowedPageIds.Contains(entry.PageId))
                        {
                            filteredOut++;
                            continue;
                        }

                        var m = s_composerInTitle.Match(rawTitle);
                        var composer = m.Success
                            ? $"{m.Groups[2].Value.Trim()} {m.Groups[1].Value.Trim()}"
                            : string.Empty;
                        var cleanTitle = s_composerSuffix.Replace(rawTitle, "").Trim();
                        var pageUrl = $"{BaseUrl}/wiki/index.php/{rawTitle.Replace(' ', '_')}";  // solo espacios → _

                        onItem(new PartituraItem
                        {
                            Title = cleanTitle,
                            Composer = composer,
                            PageUrl = pageUrl,
                            Source = "CPDL"
                            // Files empty — lazy-loaded via LoadFilesAsync
                        });
                        total++;
                    }
                }

                continueToken = null;
                if (root.TryGetProperty("continue", out var cont) &&
                    cont.TryGetProperty("apcontinue", out var apc))
                    continueToken = apc.GetString();
            }

            if (total % 1000 == 0 && total > 0)
                progress?.Report($"📋 CPDL: {total} obras catalogadas ({filteredOut} páginas sin adjuntos probables filtradas)...");

            await Task.Delay(150, ct).ConfigureAwait(false);
        }
        while (continueToken != null);

        if (total == 0 && blockedOrUnavailable)
            progress?.Report("⚠️ CPDL no disponible: protección anti-bot del sitio (Cloudflare). Pulsa '🔐 CPDL sesión' para modo interactivo y reintenta.");
        else
            progress?.Report($"✅ CPDL: {total} obras en catálogo ({filteredOut} páginas sin adjuntos probables omitidas)");
    }

    /// <summary>
    /// Lazy-loads file download links for a catalog item (fetches its detail page).
    /// </summary>
    public async Task<bool> LoadFilesAsync(PartituraItem item, CancellationToken ct = default)
    {
        if (item.Files.Count > 0) return true;
        if (string.IsNullOrEmpty(item.PageUrl)) return false;
        var loaded = await LoadWorkPageAsync(item.Title, item.PageUrl, ct).ConfigureAwait(false);
        if (loaded?.Files.Count > 0)
        {
            item.Files.AddRange(loaded.Files);
            return true;
        }
        return false;
    }

    private async Task<string?> FetchJsonAsync(string url, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(_manualCookieHeader))
                request.Headers.TryAddWithoutValidation("Cookie", _manualCookieHeader);
            if (!string.IsNullOrWhiteSpace(_manualUserAgent))
                request.Headers.TryAddWithoutValidation("User-Agent", _manualUserAgent);

            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)
                : null;
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    private async Task<string?> FetchHtmlAsync(string url, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_manualCookieHeader))
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("Cookie", _manualCookieHeader);
                if (!string.IsNullOrWhiteSpace(_manualUserAgent))
                    request.Headers.TryAddWithoutValidation("User-Agent", _manualUserAgent);

                using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch { }
        }

        return await HttpHelper.FetchHtmlAsync(_http, url, maxRetries: 3, ct).ConfigureAwait(false);
    }

    private async Task<HashSet<int>?> FetchLikelyWorkPageIdsAsync(List<(int PageId, string Title)> batch, CancellationToken ct)
    {
        var pageIds = batch.Where(x => x.PageId > 0).Select(x => x.PageId).Distinct().ToList();
        if (pageIds.Count == 0) return null;

        var result = new HashSet<int>();

        foreach (var chunk in pageIds.Chunk(100))
        {
            var url = $"{ApiUrl}?action=query&prop=categories&cllimit=max&format=json&pageids={string.Join('|', chunk)}";
            var json = await FetchJsonAsync(url, ct).ConfigureAwait(false);
            if (json is null) return null;

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch
            {
                return null;
            }

            using (doc)
            {
                if (!doc.RootElement.TryGetProperty("query", out var query) ||
                    !query.TryGetProperty("pages", out var pages))
                    return null;

                foreach (var page in pages.EnumerateObject())
                {
                    var value = page.Value;
                    if (!value.TryGetProperty("pageid", out var pageIdEl) || !pageIdEl.TryGetInt32(out var pageId))
                        continue;

                    if (value.TryGetProperty("categories", out var categories) && HasLikelyWorkCategory(categories))
                        result.Add(pageId);
                }
            }
        }

        return result;
    }

    private static bool HasLikelyWorkCategory(JsonElement categories)
    {
        foreach (var category in categories.EnumerateArray())
        {
            if (!category.TryGetProperty("title", out var titleEl))
                continue;

            var title = titleEl.GetString() ?? string.Empty;
            foreach (var prefix in LikelyWorkCategoryPrefixes)
            {
                if (title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
