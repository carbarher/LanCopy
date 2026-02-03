using System;
using System.IO;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;

namespace SlskDown.Core.Files
{
    /// <summary>
    /// Gestor de archivos parciales para reanudar descargas inspirado en Nicotine+
    /// Permite reanudar descargas desde donde se quedaron
    /// </summary>
    public class PartialFileManager
    {
        private const string PARTIAL_EXTENSION = ".partial";
        private const string METADATA_EXTENSION = ".metadata";

        public event Action<string> OnLog;

        /// <summary>
        /// Obtiene la posición desde donde reanudar una descarga
        /// </summary>
        public async Task<long> GetResumePositionAsync(string filePath)
        {
            var partialPath = GetPartialPath(filePath);
            
            if (!File.Exists(partialPath))
                return 0;

            try
            {
                var info = new FileInfo(partialPath);
                var size = info.Length;
                
                Log($"Archivo parcial encontrado: {size:N0} bytes");
                return size;
            }
            catch (Exception ex)
            {
                Log($"Error leyendo archivo parcial: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Crea o abre un archivo parcial para escritura
        /// </summary>
        public FileStream OpenPartialFile(string filePath, long resumePosition = 0)
        {
            var partialPath = GetPartialPath(filePath);
            
            try
            {
                // Crear directorio si no existe
                var directory = Path.GetDirectoryName(partialPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Abrir o crear archivo
                var stream = new FileStream(
                    partialPath,
                    FileMode.OpenOrCreate,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920  // 80 KB buffer
                );

                // Posicionar en el punto de reanudación
                if (resumePosition > 0)
                {
                    stream.Seek(resumePosition, SeekOrigin.Begin);
                    Log($"Reanudando desde posición: {resumePosition:N0} bytes");
                }

                return stream;
            }
            catch (Exception ex)
            {
                Log($"Error abriendo archivo parcial: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Completa una descarga moviendo el archivo parcial al final
        /// </summary>
        public async Task<bool> CompleteDownloadAsync(string filePath)
        {
            var partialPath = GetPartialPath(filePath);
            
            if (!File.Exists(partialPath))
            {
                Log($"Archivo parcial no encontrado: {partialPath}");
                return false;
            }

            try
            {
                // Verificar que el directorio de destino existe
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Si el archivo final ya existe, eliminarlo
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                // Mover archivo parcial al final
                File.Move(partialPath, filePath);
                
                // Eliminar metadata si existe
                var metadataPath = GetMetadataPath(filePath);
                if (File.Exists(metadataPath))
                {
                    File.Delete(metadataPath);
                }

                Log($"Descarga completada: {Path.GetFileName(filePath)}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Error completando descarga: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Cancela una descarga eliminando el archivo parcial
        /// </summary>
        public bool CancelDownload(string filePath)
        {
            var partialPath = GetPartialPath(filePath);
            
            try
            {
                if (File.Exists(partialPath))
                {
                    File.Delete(partialPath);
                    Log($"Archivo parcial eliminado: {Path.GetFileName(partialPath)}");
                }

                var metadataPath = GetMetadataPath(filePath);
                if (File.Exists(metadataPath))
                {
                    File.Delete(metadataPath);
                }

                return true;
            }
            catch (Exception ex)
            {
                Log($"Error cancelando descarga: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Guarda metadata de la descarga
        /// </summary>
        public async Task SaveMetadataAsync(string filePath, DownloadMetadata metadata)
        {
            var metadataPath = GetMetadataPath(filePath);
            
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(metadataPath, json);
            }
            catch (Exception ex)
            {
                Log($"Error guardando metadata: {ex.Message}");
            }
        }

        /// <summary>
        /// Carga metadata de la descarga
        /// </summary>
        public async Task<DownloadMetadata> LoadMetadataAsync(string filePath)
        {
            var metadataPath = GetMetadataPath(filePath);
            
            if (!File.Exists(metadataPath))
                return null;

            try
            {
                var json = await File.ReadAllTextAsync(metadataPath);
                return System.Text.Json.JsonSerializer.Deserialize<DownloadMetadata>(json);
            }
            catch (Exception ex)
            {
                Log($"Error cargando metadata: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Verifica la integridad de un archivo parcial
        /// </summary>
        public async Task<bool> VerifyPartialFileAsync(string filePath, long expectedSize)
        {
            var partialPath = GetPartialPath(filePath);
            
            if (!File.Exists(partialPath))
                return false;

            try
            {
                var info = new FileInfo(partialPath);
                
                // Verificar que el tamaño no exceda el esperado
                if (info.Length > expectedSize)
                {
                    Log($"Archivo parcial corrupto (tamaño excedido): {info.Length} > {expectedSize}");
                    return false;
                }

                // Verificar que el archivo es accesible
                using var stream = File.OpenRead(partialPath);
                
                return true;
            }
            catch (Exception ex)
            {
                Log($"Error verificando archivo parcial: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Calcula el checksum de un archivo
        /// </summary>
        public async Task<string> CalculateChecksumAsync(string filePath)
        {
            try
            {
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                var hash = await sha256.ComputeHashAsync(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception ex)
            {
                Log($"Error calculando checksum: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Limpia archivos parciales antiguos
        /// </summary>
        public int CleanupOldPartialFiles(string directory, TimeSpan maxAge)
        {
            if (!Directory.Exists(directory))
                return 0;

            int cleaned = 0;
            var cutoff = DateTime.UtcNow - maxAge;

            try
            {
                var partialFiles = Directory.GetFiles(directory, $"*{PARTIAL_EXTENSION}", SearchOption.AllDirectories);
                
                foreach (var file in partialFiles)
                {
                    var info = new FileInfo(file);
                    if (info.LastWriteTimeUtc < cutoff)
                    {
                        try
                        {
                            File.Delete(file);
                            cleaned++;
                            
                            // Eliminar metadata asociada
                            var metadataPath = file.Replace(PARTIAL_EXTENSION, METADATA_EXTENSION);
                            if (File.Exists(metadataPath))
                            {
                                File.Delete(metadataPath);
                            }
                        }
                        catch
                        {
                            // Ignorar errores individuales
                        }
                    }
                }

                if (cleaned > 0)
                {
                    Log($"Limpiados {cleaned} archivos parciales antiguos");
                }
            }
            catch (Exception ex)
            {
                Log($"Error limpiando archivos parciales: {ex.Message}");
            }

            return cleaned;
        }

        private string GetPartialPath(string filePath)
        {
            return filePath + PARTIAL_EXTENSION;
        }

        private string GetMetadataPath(string filePath)
        {
            return filePath + METADATA_EXTENSION;
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
    }

    /// <summary>
    /// Metadata de descarga
    /// </summary>
    public class DownloadMetadata
    {
        public string FileName { get; set; }
        public string Username { get; set; }
        public long TotalSize { get; set; }
        public long BytesDownloaded { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? LastUpdated { get; set; }
        public string Checksum { get; set; }
        public int RetryCount { get; set; }
    }
}
