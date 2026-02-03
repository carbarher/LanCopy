using System;

namespace SlskDown
{
    /// <summary>
    /// Implementación de Exponential Backoff con Jitter
    /// Previene "thundering herd" cuando múltiples clientes reconectan
    /// </summary>
    public class ExponentialBackoff
    {
        private int attemptCount = 0;
        private DateTime lastAttempt = DateTime.MinValue;
        private readonly Random random = new Random();
        private readonly object lockObj = new object();
        
        private readonly int baseDelaySeconds;
        private readonly int maxDelaySeconds;
        private readonly double multiplier;
        private readonly int jitterPercent;
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="baseDelaySeconds">Delay base en segundos</param>
        /// <param name="maxDelaySeconds">Delay máximo en segundos</param>
        /// <param name="multiplier">Multiplicador exponencial (default: 2.0)</param>
        /// <param name="jitterPercent">Porcentaje de jitter (0-100, default: 25)</param>
        public ExponentialBackoff(
            int baseDelaySeconds = 5,
            int maxDelaySeconds = 60,
            double multiplier = 2.0,
            int jitterPercent = 25)
        {
            this.baseDelaySeconds = baseDelaySeconds;
            this.maxDelaySeconds = maxDelaySeconds;
            this.multiplier = multiplier;
            this.jitterPercent = Math.Clamp(jitterPercent, 0, 100);
        }
        
        /// <summary>
        /// Obtiene el siguiente delay con backoff exponencial y jitter
        /// </summary>
        public TimeSpan GetNextDelay(BackoffScenario scenario = BackoffScenario.NetworkError)
        {
            lock (lockObj)
            {
                attemptCount++;
                lastAttempt = DateTime.UtcNow;
                
                int baseSeconds = scenario switch
                {
                    BackoffScenario.NetworkError => GetNetworkErrorDelay(attemptCount),
                    BackoffScenario.AuthError => GetAuthErrorDelay(attemptCount),
                    BackoffScenario.RateLimiting => GetRateLimitDelay(attemptCount),
                    _ => GetNetworkErrorDelay(attemptCount)
                };
                
                // Aplicar jitter: delay ± jitterPercent%
                int jitterMs = random.Next(0, (baseSeconds * 1000 * jitterPercent) / 100);
                
                return TimeSpan.FromSeconds(baseSeconds).Add(TimeSpan.FromMilliseconds(jitterMs));
            }
        }
        
        /// <summary>
        /// Calcula delay para errores de red/timeout
        /// </summary>
        private int GetNetworkErrorDelay(int attempt)
        {
            // Exponencial: 5s, 10s, 20s, 40s, 60s (cap)
            int delay = (int)(baseDelaySeconds * Math.Pow(multiplier, attempt - 1));
            return Math.Min(delay, maxDelaySeconds);
        }
        
        /// <summary>
        /// Calcula delay para errores de autenticación
        /// </summary>
        private int GetAuthErrorDelay(int attempt)
        {
            // Más conservador: 30s, 60s, 120s, 300s, 600s
            return attempt switch
            {
                1 => 30,
                2 => 60,
                3 => 120,
                4 => 300,
                _ => 600  // Cap at 10 min
            };
        }
        
        /// <summary>
        /// Calcula delay para rate limiting/bans
        /// </summary>
        private int GetRateLimitDelay(int strikes)
        {
            // Muy conservador: 5min, 15min, 30min, 60min
            return strikes switch
            {
                1 => 300,   // 5 min
                2 => 900,   // 15 min
                3 => 1800,  // 30 min
                _ => 3600   // 60 min
            };
        }
        
        /// <summary>
        /// Resetea el contador de intentos
        /// </summary>
        public void Reset()
        {
            lock (lockObj)
            {
                attemptCount = 0;
                lastAttempt = DateTime.MinValue;
            }
        }
        
        /// <summary>
        /// Obtiene el número de intentos actual
        /// </summary>
        public int GetAttemptCount()
        {
            lock (lockObj)
            {
                return attemptCount;
            }
        }
        
        /// <summary>
        /// Obtiene el tiempo desde el último intento
        /// </summary>
        public TimeSpan GetTimeSinceLastAttempt()
        {
            lock (lockObj)
            {
                if (lastAttempt == DateTime.MinValue)
                    return TimeSpan.Zero;
                
                return DateTime.UtcNow - lastAttempt;
            }
        }
        
        /// <summary>
        /// Crea una instancia preconfigurada para un escenario específico
        /// </summary>
        public static ExponentialBackoff ForScenario(BackoffScenario scenario)
        {
            return scenario switch
            {
                BackoffScenario.NetworkError => new ExponentialBackoff(
                    baseDelaySeconds: 5,
                    maxDelaySeconds: 60,
                    multiplier: 2.0,
                    jitterPercent: 25
                ),
                BackoffScenario.AuthError => new ExponentialBackoff(
                    baseDelaySeconds: 30,
                    maxDelaySeconds: 600,
                    multiplier: 2.0,
                    jitterPercent: 25
                ),
                BackoffScenario.RateLimiting => new ExponentialBackoff(
                    baseDelaySeconds: 300,
                    maxDelaySeconds: 3600,
                    multiplier: 2.0,
                    jitterPercent: 50
                ),
                _ => new ExponentialBackoff()
            };
        }
    }
    
    public enum BackoffScenario
    {
        /// <summary>
        /// Errores de red, timeouts, socket exceptions
        /// Backoff rápido: 5-60 segundos
        /// </summary>
        NetworkError,
        
        /// <summary>
        /// Errores de autenticación, credenciales inválidas
        /// Backoff lento: 30s-10min
        /// </summary>
        AuthError,
        
        /// <summary>
        /// Rate limiting, posibles bans
        /// Backoff muy lento: 5-60 minutos
        /// </summary>
        RateLimiting
    }
}
