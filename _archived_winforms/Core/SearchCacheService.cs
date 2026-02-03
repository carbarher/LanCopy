using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using SlskDown.Models;
using SlskDown; // For RustCore (existing namespace)

namespace SlskDown.Core
{
    /// <summary>
    /// Encapsula la lógica de caché de búsquedas, incluyendo persistencia, normalización y mantenimiento de índices.
    /// </summary>
    public class SearchCacheService
    {
        private sealed class SearchCacheEntry
        {
            public string OriginalQuery { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
                = DateTime.MinValue;
            public DateTime LastAccessUtc { get; set; }
                = DateTime.MinValue;
            public int FilesCount { get; set; }
                = 0;
            public bool HasValidFiles { get; set; }
                = false;
            public string NormalizedKey { get; set; } = string.Empty;
            public string TokenSignature { get; set; } = string.Empty;
            public string ReducedSignature { get; set; } = string.Empty;
            public string[] Tokens { get; set; } = Array.Empty<string>();
            public long HitCount { get; set; }
                = 0;
            public List<SearchResultItem> Items { get; set; } = new();
        }

        private readonly object cacheLock = new();
        private readonly string cachePath;
        private readonly TimeSpan cacheTtl;
        private readonly int cacheMaxEntries;
        private readonly TimeSpan saveInterval;
        private readonly Func<DateTime> clock;
        private readonly Action<string>? logger;

        private Dictionary<string, SearchCacheEntry> cache = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, HashSet<string>> normalizedIndex = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, HashSet<string>> signatureIndex = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, HashSet<string>> reducedSignatureIndex = new(StringComparer.OrdinalIgnoreCase);

        private long cacheHits;
        private long cacheMisses;
        private long cacheEvictions;
        private DateTime lastCleanupUtc = DateTime.MinValue;
        private DateTime lastSaveUtc = DateTime.MinValue;
        private DateTime lastLogUtc = DateTime.MinValue;
        private int saveQueued;

        private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

        public SearchCacheService(
            string cachePath,
            Action<string>? logger = null,
            TimeSpan? ttl = null,
            int maxEntries = 500,
            TimeSpan? saveInterval = null,
            Func<DateTime>? clock = null)
        {
            if (string.IsNullOrWhiteSpace(cachePath))
            {
                throw new ArgumentException("Cache path must be provided", nameof(cachePath));
            }

            this.cachePath = cachePath;
            this.logger = logger;
            cacheTtl = ttl ?? TimeSpan.FromHours(24);
            cacheMaxEntries = maxEntries > 0 ? maxEntries : 500;
            this.saveInterval = saveInterval ?? TimeSpan.FromSeconds(20);
            this.clock = clock ?? (() => DateTime.UtcNow);
        }

        public bool TryGet(string query, out List<SearchResultItem> results)
        {
            results = new List<SearchResultItem>();

            if (string.IsNullOrWhiteSpace(query))
            {
                return false;
            }

            CleanupIfNeeded();

            var normalizedKey = NormalizeSearchQuery(query);
            if (normalizedKey.Length == 0)
            {
                return false;
            }

            lock (cacheLock)
            {
                if (cache.TryGetValue(normalizedKey, out var entry))
                {
                    if (clock() - entry.Timestamp <= cacheTtl)
                    {
                        entry.LastAccessUtc = clock();
                        entry.HitCount++;
                        results = CloneSearchResultItems(entry.Items);
                        Interlocked.Increment(ref cacheHits);
                        return true;
                    }

                    RemoveEntryFromIndices(normalizedKey, entry);
                    cache.Remove(normalizedKey);
                    Interlocked.Increment(ref cacheEvictions);
                }
            }

            Interlocked.Increment(ref cacheMisses);
            return false;
        }

