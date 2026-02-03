using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace SlskDown.Core
{
    /// <summary>
    /// MEJORA #33: Utilidad para compresión de archivos de datos
    /// Usa Brotli para máxima compresión con buena velocidad
    /// </summary>
    public static class CompressionHelper
    {
        private const string COMPRESSED_EXTENSION = ".br";
        
        /// <summary>
        /// Guarda datos JSON comprimidos con Brotli
        /// </summary>
        public static void SaveCompressedJson<T>(string filePath, T data, JsonSerializerOptions options = null)
        {
            try
            {
                // Serializar a bytes
                var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(data, options);
                
                // Comprimir y guardar
                var compressedPath = filePath + COMPRESSED_EXTENSION;
                using (var fileStream = File.Create(compressedPath))
                using (var brotliStream = new BrotliStream(fileStream, CompressionLevel.Optimal))
                {
                    brotliStream.Write(jsonBytes, 0, jsonBytes.Length);
                }
                
                // Opcional: guardar también versión sin comprimir para compatibilidad
                // (comentar si solo se quiere versión comprimida)
                File.WriteAllBytes(filePath, jsonBytes);
            }
            catch (Exception ex)
            {
                throw new IOException($"Error al guardar archivo comprimido: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Carga datos JSON comprimidos con Brotli
        /// Intenta primero cargar versión comprimida, luego sin comprimir
        /// </summary>
        public static T LoadCompressedJson<T>(string filePath, JsonSerializerOptions options = null)
        {
            try
            {
                var compressedPath = filePath + COMPRESSED_EXTENSION;
                
                // Intentar cargar versión comprimida primero
                if (File.Exists(compressedPath))
                {
                    using (var fileStream = File.OpenRead(compressedPath))
                    using (var brotliStream = new BrotliStream(fileStream, CompressionMode.Decompress))
                    using (var memoryStream = new MemoryStream())
                    {
                        brotliStream.CopyTo(memoryStream);
                        var jsonBytes = memoryStream.ToArray();
                        return JsonSerializer.Deserialize<T>(jsonBytes, options);
                    }
                }
                
                // Fallback: cargar versión sin comprimir
                if (File.Exists(filePath))
                {
                    var jsonBytes = File.ReadAllBytes(filePath);
                    return JsonSerializer.Deserialize<T>(jsonBytes, options);
                }
                
                return default(T);
            }
            catch (Exception ex)
            {
                throw new IOException($"Error al cargar archivo comprimido: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Guarda texto comprimido con Brotli
        /// </summary>
        public static void SaveCompressedText(string filePath, string text, Encoding encoding = null)
        {
            try
            {
                encoding = encoding ?? Encoding.UTF8;
                var textBytes = encoding.GetBytes(text);
                
                var compressedPath = filePath + COMPRESSED_EXTENSION;
                using (var fileStream = File.Create(compressedPath))
                using (var brotliStream = new BrotliStream(fileStream, CompressionLevel.Optimal))
                {
                    brotliStream.Write(textBytes, 0, textBytes.Length);
                }
                
                // Opcional: guardar también versión sin comprimir
                File.WriteAllText(filePath, text, encoding);
            }
            catch (Exception ex)
            {
                throw new IOException($"Error al guardar texto comprimido: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Carga texto comprimido con Brotli
        /// </summary>
        public static string LoadCompressedText(string filePath, Encoding encoding = null)
        {
            try
            {
                encoding = encoding ?? Encoding.UTF8;
                var compressedPath = filePath + COMPRESSED_EXTENSION;
                
                // Intentar cargar versión comprimida primero
                if (File.Exists(compressedPath))
                {
                    using (var fileStream = File.OpenRead(compressedPath))
                    using (var brotliStream = new BrotliStream(fileStream, CompressionMode.Decompress))
                    using (var memoryStream = new MemoryStream())
                    {
                        brotliStream.CopyTo(memoryStream);
                        var textBytes = memoryStream.ToArray();
                        return encoding.GetString(textBytes);
                    }
                }
                
                // Fallback: cargar versión sin comprimir
                if (File.Exists(filePath))
                {
                    return File.ReadAllText(filePath, encoding);
                }
                
                return null;
            }
            catch (Exception ex)
            {
                throw new IOException($"Error al cargar texto comprimido: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Comprime un archivo existente
        /// </summary>
        public static long CompressFile(string filePath, bool deleteOriginal = false)
        {
            try
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"Archivo no encontrado: {filePath}");
                
                var compressedPath = filePath + COMPRESSED_EXTENSION;
                long originalSize = 0;
                long compressedSize = 0;
                
                using (var inputStream = File.OpenRead(filePath))
                using (var outputStream = File.Create(compressedPath))
                using (var brotliStream = new BrotliStream(outputStream, CompressionLevel.Optimal))
                {
                    originalSize = inputStream.Length;
                    inputStream.CopyTo(brotliStream);
                    brotliStream.Flush();
                    compressedSize = outputStream.Length;
                }
                
                if (deleteOriginal)
                {
                    File.Delete(filePath);
                }
                
                return originalSize - compressedSize; // Bytes ahorrados
            }
            catch (Exception ex)
            {
                throw new IOException($"Error al comprimir archivo: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Descomprime un archivo
        /// </summary>
        public static void DecompressFile(string compressedPath, string outputPath)
        {
            try
            {
                if (!File.Exists(compressedPath))
                    throw new FileNotFoundException($"Archivo comprimido no encontrado: {compressedPath}");
                
                using (var inputStream = File.OpenRead(compressedPath))
                using (var brotliStream = new BrotliStream(inputStream, CompressionMode.Decompress))
                using (var outputStream = File.Create(outputPath))
                {
                    brotliStream.CopyTo(outputStream);
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"Error al descomprimir archivo: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Obtiene el ratio de compresión de un archivo
        /// </summary>
        public static double GetCompressionRatio(string filePath)
        {
            try
            {
                var compressedPath = filePath + COMPRESSED_EXTENSION;
                
                if (!File.Exists(filePath) || !File.Exists(compressedPath))
                    return 0;
                
                var originalSize = new FileInfo(filePath).Length;
                var compressedSize = new FileInfo(compressedPath).Length;
                
                if (originalSize == 0)
                    return 0;
                
                return (1.0 - ((double)compressedSize / originalSize)) * 100.0;
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// Verifica si existe una versión comprimida del archivo
        /// </summary>
        public static bool HasCompressedVersion(string filePath)
        {
            return File.Exists(filePath + COMPRESSED_EXTENSION);
        }
        
        /// <summary>
        /// Elimina la versión comprimida de un archivo
        /// </summary>
        public static void DeleteCompressedVersion(string filePath)
        {
            try
            {
                var compressedPath = filePath + COMPRESSED_EXTENSION;
                if (File.Exists(compressedPath))
                {
                    File.Delete(compressedPath);
                }
            }
            catch
            {
                // Ignorar errores al eliminar
            }
        }
    }
}
