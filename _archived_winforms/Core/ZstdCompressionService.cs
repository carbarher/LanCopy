using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ZstdSharp;

namespace SlskDown.Core
{
    /// <summary>
    /// Servicio de compresión usando Zstandard (70-80% reducción de espacio)
    /// Más rápido y mejor ratio que gzip/deflate
    /// </summary>
    public static class ZstdCompressionService
    {
        // Niveles de compresión:
        // 1-3: Rápido, ratio moderado (para caché temporal)
        // 4-9: Balance (para caché persistente)
        // 10-22: Máxima compresión (para archivos de logs)
        private const int DefaultLevel = 3;
        private const int HighCompressionLevel = 9;
        private const int MaxCompressionLevel = 19;

        /// <summary>
        /// Comprime datos con nivel por defecto (rápido)
        /// </summary>
        public static byte[] Compress(byte[] data)
        {
            using var compressor = new Compressor(DefaultLevel);
            return compressor.Wrap(data).ToArray();
        }

        /// <summary>
        /// Comprime datos con nivel específico
        /// </summary>
        public static byte[] Compress(byte[] data, int level)
        {
            using var compressor = new Compressor(level);
            return compressor.Wrap(data).ToArray();
        }

        /// <summary>
        /// Descomprime datos
        /// </summary>
        public static byte[] Decompress(byte[] compressedData)
        {
            using var decompressor = new Decompressor();
            return decompressor.Unwrap(compressedData).ToArray();
        }

        /// <summary>
        /// Comprime string a bytes
        /// </summary>
        public static byte[] CompressString(string text, int level = DefaultLevel)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            return Compress(bytes, level);
        }

        /// <summary>
        /// Descomprime bytes a string
        /// </summary>
        public static string DecompressString(byte[] compressedData)
        {
            var bytes = Decompress(compressedData);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Comprime archivo
        /// </summary>
        public static async Task CompressFileAsync(string sourcePath, string destPath, int level = DefaultLevel)
        {
            var data = await File.ReadAllBytesAsync(sourcePath);
            var compressed = Compress(data, level);
            await File.WriteAllBytesAsync(destPath, compressed);
        }

        /// <summary>
        /// Descomprime archivo
        /// </summary>
        public static async Task DecompressFileAsync(string sourcePath, string destPath)
        {
            var compressed = await File.ReadAllBytesAsync(sourcePath);
            var decompressed = Decompress(compressed);
            await File.WriteAllBytesAsync(destPath, decompressed);
        }

        /// <summary>
        /// Calcula ratio de compresión
        /// </summary>
        public static double GetCompressionRatio(byte[] original, byte[] compressed)
        {
            return 1.0 - ((double)compressed.Length / original.Length);
        }
    }

    /// <summary>
    /// Caché comprimido con Zstandard
    /// </summary>
    public class CompressedCacheService
    {
        private readonly string _cacheDir;

        public CompressedCacheService(string cacheDir)
        {
            _cacheDir = cacheDir;
            Directory.CreateDirectory(_cacheDir);
        }

        /// <summary>
        /// Guarda objeto en caché comprimido
        /// </summary>
        public async Task SaveAsync<T>(string key, T value)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            var compressed = ZstdCompressionService.CompressString(json, level: 3);
            
            var filePath = GetCachePath(key);
            await File.WriteAllBytesAsync(filePath, compressed);
        }

        /// <summary>
        /// Carga objeto desde caché comprimido
        /// </summary>
        public async Task<T?> LoadAsync<T>(string key)
        {
            var filePath = GetCachePath(key);
            if (!File.Exists(filePath))
                return default;

            try
            {
                var compressed = await File.ReadAllBytesAsync(filePath);
                var json = ZstdCompressionService.DecompressString(compressed);
                return System.Text.Json.JsonSerializer.Deserialize<T>(json);
            }
            catch
            {
                // Archivo corrupto, eliminar
                File.Delete(filePath);
                return default;
            }
        }

        /// <summary>
        /// Obtiene estadísticas de compresión
        /// </summary>
        public async Task<CompressionStats> GetStatsAsync()
        {
            var files = Directory.GetFiles(_cacheDir, "*.zst");
            long totalCompressed = 0;
            long totalOriginal = 0;

            foreach (var file in files)
            {
                var info = new FileInfo(file);
                totalCompressed += info.Length;

                // Estimar tamaño original (descomprimir solo para stats)
                try
                {
                    var compressed = await File.ReadAllBytesAsync(file);
                    var decompressed = ZstdCompressionService.Decompress(compressed);
                    totalOriginal += decompressed.Length;
                }
                catch
                {
                    // Ignorar archivos corruptos
                }
            }

            return new CompressionStats
            {
                FileCount = files.Length,
                TotalCompressedBytes = totalCompressed,
                TotalOriginalBytes = totalOriginal,
                CompressionRatio = totalOriginal > 0 
                    ? 1.0 - ((double)totalCompressed / totalOriginal) 
                    : 0,
                SpaceSavedMB = (totalOriginal - totalCompressed) / 1024.0 / 1024.0
            };
        }

