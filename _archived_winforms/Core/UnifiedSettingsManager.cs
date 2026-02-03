using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SlskDown.Models;

namespace SlskDown.Core
{
    /// <summary>
    /// Gestor unificado de configuración - Reemplaza múltiples archivos JSON
    /// </summary>
    public class UnifiedSettingsManager
    {
        private static readonly string SettingsPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        
        public UnifiedSettings Settings { get; private set; }
        
        public UnifiedSettingsManager()
        {
            LoadOrCreateSettings();
        }
        
        /// <summary>
        /// Carga configuración o crea una por defecto
        /// </summary>
        private void LoadOrCreateSettings()
        {
            if (File.Exists(SettingsPath))
            {
                try
                {
                    var json = File.ReadAllText(SettingsPath);
                    Settings = JsonSerializer.Deserialize<UnifiedSettings>(json, JsonOptions) 
                             ?? CreateDefaultSettings();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error cargando settings: {ex.Message}");
                    Settings = CreateDefaultSettings();
                }
            }
            else
            {
                Settings = CreateDefaultSettings();
                SaveSettings();
            }
        }
        
        /// <summary>
        /// Crea configuración por defecto migrando desde archivos antiguos
        /// </summary>
        private UnifiedSettings CreateDefaultSettings()
        {
            var settings = new UnifiedSettings();
            
            // Intentar migrar desde config.json antiguo
            MigrateFromOldConfig(settings);
            
            return settings;
        }
        
