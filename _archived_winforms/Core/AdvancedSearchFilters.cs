using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core
{
    /// <summary>
    /// Filtros avanzados específicos por red para búsquedas multi-red
    /// Permite filtrado granular según características de cada red
    /// </summary>
    public class AdvancedSearchFilters
    {
        // Filtros generales
        public long? MinSizeBytes { get; set; }
        public long? MaxSizeBytes { get; set; }
        public List<string> FileExtensions { get; set; }
        public List<string> ExcludeKeywords { get; set; }
        public int? MinBitrate { get; set; }

        // Filtros específicos de Soulseek
        public SoulseekFilters Soulseek { get; set; } = new SoulseekFilters();

        // Filtros de reputación
        public ReputationFilters Reputation { get; set; } = new ReputationFilters();

        /// <summary>
        /// Aplica todos los filtros a una lista de resultados
        /// </summary>
        public List<SearchResult> Apply(List<SearchResult> results)
        {
            return results.Where(r => PassesAllFilters(r)).ToList();
        }

        /// <summary>
        /// Verifica si un resultado pasa todos los filtros
        /// </summary>
        public bool PassesAllFilters(SearchResult result)
        {
            // Filtros generales
            if (!PassesGeneralFilters(result))
                return false;

            // Filtros específicos por red
            if (result.NetworkSource == "Soulseek")
            {
                return Soulseek.Passes(result);
            }
            
            return true;
        }

        private bool PassesGeneralFilters(SearchResult result)
        {
            // Tamaño mínimo
            if (MinSizeBytes.HasValue && result.SizeBytes < MinSizeBytes.Value)
                return false;

            // Tamaño máximo
            if (MaxSizeBytes.HasValue && result.SizeBytes > MaxSizeBytes.Value)
                return false;

            // Extensiones permitidas
            if (FileExtensions != null && FileExtensions.Any())
            {
                var ext = System.IO.Path.GetExtension(result.FileName)?.ToLowerInvariant();
                if (!FileExtensions.Any(e => e.ToLowerInvariant() == ext))
                    return false;
            }

            // Palabras excluidas
            if (ExcludeKeywords != null && ExcludeKeywords.Any())
            {
                var fileName = result.FileName.ToLowerInvariant();
                if (ExcludeKeywords.Any(k => fileName.Contains(k.ToLowerInvariant())))
                    return false;
            }

            // Bitrate mínimo
            if (MinBitrate.HasValue && result.BitRate.HasValue && result.BitRate.Value < MinBitrate.Value)
                return false;

            return true;
        }
    }

    /// <summary>
    /// Filtros específicos para Soulseek
    /// </summary>
    public class SoulseekFilters
    {
        public int? MinFreeSlots { get; set; }
        public int? MaxQueueLength { get; set; }
        public bool RequireFreeSlots { get; set; }
        public bool ExcludeLockedFiles { get; set; }
        public List<string> PreferredUsers { get; set; }
        public List<string> BlockedUsers { get; set; }

        public bool Passes(SearchResult result)
        {
            // Slots libres mínimos
            if (MinFreeSlots.HasValue && (!result.FreeSlots.HasValue || result.FreeSlots.Value < MinFreeSlots.Value))
                return false;

            // Requiere slots libres
            if (RequireFreeSlots && (!result.FreeSlots.HasValue || result.FreeSlots.Value == 0))
                return false;

            // Cola máxima
            if (MaxQueueLength.HasValue && result.QueueLength > MaxQueueLength.Value)
                return false;

            // Archivos bloqueados
            if (ExcludeLockedFiles && result.IsLocked)
                return false;

            // Usuarios bloqueados
            if (BlockedUsers != null && BlockedUsers.Any(u => u.Equals(result.Username, StringComparison.OrdinalIgnoreCase)))
                return false;

            return true;
        }
    }


    /// <summary>
    /// Filtros basados en reputación
    /// </summary>
    public class ReputationFilters
    {
        public double? MinScore { get; set; }
        public bool ExcludeBannedSources { get; set; } = true;
        public bool PreferHighReputation { get; set; }
        public int? MinSuccessfulDownloads { get; set; }

        public bool Passes(SearchResult result, SourceReputationSystem reputationSystem)
        {
            if (reputationSystem == null)
                return true;

            var reputation = reputationSystem.GetReputation(result.NetworkSource, result.Username);
            if (reputation == null)
                return true; // Sin historial, permitir

            // Excluir fuentes baneadas
            if (ExcludeBannedSources && reputation.IsBanned)
                return false;

            // Score mínimo
            if (MinScore.HasValue && reputation.Score < MinScore.Value)
                return false;

            // Descargas exitosas mínimas
            if (MinSuccessfulDownloads.HasValue && reputation.SuccessfulDownloads < MinSuccessfulDownloads.Value)
                return false;

            return true;
        }
    }

    /// <summary>
    /// Builder para crear filtros de forma fluida
    /// </summary>
    public class SearchFilterBuilder
    {
        private readonly AdvancedSearchFilters _filters = new AdvancedSearchFilters();

        public SearchFilterBuilder WithSizeRange(long? minBytes, long? maxBytes)
        {
            _filters.MinSizeBytes = minBytes;
            _filters.MaxSizeBytes = maxBytes;
            return this;
        }

        public SearchFilterBuilder WithExtensions(params string[] extensions)
        {
            _filters.FileExtensions = extensions.ToList();
            return this;
        }

        public SearchFilterBuilder ExcludeKeywords(params string[] keywords)
        {
            _filters.ExcludeKeywords = keywords.ToList();
            return this;
        }

        public SearchFilterBuilder WithMinBitrate(int bitrate)
        {
            _filters.MinBitrate = bitrate;
            return this;
        }

        public SearchFilterBuilder SoulseekMinFreeSlots(int slots)
        {
            _filters.Soulseek.MinFreeSlots = slots;
            return this;
        }

        public SearchFilterBuilder SoulseekMaxQueue(int maxQueue)
        {
            _filters.Soulseek.MaxQueueLength = maxQueue;
            return this;
        }

        public SearchFilterBuilder SoulseekRequireFreeSlots()
        {
            _filters.Soulseek.RequireFreeSlots = true;
            return this;
        }

        public SearchFilterBuilder SoulseekBlockUser(params string[] users)
        {
            _filters.Soulseek.BlockedUsers = users.ToList();
            return this;
        }


        public SearchFilterBuilder ReputationMinScore(double score)
        {
            _filters.Reputation.MinScore = score;
            return this;
        }

        public SearchFilterBuilder ReputationExcludeBanned()
        {
            _filters.Reputation.ExcludeBannedSources = true;
            return this;
        }

        public AdvancedSearchFilters Build()
        {
            return _filters;
        }
    }
}
