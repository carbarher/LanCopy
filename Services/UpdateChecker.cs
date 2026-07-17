using System.Diagnostics;
using System.Net.Http;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LanCopy.Services;

// Comprobador de actualizaciones: consulta la API publica de GitHub Releases y compara la
// version mas reciente con la actual. Sin dependencias externas; degrada de forma silenciosa
// si no hay red. No descarga ni instala nada (eso lo decide el usuario desde la UI).
public static class UpdateChecker
{
    public sealed record UpdateInfo(bool Available, string CurrentVersion, string LatestVersion, string Url, string Notes);
    public sealed record ReleaseAssetManifest(string Sha256, string? Signature);

    // Repositorio de releases (owner/repo). Configurable si el proyecto se aloja en otro sitio.
    public static string Owner { get; set; } = "carbarher";
    public static string Repo { get; set; } = "LanCopy";

    // Pinned ECDSA P-256 public key for signed release manifests.
    // When set, missing, malformed, mismatched, or untrusted signatures fail closed.
    public static string? ReleaseManifestPublicKeyPem { get; set; } =
        """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAELP/1JvtpIcdyFwucptVjD9YSNPpE
        A6rMQS8865X46TMl1A8IhMCnGCX+mYqc8mEDMJU4akt+bicphRyz2CEFXg==
        -----END PUBLIC KEY-----
        """;

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

            var platform = GetCurrentPlatform();
            if (platform is null) return null;
            var expectedName = GetPlatformAssetName(platform, RuntimeInformation.ProcessArchitecture);
            if (expectedName is null) return null;

            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? (n.GetString() ?? "") : "";
                if (string.Equals(name, expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    var downloadUrl = asset.TryGetProperty("browser_download_url", out var d)
                        ? d.GetString()
                        : null;
                    Log.Info("update", "download-url-resolved", new { name, downloadUrl });
                    return downloadUrl;
                }
            }

            Log.Debug("update", "no-matching-asset", new { expectedName });
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
            var checksumUrl = downloadUrl + ".sha256";
            var manifest = await DownloadReleaseManifestAsync(checksumUrl, ct);
            if (manifest is null)
            {
                Log.Debug("update", "checksum-missing", new { checksumUrl });
                DeleteIfExists(destPath);
                return null;
            }

            if (!VerifyReleaseManifestSignature(manifest, fileName, ReleaseManifestPublicKeyPem))
            {
                Log.Debug("update", "signature-invalid", new { checksumUrl, fileName });
                DeleteIfExists(destPath);
                return null;
            }

            var expectedHash = manifest.Sha256;

            var actualHash = await ComputeSha256HexAsync(destPath, ct);
            if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
            {
                Log.Debug("update", "checksum-mismatch", new { destPath, expectedHash, actualHash });
                DeleteIfExists(destPath);
                return null;
            }

            await File.WriteAllTextAsync(destPath + ".sha256", SerializeReleaseManifestSidecar(manifest), ct);
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
        if (string.IsNullOrEmpty(currentExe) || !File.Exists(downloadedPath))
        {
            Log.Debug("update", "apply-failed", new { error = "Executable or downloaded update not found", currentExe, downloadedPath });
            return false;
        }

        var checksumPath = downloadedPath + ".sha256";
        if (!File.Exists(checksumPath))
        {
            Log.Debug("update", "apply-failed", new { error = "Checksum sidecar missing", checksumPath });
            return false;
        }

