using System;
using System.Threading.Tasks;
using System.Text.Json;
using StackExchange.Redis;

namespace SlskDown
{
    public class RedisCacheService
    {
        private ConnectionMultiplexer redis;
        private IDatabase db;
        private Action<string> logAction;
        private bool isConnected = false;
        
        public RedisCacheService(string connectionString, Action<string> logger)
        {
            logAction = logger;
            
            try
            {
                redis = ConnectionMultiplexer.Connect(connectionString);
                db = redis.GetDatabase();
                isConnected = true;
                logAction?.Invoke("✅ Redis cache conectado");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"⚠️ Redis no disponible, usando caché en memoria: {ex.Message}");
                isConnected = false;
            }
        }
        
        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan expiry)
        {
            if (!isConnected)
            {
                return await factory();
            }
            
            try
            {
                var cached = await db.StringGetAsync(key);
                if (!cached.IsNullOrEmpty)
                {
                    return JsonSerializer.Deserialize<T>(cached);
                }
                
                var value = await factory();
                var json = JsonSerializer.Serialize(value);
                await db.StringSetAsync(key, json, expiry);
                
                return value;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error en Redis cache: {ex.Message}");
                return await factory();
            }
        }
        
        public async Task<T> GetAsync<T>(string key)
        {
            if (!isConnected) return default(T);
            
            try
            {
                var cached = await db.StringGetAsync(key);
                if (!cached.IsNullOrEmpty)
                {
                    return JsonSerializer.Deserialize<T>(cached);
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error obteniendo de Redis: {ex.Message}");
            }
            
            return default(T);
        }
        
        public async Task SetAsync<T>(string key, T value, TimeSpan expiry)
        {
            if (!isConnected) return;
            
            try
            {
                var json = JsonSerializer.Serialize(value);
                await db.StringSetAsync(key, json, expiry);
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error guardando en Redis: {ex.Message}");
            }
        }
        
        public async Task<bool> DeleteAsync(string key)
        {
            if (!isConnected) return false;
            
            try
            {
                return await db.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error eliminando de Redis: {ex.Message}");
                return false;
            }
        }
        
        public async Task<long> IncrementAsync(string key)
        {
            if (!isConnected) return 0;
            
            try
            {
                return await db.StringIncrementAsync(key);
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error incrementando en Redis: {ex.Message}");
                return 0;
            }
        }
        
        public void Dispose()
        {
            redis?.Dispose();
        }
    }
}
