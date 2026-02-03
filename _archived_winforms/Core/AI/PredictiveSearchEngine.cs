using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SlskDown.Core.AI
{
    public class PredictiveSearchEngine
    {
        private readonly ConcurrentDictionary<string, int> _searchHistory = new();
        private readonly List<string> _popularAuthors = new();
        private const int MAX_PREDICTIONS = 5;

        public void UpdatePopularAuthors(IEnumerable<string> authors)
        {
            _popularAuthors.Clear();
            _popularAuthors.AddRange(authors.Take(100));
        }

        public void RecordSearch(string query)
        {
            _searchHistory.AddOrUpdate(query, 1, (_, count) => count + 1);
        }

        public IEnumerable<string> GetPredictions(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return Enumerable.Empty<string>();

            // 1. Coincidencias en historial (por frecuencia)
            var historyMatches = _searchHistory
                .Where(h => h.Key.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(h => h.Value)
                .Select(h => h.Key);

            // 2. Coincidencias en autores populares
            var authorMatches = _popularAuthors
                .Where(a => a.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                .Except(historyMatches);

            return historyMatches.Concat(authorMatches).Take(MAX_PREDICTIONS);
        }
    }
}