        public void Store(string query, List<SearchResultItem> results)
        {
            if (string.IsNullOrWhiteSpace(query) || results == null)
            {
                return;
            }

            CleanupIfNeeded();

            var normalizedKey = NormalizeSearchQuery(query);
            if (normalizedKey.Length == 0)
            {
                return;
            }

            var (tokens, tokenSignature, reducedSignature) = ComputeQuerySignatures(normalizedKey);
            var snapshot = CloneSearchResultItems(results);
            var now = clock();

            lock (cacheLock)
            {
                if (cache.TryGetValue(normalizedKey, out var existingEntry))
                {
                    RemoveEntryFromIndices(normalizedKey, existingEntry);
                }

                cache[normalizedKey] = new SearchCacheEntry
                {
                    OriginalQuery = query,
                    Timestamp = now,
                    LastAccessUtc = now,
                    FilesCount = snapshot.Count,
                    HasValidFiles = snapshot.Count > 0,
                    NormalizedKey = normalizedKey,
                    TokenSignature = tokenSignature,
                    ReducedSignature = reducedSignature,
                    Tokens = tokens,
                    Items = snapshot,
                    HitCount = 0
                };

                UpdateIndices(normalizedKey, tokenSignature, reducedSignature);

                if (cache.Count > cacheMaxEntries)
                {
                    var oldest = cache
                        .OrderBy(kvp => kvp.Value.LastAccessUtc)
                        .FirstOrDefault();

                    if (!string.IsNullOrEmpty(oldest.Key))
                    {
                        RemoveEntryFromIndices(oldest.Key, oldest.Value);
                        cache.Remove(oldest.Key);
                        Interlocked.Increment(ref cacheEvictions);
                    }
                }
            }

            EnqueueSave();
        }

        public void Load()
        {
            try
            {
                var directory = Path.GetDirectoryName(cachePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (!File.Exists(cachePath))
                {
                    return;
                }

                var json = File.ReadAllText(cachePath, Encoding.UTF8);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var deserialized = JsonSerializer.Deserialize<Dictionary<string, SearchCacheEntry>>(json, options)
                    ?? new Dictionary<string, SearchCacheEntry>(StringComparer.OrdinalIgnoreCase);

                var now = clock();
                var loadedCache = new Dictionary<string, SearchCacheEntry>(StringComparer.OrdinalIgnoreCase);
                var loadedNormalizedIndex = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                var loadedSignatureIndex = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                var loadedReducedIndex = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

                foreach (var kvp in deserialized)
                {
                    var entry = kvp.Value ?? new SearchCacheEntry();
                    var normalizedKey = NormalizeSearchQuery(entry.OriginalQuery ?? kvp.Key);

                    if (normalizedKey.Length == 0)
                    {
                        continue;
                    }

                    if (entry.Timestamp == default)
                    {
                        entry.Timestamp = now;
                    }

                    if (entry.LastAccessUtc == default)
                    {
                        entry.LastAccessUtc = entry.Timestamp;
                    }

                    if (now - entry.Timestamp > cacheTtl)
                    {
                        continue;
                    }

                    entry.Items ??= new List<SearchResultItem>();

                    var (tokens, tokenSignature, reducedSignature) = ComputeQuerySignatures(normalizedKey);
                    entry.NormalizedKey = normalizedKey;
                    entry.TokenSignature = tokenSignature;
                    entry.ReducedSignature = reducedSignature;
                    entry.Tokens = tokens;

                    loadedCache[normalizedKey] = entry;
                    AddToIndex(loadedNormalizedIndex, normalizedKey, normalizedKey);

                    if (!string.IsNullOrWhiteSpace(tokenSignature))
                    {
                        AddToIndex(loadedSignatureIndex, tokenSignature, normalizedKey);
                    }

                    if (!string.IsNullOrWhiteSpace(reducedSignature))
                    {
                        AddToIndex(loadedReducedIndex, reducedSignature, normalizedKey);
                    }
                }

                lock (cacheLock)
                {
                    cache = loadedCache;
                    normalizedIndex = loadedNormalizedIndex;
                    signatureIndex = loadedSignatureIndex;
                    reducedSignatureIndex = loadedReducedIndex;
                    cacheHits = 0;
                    cacheMisses = 0;
                    cacheEvictions = 0;
                }

                lastCleanupUtc = now;
                lastSaveUtc = now;
                Interlocked.Exchange(ref saveQueued, 0);

                LogSummary("load");
                lastLogUtc = clock();
            }
            catch (Exception ex)
            {
                logger?.Invoke($"Error cargando caché de búsqueda: {ex.Message}");
            }
        }

        public void Flush(string reason = "manual")
        {
            try
            {
                SaveToDisk(reason);
            }
            catch (Exception ex)
            {
                logger?.Invoke($"Error guardando caché de búsqueda ({reason}): {ex.Message}");
            }
        }

        public void LogSummary(string reason)
        {
            long hitsSnapshot;
            long missesSnapshot;
            long evictionsSnapshot;
            int count;

            lock (cacheLock)
            {
                hitsSnapshot = cacheHits;
                missesSnapshot = cacheMisses;
                evictionsSnapshot = cacheEvictions;
                count = cache.Count;
            }

            var total = hitsSnapshot + missesSnapshot;
            var hitRate = total > 0 ? (double)hitsSnapshot / total * 100.0 : 0.0;
            logger?.Invoke($"💾 Cache ({reason}): {count} entradas | Hits: {hitsSnapshot} | Misses: {missesSnapshot} | HitRate: {hitRate:F1}% | Evictions: {evictionsSnapshot}");
        }

        public SearchCacheStats GetStats()
        {
            lock (cacheLock)
            {
                return new SearchCacheStats
                {
                    EntryCount = cache.Count,
                    Hits = cacheHits,
                    Misses = cacheMisses,
                    Evictions = cacheEvictions,
                    LastSaveUtc = lastSaveUtc,
                    LastCleanupUtc = lastCleanupUtc
                };
            }
        }

        private void CleanupIfNeeded()
        {
            var now = clock();
            if ((now - lastCleanupUtc) < CleanupInterval)
            {
                return;
            }

            lock (cacheLock)
            {
                var expiredKeys = cache
                    .Where(kvp => now - kvp.Value.Timestamp > cacheTtl)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    if (cache.TryGetValue(key, out var entry) && cache.Remove(key))
                    {
                        RemoveEntryFromIndices(key, entry);
                        Interlocked.Increment(ref cacheEvictions);
                    }
                }
            }

            lastCleanupUtc = now;
        }

