using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDownImportBiblioteca;

/// <summary>
/// Limpia la carpeta de origen antes de importar:
/// 1. Normaliza nombres de archivo (autor - titulo.ext).
/// 2. Elimina archivos cuyo autor no está en autores_gutenberg.txt.
/// </summary>
internal static class SourceFolderCleaner
{
    private static readonly string[] s_bookExtensions =
        { ".epub", ".mobi", ".pdf", ".azw3", ".fb2", ".djvu", ".txt" };
    private const string GutenbergAuthorsFileName = "autores_gutenberg.txt";

    // Ruta donde buscar autores_gutenberg.txt.
    // Prioridad: override explícito -> env var -> rutas cercanas al origen -> árbol del exe.
    private static string? FindGutenbergListPath(string? pathOverride, string? srcDir)
    {
        if (!string.IsNullOrWhiteSpace(pathOverride) && File.Exists(pathOverride))
            return pathOverride;

        var envOverride = Environment.GetEnvironmentVariable("SLSDOWN_GUTENBERG_AUTHORS_PATH");
        if (!string.IsNullOrWhiteSpace(envOverride) && File.Exists(envOverride))
            return envOverride;

        if (!string.IsNullOrWhiteSpace(srcDir))
        {
            var srcCandidate = Path.Combine(srcDir, GutenbergAuthorsFileName);
            if (File.Exists(srcCandidate))
                return srcCandidate;

            var parent = Directory.GetParent(srcDir)?.FullName;
            if (!string.IsNullOrWhiteSpace(parent))
            {
                var parentCandidate = Path.Combine(parent, GutenbergAuthorsFileName);
                if (File.Exists(parentCandidate))
                    return parentCandidate;
            }
        }

        var fixedWorkspaceCandidate = Path.Combine("c:\\p2p", GutenbergAuthorsFileName);
        if (File.Exists(fixedWorkspaceCandidate))
            return fixedWorkspaceCandidate;

        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            var candidate = Path.Combine(dir, GutenbergAuthorsFileName);
            if (File.Exists(candidate))
                return candidate;
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent == null) break;
            dir = parent;
        }
        return null;
    }

    /// <summary>
    /// Carga el catálogo Gutenberg como un índice de tokens normalizados.
    /// Clave: token (palabra significativa). Valor: no importa; usamos HashSet.
    /// </summary>
    internal static HashSet<string> LoadGutenbergTokens(string? pathOverride = null, string? srcDir = null)
    {
        var path = FindGutenbergListPath(pathOverride, srcDir);
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (path == null) return tokens;

        foreach (var raw in File.ReadLines(path, Encoding.UTF8))
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;
            // Quitar anotaciones entre corchetes y después de ';'
            var semicolonIdx = line.IndexOf(';');
            if (semicolonIdx >= 0) line = line[..semicolonIdx];
            var bracketIdx = line.IndexOf('[');
            if (bracketIdx >= 0) line = line[..bracketIdx];
            line = line.Trim();
            if (line.Length < 2) continue;

            var normalized = NormalizeForLookup(line);
            foreach (var tok in TokensOf(normalized))
                tokens.Add(tok);
        }
        return tokens;
    }

    /// <summary>
    /// Limpia la carpeta de origen: normaliza nombres y elimina archivos sin autor en el catálogo.
    /// </summary>
    internal static async Task<CleanResult> CleanAsync(
        string srcDir,
        HashSet<string> gutenbergTokens,
        IProgress<string> log,
        bool dryRun,
        CancellationToken ct)
    {
        var result = new CleanResult();
        if (gutenbergTokens.Count == 0)
        {
            log.Report("⚠️ [Limpieza] No se encontró autores_gutenberg.txt; se omite la limpieza de origen.");
            return result;
        }

        return await Task.Run(() =>
        {
            List<string> files;
            try
            {
                files = EnumerateFilesRecursive(srcDir);
            }
            catch (Exception ex)
            {
                log.Report($"⚠️ [Limpieza] No se puede leer la carpeta de origen: {ex.Message}");
                return result;
            }

            log.Report($"🧹 [Limpieza] Iniciando limpieza de {files.Count} archivo(s) en origen…");

            foreach (var filePath in files)
            {
                ct.ThrowIfCancellationRequested();

                var ext = Path.GetExtension(filePath);
                if (!IsBookExtension(ext)) continue;

                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var parsed = TryResolveAuthorTitle(fileName, gutenbergTokens, out var author, out var title);

                if (string.IsNullOrWhiteSpace(author))
                {
                    log.Report($"  ⚠️ Sin autor legible, omitido: {Path.GetFileName(filePath)}");
                    result.Skipped++;
                    continue;
                }

                if (!StandaloneGutenbergPublicDomainPolicy.IsAuthorInCatalog(author, srcDir))
                {
                    if (parsed == ParsedAuthorResult.Ambiguous)
                    {
                        log.Report($"  ⚠️ Autor ambiguo (no se borra por seguridad): {Path.GetFileName(filePath)}");
                        result.Skipped++;
                        continue;
                    }

                    try
                    {
                        if (dryRun)
                        {
                            log.Report($"  🧪 DRY-RUN eliminaría (autor no en Gutenberg): {Path.GetFileName(filePath)}");
                        }
                        else
                        {
                            File.Delete(filePath);
                            log.Report($"  🗑️ Eliminado (autor no en Gutenberg): {Path.GetFileName(filePath)}");
                        }
                        result.Deleted++;
                    }
                    catch (Exception ex)
                    {
                        log.Report($"  ⚠️ No se pudo eliminar {Path.GetFileName(filePath)}: {ex.Message}");
                    }
                    continue;
                }

                var normalizedName = BuildNormalizedFileName(author, title, ext);
                if (!string.Equals(normalizedName, Path.GetFileName(filePath), StringComparison.OrdinalIgnoreCase))
                {
                    var fileDir = Path.GetDirectoryName(filePath) ?? srcDir;
                    var newPath = Path.Combine(fileDir, normalizedName);
                    try
                    {
                        if (!File.Exists(newPath) || string.Equals(newPath, filePath, StringComparison.OrdinalIgnoreCase))
                        {
                            if (dryRun)
                            {
                                log.Report($"  🧪 DRY-RUN renombraría: {Path.GetFileName(filePath)} → {normalizedName}");
                            }
                            else
                            {
                                File.Move(filePath, newPath);
                                log.Report($"  ✏️ Renombrado: {Path.GetFileName(filePath)} → {normalizedName}");
                            }
                            result.Renamed++;
                        }
                        else
                        {
                            log.Report($"  ⚠️ Colisión al renombrar {Path.GetFileName(filePath)}, se mantiene nombre original.");
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Report($"  ⚠️ No se pudo renombrar {Path.GetFileName(filePath)}: {ex.Message}");
                    }
                }
            }

            log.Report($"🧹 [Limpieza] Completa: {result.Renamed} renombrado(s), {result.Deleted} eliminado(s), {result.Skipped} omitido(s).");
            return result;
        }, ct);
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private enum ParsedAuthorResult
    {
        Resolved,
        Ambiguous,
        Missing
    }

    private static List<string> EnumerateFilesRecursive(string srcDir)
    {
        var files = new List<string>(4096);
        var stack = new Stack<string>();
        stack.Push(srcDir);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            try
            {
                foreach (var sub in Directory.EnumerateDirectories(dir))
                    stack.Push(sub);
            }
            catch
            {
            }

            try
            {
                files.AddRange(Directory.EnumerateFiles(dir));
            }
            catch
            {
            }
        }

        return files;
    }

    private static bool IsBookExtension(string ext)
    {
        foreach (var e in s_bookExtensions)
            if (string.Equals(e, ext, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static ParsedAuthorResult TryResolveAuthorTitle(
        string fileNameNoExt,
        HashSet<string> catalogTokens,
        out string author,
        out string title)
    {
        // Patrón "Título by Autor"
        var byIdx = fileNameNoExt.LastIndexOf(" by ", StringComparison.OrdinalIgnoreCase);
        if (byIdx > 0 && byIdx + 4 < fileNameNoExt.Length)
        {
            title = fileNameNoExt[..byIdx].Trim();
            author = fileNameNoExt[(byIdx + 4)..].Trim();
            return string.IsNullOrWhiteSpace(author) ? ParsedAuthorResult.Missing : ParsedAuthorResult.Resolved;
        }

        var segments = SplitNameSegments(fileNameNoExt);
        if (segments.Count >= 2)
        {
            if (segments.Count == 2)
                return ResolveTwoSegmentName(segments[0], segments[1], catalogTokens, out author, out title);

            var first = segments[0];
            var second = segments[1];
            var firstScore = CountCatalogTokenMatches(first, catalogTokens);
            var secondScore = CountCatalogTokenMatches(second, catalogTokens);

            if (secondScore > firstScore || (!LooksLikeAuthorPhrase(first) && LooksLikeAuthorPhrase(second)))
            {
                author = second;
                title = first;
                return ParsedAuthorResult.Resolved;
            }

            if (firstScore > secondScore || (LooksLikeAuthorPhrase(first) && !LooksLikeAuthorPhrase(second)))
            {
                author = first;
                title = second;
                return ParsedAuthorResult.Resolved;
            }

            author = second;
            title = first;
            return ParsedAuthorResult.Ambiguous;
        }

        author = string.Empty;
        title = fileNameNoExt.Trim();
        return ParsedAuthorResult.Missing;
    }

    private static ParsedAuthorResult ResolveTwoSegmentName(
        string left,
        string right,
        HashSet<string> catalogTokens,
        out string author,
        out string title)
    {
        var leftScore = CountCatalogTokenMatches(left, catalogTokens);
        var rightScore = CountCatalogTokenMatches(right, catalogTokens);

        if (leftScore > rightScore)
        {
            author = left;
            title = right;
            return ParsedAuthorResult.Resolved;
        }

        if (rightScore > leftScore)
        {
            author = right;
            title = left;
            return ParsedAuthorResult.Resolved;
        }

        var leftLooksAuthor = LooksLikeAuthorPhrase(left);
        var rightLooksAuthor = LooksLikeAuthorPhrase(right);

        if (leftLooksAuthor && !rightLooksAuthor)
        {
            author = left;
            title = right;
            return ParsedAuthorResult.Resolved;
        }

        if (rightLooksAuthor && !leftLooksAuthor)
        {
            author = right;
            title = right;
            return ParsedAuthorResult.Resolved;
        }

        author = left;
        title = right;
        return ParsedAuthorResult.Ambiguous;
    }

    private static List<string> SplitNameSegments(string fileNameNoExt)
    {
        var rawParts = fileNameNoExt.Split(new[] { " - ", " – ", "_" }, StringSplitOptions.RemoveEmptyEntries);
        var segments = new List<string>(rawParts.Length);
        foreach (var part in rawParts)
        {
            var cleaned = part.Trim().Trim('"', '\'', '“', '”');
            if (!string.IsNullOrWhiteSpace(cleaned))
                segments.Add(cleaned);
        }

        while (segments.Count > 2 && IsIgnorableTrailingMetadata(segments[^1]))
            segments.RemoveAt(segments.Count - 1);

        return segments;
    }

    private static bool IsIgnorableTrailingMetadata(string value)
    {
        var normalized = NormalizeForLookup(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return true;

        if (IsAllDigits(normalized.Replace(" ", string.Empty, StringComparison.Ordinal)))
            return true;

        return normalized is "spa" or "esp" or "es" or "eng" or "en" or "fre" or "fra" or "fr"
            or "ger" or "deu" or "de" or "ita" or "it" or "por" or "pt" or "rus" or "ru";
    }

    private static int CountCatalogTokenMatches(string value, HashSet<string> catalogTokens)
    {
        var normalized = NormalizeForLookup(value);
        int matches = 0;
        foreach (var tok in TokensOf(normalized))
        {
            if (catalogTokens.Contains(tok))
                matches++;
        }
        return matches;
    }

    private static bool LooksLikeAuthorPhrase(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var normalized = NormalizeForLookup(value);
        int words = 0;
        foreach (var tok in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (tok.Length >= 2)
                words++;
        }

        if (words >= 2)
            return true;

        return value.Contains(',', StringComparison.Ordinal);
    }

    private static string BuildNormalizedFileName(string author, string title, string ext)
    {
        var safeAuthor = SanitizeForFileName(string.IsNullOrWhiteSpace(author) ? "_unknown" : author);
        var safeTitle = SanitizeForFileName(string.IsNullOrWhiteSpace(title) ? "_unknown" : title);
        return $"{safeAuthor} - {safeTitle}{ext}";
    }

    private static string SanitizeForFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "_";
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(Math.Min(name.Length, 100));
        foreach (var c in name.AsSpan(0, Math.Min(name.Length, 100)))
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        var result = sb.ToString().TrimEnd();
        return result.Length > 0 ? result : "_";
    }

    /// <summary>Elimina diacríticos y convierte a minúsculas sin puntuación.</summary>
    private static string NormalizeForLookup(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var nfd = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(nfd.Length);
        bool prevSpace = false;
        foreach (var c in nfd)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            var lower = char.ToLowerInvariant(c);
            if (char.IsLetterOrDigit(lower))
            {
                sb.Append(lower);
                prevSpace = false;
            }
            else if (!prevSpace)
            {
                sb.Append(' ');
                prevSpace = true;
            }
        }
        return sb.ToString().Trim();
    }

    /// <summary>Devuelve tokens de longitud ≥ 4, no puramente numéricos, de una cadena ya normalizada.</summary>
    private static IEnumerable<string> TokensOf(string normalized)
    {
        if (string.IsNullOrEmpty(normalized)) yield break;
        foreach (var tok in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (tok.Length >= 4 && !IsAllDigits(tok)) yield return tok;
        }
    }

    private static bool IsAllDigits(string s)
    {
        foreach (var c in s)
            if (!char.IsDigit(c)) return false;
        return true;
    }

    internal sealed class CleanResult
    {
        public int Renamed { get; set; }
        public int Deleted { get; set; }
        public int Skipped { get; set; }
    }
}
