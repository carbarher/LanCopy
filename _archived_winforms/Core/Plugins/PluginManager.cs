using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SlskDown.Models;
using SlskDown.Services;

namespace SlskDown.Core.Plugins
{
    /// <summary>
    /// Sistema de plugins extensible para SlskDown
    /// </summary>
    public class PluginManager : IDisposable
    {
        private readonly Dictionary<string, IPlugin> _loadedPlugins = new Dictionary<string, IPlugin>();
        private readonly Dictionary<string, PluginMetadata> _pluginMetadata = new Dictionary<string, PluginMetadata>();
        private readonly string _pluginsDirectory;
        private readonly MainForm _mainForm;
        private volatile bool _disposed = false;

        public event EventHandler<PluginLoadedEventArgs> PluginLoaded;
        public event EventHandler<PluginUnloadedEventArgs> PluginUnloaded;
        public event EventHandler<PluginErrorEventArgs> PluginError;

        public IReadOnlyDictionary<string, IPlugin> LoadedPlugins => _loadedPlugins;
        public IReadOnlyDictionary<string, PluginMetadata> AvailablePlugins => _pluginMetadata;

        public PluginManager(MainForm mainForm)
        {
            _mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
            _pluginsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            
            InitializePluginsDirectory();
            LoadAllPlugins();
        }

        /// <summary>
        /// Inicializa directorio de plugins
        /// </summary>
        private void InitializePluginsDirectory()
        {
            if (!Directory.Exists(_pluginsDirectory))
            {
                Directory.CreateDirectory(_pluginsDirectory);
            }

            // Crear directorio para plugins desactivados
            var disabledDir = Path.Combine(_pluginsDirectory, "Disabled");
            if (!Directory.Exists(disabledDir))
            {
                Directory.CreateDirectory(disabledDir);
            }
        }

        /// <summary>
        /// Carga todos los plugins disponibles
        /// </summary>
        public async Task LoadAllPlugins()
        {
            try
            {
                var pluginFiles = Directory.GetFiles(_pluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly);
                
                foreach (var pluginFile in pluginFiles)
                {
                    await LoadPluginAsync(pluginFile);
                }

                AutoLog($"{pluginFiles.Length} plugins escaneados, {_loadedPlugins.Count} cargados");
            }
            catch (Exception ex)
            {
                AutoLog($"Error cargando plugins: {ex.Message}");
                PluginError?.Invoke(this, new PluginErrorEventArgs { Error = ex.Message });
            }
        }

