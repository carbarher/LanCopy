// <copyright file="MetadataScanner.cs" company="SlskDown">
//     Escaneo optimizado de metadata de archivos
// </copyright>

using System;
using System.IO;
using SlskDown.Models;

namespace SlskDown.Core
{
    /// <summary>
    /// Escaneo optimizado de metadata de archivos de audio.
    /// Inspirado en el get_audio_tag de Nicotine+.
    /// </summary>
    public class MetadataScanner
    {
        private const long MinimumFileSize = 128; // Skip archivos < 128 bytes
        private const uint MaxBitrate = 2000000;  // 2 Mbps máximo
        private const uint MaxSampleRate = 384000; // 384 kHz máximo
        private const uint MaxBitDepth = 32;       // 32-bit máximo
        private const uint MaxDuration = 86400;    // 24 horas máximo

        /// <summary>
        /// Escanea metadata de un archivo de audio.
        /// </summary>
        public FileMetadataStruct ScanFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return default;

            try
            {
                var fileInfo = new FileInfo(filePath);
                
                // Skip archivos sin contenido significativo
                if (!fileInfo.Exists || fileInfo.Length <= MinimumFileSize)
                    return default;

                return ExtractMetadata(filePath, fileInfo.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error escaneando metadata de {filePath}: {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// Extrae metadata usando TinyTag (similar a Nicotine+).
        /// </summary>
        private FileMetadataStruct ExtractMetadata(string filePath, long fileSize)
        {
            // TODO: Implementar con TagLib# cuando esté disponible
            // Por ahora usar método fallback
            return ExtractMetadataFallback(filePath, fileSize);
            
            /* Código para cuando TagLib# esté instalado:
            try
            {
                using var file = TagLib.File.Create(filePath);
                var properties = file.Properties;
                if (properties == null)
                    return default;

                var bitrate = properties.AudioBitrate;
                var duration = (int)properties.Duration.TotalSeconds;
                var sampleRate = properties.AudioSampleRate;
                var bitDepth = properties.BitsPerSample;

                if (!IsValidBitrate(bitrate)) bitrate = 0;
                if (!IsValidDuration(duration)) duration = 0;
                if (!IsValidSampleRate(sampleRate)) sampleRate = 0;
                if (!IsValidBitDepth(bitDepth)) bitDepth = 0;

                var isVBR = DetectVBR(file, bitrate);

                return new FileMetadataStruct(bitrate, duration, sampleRate, bitDepth, isVBR);
            }
            catch
            {
                return ExtractMetadataFallback(filePath, fileSize);
            }
            */
        }

        /// <summary>
        /// Método fallback si TagLib# falla.
        /// </summary>
        private FileMetadataStruct ExtractMetadataFallback(string filePath, long fileSize)
        {
            try
            {
                // Estimación básica basada en tamaño de archivo
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                
                // Estimaciones conservadoras
                int estimatedBitrate = 0;
                int estimatedDuration = 0;

                if (extension == ".mp3")
                {
                    // MP3: ~128-320 kbps típico
                    estimatedBitrate = 192;
                    estimatedDuration = (int)(fileSize / (estimatedBitrate * 1000 / 8));
                }
                else if (extension == ".flac")
                {
                    // FLAC: ~800-1000 kbps típico
                    estimatedBitrate = 900;
                    estimatedDuration = (int)(fileSize / (estimatedBitrate * 1000 / 8));
                }

                if (estimatedBitrate > 0 && estimatedDuration > 0)
                {
                    return new FileMetadataStruct(
                        estimatedBitrate,
                        estimatedDuration,
                        0,
                        0,
                        false
                    );
                }
            }
            catch
            {
                // Silenciar errores del fallback
            }

            return default;
        }

        /// <summary>
        /// Detecta si un archivo usa VBR.
        /// </summary>
        private bool DetectVBR(object file, int bitrate)
        {
            // TODO: Implementar cuando TagLib# esté disponible
            // Por ahora usar heurística simple
            return bitrate < 128 || bitrate > 320;
            
            /* Código para cuando TagLib# esté instalado:
            try
            {
                var tagLibFile = file as TagLib.File;
                if (tagLibFile == null) return false;
                
                var tag = tagLibFile.Tag;
                if (tag != null && tag.Comment != null && 
                    tag.Comment.Contains("VBR", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (tagLibFile.MimeType == "taglib/mp3")
                {
                    return bitrate < 128 || bitrate > 320;
                }
            }
            catch
            {
                // Ignorar errores
            }

            return false;
            */
        }

        /// <summary>
        /// Valida que el bitrate esté en un rango razonable.
        /// </summary>
        private bool IsValidBitrate(int bitrate)
        {
            return bitrate > 0 && bitrate <= MaxBitrate;
        }

        /// <summary>
        /// Valida que la duración esté en un rango razonable.
        /// </summary>
        private bool IsValidDuration(int duration)
        {
            return duration >= 0 && duration <= MaxDuration;
        }

        /// <summary>
        /// Valida que el sample rate esté en un rango razonable.
        /// </summary>
        private bool IsValidSampleRate(int sampleRate)
        {
            return sampleRate > 0 && sampleRate <= MaxSampleRate;
        }

        /// <summary>
        /// Valida que el bit depth esté en un rango razonable.
        /// </summary>
        private bool IsValidBitDepth(int bitDepth)
        {
            return bitDepth > 0 && bitDepth <= MaxBitDepth;
        }

        /// <summary>
        /// Verifica si un archivo debe ser escaneado basándose en su extensión.
        /// </summary>
        public static bool ShouldScan(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            return extension switch
            {
                ".mp3" or ".flac" or ".m4a" or ".aac" or ".ogg" or ".opus" or 
                ".wma" or ".wav" or ".ape" or ".wv" or ".tta" => true,
                _ => false
            };
        }
    }
}
