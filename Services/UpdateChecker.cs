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
            // P1: ReadAsync devuelve un único segmento TCP, no el cuerpo completo.
            // Leer en bucle hasta EOF o hasta alcanzar el límite anti-OOM (512 KB).
            // O16: Usar ArrayPool para el buffer de 512KB para evitar LOH allocations
            var rented = System.Buffers.ArrayPool<byte>.Shared.Rent(MaxResponseBytes);
            try
            {
                int total = 0, read;
                while (total < MaxResponseBytes &&
                       (read = await stream.ReadAsync(rented.AsMemory(total, MaxResponseBytes - total), ct)) > 0)
                    total += read;
                var json = System.Text.Encoding.UTF8.GetString(rented, 0, total);
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
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
            }
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

    // ---------------------------------------------------------------------------
    //  Descarga y aplicación de actualizaciones
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Consulta la API de GitHub Releases y devuelve la browser_download_url del asset
    /// que corresponde a la plataforma actual (LanCopy.exe para Windows, .tar.gz para Linux,
    /// .zip para macOS). Devuelve null si no encuentra un asset compatible.
    /// </summary>
    public static async Task<string?> GetDownloadUrlAsync(CancellationToken ct = default)
    {
        const int MaxResponseBytes = 512 * 1024;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("LanCopy-UpdateChecker");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
            using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            // O16: Usar ArrayPool en lugar de new byte[512KB]
            var rented = System.Buffers.ArrayPool<byte>.Shared.Rent(MaxResponseBytes);
            try
            {
                int total = 0, read;
                while (total < MaxResponseBytes &&
                       (read = await stream.ReadAsync(rented.AsMemory(total, MaxResponseBytes - total), ct)) > 0)
                    total += read;

                var json = System.Text.Encoding.UTF8.GetString(rented, 0, total);
                var doc = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);

            if (!doc.TryGetProperty("assets", out var assets) ||
                assets.ValueKind != System.Text.Json.JsonValueKind.Array)
                return null;

            // Determinar el sufijo esperado según la plataforma actual.
            var suffix = GetPlatformAssetSuffix();
            if (suffix is null) return null;

            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? (n.GetString() ?? "") : "";
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    var downloadUrl = asset.TryGetProperty("browser_download_url", out var d)
                        ? d.GetString()
                        : null;
                    Log.Info("update", "download-url-resolved", new { name, downloadUrl });
                    return downloadUrl;
                }
            }

            Log.Debug("update", "no-matching-asset", new { suffix });
            return null;
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
            }
        }
        catch (Exception ex)
        {
            Log.Debug("update", "download-url-failed", new { error = ex.Message });
            return null;
        }
    }

    /// <summary>
    /// Descarga el archivo desde la URL indicada a un directorio temporal, reportando progreso
    /// (0.0 – 1.0). Devuelve la ruta completa del archivo descargado o null si falla.
    /// </summary>
    public static async Task<string?> DownloadLatestAsync(
        string downloadUrl, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        try
        {
            // C13-FIX: sanitizar fileName de la URL para evitar path traversal.
            // La URL viene de GitHub API, pero si fuera manipulada (MITM o compromiso),
            // un ".." en el path resolvería fuera de LanCopy-Update con Path.Combine.
            var rawFileName = Path.GetFileName(new Uri(downloadUrl).AbsolutePath);
            // Asegurar que no contiene separadores de ruta ni caracteres inválidos
            var fileName = string.IsNullOrWhiteSpace(rawFileName) ? "LanCopy-update"
                : rawFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ? "LanCopy-update"
                : rawFileName.Contains("..") ? "LanCopy-update"
                : rawFileName;
            var destPath = Path.Combine(Path.GetTempPath(), "LanCopy-Update", fileName);

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("LanCopy-UpdateChecker");

            using var resp = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            var contentLength = resp.Content.Headers.ContentLength ?? -1;
            await using var source = await resp.Content.ReadAsStreamAsync(ct);
            await using var dest = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 81920, useAsync: true);

            long totalRead = 0;
            // O16: Usar ArrayPool para el buffer de descarga de 80KB
            var rented = System.Buffers.ArrayPool<byte>.Shared.Rent(81920);
            try
            {
                int bytesRead;
                while ((bytesRead = await source.ReadAsync(rented, ct)) > 0)
                {
                    await dest.WriteAsync(rented.AsMemory(0, bytesRead), ct);
                    totalRead += bytesRead;
                    if (contentLength > 0)
                        progress?.Report((double)totalRead / contentLength);
                }
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
            }

            progress?.Report(1.0);
            Log.Info("update", "download-complete", new { destPath, totalRead });
            return destPath;
        }
        catch (Exception ex)
        {
            Log.Debug("update", "download-failed", new { error = ex.Message });
            return null;
        }
    }

    /// <summary>
    /// Aplica la actualización: reemplaza el ejecutable actual con el descargado y lanza
    /// la nueva versión. En Windows renombra el exe actual a .old antes de copiar.
    /// Devuelve true si la operación se inició correctamente.
    /// </summary>
    public static bool ApplyUpdate(string downloadedPath)
    {
        var currentExe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(currentExe))
        {
            Log.Debug("update", "apply-failed", new { error = "Cannot determine current executable path" });
            return false;
        }

        if (!File.Exists(downloadedPath))
        {
            Log.Debug("update", "apply-failed", new { error = "Downloaded file not found", downloadedPath });
            return false;
        }

        string? oldPath = null;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Windows: no se puede sobrescribir un exe en ejecución, pero sí renombrarlo.
                oldPath = currentExe + ".old";
                // Eliminar un .old previo si existe.
                if (File.Exists(oldPath))
                    File.Delete(oldPath);

                File.Move(currentExe, oldPath);
                File.Copy(downloadedPath, currentExe, overwrite: true);

                Log.Info("update", "apply-success-win", new { currentExe, oldPath });
            }
            else
            {
                // Linux / macOS: sobrescribir directamente y preservar permisos.
                File.Copy(downloadedPath, currentExe, overwrite: true);

                // Restaurar bit de ejecución (chmod +x).
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("chmod", $"+x \"{currentExe}\"")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    System.Diagnostics.Process.Start(psi)?.WaitForExit(5000);
                }
                catch
                {
                    // No es crítico; en muchos escenarios el bit ya se conserva.
                }

                Log.Info("update", "apply-success-unix", new { currentExe });
            }

            // Lanzar la nueva versión y salir.
            var startInfo = new System.Diagnostics.ProcessStartInfo(currentExe)
            {
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(startInfo)?.Dispose(); // O16: dispose Process
            Environment.Exit(0);

            return true; // No se alcanza, pero satisface el contrato del método.
        }
        catch (Exception ex)
        {
            if (oldPath != null && File.Exists(oldPath) && !File.Exists(currentExe))
            {
                try { File.Move(oldPath, currentExe); } catch { }
            }
            Log.Debug("update", "apply-failed", new { error = ex.Message });
            return false;
        }
    }

    /// <summary>
    /// Devuelve el sufijo de nombre de archivo esperado para el asset de la plataforma actual.
    /// </summary>
    private static string? GetPlatformAssetSuffix()
    {
        if (OperatingSystem.IsWindows()) return ".exe";
        if (OperatingSystem.IsLinux()) return ".tar.gz";
        if (OperatingSystem.IsMacOS()) return ".zip";
        return null;
    }
}