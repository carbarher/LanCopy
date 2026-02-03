using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;

namespace SlskDown
{
    // Sistema de gestión de ancho de banda inteligente
    public class BandwidthManager
    {
        private int globalUploadLimit = 0; // 0 = sin límite
        private int globalDownloadLimit = 0;
        private int alternativeUploadLimit = 0;
        private int alternativeDownloadLimit = 0;
        private bool useAlternativeLimits = false;
        
        // Modo basado en ancho de banda (no en slots)
        private bool useBandwidthMode = true;
        private int bandwidthThresholdKBps = 50; // Si usuario usa <50KB/s, liberar slot
        
        private Action<string> logAction;
        private Action<int, int> applySpeedLimitsAction;
        
        public BandwidthManager(Action<string> logger, Action<int, int> speedLimitsApplier)
        {
            logAction = logger;
            applySpeedLimitsAction = speedLimitsApplier;
        }
        
        public void SetLimits(int uploadLimit, int downloadLimit, int altUploadLimit, int altDownloadLimit)
        {
            globalUploadLimit = uploadLimit;
            globalDownloadLimit = downloadLimit;
            alternativeUploadLimit = altUploadLimit;
            alternativeDownloadLimit = altDownloadLimit;
        }
        
        public void SetBandwidthMode(bool enabled, int thresholdKBps)
        {
            useBandwidthMode = enabled;
            bandwidthThresholdKBps = thresholdKBps;
            
            logAction?.Invoke($"⚡ Bandwidth Mode: {(enabled ? "ACTIVADO" : "DESACTIVADO")} (threshold: {thresholdKBps} KB/s)");
        }
        
        public bool ShouldFreeSlot(double currentSpeedBytesPerSec)
        {
            if (!useBandwidthMode) return false;
            
            double speedKBps = currentSpeedBytesPerSec / 1024;
            return speedKBps < bandwidthThresholdKBps;
        }
        
        public void ToggleAlternativeLimits()
        {
            useAlternativeLimits = !useAlternativeLimits;
            
            if (useAlternativeLimits)
            {
                applySpeedLimitsAction?.Invoke(alternativeUploadLimit, alternativeDownloadLimit);
                logAction?.Invoke($"🔄 Límites alternativos activados: ↑{alternativeUploadLimit}KB/s ↓{alternativeDownloadLimit}KB/s");
            }
            else
            {
                applySpeedLimitsAction?.Invoke(globalUploadLimit, globalDownloadLimit);
                logAction?.Invoke($"🔄 Límites globales activados: ↑{globalUploadLimit}KB/s ↓{globalDownloadLimit}KB/s");
            }
        }
        
        public void ToggleUploadLimit()
        {
            if (globalUploadLimit == 0)
            {
                globalUploadLimit = 500; // 500 KB/s por defecto
                logAction?.Invoke($"🔼 Límite de upload activado: {globalUploadLimit} KB/s");
            }
            else
            {
                globalUploadLimit = 0;
                logAction?.Invoke($"🔼 Límite de upload desactivado (sin límite)");
            }
            
            if (!useAlternativeLimits)
            {
                applySpeedLimitsAction?.Invoke(globalUploadLimit, globalDownloadLimit);
            }
        }
        
        public void ToggleDownloadLimit()
        {
            if (globalDownloadLimit == 0)
            {
                globalDownloadLimit = 1000; // 1000 KB/s por defecto
                logAction?.Invoke($"🔽 Límite de download activado: {globalDownloadLimit} KB/s");
            }
            else
            {
                globalDownloadLimit = 0;
                logAction?.Invoke($"🔽 Límite de download desactivado (sin límite)");
            }
            
            if (!useAlternativeLimits)
            {
                applySpeedLimitsAction?.Invoke(globalUploadLimit, globalDownloadLimit);
            }
        }
        
        public void CreateBandwidthToggles(StatusStrip statusBar)
        {
            // Botón para toggle de límite de upload
            var btnToggleUpload = new ToolStripButton
            {
                Text = "↑ Limit",
                ToolTipText = "Toggle upload speed limit",
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(50, 50, 50)
            };
            btnToggleUpload.Click += (s, e) => ToggleUploadLimit();
            statusBar.Items.Add(btnToggleUpload);
            
            // Botón para toggle de límite de download
            var btnToggleDownload = new ToolStripButton
            {
                Text = "↓ Limit",
                ToolTipText = "Toggle download speed limit",
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(50, 50, 50)
            };
            btnToggleDownload.Click += (s, e) => ToggleDownloadLimit();
            statusBar.Items.Add(btnToggleDownload);
            
            // Botón para toggle de límites alternativos
            var btnToggleAlt = new ToolStripButton
            {
                Text = "⚡ Alt",
                ToolTipText = "Toggle alternative speed limits",
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor = Color.Orange,
                BackColor = Color.FromArgb(50, 50, 50)
            };
            btnToggleAlt.Click += (s, e) => ToggleAlternativeLimits();
            statusBar.Items.Add(btnToggleAlt);
        }
        