        try
        {
            var manifest = ParseReleaseAssetManifest(File.ReadAllText(checksumPath));
            if (manifest is null ||
                !VerifyReleaseManifestSignature(manifest, Path.GetFileName(downloadedPath), ReleaseManifestPublicKeyPem) ||
                !string.Equals(manifest.Sha256, ComputeSha256Hex(downloadedPath), StringComparison.OrdinalIgnoreCase))
            {
                Log.Debug("update", "apply-failed", new { error = "Release verification failed", downloadedPath });
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.Debug("update", "apply-failed", new { error = ex.Message });
            return false;
        }

        string? oldPath = null;
        string? extractionDir = null;
        try
        {
            var replacementPath = downloadedPath;
            UnixFileMode? unixMode = null;
            if (!OperatingSystem.IsWindows())
            {
                unixMode = File.GetUnixFileMode(currentExe);
                extractionDir = Path.Combine(Path.GetTempPath(), "LanCopy-Update", "extract-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(extractionDir);
                if (OperatingSystem.IsLinux() && downloadedPath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                    TarFile.ExtractToDirectory(downloadedPath, extractionDir, overwriteFiles: true);
                else if (OperatingSystem.IsMacOS() && downloadedPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    ZipFile.ExtractToDirectory(downloadedPath, extractionDir, overwriteFiles: true);
                else
                    throw new InvalidDataException("Unsupported update package for this platform");

                replacementPath = Directory.EnumerateFiles(extractionDir, "LanCopy", SearchOption.AllDirectories)
                    .FirstOrDefault() ?? throw new InvalidDataException("LanCopy executable is missing from update package");
            }

            oldPath = currentExe + ".old";
            if (File.Exists(oldPath)) File.Delete(oldPath);
            File.Move(currentExe, oldPath);
            File.Copy(replacementPath, currentExe, overwrite: true);
            if (!OperatingSystem.IsWindows() && unixMode.HasValue) File.SetUnixFileMode(currentExe, unixMode.Value);

            if (extractionDir != null)
            {
                Directory.Delete(extractionDir, recursive: true);
                extractionDir = null;
            }

            Log.Info("update", "apply-success", new { currentExe, oldPath });
            Process.Start(new ProcessStartInfo(currentExe) { UseShellExecute = true })?.Dispose();
            Environment.Exit(0);
            return true;
        }
        catch (Exception ex)
        {
            if (oldPath != null && File.Exists(oldPath))
            {
                try
                {
                    if (File.Exists(currentExe)) File.Delete(currentExe);
                    File.Move(oldPath, currentExe);
                }
                catch (Exception restoreEx)
                {
                    Log.Debug("update", "restore-old-failed", new { error = restoreEx.Message });
                }
            }
            Log.Debug("update", "apply-failed", new { error = ex.Message });
            return false;
        }
        finally
        {
            if (extractionDir != null)
                try { Directory.Delete(extractionDir, recursive: true); } catch { }
        }
    }
    public static string ComputeSha256Hex(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();
    }

    public static async Task<string> ComputeSha256HexAsync(string filePath, CancellationToken ct = default)
    {
        await using var fs = File.OpenRead(filePath);
        return Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();
    }

    public static string? ParseSha256Manifest(string manifestText)
        => ParseReleaseAssetManifest(manifestText)?.Sha256;

    public static ReleaseAssetManifest? ParseReleaseAssetManifest(string manifestText)
    {
        if (string.IsNullOrWhiteSpace(manifestText))
            return null;

        var trimmed = manifestText.Trim();
        if (trimmed.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;
                var sha256 = root.TryGetProperty("sha256", out var hashEl) ? hashEl.GetString() : null;
                var normalized = NormalizeSha256(sha256 ?? "");
                if (!IsSha256Hex(normalized))
                    return null;

                var signature = root.TryGetProperty("signature", out var sigEl) ? sigEl.GetString() : null;
                return new ReleaseAssetManifest(normalized, string.IsNullOrWhiteSpace(signature) ? null : signature.Trim());
            }
            catch (JsonException)
            {
                return null;
            }
        }

        var first = manifestText.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(first))
            return null;

        var legacyHash = NormalizeSha256(first);
        return IsSha256Hex(legacyHash) ? new ReleaseAssetManifest(legacyHash, null) : null;
    }

    public static bool VerifyReleaseManifestSignature(
        ReleaseAssetManifest manifest,
        string assetName,
        string? publicKeyPem = null)
    {
        if (string.IsNullOrWhiteSpace(publicKeyPem))
            return true;

        if (string.IsNullOrWhiteSpace(manifest.Signature) || string.IsNullOrWhiteSpace(assetName))
            return false;

        try
        {
            var signature = Convert.FromBase64String(manifest.Signature);
            var payload = Encoding.UTF8.GetBytes($"{assetName}\n{manifest.Sha256}\n");
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(publicKeyPem);
            return ecdsa.VerifyData(payload, signature, HashAlgorithmName.SHA256);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<ReleaseAssetManifest?> DownloadReleaseManifestAsync(string checksumUrl, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("LanCopy-UpdateChecker");
        using var resp = await http.GetAsync(checksumUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
            return null;
        var manifest = await resp.Content.ReadAsStringAsync(ct);
        return ParseReleaseAssetManifest(manifest);
    }

    private static string SerializeReleaseManifestSidecar(ReleaseAssetManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Signature))
            return manifest.Sha256;

        return JsonSerializer.Serialize(new { sha256 = manifest.Sha256, signature = manifest.Signature });
    }
    private static string NormalizeSha256(string value)
        => value.Trim().Replace(" ", "").ToLowerInvariant();

    private static bool IsSha256Hex(string value)
        => value.Length == 64 && value.All(Uri.IsHexDigit);

    private static void DeleteIfExists(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
        try { if (File.Exists(path + ".sha256")) File.Delete(path + ".sha256"); } catch { }
    }

    internal static string? GetPlatformAssetName(string platform, Architecture architecture)
    {
        var arch = architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null
        };
        if (arch is null) return null;
        return platform switch
        {
            "win" => $"LanCopy-win-{arch}.exe",
            "linux" => $"LanCopy-linux-{arch}.tar.gz",
            "osx" => $"LanCopy-osx-{arch}.zip",
            _ => null
        };
    }

    private static string? GetCurrentPlatform()
    {
        if (OperatingSystem.IsWindows()) return "win";
        if (OperatingSystem.IsLinux()) return "linux";
        if (OperatingSystem.IsMacOS()) return "osx";
        return null;
    }
}
