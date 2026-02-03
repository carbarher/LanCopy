using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    // ═══════════════════════════════════════════════════════════════
    // FASE 4: CARACTERÍSTICAS OCULTAS Y AVANZADAS DE NICOTINE+
    // ═══════════════════════════════════════════════════════════════
    
    // ═══════════════════════════════════════════════════════════════
    // 1. TIMEOUTS GRANULARES
    // ═══════════════════════════════════════════════════════════════
    
    public class ProtocolTimeouts
    {
        public int ConnectionTimeout { get; set; } = 30;
        public int LoginTimeout { get; set; } = 15;
        public int SearchTimeout { get; set; } = 30;
        public int DownloadTimeout { get; set; } = 300;
        public int UploadTimeout { get; set; } = 300;
        public int PeerTimeout { get; set; } = 60;
        public int FileListTimeout { get; set; } = 120;
        
        public TimeSpan GetTimeout(string operation)
        {
            return operation.ToLower() switch
            {
                "connection" => TimeSpan.FromSeconds(ConnectionTimeout),
                "login" => TimeSpan.FromSeconds(LoginTimeout),
                "search" => TimeSpan.FromSeconds(SearchTimeout),
                "download" => TimeSpan.FromSeconds(DownloadTimeout),
                "upload" => TimeSpan.FromSeconds(UploadTimeout),
                "peer" => TimeSpan.FromSeconds(PeerTimeout),
                "filelist" => TimeSpan.FromSeconds(FileListTimeout),
                _ => TimeSpan.FromSeconds(30)
            };
        }
        
        public void SaveToFile(string path)
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        
        public static ProtocolTimeouts LoadFromFile(string path)
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<ProtocolTimeouts>(json) ?? new ProtocolTimeouts();
            }
            return new ProtocolTimeouts();
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 2. SISTEMA DE PRIORIDADES
    // ═══════════════════════════════════════════════════════════════
    
    public enum TransferPriority
    {
        Paused = -1,
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }
    
    public class PriorityManager
    {
        private Dictionary<string, TransferPriority> filePriorities = new Dictionary<string, TransferPriority>();
        private Action onQueueChanged;
        
        public PriorityManager(Action onQueueChanged = null)
        {
            this.onQueueChanged = onQueueChanged;
        }
        
        public void SetPriority(string filename, TransferPriority priority)
        {
            filePriorities[filename] = priority;
            ReorderQueue();
        }
        
        public TransferPriority GetPriority(string filename)
        {
            return filePriorities.ContainsKey(filename) ? filePriorities[filename] : TransferPriority.Normal;
        }
        
        public List<string> GetOrderedQueue()
        {
            return filePriorities
                .Where(kvp => kvp.Value != TransferPriority.Paused)
                .OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => kvp.Key)
                .Select(kvp => kvp.Key)
                .ToList();
        }
        
        private void ReorderQueue()
        {
            onQueueChanged?.Invoke();
        }
        
        public void Clear()
        {
            filePriorities.Clear();
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 3. LOGGER DE PROTOCOLO
    // ═══════════════════════════════════════════════════════════════
    
    public class ProtocolLogger
    {
        private bool enableLogging = false;
        private StreamWriter logWriter;
        private readonly object lockObj = new object();
        
        public void LogPacket(string direction, int messageCode, byte[] data)
        {
            if (!enableLogging || logWriter == null) return;
            
            lock (lockObj)
            {
                try
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var hex = data.Length > 100 
                        ? BitConverter.ToString(data, 0, 100).Replace("-", " ") + "..." 
                        : BitConverter.ToString(data).Replace("-", " ");
                    var message = $"[{timestamp}] {direction} Code:{messageCode} Size:{data.Length} Data:{hex}";
                    
                    logWriter.WriteLine(message);
                    logWriter.Flush();
                }
                catch { }
            }
        }
        
        public void EnableLogging(string logFile)
        {
            lock (lockObj)
            {
                enableLogging = true;
                logWriter = new StreamWriter(logFile, append: true);
            }
        }
        
        public void DisableLogging()
        {
            lock (lockObj)
            {
                enableLogging = false;
                logWriter?.Close();
                logWriter = null;
            }
        }
        
        public bool IsEnabled => enableLogging;
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 4. MONITOR DE SALUD DE RED
    // ═══════════════════════════════════════════════════════════════
    
    public enum NetworkStatus { Excellent, Good, Fair, Poor }
    
    public class NetworkHealth
    {
        public double PacketLossRate { get; set; }
        public double AverageLatency { get; set; }
        public NetworkStatus Status { get; set; }
        public int PacketsSent { get; set; }
        public int PacketsReceived { get; set; }
        public int PacketsLost { get; set; }
    }
    
    public class NetworkHealthMonitor
    {
        private int packetsSent = 0;
        private int packetsReceived = 0;
        private int packetsLost = 0;
        private List<double> latencies = new List<double>();
        private readonly object lockObj = new object();
        
        public void RecordPacket(bool sent, bool received, double latency = 0)
        {
            lock (lockObj)
            {
                if (sent) packetsSent++;
                if (received) packetsReceived++;
                else if (sent) packetsLost++;
                
                if (latency > 0)
                {
                    latencies.Add(latency);
                    if (latencies.Count > 100)
                        latencies.RemoveAt(0);
                }
            }
        }
        
        public NetworkHealth GetHealth()
        {
            lock (lockObj)
            {
                var packetLossRate = packetsSent > 0 
                    ? (double)packetsLost / packetsSent * 100 
                    : 0;
                
                var avgLatency = latencies.Count > 0 
                    ? latencies.Average() 
                    : 0;
                
                return new NetworkHealth
                {
                    PacketLossRate = packetLossRate,
                    AverageLatency = avgLatency,
                    Status = DetermineStatus(packetLossRate, avgLatency),
                    PacketsSent = packetsSent,
                    PacketsReceived = packetsReceived,
                    PacketsLost = packetsLost
                };
            }
        }
        
        private NetworkStatus DetermineStatus(double lossRate, double latency)
        {
            if (lossRate > 10 || latency > 1000)
                return NetworkStatus.Poor;
            if (lossRate > 5 || latency > 500)
                return NetworkStatus.Fair;
            if (lossRate > 2 || latency > 200)
                return NetworkStatus.Good;
            return NetworkStatus.Excellent;
        }
        
        public void Reset()
        {
            lock (lockObj)
            {
                packetsSent = 0;
                packetsReceived = 0;
                packetsLost = 0;
                latencies.Clear();
            }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 5. FILTROS DE BÚSQUEDA GUARDADOS
    // ═══════════════════════════════════════════════════════════════
    
    public class SavedSearchFilter
    {
        public string Name { get; set; }
        public string Query { get; set; }
        public List<string> ExcludeWords { get; set; } = new List<string>();
        public int MinBitrate { get; set; }
        public int MaxBitrate { get; set; }
        public long MinSize { get; set; }
        public long MaxSize { get; set; }
        public List<string> AllowedExtensions { get; set; } = new List<string>();
        public bool OnlyFreeSlots { get; set; }
        public int MinSpeed { get; set; }
    }
    
    public class FilterManager
    {
        private Dictionary<string, SavedSearchFilter> savedFilters = new Dictionary<string, SavedSearchFilter>();
        private readonly string filtersFile;
        
        public FilterManager(string filtersFile)
        {
            this.filtersFile = filtersFile;
            LoadFilters();
        }
        
        public void SaveFilter(string name, SavedSearchFilter filter)
        {
            filter.Name = name;
            savedFilters[name] = filter;
            SaveToFile();
        }
        
        public SavedSearchFilter GetFilter(string name)
        {
            return savedFilters.ContainsKey(name) ? savedFilters[name] : null;
        }
        
        public List<string> GetFilterNames()
        {
            return savedFilters.Keys.OrderBy(k => k).ToList();
        }
        
        public void DeleteFilter(string name)
        {
            savedFilters.Remove(name);
            SaveToFile();
        }
        
        private void SaveToFile()
        {
            try
            {
                var json = JsonSerializer.Serialize(savedFilters, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filtersFile, json);
            }
            catch { }
        }
        
        private void LoadFilters()
        {
            try
            {
                if (File.Exists(filtersFile))
                {
                    var json = File.ReadAllText(filtersFile);
                    savedFilters = JsonSerializer.Deserialize<Dictionary<string, SavedSearchFilter>>(json) 
                        ?? new Dictionary<string, SavedSearchFilter>();
                }
            }
            catch { }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 6. HISTORIAL DE BÚSQUEDAS CON AUTOCOMPLETADO
    // ═══════════════════════════════════════════════════════════════
    
    public class NicotineSearchHistoryEntry
    {
        public string Query { get; set; }
        public DateTime Timestamp { get; set; }
        public int ResultsCount { get; set; }
    }
    
    public class SearchHistory
    {
        private List<NicotineSearchHistoryEntry> history = new List<NicotineSearchHistoryEntry>();
        private const int MAX_HISTORY = 100;
        private readonly string historyFile;
        
        public SearchHistory(string historyFile)
        {
            this.historyFile = historyFile;
            LoadHistory();
        }
        
        public void AddSearch(string query, int resultsCount)
        {
            history.Insert(0, new NicotineSearchHistoryEntry
            {
                Query = query,
                Timestamp = DateTime.Now,
                ResultsCount = resultsCount
            });
            
            if (history.Count > MAX_HISTORY)
                history.RemoveAt(history.Count - 1);
            
            SaveHistory();
        }
        
        public List<string> GetSuggestions(string partialQuery)
        {
            if (string.IsNullOrWhiteSpace(partialQuery))
                return new List<string>();
            
            return history
                .Where(h => h.Query.StartsWith(partialQuery, StringComparison.OrdinalIgnoreCase))
                .Select(h => h.Query)
                .Distinct()
                .Take(10)
                .ToList();
        }
        
        public List<NicotineSearchHistoryEntry> GetRecentSearches(int count = 20)
        {
            return history.Take(count).ToList();
        }
        
        public void Clear()
        {
            history.Clear();
            SaveHistory();
        }
        
        private void SaveHistory()
        {
            try
            {
                var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(historyFile, json);
            }
            catch { }
        }
        
        private void LoadHistory()
        {
            try
            {
                if (File.Exists(historyFile))
                {
                    var json = File.ReadAllText(historyFile);
                    history = JsonSerializer.Deserialize<List<NicotineSearchHistoryEntry>>(json) 
                        ?? new List<NicotineSearchHistoryEntry>();
                }
            }
            catch { }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 7. LISTA DE IPS BLOQUEADAS
    // ═══════════════════════════════════════════════════════════════
    
    public class IPBlockList
    {
        private HashSet<string> blockedIPs = new HashSet<string>();
        private HashSet<string> blockedRanges = new HashSet<string>();
        private readonly string blockListFile;
        
        public IPBlockList(string blockListFile)
        {
            this.blockListFile = blockListFile;
            LoadBlockList();
        }
        
        public void BlockIP(string ip)
        {
            blockedIPs.Add(ip);
            SaveBlockList();
        }
        
        public void BlockRange(string range)
        {
            blockedRanges.Add(range);
            SaveBlockList();
        }
        
        public void UnblockIP(string ip)
        {
            blockedIPs.Remove(ip);
            SaveBlockList();
        }
        
        public bool IsBlocked(string ip)
        {
            if (blockedIPs.Contains(ip))
                return true;
            
            foreach (var range in blockedRanges)
            {
                if (IsInRange(ip, range))
                    return true;
            }
            
            return false;
        }
        
        private bool IsInRange(string ip, string range)
        {
            var parts = range.Split('/');
            if (parts.Length != 2) return false;
            
            var baseIP = parts[0];
            
            // Verificación simple: mismo prefijo
            var ipParts = ip.Split('.');
            var baseParts = baseIP.Split('.');
            
            if (ipParts.Length != 4 || baseParts.Length != 4)
                return false;
            
            // Comparar primeros 3 octetos para /24
            return ipParts[0] == baseParts[0] && 
                   ipParts[1] == baseParts[1] && 
                   ipParts[2] == baseParts[2];
        }
        
        public List<string> GetBlockedIPs() => blockedIPs.ToList();
        public List<string> GetBlockedRanges() => blockedRanges.ToList();
        
        private void SaveBlockList()
        {
            try
            {
                var data = new { IPs = blockedIPs.ToList(), Ranges = blockedRanges.ToList() };
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(blockListFile, json);
            }
            catch { }
        }
        
        private void LoadBlockList()
        {
            try
            {
                if (File.Exists(blockListFile))
                {
                    var json = File.ReadAllText(blockListFile);
                    var data = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
                    if (data != null)
                    {
                        if (data.ContainsKey("IPs"))
                            blockedIPs = new HashSet<string>(data["IPs"]);
                        if (data.ContainsKey("Ranges"))
                            blockedRanges = new HashSet<string>(data["Ranges"]);
                    }
                }
            }
            catch { }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 8. MODO PRIVADO / INVISIBLE
    // ═══════════════════════════════════════════════════════════════
    
    public class PrivacyMode
    {
        public bool InvisibleMode { get; set; } = false;
        public bool HideShares { get; set; } = false;
        public bool DisablePrivateMessages { get; set; } = false;
        public bool DisableRoomMessages { get; set; } = false;
        public bool OnlyAcceptFromFriends { get; set; } = false;
        
        private HashSet<string> friendsList = new HashSet<string>();
        
        public void AddFriend(string username)
        {
            friendsList.Add(username);
        }
        
        public void RemoveFriend(string username)
        {
            friendsList.Remove(username);
        }
        
        public bool IsFriend(string username)
        {
            return friendsList.Contains(username);
        }
        
        public bool ShouldAcceptConnection(string username)
        {
            if (InvisibleMode)
                return false;
            
            if (OnlyAcceptFromFriends)
                return IsFriend(username);
            
            return true;
        }
        
        public bool ShouldShowOnline()
        {
            return !InvisibleMode;
        }
        
        public bool ShouldAcceptMessage(string username)
        {
            if (DisablePrivateMessages)
                return false;
            
            if (OnlyAcceptFromFriends)
                return IsFriend(username);
            
            return true;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 9. EXCLUSIONES AUTOMÁTICAS DE ARCHIVOS
    // ═══════════════════════════════════════════════════════════════
    
    public class ShareExclusions
    {
        private List<string> excludedPatterns = new List<string>
        {
            "*.tmp", "*.temp", "*.cache", "*.bak",
            "Thumbs.db", ".DS_Store", "desktop.ini",
            "*.partial", "*.crdownload", "*.download"
        };
        
        private List<string> excludedFolders = new List<string>
        {
            "System Volume Information",
            "$RECYCLE.BIN", "$Recycle.Bin",
            "Windows", "Program Files", "Program Files (x86)",
            "__MACOSX", ".git", ".svn", ".hg",
            "node_modules", ".vscode", ".idea"
        };
        
        public void AddPattern(string pattern)
        {
            if (!excludedPatterns.Contains(pattern))
                excludedPatterns.Add(pattern);
        }
        
        public void AddFolder(string folder)
        {
            if (!excludedFolders.Contains(folder))
                excludedFolders.Add(folder);
        }
        
        public bool ShouldExclude(string path)
        {
            var filename = Path.GetFileName(path);
            var directory = Path.GetDirectoryName(path) ?? "";
            
            // Verificar patrones
            foreach (var pattern in excludedPatterns)
            {
                if (MatchesPattern(filename, pattern))
                    return true;
            }
            
            // Verificar carpetas
            foreach (var folder in excludedFolders)
            {
                if (directory.Contains(folder, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            
            return false;
        }
        
        private bool MatchesPattern(string filename, string pattern)
        {
            try
            {
                var regex = "^" + Regex.Escape(pattern)
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".") + "$";
                return Regex.IsMatch(filename, regex, RegexOptions.IgnoreCase);
            }
            catch
            {
                return false;
            }
        }
        
        public List<string> GetExcludedPatterns() => excludedPatterns.ToList();
        public List<string> GetExcludedFolders() => excludedFolders.ToList();
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 10. RESCANNING AUTOMÁTICO
    // ═══════════════════════════════════════════════════════════════
    
    public class AutoRescan
    {
        private Dictionary<string, FileSystemWatcher> watchers = new Dictionary<string, FileSystemWatcher>();
        private Action onFilesChanged;
        private System.Threading.Timer debounceTimer;
        private bool pendingRescan = false;
        
        public void MonitorFolder(string path, Action onChanged)
        {
            if (watchers.ContainsKey(path) || !Directory.Exists(path))
                return;
            
            onFilesChanged = onChanged;
            
            try
            {
                var watcher = new FileSystemWatcher(path)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size,
                    IncludeSubdirectories = true
                };
                
                watcher.Created += OnFileChanged;
                watcher.Deleted += OnFileChanged;
                watcher.Renamed += OnFileChanged;
                
                watcher.EnableRaisingEvents = true;
                watchers[path] = watcher;
            }
            catch { }
        }
        
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // Debounce: esperar 5 segundos antes de rescanear
            pendingRescan = true;
            debounceTimer?.Dispose();
            debounceTimer = new System.Threading.Timer(_ =>
            {
                if (pendingRescan)
                {
                    pendingRescan = false;
                    onFilesChanged?.Invoke();
                }
            }, null, 5000, System.Threading.Timeout.Infinite);
        }
        
        public void StopMonitoring()
        {
            foreach (var watcher in watchers.Values)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            watchers.Clear();
            debounceTimer?.Dispose();
        }
        
        public int MonitoredFoldersCount => watchers.Count;
    }
}
