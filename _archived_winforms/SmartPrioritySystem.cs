using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown
{
    /// <summary>
    /// Sistema de prioridades inteligente para descargas
    /// Asigna prioridad automática basada en múltiples factores
    /// </summary>
    public class SmartPrioritySystem
    {
        private readonly HashSet<string> favoriteAuthors;
        private readonly Dictionary<string, int> authorDownloadCount;
        private readonly Dictionary<string, DateTime> lastDownloadTime;
        
        // Pesos para cálculo de prioridad
        private const double WEIGHT_RARITY = 0.30;        // 30% - Rareza (pocos seeders)
        private const double WEIGHT_SIZE = 0.20;          // 20% - Tamaño (pequeños primero)
        private const double WEIGHT_AUTHOR = 0.25;        // 25% - Autor favorito
        private const double WEIGHT_QUALITY = 0.15;       // 15% - Calidad (bitrate, formato)
        private const double WEIGHT_RECENCY = 0.10;       // 10% - Reciente en búsqueda
        
        public SmartPrioritySystem()
        {
            favoriteAuthors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            authorDownloadCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            lastDownloadTime = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Calcula la prioridad de una descarga (0-100)
        /// </summary>
        public int CalculatePriority(DownloadCandidate candidate)
        {
            double score = 0;
            
            // Factor 1: Rareza (menos seeders = mayor prioridad)
            score += CalculateRarityScore(candidate.SeedersCount) * WEIGHT_RARITY;
            
            // Factor 2: Tamaño (archivos pequeños primero)
            score += CalculateSizeScore(candidate.FileSizeBytes) * WEIGHT_SIZE;
            
            // Factor 3: Autor favorito
            score += CalculateAuthorScore(candidate.Author) * WEIGHT_AUTHOR;
            
            // Factor 4: Calidad del archivo
            score += CalculateQualityScore(candidate.Filename, candidate.Bitrate) * WEIGHT_QUALITY;
            
            // Factor 5: Recencia
            score += CalculateRecencyScore(candidate.SearchTime) * WEIGHT_RECENCY;
            
            // Convertir a escala 0-100
            int priority = (int)(score * 100);
            
            // Aplicar boost manual si existe
            if (candidate.ManualPriorityBoost > 0)
            {
                priority = Math.Min(100, priority + candidate.ManualPriorityBoost);
            }
            
            return Math.Max(0, Math.Min(100, priority));
        }
        
        /// <summary>
        /// Calcula score de rareza (0.0 - 1.0)
        /// Menos seeders = más raro = mayor prioridad
        /// </summary>
        private double CalculateRarityScore(int seedersCount)
        {
            if (seedersCount <= 0)
                return 1.0; // Muy raro, máxima prioridad
            
            if (seedersCount == 1)
                return 0.95; // Solo 1 seeder
            
            if (seedersCount <= 3)
                return 0.80; // Pocos seeders
            
            if (seedersCount <= 10)
                return 0.50; // Moderado
            
            return 0.20; // Muchos seeders, baja prioridad
        }
        
        /// <summary>
        /// Calcula score de tamaño (0.0 - 1.0)
        /// Archivos pequeños primero
        /// </summary>
        private double CalculateSizeScore(long fileSizeBytes)
        {
            const long SIZE_1MB = 1024 * 1024;
            const long SIZE_10MB = 10 * SIZE_1MB;
            const long SIZE_100MB = 100 * SIZE_1MB;
            const long SIZE_1GB = 1024 * SIZE_1MB;
            
            if (fileSizeBytes < SIZE_1MB)
                return 1.0; // < 1MB - Muy pequeño
            
            if (fileSizeBytes < SIZE_10MB)
                return 0.90; // < 10MB - Pequeño
            
            if (fileSizeBytes < SIZE_100MB)
                return 0.70; // < 100MB - Mediano
            
            if (fileSizeBytes < SIZE_1GB)
                return 0.40; // < 1GB - Grande
            
            return 0.20; // > 1GB - Muy grande
        }
        
        /// <summary>
        /// Calcula score de autor (0.0 - 1.0)
        /// </summary>
        private double CalculateAuthorScore(string author)
        {
            if (string.IsNullOrWhiteSpace(author))
                return 0.5;
            
            // Autor favorito
            if (favoriteAuthors.Contains(author))
                return 1.0;
            
            // Autor con historial de descargas
            if (authorDownloadCount.TryGetValue(author, out int count))
            {
                if (count >= 10)
                    return 0.85; // Autor frecuente
                
                if (count >= 5)
                    return 0.70; // Autor conocido
                
                return 0.60; // Autor con algunas descargas
            }
            
            return 0.50; // Autor nuevo
        }
        
        /// <summary>
        /// Calcula score de calidad (0.0 - 1.0)
        /// </summary>
        private double CalculateQualityScore(string filename, int bitrate)
        {
            double score = 0.5; // Base
            
            // Formato de archivo
            var ext = System.IO.Path.GetExtension(filename)?.ToLowerInvariant();
            
            if (ext == ".flac" || ext == ".wav" || ext == ".ape")
                score += 0.30; // Lossless
            else if (ext == ".mp3" || ext == ".m4a" || ext == ".ogg")
                score += 0.15; // Lossy de calidad
            else if (ext == ".pdf" || ext == ".epub" || ext == ".mobi")
                score += 0.20; // Documentos
            
            // Bitrate (solo para audio)
            if (bitrate > 0)
            {
                if (bitrate >= 320)
                    score += 0.20; // Alta calidad
                else if (bitrate >= 256)
                    score += 0.15; // Buena calidad
                else if (bitrate >= 192)
                    score += 0.10; // Calidad aceptable
            }
            
            return Math.Min(1.0, score);
        }
        
        /// <summary>
        /// Calcula score de recencia (0.0 - 1.0)
        /// Resultados más recientes tienen mayor prioridad
        /// </summary>
        private double CalculateRecencyScore(DateTime searchTime)
        {
            var age = DateTime.Now - searchTime;
            
            if (age.TotalMinutes < 5)
                return 1.0; // Muy reciente
            
            if (age.TotalMinutes < 30)
                return 0.80; // Reciente
            
            if (age.TotalHours < 2)
                return 0.60; // Moderado
            
            if (age.TotalHours < 24)
                return 0.40; // Antiguo
            
            return 0.20; // Muy antiguo
        }
        
        /// <summary>
        /// Agrega un autor a favoritos
        /// </summary>
        public void AddFavoriteAuthor(string author)
        {
            if (!string.IsNullOrWhiteSpace(author))
            {
                favoriteAuthors.Add(author);
            }
        }
        
        /// <summary>
        /// Elimina un autor de favoritos
        /// </summary>
        public void RemoveFavoriteAuthor(string author)
        {
            favoriteAuthors.Remove(author);
        }
        
        /// <summary>
        /// Registra una descarga completada (para aprendizaje)
        /// </summary>
        public void RecordDownload(string author)
        {
            if (string.IsNullOrWhiteSpace(author))
                return;
            
            if (authorDownloadCount.ContainsKey(author))
                authorDownloadCount[author]++;
            else
                authorDownloadCount[author] = 1;
            
            lastDownloadTime[author] = DateTime.Now;
        }
        
        /// <summary>
        /// Obtiene recomendación de prioridad como enum
        /// </summary>
        public DownloadPriority GetPriorityLevel(int priorityScore)
        {
            if (priorityScore >= 80)
                return DownloadPriority.Critical;
            
            if (priorityScore >= 60)
                return DownloadPriority.High;
            
            if (priorityScore >= 40)
                return DownloadPriority.Normal;
            
            return DownloadPriority.Low;
        }
        
        /// <summary>
        /// Ordena una lista de candidatos por prioridad
        /// </summary>
        public List<DownloadCandidate> SortByPriority(List<DownloadCandidate> candidates)
        {
            foreach (var candidate in candidates)
            {
                candidate.CalculatedPriority = CalculatePriority(candidate);
                candidate.PriorityLevel = GetPriorityLevel(candidate.CalculatedPriority);
            }
            
            return candidates
                .OrderByDescending(c => c.CalculatedPriority)
                .ThenBy(c => c.FileSizeBytes)
                .ToList();
        }
        
        /// <summary>
        /// Obtiene estadísticas del sistema
        /// </summary>
        public PrioritySystemStats GetStats()
        {
            return new PrioritySystemStats
            {
                FavoriteAuthorsCount = favoriteAuthors.Count,
                TrackedAuthorsCount = authorDownloadCount.Count,
                TotalDownloadsTracked = authorDownloadCount.Values.Sum(),
                TopAuthors = authorDownloadCount
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(5)
                    .Select(kvp => new AuthorStats
                    {
                        Author = kvp.Key,
                        DownloadCount = kvp.Value,
                        LastDownload = lastDownloadTime.GetValueOrDefault(kvp.Key)
                    })
                    .ToList()
            };
        }
    }
    
    /// <summary>
    /// Candidato para descarga con metadatos
    /// </summary>
    public class DownloadCandidate
    {
        public string Filename { get; set; }
        public string Author { get; set; }
        public long FileSizeBytes { get; set; }
        public int SeedersCount { get; set; }
        public int Bitrate { get; set; }
        public DateTime SearchTime { get; set; } = DateTime.Now;
        public int ManualPriorityBoost { get; set; } = 0;
        
        // Calculados por el sistema
        public int CalculatedPriority { get; set; }
        public DownloadPriority PriorityLevel { get; set; }
    }
    
    /// <summary>
    /// Nivel de prioridad
    /// </summary>
    public enum DownloadPriority
    {
        Low = 0,        // Prioridad baja (score 0-39)
        Normal = 1,     // Prioridad normal (score 40-59)
        High = 2,       // Prioridad alta (score 60-79)
        Critical = 3    // Prioridad crítica (score 80-100)
    }
    
    /// <summary>
    /// Estadísticas del sistema de prioridades
    /// </summary>
    public class PrioritySystemStats
    {
        public int FavoriteAuthorsCount { get; set; }
        public int TrackedAuthorsCount { get; set; }
        public int TotalDownloadsTracked { get; set; }
        public List<AuthorStats> TopAuthors { get; set; }
        
        public override string ToString()
        {
            return $"Priority System: " +
                   $"Favoritos: {FavoriteAuthorsCount} | " +
                   $"Autores rastreados: {TrackedAuthorsCount} | " +
                   $"Descargas totales: {TotalDownloadsTracked}";
        }
    }
    
    /// <summary>
    /// Estadísticas de un autor
    /// </summary>
    public class AuthorStats
    {
        public string Author { get; set; }
        public int DownloadCount { get; set; }
        public DateTime LastDownload { get; set; }
    }
}
