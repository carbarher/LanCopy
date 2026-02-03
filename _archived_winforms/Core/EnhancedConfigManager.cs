using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Gestor de configuración mejorado con validación, migración y auto-guardado
    /// </summary>
    public class EnhancedConfigManager
    {
        // Configuración
        private readonly string configFilePath;
        private readonly int configVersion = 2; // Versión actual del formato
        
        // Datos
        private Dictionary<string, object> config = new Dictionary<string, object>();
        private readonly object configLock = new object();
        
        // Auto-guardado
        private System.Threading.Timer autoSaveTimer;
        private bool isDirty = false;
        private readonly int autoSaveInterval = 30000; // 30 segundos
        
        // Validadores
        private readonly Dictionary<string, Func<object, bool>> validators = new Dictionary<string, Func<object, bool>>();
        
        // Callbacks
        public Action<string> OnLog { get; set; }
        public Action<string, object> OnValueChanged { get; set; }
        
        public EnhancedConfigManager(string filePath)
        {
            configFilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            
            // Registrar validadores por defecto
            RegisterDefaultValidators();
            
            // Iniciar auto-guardado
            autoSaveTimer = new System.Threading.Timer(AutoSaveCallback, null, autoSaveInterval, autoSaveInterval);
        }
        
        #region Carga y Guardado
        
        /// <summary>
        /// Carga la configuración desde disco
        /// </summary>
        public async Task<bool> LoadAsync()
        {
            try
            {
                if (!File.Exists(configFilePath))
                {
                    Log("ℹ️ No existe archivo de configuración, usando valores por defecto");
                    return false;
                }
                
                string json = await File.ReadAllTextAsync(configFilePath);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                
                if (loaded == null)
                {
                    Log("⚠️ Archivo de configuración vacío");
                    return false;
                }
                
                lock (configLock)
                {
                    config.Clear();
                    
                    foreach (var kvp in loaded)
                    {
                        config[kvp.Key] = ConvertJsonElement(kvp.Value);
                    }
                }
                
                // Verificar y migrar versión si es necesario
                int loadedVersion = GetValue("configVersion", 1);
                if (loadedVersion < configVersion)
                {
                    Log($"🔄 Migrando configuración de v{loadedVersion} a v{configVersion}");
                    MigrateConfig(loadedVersion, configVersion);
                }
                
                Log($"✅ Configuración cargada: {config.Count} valores");
                return true;
            }
            catch (Exception ex)
            {
                Log($"❌ Error cargando configuración: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Guarda la configuración en disco
        /// </summary>
        public async Task<bool> SaveAsync()
        {
            try
            {
                Dictionary<string, object> snapshot;
                
                lock (configLock)
                {
                    if (!isDirty)
                    {
                        return true; // No hay cambios
                    }
                    
                    snapshot = new Dictionary<string, object>(config);
                    isDirty = false;
                }
                
                // Asegurar versión
                snapshot["configVersion"] = configVersion;
                
                string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                // Crear backup antes de guardar
                if (File.Exists(configFilePath))
                {
                    string backupPath = configFilePath + ".bak";
                    File.Copy(configFilePath, backupPath, true);
                }
                
                await File.WriteAllTextAsync(configFilePath, json);
                
                Log($"💾 Configuración guardada: {snapshot.Count} valores");
                return true;
            }
            catch (Exception ex)
            {
                Log($"❌ Error guardando configuración: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Callback de auto-guardado
        /// </summary>
        private void AutoSaveCallback(object state)
        {
            if (isDirty)
            {
                _ = SaveAsync();
            }
        }
        
        #endregion
        
        #region Getters y Setters
        
        /// <summary>
        /// Obtiene un valor con tipo específico
        /// </summary>
        public T GetValue<T>(string key, T defaultValue = default)
        {
            lock (configLock)
            {
                if (!config.TryGetValue(key, out var value))
                {
                    return defaultValue;
                }
                
                try
                {
                    if (value is T typedValue)
                    {
                        return typedValue;
                    }
                    
                    // Intentar conversión
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    Log($"⚠️ Error convirtiendo '{key}' a {typeof(T).Name}, usando default");
                    return defaultValue;
                }
            }
        }
        
        /// <summary>
        /// Establece un valor con validación
        /// </summary>
        public bool SetValue<T>(string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;
            
            // Validar si existe validador
            if (validators.TryGetValue(key, out var validator))
            {
                if (!validator(value))
                {
                    Log($"❌ Validación fallida para '{key}': {value}");
                    return false;
                }
            }
            
            lock (configLock)
            {
                var oldValue = config.TryGetValue(key, out var old) ? old : null;
                
                // Solo marcar como dirty si el valor cambió
                if (!Equals(oldValue, value))
                {
                    config[key] = value;
                    isDirty = true;
                    
                    OnValueChanged?.Invoke(key, value);
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Elimina un valor
        /// </summary>
        public bool RemoveValue(string key)
        {
            lock (configLock)
            {
                if (config.Remove(key))
                {
                    isDirty = true;
                    return true;
                }
                return false;
            }
        }
        
        /// <summary>
        /// Verifica si existe una clave
        /// </summary>
        public bool HasValue(string key)
        {
            lock (configLock)
            {
                return config.ContainsKey(key);
            }
        }
        
        /// <summary>
        /// Obtiene todas las claves
        /// </summary>
        public List<string> GetAllKeys()
        {
            lock (configLock)
            {
                return new List<string>(config.Keys);
            }
        }
        
        #endregion
        
        #region Validadores
        
        /// <summary>
        /// Registra un validador para una clave
        /// </summary>
        public void RegisterValidator(string key, Func<object, bool> validator)
        {
            if (string.IsNullOrWhiteSpace(key) || validator == null)
                return;
            
            validators[key] = validator;
        }
        
        /// <summary>
        /// Registra validadores por defecto
        /// </summary>
        private void RegisterDefaultValidators()
        {
            // Números positivos
            RegisterValidator("maxParallelDownloads", v => v is int i && i > 0 && i <= 20);
            RegisterValidator("maxSimultaneousDownloads", v => v is int i && i > 0 && i <= 20);
            RegisterValidator("maxParallelSearches", v => v is int i && i > 0 && i <= 20);
            RegisterValidator("maxRetries", v => v is int i && i >= 0 && i <= 10);
            RegisterValidator("maxAlternativeRetries", v => v is int i && i >= 0 && i <= 10);
            
            // Timeouts
            RegisterValidator("searchTimeout", v => v is int i && i >= 5 && i <= 120);
            RegisterValidator("responseLimit", v => v is int i && i > 0 && i <= 1000);
            RegisterValidator("fileLimit", v => v is int i && i > 0 && i <= 100000);
            
            // Tamaños
            RegisterValidator("minFileSizeKB", v => v is int i && i >= 0);
            
            // Strings no vacíos
            RegisterValidator("downloadDir", v => v is string s && !string.IsNullOrWhiteSpace(s));
            RegisterValidator("username", v => v is string s && !string.IsNullOrWhiteSpace(s));
        }
        
        #endregion
        
        #region Migración
        
        /// <summary>
        /// Migra configuración entre versiones
        /// </summary>
        private void MigrateConfig(int fromVersion, int toVersion)
        {
            lock (configLock)
            {
                if (fromVersion == 1 && toVersion >= 2)
                {
                    // Migración v1 → v2
                    // Ejemplo: Renombrar claves, convertir valores, etc.
                    
                    // Si existía "maxDownloads", renombrar a "maxSimultaneousDownloads"
                    if (config.TryGetValue("maxDownloads", out var maxDownloads))
                    {
                        config["maxSimultaneousDownloads"] = maxDownloads;
                        config.Remove("maxDownloads");
                        Log("🔄 Migrado: maxDownloads → maxSimultaneousDownloads");
                    }
                    
                    // Agregar nuevos valores por defecto
                    if (!config.ContainsKey("useVirtualListView"))
                    {
                        config["useVirtualListView"] = true;
                    }
                    
                    if (!config.ContainsKey("useSQLiteForLargeResults"))
                    {
                        config["useSQLiteForLargeResults"] = true;
                    }
                }
                
                // Actualizar versión
                config["configVersion"] = toVersion;
                isDirty = true;
                
                Log($"✅ Migración completada: v{fromVersion} → v{toVersion}");
            }
        }
        
        #endregion
        
        #region Utilidades
        
        /// <summary>
        /// Convierte JsonElement a tipo apropiado
        /// </summary>
        private object ConvertJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt32(out int i) ? (object)i : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => element.EnumerateArray(),
                JsonValueKind.Object => element,
                _ => null
            };
        }
        
        /// <summary>
        /// Exporta configuración a JSON
        /// </summary>
        public string ExportToJson()
        {
            lock (configLock)
            {
                return JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            }
        }
        
        /// <summary>
        /// Importa configuración desde JSON
        /// </summary>
        public bool ImportFromJson(string json)
        {
            try
            {
                var imported = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                
                if (imported == null)
                    return false;
                
                lock (configLock)
                {
                    foreach (var kvp in imported)
                    {
                        var value = ConvertJsonElement(kvp.Value);
                        
                        // Validar antes de importar
                        if (validators.TryGetValue(kvp.Key, out var validator))
                        {
                            if (!validator(value))
                            {
                                Log($"⚠️ Valor inválido al importar '{kvp.Key}', omitiendo");
                                continue;
                            }
                        }
                        
                        config[kvp.Key] = value;
                    }
                    
                    isDirty = true;
                }
                
                Log($"✅ Configuración importada: {imported.Count} valores");
                return true;
            }
            catch (Exception ex)
            {
                Log($"❌ Error importando configuración: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Reinicia configuración a valores por defecto
        /// </summary>
        public void ResetToDefaults()
        {
            lock (configLock)
            {
                config.Clear();
                isDirty = true;
                Log("🔄 Configuración reiniciada a valores por defecto");
            }
        }
        
        #endregion
        
        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
        
        public void Dispose()
        {
            autoSaveTimer?.Dispose();
            
            // Guardar cambios pendientes (sin bloquear)
            if (isDirty)
            {
                try
                {
                    SaveAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch { }
            }
        }
    }
}
