using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SlskDown.Models;

namespace SlskDown.Core
{
    /// <summary>
    /// Análisis inteligente de contenido con detección de duplicados, clasificación y sugerencias
    /// </summary>
    public class ContentAnalyzer
    {
        // Callbacks
        public Action<string> OnLog { get; set; }
        
        #region Detección de Duplicados Similares
        
        /// <summary>
        /// Encuentra duplicados similares usando Levenshtein distance
        /// </summary>
        public List<DuplicateGroup> FindSimilarDuplicates(List<AutoSearchFileResult> files, double similarityThreshold = 0.85)
        {
            var groups = new List<DuplicateGroup>();
            var processed = new HashSet<string>();
            
            foreach (var file in files)
            {
                string key = $"{file.FileName}_{file.SizeBytes}";
                if (processed.Contains(key)) continue;
                
                var group = new DuplicateGroup { MasterFile = file };
                
                foreach (var other in files)
                {
                    if (file == other) continue;
                    
                    string otherKey = $"{other.FileName}_{other.SizeBytes}";
                    if (processed.Contains(otherKey)) continue;
                    
                    double similarity = CalculateSimilarity(file.FileName, other.FileName);
                    
                    if (similarity >= similarityThreshold)
                    {
                        group.SimilarFiles.Add(other);
                        processed.Add(otherKey);
                    }
                }
                
                if (group.SimilarFiles.Count > 0)
                {
                    groups.Add(group);
                    processed.Add(key);
                }
            }
            
            Log($"Encontrados {groups.Count} grupos de duplicados similares");
            return groups;
        }
        
        /// <summary>
        /// Calcula similitud entre dos strings usando Levenshtein distance
        /// </summary>
        private double CalculateSimilarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
                return 0;
            
            // Normalizar
            s1 = NormalizeFileName(s1);
            s2 = NormalizeFileName(s2);
            
            int distance = LevenshteinDistance(s1, s2);
            int maxLength = Math.Max(s1.Length, s2.Length);
            
            return 1.0 - (distance / (double)maxLength);
        }
        
        /// <summary>
        /// Calcula Levenshtein distance entre dos strings
        /// </summary>
        private int LevenshteinDistance(string s1, string s2)
        {
            int[,] d = new int[s1.Length + 1, s2.Length + 1];
            
            for (int i = 0; i <= s1.Length; i++)
                d[i, 0] = i;
            
            for (int j = 0; j <= s2.Length; j++)
                d[0, j] = j;
            
            for (int j = 1; j <= s2.Length; j++)
            {
                for (int i = 1; i <= s1.Length; i++)
                {
                    int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                    
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost
                    );
                }
            }
            
