using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown.Core
{
    /// <summary>
    /// Virtual Scrolling para listas grandes (estilo Nicotine+)
    /// Renderiza solo elementos visibles para manejar 10,000+ items sin lag
    /// </summary>
    public class VirtualScrollingHelper
    {
        private readonly ListView listView;
        private List<object> allItems = new List<object>();
        private int firstVisibleIndex = 0;
        private int visibleItemCount = 0;
        private readonly int itemHeight = 20;
        private readonly Func<object, ListViewItem> itemCreator;
        
        public VirtualScrollingHelper(ListView listView, Func<object, ListViewItem> itemCreator, int itemHeight = 20)
        {
            this.listView = listView;
            this.itemCreator = itemCreator;
            this.itemHeight = itemHeight;
            
            // Configurar ListView para virtual mode
            listView.VirtualMode = true;
            listView.RetrieveVirtualItem += ListView_RetrieveVirtualItem;
            listView.CacheVirtualItems += ListView_CacheVirtualItems;
        }
        
        public void SetItems(List<object> items)
        {
            allItems = items ?? new List<object>();
            listView.VirtualListSize = allItems.Count;
            UpdateVisibleRange();
        }
        
        public void AddItem(object item)
        {
            allItems.Add(item);
            listView.VirtualListSize = allItems.Count;
        }
        
        public void Clear()
        {
            allItems.Clear();
            listView.VirtualListSize = 0;
        }
        
        private void UpdateVisibleRange()
        {
            if (listView.ClientSize.Height > 0)
            {
                visibleItemCount = (listView.ClientSize.Height / itemHeight) + 2; // +2 buffer
            }
        }
        
        private void ListView_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex >= 0 && e.ItemIndex < allItems.Count)
            {
                e.Item = itemCreator(allItems[e.ItemIndex]);
            }
        }
        
        private void ListView_CacheVirtualItems(object sender, CacheVirtualItemsEventArgs e)
        {
            // Aquí se podría implementar caché adicional si es necesario
        }
        
        public int TotalItems => allItems.Count;
        public int VisibleItems => visibleItemCount;
    }
    
    /// <summary>
    /// Búsqueda incremental en resultados sin nueva búsqueda
    /// </summary>
    public class IncrementalSearchHelper
    {
        private readonly List<object> originalItems = new List<object>();
        private readonly Func<object, string> textExtractor;
        private readonly Action<List<object>> updateDisplay;
        private readonly System.Windows.Forms.Timer debounceTimer;
        
        public IncrementalSearchHelper(
            Func<object, string> textExtractor,
            Action<List<object>> updateDisplay,
            int debounceMs = 300)
        {
            this.textExtractor = textExtractor;
            this.updateDisplay = updateDisplay;
            
            debounceTimer = new System.Windows.Forms.Timer { Interval = debounceMs };
            debounceTimer.Tick += (s, e) =>
            {
                debounceTimer.Stop();
                ApplyCurrentFilter();
            };
        }
        
        private string currentFilter = "";
        
        public void SetItems(List<object> items)
        {
            originalItems.Clear();
            originalItems.AddRange(items);
            ApplyCurrentFilter();
        }
        
        public void SetFilter(string filter)
        {
            currentFilter = filter ?? "";
            debounceTimer.Stop();
            debounceTimer.Start();
        }
        
        private void ApplyCurrentFilter()
        {
            if (string.IsNullOrWhiteSpace(currentFilter))
            {
                updateDisplay(originalItems);
                return;
            }
            
            var filtered = originalItems.FindAll(item =>
            {
                var text = textExtractor(item);
                return text.IndexOf(currentFilter, StringComparison.OrdinalIgnoreCase) >= 0;
            });
            
            updateDisplay(filtered);
        }
        
        public void Clear()
        {
            originalItems.Clear();
            currentFilter = "";
            updateDisplay(new List<object>());
        }
        
        public int OriginalCount => originalItems.Count;
    }
}
