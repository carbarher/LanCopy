using System;
using System.Collections.Generic;

namespace SlskDown.Core
{
    /// <summary>
    /// Configuración avanzada por red para sistema multi-red
    /// Permite personalizar comportamiento de cada red P2P
    /// </summary>
    public class AdvancedNetworkConfig
    {
        public Dictionary<string, NetworkSettings> Networks { get; set; } = new Dictionary<string, NetworkSettings>();

        /// <summary>
        /// Obtiene configuración de una red específica
        /// </summary>
        public NetworkSettings GetNetworkSettings(string networkName)
        {
            if (!Networks.TryGetValue(networkName, out var settings))
            {
                settings = new NetworkSettings { NetworkName = networkName };
                Networks[networkName] = settings;
            }
            return settings;
        }

        /// <summary>
        /// Configuración por defecto para Soulseek
        /// </summary>
        public static AdvancedNetworkConfig CreateDefault()
        {
            var config = new AdvancedNetworkConfig();

            // Soulseek
            config.Networks["Soulseek"] = new NetworkSettings
            {
                NetworkName = "Soulseek",
                Enabled = true,
                Priority = 1,
                SearchTimeout = TimeSpan.FromSeconds(30),
                MaxConcurrentSearches = 5,
                MaxConcurrentDownloads = 3,
                ConnectionSettings = new ConnectionSettings
                {
                    Host = "server.slsknet.org",
                    Port = 2271,
                    AutoReconnect = true,
                    ReconnectDelay = TimeSpan.FromSeconds(30)
                }
            };

            // eMule
            config.Networks["eMule"] = new NetworkSettings
            {
                NetworkName = "eMule",
                Enabled = false,
                Priority = 2,
                SearchTimeout = TimeSpan.FromSeconds(45),
                MaxConcurrentSearches = 3,
                MaxConcurrentDownloads = 2,
                ConnectionSettings = new ConnectionSettings
                {
                    Host = "127.0.0.1",
                    Port = 4712,
                    AutoReconnect = true,
                    ReconnectDelay = TimeSpan.FromSeconds(60),
                    RequiresPassword = true
                }
            };

            return config;
        }
    }

    /// <summary>
    /// Configuración de una red específica
    /// </summary>
    public class NetworkSettings
    {
        public string NetworkName { get; set; }
        public bool Enabled { get; set; } = true;
        public int Priority { get; set; } = 1;
        public TimeSpan SearchTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public int MaxConcurrentSearches { get; set; } = 5;
        public int MaxConcurrentDownloads { get; set; } = 3;
        public ConnectionSettings ConnectionSettings { get; set; } = new ConnectionSettings();
        public RetrySettings RetrySettings { get; set; } = new RetrySettings();
        public PerformanceSettings PerformanceSettings { get; set; } = new PerformanceSettings();
    }

    /// <summary>
    /// Configuración de conexión
    /// </summary>
    public class ConnectionSettings
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool RequiresPassword { get; set; }
        public bool AutoReconnect { get; set; } = true;
        public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(30);
        public int MaxReconnectAttempts { get; set; } = 5;
    }

    /// <summary>
    /// Configuración de reintentos
    /// </summary>
    public class RetrySettings
    {
        public int MaxRetries { get; set; } = 3;
        public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(5);
        public double RetryDelayMultiplier { get; set; } = 2.0;
        public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);
        public bool RetryOnTimeout { get; set; } = true;
        public bool RetryOnConnectionError { get; set; } = true;
    }

    /// <summary>
    /// Configuración de rendimiento
    /// </summary>
    public class PerformanceSettings
    {
        public int MaxResultsPerSearch { get; set; } = 500;
        public bool EnableCaching { get; set; } = true;
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(24);
        public bool EnableCompression { get; set; } = true;
        public int BufferSize { get; set; } = 8192;
        public bool EnableParallelProcessing { get; set; } = true;
    }
}