        /// <summary>
        /// Migra configuración desde archivos antiguos
        /// </summary>
        private void MigrateFromOldConfig(UnifiedSettings settings)
        {
            try
            {
                var oldConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (File.Exists(oldConfigPath))
                {
                    var oldJson = File.ReadAllText(oldConfigPath);
                    var oldConfig = JsonSerializer.Deserialize<OldConfig>(oldJson);
                    
                    if (oldConfig != null)
                    {
                        // Migrar configuración de red
                        settings.Network.Soulseek.Username = oldConfig.Username;
                        settings.Network.Soulseek.Password = oldConfig.Password;
                        settings.Network.Soulseek.AutoConnect = oldConfig.AutoConnect ?? true;
                        settings.Network.Soulseek.ListenPort = oldConfig.ListenPort ?? 50000;
                        settings.Network.Soulseek.EnableDistributedNetwork = oldConfig.EnableDistributedNetwork ?? true;
                        
                        // Migrar configuración de descargas
                        settings.Download.Directory = oldConfig.DownloadDir;
                        settings.Download.MaxParallel = oldConfig.MaxParallelDownloads ?? 3;
                        settings.Download.MaxRetries = oldConfig.MaxRetries ?? 3;
                        
                        // Migrar configuración de búsqueda
                        settings.Search.Timeout = oldConfig.SearchTimeout ?? 60;
                        settings.Search.ResponseLimit = oldConfig.ResponseLimit ?? 0;
                        settings.Search.FileLimit = oldConfig.FileLimit ?? 0;
                        
                        Console.WriteLine("Configuración migrada desde config.json");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error migrando configuración: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Guarda la configuración actual
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(Settings, JsonOptions);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error guardando settings: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtiene un valor de configuración con valor por defecto
        /// </summary>
        public T GetValue<T>(string section, string key, T defaultValue = default)
        {
            try
            {
                var property = Settings.GetType().GetProperty(section);
                if (property == null) return defaultValue;
                
                var sectionObj = property.GetValue(Settings);
                if (sectionObj == null) return defaultValue;
                
                var keyProperty = sectionObj.GetType().GetProperty(key);
                return keyProperty?.GetValue(sectionObj) is T value ? value : defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }
        
        /// <summary>
        /// Establece un valor de configuración
        /// </summary>
        public void SetValue<T>(string section, string key, T value)
        {
            try
            {
                var property = Settings.GetType().GetProperty(section);
                if (property == null) return;
                
                var sectionObj = property.GetValue(Settings);
                if (sectionObj == null) return;
                
                var keyProperty = sectionObj.GetType().GetProperty(key);
                keyProperty?.SetValue(sectionObj, value);
                
                SaveSettings();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error estableciendo valor: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Reinicia toda la configuración a valores por defecto
        /// </summary>
        public void ResetToDefaults()
        {
            Settings = CreateDefaultSettings();
            SaveSettings();
        }
        
        /// <summary>
        /// Exporta configuración a un archivo
        /// </summary>
        public void ExportSettings(string filePath)
        {
            try
            {
                var json = JsonSerializer.Serialize(Settings, JsonOptions);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error exportando configuración: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Importa configuración desde un archivo
        /// </summary>
        public void ImportSettings(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                Settings = JsonSerializer.Deserialize<UnifiedSettings>(json, JsonOptions) 
                         ?? CreateDefaultSettings();
                SaveSettings();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error importando configuración: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Configuración unificada completa
    /// </summary>
    public class UnifiedSettings
    {
        public UnifiedNetworkSettings Network { get; set; } = new();
        public DownloadSettings Download { get; set; } = new();
        public SearchSettings Search { get; set; } = new();
        public UISettings UI { get; set; } = new();
        public UnifiedPerformanceSettings Performance { get; set; } = new();
        public AdvancedSettings Advanced { get; set; } = new();
        public CalibreSettings Calibre { get; set; } = new();
    }
    
    public class UnifiedNetworkSettings
    {
        public SoulseekSettings Soulseek { get; set; } = new();
        public bool EnableVpn { get; set; } = false;
        public bool AutoReconnect { get; set; } = true;
        public int ReconnectDelayMs { get; set; } = 5000;
    }
    
    public class SoulseekSettings
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string ServerAddress { get; set; } = "server.slsknet.org";
        public int ServerPort { get; set; } = 2242;
        public bool AutoConnect { get; set; } = true;
        public int ListenPort { get; set; } = 50000;
        public bool EnableDistributedNetwork { get; set; } = true;
        public int KeepAliveIntervalMs { get; set; } = 90000;
        public int ConnectionTimeoutMs { get; set; } = 30000;
    }
    
    public class DownloadSettings
    {
        public string Directory { get; set; } = @"c:\p2p\downloads";
        public int MaxParallel { get; set; } = 3;
        public int MaxRetries { get; set; } = 3;
        public int MaxAlternativeRetries { get; set; } = 3;
        public int ChunkSizeKB { get; set; } = 64;
        public bool AutoRetryFailed { get; set; } = true;
        public bool EnableSpeedLimit { get; set; } = false;
        public long SpeedLimitKBps { get; set; } = 1000;
    }
    
    public class SearchSettings
    {
        public int Timeout { get; set; } = 60;
        public int ResponseLimit { get; set; } = 0;
        public int FileLimit { get; set; } = 0;
        public bool EnableVariations { get; set; } = true;
        public bool RemoveAccents { get; set; } = true;
        public bool FilterSpanish { get; set; } = true;
        public int ResultCacheHours { get; set; } = 24;
    }
    
    public class UISettings
    {
        public string Theme { get; set; } = "dark";
        public bool AutoScrollLog { get; set; } = true;
        public bool ShowNotifications { get; set; } = true;
        public int LogMaxLines { get; set; } = 10000;
        public bool MinimizeToTray { get; set; } = true;
        public bool StartMinimized { get; set; } = false;
    }
    
    public class UnifiedPerformanceSettings
    {
        public bool EnableVirtualMode { get; set; } = true;
        public int UpdateThrottleMs { get; set; } = 500;
        public bool EnableStringInterning { get; set; } = true;
        public bool EnableObjectPooling { get; set; } = true;
        public int MaxMemoryMB { get; set; } = 1024;
    }
    
    public class AdvancedSettings
    {
        public bool EnableDebugMode { get; set; } = false;
        public bool EnableTelemetry { get; set; } = false;
        public string LogLevel { get; set; } = "Info";
        public bool EnableExperimentalFeatures { get; set; } = false;
        public int RateLimitBaseDelayMs { get; set; } = 4000;
        public int RateLimitMaxDelayMs { get; set; } = 30000;
    }
    
    public class CalibreSettings
    {
        public string LibraryPath { get; set; } = "";
        public bool AutoAddDownloads { get; set; } = false;
    }
    
    /// <summary>
    /// Clase para migrar desde config.json antiguo
    /// </summary>
    public class OldConfig
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string DownloadDir { get; set; }
        public bool? AutoConnect { get; set; }
        public int? MaxParallelDownloads { get; set; }
        public int? MaxRetries { get; set; }
        public int? SearchTimeout { get; set; }
        public int? ResponseLimit { get; set; }
        public int? FileLimit { get; set; }
        public int? ListenPort { get; set; }
        public bool? EnableDistributedNetwork { get; set; }
    }
}
