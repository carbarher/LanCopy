using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// CompresiÃ³n automÃ¡tica de logs antiguos
    /// </summary>
    public class LogCompressor
    {
        private readonly string _logsDirectory;
        private readonly int _daysBeforeCompress;
        private readonly int _daysBeforeDelete;

        public LogCompressor(string logsDirectory, int daysBeforeCompress = 7, int daysBeforeDelete = 30)
        {
            _logsDirectory = logsDirectory;
            _daysBeforeCompress = daysBeforeCompress;
            _daysBeforeDelete = daysBeforeDelete;
        }

        /// <summary>
        /// Comprime logs antiguos y elimina los muy antiguos
        /// </summary>
        public async Task<(int compressed, int deleted, long savedBytes)> CompressOldLogsAsync()
        {
            if (!Directory.Exists(_logsDirectory))
                return (0, 0, 0);

            int compressed = 0;
            int deleted = 0;
            long savedBytes = 0;

            var now = DateTime.Now;
            var compressCutoff = now.AddDays(-_daysBeforeCompress);
            var deleteCutoff = now.AddDays(-_daysBeforeDelete);

            var logFiles = Directory.GetFiles(_logsDirectory, "slskdown-*.txt")
                .Select(f => new FileInfo(f))
                .Where(f => f.LastWriteTime < compressCutoff)
                .ToList();

            foreach (var file in logFiles)
            {
                try
                {
                    // Eliminar si es muy antiguo
                    if (file.LastWriteTime < deleteCutoff)
                    {
                        var gzFile = file.FullName + ".gz";
                        if (File.Exists(gzFile))
                        {
                            File.Delete(gzFile);
                        }
                        file.Delete();
                        deleted++;
                        continue;
                    }

                    // Comprimir si no estÃ¡ comprimido
                    var compressedFile = file.FullName + ".gz";
                    if (!File.Exists(compressedFile))
                    {
                        long originalSize = file.Length;
                        await CompressFileAsync(file.FullName, compressedFile);
                        
                        var compressedInfo = new FileInfo(compressedFile);
                        savedBytes += originalSize - compressedInfo.Length;
                        
                        // Eliminar original despuÃ©s de comprimir
                        file.Delete();
                        compressed++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error comprimiendo {file.Name}: {ex.Message}");
                }
            }

            return (compressed, deleted, savedBytes);
        }

        /// <summary>
        /// Comprime un archivo usando GZip
        /// </summary>
        private async Task CompressFileAsync(string sourceFile, string compressedFile)
        {
            using (var sourceStream = File.OpenRead(sourceFile))
            using (var destStream = File.Create(compressedFile))
            using (var gzipStream = new GZipStream(destStream, CompressionLevel.Optimal))
            {
                await sourceStream.CopyToAsync(gzipStream);
            }
        }

        /// <summary>
        /// Descomprime un archivo GZip
        /// </summary>
        public async Task<string> DecompressFileAsync(string compressedFile)
        {
            var outputFile = compressedFile.Replace(".gz", "");
            
            using (var sourceStream = File.OpenRead(compressedFile))
            using (var gzipStream = new GZipStream(sourceStream, CompressionMode.Decompress))
            using (var destStream = File.Create(outputFile))
            {
                await gzipStream.CopyToAsync(destStream);
            }

            return outputFile;
        }

        /// <summary>
        /// Obtiene estadÃ­sticas de logs
        /// </summary>
        public (int total, int compressed, long totalSize, long compressedSize) GetStats()
        {
            if (!Directory.Exists(_logsDirectory))
                return (0, 0, 0, 0);

            var txtFiles = Directory.GetFiles(_logsDirectory, "slskdown-*.txt");
            var gzFiles = Directory.GetFiles(_logsDirectory, "slskdown-*.txt.gz");

            long totalSize = txtFiles.Sum(f => new FileInfo(f).Length);
            long compressedSize = gzFiles.Sum(f => new FileInfo(f).Length);

            return (txtFiles.Length, gzFiles.Length, totalSize, compressedSize);
        }

        /// <summary>
        /// Calcula el ratio de compresiÃ³n
        /// </summary>
        public double GetCompressionRatio()
        {
            var stats = GetStats();
            if (stats.totalSize == 0)
                return 0;

            return (double)stats.compressedSize / stats.totalSize;
        }
    }
}

