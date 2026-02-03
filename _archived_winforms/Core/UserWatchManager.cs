// <copyright file="UserWatchManager.cs" company="SlskDown">
//     Gestión automática de watch/unwatch de usuarios
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Soulseek;

namespace SlskDown.Core
{
    /// <summary>
    /// Gestiona el watch/unwatch automático de usuarios.
    /// Solo monitorea usuarios con transferencias activas/pendientes.
    /// Inspirado en _unwatch_stale_user de Nicotine+.
    /// </summary>
    public class UserWatchManager
    {
        private readonly HashSet<string> _watchedUsers = new();
        private readonly Dictionary<string, HashSet<string>> _userContexts = new();
        private readonly object _lock = new object();
        private ISoulseekClient _client;

        public int WatchedCount => _watchedUsers.Count;

        public UserWatchManager(ISoulseekClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>
        /// Registra interés en un usuario desde un contexto específico.
        /// </summary>
        public async Task WatchUserAsync(string username, string context)
        {
            if (string.IsNullOrWhiteSpace(username))
                return;

            bool shouldWatch = false;

            lock (_lock)
            {
                // Agregar contexto
                if (!_userContexts.ContainsKey(username))
                    _userContexts[username] = new HashSet<string>();
                
                _userContexts[username].Add(context);

                // Si es la primera vez, marcar para watch
                if (_watchedUsers.Add(username))
                    shouldWatch = true;
            }

            if (shouldWatch)
            {
                try
                {
                    // TODO: Implementar watch cuando la API lo soporte
                    // await _client.AddUserAsync(username);
                    Console.WriteLine($"Watching user: {username} (context: {context})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error watching user {username}: {ex.Message}");
                    
                    lock (_lock)
                    {
                        _watchedUsers.Remove(username);
                    }
                }
            }
        }

        /// <summary>
        /// Elimina interés en un usuario desde un contexto específico.
        /// </summary>
        public async Task UnwatchUserAsync(string username, string context)
        {
            if (string.IsNullOrWhiteSpace(username))
                return;

            bool shouldUnwatch = false;

            lock (_lock)
            {
                if (_userContexts.TryGetValue(username, out var contexts))
                {
                    contexts.Remove(context);

                    // Si no quedan contextos, unwatch
                    if (contexts.Count == 0)
                    {
                        _userContexts.Remove(username);
                        _watchedUsers.Remove(username);
                        shouldUnwatch = true;
                    }
                }
            }

            if (shouldUnwatch)
            {
                try
                {
                    // TODO: Implementar unwatch cuando la API lo soporte
                    // await _client.RemoveUserAsync(username);
                    Console.WriteLine($"Unwatching user: {username} (no more contexts)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error unwatching user {username}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Limpia usuarios obsoletos (sin contextos activos).
        /// Similar a _unwatch_stale_user de Nicotine+.
        /// </summary>
        public async Task CleanupStaleUsersAsync()
        {
            List<string> toUnwatch;

            lock (_lock)
            {
                toUnwatch = _watchedUsers
                    .Where(username => !_userContexts.ContainsKey(username) || 
                                      _userContexts[username].Count == 0)
                    .ToList();

                foreach (var username in toUnwatch)
                {
                    _watchedUsers.Remove(username);
                    _userContexts.Remove(username);
                }
            }

            foreach (var username in toUnwatch)
            {
                try
                {
                    // TODO: Implementar unwatch cuando la API lo soporte
                    // await _client.RemoveUserAsync(username);
                    Console.WriteLine($"Unwatching stale user: {username}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error unwatching stale user {username}: {ex.Message}");
                }
            }

            if (toUnwatch.Count > 0)
            {
                Console.WriteLine($"Cleaned up {toUnwatch.Count} stale users");
            }
        }

        /// <summary>
        /// Verifica si un usuario está siendo monitoreado.
        /// </summary>
        public bool IsWatching(string username)
        {
            lock (_lock)
            {
                return _watchedUsers.Contains(username);
            }
        }

        /// <summary>
        /// Obtiene los contextos activos de un usuario.
        /// </summary>
        public HashSet<string> GetUserContexts(string username)
        {
            lock (_lock)
            {
                if (_userContexts.TryGetValue(username, out var contexts))
                    return new HashSet<string>(contexts);
                
                return new HashSet<string>();
            }
        }

        /// <summary>
        /// Obtiene todos los usuarios monitoreados.
        /// </summary>
        public List<string> GetWatchedUsers()
        {
            lock (_lock)
            {
                return new List<string>(_watchedUsers);
            }
        }

        /// <summary>
        /// Obtiene estadísticas de monitoreo.
        /// </summary>
        public Dictionary<string, int> GetStats()
        {
            lock (_lock)
            {
                return _userContexts.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Count
                );
            }
        }

        /// <summary>
        /// Limpia todos los watches.
        /// </summary>
        public async Task ClearAllAsync()
        {
            List<string> users;

            lock (_lock)
            {
                users = new List<string>(_watchedUsers);
                _watchedUsers.Clear();
                _userContexts.Clear();
            }

            foreach (var username in users)
            {
                try
                {
                    // TODO: Implementar unwatch cuando la API lo soporte
                    // await _client.RemoveUserAsync(username);
                }
                catch
                {
                    // Ignorar errores durante cleanup
                }
            }

            Console.WriteLine($"Cleared all {users.Count} watched users");
        }
    }

    /// <summary>
    /// Contextos estándar para watch de usuarios.
    /// </summary>
    public static class WatchContext
    {
        public const string Downloads = "downloads";
        public const string Uploads = "uploads";
        public const string Search = "search";
        public const string Browse = "browse";
        public const string Queue = "queue";
        public const string Failed = "failed";
    }
}
