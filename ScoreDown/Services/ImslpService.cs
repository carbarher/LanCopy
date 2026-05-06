using HtmlAgilityPack;
using ScoreDown.Infrastructure;
using ScoreDown.Models;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using HtmlDoc = HtmlAgilityPack.HtmlDocument;

namespace ScoreDown.Services;

public class ImslpService
{
    private readonly HttpClient _http;
    private const string BaseUrl = "https://imslp.org";
    private const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36";
    private static readonly string[] AllowedFormats = ["PDF", "MIDI", "XML", "MXL", "MSCZ", "MSCX"];
    private readonly Dictionary<string, string> _cookieJar = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _cookieLock = new();
    private readonly ConcurrentDictionary<string, byte> _warmedPages = new(StringComparer.OrdinalIgnoreCase);
    private string _sessionUserAgent = DefaultUserAgent;

    public ImslpService()
    {
        _http = HttpClientProvider.GetDefault();
    }

    public (string? CookieHeader, string? UserAgent) GetSessionHeaders()
    {
        lock (_cookieLock)
        {
            if (_cookieJar.Count == 0)
                return (null, _sessionUserAgent);

            var cookieHeader = string.Join("; ", _cookieJar.Select(kv => $"{kv.Key}={kv.Value}"));
            return (cookieHeader, _sessionUserAgent);
        }
    }

