using System;
using System.Collections.Generic;
using System.Linq;
using SlskDown.Models;
using SlskDown.Core.Statistics;

namespace SlskDown.Core.Prioritization
{
    /// <summary>
    /// Sistema de priorización dinámica de descargas inspirado en Nicotine+
    /// Calcula prioridad basada en múltiples factores para optimizar eficiencia
    /// </summary>
    public class DynamicDownloadPrioritizer
    {
        private readonly TransferStatistics transferStats;
        private readonly Func<string, bool> isUserOnlineFunc;

        public DynamicDownloadPrioritizer(TransferStatistics stats, Func<string, bool> isUserOnlineFunc = null)
        {
            this.transferStats = stats;
            this.isUserOnlineFunc = isUserOnlineFunc;
        }

        /// <summary>
        /// Calcula la prioridad de una descarga basada en múltiples factores
        /// </summary>
        public double CalculatePriority(DownloadTask task)
        {
            if (task?.File == null)
                return 0;

            double priority = 0;

            // Factor 1: Prioridad manual del usuario (peso más alto)
            priority += GetManualPriorityScore(task);

            // Factor 2: Velocidad histórica del proveedor
            priority += GetProviderSpeedScore(task.File.Username);

            // Factor 3: Tamaño del archivo (pequeños primero)
            priority += GetFileSizeScore(task.File.SizeBytes);

            // Factor 4: Tiempo en cola (FIFO con peso)
            priority += GetQueueTimeScore(task);

            // Factor 5: Tasa de éxito del proveedor
            priority += GetSuccessRateScore(task.File.Username);

            // Factor 6: Disponibilidad del proveedor
            priority += GetAvailabilityScore(task.File.Username);

            // Factor 7: Número de reintentos (penalizar muchos reintentos)
            priority += GetRetryPenalty(task);

            return priority;
        }

        /// <summary>
        /// Reordena una lista de descargas por prioridad
        /// </summary>
        public List<DownloadTask> ReorderByPriority(List<DownloadTask> tasks)
        {
            return tasks
                .Select(t => new { Task = t, Priority = CalculatePriority(t) })
                .OrderByDescending(x => x.Priority)
                .Select(x => x.Task)
                .ToList();
        }

        /// <summary>
        /// Obtiene estadísticas de priorización para debugging
        /// </summary>
        public PriorityBreakdown GetPriorityBreakdown(DownloadTask task)
        {
            return new PriorityBreakdown
            {
                ManualPriority = GetManualPriorityScore(task),
                ProviderSpeed = GetProviderSpeedScore(task.File.Username),
                FileSize = GetFileSizeScore(task.File.SizeBytes),
                QueueTime = GetQueueTimeScore(task),
                SuccessRate = GetSuccessRateScore(task.File.Username),
                Availability = GetAvailabilityScore(task.File.Username),
                RetryPenalty = GetRetryPenalty(task),
                TotalPriority = CalculatePriority(task)
            };
        }

        // Factores de priorización

        private double GetManualPriorityScore(DownloadTask task)
        {
            // Prioridad manual tiene el peso más alto
            return task.Priority switch
            {
                SlskDown.Models.DownloadPriority.High => 1000,
                SlskDown.Models.DownloadPriority.Normal => 0,
                SlskDown.Models.DownloadPriority.Low => -500,
                _ => 0
            };
        }

        private double GetProviderSpeedScore(string username)
        {
            if (transferStats == null || string.IsNullOrEmpty(username))
                return 0;

            var userStats = transferStats.GetUserStats(username);
            if (userStats == null || userStats.TotalTransfers == 0)
                return 0;

            // Velocidad promedio en KB/s / 100 = puntos
            // Ejemplo: 2500 KB/s = 25 puntos
            return userStats.AverageSpeed / 100;
        }

        private double GetFileSizeScore(long sizeBytes)
        {
            // Priorizar archivos pequeños para completarlos rápido
            const long SIZE_10MB = 10 * 1024 * 1024;
            const long SIZE_50MB = 50 * 1024 * 1024;
            const long SIZE_100MB = 100 * 1024 * 1024;

            if (sizeBytes < SIZE_10MB)
                return 500;  // Muy pequeño, alta prioridad
            else if (sizeBytes < SIZE_50MB)
                return 200;  // Pequeño, prioridad media
            else if (sizeBytes < SIZE_100MB)
                return 50;   // Mediano, prioridad baja
            else
                return 0;    // Grande, sin bonus
        }

        private double GetQueueTimeScore(DownloadTask task)
        {
            if (!task.StartTime.HasValue)
                return 0;

            // +1 punto por cada minuto en cola
            var queueTime = DateTime.Now - task.StartTime.Value;
            return queueTime.TotalMinutes;
        }

        private double GetSuccessRateScore(string username)
        {
            if (transferStats == null || string.IsNullOrEmpty(username))
                return 0;

            var userStats = transferStats.GetUserStats(username);
            if (userStats == null || userStats.TotalTransfers == 0)
                return 0;

            // Tasa de éxito * 100 = puntos
            // Ejemplo: 85% éxito = 85 puntos
            return userStats.SuccessRate * 100;
        }

        private double GetAvailabilityScore(string username)
        {
            if (isUserOnlineFunc == null || string.IsNullOrEmpty(username))
                return 0;

            // Bonus si el usuario está online ahora
            return isUserOnlineFunc(username) ? 200 : 0;
        }

        private double GetRetryPenalty(DownloadTask task)
        {
            // Penalizar descargas con muchos reintentos
            // Cada reintento = -50 puntos
            return -task.RetryCount * 50;
        }
    }

    /// <summary>
    /// Desglose de prioridad para debugging
    /// </summary>
    public class PriorityBreakdown
    {
        public double ManualPriority { get; set; }
        public double ProviderSpeed { get; set; }
        public double FileSize { get; set; }
        public double QueueTime { get; set; }
        public double SuccessRate { get; set; }
        public double Availability { get; set; }
        public double RetryPenalty { get; set; }
        public double TotalPriority { get; set; }

        public override string ToString()
        {
            return $"Total: {TotalPriority:F0} = Manual:{ManualPriority:F0} + Speed:{ProviderSpeed:F0} + " +
                   $"Size:{FileSize:F0} + Queue:{QueueTime:F0} + Success:{SuccessRate:F0} + " +
                   $"Avail:{Availability:F0} + Retry:{RetryPenalty:F0}";
        }
    }

    /// <summary>
    /// Enum de prioridad manual (si no existe en Models)
    /// </summary>
    public enum DownloadPriority
    {
        Low = 0,
        Normal = 1,
        High = 2
    }
}
