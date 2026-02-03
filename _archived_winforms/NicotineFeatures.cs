using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace SlskDown
{
    /// <summary>
    /// Implementación de características avanzadas de Nicotine+
    /// </summary>
    public static class NicotineFeatures
    {
        // ═══════════════════════════════════════════════════════════════
        // 1. FUENTES ALTERNATIVAS
        // ═══════════════════════════════════════════════════════════════
        
        public static List<AlternativeSource> FindAlternativeSources(
            List<Soulseek.File> searchResults, 
            string targetFilename, 
            long targetSize,
            int maxSources = 5)
        {
            var alternatives = new List<AlternativeSource>();
            
            foreach (var result in searchResults)
            {
                // Buscar archivos con nombre similar y tamaño exacto
                if (IsSimilarFile(result.Filename, targetFilename) && 
                    Math.Abs(result.Size - targetSize) < 1024) // Tolerancia de 1KB
                {
                    alternatives.Add(new AlternativeSource
                    {
                        Username = result.Username,
                        Filename = result.Filename,
                        Size = result.Size,
                        Speed = result.Speed,
                        QueueLength = result.QueueLength,
                        HasFreeSlot = result.FreeUploadSlots > 0,
                        LastSeen = DateTime.Now,
                        Priority = CalculateSourcePriority(result)
                    });
                }
            }
            
            // Ordenar por prioridad y retornar top N
            return alternatives
                .OrderByDescending(s => s.Priority)
                .ThenBy(s => s.QueueLength ?? int.MaxValue)
                .Take(maxSources)
                .ToList();
        }
        
        private static bool IsSimilarFile(string filename1, string filename2)
        {
            var name1 = Path.GetFileNameWithoutExtension(filename1).ToLower();
            var name2 = Path.GetFileNameWithoutExtension(filename2).ToLower();
            
            // Calcular similitud de Levenshtein
            var distance = LevenshteinDistance(name1, name2);
            var maxLen = Math.Max(name1.Length, name2.Length);
            var similarity = 1.0 - (double)distance / maxLen;
            
            return similarity > 0.85; // 85% de similitud
        }
        
        private static int CalculateSourcePriority(Soulseek.File file)
        {
            int priority = 0;
            
            // Prioridad por slots libres
            if (file.FreeUploadSlots > 0) priority += 100;
            
            // Prioridad por velocidad
            if (file.Speed.HasValue && file.Speed.Value > 0)
                priority += Math.Min(file.Speed.Value / 1000, 50);
            
            // Penalización por cola larga
            if (file.QueueLength.HasValue)
                priority -= Math.Min(file.QueueLength.Value, 50);
            
            return priority;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // 2. FILTROS DE BÚSQUEDA AVANZADOS
        // ═══════════════════════════════════════════════════════════════
        
        public static List<Soulseek.File> ApplyAdvancedFilters(
            List<Soulseek.File> results,
            List<string> excludeWords,
            int minBitrate,
            int maxBitrate,
            long minSize,
            long maxSize,
            List<string> allowedExtensions)
        {
            return results.Where(file =>
            {
                // Filtro de exclusión de palabras
                if (excludeWords != null && excludeWords.Any())
                {
                    var filename = file.Filename.ToLower();
                    if (excludeWords.Any(word => filename.Contains(word.ToLower())))
                        return false;
                }
                
                // Filtro de tamaño
                if (minSize > 0 && file.Size < minSize)
                    return false;
                if (maxSize > 0 && file.Size > maxSize)
                    return false;
                
                // Filtro de bitrate (si está disponible en atributos)
                if (file.Attributes != null && file.Attributes.Any())
                {
                    var bitrateAttr = file.Attributes.FirstOrDefault(a => a.Type == Soulseek.FileAttributeType.BitRate);
                    if (bitrateAttr != null)
                    {
                        var bitrate = bitrateAttr.Value;
                        if (minBitrate > 0 && bitrate < minBitrate)
                            return false;
                        if (maxBitrate > 0 && bitrate > maxBitrate)
                            return false;
                    }
                }
                
                // Filtro de extensión
                if (allowedExtensions != null && allowedExtensions.Any())
                {
                    var ext = Path.GetExtension(file.Filename).TrimStart('.').ToLower();
                    if (!allowedExtensions.Contains(ext))
                        return false;
                }
                
                return true;
            }).ToList();
        }
        
        public static (string query, Dictionary<string, string> operators) ParseSearchQuery(string rawQuery)
        {
            var operators = new Dictionary<string, string>();
            var cleanQuery = rawQuery;
            
            // Extraer operadores de tamaño: >100MB, <500MB
            var sizePattern = @"([><])(\d+)(MB|GB|KB)";
            var sizeMatches = System.Text.RegularExpressions.Regex.Matches(rawQuery, sizePattern);
            foreach (System.Text.RegularExpressions.Match match in sizeMatches)
            {
                operators[$"size_{match.Groups[1].Value}"] = match.Value;
                cleanQuery = cleanQuery.Replace(match.Value, "");
            }
            
            // Extraer operadores de bitrate: >320kbps
            var bitratePattern = @"([><])(\d+)kbps";
            var bitrateMatches = System.Text.RegularExpressions.Regex.Matches(rawQuery, bitratePattern);
            foreach (System.Text.RegularExpressions.Match match in bitrateMatches)
            {
                operators[$"bitrate_{match.Groups[1].Value}"] = match.Value;
                cleanQuery = cleanQuery.Replace(match.Value, "");
            }
            
            // Extraer operadores de extensión: ext:flac
            var extPattern = @"ext:(\w+)";
            var extMatches = System.Text.RegularExpressions.Regex.Matches(rawQuery, extPattern);
            foreach (System.Text.RegularExpressions.Match match in extMatches)
            {
                operators["ext"] = match.Groups[1].Value;
                cleanQuery = cleanQuery.Replace(match.Value, "");
            }
            
            return (cleanQuery.Trim(), operators);
        }
        
        // ═══════════════════════════════════════════════════════════════
        // 3. AGRUPACIÓN POR ÁLBUM/CARPETA
        // ═══════════════════════════════════════════════════════════════
        
        public static Dictionary<string, FolderGroup> GroupByFolder(List<Soulseek.File> files)
        {
            var groups = new Dictionary<string, FolderGroup>();
            
            foreach (var file in files)
            {
                var folderPath = Path.GetDirectoryName(file.Filename);
                if (string.IsNullOrEmpty(folderPath))
                    continue;
                
                var key = $"{file.Username}|{folderPath}";
                
                if (!groups.ContainsKey(key))
                {
                    groups[key] = new FolderGroup
                    {
                        FolderPath = folderPath,
                        Files = new List<string>(),
                        Username = file.Username,
                        TotalSize = 0,
                        CompletedFiles = 0,
                        IsAlbum = IsLikelyAlbum(folderPath),
                        AlbumName = Path.GetFileName(folderPath)
                    };
                }
                
                groups[key].Files.Add(file.Filename);
                groups[key].TotalSize += file.Size;
            }
            
            return groups;
        }
        
        private static bool IsLikelyAlbum(string folderPath)
        {
            var folderName = Path.GetFileName(folderPath).ToLower();
            
            // Patrones comunes de álbumes
            var albumPatterns = new[] { "album", "ep", "single", "lp", "cd", "disc" };
            
            // Verificar si contiene año (1900-2099)
            var hasYear = System.Text.RegularExpressions.Regex.IsMatch(folderName, @"\b(19|20)\d{2}\b");
            
            return albumPatterns.Any(p => folderName.Contains(p)) || hasYear;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // 4. VERIFICACIÓN DE INTEGRIDAD
        // ═══════════════════════════════════════════════════════════════
        
        public static string CalculateMD5(string filePath)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }
        
        public static async Task<bool> VerifyFileIntegrity(string filePath, string expectedHash)
        {
            if (string.IsNullOrEmpty(expectedHash))
                return true; // No hay hash para verificar
            
            var actualHash = await Task.Run(() => CalculateMD5(filePath));
            return actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
        }
        
        // ═══════════════════════════════════════════════════════════════
        // 5. BALANCEO DE CARGA
        // ═══════════════════════════════════════════════════════════════
        
        public static string SelectBestSource(
            List<AlternativeSource> sources,
            Dictionary<string, UserLoadInfo> userLoadInfo,
            int maxDownloadsPerUser)
        {
            if (!sources.Any())
                return null;
            
            var availableSources = sources.Where(s =>
            {
                if (!userLoadInfo.ContainsKey(s.Username))
                    return true;
                
                var loadInfo = userLoadInfo[s.Username];
                return loadInfo.ActiveDownloads < maxDownloadsPerUser;
            }).ToList();
            
            if (!availableSources.Any())
                return sources.First().Username; // Fallback
            
            // Seleccionar fuente con mejor prioridad y menos carga
            return availableSources
                .OrderByDescending(s => s.Priority)
                .ThenBy(s => userLoadInfo.ContainsKey(s.Username) 
                    ? userLoadInfo[s.Username].ActiveDownloads 
                    : 0)
                .First()
                .Username;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // 6. SISTEMA DE RETRY INTELIGENTE
        // ═══════════════════════════════════════════════════════════════
        
        public static DateTime CalculateNextRetryTime(int attemptCount, int[] backoffMinutes)
        {
            var index = Math.Min(attemptCount, backoffMinutes.Length - 1);
            var delayMinutes = backoffMinutes[index];
            return DateTime.Now.AddMinutes(delayMinutes);
        }
        
        public static bool ShouldRetry(RetryInfo retryInfo, int maxRetries)
        {
            if (retryInfo.AttemptCount >= maxRetries)
                return false;
            
            return DateTime.Now >= retryInfo.NextRetryTime;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // 7. UTILIDADES
        // ═══════════════════════════════════════════════════════════════
        
        private static int LevenshteinDistance(string s1, string s2)
        {
            int[,] d = new int[s1.Length + 1, s2.Length + 1];
            
            for (int i = 0; i <= s1.Length; i++)
                d[i, 0] = i;
            for (int j = 0; j <= s2.Length; j++)
                d[0, j] = j;
            
            for (int j = 1; j <= s2.Length; j++)
            {
                for (int i = 1; i <= s1.Length; i++)
                {
                    int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            
            return d[s1.Length, s2.Length];
        }
        
        public static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
        
        public static string FormatSpeed(long bytesPerSecond)
        {
            return $"{FormatFileSize(bytesPerSecond)}/s";
        }
    }
}
