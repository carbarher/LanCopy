using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SlskDown.Models;
using SlskDown.UI;

namespace SlskDown
{
    /// <summary>
    /// Virtual ListView optimizado para manejar millones de resultados de búsqueda
    /// sin consumir RAM excesiva. Solo renderiza items visibles.
    /// </summary>
    public class VirtualSearchResults
    {
        private ListView listView;
        private List<SearchResultItem> allItems;
        private List<SearchResultItem> filteredItems;
        private readonly object itemsLock = new object();
        
        // Cache de items renderizados (solo los visibles)
        private Dictionary<int, ListViewItem> itemCache;
        private const int CACHE_SIZE = 100;
        
        public VirtualSearchResults(ListView lv)
        {
            listView = lv;
            allItems = new List<SearchResultItem>();
            filteredItems = new List<SearchResultItem>();
            itemCache = new Dictionary<int, ListViewItem>(CACHE_SIZE);
            
            InitializeVirtualMode();
        }
        
        private void InitializeVirtualMode()
        {
            listView.VirtualMode = true;
            listView.VirtualListSize = 0;
            
            // Evento para recuperar items virtuales (solo cuando son visibles)
            listView.RetrieveVirtualItem += OnRetrieveVirtualItem;
            
            // Evento para cachear items visibles
            listView.CacheVirtualItems += OnCacheVirtualItems;
            
            // Limpiar caché cuando se hace scroll
            listView.Scroll += (s, e) => ClearCache();
        }
        
        private void OnRetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            // Intentar obtener del caché primero
            if (itemCache.TryGetValue(e.ItemIndex, out var cachedItem))
            {
                e.Item = cachedItem;
                return;
            }
            
            // Crear item si no está en caché
            lock (itemsLock)
            {
                if (e.ItemIndex < filteredItems.Count)
                {
                    var item = CreateListViewItem(filteredItems[e.ItemIndex]);
                    
                    // Agregar al caché si hay espacio
                    if (itemCache.Count < CACHE_SIZE)
                    {
                        itemCache[e.ItemIndex] = item;
                    }
                    
                    e.Item = item;
                }
            }
        }
        
        private void OnCacheVirtualItems(object sender, CacheVirtualItemsEventArgs e)
        {
            // Pre-cachear items que están a punto de ser visibles
            lock (itemsLock)
            {
                for (int i = e.StartIndex; i <= e.EndIndex && i < filteredItems.Count; i++)
                {
                    if (!itemCache.ContainsKey(i) && itemCache.Count < CACHE_SIZE)
                    {
                        itemCache[i] = CreateListViewItem(filteredItems[i]);
                    }
                }
            }
        }
        
        private ListViewItem CreateListViewItem(SearchResultItem item)
        {
            var listItem = new ListViewItem(item.Filename);
            listItem.SubItems.Add(item.Extension);
            listItem.SubItems.Add(item.Username);
            listItem.SubItems.Add(FormatSize(item.Size));
            listItem.SubItems.Add(item.UploadSpeed > 0 ? $"{item.UploadSpeed / 1024:F0} KB/s" : "-");
            listItem.SubItems.Add(item.FolderPath);
            listItem.Tag = item;
            
            // Aplicar colores según calidad
            if (item.Extension.Equals(".flac", StringComparison.OrdinalIgnoreCase))
                listItem.ForeColor = Color.FromArgb(50, 255, 255);
            else if (item.Size > 100 * 1024 * 1024)
                listItem.ForeColor = Color.FromArgb(255, 80, 80);
            
            return listItem;
        }
        
        public void SetItems(List<SearchResultItem> items)
        {
            lock (itemsLock)
            {
                allItems = new List<SearchResultItem>(items);
                filteredItems = new List<SearchResultItem>(items);
                ClearCache();
                
                // Actualizar tamaño virtual
                if (listView.InvokeRequired)
                {
                    listView.BeginInvoke(new Action(() =>
                    {
                        listView.VirtualListSize = filteredItems.Count;
                        listView.Invalidate();
                    }));
                }
                else
                {
                    listView.VirtualListSize = filteredItems.Count;
                    listView.Invalidate();
                }
            }
        }
        
        public void AddItems(List<SearchResultItem> newItems)
        {
            lock (itemsLock)
            {
                allItems.AddRange(newItems);
                filteredItems.AddRange(newItems);
                ClearCache();
                
                // Actualizar tamaño virtual
                if (listView.InvokeRequired)
                {
                    listView.BeginInvoke(new Action(() =>
                    {
                        listView.VirtualListSize = filteredItems.Count;
                        listView.Invalidate();
                    }));
                }
                else
                {
                    listView.VirtualListSize = filteredItems.Count;
                    listView.Invalidate();
                }
            }
        }
        
        public void ApplyFilter(Func<SearchResultItem, bool> predicate)
        {
            lock (itemsLock)
            {
                filteredItems = allItems.Where(predicate).ToList();
                ClearCache();
                
                if (listView.InvokeRequired)
                {
                    listView.BeginInvoke(new Action(() =>
                    {
                        listView.VirtualListSize = filteredItems.Count;
                        listView.Invalidate();
                    }));
                }
                else
                {
                    listView.VirtualListSize = filteredItems.Count;
                    listView.Invalidate();
                }
            }
        }
        
        public void ClearFilter()
        {
            lock (itemsLock)
            {
                filteredItems = new List<SearchResultItem>(allItems);
                ClearCache();
                
                if (listView.InvokeRequired)
                {
                    listView.BeginInvoke(new Action(() =>
                    {
                        listView.VirtualListSize = filteredItems.Count;
                        listView.Invalidate();
                    }));
                }
                else
                {
                    listView.VirtualListSize = filteredItems.Count;
                    listView.Invalidate();
                }
            }
        }
        
        public void Clear()
        {
            lock (itemsLock)
            {
                allItems.Clear();
                filteredItems.Clear();
                ClearCache();
                
                if (listView.InvokeRequired)
                {
                    listView.BeginInvoke(new Action(() =>
                    {
                        listView.VirtualListSize = 0;
                        listView.Invalidate();
                    }));
                }
                else
                {
                    listView.VirtualListSize = 0;
                    listView.Invalidate();
                }
            }
        }
        
        private void ClearCache()
        {
            itemCache.Clear();
        }
        
        public int Count
        {
            get
            {
                lock (itemsLock)
                {
                    return filteredItems.Count;
                }
            }
        }
        
        public int TotalCount
        {
            get
            {
                lock (itemsLock)
                {
                    return allItems.Count;
                }
            }
        }
        
        public SearchResultItem GetItem(int index)
        {
            lock (itemsLock)
            {
                if (index >= 0 && index < filteredItems.Count)
                    return filteredItems[index];
                return null;
            }
        }
        
        public List<SearchResultItem> GetSelectedItems()
        {
            var selected = new List<SearchResultItem>();
            
            lock (itemsLock)
            {
                foreach (int index in listView.SelectedIndices)
                {
                    if (index < filteredItems.Count)
                        selected.Add(filteredItems[index]);
                }
            }
            
            return selected;
        }
        
        private static string FormatSize(long bytes)
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
    }
}
