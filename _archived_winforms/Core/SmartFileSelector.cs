using System;
using System.Collections.Generic;
using System.Linq;
using Soulseek;

namespace SlskDown.Core
{
    /// <summary>
    /// Selector inteligente de archivos basado en condiciones
    /// Inspirado en slsk-batchdl file conditions
    /// </summary>
    public class SmartFileSelector
    {
        public class FileConditions
        {
            // Condiciones requeridas
            public List<string> RequiredFormats { get; set; } = new List<string>();
            public int? MinBitrate { get; set; }
            public int? MaxBitrate { get; set; }
            public long? MinSize { get; set; }
            public long? MaxSize { get; set; }
            public int? MinSampleRate { get; set; }
            public int? MaxSampleRate { get; set; }
            public TimeSpan? LengthTolerance { get; set; }
            public TimeSpan? ExpectedLength { get; set; }
            
            // Condiciones preferidas
            public List<string> PreferredFormats { get; set; } = new List<string> { "mp3" };
            public int? PrefMinBitrate { get; set; } = 200;
            public int? PrefMaxBitrate { get; set; } = 2500;
            public int? PrefMaxSampleRate { get; set; } = 48000;
            public TimeSpan? PrefLengthTolerance { get; set; } = TimeSpan.FromSeconds(3);
            public bool StrictTitle { get; set; } = false;
            public bool StrictArtist { get; set; } = false;
            public bool StrictAlbum { get; set; } = false;
            public bool PrefStrictTitle { get; set; } = true;
            public bool PrefStrictAlbum { get; set; } = true;
            
            // Contexto de búsqueda
            public string SearchTitle { get; set; }
            public string SearchArtist { get; set; }
            public string SearchAlbum { get; set; }
        }
        
