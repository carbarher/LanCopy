using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Servicio para trabajar con archivos grandes usando Memory-Mapped Files
    /// Permite acceder a archivos de GB sin cargarlos completamente en RAM
    /// </summary>
    public static class MemoryMappedFileService
    {
        /// <summary>
        /// Lee archivo grande usando memory-mapped file
        /// </summary>
        public static async Task<string> ReadLargeFileAsync(string filePath, long offset = 0, long length = -1)
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
                throw new FileNotFoundException("File not found", filePath);

            if (length == -1)
                length = fileInfo.Length - offset;

            using var mmf = MemoryMappedFile.CreateFromFile(
                filePath, 
                FileMode.Open, 
                null, 
                0, 
                MemoryMappedFileAccess.Read);

            using var accessor = mmf.CreateViewAccessor(offset, length, MemoryMappedFileAccess.Read);

            var buffer = new byte[length];
            accessor.ReadArray(0, buffer, 0, (int)length);

            return await Task.Run(() => Encoding.UTF8.GetString(buffer));
        }

        /// <summary>
        /// Escribe en archivo grande usando memory-mapped file
        /// </summary>
        public static async Task WriteLargeFileAsync(string filePath, string content, long offset = 0)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var fileSize = Math.Max(offset + bytes.Length, new FileInfo(filePath).Length);

            using var mmf = MemoryMappedFile.CreateFromFile(
                filePath,
                FileMode.OpenOrCreate,
                null,
                fileSize,
                MemoryMappedFileAccess.ReadWrite);

            using var accessor = mmf.CreateViewAccessor(offset, bytes.Length, MemoryMappedFileAccess.Write);

            await Task.Run(() => accessor.WriteArray(0, bytes, 0, bytes.Length));
        }

        /// <summary>
        /// Busca patrón en archivo grande sin cargarlo completo
        /// </summary>
        public static async Task<long> FindPatternInLargeFileAsync(
            string filePath, 
            byte[] pattern,
            int chunkSize = 1024 * 1024) // 1MB chunks
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
                return -1;

            using var mmf = MemoryMappedFile.CreateFromFile(
                filePath,
                FileMode.Open,
                null,
                0,
                MemoryMappedFileAccess.Read);

            return await Task.Run(() =>
            {
                long fileSize = fileInfo.Length;
                long position = 0;

                while (position < fileSize)
                {
                    var readSize = Math.Min(chunkSize, fileSize - position);
                    using var accessor = mmf.CreateViewAccessor(position, readSize, MemoryMappedFileAccess.Read);

                    var buffer = new byte[readSize];
                    accessor.ReadArray(0, buffer, 0, (int)readSize);

                    // Buscar patrón en chunk
                    var index = FindPattern(buffer, pattern);
                    if (index >= 0)
                        return position + index;

                    position += readSize - pattern.Length + 1; // Overlap para no perder matches
                }

                return -1L;
            });
        }

        private static int FindPattern(byte[] buffer, byte[] pattern)
        {
            for (int i = 0; i <= buffer.Length - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (buffer[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Copia archivo grande eficientemente usando memory-mapped files
        /// </summary>
        public static async Task CopyLargeFileAsync(
            string sourcePath,
            string destPath,
            IProgress<double>? progress = null,
            int chunkSize = 10 * 1024 * 1024) // 10MB chunks
        {
            var sourceInfo = new FileInfo(sourcePath);
            if (!sourceInfo.Exists)
                throw new FileNotFoundException("Source file not found", sourcePath);

            var fileSize = sourceInfo.Length;

            // Crear archivo destino con tamaño correcto
            using (var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
            {
                fs.SetLength(fileSize);
            }

            using var sourceMmf = MemoryMappedFile.CreateFromFile(
                sourcePath,
                FileMode.Open,
                null,
                0,
                MemoryMappedFileAccess.Read);

            using var destMmf = MemoryMappedFile.CreateFromFile(
                destPath,
                FileMode.Open,
                null,
                fileSize,
                MemoryMappedFileAccess.Write);

            await Task.Run(() =>
            {
                long position = 0;
                var buffer = new byte[chunkSize];

                while (position < fileSize)
                {
                    var readSize = (int)Math.Min(chunkSize, fileSize - position);

                    using var sourceAccessor = sourceMmf.CreateViewAccessor(
                        position, readSize, MemoryMappedFileAccess.Read);
                    using var destAccessor = destMmf.CreateViewAccessor(
                        position, readSize, MemoryMappedFileAccess.Write);

                    sourceAccessor.ReadArray(0, buffer, 0, readSize);
                    destAccessor.WriteArray(0, buffer, 0, readSize);

                    position += readSize;

                    progress?.Report((double)position / fileSize * 100);
                }
            });
        }
    }

    /// <summary>
    /// Caché de archivos grandes usando memory-mapped files
    /// </summary>
    public class LargeFileCache : IDisposable
    {
        private readonly string _cacheFilePath;
        private readonly long _maxSize;
        private MemoryMappedFile? _mmf;
        private long _currentSize;

        public LargeFileCache(string cacheFilePath, long maxSizeMB = 1024)
        {
            _cacheFilePath = cacheFilePath;
            _maxSize = maxSizeMB * 1024 * 1024;
            _currentSize = 0;

            // Crear archivo de caché
            if (!File.Exists(_cacheFilePath))
            {
                using var fs = new FileStream(_cacheFilePath, FileMode.Create, FileAccess.Write);
                fs.SetLength(_maxSize);
            }

            _mmf = MemoryMappedFile.CreateFromFile(
                _cacheFilePath,
                FileMode.Open,
                null,
                _maxSize,
                MemoryMappedFileAccess.ReadWrite);
        }

        /// <summary>
        /// Escribe datos en caché
        /// </summary>
        public void Write(long offset, byte[] data)
        {
            if (_mmf == null)
                throw new ObjectDisposedException(nameof(LargeFileCache));

            if (offset + data.Length > _maxSize)
                throw new ArgumentException("Data exceeds cache size");

            using var accessor = _mmf.CreateViewAccessor(
                offset, data.Length, MemoryMappedFileAccess.Write);
            
            accessor.WriteArray(0, data, 0, data.Length);
            _currentSize = Math.Max(_currentSize, offset + data.Length);
        }

        /// <summary>
        /// Lee datos de caché
        /// </summary>
        public byte[] Read(long offset, int length)
        {
            if (_mmf == null)
                throw new ObjectDisposedException(nameof(LargeFileCache));

            if (offset + length > _currentSize)
                throw new ArgumentException("Reading beyond written data");

            using var accessor = _mmf.CreateViewAccessor(
                offset, length, MemoryMappedFileAccess.Read);

            var buffer = new byte[length];
            accessor.ReadArray(0, buffer, 0, length);
            return buffer;
        }

        /// <summary>
        /// Limpia caché
        /// </summary>
        public void Clear()
        {
            _currentSize = 0;
        }

        public void Dispose()
        {
            _mmf?.Dispose();
            _mmf = null;
        }
    }

    /// <summary>
    /// Procesador de archivos grandes por chunks
    /// </summary>
    public class LargeFileProcessor
    {
        private readonly int _chunkSize;

        public LargeFileProcessor(int chunkSizeMB = 10)
        {
            _chunkSize = chunkSizeMB * 1024 * 1024;
        }

        /// <summary>
        /// Procesa archivo grande por chunks
        /// </summary>
        public async Task ProcessFileAsync(
            string filePath,
            Func<byte[], long, Task> processChunk,
            IProgress<double>? progress = null)
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
                throw new FileNotFoundException("File not found", filePath);

            var fileSize = fileInfo.Length;

            using var mmf = MemoryMappedFile.CreateFromFile(
                filePath,
                FileMode.Open,
                null,
                0,
                MemoryMappedFileAccess.Read);

            long position = 0;

            while (position < fileSize)
            {
                var readSize = (int)Math.Min(_chunkSize, fileSize - position);

                using var accessor = mmf.CreateViewAccessor(
                    position, readSize, MemoryMappedFileAccess.Read);

                var buffer = new byte[readSize];
                accessor.ReadArray(0, buffer, 0, readSize);

                await processChunk(buffer, position);

                position += readSize;
                progress?.Report((double)position / fileSize * 100);
            }
        }

        /// <summary>
        /// Cuenta líneas en archivo grande
        /// </summary>
        public async Task<long> CountLinesAsync(string filePath)
        {
            long lineCount = 0;

            await ProcessFileAsync(filePath, async (chunk, offset) =>
            {
                await Task.Run(() =>
                {
                    foreach (var b in chunk)
                    {
                        if (b == '\n')
                            lineCount++;
                    }
                });
            });

            return lineCount;
        }

        /// <summary>
        /// Calcula hash de archivo grande
        /// </summary>
        public async Task<string> CalculateHashAsync(string filePath)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hashBytes = new byte[0];

            await ProcessFileAsync(filePath, async (chunk, offset) =>
            {
                await Task.Run(() =>
                {
                    if (offset == 0)
                    {
                        // Primer chunk
                        hashBytes = md5.ComputeHash(chunk);
                    }
                    else
                    {
                        // Chunks subsecuentes: combinar con hash anterior
                        var combined = new byte[hashBytes.Length + chunk.Length];
                        Buffer.BlockCopy(hashBytes, 0, combined, 0, hashBytes.Length);
                        Buffer.BlockCopy(chunk, 0, combined, hashBytes.Length, chunk.Length);
                        hashBytes = md5.ComputeHash(combined);
                    }
                });
            });

            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
