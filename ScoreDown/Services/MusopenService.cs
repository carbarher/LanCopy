using ScoreDown.Infrastructure;
using ScoreDown.Models;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Web;

namespace ScoreDown.Services;

/// <summary>
/// Busca y descarga partituras desde Musopen (musopen.org) via su API REST publica.
/// Endpoint: https://musopen.org/api/v2/scores/
/// Descarga requiere sesion autenticada: llamar SetSession() tras login via CpdlSessionDialog.
/// </summary>
public class MusopenService
{
    private readonly HttpClient _http;
    private string? _sessionCookieHeader;
    private string? _sessionUserAgent;
    private const string BaseUrl = "https://musopen.org/api/v2/scores/";
    private const int MaxCatalogPages = 2000;
    private const int PageDelayMs = 50;

    public bool HasApiKey => true;
    public bool HasSession => _sessionCookieHeader != null;

    public (string? Cookie, string? UserAgent) GetSessionHeaders() =>
        (_sessionCookieHeader, _sessionUserAgent);

    public MusopenService()
    {
        _http = HttpClientProvider.GetDefault();
    }

    public void SetSession(string cookieHeader, string? userAgent = null)
    {
        _sessionCookieHeader = cookieHeader;
        _sessionUserAgent = userAgent;
    }

    public void ClearSession()
    {
        _sessionCookieHeader = null;
        _sessionUserAgent = null;
    }

    // -- Search (multi-page, hasta 5 paginas de 50) -------------------------

    public async Task<List<PartituraItem>> SearchAsync(
        string query,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report($"Buscando `{query}` en Musopen...");

        var results = new List<PartituraItem>();
        var seen = new HashSet<int>();
        string? url = BuildUrl(search: query, limit: 50);
        int pagesLeft = 5;

        while (url != null && pagesLeft-- > 0)
        {
            var (items, nextUrl, ok) = await FetchPageWithRetryAsync(url, progress, ct).ConfigureAwait(false);
            foreach (var item in items)
                if (seen.Add(item.SourcePageId)) results.Add(item);

            if (!ok || nextUrl == null) break;
            url = nextUrl;
        }

        progress?.Report(results.Count == 0
            ? "Sin resultados en Musopen"
            : $"Musopen: {results.Count} obras encontradas");

        return results;
    }

    // -- Catalog (completo, con pipeline de prefetch) -----------------------

    public async Task FetchAllAsync(
        Action<PartituraItem> onItem,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report("Descargando catalogo de Musopen...");

        var seen = new HashSet<int>();
        int count = 0;
        int pageCount = 0;
        int errorStreak = 0;
        const int MaxErrorStreak = 5;

        string firstUrl = BuildUrl(limit: 100);

        // Pipeline: mientras procesa pagina N, hace fetch de pagina N+1
        var inflight = FetchPageWithRetryAsync(firstUrl, progress, ct);

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (++pageCount > MaxCatalogPages) break;

            var (items, nextUrl, ok) = await inflight.ConfigureAwait(false);

            // Lanzar fetch siguiente ANTES de procesar la actual
            inflight = nextUrl != null
                ? FetchPageWithRetryAsync(nextUrl, progress, ct)
                : Task.FromResult<(List<PartituraItem>, string?, bool)>((new List<PartituraItem>(), null, true));

            if (!ok)
            {
                if (++errorStreak >= MaxErrorStreak)
                {
                    progress?.Report($"Musopen: {MaxErrorStreak} errores consecutivos, catalogo truncado en {count} obras");
                    break;
                }
                if (nextUrl == null) break;
                continue;
            }
            else
            {
                errorStreak = 0;
            }

            foreach (var item in items)
            {
                if (!seen.Add(item.SourcePageId)) continue;
                onItem(item);
                count++;
            }

            if (count % 500 == 0 && count > 0)
                progress?.Report($"Musopen: {count} obras catalogadas...");

            if (nextUrl == null) break;

            if (PageDelayMs > 0)
                await Task.Delay(PageDelayMs, ct).ConfigureAwait(false);
        }

