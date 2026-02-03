using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SlskDown
{
    public sealed class SearchFilterConfig
    {
        public long? MinSize { get; set; }
        public long? MaxSize { get; set; }
        public int? MinQuality { get; set; }
        public bool SpanishOnly { get; set; }
        public List<string>? Extensions { get; set; }
        public List<string>? RequiredWords { get; set; }
        public List<string>? ExcludedWords { get; set; }
    }

    public static class SearchFilterRules
    {
        private sealed class CacheEntry
        {
            public DateTime LastWriteTimeUtc { get; set; }
            public SearchFilterConfig Config { get; set; } = new SearchFilterConfig();
        }

        private static readonly object CacheLock = new object();
        private static readonly Dictionary<string, CacheEntry> Cache = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);

        public static SearchFilterConfig LoadFromDataDir(string dataDir)
        {
            if (string.IsNullOrWhiteSpace(dataDir))
            {
                return new SearchFilterConfig();
            }

            var path = Path.Combine(dataDir, "filters.rules");
            return Load(path);
        }

        public static SearchFilterConfig Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new SearchFilterConfig();
            }

            try
            {
                var lastWrite = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;

                lock (CacheLock)
                {
                    if (Cache.TryGetValue(path, out var entry) && entry.LastWriteTimeUtc == lastWrite)
                    {
                        return entry.Config;
                    }
                }

                var cfg = ParseFile(path);

                lock (CacheLock)
                {
                    Cache[path] = new CacheEntry { LastWriteTimeUtc = lastWrite, Config = cfg };
                }

                return cfg;
            }
            catch
            {
                return new SearchFilterConfig();
            }
        }

        public static List<SearchResultItem> ApplyPostFilters(List<SearchResultItem> results, SearchFilterConfig cfg)
        {
            if (results == null || results.Count == 0)
            {
                return results;
            }

            var required = cfg.RequiredWords;
            var excluded = cfg.ExcludedWords;
            if ((required == null || required.Count == 0) && (excluded == null || excluded.Count == 0))
            {
                return results;
            }

            for (var i = results.Count - 1; i >= 0; i--)
            {
                var item = results[i];
                var filename = item?.Filename;

                if (excluded != null && excluded.Count > 0 && ContainsAny(filename, excluded))
                {
                    results.RemoveAt(i);
                    continue;
                }

                if (required != null && required.Count > 0 && !ContainsAll(filename, required))
                {
                    results.RemoveAt(i);
                }
            }

            return results;
        }

        private static SearchFilterConfig ParseFile(string path)
        {
            var cfg = new SearchFilterConfig();
            if (!File.Exists(path))
            {
                return cfg;
            }

            var lines = File.ReadAllLines(path);
            foreach (var raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                var line = raw.Trim();
                if (line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal))
                {
                    continue;
                }

                var idx = line.IndexOf('=');
                if (idx <= 0)
                {
                    continue;
                }

                var key = line.Substring(0, idx).Trim().ToLowerInvariant();
                var value = line.Substring(idx + 1).Trim();

                if (key == "min_size" && long.TryParse(value, out var minSize))
                {
                    cfg.MinSize = minSize;
                    continue;
                }

                if (key == "max_size" && long.TryParse(value, out var maxSize))
                {
                    cfg.MaxSize = maxSize;
                    continue;
                }

                if (key == "min_quality" && int.TryParse(value, out var minQuality))
                {
                    cfg.MinQuality = minQuality;
                    continue;
                }

                if (key == "spanish_only" && bool.TryParse(value, out var spanishOnly))
                {
                    cfg.SpanishOnly = spanishOnly;
                    continue;
                }

                if (key == "extensions")
                {
                    var exts = SplitCsv(value)
                        .Select(NormalizeExtension)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    cfg.Extensions = exts.Count > 0 ? exts : null;
                    continue;
                }

                if (key == "required_words")
                {
                    var words = SplitCsv(value)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList();

                    cfg.RequiredWords = words.Count > 0 ? words : null;
                    continue;
                }

                if (key == "excluded_words")
                {
                    var words = SplitCsv(value)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList();

                    cfg.ExcludedWords = words.Count > 0 ? words : null;
                    continue;
                }
            }

            return cfg;
        }

        private static IEnumerable<string> SplitCsv(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            return value.Split(',')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x));
        }

        private static string NormalizeExtension(string ext)
        {
            if (string.IsNullOrWhiteSpace(ext))
            {
                return string.Empty;
            }

            ext = ext.Trim();
            if (!ext.StartsWith(".", StringComparison.Ordinal))
            {
                ext = "." + ext;
            }

            return ext;
        }

        private static bool ContainsAny(string? text, List<string> words)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (var w in words)
            {
                if (string.IsNullOrWhiteSpace(w))
                {
                    continue;
                }

                if (text.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsAll(string? text, List<string> words)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (var w in words)
            {
                if (string.IsNullOrWhiteSpace(w))
                {
                    continue;
                }

                if (text.IndexOf(w, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
