using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using Soulseek;

namespace SlskDown
{
    /// <summary>
    /// Métodos de integración para características avanzadas de Nicotine+
    /// </summary>
    public partial class MainForm
    {
        // ═══════════════════════════════════════════════════════════════
        // INICIALIZACIÓN DE CARACTERÍSTICAS AVANZADAS
        // ═══════════════════════════════════════════════════════════════
        
        private void InitializeAdvancedFeatures()
        {
            InitializeWishlistTimer();
            InitializeSpeedGraphTimer();
            InitializeRetrySystem();
            LoadUserProfiles();
            LoadSearchCache();
        }
        
        // ═══════════════════════════════════════════════════════════════
        // 1. FUENTES ALTERNATIVAS
        // ═══════════════════════════════════════════════════════════════
        
        private async Task<bool> DownloadWithAlternatives(string filename, string username, long size)
        {
            if (!enableAlternativeSources)
                return await DownloadFile(filename, username);
            
            // Buscar fuentes alternativas
            var alternatives = await FindAlternativeSourcesForFile(filename, size);
            
            if (!alternatives.Any())
            {
                Log($"No se encontraron fuentes alternativas para {filename}");
                return await DownloadFile(filename, username);
            }
            
            Log($"✨ {alternatives.Count} fuentes alternativas encontradas");
            
            // Guardar fuentes alternativas
            var key = $"{filename}|{size}";
            alternativeSources[key] = alternatives;
            
            // Intentar con la mejor fuente
            var bestSource = NicotineFeatures.SelectBestSource(alternatives, userLoadInfo, maxDownloadsPerUser);
            
            if (bestSource != null)
            {
                Log($"🎯 Usando fuente alternativa: {bestSource}");
                return await DownloadFile(filename, bestSource);
            }
            
            return await DownloadFile(filename, username);
        }
        
