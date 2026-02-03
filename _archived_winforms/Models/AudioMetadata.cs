using System;
using System.Collections.Generic;

namespace SlskDown.Models
{
    /// <summary>
    /// Metadatos de audio según protocolo Soulseek
    /// Atributos: 0=Bitrate, 1=Duration, 2=VBR, 4=SampleRate, 5=BitDepth
    /// </summary>
    public class AudioMetadata
    {
        public int? Bitrate { get; set; }           // Atributo 0 (kbps)
        public int? Duration { get; set; }          // Atributo 1 (segundos)
        public bool IsVBR { get; set; }             // Atributo 2
        public int? SampleRate { get; set; }        // Atributo 4 (Hz)
        public int? BitDepth { get; set; }          // Atributo 5 (bits)

        /// <summary>
        /// Tipo de archivo inferido de los atributos
        /// </summary>
        public AudioFileType FileType
        {
            get
            {
                // Lossless: tiene sample rate y bit depth
                if (SampleRate.HasValue && BitDepth.HasValue)
                    return AudioFileType.Lossless;
                
                // Lossy: tiene bitrate
                if (Bitrate.HasValue)
                    return AudioFileType.Lossy;
                
                return AudioFileType.Unknown;
            }
        }

        /// <summary>
        /// Descripción de calidad para mostrar en UI
        /// </summary>
        public string QualityDescription
        {
            get
            {
                switch (FileType)
                {
                    case AudioFileType.Lossless:
                        return $"{SampleRate / 1000.0:F1}kHz/{BitDepth}bit";
                    
                    case AudioFileType.Lossy:
                        var vbr = IsVBR ? " VBR" : "";
                        return $"{Bitrate}kbps{vbr}";
                    
                    default:
                        return "Unknown";
                }
            }
        }

        /// <summary>
        /// Badge/emoji para mostrar en UI
        /// </summary>
        public string QualityBadge
        {
            get
            {
                switch (FileType)
                {
                    case AudioFileType.Lossless:
                        return "🎵"; // Nota musical para lossless
                    
                    case AudioFileType.Lossy:
                        if (Bitrate >= 320) return "🎵"; // Alta calidad
                        if (Bitrate >= 192) return "🎶"; // Media calidad
                        return "♪"; // Baja calidad
                    
                    default:
                        return "";
                }
            }
        }

        /// <summary>
        /// Score de calidad (0-100) para ordenar resultados
        /// </summary>
        public int QualityScore
        {
            get
            {
                switch (FileType)
                {
                    case AudioFileType.Lossless:
                        int score = 80; // Base para lossless
                        
                        // Bonus por sample rate alto
                        if (SampleRate >= 96000) score += 15;
                        else if (SampleRate >= 48000) score += 10;
                        else if (SampleRate >= 44100) score += 5;
                        
                        // Bonus por bit depth alto
                        if (BitDepth >= 24) score += 5;
                        
                        return Math.Min(100, score);
                    
                    case AudioFileType.Lossy:
                        if (Bitrate >= 320) return 70;
                        if (Bitrate >= 256) return 60;
                        if (Bitrate >= 192) return 50;
                        if (Bitrate >= 128) return 40;
                        return 30;
                    
                    default:
                        return 0;
                }
            }
        }

        /// <summary>
        /// Parsea atributos de archivo de Soulseek a AudioMetadata
        /// </summary>
        public static AudioMetadata FromFileAttributes(IReadOnlyDictionary<int, int> attributes)
        {
            if (attributes == null || attributes.Count == 0)
                return null;

            var metadata = new AudioMetadata();

            // Atributo 0: Bitrate
            if (attributes.TryGetValue(0, out var bitrate))
                metadata.Bitrate = bitrate;

            // Atributo 1: Duration
            if (attributes.TryGetValue(1, out var duration))
                metadata.Duration = duration;

            // Atributo 2: VBR
            if (attributes.TryGetValue(2, out var vbr))
                metadata.IsVBR = vbr == 1;

            // Atributo 4: Sample Rate
            if (attributes.TryGetValue(4, out var sampleRate))
                metadata.SampleRate = sampleRate;

            // Atributo 5: Bit Depth
            if (attributes.TryGetValue(5, out var bitDepth))
                metadata.BitDepth = bitDepth;

            return metadata;
        }

        /// <summary>
        /// Formatea la duración en formato legible (MM:SS o HH:MM:SS)
        /// </summary>
        public string FormattedDuration
        {
            get
            {
                if (!Duration.HasValue) return "";

                var ts = TimeSpan.FromSeconds(Duration.Value);
                return ts.Hours > 0
                    ? $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                    : $"{ts.Minutes}:{ts.Seconds:D2}";
            }
        }
    }

    public enum AudioFileType
    {
        Unknown,
        Lossy,      // MP3, OGG, AAC, etc.
        Lossless    // FLAC, WAV, APE, etc.
    }

    /// <summary>
    /// Filtros de búsqueda por calidad de audio
    /// </summary>
    public class AudioQualityFilters
    {
        public int? MinBitrate { get; set; }        // Mínimo kbps (ej: 320)
        public int? MaxBitrate { get; set; }        // Máximo kbps
        public int? MinSampleRate { get; set; }     // Mínimo Hz (ej: 44100)
        public int? MinBitDepth { get; set; }       // Mínimo bits (ej: 16)
        public bool LosslessOnly { get; set; }      // Solo FLAC/WAV/APE
        public bool ExcludeVBR { get; set; }        // Excluir VBR
        public int? MinQualityScore { get; set; }   // Score mínimo (0-100)

        /// <summary>
        /// Verifica si un archivo cumple los filtros
        /// </summary>
        public bool Matches(AudioMetadata metadata)
        {
            if (metadata == null) return true; // Sin metadata = aceptar

            // Filtro: Solo lossless
            if (LosslessOnly && metadata.FileType != AudioFileType.Lossless)
                return false;

            // Filtro: Bitrate mínimo
            if (MinBitrate.HasValue && (!metadata.Bitrate.HasValue || metadata.Bitrate < MinBitrate))
                return false;

            // Filtro: Bitrate máximo
            if (MaxBitrate.HasValue && metadata.Bitrate.HasValue && metadata.Bitrate > MaxBitrate)
                return false;

            // Filtro: Sample rate mínimo
            if (MinSampleRate.HasValue && (!metadata.SampleRate.HasValue || metadata.SampleRate < MinSampleRate))
                return false;

            // Filtro: Bit depth mínimo
            if (MinBitDepth.HasValue && (!metadata.BitDepth.HasValue || metadata.BitDepth < MinBitDepth))
                return false;

            // Filtro: Excluir VBR
            if (ExcludeVBR && metadata.IsVBR)
                return false;

            // Filtro: Quality score mínimo
            if (MinQualityScore.HasValue && metadata.QualityScore < MinQualityScore)
                return false;

            return true;
        }
    }
}
