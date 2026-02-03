using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SlskDown
{
    /// <summary>
    /// Lazy loading de pestaÃ±as para reducir tiempo de inicio
    /// </summary>
    public class LazyTabLoader
    {
        private readonly TabControl _tabControl;
        private readonly Dictionary<TabPage, Action> _tabInitializers = new Dictionary<TabPage, Action>();
        private readonly HashSet<TabPage> _initializedTabs = new HashSet<TabPage>();
        private readonly object _lock = new object();

        public LazyTabLoader(TabControl tabControl)
        {
            _tabControl = tabControl;
            _tabControl.SelectedIndexChanged += OnTabChanged;
        }

        /// <summary>
        /// Registra una pestaÃ±a para lazy loading
        /// </summary>
        public void RegisterTab(TabPage tab, Action initializer)
        {
            lock (_lock)
            {
                _tabInitializers[tab] = initializer;
            }
        }

        /// <summary>
        /// Inicializa una pestaÃ±a inmediatamente (sin lazy loading)
        /// </summary>
        public void InitializeTabNow(TabPage tab)
        {
            lock (_lock)
            {
                if (!_initializedTabs.Contains(tab) && _tabInitializers.TryGetValue(tab, out var initializer))
                {
                    initializer();
                    _initializedTabs.Add(tab);
                }
            }
        }

        private void OnTabChanged(object sender, EventArgs e)
        {
            var selectedTab = _tabControl.SelectedTab;
            if (selectedTab != null)
            {
                InitializeTabNow(selectedTab);
            }
        }

        /// <summary>
        /// Verifica si una pestaÃ±a ya fue inicializada
        /// </summary>
        public bool IsInitialized(TabPage tab)
        {
            lock (_lock)
            {
                return _initializedTabs.Contains(tab);
            }
        }

        /// <summary>
        /// Obtiene estadÃ­sticas de lazy loading
        /// </summary>
        public (int total, int initialized, int pending) GetStats()
        {
            lock (_lock)
            {
                int total = _tabInitializers.Count;
                int initialized = _initializedTabs.Count;
                int pending = total - initialized;
                return (total, initialized, pending);
            }
        }
    }
}

