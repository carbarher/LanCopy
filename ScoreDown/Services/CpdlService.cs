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
    private readonly string _noFilesPageIdsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScoreDown",
        "cpdl-no-files-pageids.json");
    private readonly HashSet<int> _knownNoFilesPageIds = [];
    private readonly object _noFilesLock = new();
    private const int NoFilesFlushBatch = 50;
    private int _pendingNoFilesWrites;
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
        LoadNoFilesBlacklist();
    }

    /// <summary>
    /// Optional callback invoked automatically when CPDL is blocked (Cloudflare).
    /// Should open an interactive session dialog and call <see cref="SetManualSession"/>.
    /// Return <c>true</c> if a session was established (triggers one retry).
    /// </summary>
    public Func<Task<bool>>? RequestInteractiveSessionAsync { get; set; }

    /// <summary>
    /// When set, all CPDL HTML page fetches are routed through this delegate instead of
    /// <see cref="HttpClient"/>. Use a WebView2-based fetcher to preserve the TLS fingerprint
    /// that matched the Cloudflare challenge (cf_clearance is bound to the browser's TLS stack).
    /// </summary>
    public Func<string, CancellationToken, Task<string?>>? WebViewFetchAsync { get; set; }

    public bool HasManualSession => !string.IsNullOrWhiteSpace(_manualCookieHeader);

    public (string? Cookie, string? UserAgent) GetSessionHeaders() =>
        (_manualCookieHeader, _manualUserAgent);

    public void SetManualSession(string? cookieHeader, string? userAgent = null)
    {
        _manualCookieHeader = string.IsNullOrWhiteSpace(cookieHeader) ? null : cookieHeader.Trim();
        _manualUserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim();
    }

    public void FlushNoFilesBlacklist()
    {
        List<int>? snapshot = null;
        lock (_noFilesLock)
        {
            if (_pendingNoFilesWrites <= 0) return;
            snapshot = _knownNoFilesPageIds.OrderBy(id => id).ToList();
            _pendingNoFilesWrites = 0;
        }
        if (snapshot is not null)
            SaveNoFilesBlacklist(snapshot);
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

            var files = ExtractFiles(doc, title, ExtractComposer(doc, title));
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

    private static List<PartituraFile> ExtractFiles(HtmlDoc doc, string baseTitle, string composer)
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
            var format = ext;

            if (!LooksLikeDownloadLink(href, link)) continue;

            var absolute = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? href
                : href.StartsWith("/") ? BaseUrl + href : $"{BaseUrl}/{href.TrimStart('/')}";

            var fileName = Path.GetFileName(href.Split('?')[0]);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = FileNameHelper.GenerateFileName(composer, CleanTitle(baseTitle), ext?.ToUpperInvariant());
            else if (!fileName.Contains('.', StringComparison.Ordinal))
                fileName = FileNameHelper.GenerateFileName(composer, CleanTitle(baseTitle), ext?.ToUpperInvariant());
            else
                // Mantener el nombre del archivo si viene con extensión del URL
                fileName = FileNameHelper.SanitizeFileName(fileName);

            if (files.Any(f => string.Equals(f.DownloadUrl, absolute, StringComparison.OrdinalIgnoreCase)))
                continue;

            files.Add(new PartituraFile
            {
                Format = format,
                DownloadUrl = absolute,
                FileName = fileName
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

    // SanitizeFileName deprecated — usar FileNameHelper.SanitizeFileName


    private static string CleanTitle(string title) => title.Replace("  ", " ").Trim();

    private const string ApiUrl = "https://www.cpdl.org/wiki/api.php";
    private static readonly Regex s_composerInTitle =
        new(@"\(([^,)]+),\s*([^)]+)\)\s*$", RegexOptions.Compiled);
    private static readonly Regex s_composerSuffix =
        new(@"\s*\([^,)]+,\s*[^)]+\)\s*$", RegexOptions.Compiled);
    private static readonly Regex s_parentheticalSuffix =
        new(@"\([^)]+\)\s*$", RegexOptions.Compiled);

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
        bool retriedAfterSession = false;

    retryFetch:
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

                    var filter = await FetchLikelyWorkPageIdsAsync(batch, ct).ConfigureAwait(false);

                    foreach (var entry in batch)
                    {
                        var rawTitle = entry.Title;

                        if (entry.PageId > 0 && IsKnownNoFilesPage(entry.PageId))
                        {
                            filteredOut++;
                            continue;
                        }

                        var keep = true;
                        if (filter is not null && entry.PageId > 0 && filter.EvaluatedPageIds.Contains(entry.PageId))
                        {
                            keep = filter.AllowedPageIds.Contains(entry.PageId);
                        }
                        else
                        {
                            // If API filtering is unavailable or partial, avoid obvious non-work pages.
                            keep = LooksLikeCatalogWorkTitle(rawTitle);
                        }

                        if (!keep)
                        {
                            filteredOut++;
                            continue;
                        }

                        var m = s_composerInTitle.Match(rawTitle);
                        var composer = m.Success
                            ? $"{m.Groups[2].Value.Trim()} {m.Groups[1].Value.Trim()}"
                            : string.Empty;
                        var cleanTitle = s_composerSuffix.Replace(rawTitle, "").Trim();
                        // Encode title for URL: spaces→_ (MediaWiki convention), then escape special chars
                        var encodedTitle = Uri.EscapeDataString(rawTitle.Replace(' ', '_'))
                            .Replace("%2F", "/");  // slashes are valid path separators in MediaWiki
                        var pageUrl = $"{BaseUrl}/wiki/index.php/{encodedTitle}";

                        onItem(new PartituraItem
                        {
                            Title = cleanTitle,
                            Composer = composer,
                            PageUrl = pageUrl,
                            Source = "CPDL",
                            SourcePageId = entry.PageId
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
        {
            if (!retriedAfterSession && RequestInteractiveSessionAsync is { } ask)
            {
                progress?.Report("⚠️ CPDL bloqueado (Cloudflare). Abriendo sesión interactiva automáticamente...");
                var sessionOk = await ask().ConfigureAwait(false);
                if (sessionOk)
                {
                    retriedAfterSession = true;
                    goto retryFetch;
                }
            }
            progress?.Report("⚠️ CPDL no disponible: protección anti-bot del sitio (Cloudflare). Sin sesión activa.");
        }
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
        if (item.SourcePageId > 0 && IsKnownNoFilesPage(item.SourcePageId)) return false;

        var loaded = await LoadWorkPageAsync(item.Title, item.PageUrl, ct).ConfigureAwait(false);
        if (loaded?.Files.Count > 0)
        {
            item.Files.AddRange(loaded.Files);
            return true;
        }

        if (item.SourcePageId > 0)
            RegisterNoFilesPage(item.SourcePageId);

        return false;
    }

    private bool IsKnownNoFilesPage(int pageId)
    {
        lock (_noFilesLock)
            return _knownNoFilesPageIds.Contains(pageId);
    }

    private void RegisterNoFilesPage(int pageId)
    {
        if (pageId <= 0) return;

        List<int>? snapshot = null;
        lock (_noFilesLock)
        {
            if (!_knownNoFilesPageIds.Add(pageId)) return;

            _pendingNoFilesWrites++;
            if (_pendingNoFilesWrites >= NoFilesFlushBatch)
            {
                snapshot = _knownNoFilesPageIds.OrderBy(id => id).ToList();
                _pendingNoFilesWrites = 0;
            }
        }
        // I/O outside lock — parallel LoadFilesAsync callers don't block on file write
        if (snapshot is not null)
            SaveNoFilesBlacklist(snapshot);
    }

    private void LoadNoFilesBlacklist()
    {
        try
        {
            if (!File.Exists(_noFilesPageIdsPath)) return;

            var json = File.ReadAllText(_noFilesPageIdsPath);
            var ids = JsonSerializer.Deserialize<List<int>>(json) ?? [];
            lock (_noFilesLock)
            {
                _knownNoFilesPageIds.Clear();
                foreach (var id in ids)
                {
                    if (id > 0)
                        _knownNoFilesPageIds.Add(id);
                }
            }
        }
        catch
        {
            // Ignore corrupt cache. Next saves will replace it.
        }
    }

    private void SaveNoFilesBlacklist(List<int> payload)
    {
        var dir = Path.GetDirectoryName(_noFilesPageIdsPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        // Atomic write: temp file + rename prevents corruption on crash
        var tmp = _noFilesPageIdsPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(payload));
        File.Move(tmp, _noFilesPageIdsPath, overwrite: true);
    }

    private async Task<string?> FetchJsonAsync(string url, CancellationToken ct)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrWhiteSpace(_manualCookieHeader))
                    request.Headers.TryAddWithoutValidation("Cookie", _manualCookieHeader);
                if (!string.IsNullOrWhiteSpace(_manualUserAgent))
                    request.Headers.TryAddWithoutValidation("User-Agent", _manualUserAgent);

                using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var code = (int)response.StatusCode;
                    if ((code == 429 || code >= 500) && attempt < 2)
                    {
                        var delay = code == 429 ? 2000 * (attempt + 1) : 300 * (attempt + 1);
                        await Task.Delay(delay, ct).ConfigureAwait(false);
                        continue;
                    }
                    return null;
                }

                return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (HttpRequestException) when (attempt < 2)
            {
                await Task.Delay(300 * (attempt + 1), ct).ConfigureAwait(false);
            }
            catch { return null; }
        }
        return null;
    }

    private async Task<string?> FetchHtmlAsync(string url, CancellationToken ct)
    {
        // When a WebView2-based fetcher is available (session mode), always use it.
        // This is required because cf_clearance is bound to the TLS fingerprint of the browser
        // that solved the challenge. .NET HttpClient has a different TLS fingerprint → 403.
        if (WebViewFetchAsync is { } wvFetch && HasManualSession)
        {
            try
            {
                var html = await wvFetch(url, ct).ConfigureAwait(false);
                if (html is not null)
                {
                    if (IsCloudflareChallengePage(html))
                    {
                        // Challenge page returned even through WebView2 — session expired.
                        // Re-trigger interactive session if a handler is registered.
                        if (RequestInteractiveSessionAsync is { } ask)
                        {
                            var ok = await ask().ConfigureAwait(false);
                            if (ok)
                            {
                                // One retry after fresh session.
                                html = await wvFetch(url, ct).ConfigureAwait(false);
                                if (html is not null && !IsCloudflareChallengePage(html))
                                    return html;
                            }
                        }
                        return null;
                    }
                    return html;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }
            // WebView fetch failed — do NOT fall through to sessionless HttpClient;
            // a sessionless request will also be blocked by Cloudflare.
            return null;
        }

        // No session: try with stored cookie header (legacy path, may fail with Cloudflare).
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
                {
                    var html = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    if (!IsCloudflareChallengePage(html))
                        return html;
                    // Got a challenge page — DO NOT fall through to unauthenticated request.
                    return null;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }
        }

        return await HttpHelper.FetchHtmlAsync(_http, url, maxRetries: 3, ct).ConfigureAwait(false);
    }

    private static bool IsCloudflareChallengePage(string html) =>
        html.Contains("cf-challenge-running", StringComparison.OrdinalIgnoreCase) ||
        html.Contains("Just a moment", StringComparison.Ordinal) ||
        html.Contains("cf_chl_opt", StringComparison.OrdinalIgnoreCase) ||
        html.Contains("Checking your browser", StringComparison.OrdinalIgnoreCase);

    private sealed class CpdlPageFilterResult
    {
        public HashSet<int> AllowedPageIds { get; } = [];
        public HashSet<int> EvaluatedPageIds { get; } = [];
    }

    private async Task<CpdlPageFilterResult?> FetchLikelyWorkPageIdsAsync(List<(int PageId, string Title)> batch, CancellationToken ct)
    {
        var pageIds = batch.Where(x => x.PageId > 0).Select(x => x.PageId).Distinct().ToList();
        if (pageIds.Count == 0) return null;

        var result = new CpdlPageFilterResult();
        var chunks = pageIds.Chunk(100).ToList();

        // Fetch all chunks in parallel (max 3 concurrent) — read-only API, safe to parallelize
        using var chunkSem = new SemaphoreSlim(3);
        var successfulChunks = 0;
        var chunkLock = new object();

        await Task.WhenAll(chunks.Select(async chunk =>
        {
            await chunkSem.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var url = $"{ApiUrl}?action=query&prop=categories|images&cllimit=max&imlimit=max&format=json&pageids={string.Join('|', chunk)}";
                var json = await FetchJsonAsync(url, ct).ConfigureAwait(false);
                if (json is null) return;

                JsonDocument doc;
                try { doc = JsonDocument.Parse(json); }
                catch { return; }

                using (doc)
                {
                    if (!doc.RootElement.TryGetProperty("query", out var query) ||
                        !query.TryGetProperty("pages", out var pages))
                        return;

                    var allowed = new List<int>();
                    var evaluated = new List<int>();

                    foreach (var page in pages.EnumerateObject())
                    {
                        var value = page.Value;
                        if (!value.TryGetProperty("pageid", out var pageIdEl) || !pageIdEl.TryGetInt32(out var pageId))
                            continue;

                        evaluated.Add(pageId);

                        var hasLikelyCategory = value.TryGetProperty("categories", out var categories) && HasLikelyWorkCategory(categories);
                        var hasDownloadableMedia = value.TryGetProperty("images", out var images) && HasLikelyDownloadableMedia(images);
                        if (hasLikelyCategory && hasDownloadableMedia)
                            allowed.Add(pageId);
                    }

                    lock (chunkLock)
                    {
                        successfulChunks++;
                        foreach (var id in evaluated) result.EvaluatedPageIds.Add(id);
                        foreach (var id in allowed) result.AllowedPageIds.Add(id);
                    }
                }
            }
            finally
            {
                chunkSem.Release();
            }
        })).ConfigureAwait(false);

        return successfulChunks > 0 ? result : null;
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

    private static bool HasLikelyDownloadableMedia(JsonElement images)
    {
        foreach (var image in images.EnumerateArray())
        {
            if (!image.TryGetProperty("title", out var titleEl))
                continue;

            var title = titleEl.GetString() ?? string.Empty;
            if (!title.StartsWith("File:", StringComparison.OrdinalIgnoreCase))
                continue;

            if (GetExtension(title) is not null)
                return true;
        }

        return false;
    }

    private static bool LooksLikeCatalogWorkTitle(string rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle)) return false;
        if (rawTitle.Length < 6) return false;

        if (s_composerInTitle.IsMatch(rawTitle))
            return true;

        // Many CPDL works end with a parenthetical disambiguator; keep those as fallback.
        return s_parentheticalSuffix.IsMatch(rawTitle);
    }
}
