using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SlskDown.Core
{
    /// <summary>
    /// Item de wishlist inteligente con filtros persistentes y descarte de resultados
    /// </summary>
    public class IntelligentWishlistItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("searchQuery")]
        public string SearchQuery { get; set; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("filterId")]
        public string FilterId { get; set; }

        [JsonPropertyName("customFilter")]
        public SavedSearchFilter CustomFilter { get; set; }

        [JsonPropertyName("dismissedResultHashes")]
        public HashSet<string> DismissedResultHashes { get; set; } = new HashSet<string>();

        [JsonPropertyName("notifyOnNewResults")]
        public bool NotifyOnNewResults { get; set; } = true;

        [JsonPropertyName("lastSearchTime")]
        public DateTime LastSearchTime { get; set; } = DateTime.MinValue;

        [JsonPropertyName("lastNotificationTime")]
        public DateTime LastNotificationTime { get; set; } = DateTime.MinValue;

        [JsonPropertyName("newResultsCount")]
        public int NewResultsCount { get; set; }

        [JsonPropertyName("totalResultsFound")]
        public int TotalResultsFound { get; set; }

        [JsonPropertyName("created")]
        public DateTime Created { get; set; } = DateTime.Now;

        [JsonPropertyName("searchIntervalMinutes")]
        public int SearchIntervalMinutes { get; set; } = 60;

        [JsonPropertyName("notes")]
        public string Notes { get; set; }

        /// <summary>
        /// Verifica si un resultado debe ser mostrado (no está descartado)
        /// </summary>
        public bool ShouldShowResult(string resultHash)
        {
            return !DismissedResultHashes.Contains(resultHash);
        }

        /// <summary>
        /// Descarta un resultado permanentemente
        /// </summary>
        public void DismissResult(string resultHash)
        {
            DismissedResultHashes.Add(resultHash);
        }

        /// <summary>
        /// Restaura un resultado descartado
        /// </summary>
        public void RestoreResult(string resultHash)
        {
            DismissedResultHashes.Remove(resultHash);
        }

        /// <summary>
        /// Limpia resultados descartados antiguos (más de 30 días)
        /// </summary>
        public void CleanupOldDismissedResults()
        {
            // Por ahora mantenemos todos los descartados
            // En el futuro podríamos implementar limpieza basada en tiempo
        }

        /// <summary>
        /// Verifica si es momento de buscar de nuevo
        /// </summary>
        public bool ShouldSearchNow()
        {
            if (!Enabled)
                return false;

            var timeSinceLastSearch = DateTime.Now - LastSearchTime;
            return timeSinceLastSearch.TotalMinutes >= SearchIntervalMinutes;
        }

        /// <summary>
        /// Genera un hash único para un resultado de búsqueda
        /// </summary>
        public static string GenerateResultHash(string username, string filename, long sizeBytes)
        {
            var combined = $"{username}|{filename}|{sizeBytes}";
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(combined);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// Obtiene el filtro efectivo (custom o referenciado)
        /// </summary>
        public SavedSearchFilter GetEffectiveFilter(FilterPresetManager presetManager)
        {
            if (CustomFilter != null)
                return CustomFilter;

            if (!string.IsNullOrEmpty(FilterId) && presetManager != null)
                return presetManager.GetPreset(FilterId);

            return null;
        }

        public override string ToString()
        {
            var status = Enabled ? "✓" : "✗";
            var newResults = NewResultsCount > 0 ? $" ({NewResultsCount} nuevos)" : "";
            return $"{status} {SearchQuery}{newResults}";
        }
    }

    /// <summary>
    /// Resultado de búsqueda de wishlist con información adicional
    /// </summary>
    public class WishlistSearchResult
    {
        public string WishlistItemId { get; set; }
        public string Username { get; set; }
        public string Filename { get; set; }
        public long SizeBytes { get; set; }
        public int? Bitrate { get; set; }
        public bool HasFreeSlot { get; set; }
        public int? UploadSpeed { get; set; }
        public DateTime FoundTime { get; set; } = DateTime.Now;
        public bool IsNew { get; set; }
        public string ResultHash { get; set; }

        public WishlistSearchResult()
        {
            ResultHash = IntelligentWishlistItem.GenerateResultHash(Username, Filename, SizeBytes);
        }
    }
}
