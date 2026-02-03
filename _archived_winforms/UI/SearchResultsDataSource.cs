using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SlskDown
{
    /// <summary>
    /// Interfaz para data source del Virtual ListView
    /// </summary>
    public interface IVirtualListDataSource
    {
        /// <summary>
        /// Número total de items
        /// </summary>
        int Count { get; }
        
        /// <summary>
        /// Obtiene un item por índice
        /// </summary>
        ListViewItem GetItem(int index);
    }

    /// <summary>
    /// Item de resultado de búsqueda
    /// </summary>
    public class SearchResultItem
    {
        public string Filename { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public int Bitrate { get; set; }
        public int Length { get; set; }
        public string FolderPath { get; set; } = string.Empty;
        public int QueueLength { get; set; }
        public int FreeUploadSlots { get; set; }
        public int UploadSpeed { get; set; }
        
        // Metadata adicional
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
        public bool IsDownloaded { get; set; }
        public bool IsQueued { get; set; }
        public int QualityScore { get; set; } = 100; // Puntuación de calidad (0-100)
        public double RelevanceScore { get; set; }

        // Red de origen del resultado (siempre Soulseek)
        public string Network { get; set; } = "Soulseek";
        public string Source { get; set; } = "Soulseek"; // Alias de Network
        
        // Autor del archivo (para ebooks)
        public string Author { get; set; } = string.Empty;
        
        // Propiedades adicionales para optimizaciones avanzadas
        public int Quality { get; set; } = 100; // Alias de QualityScore
        public int Speed { get; set; } = 0; // Velocidad de descarga estimada
        public bool HasFreeSlot { get; set; } = false; // Tiene slot libre
    }
}

namespace SlskDown.UI
{
    /// <summary>
    /// Data source optimizado para resultados de búsqueda
    /// </summary>
    public class SearchResultsDataSource : IVirtualListDataSource
    {
        private List<SearchResultItem> _items;
        private readonly Func<SearchResultItem, ListViewItem> _itemFactory;
        
        public SearchResultsDataSource(Func<SearchResultItem, ListViewItem>? itemFactory = null)
        {
            _items = new List<SearchResultItem>();
            _itemFactory = itemFactory ?? DefaultItemFactory;
        }
        
        public int Count => _items.Count;
        
        /// <summary>
        /// Establece los items
        /// </summary>
        public void SetItems(IEnumerable<SearchResultItem> items)
        {
            // using (PerformanceMetrics.Instance.Track("SearchResults.SetItems")) // DESACTIVADO: PerformanceMetrics excluido
            {
                _items = items.ToList();
            }
        }
        
        /// <summary>
        /// Agrega items
        /// </summary>
        public void AddItems(IEnumerable<SearchResultItem> items)
        {
            // using (PerformanceMetrics.Instance.Track("SearchResults.AddItems")) // DESACTIVADO: PerformanceMetrics excluido
            {
                _items.AddRange(items);
            }
        }
        
        /// <summary>
        /// Limpia los items
        /// </summary>
        public void Clear()
        {
            _items.Clear();
        }
        
        /// <summary>
        /// Filtra items
        /// </summary>
        public void Filter(Func<SearchResultItem, bool> predicate)
        {
            // using (PerformanceMetrics.Instance.Track("SearchResults.Filter")) // DESACTIVADO: PerformanceMetrics excluido
            {
                _items = _items.Where(predicate).ToList();
            }
        }
        
        /// <summary>
        /// Ordena items
        /// </summary>
        public void Sort(Comparison<SearchResultItem> comparison)
        {
            // using (PerformanceMetrics.Instance.Track("SearchResults.Sort")) // DESACTIVADO: PerformanceMetrics excluido
            {
                _items.Sort(comparison);
            }
        }
        
        /// <summary>
        /// Obtiene un item del ListView
        /// </summary>
        public ListViewItem GetItem(int index)
        {
            if (index < 0 || index >= _items.Count)
                return new ListViewItem("Invalid index");
            
            return _itemFactory(_items[index]);
        }
        
        /// <summary>
        /// Obtiene el item de datos original
        /// </summary>
        public SearchResultItem GetDataItem(int index)
        {
            return _items[index];
        }
        
        /// <summary>
        /// Factory por defecto para crear ListViewItems
        /// </summary>
        private ListViewItem DefaultItemFactory(SearchResultItem item)
        {
            var listItem = new ListViewItem(item.Filename)
            {
                Tag = item
            };
            
            listItem.SubItems.Add(FormatFileSize(item.Size));
            listItem.SubItems.Add(item.Username);
            listItem.SubItems.Add(item.Extension);
            listItem.SubItems.Add(item.Bitrate > 0 ? $"{item.Bitrate} kbps" : "");
            listItem.SubItems.Add(item.Length > 0 ? FormatDuration(item.Length) : "");
            
            // Color según calidad
            if (item.Bitrate >= 320)
                listItem.ForeColor = Color.LightGreen;
            else if (item.Bitrate >= 192)
                listItem.ForeColor = Color.White;
            else if (item.Bitrate > 0)
                listItem.ForeColor = Color.LightGray;
            
            return listItem;
        }
        
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }
        
        private string FormatDuration(int seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.Hours > 0 
                ? $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes}:{ts.Seconds:D2}";
        }
    }
}
