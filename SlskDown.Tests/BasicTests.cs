using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using SlskDown;

namespace SlskDown.Tests
{
    public class BasicTests
    {
        [Fact]
        public void SimpleTest_ShouldPass()
        {
            int expected = 4;
            
            int actual = 2 + 2;
            
            Assert.Equal(expected, actual);
        }
        
        [Fact]
        public void StringTest_ShouldPass()
        {
            string expected = "Hello";
            
            string actual = "Hello";
            
            Assert.Equal(expected, actual);
        }
        
        [Theory]
        [InlineData(1, 1, 2)]
        [InlineData(2, 2, 4)]
        [InlineData(3, 3, 6)]
        public void AdditionTest_ShouldPass(int a, int b, int expected)
        {
            int actual = a + b;
            
            Assert.Equal(expected, actual);
        }
        
        [Fact]
        public void BooleanTest_ShouldPass()
        {
            Assert.True(true);
            Assert.False(false);
        }
        
        [Fact]
        public void NullTest_ShouldPass()
        {
            string? nullString = null;
            string notNullString = "test";
            
            Assert.Null(nullString);
            Assert.NotNull(notNullString);
        }

        [Fact]
        public void NativeFilterSort_ShouldMatchCSharp_WhenEnabled()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("RUN_RUST_BENCH"), "1", StringComparison.Ordinal))
            {
                return;
            }

            if (!SlskNativeInterop.SupportsSearchFilterSort)
            {
                return;
            }

            var rng = new Random(123);
            var data = new List<SearchResultItem>();
            string[] exts = { ".pdf", ".epub", ".mobi", ".txt" };
            for (int i = 0; i < 5000; i++)
            {
                var ext = exts[rng.Next(exts.Length)];
                data.Add(new SearchResultItem
                {
                    Filename = $"Book_{i:D5}{ext}",
                    Extension = ext,
                    Size = rng.NextInt64(10 * 1024, 10 * 1024 * 1024),
                    QualityScore = rng.Next(0, 101)
                });
            }

            const long minSize = 100 * 1024;
            const long maxSize = 5 * 1024 * 1024;
            var allowedExt = new List<string> { ".pdf", ".epub" };

            var csharpFiltered = data
                .Where(x => x.Size >= minSize && x.Size <= maxSize)
                .Where(x => allowedExt.Contains(x.Extension, StringComparer.OrdinalIgnoreCase))
                .ToList();

            Assert.True(SlskNativeInterop.TryFilterSearchResultsNative(
                data,
                minSize: minSize,
                maxSize: maxSize,
                extensions: allowedExt,
                spanishOnly: false,
                minQuality: 0,
                out var nativeFiltered));

            var keysC = new HashSet<string>(csharpFiltered.Select(x => $"{x.Filename}|{x.Size}"), StringComparer.OrdinalIgnoreCase);
            var keysN = new HashSet<string>(nativeFiltered.Select(x => $"{x.Filename}|{x.Size}"), StringComparer.OrdinalIgnoreCase);
            Assert.True(keysC.SetEquals(keysN));

            var csharpSorted = nativeFiltered.OrderByDescending(x => x.QualityScore).ToList();
            Assert.True(SlskNativeInterop.TrySortByQualityNative(nativeFiltered, out var nativeSorted));

            Assert.True(IsNonIncreasing(nativeSorted.Select(x => (long)x.QualityScore)));
            Assert.True(IsNonIncreasing(csharpSorted.Select(x => (long)x.QualityScore)));
        }

        [Fact]
        public void NativeSinglePassPipeline_ShouldMatchCSharp_WhenEnabled()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("RUN_RUST_BENCH"), "1", StringComparison.Ordinal))
            {
                return;
            }

            if (!SlskNativeInterop.SupportsSearchPipeline)
            {
                return;
            }

            var rng = new Random(777);
            var data = GenerateSearchDataset(rng, count: 15000);

            const long minSize = 100 * 1024;
            const long maxSize = 5 * 1024 * 1024;
            var allowedExt = new List<string> { ".pdf", ".epub" };
            const int minQuality = 60;

            var csharp = data
                .Where(x => x.Size >= minSize && x.Size <= maxSize)
                .Where(x => allowedExt.Contains(x.Extension, StringComparer.OrdinalIgnoreCase))
                .Where(x => x.QualityScore >= minQuality)
                .GroupBy(x => $"{x.Filename}|{x.Size}", StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.QualityScore).ThenBy(x => x.Filename, StringComparer.OrdinalIgnoreCase).First())
                .OrderByDescending(x => x.QualityScore)
                .ToList();

            Assert.True(SlskNativeInterop.TryProcessSearchResultsNativeTable(
                data,
                minSize: minSize,
                maxSize: maxSize,
                extensions: allowedExt,
                spanishOnly: false,
                minQuality: minQuality,
                getProviderScore: _ => 0,
                out var native));

            var keysC = new HashSet<string>(csharp.Select(x => $"{x.Filename}|{x.Size}"), StringComparer.OrdinalIgnoreCase);
            var keysN = new HashSet<string>(native.Select(x => $"{x.Filename}|{x.Size}"), StringComparer.OrdinalIgnoreCase);
            Assert.True(keysC.SetEquals(keysN));

            Assert.True(IsNonIncreasing(native.Select(x => (long)x.QualityScore)));
        }

        [Fact]
        public void NativeSinglePassPipeline_ArenaVsPointers_ShouldMatch_WhenEnabled()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("RUN_RUST_BENCH"), "1", StringComparison.Ordinal))
            {
                return;
            }

            if (!SlskNativeInterop.SupportsSearchPipeline)
            {
                return;
            }

            var rng = new Random(888);
            var data = GenerateSearchDataset(rng, count: 20000);

            const long minSize = 0;
            const long maxSize = long.MaxValue;

            Assert.True(SlskNativeInterop.TryProcessSearchResultsNativeTable(
                data,
                minSize: minSize,
                maxSize: maxSize,
                extensions: null,
                spanishOnly: false,
                minQuality: 0,
                getProviderScore: _ => 0,
                out var arena));

            Assert.True(SlskNativeInterop.TryProcessSearchResultsNative(
                data,
                minSize: minSize,
                maxSize: maxSize,
                extensions: null,
                spanishOnly: false,
                minQuality: 0,
                getProviderScore: _ => 0,
                out var ptrs));

            var keysA = new HashSet<string>(arena.Select(x => $"{x.Filename}|{x.Size}"), StringComparer.OrdinalIgnoreCase);
            var keysP = new HashSet<string>(ptrs.Select(x => $"{x.Filename}|{x.Size}"), StringComparer.OrdinalIgnoreCase);
            Assert.True(keysA.SetEquals(keysP));
        }

        [Fact]
        public void NativeSinglePassPipeline_ShouldPreferHigherProviderScore_WhenQualityTies()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("RUN_RUST_BENCH"), "1", StringComparison.Ordinal))
            {
                return;
            }

            if (!SlskNativeInterop.SupportsSearchPipeline)
            {
                return;
            }

            var data = new List<SearchResultItem>
            {
                new SearchResultItem { Username = "u_low", Filename = "Same.pdf", Extension = ".pdf", Size = 1234, QualityScore = 80 },
                new SearchResultItem { Username = "u_high", Filename = "Same.pdf", Extension = ".pdf", Size = 1234, QualityScore = 80 },
                new SearchResultItem { Username = "u_mid", Filename = "Other.pdf", Extension = ".pdf", Size = 2000, QualityScore = 80 },
                new SearchResultItem { Username = "u_any", Filename = "Top.pdf", Extension = ".pdf", Size = 3000, QualityScore = 95 }
            };

            int ProviderScore(string u)
            {
                return u switch
                {
                    "u_low" => 1,
                    "u_mid" => 5,
                    "u_high" => 10,
                    _ => 0
                };
            }

            Assert.True(SlskNativeInterop.TryProcessSearchResultsNativeTable(
                data,
                minSize: 0,
                maxSize: long.MaxValue,
                extensions: new List<string> { ".pdf" },
                spanishOnly: false,
                minQuality: 0,
                getProviderScore: ProviderScore,
                out var native));

            Assert.Equal(3, native.Count);
            Assert.Equal("Top.pdf", native[0].Filename);
            Assert.Equal("Same.pdf", native[1].Filename);
            Assert.Equal("u_high", native[1].Username);
            Assert.Equal("Other.pdf", native[2].Filename);
            Assert.Equal("u_mid", native[2].Username);
        }

        [Fact]
        public void ReproDataset_SinglePassPipeline_ShouldMatchCSharp_WhenEnabled()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("RUN_RUST_BENCH"), "1", StringComparison.Ordinal))
            {
                return;
            }

            if (!SlskNativeInterop.SupportsSearchPipeline)
            {
                return;
            }

            var datasetPath = GetDatasetPath("search_dataset_seed777_50000.jsonl");
            EnsureSearchDataset(datasetPath, seed: 777, count: 50000);
            var data = LoadDataset(datasetPath);

            const long minSize = 100 * 1024;
            const long maxSize = 5 * 1024 * 1024;
            var allowedExt = new List<string> { ".pdf", ".epub" };
            const int minQuality = 60;

            var csharp = data
                .Where(x => x.Size >= minSize && x.Size <= maxSize)
                .Where(x => allowedExt.Contains(x.Extension, StringComparer.OrdinalIgnoreCase))
                .Where(x => x.QualityScore >= minQuality)
                .GroupBy(x => $"{x.Filename}|{x.Size}", StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.QualityScore).ThenBy(x => x.Filename, StringComparer.OrdinalIgnoreCase).First())
                .OrderByDescending(x => x.QualityScore)
                .ToList();

            Assert.True(SlskNativeInterop.TryProcessSearchResultsNativeTable(
                data,
                minSize: minSize,
                maxSize: maxSize,
                extensions: allowedExt,
                spanishOnly: false,
                minQuality: minQuality,
                getProviderScore: _ => 0,
                out var native));

            var keysC = new HashSet<string>(csharp.Select(x => $"{x.Filename}|{x.Size}"), StringComparer.OrdinalIgnoreCase);
            var keysN = new HashSet<string>(native.Select(x => $"{x.Filename}|{x.Size}"), StringComparer.OrdinalIgnoreCase);
            Assert.True(keysC.SetEquals(keysN));
            Assert.True(IsNonIncreasing(native.Select(x => (long)x.QualityScore)));
        }

        private static string GetDatasetPath(string fileName)
        {
            var baseDir = AppContext.BaseDirectory;
            var candidateProjectDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            var candidateTestDataDir = Path.Combine(candidateProjectDir, "TestData");
            try
            {
                Directory.CreateDirectory(candidateTestDataDir);
                return Path.Combine(candidateTestDataDir, fileName);
            }
            catch
            {
                var fallback = Path.Combine(baseDir, "TestData");
                Directory.CreateDirectory(fallback);
                return Path.Combine(fallback, fileName);
            }
        }

        private static void EnsureSearchDataset(string path, int seed, int count)
        {
            if (File.Exists(path))
            {
                return;
            }

            var rng = new Random(seed);
            var dataset = GenerateSearchDataset(rng, count);
            SaveDataset(path, dataset);
        }

        private static void SaveDataset(string path, List<SearchResultItem> items)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? AppDomain.CurrentDomain.BaseDirectory);
            using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            foreach (var item in items)
            {
                var json = JsonSerializer.Serialize(item);
                writer.WriteLine(json);
            }
        }

        private static List<SearchResultItem> LoadDataset(string path)
        {
            var list = new List<SearchResultItem>();
            foreach (var line in File.ReadLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var item = JsonSerializer.Deserialize<SearchResultItem>(line);
                if (item != null)
                {
                    list.Add(item);
                }
            }
            return list;
        }

        private static List<SearchResultItem> GenerateSearchDataset(Random rng, int count)
        {
            var items = new List<SearchResultItem>(count);
            string[] usernames = { "u1", "u2", "u3", "u4", "u5" };
            string[] exts = { ".pdf", ".epub", ".mobi", ".txt" };
            var baseCount = (int)(count * 0.7);
            for (int i = 0; i < baseCount; i++)
            {
                var ext = exts[rng.Next(exts.Length)];
                var size = rng.NextInt64(10 * 1024, 12 * 1024 * 1024);
                items.Add(new SearchResultItem
                {
                    Username = usernames[rng.Next(usernames.Length)],
                    Filename = $"Book_{i % 5000:D5}{ext}",
                    Extension = ext,
                    Size = size,
                    QualityScore = rng.Next(0, 101)
                });
            }

            while (items.Count < count)
            {
                var pick = items[rng.Next(items.Count)];
                items.Add(new SearchResultItem
                {
                    Username = usernames[rng.Next(usernames.Length)],
                    Filename = pick.Filename,
                    Extension = pick.Extension,
                    Size = pick.Size,
                    QualityScore = rng.Next(0, 101)
                });
            }

            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }

            return items;
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
    }
}