        private string GetCachePath(string key)
        {
            var hash = GetKeyHash(key);
            return Path.Combine(_cacheDir, $"{hash}.zst");
        }

        private static string GetKeyHash(string key)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var bytes = Encoding.UTF8.GetBytes(key);
            var hash = md5.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>
    /// Compresión adaptativa según tipo de datos
    /// </summary>
    public static class AdaptiveCompression
    {
        /// <summary>
        /// Comprime con nivel adaptativo según entropía de datos
        /// </summary>
        public static byte[] CompressAdaptive(byte[] data)
        {
            var entropy = CalculateEntropy(data);

            int level;
            if (entropy < 3.0)
            {
                // Baja entropía = muy compresible (texto, JSON, etc.)
                level = 9; // Máxima compresión
            }
            else if (entropy < 5.0)
            {
                // Entropía media = moderadamente compresible
                level = 3; // Balance
            }
            else
            {
                // Alta entropía = poco compresible (ya comprimido, binario random)
                // No comprimir (overhead no vale la pena)
                return data;
            }

            var compressed = ZstdCompressionService.Compress(data, level);
            
            // Si compresión no mejora >10%, no comprimir
            if (compressed.Length > data.Length * 0.9)
                return data;

            return compressed;
        }

        /// <summary>
        /// Calcula entropía de Shannon de los datos
        /// </summary>
        private static double CalculateEntropy(byte[] data)
        {
            if (data.Length == 0)
                return 0;

            // Contar frecuencia de cada byte
            var frequencies = new int[256];
            foreach (var b in data)
                frequencies[b]++;

            // Calcular entropía
            double entropy = 0;
            foreach (var freq in frequencies)
            {
                if (freq == 0)
                    continue;

                var probability = (double)freq / data.Length;
                entropy -= probability * Math.Log2(probability);
            }

            return entropy;
        }

        /// <summary>
        /// Determina si datos son compresibles
        /// </summary>
        public static bool IsCompressible(byte[] data, int sampleSize = 1024)
        {
            // Analizar muestra para decidir rápidamente
            var sample = data.Length > sampleSize 
                ? data[..sampleSize] 
                : data;

            var entropy = CalculateEntropy(sample);
            return entropy < 6.0; // Umbral empírico
        }
    }

    /// <summary>
    /// Servicio de compresión de logs
    /// </summary>
    public class LogCompressionService
    {
        private readonly string _logDir;

        public LogCompressionService(string logDir)
        {
            _logDir = logDir;
        }

        /// <summary>
        /// Comprime logs antiguos automáticamente
        /// </summary>
        public async Task CompressOldLogsAsync(int daysOld = 7)
        {
            var cutoffDate = DateTime.Now.AddDays(-daysOld);
            var logFiles = Directory.GetFiles(_logDir, "*.log");

            foreach (var logFile in logFiles)
            {
                var fileInfo = new FileInfo(logFile);
                
                if (fileInfo.LastWriteTime < cutoffDate)
                {
                    var compressedPath = logFile + ".zst";
                    
                    // Comprimir con máxima compresión (logs son muy compresibles)
                    await ZstdCompressionService.CompressFileAsync(
                        logFile, 
                        compressedPath, 
                        level: 19);

                    // Eliminar original
                    File.Delete(logFile);

                    var originalSize = fileInfo.Length;
                    var compressedSize = new FileInfo(compressedPath).Length;
                    var ratio = ZstdCompressionService.GetCompressionRatio(
                        new byte[originalSize], 
                        new byte[compressedSize]);

                    System.Diagnostics.Debug.WriteLine(
                        $"Compressed {fileInfo.Name}: {originalSize / 1024}KB → {compressedSize / 1024}KB ({ratio:P0} reduction)");
                }
            }
        }
    }

    public class CompressionStats
    {
        public int FileCount { get; set; }
        public long TotalCompressedBytes { get; set; }
        public long TotalOriginalBytes { get; set; }
        public double CompressionRatio { get; set; }
        public double SpaceSavedMB { get; set; }
    }
}
