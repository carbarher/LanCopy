using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SlskDown.Core
{
    /// <summary>
    /// MEJORA #42: Detecta duplicados antes de descargar
    /// Verifica archivos por nombre, tamaño y hash (opcional)
    /// </summary>
    public class DuplicateDetector
    {
        private readonly HashSet<string> downloadedFiles;
        private readonly Dictionary<string, string> fileHashes; // filename -> hash
        private readonly object lockObj = new object();
        
        public DuplicateDetector()
        {
            downloadedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            fileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Registra un archivo como descargado
        /// </summary>
        public void RegisterDownload(string filename, long size, string localPath = null)
        {
            if (string.IsNullOrEmpty(filename)) return;
            
            lock (lockObj)
            {
                string key = GetFileKey(filename, size);
                downloadedFiles.Add(key);
                
                // Si existe el archivo local, calcular hash para comparaciones futuras
                if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
                {
                    try
                    {
                        string hash = CalculateFileHash(localPath);
                        fileHashes[key] = hash;
                    }
                    catch
                    {
                        // Ignorar errores de hash
                    }
                }
            }
        }
        
        /// <summary>
        /// Verifica si un archivo ya fue descargado
        /// </summary>
        public bool IsDuplicate(string filename, long size)
        {
            if (string.IsNullOrEmpty(filename)) return false;
            
            lock (lockObj)
            {
                string key = GetFileKey(filename, size);
                return downloadedFiles.Contains(key);
            }
        }
        
        /// <summary>
        /// Verifica si un archivo es duplicado comparando con archivo local
        /// </summary>
        public DuplicateCheckResult CheckDuplicate(string filename, long size, string localPath)
        {
            if (string.IsNullOrEmpty(filename))
                return new DuplicateCheckResult { IsDuplicate = false };
            
            // 1. Verificar si ya está en el registro
            string key = GetFileKey(filename, size);
            lock (lockObj)
            {
                if (downloadedFiles.Contains(key))
                {
                    return new DuplicateCheckResult
                    {
                        IsDuplicate = true,
                        Reason = "Ya registrado en historial",
                        Confidence = DuplicateConfidence.High
                    };
                }
            }
            
            // 2. Verificar si existe archivo local con mismo nombre
            if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
            {
                var fileInfo = new FileInfo(localPath);
                long sizeDiff = Math.Abs(fileInfo.Length - size);
                
                // Si el tamaño es idéntico o muy similar (±1KB)
                if (sizeDiff < 1024)
                {
                    return new DuplicateCheckResult
                    {
                        IsDuplicate = true,
                        Reason = $"Archivo existe con tamaño similar ({FormatSize(fileInfo.Length)})",
                        Confidence = DuplicateConfidence.High,
                        ExistingFilePath = localPath
                    };
                }
                
                // Si el tamaño difiere significativamente
                if (sizeDiff > 1024 * 100) // >100KB diferencia
                {
                    return new DuplicateCheckResult
                    {
                        IsDuplicate = false,
                        Reason = $"Archivo existe pero tamaño diferente ({FormatSize(fileInfo.Length)} vs {FormatSize(size)})",
                        Confidence = DuplicateConfidence.Low,
                        ExistingFilePath = localPath
                    };
                }
                
                // Tamaño similar pero no idéntico - verificar hash si está disponible
                lock (lockObj)
                {
                    if (fileHashes.TryGetValue(key, out string storedHash))
                    {
                        try
                        {
                            string currentHash = CalculateFileHash(localPath);
                            if (currentHash == storedHash)
                            {
                                return new DuplicateCheckResult
                                {
                                    IsDuplicate = true,
                                    Reason = "Hash idéntico (archivo exacto)",
                                    Confidence = DuplicateConfidence.VeryHigh,
                                    ExistingFilePath = localPath
                                };
                            }
                        }
                        catch
                        {
                            // Error calculando hash, asumir no duplicado
                        }
                    }
                }
                
                return new DuplicateCheckResult
                {
                    IsDuplicate = true,
                    Reason = $"Archivo existe con tamaño cercano ({FormatSize(fileInfo.Length)})",
                    Confidence = DuplicateConfidence.Medium,
                    ExistingFilePath = localPath
                };
            }
            
            return new DuplicateCheckResult { IsDuplicate = false };
        }
        
        /// <summary>
        /// Limpia archivos del registro que ya no existen
        /// </summary>
        public int CleanupMissingFiles(string downloadDirectory)
        {
            if (string.IsNullOrEmpty(downloadDirectory) || !Directory.Exists(downloadDirectory))
                return 0;
            
            int removed = 0;
            lock (lockObj)
            {
                var toRemove = new List<string>();
                
                foreach (var key in downloadedFiles)
                {
                    // Extraer nombre de archivo del key
                    var parts = key.Split('|');
                    if (parts.Length < 2) continue;
                    
                    string filename = parts[0];
                    string localPath = Path.Combine(downloadDirectory, filename);
                    
                    if (!File.Exists(localPath))
                    {
                        toRemove.Add(key);
                    }
                }
                
                foreach (var key in toRemove)
                {
                    downloadedFiles.Remove(key);
                    fileHashes.Remove(key);
                    removed++;
                }
            }
            
            return removed;
        }
        
        /// <summary>
        /// Obtiene estadísticas del detector
        /// </summary>
        public DuplicateDetectorStats GetStats()
        {
            lock (lockObj)
            {
                return new DuplicateDetectorStats
                {
                    TotalRegistered = downloadedFiles.Count,
                    TotalWithHashes = fileHashes.Count
                };
            }
        }
        
        /// <summary>
        /// Limpia todo el registro
        /// </summary>
        public void Clear()
        {
            lock (lockObj)
            {
                downloadedFiles.Clear();
                fileHashes.Clear();
            }
        }
        
        private string GetFileKey(string filename, long size)
        {
            // Usar nombre + tamaño como clave única
            return $"{Path.GetFileName(filename)}|{size}";
        }
        
        private string CalculateFileHash(string filePath)
        {
            // Calcular hash MD5 rápido (solo primeros 1MB para archivos grandes)
            using (var stream = File.OpenRead(filePath))
            using (var md5 = MD5.Create())
            {
                // Para archivos grandes, solo hashear el inicio
                long bytesToRead = Math.Min(stream.Length, 1024 * 1024); // 1MB
                byte[] buffer = new byte[bytesToRead];
                stream.Read(buffer, 0, (int)bytesToRead);
                
                byte[] hash = md5.ComputeHash(buffer);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
        
        private string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
    
    /// <summary>
    /// Resultado de verificación de duplicados
    /// </summary>
    public class DuplicateCheckResult
    {
        public bool IsDuplicate { get; set; }
        public string Reason { get; set; }
        public DuplicateConfidence Confidence { get; set; }
        public string ExistingFilePath { get; set; }
    }
    
    /// <summary>
    /// Nivel de confianza en la detección de duplicados
    /// </summary>
    public enum DuplicateConfidence
    {
        Low,      // Posible duplicado
        Medium,   // Probable duplicado
        High,     // Muy probable duplicado
        VeryHigh  // Duplicado confirmado (hash idéntico)
    }
    
    /// <summary>
    /// Estadísticas del detector de duplicados
    /// </summary>
    public class DuplicateDetectorStats
    {
        public int TotalRegistered { get; set; }
        public int TotalWithHashes { get; set; }
    }
}
