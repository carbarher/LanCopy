using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace SlskDown
{
    public class BrowsedFile
    {
        public string Filename { get; set; }
        public long Size { get; set; }
        public string Extension { get; set; }
        public int Bitrate { get; set; }
        public int Duration { get; set; }
    }
    
    public class BuddyBrowseCache
    {
        public string Username { get; set; }
        public DateTime LastBrowsed { get; set; }
        public List<BrowsedFile> Files { get; set; } = new List<BrowsedFile>();
        public int TotalFiles => Files.Count;
        public long TotalSize => Files.Sum(f => f.Size);
    }
    
    public class BuddyAutoBrowseSystem
    {
        private Dictionary<string, BuddyBrowseCache> browseCache = new Dictionary<string, BuddyBrowseCache>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> autoBrowseEnabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, DateTime> lastBrowsed = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private const int CACHE_HOURS = 24;
        private string dataDir;
        private Action<string> logAction;
        private Func<string, Task<List<BrowsedFile>>> browseUserFunc;
        
        public BuddyAutoBrowseSystem(string dataDirectory, Action<string> logger, Func<string, Task<List<BrowsedFile>>> browseFunc)
        {
            dataDir = dataDirectory;
            logAction = logger;
            browseUserFunc = browseFunc;
            LoadCache();
        }
        
        public void EnableAutoBrowse(string username, bool enable)
        {
            if (enable)
            {
                autoBrowseEnabled.Add(username);
                logAction?.Invoke($"Auto-browse habilitado para: {username}");
            }
            else
            {
                autoBrowseEnabled.Remove(username);
                logAction?.Invoke($"Auto-browse deshabilitado para: {username}");
            }
            
            SaveCache();
        }
        
        public bool IsAutoBrowseEnabled(string username)
        {
            return autoBrowseEnabled.Contains(username);
        }
        
        public async Task OnBuddyOnline(string username)
        {
            if (!autoBrowseEnabled.Contains(username))
                return;
            
            // Solo browsear si no se ha hecho en las últimas 24h
            if (lastBrowsed.TryGetValue(username, out DateTime last))
            {
                if ((DateTime.Now - last).TotalHours < CACHE_HOURS)
                {
                    logAction?.Invoke($"{username} ya fue browseado recientemente (hace {(DateTime.Now - last).TotalHours:F1}h)");
                    return;
                }
            }
            
            try
            {
                logAction?.Invoke($"Auto-browsing buddy: {username}");
                
                var files = await browseUserFunc(username);
                
                if (files != null && files.Count > 0)
                {
                    browseCache[username] = new BuddyBrowseCache
                    {
                        Username = username,
                        LastBrowsed = DateTime.Now,
                        Files = files
                    };
                    
                    lastBrowsed[username] = DateTime.Now;
                    
                    logAction?.Invoke($"Cached {files.Count} files from {username} ({FormatSize(files.Sum(f => f.Size))})");
                    
                    SaveCache();
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error auto-browsing {username}: {ex.Message}");
            }
        }
        
        public BuddyBrowseCache GetCachedFiles(string username)
        {
            if (browseCache.TryGetValue(username, out var cache))
            {
                // Verificar si el caché no está muy viejo
                if ((DateTime.Now - cache.LastBrowsed).TotalHours < CACHE_HOURS * 7) // 7 días máximo
                {
                    return cache;
                }
            }
            
            return null;
        }
        
        public List<BrowsedFile> SearchInCache(string username, string query)
        {
            var cache = GetCachedFiles(username);
            if (cache == null) return new List<BrowsedFile>();
            
            return cache.Files.Where(f => 
                f.Filename.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            ).ToList();
        }
        
        public Dictionary<string, BuddyBrowseCache> GetAllCaches()
        {
            return new Dictionary<string, BuddyBrowseCache>(browseCache);
        }
        
        public void ClearCache(string username)
        {
            browseCache.Remove(username);
            lastBrowsed.Remove(username);
            SaveCache();
            logAction?.Invoke($"🗑️ Caché eliminado para: {username}");
        }
        
        public void ClearOldCaches()
        {
            var toRemove = browseCache
                .Where(kvp => (DateTime.Now - kvp.Value.LastBrowsed).TotalDays > 30)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var username in toRemove)
            {
                browseCache.Remove(username);
                lastBrowsed.Remove(username);
            }
            
            if (toRemove.Count > 0)
            {
                SaveCache();
                logAction?.Invoke($"🗑️ {toRemove.Count} cachés antiguos eliminados");
            }
        }
        
        private string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
        
        private void LoadCache()
        {
            try
            {
                string path = Path.Combine(dataDir, "buddy_browse_cache.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var data = JsonSerializer.Deserialize<BrowseCacheData>(json);
                    
                    if (data != null)
                    {
                        browseCache = data.BrowseCache?.ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value,
                            StringComparer.OrdinalIgnoreCase
                        ) ?? new Dictionary<string, BuddyBrowseCache>(StringComparer.OrdinalIgnoreCase);
                        
                        autoBrowseEnabled = new HashSet<string>(
                            data.AutoBrowseEnabled ?? new List<string>(),
                            StringComparer.OrdinalIgnoreCase
                        );
                        
                        lastBrowsed = data.LastBrowsed?.ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value,
                            StringComparer.OrdinalIgnoreCase
                        ) ?? new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"⚠️ Error cargando caché de browse: {ex.Message}");
            }
        }
        
        private void SaveCache()
        {
            try
            {
                var data = new BrowseCacheData
                {
                    BrowseCache = browseCache,
                    AutoBrowseEnabled = autoBrowseEnabled.ToList(),
                    LastBrowsed = lastBrowsed
                };
                
                string path = Path.Combine(dataDir, "buddy_browse_cache.json");
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"⚠️ Error guardando caché de browse: {ex.Message}");
            }
        }
        
        private class BrowseCacheData
        {
            public Dictionary<string, BuddyBrowseCache> BrowseCache { get; set; }
            public List<string> AutoBrowseEnabled { get; set; }
            public Dictionary<string, DateTime> LastBrowsed { get; set; }
        }
    }
}
