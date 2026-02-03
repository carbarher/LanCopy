using System;
using System.IO;
using System.Text;
using ZstdSharp;

namespace SlskDown
{
    /// <summary>
    /// Utilidades de compresión Zstandard para cachés y datos
    /// </summary>
    public static class ZstdCompression
    {
        private const int DefaultCompressionLevel = 3; // Balance entre velocidad y compresión
        private const int MaxCompressionLevel = 22;
        private const int FastCompressionLevel = 1;

        /// <summary>
        /// Comprime datos usando Zstandard
        /// </summary>
        public static byte[] Compress(byte[] data, int compressionLevel = DefaultCompressionLevel)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<byte>();

            try
            {
                using var compressor = new Compressor(compressionLevel);
                return compressor.Wrap(data).ToArray();
            }
            catch
            {
                return data; // Si falla, devolver sin comprimir
            }
        }

        /// <summary>
        /// Comprime texto usando Zstandard
        /// </summary>
        public static byte[] CompressText(string text, int compressionLevel = DefaultCompressionLevel)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<byte>();

            var bytes = Encoding.UTF8.GetBytes(text);
            return Compress(bytes, compressionLevel);
        }

        /// <summary>
        /// Descomprime datos usando Zstandard
        /// </summary>
        public static byte[] Decompress(byte[] compressedData)
        {
            if (compressedData == null || compressedData.Length == 0)
                return Array.Empty<byte>();

            try
            {
                using var decompressor = new Decompressor();
                return decompressor.Unwrap(compressedData).ToArray();
            }
            catch
            {
                return compressedData; // Si falla, asumir que no está comprimido
            }
        }

        /// <summary>
        /// Descomprime datos a texto usando Zstandard
        /// </summary>
        public static string DecompressText(byte[] compressedData)
        {
            if (compressedData == null || compressedData.Length == 0)
                return string.Empty;

            try
            {
                var decompressed = Decompress(compressedData);
                return Encoding.UTF8.GetString(decompressed);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Comprime un archivo
        /// </summary>
        public static void CompressFile(string inputPath, string outputPath, int compressionLevel = DefaultCompressionLevel)
        {
            if (!File.Exists(inputPath))
                throw new FileNotFoundException($"Archivo no encontrado: {inputPath}");

            var data = File.ReadAllBytes(inputPath);
            var compressed = Compress(data, compressionLevel);
            File.WriteAllBytes(outputPath, compressed);
        }

        /// <summary>
        /// Descomprime un archivo
        /// </summary>
        public static void DecompressFile(string inputPath, string outputPath)
        {
            if (!File.Exists(inputPath))
                throw new FileNotFoundException($"Archivo no encontrado: {inputPath}");

            var compressedData = File.ReadAllBytes(inputPath);
            var decompressed = Decompress(compressedData);
            File.WriteAllBytes(outputPath, decompressed);
        }

        /// <summary>
        /// Comprime un stream
        /// </summary>
        public static void CompressStream(Stream input, Stream output, int compressionLevel = DefaultCompressionLevel)
        {
            using var compressor = new Compressor(compressionLevel);
            using var compressionStream = new CompressionStream(output, compressor);
            input.CopyTo(compressionStream);
        }

        /// <summary>
        /// Descomprime un stream
        /// </summary>
        public static void DecompressStream(Stream input, Stream output)
        {
            using var decompressor = new Decompressor();
            using var decompressionStream = new DecompressionStream(input, decompressor);
            decompressionStream.CopyTo(output);
        }

        /// <summary>
        /// Calcula ratio de compresión
        /// </summary>
        public static double GetCompressionRatio(byte[] original, byte[] compressed)
        {
            if (original == null || original.Length == 0)
                return 0;

            if (compressed == null || compressed.Length == 0)
                return 0;

            return (double)compressed.Length / original.Length;
        }

        /// <summary>
        /// Calcula espacio ahorrado
        /// </summary>
        public static long GetSpaceSaved(byte[] original, byte[] compressed)
        {
            if (original == null || compressed == null)
                return 0;

            return original.Length - compressed.Length;
        }

        /// <summary>
        /// Comprime con nivel rápido (para datos temporales)
        /// </summary>
        public static byte[] CompressFast(byte[] data)
        {
            return Compress(data, FastCompressionLevel);
        }

        /// <summary>
        /// Comprime con nivel máximo (para archivos permanentes)
        /// </summary>
        public static byte[] CompressMax(byte[] data)
        {
            return Compress(data, MaxCompressionLevel);
        }

        /// <summary>
        /// Verifica si los datos están comprimidos con Zstandard
        /// </summary>
        public static bool IsZstdCompressed(byte[] data)
        {
            if (data == null || data.Length < 4)
                return false;

            // Magic number de Zstandard: 0x28, 0xB5, 0x2F, 0xFD
            return data[0] == 0x28 && data[1] == 0xB5 && data[2] == 0x2F && data[3] == 0xFD;
        }

        /// <summary>
        /// Comprime solo si vale la pena (ahorra al menos 10%)
        /// </summary>
        public static (byte[] Data, bool IsCompressed) CompressIfWorthwhile(byte[] data, int compressionLevel = DefaultCompressionLevel)
        {
            if (data == null || data.Length < 1024) // No comprimir datos muy pequeños
                return (data, false);

            var compressed = Compress(data, compressionLevel);
            var ratio = GetCompressionRatio(data, compressed);

            // Si ahorra menos del 10%, no vale la pena
            if (ratio > 0.9)
                return (data, false);

            return (compressed, true);
        }

        /// <summary>
        /// Descomprime automáticamente si está comprimido
        /// </summary>
        public static byte[] DecompressIfNeeded(byte[] data)
        {
            if (IsZstdCompressed(data))
                return Decompress(data);

            return data;
        }
    }
}