        public bool IsUsingAlternativeLimits()
        {
            return useAlternativeLimits;
        }
        
        public (int upload, int download) GetCurrentLimits()
        {
            if (useAlternativeLimits)
                return (alternativeUploadLimit, alternativeDownloadLimit);
            else
                return (globalUploadLimit, globalDownloadLimit);
        }
    }
    
    // Sistema de estadísticas de usuario detalladas
    public class UserStatistics
    {
        public string Username { get; set; }
        
        // Estadísticas de descarga
        public int TotalDownloads { get; set; }
        public int CompletedDownloads { get; set; }
        public long TotalBytesDownloaded { get; set; }
        public double AverageDownloadSpeed { get; set; }
        public double FastestDownloadSpeed { get; set; }
        public double SlowestDownloadSpeed { get; set; } = double.MaxValue;
        
        // Estadísticas de upload
        public int TotalUploads { get; set; }
        public int CompletedUploads { get; set; }
        public long TotalBytesUploaded { get; set; }
        public double AverageUploadSpeed { get; set; }
        
        // Fiabilidad
        public int ConnectionTimeouts { get; set; }
        public int ConnectionFailures { get; set; }
        public double ReliabilityScore => TotalDownloads > 0 
            ? (double)CompletedDownloads / TotalDownloads * 100 
            : 0;
        
        // Tiempos
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public TimeSpan TotalConnectionTime { get; set; }
        
        // Calificación general (0-5 estrellas)
        public double Rating => CalculateRating();
        
        private double CalculateRating()
        {
            if (TotalDownloads == 0) return 0;
            
            double rating = 0;
            
            // 40% basado en fiabilidad
            rating += (ReliabilityScore / 100) * 2;
            
            // 30% basado en velocidad (normalizado)
            if (AverageDownloadSpeed > 0)
            {
                double speedScore = Math.Min(AverageDownloadSpeed / (1024 * 1024), 1.0); // Max 1MB/s = 1.0
                rating += speedScore * 1.5;
            }
            
            // 30% basado en cantidad de descargas exitosas
            double volumeScore = Math.Min(CompletedDownloads / 100.0, 1.0); // Max 100 descargas = 1.0
            rating += volumeScore * 1.5;
            
            return Math.Min(rating, 5.0);
        }
    }
    
    public class StatisticsManager
    {
        private Dictionary<string, UserStatistics> userStats = new Dictionary<string, UserStatistics>(StringComparer.OrdinalIgnoreCase);
        private string statsPath;
        private Action<string> logAction;
        
        public StatisticsManager(string dataDir, Action<string> logger)
        {
            statsPath = System.IO.Path.Combine(dataDir, "user_statistics.json");
            logAction = logger;
            LoadStatistics();
        }
        
        public void UpdateDownloadStats(string username, long bytes, double speed, bool completed, bool timeout = false, bool failed = false)
        {
            if (!userStats.ContainsKey(username))
            {
                userStats[username] = new UserStatistics 
                { 
                    Username = username, 
                    FirstSeen = DateTime.Now,
                    SlowestDownloadSpeed = double.MaxValue
                };
            }
            
            var stats = userStats[username];
            stats.TotalDownloads++;
            if (completed) stats.CompletedDownloads++;
            if (timeout) stats.ConnectionTimeouts++;
            if (failed) stats.ConnectionFailures++;
            
            stats.TotalBytesDownloaded += bytes;
            
            if (speed > 0)
            {
                // Actualizar velocidad promedio (media móvil)
                if (stats.AverageDownloadSpeed == 0)
                    stats.AverageDownloadSpeed = speed;
                else
                    stats.AverageDownloadSpeed = (stats.AverageDownloadSpeed * 0.8) + (speed * 0.2);
                
                stats.FastestDownloadSpeed = Math.Max(stats.FastestDownloadSpeed, speed);
                stats.SlowestDownloadSpeed = Math.Min(stats.SlowestDownloadSpeed, speed);
            }
            
            stats.LastSeen = DateTime.Now;
            
            SaveStatistics();
        }
        
        public void UpdateUploadStats(string username, long bytes, double speed, bool completed)
        {
            if (!userStats.ContainsKey(username))
            {
                userStats[username] = new UserStatistics 
                { 
                    Username = username, 
                    FirstSeen = DateTime.Now,
                    SlowestDownloadSpeed = double.MaxValue
                };
            }
            
            var stats = userStats[username];
            stats.TotalUploads++;
            if (completed) stats.CompletedUploads++;
            stats.TotalBytesUploaded += bytes;
            
            if (speed > 0)
            {
                if (stats.AverageUploadSpeed == 0)
                    stats.AverageUploadSpeed = speed;
                else
                    stats.AverageUploadSpeed = (stats.AverageUploadSpeed * 0.8) + (speed * 0.2);
            }
            
            stats.LastSeen = DateTime.Now;
            
            SaveStatistics();
        }
        
