using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;

namespace SlskDown.Core.AI
{
    public class MultiSourceResult
    {
        public string FileName { get; set; }
        public long SizeBytes { get; set; }
        public string Source { get; set; } // "Soulseek", "eMule"
        public string Username { get; set; }
        public double EstimatedSpeed { get; set; }
        public int QueuePosition { get; set; }
        public double QualityScore { get; set; }
    }

    public class MultiSourceSearchResults
    {
        public List<MultiSourceResult> AllResults { get; set; } = new List<MultiSourceResult>();
        public Dictionary<string, int> ResultsBySource { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, double> AverageSpeedBySource { get; set; } = new Dictionary<string, double>();
        public List<MultiSourceResult> UniqueToSoulseek { get; set; } = new List<MultiSourceResult>();
        public List<MultiSourceResult> UniqueToEmule { get; set; } = new List<MultiSourceResult>();
        public List<MultiSourceResult> Duplicates { get; set; } = new List<MultiSourceResult>();
        public TimeSpan SearchDuration { get; set; }
    }

    /// <summary>
    /// Búsqueda multi-fuente: Soulseek + eMule en paralelo
    /// </summary>
    public class MultiSourceSearch
    {
        private readonly DuplicateDetector duplicateDetector = new DuplicateDetector();
        private readonly FileQualityAnalyzer qualityAnalyzer = new FileQualityAnalyzer();

