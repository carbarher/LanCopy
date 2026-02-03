using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Métodos de extensión para NetworkOrchestrator
    /// Proporciona APIs simplificadas para búsquedas multi-red
    /// </summary>
    public static class NetworkOrchestratorExtensions
    {
        /// <summary>
        /// Realiza búsqueda multi-red con query simple (string)
        /// </summary>
        public static async Task<List<NetworkSearchResult>> SearchAsync(
            this NetworkOrchestrator orchestrator,
            string query,
            int maxResults = 100,
            int timeoutSeconds = 10,
            CancellationToken cancellationToken = default)
        {
            if (orchestrator == null)
                throw new ArgumentNullException(nameof(orchestrator));
            
            if (string.IsNullOrWhiteSpace(query))
                return new List<NetworkSearchResult>();
            
            // Crear SearchRequest
            var request = new SearchRequest
            {
                Query = query,
                MaxResults = maxResults,
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };
            
            // Realizar búsqueda
            var response = await orchestrator.SearchAsync(request, null, cancellationToken);
            
            // Convertir a NetworkSearchResult
            return response.DeduplicatedResults.Select(r => new NetworkSearchResult
            {
                Filename = r.FileName,
                Source = r.Username,
                Size = r.SizeBytes,
                Network = r.NetworkSource,
                QueueLength = r.QueueLength,
                FreeSlots = r.FreeSlots,
                BitRate = r.BitRate,
                Duration = r.Duration,
                FileExtension = r.FileExtension,
                Metadata = r.Metadata
            }).ToList();
        }
        
        /// <summary>
        /// Obtiene lista de redes activas
        /// </summary>
        public static IEnumerable<string> GetActiveNetworks(this NetworkOrchestrator orchestrator)
        {
            if (orchestrator == null)
                return Enumerable.Empty<string>();
            
            return orchestrator.GetClients()
                .Where(kvp => kvp.Value.IsConnected)
                .Select(kvp => kvp.Key);
        }
        
        /// <summary>
        /// Verifica si hay redes activas disponibles
        /// </summary>
        public static bool HasActiveNetworks(this NetworkOrchestrator orchestrator)
        {
            return orchestrator?.GetActiveNetworks().Any() ?? false;
        }
    }
    
    /// <summary>
    /// Resultado de búsqueda multi-red simplificado
    /// Compatible con el código existente de MainForm
    /// </summary>
    public class NetworkSearchResult
    {
        /// <summary>
        /// Nombre del archivo (con ruta completa)
        /// </summary>
        public string Filename { get; set; }
        
        /// <summary>
        /// Usuario/fuente que comparte el archivo
        /// </summary>
        public string Source { get; set; }
        
        /// <summary>
        /// Tamaño del archivo en bytes
        /// </summary>
        public long Size { get; set; }
        
        /// <summary>
        /// Red de origen (Soulseek, SoulseekQt, etc.)
        /// </summary>
        public string Network { get; set; }
        
        /// <summary>
        /// Longitud de la cola del proveedor
        /// </summary>
        public int QueueLength { get; set; }
        
        /// <summary>
        /// Slots libres del proveedor (si disponible)
        /// </summary>
        public int? FreeSlots { get; set; }
        
        /// <summary>
        /// Bitrate del archivo (para audio)
        /// </summary>
        public long? BitRate { get; set; }
        
        /// <summary>
        /// Duración del archivo (para audio/video)
        /// </summary>
        public int? Duration { get; set; }
        
        /// <summary>
        /// Extensión del archivo
        /// </summary>
        public string FileExtension { get; set; }
        
        /// <summary>
        /// Metadata adicional del resultado
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        
        /// <summary>
        /// Calcula puntuación de calidad del resultado
        /// Mayor puntuación = mejor resultado
        /// </summary>
        public double CalculateQualityScore()
        {
            double score = 0;
            
            // Priorizar slots libres
            if (FreeSlots.HasValue && FreeSlots.Value > 0)
            {
                score += 100;
            }
            
            // Penalizar cola larga
            if (QueueLength > 0)
            {
                score -= QueueLength * 2;
            }
            
            // Priorizar bitrate alto
            if (BitRate.HasValue)
            {
                score += BitRate.Value / 1000.0;
            }
            
            // Priorizar Soulseek (más rápido típicamente)
            if (Network == "Soulseek")
            {
                score += 50;
            }
            
            return score;
        }
        
        /// <summary>
        /// Verifica si el resultado es de alta calidad
        /// </summary>
        public bool IsHighQuality()
        {
            return CalculateQualityScore() >= 100;
        }
        
        /// <summary>
        /// Obtiene descripción legible del resultado
        /// </summary>
        public override string ToString()
        {
            return $"{Filename} ({FormatSize(Size)}) - {Source} [{Network}]";
        }
        
        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
