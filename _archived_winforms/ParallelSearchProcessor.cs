using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Soulseek;
using SlskDown.Models;
using SlskDown.UI;

namespace SlskDown
{
    /// <summary>
    /// Procesador paralelo de resultados de búsqueda para máximo rendimiento
    /// Usa Parallel.ForEach y particionamiento optimizado
    /// </summary>
    public class ParallelSearchProcessor
    {
        private readonly int maxDegreeOfParallelism;
        
        public ParallelSearchProcessor(int? maxParallelism = null)
        {
            maxDegreeOfParallelism = maxParallelism ?? Environment.ProcessorCount;
        }
        
        /// <summary>
        /// Procesa respuestas de búsqueda en paralelo con filtros
        /// </summary>
        public List<SearchResultItem> ProcessSearchResponses(
            IEnumerable<SearchResponse> responses,
            long minSizeBytes = 0,
            long maxSizeBytes = long.MaxValue,
            string extensionFilter = null,
            bool filterSpanish = false,
            HashSet<string> blacklist = null)
        {
            var results = new ConcurrentBag<SearchResultItem>();
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism
            };
            
            // Procesar respuestas en paralelo
            Parallel.ForEach(responses, options, response =>
            {
                if (blacklist != null && blacklist.Contains(response.Username))
                    return;
                
                // Procesar archivos de esta respuesta
                var localResults = new List<SearchResultItem>();
                
                foreach (var file in response.Files)
                {
                    // Filtro de tamaño
                    if (file.Size < minSizeBytes || file.Size > maxSizeBytes)
                        continue;
                    
                    // Filtro de extensión
                    if (!string.IsNullOrEmpty(extensionFilter))
                    {
                        var fileExt = System.IO.Path.GetExtension(file.Filename);
                        if (!MatchesCategory(fileExt, extensionFilter))
                            continue;
                    }
                    
                    // Filtro de idioma español
                    if (filterSpanish)
                    {
                        if (!PerformanceOptimizations.IsSpanishTextFast(file.Filename.AsSpan()))
                            continue;
                    }
                    
                    // Filtro de archivos basura
                    if (PerformanceOptimizations.IsGarbageFileFast(file.Filename.AsSpan()))
                        continue;
                    
                    // Crear item de resultado
                    localResults.Add(new SearchResultItem
                    {
                        Filename = System.IO.Path.GetFileName(file.Filename),
                        Extension = System.IO.Path.GetExtension(file.Filename),
                        Username = response.Username,
                        Size = file.Size,
                        UploadSpeed = response.UploadSpeed,
                        FolderPath = System.IO.Path.GetDirectoryName(file.Filename) ?? ""
                    });
                }
                
                // Agregar resultados locales al bag concurrente
                foreach (var item in localResults)
                {
                    results.Add(item);
                }
            });
            
            return results.ToList();
        }
        
        /// <summary>
        /// Procesa respuestas de búsqueda en paralelo con callback de progreso
        /// </summary>
        public List<SearchResultItem> ProcessSearchResponsesWithProgress(
            IEnumerable<SearchResponse> responses,
            Action<int, int> progressCallback,
            long minSizeBytes = 0,
            long maxSizeBytes = long.MaxValue,
            string extensionFilter = null,
            bool filterSpanish = false,
            HashSet<string> blacklist = null)
        {
            var responsesList = responses.ToList();
            var results = new ConcurrentBag<SearchResultItem>();
            var processedCount = 0;
            var totalCount = responsesList.Count;
            
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism
            };
            
