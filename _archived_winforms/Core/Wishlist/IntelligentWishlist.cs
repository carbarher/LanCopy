using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;

namespace SlskDown.Core.Wishlist
{
    /// <summary>
    /// Sistema de wishlist inteligente inspirado en Nicotine+
    /// Realiza búsquedas automáticas periódicas y descarga nuevos resultados
    /// </summary>
    public class IntelligentWishlist
    {
        private readonly Dictionary<string, WishlistItem> items;
        private readonly System.Threading.Timer autoSearchTimer;
        private readonly object itemsLock = new object();
        private bool isRunning;

        public event Action<WishlistItem, SearchResult> OnNewResultFound;
        public event Action<string> OnLog;

        public IntelligentWishlist()
        {
            items = new Dictionary<string, WishlistItem>(StringComparer.OrdinalIgnoreCase);
            autoSearchTimer = new System.Threading.Timer(OnSearchTimerTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Agrega un item a la wishlist
        /// </summary>
        public void AddItem(string searchTerm, bool autoDownload = false, 
            bool notifyNewResults = true, TimeSpan? searchInterval = null)
        {
            lock (itemsLock)
            {
                if (items.ContainsKey(searchTerm))
                {
                    Log($"Item ya existe en wishlist: {searchTerm}");
                    return;
                }

                var item = new WishlistItem
                {
                    SearchTerm = searchTerm,
                    AutoDownload = autoDownload,
                    NotifyNewResults = notifyNewResults,
                    SearchInterval = searchInterval ?? TimeSpan.FromHours(1),
                    SeenResults = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    Filters = new List<SearchFilter>(),
                    CreatedAt = DateTime.UtcNow,
                    LastSearched = null,
                    Enabled = true
                };

                items[searchTerm] = item;
                Log($"Agregado a wishlist: {searchTerm} (auto-download: {autoDownload})");
            }
        }

        /// <summary>
        /// Remueve un item de la wishlist
        /// </summary>
        public bool RemoveItem(string searchTerm)
        {
            lock (itemsLock)
            {
                if (items.Remove(searchTerm))
                {
                    Log($"Removido de wishlist: {searchTerm}");
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Obtiene todos los items de la wishlist
        /// </summary>
        public List<WishlistItem> GetAllItems()
        {
            lock (itemsLock)
            {
                return items.Values.ToList();
            }
        }

        /// <summary>
        /// Inicia el procesamiento automático de la wishlist
        /// </summary>
        public void Start(TimeSpan checkInterval)
        {
            if (isRunning)
            {
                Log("Wishlist ya está en ejecución");
                return;
            }

            isRunning = true;
            autoSearchTimer.Change(TimeSpan.Zero, checkInterval);
            Log($"Wishlist iniciada (check cada {checkInterval.TotalMinutes:F0} minutos)");
        }

        /// <summary>
        /// Detiene el procesamiento automático
        /// </summary>
        public void Stop()
        {
            isRunning = false;
            searchTimer.Change(Timeout.Infinite, Timeout.Infinite);
            Log("Wishlist detenida");
        }

        /// <summary>
        /// Procesa un item de la wishlist manualmente
        /// </summary>
        public async Task<WishlistSearchResult> ProcessItemAsync(string searchTerm, 
            Func<string, Task<List<SearchResult>>> searchFunc)
        {
            WishlistItem item;
            lock (itemsLock)
            {
                if (!items.TryGetValue(searchTerm, out item))
                {
                    return new WishlistSearchResult
                    {
                        Success = false,
                        ErrorMessage = "Item no encontrado en wishlist"
                    };
                }

                if (!item.Enabled)
                {
                    return new WishlistSearchResult
                    {
                        Success = false,
                        ErrorMessage = "Item deshabilitado"
                    };
                }
            }

            try
            {
                Log($"Buscando: {searchTerm}");
                var results = await searchFunc(searchTerm);
                
                var newResults = new List<SearchResult>();
                
                lock (itemsLock)
                {
                    foreach (var result in results)
                    {
                        var resultKey = GetResultKey(result);
                        
                        if (!item.SeenResults.Contains(resultKey))
                        {
                            // Verificar filtros
                            if (PassesFilters(result, item.Filters))
                            {
                                newResults.Add(result);
                                item.SeenResults.Add(resultKey);
                                
                                // Notificar nuevo resultado
                                OnNewResultFound?.Invoke(item, result);
                                
                                if (item.NotifyNewResults)
                                {
                                    Log($"🆕 Nuevo resultado: {result.FileName} de {result.Username}");
                                }
                            }
                        }
                    }
                    
                    item.LastSearched = DateTime.UtcNow;
                    item.TotalSearches++;
                    item.TotalResultsFound += results.Count;
                    item.NewResultsFound += newResults.Count;
                }

                Log($"Búsqueda completada: {searchTerm} ({newResults.Count} nuevos de {results.Count} totales)");

                return new WishlistSearchResult
                {
                    Success = true,
                    SearchTerm = searchTerm,
                    TotalResults = results.Count,
                    NewResults = newResults,
                    AutoDownloadEnabled = item.AutoDownload
                };
            }
            catch (Exception ex)
            {
                Log($"Error buscando '{searchTerm}': {ex.Message}");
                return new WishlistSearchResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Agrega un filtro a un item de la wishlist
        /// </summary>
        public void AddFilter(string searchTerm, SearchFilter filter)
        {
            lock (itemsLock)
            {
                if (items.TryGetValue(searchTerm, out var item))
                {
                    item.Filters.Add(filter);
                    Log($"Filtro agregado a '{searchTerm}': {filter.Type}");
                }
            }
        }

        /// <summary>
        /// Guarda la wishlist en un archivo JSON
        /// </summary>
        public async Task SaveToFileAsync(string filePath)
        {
            try
            {
                List<WishlistItem> snapshot;
                lock (itemsLock)
                {
                    snapshot = items.Values.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(filePath, json);
                Log($"Wishlist guardada: {snapshot.Count} items");
            }
            catch (Exception ex)
            {
                Log($"Error guardando wishlist: {ex.Message}");
            }
        }

        /// <summary>
        /// Carga la wishlist desde un archivo JSON
        /// </summary>
        public async Task LoadFromFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Log("Archivo de wishlist no encontrado");
                    return;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var loadedItems = JsonSerializer.Deserialize<List<WishlistItem>>(json);

                lock (itemsLock)
                {
                    items.Clear();
                    foreach (var item in loadedItems)
                    {
                        items[item.SearchTerm] = item;
                    }
                }

                Log($"Wishlist cargada: {loadedItems.Count} items");
            }
            catch (Exception ex)
            {
                Log($"Error cargando wishlist: {ex.Message}");
            }
        }

        private void OnSearchTimerTick(object state)
        {
            if (!isRunning)
                return;

            List<WishlistItem> itemsToProcess;
            lock (itemsLock)
            {
                var now = DateTime.UtcNow;
                itemsToProcess = items.Values
                    .Where(i => i.Enabled && ShouldSearch(i, now))
                    .ToList();
            }

            if (itemsToProcess.Count > 0)
            {
                Log($"⏰ Procesando {itemsToProcess.Count} items de wishlist");
            }
        }

        private bool ShouldSearch(WishlistItem item, DateTime now)
        {
            if (!item.LastSearched.HasValue)
                return true;

            return (now - item.LastSearched.Value) >= item.SearchInterval;
        }

        private bool PassesFilters(SearchResult result, List<SearchFilter> filters)
        {
            if (filters == null || filters.Count == 0)
                return true;

            foreach (var filter in filters)
            {
                if (!filter.Matches(result))
                    return false;
            }

            return true;
        }

        private string GetResultKey(SearchResult result)
        {
            return $"{result.Username}|{result.FileName}|{result.SizeBytes}";
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }

        public void Dispose()
        {
            Stop();
            autoSearchTimer?.Dispose();
        }
    }

    /// <summary>
    /// Item de la wishlist - USAR SlskDown.Models.WishlistItem en su lugar
    /// </summary>
    /*
    public class WishlistItem
    {
        public string SearchTerm { get; set; }
        public bool AutoDownload { get; set; }
        public bool NotifyNewResults { get; set; }
        public TimeSpan SearchInterval { get; set; }
        public HashSet<string> SeenResults { get; set; }
        public List<SearchFilter> Filters { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastSearched { get; set; }
        public bool Enabled { get; set; }
        public int TotalSearches { get; set; }
        public int TotalResultsFound { get; set; }
        public int NewResultsFound { get; set; }
    }
    */

    /// <summary>
    /// Filtro de búsqueda para wishlist
    /// </summary>
    public class SearchFilter
    {
        public FilterType Type { get; set; }
        public string Value { get; set; }

        public bool Matches(SearchResult result)
        {
            return Type switch
            {
                FilterType.MinSize => result.SizeBytes >= long.Parse(Value),
                FilterType.MaxSize => result.SizeBytes <= long.Parse(Value),
                FilterType.Extension => result.FileName.EndsWith(Value, StringComparison.OrdinalIgnoreCase),
                FilterType.ExcludeExtension => !result.FileName.EndsWith(Value, StringComparison.OrdinalIgnoreCase),
                FilterType.ContainsKeyword => result.FileName.Contains(Value, StringComparison.OrdinalIgnoreCase),
                FilterType.ExcludesKeyword => !result.FileName.Contains(Value, StringComparison.OrdinalIgnoreCase),
                _ => true
            };
        }
    }

    public enum FilterType
    {
        MinSize,
        MaxSize,
        Extension,
        ExcludeExtension,
        ContainsKeyword,
        ExcludesKeyword
    }

    /// <summary>
    /// Resultado de búsqueda (simplificado)
    /// </summary>
    public class SearchResult
    {
        public string Username { get; set; }
        public string FileName { get; set; }
        public long SizeBytes { get; set; }
        public string Directory { get; set; }
    }

    /// <summary>
    /// Resultado de procesamiento de wishlist
    /// </summary>
    public class WishlistSearchResult
    {
        public bool Success { get; set; }
        public string SearchTerm { get; set; }
        public int TotalResults { get; set; }
        public List<SearchResult> NewResults { get; set; }
        public bool AutoDownloadEnabled { get; set; }
        public string ErrorMessage { get; set; }
    }
}
