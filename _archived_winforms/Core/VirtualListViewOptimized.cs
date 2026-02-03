using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown.Core
{
    /// <summary>
    /// ListView optimizado con virtual scrolling para millones de items
    /// Renderiza solo items visibles, soporta listas enormes sin lag
    /// </summary>
    public class VirtualListViewOptimized : ListView
    {
        private List<object> _dataSource = new();
        private int _visibleStart = 0;
        private int _visibleEnd = 0;
        private const int BufferSize = 20; // Items extra arriba/abajo
        private int _itemHeight = 20;
        private readonly Dictionary<int, ListViewItem> _itemCache = new();
        private const int MaxCacheSize = 1000;

        public VirtualListViewOptimized()
        {
            VirtualMode = true;
            OwnerDraw = false;
            DoubleBuffered = true;
            
            // Eventos de virtual mode
            RetrieveVirtualItem += OnRetrieveVirtualItem;
            CacheVirtualItems += OnCacheVirtualItems;
            
            // Optimizaciones
            SetStyle(ControlStyles.OptimizedDoubleBuffer | 
                     ControlStyles.AllPaintingInWmPaint, true);
        }

        /// <summary>
        /// Establece el origen de datos
        /// </summary>
        public void SetDataSource<T>(List<T> dataSource)
        {
            _dataSource = new List<object>(dataSource.Count);
            foreach (var item in dataSource)
            {
                _dataSource.Add(item!);
            }
            
            VirtualListSize = _dataSource.Count;
            _itemCache.Clear();
            
            Invalidate();
        }

        /// <summary>
        /// Agrega item al final
        /// </summary>
        public void AddItem(object item)
        {
            _dataSource.Add(item);
            VirtualListSize = _dataSource.Count;
        }

        /// <summary>
        /// Agrega múltiples items
        /// </summary>
        public void AddItems(IEnumerable<object> items)
        {
            _dataSource.AddRange(items);
            VirtualListSize = _dataSource.Count;
        }

        /// <summary>
        /// Limpia todos los items
        /// </summary>
        public void ClearItems()
        {
            _dataSource.Clear();
            _itemCache.Clear();
            VirtualListSize = 0;
        }

        /// <summary>
        /// Obtiene item en índice
        /// </summary>
        public object? GetItem(int index)
        {
            if (index >= 0 && index < _dataSource.Count)
                return _dataSource[index];
            return null;
        }

        /// <summary>
        /// Actualiza item en índice
        /// </summary>
        public void UpdateItem(int index, object item)
        {
            if (index >= 0 && index < _dataSource.Count)
            {
                _dataSource[index] = item;
                
                // Invalidar caché para este item
                _itemCache.Remove(index);
                
                // Refrescar si está visible
                if (index >= _visibleStart && index <= _visibleEnd)
                {
                    RedrawItems(index, index, false);
                }
            }
        }

        /// <summary>
        /// Evento para recuperar item virtual
        /// </summary>
        private void OnRetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
        {
            // Verificar caché primero
            if (_itemCache.TryGetValue(e.ItemIndex, out var cachedItem))
            {
                e.Item = cachedItem;
                return;
            }

            // Crear nuevo item
            var item = CreateListViewItem(e.ItemIndex);
            
            // Agregar a caché si no está lleno
            if (_itemCache.Count < MaxCacheSize)
            {
                _itemCache[e.ItemIndex] = item;
            }
            
            e.Item = item;
        }

        /// <summary>
        /// Evento para cachear rango de items
        /// </summary>
        private void OnCacheVirtualItems(object? sender, CacheVirtualItemsEventArgs e)
        {
            _visibleStart = Math.Max(0, e.StartIndex - BufferSize);
            _visibleEnd = Math.Min(_dataSource.Count - 1, e.EndIndex + BufferSize);

            // Limpiar caché de items fuera del rango visible
            var keysToRemove = new List<int>();
            foreach (var key in _itemCache.Keys)
            {
                if (key < _visibleStart || key > _visibleEnd)
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _itemCache.Remove(key);
            }

            // Pre-cachear items visibles
            for (int i = _visibleStart; i <= _visibleEnd && i < _dataSource.Count; i++)
            {
                if (!_itemCache.ContainsKey(i))
                {
                    _itemCache[i] = CreateListViewItem(i);
                }
            }
        }

        /// <summary>
        /// Crea ListViewItem desde objeto (override en subclase)
        /// </summary>
        protected virtual ListViewItem CreateListViewItem(int index)
        {
            var data = _dataSource[index];
            var item = new ListViewItem(data.ToString() ?? "");
            item.Tag = data;
            return item;
        }

        /// <summary>
        /// Calcula altura de item
        /// </summary>
        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            _itemHeight = Font.Height + 4;
        }

        /// <summary>
        /// Obtiene estadísticas de rendimiento
        /// </summary>
        public VirtualListStats GetStats()
        {
            return new VirtualListStats
            {
                TotalItems = _dataSource.Count,
                CachedItems = _itemCache.Count,
                VisibleStart = _visibleStart,
                VisibleEnd = _visibleEnd,
                VisibleCount = _visibleEnd - _visibleStart + 1
            };
        }
    }

    /// <summary>
    /// ListView virtual especializado para resultados de búsqueda
    /// </summary>
    public class SearchResultsVirtualListView : VirtualListViewOptimized
    {
        public SearchResultsVirtualListView()
        {
            View = View.Details;
            FullRowSelect = true;
            GridLines = true;

            // Columnas
            Columns.Add("Archivo", 300);
            Columns.Add("Tamaño", 100);
            Columns.Add("Usuario", 150);
            Columns.Add("Calidad", 80);
            Columns.Add("Velocidad", 100);
        }

        protected override ListViewItem CreateListViewItem(int index)
        {
            var data = GetItem(index);
            
            if (data is SearchResultItem result)
            {
                var item = new ListViewItem(result.Filename ?? "");
                item.SubItems.Add(FormatFileSize(result.Size));
                item.SubItems.Add(result.Username ?? "");
                item.SubItems.Add(result.Quality.ToString());
                item.SubItems.Add(FormatSpeed(result.Speed));
                item.Tag = result;

                // Colorear según calidad
                if (result.Quality >= 80)
                    item.BackColor = Color.LightGreen;
                else if (result.Quality >= 50)
                    item.BackColor = Color.LightYellow;
                else
                    item.BackColor = Color.LightCoral;

                return item;
            }

            return base.CreateListViewItem(index);
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        private string FormatSpeed(int speed)
        {
            if (speed == 0)
                return "N/A";
            
            return speed >= 1024 
                ? $"{speed / 1024.0:F1} MB/s" 
                : $"{speed} KB/s";
        }
    }

    /// <summary>
    /// ListView virtual para descargas
    /// </summary>
    public class DownloadsVirtualListView : VirtualListViewOptimized
    {
        public DownloadsVirtualListView()
        {
            View = View.Details;
            FullRowSelect = true;
            GridLines = true;

            Columns.Add("Archivo", 250);
            Columns.Add("Progreso", 100);
            Columns.Add("Velocidad", 100);
            Columns.Add("Estado", 100);
            Columns.Add("Usuario", 120);
        }

        protected override ListViewItem CreateListViewItem(int index)
        {
            var data = GetItem(index);
            
            if (data is DownloadItem download)
            {
                var item = new ListViewItem(download.Filename ?? "");
                item.SubItems.Add($"{download.Progress:F1}%");
                item.SubItems.Add(FormatSpeed(download.Speed));
                item.SubItems.Add(download.Status);
                item.SubItems.Add(download.Username ?? "");
                item.Tag = download;

                // Colorear según estado
                item.BackColor = download.Status switch
                {
                    "Completado" => Color.LightGreen,
                    "Descargando" => Color.LightBlue,
                    "En cola" => Color.LightYellow,
                    "Error" => Color.LightCoral,
                    _ => Color.White
                };

                return item;
            }

            return base.CreateListViewItem(index);
        }

        private string FormatSpeed(double speedMBps)
        {
            if (speedMBps == 0)
                return "N/A";
            
            return speedMBps >= 1 
                ? $"{speedMBps:F2} MB/s" 
                : $"{speedMBps * 1024:F0} KB/s";
        }
    }

    #region DTOs

    public class VirtualListStats
    {
        public int TotalItems { get; set; }
        public int CachedItems { get; set; }
        public int VisibleStart { get; set; }
        public int VisibleEnd { get; set; }
        public int VisibleCount { get; set; }
    }

    public class DownloadItem
    {
        public string Filename { get; set; } = "";
        public double Progress { get; set; }
        public double Speed { get; set; }
        public string Status { get; set; } = "";
        public string Username { get; set; } = "";
    }

    #endregion
}