            return d[s1.Length, s2.Length];
        }
        
        #endregion
        
        #region Clasificación Automática
        
        /// <summary>
        /// Clasifica archivos por género/categoría
        /// </summary>
        public Dictionary<string, List<AutoSearchFileResult>> ClassifyByGenre(List<AutoSearchFileResult> files)
        {
            var classified = new Dictionary<string, List<AutoSearchFileResult>>();
            
            var genres = new Dictionary<string, string[]>
            {
                ["Rock"] = new[] { "rock", "metal", "punk", "grunge", "alternative" },
                ["Pop"] = new[] { "pop", "dance", "disco", "electro" },
                ["Jazz"] = new[] { "jazz", "blues", "swing", "bebop" },
                ["Classical"] = new[] { "classical", "symphony", "concerto", "opera", "baroque" },
                ["Electronic"] = new[] { "techno", "house", "trance", "dubstep", "edm" },
                ["Hip Hop"] = new[] { "hip hop", "rap", "trap", "r&b", "rnb" },
                ["Country"] = new[] { "country", "folk", "bluegrass", "americana" },
                ["Latin"] = new[] { "salsa", "reggaeton", "bachata", "merengue", "cumbia" },
                ["Soundtrack"] = new[] { "soundtrack", "ost", "score", "theme" },
                ["Audiobook"] = new[] { "audiobook", "audiolibro", "narrated", "unabridged" }
            };
            
            foreach (var file in files)
            {
                string fileName = file.FileName.ToLower();
                bool categorized = false;
                
                foreach (var genre in genres)
                {
                    if (genre.Value.Any(keyword => fileName.Contains(keyword)))
                    {
                        if (!classified.ContainsKey(genre.Key))
                            classified[genre.Key] = new List<AutoSearchFileResult>();
                        
                        classified[genre.Key].Add(file);
                        categorized = true;
                        break;
                    }
                }
                
                if (!categorized)
                {
                    if (!classified.ContainsKey("Other"))
                        classified["Other"] = new List<AutoSearchFileResult>();
                    
                    classified["Other"].Add(file);
                }
            }
            
            Log($"Clasificados en {classified.Count} géneros");
            return classified;
        }
        
        /// <summary>
        /// Detecta calidad de audio por nombre de archivo
        /// </summary>
        public AudioQuality DetectAudioQuality(string fileName)
        {
            fileName = fileName.ToLower();
            
            if (fileName.Contains("flac") || fileName.Contains("lossless"))
                return AudioQuality.Lossless;
            
            if (fileName.Contains("320") || fileName.Contains("320kbps"))
                return AudioQuality.High320;
            
            if (fileName.Contains("256") || fileName.Contains("256kbps"))
                return AudioQuality.High256;
            
            if (fileName.Contains("192") || fileName.Contains("192kbps"))
                return AudioQuality.Medium192;
            
            if (fileName.Contains("128") || fileName.Contains("128kbps"))
                return AudioQuality.Medium128;
            
            if (fileName.Contains("96") || fileName.Contains("96kbps") || fileName.Contains("64"))
                return AudioQuality.Low;
            
            return AudioQuality.Unknown;
        }
        
        #endregion
        
        #region Sugerencias Inteligentes
        
        /// <summary>
        /// Genera sugerencias de búsquedas relacionadas
        /// </summary>
        public List<string> GenerateRelatedSearches(string query, List<AutoSearchFileResult> recentDownloads)
        {
            var suggestions = new HashSet<string>();
            
            // Extraer artista del query
            string artist = ExtractArtist(query);
            
            if (!string.IsNullOrEmpty(artist))
            {
                // Sugerir álbumes del mismo artista
                var artistFiles = recentDownloads
                    .Where(f => f.Author != null && f.Author.Equals(artist, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                var albums = artistFiles
                    .Select(f => ExtractAlbum(f.FileName))
                    .Where(a => !string.IsNullOrEmpty(a))
                    .Distinct()
                    .Take(3);
                
                foreach (var album in albums)
                {
                    suggestions.Add($"{artist} {album}");
                }
                
                // Sugerir "discography"
                suggestions.Add($"{artist} discography");
                suggestions.Add($"{artist} best of");
                suggestions.Add($"{artist} greatest hits");
            }
            
            // Sugerir variaciones del query
            suggestions.Add($"{query} album");
            suggestions.Add($"{query} flac");
            suggestions.Add($"{query} 320");
            
            // Sugerir artistas similares (basado en co-ocurrencia)
            var similarArtists = FindSimilarArtists(artist, recentDownloads);
            foreach (var similar in similarArtists.Take(3))
            {
                suggestions.Add(similar);
            }
            
            Log($"💡 Generadas {suggestions.Count} sugerencias");
            return suggestions.Take(10).ToList();
        }
        
        /// <summary>
        /// Encuentra artistas similares basado en historial
        /// </summary>
        private List<string> FindSimilarArtists(string artist, List<AutoSearchFileResult> files)
        {
            if (string.IsNullOrEmpty(artist))
                return new List<string>();
            
            // Encontrar usuarios que tienen archivos de este artista
            var usersWithArtist = files
                .Where(f => f.Author != null && f.Author.Equals(artist, StringComparison.OrdinalIgnoreCase))
                .Select(f => f.Username)
                .Distinct()
                .ToList();
            
            // Encontrar otros artistas que tienen esos mismos usuarios
            var otherArtists = files
                .Where(f => usersWithArtist.Contains(f.Username))
                .Where(f => f.Author != null && !f.Author.Equals(artist, StringComparison.OrdinalIgnoreCase))
                .GroupBy(f => f.Author)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .ToList();
            
            return otherArtists;
        }
        
        #endregion
        
        #region Detección de Calidad
        
        /// <summary>
        /// Analiza calidad general de un archivo
        /// </summary>
        public FileQualityScore AnalyzeFileQuality(AutoSearchFileResult file)
        {
            var score = new FileQualityScore { FileName = file.FileName };
            
            // Factor 1: Calidad de audio (40%)
            var audioQuality = DetectAudioQuality(file.FileName);
            score.AudioQualityScore = audioQuality switch
            {
                AudioQuality.Lossless => 100,
                AudioQuality.High320 => 90,
                AudioQuality.High256 => 80,
                AudioQuality.Medium192 => 60,
                AudioQuality.Medium128 => 40,
                AudioQuality.Low => 20,
                _ => 50
            };
            
            // Factor 2: Tamaño apropiado (30%)
            long expectedSize = EstimateExpectedSize(file.FileName, audioQuality);
            double sizeRatio = file.SizeBytes / (double)expectedSize;
            score.SizeScore = sizeRatio >= 0.8 && sizeRatio <= 1.2 ? 100 : 50;
            
            // Factor 3: Nombre limpio (20%)
            score.NameQualityScore = AnalyzeNameQuality(file.FileName);
            
            // Factor 4: Metadata presente (10%)
            score.MetadataScore = HasMetadata(file.FileName) ? 100 : 0;
            
            // Score total
            score.TotalScore = 
                (score.AudioQualityScore * 0.4) +
                (score.SizeScore * 0.3) +
                (score.NameQualityScore * 0.2) +
                (score.MetadataScore * 0.1);
            
            return score;
        }
        
        private long EstimateExpectedSize(string fileName, AudioQuality quality)
        {
            // Estimar duración (3-5 minutos promedio para una canción)
            int durationSeconds = 240;
            
            int bitrate = quality switch
            {
                AudioQuality.Lossless => 1000,
                AudioQuality.High320 => 320,
                AudioQuality.High256 => 256,
                AudioQuality.Medium192 => 192,
                AudioQuality.Medium128 => 128,
                AudioQuality.Low => 96,
                _ => 192
            };
            
            return (long)(bitrate * 1000 / 8 * durationSeconds);
        }
        
        private int AnalyzeNameQuality(string fileName)
        {
            int score = 100;
            
            // Penalizar caracteres extraños
            if (Regex.IsMatch(fileName, @"[^\w\s\-\.\(\)\[\]]"))
                score -= 20;
            
            // Penalizar nombres muy cortos o muy largos
            if (fileName.Length < 10 || fileName.Length > 150)
                score -= 10;
            
            // Bonificar si tiene estructura clara
            if (Regex.IsMatch(fileName, @"\w+ - \w+"))
                score += 10;
            
            return Math.Max(0, Math.Min(100, score));
        }
        
        private bool HasMetadata(string fileName)
        {
            // Detectar si tiene información de artista, álbum, año, etc.
            return Regex.IsMatch(fileName, @"\d{4}") || // Año
                   Regex.IsMatch(fileName, @" - ") ||    // Separador artista-título
                   fileName.Contains("[") ||              // Tags
                   fileName.Contains("(");                // Info adicional
        }
        
        #endregion
        
        #region Utilidades
        
        private string NormalizeFileName(string fileName)
        {
            // Eliminar extensión
            fileName = System.IO.Path.GetFileNameWithoutExtension(fileName);
            
            // Convertir a minúsculas
            fileName = fileName.ToLower();
            
            // Eliminar caracteres especiales
            fileName = Regex.Replace(fileName, @"[^\w\s]", " ");
            
            // Normalizar espacios
            fileName = Regex.Replace(fileName, @"\s+", " ").Trim();
            
            return fileName;
        }
        
        private string ExtractArtist(string query)
        {
            // Intentar extraer artista de formato "Artista - Canción"
            var match = Regex.Match(query, @"^([^-]+)\s*-");
            if (match.Success)
                return match.Groups[1].Value.Trim();
            
            // Si no hay guión, asumir que es el artista
            return query.Split(' ').FirstOrDefault() ?? "";
        }
        
        private string ExtractAlbum(string fileName)
        {
            // Buscar texto entre corchetes o paréntesis
            var match = Regex.Match(fileName, @"[\[\(]([^\]\)]+)[\]\)]");
            if (match.Success)
                return match.Groups[1].Value.Trim();
            
            return "";
        }
        
        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
        
        #endregion
    }
    
    #region Modelos
    
    public class DuplicateGroup
    {
        public AutoSearchFileResult MasterFile { get; set; }
        public List<AutoSearchFileResult> SimilarFiles { get; set; } = new List<AutoSearchFileResult>();
    }
    
    public enum AudioQuality
    {
        Unknown,
        Low,
        Medium128,
        Medium192,
        High256,
        High320,
        Lossless
    }
    
    public class FileQualityScore
    {
        public string FileName { get; set; }
        public double AudioQualityScore { get; set; }
        public double SizeScore { get; set; }
        public double NameQualityScore { get; set; }
        public double MetadataScore { get; set; }
        public double TotalScore { get; set; }
        
        public string GetGrade()
        {
            if (TotalScore >= 90) return "A+ Excelente";
            if (TotalScore >= 80) return "A Muy Bueno";
            if (TotalScore >= 70) return "B Bueno";
            if (TotalScore >= 60) return "C Regular";
            return "D Pobre";
        }
    }
    
    #endregion
}