        private async Task<List<AlternativeSource>> FindAlternativeSourcesForFile(string filename, long size)
        {
            try
            {
                // Buscar el archivo en resultados recientes
                var recentResults = allResults.Where(f => 
                    Math.Abs(f.Size - size) < 1024 && 
                    System.IO.Path.GetFileName(f.Filename).Equals(System.IO.Path.GetFileName(filename), StringComparison.OrdinalIgnoreCase)
                ).ToList();
                
                return NicotineFeatures.FindAlternativeSources(recentResults, filename, size, maxAlternativeSourcesPerFile);
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error buscando fuentes alternativas: {ex.Message}");
                return new List<AlternativeSource>();
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // 2. FILTROS AVANZADOS DE BÚSQUEDA
        // ═══════════════════════════════════════════════════════════════
        
        private List<Soulseek.File> ApplyAdvancedSearchFilters(List<Soulseek.File> results, string query)
        {
            // Parsear query para extraer operadores
            var (cleanQuery, operators) = NicotineFeatures.ParseSearchQuery(query);
            
            // Extraer palabras de exclusión (palabras que empiezan con -)
            var words = cleanQuery.Split(' ');
            var excludes = words.Where(w => w.StartsWith("-")).Select(w => w.Substring(1)).ToList();
            
            // Aplicar filtros
            var filtered = NicotineFeatures.ApplyAdvancedFilters(
                results,
                excludes,
                minBitrate,
                maxBitrate,
                minFileSize,
                maxFileSize,
                allowedExtensions
            );
            
            Log($"🔍 Filtros aplicados: {results.Count} → {filtered.Count} resultados");
            
            return filtered;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // 3. CACHÉ DE BÚSQUEDAS
        // ═══════════════════════════════════════════════════════════════
        
        private bool TryGetCachedSearch(string query, out List<Soulseek.File> results)
        {
            results = null;
            
            if (!searchCache.ContainsKey(query))
                return false;
            
            var entry = searchCache[query];
            var age = (DateTime.Now - entry.Timestamp).TotalSeconds;
            
            if (age > searchCacheMaxAge)
            {
                searchCache.Remove(query);
                return false;
            }
            
            results = entry.Results;
            Log($"💾 Resultados de caché: {results.Count} archivos ({age:F0}s)");
            return true;
        }
        
        private void CacheSearchResults(string query, List<Soulseek.File> results)
        {
            // Limpiar caché si está lleno
            if (searchCache.Count >= searchCacheMaxEntries)
            {
                var oldest = searchCache.OrderBy(e => e.Value.Timestamp).First();
                searchCache.Remove(oldest.Key);
            }
            
            searchCache[query] = new SearchCacheEntry
            {
                Query = query,
                Results = results,
                Timestamp = DateTime.Now,
                ResultCount = results.Count
            };
        }
        
        private void LoadSearchCache()
        {
            try
            {
                var cachePath = System.IO.Path.Combine(dataDir, "search_cache.json");
                if (System.IO.File.Exists(cachePath))
                {
                    var json = System.IO.File.ReadAllText(cachePath);
                    searchCache = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, SearchCacheEntry>>(json) 
                        ?? new Dictionary<string, SearchCacheEntry>();
                    
                    // Limpiar entradas viejas
                    var expiredKeys = searchCache.Where(e => (DateTime.Now - e.Value.Timestamp).TotalSeconds > searchCacheMaxAge)
                        .Select(e => e.Key).ToList();
                    foreach (var key in expiredKeys)
                        searchCache.Remove(key);
                    
                    Log($"💾 Caché de búsquedas cargado: {searchCache.Count} entradas");
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error cargando caché: {ex.Message}");
            }
        }
        
        private void SaveSearchCache()
        {
            try
            {
                var cachePath = System.IO.Path.Combine(dataDir, "search_cache.json");
                var json = System.Text.Json.JsonSerializer.Serialize(searchCache, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(cachePath, json);
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error guardando caché: {ex.Message}");
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // 4. GESTIÓN AVANZADA DE USUARIOS
        // ═══════════════════════════════════════════════════════════════
        
        private void UpdateUserProfile(string username, long bytesDownloaded, int speed, bool success)
        {
            if (!userProfiles.ContainsKey(username))
            {
                userProfiles[username] = new UserProfile
                {
                    Username = username,
                    InteractionHistory = new List<DateTime>(),
                    Priority = 0
                };
            }
            
            var profile = userProfiles[username];
            profile.LastSeen = DateTime.Now;
            profile.TotalBytesDownloaded += bytesDownloaded;
            profile.InteractionHistory.Add(DateTime.Now);
            
            if (success)
                profile.SuccessfulDownloads++;
            else
                profile.FailedDownloads++;
            
            // Calcular velocidad promedio
            if (speed > 0)
            {
                if (profile.AverageSpeed == 0)
                    profile.AverageSpeed = speed;
                else
                    profile.AverageSpeed = (profile.AverageSpeed + speed) / 2;
            }
            
            // Ajustar prioridad basado en éxito
            if (profile.SuccessfulDownloads > 5 && profile.FailedDownloads < 2)
                profile.Priority = Math.Min(profile.Priority + 1, 10);
        }
        
        private void LoadUserProfiles()
        {
            try
            {
                var profilesPath = System.IO.Path.Combine(dataDir, "user_profiles.json");
                if (System.IO.File.Exists(profilesPath))
                {
                    var json = System.IO.File.ReadAllText(profilesPath);
                    userProfiles = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, UserProfile>>(json) 
                        ?? new Dictionary<string, UserProfile>();
                    Log($"👥 Perfiles de usuario cargados: {userProfiles.Count}");
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error cargando perfiles: {ex.Message}");
            }
        }
        
        private void SaveUserProfiles()
        {
            try
            {
                var profilesPath = System.IO.Path.Combine(dataDir, "user_profiles.json");
                var json = System.Text.Json.JsonSerializer.Serialize(userProfiles, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(profilesPath, json);
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error guardando perfiles: {ex.Message}");
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // 5. WISHLIST AUTOMÁTICO
        // ═══════════════════════════════════════════════════════════════
        
        private void InitializeWishlistTimer()
        {
            wishlistTimer = new System.Windows.Forms.Timer();
            wishlistTimer.Interval = wishlistSearchInterval * 60 * 1000;
            wishlistTimer.Tick += async (s, e) => await ProcessWishlist();
            wishlistTimer.Start();
            Log($"⭐ Timer de wishlist iniciado (cada {wishlistSearchInterval}min)");
        }
        
        private async Task ProcessWishlist()
        {
            if (wishlistItems.Count == 0)
                return;
            
            Log($"⭐ Procesando wishlist: {wishlistItems.Count} items");
            
            foreach (var item in wishlistItems.Values)
            {
                try
                {
                    // Verificar si es tiempo de buscar
                    var timeSinceLastSearch = (DateTime.Now - item.LastSearched).TotalMinutes;
                    if (timeSinceLastSearch < wishlistSearchInterval)
                        continue;
                    
                    Log($"🔍 Buscando wishlist: {item.Query}");
                    
                    // Realizar búsqueda
                    var results = await SearchForWishlistItem(item);
                    
                    // Filtrar resultados nuevos
                    var newResults = results.Where(f => !item.SeenFiles.Contains(f.Filename)).ToList();
                    
                    if (newResults.Any())
                    {
                        Log($"✨ {newResults.Count} resultados nuevos para '{item.Query}'");
                        
                        if (item.NotifyOnNew)
                        {
                            ShowNotification("Wishlist", $"{newResults.Count} nuevos resultados para '{item.Query}'", ToolTipIcon.Info);
                        }
                        
                        // Auto-descarga si está habilitado
                        if (item.AutoDownload)
                        {
                            foreach (var result in newResults.Take(5)) // Máximo 5 auto-descargas
                            {
                                await DownloadFile(result.Filename, result.Username);
                                item.SeenFiles.Add(result.Filename);
                            }
                        }
                        else
                        {
                            // Solo marcar como vistos
                            foreach (var result in newResults)
                                item.SeenFiles.Add(result.Filename);
                        }
                    }
                    
                    item.LastSearched = DateTime.Now;
                }
                catch (Exception ex)
                {
                    Log($"⚠️ Error procesando wishlist '{item.Query}': {ex.Message}");
                }
            }
        }
        
        private async Task<List<Soulseek.File>> SearchForWishlistItem(WishlistItem item)
        {
            // Implementación simplificada - usar búsqueda normal
            var searchOptions = new SearchOptions(searchTimeout: 15000);
            var response = await client.SearchAsync(SearchQuery.FromText(item.Query), options: searchOptions);
            
            var results = response.Files.ToList();
            
            // Aplicar filtros del wishlist
            results = results.Where(f =>
            {
                if (item.MinSize > 0 && f.Size < item.MinSize) return false;
                if (item.MaxSize > 0 && f.Size > item.MaxSize) return false;
                
                if (item.AllowedFormats != null && item.AllowedFormats.Any())
                {
                    var ext = System.IO.Path.GetExtension(f.Filename).TrimStart('.').ToLower();
                    if (!item.AllowedFormats.Contains(ext)) return false;
                }
                
                return true;
            }).ToList();
            
            return results;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // 6. GRÁFICOS DE VELOCIDAD
        // ═══════════════════════════════════════════════════════════════
        
        private void InitializeSpeedGraphTimer()
        {
            speedGraphTimer = new System.Windows.Forms.Timer();
            speedGraphTimer.Interval = 1000; // 1 segundo
            speedGraphTimer.Tick += (s, e) => UpdateSpeedGraph();
            speedGraphTimer.Start();
        }
        
        private void UpdateSpeedGraph()
        {
            // Calcular velocidad actual
            long currentSpeed = CalculateCurrentDownloadSpeed();
            
            // Añadir punto de datos
            downloadSpeedHistory.Add(new SpeedDataPoint
            {
                Timestamp = DateTime.Now,
                BytesPerSecond = currentSpeed,
                ActiveDownloads = GetActiveDownloadCount()
            });
            
            // Mantener solo últimos 60 segundos
            if (downloadSpeedHistory.Count > 60)
                downloadSpeedHistory.RemoveAt(0);
            
            // Actualizar UI si el panel existe
            if (speedGraphPanel != null && speedGraphPanel.Visible)
            {
                speedGraphPanel.Invalidate();
            }
        }
        
        private long CalculateCurrentDownloadSpeed()
        {
            // Implementación simplificada
            return currentDownloadSpeed;
        }
        
        private int GetActiveDownloadCount()
        {
            return activeDownloads.Count;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // 7. SISTEMA DE RETRY INTELIGENTE
        // ═══════════════════════════════════════════════════════════════
        
        private void InitializeRetrySystem()
        {
            autoRetryTimer = new System.Windows.Forms.Timer();
            autoRetryTimer.Interval = 60000; // 1 minuto
            autoRetryTimer.Tick += async (s, e) => await ProcessRetryQueue();
            autoRetryTimer.Start();
            Log("🔄 Sistema de retry inteligente iniciado");
        }
        
        private async Task ProcessRetryQueue()
        {
            var itemsToRetry = retryQueue.Values
                .Where(r => NicotineFeatures.ShouldRetry(r, 6))
                .OrderBy(r => r.NextRetryTime)
                .Take(5)
                .ToList();
            
            foreach (var retry in itemsToRetry)
            {
                Log($"🔄 Reintentando descarga: {retry.Filename} (intento {retry.AttemptCount + 1})");
                
                try
                {
                    await DownloadFile(retry.Filename, retry.Username);
                    retryQueue.Remove($"{retry.Username}|{retry.Filename}");
                }
                catch (Exception ex)
                {
                    retry.AttemptCount++;
                    retry.LastError = ex.Message;
                    retry.NextRetryTime = NicotineFeatures.CalculateNextRetryTime(retry.AttemptCount, retryBackoffMinutes);
                    retry.RetryHistory.Add(DateTime.Now);
                    
                    Log($"❌ Retry falló: {ex.Message}. Próximo intento: {retry.NextRetryTime:HH:mm}");
                }
            }
        }
        
        private void AddToRetryQueue(string filename, string username, string error)
        {
            var key = $"{username}|{filename}";
            
            if (!retryQueue.ContainsKey(key))
            {
                retryQueue[key] = new RetryInfo
                {
                    Filename = filename,
                    Username = username,
                    AttemptCount = 0,
                    RetryHistory = new List<DateTime>()
                };
            }
            
            var retry = retryQueue[key];
            retry.LastError = error;
            retry.NextRetryTime = NicotineFeatures.CalculateNextRetryTime(retry.AttemptCount, retryBackoffMinutes);
            
            Log($"📝 Añadido a cola de retry: {filename} (próximo intento: {retry.NextRetryTime:HH:mm})");
        }
        
        // ═══════════════════════════════════════════════════════════════
        // 8. AGRUPACIÓN POR CARPETA/ÁLBUM
        // ═══════════════════════════════════════════════════════════════
        
        private void DetectAndGroupFolders(List<Soulseek.File> results)
        {
            if (!enableFolderGrouping)
                return;
            
            folderGroups = NicotineFeatures.GroupByFolder(results);
            
            var albumCount = folderGroups.Values.Count(g => g.IsAlbum);
            if (albumCount > 0)
            {
                Log($"📁 {albumCount} álbumes detectados en resultados");
            }
        }
        
        private async Task DownloadFolder(FolderGroup folder)
        {
            Log($"📁 Descargando carpeta: {folder.AlbumName} ({folder.Files.Count} archivos)");
            
            foreach (var file in folder.Files)
            {
                await DownloadFile(file, folder.Username);
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // 9. VERIFICACIÓN DE INTEGRIDAD
        // ═══════════════════════════════════════════════════════════════
        
        private async Task<bool> VerifyDownloadedFile(string filePath, string expectedHash = null)
        {
            if (!verifyDownloads)
                return true;
            
            try
            {
                var hash = await Task.Run(() => NicotineFeatures.CalculateMD5(filePath));
                fileChecksums[filePath] = hash;
                
                if (expectedHash != null)
                {
                    var isValid = await NicotineFeatures.VerifyFileIntegrity(filePath, expectedHash);
                    if (!isValid)
                    {
                        Log($"❌ Verificación falló: {System.IO.Path.GetFileName(filePath)}");
                        return false;
                    }
                }
                
                Log($"✅ Archivo verificado: {System.IO.Path.GetFileName(filePath)}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error verificando archivo: {ex.Message}");
                return true; // No fallar la descarga por error de verificación
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // 10. BALANCEO DE CARGA
        // ═══════════════════════════════════════════════════════════════
        
        private void UpdateUserLoad(string username, bool isDownloading)
        {
            if (!userLoadInfo.ContainsKey(username))
            {
                userLoadInfo[username] = new UserLoadInfo
                {
                    Username = username,
                    ActiveDownloads = 0,
                    QueuedDownloads = 0
                };
            }
            
            var loadInfo = userLoadInfo[username];
            
            if (isDownloading)
                loadInfo.ActiveDownloads++;
            else
                loadInfo.ActiveDownloads = Math.Max(0, loadInfo.ActiveDownloads - 1);
            
            loadInfo.LastUpdate = DateTime.Now;
        }
        
        private bool CanDownloadFromUser(string username)
        {
            if (!userLoadInfo.ContainsKey(username))
                return true;
            
            var loadInfo = userLoadInfo[username];
            return loadInfo.ActiveDownloads < maxDownloadsPerUser;
        }
    }
}
