using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SlskDown.Data
{
    /// <summary>
    /// Gestiona la carga y guardado de configuración de la aplicación
    /// </summary>
    public class ConfigManager
    {
        private readonly string _configPath;
        private readonly string _dataDir;
        private Dictionary<string, object> _config;
        private readonly object _configLock = new object();

        public ConfigManager(string dataDir)
        {
            _dataDir = dataDir;
            _configPath = Path.Combine(dataDir, "config.json");
            _config = new Dictionary<string, object>();
        }

        /// <summary>
        /// Carga la configuración desde el archivo JSON
        /// </summary>
        public Dictionary<string, object> LoadConfig()
        {
            lock (_configLock)
            {
                try
                {
                    if (!File.Exists(_configPath))
                    {
                        // Crear archivo con configuración por defecto
                        _config = GetDefaultConfig();
                        SaveConfig(_config);
                        return _config;
                    }

                    var json = File.ReadAllText(_configPath);
                    _config = JsonSerializer.Deserialize<Dictionary<string, object>>(json) 
                              ?? new Dictionary<string, object>();
                    
                    return _config;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading config: {ex.Message}");
                    return GetDefaultConfig();
                }
            }
        }

        /// <summary>
        /// Guarda la configuración en el archivo JSON
        /// </summary>
        public void SaveConfig(Dictionary<string, object> config)
        {
            lock (_configLock)
            {
                try
                {
                    _config = config;
                    
                    // Crear directorio si no existe
                    var dir = Path.GetDirectoryName(_configPath);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    var json = JsonSerializer.Serialize(config, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });

                    var tmpPath = _configPath + ".tmp";
                    File.WriteAllText(tmpPath, json);

                    if (File.Exists(_configPath))
                    {
                        File.Replace(tmpPath, _configPath, null, true);
                    }
                    else
                    {
                        File.Move(tmpPath, _configPath);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving config: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Obtiene un valor de configuración
        /// </summary>
        public T GetValue<T>(string key, T defaultValue = default)
        {
            lock (_configLock)
            {
                try
                {
                    if (_config.ContainsKey(key))
                    {
                        var value = _config[key];
                        
                        if (value is JsonElement element)
                        {
                            return DeserializeJsonElement<T>(element);
                        }
                        
                        if (value is T typedValue)
                        {
                            return typedValue;
                        }
                        
                        // Intentar convertir
                        return (T)Convert.ChangeType(value, typeof(T));
                    }
                }
                catch
                {
                    // Retornar valor por defecto si hay error
                }
                
                return defaultValue;
            }
        }

        /// <summary>
        /// Establece un valor de configuración
        /// </summary>
        public void SetValue<T>(string key, T value)
        {
            lock (_configLock)
            {
                _config[key] = value;
            }
        }

        /// <summary>
        /// Guarda la configuración actual
        /// </summary>
        public void Save()
        {
            SaveConfig(_config);
        }

        /// <summary>
        /// Crea un backup de la configuración
        /// </summary>
        public void CreateBackup()
        {
            try
            {
                var backupDir = Path.Combine(_dataDir, "backups");
                Directory.CreateDirectory(backupDir);
                
                // Mantener solo los últimos 5 backups
                var backupFiles = Directory.GetFiles(backupDir, "config_*.json")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .ToList();
                
                // Eliminar backups antiguos
                foreach (var oldBackup in backupFiles.Skip(4))
                {
                    try { File.Delete(oldBackup); } catch { }
                }
                
                // Crear nuevo backup
                var backupPath = Path.Combine(backupDir, $"config_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                File.Copy(_configPath, backupPath, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating backup: {ex.Message}");
            }
        }

        /// <summary>
        /// Restaura la configuración desde un backup
        /// </summary>
        public bool RestoreFromBackup(string backupPath)
        {
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, _configPath, true);
                    LoadConfig();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restoring backup: {ex.Message}");
            }
            
            return false;
        }

        /// <summary>
        /// Obtiene la lista de backups disponibles
        /// </summary>
        public List<string> GetAvailableBackups()
        {
            try
            {
                var backupDir = Path.Combine(_dataDir, "backups");
                if (Directory.Exists(backupDir))
                {
                    return Directory.GetFiles(backupDir, "config_*.json")
                        .OrderByDescending(f => File.GetCreationTime(f))
                        .ToList();
                }
            }
            catch { }
            
            return new List<string>();
        }

        /// <summary>
        /// Verifica si existe una clave en la configuración
        /// </summary>
        public bool ContainsKey(string key)
        {
            lock (_configLock)
            {
                return _config.ContainsKey(key);
            }
        }

        /// <summary>
        /// Obtiene todas las claves de configuración
        /// </summary>
        public IEnumerable<string> GetKeys()
        {
            lock (_configLock)
            {
                return _config.Keys.ToList();
            }
        }

        /// <summary>
        /// Limpia la configuración (resetea a valores por defecto)
        /// </summary>
        public void Reset()
        {
            lock (_configLock)
            {
                _config = GetDefaultConfig();
                SaveConfig(_config);
            }
        }

        /// <summary>
        /// Obtiene la configuración por defecto
        /// </summary>
        private Dictionary<string, object> GetDefaultConfig()
        {
            return new Dictionary<string, object>
            {
                ["username"] = "",
                ["password"] = "",
                ["downloadDir"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                ["autoConnect"] = true,
                ["maxParallelDownloads"] = 3,
                ["maxDownloadsPerProvider"] = 1,
                ["maxParallelSearches"] = 3,
                ["maxRetries"] = 3,
                ["maxAlternativeRetries"] = 3,
                ["searchTimeout"] = 30,
                ["responseLimit"] = 5000,
                ["fileLimit"] = 0,
                ["listenPort"] = 50000,
                ["enableDistributedNetwork"] = true,
                ["minFileSizeKB"] = 0,
                ["organizeByAuthor"] = false,
                ["autoBackup"] = false,
                ["autoMode"] = false,
                ["instantDownload"] = true,
                ["onlyNewFilesInAutoSearch"] = false,
                ["priorityBySize"] = false,
                ["searchHistory"] = new List<string>(),
                ["favorites"] = new List<string>(),
                ["lvDownloadsColumnWidths"] = new List<int>()
            };
        }

        /// <summary>
        /// Deserializa un JsonElement al tipo especificado
        /// </summary>
        private T DeserializeJsonElement<T>(JsonElement element)
        {
            var type = typeof(T);
            
            if (type == typeof(string))
                return (T)(object)element.GetString();
            if (type == typeof(int))
                return (T)(object)element.GetInt32();
            if (type == typeof(long))
                return (T)(object)element.GetInt64();
            if (type == typeof(bool))
                return (T)(object)element.GetBoolean();
            if (type == typeof(double))
                return (T)(object)element.GetDouble();
            if (type == typeof(List<string>))
                return (T)(object)JsonSerializer.Deserialize<List<string>>(element.GetRawText());
            if (type == typeof(List<int>))
                return (T)(object)JsonSerializer.Deserialize<List<int>>(element.GetRawText());
            
            // Intentar deserialización genérica
            return JsonSerializer.Deserialize<T>(element.GetRawText());
        }
    }
}
