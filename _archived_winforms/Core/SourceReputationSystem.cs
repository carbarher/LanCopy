using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core
{
    /// <summary>
    /// Sistema de reputación para fuentes multi-red
    /// Trackea éxito/fallo por usuario y red para priorizar fuentes confiables
    /// </summary>
    public class SourceReputationSystem
    {
        private readonly ConcurrentDictionary<string, SourceReputation> _reputations = new ConcurrentDictionary<string, SourceReputation>();
        private readonly ReputationConfig _config;

        public SourceReputationSystem(ReputationConfig config = null)
        {
            _config = config ?? new ReputationConfig();
        }

        /// <summary>
        /// Registra un intento de descarga exitoso
        /// </summary>
        public void RecordSuccess(string networkSource, string username, long bytesDownloaded, TimeSpan duration)
        {
            var key = GetKey(networkSource, username);
            var reputation = _reputations.GetOrAdd(key, _ => new SourceReputation
            {
                NetworkSource = networkSource,
                Username = username
            });

            lock (reputation)
            {
                reputation.TotalAttempts++;
                reputation.SuccessfulDownloads++;
                reputation.TotalBytesDownloaded += bytesDownloaded;
                reputation.LastSuccessTime = DateTime.UtcNow;
                
                // Calcular velocidad promedio
                var speedBps = bytesDownloaded / duration.TotalSeconds;
                reputation.AverageSpeedBps = reputation.AverageSpeedBps == 0
                    ? speedBps
                    : (reputation.AverageSpeedBps * 0.7 + speedBps * 0.3); // Media móvil

                UpdateScore(reputation);
            }
        }

        /// <summary>
        /// Registra un intento de descarga fallido
        /// </summary>
        public void RecordFailure(string networkSource, string username, FailureReason reason)
        {
            var key = GetKey(networkSource, username);
            var reputation = _reputations.GetOrAdd(key, _ => new SourceReputation
            {
                NetworkSource = networkSource,
                Username = username
            });

            lock (reputation)
            {
                reputation.TotalAttempts++;
                reputation.FailedDownloads++;
                reputation.LastFailureTime = DateTime.UtcNow;
                reputation.LastFailureReason = reason;

                // Penalizar más por ciertos tipos de fallo
                switch (reason)
                {
                    case FailureReason.UserOffline:
                        reputation.ConsecutiveFailures++;
                        break;
                    case FailureReason.FileNotFound:
                        reputation.ConsecutiveFailures += 2;
                        break;
                    case FailureReason.Timeout:
                        reputation.ConsecutiveFailures++;
                        break;
                    case FailureReason.Banned:
                        reputation.ConsecutiveFailures += 5;
                        reputation.IsBanned = true;
                        break;
                }

                UpdateScore(reputation);
            }
        }

        /// <summary>
        /// Registra velocidad lenta
        /// </summary>
        public void RecordSlowSpeed(string networkSource, string username, double speedBps)
        {
            var key = GetKey(networkSource, username);
            if (_reputations.TryGetValue(key, out var reputation))
            {
                lock (reputation)
                {
                    reputation.SlowSpeedCount++;
                    UpdateScore(reputation);
                }
            }
        }

        /// <summary>
        /// Obtiene la reputación de una fuente
        /// </summary>
        public SourceReputation GetReputation(string networkSource, string username)
        {
            var key = GetKey(networkSource, username);
            return _reputations.TryGetValue(key, out var reputation) ? reputation : null;
        }

        /// <summary>
        /// Obtiene el score de una fuente (0-100)
        /// </summary>
        public double GetScore(string networkSource, string username)
        {
            var reputation = GetReputation(networkSource, username);
            return reputation?.Score ?? _config.DefaultScore;
        }

        /// <summary>
        /// Verifica si una fuente está baneada
        /// </summary>
        public bool IsBanned(string networkSource, string username)
        {
            var reputation = GetReputation(networkSource, username);
            return reputation?.IsBanned ?? false;
        }

        /// <summary>
        /// Obtiene las mejores fuentes para un archivo
        /// </summary>
        public List<SearchResult> RankSources(List<SearchResult> results)
        {
            return results
                .Select(r => new
                {
                    Result = r,
                    Score = GetScore(r.NetworkSource, r.Username),
                    Reputation = GetReputation(r.NetworkSource, r.Username)
                })
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Reputation?.AverageSpeedBps ?? 0)
                .ThenBy(x => x.Result.QueueLength)
                .Select(x => x.Result)
                .ToList();
        }

        /// <summary>
        /// Actualiza el score basado en métricas
        /// </summary>
        private void UpdateScore(SourceReputation reputation)
        {
            double score = _config.DefaultScore;

            // Factor 1: Tasa de éxito (40%)
            if (reputation.TotalAttempts > 0)
            {
                var successRate = (double)reputation.SuccessfulDownloads / reputation.TotalAttempts;
                score += (successRate - 0.5) * 40; // -20 a +20
            }

            // Factor 2: Velocidad promedio (30%)
            if (reputation.AverageSpeedBps > 0)
            {
                var speedMbps = reputation.AverageSpeedBps / 1_000_000;
                if (speedMbps > 5) score += 15;
                else if (speedMbps > 2) score += 10;
                else if (speedMbps > 1) score += 5;
                else if (speedMbps < 0.1) score -= 10;
            }

            // Factor 3: Fallos consecutivos (penalización)
            score -= reputation.ConsecutiveFailures * 5;

            // Factor 4: Velocidades lentas
            score -= reputation.SlowSpeedCount * 2;

            // Factor 5: Bonus por red (Soulseek más confiable)
            if (reputation.NetworkSource == "Soulseek")
                score += 5;

            // Factor 6: Bonus por historial largo
            if (reputation.TotalAttempts > 10)
                score += 5;
            if (reputation.TotalAttempts > 50)
                score += 10;

            // Factor 7: Penalización por ban
            if (reputation.IsBanned)
                score = 0;

            // Limitar entre 0 y 100
            reputation.Score = Math.Max(0, Math.Min(100, score));

            // Resetear fallos consecutivos después de éxito
            if (reputation.LastSuccessTime > reputation.LastFailureTime)
            {
                reputation.ConsecutiveFailures = 0;
            }
        }

        private string GetKey(string networkSource, string username)
        {
            return $"{networkSource}:{username}";
        }

        /// <summary>
        /// Obtiene estadísticas globales
        /// </summary>
        public ReputationStatistics GetStatistics()
        {
            var stats = new ReputationStatistics();

            foreach (var reputation in _reputations.Values)
            {
                stats.TotalSources++;
                stats.TotalAttempts += reputation.TotalAttempts;
                stats.TotalSuccesses += reputation.SuccessfulDownloads;
                stats.TotalFailures += reputation.FailedDownloads;
                stats.TotalBytesDownloaded += reputation.TotalBytesDownloaded;

                if (reputation.IsBanned)
                    stats.BannedSources++;

                if (reputation.Score >= 80)
                    stats.HighReputationSources++;
                else if (reputation.Score <= 20)
                    stats.LowReputationSources++;
            }

            stats.OverallSuccessRate = stats.TotalAttempts > 0
                ? (double)stats.TotalSuccesses / stats.TotalAttempts * 100
                : 0;

            return stats;
        }

        /// <summary>
        /// Limpia reputaciones antiguas
        /// </summary>
        public int CleanupOldReputations(TimeSpan maxAge)
        {
            var cutoff = DateTime.UtcNow - maxAge;
            var toRemove = _reputations
                .Where(kvp => kvp.Value.LastSuccessTime < cutoff && kvp.Value.LastFailureTime < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in toRemove)
            {
                _reputations.TryRemove(key, out _);
            }

            return toRemove.Count;
        }
    }

    /// <summary>
    /// Reputación de una fuente
    /// </summary>
    public class SourceReputation
    {
        public string NetworkSource { get; set; }
        public string Username { get; set; }
        public int TotalAttempts { get; set; }
        public int SuccessfulDownloads { get; set; }
        public int FailedDownloads { get; set; }
        public int ConsecutiveFailures { get; set; }
        public int SlowSpeedCount { get; set; }
        public long TotalBytesDownloaded { get; set; }
        public double AverageSpeedBps { get; set; }
        public double Score { get; set; }
        public bool IsBanned { get; set; }
        public DateTime LastSuccessTime { get; set; }
        public DateTime LastFailureTime { get; set; }
        public FailureReason LastFailureReason { get; set; }

        public double SuccessRate => TotalAttempts > 0 
            ? (double)SuccessfulDownloads / TotalAttempts * 100 
            : 0;
    }

    /// <summary>
    /// Razones de fallo
    /// </summary>
    public enum FailureReason
    {
        Unknown,
        UserOffline,
        FileNotFound,
        Timeout,
        ConnectionFailed,
        Banned,
        QueueFull,
        SlowSpeed
    }

    /// <summary>
    /// Configuración del sistema de reputación
    /// </summary>
    public class ReputationConfig
    {
        public double DefaultScore { get; set; } = 50;
        public int MinAttemptsForRanking { get; set; } = 3;
        public double SlowSpeedThresholdBps { get; set; } = 100_000; // 100 KB/s
    }

    /// <summary>
    /// Estadísticas del sistema de reputación
    /// </summary>
    public class ReputationStatistics
    {
        public int TotalSources { get; set; }
        public int TotalAttempts { get; set; }
        public int TotalSuccesses { get; set; }
        public int TotalFailures { get; set; }
        public long TotalBytesDownloaded { get; set; }
        public int BannedSources { get; set; }
        public int HighReputationSources { get; set; }
        public int LowReputationSources { get; set; }
        public double OverallSuccessRate { get; set; }
    }
}
