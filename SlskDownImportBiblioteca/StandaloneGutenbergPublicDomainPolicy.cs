using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualBasic.FileIO;

namespace SlskDownImportBiblioteca;

internal static class StandaloneGutenbergPublicDomainPolicy
{
    internal readonly record struct CatalogSnapshot(string? SourcePath, int AuthorCount, DateTime LoadedUtc);
    internal readonly record struct PolicyStatsSnapshot(
        int Evaluated,
        int Accepted,
        int Rejected,
        int RejectedNonLiterary,
        int RejectedNoAuthor,
        int RejectedNoCatalog,
        int RejectedAuthorNotInCatalog);

    private static readonly string[] s_nonLiteraryTokens =
    {
        "magazine", "journal", "newspaper", "periodical", "music", "mp3", "flac",
        "bootleg", "audiobook", "podcast", "video", "film", "movie", "manual",
        "guide", "catalog", "map", "atlas", "concert", "festival"
    };

    private static readonly object s_catalogLock = new();
    private static readonly TimeSpan s_catalogReloadInterval = TimeSpan.FromMinutes(10);
    private static DateTime s_catalogLastLoadUtc = DateTime.MinValue;
    private static string? s_catalogSourcePath;
    private static HashSet<string> s_catalogAuthors = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, List<string>> s_catalogTokenIndex = new(StringComparer.OrdinalIgnoreCase);
    private static int s_evaluated;
    private static int s_accepted;
    private static int s_rejected;
    private static int s_rejectedNonLiterary;
    private static int s_rejectedNoAuthor;
    private static int s_rejectedNoCatalog;
    private static int s_rejectedAuthorNotInCatalog;

    public static bool ShouldReject(string destFileName, string? sourcePathOrEntry, out string reason)
    {
        reason = string.Empty;
        Interlocked.Increment(ref s_evaluated);

        var stem = Path.GetFileNameWithoutExtension(destFileName);
        foreach (var token in s_nonLiteraryTokens)
        {
            if (stem.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref s_rejected);
                Interlocked.Increment(ref s_rejectedNonLiterary);
                reason = $"❌ Fuera de alcance: token no literario '{token}'";
                return true;
            }
        }

        var author = TryExtractAuthor(destFileName, sourcePathOrEntry);
        if (string.IsNullOrWhiteSpace(author))
        {
            Interlocked.Increment(ref s_rejected);
            Interlocked.Increment(ref s_rejectedNoAuthor);
            reason = "❌ Autor no identificable";
            return true;
        }

        EnsureCatalogLoaded(sourcePathOrEntry);
        if (s_catalogAuthors.Count == 0)
        {
            Interlocked.Increment(ref s_rejected);
            Interlocked.Increment(ref s_rejectedNoCatalog);
            reason = "❌ Catálogo Gutenberg no disponible";
            return true;
        }

        if (AuthorInCatalog(author))
        {
            Interlocked.Increment(ref s_accepted);
            reason = $"Dominio público: autor '{author}' presente en catálogo Gutenberg";
            return false;
        }

