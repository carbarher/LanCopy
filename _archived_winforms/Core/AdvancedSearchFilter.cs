using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SlskDown.Core
{
    public class AdvancedSearchFilter
    {
        public long? MinSize { get; set; }
        public long? MaxSize { get; set; }
        public string[] AllowedExtensions { get; set; }
        public int? MinAvailability { get; set; }
        
        public int? MinSources { get; set; }
        public int? MaxSources { get; set; }
        public string[] RequiredKeywords { get; set; }
        public string[] ExcludedKeywords { get; set; }
        public FileType? FileType { get; set; }
        public int? MinBitrate { get; set; }
        public int? MinLength { get; set; }
        
        public bool ExcludeFakes { get; set; }
        public bool ExcludeLowQuality { get; set; }
        public bool PreferCompleteFiles { get; set; }
        public bool SpanishOnly { get; set; }
        
        private readonly Action<string> _onLog;

        public AdvancedSearchFilter(Action<string> onLog = null)
        {
            _onLog = onLog;
            ExcludeFakes = true;
            ExcludeLowQuality = false;
            PreferCompleteFiles = false;
        }

        public bool Matches(SearchResultItem result, out string rejectionReason)
        {
            rejectionReason = null;

            if (MinSize.HasValue && result.Size < MinSize.Value)
            {
                rejectionReason = $"Tamaño {result.Size} < mínimo {MinSize.Value}";
                return false;
            }

            if (MaxSize.HasValue && result.Size > MaxSize.Value)
            {
                rejectionReason = $"Tamaño {result.Size} > máximo {MaxSize.Value}";
                return false;
            }

            if (AllowedExtensions?.Length > 0)
            {
                var ext = Path.GetExtension(result.Filename)?.TrimStart('.').ToLowerInvariant();
                if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                {
                    rejectionReason = $"Extensión '{ext}' no permitida";
                    return false;
                }
            }

            // SourceCount no está disponible en SearchResultItem
            // if (MinAvailability.HasValue && result.SourceCount < MinAvailability.Value)
            // {
            //     rejectionReason = $"Disponibilidad {result.SourceCount} < mínimo {MinAvailability.Value}";
            //     return false;
            // }

            // if (MinSources.HasValue && result.SourceCount < MinSources.Value)
            // {
            //     rejectionReason = $"Fuentes {result.SourceCount} < mínimo {MinSources.Value}";
            //     return false;
            // }

            // if (MaxSources.HasValue && result.SourceCount > MaxSources.Value)
            // {
            //     rejectionReason = $"Fuentes {result.SourceCount} > máximo {MaxSources.Value}";
            //     return false;
            // }

            if (RequiredKeywords?.Length > 0)
            {
                var filename = result.Filename.ToLowerInvariant();
                var missingKeywords = RequiredKeywords.Where(kw => !filename.Contains(kw.ToLowerInvariant())).ToArray();
                if (missingKeywords.Length > 0)
                {
                    rejectionReason = $"Faltan keywords requeridas: {string.Join(", ", missingKeywords)}";
                    return false;
                }
            }

            if (ExcludedKeywords?.Length > 0)
            {
                var filename = result.Filename.ToLowerInvariant();
                var foundExcluded = ExcludedKeywords.FirstOrDefault(kw => filename.Contains(kw.ToLowerInvariant()));
                if (foundExcluded != null)
                {
                    rejectionReason = $"Contiene keyword excluida: {foundExcluded}";
                    return false;
                }
            }

            if (ExcludeFakes && IsProbablyFake(result, out var fakeReason))
            {
                rejectionReason = $"Detectado como fake: {fakeReason}";
                return false;
            }

            if (ExcludeLowQuality && IsProbablyLowQuality(result, out var qualityReason))
            {
                rejectionReason = $"Calidad baja: {qualityReason}";
                return false;
            }

            return true;
        }

        private bool IsProbablyFake(SearchResultItem result, out string reason)
        {
            reason = null;
            var filename = result.Filename.ToLowerInvariant();
            var ext = Path.GetExtension(filename).TrimStart('.');

            if (result.Size < 1024 * 1024)
            {
                var videoExts = new[] { "avi", "mkv", "mp4", "mov", "wmv", "flv", "webm" };
                if (videoExts.Contains(ext))
                {
                    reason = "Video demasiado pequeño (< 1 MB)";
                    return true;
                }
            }

            if (result.Size < 100 * 1024)
            {
                var audioExts = new[] { "mp3", "flac", "wav", "ogg", "m4a", "aac" };
                if (audioExts.Contains(ext))
                {
                    reason = "Audio demasiado pequeño (< 100 KB)";
                    return true;
                }
            }

            if (Regex.IsMatch(filename, @"\.(exe|scr|bat|com|pif|vbs|js)\.(avi|mkv|mp3|pdf|epub|mobi|doc|docx)$"))
            {
                reason = "Doble extensión sospechosa (ejecutable disfrazado)";
                return true;
            }

            var executableExts = new[] { "exe", "scr", "bat", "com", "pif", "vbs", "js", "msi", "dll" };
            if (executableExts.Contains(ext))
            {
                var documentKeywords = new[] { "pdf", "epub", "mobi", "book", "libro", "ebook" };
                if (documentKeywords.Any(kw => filename.Contains(kw)))
                {
                    reason = "Ejecutable disfrazado de documento";
                    return true;
                }
            }

            var spamKeywords = new[] { "crack", "keygen", "serial", "patch", "activator", "generator", "free download", "click here" };
            var spamCount = spamKeywords.Count(kw => filename.Contains(kw));
            if (spamCount >= 3)
            {
                reason = $"Demasiadas keywords spam ({spamCount})";
                return true;
            }

            if (Regex.IsMatch(filename, @"(www\.|http|\.com|\.net|\.org){2,}"))
            {
                reason = "Múltiples URLs en nombre (spam)";
                return true;
            }

            if (filename.Count(c => c == '_') > 10 || filename.Count(c => c == '-') > 10)
            {
                reason = "Demasiados separadores (nombre generado automáticamente)";
                return true;
            }

            return false;
        }

        private bool IsProbablyLowQuality(SearchResultItem result, out string reason)
        {
            reason = null;
            var filename = result.Filename.ToLowerInvariant();
            var ext = Path.GetExtension(filename).TrimStart('.');

            var lowQualityIndicators = new[] { "cam", "ts", "tc", "r5", "screener", "dvdscr", "workprint", "sample" };
            var foundIndicator = lowQualityIndicators.FirstOrDefault(ind => filename.Contains(ind));
            if (foundIndicator != null)
            {
                reason = $"Indicador de baja calidad: {foundIndicator}";
                return true;
            }

            if (filename.Contains("128kbps") || filename.Contains("96kbps") || filename.Contains("64kbps"))
            {
                var audioExts = new[] { "mp3", "m4a", "aac", "ogg" };
                if (audioExts.Contains(ext))
                {
                    reason = "Bitrate bajo para audio";
                    return true;
                }
            }

            if (Regex.IsMatch(filename, @"\b(240p|360p|480p)\b"))
            {
                var videoExts = new[] { "avi", "mkv", "mp4", "mov", "wmv" };
                if (videoExts.Contains(ext))
                {
                    reason = "Resolución baja para video";
                    return true;
                }
            }

            return false;
        }

        public FilterStatistics GetStatistics(IEnumerable<SearchResultItem> results)
        {
            var stats = new FilterStatistics();
            
            foreach (var result in results)
            {
                stats.TotalResults++;
                
                if (Matches(result, out var rejectionReason))
                {
                    stats.AcceptedResults++;
                }
                else
                {
                    stats.RejectedResults++;
                    
                    if (rejectionReason != null)
                    {
                        if (!stats.RejectionReasons.ContainsKey(rejectionReason))
                        {
                            stats.RejectionReasons[rejectionReason] = 0;
                        }
                        stats.RejectionReasons[rejectionReason]++;
                    }
                }
            }
            
            return stats;
        }

        public class FilterStatistics
        {
            public int TotalResults { get; set; }
            public int AcceptedResults { get; set; }
            public int RejectedResults { get; set; }
            public Dictionary<string, int> RejectionReasons { get; set; } = new Dictionary<string, int>();
            
            public double AcceptanceRate => TotalResults > 0 ? AcceptedResults / (double)TotalResults : 0;
            
            public override string ToString()
            {
                var result = $"Total: {TotalResults}, Aceptados: {AcceptedResults} ({AcceptanceRate:P0}), Rechazados: {RejectedResults}\n";
                
                if (RejectionReasons.Any())
                {
                    result += "Razones de rechazo:\n";
                    foreach (var kvp in RejectionReasons.OrderByDescending(x => x.Value).Take(5))
                    {
                        result += $"  - {kvp.Key}: {kvp.Value}\n";
                    }
                }
                
                return result;
            }
        }
    }
}
