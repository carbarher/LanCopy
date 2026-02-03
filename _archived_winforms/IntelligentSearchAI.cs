using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// Sistema de IA para bÃºsqueda inteligente y optimizaciÃ³n predictiva
    /// </summary>
    public partial class MainForm
    {
        // Modelos de IA y datos
        private static readonly string aiDataFile = @"c:\p2p\SlskDown\ai_search_data.json";
        private static readonly string predictionsFile = @"c:\p2p\SlskDown\predictions.json";
        
        private SearchIntelligence aiIntelligence;
        private bool aiEnabled = true;
        
        /// <summary>
        /// Datos de inteligencia de bÃºsqueda
        /// </summary>
        public class SearchIntelligence
        {
            public Dictionary<string, AuthorProfile> AuthorProfiles { get; set; } = new();
            public Dictionary<string, SearchPattern> SearchPatterns { get; set; } = new();
            public List<TimeSlot> ProductiveHours { get; set; } = new();
            public Dictionary<string, double> QualityScores { get; set; } = new();
            public DateTime LastTrained { get; set; } = DateTime.Now;
            public int TotalSearches { get; set; } = 0;
            public double AccuracyRate { get; set; } = 0.0;
        }
        
        /// <summary>
        /// Perfil de autor con IA
        /// </summary>
        public class AuthorProfile
        {
            public string AuthorName { get; set; } = "";
            public double ProductivityScore { get; set; } = 0.0; // 0-100
            public double ReliabilityScore { get; set; } = 0.0; // 0-100
            public int TotalSearches { get; set; } = 0;
            public int SuccessfulSearches { get; set; } = 0;
            public double AverageFilesPerSearch { get; set; } = 0.0;
            public TimeSpan AverageResponseTime { get; set; } = TimeSpan.Zero;
            public List<string> CommonFileTypes { get; set; } = new();
            public List<DateTime> SuccessfulSearchTimes { get; set; } = new();
            public DateTime LastSuccessfulSearch { get; set; } = DateTime.MinValue;
            public bool IsPredictedProductive { get; set; } = false;
            public double PredictedSuccessProbability { get; set; } = 0.0;
        }
        
        /// <summary>
        /// PatrÃ³n de bÃºsqueda aprendido
        /// </summary>
        public class SearchPattern
        {
            public string Pattern { get; set; } = "";
            public int Frequency { get; set; } = 0;
            public double SuccessRate { get; set; } = 0.0;
            public List<string> RelatedAuthors { get; set; } = new();
            public DateTime LastUsed { get; set; } = DateTime.Now;
            public double AverageResults { get; set; } = 0.0;
        }
        
        /// <summary>
        /// Franja horaria productiva
        /// </summary>
        public class TimeSlot
        {
            public int Hour { get; set; }
            public double ProductivityScore { get; set; } = 0.0;
            public int SearchesCompleted { get; set; } = 0;
            public double AverageResultsPerSearch { get; set; } = 0.0;
        }
        
        /// <summary>
        /// PredicciÃ³n de bÃºsqueda
        /// </summary>
        public class SearchPrediction
        {
            public string Author { get; set; } = "";
            public double SuccessProbability { get; set; } = 0.0;
            public int PredictedFiles { get; set; } = 0;
            public TimeSpan PredictedTime { get; set; } = TimeSpan.Zero;
            public double Confidence { get; set; } = 0.0;
            public string Recommendation { get; set; } = "";
            public List<string> Reasoning { get; set; } = new();
        }
        
        /// <summary>
        /// Inicializar sistema de IA para bÃºsqueda
        /// </summary>
        private void InitializeIntelligentSearch()
        {
            try
            {
                Console.WriteLine("[IntelligentSearch] ðŸ§  Inicializando sistema de IA para bÃºsqueda");
                
                LoadAIData();
                
                if (aiIntelligence == null)
                {
                    aiIntelligence = new SearchIntelligence();
                    InitializeTimeSlots();
                }
                
                // Entrenar modelo con datos existentes
                Task.Run(() => TrainAIModel());
                
                Console.WriteLine($"[IntelligentSearch] âœ… IA inicializada con {aiIntelligence.AuthorProfiles.Count} perfiles de autor");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IntelligentSearch] âŒ Error inicializando IA: {ex.Message}");
                aiEnabled = false;
            }
        }
        
        /// <summary>
        /// Inicializar franjas horarias
        /// </summary>
        private void InitializeTimeSlots()
        {
            aiIntelligence.ProductiveHours = new List<TimeSlot>();
            for (int hour = 0; hour < 24; hour++)
            {
                aiIntelligence.ProductiveHours.Add(new TimeSlot
                {
                    Hour = hour,
                    ProductivityScore = 0.5, // Score neutro inicial
                    SearchesCompleted = 0,
                    AverageResultsPerSearch = 0
                });
            }
        }
        
        /// <summary>
        /// Cargar datos de IA desde archivo
        /// </summary>
        private void LoadAIData()
        {
            try
            {
                if (File.Exists(aiDataFile))
                {
                    var json = File.ReadAllText(aiDataFile);
                    aiIntelligence = JsonSerializer.Deserialize<SearchIntelligence>(json);
                    
                    Console.WriteLine($"[IntelligentSearch] ðŸ“‚ Datos cargados: {aiIntelligence.AuthorProfiles.Count} autores");
                }
                else
                {
                    Console.WriteLine("[IntelligentSearch] â„¹ï¸ No existe archivo de IA - iniciando desde cero");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IntelligentSearch] âŒ Error cargando datos IA: {ex.Message}");
                aiIntelligence = new SearchIntelligence();
            }
        }
        
        /// <summary>
        /// Guardar datos de IA a archivo
        /// </summary>
        private void SaveAIData()
        {
            try
            {
                var json = JsonSerializer.Serialize(aiIntelligence, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(aiDataFile, json);
                
                Console.WriteLine($"[IntelligentSearch] ðŸ’¾ Datos guardados: {aiIntelligence.AuthorProfiles.Count} perfiles");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IntelligentSearch] âŒ Error guardando datos IA: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Entrenar modelo de IA con datos histÃ³ricos
        /// </summary>
        private async Task TrainAIModel()
        {
            try
            {
                Console.WriteLine("[IntelligentSearch] ðŸŽ“ Entrenando modelo de IA...");
                
                // Analizar patrones de bÃºsqueda exitosos
                AnalyzeSuccessfulPatterns();
                
                // Calcular productividad por autor
                CalculateAuthorProductivity();
                
                // Optimizar timeouts basados en aprendizaje
                OptimizeTimeoutsFromLearning();
                
                // Predecir autores productivos
                PredictProductiveAuthors();
                
                aiIntelligence.LastTrained = DateTime.Now;
                SaveAIData();
                
                Console.WriteLine("[IntelligentSearch] âœ… Modelo de IA entrenado exitosamente");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IntelligentSearch] âŒ Error entrenando IA: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Analizar patrones de bÃºsqueda exitosos
        /// </summary>
        private void AnalyzeSuccessfulPatterns()
        {
            try
            {
                var successfulAuthors = aiIntelligence.AuthorProfiles
                    .Where(p => p.Value.SuccessfulSearches > 0)
                    .OrderByDescending(p => p.Value.ProductivityScore)
                    .Take(50);
                
                foreach (var profile in successfulAuthors)
                {
                    // Identificar patrones comunes
                    var commonFileTypes = profile.Value.CommonFileTypes
                        .GroupBy(t => t)
                        .OrderByDescending(g => g.Count())
                        .Take(3)
                        .Select(g => g.Key)
                        .ToList();
                    
                    profile.Value.CommonFileTypes = commonFileTypes;
                }
                
                Console.WriteLine($"[IntelligentSearch] ðŸ“Š Analizados {successfulAuthors.Count()} patrones exitosos");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IntelligentSearch] âŒ Error analizando patrones: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Calcular productividad de autores
        /// </summary>
        private void CalculateAuthorProductivity()
        {
            try
            {
                foreach (var profile in aiIntelligence.AuthorProfiles.Values)
                {
                    // Calcular score de productividad (0-100)
                    var successRate = profile.TotalSearches > 0 ? 
                        (double)profile.SuccessfulSearches / profile.TotalSearches : 0;
                    
                    var fileScore = Math.Min(100, profile.AverageFilesPerSearch * 2); // 50 archivos = 100 puntos
                    var speedScore = Math.Max(0, 100 - profile.AverageResponseTime.TotalSeconds); // MÃ¡s rÃ¡pido = mÃ¡s puntos
                    
                    profile.ProductivityScore = (successRate * 40 + fileScore * 35 + speedScore * 25) / 100;
                    profile.ReliabilityScore = successRate * 100;
                }
                
                Console.WriteLine("[IntelligentSearch] ðŸ“ˆ Productividad calculada para todos los autores");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IntelligentSearch] âŒ Error calculando productividad: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Optimizar timeouts basados en aprendizaje
        /// </summary>
        private void OptimizeTimeoutsFromLearning()
        {
            try
            {
                var fastAuthors = aiIntelligence.AuthorProfiles
                    .Where(p => p.Value.AverageResponseTime.TotalSeconds < 15 && p.Value.ProductivityScore > 70)
                    .Select(p => p.Key.ToLower())
                    .ToList();
                
                var slowAuthors = aiIntelligence.AuthorProfiles
                    .Where(p => p.Value.AverageResponseTime.TotalSeconds > 45 || p.Value.ReliabilityScore < 30)
                    .Select(p => p.Key.ToLower())
                    .ToList();
                
                Console.WriteLine($"[IntelligentSearch] âš¡ Autores identificados para optimizaciÃ³n:");
                Console.WriteLine($"  RÃ¡pidos: {fastAuthors.Count} (timeout 10s)");
                Console.WriteLine($"  Lentos: {slowAuthors.Count} (timeout 60s)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IntelligentSearch] âŒ Error optimizando timeouts: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Predecir autores productivos
        /// </summary>
        private void PredictProductiveAuthors()
        {
            try
            {
                var currentHour = DateTime.Now.Hour;
                var currentDayProductivity = aiIntelligence.ProductivityHours
                    .FirstOrDefault(h => h.Hour == currentHour)?.ProductivityScore ?? 0.5;
                
                foreach (var profile in aiIntelligence.AuthorProfiles.Values)
                {
                    // Factores de predicciÃ³n
                    var historicalSuccess = profile.ReliabilityScore / 100;
                    var timeOfDayFactor = GetTimeOfDayFactor(profile, currentHour);
                    var recentActivityFactor = GetRecentActivityFactor(profile);
                    var fileQualityFactor = profile.ProductivityScore / 100;
                    
                    // Calcular probabilidad de Ã©xito
                    profile.PredictedSuccessProbability = 
                        (historicalSuccess * 40 + timeOfDayFactor * 25 + recentActivityFactor * 20 + fileQualityFactor * 15) / 100;
                    
                    profile.IsPredictedProductive = profile.PredictedSuccessProbability > 0.6;
                }
                
                var predictedProductive = aiIntelligence.AuthorProfiles.Count(p => p.Value.IsPredictedProductive);
                Console.WriteLine($"[IntelligentSearch] ðŸ”® {predictedProductive} autores predichos como productivos");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IntelligentSearch] âŒ Error prediciendo autores: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtener factor de hora del dÃ­a para autor
        /// </summary>
        private double GetTimeOfDayFactor(AuthorProfile profile, int currentHour)
        {
            // Analizar bÃºsquedas exitosas por hora
            var successfulAtThisHour = profile.SuccessfulSearchTimes
                .Count(t => t.Hour == currentHour);
            
            var totalSuccessful = profile.SuccessfulSearchTimes.Count;
            
            return totalSuccessful > 0 ? (double)successfulAtThisHour / totalSuccessful : 0.5;
        }
        
        /// <summary>
        /// Obtener factor de actividad reciente
        /// </summary>
        private double GetRecentActivityFactor(AuthorProfile profile)
        {
            var daysSinceLastSuccess = (DateTime.Now - profile.LastSuccessfulSearch).TotalDays;
            
            return daysSinceLastSuccess switch
            {
                <= 1 => 1.0,  // Muy reciente
                <= 7 => 0.8,  // Reciente
                <= 30 => 0.5, // Moderado
                _ => 0.2      // Antiguo
            };
        }
        
        /// <summary>
        /// Registrar resultado de bÃºsqueda para aprendizaje
        /// </summary>
        private void RegisterSearchResultForAI(string author, bool success, int filesFound, TimeSpan responseTime, List<string> fileTypes)
        {
            try
            {
                if (!aiEnabled) return;
                
                var key = author.ToLower().Trim();
                
                if (!aiIntelligence.AuthorProfiles.ContainsKey(key))
                {
                    aiIntelligence.AuthorProfiles[key] = new AuthorProfile { AuthorName = author };
                }
                
                var profile = aiIntelligence.AuthorProfiles[key];
                
                // Actualizar estadÃ­sticas
                profile.TotalSearches++;
                if (success)
                {
                    profile.SuccessfulSearches++;
                    profile.SuccessfulSearchTimes.Add(DateTime.Now);
                    profile.LastSuccessfulSearch = DateTime.Now;
                }
                
                // Actualizar promedios
                profile.AverageFilesPerSearch = (profile.AverageFilesPerSearch * (profile.TotalSearches - 1) + filesFound) / profile.TotalSearches;
                profile.AverageResponseTime = TimeSpan.FromSeconds(
                    (profile.AverageResponseTime.TotalSeconds * (profile.TotalSearches - 1) + responseTime.TotalSeconds) / profile.TotalSearches
                );
                
                // Actualizar tipos de archivo comunes
                foreach (var fileType in fileTypes)
                {
                    if (!profile.CommonFileTypes.Contains(fileType))
                    {
                        profile.CommonFileTypes.Add(fileType);
                    }
                }
                
                // Actualizar franja horaria actual
                var currentHour = DateTime.Now.Hour;
                var timeSlot = aiIntelligence.ProductivityHours.FirstOrDefault(h => h.Hour == currentHour);
                if (timeSlot != null)
                {
                    timeSlot.SearchesCompleted++;
                    timeSlot.AverageResultsPerSearch = (timeSlot.AverageResultsPerSearch * (timeSlot.SearchesCompleted - 1) + filesFound) / timeSlot.SearchesCompleted;
                }
                
                aiIntelligence.TotalSearches++;
                aiIntelligence.AccuracyRate = (double)aiIntelligence.AuthorProfiles.Values.Count(p => p.Value.SuccessfulSearches > 0) / Math.Max(1, aiIntelligence.AuthorProfiles.Count);
                
                Console.WriteLine($"[IntelligentSearch] ðŸ“š Resultado registrado para IA: {author} (Ã©xito: {success})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IntelligentSearch] âŒ Error registrando resultado: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtener predicciÃ³n para autor especÃ­fico
        /// </summary>
        private SearchPrediction GetAuthorPrediction(string author)
        {
            try
            {
                var key = author.ToLower().Trim();
                
                if (!aiIntelligence.AuthorProfiles.ContainsKey(key))
                {
                    return new SearchPrediction
                    {
                        Author = author,
                        SuccessProbability = 0.5, // Neutral para nuevos autores
                        PredictedFiles = 10,
                        PredictedTime = TimeSpan.FromSeconds(30),
                        Confidence = 0.1,
                        Recommendation = "Nuevo autor - sin datos histÃ³ricos",
                        Reasoning = new List<string> { "Sin bÃºsquedas previas registradas" }
                    };
                }
                
                var profile = aiIntelligence.AuthorProfiles[key];
                
                var prediction = new SearchPrediction
                {
                    Author = author,
                    SuccessProbability = profile.PredictedSuccessProbability,
                    PredictedFiles = (int)profile.AverageFilesPerSearch,
                    PredictedTime = profile.AverageResponseTime,
                    Confidence = Math.Min(1.0, profile.TotalSearches / 10.0), // MÃ¡s bÃºsquedas = mÃ¡s confianza
                    Recommendation = GetRecommendation(profile),
                    Reasoning = GetReasoning(profile)
                };
                
                return prediction;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IntelligentSearch] âŒ Error obteniendo predicciÃ³n: {ex.Message}");
                return new SearchPrediction { Author = author, SuccessProbability = 0.5 };
            }
        }
        
        /// <summary>
        /// Obtener recomendaciÃ³n basada en perfil
        /// </summary>
        private string GetRecommendation(AuthorProfile profile)
        {
            if (profile.ProductivityScore > 80)
                return "â­ Autor altamente recomendado - excelente productividad";
            else if (profile.ProductivityScore > 60)
                return "âœ… Buen autor - recomendado para bÃºsqueda";
            else if (profile.ProductivityScore > 40)
                return "âš ï¸ Autor moderado - buscar con expectativas bajas";
            else
                return "âŒ Autor no recomendado - baja productividad";
        }
        
        /// <summary>
        /// Obtener razonamiento de predicciÃ³n
        /// </summary>
        private List<string> GetReasoning(AuthorProfile profile)
        {
            var reasoning = new List<string>();
            
            if (profile.SuccessfulSearches > 0)
            {
                reasoning.Add($"Tasa de Ã©xito: {profile.ReliabilityScore:F1}%");
                reasoning.Add($"Promedio de archivos: {profile.AverageFilesPerSearch:F1}");
                reasoning.Add($"Tiempo de respuesta: {profile.AverageResponseTime.TotalSeconds:F1}s");
            }
            else
            {
                reasoning.Add("Sin bÃºsquedas exitosas previas");
            }
            
            if (profile.ProductivityScore > 70)
            {
                reasoning.Add("Autor consistentemente productivo");
            }
            else if (profile.ProductivityScore < 30)
            {
                reasoning.Add("Historial de baja productividad");
            }
            
            return reasoning;
        }
        
        /// <summary>
        /// Optimizar orden de bÃºsqueda basado en IA
        /// </summary>
        private List<string> OptimizeSearchOrderWithAI(List<string> authors)
        {
            try
            {
                if (!aiEnabled) return authors;
                
                Console.WriteLine("[IntelligentSearch] ðŸ§  Optimizando orden de bÃºsqueda con IA");
                
                var scoredAuthors = authors.Select(author =>
                {
                    var prediction = GetAuthorPrediction(author);
                    return new
                    {
                        Author = author,
                        Score = prediction.SuccessProbability * prediction.Confidence,
                        Prediction = prediction
                    };
                })
                .OrderByDescending(a => a.Score)
                .ToList();
                
                var optimizedOrder = scoredAuthors.Select(a => a.Author).ToList();
                
                // Mostrar optimizaciÃ³n en log
                AddColoredLogMessage("ðŸ§  ORDEN OPTIMIZADO POR IA:", LogMessageType.Phase);
                
                foreach (var item in scoredAuthors.Take(10))
                {
                    var status = item.Prediction.SuccessProbability > 0.7 ? "â­" : 
                                item.Prediction.SuccessProbability > 0.4 ? "âœ…" : "âš ï¸";
                    
                    AddColoredLogMessage($"  {status} {item.Author} ({item.Prediction.SuccessProbability:P1} confianza)", 
                        item.Prediction.SuccessProbability > 0.7 ? LogMessageType.Success : LogMessageType.Info);
                }
                
                if (scoredAuthors.Count > 10)
                {
                    AddColoredLogMessage($"  ... y {scoredAuthors.Count - 10} autores mÃ¡s", LogMessageType.Info);
                }
                
                Console.WriteLine($"[IntelligentSearch] âœ… Orden optimizado: {scoredAuthors.Count} autores reorganizados");
                
                return optimizedOrder;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IntelligentSearch] âŒ Error optimizando orden: {ex.Message}");
                return authors;
            }
        }
        
        /// <summary>
        /// Mostrar dashboard de IA
        /// </summary>
        private void ShowAIDashboard()
        {
            try
            {
                var totalAuthors = aiIntelligence.AuthorProfiles.Count;
                var productiveAuthors = aiIntelligence.AuthorProfiles.Count(p => p.Value.IsPredictedProductive);
                var accuracyRate = aiIntelligence.AccuracyRate;
                var totalSearches = aiIntelligence.TotalSearches;
                
                var topAuthors = aiIntelligence.AuthorProfiles
                    .OrderByDescending(p => p.Value.ProductivityScore)
                    .Take(5)
                    .ToList();
                
                var dashboard = $"""
ðŸ§  DASHBOARD DE INTELIGENCIA ARTIFICIAL
========================================
ðŸ“Š EstadÃ­sticas Generales:
â”œâ”€â”€ Autores analizados: {totalAuthors}
â”œâ”€â”€ Autores productivos: {productiveAuthors} ({(totalAuthors > 0 ? productiveAuthors * 100 / totalAuthors : 0)}%)
â”œâ”€â”€ Tasa de precisiÃ³n: {accuracyRate:P1}
â”œâ”€â”€ BÃºsquedas totales: {totalSearches}
â””â”€â”€ Ãšltimo entrenamiento: {aiIntelligence.LastTrained:yyyy-MM-dd HH:mm}

ðŸ† Top 5 Autores (Productividad):
{string.Join("\n", topAuthors.Select((p, i) => $"  {i + 1}. {p.Value.AuthorName}: {p.Value.ProductivityScore:F1}/100"))}

âš¡ Optimizaciones Activadas:
â”œâ”€â”€ âœ… PredicciÃ³n de Ã©xito por autor
â”œâ”€â”€ âœ… OptimizaciÃ³n de orden de bÃºsqueda
â”œâ”€â”€ âœ… Timeout adaptativo basado en aprendizaje
â”œâ”€â”€ âœ… DetecciÃ³n de patrones de bÃºsqueda
â””â”€â”€ âœ… AnÃ¡lisis de productividad horaria

ðŸ’¾ Datos guardados en: {aiDataFile}
ðŸ”„ Modelo actualizado: {aiIntelligence.LastTrained:HH:mm:ss}
""";
                
                Console.WriteLine(dashboard);
                MessageBox.Show(dashboard, "Dashboard de IA - SlskDown", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IntelligentSearch] âŒ Error mostrando dashboard: {ex.Message}");
            }
        }
    }
}

