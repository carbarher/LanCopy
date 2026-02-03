using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// MEJORA #16: Verificación de integridad de archivos descargados
    /// Calcula checksums (SHA256) para verificar que los archivos descargados
    /// no estén corruptos y permite re-descarga automática si fallan la verificación.
    /// </summary>
    public class FileIntegrityVerifier
    {
        private readonly ConcurrentDictionary<string, string> checksumCache;
        private readonly int maxCacheSize;
        
        public FileIntegrityVerifier(int maxCacheSize = 1000)
        {
            this.checksumCache = new ConcurrentDictionary<string, string>();
            this.maxCacheSize = maxCacheSize;
        }
        
        /// <summary>
        /// Calcula el checksum SHA256 de un archivo
        /// </summary>
        public async Task<string> CalculateChecksumAsync(string filePath, CancellationToken ct = default)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Archivo no encontrado: {filePath}");
            
            // Verificar si está en caché
            if (checksumCache.TryGetValue(filePath, out var cachedChecksum))
            {
                // Verificar que el archivo no haya sido modificado
                var fileInfo = new FileInfo(filePath);
                var cacheKey = $"{filePath}_{fileInfo.Length}_{fileInfo.LastWriteTimeUtc.Ticks}";
                
                if (checksumCache.TryGetValue(cacheKey, out var validChecksum))
                {
                    return validChecksum;
                }
            }
            
            // Calcular checksum
            using (var sha256 = SHA256.Create())
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true))
            {
                var hashBytes = await Task.Run(() => sha256.ComputeHash(stream), ct);
                var checksum = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                
                // Guardar en caché con metadata del archivo
                var fileInfo = new FileInfo(filePath);
                var cacheKey = $"{filePath}_{fileInfo.Length}_{fileInfo.LastWriteTimeUtc.Ticks}";
                
                if (checksumCache.Count >= maxCacheSize)
                {
                    // Limpiar caché si está lleno (eliminar 10% más antiguo)
                    var toRemove = checksumCache.Count / 10;
                    var removed = 0;
                    foreach (var key in checksumCache.Keys)
                    {
                        if (removed >= toRemove) break;
                        checksumCache.TryRemove(key, out _);
                        removed++;
                    }
                }
                
                checksumCache.TryAdd(cacheKey, checksum);
                checksumCache.TryAdd(filePath, checksum); // También guardar por path simple
                
                return checksum;
            }
        }
        
        /// <summary>
        /// Verifica la integridad básica de un archivo (tamaño, lectura)
        /// </summary>
        public async Task<FileIntegrityResult> VerifyFileIntegrityAsync(string filePath, long? expectedSize = null, CancellationToken ct = default)
        {
            var result = new FileIntegrityResult
            {
                FilePath = filePath,
                IsValid = false,
                Timestamp = DateTime.UtcNow
            };
            
            try
            {
                if (!File.Exists(filePath))
                {
                    result.ErrorMessage = "Archivo no existe";
                    return result;
                }
                
                var fileInfo = new FileInfo(filePath);
                result.ActualSize = fileInfo.Length;
                
                // Verificar tamaño mínimo (archivos de 0 bytes son inválidos)
                if (fileInfo.Length == 0)
                {
                    result.ErrorMessage = "Archivo vacío (0 bytes)";
                    return result;
                }
                
                // Verificar tamaño esperado si se proporciona
                if (expectedSize.HasValue && fileInfo.Length != expectedSize.Value)
                {
                    result.ErrorMessage = $"Tamaño incorrecto: esperado {expectedSize.Value:N0} bytes, actual {fileInfo.Length:N0} bytes";
                    return result;
                }
                
                // Intentar leer el archivo para verificar que no está corrupto
                try
                {
                    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
                    {
                        var buffer = new byte[4096];
                        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                        
                        if (bytesRead == 0 && stream.Length > 0)
                        {
                            result.ErrorMessage = "No se pudo leer el archivo";
                            return result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorMessage = $"Error al leer archivo: {ex.Message}";
                    return result;
                }
                
                // Calcular checksum
                result.Checksum = await CalculateChecksumAsync(filePath, ct);
                result.IsValid = true;
                
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Error en verificación: {ex.Message}";
                return result;
            }
        }
        
        /// <summary>
        /// Verifica si dos archivos son idénticos comparando sus checksums
        /// </summary>
        public async Task<bool> AreFilesIdenticalAsync(string filePath1, string filePath2, CancellationToken ct = default)
        {
            if (!File.Exists(filePath1) || !File.Exists(filePath2))
                return false;
            
            // Comparación rápida por tamaño primero
            var file1Info = new FileInfo(filePath1);
            var file2Info = new FileInfo(filePath2);
            
            if (file1Info.Length != file2Info.Length)
                return false;
            
            // Comparación por checksum
            var checksum1 = await CalculateChecksumAsync(filePath1, ct);
            var checksum2 = await CalculateChecksumAsync(filePath2, ct);
            
            return checksum1.Equals(checksum2, StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Verifica un archivo contra un checksum conocido
        /// </summary>
        public async Task<bool> VerifyChecksumAsync(string filePath, string expectedChecksum, CancellationToken ct = default)
        {
            if (!File.Exists(filePath))
                return false;
            
            var actualChecksum = await CalculateChecksumAsync(filePath, ct);
            return actualChecksum.Equals(expectedChecksum, StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Limpia el caché de checksums
        /// </summary>
        public void ClearCache()
        {
            checksumCache.Clear();
        }
        
        /// <summary>
        /// Obtiene estadísticas del caché
        /// </summary>
        public (int count, int maxSize) GetCacheStats()
        {
            return (checksumCache.Count, maxCacheSize);
        }
    }
    
    /// <summary>
    /// Resultado de la verificación de integridad de un archivo
    /// </summary>
    public class FileIntegrityResult
    {
        public string FilePath { get; set; }
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public string Checksum { get; set; }
        public long ActualSize { get; set; }
        public DateTime Timestamp { get; set; }
        
        public override string ToString()
        {
            if (IsValid)
            {
                return $"✅ Válido | {ActualSize:N0} bytes | SHA256: {Checksum?.Substring(0, 16)}...";
            }
            else
            {
                return $"Inválido | {ErrorMessage}";
            }
        }
    }
}
