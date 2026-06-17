using System.Net.Http;
using System.Text.Json;

namespace LanCopy.Services;

// Comprobador de actualizaciones: consulta la API publica de GitHub Releases y compara la
// version mas reciente con la actual. Sin dependencias externas; degrada de forma silenciosa
// si no hay red. No descarga ni instala nada (eso lo decide el usuario desde la UI).
public static class UpdateChecker
{
    public sealed record UpdateInfo(bool Available, string CurrentVersion, string LatestVersion, string Url, string Notes);

    // Repositorio de releases (owner/repo). Configurable si el proyecto se aloja en otro sitio.
    public static string Owner { get; set; } = "carbarher";
    public static string Repo { get; set; } = "LanCopy";

    public static string CurrentVersion =>
        typeof(UpdateChecker).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        // Respuesta máxima: 512 KB (la API de GitHub puede devolver releases muy grandes).
        const int MaxResponseBytes = 512 * 1024;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("LanCopy-UpdateChecker");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
            // Leer con límite: evita OOM si el endpoint devuelve una respuesta enorme.
            using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            var buf = new byte[MaxResponseBytes];
            int read = await stream.ReadAsync(buf.AsMemory(0, MaxResponseBytes), ct);
            var json = System.Text.Encoding.UTF8.GetString(buf, 0, read);
            var doc = JsonSerializer.Deserialize<JsonElement>(json);

            var tag = doc.TryGetProperty("tag_name", out var t) ? (t.GetString() ?? "") : "";
            var htmlUrl = doc.TryGetProperty("html_url", out var h) ? (h.GetString() ?? "") : "";
            // Truncar notas a 4 KB: el cuerpo del release puede tener miles de caracteres.
            var rawNotes = doc.TryGetProperty("body", out var b) ? (b.GetString() ?? "") : "";
            var notes = rawNotes.Length > 4096 ? rawNotes[..4096] + "\u2026" : rawNotes;

            var latest = NormalizeVersion(tag);
            var current = CurrentVersion;
            var available = CompareVersions(latest, current) > 0;

            Log.Info("update", "checked", new { current, latest, available });
            return new UpdateInfo(available, current, latest, htmlUrl, notes);
        }
        catch (Exception ex)
        {
            Log.Debug("update", "check-failed", new { error = ex.Message });
            return null;
        }
    }

    private static string NormalizeVersion(string tag)
    {
        var s = tag.Trim();
        if (s.StartsWith('v') || s.StartsWith('V')) s = s[1..];
        return s;
    }

    // Devuelve >0 si a>b, 0 si igual, <0 si a<b. Compara hasta 4 componentes numericos.
    public static int CompareVersions(string a, string b)
    {
        var pa = Parse(a);
        var pb = Parse(b);
        for (int i = 0; i < 4; i++)
        {
            var c = pa[i].CompareTo(pb[i]);
            if (c != 0) return c;
        }
        return 0;
    }

    private static int[] Parse(string v)
    {
        var parts = v.Split('.', '-', '+');
        var res = new int[4];
        for (int i = 0; i < 4 && i < parts.Length; i++)
            int.TryParse(parts[i], out res[i]);
        return res;
    }
}