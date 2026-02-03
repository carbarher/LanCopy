using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

namespace SlskDown.UI
{
    /// <summary>
    /// Contenedor especializado para gestionar CollapsiblePanels sin solapamientos
    /// Usa posicionamiento manual absoluto para control total del layout
    /// </summary>
    public class CollapsibleContainer : TableLayoutPanel
    {
        private List<CollapsiblePanel> panels = new List<CollapsiblePanel>();
        
        public CollapsibleContainer()
        {
            ColumnCount = 1;
            RowCount = 0;
            AutoScroll = true;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Color.Transparent;
            Padding = new Padding(10);
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None;
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            
            // Sistema responsive
            this.SizeChanged += CollapsibleContainer_SizeChanged;
        }
        
        private void CollapsibleContainer_SizeChanged(object sender, EventArgs e)
        {
            // Ajustar ancho de todos los paneles al cambiar el tamaño del contenedor
            SuspendLayout();
            
            foreach (var panel in panels)
            {
                if (panel.Visible)
                {
                    int newWidth = this.ClientSize.Width - this.Padding.Horizontal;
                    if (this.AutoScroll && this.VerticalScroll.Visible)
                    {
                        newWidth -= SystemInformation.VerticalScrollBarWidth;
                    }
                    
                    if (panel.Width != newWidth && newWidth > 0)
                    {
                        panel.Width = newWidth;
                    }
                }
            }
            
            ResumeLayout(true);
        }
        
        public void AddPanel(CollapsiblePanel panel)
        {
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(0, 0, 0, 0);
            
            // Suscribirse al evento de expansión/colapso
            panel.ExpandedChanged += (s, e) => this.PerformLayout();
            
            RowCount++;
            RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(panel, 0, RowCount - 1);
            
            panels.Add(panel);
        }
        
        public void RecalculateLayout()
        {
            PerformLayout();
        }
        
    }
}
