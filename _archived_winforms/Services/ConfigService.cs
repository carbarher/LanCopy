using System;
using System.IO;
using System.Text.Json;

namespace SlskDown.Services
{
    /// <summary>
    /// Implementación del servicio de configuración con encriptación
    /// </summary>
    public class ConfigService : IConfigService
    {
        private readonly ISecurityService _securityService;
        private readonly string _configPath;
        private AppConfig? _cachedConfig;
        private readonly object _lock = new();

        public ConfigService(ISecurityService securityService)
            : this(securityService, null)
        {
        }

        public ConfigService(ISecurityService securityService, string? configDirectory)
        {
            _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var portableDir = Path.Combine(baseDir, "data");
            var resolvedDir = !string.IsNullOrWhiteSpace(configDirectory)
                ? configDirectory
                : Directory.Exists(portableDir)
                    ? portableDir
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SlskDown");

            if (!Directory.Exists(resolvedDir))
            {
                Directory.CreateDirectory(resolvedDir);
            }

            _configPath = Path.Combine(resolvedDir, "config_secure.json");
        }

        /// <summary>
        /// Carga la configuración desde disco
        /// </summary>
        public AppConfig LoadConfig()
        {
            if (_cachedConfig != null)
                return _cachedConfig;

            lock (_lock)
            {
                // Double-check locking pattern
                if (_cachedConfig != null)
                    return _cachedConfig;

                try
                {
                    if (!File.Exists(_configPath))
                    {
                        try
                        {
                            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                            var legacyCandidates = new[]
                            {
                                Path.Combine(baseDir, "config_secure.json"),
                                Path.Combine(baseDir, "data", "config_secure.json")
                            };

                            foreach (var legacyPath in legacyCandidates)
                            {
                                if (File.Exists(legacyPath) &&
                                    !string.Equals(legacyPath, _configPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    var targetDir = Path.GetDirectoryName(_configPath);
                                    if (!string.IsNullOrWhiteSpace(targetDir) && !Directory.Exists(targetDir))
                                    {
                                        Directory.CreateDirectory(targetDir);
                                    }

                                    File.Copy(legacyPath, _configPath, overwrite: false);
                                    break;
                                }
                            }
                        }
                        catch
                        {
                        }
                    }

                    if (File.Exists(_configPath))
                    {
                        var json = File.ReadAllText(_configPath);
                        _cachedConfig = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                    }
                    else
                    {
                        _cachedConfig = new AppConfig();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error cargando configuración: {ex.Message}");
                    _cachedConfig = new AppConfig();
                }

                return _cachedConfig;
            }
        }

        /// <summary>
        /// Guarda la configuración en disco
        /// </summary>
        public void SaveConfig(AppConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            lock (_lock)
            {
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };

                    var json = JsonSerializer.Serialize(config, options);
                    
                    // Escribir a archivo temporal primero
                    var tempPath = _configPath + ".tmp";
                    File.WriteAllText(tempPath, json);
                    
                    // Reemplazar archivo original
                    File.Move(tempPath, _configPath, true);
                    
                    _cachedConfig = config;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Error guardando configuración", ex);
                }
            }
        }

        /// <summary>
        /// Obtiene las credenciales desencriptadas
        /// </summary>
        public (string username, string password) GetCredentials()
        {
            var config = LoadConfig();

            try
            {
                string username = string.Empty;
                string password = string.Empty;

                if (config.EncryptedUsername != null && config.EncryptedUsername.Length > 0)
                {
                    username = _securityService.Unprotect(config.EncryptedUsername);
                }

                if (config.EncryptedPassword != null && config.EncryptedPassword.Length > 0)
                {
                    password = _securityService.Unprotect(config.EncryptedPassword);
                }

                return (username, password);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error desencriptando credenciales: {ex.Message}");
                return (string.Empty, string.Empty);
            }
        }

        /// <summary>
        /// Guarda las credenciales encriptadas
        /// </summary>
        public void SaveCredentials(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("El nombre de usuario no puede estar vacío", nameof(username));

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("La contraseña no puede estar vacía", nameof(password));

            var config = LoadConfig();

            try
            {
                config.EncryptedUsername = _securityService.Protect(username);
                config.EncryptedPassword = _securityService.Protect(password);

                SaveConfig(config);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error guardando credenciales", ex);
            }
        }

        public void ClearCredentials()
        {
            lock (_lock)
            {
                var config = LoadConfig();
                config.EncryptedUsername = null;
                config.EncryptedPassword = null;
                SaveConfig(config);
            }
        }
    }
}