        public async Task<MultiSourceSearchResults> SearchAllSourcesAsync(
            string query,
            Func<string, Task<List<dynamic>>> soulseekSearch,
            Func<string, Task<List<dynamic>>> emuleSearch,
            bool enableSoulseek = true,
            bool enableEmule = true)
        {
            var startTime = DateTime.Now;
            var results = new MultiSourceSearchResults();

            var tasks = new List<Task<(string source, List<dynamic> results)>>();

            // Búsqueda en Soulseek
            if (enableSoulseek && soulseekSearch != null)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var soulseekResults = await soulseekSearch(query);
                        return ("Soulseek", soulseekResults ?? new List<dynamic>());
                    }
                    catch
                    {
                        return ("Soulseek", new List<dynamic>());
                    }
                }));
            }

            // Búsqueda en eMule
            if (enableEmule && emuleSearch != null)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var emuleResults = await emuleSearch(query);
                        return ("eMule", emuleResults ?? new List<dynamic>());
                    }
                    catch
                    {
                        return ("eMule", new List<dynamic>());
                    }
                }));
            }

            // Esperar todas las búsquedas en paralelo
            var searchResults = await Task.WhenAll(tasks);

            // Procesar resultados de cada fuente
            foreach (var (source, sourceResults) in searchResults)
            {
                results.ResultsBySource[source] = sourceResults.Count;

                foreach (var result in sourceResults)
                {
                    var multiResult = ConvertToMultiSourceResult(result, source);
                    results.AllResults.Add(multiResult);
                }
            }

            // Calcular velocidades promedio por fuente
            CalculateAverageSpeeds(results);

            // Deduplicar y clasificar
            ClassifyResults(results);

            results.SearchDuration = DateTime.Now - startTime;

            return results;
        }

        private MultiSourceResult ConvertToMultiSourceResult(dynamic result, string source)
        {
            var fileName = result.FileName?.ToString() ?? result.Name?.ToString() ?? "Unknown";
            var sizeBytes = (long)(result.SizeBytes ?? result.Size ?? 0);
            var username = result.Username?.ToString() ?? result.User?.ToString() ?? "Unknown";

            // Analizar calidad
            var quality = qualityAnalyzer.AnalyzeFile(fileName, sizeBytes, username);

            return new MultiSourceResult
            {
                FileName = fileName,
                SizeBytes = sizeBytes,
                Source = source,
                Username = username,
                EstimatedSpeed = EstimateSpeed(source, username),
                QueuePosition = result.QueuePosition ?? 0,
                QualityScore = quality.Score
            };
        }

        private double EstimateSpeed(string source, string username)
        {
            // Estimaciones basadas en estadísticas conocidas
            if (source == "Soulseek")
            {
                // Soulseek típicamente más rápido
                return 2.5 * 1024 * 1024; // 2.5 MB/s promedio
            }
            else if (source == "eMule")
            {
                // eMule típicamente más lento
                return 0.8 * 1024 * 1024; // 800 KB/s promedio
            }

            return 1.0 * 1024 * 1024; // 1 MB/s por defecto
        }

        private void CalculateAverageSpeeds(MultiSourceSearchResults results)
        {
            var bySource = results.AllResults.GroupBy(r => r.Source);

            foreach (var group in bySource)
            {
                var avgSpeed = group.Average(r => r.EstimatedSpeed);
                results.AverageSpeedBySource[group.Key] = avgSpeed;
            }
        }

        private void ClassifyResults(MultiSourceSearchResults results)
        {
            // Agrupar por nombre normalizado
            var fileGroups = results.AllResults
                .GroupBy(r => NormalizeFileName(r.FileName))
                .ToList();

            foreach (var group in fileGroups)
            {
                var files = group.ToList();

                if (files.Count == 1)
                {
                    // Único en su fuente
                    var file = files[0];
                    if (file.Source == "Soulseek")
                        results.UniqueToSoulseek.Add(file);
                    else if (file.Source == "eMule")
                        results.UniqueToEmule.Add(file);
                }
                else
                {
                    // Duplicado entre fuentes - elegir mejor
                    var best = files.OrderByDescending(f => f.QualityScore)
                                   .ThenByDescending(f => f.EstimatedSpeed)
                                   .First();

                    results.Duplicates.Add(best);
                }
            }
        }

        private string NormalizeFileName(string filename)
        {
            return duplicateDetector.GetType()
                .GetMethod("NormalizeFilename", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(duplicateDetector, new object[] { filename })?.ToString() ?? filename.ToLower();
        }

        public string GenerateMultiSourceReport(MultiSourceSearchResults results, string query)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"🔍 BÚSQUEDA MULTI-FUENTE: \"{query}\"\n");
            sb.AppendLine($"⏱️ Tiempo: {results.SearchDuration.TotalSeconds:F1}s\n");

            // Resumen por fuente
            sb.AppendLine("📊 RESULTADOS POR FUENTE:");
            foreach (var kvp in results.ResultsBySource.OrderByDescending(x => x.Value))
            {
                var avgSpeed = results.AverageSpeedBySource.GetValueOrDefault(kvp.Key, 0);
                var speedStr = FormatSpeed(avgSpeed);
                
                sb.AppendLine($"  • {kvp.Key}: {kvp.Value} archivos (promedio: {speedStr})");
            }
            sb.AppendLine();

            // Total y deduplicación
            var totalUnique = results.UniqueToSoulseek.Count + results.UniqueToEmule.Count + results.Duplicates.Count;
            sb.AppendLine($"📦 TOTAL: {results.AllResults.Count} archivos ({totalUnique} únicos)\n");

            // Archivos únicos por fuente
            if (results.UniqueToSoulseek.Count > 0)
            {
                sb.AppendLine($"⭐ EXCLUSIVOS DE SOULSEEK: {results.UniqueToSoulseek.Count}");
                foreach (var file in results.UniqueToSoulseek.Take(3))
                {
                    sb.AppendLine($"  • {file.FileName} ({FormatSize(file.SizeBytes)})");
                }
                if (results.UniqueToSoulseek.Count > 3)
                    sb.AppendLine($"  ... y {results.UniqueToSoulseek.Count - 3} más");
                sb.AppendLine();
            }

            if (results.UniqueToEmule.Count > 0)
            {
                sb.AppendLine($"⭐ EXCLUSIVOS DE EMULE: {results.UniqueToEmule.Count}");
                foreach (var file in results.UniqueToEmule.Take(3))
                {
                    sb.AppendLine($"  • {file.FileName} ({FormatSize(file.SizeBytes)})");
                }
                if (results.UniqueToEmule.Count > 3)
                    sb.AppendLine($"  ... y {results.UniqueToEmule.Count - 3} más");
                sb.AppendLine();
            }

            // Recomendación
            sb.AppendLine("💡 RECOMENDACIÓN:");
            var bestSource = results.ResultsBySource.OrderByDescending(x => x.Value).FirstOrDefault();
            var bestSpeed = results.AverageSpeedBySource.OrderByDescending(x => x.Value).FirstOrDefault();

            if (bestSource.Key == bestSpeed.Key)
            {
                sb.AppendLine($"  {bestSource.Key} tiene más resultados ({bestSource.Value}) y mejor velocidad ({FormatSpeed(bestSpeed.Value)})");
            }
            else
            {
                sb.AppendLine($"  {bestSource.Key} tiene más resultados ({bestSource.Value})");
                sb.AppendLine($"  {bestSpeed.Key} tiene mejor velocidad ({FormatSpeed(bestSpeed.Value)})");
            }

            if (results.UniqueToEmule.Count > 0 && results.UniqueToSoulseek.Count > 0)
            {
                sb.AppendLine($"\n  ✨ Ambas fuentes tienen archivos únicos. Recomiendo buscar en ambas.");
            }

            return sb.ToString();
        }

        public List<MultiSourceResult> GetBestResults(MultiSourceSearchResults results, int count = 10)
        {
            return results.AllResults
                .OrderByDescending(r => r.QualityScore)
                .ThenByDescending(r => r.EstimatedSpeed)
                .Take(count)
                .ToList();
        }

        private string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }

        private string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond < 1024) return $"{bytesPerSecond:F0} B/s";
            if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024:F1} KB/s";
            return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
        }
    }
}
