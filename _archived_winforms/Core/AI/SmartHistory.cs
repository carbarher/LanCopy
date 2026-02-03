using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;

namespace SlskDown.Core.AI
{
    public class HistoryEntry
    {
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } // "search", "download", "command"
        public string Content { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Historial inteligente que recuerda búsquedas, descargas y patrones
    /// </summary>
    public static class SmartHistory
    {
        private static List<HistoryEntry> history = new List<HistoryEntry>();
        private static string historyFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "smart_history.json");
        private const int MAX_HISTORY = 1000;

        public static void RecordSearch(string query, int resultsFound)
        {
            AddEntry(new HistoryEntry
            {
                Timestamp = DateTime.Now,
                Type = "search",
                Content = query,
                Metadata = new Dictionary<string, string>
                {
                    ["results"] = resultsFound.ToString()
                }
            });
        }

        public static void RecordDownload(string filename, string author)
        {
            AddEntry(new HistoryEntry
            {
                Timestamp = DateTime.Now,
                Type = "download",
                Content = filename,
                Metadata = new Dictionary<string, string>
                {
                    ["author"] = author
                }
            });
        }

        public static void RecordCommand(string command, bool success)
        {
            AddEntry(new HistoryEntry
            {
                Timestamp = DateTime.Now,
                Type = "command",
                Content = command,
                Metadata = new Dictionary<string, string>
                {
                    ["success"] = success.ToString()
                }
            });
        }

        private static void AddEntry(HistoryEntry entry)
        {
            history.Add(entry);
            
            // Mantener límite
            if (history.Count > MAX_HISTORY)
            {
                history.RemoveAt(0);
            }

            Save();
        }

        /// <summary>
        /// Encuentra patrones en el historial
        /// </summary>
        public static List<string> FindPatterns()
        {
            var patterns = new List<string>();

            // Autores más buscados
            var topAuthors = history
                .Where(h => h.Type == "search")
                .GroupBy(h => h.Content)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key)
                .ToList();

            if (topAuthors.Count > 0)
            {
                patterns.Add($"Autores frecuentes: {string.Join(", ", topAuthors)}");
            }

            // Horarios de mayor actividad
            var hourGroups = history
                .GroupBy(h => h.Timestamp.Hour)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (hourGroups != null)
            {
                patterns.Add($"Hora más activa: {hourGroups.Key}:00");
            }

            return patterns;
        }

        /// <summary>
        /// Obtiene sugerencias basadas en historial
        /// </summary>
        public static List<string> GetSuggestions(string context)
        {
            var suggestions = new List<string>();

            // Últimas búsquedas similares
            var recentSearches = history
                .Where(h => h.Type == "search" && h.Content.Contains(context, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(h => h.Timestamp)
                .Take(3)
                .Select(h => h.Content)
                .ToList();

            suggestions.AddRange(recentSearches);

            return suggestions.Distinct().ToList();
        }

        /// <summary>
        /// Obtiene el último elemento de un tipo
        /// </summary>
        public static HistoryEntry GetLast(string type)
        {
            return history
                .Where(h => h.Type == type)
                .OrderByDescending(h => h.Timestamp)
                .FirstOrDefault();
        }

        /// <summary>
        /// Obtiene estadísticas del historial
        /// </summary>
        public static Dictionary<string, int> GetStats()
        {
            return new Dictionary<string, int>
            {
                ["total"] = history.Count,
                ["searches"] = history.Count(h => h.Type == "search"),
                ["downloads"] = history.Count(h => h.Type == "download"),
                ["commands"] = history.Count(h => h.Type == "command")
            };
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(historyFile))
                {
                    var json = File.ReadAllText(historyFile);
                    history = JsonSerializer.Deserialize<List<HistoryEntry>>(json) ?? new List<HistoryEntry>();
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                var dataDir = Path.GetDirectoryName(historyFile);
                Directory.CreateDirectory(dataDir);

                var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(historyFile, json);
            }
            catch { }
        }

        public static void Clear()
        {
            history.Clear();
            Save();
        }
    }
}
