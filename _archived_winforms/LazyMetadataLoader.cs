using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// Carga diferida de metadatos de archivos para mejorar rendimiento
    /// </summary>
    public class LazyMetadataLoader
    {
        private class FileMetadata
        {
            public string FilePath { get; set; } = "";
            public string FileName { get; set; } = "";
            public long FileSize { get; set; }
            public DateTime LastModified { get; set; }
            public string Extension { get; set; } = "";
            public string? Author { get; set; }
            public string? Title { get; set; }
            public bool MetadataLoaded { get; set; }
        }

        private readonly ConcurrentDictionary<string, FileMetadata> metadataCache;
        private readonly SemaphoreSlim loadSemaphore;
        private readonly int maxConcurrentLoads;

        public LazyMetadataLoader(int maxConcurrentLoads = 4)
        {
            this.maxConcurrentLoads = maxConcurrentLoads;
            metadataCache = new ConcurrentDictionary<string, FileMetadata>(StringComparer.OrdinalIgnoreCase);
            loadSemaphore = new SemaphoreSlim(maxConcurrentLoads, maxConcurrentLoads);
        }

        /// <summary>
        /// Registra un archivo sin cargar metadatos completos
        /// </summary>
        public void RegisterFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            if (metadataCache.ContainsKey(filePath))
                return;

            try
            {
                var fileInfo = new FileInfo(filePath);
                var metadata = new FileMetadata
                {
                    FilePath = filePath,
                    FileName = fileInfo.Name,
                    FileSize = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTimeUtc,
                    Extension = fileInfo.Extension.ToLowerInvariant(),
                    MetadataLoaded = false
                };

                metadataCache.TryAdd(filePath, metadata);
            }
            catch
            {
                // Error registrando archivo
            }
        }

        /// <summary>
        /// Registra múltiples archivos en paralelo
        /// </summary>
        public void RegisterFiles(IEnumerable<string> filePaths)
        {
            Parallel.ForEach(filePaths, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, filePath =>
            {
                RegisterFile(filePath);
            });
        }

        /// <summary>
        /// Obtiene metadatos básicos (sin autor/título)
        /// </summary>
        public (string FileName, long FileSize, DateTime LastModified, string Extension)? GetBasicMetadata(string filePath)
        {
            if (!metadataCache.TryGetValue(filePath, out var metadata))
                return null;

            return (metadata.FileName, metadata.FileSize, metadata.LastModified, metadata.Extension);
        }

        /// <summary>
        /// Obtiene metadatos completos (carga autor/título si no están cargados)
        /// </summary>
        public async Task<(string FileName, long FileSize, DateTime LastModified, string Extension, string? Author, string? Title)> GetFullMetadataAsync(string filePath)
        {
            if (!metadataCache.TryGetValue(filePath, out var metadata))
            {
                RegisterFile(filePath);
                if (!metadataCache.TryGetValue(filePath, out metadata))
                    throw new FileNotFoundException($"No se pudo cargar metadatos para: {filePath}");
            }

            // Si los metadatos ya están cargados, devolverlos
            if (metadata.MetadataLoaded)
            {
                return (metadata.FileName, metadata.FileSize, metadata.LastModified, metadata.Extension, metadata.Author, metadata.Title);
            }

            // Cargar metadatos de forma diferida
            await loadSemaphore.WaitAsync();
            try
            {
                // Verificar nuevamente por si otro hilo ya cargó los metadatos
                if (metadata.MetadataLoaded)
                {
                    return (metadata.FileName, metadata.FileSize, metadata.LastModified, metadata.Extension, metadata.Author, metadata.Title);
                }

                // Extraer autor y título del nombre de archivo
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(metadata.FileName);
                ExtractAuthorAndTitle(fileNameWithoutExt, out string? author, out string? title);

                metadata.Author = author;
                metadata.Title = title;
                metadata.MetadataLoaded = true;

                return (metadata.FileName, metadata.FileSize, metadata.LastModified, metadata.Extension, metadata.Author, metadata.Title);
            }
            finally
            {
                loadSemaphore.Release();
            }
        }

        /// <summary>
        /// Pre-carga metadatos completos para un conjunto de archivos
        /// </summary>
        public async Task PreloadMetadataAsync(IEnumerable<string> filePaths, IProgress<int>? progress = null)
        {
            var fileList = filePaths.ToList();
            var completed = 0;
            var lockObj = new object();

            await Task.Run(() =>
            {
                Parallel.ForEach(fileList, new ParallelOptions { MaxDegreeOfParallelism = maxConcurrentLoads }, async filePath =>
                {
                    try
                    {
                        await GetFullMetadataAsync(filePath);

                        lock (lockObj)
                        {
                            completed++;
                            progress?.Report(completed);
                        }
                    }
                    catch
                    {
                        // Error cargando metadatos de un archivo
                    }
                });
            });
        }

        /// <summary>
        /// Obtiene todos los archivos registrados
        /// </summary>
        public List<string> GetAllFiles()
        {
            return metadataCache.Keys.ToList();
        }

        /// <summary>
        /// Obtiene estadísticas del loader
        /// </summary>
        public (int TotalFiles, int LoadedMetadata, int PendingMetadata) GetStats()
        {
            var total = metadataCache.Count;
            var loaded = metadataCache.Values.Count(m => m.MetadataLoaded);
            var pending = total - loaded;

            return (total, loaded, pending);
        }

        /// <summary>
        /// Limpia el caché
        /// </summary>
        public void Clear()
        {
            metadataCache.Clear();
        }

        /// <summary>
        /// Invalida metadatos de un archivo
        /// </summary>
        public void Invalidate(string filePath)
        {
            metadataCache.TryRemove(filePath, out _);
        }

        /// <summary>
        /// Extrae autor y título del nombre de archivo
        /// </summary>
        private void ExtractAuthorAndTitle(string fileName, out string? author, out string? title)
        {
            author = null;
            title = null;

            try
            {
                // Formato esperado: "Autor - Título"
                var parts = fileName.Split(new[] { " - ", " – " }, StringSplitOptions.RemoveEmptyEntries);
                
                if (parts.Length >= 2)
                {
                    author = parts[0].Trim();
                    title = string.Join(" - ", parts.Skip(1)).Trim();
                }
                else
                {
                    title = fileName.Trim();
                }
            }
            catch
            {
                title = fileName;
            }
        }

        /// <summary>
        /// Busca archivos por autor (solo en metadatos ya cargados)
        /// </summary>
        public List<string> FindByAuthor(string authorQuery)
        {
            if (string.IsNullOrWhiteSpace(authorQuery))
                return new List<string>();

            var query = authorQuery.ToLowerInvariant();
            return metadataCache.Values
                .Where(m => m.MetadataLoaded && m.Author != null && m.Author.ToLowerInvariant().Contains(query))
                .Select(m => m.FilePath)
                .ToList();
        }

        /// <summary>
        /// Busca archivos por título (solo en metadatos ya cargados)
        /// </summary>
        public List<string> FindByTitle(string titleQuery)
        {
            if (string.IsNullOrWhiteSpace(titleQuery))
                return new List<string>();

            var query = titleQuery.ToLowerInvariant();
            return metadataCache.Values
                .Where(m => m.MetadataLoaded && m.Title != null && m.Title.ToLowerInvariant().Contains(query))
                .Select(m => m.FilePath)
                .ToList();
        }

        /// <summary>
        /// Obtiene archivos agrupados por autor (solo metadatos cargados)
        /// </summary>
        public Dictionary<string, List<string>> GetFilesByAuthor()
        {
            return metadataCache.Values
                .Where(m => m.MetadataLoaded && !string.IsNullOrWhiteSpace(m.Author))
                .GroupBy(m => m.Author!)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(m => m.FilePath).ToList()
                );
        }

        /// <summary>
        /// Obtiene archivos agrupados por extensión
        /// </summary>
        public Dictionary<string, List<string>> GetFilesByExtension()
        {
            return metadataCache.Values
                .GroupBy(m => m.Extension)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(m => m.FilePath).ToList()
                );
        }
    }
}