        private void EnqueueSave()
        {
            lastSaveUtc = clock();
            if (Interlocked.Exchange(ref saveQueued, 1) == 1)
            {
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        var elapsed = clock() - lastSaveUtc;
                        if (elapsed >= saveInterval)
                        {
                            break;
                        }

                        var delayMs = Math.Max(100, (int)(saveInterval - elapsed).TotalMilliseconds);
                        await Task.Delay(delayMs).ConfigureAwait(false);
                    }

                    SaveToDisk();
                }
                finally
                {
                    Interlocked.Exchange(ref saveQueued, 0);
                }
            });
        }

        private void SaveToDisk(string reason = "auto")
        {
            Dictionary<string, SearchCacheEntry> snapshot;
            lock (cacheLock)
            {
                snapshot = cache.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            var directory = Path.GetDirectoryName(cachePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            File.WriteAllText(cachePath, json, Encoding.UTF8);
            lastSaveUtc = clock();

            var shouldLog = reason != "auto" || (clock() - lastLogUtc) >= TimeSpan.FromMinutes(5);
            if (shouldLog)
            {
                LogSummary(reason);
                lastLogUtc = clock();
            }
        }

        private static List<SearchResultItem> CloneSearchResultItems(IEnumerable<SearchResultItem> items)
        {
            return items?.Select(CloneSearchResultItem).ToList() ?? new List<SearchResultItem>();
        }

        private static SearchResultItem CloneSearchResultItem(SearchResultItem item)
        {
            return new SearchResultItem
            {
                Filename = item.Filename,
                Size = item.Size,
                Username = item.Username,
                Extension = item.Extension,
                Bitrate = item.Bitrate,
                Length = item.Length,
                FolderPath = item.FolderPath,
                QueueLength = item.QueueLength,
                FreeUploadSlots = item.FreeUploadSlots,
                UploadSpeed = item.UploadSpeed,
                AddedAt = item.AddedAt,
                IsDownloaded = item.IsDownloaded,
                IsQueued = item.IsQueued,
                QualityScore = item.QualityScore,
                Network = item.Network,
                Author = item.Author
            };
        }

        private string NormalizeSearchQuery(string query)
        {
            var trimmed = (query ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                return string.Empty;
            }

            try
            {
                if (RustCore.IsAvailable())
                {
                    var normalized = RustCore.NormalizeText(trimmed);
                    if (!string.IsNullOrWhiteSpace(normalized))
                    {
                        return normalized.Trim();
                    }
                }
            }
            catch
            {
                // Ignorar fallos y usar fallback administrado
            }

            var withoutAccents = RemoveAccents(trimmed);
            return string.IsNullOrWhiteSpace(withoutAccents)
                ? trimmed.ToLowerInvariant()
                : withoutAccents.ToLowerInvariant();
        }

        private static (string[] Tokens, string TokenSignature, string ReducedSignature) ComputeQuerySignatures(string query)
        {
            var tokens = new List<string>();

            try
            {
                if (RustCore.IsAvailable())
                {
                    var rustTokens = RustCore.Tokenize(query);
                    if (rustTokens != null && rustTokens.Count > 0)
                    {
                        tokens.AddRange(rustTokens);
                    }
                }
            }
            catch
            {
                // Ignorar fallos y usar fallback administrado
            }

            if (tokens.Count == 0)
            {
                tokens = query
                    .Split(new[] { ' ', '\t', '-', '_', '.', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .ToList();
            }

            var normalizedTokens = tokens
                .Select(token =>
                {
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        return string.Empty;
                    }

                    var cleaned = RemoveAccents(token.Trim());
                    return (string.IsNullOrWhiteSpace(cleaned) ? token : cleaned).ToLowerInvariant();
                })
                .Where(token => token.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(token => token, StringComparer.Ordinal)
                .ToArray();

            var tokenSignature = normalizedTokens.Length > 0
                ? string.Join("|", normalizedTokens)
                : string.Empty;

            var reducedSignature = normalizedTokens.Length > 0
                ? string.Join(string.Empty, normalizedTokens.Select(t => t.Length >= 3 ? t[..3] : t))
                : string.Empty;

            return (normalizedTokens, tokenSignature, reducedSignature);
        }

        private static string RemoveAccents(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder(normalizedString.Length);

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        private static void AddToIndex(Dictionary<string, HashSet<string>> index, string key, string normalizedKey)
        {
            if (!index.TryGetValue(key, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                index[key] = set;
            }

            set.Add(normalizedKey);
        }

        private void UpdateIndices(string normalizedKey, string tokenSignature, string reducedSignature)
        {
            AddToIndex(normalizedIndex, normalizedKey, normalizedKey);

            if (!string.IsNullOrWhiteSpace(tokenSignature))
            {
                AddToIndex(signatureIndex, tokenSignature, normalizedKey);
            }

            if (!string.IsNullOrWhiteSpace(reducedSignature))
            {
                AddToIndex(reducedSignatureIndex, reducedSignature, normalizedKey);
            }
        }

        private void RemoveEntryFromIndices(string normalizedKey, SearchCacheEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            normalizedKey = string.IsNullOrWhiteSpace(normalizedKey) ? entry.NormalizedKey : normalizedKey;

            if (!string.IsNullOrWhiteSpace(normalizedKey) && normalizedIndex.TryGetValue(normalizedKey, out var normalizedSet))
            {
                normalizedSet.Remove(normalizedKey);
                if (normalizedSet.Count == 0)
                {
                    normalizedIndex.Remove(normalizedKey);
                }
            }

            if (!string.IsNullOrWhiteSpace(entry.TokenSignature) && signatureIndex.TryGetValue(entry.TokenSignature, out var signatureSet))
            {
                signatureSet.Remove(normalizedKey);
                if (signatureSet.Count == 0)
                {
                    signatureIndex.Remove(entry.TokenSignature);
                }
            }

            if (!string.IsNullOrWhiteSpace(entry.ReducedSignature) && reducedSignatureIndex.TryGetValue(entry.ReducedSignature, out var reducedSet))
            {
                reducedSet.Remove(normalizedKey);
                if (reducedSet.Count == 0)
                {
                    reducedSignatureIndex.Remove(entry.ReducedSignature);
                }
            }
        }
    }

    public sealed class SearchCacheStats
    {
        public int EntryCount { get; init; }
        public long Hits { get; init; }
        public long Misses { get; init; }
        public long Evictions { get; init; }
        public DateTime LastSaveUtc { get; init; }
        public DateTime LastCleanupUtc { get; init; }
    }
}
