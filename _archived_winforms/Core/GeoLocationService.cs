using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
using SlskDown.Core.Optimization;

namespace SlskDown.Core
{
    /// <summary>
    /// Servicio de geolocalización para optimizar descargas basadas en proximidad geográfica
    /// </summary>
    public class GeoLocationService
    {
        private readonly HttpClient httpClient;
        private readonly Dictionary<string, GeoLocation> locationCache;
        private readonly object cacheLock = new object();
        private readonly string cacheFilePath;
        private GeoLocation myLocation;
        
        // Configuración
        private const int CACHE_EXPIRY_DAYS = 30;
        private const int MAX_CACHE_SIZE = 10000;
        
        public GeoLocationService(string dataDirectory)
        {
            httpClient = OptimizedHttpClient.CreateCustomClient(
                maxConnectionsPerServer: 5,
                timeout: TimeSpan.FromSeconds(5)
            );
            
            locationCache = new Dictionary<string, GeoLocation>();
            cacheFilePath = Path.Combine(dataDirectory, "geo_cache.json");
            
            LoadCache();
            _ = InitializeMyLocation();
        }
        
        /// <summary>
        /// Inicializa la ubicación del usuario actual
        /// </summary>
        private async Task InitializeMyLocation()
        {
            try
            {
                // Usar servicio gratuito de geolocalización
                var response = await httpClient.GetStringAsync("http://ip-api.com/json/");
                var data = JsonSerializer.Deserialize<JsonElement>(response);
                
                myLocation = new GeoLocation
                {
                    IP = data.GetProperty("query").GetString(),
                    Country = data.GetProperty("country").GetString(),
                    CountryCode = data.GetProperty("countryCode").GetString(),
                    Region = data.GetProperty("regionName").GetString(),
                    City = data.GetProperty("city").GetString(),
                    Latitude = data.GetProperty("lat").GetDouble(),
                    Longitude = data.GetProperty("lon").GetDouble(),
                    ISP = data.GetProperty("isp").GetString(),
                    Timestamp = DateTime.UtcNow
                };
                
                OnLog?.Invoke($"📍 Tu ubicación: {myLocation.City}, {myLocation.Country}");
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"⚠️ No se pudo obtener tu ubicación: {ex.Message}");
                
                // Ubicación por defecto (desconocida)
                myLocation = new GeoLocation
                {
                    Country = "Unknown",
                    Latitude = 0,
                    Longitude = 0,
                    Timestamp = DateTime.UtcNow
                };
            }
        }
        
