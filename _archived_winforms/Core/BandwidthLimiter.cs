using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Gestor de límite de ancho de banda para descargas
    /// </summary>
    public class BandwidthLimiter
    {
        private readonly int _maxBytesPerSecond;
        private readonly ConcurrentQueue<DateTime> _requestTimes = new ConcurrentQueue<DateTime>();
        private readonly SemaphoreSlim _semaphore;
        private long _bytesTransferred = 0;
        private DateTime _lastReset = DateTime.Now;
        private readonly object _resetLock = new object();

        public BandwidthLimiter(int maxKBps)
        {
            _maxBytesPerSecond = maxKBps * 1024; // Convertir KB a bytes
            if (_maxBytesPerSecond > 0)
            {
                // Calcular cuántas solicitudes permitimos por segundo
                // Asumimos un tamaño promedio de chunk de 8KB
                var requestsPerSecond = Math.Max(1, _maxBytesPerSecond / 8192);
                _semaphore = new SemaphoreSlim(requestsPerSecond, requestsPerSecond);
            }
            else
            {
                _semaphore = new SemaphoreSlim(int.MaxValue, int.MaxValue); // Sin límite
            }
        }

        /// <summary>
        /// Espera si es necesario para respetar el límite de ancho de banda
        /// </summary>
        public async Task WaitAsync()
        {
            if (_maxBytesPerSecond <= 0)
                return; // Sin límite

            await _semaphore.WaitAsync();
            
            // Resetear el semáforo cada segundo
            lock (_resetLock)
            {
                if (DateTime.Now - _lastReset > TimeSpan.FromSeconds(1))
                {
                    _bytesTransferred = 0;
                    _lastReset = DateTime.Now;
                    
                    // Liberar el semáforo para el siguiente segundo
                    var requestsPerSecond = Math.Max(1, _maxBytesPerSecond / 8192);
                    _semaphore.Release(Math.Min(_semaphore.CurrentCount, requestsPerSecond));
                }
            }
        }

        /// <summary>
        /// Registra la transferencia de bytes para ajustar el límite
        /// </summary>
        public void RecordTransfer(long bytes)
        {
            if (_maxBytesPerSecond <= 0)
                return;

            lock (_resetLock)
            {
                _bytesTransferred += bytes;
                
                // Si excedemos el límite, esperamos el tiempo proporcional
                if (_bytesTransferred > _maxBytesPerSecond)
                {
                    var excessBytes = _bytesTransferred - _maxBytesPerSecond;
                    var waitTime = (int)(excessBytes * 1000.0 / _maxBytesPerSecond);
                    
                    if (waitTime > 0)
                    {
                        Task.Delay(waitTime).Wait();
                    }
                }
            }
        }

        /// <summary>
        /// Obtiene las estadísticas actuales del ancho de banda
        /// </summary>
        public (long BytesTransferred, int MaxBytesPerSecond, double CurrentUsagePercent) GetStats()
        {
            lock (_resetLock)
            {
                var usagePercent = _maxBytesPerSecond > 0 ? 
                    (double)_bytesTransferred / _maxBytesPerSecond * 100 : 0;
                
                return (_bytesTransferred, _maxBytesPerSecond, usagePercent);
            }
        }

        public void Dispose()
        {
            _semaphore?.Dispose();
        }
    }
}
