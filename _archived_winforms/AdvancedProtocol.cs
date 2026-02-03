using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SlskDown
{
    public class ExactSearchRequest
    {
        public string Filename { get; set; }
        public string Folder { get; set; }
        public long Size { get; set; }
        public string Checksum { get; set; }
        public uint Token { get; set; }
    }
    
    public class PlaceInLineInfo
    {
        public string Username { get; set; }
        public string Filename { get; set; }
        public int Position { get; set; }
        public int TotalInQueue { get; set; }
        public DateTime RequestTime { get; set; }
        public int EstimatedWaitMinutes { get; set; }
    }
    
    public class RelatedSearch
    {
        public string OriginalQuery { get; set; }
        public List<string> RelatedQueries { get; set; } = new List<string>();
        public DateTime GeneratedTime { get; set; }
    }
    
    public class AdvancedProtocolManager
    {
        private Dictionary<string, PlaceInLineInfo> placeInLineCache = new Dictionary<string, PlaceInLineInfo>();
        private Dictionary<string, RelatedSearch> relatedSearchCache = new Dictionary<string, RelatedSearch>();
        private HashSet<string> excludedPhrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Action<string> logAction;
        
        public AdvancedProtocolManager(Action<string> logger)
        {
            logAction = logger;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // BÚSQUEDA EXACTA DE ARCHIVO (Server Code 65)
        // ═══════════════════════════════════════════════════════════════
        
        public async Task<List<object>> ExactFileSearch(string filename, string folder, long size, string checksum = "")
        {
            try
            {
                logAction?.Invoke($"Búsqueda exacta: {filename} ({FormatSize(size)})");
                
                var request = new ExactSearchRequest
                {
                    Filename = filename,
                    Folder = folder,
                    Size = size,
                    Checksum = checksum,
                    Token = (uint)DateTime.Now.Ticks
                };
                
                // TODO: Implementar envío al servidor Soulseek
                // await SendExactSearchRequest(request);
                
                logAction?.Invoke($"Búsqueda exacta enviada (token: {request.Token})");
                
                return new List<object>();
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error en búsqueda exacta: {ex.Message}");
                return new List<object>();
            }
        }
        
        public async Task<List<object>> FindAlternativeSources(string filename, long size)
        {
            // Buscar fuentes alternativas para un archivo específico
            string folder = System.IO.Path.GetDirectoryName(filename) ?? "";
            string file = System.IO.Path.GetFileName(filename);
            
            return await ExactFileSearch(file, folder, size);
        }
        
        // ═══════════════════════════════════════════════════════════════
        // LUGAR EN COLA (Server Code 59/60)
        // ═══════════════════════════════════════════════════════════════
        
        public async Task<PlaceInLineInfo> RequestPlaceInLine(string username, string filename)
        {
            try
            {
                string key = $"{username}|{filename}";
                
                logAction?.Invoke($"Solicitando posición en cola: {username} - {filename}");
                
                // TODO: Implementar envío al servidor Soulseek
                // var response = await SendPlaceInLineRequest(username, filename);
                
                // Simulación de respuesta (reemplazar con protocolo real)
                var info = new PlaceInLineInfo
                {
                    Username = username,
                    Filename = filename,
                    Position = 5, // Ejemplo
                    TotalInQueue = 20, // Ejemplo
                    RequestTime = DateTime.Now,
                    EstimatedWaitMinutes = 15 // Ejemplo
                };
                
                placeInLineCache[key] = info;
                
                logAction?.Invoke($"Posición en cola: {info.Position}/{info.TotalInQueue} (espera: ~{info.EstimatedWaitMinutes} min)");
                
                return info;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error solicitando posición en cola: {ex.Message}");
                return null;
            }
        }
        
        public PlaceInLineInfo GetCachedPlaceInLine(string username, string filename)
        {
            string key = $"{username}|{filename}";
            
            if (placeInLineCache.TryGetValue(key, out var info))
            {
                // Verificar que no sea muy viejo (5 minutos)
                if ((DateTime.Now - info.RequestTime).TotalMinutes < 5)
                {
                    return info;
                }
            }
            
            return null;
        }
        
        public async Task UpdateAllPlaceInLine(List<Tuple<string, string>> downloads)
        {
            var tasks = downloads.Select(d => RequestPlaceInLine(d.Item1, d.Item2));
            await Task.WhenAll(tasks);
            
            logAction?.Invoke($"{downloads.Count} posiciones en cola actualizadas");
        }
        
        // ═══════════════════════════════════════════════════════════════
        // BÚSQUEDAS RELACIONADAS (Server Code 153)
        // ═══════════════════════════════════════════════════════════════
        
        public async Task<List<string>> GetRelatedSearches(string query)
        {
            try
            {
                if (relatedSearchCache.TryGetValue(query, out var cached))
                {
                    // Usar caché si no es muy viejo (1 hora)
                    if ((DateTime.Now - cached.GeneratedTime).TotalHours < 1)
                    {
                        return cached.RelatedQueries;
                    }
                }
                
                logAction?.Invoke($"Solicitando búsquedas relacionadas: {query}");
                
                // TODO: Implementar envío al servidor Soulseek
                // var related = await SendRelatedSearchRequest(query);
                
                // Simulación de respuestas relacionadas (reemplazar con protocolo real)
                var related = GenerateRelatedSearches(query);
                
                relatedSearchCache[query] = new RelatedSearch
                {
                    OriginalQuery = query,
                    RelatedQueries = related,
                    GeneratedTime = DateTime.Now
                };
                
                logAction?.Invoke($"{related.Count} búsquedas relacionadas encontradas");
                
                return related;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error obteniendo búsquedas relacionadas: {ex.Message}");
                return new List<string>();
            }
        }
        
        private List<string> GenerateRelatedSearches(string query)
        {
            // Generación básica de búsquedas relacionadas
            var related = new List<string>();
            var words = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (words.Length > 1)
            {
                // Búsquedas con palabras individuales
                foreach (var word in words)
                {
                    if (word.Length > 3)
                    {
                        related.Add(word);
                    }
                }
                
                // Búsquedas con combinaciones
                for (int i = 0; i < words.Length - 1; i++)
                {
                    related.Add($"{words[i]} {words[i + 1]}");
                }
            }
            
            return related.Distinct().Take(10).ToList();
        }
        
        // ═══════════════════════════════════════════════════════════════
        // FRASES EXCLUIDAS (Server Code 160)
        // ═══════════════════════════════════════════════════════════════
        
        public void UpdateExcludedPhrases(List<string> phrases)
        {
            excludedPhrases.Clear();
            foreach (var phrase in phrases)
            {
                excludedPhrases.Add(phrase);
            }
            
            logAction?.Invoke($"🚫 {phrases.Count} frases excluidas actualizadas");
        }
        
        public bool IsQueryAllowed(string query)
        {
            var queryLower = query.ToLowerInvariant();
            
            foreach (var phrase in excludedPhrases)
            {
                if (queryLower.Contains(phrase.ToLowerInvariant()))
                {
                    logAction?.Invoke($"🚫 Búsqueda bloqueada (frase excluida): {phrase}");
                    return false;
                }
            }
            
            return true;
        }
        
        public List<string> GetExcludedPhrases()
        {
            return excludedPhrases.ToList();
        }
        
        // ═══════════════════════════════════════════════════════════════
        // UTILIDADES
        // ═══════════════════════════════════════════════════════════════
        
        private string FormatSize(long bytes)
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
        
        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                { "PlaceInLineRequests", placeInLineCache.Count },
                { "RelatedSearchesCache", relatedSearchCache.Count },
                { "ExcludedPhrases", excludedPhrases.Count }
            };
        }
        
        public void ClearCaches()
        {
            placeInLineCache.Clear();
            relatedSearchCache.Clear();
            logAction?.Invoke("Cachés del protocolo avanzado limpiados");
        }
    }
}