        /// <summary>
        /// Obtiene la ubicación geográfica de una IP
        /// </summary>
        public async Task<GeoLocation> GetLocationAsync(string ip)
        {
            if (string.IsNullOrEmpty(ip))
                return null;
            
            // Verificar cache
            lock (cacheLock)
            {
                if (locationCache.TryGetValue(ip, out var cached))
                {
                    // Verificar si no ha expirado
                    if ((DateTime.UtcNow - cached.Timestamp).TotalDays < CACHE_EXPIRY_DAYS)
                    {
                        return cached;
                    }
                    
                    // Expirado, remover
                    locationCache.Remove(ip);
                }
            }
            
            // Consultar servicio de geolocalización
            try
            {
                var response = await httpClient.GetStringAsync($"http://ip-api.com/json/{ip}");
                var data = JsonSerializer.Deserialize<JsonElement>(response);
                
                if (data.GetProperty("status").GetString() == "success")
                {
                    var location = new GeoLocation
                    {
                        IP = ip,
                        Country = data.GetProperty("country").GetString(),
                        CountryCode = data.GetProperty("countryCode").GetString(),
                        Region = data.GetProperty("regionName").GetString(),
                        City = data.GetProperty("city").GetString(),
                        Latitude = data.GetProperty("lat").GetDouble(),
                        Longitude = data.GetProperty("lon").GetDouble(),
                        ISP = data.GetProperty("isp").GetString(),
                        Timestamp = DateTime.UtcNow
                    };
                    
                    // Guardar en cache
                    lock (cacheLock)
                    {
                        locationCache[ip] = location;
                        
                        // Limitar tamaño del cache
                        if (locationCache.Count > MAX_CACHE_SIZE)
                        {
                            var oldest = locationCache.OrderBy(x => x.Value.Timestamp).First();
                            locationCache.Remove(oldest.Key);
                        }
                    }
                    
                    SaveCache();
                    return location;
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"⚠️ Error obteniendo ubicación de {ip}: {ex.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Calcula la distancia en kilómetros entre dos ubicaciones usando la fórmula de Haversine
        /// </summary>
        public double CalculateDistance(GeoLocation loc1, GeoLocation loc2)
        {
            if (loc1 == null || loc2 == null)
                return double.MaxValue;
            
            const double R = 6371; // Radio de la Tierra en km
            
            var lat1 = ToRadians(loc1.Latitude);
            var lat2 = ToRadians(loc2.Latitude);
            var dLat = ToRadians(loc2.Latitude - loc1.Latitude);
            var dLon = ToRadians(loc2.Longitude - loc1.Longitude);
            
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1) * Math.Cos(lat2) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            
            return R * c;
        }
        
        /// <summary>
        /// Calcula la distancia desde tu ubicación a una IP
        /// </summary>
        public async Task<double> CalculateDistanceFromMeAsync(string ip)
        {
            if (myLocation == null)
                return double.MaxValue;
            
            var location = await GetLocationAsync(ip);
            return CalculateDistance(myLocation, location);
        }
        
        /// <summary>
        /// Obtiene un score de proximidad (0-100, donde 100 es más cercano)
        /// </summary>
        public async Task<int> GetProximityScoreAsync(string ip)
        {
            var distance = await CalculateDistanceFromMeAsync(ip);
            
            if (distance == double.MaxValue)
                return 50; // Score neutral si no se puede determinar
            
            // Convertir distancia a score (0-100)
            // 0 km = 100 puntos
            // 20000 km = 0 puntos
            var score = Math.Max(0, 100 - (int)(distance / 200));
            return score;
        }
        
        /// <summary>
        /// Obtiene estadísticas de ubicaciones en cache
        /// </summary>
        public GeoStats GetStats()
        {
            lock (cacheLock)
            {
                var stats = new GeoStats
                {
                    TotalCached = locationCache.Count,
                    MyLocation = myLocation,
                    CountryDistribution = locationCache
                        .GroupBy(x => x.Value.Country)
                        .OrderByDescending(g => g.Count())
                        .Take(10)
                        .ToDictionary(g => g.Key, g => g.Count())
                };
                
                if (myLocation != null)
                {
                    // Calcular distancia promedio
                    var distances = locationCache.Values
                        .Select(loc => CalculateDistance(myLocation, loc))
                        .Where(d => d != double.MaxValue)
                        .ToList();
                    
                    if (distances.Any())
                    {
                        stats.AverageDistance = distances.Average();
                        stats.MinDistance = distances.Min();
                        stats.MaxDistance = distances.Max();
                    }
                }
                
                return stats;
            }
        }
        
        /// <summary>
        /// Guarda el cache en disco
        /// </summary>
        private void SaveCache()
        {
            try
            {
                lock (cacheLock)
                {
                    var json = JsonSerializer.Serialize(locationCache, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    
                    File.WriteAllText(cacheFilePath, json);
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"⚠️ Error guardando cache de geolocalización: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Carga el cache desde disco
        /// </summary>
        private void LoadCache()
        {
            try
            {
                if (File.Exists(cacheFilePath))
                {
                    var json = File.ReadAllText(cacheFilePath);
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, GeoLocation>>(json);
                    
                    if (loaded != null)
                    {
                        lock (cacheLock)
                        {
                            foreach (var kvp in loaded)
                            {
                                // Solo cargar si no ha expirado
                                if ((DateTime.UtcNow - kvp.Value.Timestamp).TotalDays < CACHE_EXPIRY_DAYS)
                                {
                                    locationCache[kvp.Key] = kvp.Value;
                                }
                            }
                        }
                        
                        OnLog?.Invoke($"📍 Cache de geolocalización cargado: {locationCache.Count} ubicaciones");
                    }
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"⚠️ Error cargando cache de geolocalización: {ex.Message}");
            }
        }
        
        private double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
        
        public Action<string> OnLog { get; set; }
    }
    
    /// <summary>
    /// Información de ubicación geográfica
    /// </summary>
    public class GeoLocation
    {
        public string IP { get; set; }
        public string Country { get; set; }
        public string CountryCode { get; set; }
        public string Region { get; set; }
        public string City { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string ISP { get; set; }
        public DateTime Timestamp { get; set; }
        
        public override string ToString()
        {
            return $"{City}, {Region}, {Country} ({Latitude:F2}, {Longitude:F2})";
        }
    }
    
    /// <summary>
    /// Estadísticas de geolocalización
    /// </summary>
    public class GeoStats
    {
        public int TotalCached { get; set; }
        public GeoLocation MyLocation { get; set; }
        public Dictionary<string, int> CountryDistribution { get; set; }
        public double AverageDistance { get; set; }
        public double MinDistance { get; set; }
        public double MaxDistance { get; set; }
    }
}
