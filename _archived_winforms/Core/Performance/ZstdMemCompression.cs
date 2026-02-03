using System;
using System.IO;
using ZstdSharp;

namespace SlskDown.Core.Performance
{
    public static class ZstdMemCompression
    {
        private static readonly Compressor _compressor = new Compressor(3);
        private static readonly Decompressor _decompressor = new Decompressor();

        public static byte[] Compress(byte[] data)
        {
            return _compressor.Wrap(data).ToArray();
        }

        public static byte[] Decompress(byte[] compressedData)
        {
            return _decompressor.Unwrap(compressedData).ToArray();
        }

        public static string CompressString(string text)
        {
            var data = System.Text.Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(Compress(data));
        }

        public static string DecompressString(string compressedText)
        {
            var data = Convert.FromBase64String(compressedText);
            return System.Text.Encoding.UTF8.GetString(Decompress(data));
        }
    }
}
