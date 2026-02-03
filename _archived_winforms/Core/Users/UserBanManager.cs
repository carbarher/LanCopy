using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;

namespace SlskDown.Core.Users
{
    /// <summary>
    /// Sistema de gestión de usuarios baneados con auto-ban inspirado en Nicotine+
    /// Banea automáticamente usuarios problemáticos basado en fallos consecutivos
    /// </summary>
    public class UserBanManager
    {
        private readonly HashSet<string> permanentlyBannedUsers;
        private readonly Dictionary<string, List<FailureRecord>> userFailures;
        private readonly Dictionary<string, BanRecord> temporaryBans;
        private readonly object lockObj = new object();
        private readonly BanConfig config;

        public event Action<string, string> OnUserBanned;
        public event Action<string> OnUserUnbanned;
        public event Action<string> OnLog;

        public UserBanManager(BanConfig config = null)
        {
            this.config = config ?? new BanConfig();
            permanentlyBannedUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            userFailures = new Dictionary<string, List<FailureRecord>>(StringComparer.OrdinalIgnoreCase);
            temporaryBans = new Dictionary<string, BanRecord>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Registra un fallo de un usuario y evalúa auto-ban
        /// </summary>
        public void RecordFailure(string username, string reason)
        {
            if (string.IsNullOrEmpty(username))
                return;

            lock (lockObj)
            {
                if (!userFailures.ContainsKey(username))
                    userFailures[username] = new List<FailureRecord>();

                userFailures[username].Add(new FailureRecord
                {
                    Timestamp = DateTime.UtcNow,
                    Reason = reason
                });

                // Limpiar fallos antiguos fuera de la ventana de tiempo
                CleanOldFailures(username);

                // Evaluar auto-ban
                var recentFailures = userFailures[username].Count;
                if (recentFailures >= config.MaxFailures)
                {
                    BanUserTemporarily(username, config.BanDuration, 
                        $"Auto-ban: {recentFailures} fallos en {config.TimeWindow.TotalMinutes:F0} minutos");
                }
            }
        }

        /// <summary>
        /// Banea un usuario permanentemente
        /// </summary>
        public void BanUserPermanently(string username, string reason = null)
        {
            if (string.IsNullOrEmpty(username))
                return;

            lock (lockObj)
            {
                permanentlyBannedUsers.Add(username);
                temporaryBans.Remove(username);
                
                Log($"🚫 Usuario baneado permanentemente: {username}" + 
                    (reason != null ? $" - {reason}" : ""));
                
                OnUserBanned?.Invoke(username, reason ?? "Ban permanente");
            }
        }

        /// <summary>
        /// Banea un usuario temporalmente
        /// </summary>
        public void BanUserTemporarily(string username, TimeSpan duration, string reason = null)
        {
            if (string.IsNullOrEmpty(username))
                return;

            lock (lockObj)
            {
                var banUntil = DateTime.UtcNow + duration;
                temporaryBans[username] = new BanRecord
                {
                    Username = username,
                    BannedAt = DateTime.UtcNow,
                    BanUntil = banUntil,
                    Reason = reason ?? "Ban temporal",
                    IsTemporary = true
                };

                Log($"Usuario baneado temporalmente: {username} hasta {banUntil:yyyy-MM-dd HH:mm}" +
                    (reason != null ? $" - {reason}" : ""));
                
                OnUserBanned?.Invoke(username, reason ?? "Ban temporal");
            }
        }

        /// <summary>
        /// Desbanea un usuario
        /// </summary>
        public bool UnbanUser(string username)
        {
            if (string.IsNullOrEmpty(username))
                return false;

            lock (lockObj)
            {
                var wasBanned = permanentlyBannedUsers.Remove(username) || 
                               temporaryBans.Remove(username);
                
                if (wasBanned)
                {
                    userFailures.Remove(username);
                    Log($"Usuario desbaneado: {username}");
                    OnUserUnbanned?.Invoke(username);
                }
                
                return wasBanned;
            }
        }

        /// <summary>
        /// Verifica si un usuario está baneado
        /// </summary>
        public bool IsUserBanned(string username)
        {
            if (string.IsNullOrEmpty(username))
                return false;

            lock (lockObj)
            {
                // Verificar ban permanente
                if (permanentlyBannedUsers.Contains(username))
                    return true;

                // Verificar ban temporal
                if (temporaryBans.TryGetValue(username, out var ban))
                {
                    if (DateTime.UtcNow < ban.BanUntil)
                        return true;
                    
                    // Ban temporal expirado, remover
                    temporaryBans.Remove(username);
                    Log($"⏰ Ban temporal expirado: {username}");
                    OnUserUnbanned?.Invoke(username);
                }

                return false;
            }
        }

        /// <summary>
        /// Obtiene información de ban de un usuario
        /// </summary>
        public BanInfo GetBanInfo(string username)
        {
            if (string.IsNullOrEmpty(username))
                return null;

            lock (lockObj)
            {
                if (permanentlyBannedUsers.Contains(username))
                {
                    return new BanInfo
                    {
                        Username = username,
                        IsBanned = true,
                        IsTemporary = false,
                        Reason = "Ban permanente"
                    };
                }

                if (temporaryBans.TryGetValue(username, out var ban))
                {
                    if (DateTime.UtcNow < ban.BanUntil)
                    {
                        return new BanInfo
                        {
                            Username = username,
                            IsBanned = true,
                            IsTemporary = true,
                            BanUntil = ban.BanUntil,
                            Reason = ban.Reason,
                            TimeRemaining = ban.BanUntil - DateTime.UtcNow
                        };
                    }
                }

                return new BanInfo
                {
                    Username = username,
                    IsBanned = false
                };
            }
        }

        /// <summary>
        /// Obtiene estadísticas de fallos de un usuario
        /// </summary>
        public UserFailureStats GetUserFailureStats(string username)
        {
            if (string.IsNullOrEmpty(username))
                return null;

            lock (lockObj)
            {
                if (!userFailures.TryGetValue(username, out var failures))
                {
                    return new UserFailureStats
                    {
                        Username = username,
                        RecentFailures = 0,
                        TotalFailures = 0
                    };
                }

                CleanOldFailures(username);

                return new UserFailureStats
                {
                    Username = username,
                    RecentFailures = failures.Count,
                    TotalFailures = failures.Count,
                    LastFailure = failures.LastOrDefault()?.Timestamp,
                    MostCommonReason = failures
                        .GroupBy(f => f.Reason)
                        .OrderByDescending(g => g.Count())
                        .FirstOrDefault()?.Key
                };
            }
        }

        /// <summary>
        /// Obtiene lista de todos los usuarios baneados
        /// </summary>
        public List<BanInfo> GetAllBannedUsers()
        {
            lock (lockObj)
            {
                var result = new List<BanInfo>();

                // Bans permanentes
                foreach (var username in permanentlyBannedUsers)
                {
                    result.Add(new BanInfo
                    {
                        Username = username,
                        IsBanned = true,
                        IsTemporary = false,
                        Reason = "Ban permanente"
                    });
                }

                // Bans temporales activos
                var now = DateTime.UtcNow;
                foreach (var ban in temporaryBans.Values)
                {
                    if (now < ban.BanUntil)
                    {
                        result.Add(new BanInfo
                        {
                            Username = ban.Username,
                            IsBanned = true,
                            IsTemporary = true,
                            BanUntil = ban.BanUntil,
                            Reason = ban.Reason,
                            TimeRemaining = ban.BanUntil - now
                        });
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Guarda la lista de bans en un archivo
        /// </summary>
        public async Task SaveToFileAsync(string filePath)
        {
            try
            {
                BanData data;
                lock (lockObj)
                {
                    data = new BanData
                    {
                        PermanentBans = permanentlyBannedUsers.ToList(),
                        TemporaryBans = temporaryBans.Values.ToList()
                    };
                }

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(filePath, json);
                Log($"Lista de bans guardada: {data.PermanentBans.Count} permanentes, {data.TemporaryBans.Count} temporales");
            }
            catch (Exception ex)
            {
                Log($"Error guardando bans: {ex.Message}");
            }
        }

        /// <summary>
        /// Carga la lista de bans desde un archivo
        /// </summary>
        public async Task LoadFromFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return;

                var json = await File.ReadAllTextAsync(filePath);
                var data = JsonSerializer.Deserialize<BanData>(json);

                lock (lockObj)
                {
                    permanentlyBannedUsers.Clear();
                    temporaryBans.Clear();

                    foreach (var username in data.PermanentBans)
                        permanentlyBannedUsers.Add(username);

                    foreach (var ban in data.TemporaryBans)
                        temporaryBans[ban.Username] = ban;
                }

                Log($"Lista de bans cargada: {data.PermanentBans.Count} permanentes, {data.TemporaryBans.Count} temporales");
            }
            catch (Exception ex)
            {
                Log($"Error cargando bans: {ex.Message}");
            }
        }

        private void CleanOldFailures(string username)
        {
            if (!userFailures.ContainsKey(username))
                return;

            var cutoff = DateTime.UtcNow - config.TimeWindow;
            userFailures[username] = userFailures[username]
                .Where(f => f.Timestamp > cutoff)
                .ToList();

            if (userFailures[username].Count == 0)
                userFailures.Remove(username);
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
    }

    /// <summary>
    /// Configuración del sistema de bans
    /// </summary>
    public class BanConfig
    {
        public int MaxFailures { get; set; } = 5;
        public TimeSpan TimeWindow { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan BanDuration { get; set; } = TimeSpan.FromHours(24);
    }

    /// <summary>
    /// Registro de fallo
    /// </summary>
    public class FailureRecord
    {
        public DateTime Timestamp { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>
    /// Registro de ban
    /// </summary>
    public class BanRecord
    {
        public string Username { get; set; }
        public DateTime BannedAt { get; set; }
        public DateTime BanUntil { get; set; }
        public string Reason { get; set; }
        public bool IsTemporary { get; set; }
    }

    /// <summary>
    /// Información de ban
    /// </summary>
    public class BanInfo
    {
        public string Username { get; set; }
        public bool IsBanned { get; set; }
        public bool IsTemporary { get; set; }
        public DateTime? BanUntil { get; set; }
        public string Reason { get; set; }
        public TimeSpan? TimeRemaining { get; set; }
    }

    /// <summary>
    /// Estadísticas de fallos de usuario
    /// </summary>
    public class UserFailureStats
    {
        public string Username { get; set; }
        public int RecentFailures { get; set; }
        public int TotalFailures { get; set; }
        public DateTime? LastFailure { get; set; }
        public string MostCommonReason { get; set; }
    }

    /// <summary>
    /// Datos de bans para persistencia
    /// </summary>
    public class BanData
    {
        public List<string> PermanentBans { get; set; }
        public List<BanRecord> TemporaryBans { get; set; }
    }
}
