using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Motor de búsqueda paralela nativo en C# sin dependencias de Rust
    /// Usa PLINQ para paralelización automática
    /// </summary>
    public class ParallelSearchNative
    {
        public class SearchResult
        {
            public string FileName { get; set; }
            public string Author { get; set; }
            public string Extension { get; set; }
            public long Size { get; set; }
            public string Username { get; set; }
            public string Folder { get; set; }
            public double Score { get; set; }
        }

        public List<SearchResult> SearchParallel(
            IEnumerable<SearchResult> items,
            string query,
            int maxResults = 1000)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<SearchResult>();

            var queryLower = query.ToLowerInvariant();
            var queryTerms = queryLower.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // Búsqueda paralela con PLINQ
            var results = items
                .AsParallel()
                .WithDegreeOfParallelism(Environment.ProcessorCount)
                .Select(item => new
                {
                    Item = item,
                    Score = CalculateScore(item, queryTerms, queryLower)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(maxResults)
                .Select(x =>
                {
                    var result = x.Item;
                    result.Score = x.Score;
                    return result;
                })
                .ToList();

            return results;
        }

        private double CalculateScore(SearchResult item, string[] queryTerms, string fullQuery)
        {
            double score = 0;
            var fileNameLower = item.FileName?.ToLowerInvariant() ?? "";
            var authorLower = item.Author?.ToLowerInvariant() ?? "";
            var folderLower = item.Folder?.ToLowerInvariant() ?? "";

            // Coincidencia exacta de frase completa (peso alto)
            if (fileNameLower.Contains(fullQuery))
                score += 100;
            if (authorLower.Contains(fullQuery))
                score += 80;

            // Coincidencia de términos individuales
            foreach (var term in queryTerms)
            {
                if (fileNameLower.Contains(term))
                    score += 10;
                if (authorLower.Contains(term))
                    score += 8;
                if (folderLower.Contains(term))
                    score += 5;

                // Bonus por coincidencia al inicio
                if (fileNameLower.StartsWith(term))
                    score += 5;
                if (authorLower.StartsWith(term))
                    score += 4;
            }

            // Bonus por coincidencia de todos los términos
            if (queryTerms.All(term => fileNameLower.Contains(term) || authorLower.Contains(term)))
                score += 20;

            return score;
        }

        public Task<List<SearchResult>> SearchParallelAsync(
            IEnumerable<SearchResult> items,
            string query,
            int maxResults = 1000)
        {
            return Task.Run(() => SearchParallel(items, query, maxResults));
        }
    }
}
