using System;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// MEJORA #4: Retry inteligente con exponential backoff para descargas
    /// Implementa una política de reintentos con backoff exponencial y jitter
    /// para evitar saturar el servidor y mejorar la tasa de éxito de descargas.
    /// </summary>
    public class DownloadRetryPolicy
    {
        private readonly int maxRetries;
        private readonly TimeSpan initialDelay;
        private readonly TimeSpan maxDelay;
        private readonly double backoffMultiplier;
        private readonly Random random = new Random();
        
        public DownloadRetryPolicy(
            int maxRetries = 5,
            TimeSpan? initialDelay = null,
            TimeSpan? maxDelay = null,
            double backoffMultiplier = 2.0)
        {
            this.maxRetries = maxRetries;
            this.initialDelay = initialDelay ?? TimeSpan.FromSeconds(2);
            this.maxDelay = maxDelay ?? TimeSpan.FromMinutes(5);
            this.backoffMultiplier = backoffMultiplier;
        }
        
        /// <summary>
        /// Ejecuta una operación de descarga con reintentos automáticos
        /// </summary>
        public async Task<T> ExecuteAsync<T>(
            Func<int, Task<T>> operation,
            Func<Exception, bool> shouldRetry = null,
            Action<int, TimeSpan, Exception> onRetry = null,
            CancellationToken cancellationToken = default)
        {
            int attempt = 0;
            Exception lastException = null;
            
            while (attempt < maxRetries)
            {
                try
                {
                    return await operation(attempt);
                }
                catch (Exception ex) when (attempt < maxRetries - 1)
                {
                    lastException = ex;
                    
                    // Verificar si debemos reintentar este tipo de error
                    if (shouldRetry != null && !shouldRetry(ex))
                    {
                        throw; // No reintentar este error
                    }
                    
                    // Calcular delay con exponential backoff y jitter
                    var delay = CalculateDelay(attempt);
                    
                    // Notificar del reintento
                    onRetry?.Invoke(attempt + 1, delay, ex);
                    
                    // Esperar antes del siguiente intento
                    await Task.Delay(delay, cancellationToken);
                    
                    attempt++;
                }
            }
            
            // Si llegamos aquí, todos los reintentos fallaron
            throw new AggregateException(
                $"La descarga falló después de {maxRetries} intentos",
                lastException ?? new Exception("Error desconocido"));
        }
        
        /// <summary>
        /// Calcula el delay para el siguiente intento con exponential backoff y jitter
        /// </summary>
        private TimeSpan CalculateDelay(int attempt)
        {
            // Exponential backoff: delay = initialDelay * (backoffMultiplier ^ attempt)
            var exponentialDelay = initialDelay.TotalMilliseconds * Math.Pow(backoffMultiplier, attempt);
            
            // Limitar al máximo delay
            exponentialDelay = Math.Min(exponentialDelay, maxDelay.TotalMilliseconds);
            
            // Agregar jitter (±25%) para evitar thundering herd
            var jitterFactor = 0.75 + (random.NextDouble() * 0.5); // 0.75 a 1.25
            var finalDelay = exponentialDelay * jitterFactor;
            
            return TimeSpan.FromMilliseconds(finalDelay);
        }
        
        /// <summary>
        /// Determina si un error es recuperable y debe reintentar
        /// </summary>
        public static bool IsRetryableError(Exception ex)
        {
            if (ex is OperationCanceledException)
                return false; // No reintentar cancelaciones manuales
            
            var message = ex.Message.ToLowerInvariant();
            
            // Errores de red temporales (reintentar)
            if (message.Contains("timeout") ||
                message.Contains("connection") ||
                message.Contains("network") ||
                message.Contains("remote connection closed") ||
                message.Contains("read error") ||
                message.Contains("connection lost") ||
                message.Contains("transfer timed out"))
            {
                return true;
            }
            
            // Usuario offline (reintentar con backoff largo)
            if (message.Contains("offline") ||
                message.Contains("not responding"))
            {
                return true;
            }
            
            // Errores de servidor temporales
            if (message.Contains("server error") ||
                message.Contains("service unavailable") ||
                message.Contains("too many requests"))
            {
                return true;
            }
            
            // Errores permanentes (no reintentar)
            if (message.Contains("file not found") ||
                message.Contains("access denied") ||
                message.Contains("invalid") ||
                message.Contains("forbidden"))
            {
                return false;
            }
            
            // Por defecto, reintentar errores desconocidos
            return true;
        }
        
        /// <summary>
        /// Crea una política adaptativa basada en el número de fallos previos
        /// </summary>
        public static DownloadRetryPolicy CreateAdaptive(int previousFailures)
        {
            if (previousFailures == 0)
            {
                // Primera descarga: política estándar
                return new DownloadRetryPolicy(
                    maxRetries: 3,
                    initialDelay: TimeSpan.FromSeconds(2),
                    maxDelay: TimeSpan.FromMinutes(2),
                    backoffMultiplier: 2.0
                );
            }
            else if (previousFailures < 3)
            {
                // Algunos fallos: ser más paciente
                return new DownloadRetryPolicy(
                    maxRetries: 5,
                    initialDelay: TimeSpan.FromSeconds(5),
                    maxDelay: TimeSpan.FromMinutes(5),
                    backoffMultiplier: 2.5
                );
            }
            else
            {
                // Muchos fallos: modo agresivo con delays largos
                return new DownloadRetryPolicy(
                    maxRetries: 7,
                    initialDelay: TimeSpan.FromSeconds(10),
                    maxDelay: TimeSpan.FromMinutes(10),
                    backoffMultiplier: 3.0
                );
            }
        }
    }
}
