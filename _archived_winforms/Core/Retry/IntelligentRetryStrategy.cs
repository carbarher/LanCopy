using System;
using SlskDown.Models;

namespace SlskDown.Core.Retry
{
    /// <summary>
    /// Estrategia de retry inteligente con backoff exponencial inspirada en Nicotine+
    /// Ajusta delays según tipo de error y agrega jitter para evitar thundering herd
    /// </summary>
    public class IntelligentRetryStrategy
    {
        private readonly RetryConfig config;
        private readonly Random random;

        public IntelligentRetryStrategy(RetryConfig config = null)
        {
            this.config = config ?? new RetryConfig();
            this.random = new Random();
        }

        /// <summary>
        /// Calcula el delay óptimo para el próximo reintento
        /// </summary>
        public TimeSpan CalculateRetryDelay(DownloadTask task)
        {
            if (task == null)
                return config.BaseDelay;

            // Backoff exponencial: baseDelay * 2^retryCount
            var exponentialDelay = TimeSpan.FromSeconds(
                Math.Min(
                    config.BaseDelay.TotalSeconds * Math.Pow(2, task.RetryCount),
                    config.MaxDelay.TotalSeconds
                )
            );

            // Agregar jitter aleatorio ±20% para evitar thundering herd
            var jitter = 0.8 + (random.NextDouble() * 0.4);  // 0.8 a 1.2
            var delayWithJitter = TimeSpan.FromSeconds(exponentialDelay.TotalSeconds * jitter);

            // Ajustar según razón del fallo
            var adjustedDelay = AdjustDelayByFailureReason(delayWithJitter, task.LastFailureReason);

            return adjustedDelay;
        }

        /// <summary>
        /// Determina si se debe reintentar una descarga
        /// </summary>
        public bool ShouldRetry(DownloadTask task)
        {
            if (task == null)
                return false;

            // No reintentar si se alcanzó el máximo
            if (task.RetryCount >= config.MaxRetries)
                return false;

            // No reintentar si está deshabilitado
            if (!task.AutoRetryEnabled)
                return false;

            // Verificar si la razón del fallo es retryable
            return IsRetryableFailure(task.LastFailureReason);
        }

        /// <summary>
        /// Obtiene información detallada del retry
        /// </summary>
        public RetryInfo GetRetryInfo(DownloadTask task)
        {
            if (task == null)
                return null;

            var shouldRetry = ShouldRetry(task);
            var delay = shouldRetry ? CalculateRetryDelay(task) : TimeSpan.Zero;
            var nextRetryTime = shouldRetry ? DateTime.UtcNow + delay : (DateTime?)null;

            return new RetryInfo
            {
                ShouldRetry = shouldRetry,
                RetryCount = task.RetryCount,
                MaxRetries = config.MaxRetries,
                RetryDelay = delay,
                NextRetryTime = nextRetryTime,
                IsRetryable = IsRetryableFailure(task.LastFailureReason),
                FailureReason = task.LastFailureReason,
                RecommendedAction = GetRecommendedAction(task)
            };
        }

        /// <summary>
        /// Ajusta el delay según el tipo de error
        /// </summary>
        private TimeSpan AdjustDelayByFailureReason(TimeSpan baseDelay, DownloadFailureReason reason)
        {
            var multiplier = reason switch
            {
                // Errores de conexión: reintentar más rápido
                DownloadFailureReason.Connection => 0.5,
                DownloadFailureReason.Timeout => 0.7,

                // Usuario offline: esperar más tiempo
                DownloadFailureReason.Unknown when IsUserOfflineError(reason) => 2.0,

                // Cola llena: esperar más tiempo
                DownloadFailureReason.QueueFull => 1.5,
                DownloadFailureReason.QuotaExceeded => 1.5,

                // Archivo no compartido: no tiene sentido reintentar rápido
                DownloadFailureReason.FileNotShared => 3.0,

                // Baneado: esperar mucho más
                DownloadFailureReason.Banned => 5.0,

                // Error de I/O: reintentar moderadamente rápido
                DownloadFailureReason.FileIo => 0.8,

                // Cancelado por usuario: no reintentar
                DownloadFailureReason.UserCancelled => 0,

                // Otros: delay normal
                _ => 1.0
            };

            return TimeSpan.FromSeconds(baseDelay.TotalSeconds * multiplier);
        }

        /// <summary>
        /// Determina si un fallo es retryable
        /// </summary>
        private bool IsRetryableFailure(DownloadFailureReason reason)
        {
            return reason switch
            {
                // Retryable
                DownloadFailureReason.Connection => true,
                DownloadFailureReason.Timeout => true,
                DownloadFailureReason.QueueFull => true,
                DownloadFailureReason.QuotaExceeded => true,
                DownloadFailureReason.FileIo => true,
                DownloadFailureReason.Unknown => true,

                // No retryable
                DownloadFailureReason.UserCancelled => false,
                DownloadFailureReason.Banned => false,
                DownloadFailureReason.FileNotShared => false,
                DownloadFailureReason.RemoteCancelled => false,
                DownloadFailureReason.PendingShutdown => false,

                _ => true  // Por defecto, reintentar
            };
        }

        private bool IsUserOfflineError(DownloadFailureReason reason)
        {
            // Lógica para detectar si el error es por usuario offline
            // Esto podría mejorarse con información adicional
            return reason == DownloadFailureReason.Connection;
        }

        private string GetRecommendedAction(DownloadTask task)
        {
            if (task.RetryCount >= config.MaxRetries)
                return "Máximo de reintentos alcanzado. Buscar proveedor alternativo.";

            return task.LastFailureReason switch
            {
                DownloadFailureReason.Connection => "Reintentar cuando el usuario esté online.",
                DownloadFailureReason.Timeout => "Reintentar con timeout más largo.",
                DownloadFailureReason.QueueFull => "Esperar a que la cola del usuario se libere.",
                DownloadFailureReason.QuotaExceeded => "Esperar a que la cuota del usuario se renueve.",
                DownloadFailureReason.FileNotShared => "Buscar proveedor alternativo.",
                DownloadFailureReason.Banned => "Usuario te ha baneado. Buscar proveedor alternativo.",
                DownloadFailureReason.FileIo => "Verificar espacio en disco y permisos.",
                _ => "Reintentar automáticamente."
            };
        }
    }

    /// <summary>
    /// Configuración de la estrategia de retry
    /// </summary>
    public class RetryConfig
    {
        /// <summary>
        /// Delay base para el primer reintento
        /// </summary>
        public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Delay máximo permitido
        /// </summary>
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Número máximo de reintentos
        /// </summary>
        public int MaxRetries { get; set; } = 5;
    }

    /// <summary>
    /// Información detallada de retry
    /// </summary>
    public class RetryInfo
    {
        public bool ShouldRetry { get; set; }
        public int RetryCount { get; set; }
        public int MaxRetries { get; set; }
        public TimeSpan RetryDelay { get; set; }
        public DateTime? NextRetryTime { get; set; }
        public bool IsRetryable { get; set; }
        public DownloadFailureReason FailureReason { get; set; }
        public string RecommendedAction { get; set; }

        public override string ToString()
        {
            if (!ShouldRetry)
                return $"No reintentar ({RetryCount}/{MaxRetries})";

            return $"Reintentar en {RetryDelay.TotalMinutes:F0} minutos " +
                   $"({RetryCount + 1}/{MaxRetries}) - {RecommendedAction}";
        }
    }
}
