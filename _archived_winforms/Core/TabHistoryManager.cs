using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SlskDown.Core
{
    /// <summary>
    /// MEJORA #12 (Nicotine+ 3.3.0): Reopen Closed Tab
    /// Permite reabrir pestañas cerradas con Ctrl+Shift+T
    /// </summary>
    public class TabHistoryManager
    {
        private readonly Stack<TabPageInfo> closedTabs = new Stack<TabPageInfo>();
        private readonly int maxHistory;
        private readonly Action<string> log;

        public TabHistoryManager(int maxHistory = 10, Action<string> log = null)
        {
            this.maxHistory = maxHistory;
            this.log = log;
        }

        /// <summary>
        /// Registra una pestaña cerrada
        /// </summary>
        public void RecordClosedTab(TabPage tab, int originalIndex)
        {
            if (tab == null) return;

            var info = new TabPageInfo
            {
                TabPage = tab,
                OriginalIndex = originalIndex,
                ClosedTime = DateTime.Now,
                Title = tab.Text
            };

            closedTabs.Push(info);

            // Limitar historial
            while (closedTabs.Count > maxHistory)
            {
                var oldest = closedTabs.ToArray()[closedTabs.Count - 1];
                var temp = new Stack<TabPageInfo>();
                
                // Mover todos excepto el más antiguo
                while (closedTabs.Count > 1)
                {
                    temp.Push(closedTabs.Pop());
                }
                
                // Descartar el más antiguo
                closedTabs.Pop();
                
                // Restaurar el resto
                while (temp.Count > 0)
                {
                    closedTabs.Push(temp.Pop());
                }
            }

            log?.Invoke($"Pestaña '{info.Title}' guardada en historial ({closedTabs.Count}/{maxHistory})");
        }

        /// <summary>
        /// Reabre la última pestaña cerrada
        /// </summary>
        public bool ReopenLastTab(TabControl tabControl)
        {
            if (closedTabs.Count == 0)
            {
                log?.Invoke("No hay pestañas cerradas para reabrir");
                return false;
            }

            var info = closedTabs.Pop();
            
            // Insertar en la posición original (o al final si ya no es válida)
            var insertIndex = Math.Min(info.OriginalIndex, tabControl.TabPages.Count);
            tabControl.TabPages.Insert(insertIndex, info.TabPage);
            tabControl.SelectedTab = info.TabPage;

            var elapsed = DateTime.Now - info.ClosedTime;
            log?.Invoke($"Pestaña '{info.Title}' reabierta (cerrada hace {elapsed.TotalSeconds:F0}s)");

            return true;
        }

        /// <summary>
        /// Obtiene el número de pestañas en el historial
        /// </summary>
        public int GetHistoryCount()
        {
            return closedTabs.Count;
        }

        /// <summary>
        /// Limpia el historial
        /// </summary>
        public void ClearHistory()
        {
            var count = closedTabs.Count;
            closedTabs.Clear();
            log?.Invoke($"Historial de pestañas limpiado ({count} entradas)");
        }

        /// <summary>
        /// Obtiene información del historial
        /// </summary>
        public List<TabPageInfo> GetHistory()
        {
            return new List<TabPageInfo>(closedTabs);
        }

        public class TabPageInfo
        {
            public TabPage TabPage { get; set; }
            public int OriginalIndex { get; set; }
            public DateTime ClosedTime { get; set; }
            public string Title { get; set; }

            public string DisplayText
            {
                get
                {
                    var elapsed = DateTime.Now - ClosedTime;
                    if (elapsed.TotalMinutes < 1)
                        return $"{Title} (hace {elapsed.TotalSeconds:F0}s)";
                    else if (elapsed.TotalHours < 1)
                        return $"{Title} (hace {elapsed.TotalMinutes:F0}m)";
                    else
                        return $"{Title} (hace {elapsed.TotalHours:F1}h)";
                }
            }
        }
    }

    /// <summary>
    /// Extension methods para TabControl
    /// </summary>
    public static class TabControlExtensions
    {
        /// <summary>
        /// Procesa teclas para Ctrl+Shift+T
        /// </summary>
        public static bool ProcessTabShortcut(this Form form, Keys keyData, TabHistoryManager historyManager, TabControl tabControl)
        {
            // Ctrl+Shift+T: Reabrir última pestaña cerrada
            if (keyData == (Keys.Control | Keys.Shift | Keys.T))
            {
                return historyManager.ReopenLastTab(tabControl);
            }

            // Ctrl+W: Cerrar pestaña actual (opcional)
            if (keyData == (Keys.Control | Keys.W))
            {
                if (tabControl.SelectedTab != null && tabControl.TabPages.Count > 1)
                {
                    var tab = tabControl.SelectedTab;
                    var index = tabControl.SelectedIndex;
                    tabControl.TabPages.Remove(tab);
                    historyManager.RecordClosedTab(tab, index);
                    return true;
                }
            }

            return false;
        }
    }
}
