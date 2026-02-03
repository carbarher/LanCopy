using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Services
{
    /// <summary>
    /// Utilidades para operaciones de red
    /// </summary>
    public static class NetworkHelpers
    {
        /// <summary>
        /// Verifica si hay conexión a Internet
        /// </summary>
        public static bool IsInternetAvailable()
        {
            try
            {
                using (var ping = new Ping())
                {
                    // Intentar ping a DNS de Google
                    var reply = ping.Send("8.8.8.8", 3000);
                    return reply?.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verifica si hay conexión a Internet de forma asíncrona
        /// </summary>
        public static async Task<bool> IsInternetAvailableAsync()
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync("8.8.8.8", 3000);
                    return reply?.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Ejecuta una acción con política de reintentos
        /// </summary>
        public static async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> action,
            int maxAttempts = 3,
            int initialDelayMs = 1000,
            double backoffMultiplier = 2.0,
            Action<int, Exception> onRetry = null)
        {
            int attempt = 0;
            int delay = initialDelayMs;

            while (true)
            {
                attempt++;
                try
                {
                    return await action();
                }
                catch (Exception ex)
                {
                    if (attempt >= maxAttempts)
                        throw;

                    onRetry?.Invoke(attempt, ex);
                    await Task.Delay(delay);
                    delay = (int)(delay * backoffMultiplier);
                }
            }
        }

        /// <summary>
        /// Ejecuta una acción con política de reintentos (versión síncrona)
        /// </summary>
        public static T ExecuteWithRetry<T>(
            Func<T> action,
            int maxAttempts = 3,
            int initialDelayMs = 1000,
            double backoffMultiplier = 2.0,
            Action<int, Exception> onRetry = null)
        {
            int attempt = 0;
            int delay = initialDelayMs;

            while (true)
            {
                attempt++;
                try
                {
                    return action();
                }
                catch (Exception ex)
                {
                    if (attempt >= maxAttempts)
                        throw;

                    onRetry?.Invoke(attempt, ex);
                    Thread.Sleep(delay);
                    delay = (int)(delay * backoffMultiplier);
                }
            }
        }

        /// <summary>
        /// Ejecuta una acción con timeout
        /// </summary>
        public static async Task<T> ExecuteWithTimeoutAsync<T>(
            Func<Task<T>> action,
            int timeoutMs)
        {
            using (var cts = new CancellationTokenSource(timeoutMs))
            {
                var task = action();
                var completedTask = await Task.WhenAny(task, Task.Delay(timeoutMs, cts.Token));

                if (completedTask == task)
                {
                    cts.Cancel(); // Cancelar el delay
                    return await task;
                }
                else
                {
                    throw new TimeoutException($"La operación excedió el timeout de {timeoutMs}ms");
                }
            }
        }

        /// <summary>
        /// Verifica si un host es alcanzable
        /// </summary>
        public static bool IsHostReachable(string host, int timeoutMs = 3000)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send(host, timeoutMs);
                    return reply?.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Obtiene la latencia a un host
        /// </summary>
        public static long? GetLatency(string host, int timeoutMs = 3000)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send(host, timeoutMs);
                    if (reply?.Status == IPStatus.Success)
                    {
                        return reply.RoundtripTime;
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Verifica si un puerto está abierto
        /// </summary>
        public static bool IsPortOpen(string host, int port, int timeoutMs = 3000)
        {
            try
            {
                using (var client = new System.Net.Sockets.TcpClient())
                {
                    var result = client.BeginConnect(host, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(timeoutMs);
                    
                    if (success)
                    {
                        client.EndConnect(result);
                        return true;
                    }
                    
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Obtiene la dirección IP local
        /// </summary>
        public static string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }

            return "127.0.0.1";
        }

        /// <summary>
        /// Obtiene la dirección IP pública
        /// </summary>
        public static async Task<string> GetPublicIPAddressAsync()
        {
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var response = await client.GetStringAsync("https://api.ipify.org");
                    return response?.Trim();
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Calcula el backoff exponencial
        /// </summary>
        public static int CalculateExponentialBackoff(int attempt, int baseDelayMs = 1000, int maxDelayMs = 60000)
        {
            var delay = baseDelayMs * Math.Pow(2, attempt - 1);
            return (int)Math.Min(delay, maxDelayMs);
        }

        /// <summary>
        /// Verifica la velocidad de conexión aproximada
        /// </summary>
        public static async Task<double> MeasureDownloadSpeedAsync(string testUrl = "https://www.google.com", int durationMs = 3000)
        {
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromMilliseconds(durationMs);
                    
                    var startTime = DateTime.Now;
                    var response = await client.GetAsync(testUrl);
                    var content = await response.Content.ReadAsByteArrayAsync();
                    var endTime = DateTime.Now;
                    
                    var duration = (endTime - startTime).TotalSeconds;
                    var bytesDownloaded = content.Length;
                    
                    // Retornar MB/s
                    return (bytesDownloaded / duration) / (1024.0 * 1024.0);
                }
            }
            catch
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// Circuit Breaker para prevenir llamadas a servicios que fallan constantemente
    /// </summary>
    public class CircuitBreaker
    {
        private readonly int _failureThreshold;
        private readonly TimeSpan _timeout;
        private int _failureCount;
        private DateTime _lastFailureTime;
        private CircuitBreakerState _state;
        private readonly object _lock = new object();

        public CircuitBreaker(int failureThreshold = 5, int timeoutSeconds = 60)
        {
            _failureThreshold = failureThreshold;
            _timeout = TimeSpan.FromSeconds(timeoutSeconds);
            _state = CircuitBreakerState.Closed;
        }

        public CircuitBreakerState State => _state;

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
        {
            lock (_lock)
            {
                if (_state == CircuitBreakerState.Open)
                {
                    if (DateTime.Now - _lastFailureTime > _timeout)
                    {
                        _state = CircuitBreakerState.HalfOpen;
                    }
                    else
                    {
                        throw new InvalidOperationException("Circuit breaker is OPEN");
                    }
                }
            }

            try
            {
                var result = await action();
                
                lock (_lock)
                {
                    if (_state == CircuitBreakerState.HalfOpen)
                    {
                        _state = CircuitBreakerState.Closed;
                        _failureCount = 0;
                    }
                }
                
                return result;
            }
            catch (Exception)
            {
                lock (_lock)
                {
                    _failureCount++;
                    _lastFailureTime = DateTime.Now;
                    
                    if (_failureCount >= _failureThreshold)
                    {
                        _state = CircuitBreakerState.Open;
                    }
                }
                
                throw;
            }
        }
    }

    public enum CircuitBreakerState
    {
        Closed,   // Normal operation
        Open,     // Failing, reject requests
        HalfOpen  // Testing if service recovered
    }
}
