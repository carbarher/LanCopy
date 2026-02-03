using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown.UI
{
    /// <summary>
    /// ListView optimizado con modo virtual y cache inteligente
    /// Puede manejar millones de items sin problemas de performance
    /// </summary>
    public class VirtualListView : ListView
    {
        private IVirtualListDataSource? _dataSource;
        private readonly VirtualListCache _cache;
        private int _lastFirstVisibleIndex = -1;
        private int _lastVisibleCount = 0;
        
        public VirtualListView()
        {
            _cache = new VirtualListCache(maxSize: 1000);
            
            // Configurar para modo virtual
            VirtualMode = true;
            DoubleBuffered = true;
            View = View.Details;
            FullRowSelect = true;
            GridLines = false;
            
            // Eventos
            RetrieveVirtualItem += OnRetrieveVirtualItem;
            CacheVirtualItems += OnCacheVirtualItems;
            
            // Optimizaciones visuales
            SetStyle(ControlStyles.OptimizedDoubleBuffer | 
                     ControlStyles.AllPaintingInWmPaint, true);
        }
        
        /// <summary>
        /// Establece el data source
        /// </summary>
        public void SetDataSource(IVirtualListDataSource dataSource)
        {
            _dataSource = dataSource;
            VirtualListSize = dataSource.Count;
            _cache.Clear();
            Invalidate();
        }
        
        /// <summary>
        /// Actualiza el data source sin recrear items
        /// </summary>
        public void RefreshDataSource()
        {
            if (_dataSource != null)
            {
                VirtualListSize = _dataSource.Count;
                _cache.Clear();
                Invalidate();
            }
        }
        
        /// <summary>
        /// Obtiene item del cache o lo crea
        /// </summary>
        private void OnRetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
        {
            if (_dataSource == null)
            {
                e.Item = new ListViewItem("No data");
                return;
            }
            
            // ERROR: using (PerformanceMetrics.Instance.Track("VirtualListView.RetrieveItem"))
            {
                // Intentar obtener del cache
                var item = _cache.GetItem(e.ItemIndex);
                
                if (item == null)
                {
                    // No está en cache, crear nuevo
                    item = _dataSource.GetItem(e.ItemIndex);
                    _cache.AddItem(e.ItemIndex, item);
                }
                
                e.Item = item;
            }
        }
        
        /// <summary>
        /// Pre-cachea items que están por ser visibles
        /// </summary>
        private void OnCacheVirtualItems(object? sender, CacheVirtualItemsEventArgs e)
        {
            if (_dataSource == null)
                return;
            
            // ERROR: using (PerformanceMetrics.Instance.Track("VirtualListView.CacheItems"))
            {
                // Solo pre-cachear si el rango cambió significativamente
                if (e.StartIndex == _lastFirstVisibleIndex && 
                    (e.EndIndex - e.StartIndex) == _lastVisibleCount)
                {
                    return;
                }
                
                _lastFirstVisibleIndex = e.StartIndex;
                _lastVisibleCount = e.EndIndex - e.StartIndex;
                
                // Pre-cachear items visibles + buffer
                int bufferSize = 50;
                int startIndex = Math.Max(0, e.StartIndex - bufferSize);
                int endIndex = Math.Min(_dataSource.Count - 1, e.EndIndex + bufferSize);
                
                for (int i = startIndex; i <= endIndex; i++)
                {
                    if (!_cache.Contains(i))
                    {
                        var item = _dataSource.GetItem(i);
                        _cache.AddItem(i, item);
                    }
                }
            }
        }
        
        /// <summary>
        /// Limpia el cache
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }
        
        /// <summary>
        /// Obtiene estadísticas del cache
        /// </summary>
        public CacheStats GetCacheStats()
        {
            return _cache.GetStats();
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cache.Clear();
            }
            base.Dispose(disposing);
        }
    }
    
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
    /// Cache inteligente para items del ListView
    /// Usa LRU (Least Recently Used) para mantener items más usados
    /// </summary>
    internal class VirtualListCache
    {
        private readonly Dictionary<int, CacheEntry> _cache;
        private readonly LinkedList<int> _lruList;
        private readonly int _maxSize;
        private int _hits;
        private int _misses;
        
        public VirtualListCache(int maxSize)
        {
            _maxSize = maxSize;
            _cache = new Dictionary<int, CacheEntry>(maxSize);
            _lruList = new LinkedList<int>();
        }
        
        public ListViewItem? GetItem(int index)
        {
            if (_cache.TryGetValue(index, out var entry))
            {
                // Cache hit - mover al frente de LRU
                _hits++;
                _lruList.Remove(entry.LruNode);
                entry.LruNode = _lruList.AddFirst(index);
                return entry.Item;
            }
            
            // Cache miss
            _misses++;
            return null;
        }
        
        public void AddItem(int index, ListViewItem item)
        {
            // Si ya existe, actualizar
            if (_cache.ContainsKey(index))
            {
                var existing = _cache[index];
                _lruList.Remove(existing.LruNode);
                existing.Item = item;
                existing.LruNode = _lruList.AddFirst(index);
                return;
            }
            
            // Si el cache está lleno, eliminar el menos usado (LRU)
            if (_cache.Count >= _maxSize && _lruList.Last != null)
            {
                var lruIndex = _lruList.Last.Value;
                _lruList.RemoveLast();
                _cache.Remove(lruIndex);
            }
            
            // Agregar nuevo item
            var node = _lruList.AddFirst(index);
            _cache[index] = new CacheEntry
            {
                Item = item,
                LruNode = node
            };
        }
        
        public bool Contains(int index)
        {
            return _cache.ContainsKey(index);
        }
        
        public void Clear()
        {
            _cache.Clear();
            _lruList.Clear();
            _hits = 0;
            _misses = 0;
        }
        
        public CacheStats GetStats()
        {
            return new CacheStats
            {
                Size = _cache.Count,
                MaxSize = _maxSize,
                Hits = _hits,
                Misses = _misses,
                HitRate = _hits + _misses > 0 ? (_hits * 100.0) / (_hits + _misses) : 0
            };
        }
        
        private class CacheEntry
        {
            public ListViewItem Item { get; set; } = null!;
            public LinkedListNode<int> LruNode { get; set; } = null!;
        }
    }
    
    /// <summary>
    /// Estadísticas del cache
    /// </summary>
    public class CacheStats
    {
        public int Size { get; set; }
        public int MaxSize { get; set; }
        public int Hits { get; set; }
        public int Misses { get; set; }
        public double HitRate { get; set; }
        
        public override string ToString()
        {
            return $"Cache: {Size}/{MaxSize} items, Hit rate: {HitRate:F1}% ({Hits} hits, {Misses} misses)";
        }
    }
}
