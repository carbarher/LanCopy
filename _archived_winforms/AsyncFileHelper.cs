using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// Helper para operaciones de archivo asíncronas optimizadas
    /// </summary>
    public static class AsyncFileHelper
    {
        private const int DefaultBufferSize = 4096;
        
        /// <summary>
        /// Lee un archivo de texto de forma asíncrona
        /// </summary>
        public static async Task<string> ReadAllTextAsync(
            string path, 
            CancellationToken cancellationToken = default)
        {
            using var metrics = PerformanceMetrics.Instance.Track("File.ReadAllText");
            
            if (!File.Exists(path))
                return string.Empty;
            
            using var stream = new FileStream(
                path, 
                FileMode.Open, 
                FileAccess.Read, 
                FileShare.Read,
                DefaultBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Escribe un archivo de texto de forma asíncrona
        /// </summary>
        public static async Task WriteAllTextAsync(
            string path, 
            string contents,
            CancellationToken cancellationToken = default)
        {
            using var metrics = PerformanceMetrics.Instance.Track("File.WriteAllText");
            
            // Crear directorio si no existe
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // Escribir a archivo temporal primero
            var tempPath = path + ".tmp";
            
            using (var stream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                DefaultBufferSize,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                using var writer = new StreamWriter(stream, Encoding.UTF8);
                await writer.WriteAsync(contents.AsMemory(), cancellationToken).ConfigureAwait(false);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            
            // Mover archivo temporal al destino
            File.Move(tempPath, path, overwrite: true);
        }
        
        /// <summary>
        /// Agrega texto a un archivo de forma asíncrona
        /// </summary>
        public static async Task AppendAllTextAsync(
            string path,
            string contents,
            CancellationToken cancellationToken = default)
        {
            using var metrics = PerformanceMetrics.Instance.Track("File.AppendAllText");
            
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            using var stream = new FileStream(
                path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                DefaultBufferSize,
                FileOptions.Asynchronous);
            
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            await writer.WriteAsync(contents.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Serializa un objeto a JSON y lo guarda de forma asíncrona
        /// </summary>
        public static async Task SaveJsonAsync<T>(
            string path,
            T obj,
            JsonSerializerOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            using var metrics = PerformanceMetrics.Instance.Track("File.SaveJson");
            
            options ??= new JsonSerializerOptions { WriteIndented = true };
            
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var tempPath = path + ".tmp";
            
            using (var stream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                DefaultBufferSize,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, obj, options, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            
            File.Move(tempPath, path, overwrite: true);
        }
        
        /// <summary>
        /// Carga un objeto desde JSON de forma asíncrona
        /// </summary>
        public static async Task<T?> LoadJsonAsync<T>(
            string path,
            JsonSerializerOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            using var metrics = PerformanceMetrics.Instance.Track("File.LoadJson");
            
            if (!File.Exists(path))
                return default;
            
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                DefaultBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            
            return await JsonSerializer.DeserializeAsync<T>(stream, options, cancellationToken)
                .ConfigureAwait(false);
        }
        
        /// <summary>
        /// Copia un archivo de forma asíncrona con progreso
        /// </summary>
        public static async Task CopyFileAsync(
            string sourcePath,
            string destinationPath,
            IProgress<long>? progress = null,
            CancellationToken cancellationToken = default)
        {
            using var metrics = PerformanceMetrics.Instance.Track("File.Copy");
            
            const int bufferSize = 81920; // 80KB buffer
            
            using var sourceStream = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            
            using var destinationStream = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            
            var buffer = new byte[bufferSize];
            long totalBytesRead = 0;
            int bytesRead;
            
            while ((bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken)
                .ConfigureAwait(false)) > 0)
            {
                await destinationStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                    .ConfigureAwait(false);
                
                totalBytesRead += bytesRead;
                progress?.Report(totalBytesRead);
            }
        }
        
        /// <summary>
        /// Verifica si un archivo existe de forma segura
        /// </summary>
        public static bool SafeFileExists(string path)
        {
            try
            {
                return !string.IsNullOrEmpty(path) && File.Exists(path);
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Elimina un archivo de forma segura
        /// </summary>
        public static bool SafeDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
