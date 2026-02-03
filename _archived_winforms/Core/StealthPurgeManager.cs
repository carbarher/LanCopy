using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Gestor de purga en modo Stealth - Evita detección por servidores restrictivos
    /// </summary>
    public class StealthPurgeManager
    {
        private readonly RateLimitDetector _rateLimitDetector;
        private readonly Random _random = new();
        
        public enum StealthMode
        {
            Conservative,  // 4-6 segundos, pausas cada 10
            Stealth,       // 6-15 segundos, pausas cada 5
            UltraStealth   // 10-30 segundos, pausas cada 3
        }
        
        public StealthPurgeManager(RateLimitDetector rateLimitDetector)
        {
            _rateLimitDetector = rateLimitDetector;
        }
        
        public class StealthConfig
        {
            public StealthMode Mode { get; set; } = StealthMode.Conservative;
            public int MinDelayMs { get; set; } = 4000;
            public int MaxDelayMs { get; set; } = 6000;
            public int PauseEveryAuthors { get; set; } = 10;
            public int PauseDurationMs { get; set; } = 30000;
            public bool EnableRandomJitter { get; set; } = true;
            public bool EnableTimeBasedDelays { get; set; } = true;
            public bool EnableServerAwareDelays { get; set; } = true;
        }
        
        /// <summary>
        /// Ejecuta purga en modo Stealth
        /// </summary>
        public async Task<PurgeResult> ExecuteStealthPurgeAsync<T>(
            IEnumerable<T> items,
            Func<T, Task<bool>> validator,
            StealthConfig config = null,
            IProgress<PurgeProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            config ??= GetDefaultConfig();
            var itemsList = items.ToList();
            var result = new PurgeResult { TotalItems = itemsList.Count };
            
            Console.WriteLine($"Iniciando purga en modo {config.Mode} ({itemsList.Count} items)");
            Console.WriteLine($"Delays: {config.MinDelayMs}-{config.MaxDelayMs}ms | Pausas cada {config.PauseEveryAuthors} ({config.PauseDurationMs/1000}s)");
            
            var startTime = DateTime.UtcNow;
            
            for (int i = 0; i < itemsList.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var item = itemsList[i];
                var itemStartTime = DateTime.UtcNow;
                
                try
                {
                    // Calcular delay adaptativo antes de la operación
                    var preDelay = CalculateStealthDelay(config, i, result);
                    if (preDelay > 0)
                    {
                        Console.WriteLine($"Stealth delay: {preDelay}ms antes del item {i+1}");
                        await Task.Delay(preDelay, cancellationToken);
                    }
                    
                    // Ejecutar validación
                    var isValid = await validator(item);
                    
                    if (isValid)
                    {
                        result.ValidItems++;
                        _rateLimitDetector.RegisterSuccess();
                    }
                    else
                    {
                        result.InvalidItems++;
                        result.RemovedItems.Add(item);
                    }
                    
                    result.ProcessedItems++;
                    
                    // Calcular delay post-operación
                    var postDelay = CalculateStealthDelay(config, i, result, isPostDelay: true);
                    if (postDelay > 0)
                    {
                        await Task.Delay(postDelay, cancellationToken);
                    }
                    
                    // Pausas estratégicas
                    if (ShouldTakeStrategicPause(i, config))
                    {
                        var pauseDuration = CalculateStrategicPauseDuration(config);
                        Console.WriteLine($"Pausa estratégica de {pauseDuration/1000}s después de {i+1} items");
                        await Task.Delay(pauseDuration, cancellationToken);
                    }
                    
                    // Reportar progreso
                    progress?.Report(new PurgeProgress
                    {
                        ProcessedItems = result.ProcessedItems,
                        TotalItems = result.TotalItems,
                        ValidItems = result.ValidItems,
                        InvalidItems = result.InvalidItems,
                        ElapsedTime = DateTime.UtcNow - startTime,
                        EstimatedTimeRemaining = EstimateRemainingTime(i, itemsList.Count, startTime, config)
                    });
                    
                }
                catch (Exception ex)
                {
                    result.FailedItems++;
                    result.Errors.Add($"{item}: {ex.Message}");
                    _rateLimitDetector.RegisterFailure(exception: ex);
                    
                    Console.WriteLine($"Error procesando item {i+1}: {ex.Message}");
                    
                    // En caso de error, aumentar delay
                    var errorDelay = Math.Min(config.MaxDelayMs * 2, 60000);
                    await Task.Delay(errorDelay, cancellationToken);
                }
                
                // Simular comportamiento humano - variaciones aleatorias
                if (config.EnableRandomJitter && _random.Next(0, 100) < 10) // 10% de probabilidad
                {
                    var randomPause = _random.Next(5000, 15000);
                    Console.WriteLine($"🎲 Pausa aleatoria de {randomPause/1000}s (simulación humana)");
                    await Task.Delay(randomPause, cancellationToken);
                }
            }
            
            result.ElapsedTime = DateTime.UtcNow - startTime;
            result.AverageItemsPerMinute = result.ProcessedItems / Math.Max(result.ElapsedTime.TotalMinutes, 1);
            
            Console.WriteLine($"Purga completada en {result.ElapsedTime.TotalMinutes:F1} minutos");
            Console.WriteLine($"Resultado: {result.ValidItems} válidos, {result.InvalidItems} inválidos, {result.FailedItems} errores");
            Console.WriteLine($"Velocidad: {result.AverageItemsPerMinute:F1} items/minuto");
            
            return result;
        }
        
        /// <summary>
        /// Calcula delay adaptativo en modo Stealth
        /// </summary>
        private int CalculateStealthDelay(StealthConfig config, int currentIndex, PurgeResult result, bool isPostDelay = false)
        {
            var baseDelay = _random.Next(config.MinDelayMs, config.MaxDelayMs);
            
            // Ajustar según estadísticas del servidor
            if (config.EnableServerAwareDelays)
            {
                var serverStats = _rateLimitDetector.GetStats();
                if (serverStats.IsRateLimited)
                {
                    baseDelay = Math.Max(baseDelay, serverStats.CurrentDelayMs);
                }
                else if (serverStats.ConsecutiveFailures > 0)
                {
                    baseDelay = (int)(baseDelay * (1 + serverStats.ConsecutiveFailures * 0.5));
                }
            }
            
            // Ajustar según hora del día (simular patrones humanos)
            if (config.EnableTimeBasedDelays)
            {
                var hour = DateTime.UtcNow.Hour;
                if (hour >= 2 && hour <= 6) // Madrugada - más lento
                {
                    baseDelay = (int)(baseDelay * 1.5);
                }
                else if (hour >= 19 && hour <= 23) // Noche - moderado
                {
                    baseDelay = (int)(baseDelay * 1.2);
                }
            }
            
            // Ajustar según tasa de errores reciente
            if (result.FailedItems > 0)
            {
                var errorRate = (double)result.FailedItems / Math.Max(result.ProcessedItems, 1);
                if (errorRate > 0.1) // Más de 10% de errores
                {
                    baseDelay = (int)(baseDelay * 1.5);
                }
            }
            
            // Agregar jitter
            var jitter = _random.Next(0, baseDelay / 5); // 0-20% de jitter
            
            return baseDelay + jitter;
        }
        
        /// <summary>
        /// Determina si se debe tomar una pausa estratégica
        /// </summary>
        private bool ShouldTakeStrategicPause(int currentIndex, StealthConfig config)
        {
            return (currentIndex + 1) % config.PauseEveryAuthors == 0;
        }
        
        /// <summary>
        /// Calcula duración de pausa estratégica
        /// </summary>
        private int CalculateStrategicPauseDuration(StealthConfig config)
        {
            var basePause = config.PauseDurationMs;
            
            // Variación aleatoria ±25%
            var variation = basePause / 4;
            return basePause + _random.Next(-variation, variation);
        }
        
        /// <summary>
        /// Estima tiempo restante
        /// </summary>
        private TimeSpan EstimateRemainingTime(int processed, int total, DateTime startTime, StealthConfig config)
        {
            if (processed == 0) return TimeSpan.Zero;
            
            var elapsed = DateTime.UtcNow - startTime;
            var avgTimePerItem = elapsed.TotalMilliseconds / processed;
            
            var remainingItems = total - processed;
            var estimatedMs = remainingItems * avgTimePerItem;
            
            // Agregar tiempo de pausas estratégicas
            var strategicPauses = remainingItems / config.PauseEveryAuthors;
            estimatedMs += strategicPauses * config.PauseDurationMs;
            
            return TimeSpan.FromMilliseconds(estimatedMs);
        }
        
        /// <summary>
        /// Obtiene configuración por defecto según modo
        /// </summary>
        private StealthConfig GetDefaultConfig()
        {
            return new StealthConfig
            {
                Mode = StealthMode.Conservative,
                MinDelayMs = 4000,
                MaxDelayMs = 6000,
                PauseEveryAuthors = 10,
                PauseDurationMs = 30000,
                EnableRandomJitter = true,
                EnableTimeBasedDelays = true,
                EnableServerAwareDelays = true
            };
        }
        
        /// <summary>
        /// Crea configuración para modo específico
        /// </summary>
        public static StealthConfig CreateConfig(StealthMode mode)
        {
            return mode switch
            {
                StealthMode.Conservative => new StealthConfig
                {
                    Mode = mode,
                    MinDelayMs = 4000,
                    MaxDelayMs = 6000,
                    PauseEveryAuthors = 10,
                    PauseDurationMs = 30000
                },
                StealthMode.Stealth => new StealthConfig
                {
                    Mode = mode,
                    MinDelayMs = 6000,
                    MaxDelayMs = 15000,
                    PauseEveryAuthors = 5,
                    PauseDurationMs = 45000
                },
                StealthMode.UltraStealth => new StealthConfig
                {
                    Mode = mode,
                    MinDelayMs = 10000,
                    MaxDelayMs = 30000,
                    PauseEveryAuthors = 3,
                    PauseDurationMs = 60000
                },
                _ => new StealthConfig()
            };
        }
    }
    
    /// <summary>
    /// Resultado de operación de purga
    /// </summary>
    public class PurgeResult
    {
        public int TotalItems { get; set; }
        public int ProcessedItems { get; set; }
        public int ValidItems { get; set; }
        public int InvalidItems { get; set; }
        public int FailedItems { get; set; }
        public List<object> RemovedItems { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public TimeSpan ElapsedTime { get; set; }
        public double AverageItemsPerMinute { get; set; }
    }
    
    /// <summary>
    /// Progreso de operación de purga
    /// </summary>
    public class PurgeProgress
    {
        public int ProcessedItems { get; set; }
        public int TotalItems { get; set; }
        public int ValidItems { get; set; }
        public int InvalidItems { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
        public double PercentComplete => TotalItems > 0 ? (double)ProcessedItems / TotalItems * 100 : 0;
    }
}
