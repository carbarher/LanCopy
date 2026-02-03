using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SlskDown.Models;

namespace SlskDown.Core.Wishlist
{
    /// <summary>
    /// Helper para integrar IntelligentWishlist en MainForm sin modificar mucho código
    /// </summary>
    public class WishlistIntegrationHelper
    {
        private readonly IntelligentWishlist wishlist;
        private readonly Func<string, Task<List<AutoSearchFileResult>>> searchFunc;
        private readonly Func<AutoSearchFileResult, Task> downloadFunc;

        public WishlistIntegrationHelper(
            IntelligentWishlist wishlist,
            Func<string, Task<List<AutoSearchFileResult>>> searchFunc,
            Func<AutoSearchFileResult, Task> downloadFunc)
        {
            this.wishlist = wishlist;
            this.searchFunc = searchFunc;
            this.downloadFunc = downloadFunc;
        }

        /// <summary>
        /// Inicia el procesamiento automático de wishlist
        /// </summary>
        public void Start()
        {
            wishlist.Start(TimeSpan.FromMinutes(5)); // Check cada 5 minutos
        }

        /// <summary>
        /// Detiene el procesamiento
        /// </summary>
        public void Stop()
        {
            wishlist.Stop();
        }

        /// <summary>
        /// Procesa un item de wishlist manualmente
        /// </summary>
        public async Task<WishlistSearchResult> ProcessItemAsync(string searchTerm)
        {
            var result = await wishlist.ProcessItemAsync(searchTerm, async (term) =>
            {
                var results = await searchFunc(term);
                return results.Select(r => new SearchResult
                {
                    Username = r.Username,
                    FileName = r.FileName,
                    SizeBytes = r.SizeBytes,
                    Directory = r.Directory
                }).ToList();
            });

            // Si hay auto-download habilitado y hay nuevos resultados, descargarlos
            if (result.Success && result.AutoDownloadEnabled && result.NewResults.Count > 0)
            {
                foreach (var newResult in result.NewResults)
                {
                    try
                    {
                        // Buscar el resultado original para descargar
                        var originalResults = await searchFunc(searchTerm);
                        var matchingResult = originalResults.FirstOrDefault(r =>
                            r.Username == newResult.Username &&
                            r.FileName == newResult.FileName &&
                            r.SizeBytes == newResult.SizeBytes);

                        if (matchingResult != null)
                        {
                            await downloadFunc(matchingResult);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error auto-downloading: {ex.Message}");
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Agrega un item a la wishlist
        /// </summary>
        public void AddItem(string searchTerm, bool autoDownload = false, TimeSpan? interval = null)
        {
            wishlist.AddItem(searchTerm, autoDownload, notifyNewResults: true, interval);
        }

        /// <summary>
        /// Remueve un item
        /// </summary>
        public bool RemoveItem(string searchTerm)
        {
            return wishlist.RemoveItem(searchTerm);
        }

        /// <summary>
        /// Obtiene todos los items
        /// </summary>
        public List<WishlistItem> GetAllItems()
        {
            return wishlist.GetAllItems();
        }

        /// <summary>
        /// Guarda la wishlist
        /// </summary>
        public async Task SaveAsync(string filePath)
        {
            await wishlist.SaveToFileAsync(filePath);
        }

        /// <summary>
        /// Carga la wishlist
        /// </summary>
        public async Task LoadAsync(string filePath)
        {
            await wishlist.LoadFromFileAsync(filePath);
        }
    }
}
