using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Soulseek;

namespace SlskDown.Core.Services.Search
{
    /// <summary>
    /// Servicio de filtrado que aplica las mismas reglas que MainForm.
    /// Usa delegados para heurísticas específicas que siguen residiendo en la UI.
    /// </summary>
    public sealed class SearchFilterService : ISearchFilterService
    {
        private readonly Func<string, string, bool> matchesCategory;

        public SearchFilterService(Func<string, string, bool> matchesCategory)
        {
            this.matchesCategory = matchesCategory ?? throw new ArgumentNullException(nameof(matchesCategory));
        }

        public SearchFilterResult FilterResponse(Soulseek.SearchResponse response, SearchFilterContext context)
        {
            if (response is null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var files = response.Files ?? Array.Empty<Soulseek.File>();

            if (context.BlacklistedUsers.Contains(response.Username))
            {
                return new SearchFilterResult
                {
                    FilteredByBlacklist = files.Count(),
                    TotalFilesEvaluated = files.Count()
                };
            }

            var accepted = new List<Soulseek.File>();
            var filteredBySize = 0;
            var filteredByExtension = 0;
            var filteredByGarbage = 0;
            var filteredBySpanish = 0;
            var filteredByBlacklist = 0;
            var totalEvaluated = 0;

            foreach (var file in files)
            {
                totalEvaluated++;

                if (file is null)
                {
                    continue;
                }

                if (file.Size <= 0)
                {
                    filteredBySize++;
                    continue;
                }

                if (context.MinSizeBytes.HasValue && file.Size < context.MinSizeBytes.Value)
                {
                    filteredBySize++;
                    continue;
                }

                if (context.MaxSizeBytes.HasValue && file.Size > context.MaxSizeBytes.Value)
                {
                    filteredBySize++;
                    continue;
                }

                var extension = Path.GetExtension(file.Filename)?.ToLowerInvariant() ?? string.Empty;

                if (context.AllowedExtensions.Count > 0 && !context.AllowedExtensions.Contains(extension))
                {
                    filteredByExtension++;
                    continue;
                }

                if (context.ExtensionPredicate != null && !context.ExtensionPredicate(extension))
                {
                    filteredByExtension++;
                    continue;
                }

                if (!MatchesCategory(extension, context))
                {
                    filteredByExtension++;
                    continue;
                }

                if (context.GarbageFilePredicate != null && context.GarbageFilePredicate(file.Filename))
                {
                    filteredByGarbage++;
                    continue;
                }

                if (context.BlacklistFilePredicate != null && context.BlacklistFilePredicate(file))
                {
                    filteredByBlacklist++;
                    continue;
                }

                if (context.SpanishOnly && context.SpanishHeuristic != null && !context.SpanishHeuristic(file.Filename))
                {
                    filteredBySpanish++;
                    continue;
                }

                if (context.AdditionalFilter != null && !context.AdditionalFilter(response, file))
                {
                    continue;
                }

                accepted.Add(file);
            }

            return new SearchFilterResult
            {
                AcceptedFiles = accepted,
                FilteredBySize = filteredBySize,
                FilteredByExtension = filteredByExtension,
                FilteredByGarbage = filteredByGarbage,
                FilteredBySpanish = filteredBySpanish,
                FilteredByBlacklist = filteredByBlacklist,
                TotalFilesEvaluated = totalEvaluated
            };
        }

        private bool MatchesCategory(string extension, SearchFilterContext context)
        {
            if (context.AllowedExtensions.Count > 0)
            {
                return true;
            }

            return matchesCategory(extension, context.ExtensionCategory);
        }
    }
}
