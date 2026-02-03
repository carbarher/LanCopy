using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown
{
    /// <summary>
    /// OptimizaciÃ³n de ListView usando VirtualMode para manejar grandes cantidades de datos
    /// </summary>
    public class VirtualListViewOptimization
    {
        private ListView _listView;
        private List<SearchResult> _data = new List<SearchResult>();
        private Dictionary<int, ListViewItem> _cache = new Dictionary<int, ListViewItem>();
        private int _cacheSize = 100;

        public VirtualListViewOptimization(ListView listView)
        {
            _listView = listView;
            _listView.VirtualMode = true;
            _listView.RetrieveVirtualItem += OnRetrieveVirtualItem;
            _listView.CacheVirtualItems += OnCacheVirtualItems;
        }

        public void SetData(List<SearchResult> data)
        {
            _data = data;
            _cache.Clear();
            _listView.VirtualListSize = data.Count;
            _listView.Invalidate();
        }

        public void AddItem(SearchResult item)
        {
            _data.Add(item);
            _listView.VirtualListSize = _data.Count;
        }

        public void Clear()
        {
            _data.Clear();
            _cache.Clear();
            _listView.VirtualListSize = 0;
        }

        public SearchResult GetItem(int index)
        {
            if (index >= 0 && index < _data.Count)
                return _data[index];
            return null;
        }

        public List<SearchResult> GetSelectedItems()
        {
            var selected = new List<SearchResult>();
            foreach (int index in _listView.SelectedIndices)
            {
                if (index < _data.Count)
                    selected.Add(_data[index]);
            }
            return selected;
        }

        private void OnRetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            // Buscar en cachÃ© primero
            if (_cache.TryGetValue(e.ItemIndex, out var cachedItem))
            {
                e.Item = cachedItem;
                return;
            }

            // Crear nuevo item
            if (e.ItemIndex < _data.Count)
            {
                var result = _data[e.ItemIndex];
                var item = CreateListViewItem(result);
                
                // Agregar a cachÃ© si hay espacio
                if (_cache.Count < _cacheSize)
                {
                    _cache[e.ItemIndex] = item;
                }
                
                e.Item = item;
            }
        }

        private void OnCacheVirtualItems(object sender, CacheVirtualItemsEventArgs e)
        {
            // Pre-cargar items en el rango visible
            for (int i = e.StartIndex; i <= e.EndIndex && i < _data.Count; i++)
            {
                if (!_cache.ContainsKey(i))
                {
                    var result = _data[i];
                    var item = CreateListViewItem(result);
                    
                    if (_cache.Count < _cacheSize)
                    {
                        _cache[i] = item;
                    }
                }
            }
        }

        public static ListViewItem CreateListViewItem(SearchResult result)
        {
            var item = new ListViewItem(result.Username);
            item.SubItems.Add(result.Country ?? "");
            item.SubItems.Add(System.IO.Path.GetFileName(result.Filename));
            
            // Formatear tamaÃ±o
            string sizeStr = Optimizations.FormatSize(result.Size);
            item.SubItems.Add(sizeStr);
            
            item.SubItems.Add(result.Extension);
            item.SubItems.Add(result.Bitrate > 0 ? $"{result.Bitrate} kbps" : "");
            item.SubItems.Add(result.Length > 0 ? TimeSpan.FromSeconds(result.Length.Value).ToString(@"mm\:ss") : "");
            item.SubItems.Add(System.IO.Path.GetDirectoryName(result.Filename) ?? "");
            
            item.Tag = result;
            
            // Colorear segÃºn calidad
            if (result.Bitrate >= 320)
                item.BackColor = Color.FromArgb(50, 70, 50);
            else if (result.Bitrate >= 256)
                item.BackColor = Color.FromArgb(50, 60, 50);
            
            return item;
        }

        public int Count => _data.Count;
    }
}

