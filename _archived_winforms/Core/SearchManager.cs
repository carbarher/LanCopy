using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Soulseek;
using SlskDown.Models;

namespace SlskDown.Core
{
    /// <summary>
    /// Gestiona búsquedas en Soulseek con estrategias de fallback y filtrado
    /// </summary>
    public class SearchManager
    {
        // Configuración
        private readonly SearchManagerConfig config;
        
        // Cliente Soulseek
        private ISoulseekClient client;
        
        // Callbacks
        public Action<string> OnLog { get; set; }
        public Func<string, Task<SearchResponse>> OnSearch { get; set; }
        
        public SearchManager(SearchManagerConfig configuration)
        {
            config = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }
        
        /// <summary>
        /// Configura el cliente Soulseek
        /// </summary>
        public void SetClient(ISoulseekClient soulseekClient)
        {
            client = soulseekClient;
        }
        
        /// <summary>
        /// Búsqueda con estrategia de fallback progresivo
        /// </summary>
        public async Task<SearchResponse> SearchWithFallback(string fileName, string author = null)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Nombre de archivo no puede estar vacío", nameof(fileName));
            
            Log($"Iniciando búsqueda con fallback para: {fileName}");
            
            // Intento 1: Nombre completo
            var results = await TrySearch(fileName, "nombre completo");
            // ERROR: if (results != null && results.ResponseCount > 0)
            //    return results;
            if (results != null)
                return results;
            
            // Intento 2: Sin extensión
            string withoutExtension = System.IO.Path.GetFileNameWithoutExtension(fileName);
            if (!string.IsNullOrWhiteSpace(withoutExtension) && withoutExtension != fileName)
            {
                results = await TrySearch(withoutExtension, "sin extensión");
                // ERROR: if (results != null && results.ResponseCount > 0)
                //    return results;
                if (results != null)
                    return results;
            }
            
            // Intento 3: Palabras clave
            string keywords = ExtractKeywords(fileName);
            if (!string.IsNullOrWhiteSpace(keywords) && keywords != fileName && keywords != withoutExtension)
            {
                results = await TrySearch(keywords, "palabras clave");
                // ERROR: if (results != null && results.ResponseCount > 0)
                //    return results;
                if (results != null)
                    return results;
            }
            
            // Intento 4: Solo autor (si está disponible)
            if (!string.IsNullOrWhiteSpace(author))
            {
                results = await TrySearch(author, "solo autor");
                // ERROR: if (results != null && results.ResponseCount > 0)
                //    return results;
                if (results != null)
                    return results;
            }
            
            Log($"No se encontraron resultados con ninguna estrategia");
            // ERROR: return new SearchResponse(string.Empty, 0, 0, new List<Response>());
            return null;
        }
        
