using System;
using System.Runtime.InteropServices;

namespace SlskDown.Core
{
    /// <summary>
    /// Wrapper C# para funciones de metadata de Rust
    /// </summary>
    public static class RustMetadataWrapper
    {
        private const string DLL_NAME = "rust_core.dll";

        // ============================================================================
        // FFI DECLARATIONS
        // ============================================================================

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr extract_mp3_metadata(
            [MarshalAs(UnmanagedType.LPStr)] string filePath
        );

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr extract_flac_metadata(
            [MarshalAs(UnmanagedType.LPStr)] string filePath
        );

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr detect_language_advanced(
            [MarshalAs(UnmanagedType.LPStr)] string text
        );

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr compress_log_data(
            byte[] data,
            int length,
            out int compressedLength
        );

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr decompress_log_data(
            byte[] data,
            int length,
            out int decompressedLength
        );

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void free_rust_string(IntPtr ptr);

        // ============================================================================
        // PUBLIC API
        // ============================================================================

        /// <summary>
        /// Extrae metadata de un archivo MP3
        /// </summary>
        public static AudioMetadata ExtractMp3Metadata(string filePath)
        {
            try
            {
                var ptr = extract_mp3_metadata(filePath);
                if (ptr == IntPtr.Zero)
                    return null;

                var json = Marshal.PtrToStringAnsi(ptr);
                free_rust_string(ptr);

                return ParseAudioMetadata(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting MP3 metadata: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extrae metadata de un archivo FLAC
        /// </summary>
        public static AudioMetadata ExtractFlacMetadata(string filePath)
        {
            try
            {
                var ptr = extract_flac_metadata(filePath);
                if (ptr == IntPtr.Zero)
                    return null;

                var json = Marshal.PtrToStringAnsi(ptr);
                free_rust_string(ptr);

                return ParseAudioMetadata(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting FLAC metadata: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Detecta el idioma de un texto (mejorado con NLP)
        /// </summary>
        public static string DetectLanguageAdvanced(string text)
        {
            try
            {
                var ptr = detect_language_advanced(text);
                if (ptr == IntPtr.Zero)
                    return "unknown";

                var language = Marshal.PtrToStringAnsi(ptr);
                free_rust_string(ptr);

                return language;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting language: {ex.Message}");
                return "unknown";
            }
        }

        /// <summary>
        /// Comprime datos de log usando RLE
        /// </summary>
        public static byte[] CompressLogData(byte[] data)
        {
            try
            {
                var ptr = compress_log_data(data, data.Length, out int compressedLength);
                if (ptr == IntPtr.Zero)
                    return data;

                var compressed = new byte[compressedLength];
                Marshal.Copy(ptr, compressed, 0, compressedLength);
                
                return compressed;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error compressing log data: {ex.Message}");
                return data;
            }
        }

        /// <summary>
        /// Descomprime datos de log
        /// </summary>
        public static byte[] DecompressLogData(byte[] data)
        {
            try
            {
                var ptr = decompress_log_data(data, data.Length, out int decompressedLength);
                if (ptr == IntPtr.Zero)
                    return data;

                var decompressed = new byte[decompressedLength];
                Marshal.Copy(ptr, decompressed, 0, decompressedLength);
                
                return decompressed;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error decompressing log data: {ex.Message}");
                return data;
            }
        }

        // ============================================================================
        // HELPER METHODS
        // ============================================================================

        private static AudioMetadata ParseAudioMetadata(string json)
        {
            // Parseo simple de JSON (puedes usar System.Text.Json si prefieres)
            var metadata = new AudioMetadata();

            try
            {
                // Extraer campos del JSON
                metadata.Title = ExtractJsonField(json, "title");
                metadata.Artist = ExtractJsonField(json, "artist");
                metadata.Album = ExtractJsonField(json, "album");
                metadata.Year = ExtractJsonField(json, "year");
                metadata.Genre = ExtractJsonField(json, "genre");
                
                var bitrateStr = ExtractJsonField(json, "bitrate");
                if (int.TryParse(bitrateStr, out int bitrate))
                    metadata.Bitrate = bitrate;

                var durationStr = ExtractJsonField(json, "duration_seconds");
                if (int.TryParse(durationStr, out int duration))
                    metadata.DurationSeconds = duration;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing metadata JSON: {ex.Message}");
            }

            return metadata;
        }

        private static string ExtractJsonField(string json, string fieldName)
        {
            try
            {
                var key = $"\"{fieldName}\":";
                var startIndex = json.IndexOf(key);
                if (startIndex == -1)
                    return null;

                startIndex += key.Length;
                
                // Saltar espacios y comillas
                while (startIndex < json.Length && (json[startIndex] == ' ' || json[startIndex] == '"'))
                    startIndex++;

                var endIndex = startIndex;
                while (endIndex < json.Length && json[endIndex] != '"' && json[endIndex] != ',' && json[endIndex] != '}')
                    endIndex++;

                return json.Substring(startIndex, endIndex - startIndex).Trim();
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Metadata de audio extraída
    /// </summary>
    public class AudioMetadata
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Year { get; set; }
        public string Genre { get; set; }
        public int? Bitrate { get; set; }
        public int? DurationSeconds { get; set; }

        public override string ToString()
        {
            return $"{Artist} - {Title} ({Year}) [{Bitrate} kbps]";
        }
    }
}
