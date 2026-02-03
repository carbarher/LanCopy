using System;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Resilience
{
    /// <summary>
    /// Sistema de retry con backoff exponencial
    /// </summary>
    public class RetryPolicy
    {
        private readonly int _maxRetries;
        private readonly TimeSpan _initialDelay;
        private readonly double _backoffMultiplier;
        private readonly TimeSpan _maxDelay;
        
        public RetryPolicy(
            int maxRetries = 3,
            TimeSpan? initialDelay = null,
            double backoffMultiplier = 2.0,
            TimeSpan? maxDelay = null)
        {
            _maxRetries = maxRetries;
            _initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
            _backoffMultiplier = backoffMultiplier;
            _maxDelay = maxDelay ?? TimeSpan.FromMinutes(1);
        }
        
        /// <summary>
        /// Ejecuta una acción con retry
        /// </summary>
        public async Task ExecuteAsync(
            Func<Task> action,
            CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(async () =>
            {
                await action();
                return true;
            }, cancellationToken);
        }
        
        /// <summary>
        /// Ejecuta una función con retry y retorna resultado
        /// </summary>
        public async Task<T> ExecuteAsync<T>(
            Func<Task<T>> func,
            CancellationToken cancellationToken = default)
        {
            Exception? lastException = null;
            
            for (int attempt = 0; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    using (PerformanceMetrics.Instance.Track($"RetryPolicy.Attempt{attempt}"))
                    {
                        return await func();
                    }
                }
                catch (Exception ex) when (attempt < _maxRetries && ShouldRetry(ex))
                {
                    lastException = ex;
                    
                    var delay = CalculateDelay(attempt);
                    
                    Logging.Logger.Instance.Warning(
                        $"Retry attempt {attempt + 1}/{_maxRetries} after {delay.TotalSeconds:F1}s",
                        ex);
                    
                    await Task.Delay(delay, cancellationToken);
                }
            }
            
            // Si llegamos aquí, todos los intentos fallaron
            throw new RetryException(
                $"Operation failed after {_maxRetries} retries",
                lastException);
        }
        
        /// <summary>
        /// Calcula el delay para el siguiente intento
        /// </summary>
        private TimeSpan CalculateDelay(int attempt)
        {
            var delay = TimeSpan.FromMilliseconds(
                _initialDelay.TotalMilliseconds * Math.Pow(_backoffMultiplier, attempt));
            
            return delay > _maxDelay ? _maxDelay : delay;
        }
        
        /// <summary>
        /// Determina si se debe reintentar basado en la excepción
        /// </summary>
        private bool ShouldRetry(Exception ex)
        {
            // No reintentar en estos casos
            if (ex is OperationCanceledException)
                return false;
            
            if (ex is ArgumentException)
                return false;
            
            if (ex is InvalidOperationException)
                return false;
            
            // Reintentar en casos de red, I/O, etc.
            return true;
        }
    }
    
    /// <summary>
    /// Excepción lanzada cuando fallan todos los reintentos
    /// </summary>
    public class RetryException : Exception
    {
        public RetryException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
    
    /// <summary>
    /// Builder para crear políticas de retry
    /// </summary>
    public class RetryPolicyBuilder
    {
        private int _maxRetries = 3;
        private TimeSpan _initialDelay = TimeSpan.FromSeconds(1);
        private double _backoffMultiplier = 2.0;
        private TimeSpan _maxDelay = TimeSpan.FromMinutes(1);
        
        public RetryPolicyBuilder WithMaxRetries(int maxRetries)
        {
            _maxRetries = maxRetries;
            return this;
        }
        
        public RetryPolicyBuilder WithInitialDelay(TimeSpan delay)
        {
            _initialDelay = delay;
            return this;
        }
        
        public RetryPolicyBuilder WithBackoffMultiplier(double multiplier)
        {
            _backoffMultiplier = multiplier;
            return this;
        }
        
        public RetryPolicyBuilder WithMaxDelay(TimeSpan maxDelay)
        {
            _maxDelay = maxDelay;
            return this;
        }
        
        public RetryPolicy Build()
        {
            return new RetryPolicy(_maxRetries, _initialDelay, _backoffMultiplier, _maxDelay);
        }
    }
    
    /// <summary>
    /// Políticas de retry predefinidas
    /// </summary>
    public static class RetryPolicies
    {
        /// <summary>
        /// Política rápida: 3 intentos, 500ms inicial
        /// </summary>
        public static RetryPolicy Fast => new RetryPolicy(
            maxRetries: 3,
            initialDelay: TimeSpan.FromMilliseconds(500),
            backoffMultiplier: 2.0,
            maxDelay: TimeSpan.FromSeconds(5));
        
        /// <summary>
        /// Política estándar: 3 intentos, 1s inicial
        /// </summary>
        public static RetryPolicy Standard => new RetryPolicy(
            maxRetries: 3,
            initialDelay: TimeSpan.FromSeconds(1),
            backoffMultiplier: 2.0,
            maxDelay: TimeSpan.FromSeconds(30));
        
        /// <summary>
        /// Política lenta: 5 intentos, 2s inicial
        /// </summary>
        public static RetryPolicy Slow => new RetryPolicy(
            maxRetries: 5,
            initialDelay: TimeSpan.FromSeconds(2),
            backoffMultiplier: 2.0,
            maxDelay: TimeSpan.FromMinutes(2));
        
        /// <summary>
        /// Política agresiva: 10 intentos, 100ms inicial
        /// </summary>
        public static RetryPolicy Aggressive => new RetryPolicy(
            maxRetries: 10,
            initialDelay: TimeSpan.FromMilliseconds(100),
            backoffMultiplier: 1.5,
            maxDelay: TimeSpan.FromSeconds(10));
    }
}
