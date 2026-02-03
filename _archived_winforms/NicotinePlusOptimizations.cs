using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace SlskDown
{
    // ⭐ CONSTANTES INSPIRADAS EN NICOTINE+ ⭐
    public static class NicotinePlusConstants
    {
        // Búsquedas
        public const int MIN_SEARCH_CHARS = 3;
        public const int SEARCH_RESULTS_LIMIT = 500;
        public const int MAX_DISPLAYED_RESULTS = 2500;
        
        // Conexiones (en SEGUNDOS)
        // NOTA: Durante descargas puede no haber actividad de búsqueda por varios minutos
        // No confundir con zombie real - solo reconectar si realmente está muerta
        public const int CONNECTION_MAX_IDLE = 300; // 5 minutos (antes 60s era demasiado agresivo)
        public const int CONNECTION_MAX_IDLE_GHOST = 30; // 30 segundos (antes 10s era demasiado agresivo)
        public const int INDIRECT_CONNECTION_TIMEOUT = 20000;
        
        // Bandwidth
        public const int BANDWIDTH_CALC_INTERVAL_SECONDS = 1;
    }
    
    // ⭐ VALIDADOR DE BÚSQUEDAS (NICOTINE+) ⭐
    public static class SearchValidator
    {
        public static bool IsValidSearchQuery(string query, out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                errorMessage = "Búsqueda vacía";
                return false;
            }
            
            var trimmed = query.Trim();
            if (trimmed.Length < NicotinePlusConstants.MIN_SEARCH_CHARS)
            {
                errorMessage = $"Mínimo {NicotinePlusConstants.MIN_SEARCH_CHARS} caracteres";
                return false;
            }
            
            errorMessage = null;
            return true;
        }
    }
    
    // ⭐ DETECTOR DE CONEXIONES ZOMBIE (NICOTINE+) ⭐
    public class ZombieConnectionDetector
    {
        private DateTime lastActivity = DateTime.Now;
        private readonly object lockObj = new object();
        
        public void RecordActivity()
        {
            lock (lockObj)
            {
                lastActivity = DateTime.Now;
            }
        }
        
        public TimeSpan TimeSinceLastActivity
        {
            get
            {
                lock (lockObj)
                {
                    return DateTime.Now - lastActivity;
                }
            }
        }
        
        public bool IsConnectionActive(bool isCritical)
        {
            var timeSinceActivity = TimeSinceLastActivity;
            
            if (isCritical)
            {
                return timeSinceActivity.TotalSeconds <= NicotinePlusConstants.CONNECTION_MAX_IDLE_GHOST;
            }
            else
            {
                return timeSinceActivity.TotalSeconds <= NicotinePlusConstants.CONNECTION_MAX_IDLE;
            }
        }
    }
    
    // ⭐ TRACKING DE BANDWIDTH GLOBAL (NICOTINE+) ⭐
    public class GlobalBandwidthTracker
    {
        private long totalBytesDownloaded = 0;
        private long totalBytesUploaded = 0;
        private DateTime lastCalculation = DateTime.Now;
        private double currentDownloadSpeed = 0;
        private double currentUploadSpeed = 0;
        private readonly object lockObj = new object();
        
        public void RecordDownload(long bytes)
        {
            lock (lockObj)
            {
                totalBytesDownloaded += bytes;
                UpdateSpeeds();
            }
        }
        
        public void RecordUpload(long bytes)
        {
            lock (lockObj)
            {
                totalBytesUploaded += bytes;
                UpdateSpeeds();
            }
        }
        
        private void UpdateSpeeds()
        {
            var elapsed = (DateTime.Now - lastCalculation).TotalSeconds;
            if (elapsed >= NicotinePlusConstants.BANDWIDTH_CALC_INTERVAL_SECONDS)
            {
                currentDownloadSpeed = totalBytesDownloaded / elapsed;
                currentUploadSpeed = totalBytesUploaded / elapsed;
                
                totalBytesDownloaded = 0;
                totalBytesUploaded = 0;
                lastCalculation = DateTime.Now;
            }
        }
        
        public double CurrentDownloadSpeedMBps
        {
            get
            {
                lock (lockObj)
                {
                    return currentDownloadSpeed / 1024.0 / 1024.0;
                }
            }
        }
        
        public double CurrentUploadSpeedMBps
        {
            get
            {
                lock (lockObj)
                {
                    return currentUploadSpeed / 1024.0 / 1024.0;
                }
            }
        }
    }
    
    // ⭐ CACHÉ DE DIRECCIONES IP DE USUARIOS (NICOTINE+) ⭐
    public class UserAddressCache
    {
        private readonly ConcurrentDictionary<string, CachedAddress> cache = new ConcurrentDictionary<string, CachedAddress>();
        private const int CACHE_EXPIRATION_MINUTES = 30;
        
        private class CachedAddress
        {
            public IPEndPoint Address { get; set; }
            public DateTime CachedAt { get; set; }
        }
        
        public bool TryGetAddress(string username, out IPEndPoint address)
        {
            if (cache.TryGetValue(username, out var cached))
            {
                if ((DateTime.Now - cached.CachedAt).TotalMinutes < CACHE_EXPIRATION_MINUTES)
                {
                    address = cached.Address;
                    return true;
                }
                else
                {
                    cache.TryRemove(username, out _);
                }
            }
            
            address = null;
            return false;
        }
        
        public void CacheAddress(string username, IPEndPoint address)
        {
            cache[username] = new CachedAddress
            {
                Address = address,
                CachedAt = DateTime.Now
            };
        }
        
        public void Clear()
        {
            cache.Clear();
        }
        
        public int Count => cache.Count;
    }
    
    // ⭐ BUFFER DINÁMICO PARA UPLOADS (NICOTINE+) ⭐
    public class DynamicBufferCalculator
    {
        private const int MIN_BUFFER = 4096;      // 4 KB mínimo
        private const int MAX_BUFFER = 1048576;   // 1 MB máximo
        private const double BUFFER_MULTIPLIER = 1.25;  // 125% de velocidad actual
        
        public static int CalculateOptimalBufferSize(long bytesSent, TimeSpan elapsed)
        {
            if (elapsed.TotalSeconds <= 0 || bytesSent <= 0)
                return MIN_BUFFER;
            
            // Velocidad actual en bytes/segundo
            double currentSpeed = bytesSent / elapsed.TotalSeconds;
            
            // Buffer = 125% de la velocidad actual (Nicotine+ usa 1.25x)
            int optimalBuffer = (int)(currentSpeed * BUFFER_MULTIPLIER);
            
            // Limitar entre MIN y MAX
            return Math.Max(MIN_BUFFER, Math.Min(optimalBuffer, MAX_BUFFER));
        }
        
        public static int CalculateOptimalBufferSize(double speedBytesPerSecond)
        {
            if (speedBytesPerSecond <= 0)
                return MIN_BUFFER;
            
            int optimalBuffer = (int)(speedBytesPerSecond * BUFFER_MULTIPLIER);
            return Math.Max(MIN_BUFFER, Math.Min(optimalBuffer, MAX_BUFFER));
        }
    }
    
    // ⭐ LOGGING POR MÓDULOS CON COLAPSO (NICOTINE+) ⭐
    public enum DebugModule
    {
        None,
        Connection,
        Search,
        Download,
        Upload,
        Heartbeat,
        All
    }
    
    public class ModularLogger
    {
        private readonly ConcurrentDictionary<string, int> logCollapseCount = new ConcurrentDictionary<string, int>();
        private readonly ConcurrentDictionary<string, DateTime> lastLogTime = new ConcurrentDictionary<string, DateTime>();
        private readonly HashSet<DebugModule> enabledModules = new HashSet<DebugModule>();
        private readonly Action<string> logAction;
        private const int COLLAPSE_THRESHOLD = 5;  // Colapsar después de 5 repeticiones
        private const int COLLAPSE_INTERVAL_SECONDS = 10;  // Intervalo para resetear contador
        
        public ModularLogger(Action<string> logAction)
        {
            this.logAction = logAction;
            enabledModules.Add(DebugModule.All);  // Por defecto, todo habilitado
        }
        
        public void EnableModule(DebugModule module)
        {
            enabledModules.Add(module);
        }
        
        public void DisableModule(DebugModule module)
        {
            enabledModules.Remove(module);
        }
        
        public void Log(string message, DebugModule module = DebugModule.None)
        {
            // Verificar si el módulo está habilitado
            if (module != DebugModule.None && 
                !enabledModules.Contains(module) && 
                !enabledModules.Contains(DebugModule.All))
            {
                return;
            }
            
            // Colapsar logs repetidos
            var now = DateTime.Now;
            
            if (lastLogTime.TryGetValue(message, out var lastTime))
            {
                var elapsed = (now - lastTime).TotalSeconds;
                
                // Si pasó suficiente tiempo, resetear contador
                if (elapsed > COLLAPSE_INTERVAL_SECONDS)
                {
                    logCollapseCount[message] = 1;
                    lastLogTime[message] = now;
                    logAction(message);
                    return;
                }
            }
            
            // Incrementar contador
            int count = logCollapseCount.AddOrUpdate(message, 1, (k, v) => v + 1);
            lastLogTime[message] = now;
            
            // Mostrar log colapsado cada COLLAPSE_THRESHOLD veces
            if (count == 1)
            {
                logAction(message);
            }
            else if (count >= COLLAPSE_THRESHOLD && count % COLLAPSE_THRESHOLD == 0)
            {
                logAction($"{message} (x{count})");
            }
        }
        
        public void ClearCollapseCounters()
        {
            logCollapseCount.Clear();
            lastLogTime.Clear();
        }
    }
    
    // ⭐ FILTROS AVANZADOS DE BÚSQUEDA (NICOTINE+) ⭐
    public class AdvancedSearchFilters
    {
        public List<string> MustInclude { get; set; } = new List<string>();      // Palabras obligatorias
        public List<string> MustExclude { get; set; } = new List<string>();      // Palabras excluidas
        public List<string> CountryCodes { get; set; } = new List<string>();     // Códigos de país
        public long? MinSize { get; set; }                                        // Tamaño mínimo en bytes
        public long? MaxSize { get; set; }                                        // Tamaño máximo en bytes
        public int? MinBitrate { get; set; }                                      // Bitrate mínimo
        public int? MaxBitrate { get; set; }                                      // Bitrate máximo
        public List<string> FileExtensions { get; set; } = new List<string>();   // Extensiones permitidas
        public TimeSpan? MinDuration { get; set; }                                // Duración mínima
        public TimeSpan? MaxDuration { get; set; }                                // Duración máxima
        
        public bool PassesFilter(string filename, long fileSize, string username = null)
        {
            var lowerFilename = filename.ToLowerInvariant();
            
            // Verificar palabras obligatorias
            if (MustInclude.Any() && !MustInclude.All(word => lowerFilename.Contains(word.ToLowerInvariant())))
            {
                return false;
            }
            
            // Verificar palabras excluidas
            if (MustExclude.Any(word => lowerFilename.Contains(word.ToLowerInvariant())))
            {
                return false;
            }
            
            // Verificar tamaño
            if (MinSize.HasValue && fileSize < MinSize.Value)
            {
                return false;
            }
            
            if (MaxSize.HasValue && fileSize > MaxSize.Value)
            {
                return false;
            }
            
            // Verificar extensión
            if (FileExtensions.Any())
            {
                var extension = System.IO.Path.GetExtension(filename).ToLowerInvariant().TrimStart('.');
                if (!FileExtensions.Contains(extension))
                {
                    return false;
                }
            }
            
            return true;
        }
        
        public bool IsEmpty()
        {
            return !MustInclude.Any() && 
                   !MustExclude.Any() && 
                   !CountryCodes.Any() && 
                   !MinSize.HasValue && 
                   !MaxSize.HasValue && 
                   !MinBitrate.HasValue && 
                   !MaxBitrate.HasValue && 
                   !FileExtensions.Any() && 
                   !MinDuration.HasValue && 
                   !MaxDuration.HasValue;
        }
    }
    
    // ⭐ TIMEOUT PARA CONEXIONES INDIRECTAS (NICOTINE+) ⭐
    public class IndirectConnectionManager
    {
        private readonly ConcurrentDictionary<string, DateTime> pendingRequests = new ConcurrentDictionary<string, DateTime>();
        private const int TIMEOUT_MILLISECONDS = NicotinePlusConstants.INDIRECT_CONNECTION_TIMEOUT;
        
        public void RegisterRequest(string username)
        {
            pendingRequests[username] = DateTime.Now;
        }
        
        public void CompleteRequest(string username)
        {
            pendingRequests.TryRemove(username, out _);
        }
        
        public List<string> GetTimedOutRequests()
        {
            var now = DateTime.Now;
            var timedOut = new List<string>();
            
            foreach (var kvp in pendingRequests)
            {
                var elapsed = (now - kvp.Value).TotalMilliseconds;
                if (elapsed > TIMEOUT_MILLISECONDS)
                {
                    timedOut.Add(kvp.Key);
                }
            }
            
            // Limpiar requests que expiraron
            foreach (var username in timedOut)
            {
                pendingRequests.TryRemove(username, out _);
            }
            
            return timedOut;
        }
        
        public int PendingCount => pendingRequests.Count;
        
        public void Clear()
        {
            pendingRequests.Clear();
        }
        
        // Métodos adicionales requeridos por MainForm
        public void RequestIndirectConnection(string username)
        {
            RegisterRequest(username);
        }
        
        public void ConfirmConnection(string username)
        {
            CompleteRequest(username);
        }
        
        public void CleanupExpiredRequests()
        {
            GetTimedOutRequests();
        }
    }
}