            Parallel.ForEach(responsesList, options, response =>
            {
                if (blacklist != null && blacklist.Contains(response.Username))
                {
                    System.Threading.Interlocked.Increment(ref processedCount);
                    progressCallback?.Invoke(processedCount, totalCount);
                    return;
                }
                
                var localResults = new List<SearchResultItem>();
                
                foreach (var file in response.Files)
                {
                    if (file.Size < minSizeBytes || file.Size > maxSizeBytes)
                        continue;
                    
                    if (!string.IsNullOrEmpty(extensionFilter))
                    {
                        var fileExt = System.IO.Path.GetExtension(file.Filename);
                        if (!MatchesCategory(fileExt, extensionFilter))
                            continue;
                    }
                    
                    if (filterSpanish && !PerformanceOptimizations.IsSpanishTextFast(file.Filename.AsSpan()))
                        continue;
                    
                    if (PerformanceOptimizations.IsGarbageFileFast(file.Filename.AsSpan()))
                        continue;
                    
                    localResults.Add(new SearchResultItem
                    {
                        Filename = System.IO.Path.GetFileName(file.Filename),
                        Extension = System.IO.Path.GetExtension(file.Filename),
                        Username = response.Username,
                        Size = file.Size,
                        UploadSpeed = response.UploadSpeed,
                        FolderPath = System.IO.Path.GetDirectoryName(file.Filename) ?? ""
                    });
                }
                
                foreach (var item in localResults)
                {
                    results.Add(item);
                }
                
                System.Threading.Interlocked.Increment(ref processedCount);
                progressCallback?.Invoke(processedCount, totalCount);
            });
            
            return results.ToList();
        }
        
        /// <summary>
        /// Agrupa archivos por autor en paralelo
        /// </summary>
        public Dictionary<string, List<SearchResultItem>> GroupByAuthorParallel(List<SearchResultItem> items)
        {
            var grouped = new ConcurrentDictionary<string, ConcurrentBag<SearchResultItem>>(
                Environment.ProcessorCount * 4,
                items.Count / 10,
                StringComparer.OrdinalIgnoreCase
            );
            
            Parallel.ForEach(items, 
                new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
                item =>
                {
                    var bag = grouped.GetOrAdd(item.Username, _ => new ConcurrentBag<SearchResultItem>());
                    bag.Add(item);
                });
            
            // Convertir a diccionario normal
            return grouped.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToList(),
                StringComparer.OrdinalIgnoreCase
            );
        }
        
        /// <summary>
        /// Filtra resultados en paralelo
        /// </summary>
        public List<SearchResultItem> FilterParallel(
            List<SearchResultItem> items,
            Func<SearchResultItem, bool> predicate)
        {
            var results = new ConcurrentBag<SearchResultItem>();
            
            Parallel.ForEach(items,
                new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
                item =>
                {
                    if (predicate(item))
                        results.Add(item);
                });
            
            return results.ToList();
        }
        
        private static bool MatchesCategory(string extension, string category)
        {
            if (string.IsNullOrEmpty(extension) || string.IsNullOrEmpty(category))
                return true;
            
            extension = extension.ToLower();
            category = category.ToLower();
            
            if (category == "todos" || category == "all")
                return true;
            
            // Categorías de documentos
            if (category == "documentos" || category == "documents")
            {
                return extension == ".pdf" || extension == ".epub" || extension == ".mobi" ||
                       extension == ".azw3" || extension == ".djvu" || extension == ".doc" ||
                       extension == ".docx" || extension == ".txt" || extension == ".rtf" ||
                       extension == ".odt" || extension == ".fb2" || extension == ".lit" ||
                       extension == ".pdb" || extension == ".cbr" || extension == ".cbz";
            }
            
            // Categorías de audio
            if (category == "audio" || category == "música" || category == "music")
            {
                return extension == ".mp3" || extension == ".flac" || extension == ".m4a" ||
                       extension == ".aac" || extension == ".ogg" || extension == ".wav" ||
                       extension == ".wma" || extension == ".ape" || extension == ".opus";
            }
            
            // Categorías de video
            if (category == "video" || category == "vídeo")
            {
                return extension == ".mp4" || extension == ".mkv" || extension == ".avi" ||
                       extension == ".mov" || extension == ".wmv" || extension == ".flv" ||
                       extension == ".webm" || extension == ".m4v" || extension == ".mpg" ||
                       extension == ".mpeg";
            }
            
            // Extensión específica
            return extension == category || extension == "." + category;
        }
    }
}