        /// <summary>
        /// Evalúa si un archivo cumple condiciones requeridas
        /// </summary>
        public bool MeetsRequiredConditions(Soulseek.File file, FileConditions conditions)
        {
            // Formato
            if (conditions.RequiredFormats.Any())
            {
                var ext = System.IO.Path.GetExtension(file.Filename)?.ToLower().TrimStart('.');
                if (!conditions.RequiredFormats.Contains(ext))
                    return false;
            }
            
            // Tamaño
            if (conditions.MinSize.HasValue && file.Size < conditions.MinSize.Value)
                return false;
            
            if (conditions.MaxSize.HasValue && file.Size > conditions.MaxSize.Value)
                return false;
            
            // Atributos de audio
            if (file.Attributes != null)
            {
                var bitrate = GetBitrate(file);
                var sampleRate = GetSampleRate(file);
                var length = GetLength(file);
                
                if (conditions.MinBitrate.HasValue && bitrate < conditions.MinBitrate.Value)
                    return false;
                
                if (conditions.MaxBitrate.HasValue && bitrate > conditions.MaxBitrate.Value)
                    return false;
                
                if (conditions.MinSampleRate.HasValue && sampleRate < conditions.MinSampleRate.Value)
                    return false;
                
                if (conditions.MaxSampleRate.HasValue && sampleRate > conditions.MaxSampleRate.Value)
                    return false;
                
                if (conditions.ExpectedLength.HasValue && conditions.LengthTolerance.HasValue)
                {
                    var diff = Math.Abs((length - conditions.ExpectedLength.Value).TotalSeconds);
                    if (diff > conditions.LengthTolerance.Value.TotalSeconds)
                        return false;
                }
            }
            
            // Strict matching
            if (conditions.StrictTitle && !string.IsNullOrEmpty(conditions.SearchTitle))
            {
                if (!ContainsIgnoreCase(file.Filename, conditions.SearchTitle))
                    return false;
            }
            
            if (conditions.StrictArtist && !string.IsNullOrEmpty(conditions.SearchArtist))
            {
                if (!ContainsIgnoreCase(file.Filename, conditions.SearchArtist))
                    return false;
            }
            
            if (conditions.StrictAlbum && !string.IsNullOrEmpty(conditions.SearchAlbum))
            {
                if (!ContainsIgnoreCase(file.Filename, conditions.SearchAlbum))
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Calcula score de preferencia para un archivo
        /// </summary>
        public double CalculatePreferenceScore(Soulseek.File file, FileConditions conditions)
        {
            double score = 0;
            int criteriaCount = 0;
            
            // Formato preferido
            if (conditions.PreferredFormats.Any())
            {
                var ext = System.IO.Path.GetExtension(file.Filename)?.ToLower().TrimStart('.');
                if (conditions.PreferredFormats.Contains(ext))
                    score += 1;
                criteriaCount++;
            }
            
            var bitrate = GetBitrate(file);
            var sampleRate = GetSampleRate(file);
            var length = GetLength(file);
            
            // Bitrate preferido
            if (conditions.PrefMinBitrate.HasValue && conditions.PrefMaxBitrate.HasValue)
            {
                if (bitrate >= conditions.PrefMinBitrate.Value && bitrate <= conditions.PrefMaxBitrate.Value)
                    score += 1;
                criteriaCount++;
            }
            
            // Sample rate preferido
            if (conditions.PrefMaxSampleRate.HasValue)
            {
                if (sampleRate <= conditions.PrefMaxSampleRate.Value)
                    score += 1;
                criteriaCount++;
            }
            
            // Length tolerance preferido
            if (conditions.ExpectedLength.HasValue && conditions.PrefLengthTolerance.HasValue)
            {
                var diff = Math.Abs((length - conditions.ExpectedLength.Value).TotalSeconds);
                if (diff <= conditions.PrefLengthTolerance.Value.TotalSeconds)
                    score += 1;
                criteriaCount++;
            }
            
            // Strict title preferido
            if (conditions.PrefStrictTitle && !string.IsNullOrEmpty(conditions.SearchTitle))
            {
                if (ContainsIgnoreCase(file.Filename, conditions.SearchTitle))
                    score += 1;
                criteriaCount++;
            }
            
            // Strict album preferido
            if (conditions.PrefStrictAlbum && !string.IsNullOrEmpty(conditions.SearchAlbum))
            {
                if (ContainsIgnoreCase(file.Filename, conditions.SearchAlbum))
                    score += 1;
                criteriaCount++;
            }
            
            return criteriaCount > 0 ? score / criteriaCount : 0;
        }
        
        /// <summary>
        /// Selecciona el mejor archivo de una lista
        /// </summary>
        public Soulseek.File SelectBestFile(List<Soulseek.File> files, FileConditions conditions)
        {
            // Filtrar por condiciones requeridas
            var validFiles = files.Where(f => MeetsRequiredConditions(f, conditions)).ToList();
            
            if (!validFiles.Any())
                return null;
            
            // Ordenar por score de preferencia
            return validFiles
                .OrderByDescending(f => CalculatePreferenceScore(f, conditions))
                .ThenByDescending(f => GetBitrate(f))
                .ThenByDescending(f => f.Size)
                .First();
        }
        
        /// <summary>
        /// Filtra y ordena archivos
        /// </summary>
        public List<Soulseek.File> FilterAndSort(List<Soulseek.File> files, FileConditions conditions)
        {
            return files
                .Where(f => MeetsRequiredConditions(f, conditions))
                .OrderByDescending(f => CalculatePreferenceScore(f, conditions))
                .ThenByDescending(f => GetBitrate(f))
                .ThenByDescending(f => f.Size)
                .ToList();
        }
        
        private int GetBitrate(Soulseek.File file)
        {
            return file.Attributes?
                .Where(a => a.Type == FileAttributeType.BitRate)
                .Select(a => a.Value)
                .FirstOrDefault() ?? 0;
        }
        
        private int GetSampleRate(Soulseek.File file)
        {
            return file.Attributes?
                .Where(a => a.Type == FileAttributeType.SampleRate)
                .Select(a => a.Value)
                .FirstOrDefault() ?? 0;
        }
        
        private TimeSpan GetLength(Soulseek.File file)
        {
            var seconds = file.Attributes?
                .Where(a => a.Type == FileAttributeType.Length)
                .Select(a => a.Value)
                .FirstOrDefault() ?? 0;
            
            return TimeSpan.FromSeconds(seconds);
        }
        
        private bool ContainsIgnoreCase(string text, string search)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(search))
                return false;
            
            return text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        
        /// <summary>
        /// Crea condiciones predefinidas para música lossless
        /// </summary>
        public static FileConditions CreateLosslessConditions()
        {
            return new FileConditions
            {
                RequiredFormats = new List<string> { "flac", "ape", "wav" },
                MinBitrate = 900,
                MinSize = 20 * 1024 * 1024 // 20 MB
            };
        }
        
        /// <summary>
        /// Crea condiciones predefinidas para MP3 de alta calidad
        /// </summary>
        public static FileConditions CreateHighQualityMP3Conditions()
        {
            return new FileConditions
            {
                RequiredFormats = new List<string> { "mp3" },
                MinBitrate = 320,
                PrefMinBitrate = 320,
                PrefMaxBitrate = 320
            };
        }
    }
}
