using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SlskDown.Models;

namespace SlskDown.Core
{
    /// <summary>
    /// Priorizador inteligente que considera la ubicación geográfica
    /// </summary>
    public class GeoAwarePrioritizer
    {
        private readonly GeoLocationService geoService;
        private readonly Dictionary<string, int> userProximityCache;
        private readonly object cacheLock = new object();
        
        // Pesos para el algoritmo de scoring
        private const double PROXIMITY_WEIGHT = 0.3;      // 30% - Cercanía geográfica
        private const double SPEED_WEIGHT = 0.25;         // 25% - Velocidad histórica
        private const double QUEUE_WEIGHT = 0.2;          // 20% - Posición en cola
        private const double RELIABILITY_WEIGHT = 0.15;   // 15% - Confiabilidad
        private const double FILE_SIZE_WEIGHT = 0.1;      // 10% - Tamaño del archivo
        
        public GeoAwarePrioritizer(GeoLocationService service)
        {
            geoService = service;
            userProximityCache = new Dictionary<string, int>();
        }
        
        /// <summary>
        /// Calcula un score compuesto para priorizar descargas
        /// </summary>
        public async Task<double> CalculateDownloadScoreAsync(DownloadTask task, ProviderStats stats)
        {
            double score = 0;
            
            // 1. Score de proximidad geográfica
            var proximityScore = await GetProximityScoreAsync(task.File.Username);
            score += proximityScore * PROXIMITY_WEIGHT;
            
            // 2. Score de velocidad (basado en histórico)
            var speedScore = CalculateSpeedScore(stats);
            score += speedScore * SPEED_WEIGHT;
            
            // 3. Score de cola (menos es mejor)
            var queueScore = CalculateQueueScore(task);
            score += queueScore * QUEUE_WEIGHT;
            
            // 4. Score de confiabilidad
            var reliabilityScore = CalculateReliabilityScore(stats);
            score += reliabilityScore * RELIABILITY_WEIGHT;
            
            // 5. Score de tamaño de archivo (archivos pequeños primero)
            var sizeScore = CalculateFileSizeScore(task.File.SizeBytes);
            score += sizeScore * FILE_SIZE_WEIGHT;
            
            return score;
        }
        
        /// <summary>
        /// Obtiene el score de proximidad para un usuario (con cache)
        /// </summary>
        private async Task<int> GetProximityScoreAsync(string username)
        {
            // Verificar cache
            lock (cacheLock)
            {
                if (userProximityCache.TryGetValue(username, out var cached))
                {
                    return cached;
                }
            }
            
            // Aquí necesitarías obtener la IP del usuario desde Soulseek
            // Por ahora, retornar un valor neutral
            // TODO: Integrar con Soulseek para obtener IP del peer
            
            var score = 50; // Neutral por defecto
            
            lock (cacheLock)
            {
                userProximityCache[username] = score;
            }
            
            return score;
        }
        
        /// <summary>
        /// Calcula score basado en velocidad histórica
        /// </summary>
        private int CalculateSpeedScore(ProviderStats stats)
        {
            if (stats == null || stats.TotalDownloads == 0)
                return 50; // Neutral para usuarios nuevos
            
            // Velocidad promedio en MB/s
            var avgSpeedMBps = stats.AverageSpeed / 1024.0 / 1024.0;
            
            // Escalar a 0-100
            // 0 MB/s = 0 puntos
            // 10 MB/s o más = 100 puntos
            var score = Math.Min(100, (int)(avgSpeedMBps * 10));
            
            return score;
        }
        
        /// <summary>
        /// Calcula score basado en posición en cola
        /// </summary>
        private int CalculateQueueScore(DownloadTask task)
        {
            // Posición 0 = 100 puntos
            // Posición 10+ = 0 puntos
            var score = Math.Max(0, 100 - (task.QueuePosition * 10));
            return score;
        }
        
        /// <summary>
        /// Calcula score de confiabilidad
        /// </summary>
        private int CalculateReliabilityScore(ProviderStats stats)
        {
            if (stats == null || stats.TotalDownloads == 0)
                return 50; // Neutral
            
            // Tasa de éxito
            var successRate = (double)stats.SuccessfulDownloads / stats.TotalDownloads;
            
            // Convertir a 0-100
            var score = (int)(successRate * 100);
            
            return score;
        }
        
        /// <summary>
        /// Calcula score basado en tamaño de archivo
        /// </summary>
        private int CalculateFileSizeScore(long sizeBytes)
        {
            // Archivos pequeños tienen prioridad
            var sizeMB = sizeBytes / 1024.0 / 1024.0;
            
            // 0-10 MB = 100 puntos
            // 100+ MB = 0 puntos
            var score = Math.Max(0, 100 - (int)(sizeMB / 10));
            
            return score;
        }
        
        /// <summary>
        /// Ordena una lista de tareas por score geográfico y otros factores
        /// </summary>
        public async Task<List<DownloadTask>> PrioritizeTasksAsync(
            List<DownloadTask> tasks, 
            Dictionary<string, ProviderStats> providerStats)
        {
            var scoredTasks = new List<(DownloadTask task, double score)>();
            
            foreach (var task in tasks)
            {
                providerStats.TryGetValue(task.File.Username, out var stats);
                var score = await CalculateDownloadScoreAsync(task, stats);
                scoredTasks.Add((task, score));
            }
            
            return scoredTasks
                .OrderByDescending(x => x.score)
                .Select(x => x.task)
                .ToList();
        }
        
        /// <summary>
        /// Obtiene recomendaciones de proveedores basadas en proximidad
        /// </summary>
        public async Task<List<ProviderRecommendation>> GetProviderRecommendationsAsync(
            Dictionary<string, ProviderStats> providerStats)
        {
            var recommendations = new List<ProviderRecommendation>();
            
            foreach (var kvp in providerStats)
            {
                var username = kvp.Key;
                var stats = kvp.Value;
                
                var proximityScore = await GetProximityScoreAsync(username);
                var speedScore = CalculateSpeedScore(stats);
                var reliabilityScore = CalculateReliabilityScore(stats);
                
                var overallScore = (proximityScore * 0.4) + (speedScore * 0.3) + (reliabilityScore * 0.3);
                
                recommendations.Add(new ProviderRecommendation
                {
                    Username = username,
                    ProximityScore = proximityScore,
                    SpeedScore = speedScore,
                    ReliabilityScore = reliabilityScore,
                    OverallScore = overallScore,
                    Stats = stats
                });
            }
            
            return recommendations
                .OrderByDescending(r => r.OverallScore)
                .ToList();
        }
        
        /// <summary>
        /// Limpia el cache de proximidad
        /// </summary>
        public void ClearProximityCache()
        {
            lock (cacheLock)
            {
                userProximityCache.Clear();
            }
        }
        
        public Action<string> OnLog { get; set; }
    }
    
    /// <summary>
    /// Recomendación de proveedor con scores detallados
    /// </summary>
    public class ProviderRecommendation
    {
        public string Username { get; set; }
        public int ProximityScore { get; set; }
        public int SpeedScore { get; set; }
        public int ReliabilityScore { get; set; }
        public double OverallScore { get; set; }
        public ProviderStats Stats { get; set; }
        
        public string GetRating()
        {
            if (OverallScore >= 80) return "⭐⭐⭐⭐⭐ Excelente";
            if (OverallScore >= 60) return "⭐⭐⭐⭐ Muy bueno";
            if (OverallScore >= 40) return "⭐⭐⭐ Bueno";
            if (OverallScore >= 20) return "⭐⭐ Regular";
            return "⭐ Bajo";
        }
    }
}
