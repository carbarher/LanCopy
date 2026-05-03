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

public class ImslpService
{
    private readonly HttpClient _http;
    private const string BaseUrl = "https://imslp.org";
    private static readonly string[] AllowedFormats = ["PDF", "MIDI", "XML", "MXL", "MSCZ", "MSCX"];

    public ImslpService()
    {
        _http = HttpClientProvider.GetDefault();
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
            var files = ExtractFiles(doc, title);

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

    private static List<PartituraFile> ExtractFiles(HtmlDoc doc, string baseTitle)
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
                    FileName = SanitizeFileName(fileName)
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
                FileName = SanitizeFileName(fileName)
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
        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)
                : null;
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    private Task<string?> FetchHtmlAsync(string url, CancellationToken ct)
        => HttpHelper.FetchHtmlAsync(_http, url, maxRetries: 3, ct);
}
