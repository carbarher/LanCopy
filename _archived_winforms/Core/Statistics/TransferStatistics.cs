using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SlskDown.Core.Statistics
{
    /// <summary>
    /// Sistema de estadísticas de transferencias inspirado en Nicotine+
    /// Tracking detallado por usuario, proveedor y global
    /// </summary>
    public class TransferStatistics
    {
        private readonly ConcurrentDictionary<string, UserStats> _userStats;
        private readonly ConcurrentDictionary<string, ProviderStats> _providerStats;
        private long _totalBytesTransferred;
        private long _totalBandwidth;
        private int _totalTransfers;
        private int _successfulTransfers;
        private int _failedTransfers;
        private readonly object _statsLock = new object();

        public TransferStatistics()
        {
            _userStats = new ConcurrentDictionary<string, UserStats>(StringComparer.OrdinalIgnoreCase);
            _providerStats = new ConcurrentDictionary<string, ProviderStats>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Actualiza progreso de una transferencia
        /// </summary>
        public void UpdateProgress(string username, string provider, long currentOffset, long lastOffset, double speed)
        {
            if (lastOffset >= 0 && currentOffset > lastOffset)
            {
                var bytesTransferred = currentOffset - lastOffset;

                // Actualizar totales globales
                Interlocked.Add(ref _totalBytesTransferred, bytesTransferred);
                Interlocked.Add(ref _totalBandwidth, (long)speed);

                // Actualizar estadísticas por usuario
                var userStats = _userStats.GetOrAdd(username, _ => new UserStats(username));
                userStats.AddBytes(bytesTransferred);
                userStats.UpdateSpeed(speed);

                // Actualizar estadísticas por proveedor
                var providerStats = _providerStats.GetOrAdd(provider, _ => new ProviderStats(provider));
                providerStats.AddBytes(bytesTransferred);
                providerStats.UpdateSpeed(speed);
            }
        }

        /// <summary>
        /// Registra inicio de transferencia
        /// </summary>
        public void RecordTransferStart(string username, string provider)
        {
            Interlocked.Increment(ref _totalTransfers);

            var userStats = _userStats.GetOrAdd(username, _ => new UserStats(username));
            userStats.IncrementTransfers();

            var providerStats = _providerStats.GetOrAdd(provider, _ => new ProviderStats(provider));
            providerStats.IncrementTransfers();
        }

        /// <summary>
        /// Registra transferencia exitosa
        /// </summary>
        public void RecordTransferSuccess(string username, string provider, long fileSize, TimeSpan duration)
        {
            Interlocked.Increment(ref _successfulTransfers);

            var userStats = _userStats.GetOrAdd(username, _ => new UserStats(username));
            userStats.RecordSuccess(fileSize, duration);

            var providerStats = _providerStats.GetOrAdd(provider, _ => new ProviderStats(provider));
            providerStats.RecordSuccess(fileSize, duration);
        }

        /// <summary>
        /// Registra transferencia fallida
        /// </summary>
        public void RecordTransferFailure(string username, string provider, string reason)
        {
            Interlocked.Increment(ref _failedTransfers);

            var userStats = _userStats.GetOrAdd(username, _ => new UserStats(username));
            userStats.RecordFailure(reason);

            var providerStats = _providerStats.GetOrAdd(provider, _ => new ProviderStats(provider));
            providerStats.RecordFailure(reason);
        }

        /// <summary>
        /// Obtiene estadísticas de un usuario
        /// </summary>
        public UserStats GetUserStats(string username)
        {
            return _userStats.GetOrAdd(username, _ => new UserStats(username));
        }

        /// <summary>
        /// Obtiene estadísticas de un proveedor
        /// </summary>
        public ProviderStats GetProviderStats(string provider)
        {
            return _providerStats.GetOrAdd(provider, _ => new ProviderStats(provider));
        }

        /// <summary>
        /// Obtiene estadísticas globales
        /// </summary>
        public GlobalStats GetGlobalStats()
        {
            return new GlobalStats
            {
                TotalBytesTransferred = Interlocked.Read(ref _totalBytesTransferred),
                TotalTransfers = _totalTransfers,
                SuccessfulTransfers = _successfulTransfers,
                FailedTransfers = _failedTransfers,
                AverageSpeed = _userStats.Values.Any() ? _userStats.Values.Average(s => s.AverageSpeed) : 0,
                UniqueUsers = _userStats.Count,
                UniqueProviders = _providerStats.Count
            };
        }

        /// <summary>
        /// Obtiene top usuarios por bytes transferidos
        /// </summary>
        public List<UserStats> GetTopUsersByBytes(int count = 10)
        {
            return _userStats.Values
                .OrderByDescending(s => s.TotalBytes)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Obtiene top usuarios por velocidad promedio
        /// </summary>
        public List<UserStats> GetTopUsersBySpeed(int count = 10)
        {
            return _userStats.Values
                .OrderByDescending(s => s.AverageSpeed)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Limpia estadísticas
        /// </summary>
        public void Clear()
        {
            _userStats.Clear();
            _providerStats.Clear();
            Interlocked.Exchange(ref _totalBytesTransferred, 0);
            Interlocked.Exchange(ref _totalBandwidth, 0);
            _totalTransfers = 0;
            _successfulTransfers = 0;
            _failedTransfers = 0;
        }
    }

    /// <summary>
    /// Estadísticas por usuario
    /// </summary>
    public class UserStats
    {
        public string Username { get; }
        private long _totalBytes;
        private int _totalTransfers;
        private int _successfulTransfers;
        private int _failedTransfers;
        private readonly Queue<double> _speedSamples;
        private readonly Queue<TimeSpan> _durationSamples;
        private readonly ConcurrentDictionary<string, int> _failureReasons;
        private readonly object _lock = new object();
        private DateTime _firstTransferAt;
        private DateTime _lastTransferAt;

        public UserStats(string username)
        {
            Username = username;
            _speedSamples = new Queue<double>(100);
            _durationSamples = new Queue<TimeSpan>(100);
            _failureReasons = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _firstTransferAt = DateTime.UtcNow;
            _lastTransferAt = DateTime.UtcNow;
        }

        public void AddBytes(long bytes)
        {
            Interlocked.Add(ref _totalBytes, bytes);
            _lastTransferAt = DateTime.UtcNow;
        }

        public void UpdateSpeed(double speed)
        {
            lock (_lock)
            {
                _speedSamples.Enqueue(speed);
                if (_speedSamples.Count > 100)
                    _speedSamples.Dequeue();
            }
        }

        public void IncrementTransfers()
        {
            Interlocked.Increment(ref _totalTransfers);
        }

        public void RecordSuccess(long fileSize, TimeSpan duration)
        {
            Interlocked.Increment(ref _successfulTransfers);
            
            lock (_lock)
            {
                _durationSamples.Enqueue(duration);
                if (_durationSamples.Count > 100)
                    _durationSamples.Dequeue();
            }
        }

        public void RecordFailure(string reason)
        {
            Interlocked.Increment(ref _failedTransfers);
            _failureReasons.AddOrUpdate(reason, 1, (_, count) => count + 1);
        }

        public long TotalBytes => Interlocked.Read(ref _totalBytes);
        public int TotalTransfers => _totalTransfers;
        public int SuccessfulTransfers => _successfulTransfers;
        public int FailedTransfers => _failedTransfers;
        
        public double AverageSpeed
        {
            get
            {
                lock (_lock)
                {
                    return _speedSamples.Count > 0 ? _speedSamples.Average() : 0;
                }
            }
        }

        public double SuccessRate => _totalTransfers > 0 ? (double)_successfulTransfers / _totalTransfers : 0;

        public TimeSpan AverageDuration
        {
            get
            {
                lock (_lock)
                {
                    if (_durationSamples.Count == 0)
                        return TimeSpan.Zero;
                    
                    var avgTicks = (long)_durationSamples.Average(d => d.Ticks);
                    return TimeSpan.FromTicks(avgTicks);
                }
            }
        }

        public Dictionary<string, int> GetFailureReasons()
        {
            return new Dictionary<string, int>(_failureReasons);
        }

        public string GetMostCommonFailureReason()
        {
            return _failureReasons.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key ?? "None";
        }
    }

    /// <summary>
    /// Estadísticas por proveedor (Soulseek, eMule, etc.)
    /// </summary>
    public class ProviderStats
    {
        public string Provider { get; }
        private long _totalBytes;
        private int _totalTransfers;
        private int _successfulTransfers;
        private int _failedTransfers;
        private readonly Queue<double> _speedSamples;
        private readonly object _lock = new object();

        public ProviderStats(string provider)
        {
            Provider = provider;
            _speedSamples = new Queue<double>(100);
        }

        public void AddBytes(long bytes)
        {
            Interlocked.Add(ref _totalBytes, bytes);
        }

        public void UpdateSpeed(double speed)
        {
            lock (_lock)
            {
                _speedSamples.Enqueue(speed);
                if (_speedSamples.Count > 100)
                    _speedSamples.Dequeue();
            }
        }

        public void IncrementTransfers()
        {
            Interlocked.Increment(ref _totalTransfers);
        }

        public void RecordSuccess(long fileSize, TimeSpan duration)
        {
            Interlocked.Increment(ref _successfulTransfers);
        }

        public void RecordFailure(string reason)
        {
            Interlocked.Increment(ref _failedTransfers);
        }

        public long TotalBytes => Interlocked.Read(ref _totalBytes);
        public int TotalTransfers => _totalTransfers;
        public int SuccessfulTransfers => _successfulTransfers;
        public int FailedTransfers => _failedTransfers;
        
        public double AverageSpeed
        {
            get
            {
                lock (_lock)
                {
                    return _speedSamples.Count > 0 ? _speedSamples.Average() : 0;
                }
            }
        }

        public double SuccessRate => _totalTransfers > 0 ? (double)_successfulTransfers / _totalTransfers : 0;
    }

    /// <summary>
    /// Estadísticas globales
    /// </summary>
    public class GlobalStats
    {
        public long TotalBytesTransferred { get; set; }
        public int TotalTransfers { get; set; }
        public int SuccessfulTransfers { get; set; }
        public int FailedTransfers { get; set; }
        public double AverageSpeed { get; set; }
        public int UniqueUsers { get; set; }
        public int UniqueProviders { get; set; }

        public double SuccessRate => TotalTransfers > 0 ? (double)SuccessfulTransfers / TotalTransfers : 0;

        public override string ToString()
        {
            return $"Total: {TotalBytesTransferred:N0} bytes, Transfers: {TotalTransfers}, " +
                   $"Success: {SuccessfulTransfers}, Failed: {FailedTransfers}, " +
                   $"Avg Speed: {AverageSpeed:F2} KB/s, Users: {UniqueUsers}";
        }
    }
}
