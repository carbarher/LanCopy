using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SlskDown
{
    public interface ISlskDownPlugin
    {
        string Name { get; }
        string Version { get; }
        string Description { get; }
        
        void Initialize(IPluginHost host);
        void OnSearchResult(object result);
        void OnDownloadComplete(string username, string filename, string localPath);
        void OnPrivateMessage(string username, string message, bool isOutgoing);
        void OnRoomMessage(string room, string username, string message);
        void OnConnectionStateChanged(bool isConnected);
    }
    
    public interface IPluginHost
    {
        void Log(string message);
        void SendPrivateMessage(string username, string message);
        void SendRoomMessage(string room, string message);
        void AddToDownloads(string username, string filename);
        string GetConfig(string key, string defaultValue = "");
        void SetConfig(string key, string value);
    }
    
    public class PluginManager
    {
        private List<ISlskDownPlugin> loadedPlugins = new List<ISlskDownPlugin>();
        private IPluginHost pluginHost;
        private string pluginsDir;
        private Action<string> logAction;
        
        public PluginManager(string pluginsDirectory, IPluginHost host, Action<string> logger)
        {
            pluginsDir = pluginsDirectory;
            pluginHost = host;
            logAction = logger;
            
            if (!Directory.Exists(pluginsDir))
            {
                Directory.CreateDirectory(pluginsDir);
            }
        }
        
        public void LoadPlugins()
        {
            try
            {
                var dllFiles = Directory.GetFiles(pluginsDir, "*.dll");
                
                foreach (var dllFile in dllFiles)
                {
                    try
                    {
                        var assembly = Assembly.LoadFrom(dllFile);
                        var pluginTypes = assembly.GetTypes()
                            .Where(t => typeof(ISlskDownPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
                        
                        foreach (var type in pluginTypes)
                        {
                            var plugin = (ISlskDownPlugin)Activator.CreateInstance(type);
                            plugin.Initialize(pluginHost);
                            loadedPlugins.Add(plugin);
                            logAction?.Invoke($"✅ Plugin cargado: {plugin.Name} v{plugin.Version}");
                        }
                    }
                    catch (Exception ex)
                    {
                        logAction?.Invoke($"❌ Error cargando plugin {Path.GetFileName(dllFile)}: {ex.Message}");
                    }
                }
                
                if (loadedPlugins.Count == 0)
                {
                    logAction?.Invoke($"ℹ️ No se encontraron plugins en {pluginsDir}");
                }
                else
                {
                    logAction?.Invoke($"🔌 {loadedPlugins.Count} plugin(s) cargado(s)");
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error en LoadPlugins: {ex.Message}");
            }
        }
        
        public void OnSearchResult(object result)
        {
            foreach (var plugin in loadedPlugins)
            {
                try
                {
                    plugin.OnSearchResult(result);
                }
                catch (Exception ex)
                {
                    logAction?.Invoke($"❌ Error en plugin {plugin.Name}.OnSearchResult: {ex.Message}");
                }
            }
        }
        
        public void OnDownloadComplete(string username, string filename, string localPath)
        {
            foreach (var plugin in loadedPlugins)
            {
                try
                {
                    plugin.OnDownloadComplete(username, filename, localPath);
                }
                catch (Exception ex)
                {
                    logAction?.Invoke($"❌ Error en plugin {plugin.Name}.OnDownloadComplete: {ex.Message}");
                }
            }
        }
        
        public void OnPrivateMessage(string username, string message, bool isOutgoing)
        {
            foreach (var plugin in loadedPlugins)
            {
                try
                {
                    plugin.OnPrivateMessage(username, message, isOutgoing);
                }
                catch (Exception ex)
                {
                    logAction?.Invoke($"❌ Error en plugin {plugin.Name}.OnPrivateMessage: {ex.Message}");
                }
            }
        }
        
        public void OnRoomMessage(string room, string username, string message)
        {
            foreach (var plugin in loadedPlugins)
            {
                try
                {
                    plugin.OnRoomMessage(room, username, message);
                }
                catch (Exception ex)
                {
                    logAction?.Invoke($"❌ Error en plugin {plugin.Name}.OnRoomMessage: {ex.Message}");
                }
            }
        }
        
        public void OnConnectionStateChanged(bool isConnected)
        {
            foreach (var plugin in loadedPlugins)
            {
                try
                {
                    plugin.OnConnectionStateChanged(isConnected);
                }
                catch (Exception ex)
                {
                    logAction?.Invoke($"❌ Error en plugin {plugin.Name}.OnConnectionStateChanged: {ex.Message}");
                }
            }
        }
        
        public List<ISlskDownPlugin> GetLoadedPlugins()
        {
            return new List<ISlskDownPlugin>(loadedPlugins);
        }
        
        public void UnloadAllPlugins()
        {
            loadedPlugins.Clear();
            logAction?.Invoke("🔌 Todos los plugins descargados");
        }
    }
    
    public class AutoReplyPlugin : ISlskDownPlugin
    {
        public string Name => "Auto-Reply";
        public string Version => "1.0.0";
        public string Description => "Responde automáticamente a mensajes privados cuando estás ausente";
        
        private IPluginHost host;
        private bool enabled = false;
        private string autoReplyMessage = "Gracias por tu mensaje. Estoy ausente en este momento, responderé pronto.";
        private HashSet<string> repliedUsers = new HashSet<string>();
        
        public void Initialize(IPluginHost pluginHost)
        {
            host = pluginHost;
            
            enabled = host.GetConfig("AutoReply.Enabled", "false") == "true";
            autoReplyMessage = host.GetConfig("AutoReply.Message", autoReplyMessage);
            
            host.Log($"[Auto-Reply] Plugin inicializado (Enabled: {enabled})");
        }
        
        public void OnPrivateMessage(string username, string message, bool isOutgoing)
        {
            if (!enabled || isOutgoing || repliedUsers.Contains(username))
            {
                return;
            }
            
            if (!string.IsNullOrEmpty(autoReplyMessage))
            {
                host.SendPrivateMessage(username, autoReplyMessage);
                repliedUsers.Add(username);
                host.Log($"[Auto-Reply] Respuesta automática enviada a {username}");
            }
        }
        
        public void SetEnabled(bool enable)
        {
            enabled = enable;
            host.SetConfig("AutoReply.Enabled", enable.ToString().ToLower());
            
            if (!enable)
            {
                repliedUsers.Clear();
            }
        }
        
        public void SetMessage(string message)
        {
            autoReplyMessage = message;
            host.SetConfig("AutoReply.Message", message);
        }
        
        public void OnSearchResult(object result) { }
        public void OnDownloadComplete(string username, string filename, string localPath) { }
        public void OnRoomMessage(string room, string username, string message) { }
        public void OnConnectionStateChanged(bool isConnected)
        {
            if (isConnected)
            {
                repliedUsers.Clear();
            }
        }
    }
}