    public void SetManualSession(string? cookieHeader, string? userAgent = null)
    {
        lock (_cookieLock)
        {
            if (!string.IsNullOrWhiteSpace(userAgent))
                _sessionUserAgent = userAgent.Trim();

            if (string.IsNullOrWhiteSpace(cookieHeader))
                return;

            var pairs = cookieHeader.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var eq = pair.IndexOf('=');
                if (eq <= 0 || eq >= pair.Length - 1)
                    continue;

                var name = pair[..eq].Trim();
                var value = pair[(eq + 1)..].Trim();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
                    continue;

                _cookieJar[name] = value;
            }
        }
    }

    public async Task WarmupPageAsync(string? pageUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pageUrl))
            return;
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri))
            return;
        if (!string.Equals(uri.Host, "imslp.org", StringComparison.OrdinalIgnoreCase))
            return;
        if (_warmedPages.ContainsKey(pageUrl))
            return;

        using var warmupCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        warmupCts.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var response = await SendGetWithSessionAsync(pageUrl, warmupCts.Token).ConfigureAwait(false);
            _ = response.StatusCode;
            _warmedPages.TryAdd(pageUrl, 1);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { /* timeout: best effort */ }
        catch (OperationCanceledException) { throw; }
        catch
        {
            // Warm-up best effort: si falla, flujo normal de descarga seguirá intentando.
        }
    }

    public async Task<List<PartituraItem>> SearchAsync(
        string query,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report($"🔍 Buscando '{query}' en IMSLP...");

        var encoded = HttpUtility.UrlEncode(query);
        var searchUrl = $"{BaseUrl}/wiki/Special:Search?search={encoded}&ns0=1";

        var html = await FetchHtmlAsync(searchUrl, ct).ConfigureAwait(false);
        if (html is null)
        {
            progress?.Report("❌ No se pudo conectar con IMSLP");
            return [];
        }

        var doc = new HtmlDoc();
        doc.LoadHtml(html);

        // Resultados de búsqueda: enlaces en .mw-search-result-heading
        var resultNodes = doc.DocumentNode
            .SelectNodes("//div[@class='mw-search-result-heading']//a")
            ?? doc.DocumentNode.SelectNodes("//ul[@class='mw-search-results']//a");

        if (resultNodes is null || resultNodes.Count == 0)
        {
            progress?.Report("⚠️ No se encontraron resultados");
            return [];
        }

        progress?.Report($"📄 {resultNodes.Count} resultados encontrados, cargando detalles...");

        var items = new List<PartituraItem>();
        int idx = 0;
        foreach (var node in resultNodes.Take(20))
        {
            ct.ThrowIfCancellationRequested();
            idx++;

            var href = node.GetAttributeValue("href", "");
            var title = node.InnerText.Trim();
            if (string.IsNullOrEmpty(href) || string.IsNullOrEmpty(title)) continue;
            if (!href.StartsWith("/wiki/")) continue;

            // Excluir páginas que no son obras musicales
            if (href.Contains("Special:") || href.Contains("Category:") || href.Contains("IMSLP:")) continue;

            var pageUrl = BaseUrl + href;
            progress?.Report($"📋 [{idx}/{Math.Min(resultNodes.Count, 20)}] Analizando: {title}");

            var item = await LoadPartituraPageAsync(title, pageUrl, ct).ConfigureAwait(false);
            if (item is not null && item.Files.Count > 0)
                items.Add(item);

            await Task.Delay(800, ct).ConfigureAwait(false); // cortesía al servidor
        }

        progress?.Report($"✅ {items.Count} obras con archivos descargables");
        return items;
    }

    private async Task<PartituraItem?> LoadPartituraPageAsync(string title, string pageUrl, CancellationToken ct)
    {
        try
        {
            var html = await FetchHtmlAsync(pageUrl, ct).ConfigureAwait(false);
            if (html is null) return null;

            var doc = new HtmlDoc();
            doc.LoadHtml(html);

            // Extraer compositor del título H1 o del infobox
            var composer = ExtractComposer(doc, title);

            // Buscar enlaces de descarga en tablas de archivos
            var files = ExtractFiles(doc, title, pageUrl);

            return new PartituraItem
            {
                Title = CleanTitle(title),
                Composer = composer,
                PageUrl = pageUrl,
                Files = files
            };
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractComposer(HtmlDoc doc, string title)
    {
        // Intento 1: categorías con "Category:Compositions by"
        var cats = doc.DocumentNode.SelectNodes("//div[@id='mw-normal-catlinks']//a");
        if (cats != null)
        {
            foreach (var c in cats)
            {
                var t = c.InnerText;
                if (t.StartsWith("Works by ") || t.StartsWith("Compositions by "))
                    return t.Replace("Works by ", "").Replace("Compositions by ", "").Trim();
            }
        }

        // Intento 2: primer segmento antes de la coma en el título
        var comma = title.IndexOf(',');
        if (comma > 0) return title[..comma].Trim();

        return string.Empty;
    }

    private static List<PartituraFile> ExtractFiles(HtmlDoc doc, string baseTitle, string pageUrl)
    {
        var files = new List<PartituraFile>();

        // IMSLP usa spans/divs con clase "we" para los archivos
        var fileRows = doc.DocumentNode.SelectNodes("//div[contains(@class,'we_file_download')]//a[@href]")
                     ?? doc.DocumentNode.SelectNodes("//a[contains(@href,'Special:ImagePage') or contains(@href,'/images/')]");

        if (fileRows is null)
        {
            // Fallback: buscar links directos a PDF/MIDI
            var links = doc.DocumentNode.SelectNodes("//a[@href]");
            if (links is null) return files;

            foreach (var link in links)
            {
                var href = link.GetAttributeValue("href", "");
                var ext = GetExtension(href);
                if (ext is null) continue;

                var fileName = System.IO.Path.GetFileName(href.Split('?')[0]);
                if (string.IsNullOrEmpty(fileName)) fileName = $"{CleanTitle(baseTitle)}.{ext.ToLower()}";

                files.Add(new PartituraFile
                {
                    Format = ext,
                    DownloadUrl = href.StartsWith("http") ? href : "https://imslp.org" + href,
                    FileName = SanitizeFileName(fileName),
                    SourcePageUrl = pageUrl
                });
            }
            return files;
        }

        foreach (var link in fileRows)
        {
            var href = link.GetAttributeValue("href", "");
            var ext = GetExtension(href) ?? GetExtension(link.InnerText);
            if (ext is null) continue;

            var fileName = System.IO.Path.GetFileName(href.Split('?')[0]);
            if (string.IsNullOrEmpty(fileName)) fileName = $"{CleanTitle(baseTitle)}.{ext.ToLower()}";

            files.Add(new PartituraFile
            {
                Format = ext,
                DownloadUrl = href.StartsWith("http") ? href : "https://imslp.org" + href,
                FileName = SanitizeFileName(fileName),
                SourcePageUrl = pageUrl
            });
        }

        return files;
    }

    private static string? GetExtension(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        // Comprobar primero la extensión real del path/URL
        var ext = Path.GetExtension(text.Split('?')[0]).TrimStart('.').ToUpperInvariant();
        if (Array.IndexOf(AllowedFormats, ext) >= 0) return ext;
        // Fallback: el texto visible del enlace (ej. "PDF", "MIDI")
        var upper = text.ToUpperInvariant();
        foreach (var fmt in AllowedFormats)
            if (upper == fmt || upper.EndsWith("." + fmt))
                return fmt;
        return null;
    }

    private static string CleanTitle(string title) =>
        title.Replace("(IMSLP)", "").Replace("  ", " ").Trim();

    private static readonly HashSet<char> s_invalidFileNameChars =
        new(Path.GetInvalidFileNameChars());

    private static string SanitizeFileName(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(s_invalidFileNameChars.Contains(c) ? '_' : c);
        return sb.ToString();
    }

    private const string ApiUrl = "https://imslp.org/api.php";
    private static readonly Regex s_composerInTitle =
        new(@"\(([^,)]+),\s*([^)]+)\)\s*$", RegexOptions.Compiled);
    private static readonly Regex s_composerSuffix =
        new(@"\s*\([^,)]+,\s*[^)]+\)\s*$", RegexOptions.Compiled);
    private const int MaxCatalogItems = 5000;
    // Páginas del namespace 0 de IMSLP que no son obras musicales (páginas de compositor, listas, etc.)
    // Formato compositor IMSLP: "Apellido, Nombre" sin paréntesis adicionales — se distinguen de obras
    // porque no tienen el sufijo "(Compositor, Apellido)" al final.
    private static bool IsLikelyNonWorkPage(string title)
    {
        // Páginas de compositor: "Apellido, Nombre" — tienen coma pero NO terminan en "(...)"
        // Las obras tienen formato "Título (Apellido, Nombre)"
        if (title.EndsWith(')')) return false;  // tiene sufijo de compositor → es obra
        // Título con coma pero sin paréntesis al final → probable página de compositor
        if (title.Contains(',') && !title.Contains('(')) return true;
        return false;
    }

    /// <summary>
    /// Streams the full IMSLP catalog via MediaWiki allpages API.
    /// Items have empty Files — call <see cref="LoadFilesAsync"/> before downloading.
    /// </summary>
    public async Task FetchAllAsync(
        Action<PartituraItem> onItem,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report("📋 Listando páginas de IMSLP via API...");
        string? apFrom = null;
        string? lastBatchLastTitle = null;
        int total = 0;
        bool hitLimit = false;
        var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var url = $"{ApiUrl}?action=query&list=allpages&aplimit=500&apnamespace=0&apfilterredir=nonredirects&format=json";
            if (!string.IsNullOrWhiteSpace(apFrom))
                url += $"&apfrom={HttpUtility.UrlEncode(apFrom)}";

            var json = await FetchJsonAsync(url, ct).ConfigureAwait(false);
            if (json is null) break;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("query", out var query) || !query.TryGetProperty("allpages", out var pages))
                break;

            int addedThisBatch = 0;
            string? batchLastTitle = null;

            foreach (var page in pages.EnumerateArray())
            {
                var rawTitle = page.GetProperty("title").GetString() ?? "";
                batchLastTitle = rawTitle;
                if (string.IsNullOrEmpty(rawTitle) || rawTitle.Contains(':')) continue;
                if (IsLikelyNonWorkPage(rawTitle)) continue;
                if (!seenTitles.Add(rawTitle)) continue;

                var m = s_composerInTitle.Match(rawTitle);
                var composer = m.Success
                    ? $"{m.Groups[2].Value.Trim()} {m.Groups[1].Value.Trim()}"
                    : string.Empty;
                var cleanTitle = s_composerSuffix.Replace(rawTitle, "").Trim();
                var pageUrl = $"{BaseUrl}/wiki/{rawTitle.Replace(' ', '_')}";

                onItem(new PartituraItem
                {
                    Title = cleanTitle,
                    Composer = composer,
                    PageUrl = pageUrl,
                    Source = "IMSLP"
                });
                total++;
                addedThisBatch++;

                if (total >= MaxCatalogItems)
                {
                    hitLimit = true;
                    break;
                }
            }

            if (total % 2000 == 0 && total > 0)
                progress?.Report($"📋 IMSLP: {total} obras catalogadas...");

            if (hitLimit)
                break;

            if (string.IsNullOrWhiteSpace(batchLastTitle))
                break;
            if (addedThisBatch == 0)
                break;
            if (string.Equals(batchLastTitle, lastBatchLastTitle, StringComparison.Ordinal))
                break;

            lastBatchLastTitle = batchLastTitle;
            apFrom = batchLastTitle;

            await Task.Delay(100, ct).ConfigureAwait(false);
        }

        if (hitLimit)
            progress?.Report($"⚠️ IMSLP: límite de seguridad alcanzado ({MaxCatalogItems} obras)");
        progress?.Report($"✅ IMSLP: {total} obras en catálogo");
    }

    /// <summary>
    /// Lazy-loads file download links for a catalog item (fetches its detail page).
    /// </summary>
    public async Task<bool> LoadFilesAsync(PartituraItem item, CancellationToken ct = default)
    {
        if (item.Files.Count > 0) return true;
        if (string.IsNullOrEmpty(item.PageUrl)) return false;
        var loaded = await LoadPartituraPageAsync(item.Title, item.PageUrl, ct).ConfigureAwait(false);
        if (loaded?.Files.Count > 0)
        {
            item.Files.AddRange(loaded.Files);
            return true;
        }
        return false;
    }

    private async Task<string?> FetchJsonAsync(string url, CancellationToken ct)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var response = await SendGetWithSessionAsync(url, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var code = (int)response.StatusCode;
                    if ((code == 429 || code >= 500) && attempt < 2)
                    {
                        var retryAfter = response.Headers.RetryAfter;
                        var candidate = retryAfter?.Delta
                            ?? (retryAfter?.Date.HasValue == true ? retryAfter.Date.Value - DateTimeOffset.UtcNow : (TimeSpan?)null);
                        var delay = candidate.HasValue && candidate.Value > TimeSpan.Zero && candidate.Value <= TimeSpan.FromSeconds(60)
                            ? (int)candidate.Value.TotalMilliseconds
                            : (code == 429 ? 2000 * (attempt + 1) : 300 * (attempt + 1));
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
        for (int attempt = 0; attempt < 3; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var response = await SendGetWithSessionAsync(url, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var code = (int)response.StatusCode;
                    if ((code == 429 || code >= 500) && attempt < 2)
                    {
                        var retryAfter = response.Headers.RetryAfter;
                        var candidate = retryAfter?.Delta
                            ?? (retryAfter?.Date.HasValue == true ? retryAfter.Date.Value - DateTimeOffset.UtcNow : (TimeSpan?)null);
                        var delay = candidate.HasValue && candidate.Value > TimeSpan.Zero && candidate.Value <= TimeSpan.FromSeconds(60)
                            ? (int)candidate.Value.TotalMilliseconds
                            : (code == 429 ? 2000 * (attempt + 1) : 300 * (attempt + 1));
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
        }

        return null;
    }

    private async Task<HttpResponseMessage> SendGetWithSessionAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", _sessionUserAgent);
        request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        request.Headers.TryAddWithoutValidation("Accept-Language", "es-ES,es;q=0.9,en;q=0.8");

        var cookieHeader = GetCookieHeader();
        if (!string.IsNullOrWhiteSpace(cookieHeader))
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);

        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        CaptureSetCookieHeaders(response.Headers);
        if (response.Content?.Headers != null)
            CaptureSetCookieHeaders(response.Content.Headers);
        return response;
    }

    private string? GetCookieHeader()
    {
        lock (_cookieLock)
            return _cookieJar.Count == 0
                ? null
                : string.Join("; ", _cookieJar.Select(kv => $"{kv.Key}={kv.Value}"));
    }

    private void CaptureSetCookieHeaders(HttpHeaders headers)
    {
        if (!headers.TryGetValues("Set-Cookie", out var setCookies))
            return;

        lock (_cookieLock)
        {
            foreach (var raw in setCookies)
            {
                var first = raw.Split(';', 2, StringSplitOptions.TrimEntries)[0];
                var eq = first.IndexOf('=');
                if (eq <= 0 || eq >= first.Length - 1)
                    continue;

                var name = first[..eq].Trim();
                var value = first[(eq + 1)..].Trim();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
                    continue;
                if (string.Equals(value, "deleted", StringComparison.OrdinalIgnoreCase))
                    continue;

                _cookieJar[name] = value;
            }
        }
    }
}
