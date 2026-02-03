using System;
using System.Collections.Generic;

namespace SlskDown.Core
{
    public static class RustAdvancedCore
    {
        public static bool IsAvailable() => false;

        public sealed class SortingBenchmarkStats
        {
            public int ItemsProcessed { get; set; }

            public long TimeMs { get; set; }

            public double ItemsPerSecond { get; set; }
        }
        
        public static string NormalizeAuthorName(string name) => name ?? "";
        
        public static List<string> ProcessSearchResults(List<string> results) => results ?? new List<string>();

        public static SortingBenchmarkStats BenchmarkSorting(int items)
        {
            return new SortingBenchmarkStats
            {
                ItemsProcessed = 0,
                TimeMs = 0,
                ItemsPerSecond = 0
            };
        }

        public static List<SearchResultItem> FilterResultsParallel(
            IReadOnlyList<SearchResultItem> candidates,
            long minSizeBytes,
            long maxSizeBytes,
            List<string> extensionFilters,
            bool spanishOnly,
            int minQuality)
        {
            // Fallback: sin Rust. Devolver lista vacía para forzar fallback en caller.
            return new List<SearchResultItem>();
        }
        
        public static string OptimizeQuery(string query) => query ?? "";
        
        public static bool ValidateMetadata(string metadata) => false;
    }
}
