using System;
using System.Collections.Concurrent;
using System.Linq;

namespace SlskDown
{
    /// <summary>
    /// Sistema de paralelismo adaptativo que ajusta dinámicamente
    /// el nivel de concurrencia basado en tasas de éxito/fallo
    /// </summary>
    public class AdaptiveParallelism
    {
        private int currentParallelism;
        private readonly int minParallelism;
        private readonly int maxParallelism;
        private readonly int adjustmentStep;
        
        private readonly ConcurrentQueue<bool> recentResults;
        private readonly int windowSize;
        
        private double successRate = 1.0;
        private DateTime lastAdjustment = DateTime.Now;
        private readonly TimeSpan adjustmentCooldown = TimeSpan.FromSeconds(30);
        
        public int CurrentParallelism => currentParallelism;
        public double SuccessRate => successRate;
        
        public AdaptiveParallelism(int initialParallelism, int minParallelism, int maxParallelism, int adjustmentStep = 10, int windowSize = 100)
        {
            this.currentParallelism = Math.Clamp(initialParallelism, minParallelism, maxParallelism);
            this.minParallelism = minParallelism;
            this.maxParallelism = maxParallelism;
            this.adjustmentStep = adjustmentStep;
            this.windowSize = windowSize;
            this.recentResults = new ConcurrentQueue<bool>();
        }
        
        /// <summary>
        /// Registra el resultado de una operación (true = éxito, false = fallo)
        /// </summary>
        public void RecordResult(bool success)
        {
            recentResults.Enqueue(success);
            
            // Mantener solo los últimos N resultados
            while (recentResults.Count > windowSize)
            {
                recentResults.TryDequeue(out _);
            }
            
            // Recalcular tasa de éxito
            if (recentResults.Count > 0)
            {
                successRate = recentResults.Count(r => r) / (double)recentResults.Count;
            }
        }
        
        /// <summary>
        /// Obtiene el nivel óptimo de paralelismo basado en el rendimiento reciente
        /// </summary>
        public int GetOptimalParallelism()
        {
            // Solo ajustar si ha pasado el tiempo de cooldown
            if ((DateTime.Now - lastAdjustment) < adjustmentCooldown)
            {
                return currentParallelism;
            }
            
            // Necesitamos suficientes datos para tomar decisiones
            if (recentResults.Count < windowSize / 2)
            {
                return currentParallelism;
            }
            
            int newParallelism = currentParallelism;
            
            // Si tasa de éxito > 90%, aumentar paralelismo (más agresivo)
            if (successRate > 0.90 && currentParallelism < maxParallelism)
            {
                newParallelism = Math.Min(currentParallelism + adjustmentStep, maxParallelism);
            }
            // Si tasa de éxito entre 80-90%, aumentar ligeramente
            else if (successRate > 0.80 && successRate <= 0.90 && currentParallelism < maxParallelism)
            {
                newParallelism = Math.Min(currentParallelism + (adjustmentStep / 2), maxParallelism);
            }
            // Si tasa de éxito entre 60-70%, reducir ligeramente
            else if (successRate >= 0.60 && successRate < 0.70 && currentParallelism > minParallelism)
            {
                newParallelism = Math.Max(currentParallelism - (adjustmentStep / 2), minParallelism);
            }
            // Si tasa de éxito < 60%, reducir paralelismo (más conservador)
            else if (successRate < 0.60 && currentParallelism > minParallelism)
            {
                newParallelism = Math.Max(currentParallelism - adjustmentStep, minParallelism);
            }
            
            // Si hubo cambio, actualizar y registrar
            if (newParallelism != currentParallelism)
            {
                currentParallelism = newParallelism;
                lastAdjustment = DateTime.Now;
            }
            
            return currentParallelism;
        }
        
        /// <summary>
        /// Fuerza un ajuste inmediato del paralelismo
        /// </summary>
        public void ForceAdjustment()
        {
            lastAdjustment = DateTime.MinValue;
            GetOptimalParallelism();
        }
        
        /// <summary>
        /// Reinicia las estadísticas
        /// </summary>
        public void Reset()
        {
            while (recentResults.TryDequeue(out _)) { }
            successRate = 1.0;
            lastAdjustment = DateTime.Now;
        }
        
        /// <summary>
        /// Obtiene un resumen del estado actual
        /// </summary>
        public string GetStatusSummary()
        {
            return $"Paralelismo: {currentParallelism} (rango: {minParallelism}-{maxParallelism}) | " +
                   $"Tasa de éxito: {successRate:P1} | " +
                   $"Muestras: {recentResults.Count}/{windowSize}";
        }
    }
}
