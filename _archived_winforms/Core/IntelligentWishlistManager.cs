using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Gestor de wishlist inteligente con búsquedas automáticas y filtros persistentes
    /// </summary>
    public class IntelligentWishlistManager
    {
        private List<IntelligentWishlistItem> _items;
        private readonly string _wishlistFilePath;
        private readonly FilterPresetManager _filterManager;
        private readonly NotificationManager _notificationManager;
        private readonly object _lock = new object();
        private System.Threading.Timer _autoSearchTimer;

        public event EventHandler<IntelligentWishlistItem> ItemAdded;
        public event EventHandler<IntelligentWishlistItem> ItemRemoved;
        public event EventHandler<IntelligentWishlistItem> ItemUpdated;
        public event EventHandler<WishlistSearchResultsEventArgs> NewResultsFound;

        public IntelligentWishlistManager(
            string dataDirectory,
            FilterPresetManager filterManager,
            NotificationManager notificationManager)
        {
            _wishlistFilePath = Path.Combine(dataDirectory, "intelligent_wishlist.json");
            _filterManager = filterManager;
            _notificationManager = notificationManager;
            _items = new List<IntelligentWishlistItem>();
            
            LoadWishlist();
            StartAutoSearch();
        }

        /// <summary>
        /// Obtiene todos los items de wishlist
        /// </summary>
        public List<IntelligentWishlistItem> GetAllItems()
        {
            lock (_lock)
            {
                return new List<IntelligentWishlistItem>(_items);
            }
        }

        /// <summary>
        /// Obtiene un item por ID
        /// </summary>
        public IntelligentWishlistItem GetItem(string id)
        {
            lock (_lock)
            {
                return _items.FirstOrDefault(i => i.Id == id);
            }
        }

        /// <summary>
        /// Agrega un nuevo item a la wishlist
        /// </summary>
        public IntelligentWishlistItem AddItem(string searchQuery, string filterId = null, SavedSearchFilter customFilter = null)
        {
            lock (_lock)
            {
                var item = new IntelligentWishlistItem
                {
                    SearchQuery = searchQuery,
                    FilterId = filterId,
                    CustomFilter = customFilter
                };

                _items.Add(item);
                SaveWishlist();
                ItemAdded?.Invoke(this, item);

                return item;
            }
        }

        /// <summary>
        /// Actualiza un item existente
        /// </summary>
        public bool UpdateItem(IntelligentWishlistItem item)
        {
            lock (_lock)
            {
                var existing = _items.FirstOrDefault(i => i.Id == item.Id);
                if (existing == null)
                    return false;

                _items.Remove(existing);
                _items.Add(item);
                SaveWishlist();
                ItemUpdated?.Invoke(this, item);

                return true;
            }
        }

        /// <summary>
        /// Elimina un item de la wishlist
        /// </summary>
        public bool RemoveItem(string id)
        {
            lock (_lock)
            {
                var item = _items.FirstOrDefault(i => i.Id == id);
                if (item == null)
                    return false;

                _items.Remove(item);
                SaveWishlist();
                ItemRemoved?.Invoke(this, item);

                return true;
            }
        }

        /// <summary>
        /// Descarta un resultado para un item específico
        /// </summary>
        public void DismissResult(string itemId, string resultHash)
        {
            lock (_lock)
            {
                var item = _items.FirstOrDefault(i => i.Id == itemId);
                if (item != null)
                {
                    item.DismissResult(resultHash);
                    SaveWishlist();
                }
            }
        }

        /// <summary>
        /// Procesa resultados de búsqueda para un item de wishlist
        /// </summary>
        public List<WishlistSearchResult> ProcessSearchResults(
            string itemId,
            List<WishlistSearchResult> results)
        {
            lock (_lock)
            {
                var item = _items.FirstOrDefault(i => i.Id == itemId);
                if (item == null)
                    return new List<WishlistSearchResult>();

                var filter = item.GetEffectiveFilter(_filterManager);
                var filteredResults = new List<WishlistSearchResult>();
                int newCount = 0;

                foreach (var result in results)
                {
                    // Generar hash del resultado
                    result.ResultHash = IntelligentWishlistItem.GenerateResultHash(
                        result.Username, result.Filename, result.SizeBytes);

                    // Verificar si está descartado
                    if (!item.ShouldShowResult(result.ResultHash))
                        continue;

                    // Aplicar filtro si existe
                    if (filter != null)
                    {
                        if (!filter.MatchesFilter(result.Filename, result.SizeBytes, result.Bitrate, result.HasFreeSlot))
                            continue;
                    }

                    // Marcar como nuevo si no lo habíamos visto antes
                    result.IsNew = !item.DismissedResultHashes.Contains(result.ResultHash);
                    if (result.IsNew)
                        newCount++;

                    filteredResults.Add(result);
                }

                // Actualizar estadísticas
                item.LastSearchTime = DateTime.Now;
                item.TotalResultsFound += filteredResults.Count;
                item.NewResultsCount = newCount;

                // Notificar si hay nuevos resultados
                if (newCount > 0 && item.NotifyOnNewResults)
                {
                    var timeSinceLastNotification = DateTime.Now - item.LastNotificationTime;
                    if (timeSinceLastNotification.TotalMinutes >= 30) // No notificar más de cada 30 min
                    {
                        _notificationManager?.NotifyWishlistMatch(item.SearchQuery, newCount);
                        item.LastNotificationTime = DateTime.Now;
                    }
                }

                SaveWishlist();

                // Disparar evento
                NewResultsFound?.Invoke(this, new WishlistSearchResultsEventArgs
                {
                    Item = item,
                    Results = filteredResults,
                    NewResultsCount = newCount
                });

                return filteredResults;
            }
        }

        /// <summary>
        /// Obtiene items que necesitan búsqueda automática
        /// </summary>
        public List<IntelligentWishlistItem> GetItemsNeedingSearch()
        {
            lock (_lock)
            {
                return _items.Where(i => i.ShouldSearchNow()).ToList();
            }
        }

        /// <summary>
        /// Limpia resultados descartados antiguos de todos los items
        /// </summary>
        public void CleanupOldDismissedResults()
        {
            lock (_lock)
            {
                foreach (var item in _items)
                {
                    item.CleanupOldDismissedResults();
                }
                SaveWishlist();
            }
        }

        /// <summary>
        /// Exporta la wishlist a un archivo
        /// </summary>
        public void ExportWishlist(string filePath)
        {
            lock (_lock)
            {
                var json = JsonSerializer.Serialize(_items, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(filePath, json);
            }
        }

        /// <summary>
        /// Importa wishlist desde un archivo
        /// </summary>
        public int ImportWishlist(string filePath, bool merge = false)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var importedItems = JsonSerializer.Deserialize<List<IntelligentWishlistItem>>(json);

                if (importedItems == null || importedItems.Count == 0)
                    return 0;

                lock (_lock)
                {
                    if (!merge)
                    {
                        _items.Clear();
                    }

                    foreach (var item in importedItems)
                    {
                        // Generar nuevo ID para evitar conflictos
                        item.Id = Guid.NewGuid().ToString();
                        _items.Add(item);
                    }

                    SaveWishlist();
                    return importedItems.Count;
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Inicia el timer de búsqueda automática
        /// </summary>
        private void StartAutoSearch()
        {
            // Verificar cada 5 minutos si hay items que necesitan búsqueda
            _autoSearchTimer = new System.Threading.Timer(AutoSearchCallback, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Callback del timer de búsqueda automática
        /// </summary>
        private void AutoSearchCallback(object state)
        {
            // Este método será llamado por el timer
            // La búsqueda real debe ser implementada por el MainForm
            // que tiene acceso al cliente de Soulseek
        }

        /// <summary>
        /// Carga wishlist desde archivo
        /// </summary>
        private void LoadWishlist()
        {
            try
            {
                if (File.Exists(_wishlistFilePath))
                {
                    var json = File.ReadAllText(_wishlistFilePath);
                    _items = JsonSerializer.Deserialize<List<IntelligentWishlistItem>>(json) 
                        ?? new List<IntelligentWishlistItem>();
                }
            }
            catch
            {
                _items = new List<IntelligentWishlistItem>();
            }
        }

        /// <summary>
        /// Guarda wishlist a archivo
        /// </summary>
        private void SaveWishlist()
        {
            try
            {
                var json = JsonSerializer.Serialize(_items, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var directory = Path.GetDirectoryName(_wishlistFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(_wishlistFilePath, json);
            }
            catch
            {
                // Ignorar errores de guardado
            }
        }

        public void Dispose()
        {
            _autoSearchTimer?.Dispose();
        }
    }

    /// <summary>
    /// Argumentos del evento de nuevos resultados de wishlist
    /// </summary>
    public class WishlistSearchResultsEventArgs : EventArgs
    {
        public IntelligentWishlistItem Item { get; set; }
        public List<WishlistSearchResult> Results { get; set; }
        public int NewResultsCount { get; set; }
    }
}
