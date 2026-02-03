using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// MEJORA #43: Verificador de integridad de archivos usando checksums
    /// Calcula y verifica hashes SHA256 para detectar archivos corruptos o incompletos
    /// </summary>
    public class FileIntegrityChecker
    {
        // Caché de checksums calculados: filepath -> hash
        private readonly ConcurrentDictionary<string, string> checksumCache;
        
        // Caché de archivos verificados: filepath -> (isValid, timestamp)
        private readonly ConcurrentDictionary<string, (bool isValid, DateTime timestamp)> verificationCache;
        
        // Tiempo de validez de la caché (24 horas)
        private readonly TimeSpan cacheValidityPeriod = TimeSpan.FromHours(24);
        
        // MEJORA: Referencia a la base de datos para persistencia
        private SlskDown.Database.SlskDatabase database;
        
        public FileIntegrityChecker(SlskDown.Database.SlskDatabase database = null)
        {
            checksumCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            verificationCache = new ConcurrentDictionary<string, (bool, DateTime)>(StringComparer.OrdinalIgnoreCase);
            this.database = database;
        }
        
        /// <summary>
        /// Configura la base de datos para persistencia de checksums
        /// </summary>
        public void SetDatabase(SlskDown.Database.SlskDatabase db)
        {
            this.database = db;
        }
        
        /// <summary>
        /// Calcula el hash SHA256 de un archivo
        /// </summary>
        public async Task<string> CalculateChecksumAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;
            
            // MEJORA: Verificar base de datos primero (persistente)
            if (database != null)
            {
                try
                {
                    var dbChecksum = await database.GetChecksumAsync(filePath);
                    if (!string.IsNullOrEmpty(dbChecksum))
                    {
                        // Guardar en caché en memoria para acceso rápido
                        checksumCache[filePath] = dbChecksum;
                        return dbChecksum;
                    }
                }
                catch
                {
                    // Si falla la DB, continuar con cálculo normal
                }
            }
            
            // Verificar caché en memoria
            if (checksumCache.TryGetValue(filePath, out string cachedHash))
            {
                // Verificar que el archivo no haya sido modificado
                var fileInfo = new FileInfo(filePath);
                var cacheKey = $"{filePath}_{fileInfo.Length}_{fileInfo.LastWriteTimeUtc.Ticks}";
                
                if (checksumCache.TryGetValue(cacheKey, out string validHash))
                    return validHash;
            }
            
            try
            {
                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    var hashBytes = await sha256.ComputeHashAsync(stream);
                    var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    
                    // Guardar en caché con metadata del archivo
                    var fileInfo = new FileInfo(filePath);
                    var cacheKey = $"{filePath}_{fileInfo.Length}_{fileInfo.LastWriteTimeUtc.Ticks}";
                    checksumCache[cacheKey] = hash;
                    checksumCache[filePath] = hash; // También guardar por ruta simple
                    
                    // MEJORA: Guardar en base de datos para persistencia
                    if (database != null)
                    {
                        try
                        {
                            await database.SaveChecksumAsync(filePath, fileInfo.Name, fileInfo.Length, hash);
                        }
                        catch
                        {
                            // Si falla guardar en DB, no es crítico
                        }
                    }
                    
                    return hash;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
        
        /// <summary>
        /// Verifica si un archivo está corrupto comparando con un checksum esperado
        /// </summary>
        public async Task<bool> VerifyIntegrityAsync(string filePath, string expectedChecksum)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return false;
            
            if (string.IsNullOrWhiteSpace(expectedChecksum))
                return true; // Si no hay checksum esperado, asumir válido
            
            var actualChecksum = await CalculateChecksumAsync(filePath);
            if (actualChecksum == null)
                return false;
            
            return actualChecksum.Equals(expectedChecksum, StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Verifica la integridad básica de un archivo (tamaño, lectura)
        /// </summary>
        public async Task<(bool isValid, string reason)> VerifyFileBasicIntegrityAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return (false, "Ruta vacía");
            
            if (!File.Exists(filePath))
                return (false, "Archivo no existe");
            
            // Verificar caché
            if (verificationCache.TryGetValue(filePath, out var cached))
            {
                if (DateTime.Now - cached.timestamp < cacheValidityPeriod)
                    return (cached.isValid, cached.isValid ? "Válido (caché)" : "Inválido (caché)");
            }
            
            try
            {
                var fileInfo = new FileInfo(filePath);
                
                // 1. Verificar tamaño
                if (fileInfo.Length == 0)
                {
                    verificationCache[filePath] = (false, DateTime.Now);
                    return (false, "Archivo vacío (0 bytes)");
                }
                
                // 2. Verificar que se puede leer
                using (var stream = File.OpenRead(filePath))
                {
                    // Intentar leer el primer byte
                    if (stream.ReadByte() == -1)
                    {
                        verificationCache[filePath] = (false, DateTime.Now);
                        return (false, "No se puede leer el archivo");
                    }
                    
                    // Intentar leer el último byte
                    if (stream.Length > 1)
                    {
                        stream.Seek(-1, SeekOrigin.End);
                        if (stream.ReadByte() == -1)
                        {
                            verificationCache[filePath] = (false, DateTime.Now);
                            return (false, "Archivo truncado");
                        }
                    }
                }
                
                // 3. Verificar extensión válida
                var extension = Path.GetExtension(filePath);
                if (string.IsNullOrEmpty(extension))
                {
                    verificationCache[filePath] = (false, DateTime.Now);
                    return (false, "Sin extensión");
                }
                
                // Todo OK
                verificationCache[filePath] = (true, DateTime.Now);
                return (true, "Válido");
            }
            catch (UnauthorizedAccessException)
            {
                verificationCache[filePath] = (false, DateTime.Now);
                return (false, "Acceso denegado");
            }
            catch (IOException ex)
            {
                verificationCache[filePath] = (false, DateTime.Now);
                return (false, $"Error I/O: {ex.Message}");
            }
            catch (Exception ex)
            {
                verificationCache[filePath] = (false, DateTime.Now);
                return (false, $"Error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Verifica múltiples archivos en paralelo
        /// </summary>
        public async Task<ConcurrentDictionary<string, (bool isValid, string reason)>> VerifyMultipleFilesAsync(string[] filePaths, int maxParallelism = 4)
        {
            var results = new ConcurrentDictionary<string, (bool, string)>(StringComparer.OrdinalIgnoreCase);
            
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxParallelism
            };
            
            await Parallel.ForEachAsync(filePaths, options, async (filePath, ct) =>
            {
                var result = await VerifyFileBasicIntegrityAsync(filePath);
                results[filePath] = result;
            });
            
            return results;
        }
        
        /// <summary>
        /// Limpia la caché de verificaciones antiguas
        /// </summary>
        public void CleanupCache()
        {
            var now = DateTime.Now;
            var keysToRemove = new System.Collections.Generic.List<string>();
            
            foreach (var kvp in verificationCache)
            {
                if (now - kvp.Value.timestamp > cacheValidityPeriod)
                    keysToRemove.Add(kvp.Key);
            }
            
            foreach (var key in keysToRemove)
            {
                verificationCache.TryRemove(key, out _);
            }
        }
        
        /// <summary>
        /// Obtiene estadísticas de la caché
        /// </summary>
        public (int checksumCacheSize, int verificationCacheSize) GetCacheStats()
        {
            return (checksumCache.Count, verificationCache.Count);
        }
        
        /// <summary>
        /// Limpia toda la caché
        /// </summary>
        public void ClearCache()
        {
            checksumCache.Clear();
            verificationCache.Clear();
        }
    }
}
