using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using K4os.Compression.LZ4;
using ZstdSharp;

namespace SlskDown.Core.Optimization
{
    /// <summary>
    /// Helper de compresión con múltiples algoritmos
    /// LZ4 (rápido), Zstd (mejor ratio), GZip (compatible)
    /// </summary>
    public static class CompressionHelper
    {
        public enum CompressionAlgorithm
        {
            None,
            GZip,      // Compatible, ratio medio, velocidad media
            LZ4,       // Muy rápido, ratio bajo
            Zstd       // Mejor ratio, velocidad buena
        }
        
        /// <summary>
        /// Comprime bytes con algoritmo especificado
        /// </summary>
        public static byte[] Compress(byte[] data, CompressionAlgorithm algorithm = CompressionAlgorithm.Zstd)
        {
            return algorithm switch
            {
                CompressionAlgorithm.GZip => CompressGZip(data),
                CompressionAlgorithm.LZ4 => CompressLZ4(data),
                CompressionAlgorithm.Zstd => CompressZstd(data),
                _ => data
            };
        }
        
        /// <summary>
        /// Descomprime bytes
        /// </summary>
        public static byte[] Decompress(byte[] data, CompressionAlgorithm algorithm = CompressionAlgorithm.Zstd)
        {
            return algorithm switch
            {
                CompressionAlgorithm.GZip => DecompressGZip(data),
                CompressionAlgorithm.LZ4 => DecompressLZ4(data),
                CompressionAlgorithm.Zstd => DecompressZstd(data),
                _ => data
            };
        }
        
        // GZip
        private static byte[] CompressGZip(byte[] data)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
            {
                gzip.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }
        
        private static byte[] DecompressGZip(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
        
        // LZ4
        private static byte[] CompressLZ4(byte[] data)
        {
            var maxLength = LZ4Codec.MaximumOutputSize(data.Length);
            var target = new byte[maxLength];
            var encodedLength = LZ4Codec.Encode(data, 0, data.Length, target, 0, target.Length, LZ4Level.L00_FAST);
            
            var result = new byte[encodedLength];
            Array.Copy(target, result, encodedLength);
            return result;
        }
        
        private static byte[] DecompressLZ4(byte[] data)
        {
            var target = new byte[data.Length * 10]; // Estimación
            var decodedLength = LZ4Codec.Decode(data, 0, data.Length, target, 0, target.Length);
            
            var result = new byte[decodedLength];
            Array.Copy(target, result, decodedLength);
            return result;
        }
        
        // Zstd
        private static byte[] CompressZstd(byte[] data)
        {
            using var compressor = new Compressor();
            return compressor.Wrap(data).ToArray();
        }
        
        private static byte[] DecompressZstd(byte[] data)
        {
            using var decompressor = new Decompressor();
            return decompressor.Unwrap(data).ToArray();
        }
        
        /// <summary>
        /// Comprime archivo
        /// </summary>
        public static async Task CompressFileAsync(
            string sourcePath,
            string destinationPath,
            CompressionAlgorithm algorithm = CompressionAlgorithm.Zstd)
        {
            var data = await File.ReadAllBytesAsync(sourcePath).ConfigureAwait(false);
            var compressed = Compress(data, algorithm);
            await File.WriteAllBytesAsync(destinationPath, compressed).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Descomprime archivo
        /// </summary>
        public static async Task DecompressFileAsync(
            string sourcePath,
            string destinationPath,
            CompressionAlgorithm algorithm = CompressionAlgorithm.Zstd)
        {
            var data = await File.ReadAllBytesAsync(sourcePath).ConfigureAwait(false);
            var decompressed = Decompress(data, algorithm);
            await File.WriteAllBytesAsync(destinationPath, decompressed).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Obtiene ratio de compresión
        /// </summary>
        public static double GetCompressionRatio(byte[] original, byte[] compressed)
        {
            return (1.0 - (compressed.Length / (double)original.Length)) * 100.0;
        }
        
        /// <summary>
        /// Compara algoritmos y retorna el mejor
        /// </summary>
        public static (CompressionAlgorithm Algorithm, byte[] Data, double Ratio) GetBestCompression(byte[] data)
        {
            var gzip = CompressGZip(data);
            var lz4 = CompressLZ4(data);
            var zstd = CompressZstd(data);
            
            var gzipRatio = GetCompressionRatio(data, gzip);
            var lz4Ratio = GetCompressionRatio(data, lz4);
            var zstdRatio = GetCompressionRatio(data, zstd);
            
            if (zstdRatio >= gzipRatio && zstdRatio >= lz4Ratio)
                return (CompressionAlgorithm.Zstd, zstd, zstdRatio);
            else if (gzipRatio >= lz4Ratio)
                return (CompressionAlgorithm.GZip, gzip, gzipRatio);
            else
                return (CompressionAlgorithm.LZ4, lz4, lz4Ratio);
        }
    }
}
