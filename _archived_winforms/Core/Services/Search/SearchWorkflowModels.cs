using System;
using System.Collections.Generic;
using Soulseek;

namespace SlskDown.Core.Services.Search
{
    /// <summary>
    /// Parámetros para ejecutar una búsqueda.
    /// </summary>
    public sealed class SearchWorkflowRequest
    {
        public string Query { get; init; } = string.Empty;
        public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
        public int ResponseLimit { get; init; } = 1000;
        public int FileLimit { get; init; } = 50000;
        public bool ContinuousMode { get; init; }
        public TimeSpan ContinuousInterval { get; init; } = TimeSpan.FromSeconds(2);
        public int ContinuousEmptyThreshold { get; init; } = 3;
        public bool UseCache { get; init; } = true;
        public bool CacheOnly { get; init; }
        public SearchFilterContext Filters { get; init; } = new SearchFilterContext();
    }

    /// <summary>
    /// Contexto para aplicar filtros a cada respuesta.
    /// </summary>
    public sealed class SearchFilterContext
    {
        public long? MinSizeBytes { get; init; }
        public long? MaxSizeBytes { get; init; }
        public IReadOnlyCollection<string> AllowedExtensions { get; init; } = Array.Empty<string>();
        public bool SpanishOnly { get; init; }
        public IReadOnlyCollection<string> BlacklistedUsers { get; init; } = Array.Empty<string>();
        public Func<Soulseek.File, bool>? BlacklistFilePredicate { get; init; }
        public Func<string, bool>? GarbageFilePredicate { get; init; }
        public Func<string, bool>? SpanishHeuristic { get; init; }
        public Func<string, bool>? ExtensionPredicate { get; init; }
        public Func<Soulseek.SearchResponse, Soulseek.File, bool>? AdditionalFilter { get; init; }
        public string ExtensionCategory { get; init; } = "Todos";
    }

    /// <summary>
    /// Resultado completo de un workflow de búsqueda.
    /// </summary>
    public sealed class SearchWorkflowResult
    {
        public IReadOnlyList<SearchResultItem> Items { get; init; } = Array.Empty<SearchResultItem>();
        public bool FromCache { get; init; }
        public bool Cancelled { get; init; }
        public bool TimedOut { get; init; }
        public SearchWorkflowStats Stats { get; init; } = new SearchWorkflowStats();
    }

    /// <summary>
    /// Métricas resumen de la ejecución.
    /// </summary>
    public sealed class SearchWorkflowStats
    {
        public int ResponsesProcessed { get; init; }
        public int FilesProcessed { get; init; }
        public int FilesFilteredBySize { get; init; }
        public int FilesFilteredByExtension { get; init; }
        public int FilesFilteredByGarbage { get; init; }
        public int FilesFilteredBySpanish { get; init; }
        public int FilesFilteredByBlacklist { get; init; }
        public int TotalFilesAccepted { get; init; }
        public TimeSpan Duration { get; init; }
    }

    /// <summary>
    /// Resultado detallado del filtrado de una respuesta.
    /// </summary>
    public sealed class SearchFilterResult
    {
        public IReadOnlyList<Soulseek.File> AcceptedFiles { get; init; } = Array.Empty<Soulseek.File>();
        public int FilteredBySize { get; init; }
        public int FilteredByExtension { get; init; }
        public int FilteredByGarbage { get; init; }
        public int FilteredBySpanish { get; init; }
        public int FilteredByBlacklist { get; init; }
        public int TotalFilesEvaluated { get; init; }
    }
}