        progress?.Report($"Musopen: {count} obras en catalogo");
    }

    // -- Core fetch: reintentos + backoff exponencial + 429 ----------------

    private async Task<(List<PartituraItem> Items, string? Next, bool Ok)> FetchPageWithRetryAsync(
        string url, IProgress<string>? progress, CancellationToken ct)
    {
        const int MaxAttempts = 3;
        int rateRetries = 0;
        const int MaxRateRetries = 3;

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("Accept", "application/json");

                using var response = await _http.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

                if ((int)response.StatusCode == 429)
                {
                    if (++rateRetries > MaxRateRetries) return (new List<PartituraItem>(), null, false);

                    var delay = 60;
                    if (response.Headers.TryGetValues("Retry-After", out var values) &&
                        int.TryParse(System.Linq.Enumerable.FirstOrDefault(values), out var ra))
                        delay = Math.Clamp(ra, 5, 300);

                    progress?.Report($"Musopen: rate limit, esperando {delay}s...");
                    await Task.Delay(TimeSpan.FromSeconds(delay), ct).ConfigureAwait(false);
                    attempt--;
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                    return (new List<PartituraItem>(), null, false);

                using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                var (items, nextUrl) = await ParseResponseAsync(stream, ct).ConfigureAwait(false);
                return (items, nextUrl, true);
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                if (attempt < MaxAttempts - 1)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)), ct).ConfigureAwait(false);
            }
        }

        return (new List<PartituraItem>(), null, false);
    }

    // -- JSON parsing (stream-based) ----------------------------------------

    private static async Task<(List<PartituraItem> Items, string? Next)> ParseResponseAsync(
        Stream stream, CancellationToken ct)
    {
        var items = new List<PartituraItem>();
        string? nextUrl = null;

        try
        {
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;

            JsonElement resultsEl;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("results", out resultsEl))
            {
                if (root.TryGetProperty("next", out var nextProp) && nextProp.ValueKind != JsonValueKind.Null)
                    nextUrl = nextProp.GetString();
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                resultsEl = root;
            }
            else
            {
                return (new List<PartituraItem>(), null);
            }

            foreach (var scoreEl in resultsEl.EnumerateArray())
            {
                var item = ParseScore(scoreEl);
                if (item != null) items.Add(item);
            }
        }
        catch { }

        return (items, nextUrl);
    }

    // -- Mapeo JSON -> PartituraItem ----------------------------------------

    private static PartituraItem? ParseScore(JsonElement el)
    {
        if (!el.TryGetProperty("id", out var idProp)) return null;
        var id = idProp.GetInt32();

        var title = GetString(el, "title");
        if (string.IsNullOrWhiteSpace(title)) return null;

        var fileUrl = GetString(el, "fileurl");
        if (string.IsNullOrWhiteSpace(fileUrl)) return null;

        var composer = string.Empty;
        var genre = string.Empty;
        var instrument = string.Empty;
        var license = string.Empty;

        if (el.TryGetProperty("piece", out var pieceEl) && pieceEl.ValueKind == JsonValueKind.Object)
        {
            if (pieceEl.TryGetProperty("composer", out var composerEl) && composerEl.ValueKind == JsonValueKind.Object)
            {
                var first = GetString(composerEl, "first_name") ?? string.Empty;
                var last = GetString(composerEl, "last_name") ?? string.Empty;
                composer = (first + " " + last).Trim();
            }

            if (pieceEl.TryGetProperty("form", out var formEl) && formEl.ValueKind == JsonValueKind.Object)
                genre = GetString(formEl, "name") ?? string.Empty;

            if (pieceEl.TryGetProperty("period", out var periodEl) && periodEl.ValueKind == JsonValueKind.Object)
            {
                var period = GetString(periodEl, "name") ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(period))
                    genre = string.IsNullOrWhiteSpace(genre) ? period : $"{genre} · {period}";
            }

            if (pieceEl.TryGetProperty("instruments", out var pieceInstr) && pieceInstr.ValueKind == JsonValueKind.Array)
                instrument = JoinNames(pieceInstr);

            if (pieceEl.TryGetProperty("licenses", out var licArr) && licArr.ValueKind == JsonValueKind.Array)
            {
                var licenses = new List<string>();
                foreach (var l in licArr.EnumerateArray())
                    if (l.ValueKind == JsonValueKind.String) { var s = l.GetString(); if (s != null) licenses.Add(s); }
                license = string.Join(", ", licenses);
            }
        }

        if (string.IsNullOrWhiteSpace(instrument) &&
            el.TryGetProperty("instruments", out var scoreInstr) && scoreInstr.ValueKind == JsonValueKind.Array)
            instrument = JoinNames(scoreInstr);

        var pageUrl = GetString(el, "url") ?? $"https://musopen.org/sheetmusic/{id}/";
        var fileNameComposer = string.IsNullOrWhiteSpace(composer) ? "" : composer + " - ";
        var fileName = SanitizeFileName($"{fileNameComposer}{title}.pdf");

        return new PartituraItem
        {
            Title = title,
            Composer = composer,
            Genre = genre,
            Instrument = instrument,
            License = license,
            PageUrl = pageUrl,
            Source = "Musopen",
            SourcePageId = id,
            Files = new List<PartituraFile>
            {
                new PartituraFile
                {
                    Format = "PDF",
                    DownloadUrl = fileUrl,
                    FileName = fileName,
                    SourcePageUrl = pageUrl
                }
            }
        };
    }

    // -- Helpers ------------------------------------------------------------

    private static string JoinNames(JsonElement arr)
    {
        var names = new List<string>();
        foreach (var el in arr.EnumerateArray())
            if (el.ValueKind == JsonValueKind.Object)
            {
                var n = GetString(el, "name");
                if (!string.IsNullOrWhiteSpace(n)) names.Add(n!);
            }
        return string.Join(", ", names);
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static string BuildUrl(string? search = null, int limit = 20)
    {
        var sb = new System.Text.StringBuilder(BaseUrl);
        sb.Append('?');
        if (!string.IsNullOrWhiteSpace(search))
            sb.Append("search=").Append(HttpUtility.UrlEncode(search)).Append('&');
        sb.Append("limit=").Append(limit);
        return sb.ToString();
    }

    private static readonly HashSet<char> s_invalidFileNameChars = new(System.IO.Path.GetInvalidFileNameChars());

    private static string SanitizeFileName(string name)
    {
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(s_invalidFileNameChars.Contains(c) ? '_' : c);
        return sb.ToString().Trim('_', ' ');
    }
}
