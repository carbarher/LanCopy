using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Soulseek;

namespace SlskDown.Core
{
    /// <summary>
    /// Motor de búsqueda agregada - agrupa resultados por popularidad
    /// Inspirado en slsk-batchdl aggregate mode
    /// </summary>
    public class AggregateSearchEngine
    {
        private readonly SoulseekClient client;
        private readonly Action<string> logFunc;
        
        public int MinSharesThreshold { get; set; } = 2;
        
        public AggregateSearchEngine(SoulseekClient soulseekClient, Action<string> log = null)
        {
            client = soulseekClient;
            logFunc = log;
        }
        
        /// <summary>
        /// Resultado agregado con popularidad
        /// </summary>
        public class AggregatedResult
        {
            public string Title { get; set; }
            public string Artist { get; set; }
            public string Album { get; set; }
            public List<Soulseek.File> Files { get; set; } = new List<Soulseek.File>();
            public int ShareCount { get; set; }
            public double AverageQuality { get; set; }
            public Soulseek.File BestFile { get; set; }
        }
        
        /// <summary>
        /// Busca y agrupa resultados por canción única
        /// </summary>
        public async Task<List<AggregatedResult>> SearchAndAggregateByTrack(
            string query,
            int maxResults = 100)
        {
            logFunc?.Invoke($"Búsqueda agregada: {query}");
            
            var searchResponse = await client.SearchAsync(
                SearchQuery.FromText(query),
                options: new SearchOptions(
                    filterResponses: false,
                    responseLimit: maxResults
                )
            );
            
            var allFiles = searchResponse.Responses
                .SelectMany(r => r.Files)
                .ToList();
            
            // Agrupar por título normalizado
            var grouped = allFiles
                .GroupBy(f => NormalizeTitle(System.IO.Path.GetFileNameWithoutExtension(f.Filename)))
                .Select(g => new AggregatedResult
                {
                    Title = g.Key,
                    Files = g.ToList(),
                    ShareCount = g.Count(),
                    AverageQuality = g.Average(f => CalculateQuality(f)),
                    BestFile = g.OrderByDescending(f => CalculateQuality(f)).First()
                })
                .Where(r => r.ShareCount >= MinSharesThreshold)
                .OrderByDescending(r => r.ShareCount)
                .ThenByDescending(r => r.AverageQuality)
                .ToList();
            
            logFunc?.Invoke($"Encontradas {grouped.Count} canciones únicas (min {MinSharesThreshold} shares)");
            
            return grouped;
        }
        
        /// <summary>
        /// Busca y agrupa resultados por álbum único
        /// </summary>
        public async Task<List<AggregatedResult>> SearchAndAggregateByAlbum(
            string query,
            int maxResults = 100)
        {
            logFunc?.Invoke($"Búsqueda agregada de álbumes: {query}");
            
            var searchResponse = await client.SearchAsync(
                SearchQuery.FromText(query),
                options: new SearchOptions(
                    filterResponses: false,
                    responseLimit: maxResults
                )
            );
            
            var allFiles = searchResponse.Responses
                .SelectMany(r => r.Files)
                .ToList();
            
            // Agrupar por directorio (álbum)
            var grouped = allFiles
                .GroupBy(f => NormalizeAlbumPath(System.IO.Path.GetDirectoryName(f.Filename)))
                .Select(g => new AggregatedResult
                {
                    Album = g.Key,
                    Files = g.ToList(),
                    ShareCount = g.Count(), // Número de archivos compartidos
                    AverageQuality = g.Average(f => CalculateQuality(f)),
                    BestFile = g.OrderByDescending(f => CalculateQuality(f)).First()
                })
                .Where(r => r.ShareCount >= MinSharesThreshold && r.Files.Count >= 3)
                .OrderByDescending(r => r.ShareCount)
                .ThenByDescending(r => r.Files.Count)
                .ToList();
            
            logFunc?.Invoke($"Encontrados {grouped.Count} álbumes únicos");
            
            return grouped;
        }
        
        /// <summary>
        /// Normaliza título para agrupación
        /// </summary>
        private string NormalizeTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
                return "";
            
            // Remover números de track, extensiones, etc.
            var normalized = title.ToLower()
                .Replace("_", " ")
                .Replace("-", " ")
                .Trim();
            
            // Remover números al inicio (01, 02, etc.)
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"^\d+\s*", "");
            
            // Remover feat/ft
            normalized = System.Text.RegularExpressions.Regex.Replace(
                normalized, 
                @"\s*[\(\[]?\s*(feat|ft|featuring)\.?\s+.*?[\)\]]?\s*$", 
                "", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            
            return normalized.Trim();
        }
        
        /// <summary>
        /// Normaliza path de álbum
        /// </summary>
        private string NormalizeAlbumPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "";
            
            // Tomar solo el último directorio (nombre del álbum)
            var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return "";
            
            var albumName = parts[parts.Length - 1].ToLower().Trim();
            
            // Remover años entre paréntesis
            albumName = System.Text.RegularExpressions.Regex.Replace(
                albumName, 
                @"\s*[\(\[]?\s*\d{4}\s*[\)\]]?\s*", 
                ""
            );
            
            return albumName.Trim();
        }
        
        /// <summary>
        /// Calcula calidad de archivo
        /// </summary>
        private double CalculateQuality(Soulseek.File file)
        {
            double score = 0;
            
            // Tamaño (más grande = mejor calidad generalmente)
            score += Math.Min(file.Size / (1024.0 * 1024.0), 50); // Max 50 puntos
            
            // Extensión
            var ext = System.IO.Path.GetExtension(file.Filename)?.ToLower();
            if (ext == ".flac" || ext == ".ape" || ext == ".wav")
                score += 50; // Lossless
            else if (ext == ".mp3" || ext == ".ogg")
                score += 30; // Lossy de calidad
            else if (ext == ".m4a" || ext == ".aac")
                score += 25;
            
            // Bitrate si está disponible (de atributos)
            if (file.Attributes != null)
            {
                var bitrate = file.Attributes
                    .Where(a => a.Type == FileAttributeType.BitRate)
                    .Select(a => a.Value)
                    .FirstOrDefault();
                
                if (bitrate > 0)
                {
                    score += Math.Min(bitrate / 10.0, 50); // Max 50 puntos
                }
            }
            
            return score;
        }
        
        /// <summary>
        /// Obtiene discografía completa de un artista
        /// </summary>
        public async Task<List<AggregatedResult>> GetArtistDiscography(
            string artistName,
            bool albumsOnly = true)
        {
            if (albumsOnly)
            {
                return await SearchAndAggregateByAlbum(artistName);
            }
            else
            {
                return await SearchAndAggregateByTrack(artistName);
            }
        }
    }
}
