using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Soulseek;

namespace SlskDown.Core
{
    /// <summary>
    /// Optimizaciones inspiradas en Nicotine+ para mejorar estabilidad y performance
    /// Basado en análisis del código fuente de nicotine-plus/nicotine-plus
    /// </summary>
    public static class NicotinePlusOptimizations
    {
        // CONSTANTES EXTRAÍDAS DE NICOTINE+ (pynicotine/slskproto.py)
        
        /// <summary>
        /// Timeout para conexiones indirectas (20 segundos)
        /// Nicotine+: INDIRECT_REQUEST_TIMEOUT = 20
        /// </summary>
        public const int INDIRECT_CONNECTION_TIMEOUT = 20000;
        
        /// <summary>
        /// Timeout para conexiones inactivas normales (60 segundos)
        /// Nicotine+: CONNECTION_MAX_IDLE = 60
        /// </summary>
        public const int CONNECTION_MAX_IDLE = 60000;
        
        /// <summary>
        /// Timeout para conexiones "fantasma" sin datos (10 segundos)
        /// Nicotine+: CONNECTION_MAX_IDLE_GHOST = 10
        /// </summary>
        public const int CONNECTION_MAX_IDLE_GHOST = 10000;
        
        /// <summary>
        /// Máximo de resultados por búsqueda
        /// Nicotine+: "maxresults": 300
        /// </summary>
        public const int MAX_SEARCH_RESULTS = 300;
        
        /// <summary>
        /// Máximo de resultados mostrados en UI
        /// Nicotine+: "max_displayed_results": 2500
        /// </summary>
        public const int MAX_DISPLAYED_RESULTS = 2500;
        
        /// <summary>
        /// Mínimo de caracteres para iniciar búsqueda
        /// Nicotine+: "min_search_chars": 3
        /// </summary>
        public const int MIN_SEARCH_CHARS = 3;
        
        /// <summary>
        /// Buffer mínimo para uploads (4 KB)
        /// Nicotine+: max(4096, num_sent_bytes * 1.25)
        /// </summary>
        public const int MIN_UPLOAD_BUFFER = 4096;
        
        /// <summary>
        /// Multiplicador para buffer dinámico (125%)
        /// Nicotine+: num_sent_bytes * 1.25
        /// </summary>
        public const double BUFFER_SPEED_MULTIPLIER = 1.25;
    }
    
    /// <summary>
    /// Caché de direcciones IP de usuarios
    /// Inspirado en Nicotine+: self._user_addresses = {}
    /// </summary>
    public class UserAddressCache
    {
        private readonly Dictionary<string, CachedAddress> _cache = new Dictionary<string, CachedAddress>();
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);
        private readonly object _lock = new object();
        
        private class CachedAddress
        {
            public IPEndPoint Address { get; set; }
            public DateTime CachedAt { get; set; }
            public bool IsOnline { get; set; }
        }
        
