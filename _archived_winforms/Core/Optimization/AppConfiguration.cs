using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlskDown.Core.Optimization
{
    /// <summary>
    /// Configuración centralizada de la aplicación
    /// </summary>
    public class AppConfiguration
    {
        private static AppConfiguration instance;
        private static readonly object lockObj = new object();
        
        public static AppConfiguration Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (lockObj)
                    {
                        instance ??= new AppConfiguration();
                    }
                }
                return instance;
            }
        }
        
        // Download Settings
        public int MaxConcurrentDownloads { get; set; } = 3;
        public int MaxRetryAttempts { get; set; } = 3;
        public int RetryDelaySeconds { get; set; } = 60;
        
        // Buffer Settings
        public int ReadBufferSize { get; set; } = 16384;      // 16 KB
        public int WriteBufferSize { get; set; } = 16384;     // 16 KB
        public int TransferBufferSize { get; set; } = 262144; // 256 KB
        
        // Cache Settings
        public int MaxCacheSize { get; set; } = 1000;
        public int CacheExpirationHours { get; set; } = 24;
        
        // Connection Settings
        public int ConnectionTimeoutSeconds { get; set; } = 10;
        public int InactivityTimeoutSeconds { get; set; } = 15;
        public int TransferTimeoutSeconds { get; set; } = 30;
        
        // Rate Limiting
        public int MaxRequestsPerMinute { get; set; } = 60;
        public int ApiRateLimitPerSecond { get; set; } = 5;
        
        // Circuit Breaker
        public int CircuitBreakerThreshold { get; set; } = 5;
        public int CircuitBreakerTimeoutSeconds { get; set; } = 60;
        
        // Slot Management
        public int MaxUploadSlots { get; set; } = 20;
        public int MaxDownloadSlots { get; set; } = 500;
        public int MinUploadSlots { get; set; } = 2;
        public int MinDownloadSlots { get; set; } = 5;
        
        // User Limits
        public int MaxQueuedFilesPerUser { get; set; } = 500;
        public long MaxQueuedMegabytesPerUser { get; set; } = 5000;
        public int MaxDailyFilesPerUser { get; set; } = 1000;
        public long MaxDailyMegabytesPerUser { get; set; } = 10000;
        
        // Search Settings
        public int MinSharesThreshold { get; set; } = 2;
        public int MaxSearchResults { get; set; } = 100;
        public int SearchTimeoutSeconds { get; set; } = 30;
        
        // Metrics
        public bool EnableMetrics { get; set; } = true;
        public int MetricsRetentionHours { get; set; } = 24;
        
        // API Settings
        public int ApiPort { get; set; } = 8080;
        public bool EnableApi { get; set; } = false;
        
        /// <summary>
        /// Carga configuración desde archivo JSON
        /// </summary>
        public static AppConfiguration Load(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var config = JsonSerializer.Deserialize<AppConfiguration>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        WriteIndented = true
                    });
                    
                    lock (lockObj)
                    {
                        instance = config;
                    }
                    
                    return config;
                }
            }
            catch
            {
                // Usar configuración por defecto
            }
            
            return Instance;
        }
        
        /// <summary>
        /// Guarda configuración a archivo JSON
        /// </summary>
        public void Save(string path)
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never
                });
                
                File.WriteAllText(path, json);
            }
            catch
            {
                // Ignorar errores de guardado
            }
        }
        
        /// <summary>
        /// Valida configuración
        /// </summary>
        public bool Validate(out string error)
        {
            if (MaxConcurrentDownloads <= 0)
            {
                error = "MaxConcurrentDownloads must be positive";
                return false;
            }
            
            if (TransferBufferSize < 4096)
            {
                error = "TransferBufferSize must be at least 4KB";
                return false;
            }
            
            if (MaxCacheSize <= 0)
            {
                error = "MaxCacheSize must be positive";
                return false;
            }
            
            if (ConnectionTimeoutSeconds <= 0)
            {
                error = "ConnectionTimeoutSeconds must be positive";
                return false;
            }
            
            error = null;
            return true;
        }
        
        /// <summary>
        /// Obtiene TimeSpan para timeout de conexión
        /// </summary>
        [JsonIgnore]
        public TimeSpan ConnectionTimeout => TimeSpan.FromSeconds(ConnectionTimeoutSeconds);
        
        /// <summary>
        /// Obtiene TimeSpan para timeout de inactividad
        /// </summary>
        [JsonIgnore]
        public TimeSpan InactivityTimeout => TimeSpan.FromSeconds(InactivityTimeoutSeconds);
        
        /// <summary>
        /// Obtiene TimeSpan para timeout de transferencia
        /// </summary>
        [JsonIgnore]
        public TimeSpan TransferTimeout => TimeSpan.FromSeconds(TransferTimeoutSeconds);
        
        /// <summary>
        /// Obtiene TimeSpan para delay de reintentos
        /// </summary>
        [JsonIgnore]
        public TimeSpan RetryDelay => TimeSpan.FromSeconds(RetryDelaySeconds);
    }
}
