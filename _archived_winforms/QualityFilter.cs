using System;
using System.Collections.Generic;
using System.Linq;
using SlskDown.UI;

namespace SlskDown
{
    /// <summary>
    /// Filtro de calidad para detectar archivos de baja calidad
    /// </summary>
    public class QualityFilter
    {
        // Palabras que indican baja calidad
        private static readonly string[] lowQualityKeywords = new[]
        {
            "sample", "preview", "demo", "test", "prueba",
            "incomplete", "incompleto", "partial", "parcial",
            "low quality", "baja calidad", "bad quality",
            "corrupted", "corrupto", "damaged", "dañado",
            "raw scan", "scan sin procesar", "unedited"
        };
        
        // Palabras que indican alta calidad
        private static readonly string[] highQualityKeywords = new[]
        {
            "retail", "oficial", "official", "editorial",
            "hd", "high quality", "alta calidad", "remaster",
            "complete", "completo", "full", "integral",
            "premium", "professional", "profesional"
        };
        
        // Formatos preferidos por tipo
        private static readonly Dictionary<string, string[]> preferredFormats = new Dictionary<string, string[]>
        {
            { "ebook", new[] { ".epub", ".mobi", ".azw3", ".pdf", ".txt" } },
            { "comic", new[] { ".cbz", ".cbr", ".cb7", ".pdf" } },
            { "audio", new[] { ".flac", ".m4a", ".mp3", ".ogg" } },
            { "video", new[] { ".mkv", ".mp4", ".avi", ".webm" } }
        };
        
        public static QualityScore EvaluateQuality(SearchResultItem item)
        {
            int score = 100; // Puntuación base
            var reasons = new List<string>();
            
            var lowerName = item.Filename.ToLower();
            var ext = item.Extension.ToLower();
            
            // 1. Tamaño sospechoso
            if (item.Size < 50 * 1024) // < 50 KB
            {
                score -= 60;
                reasons.Add("Tamaño muy pequeño (< 50 KB)");
            }
            else if (item.Size < 100 * 1024) // < 100 KB
            {
                score -= 40;
                reasons.Add("Tamaño pequeño (< 100 KB)");
            }
            else if (item.Size > 500 * 1024 * 1024) // > 500 MB
            {
                score -= 20;
                reasons.Add("Tamaño muy grande (> 500 MB)");
            }
            
            // 2. Palabras de baja calidad
            foreach (var keyword in lowQualityKeywords)
            {
                if (lowerName.Contains(keyword))
                {
                    score -= 50;
                    reasons.Add($"Palabra clave de baja calidad: '{keyword}'");
                    break; // Solo penalizar una vez
                }
            }
            
            // 3. Palabras de alta calidad
            foreach (var keyword in highQualityKeywords)
            {
                if (lowerName.Contains(keyword))
                {
                    score += 15;
                    reasons.Add($"Palabra clave de alta calidad: '{keyword}'");
                    break; // Solo bonificar una vez
                }
            }
            
            // 4. Formato preferido (ebooks)
            if (IsEbook(ext))
            {
                var ebookFormats = preferredFormats["ebook"];
                int formatIndex = Array.IndexOf(ebookFormats, ext);
                if (formatIndex >= 0)
                {
                    // EPUB es el mejor (índice 0), PDF es peor (índice 3)
                    int bonus = (ebookFormats.Length - formatIndex) * 3;
                    score += bonus;
                    reasons.Add($"Formato preferido: {ext} (+{bonus})");
                }
            }
            
            // 5. Formato preferido (comics)
            if (IsComic(ext))
            {
                var comicFormats = preferredFormats["comic"];
                int formatIndex = Array.IndexOf(comicFormats, ext);
                if (formatIndex >= 0)
                {
                    int bonus = (comicFormats.Length - formatIndex) * 3;
                    score += bonus;
                    reasons.Add($"Formato preferido: {ext} (+{bonus})");
                }
            }
            
            // 6. Bitrate para audio
            if (IsAudio(ext) && item.Bitrate > 0)
            {
                if (item.Bitrate >= 320)
                {
                    score += 15;
                    reasons.Add($"Alta calidad de audio: {item.Bitrate} kbps");
                }
                else if (item.Bitrate >= 192)
                {
                    score += 5;
                    reasons.Add($"Calidad media de audio: {item.Bitrate} kbps");
                }
                else if (item.Bitrate < 128)
                {
                    score -= 15;
                    reasons.Add($"Baja calidad de audio: {item.Bitrate} kbps");
                }
            }
            
            // 7. Extensión FLAC (máxima calidad audio)
            if (ext == ".flac")
            {
                score += 20;
                reasons.Add("Formato FLAC (sin pérdida)");
            }
            
            // 8. Nombres sospechosos (números aleatorios, caracteres raros)
            if (System.Text.RegularExpressions.Regex.IsMatch(lowerName, @"[0-9]{8,}"))
            {
                score -= 10;
                reasons.Add("Nombre sospechoso (muchos números)");
            }
            
            // Limitar score entre 0 y 100
            score = Math.Max(0, Math.Min(100, score));
            
            return new QualityScore
            {
                Score = score,
                Reasons = reasons,
                Category = GetQualityCategory(score)
            };
        }
        
        private static string GetQualityCategory(int score)
        {
            if (score >= 90) return "Excelente";
            if (score >= 75) return "Muy Buena";
            if (score >= 60) return "Buena";
            if (score >= 40) return "Regular";
            if (score >= 20) return "Baja";
            return "Muy Baja";
        }
        
        private static bool IsEbook(string ext)
        {
            return ext == ".epub" || ext == ".mobi" || ext == ".azw3" || 
                   ext == ".pdf" || ext == ".txt" || ext == ".djvu";
        }
        
        private static bool IsComic(string ext)
        {
            return ext == ".cbz" || ext == ".cbr" || ext == ".cb7" || 
                   ext == ".cbt" || ext == ".cba";
        }
        
        private static bool IsAudio(string ext)
        {
            return ext == ".mp3" || ext == ".flac" || ext == ".m4a" || 
                   ext == ".ogg" || ext == ".wma" || ext == ".aac";
        }
    }
    
    public class QualityScore
    {
        public int Score { get; set; }
        public List<string> Reasons { get; set; }
        public string Category { get; set; }
        
        public string GetStars()
        {
            int stars = Score / 20; // 0-5 estrellas
            return new string('⭐', stars);
        }
        
        public override string ToString()
        {
            return $"{Category} ({Score}/100) {GetStars()}";
        }
    }
}