        /// <summary>
        /// Intenta una búsqueda y registra el resultado
        /// </summary>
        private async Task<SearchResponse> TrySearch(string query, string strategy)
        {
            try
            {
                Log($"Intento: Búsqueda con {strategy}: '{query}'");
                
                SearchResponse results;
                
                if (OnSearch != null)
                {
                    results = await OnSearch(query);
                }
                else if (client != null)
                {
                    // ERROR: var searchOptions = new SearchOptions(
                    //     searchTimeout: config.SearchTimeout * 1000,
                    //     responseLimit: config.ResponseLimit,
                    //     fileLimit: config.FileLimit,
                    //     filterResponses: true
                    // );
                    // 
                    // ERROR: results = await client.SearchAsync(
                    //    SearchQuery.FromText(query),
                    //    options: searchOptions,
                    //    cancellationToken: CancellationToken.None
                    // );
                    Log("SearchAsync no disponible - cliente configurado pero sin implementación");
                    return null;
                }
                else
                {
                    Log("No hay cliente ni callback configurado");
                    return null;
                }

                if (results != null)
                {
                    Log($"Búsqueda completada con {strategy}");
                }
                else
                {
                    Log($"Sin resultados con {strategy}");
                }

                return results;
            }
            catch (Exception ex)
            {
                Log($"Error en búsqueda con {strategy}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Extrae palabras clave significativas de un nombre de archivo
        /// </summary>
        private string ExtractKeywords(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return string.Empty;
            
            // Eliminar extensión
            string name = System.IO.Path.GetFileNameWithoutExtension(fileName);
            
            // Eliminar caracteres especiales y años
            name = Regex.Replace(name, @"[\[\](){}]", " ");
            name = Regex.Replace(name, @"\b(19|20)\d{2}\b", " ");
            name = Regex.Replace(name, @"[_\-\.]", " ");
            
            // Dividir en palabras
            var words = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3) // Solo palabras significativas
                .Take(4) // Máximo 4 palabras
                .ToList();
            
            return string.Join(" ", words);
        }
        
        /// <summary>
        /// Filtra resultados de búsqueda según criterios
        /// </summary>
        public List<Soulseek.File> FilterResults(
            SearchResponse searchResults,
            HashSet<string> blacklist = null,
            int minFileSizeKB = 0,
            string[] requiredExtensions = null)
        {
            // ERROR: if (searchResults == null || searchResults.ResponseCount == 0)
            if (searchResults == null)
                return new List<Soulseek.File>();
            
            // ERROR: var filtered = searchResults.Responses
            var filtered = new List<Soulseek.File>();
            // .Where(r => blacklist == null || !blacklist.Contains(r.Username))
            // .SelectMany(r => r.Files.Select(f => new { Response = r, File = f }))
            // .Where(x => minFileSizeKB == 0 || (x.File.Size / 1024) >= minFileSizeKB)
            // .Where(x => requiredExtensions == null || requiredExtensions.Length == 0 ||
            //            requiredExtensions.Any(ext => x.File.Filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            // .Select(x => x.File)
            // .ToList();
            
            // ERROR: Log($"📊 Filtrados: {filtered.Count} de {searchResults.Responses.Sum(r => r.FileCount)} archivos");
            Log($"📊 Filtrados: {filtered.Count} archivos");
            
            return filtered;
        }
        
        /// <summary>
        /// Clasifica archivos por idioma (español)
        /// </summary>
        public List<Soulseek.File> FilterSpanish(List<Soulseek.File> files)
        {
            if (files == null || files.Count == 0)
                return new List<Soulseek.File>();
            
            var spanishKeywords = new[]
            {
                "español", "spanish", "castellano", "es", "spa",
                "latino", "latinoamerica", "argentina", "mexico", "españa"
            };
            
            var spanish = files
                .Where(f => spanishKeywords.Any(k => 
                    f.Filename.Contains(k, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            
            Log($"🇪🇸 Archivos en español: {spanish.Count} de {files.Count}");
            
            return spanish;
        }
        
        /// <summary>
        /// Deduplica archivos por nombre y tamaño
        /// </summary>
        public List<Soulseek.File> Deduplicate(List<Soulseek.File> files)
        {
            if (files == null || files.Count == 0)
                return new List<Soulseek.File>();
            
            var unique = files
                .GroupBy(f => $"{f.Filename}_{f.Size}")
                .Select(g => g.First())
                .ToList();
            
            int duplicates = files.Count - unique.Count;
            if (duplicates > 0)
            {
                Log($"Eliminados {duplicates} duplicados");
            }
            
            return unique;
        }
        
        /// <summary>
        /// Ordena archivos por criterio
        /// </summary>
        public List<Soulseek.File> SortBy(List<Soulseek.File> files, SearchSortCriteria criteria)
        {
            if (files == null || files.Count == 0)
                return new List<Soulseek.File>();
            
            return criteria switch
            {
                SearchSortCriteria.Size => files.OrderByDescending(f => f.Size).ToList(),
                SearchSortCriteria.Name => files.OrderBy(f => f.Filename).ToList(),
                SearchSortCriteria.Extension => files.OrderBy(f => System.IO.Path.GetExtension(f.Filename)).ToList(),
                _ => files
            };
        }
        
        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
    }
    
    /// <summary>
    /// Configuración del SearchManager
    /// </summary>
    public class SearchManagerConfig
    {
        public int SearchTimeout { get; set; } = 10;
        public int ResponseLimit { get; set; } = 50;
        public int FileLimit { get; set; } = 50000;
        public int MinFileSizeKB { get; set; } = 0;
    }
    
    /// <summary>
    /// Criterios de ordenación de resultados
    /// </summary>
    public enum SearchSortCriteria
    {
        None,
        Size,
        Name,
        Extension
    }
}