        /// <summary>
        /// Obtiene la dirección IP cacheada de un usuario
        /// </summary>
        public bool TryGetAddress(string username, out IPEndPoint address)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(username, out var cached))
                {
                    // Verificar si no ha expirado
                    if (DateTime.Now - cached.CachedAt < _cacheExpiration && cached.IsOnline)
                    {
                        address = cached.Address;
                        return true;
                    }
                    
                    // Expirado, eliminar
                    _cache.Remove(username);
                }
                
                address = null;
                return false;
            }
        }
        
        /// <summary>
        /// Cachea la dirección IP de un usuario
        /// </summary>
        public void CacheAddress(string username, IPEndPoint address, bool isOnline = true)
        {
            lock (_lock)
            {
                _cache[username] = new CachedAddress
                {
                    Address = address,
                    CachedAt = DateTime.Now,
                    IsOnline = isOnline
                };
            }
        }
        
        /// <summary>
        /// Marca un usuario como offline (resetea su IP)
        /// Nicotine+: if msg.status == UserStatus.OFFLINE: self._user_addresses[msg.user] = None
        /// </summary>
        public void MarkOffline(string username)
        {
            lock (_lock)
            {
                _cache.Remove(username);
            }
        }
        
        /// <summary>
        /// Limpia entradas expiradas del caché
        /// </summary>
        public void CleanExpired()
        {
            lock (_lock)
            {
                var expired = new List<string>();
                
                foreach (var kvp in _cache)
                {
                    if (DateTime.Now - kvp.Value.CachedAt >= _cacheExpiration)
                    {
                        expired.Add(kvp.Key);
                    }
                }
                
                foreach (var username in expired)
                {
                    _cache.Remove(username);
                }
            }
        }
        
        /// <summary>
        /// Obtiene el número de entradas en caché
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _cache.Count;
                }
            }
        }
    }
    
    /// <summary>
    /// Calculador de buffer dinámico para uploads
    /// Inspirado en Nicotine+: _process_upload()
    /// </summary>
    public static class DynamicBufferCalculator
    {
        /// <summary>
        /// Calcula el tamaño óptimo de buffer según la velocidad actual
        /// Nicotine+: num_bytes_to_read = int((max(4096, num_sent_bytes * 1.25) / elapsed_time))
        /// </summary>
        /// <param name="bytesSent">Bytes enviados recientemente</param>
        /// <param name="elapsed">Tiempo transcurrido</param>
        /// <returns>Tamaño óptimo de buffer en bytes</returns>
        public static int CalculateOptimalBufferSize(long bytesSent, TimeSpan elapsed)
        {
            if (elapsed.TotalSeconds <= 0)
                return NicotinePlusOptimizations.MIN_UPLOAD_BUFFER;
            
            // Velocidad actual en bytes/segundo
            double currentSpeed = bytesSent / elapsed.TotalSeconds;
            
            // Buffer = 125% de la velocidad actual (mínimo 4KB)
            int optimalBuffer = (int)(currentSpeed * NicotinePlusOptimizations.BUFFER_SPEED_MULTIPLIER);
            
            return Math.Max(NicotinePlusOptimizations.MIN_UPLOAD_BUFFER, optimalBuffer);
        }
        
        /// <summary>
        /// Calcula el tamaño óptimo de buffer basado en bytes enviados en el último chunk
        /// </summary>
        public static int CalculateOptimalBufferSize(long recentBytesSent)
        {
            // Mínimo 4KB, máximo 125% de los bytes enviados recientemente
            int optimalBuffer = (int)(recentBytesSent * NicotinePlusOptimizations.BUFFER_SPEED_MULTIPLIER);
            
            return Math.Max(NicotinePlusOptimizations.MIN_UPLOAD_BUFFER, optimalBuffer);
        }
    }
    
    /// <summary>
    /// Tracker de bandwidth global
    /// Inspirado en Nicotine+: self._total_download_bandwidth, self._total_upload_bandwidth
    /// </summary>
    public class GlobalBandwidthTracker
    {
        private long _totalDownloadBandwidth = 0;
        private long _totalUploadBandwidth = 0;
        private readonly object _lock = new object();
        
        /// <summary>
        /// Registra bytes descargados
        /// Nicotine+: self._total_download_bandwidth += data_len
        /// </summary>
        public void RecordDownload(long bytes)
        {
            lock (_lock)
            {
                _totalDownloadBandwidth += bytes;
            }
        }
        
        /// <summary>
        /// Registra bytes subidos
        /// Nicotine+: self._total_upload_bandwidth += num_sent_bytes
        /// </summary>
        public void RecordUpload(long bytes)
        {
            lock (_lock)
            {
                _totalUploadBandwidth += bytes;
            }
        }
        
        /// <summary>
        /// Obtiene el total de bytes descargados
        /// </summary>
        public long TotalDownloadBandwidth
        {
            get
            {
                lock (_lock)
                {
                    return _totalDownloadBandwidth;
                }
            }
        }
        
        /// <summary>
        /// Obtiene el total de bytes subidos
        /// </summary>
        public long TotalUploadBandwidth
        {
            get
            {
                lock (_lock)
                {
                    return _totalUploadBandwidth;
                }
            }
        }
        
        /// <summary>
        /// Resetea los contadores
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _totalDownloadBandwidth = 0;
                _totalUploadBandwidth = 0;
            }
        }
    }
    
    /// <summary>
    /// Detector de conexiones zombie (inactivas)
    /// Inspirado en Nicotine+: _is_connection_still_active()
    /// </summary>
    public class ZombieConnectionDetector
    {
        private DateTime _lastActivityTime = DateTime.Now;
        private bool _hasPendingData = false;
        private readonly object _lock = new object();
        
        /// <summary>
        /// Registra actividad en la conexión
        /// </summary>
        public void RecordActivity()
        {
            lock (_lock)
            {
                _lastActivityTime = DateTime.Now;
            }
        }
        
        /// <summary>
        /// Establece si hay datos pendientes
        /// Nicotine+: len(conn.out_buffer) > 0 or len(conn.in_buffer) > 0
        /// </summary>
        public void SetHasPendingData(bool hasPending)
        {
            lock (_lock)
            {
                _hasPendingData = hasPending;
            }
        }
        
        /// <summary>
        /// Verifica si la conexión está activa
        /// </summary>
        /// <param name="isCritical">Si es conexión crítica (distributed, file)</param>
        /// <returns>True si está activa, False si es zombie</returns>
        public bool IsConnectionActive(bool isCritical = false)
        {
            lock (_lock)
            {
                // Conexiones críticas siempre se consideran activas
                if (isCritical)
                    return true;
                
                var timeSinceLastActivity = DateTime.Now - _lastActivityTime;
                
                // Si hay datos pendientes, está activa
                if (_hasPendingData)
                    return true;
                
                // Conexión "fantasma" (sin datos): timeout 10s
                if (timeSinceLastActivity.TotalMilliseconds > NicotinePlusOptimizations.CONNECTION_MAX_IDLE_GHOST)
                    return false;
                
                // Conexión normal: timeout 60s
                if (timeSinceLastActivity.TotalMilliseconds > NicotinePlusOptimizations.CONNECTION_MAX_IDLE)
                    return false;
                
                return true;
            }
        }
        
        /// <summary>
        /// Obtiene el tiempo desde la última actividad
        /// </summary>
        public TimeSpan TimeSinceLastActivity
        {
            get
            {
                lock (_lock)
                {
                    return DateTime.Now - _lastActivityTime;
                }
            }
        }
    }
    
    /// <summary>
    /// Validador de búsquedas
    /// Inspirado en Nicotine+: "min_search_chars": 3
    /// </summary>
    public static class SearchValidator
    {
        /// <summary>
        /// Valida si una consulta de búsqueda es válida
        /// </summary>
        public static bool IsValidSearchQuery(string query, out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                errorMessage = "La búsqueda no puede estar vacía";
                return false;
            }
            
            if (query.Trim().Length < NicotinePlusOptimizations.MIN_SEARCH_CHARS)
            {
                errorMessage = $"La búsqueda requiere mínimo {NicotinePlusOptimizations.MIN_SEARCH_CHARS} caracteres";
                return false;
            }
            
            errorMessage = null;
            return true;
        }
    }
}
