using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;

namespace SlskDown
{
    public class SimilarityInput
    {
        public string Interests1 { get; set; }
        public string Interests2 { get; set; }
    }
    
    public class SimilarityOutput
    {
        public double Score { get; set; }
    }
    
    public class DownloadFeatures
    {
        public double UserScore { get; set; }
        public long FileSize { get; set; }
        public bool IsPrivileged { get; set; }
        public int QueuePosition { get; set; }
        public int PreviousFailures { get; set; }
        public double AverageSpeed { get; set; }
    }
    
    public class DownloadPrediction
    {
        public double Probability { get; set; }
        public string Recommendation { get; set; }
    }
    
    public class MLRecommendationEngine
    {
        private InterestsSystem interestsSystem;
        private PrivilegedUsersManager privilegedUsersManager;
        private Action<string> logAction;
        private Dictionary<string, double> userSuccessRates = new Dictionary<string, double>();
        private Dictionary<string, List<double>> downloadHistory = new Dictionary<string, List<double>>();
        
        public MLRecommendationEngine(
            InterestsSystem interests,
            PrivilegedUsersManager privileged,
            Action<string> logger)
        {
            interestsSystem = interests;
            privilegedUsersManager = privileged;
            logAction = logger;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // MODELO DE SIMILITUD MEJORADO
        // ═══════════════════════════════════════════════════════════════
        
        public int CalculateSimilarityML(List<string> myInterests, List<string> otherInterests)
        {
            try
            {
                // Algoritmo mejorado usando Jaccard similarity + TF-IDF weights
                
                // 1. Calcular Jaccard similarity básico
                var intersection = myInterests.Intersect(otherInterests, StringComparer.OrdinalIgnoreCase).Count();
                var union = myInterests.Union(otherInterests, StringComparer.OrdinalIgnoreCase).Count();
                
                if (union == 0) return 0;
                
                double jaccardScore = (double)intersection / union;
                
                // 2. Aplicar pesos TF-IDF (simulado)
                var weights = CalculateTFIDFWeights(myInterests, otherInterests);
                double weightedScore = 0;
                
                foreach (var interest in myInterests.Intersect(otherInterests, StringComparer.OrdinalIgnoreCase))
                {
                    weightedScore += weights.GetValueOrDefault(interest, 1.0);
                }
                
                // 3. Normalizar
                double normalizedScore = weightedScore / Math.Max(myInterests.Count, otherInterests.Count);
                
                // 4. Combinar Jaccard y weighted score
                double finalScore = (jaccardScore * 0.4 + normalizedScore * 0.6) * 100;
                
                return (int)Math.Min(100, Math.Max(0, finalScore));
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error en cálculo ML de similitud: {ex.Message}");
                return interestsSystem.CalculateSimilarity(myInterests, otherInterests);
            }
        }
        
        private Dictionary<string, double> CalculateTFIDFWeights(List<string> interests1, List<string> interests2)
        {
            var weights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var allInterests = interests1.Union(interests2).ToList();
            
            // Simular IDF (Inverse Document Frequency)
            // En producción, esto vendría de estadísticas globales del servidor
            foreach (var interest in allInterests)
            {
                // Intereses más raros tienen mayor peso
                double idf = Math.Log(1000.0 / (GetInterestFrequency(interest) + 1));
                weights[interest] = Math.Max(0.1, Math.Min(5.0, idf));
            }
            
            return weights;
        }
        
        private int GetInterestFrequency(string interest)
        {
            // Simular frecuencia global
            // En producción, esto vendría del servidor
            var commonInterests = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "music", 500 },
                { "books", 450 },
                { "movies", 400 },
                { "science fiction", 200 },
                { "fantasy", 180 }
            };
            
            return commonInterests.GetValueOrDefault(interest, 50);
        }
        
        // ═══════════════════════════════════════════════════════════════
        // PREDICCIÓN DE ÉXITO DE DESCARGAS
        // ═══════════════════════════════════════════════════════════════
        
