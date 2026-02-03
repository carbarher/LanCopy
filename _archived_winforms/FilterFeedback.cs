using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SlskDown
{
    /// <summary>
    /// Sistema de feedback para reportar archivos mal clasificados
    /// </summary>
    public class FilterFeedbackSystem
    {
        private static readonly Lazy<FilterFeedbackSystem> instance =
            new Lazy<FilterFeedbackSystem>(() => new FilterFeedbackSystem());

        public static FilterFeedbackSystem Instance => instance.Value;

        private readonly string feedbackFile;
        private List<FilterFeedback> feedbackList;

        private FilterFeedbackSystem()
        {
            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SlskDown"
            );
            Directory.CreateDirectory(appData);
            feedbackFile = Path.Combine(appData, "filter_feedback.json");
            feedbackList = new List<FilterFeedback>();
            Load();
        }

        /// <summary>
        /// Reporta un falso positivo (archivo que NO debería pasar el filtro)
        /// </summary>
        public void ReportFalsePositive(string filename, string reason = "")
        {
            var feedback = new FilterFeedback
            {
                Filename = filename,
                ReportedAs = "false_positive",
                Timestamp = DateTime.Now,
                UserComment = reason
            };

            feedbackList.Add(feedback);
            Save();
        }

        /// <summary>
        /// Reporta un falso negativo (archivo que SÍ debería pasar el filtro)
        /// </summary>
        public void ReportFalseNegative(string filename, string reason = "")
        {
            var feedback = new FilterFeedback
            {
                Filename = filename,
                ReportedAs = "false_negative",
                Timestamp = DateTime.Now,
                UserComment = reason
            };

            feedbackList.Add(feedback);
            Save();
        }

        /// <summary>
        /// Analiza el feedback y genera sugerencias
        /// </summary>
        public FeedbackAnalysis AnalyzeFeedback()
        {
            var analysis = new FeedbackAnalysis();

            var falsePositives = feedbackList.Where(f => f.ReportedAs == "false_positive").ToList();
            var falseNegatives = feedbackList.Where(f => f.ReportedAs == "false_negative").ToList();

            analysis.TotalFeedback = feedbackList.Count;
            analysis.FalsePositives = falsePositives.Count;
            analysis.FalseNegatives = falseNegatives.Count;

            // Analizar palabras comunes en falsos positivos
            var fpWords = new Dictionary<string, int>();
            foreach (var fp in falsePositives)
            {
                var words = fp.Filename.ToLower()
                    .Split(new[] { ' ', '.', '_', '-', '[', ']', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var word in words)
                {
                    if (word.Length > 2) // Ignorar palabras muy cortas
                    {
                        fpWords[word] = fpWords.GetValueOrDefault(word, 0) + 1;
                    }
                }
            }

            analysis.CommonFalsePositiveWords = fpWords
                .OrderByDescending(kv => kv.Value)
                .Take(10)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            // Analizar palabras comunes en falsos negativos
            var fnWords = new Dictionary<string, int>();
            foreach (var fn in falseNegatives)
            {
                var words = fn.Filename.ToLower()
                    .Split(new[] { ' ', '.', '_', '-', '[', ']', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var word in words)
                {
                    if (word.Length > 2)
                    {
                        fnWords[word] = fnWords.GetValueOrDefault(word, 0) + 1;
                    }
                }
            }

            analysis.CommonFalseNegativeWords = fnWords
                .OrderByDescending(kv => kv.Value)
                .Take(10)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            // Generar sugerencias
            analysis.Suggestions = GenerateSuggestions(analysis);

            return analysis;
        }

        private List<string> GenerateSuggestions(FeedbackAnalysis analysis)
        {
            var suggestions = new List<string>();

            // Sugerencias basadas en falsos positivos
            if (analysis.FalsePositives > 5)
            {
                suggestions.Add($"⚠️ {analysis.FalsePositives} falsos positivos detectados. Considera aumentar el umbral de español.");

                foreach (var word in analysis.CommonFalsePositiveWords.Take(3))
                {
                    suggestions.Add($"  - Palabra común en falsos positivos: '{word.Key}' ({word.Value} veces) - Considera agregarla a la lista de rechazo");
                }
            }

            // Sugerencias basadas en falsos negativos
            if (analysis.FalseNegatives > 5)
            {
                suggestions.Add($"⚠️ {analysis.FalseNegatives} falsos negativos detectados. Considera reducir el umbral de español.");

                foreach (var word in analysis.CommonFalseNegativeWords.Take(3))
                {
                    suggestions.Add($"  - Palabra común en falsos negativos: '{word.Key}' ({word.Value} veces) - Considera agregarla a la lista de aceptación");
                }
            }

            if (suggestions.Count == 0)
            {
                suggestions.Add("✅ No hay suficiente feedback para generar sugerencias.");
            }

            return suggestions;
        }

        /// <summary>
        /// Obtiene todo el feedback
        /// </summary>
        public List<FilterFeedback> GetAllFeedback()
        {
            return feedbackList.ToList();
        }

        /// <summary>
        /// Limpia todo el feedback
        /// </summary>
        public void ClearFeedback()
        {
            feedbackList.Clear();
            Save();
        }

        private void Load()
        {
            try
            {
                if (File.Exists(feedbackFile))
                {
                    var json = File.ReadAllText(feedbackFile);
                    feedbackList = JsonSerializer.Deserialize<List<FilterFeedback>>(json) ?? new List<FilterFeedback>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cargando feedback: {ex.Message}");
                feedbackList = new List<FilterFeedback>();
            }
        }

        private void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(feedbackList, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(feedbackFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error guardando feedback: {ex.Message}");
            }
        }
    }

    public class FilterFeedback
    {
        public string Filename { get; set; } = string.Empty;
        public string ReportedAs { get; set; } = string.Empty; // "false_positive" o "false_negative"
        public DateTime Timestamp { get; set; }
        public string UserComment { get; set; } = string.Empty;
    }

    public class FeedbackAnalysis
    {
        public int TotalFeedback { get; set; }
        public int FalsePositives { get; set; }
        public int FalseNegatives { get; set; }
        public Dictionary<string, int> CommonFalsePositiveWords { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> CommonFalseNegativeWords { get; set; } = new Dictionary<string, int>();
        public List<string> Suggestions { get; set; } = new List<string>();

        public override string ToString()
        {
            var result = $"=== ANÁLISIS DE FEEDBACK ===\n";
            result += $"Total de reportes: {TotalFeedback}\n";
            result += $"Falsos positivos: {FalsePositives}\n";
            result += $"Falsos negativos: {FalseNegatives}\n\n";

            if (CommonFalsePositiveWords.Any())
            {
                result += "Palabras comunes en falsos positivos:\n";
                foreach (var word in CommonFalsePositiveWords.Take(5))
                {
                    result += $"  - {word.Key}: {word.Value} veces\n";
                }
                result += "\n";
            }

            if (CommonFalseNegativeWords.Any())
            {
                result += "Palabras comunes en falsos negativos:\n";
                foreach (var word in CommonFalseNegativeWords.Take(5))
                {
                    result += $"  - {word.Key}: {word.Value} veces\n";
                }
                result += "\n";
            }

            if (Suggestions.Any())
            {
                result += "Sugerencias:\n";
                foreach (var suggestion in Suggestions)
                {
                    result += $"{suggestion}\n";
                }
            }

            return result;
        }
    }
}