        public UserStatistics GetUserStats(string username)
        {
            return userStats.ContainsKey(username) ? userStats[username] : null;
        }
        
        public List<UserStatistics> GetTopUsers(int count = 10, string sortBy = "bytes")
        {
            IEnumerable<UserStatistics> sorted = sortBy.ToLower() switch
            {
                "bytes" => userStats.Values.OrderByDescending(s => s.TotalBytesDownloaded),
                "speed" => userStats.Values.OrderByDescending(s => s.AverageDownloadSpeed),
                "reliability" => userStats.Values.OrderByDescending(s => s.ReliabilityScore),
                "rating" => userStats.Values.OrderByDescending(s => s.Rating),
                _ => userStats.Values.OrderByDescending(s => s.TotalBytesDownloaded)
            };
            
            return sorted.Take(count).ToList();
        }
        
        public void ShowUserStatistics(string username, Func<long, string> formatSize, Func<double, string> formatSpeed)
        {
            if (!userStats.ContainsKey(username))
            {
                logAction?.Invoke($"⚠️ No hay estadísticas para {username}");
                return;
            }
            
            var stats = userStats[username];
            logAction?.Invoke("");
            logAction?.Invoke($"📊 ESTADÍSTICAS DE {username}");
            logAction?.Invoke($"═══════════════════════════════════════");
            logAction?.Invoke($"⭐ Calificación: {new string('★', (int)Math.Round(stats.Rating))}{new string('☆', 5 - (int)Math.Round(stats.Rating))} ({stats.Rating:F1}/5.0)");
            logAction?.Invoke($"");
            logAction?.Invoke($"📥 DESCARGAS:");
            logAction?.Invoke($"   Total: {stats.CompletedDownloads}/{stats.TotalDownloads} ({stats.ReliabilityScore:F1}%)");
            logAction?.Invoke($"   Bytes: {formatSize(stats.TotalBytesDownloaded)}");
            logAction?.Invoke($"   Velocidad promedio: {formatSpeed(stats.AverageDownloadSpeed)}");
            logAction?.Invoke($"   Velocidad máxima: {formatSpeed(stats.FastestDownloadSpeed)}");
            if (stats.SlowestDownloadSpeed < double.MaxValue)
                logAction?.Invoke($"   Velocidad mínima: {formatSpeed(stats.SlowestDownloadSpeed)}");
            logAction?.Invoke($"");
            logAction?.Invoke($"📤 UPLOADS:");
            logAction?.Invoke($"   Total: {stats.CompletedUploads}/{stats.TotalUploads}");
            logAction?.Invoke($"   Bytes: {formatSize(stats.TotalBytesUploaded)}");
            logAction?.Invoke($"   Velocidad promedio: {formatSpeed(stats.AverageUploadSpeed)}");
            logAction?.Invoke($"");
            logAction?.Invoke($"🔌 CONEXIÓN:");
            logAction?.Invoke($"   Timeouts: {stats.ConnectionTimeouts}");
            logAction?.Invoke($"   Fallos: {stats.ConnectionFailures}");
            logAction?.Invoke($"   Primera vez: {stats.FirstSeen:yyyy-MM-dd HH:mm}");
            logAction?.Invoke($"   Última vez: {stats.LastSeen:yyyy-MM-dd HH:mm}");
            logAction?.Invoke($"═══════════════════════════════════════");
        }
        
        public void ShowTopUsers(int count, Func<long, string> formatSize, Func<double, string> formatSpeed)
        {
            var topUsers = GetTopUsers(count, "rating");
            
            logAction?.Invoke("");
            logAction?.Invoke($"🏆 TOP {count} USUARIOS (por calificación)");
            logAction?.Invoke($"═══════════════════════════════════════");
            
            int rank = 1;
            foreach (var stats in topUsers)
            {
                string medal = rank switch
                {
                    1 => "🥇",
                    2 => "🥈",
                    3 => "🥉",
                    _ => $"{rank}."
                };
                
                logAction?.Invoke($"{medal} {stats.Username}");
                logAction?.Invoke($"   ⭐ {stats.Rating:F1}/5.0 | {stats.CompletedDownloads} descargas | {formatSize(stats.TotalBytesDownloaded)}");
                logAction?.Invoke($"   ⚡ {formatSpeed(stats.AverageDownloadSpeed)} | Fiabilidad: {stats.ReliabilityScore:F0}%");
                logAction?.Invoke("");
                rank++;
            }
            
            logAction?.Invoke($"═══════════════════════════════════════");
        }
        
        private void LoadStatistics()
        {
            try
            {
                if (System.IO.File.Exists(statsPath))
                {
                    var json = System.IO.File.ReadAllText(statsPath);
                    var stats = System.Text.Json.JsonSerializer.Deserialize<List<UserStatistics>>(json);
                    
                    if (stats != null)
                    {
                        userStats = stats.ToDictionary(s => s.Username, StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
            catch { }
        }
        
        private void SaveStatistics()
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(userStats.Values.ToList(), 
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(statsPath, json);
            }
            catch { }
        }
    }
}
