using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Soulseek;
using SoulseekDirectory = Soulseek.Directory;

namespace SlskDown.Core.Browse
{
    /// <summary>
    /// Gestiona la exploración de carpetas y archivos de usuarios (Browse User)
    /// Inspirado en Nicotine+
    /// </summary>
    public class UserBrowser
    {
        private readonly ISoulseekClient client;
        private readonly Dictionary<string, BrowseResponse> cachedBrowses;
        private readonly object cacheLock = new object();
        private readonly TimeSpan cacheExpiration = TimeSpan.FromMinutes(10);

        public event Action<string> OnLog;

        public UserBrowser(ISoulseekClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.cachedBrowses = new Dictionary<string, BrowseResponse>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Explora todos los archivos compartidos de un usuario
        /// </summary>
        public async Task<BrowseResult> BrowseUserAsync(string username, bool useCache = true)
        {
            try
            {
                Log($"Explorando archivos de usuario: {username}");

                // Verificar caché
                if (useCache)
                {
                    lock (cacheLock)
                    {
                        if (cachedBrowses.TryGetValue(username, out var cached))
                        {
                            if (DateTime.UtcNow - cached.CachedAt < cacheExpiration)
                            {
                                Log($"✅ Usando caché para {username} ({cached.Directories.Count} carpetas)");
                                return ConvertToResult(username, cached);
                            }
                        }
                    }
                }

                // Realizar browse
                var response = await client.BrowseAsync(username);
                
                // Guardar en caché
                var browseResponse = new BrowseResponse
                {
                    Username = username,
                    Directories = response.Directories.ToList(),
                    LockedDirectories = response.LockedDirectories.ToList(),
                    CachedAt = DateTime.UtcNow
                };

                lock (cacheLock)
                {
                    cachedBrowses[username] = browseResponse;
                }

                var result = ConvertToResult(username, browseResponse);
                Log($"✅ Browse completado: {username} - {result.TotalDirectories} carpetas, {result.TotalFiles} archivos ({FormatSize(result.TotalSize)})");

                return result;
            }
            catch (Exception ex)
            {
                Log($"❌ Error explorando {username}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Obtiene archivos de una carpeta específica
        /// </summary>
        public async Task<List<BrowseFile>> GetDirectoryFilesAsync(string username, string directory)
        {
            var browseResult = await BrowseUserAsync(username);
            var dir = browseResult.Directories.FirstOrDefault(d => d.Name == directory);
            return dir?.Files ?? new List<BrowseFile>();
        }

        /// <summary>
        /// Busca archivos en el browse por nombre
        /// </summary>
        public async Task<List<BrowseFile>> SearchInBrowseAsync(string username, string searchTerm)
        {
            var browseResult = await BrowseUserAsync(username);
            var results = new List<BrowseFile>();

            foreach (var dir in browseResult.Directories)
            {
                var matchingFiles = dir.Files.Where(f => 
                    f.FileName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                results.AddRange(matchingFiles);
            }

            return results;
        }

        /// <summary>
        /// Obtiene estadísticas del browse
        /// </summary>
        public async Task<BrowseStats> GetBrowseStatsAsync(string username)
        {
            var browseResult = await BrowseUserAsync(username);

            var stats = new BrowseStats
            {
                Username = username,
                TotalDirectories = browseResult.TotalDirectories,
                TotalFiles = browseResult.TotalFiles,
                TotalSize = browseResult.TotalSize,
                FilesByExtension = new Dictionary<string, int>(),
                DirectoriesByDepth = new Dictionary<int, int>()
            };

            // Agrupar por extensión
            foreach (var dir in browseResult.Directories)
            {
                foreach (var file in dir.Files)
                {
                    var ext = file.Extension.ToLowerInvariant();
                    if (!stats.FilesByExtension.ContainsKey(ext))
                        stats.FilesByExtension[ext] = 0;
                    stats.FilesByExtension[ext]++;
                }

                // Calcular profundidad
                var depth = dir.Name.Split('\\', '/').Length;
                if (!stats.DirectoriesByDepth.ContainsKey(depth))
                    stats.DirectoriesByDepth[depth] = 0;
                stats.DirectoriesByDepth[depth]++;
            }

            return stats;
        }

        /// <summary>
        /// Limpia el caché de un usuario
        /// </summary>
        public void ClearCache(string username = null)
        {
            lock (cacheLock)
            {
                if (username == null)
                {
                    cachedBrowses.Clear();
                    Log("🧹 Caché de browse limpiado completamente");
                }
                else
                {
                    cachedBrowses.Remove(username);
                    Log($"🧹 Caché de browse limpiado para {username}");
                }
            }
        }

        private BrowseResult ConvertToResult(string username, BrowseResponse response)
        {
            var result = new BrowseResult
            {
                Username = username,
                Directories = new List<BrowseDirectory>(),
                TotalDirectories = response.Directories.Count,
                TotalFiles = 0,
                TotalSize = 0,
                BrowsedAt = response.CachedAt
            };

            foreach (var dir in response.Directories)
            {
                var browseDir = new BrowseDirectory
                {
                    Name = dir.Name,
                    Files = new List<BrowseFile>()
                };

                foreach (var file in dir.Files)
                {
                    var browseFile = new BrowseFile
                    {
                        FileName = file.Filename,
                        Size = file.Size,
                        Extension = System.IO.Path.GetExtension(file.Filename),
                        Directory = dir.Name,
                        Username = username
                    };

                    browseDir.Files.Add(browseFile);
                    result.TotalFiles++;
                    result.TotalSize += file.Size;
                }

                result.Directories.Add(browseDir);
            }

            return result;
        }

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

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
    }

    public class BrowseResponse
    {
        public string Username { get; set; }
        public List<SoulseekDirectory> Directories { get; set; }
        public List<SoulseekDirectory> LockedDirectories { get; set; }
        public DateTime CachedAt { get; set; }
    }

    public class BrowseResult
    {
        public string Username { get; set; }
        public List<BrowseDirectory> Directories { get; set; }
        public int TotalDirectories { get; set; }
        public int TotalFiles { get; set; }
        public long TotalSize { get; set; }
        public DateTime BrowsedAt { get; set; }
    }

    public class BrowseDirectory
    {
        public string Name { get; set; }
        public List<BrowseFile> Files { get; set; }
        
        public int FileCount => Files?.Count ?? 0;
        public long TotalSize => Files?.Sum(f => f.Size) ?? 0;
    }

    public class BrowseFile
    {
        public string FileName { get; set; }
        public long Size { get; set; }
        public string Extension { get; set; }
        public string Directory { get; set; }
        public string Username { get; set; }
        
        public string FullPath => System.IO.Path.Combine(Directory, FileName);
    }

    public class BrowseStats
    {
        public string Username { get; set; }
        public int TotalDirectories { get; set; }
        public int TotalFiles { get; set; }
        public long TotalSize { get; set; }
        public Dictionary<string, int> FilesByExtension { get; set; }
        public Dictionary<int, int> DirectoriesByDepth { get; set; }
        
        public List<(string Extension, int Count)> TopExtensions(int count = 10)
        {
            return FilesByExtension
                .OrderByDescending(kvp => kvp.Value)
                .Take(count)
                .Select(kvp => (kvp.Key, kvp.Value))
                .ToList();
        }
    }
}
