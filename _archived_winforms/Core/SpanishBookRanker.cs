using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SlskDown.Core
{
    public sealed class SpanishBookRanker
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".epub", ".mobi", ".txt"
        };

        private static readonly HashSet<string> RejectExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".flac", ".wav", ".m4a", ".aac", ".ogg", ".opus",
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm",
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
            ".zip", ".rar", ".7z"
        };

        private static readonly string[] SpanishHints =
        {
            "español", "espanol", "castellano", "spanish", "[esp]", "(esp)", "_esp", "-esp", " esp ", " spa ", " [spa] ", "(spa)"
        };

        private static readonly string[] NonSpanishHints =
        {
            "english", "inglés", "ingles", "français", "francais", "french", "deutsch", "german", "italian", "portuguese"
        };

        private static readonly string[] QualityHints =
        {
            "completo", "completa", "integral", "edicion", "edición", "tomo", "vol", "vol.", "volumen", "coleccion", "colección"
        };

        private static readonly Regex MultiSpaceRegex = new(@"\s+", RegexOptions.Compiled);

        public RankedCandidate Rank(CandidateFileInfo candidate, string? targetAuthor = null, string? targetTitle = null)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));

            var reasons = new List<string>();
            double score = 0;

            var fileName = candidate.FileName ?? string.Empty;
            var ext = NormalizeExtension(candidate.Extension, fileName);

            if (RejectExtensions.Contains(ext))
            {
                return new RankedCandidate
                {
                    File = candidate,
                    Score = 0,
                    Reasons = new List<string> { "-ext_rechazada" }
                };
            }

            if (!AllowedExtensions.Contains(ext))
            {
                return new RankedCandidate
                {
                    File = candidate,
                    Score = 0,
                    Reasons = new List<string> { "-ext_no_libro" }
                };
            }

            score += 30;
            reasons.Add($"+formato:{ext}");

            var normalizedName = NormalizeText(fileName);

            if (ContainsAny(normalizedName, SpanishHints))
            {
                score += 25;
                reasons.Add("+hint_es");
            }
            else
            {
                score += 5;
                reasons.Add("~sin_hint_es");
            }

            if (ContainsAny(normalizedName, NonSpanishHints))
            {
                score -= 30;
                reasons.Add("-hint_no_es");
            }

            if (!string.IsNullOrWhiteSpace(targetAuthor))
            {
                var normAuthor = NormalizeText(targetAuthor);
                if (normalizedName.Contains(normAuthor, StringComparison.OrdinalIgnoreCase))
                {
                    score += 15;
                    reasons.Add("+match_autor");
                }
                else
                {
                    score -= 5;
                    reasons.Add("~no_match_autor");
                }
            }

            if (!string.IsNullOrWhiteSpace(targetTitle))
            {
                var normTitle = NormalizeText(targetTitle);
                if (normTitle.Length >= 4 && normalizedName.Contains(normTitle, StringComparison.OrdinalIgnoreCase))
                {
                    score += 10;
                    reasons.Add("+match_titulo");
                }
            }

            if (ContainsAny(normalizedName, QualityHints))
            {
                score += 10;
                reasons.Add("+hint_calidad");
            }

            score += ScoreBySize(candidate.SizeBytes, reasons);

            if (candidate.FreeUploadSlots > 0)
            {
                score += 5;
                reasons.Add("+slot_libre");
            }

            if (candidate.QueueLength > 0)
            {
                score -= Math.Min(10, candidate.QueueLength);
                reasons.Add("-cola_usuario");
            }

            if (candidate.UploadSpeed > 0)
            {
                var speedBonus = Math.Min(10, candidate.UploadSpeed / 1024.0 / 50.0);
                score += speedBonus;
                reasons.Add("+velocidad");
            }

            score = Math.Max(0, Math.Min(100, score));

            return new RankedCandidate
            {
                File = candidate,
                Score = score,
                Reasons = reasons
            };
        }

        private static double ScoreBySize(long sizeBytes, List<string> reasons)
        {
            if (sizeBytes <= 0)
            {
                reasons.Add("-size_0");
                return -50;
            }

            const long kb50 = 50 * 1024;
            const long mb1 = 1L * 1024 * 1024;
            const long mb20 = 20L * 1024 * 1024;
            const long mb200 = 200L * 1024 * 1024;
            const long mb500 = 500L * 1024 * 1024;

            if (sizeBytes < kb50)
            {
                reasons.Add("-muy_peq");
                return -20;
            }

            if (sizeBytes < mb1)
            {
                reasons.Add("~pequenio");
                return 2;
            }

            if (sizeBytes <= mb20)
            {
                reasons.Add("+size_ok");
                return 15;
            }

            if (sizeBytes <= mb200)
            {
                reasons.Add("~size_grande");
                return 5;
            }

            if (sizeBytes <= mb500)
            {
                reasons.Add("-muy_grande");
                return -10;
            }

            reasons.Add("-enorme");
            return -25;
        }

        private static string NormalizeExtension(string? ext, string fileName)
        {
            if (!string.IsNullOrWhiteSpace(ext))
            {
                return ext.StartsWith('.') ? ext : "." + ext;
            }

            var fromName = Path.GetExtension(fileName);
            return string.IsNullOrWhiteSpace(fromName) ? string.Empty : fromName;
        }

        private static bool ContainsAny(string normalizedText, IEnumerable<string> patterns)
        {
            foreach (var pattern in patterns)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    continue;
                }

                if (normalizedText.Contains(NormalizeText(pattern), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var trimmed = text.Trim();
            trimmed = trimmed.Replace('_', ' ').Replace('-', ' ');
            trimmed = MultiSpaceRegex.Replace(trimmed, " ");
            return trimmed;
        }
    }
}
