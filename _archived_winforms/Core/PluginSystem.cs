using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SlskDown.Core
{
    // ═══════════════════════════════════════════════════════════════
    // SISTEMA DE PLUGINS
    // ═══════════════════════════════════════════════════════════════
    
    public interface ISlskPlugin
    {
        string Name { get; }
        string Version { get; }
        string Description { get; }
        string Author { get; }
        
        void Initialize(IPluginHost host);
        void OnSearchResults(List<object> results);
        void OnDownloadComplete(string filename, string username, bool success);
        void OnMessageReceived(string username, string message);
        void OnConnectionStateChanged(bool connected);
        void Shutdown();
    }
    
    public interface IPluginHost
    {
        void Log(string message);
        void ShowNotification(string title, string message);
        EventBusSystem EventBus { get; }
        Dictionary<string, object> GetConfig();
        void SetConfig(string key, object value);
    }
    
    public class PluginManager : IPluginHost
    {
        private readonly List<ISlskPlugin> loadedPlugins = new List<ISlskPlugin>();
        private readonly EventBusSystem eventBus;
        private readonly Action<string> logger;
        private readonly Action<string, string> notifier;
        private readonly Dictionary<string, object> config = new Dictionary<string, object>();
        
        public EventBusSystem EventBus => eventBus;
        
        public PluginManager(EventBusSystem eventBus, Action<string> logger, Action<string, string> notifier)
        {
            this.eventBus = eventBus;
            this.logger = logger;
            this.notifier = notifier;
        }
        
        public void LoadPlugins(string pluginsDirectory)
        {
            if (!Directory.Exists(pluginsDirectory))
            {
                Directory.CreateDirectory(pluginsDirectory);
                Log($"📁 Directorio de plugins creado: {pluginsDirectory}");
                return;
            }
            
            var dllFiles = Directory.GetFiles(pluginsDirectory, "*.dll");
            
            foreach (var dll in dllFiles)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dll);
                    var pluginTypes = assembly.GetTypes()
                        .Where(t => typeof(ISlskPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
                    
                    foreach (var type in pluginTypes)
                    {
                        var plugin = (ISlskPlugin)Activator.CreateInstance(type);
                        plugin.Initialize(this);
                        loadedPlugins.Add(plugin);
                        Log($"Plugin cargado: {plugin.Name} v{plugin.Version} por {plugin.Author}");
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error cargando plugin {Path.GetFileName(dll)}: {ex.Message}");
                }
            }
            
            if (loadedPlugins.Count == 0)
            {
                Log($"No se encontraron plugins en {pluginsDirectory}");
            }
        }
        
        public void NotifySearchResults(List<object> results)
        {
            foreach (var plugin in loadedPlugins)
            {
                try
                {
                    plugin.OnSearchResults(results);
                }
                catch (Exception ex)
                {
                    Log($"Error en plugin {plugin.Name}.OnSearchResults: {ex.Message}");
                }
            }
        }
        
        public void NotifyDownloadComplete(string filename, string username, bool success)
        {
            foreach (var plugin in loadedPlugins)
            {
                try
                {
                    plugin.OnDownloadComplete(filename, username, success);
                }
                catch (Exception ex)
                {
                    Log($"Error en plugin {plugin.Name}.OnDownloadComplete: {ex.Message}");
                }
            }
        }
        
        public void NotifyMessageReceived(string username, string message)
        {
            foreach (var plugin in loadedPlugins)
            {
                try
                {
                    plugin.OnMessageReceived(username, message);
                }
                catch (Exception ex)
                {
                    Log($"Error en plugin {plugin.Name}.OnMessageReceived: {ex.Message}");
                }
            }
        }
        
        public void NotifyConnectionStateChanged(bool connected)
        {
            foreach (var plugin in loadedPlugins)
            {
                try
                {
                    plugin.OnConnectionStateChanged(connected);
                }
                catch (Exception ex)
                {
                    Log($"Error en plugin {plugin.Name}.OnConnectionStateChanged: {ex.Message}");
                }
            }
        }
        
        public void UnloadAll()
        {
            foreach (var plugin in loadedPlugins)
            {
                try
                {
                    plugin.Shutdown();
                    Log($"Plugin descargado: {plugin.Name}");
                }
                catch (Exception ex)
                {
                    Log($"Error descargando plugin {plugin.Name}: {ex.Message}");
                }
            }
            
            loadedPlugins.Clear();
        }
        
        public List<ISlskPlugin> GetLoadedPlugins()
        {
            return new List<ISlskPlugin>(loadedPlugins);
        }
        
        // IPluginHost implementation
        public void Log(string message)
        {
            logger?.Invoke(message);
        }
        
        public void ShowNotification(string title, string message)
        {
            notifier?.Invoke(title, message);
        }
        
        public Dictionary<string, object> GetConfig()
        {
            return new Dictionary<string, object>(config);
        }
        
        public void SetConfig(string key, object value)
        {
            config[key] = value;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // PLUGIN DE EJEMPLO: AUTO-RESPONDER
    // ═══════════════════════════════════════════════════════════════
    
    public class AutoResponderPlugin : ISlskPlugin
    {
        public string Name => "Auto Responder";
        public string Version => "1.0.0";
        public string Description => "Responde automáticamente a mensajes privados";
        public string Author => "SlskDown Team";
        
        private IPluginHost host;
        private Dictionary<string, string> autoResponses = new Dictionary<string, string>();
        
        public void Initialize(IPluginHost host)
        {
            this.host = host;
            
            // Cargar respuestas automáticas desde config
            autoResponses["hola"] = "¡Hola! Soy un bot. El usuario volverá pronto.";
            autoResponses["hello"] = "Hello! I'm a bot. The user will be back soon.";
            autoResponses["hi"] = "Hi! This is an automated response.";
            
            host.Log($"{Name} inicializado con {autoResponses.Count} respuestas");
        }
        
        public void OnSearchResults(List<object> results)
        {
            // No hacer nada
        }
        
        public void OnDownloadComplete(string filename, string username, bool success)
        {
            // No hacer nada
        }
        
        public void OnMessageReceived(string username, string message)
        {
            var lowerMessage = message.ToLower().Trim();
            
            foreach (var kvp in autoResponses)
            {
                if (lowerMessage.Contains(kvp.Key))
                {
                    host.Log($"Auto-respuesta enviada a {username}: {kvp.Value}");
                    // Aquí se enviaría el mensaje (requiere acceso al cliente)
                    break;
                }
            }
        }
        
        public void OnConnectionStateChanged(bool connected)
        {
            host.Log($"Conexión {(connected ? "establecida" : "perdida")}");
        }
        
        public void Shutdown()
        {
            host.Log($"{Name} apagado");
        }
    }
}
