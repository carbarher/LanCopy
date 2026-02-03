using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SlskDown.Core.AI
{
    /// <summary>
    /// Predictor de disponibilidad de archivos usando IA
    /// </summary>
    public class AvailabilityPredictor
    {
        private readonly OllamaClient ollama;
        private readonly Dictionary<string, List<SearchHistoryEntry>> searchHistory;

        public event Action<string> OnLog;

        public AvailabilityPredictor(OllamaClient ollama)
        {
            this.ollama = ollama;
            searchHistory = new Dictionary<string, List<SearchHistoryEntry>>();
        }

        /// <summary>
        /// Registra búsqueda en el historial
        /// </summary>
        public void RecordSearch(string searchTerm, int resultsFound, DateTime timestamp)
        {
            if (!searchHistory.ContainsKey(searchTerm))
                searchHistory[searchTerm] = new List<SearchHistoryEntry>();

            searchHistory[searchTerm].Add(new SearchHistoryEntry
            {
                Timestamp = timestamp,
                ResultsFound = resultsFound,
                Hour = timestamp.Hour,
                DayOfWeek = timestamp.DayOfWeek
            });
        }

        /// <summary>
        /// Predice disponibilidad de un archivo
        /// </summary>
        public async Task<AvailabilityPrediction> PredictAvailabilityAsync(string searchTerm)
        {
            try
            {
                Log($"🔮 Prediciendo disponibilidad: {searchTerm}");

                var history = GetSearchHistory(searchTerm);
                var historyJson = JsonSerializer.Serialize(history);

                var prompt = $@"
Archivo buscado: ""{searchTerm}""

Histórico de búsquedas (últimas 30 días):
{historyJson}

Analiza y predice en formato JSON:
{{
  ""probability"": 75,
  ""bestTimeRange"": ""20:00-23:00"",
  ""bestDays"": [""sábado"", ""domingo""],
  ""likelyUsers"": [""usuario1"", ""usuario2""],
  ""estimatedWaitDays"": 3,
  ""reasoning"": ""Explicación breve"",
  ""tips"": [""consejo1"", ""consejo2""]
}}

Considera:
- Patrones de disponibilidad históricos
- Horarios con más resultados
- Días de la semana más activos
- Rareza del archivo
";

                var response = await ollama.GetCompletionAsync(prompt, temperature: 0.5);
                
                if (string.IsNullOrEmpty(response))
                    return CreateDefaultPrediction();

                var prediction = ParsePrediction(response);
                Log($"✅ Probabilidad: {prediction.Probability}% - Espera: {prediction.EstimatedWaitDays} días");
                
                return prediction;
            }
            catch (Exception ex)
            {
                Log($"❌ Error en PredictAvailabilityAsync: {ex.Message}");
                return CreateDefaultPrediction();
            }
        }

        /// <summary>
        /// Sugiere mejor momento para buscar
        /// </summary>
        public async Task<BestSearchTime> SuggestBestSearchTimeAsync(string searchTerm)
        {
            try
            {
                var history = GetSearchHistory(searchTerm);
                
                if (history.Count == 0)
                {
                    return new BestSearchTime
                    {
                        Hour = 20,
                        DayOfWeek = DayOfWeek.Saturday,
                        Confidence = 0.3,
                        Reasoning = "Sin datos históricos, usando horarios típicos de alta actividad"
                    };
                }

                // Analizar patrones
                var hourlyStats = history
                    .GroupBy(h => h.Hour)
                    .Select(g => new { Hour = g.Key, AvgResults = g.Average(h => h.ResultsFound) })
                    .OrderByDescending(s => s.AvgResults)
                    .First();

                var dailyStats = history
                    .GroupBy(h => h.DayOfWeek)
                    .Select(g => new { Day = g.Key, AvgResults = g.Average(h => h.ResultsFound) })
                    .OrderByDescending(s => s.AvgResults)
                    .First();

                return new BestSearchTime
                {
                    Hour = hourlyStats.Hour,
                    DayOfWeek = dailyStats.Day,
                    Confidence = 0.8,
                    Reasoning = $"Basado en {history.Count} búsquedas previas"
                };
            }
            catch (Exception ex)
            {
                Log($"❌ Error en SuggestBestSearchTimeAsync: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Predice usuarios que probablemente tengan el archivo
        /// </summary>
        public async Task<List<string>> PredictLikelyUsersAsync(string searchTerm, List<string> knownUsers)
        {
            try
            {
                var usersText = string.Join(", ", knownUsers.Take(20));

                var prompt = $@"
Archivo buscado: ""{searchTerm}""
Usuarios conocidos en la red: {usersText}

Basándote en el tipo de archivo, predice qué 5 usuarios tienen más probabilidad de tenerlo.
Formato: Un usuario por línea, sin numeración.
";

                var response = await ollama.GetCompletionAsync(prompt, temperature: 0.6);
                
                if (string.IsNullOrEmpty(response))
                    return new List<string>();

                return response
                    .Split('\n')
                    .Select(u => u.Trim())
                    .Where(u => !string.IsNullOrEmpty(u))
                    .Take(5)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log($"❌ Error en PredictLikelyUsersAsync: {ex.Message}");
                return new List<string>();
            }
        }

        private List<SearchHistoryEntry> GetSearchHistory(string searchTerm)
        {
            if (searchHistory.ContainsKey(searchTerm))
                return searchHistory[searchTerm].OrderByDescending(h => h.Timestamp).Take(30).ToList();
            
            return new List<SearchHistoryEntry>();
        }

        private AvailabilityPrediction ParsePrediction(string jsonResponse)
        {
            try
            {
                var startIndex = jsonResponse.IndexOf('{');
                var endIndex = jsonResponse.LastIndexOf('}');
                
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    var jsonText = jsonResponse.Substring(startIndex, endIndex - startIndex + 1);
                    return JsonSerializer.Deserialize<AvailabilityPrediction>(jsonText);
                }

                return CreateDefaultPrediction();
            }
            catch
            {
                return CreateDefaultPrediction();
            }
        }

        private AvailabilityPrediction CreateDefaultPrediction()
        {
            return new AvailabilityPrediction
            {
                Probability = 50,
                BestTimeRange = "20:00-23:00",
                BestDays = new[] { "sábado", "domingo" },
                EstimatedWaitDays = 7,
                Reasoning = "Predicción basada en patrones generales"
            };
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
    }

    public class SearchHistoryEntry
    {
        public DateTime Timestamp { get; set; }
        public int ResultsFound { get; set; }
        public int Hour { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
    }

    public class AvailabilityPrediction
    {
        public int Probability { get; set; }
        public string BestTimeRange { get; set; }
        public string[] BestDays { get; set; }
        public string[] LikelyUsers { get; set; }
        public int EstimatedWaitDays { get; set; }
        public string Reasoning { get; set; }
        public string[] Tips { get; set; }
    }

    public class BestSearchTime
    {
        public int Hour { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public double Confidence { get; set; }
        public string Reasoning { get; set; }
    }
}
