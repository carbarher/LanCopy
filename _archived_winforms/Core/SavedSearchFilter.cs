using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SlskDown.Core
{
    /// <summary>
    /// Representa un filtro de búsqueda guardado que puede ser reutilizado
    /// </summary>
    public class SavedSearchFilter
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("icon")]
        public string Icon { get; set; } = "🔍";

        [JsonPropertyName("description")]
        public string Description { get; set; }

        // Filtros de formato
        [JsonPropertyName("allowedExtensions")]
        public List<string> AllowedExtensions { get; set; } = new List<string>();

        [JsonPropertyName("excludedExtensions")]
        public List<string> ExcludedExtensions { get; set; } = new List<string>();

        // Filtros de tamaño
        [JsonPropertyName("minSizeBytes")]
        public long? MinSizeBytes { get; set; }

        [JsonPropertyName("maxSizeBytes")]
        public long? MaxSizeBytes { get; set; }

        // Filtros de calidad (audio)
        [JsonPropertyName("minBitrate")]
        public int? MinBitrate { get; set; }

        [JsonPropertyName("maxBitrate")]
        public int? MaxBitrate { get; set; }

        // Filtros de palabras clave
        [JsonPropertyName("requiredKeywords")]
        public List<string> RequiredKeywords { get; set; } = new List<string>();

        [JsonPropertyName("excludedKeywords")]
        public List<string> ExcludedKeywords { get; set; } = new List<string>();

        // Filtros de usuario
        [JsonPropertyName("freeSlotOnly")]
        public bool FreeSlotOnly { get; set; }

        [JsonPropertyName("minUploadSpeed")]
        public int? MinUploadSpeed { get; set; }

        // Metadatos
        [JsonPropertyName("created")]
        public DateTime Created { get; set; } = DateTime.Now;

        [JsonPropertyName("lastUsed")]
        public DateTime LastUsed { get; set; } = DateTime.Now;

        [JsonPropertyName("timesUsed")]
        public int TimesUsed { get; set; }

        /// <summary>
        /// Aplica este filtro a una lista de resultados de búsqueda
        /// </summary>
        public bool MatchesFilter(string filename, long sizeBytes, int? bitrate = null, bool hasFreeSlot = true)
        {
            // Filtro de extensión
            if (AllowedExtensions.Count > 0)
            {
                var ext = System.IO.Path.GetExtension(filename)?.ToLowerInvariant().TrimStart('.');
                if (ext == null || !AllowedExtensions.Contains(ext))
                    return false;
            }

            if (ExcludedExtensions.Count > 0)
            {
                var ext = System.IO.Path.GetExtension(filename)?.ToLowerInvariant().TrimStart('.');
                if (ext != null && ExcludedExtensions.Contains(ext))
                    return false;
            }

            // Filtro de tamaño
            if (MinSizeBytes.HasValue && sizeBytes < MinSizeBytes.Value)
                return false;

            if (MaxSizeBytes.HasValue && sizeBytes > MaxSizeBytes.Value)
                return false;

            // Filtro de bitrate
            if (MinBitrate.HasValue && bitrate.HasValue && bitrate.Value < MinBitrate.Value)
                return false;

            if (MaxBitrate.HasValue && bitrate.HasValue && bitrate.Value > MaxBitrate.Value)
                return false;

            // Filtro de palabras clave requeridas
            var lowerFilename = filename.ToLowerInvariant();
            if (RequiredKeywords.Count > 0)
            {
                foreach (var keyword in RequiredKeywords)
                {
                    if (!lowerFilename.Contains(keyword.ToLowerInvariant()))
                        return false;
                }
            }

            // Filtro de palabras clave excluidas
            if (ExcludedKeywords.Count > 0)
            {
                foreach (var keyword in ExcludedKeywords)
                {
                    if (lowerFilename.Contains(keyword.ToLowerInvariant()))
                        return false;
                }
            }

            // Filtro de slot libre
            if (FreeSlotOnly && !hasFreeSlot)
                return false;

            return true;
        }

        /// <summary>
        /// Crea una copia de este filtro
        /// </summary>
        public SavedSearchFilter Clone()
        {
            return new SavedSearchFilter
            {
                Id = Guid.NewGuid().ToString(),
                Name = Name + " (copia)",
                Icon = Icon,
                Description = Description,
                AllowedExtensions = new List<string>(AllowedExtensions),
                ExcludedExtensions = new List<string>(ExcludedExtensions),
                MinSizeBytes = MinSizeBytes,
                MaxSizeBytes = MaxSizeBytes,
                MinBitrate = MinBitrate,
                MaxBitrate = MaxBitrate,
                RequiredKeywords = new List<string>(RequiredKeywords),
                ExcludedKeywords = new List<string>(ExcludedKeywords),
                FreeSlotOnly = FreeSlotOnly,
                MinUploadSpeed = MinUploadSpeed
            };
        }

        public override string ToString()
        {
            return $"{Icon} {Name}";
        }
    }
}
