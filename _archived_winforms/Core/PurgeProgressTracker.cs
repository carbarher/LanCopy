using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core
{
    /// <summary>
    /// MEJORA #7: Progreso granular con ETA en purga
    /// Rastrea el progreso de la purga y calcula el tiempo estimado de finalización
    /// basándose en el historial de velocidad de procesamiento.
    /// </summary>
    public class PurgeProgressTracker
    {
        private readonly int totalItems;
        private readonly DateTime startTime;
        private readonly List<(DateTime timestamp, int itemsProcessed)> progressHistory;
        private readonly int historyWindowSize;
        
        public int ItemsProcessed { get; private set; }
        public int ItemsRemaining => totalItems - ItemsProcessed;
        public double ProgressPercent => totalItems > 0 ? (ItemsProcessed / (double)totalItems) * 100.0 : 0.0;
        public TimeSpan Elapsed => DateTime.Now - startTime;
        public double ElapsedSeconds => Elapsed.TotalSeconds;
        
        public PurgeProgressTracker(int totalItems, int historyWindowSize = 20)
        {
            this.totalItems = totalItems;
            this.startTime = DateTime.Now;
            this.historyWindowSize = historyWindowSize;
            this.progressHistory = new List<(DateTime, int)>();
            this.ItemsProcessed = 0;
        }
        
        /// <summary>
        /// Registra el progreso de un item procesado
        /// </summary>
        public void RecordProgress(int itemsProcessed = 1)
        {
            ItemsProcessed += itemsProcessed;
            
            // Agregar al historial
            progressHistory.Add((DateTime.Now, ItemsProcessed));
            
            // Mantener solo las últimas N entradas
            if (progressHistory.Count > historyWindowSize)
            {
                progressHistory.RemoveAt(0);
            }
        }
        
        /// <summary>
        /// Calcula el ETA basándose en la velocidad promedio reciente
        /// </summary>
        public TimeSpan? CalculateETA()
        {
            if (ItemsProcessed == 0 || ItemsRemaining == 0)
                return null;
            
            // Usar historial reciente si está disponible
            if (progressHistory.Count >= 2)
            {
                var recentSpeed = CalculateRecentSpeed();
                if (recentSpeed > 0)
                {
                    var secondsRemaining = ItemsRemaining / recentSpeed;
                    return TimeSpan.FromSeconds(secondsRemaining);
                }
            }
            
            // Fallback: usar velocidad promedio total
            var overallSpeed = ItemsProcessed / Elapsed.TotalSeconds;
            if (overallSpeed > 0)
            {
                var secondsRemaining = ItemsRemaining / overallSpeed;
                return TimeSpan.FromSeconds(secondsRemaining);
            }
            
            return null;
        }
        
        /// <summary>
        /// Calcula la velocidad de procesamiento reciente (items/segundo)
        /// </summary>
        private double CalculateRecentSpeed()
        {
            if (progressHistory.Count < 2)
                return 0;
            
            var oldest = progressHistory.First();
            var newest = progressHistory.Last();
            
            var timeDiff = (newest.timestamp - oldest.timestamp).TotalSeconds;
            var itemsDiff = newest.itemsProcessed - oldest.itemsProcessed;
            
            return timeDiff > 0 ? itemsDiff / timeDiff : 0;
        }
        
        /// <summary>
        /// Obtiene la velocidad de procesamiento actual (items/minuto)
        /// </summary>
        public double GetCurrentSpeed()
        {
            var speed = CalculateRecentSpeed();
            return speed * 60.0; // Convertir a items/minuto
        }
        
        /// <summary>
        /// Obtiene la velocidad promedio total (items/minuto)
        /// </summary>
        public double GetAverageSpeed()
        {
            if (Elapsed.TotalSeconds == 0)
                return 0;
            
            return (ItemsProcessed / Elapsed.TotalSeconds) * 60.0;
        }
        
        /// <summary>
        /// Genera un string de progreso formateado para mostrar en UI
        /// </summary>
        public string GetProgressString(bool includeETA = true, bool includeSpeed = true)
        {
            var parts = new List<string>();
            
            // Progreso básico
            parts.Add($"{ItemsProcessed}/{totalItems} ({ProgressPercent:F1}%)");
            
            // Velocidad
            if (includeSpeed && ItemsProcessed > 0)
            {
                var speed = GetCurrentSpeed();
                if (speed > 0)
                {
                    parts.Add($"{speed:F1} items/min");
                }
            }
            
            // ETA
            if (includeETA)
            {
                var eta = CalculateETA();
                if (eta.HasValue)
                {
                    parts.Add($"ETA: {FormatTimeSpan(eta.Value)}");
                }
            }
            
            return string.Join(" | ", parts);
        }
        
        /// <summary>
        /// Genera un string detallado con todas las estadísticas
        /// </summary>
        public string GetDetailedStats()
        {
            var lines = new List<string>();
            
            lines.Add($"Progreso: {ItemsProcessed}/{totalItems} ({ProgressPercent:F1}%)");
            lines.Add($"Tiempo transcurrido: {FormatTimeSpan(Elapsed)}");
            
            var eta = CalculateETA();
            if (eta.HasValue)
            {
                lines.Add($"Tiempo estimado restante: {FormatTimeSpan(eta.Value)}");
                lines.Add($"Finalización estimada: {DateTime.Now.Add(eta.Value):HH:mm:ss}");
            }
            
            var currentSpeed = GetCurrentSpeed();
            var avgSpeed = GetAverageSpeed();
            
            if (currentSpeed > 0)
            {
                lines.Add($"Velocidad actual: {currentSpeed:F1} items/min");
            }
            
            if (avgSpeed > 0)
            {
                lines.Add($"Velocidad promedio: {avgSpeed:F1} items/min");
            }
            
            return string.Join("\n", lines);
        }
        
        /// <summary>
        /// Formatea un TimeSpan de manera legible
        /// </summary>
        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalSeconds < 60)
            {
                return $"{ts.TotalSeconds:F0}s";
            }
            else if (ts.TotalMinutes < 60)
            {
                return $"{ts.TotalMinutes:F0}m {ts.Seconds}s";
            }
            else if (ts.TotalHours < 24)
            {
                return $"{ts.Hours}h {ts.Minutes}m";
            }
            else
            {
                return $"{ts.Days}d {ts.Hours}h";
            }
        }
        
        /// <summary>
        /// Genera una barra de progreso visual en texto
        /// </summary>
        public string GetProgressBar(int width = 20)
        {
            if (totalItems == 0)
                return "[" + new string(' ', width) + "]";
            
            var filled = (int)(ProgressPercent / 100.0 * width);
            filled = Math.Max(0, Math.Min(width, filled));
            
            var bar = new string('█', filled) + new string('░', width - filled);
            return $"[{bar}]";
        }
        
        /// <summary>
        /// Obtiene un resumen compacto para logs
        /// </summary>
        public string GetCompactSummary()
        {
            var eta = CalculateETA();
            var etaStr = eta.HasValue ? $" | ETA {FormatTimeSpan(eta.Value)}" : "";
            
            return $"{GetProgressBar(10)} {ProgressPercent:F1}%{etaStr}";
        }
    }
}