        /// <summary>
        /// Carga un plugin específico
        /// </summary>
        public async Task<bool> LoadPluginAsync(string pluginPath)
        {
            if (_disposed || string.IsNullOrWhiteSpace(pluginPath))
                return false;

            try
            {
                var assembly = Assembly.LoadFrom(pluginPath);
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                foreach (var pluginType in pluginTypes)
                {
                    var plugin = (IPlugin)Activator.CreateInstance(pluginType);
                    var metadata = plugin.GetMetadata();

                    // Validar metadata
                    if (!ValidatePluginMetadata(metadata))
                    {
                        AutoLog($"Plugin inválido: {metadata.Name} - metadata inválido");
                        continue;
                    }

                    // Verificar dependencias
                    if (!await CheckDependencies(metadata))
                    {
                        AutoLog($"Plugin no cargado: {metadata.Name} - dependencias faltantes");
                        continue;
                    }

                    // Inicializar plugin
                    try
                    {
                        await plugin.InitializeAsync(_mainForm);
                        
                        _loadedPlugins[metadata.Id] = plugin;
                        _pluginMetadata[metadata.Id] = metadata;

                        PluginLoaded?.Invoke(this, new PluginLoadedEventArgs 
                        { 
                            Plugin = plugin, 
                            Metadata = metadata 
                        });

                        AutoLog($"Plugin cargado: {metadata.Name} v{metadata.Version}");
                        return true;
                    }
                    catch (Exception initEx)
                    {
                        AutoLog($"Error inicializando plugin {metadata.Name}: {initEx.Message}");
                        PluginError?.Invoke(this, new PluginErrorEventArgs 
                        { 
                            PluginId = metadata.Id, 
                            Error = initEx.Message 
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                AutoLog($"Error cargando plugin {pluginPath}: {ex.Message}");
                PluginError?.Invoke(this, new PluginErrorEventArgs { Error = ex.Message });
            }

            return false;
        }

        /// <summary>
        /// Descarga un plugin específico
        /// </summary>
        public async Task<bool> UnloadPluginAsync(string pluginId)
        {
            if (!_loadedPlugins.TryGetValue(pluginId, out var plugin))
                return false;

            try
            {
                var metadata = _pluginMetadata[pluginId];
                
                // Detener plugin
                await plugin.ShutdownAsync();
                
                // Remover de colecciones
                _loadedPlugins.Remove(pluginId);
                _pluginMetadata.Remove(pluginId);

                PluginUnloaded?.Invoke(this, new PluginUnloadedEventArgs 
                { 
                    PluginId = pluginId, 
                    Metadata = metadata 
                });

                AutoLog($"Plugin descargado: {metadata.Name}");
                return true;
            }
            catch (Exception ex)
            {
                AutoLog($"Error descargando plugin {pluginId}: {ex.Message}");
                PluginError?.Invoke(this, new PluginErrorEventArgs 
                { 
                    PluginId = pluginId, 
                    Error = ex.Message 
                });
                return false;
            }
        }

        /// <summary>
        /// Ejecuta comando en todos los plugins cargados
        /// </summary>
        public async Task<object> ExecuteCommandAsync(string command, params object[] parameters)
        {
            var results = new List<object>();

            foreach (var kvp in _loadedPlugins)
            {
                try
                {
                    var result = await kvp.Value.ExecuteCommandAsync(command, parameters);
                    if (result != null)
                    {
                        results.Add(result);
                    }
                }
                catch (Exception ex)
                {
                    AutoLog($"Error ejecutando comando '{command}' en plugin {kvp.Key}: {ex.Message}");
                    PluginError?.Invoke(this, new PluginErrorEventArgs 
                    { 
                        PluginId = kvp.Key, 
                        Error = ex.Message,
                        Command = command
                    });
                }
            }

            return results.Count > 0 ? results : null;
        }

        /// <summary>
        /// Ejecuta comando en plugin específico
        /// </summary>
        public async Task<object> ExecuteCommandAsync(string pluginId, string command, params object[] parameters)
        {
            if (!_loadedPlugins.TryGetValue(pluginId, out var plugin))
                return null;

            try
            {
                return await plugin.ExecuteCommandAsync(command, parameters);
            }
            catch (Exception ex)
            {
                AutoLog($"Error ejecutando comando '{command}' en plugin {pluginId}: {ex.Message}");
                PluginError?.Invoke(this, new PluginErrorEventArgs 
                { 
                    PluginId = pluginId, 
                    Error = ex.Message,
                    Command = command
                });
                return null;
            }
        }

        /// <summary>
        /// Obtiene plugins por tipo
        /// </summary>
        public IEnumerable<IPlugin> GetPluginsByType(PluginType type)
        {
            return _loadedPlugins.Values
                .Where(p => p.GetMetadata().Type == type);
        }

        /// <summary>
        /// Instala plugin desde archivo
        /// </summary>
        public async Task<bool> InstallPluginAsync(string sourcePath)
        {
            try
            {
                if (!File.Exists(sourcePath))
                {
                    AutoLog($"Archivo no encontrado: {sourcePath}");
                    return false;
                }

                var fileName = Path.GetFileName(sourcePath);
                var destPath = Path.Combine(_pluginsDirectory, fileName);

                // Verificar si ya existe
                if (File.Exists(destPath))
                {
                    AutoLog($"Plugin ya existe: {fileName}");
                    return false;
                }

                // Copiar archivo
                File.Copy(sourcePath, destPath);

                // Cargar plugin
                return await LoadPluginAsync(destPath);
            }
            catch (Exception ex)
            {
                AutoLog($"Error instalando plugin: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Desinstala plugin
        /// </summary>
        public async Task<bool> UninstallPluginAsync(string pluginId)
        {
            try
            {
                if (!_pluginMetadata.TryGetValue(pluginId, out var metadata))
                    return false;

                // Descargar plugin primero
                await UnloadPluginAsync(pluginId);

                // Mover archivo a directorio de desactivados
                var sourcePath = Path.Combine(_pluginsDirectory, metadata.FileName);
                var disabledPath = Path.Combine(_pluginsDirectory, "Disabled", metadata.FileName);

                if (File.Exists(sourcePath))
                {
                    File.Move(sourcePath, disabledPath);
                }

                AutoLog($"Plugin desinstalado: {metadata.Name}");
                return true;
            }
            catch (Exception ex)
            {
                AutoLog($"Error desinstalando plugin {pluginId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Valida metadata del plugin
        /// </summary>
        private bool ValidatePluginMetadata(PluginMetadata metadata)
        {
            if (metadata == null) return false;
            if (string.IsNullOrWhiteSpace(metadata.Id)) return false;
            if (string.IsNullOrWhiteSpace(metadata.Name)) return false;
            if (string.IsNullOrWhiteSpace(metadata.Version)) return false;
            if (string.IsNullOrWhiteSpace(metadata.Author)) return false;
            if (metadata.Type == PluginType.Unknown) return false;

            return true;
        }

        /// <summary>
        /// Verifica dependencias del plugin
        /// </summary>
        private async Task<bool> CheckDependencies(PluginMetadata metadata)
        {
            if (metadata.Dependencies == null || !metadata.Dependencies.Any())
                return true;

            foreach (var dependency in metadata.Dependencies)
            {
                // Verificar si la dependencia está cargada
                if (!_loadedPlugins.ContainsKey(dependency))
                {
                    // Verificar si la dependencia está disponible en el sistema
                    if (!await CheckSystemDependency(dependency))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Verifica dependencia del sistema
        /// </summary>
        private async Task<bool> CheckSystemDependency(string dependency)
        {
            // Implementar verificación de dependencias del sistema
            // Por ejemplo: .NET Framework, librerías específicas, etc.
            
            await Task.Delay(10); // Simular verificación
            
            return dependency switch
            {
                "System.Speech" => true, // Verificar si System.Speech está disponible
                "System.Reactive" => true, // Verificar si System.Reactive está disponible
                "CUDA" => false, // Simular que CUDA no está disponible
                _ => true // Asumir disponible por defecto
            };
        }

        /// <summary>
        /// Obtiene estadísticas de plugins
        /// </summary>
        public PluginStatistics GetStatistics()
        {
            return new PluginStatistics
            {
                TotalPlugins = _pluginMetadata.Count,
                LoadedPlugins = _loadedPlugins.Count,
                ActivePlugins = _loadedPlugins.Values.Count(p => p.GetMetadata().IsEnabled),
                PluginsByType = _loadedPlugins.Values
                    .GroupBy(p => p.GetMetadata().Type)
                    .ToDictionary(g => g.Key, g => g.Count()),
                LastUpdated = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Recarga todos los plugins
        /// </summary>
        public async Task ReloadAllPluginsAsync()
        {
            // Descargar todos los plugins
            var pluginIds = _loadedPlugins.Keys.ToList();
            foreach (var pluginId in pluginIds)
            {
                await UnloadPluginAsync(pluginId);
            }

            // Limpiar colecciones
            _loadedPlugins.Clear();
            _pluginMetadata.Clear();

            // Recargar todos
            await LoadAllPlugins();
        }

        /// <summary>
        /// Habilita/deshabilita plugin
        /// </summary>
        public async Task<bool> SetPluginEnabledAsync(string pluginId, bool enabled)
        {
            if (!_loadedPlugins.TryGetValue(pluginId, out var plugin))
                return false;

            try
            {
                var metadata = _pluginMetadata[pluginId];
                metadata.IsEnabled = enabled;

                if (enabled)
                {
                    await plugin.InitializeAsync(_mainForm);
                }
                else
                {
                    await plugin.ShutdownAsync();
                }

                AutoLog($"Plugin {metadata.Name} {(enabled ? "habilitado" : "deshabilitado")}");
                return true;
            }
            catch (Exception ex)
            {
                AutoLog($"Error {(enabled ? "habilitando" : "deshabilitando")} plugin {pluginId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Escribe log
        /// </summary>
        private void AutoLog(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[PluginManager] {message}");
            // También podría escribir a archivo o notificar a MainForm
        }

        /// <summary>
        /// Libera recursos
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            // Descargar todos los plugins
            var pluginIds = _loadedPlugins.Keys.ToList();
            foreach (var pluginId in pluginIds)
            {
                try
                {
                    UnloadPluginAsync(pluginId).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    AutoLog($"Error descargando plugin {pluginId}: {ex.Message}");
                }
            }
        }
    }

    #region Interfaces y Modelos

    /// <summary>
    /// Interfaz base para todos los plugins
    /// </summary>
    public interface IPlugin
    {
        PluginMetadata GetMetadata();
        Task InitializeAsync(MainForm mainForm);
        Task ShutdownAsync();
        Task<object> ExecuteCommandAsync(string command, params object[] parameters);
    }

    /// <summary>
    /// Metadata del plugin
    /// </summary>
    public class PluginMetadata
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public PluginType Type { get; set; }
        public string[] Dependencies { get; set; }
        public string FileName { get; set; }
        public bool IsEnabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Tipos de plugins
    /// </summary>
    public enum PluginType
    {
        Unknown,
        SearchProvider,
        DownloadHandler,
        UIExtension,
        Notification,
        Metrics,
        AIProcessor,
        FileProcessor,
        NetworkExtension
    }

    /// <summary>
    /// Estadísticas de plugins
    /// </summary>
    public class PluginStatistics
    {
        public int TotalPlugins { get; set; }
        public int LoadedPlugins { get; set; }
        public int ActivePlugins { get; set; }
        public Dictionary<PluginType, int> PluginsByType { get; set; } = new Dictionary<PluginType, int>();
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Argumentos de eventos
    /// </summary>
    public class PluginLoadedEventArgs : EventArgs
    {
        public IPlugin Plugin { get; set; }
        public PluginMetadata Metadata { get; set; }
    }

    public class PluginUnloadedEventArgs : EventArgs
    {
        public string PluginId { get; set; }
        public PluginMetadata Metadata { get; set; }
    }

    public class PluginErrorEventArgs : EventArgs
    {
        public string PluginId { get; set; }
        public string Error { get; set; }
        public string Command { get; set; }
    }

    #endregion
}