        public DownloadPrediction PredictDownloadSuccess(DownloadFeatures features)
        {
            try
            {
                // Modelo de regresión logística simplificado
                double score = 0;
                
                // Factor 1: Score del usuario (0-5) → peso 0.3
                score += (features.UserScore / 5.0) * 0.3;
                
                // Factor 2: Usuario privilegiado → peso 0.2
                if (features.IsPrivileged)
                {
                    score += 0.2;
                }
                
                // Factor 3: Tamaño del archivo (penalizar muy grandes) → peso 0.15
                double sizeScore = 1.0 - Math.Min(1.0, features.FileSize / (1024.0 * 1024 * 1024)); // Normalizar a 1GB
                score += sizeScore * 0.15;
                
                // Factor 4: Posición en cola (mejor posición = mayor probabilidad) → peso 0.15
                double queueScore = features.QueuePosition > 0 
                    ? Math.Max(0, 1.0 - (features.QueuePosition / 100.0))
                    : 0.5;
                score += queueScore * 0.15;
                
                // Factor 5: Fallos previos (penalizar) → peso 0.1
                double failureScore = Math.Max(0, 1.0 - (features.PreviousFailures / 5.0));
                score += failureScore * 0.1;
                
                // Factor 6: Velocidad promedio → peso 0.1
                double speedScore = Math.Min(1.0, features.AverageSpeed / (1024 * 1024)); // Normalizar a 1MB/s
                score += speedScore * 0.1;
                
                // Aplicar función sigmoide para obtener probabilidad
                double probability = 1.0 / (1.0 + Math.Exp(-5 * (score - 0.5)));
                
                // Generar recomendación
                string recommendation;
                if (probability >= 0.8)
                    recommendation = "Alta probabilidad de éxito - Descargar ahora";
                else if (probability >= 0.6)
                    recommendation = "Probabilidad moderada - Considerar descargar";
                else if (probability >= 0.4)
                    recommendation = "Probabilidad baja - Buscar alternativas";
                else
                    recommendation = "Muy baja probabilidad - No recomendado";
                
                return new DownloadPrediction
                {
                    Probability = probability,
                    Recommendation = recommendation
                };
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error en predicción de descarga: {ex.Message}");
                return new DownloadPrediction
                {
                    Probability = 0.5,
                    Recommendation = "No se pudo calcular predicción"
                };
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // RECOMENDACIONES PERSONALIZADAS
        // ═══════════════════════════════════════════════════════════════
        
        public List<string> GetPersonalizedRecommendations(int count = 10)
        {
            try
            {
                var myInterests = interestsSystem.GetLikedInterests();
                var similarUsers = interestsSystem.GetSimilarUsers(50); // Mínimo 50% similitud
                
                // Collaborative filtering: recomendar intereses de usuarios similares
                var recommendations = new Dictionary<string, double>();
                
                foreach (var user in similarUsers)
                {
                    foreach (var interest in user.CommonInterests)
                    {
                        if (myInterests.Contains(interest, StringComparer.OrdinalIgnoreCase))
                            continue;
                        
                        // Peso basado en similitud del usuario
                        double weight = user.SimilarityScore / 100.0;
                        
                        if (recommendations.ContainsKey(interest))
                            recommendations[interest] += weight;
                        else
                            recommendations[interest] = weight;
                    }
                }
                
                // Ordenar por peso y retornar top N
                return recommendations
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(count)
                    .Select(kvp => kvp.Key)
                    .ToList();
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error generando recomendaciones: {ex.Message}");
                return new List<string>();
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // APRENDIZAJE Y ACTUALIZACIÓN
        // ═══════════════════════════════════════════════════════════════
        
        public void RecordDownloadSuccess(string username, bool success)
        {
            if (!downloadHistory.ContainsKey(username))
            {
                downloadHistory[username] = new List<double>();
            }
            
            downloadHistory[username].Add(success ? 1.0 : 0.0);
            
            // Mantener solo últimos 100 registros
            if (downloadHistory[username].Count > 100)
            {
                downloadHistory[username].RemoveAt(0);
            }
            
            // Actualizar tasa de éxito
            userSuccessRates[username] = downloadHistory[username].Average();
        }
        
        public double GetUserSuccessRate(string username)
        {
            return userSuccessRates.GetValueOrDefault(username, 0.5);
        }
        
        // ═══════════════════════════════════════════════════════════════
        // CLUSTERING DE USUARIOS
        // ═══════════════════════════════════════════════════════════════
        
        public List<List<SimilarUser>> ClusterSimilarUsers(int numClusters = 5)
        {
            try
            {
                var users = interestsSystem.GetSimilarUsers();
                if (users.Count < numClusters)
                {
                    return new List<List<SimilarUser>> { users };
                }
                
                // K-means clustering simplificado
                var clusters = new List<List<SimilarUser>>();
                
                // Inicializar clusters con usuarios aleatorios
                var random = new Random();
                var centroids = users.OrderBy(x => random.Next()).Take(numClusters).ToList();
                
                // Asignar usuarios a clusters
                for (int i = 0; i < numClusters; i++)
                {
                    clusters.Add(new List<SimilarUser>());
                }
                
                foreach (var user in users)
                {
                    int closestCluster = 0;
                    int maxSimilarity = 0;
                    
                    for (int i = 0; i < centroids.Count; i++)
                    {
                        int similarity = CalculateSimilarityML(
                            user.CommonInterests,
                            centroids[i].CommonInterests
                        );
                        
                        if (similarity > maxSimilarity)
                        {
                            maxSimilarity = similarity;
                            closestCluster = i;
                        }
                    }
                    
                    clusters[closestCluster].Add(user);
                }
                
                logAction?.Invoke($"🤖 Clustering completado: {numClusters} clusters creados");
                
                return clusters;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error en clustering: {ex.Message}");
                return new List<List<SimilarUser>>();
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // PERSISTENCIA
        // ═══════════════════════════════════════════════════════════════
        
        public void SaveModel(string path)
        {
            try
            {
                var data = new
                {
                    UserSuccessRates = userSuccessRates,
                    DownloadHistory = downloadHistory
                };
                
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                
                logAction?.Invoke($"💾 Modelo ML guardado: {path}");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error guardando modelo: {ex.Message}");
            }
        }
        
        public void LoadModel(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                
                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<JsonElement>(json);
                
                if (data.TryGetProperty("UserSuccessRates", out var rates))
                {
                    userSuccessRates = JsonSerializer.Deserialize<Dictionary<string, double>>(rates.GetRawText());
                }
                
                if (data.TryGetProperty("DownloadHistory", out var history))
                {
                    downloadHistory = JsonSerializer.Deserialize<Dictionary<string, List<double>>>(history.GetRawText());
                }
                
                logAction?.Invoke($"💾 Modelo ML cargado: {path}");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error cargando modelo: {ex.Message}");
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // ESTADÍSTICAS
        // ═══════════════════════════════════════════════════════════════
        
        public Dictionary<string, object> GetMLStats()
        {
            return new Dictionary<string, object>
            {
                { "TrackedUsers", userSuccessRates.Count },
                { "TotalDownloads", downloadHistory.Values.Sum(h => h.Count) },
                { "AverageSuccessRate", userSuccessRates.Values.Any() ? userSuccessRates.Values.Average() : 0 }
            };
        }
    }
}
