using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SlskDown.Core;
using SlskDown.Models;
using SlskDown.Services;
using SlskDown;
using Xunit;
using Xunit.Abstractions;

namespace SlskDown.Tests
{
    public sealed class RustOptimization2Benchmarks
    {
        private readonly ITestOutputHelper output;
        private readonly List<string> logLines = new List<string>();

        public RustOptimization2Benchmarks(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void Benchmark_Dedupe_Filter_Sort_RustVsCSharp()
        {
            var logPath = Environment.GetEnvironmentVariable("BENCH_LOG_PATH");
            if (string.IsNullOrWhiteSpace(logPath))
            {
                logPath = GetDatasetPath("bench_last.txt");
            }

            try
            {
                var dir = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(
                    logPath,
                    $"START\nRUN_RUST_BENCH={Environment.GetEnvironmentVariable("RUN_RUST_BENCH")}\nBENCH_SIZE={Environment.GetEnvironmentVariable("BENCH_SIZE")}\n");
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BENCH_LOG_PATH")))
                {
                    throw;
                }
            }

            if (!string.Equals(Environment.GetEnvironmentVariable("RUN_RUST_BENCH"), "1", StringComparison.Ordinal))
            {
                try
                {
                    File.AppendAllLines(logPath, new[] { "SKIPPED (set RUN_RUST_BENCH=1)" });
                }
                catch
                {
                    // ignore
                }
                output.WriteLine("Skipping benchmark. Set RUN_RUST_BENCH=1 to run.");
                return;
            }

            int size = 50000;
            var sizeEnv = Environment.GetEnvironmentVariable("BENCH_SIZE");
            if (int.TryParse(sizeEnv, out var parsedSize) && parsedSize > 0)
            {
                size = parsedSize;
            }

            output.WriteLine($"RUN_RUST_BENCH=1 | BENCH_SIZE={size}");
            output.WriteLine($"RustAvailable={RustAdvancedCore.IsAvailable()}");

            var rng = new Random(12345);
            var data = GenerateDataset(rng, size);

            // Warmup
            _ = CSharpDeduplicate(data);
            _ = CSharpFilter(data, minSize: 100 * 1024, maxSize: 5 * 1024 * 1024, allowedExtensions: new[] { ".pdf", ".epub" });
            _ = CSharpSort(data, SortBy.Size);

            if (RustAdvancedCore.IsAvailable())
            {
                _ = RustAdvancedCore.DeduplicateFiles(data);
                _ = RustAdvancedCore.FilterResultsParallel(
                    data,
                    minSize: 100 * 1024,
                    maxSize: 5 * 1024 * 1024,
                    extensions: new List<string> { ".pdf", ".epub" },
                    spanishOnly: false,
                    minQuality: 0);
                _ = RustAdvancedCore.SortSearchResults(data, RustAdvancedCore.SortCriteria.Size);
            }

            // ===================== DEDUPE =====================
            var dedupeCSharp = Time("C# dedupe (Filename|Size)", () => CSharpDeduplicate(data));
            List<SearchResultItem>? dedupeRustNative = null;
            if (SlskNativeInterop.IsAvailable)
            {
                dedupeRustNative = Time("Rust(native) deduplicate_files_native", () => SlskNativeInterop.DeduplicateFiles(
                    data,
                    getFileName: r => DedupeKeyHelpers.BuildRemoteFileKey(r.Filename, r.Size, normalizeFileName: false),
                    getUsername: r => r.Username,
                    getSize: r => r.Size,
                    getProviderScore: _ => 0));
            }

            List<SearchResultItem>? dedupeRustNativeTable = null;
            if (SlskNativeInterop.IsAvailable && SlskNativeInterop.SupportsDedupeKeysTable)
            {
                dedupeRustNativeTable = Time("Rust(native) deduplicate_keys_table_native", () =>
                {
                    var copy = data.ToList();
                    if (!SlskNativeInterop.TryDeduplicateKeysNativeTable(
                            copy,
                            getKey: r => Path.GetFileName(r.Filename ?? string.Empty),
                            getProviderScore: _ => 0,
                            out var unique))
                    {
                        return copy;
                    }

                    return unique;
                });
            }

            List<SearchResultItem>? dedupeRustJson = null;
            if (RustAdvancedCore.IsAvailable())
            {
                dedupeRustJson = Time("Rust(JSON) deduplicate_files_fast", () => RustAdvancedCore.DeduplicateFiles(data));
            }

            // Compare unique key sets (Filename|Size) so provider-selection differences don't fail the benchmark.
            if (dedupeRustNative != null)
            {
                var keysC = new HashSet<string>(dedupeCSharp.Select(DedupeKey), StringComparer.OrdinalIgnoreCase);
                var keysR = new HashSet<string>(dedupeRustNative.Select(DedupeKey), StringComparer.OrdinalIgnoreCase);
                Assert.True(keysC.SetEquals(keysR), "Dedupe result sets differ between Rust(native) and C#");
            }

            if (dedupeRustNativeTable != null)
            {
                var keysC = new HashSet<string>(dedupeCSharp.Select(DedupeKey), StringComparer.OrdinalIgnoreCase);
                var keysR = new HashSet<string>(dedupeRustNativeTable.Select(DedupeKey), StringComparer.OrdinalIgnoreCase);
                Assert.True(keysC.SetEquals(keysR), "Dedupe result sets differ between Rust(native table) and C#");
            }

            if (dedupeRustJson != null)
            {
                var keysC = new HashSet<string>(dedupeCSharp.Select(DedupeKey), StringComparer.OrdinalIgnoreCase);
                var keysR = new HashSet<string>(dedupeRustJson.Select(DedupeKey), StringComparer.OrdinalIgnoreCase);
                Assert.True(keysC.SetEquals(keysR), "Dedupe result sets differ between Rust(JSON) and C#");
            }

            // ===================== FILTER =====================
            const long minSizeBytes = 100 * 1024;
            const long maxSizeBytes = 5 * 1024 * 1024;
            var allowedExt = new[] { ".pdf", ".epub" };

            var filterCSharp = Time("C# filter (size+extension)", () => CSharpFilter(data, minSizeBytes, maxSizeBytes, allowedExt));
            List<SearchResultItem>? filterRustNative = null;
            if (SlskNativeInterop.SupportsSearchFilterSort)
            {
                filterRustNative = Time("Rust(native) filter_search_results_native", () =>
                {
                    if (!SlskNativeInterop.TryFilterSearchResultsNative(
                            data,
                            minSize: minSizeBytes,
                            maxSize: maxSizeBytes,
                            extensions: allowedExt.ToList(),
                            spanishOnly: false,
                            minQuality: 0,
                            out var filtered))
                    {
                        return data;
                    }

                    return filtered;
                });

                var keysC = new HashSet<string>(filterCSharp.Select(DedupeKey), StringComparer.OrdinalIgnoreCase);
                var keysR = new HashSet<string>(filterRustNative.Select(DedupeKey), StringComparer.OrdinalIgnoreCase);
                Assert.True(keysC.SetEquals(keysR), "Filter result sets differ between Rust(native) and C#");
            }
            List<SearchResultItem>? filterRust = null;
            if (RustAdvancedCore.IsAvailable())
            {
                filterRust = Time("Rust filter_results_parallel", () => RustAdvancedCore.FilterResultsParallel(
                    data,
                    minSize: minSizeBytes,
                    maxSize: maxSizeBytes,
                    extensions: allowedExt.ToList(),
                    spanishOnly: false,
                    minQuality: 0));
            }

            if (filterRust != null)
            {
                var keysC = new HashSet<string>(filterCSharp.Select(DedupeKey), StringComparer.OrdinalIgnoreCase);
                var keysR = new HashSet<string>(filterRust.Select(DedupeKey), StringComparer.OrdinalIgnoreCase);
                Assert.True(keysC.SetEquals(keysR), "Filter result sets differ between Rust and C#");
            }

            // ===================== SORT =====================
            var sortInput = filterRustNative ?? filterRust ?? filterCSharp;
            var sortCSharp = Time("C# sort by size", () => CSharpSort(sortInput, SortBy.Size));
            List<SearchResultItem>? sortRustNative = null;
            if (SlskNativeInterop.SupportsSearchFilterSort)
            {
                sortRustNative = Time("Rust(native) sort_by_quality_native", () =>
                {
                    if (!SlskNativeInterop.TrySortByQualityNative(sortInput, out var sorted))
                    {
                        return sortInput;
                    }

                    return sorted;
                });

                Assert.True(IsNonIncreasing(sortRustNative.Select(x => (long)x.QualityScore)), "Rust(native) sort is not non-increasing by quality");
            }
            List<SearchResultItem>? sortRust = null;
            if (RustAdvancedCore.IsAvailable())
            {
                sortRust = Time("Rust sort_search_results_fast (Size)", () => RustAdvancedCore.SortSearchResults(sortInput, RustAdvancedCore.SortCriteria.Size));
            }

            if (sortRust != null)
            {
                // Sorting stability may differ; validate monotonic by size.
                Assert.True(IsNonDecreasing(sortCSharp.Select(x => x.Size)), "C# sort is not non-decreasing by size");
                Assert.True(IsNonDecreasing(sortRust.Select(x => x.Size)), "Rust sort is not non-decreasing by size");
            }

            output.WriteLine("Benchmark finished.");

            try
            {
                File.AppendAllLines(logPath, logLines);
                File.AppendAllLines(logPath, new[] { "END" });
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BENCH_LOG_PATH")))
                {
                    throw;
                }
            }

            // ===================== PERF THRESHOLDS (ADAPTIVE) =====================
            // Nota: esto no es un microbenchmark riguroso; es un guardrail para detectar regresiones grandes.
            // Ajuste adaptativo: solo exigimos mejoras si el tamaño del dataset es lo suficientemente grande.
            if (size >= 50000 && SlskNativeInterop.IsAvailable)
            {
                var strict = string.Equals(Environment.GetEnvironmentVariable("RUN_RUST_BENCH_STRICT"), "1", StringComparison.Ordinal);
                // Re-ejecutar con timing crudo para ratio.
                var (msCSharp, _) = TimeMs(() => CSharpDeduplicate(data));
                if (SlskNativeInterop.SupportsDedupeKeysTable)
                {
                    var (msTable, _) = TimeMs(() =>
                    {
                        var copy = data.ToList();
                        _ = SlskNativeInterop.TryDeduplicateKeysNativeTable(
                            copy,
                            getKey: r => Path.GetFileName(r.Filename ?? string.Empty),
                            getProviderScore: _ => 0,
                            out var unique);
                        return unique ?? copy;
                    });

                    output.WriteLine($"Perf ratio dedupe table: C#={msCSharp}ms vs RustTable={msTable}ms");
                    if (strict)
                    {
                        Assert.True(msTable <= msCSharp * 0.85, "Expected Rust table dedupe to be at least ~15% faster than C# on large datasets");
                    }
                }
            }
        }

        private static (long ms, T result) TimeMs<T>(Func<T> fn)
        {
            var sw = Stopwatch.StartNew();
            var result = fn();
            sw.Stop();
            return (sw.ElapsedMilliseconds, result);
        }

        private static string DedupeKey(SearchResultItem item)
        {
            return DedupeKeyHelpers.BuildRemoteFileKey(item.Filename, item.Size, normalizeFileName: false);
        }

        private static bool IsNonDecreasing(IEnumerable<long> values)
        {
            long prev = long.MinValue;
            foreach (var v in values)
            {
                if (v < prev)
                {
                    return false;
                }

                prev = v;
            }

            return true;
        }

        private static bool IsNonIncreasing(IEnumerable<long> values)
        {
            long prev = long.MaxValue;
            foreach (var v in values)
            {
                if (v > prev)
                {
                    return false;
                }

                prev = v;
            }

            return true;
        }

        private enum SortBy
        {
            Size
        }

        private static List<SearchResultItem> GenerateDataset(Random rng, int count)
        {
            // Deterministic duplicates: ~20% share same (Filename|Size)
            var baseCount = (int)(count * 0.8);
            var baseItems = new List<SearchResultItem>(baseCount);

            string[] usernames = { "u1", "u2", "u3", "u4", "u5", "u6" };
            string[] exts = { ".pdf", ".epub", ".mobi", ".txt", ".mp3" };

            for (int i = 0; i < baseCount; i++)
            {
                var ext = exts[rng.Next(exts.Length)];
                var size = rng.NextInt64(10 * 1024, 10 * 1024 * 1024);
                var fileName = $"Book_{i % 10000:D5}{ext}";

                baseItems.Add(new SearchResultItem
                {
                    Username = usernames[rng.Next(usernames.Length)],
                    Filename = fileName,
                    Size = size,
                    Extension = ext,
                    Bitrate = null,
                    Length = null
                });
            }

            var all = new List<SearchResultItem>(count);
            all.AddRange(baseItems);

            while (all.Count < count)
            {
                var pick = baseItems[rng.Next(baseItems.Count)];
                // Duplicate key but different provider sometimes.
                all.Add(new SearchResultItem
                {
                    Username = usernames[rng.Next(usernames.Length)],
                    Filename = pick.Filename,
                    Size = pick.Size,
                    Extension = pick.Extension,
                    Bitrate = pick.Bitrate,
                    Length = pick.Length
                });
            }

            // Shuffle
            for (int i = all.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (all[i], all[j]) = (all[j], all[i]);
            }

            return all;
        }

        private static List<SearchResultItem> CSharpDeduplicate(List<SearchResultItem> data)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<SearchResultItem>(data.Count);
            foreach (var item in data)
            {
                if (seen.Add(DedupeKey(item)))
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private static List<SearchResultItem> CSharpFilter(
            List<SearchResultItem> data,
            long minSize,
            long maxSize,
            string[] allowedExtensions)
        {
            var extSet = new HashSet<string>(allowedExtensions ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            return data
                .Where(x => x.Size >= minSize && x.Size <= maxSize)
                .Where(x => extSet.Count == 0 || extSet.Contains(x.Extension ?? string.Empty))
                .ToList();
        }

        private static List<SearchResultItem> CSharpSort(List<SearchResultItem> data, SortBy sortBy)
        {
            return sortBy switch
            {
                SortBy.Size => data.OrderBy(x => x.Size).ToList(),
                _ => data
            };
        }

        private List<T> Time<T>(string label, Func<List<T>> fn)
        {
            var sw = Stopwatch.StartNew();
            var result = fn();
            sw.Stop();
            var line = $"{label}: {result.Count:N0} items in {sw.ElapsedMilliseconds:N0}ms";
            logLines.Add(line);
            output.WriteLine(line);
            return result;
        }
    }
}
