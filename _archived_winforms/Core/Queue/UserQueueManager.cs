using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core.Queue
{
    /// <summary>
    /// Gestor de límites de cola por usuario inspirado en Nicotine+
    /// Respeta límites de slots de cada usuario para mejor relación con la comunidad
    /// </summary>
    public class UserQueueManager
    {
        private readonly ConcurrentDictionary<string, int> _userQueueLimits;
        private readonly ConcurrentDictionary<string, int> _userQueueSizes;
        private readonly ConcurrentDictionary<string, DateTime> _lastQueueUpdate;
        private readonly int _defaultQueueLimit;

        public UserQueueManager(int defaultQueueLimit = int.MaxValue)
        {
            _userQueueLimits = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _userQueueSizes = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _lastQueueUpdate = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            _defaultQueueLimit = defaultQueueLimit;
        }

        /// <summary>
        /// Verifica si se puede agregar una transferencia a la cola del usuario
        /// </summary>
        public bool CanQueueTransfer(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;

            var currentSize = _userQueueSizes.GetOrAdd(username, 0);
            var limit = _userQueueLimits.GetOrAdd(username, _defaultQueueLimit);

            return currentSize < limit;
        }

        /// <summary>
        /// Actualiza el límite de cola de un usuario
        /// </summary>
        public void UpdateUserQueueLimit(string username, int limit)
        {
            if (string.IsNullOrWhiteSpace(username))
                return;

            _userQueueLimits[username] = Math.Max(0, limit);
            _lastQueueUpdate[username] = DateTime.UtcNow;
        }

        /// <summary>
        /// Incrementa el tamaño de cola de un usuario
        /// </summary>
        public int IncrementQueueSize(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return 0;

            return _userQueueSizes.AddOrUpdate(username, 1, (_, count) => count + 1);
        }

        /// <summary>
        /// Decrementa el tamaño de cola de un usuario
        /// </summary>
        public int DecrementQueueSize(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return 0;

            return _userQueueSizes.AddOrUpdate(username, 0, (_, count) => Math.Max(0, count - 1));
        }

        /// <summary>
        /// Obtiene el tamaño actual de cola de un usuario
        /// </summary>
        public int GetQueueSize(string username)
        {
            return _userQueueSizes.GetOrAdd(username, 0);
        }

        /// <summary>
        /// Obtiene el límite de cola de un usuario
        /// </summary>
        public int GetQueueLimit(string username)
        {
            return _userQueueLimits.GetOrAdd(username, _defaultQueueLimit);
        }

        /// <summary>
        /// Obtiene el espacio disponible en la cola de un usuario
        /// </summary>
        public int GetAvailableQueueSpace(string username)
        {
            var limit = GetQueueLimit(username);
            var size = GetQueueSize(username);
            return Math.Max(0, limit - size);
        }

        /// <summary>
        /// Verifica si la cola de un usuario está llena
        /// </summary>
        public bool IsQueueFull(string username)
        {
            return GetAvailableQueueSpace(username) == 0;
        }

        /// <summary>
        /// Resetea el tamaño de cola de un usuario
        /// </summary>
        public void ResetQueueSize(string username)
        {
            _userQueueSizes.TryRemove(username, out _);
        }

        /// <summary>
        /// Obtiene estadísticas de colas
        /// </summary>
        public QueueStatistics GetStatistics()
        {
            var stats = new QueueStatistics
            {
                TotalUsers = _userQueueSizes.Count,
                TotalQueuedTransfers = _userQueueSizes.Values.Sum(),
                UsersWithFullQueue = _userQueueSizes.Count(kvp => 
                {
                    var limit = _userQueueLimits.GetOrAdd(kvp.Key, _defaultQueueLimit);
                    return kvp.Value >= limit;
                }),
                AverageQueueSize = _userQueueSizes.Values.Any() ? _userQueueSizes.Values.Average() : 0
            };

            return stats;
        }

        /// <summary>
        /// Obtiene usuarios con cola llena
        /// </summary>
        public List<string> GetUsersWithFullQueue()
        {
            return _userQueueSizes
                .Where(kvp =>
                {
                    var limit = _userQueueLimits.GetOrAdd(kvp.Key, _defaultQueueLimit);
                    return kvp.Value >= limit;
                })
                .Select(kvp => kvp.Key)
                .ToList();
        }

        /// <summary>
        /// Obtiene usuarios ordenados por espacio disponible
        /// </summary>
        public List<(string Username, int Available)> GetUsersByAvailableSpace()
        {
            return _userQueueSizes
                .Select(kvp =>
                {
                    var limit = _userQueueLimits.GetOrAdd(kvp.Key, _defaultQueueLimit);
                    var available = Math.Max(0, limit - kvp.Value);
                    return (kvp.Key, available);
                })
                .OrderByDescending(t => t.Item2)
                .ToList();
        }

        /// <summary>
        /// Limpia datos de usuarios inactivos
        /// </summary>
        public void CleanupInactiveUsers(TimeSpan inactivityThreshold)
        {
            var now = DateTime.UtcNow;
            var inactiveUsers = _lastQueueUpdate
                .Where(kvp => now - kvp.Value > inactivityThreshold)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var username in inactiveUsers)
            {
                _userQueueSizes.TryRemove(username, out _);
                _userQueueLimits.TryRemove(username, out _);
                _lastQueueUpdate.TryRemove(username, out _);
            }
        }

        /// <summary>
        /// Limpia todos los datos
        /// </summary>
        public void Clear()
        {
            _userQueueLimits.Clear();
            _userQueueSizes.Clear();
            _lastQueueUpdate.Clear();
        }
    }

    public class QueueStatistics
    {
        public int TotalUsers { get; set; }
        public int TotalQueuedTransfers { get; set; }
        public int UsersWithFullQueue { get; set; }
        public double AverageQueueSize { get; set; }

        public override string ToString()
        {
            return $"Users: {TotalUsers}, Queued: {TotalQueuedTransfers}, " +
                   $"Full: {UsersWithFullQueue}, Avg: {AverageQueueSize:F1}";
        }
    }
}
