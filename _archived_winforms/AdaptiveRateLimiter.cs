using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// Rate limiter adaptativo que ajusta límites según condiciones de red
    /// Mide latencia, ancho de banda y ajusta búsquedas/descargas automáticamente
    /// </summary>
    public class AdaptiveRateLimiter : IDisposable
    {
        private readonly System.Threading.Timer measurementTimer;
        private readonly Queue<double> latencyHistory;
        private readonly object lockObj = new object();
        
        // Configuración
        private const int MEASUREMENT_INTERVAL_MS = 10000; // 10 segundos
        private const int LATENCY_HISTORY_SIZE = 30; // 5 minutos de historial
        private const string PING_HOST = "8.8.8.8"; // Google DNS
        
        // Estado actual
        private NetworkQuality currentQuality = NetworkQuality.Unknown;
        private double currentLatencyMs = 0;
        private double averageLatencyMs = 0;
        private bool isNetworkSaturated = false;
        private DateTime lastMeasurement;
        
        // Límites actuales
        private int currentSearchesPerMinute = 15;
        private int currentParallelDownloads = 6;
        private bool isPaused = false;
        
        // Estadísticas
        private long totalMeasurements = 0;
        private long totalTimeouts = 0;
        private long totalAdjustments = 0;
        
        // Eventos
        public event Action<NetworkQuality, NetworkQuality> QualityChanged;
        public event Action<RateLimits> LimitsChanged;
        public event Action<string> NetworkPaused;
        public event Action<string> NetworkResumed;
        
        public AdaptiveRateLimiter()
        {
            latencyHistory = new Queue<double>(LATENCY_HISTORY_SIZE);
            lastMeasurement = DateTime.Now;
            
            // Iniciar mediciones periódicas
            System.Threading.Timer _timer = new System.Threading.Timer(async _ => await AdjustLimitsAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            measurementTimer = new System.Threading.Timer(async _ => await MeasureNetworkAsync(), null, System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
            measurementTimer.Change(MEASUREMENT_INTERVAL_MS, MEASUREMENT_INTERVAL_MS);
            
            Console.WriteLine("[RateLimiter] Inicializado - Mediciones cada 10s");
        }
        
        /// <summary>
        /// Mide la calidad de la red
        /// </summary>
        public async Task<NetworkMeasurement> MeasureNetworkAsync()
        {
            try
            {
                totalMeasurements++;
                
                // Medir latencia con ping
                var latency = await MeasureLatencyAsync();
                
                lock (lockObj)
                {
                    currentLatencyMs = latency;
                    
                    // Agregar al historial
                    latencyHistory.Enqueue(latency);
                    if (latencyHistory.Count > LATENCY_HISTORY_SIZE)
                        latencyHistory.Dequeue();
                    
                    // Calcular promedio
                    averageLatencyMs = latencyHistory.Average();
                }
                
                // Determinar calidad de red
                var previousQuality = currentQuality;
                currentQuality = DetermineNetworkQuality(latency);
                
                // Detectar saturación
                isNetworkSaturated = DetectSaturation();
                
                // Notificar cambio de calidad
                if (previousQuality != currentQuality && previousQuality != NetworkQuality.Unknown)
                {
                    Console.WriteLine($"[RateLimiter] Calidad de red: {previousQuality} → {currentQuality}");
                    QualityChanged?.Invoke(previousQuality, currentQuality);
                }
                
                // Ajustar límites si es necesario
                await AdjustLimitsAsync();
                
                lastMeasurement = DateTime.Now;
                
                return new NetworkMeasurement
                {
                    LatencyMs = latency,
                    AverageLatencyMs = averageLatencyMs,
                    Quality = currentQuality,
                    IsSaturated = isNetworkSaturated,
                    Timestamp = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RateLimiter] Error midiendo red: {ex.Message}");
                totalTimeouts++;
                return null;
            }
        }
        
        /// <summary>
        /// Mide latencia con ping
        /// </summary>
        private async Task<double> MeasureLatencyAsync()
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(PING_HOST, 5000);
                    
                    if (reply.Status == IPStatus.Success)
                    {
                        return reply.RoundtripTime;
                    }
                    else
                    {
                        totalTimeouts++;
                        return 999; // Timeout
                    }
                }
            }
            catch
            {
                totalTimeouts++;
                return 999;
            }
        }
        
        /// <summary>
        /// Determina la calidad de red según latencia
        /// </summary>
        private NetworkQuality DetermineNetworkQuality(double latencyMs)
        {
            if (latencyMs < 50)
                return NetworkQuality.Excellent;
            
            if (latencyMs < 100)
                return NetworkQuality.Good;
            
            if (latencyMs < 200)
                return NetworkQuality.Fair;
            
            if (latencyMs < 500)
                return NetworkQuality.Poor;
            
            return NetworkQuality.VeryPoor;
        }
        
        /// <summary>
        /// Detecta si la red está saturada
        /// </summary>
        private bool DetectSaturation()
        {
            lock (lockObj)
            {
                if (latencyHistory.Count < 5)
                    return false;
                
                // Saturación si latencia aumenta consistentemente
                var recent = latencyHistory.TakeLast(5).ToList();
                var older = latencyHistory.Take(latencyHistory.Count - 5).ToList();
                
                if (!older.Any())
                    return false;
                
                var recentAvg = recent.Average();
                var olderAvg = older.Average();
                
                // Saturado si latencia reciente es 2x mayor
                return recentAvg > olderAvg * 2 && recentAvg > 200;
            }
        }
        
        /// <summary>
        /// Ajusta límites según calidad de red
        /// </summary>
        private async Task AdjustLimitsAsync()
        {
            var previousLimits = new RateLimits
            {
                SearchesPerMinute = currentSearchesPerMinute,
                ParallelDownloads = currentParallelDownloads,
                IsPaused = isPaused
            };
            
            // Pausar si red muy saturada
            if (isNetworkSaturated && currentQuality == NetworkQuality.VeryPoor)
            {
                if (!isPaused)
                {
                    isPaused = true;
                    Console.WriteLine("[RateLimiter] ⏸️ Red saturada - PAUSANDO operaciones");
                    NetworkPaused?.Invoke("Red saturada - Pausando 30 segundos");
                    
                    // Esperar y reintentar
                    await Task.Delay(30000);
                    isPaused = false;
                    NetworkResumed?.Invoke("Reanudando operaciones");
                }
                return;
            }
            
            // Ajustar según calidad
            var newLimits = GetRecommendedLimits(currentQuality);
            
            // Solo ajustar si hay cambio significativo
            if (Math.Abs(newLimits.SearchesPerMinute - currentSearchesPerMinute) >= 2 ||
                Math.Abs(newLimits.ParallelDownloads - currentParallelDownloads) >= 1)
            {
                currentSearchesPerMinute = newLimits.SearchesPerMinute;
                currentParallelDownloads = newLimits.ParallelDownloads;
                totalAdjustments++;
                
                Console.WriteLine($"[RateLimiter] 📊 Límites ajustados: " +
                                $"Búsquedas: {currentSearchesPerMinute}/min, " +
                                $"Descargas: {currentParallelDownloads} paralelas");
                
                LimitsChanged?.Invoke(newLimits);
            }
        }
        
        /// <summary>
        /// Obtiene límites recomendados según calidad
        /// </summary>
        public RateLimits GetRecommendedLimits(NetworkQuality quality = NetworkQuality.Unknown)
        {
            if (quality == NetworkQuality.Unknown)
                quality = currentQuality;
            
            return quality switch
            {
                NetworkQuality.Excellent => new RateLimits
                {
                    SearchesPerMinute = 20,
                    ParallelDownloads = 10,
                    IsPaused = false
                },
                NetworkQuality.Good => new RateLimits
                {
                    SearchesPerMinute = 15,
                    ParallelDownloads = 6,
                    IsPaused = false
                },
                NetworkQuality.Fair => new RateLimits
                {
                    SearchesPerMinute = 10,
                    ParallelDownloads = 3,
                    IsPaused = false
                },
                NetworkQuality.Poor => new RateLimits
                {
                    SearchesPerMinute = 5,
                    ParallelDownloads = 2,
                    IsPaused = false
                },
                NetworkQuality.VeryPoor => new RateLimits
                {
                    SearchesPerMinute = 2,
                    ParallelDownloads = 1,
                    IsPaused = false
                },
                _ => new RateLimits
                {
                    SearchesPerMinute = 15,
                    ParallelDownloads = 6,
                    IsPaused = false
                }
            };
        }
        
        /// <summary>
        /// Obtiene límites actuales
        /// </summary>
        public RateLimits GetCurrentLimits()
        {
            return new RateLimits
            {
                SearchesPerMinute = currentSearchesPerMinute,
                ParallelDownloads = currentParallelDownloads,
                IsPaused = isPaused
            };
        }
        
        /// <summary>
        /// Fuerza límites específicos (override manual)
        /// </summary>
        public void SetManualLimits(int searchesPerMinute, int parallelDownloads)
        {
            currentSearchesPerMinute = Math.Max(1, Math.Min(30, searchesPerMinute));
            currentParallelDownloads = Math.Max(1, Math.Min(20, parallelDownloads));
            
            Console.WriteLine($"[RateLimiter] ⚙️ Límites manuales: " +
                            $"Búsquedas: {currentSearchesPerMinute}/min, " +
                            $"Descargas: {currentParallelDownloads}");
            
            LimitsChanged?.Invoke(GetCurrentLimits());
        }
        
        /// <summary>
        /// Obtiene estadísticas del rate limiter
        /// </summary>
        public RateLimiterStats GetStats()
        {
            return new RateLimiterStats
            {
                CurrentQuality = currentQuality,
                CurrentLatencyMs = currentLatencyMs,
                AverageLatencyMs = averageLatencyMs,
                IsSaturated = isNetworkSaturated,
                CurrentSearchesPerMinute = currentSearchesPerMinute,
                CurrentParallelDownloads = currentParallelDownloads,
                IsPaused = isPaused,
                TotalMeasurements = totalMeasurements,
                TotalTimeouts = totalTimeouts,
                TotalAdjustments = totalAdjustments,
                LastMeasurement = lastMeasurement,
                TimeoutRate = totalMeasurements > 0 
                    ? (double)totalTimeouts / totalMeasurements * 100 
                    : 0
            };
        }
        
        public void Dispose()
        {
            measurementTimer?.Dispose();
            Console.WriteLine("[RateLimiter] Detenido");
        }
    }
    
    /// <summary>
    /// Calidad de red
    /// </summary>
    public enum NetworkQuality
    {
        Unknown = 0,
        Excellent = 1,   // <50ms
        Good = 2,        // 50-100ms
        Fair = 3,        // 100-200ms
        Poor = 4,        // 200-500ms
        VeryPoor = 5     // >500ms
    }
    
    /// <summary>
    /// Límites de rate
    /// </summary>
    public class RateLimits
    {
        public int SearchesPerMinute { get; set; }
        public int ParallelDownloads { get; set; }
        public bool IsPaused { get; set; }
    }
    
    /// <summary>
    /// Medición de red
    /// </summary>
    public class NetworkMeasurement
    {
        public double LatencyMs { get; set; }
        public double AverageLatencyMs { get; set; }
        public NetworkQuality Quality { get; set; }
        public bool IsSaturated { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    /// <summary>
    /// Estadísticas del rate limiter
    /// </summary>
    public class RateLimiterStats
    {
        public NetworkQuality CurrentQuality { get; set; }
        public double CurrentLatencyMs { get; set; }
        public double AverageLatencyMs { get; set; }
        public bool IsSaturated { get; set; }
        public int CurrentSearchesPerMinute { get; set; }
        public int CurrentParallelDownloads { get; set; }
        public bool IsPaused { get; set; }
        public long TotalMeasurements { get; set; }
        public long TotalTimeouts { get; set; }
        public long TotalAdjustments { get; set; }
        public DateTime LastMeasurement { get; set; }
        public double TimeoutRate { get; set; }
        
        public override string ToString()
        {
            return $"Rate Limiter Stats:\n" +
                   $"  Calidad: {CurrentQuality}\n" +
                   $"  Latencia: {CurrentLatencyMs:F1}ms (avg: {AverageLatencyMs:F1}ms)\n" +
                   $"  Límites: {CurrentSearchesPerMinute}/min búsquedas, {CurrentParallelDownloads} descargas\n" +
                   $"  Mediciones: {TotalMeasurements:N0} (timeouts: {TimeoutRate:F1}%)\n" +
                   $"  Ajustes: {TotalAdjustments:N0}";
        }
    }
}
