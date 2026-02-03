using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace SlskDown.Services
{
    /// <summary>
    /// Verificador de integridad de archivos descargados
    /// Calcula y verifica checksums MD5/SHA256
    /// </summary>
    public class FileIntegrityChecker
    {
        public event Action<string> OnLog;

        /// <summary>
        /// Verifica la integridad de un archivo descargado
        /// </summary>
        public async Task<IntegrityCheckResult> VerifyFileAsync(string filePath, string expectedHash = null, HashAlgorithmType algorithm = HashAlgorithmType.MD5)
        {
            var result = new IntegrityCheckResult
            {
                FilePath = filePath,
                Algorithm = algorithm,
                CheckTime = DateTime.Now
            };

            try
            {
                if (!File.Exists(filePath))
                {
                    result.Success = false;
                    result.ErrorMessage = "Archivo no encontrado";
                    return result;
                }

                var fileInfo = new FileInfo(filePath);
                result.FileSize = fileInfo.Length;

                // Verificar tamaño mínimo
                if (fileInfo.Length == 0)
                {
                    result.Success = false;
                    result.ErrorMessage = "Archivo vacío (0 bytes)";
                    OnLog?.Invoke($"Archivo vacío: {filePath}");
                    return result;
                }

                // Calcular hash
                result.CalculatedHash = await ComputeHashAsync(filePath, algorithm);

                // Verificar contra hash esperado si se proporciona
                if (!string.IsNullOrEmpty(expectedHash))
                {
                    result.ExpectedHash = expectedHash.ToUpperInvariant();
                    result.Success = result.CalculatedHash.Equals(result.ExpectedHash, StringComparison.OrdinalIgnoreCase);
                    
                    if (result.Success)
                    {
                        OnLog?.Invoke($"Integridad verificada: {Path.GetFileName(filePath)}");
                    }
                    else
                    {
                        OnLog?.Invoke($"Hash no coincide: {Path.GetFileName(filePath)}");
                        OnLog?.Invoke($"   Esperado: {result.ExpectedHash}");
                        OnLog?.Invoke($"   Obtenido: {result.CalculatedHash}");
                    }
                }
                else
                {
                    // Sin hash esperado, solo calcular
                    result.Success = true;
                    OnLog?.Invoke($"Hash calculado para {Path.GetFileName(filePath)}: {result.CalculatedHash}");
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                OnLog?.Invoke($"Error verificando integridad: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Calcula el hash de un archivo
        /// </summary>
        public async Task<string> ComputeHashAsync(string filePath, HashAlgorithmType algorithm = HashAlgorithmType.MD5)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
            
            HashAlgorithm hashAlgorithm = algorithm switch
            {
                HashAlgorithmType.MD5 => MD5.Create(),
                HashAlgorithmType.SHA1 => SHA1.Create(),
                HashAlgorithmType.SHA256 => SHA256.Create(),
                HashAlgorithmType.SHA512 => SHA512.Create(),
                _ => MD5.Create()
            };

            using (hashAlgorithm)
            {
                var hashBytes = await hashAlgorithm.ComputeHashAsync(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();
            }
        }

        /// <summary>
        /// Verifica múltiples archivos en batch
        /// </summary>
        public async Task<BatchIntegrityResult> VerifyMultipleFilesAsync(string[] filePaths, HashAlgorithmType algorithm = HashAlgorithmType.MD5)
        {
            var batchResult = new BatchIntegrityResult
            {
                TotalFiles = filePaths.Length,
                StartTime = DateTime.Now
            };

            foreach (var filePath in filePaths)
            {
                var result = await VerifyFileAsync(filePath, null, algorithm);
                
                if (result.Success)
                {
                    batchResult.SuccessCount++;
                }
                else
                {
                    batchResult.FailureCount++;
                }

                batchResult.Results.Add(result);
            }

            batchResult.EndTime = DateTime.Now;
            batchResult.Duration = batchResult.EndTime - batchResult.StartTime;

            return batchResult;
        }

        /// <summary>
        /// Detecta archivos corruptos o incompletos
        /// </summary>
        public async Task<bool> IsFileCorruptedAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return true;

                var fileInfo = new FileInfo(filePath);
                
                // Archivo vacío = corrupto
                if (fileInfo.Length == 0)
                    return true;

                // Intentar leer el archivo completo
                using var stream = File.OpenRead(filePath);
                var buffer = new byte[8192];
                long totalRead = 0;

                while (true)
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                        break;
                    totalRead += bytesRead;
                }

                // Verificar que se leyó todo el archivo
                return totalRead != fileInfo.Length;
            }
            catch
            {
                return true; // Error al leer = corrupto
            }
        }

        /// <summary>
        /// Genera un reporte de integridad para un directorio
        /// </summary>
        public async Task<DirectoryIntegrityReport> GenerateDirectoryReportAsync(string directoryPath, string searchPattern = "*.*")
        {
            var report = new DirectoryIntegrityReport
            {
                DirectoryPath = directoryPath,
                ScanTime = DateTime.Now
            };

            if (!Directory.Exists(directoryPath))
            {
                report.ErrorMessage = "Directorio no encontrado";
                return report;
            }

            var files = Directory.GetFiles(directoryPath, searchPattern, SearchOption.AllDirectories);
            report.TotalFiles = files.Length;

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                report.TotalSize += fileInfo.Length;

                var isCorrupted = await IsFileCorruptedAsync(file);
                if (isCorrupted)
                {
                    report.CorruptedFiles.Add(file);
                }
                else
                {
                    report.ValidFiles++;
                }
            }

            return report;
        }
    }

    public class IntegrityCheckResult
    {
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public HashAlgorithmType Algorithm { get; set; }
        public string ExpectedHash { get; set; }
        public string CalculatedHash { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime CheckTime { get; set; }

        public bool HashMatches => !string.IsNullOrEmpty(ExpectedHash) && 
                                   !string.IsNullOrEmpty(CalculatedHash) &&
                                   ExpectedHash.Equals(CalculatedHash, StringComparison.OrdinalIgnoreCase);
    }

    public class BatchIntegrityResult
    {
        public int TotalFiles { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public System.Collections.Generic.List<IntegrityCheckResult> Results { get; set; } = new System.Collections.Generic.List<IntegrityCheckResult>();

        public double SuccessRate => TotalFiles > 0 ? (SuccessCount * 100.0 / TotalFiles) : 0;
    }

    public class DirectoryIntegrityReport
    {
        public string DirectoryPath { get; set; }
        public int TotalFiles { get; set; }
        public long TotalSize { get; set; }
        public int ValidFiles { get; set; }
        public System.Collections.Generic.List<string> CorruptedFiles { get; set; } = new System.Collections.Generic.List<string>();
        public DateTime ScanTime { get; set; }
        public string ErrorMessage { get; set; }

        public int CorruptedCount => CorruptedFiles.Count;
        public string TotalSizeMB => $"{TotalSize / (1024.0 * 1024.0):F2} MB";
    }

    public enum HashAlgorithmType
    {
        MD5,
        SHA1,
        SHA256,
        SHA512
    }
}
