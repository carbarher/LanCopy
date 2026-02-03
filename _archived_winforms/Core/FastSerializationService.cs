using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MessagePack;

namespace SlskDown.Core
{
    /// <summary>
    /// Servicio de serialización ultra-rápida usando MessagePack
    /// 5-10x más rápido que JSON para caché y persistencia
    /// </summary>
    public static class FastSerializationService
    {
        private static readonly MessagePackSerializerOptions _options = 
            MessagePackSerializerOptions.Standard
                .WithCompression(MessagePackCompression.Lz4BlockArray);

        /// <summary>
        /// Serializa un objeto a bytes
        /// </summary>
        public static byte[] Serialize<T>(T obj)
        {
            return MessagePackSerializer.Serialize(obj, _options);
        }

        /// <summary>
        /// Deserializa bytes a objeto
        /// </summary>
        public static T Deserialize<T>(byte[] data)
        {
            return MessagePackSerializer.Deserialize<T>(data, _options);
        }

        /// <summary>
        /// Serializa a archivo
        /// </summary>
        public static async Task SerializeToFileAsync<T>(string filePath, T obj)
        {
            var bytes = Serialize(obj);
            await File.WriteAllBytesAsync(filePath, bytes);
        }

        /// <summary>
        /// Deserializa desde archivo
        /// </summary>
        public static async Task<T> DeserializeFromFileAsync<T>(string filePath)
        {
            var bytes = await File.ReadAllBytesAsync(filePath);
            return Deserialize<T>(bytes);
        }

        /// <summary>
        /// Serializa a stream
        /// </summary>
        public static async Task SerializeToStreamAsync<T>(Stream stream, T obj)
        {
            await MessagePackSerializer.SerializeAsync(stream, obj, _options);
        }

        /// <summary>
        /// Deserializa desde stream
        /// </summary>
        public static async Task<T> DeserializeFromStreamAsync<T>(Stream stream)
        {
            return await MessagePackSerializer.DeserializeAsync<T>(stream, _options);
        }
    }

    /// <summary>
    /// DTOs optimizados para MessagePack
    /// </summary>
    [MessagePackObject]
    public class CachedSearchResults
    {
        [Key(0)]
        public string Query { get; set; } = "";

        [Key(1)]
        public List<CachedSearchResultItem> Results { get; set; } = new();

        [Key(2)]
        public DateTime Timestamp { get; set; }

        [Key(3)]
        public int TotalCount { get; set; }
    }

    [MessagePackObject]
    public class CachedSearchResultItem
    {
        [Key(0)]
        public string Filename { get; set; } = "";

        [Key(1)]
        public long Size { get; set; }

        [Key(2)]
        public string Username { get; set; } = "";

        [Key(3)]
        public string Extension { get; set; } = "";

        [Key(4)]
        public int Quality { get; set; }

        [Key(5)]
        public int Speed { get; set; }

        [Key(6)]
        public int QueueLength { get; set; }

        [Key(7)]
        public bool HasFreeSlot { get; set; }

        /// <summary>
        /// Convierte a SearchResultItem
        /// </summary>
        public SearchResultItem ToSearchResultItem()
        {
            return new SearchResultItem
            {
                Filename = Filename,
                Size = Size,
                Username = Username,
                Extension = Extension,
                Quality = Quality,
                Speed = Speed,
                QueueLength = QueueLength,
                HasFreeSlot = HasFreeSlot
            };
        }

        /// <summary>
        /// Crea desde SearchResultItem
        /// </summary>
        public static CachedSearchResultItem FromSearchResultItem(SearchResultItem item)
        {
            return new CachedSearchResultItem
            {
                Filename = item.Filename ?? "",
                Size = item.Size,
                Username = item.Username ?? "",
                Extension = item.Extension ?? "",
                Quality = item.Quality,
                Speed = item.Speed,
                QueueLength = item.QueueLength,
                HasFreeSlot = item.HasFreeSlot
            };
        }
    }

    /// <summary>
    /// Caché de búsquedas con MessagePack
    /// </summary>
    public class MessagePackSearchCache
    {
        private readonly string _cacheDir;
        private static readonly TimeSpan DefaultTTL = TimeSpan.FromHours(24);

        public MessagePackSearchCache(string cacheDir)
        {
            _cacheDir = cacheDir;
            Directory.CreateDirectory(_cacheDir);
        }

        /// <summary>
        /// Guarda resultados de búsqueda
        /// </summary>
        public async Task SaveResultsAsync(string query, List<SearchResultItem> results)
        {
            var cached = new CachedSearchResults
            {
                Query = query,
                Results = results.Select(CachedSearchResultItem.FromSearchResultItem).ToList(),
                Timestamp = DateTime.UtcNow,
                TotalCount = results.Count
            };

            var filePath = GetCacheFilePath(query);
            await FastSerializationService.SerializeToFileAsync(filePath, cached);
        }

        /// <summary>
        /// Obtiene resultados de búsqueda
        /// </summary>
        public async Task<List<SearchResultItem>?> GetResultsAsync(string query)
        {
            var filePath = GetCacheFilePath(query);
            if (!File.Exists(filePath))
                return null;

            try
            {
                var cached = await FastSerializationService.DeserializeFromFileAsync<CachedSearchResults>(filePath);
                
                // Verificar TTL
                if (DateTime.UtcNow - cached.Timestamp > DefaultTTL)
                {
                    File.Delete(filePath);
                    return null;
                }

                return cached.Results.Select(r => r.ToSearchResultItem()).ToList();
            }
            catch
            {
                // Archivo corrupto, eliminar
                File.Delete(filePath);
                return null;
            }
        }

        /// <summary>
        /// Limpia caché antiguo
        /// </summary>
        public void CleanOldCache()
        {
            var files = Directory.GetFiles(_cacheDir, "*.msgpack");
            foreach (var file in files)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (DateTime.UtcNow - fileInfo.LastWriteTimeUtc > DefaultTTL)
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // Ignorar errores
                }
            }
        }

        private string GetCacheFilePath(string query)
        {
            var hash = GetQueryHash(query);
            return Path.Combine(_cacheDir, $"{hash}.msgpack");
        }

        private static string GetQueryHash(string query)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(query.ToLowerInvariant());
            var hash = md5.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>
    /// Configuración serializable con MessagePack
    /// </summary>
    [MessagePackObject]
    public class FastConfig
    {
        [Key(0)]
        public string Username { get; set; } = "";

        [Key(1)]
        public string DownloadDirectory { get; set; } = "";

        [Key(2)]
        public int MaxConcurrentDownloads { get; set; } = 3;

        [Key(3)]
        public bool EnableAutoSearch { get; set; }

        [Key(4)]
        public List<string> Authors { get; set; } = new();

        [Key(5)]
        public Dictionary<string, string> Settings { get; set; } = new();
    }
}
