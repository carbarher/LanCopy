using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SlskDown.Configuration
{
    /// <summary>
    /// Configuración de la aplicación con validación
    /// </summary>
    public class AppSettings
    {
        [Required]
        [Range(1, 100)]
        public int MaxConcurrentDownloads { get; set; } = 3;
        
        [Required]
        [Range(1000, 300000)]
        public int SearchTimeoutMs { get; set; } = 30000;
        
        [Required]
        [Range(100, 100000)]
        public int MaxSearchResults { get; set; } = 5000;
        
        [Required]
        [Range(1, 1000)]
        public int MaxRetries { get; set; } = 3;
        
        [Required]
        [Range(100, 60000)]
        public int RetryDelayMs { get; set; } = 1000;
        
        [Required]
        [Range(1, 100)]
        public int CircuitBreakerThreshold { get; set; } = 5;
        
        [Required]
        [Range(1000, 300000)]
        public int CircuitBreakerTimeoutMs { get; set; } = 30000;
        
        [Required]
        [Range(1, 100)]
        public int ObjectPoolMaxSize { get; set; } = 50;
        
        [Required]
        [Range(1, 1000)]
        public int LogMaxFiles { get; set; } = 10;
        
        [Required]
        [Range(1, 100)]
        public int LogMaxSizeMB { get; set; } = 10;
        
        [Required]
        public string DownloadPath { get; set; } = "Downloads";
        
        [Required]
        public string LogPath { get; set; } = "logs";
        
        public bool EnableMetrics { get; set; } = true;
        
        public bool EnableLogging { get; set; } = true;
        
        public bool EnableRetryPolicy { get; set; } = true;
        
        public bool EnableCircuitBreaker { get; set; } = true;
        
        public bool EnableObjectPooling { get; set; } = true;
        
        /// <summary>
        /// Valida la configuración
        /// </summary>
        public ConfigValidationResult Validate()
        {
            var context = new ValidationContext(this);
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            
            if (!Validator.TryValidateObject(this, context, results, validateAllProperties: true))
            {
                var errors = string.Join(", ", results.Select(r => r.ErrorMessage));
                return ConfigValidationResult.Failure(errors);
            }
            
            // Validaciones adicionales
            if (!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(DownloadPath) ?? "."))
            {
                return ConfigValidationResult.Failure($"Download path directory does not exist: {DownloadPath}");
            }
            
            return ConfigValidationResult.Success();
        }
        
        /// <summary>
        /// Crea configuración por defecto
        /// </summary>
        public static AppSettings Default => new AppSettings();
    }
    
    /// <summary>
    /// Resultado de validación de configuración
    /// </summary>
    public class ConfigValidationResult
    {
        public bool IsValid { get; private set; }
        public string? ErrorMessage { get; private set; }
        
        private ConfigValidationResult(bool isValid, string? errorMessage = null)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }
        
        public static ConfigValidationResult Success() => new ConfigValidationResult(true);
        public static ConfigValidationResult Failure(string errorMessage) => new ConfigValidationResult(false, errorMessage);
    }
    
    /// <summary>
    /// Manager de configuración con carga y guardado
    /// </summary>
    public class ConfigurationManager
    {
        private static readonly Lazy<ConfigurationManager> _instance = 
            new(() => new ConfigurationManager());
        
        public static ConfigurationManager Instance => _instance.Value;
        
        private AppSettings _settings;
        private readonly string _configPath;
        
        private ConfigurationManager()
        {
            _configPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "appsettings.json");
            
            _settings = LoadSettings();
        }
        
        /// <summary>
        /// Configuración actual
        /// </summary>
        public AppSettings Settings => _settings;
        
        /// <summary>
        /// Carga la configuración desde disco
        /// </summary>
        private AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    
                    if (settings != null)
                    {
                        var validation = settings.Validate();
                        
                        if (validation.IsValid)
                        {
                            Logging.Logger.Instance.Info("Configuration loaded successfully");
                            return settings;
                        }
                        else
                        {
                            Logging.Logger.Instance.Warning(
                                $"Configuration validation failed: {validation.ErrorMessage}. Using defaults.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Logger.Instance.Error("Error loading configuration. Using defaults.", ex);
            }
            
            return AppSettings.Default;
        }
        
        /// <summary>
        /// Guarda la configuración a disco
        /// </summary>
        public async System.Threading.Tasks.Task SaveSettingsAsync()
        {
            try
            {
                var validation = _settings.Validate();
                
                if (!validation.IsValid)
                {
                    throw new InvalidOperationException(
                        $"Cannot save invalid configuration: {validation.ErrorMessage}");
                }
                
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_configPath, json);
                Logging.Logger.Instance.Info("Configuration saved successfully");
            }
            catch (Exception ex)
            {
                Logging.Logger.Instance.Error("Error saving configuration", ex);
                throw;
            }
        }
        
        /// <summary>
        /// Actualiza la configuración
        /// </summary>
        public void UpdateSettings(Action<AppSettings> updateAction)
        {
            updateAction(_settings);
            
            var validation = _settings.Validate();
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Configuration update resulted in invalid state: {validation.ErrorMessage}");
            }
        }
        
        /// <summary>
        /// Resetea a configuración por defecto
        /// </summary>
        public void ResetToDefaults()
        {
            _settings = AppSettings.Default;
            Logging.Logger.Instance.Info("Configuration reset to defaults");
        }
    }
}
