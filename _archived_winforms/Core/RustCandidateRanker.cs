using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace SlskDown.Core
{
    public static class RustCandidateRanker
    {
        private static readonly object Sync = new();
        private static DateTime? lastFailureUtc;
        private static int consecutiveFailures;
        private static readonly TimeSpan FailureCooldown = TimeSpan.FromSeconds(30);

        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
        private const int MaxCacheEntries = 5000;
        private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new(StringComparer.OrdinalIgnoreCase);

        private sealed record CacheEntry(DateTime ExpiresUtc, RankedCandidate Candidate);

        private static long cacheHits;
        private static long cacheMisses;
        private static long rustCalls;
        private static long rustFailures;
        private static long cooldownBlocks;

        private static long rustTotalBatchMs;
        private static long rustTotalBatches;
        private static long rustTotalCandidates;

        public sealed record RankerStatsSnapshot(
            long CacheHits,
            long CacheMisses,
            long RustCalls,
            long RustFailures,
            long CooldownBlocks,
            int CacheSize,
            long RustTotalBatchMs,
            long RustTotalBatches,
            long RustTotalCandidates);

        public static RankerStatsSnapshot GetStatsSnapshot()
        {
            return new RankerStatsSnapshot(
                Interlocked.Read(ref cacheHits),
                Interlocked.Read(ref cacheMisses),
                Interlocked.Read(ref rustCalls),
                Interlocked.Read(ref rustFailures),
                Interlocked.Read(ref cooldownBlocks),
                Cache.Count,
                Interlocked.Read(ref rustTotalBatchMs),
                Interlocked.Read(ref rustTotalBatches),
                Interlocked.Read(ref rustTotalCandidates));
        }

        private sealed class CandidateRankInput
        {
            public CandidateFileInfo File { get; set; } = new();
            public string? TargetAuthor { get; set; }
            public string? TargetTitle { get; set; }
        }

        private sealed class CandidateRankRequest
        {
            public int SchemaVersion { get; set; } = CandidateQueueSchema.CurrentVersion;
            public List<CandidateRankInput> Candidates { get; set; } = new();
        }

        private sealed class CandidateRankResponse
        {
            public int SchemaVersion { get; set; }
            public List<RankedCandidate> Results { get; set; } = new();
        }

        private static string BuildCacheKey(CandidateFileInfo candidate, string? targetAuthor, string? targetTitle)
        {
            var fileName = candidate.FileName ?? string.Empty;
            var user = candidate.Username ?? string.Empty;
            var author = targetAuthor ?? string.Empty;
            var title = targetTitle ?? string.Empty;

            return string.Concat(
                fileName, "|",
                user, "|",
                candidate.SizeBytes.ToString(CultureInfo.InvariantCulture), "|",
                author, "|",
                title);
        }

        public static bool TryRankCandidates(
            IReadOnlyList<CandidateFileInfo> candidates,
            IReadOnlyList<(string? targetAuthor, string? targetTitle)> targets,
            out IReadOnlyList<RankedCandidate> ranked,
            out string? error)
        {
            ranked = Array.Empty<RankedCandidate>();
            error = null;

            if (candidates == null || candidates.Count == 0)
            {
                ranked = Array.Empty<RankedCandidate>();
                return true;
            }

            if (targets == null || targets.Count != candidates.Count)
            {
                error = "targets count mismatch";
                return false;
            }

            var nowUtc = DateTime.UtcNow;
            var merged = new RankedCandidate?[candidates.Count];
            var missingCandidates = new List<CandidateFileInfo>();
            var missingTargets = new List<(string? targetAuthor, string? targetTitle)>();
            var missingIndices = new List<int>();

            for (var i = 0; i < candidates.Count; i++)
            {
                var key = BuildCacheKey(candidates[i], targets[i].targetAuthor, targets[i].targetTitle);
                if (Cache.TryGetValue(key, out var entry))
                {
                    if (entry.ExpiresUtc > nowUtc)
                    {
                        Interlocked.Increment(ref cacheHits);
                        merged[i] = entry.Candidate;
                        continue;
                    }

                    Cache.TryRemove(key, out _);
                }

                Interlocked.Increment(ref cacheMisses);

                missingCandidates.Add(candidates[i]);
                missingTargets.Add(targets[i]);
                missingIndices.Add(i);
            }

            if (missingCandidates.Count == 0)
            {
                ranked = merged.Where(c => c != null).Cast<RankedCandidate>().ToArray();
                return true;
            }

            try
            {
                lock (Sync)
                {
                    if (lastFailureUtc.HasValue && consecutiveFailures > 0)
                    {
                        var elapsed = DateTime.UtcNow - lastFailureUtc.Value;
                        if (elapsed < FailureCooldown)
                        {
                            Interlocked.Increment(ref cooldownBlocks);
                            error = "Rust ranker cooldown";
                            return false;
                        }
                    }
                }

                Interlocked.Increment(ref rustCalls);

                var stopwatch = Stopwatch.StartNew();

                var request = new CandidateRankRequest
                {
                    SchemaVersion = CandidateQueueSchema.CurrentVersion,
                    Candidates = missingCandidates.Select((c, i) => new CandidateRankInput
                    {
                        File = c,
                        TargetAuthor = missingTargets[i].targetAuthor,
                        TargetTitle = missingTargets[i].targetTitle
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null,
                    WriteIndented = false
                });

                var responseJson = RustCore.RankCandidatesV1(json);
                if (string.IsNullOrWhiteSpace(responseJson))
                {
                    stopwatch.Stop();
                    Interlocked.Increment(ref rustTotalBatches);
                    Interlocked.Add(ref rustTotalCandidates, missingCandidates.Count);
                    Interlocked.Add(ref rustTotalBatchMs, stopwatch.ElapsedMilliseconds);

                    lock (Sync)
                    {
                        lastFailureUtc = DateTime.UtcNow;
                        consecutiveFailures++;
                    }
                    Interlocked.Increment(ref rustFailures);
                    error = "Rust rank returned empty";
                    return false;
                }

                var response = JsonSerializer.Deserialize<CandidateRankResponse>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (response?.Results == null)
                {
                    stopwatch.Stop();
                    Interlocked.Increment(ref rustTotalBatches);
                    Interlocked.Add(ref rustTotalCandidates, missingCandidates.Count);
                    Interlocked.Add(ref rustTotalBatchMs, stopwatch.ElapsedMilliseconds);

                    lock (Sync)
                    {
                        lastFailureUtc = DateTime.UtcNow;
                        consecutiveFailures++;
                    }
                    Interlocked.Increment(ref rustFailures);
                    error = "Rust rank deserialize null";
                    return false;
                }

                if (response.Results.Count != missingCandidates.Count)
                {
                    stopwatch.Stop();
                    Interlocked.Increment(ref rustTotalBatches);
                    Interlocked.Add(ref rustTotalCandidates, missingCandidates.Count);
                    Interlocked.Add(ref rustTotalBatchMs, stopwatch.ElapsedMilliseconds);

                    lock (Sync)
                    {
                        lastFailureUtc = DateTime.UtcNow;
                        consecutiveFailures++;
                    }
                    Interlocked.Increment(ref rustFailures);
                    error = "Rust rank results count mismatch";
                    return false;
                }

                for (var j = 0; j < response.Results.Count; j++)
                {
                    var idx = missingIndices[j];
                    var candidateRanked = response.Results[j];
                    merged[idx] = candidateRanked;

                    var key = BuildCacheKey(candidates[idx], targets[idx].targetAuthor, targets[idx].targetTitle);
                    Cache[key] = new CacheEntry(nowUtc + CacheTtl, candidateRanked);
                }

                TrimCache(nowUtc);

                stopwatch.Stop();
                Interlocked.Increment(ref rustTotalBatches);
                Interlocked.Add(ref rustTotalCandidates, missingCandidates.Count);
                Interlocked.Add(ref rustTotalBatchMs, stopwatch.ElapsedMilliseconds);

                if (merged.Any(r => r == null))
                {
                    lock (Sync)
                    {
                        lastFailureUtc = DateTime.UtcNow;
                        consecutiveFailures++;
                    }
                    Interlocked.Increment(ref rustFailures);
                    error = "Rust rank merge incomplete";
                    return false;
                }

                ranked = merged.Cast<RankedCandidate>().ToArray();

                lock (Sync)
                {
                    consecutiveFailures = 0;
                    lastFailureUtc = null;
                }
                return true;
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref rustTotalBatches);
                Interlocked.Add(ref rustTotalCandidates, missingCandidates.Count);

                lock (Sync)
                {
                    lastFailureUtc = DateTime.UtcNow;
                    consecutiveFailures++;
                }
                Interlocked.Increment(ref rustFailures);
                error = ex.Message;
                return false;
            }
        }

        private static void TrimCache(DateTime nowUtc)
        {
            if (Cache.IsEmpty)
            {
                return;
            }

            foreach (var pair in Cache)
            {
                if (pair.Value.ExpiresUtc <= nowUtc)
                {
                    Cache.TryRemove(pair.Key, out _);
                }
            }

            var overflow = Cache.Count - MaxCacheEntries;
            if (overflow <= 0)
            {
                return;
            }

            var toRemove = Cache
                .Select(pair => (pair.Key, pair.Value.ExpiresUtc))
                .OrderBy(pair => pair.ExpiresUtc)
                .Take(overflow)
                .Select(pair => pair.Key)
                .ToList();

            foreach (var key in toRemove)
            {
                Cache.TryRemove(key, out _);
            }
        }
    }
}
