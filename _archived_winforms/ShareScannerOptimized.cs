using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown
{
    public class FileMetadata
    {
        public string Path { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string Extension { get; set; }
        public string Hash { get; set; }
    }
    
    public class ShareScannerOptimized
    {
        private ConcurrentDictionary<string, FileMetadata> index = new ConcurrentDictionary<string, FileMetadata>(StringComparer.OrdinalIgnoreCase);
        private List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
        private List<string> sharedFolders = new List<string>();
        private bool isScanning = false;
        private Action<string> logAction;
        private Action<int, int> progressAction;
        
        public ShareScannerOptimized(Action<string> logger, Action<int, int> progress)
        {
            logAction = logger;
            progressAction = progress;
        }
        
        public void SetSharedFolders(List<string> folders)
        {
            sharedFolders = folders.Where(f => Directory.Exists(f)).ToList();
            logAction?.Invoke($"📁 {sharedFolders.Count} carpetas compartidas configuradas");
        }
        
        public async Task<int> ScanSharesOptimized()
        {
            if (isScanning)
            {
                logAction?.Invoke("⚠️ Escaneo ya en progreso");
                return 0;
            }
            
            isScanning = true;
            var sw = Stopwatch.StartNew();
            int filesScanned = 0;
            int filesSkipped = 0;
            int filesTotal = 0;
            
            try
            {
                logAction?.Invoke("🔍 Iniciando escaneo optimizado de shares...");
                
                // Escaneo paralelo de carpetas
                var tasks = sharedFolders.Select(async folder =>
                {
                    await Task.Run(() =>
                    {
                        try
                        {
                            var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories);
                            
                            foreach (var file in files)
                            {
                                Interlocked.Increment(ref filesTotal);
                                
                                try
                                {
                                    var fi = new FileInfo(file);
                                    
                                    // Skip archivos ocultos o del sistema
                                    if ((fi.Attributes & FileAttributes.Hidden) != 0 ||
                                        (fi.Attributes & FileAttributes.System) != 0)
                                    {
                                        Interlocked.Increment(ref filesSkipped);
                                        continue;
                                    }
                                    
                                    // Skip si no ha cambiado (escaneo incremental)
                                    if (index.TryGetValue(file, out var cached))
                                    {
                                        if (cached.LastModified == fi.LastWriteTime && cached.Size == fi.Length)
                                        {
                                            Interlocked.Increment(ref filesSkipped);
                                            continue;
                                        }
                                    }
                                    
                                    // Indexar archivo
                                    index[file] = new FileMetadata
                                    {
                                        Path = file,
                                        Size = fi.Length,
                                        LastModified = fi.LastWriteTime,
                                        Extension = fi.Extension.ToLowerInvariant()
                                    };
                                    
                                    Interlocked.Increment(ref filesScanned);
                                    
                                    // Reportar progreso cada 100 archivos
                                    if (filesTotal % 100 == 0)
                                    {
                                        progressAction?.Invoke(filesTotal, filesScanned);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Skip archivos con errores de acceso
                                    Interlocked.Increment(ref filesSkipped);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logAction?.Invoke($"❌ Error escaneando {folder}: {ex.Message}");
                        }
                    });
                }).ToArray();
                
                await Task.WhenAll(tasks);
                
                sw.Stop();
                
                double scanRate = filesTotal / sw.Elapsed.TotalSeconds;
                logAction?.Invoke($"✅ Escaneo completo: {filesScanned} nuevos/modificados, {filesSkipped} sin cambios");
                logAction?.Invoke($"   Total: {filesTotal} archivos en {sw.Elapsed.TotalSeconds:F1}s ({scanRate:F0} archivos/s)");
                
                return filesScanned;
            }
            finally
            {
                isScanning = false;
            }
        }
        
        public void EnableFileWatchers()
        {
            // Limpiar watchers existentes
            foreach (var watcher in watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            watchers.Clear();
            
            // Crear nuevos watchers
            foreach (var folder in sharedFolders)
            {
                try
                {
                    var watcher = new FileSystemWatcher(folder)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | 
                                      NotifyFilters.LastWrite | 
                                      NotifyFilters.Size
                    };
                    
                    watcher.Created += OnFileCreated;
                    watcher.Changed += OnFileChanged;
                    watcher.Deleted += OnFileDeleted;
                    watcher.Renamed += OnFileRenamed;
                    
                    watcher.EnableRaisingEvents = true;
                    watchers.Add(watcher);
                    
                    logAction?.Invoke($"👁️ FileSystemWatcher habilitado: {folder}");
                }
                catch (Exception ex)
                {
                    logAction?.Invoke($"⚠️ Error creando watcher para {folder}: {ex.Message}");
                }
            }
            
            logAction?.Invoke($"👁️ {watchers.Count} FileSystemWatchers activos");
        }
        
        public void DisableFileWatchers()
        {
            foreach (var watcher in watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            watchers.Clear();
            logAction?.Invoke("👁️ FileSystemWatchers deshabilitados");
        }
        
        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            UpdateIndex(e.FullPath);
        }
        
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            UpdateIndex(e.FullPath);
        }
        
        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            RemoveFromIndex(e.FullPath);
        }
        
        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            RemoveFromIndex(e.OldFullPath);
            UpdateIndex(e.FullPath);
        }
        
        private void UpdateIndex(string filePath)
        {
            try
            {
                var fi = new FileInfo(filePath);
                
                if (!fi.Exists) return;
                
                // Skip archivos ocultos o del sistema
                if ((fi.Attributes & FileAttributes.Hidden) != 0 ||
                    (fi.Attributes & FileAttributes.System) != 0)
                    return;
                
                index[filePath] = new FileMetadata
                {
                    Path = filePath,
                    Size = fi.Length,
                    LastModified = fi.LastWriteTime,
                    Extension = fi.Extension.ToLowerInvariant()
                };
                
                logAction?.Invoke($"📝 Índice actualizado: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                // Ignorar errores de acceso
            }
        }
        
        private void RemoveFromIndex(string filePath)
        {
            if (index.TryRemove(filePath, out _))
            {
                logAction?.Invoke($"🗑️ Eliminado del índice: {Path.GetFileName(filePath)}");
            }
        }
        
        public List<FileMetadata> SearchInIndex(string query)
        {
            var queryLower = query.ToLowerInvariant();
            
            return index.Values
                .Where(f => Path.GetFileName(f.Path).ToLowerInvariant().Contains(queryLower))
                .OrderBy(f => f.Path)
                .ToList();
        }
        
        public List<FileMetadata> GetAllFiles()
        {
            return index.Values.OrderBy(f => f.Path).ToList();
        }
        
        public int GetFileCount()
        {
            return index.Count;
        }
        
        public long GetTotalSize()
        {
            return index.Values.Sum(f => f.Size);
        }
        
        public Dictionary<string, int> GetFileTypeStats()
        {
            return index.Values
                .GroupBy(f => f.Extension)
                .ToDictionary(g => g.Key, g => g.Count())
                .OrderByDescending(kvp => kvp.Value)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        
        public void ClearIndex()
        {
            index.Clear();
            logAction?.Invoke("🗑️ Índice limpiado");
        }
    }
}