        Interlocked.Increment(ref s_rejected);
        Interlocked.Increment(ref s_rejectedAuthorNotInCatalog);
        reason = $"❌ Autor '{author}' no presente en catálogo Gutenberg";
        return true;
    }

    public static void ResetPolicyStats()
    {
        Interlocked.Exchange(ref s_evaluated, 0);
        Interlocked.Exchange(ref s_accepted, 0);
        Interlocked.Exchange(ref s_rejected, 0);
        Interlocked.Exchange(ref s_rejectedNonLiterary, 0);
        Interlocked.Exchange(ref s_rejectedNoAuthor, 0);
        Interlocked.Exchange(ref s_rejectedNoCatalog, 0);
        Interlocked.Exchange(ref s_rejectedAuthorNotInCatalog, 0);
    }

    public static PolicyStatsSnapshot GetPolicyStatsSnapshot() =>
        new(
            Interlocked.CompareExchange(ref s_evaluated, 0, 0),
            Interlocked.CompareExchange(ref s_accepted, 0, 0),
            Interlocked.CompareExchange(ref s_rejected, 0, 0),
            Interlocked.CompareExchange(ref s_rejectedNonLiterary, 0, 0),
            Interlocked.CompareExchange(ref s_rejectedNoAuthor, 0, 0),
            Interlocked.CompareExchange(ref s_rejectedNoCatalog, 0, 0),
            Interlocked.CompareExchange(ref s_rejectedAuthorNotInCatalog, 0, 0));

    public static bool IsAuthorInCatalog(string author, string? sourcePathOrEntry)
    {
        if (string.IsNullOrWhiteSpace(author))
            return false;

        EnsureCatalogLoaded(sourcePathOrEntry);
        if (s_catalogAuthors.Count == 0)
            return false;

        return AuthorInCatalog(author);
    }

    public static CatalogSnapshot GetCatalogSnapshot(string? sourcePathOrEntry)
    {
        EnsureCatalogLoaded(sourcePathOrEntry);
        return new CatalogSnapshot(s_catalogSourcePath, s_catalogAuthors.Count, s_catalogLastLoadUtc);
    }

    private static void EnsureCatalogLoaded(string? sourcePathOrEntry)
    {
        var now = DateTime.UtcNow;
        var sourceDir = GetCatalogSearchRoot(sourcePathOrEntry);
        var currentPath = FindCatalogPath(sourceDir);

        if ((now - s_catalogLastLoadUtc) < s_catalogReloadInterval &&
            string.Equals(currentPath, s_catalogSourcePath, StringComparison.OrdinalIgnoreCase) &&
            s_catalogAuthors.Count > 0)
        {
            return;
        }

        lock (s_catalogLock)
        {
            currentPath = FindCatalogPath(sourceDir);
            if ((DateTime.UtcNow - s_catalogLastLoadUtc) < s_catalogReloadInterval &&
                string.Equals(currentPath, s_catalogSourcePath, StringComparison.OrdinalIgnoreCase) &&
                s_catalogAuthors.Count > 0)
            {
                return;
            }

            var authors = currentPath is null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : LoadCatalogAuthors(currentPath);
            s_catalogAuthors = authors;
            s_catalogTokenIndex = BuildTokenIndex(authors);
            s_catalogSourcePath = currentPath;
            s_catalogLastLoadUtc = DateTime.UtcNow;
        }
    }

    private static string? GetCatalogSearchRoot(string? sourcePathOrEntry)
    {
        if (string.IsNullOrWhiteSpace(sourcePathOrEntry))
            return null;

        try
        {
            if (File.Exists(sourcePathOrEntry))
                return Path.GetDirectoryName(sourcePathOrEntry);
            if (Directory.Exists(sourcePathOrEntry))
                return sourcePathOrEntry;
        }
        catch
        {
        }

        return null;
    }

    private static string? FindCatalogPath(string? sourceDir)
    {
        var overridePath = Environment.GetEnvironmentVariable("SLSDOWN_GUTENBERG_AUTHORS_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
            return overridePath;

        foreach (var candidate in EnumerateCatalogCandidates(sourceDir))
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCatalogCandidates(string? sourceDir)
    {
        if (!string.IsNullOrWhiteSpace(sourceDir))
        {
            yield return Path.Combine(sourceDir, "autores_gutenberg.txt");

            var parent = Directory.GetParent(sourceDir)?.FullName;
            if (!string.IsNullOrWhiteSpace(parent))
            {
                yield return Path.Combine(parent, "autores_gutenberg.txt");
                yield return Path.Combine(parent, "gutenberg_metadata.csv");
            }

            yield return Path.Combine(sourceDir, "gutenberg_metadata.csv");
        }

        yield return Path.Combine("c:\\p2p", "autores_gutenberg.txt");
        yield return Path.Combine("c:\\p2p", "gutenberg_metadata.csv");
        yield return Path.Combine(Environment.CurrentDirectory, "autores_gutenberg.txt");
        yield return Path.Combine(Environment.CurrentDirectory, "gutenberg_metadata.csv");

        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            yield return Path.Combine(dir, "autores_gutenberg.txt");
            yield return Path.Combine(dir, "gutenberg_metadata.csv");
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent == null)
                yield break;
            dir = parent;
        }
    }

    private static HashSet<string> LoadCatalogAuthors(string path)
    {
        var authors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ext = Path.GetExtension(path);

        if (string.Equals(ext, ".csv", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var author in LoadAuthorsFromCsv(path))
                authors.Add(author);
            return authors;
        }

        foreach (var raw in File.ReadLines(path, Encoding.UTF8))
        {
            var normalized = NormalizeAuthor(raw);
            if (!string.IsNullOrWhiteSpace(normalized))
                authors.Add(normalized);
        }

        return authors;
    }

    private static IEnumerable<string> LoadAuthorsFromCsv(string csvPath)
    {
        using var parser = new TextFieldParser(csvPath);
        parser.TextFieldType = FieldType.Delimited;
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;
        parser.TrimWhiteSpace = true;

        int authorsCol = 7;
        var header = parser.ReadFields();
        if (header != null)
        {
            for (int i = 0; i < header.Length; i++)
            {
                if (string.Equals(header[i]?.Trim(), "Authors", StringComparison.OrdinalIgnoreCase))
                {
                    authorsCol = i;
                    break;
                }
            }
        }

        while (!parser.EndOfData)
        {
            string[]? columns;
            try
            {
                columns = parser.ReadFields();
            }
            catch (MalformedLineException)
            {
                continue;
            }

            if (columns == null || columns.Length <= authorsCol)
                continue;

            var authorsCell = columns[authorsCol];
            if (string.IsNullOrWhiteSpace(authorsCell))
                continue;

            foreach (var rawAuthor in authorsCell.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var normalized = NormalizeAuthor(rawAuthor);
                if (!string.IsNullOrWhiteSpace(normalized))
                    yield return normalized;
            }
        }
    }

    private static Dictionary<string, List<string>> BuildTokenIndex(HashSet<string> authors)
    {
        var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var author in authors)
        {
            foreach (var token in GetLookupTokens(author))
            {
                if (!index.TryGetValue(token, out var list))
                {
                    list = new List<string>();
                    index[token] = list;
                }

                list.Add(author);
            }
        }

        return index;
    }

    private static bool AuthorInCatalog(string author)
    {
        var normalized = NormalizeAuthor(author);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        if (s_catalogAuthors.Contains(normalized))
            return true;

        var tokens = GetLookupTokens(normalized);
        if (tokens.Count == 0)
            return false;

        var pivot = tokens.OrderByDescending(static t => t.Length).First();
        if (!s_catalogTokenIndex.TryGetValue(pivot, out var candidates))
            return false;

        for (int i = 0; i < candidates.Count; i++)
        {
            if (CandidateMatches(normalized, tokens, candidates[i]))
                return true;
        }

        return false;
    }

    private static bool CandidateMatches(string normalizedAuthor, List<string> tokens, string candidate)
    {
        if (string.Equals(normalizedAuthor, candidate, StringComparison.OrdinalIgnoreCase))
            return true;

        int hits = 0;
        for (int i = 0; i < tokens.Count; i++)
        {
            if (candidate.Contains(tokens[i], StringComparison.OrdinalIgnoreCase))
                hits++;
        }

        return hits >= 2;
    }

    private static string? TryExtractAuthor(string fileName, string? fullPath)
    {
        var fn = Path.GetFileNameWithoutExtension(fileName);
        var segments = SplitNameSegments(fn);
        if (segments.Count >= 2)
        {
            var first = NormalizeAuthor(segments[0]);
            var second = NormalizeAuthor(segments[1]);

            if (LooksLikeAuthorCandidate(second) && !LooksLikeAuthorCandidate(first))
                return segments[1];

            if (LooksLikeAuthorCandidate(first) && !LooksLikeAuthorCandidate(second))
                return segments[0];

            return segments[1];
        }

        if (!string.IsNullOrEmpty(fullPath))
        {
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
            {
                var parent = Path.GetFileName(dir);
                if (!string.IsNullOrWhiteSpace(parent) && parent.Length > 2)
                    return parent;
            }
        }

        var separators = new[] { " - ", "_", " – " };
        foreach (var sep in separators)
        {
            var idx = fn.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
                return fn[..idx].Trim();
        }

        return null;
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
        var normalized = NormalizeAuthor(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return true;

        var compact = normalized.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (compact.All(char.IsDigit))
            return true;

        return normalized is "spa" or "esp" or "es" or "eng" or "en" or "fre" or "fra" or "fr"
            or "ger" or "deu" or "de" or "ita" or "it" or "por" or "pt" or "rus" or "ru";
    }

    private static bool LooksLikeAuthorCandidate(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length >= 2)
            return true;

        return normalized.Contains(',', StringComparison.Ordinal);
    }

    private static string NormalizeAuthor(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var line = value.Trim().Trim('"');
        var semicolonIdx = line.IndexOf(';');
        if (semicolonIdx >= 0)
            line = line[..semicolonIdx];
        var bracketIdx = line.IndexOf('[');
        if (bracketIdx >= 0)
            line = line[..bracketIdx];

        var nfd = line.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(nfd.Length);
        bool prevSpace = false;
        foreach (var c in nfd)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            var lower = char.ToLowerInvariant(c);
            if (char.IsLetter(lower))
            {
                sb.Append(lower);
                prevSpace = false;
                continue;
            }

            if (!prevSpace)
            {
                sb.Append(' ');
                prevSpace = true;
            }
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static List<string> GetLookupTokens(string normalizedAuthor)
    {
        var tokens = new List<string>();
        foreach (var token in normalizedAuthor.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length >= 4)
                tokens.Add(token);
        }

        return tokens;
    }
}