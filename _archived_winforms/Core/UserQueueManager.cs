// <copyright file="UserQueueManager.cs" company="SlskDown">
//     Gestión de colas de descargas separadas por usuario
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using SlskDown.Models;

namespace SlskDown.Core
{
    /// <summary>
    /// Gestiona colas de descargas separadas por usuario para fairness.
    /// Inspirado en el sistema de colas de Nicotine+.
    /// </summary>
    public class UserQueueManager
    {
        private readonly Dictionary<string, Queue<DownloadTask>> _userQueues = new();
        private readonly Dictionary<string, int> _activeDownloadsPerUser = new();
        private readonly Dictionary<string, int> _userQueueLimits = new();
        private readonly HashSet<string> _failedUsers = new();
        private int _maxDownloadsPerUser = 2;
        private int _currentRoundRobinIndex = 0;

        /// <summary>
        /// Callback invocado cuando un usuario alcanza su límite de descargas
        /// </summary>
        public Action<string, int, int> OnUserLimitReached { get; set; }

        public int MaxDownloadsPerUser
        {
            get => _maxDownloadsPerUser;
            set => _maxDownloadsPerUser = Math.Max(1, value);
        }

        public int TotalQueuedDownloads => _userQueues.Sum(q => q.Value.Count);
        public int TotalActiveDownloads => _activeDownloadsPerUser.Sum(kvp => kvp.Value);
        public int TotalUsers => _userQueues.Count;

        /// <summary>
        /// Agrega una descarga a la cola del usuario.
        /// </summary>
        public void Enqueue(DownloadTask task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            var username = task.File?.Username ?? "Unknown";

            if (!_userQueues.ContainsKey(username))
                _userQueues[username] = new Queue<DownloadTask>();

            _userQueues[username].Enqueue(task);
        }

        /// <summary>
        /// Obtiene la siguiente descarga usando round-robin entre usuarios.
        /// Esto asegura fairness: un usuario lento no bloquea a otros.
        /// </summary>
        public DownloadTask GetNext()
        {
            if (_userQueues.Count == 0)
                return null;

            var usernames = _userQueues.Keys.ToList();
            var startIndex = _currentRoundRobinIndex % usernames.Count;

            // Intentar con cada usuario en round-robin
            for (int i = 0; i < usernames.Count; i++)
            {
                var index = (startIndex + i) % usernames.Count;
                var username = usernames[index];

                // Skip usuarios que ya alcanzaron su límite
                var activeCount = _activeDownloadsPerUser.GetValueOrDefault(username);
                var limit = GetUserLimit(username);

                if (activeCount >= limit)
                {
                    // Invocar callback si está configurado
                    OnUserLimitReached?.Invoke(username, activeCount, limit);
                    continue;
                }

                // Skip usuarios en failed state
                if (_failedUsers.Contains(username))
                    continue;

                var queue = _userQueues[username];
                if (queue.Count == 0)
                    continue;

                // Encontramos una descarga válida
                var task = queue.Dequeue();
                
                // Actualizar contadores
                _activeDownloadsPerUser[username] = activeCount + 1;
                _currentRoundRobinIndex = (index + 1) % usernames.Count;

                // Limpiar cola vacía
                if (queue.Count == 0)
                    _userQueues.Remove(username);

                return task;
            }

            return null;
        }

        /// <summary>
        /// Marca una descarga como completada.
        /// </summary>
        public void MarkCompleted(string username)
        {
            if (_activeDownloadsPerUser.TryGetValue(username, out var count))
            {
                _activeDownloadsPerUser[username] = Math.Max(0, count - 1);
                
                if (_activeDownloadsPerUser[username] == 0)
                    _activeDownloadsPerUser.Remove(username);
            }

            // Remover de failed si estaba
            _failedUsers.Remove(username);
        }

        /// <summary>
        /// Marca un usuario como fallido temporalmente.
        /// </summary>
        public void MarkFailed(string username)
        {
            _failedUsers.Add(username);
            MarkCompleted(username); // Liberar slot
        }

        /// <summary>
        /// Reintenta usuarios fallidos.
        /// </summary>
        public void RetryFailedUsers()
        {
            _failedUsers.Clear();
        }

        /// <summary>
        /// Establece un límite personalizado para un usuario.
        /// </summary>
        public void SetUserLimit(string username, int limit)
        {
            _userQueueLimits[username] = Math.Max(1, limit);
        }

        /// <summary>
        /// Obtiene el límite de descargas para un usuario.
        /// </summary>
        public int GetUserLimit(string username)
        {
            return _userQueueLimits.TryGetValue(username, out var limit) 
                ? limit 
                : _maxDownloadsPerUser;
        }

        /// <summary>
        /// Obtiene el tamaño de la cola de un usuario.
        /// </summary>
        public int GetUserQueueSize(string username)
        {
            return _userQueues.TryGetValue(username, out var queue) 
                ? queue.Count 
                : 0;
        }

        /// <summary>
        /// Obtiene las descargas activas de un usuario.
        /// </summary>
        public int GetUserActiveCount(string username)
        {
            return _activeDownloadsPerUser.GetValueOrDefault(username);
        }

        /// <summary>
        /// Limpia todas las colas.
        /// </summary>
        public void Clear()
        {
            _userQueues.Clear();
            _activeDownloadsPerUser.Clear();
            _failedUsers.Clear();
            _currentRoundRobinIndex = 0;
        }

        /// <summary>
        /// Obtiene estadísticas de las colas.
        /// </summary>
        public Dictionary<string, (int Queued, int Active, int Limit)> GetStats()
        {
            var stats = new Dictionary<string, (int, int, int)>();

            var allUsers = new HashSet<string>(_userQueues.Keys);
            allUsers.UnionWith(_activeDownloadsPerUser.Keys);

            foreach (var username in allUsers)
            {
                var queued = GetUserQueueSize(username);
                var active = GetUserActiveCount(username);
                var limit = GetUserLimit(username);
                stats[username] = (queued, active, limit);
            }

            return stats;
        }
    }
}
