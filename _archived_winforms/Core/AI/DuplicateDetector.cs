using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SlskDown.Core.AI
{
    public class DuplicateGroup
    {
        public List<string> Files { get; set; } = new List<string>();
        public string NormalizedName { get; set; }
        public double Similarity { get; set; }
    }

    /// <summary>
    /// Detector inteligente de duplicados aunque tengan nombres diferentes
    /// </summary>
    public class DuplicateDetector
    {
        public List<DuplicateGroup> FindDuplicates(List<string> filenames, double similarityThreshold = 0.8)
        {
            var groups = new List<DuplicateGroup>();
            var processed = new HashSet<string>();

            foreach (var file in filenames)
            {
                if (processed.Contains(file))
                    continue;

                var normalized = NormalizeFilename(file);
                var duplicates = new List<string> { file };
                processed.Add(file);

                // Buscar archivos similares
                foreach (var other in filenames)
                {
                    if (processed.Contains(other))
                        continue;

                    var otherNormalized = NormalizeFilename(other);
                    var similarity = CalculateSimilarity(normalized, otherNormalized);

                    if (similarity >= similarityThreshold)
                    {
                        duplicates.Add(other);
                        processed.Add(other);
                    }
                }

                if (duplicates.Count > 1)
                {
                    groups.Add(new DuplicateGroup
                    {
                        Files = duplicates,
                        NormalizedName = normalized,
                        Similarity = 1.0
                    });
                }
            }

            return groups;
        }

        private string NormalizeFilename(string filename)
        {
            // Remover extensión
            var nameOnly = System.IO.Path.GetFileNameWithoutExtension(filename);

            // Convertir a minúsculas
            var normalized = nameOnly.ToLower();

            // Remover caracteres especiales
            normalized = Regex.Replace(normalized, @"[^\w\s]", " ");

            // Remover números de volumen/parte
            normalized = Regex.Replace(normalized, @"\b(vol|volume|tomo|parte|part|book)\s*\d+\b", "", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"\b\d{4}\b", ""); // Años

            // Remover palabras comunes
            var noiseWords = new[] { "the", "el", "la", "los", "las", "de", "del", "epub", "pdf", "mobi", "retail", "spanish", "español" };
            foreach (var word in noiseWords)
            {
                normalized = Regex.Replace(normalized, $@"\b{word}\b", "", RegexOptions.IgnoreCase);
            }

            // Normalizar espacios
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

            return normalized;
        }

        private double CalculateSimilarity(string str1, string str2)
        {
            if (str1 == str2)
                return 1.0;

            // Levenshtein distance simplificado
            var longer = str1.Length > str2.Length ? str1 : str2;
            var shorter = str1.Length > str2.Length ? str2 : str1;

            if (longer.Length == 0)
                return 1.0;

            // Calcular distancia
            var distance = LevenshteinDistance(str1, str2);
            return 1.0 - (distance / (double)longer.Length);
        }

        private int LevenshteinDistance(string s1, string s2)
        {
            var len1 = s1.Length;
            var len2 = s2.Length;
            var matrix = new int[len1 + 1, len2 + 1];

            for (int i = 0; i <= len1; i++)
                matrix[i, 0] = i;

            for (int j = 0; j <= len2; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= len1; i++)
            {
                for (int j = 1; j <= len2; j++)
                {
                    var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost
                    );
                }
            }

            return matrix[len1, len2];
        }

        public string GenerateDuplicateReport(List<DuplicateGroup> duplicates)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"🔍 DUPLICADOS DETECTADOS: {duplicates.Count} grupos\n");

            foreach (var group in duplicates.OrderByDescending(g => g.Files.Count))
            {
                sb.AppendLine($"📚 Grupo: {group.NormalizedName}");
                sb.AppendLine($"   Archivos: {group.Files.Count}");
                
                foreach (var file in group.Files)
                {
                    sb.AppendLine($"   • {file}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("💡 SUGERENCIA:");
            sb.AppendLine("Revisa estos grupos y elimina las versiones que no necesites.");

            return sb.ToString();
        }

        public bool IsProbablyDuplicate(string file1, string file2, double threshold = 0.8)
        {
            var norm1 = NormalizeFilename(file1);
            var norm2 = NormalizeFilename(file2);
            return CalculateSimilarity(norm1, norm2) >= threshold;
        }
    }
}
