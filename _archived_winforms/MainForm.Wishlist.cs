using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SlskDown.Models;
using SlskDown.Core.Wishlist;
using Soulseek;

namespace SlskDown
{
    /// <summary>
    /// Partial class de MainForm para integración de IntelligentWishlist
    /// </summary>
    public partial class MainForm
    {
        // ============================================================================
        // INTEGRACIÓN NICOTINE+: IntelligentWishlist
        // ============================================================================
        
        private void InitializeIntelligentWishlist()
        {
            try
            {
                // Crear instancia de IntelligentWishlist
                intelligentWishlist = new IntelligentWishlist();
                
                // Configurar eventos
                intelligentWishlist.OnNewResultFound += OnWishlistNewResult;
                intelligentWishlist.OnLog += (message) => Log($"[Wishlist] {message}");
                
                // Crear helper para integración
                wishlistHelper = new WishlistIntegrationHelper(
                    intelligentWishlist,
                    SearchForWishlistAsync,
                    DownloadFromWishlistAsync
                );
                
                // Cargar wishlist guardada
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var wishlistPath = Path.Combine(dataDir, "wishlist.json");
                        if (File.Exists(wishlistPath))
                        {
                            await wishlistHelper.LoadAsync(wishlistPath);
                            Log("✅ Wishlist cargada");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"⚠️ Error cargando wishlist: {ex.Message}");
                    }
                });
                
                Log("✅ IntelligentWishlist inicializado");
            }
            catch (Exception ex)
            {
                Log($"❌ Error inicializando IntelligentWishlist: {ex.Message}");
            }
        }
        
        private void OnWishlistNewResult(WishlistItem item, SearchResult result)
        {
            try
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => OnWishlistNewResult(item, result)));
                    return;
                }
                
                Log($"🔔 [Wishlist] Nuevo resultado para '{item.SearchTerm}': {result.FileName} ({FormatFileSize(result.SizeBytes)})");
            }
            catch (Exception ex)
            {
                Log($"❌ Error procesando nuevo resultado de wishlist: {ex.Message}");
            }
        }
        
        private async Task<List<AutoSearchFileResult>> SearchForWishlistAsync(string searchTerm)
        {
            try
            {
                var results = new List<AutoSearchFileResult>();
                
                // Usar el sistema de búsqueda existente
                var searchResponse = await client.SearchAsync(SearchQuery.FromText(searchTerm));
                
                await foreach (var response in searchResponse)
                {
                    foreach (var file in response.Files)
                    {
                        results.Add(new AutoSearchFileResult
                        {
                            Username = response.Username,
                            FileName = file.Filename,
                            SizeBytes = file.Size,
                            Directory = Path.GetDirectoryName(file.Filename),
                            Size = file.Size,
                            Extension = Path.GetExtension(file.Filename),
                            Network = "Soulseek",
                            IsSpanish = false,
                            IsDocument = false
                        });
                    }
                }
                
                return results;
            }
            catch (Exception ex)
            {
                Log($"❌ Error en búsqueda de wishlist: {ex.Message}");
                return new List<AutoSearchFileResult>();
            }
        }
        
        private async Task DownloadFromWishlistAsync(AutoSearchFileResult result)
        {
            try
            {
                // Usar el sistema de descarga existente
                await AddDownloadTask(result);
                Log($"📥 [Wishlist] Auto-descarga iniciada: {result.FileName}");
            }
            catch (Exception ex)
            {
                Log($"❌ Error en auto-descarga de wishlist: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Agrega un término a la wishlist (llamar desde UI)
        /// </summary>
        public void AddToWishlist(string searchTerm, bool autoDownload = false, int intervalMinutes = 60)
        {
            try
            {
                wishlistHelper?.AddItem(searchTerm, autoDownload, TimeSpan.FromMinutes(intervalMinutes));
                Log($"✅ Agregado a wishlist: {searchTerm} (auto-download: {autoDownload})");
                
                // Guardar wishlist
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var wishlistPath = Path.Combine(dataDir, "wishlist.json");
                        await wishlistHelper.SaveAsync(wishlistPath);
                    }
                    catch (Exception ex)
                    {
                        Log($"⚠️ Error guardando wishlist: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Log($"❌ Error agregando a wishlist: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Inicia el procesamiento automático de wishlist
        /// </summary>
        public void StartWishlist()
        {
            try
            {
                wishlistHelper?.Start();
                Log("✅ Wishlist automática iniciada (check cada 5 minutos)");
            }
            catch (Exception ex)
            {
                Log($"❌ Error iniciando wishlist: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Detiene el procesamiento de wishlist
        /// </summary>
        public void StopWishlist()
        {
            try
            {
                wishlistHelper?.Stop();
                Log("⏹️ Wishlist automática detenida");
            }
            catch (Exception ex)
            {
                Log($"❌ Error deteniendo wishlist: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtiene todos los items de la wishlist
        /// </summary>
        public List<WishlistItem> GetWishlistItems()
        {
            try
            {
                return wishlistHelper?.GetAllItems() ?? new List<WishlistItem>();
            }
            catch (Exception ex)
            {
                Log($"❌ Error obteniendo items de wishlist: {ex.Message}");
                return new List<WishlistItem>();
            }
        }
        
        /// <summary>
        /// Remueve un item de la wishlist
        /// </summary>
        public bool RemoveFromWishlist(string searchTerm)
        {
            try
            {
                var removed = wishlistHelper?.RemoveItem(searchTerm) ?? false;
                if (removed)
                {
                    Log($"✅ Removido de wishlist: {searchTerm}");
                    
                    // Guardar wishlist
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var wishlistPath = Path.Combine(dataDir, "wishlist.json");
                            await wishlistHelper.SaveAsync(wishlistPath);
                        }
                        catch (Exception ex)
                        {
                            Log($"⚠️ Error guardando wishlist: {ex.Message}");
                        }
                    });
                }
                return removed;
            }
            catch (Exception ex)
            {
                Log($"❌ Error removiendo de wishlist: {ex.Message}");
                return false;
            }
        }
    }
}
