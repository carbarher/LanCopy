using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown.UI
{
    /// <summary>
    /// Panel de sección simple con título fijo - SIN funcionalidad de colapsar
    /// Todos los controles siempre visibles
    /// </summary>
    public class SectionPanel : UserControl
    {
        private TableLayoutPanel mainLayout;
        private Panel headerPanel;
        private Label titleLabel;
        private TableLayoutPanel contentLayout;
        
        public Color HeaderColor { get; set; }
        public Color HeaderTextColor { get; set; } = Color.White;
        
        public SectionPanel(string title, Color? headerColor = null)
        {
            HeaderColor = headerColor ?? Color.FromArgb(40, 40, 40);
            InitializeComponents(title);
        }
        
        private void InitializeComponents(string title)
        {
            // UserControl principal
            BackColor = Color.FromArgb(35, 35, 35);
            Margin = new Padding(0, 0, 0, 12);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            
            // TableLayoutPanel principal: 2 filas (header + content)
            mainLayout = new TableLayoutPanel
            {
                RowCount = 2,
                ColumnCount = 1,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            
            // Fila 0: Header fijo 40px, Fila 1: Content auto-size
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            
            // Header panel
            headerPanel = new Panel
            {
                Height = 40,
                Dock = DockStyle.Fill,
                BackColor = HeaderColor,
                Margin = new Padding(0)
            };
            
            // Título
            titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = HeaderTextColor,
                AutoSize = true,
                Location = new Point(15, 10)
            };
            
            headerPanel.Controls.Add(titleLabel);
            
            // Content layout - TableLayoutPanel para controles
            contentLayout = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 0,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.FromArgb(35, 35, 35),
                Padding = new Padding(20, 10, 20, 15),
                Margin = new Padding(0),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            
            // Agregar a mainLayout
            mainLayout.Controls.Add(headerPanel, 0, 0);
            mainLayout.Controls.Add(contentLayout, 0, 1);
            
            Controls.Add(mainLayout);
            
            // Sistema responsive
            this.ParentChanged += (s, e) =>
            {
                if (this.Parent != null)
                {
                    this.Parent.SizeChanged += Parent_SizeChanged;
                }
            };
        }
        
        private void Parent_SizeChanged(object sender, EventArgs e)
        {
            if (this.Parent != null)
            {
                int newWidth = this.Parent.ClientSize.Width - this.Margin.Horizontal;
                if (this.Width != newWidth && newWidth > 0)
                {
                    this.Width = newWidth;
                }
            }
        }
        
        public void AddContent(Control control)
        {
            control.Dock = DockStyle.Top;
            control.Margin = new Padding(0, 0, 0, 8);
            
            contentLayout.RowCount++;
            contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contentLayout.Controls.Add(control, 0, contentLayout.RowCount - 1);
        }
        
        public void AddContentRange(params Control[] controls)
        {
            contentLayout.SuspendLayout();
            
            foreach (var control in controls)
            {
                control.Dock = DockStyle.Top;
                control.Margin = new Padding(0, 0, 0, 8);
                
                contentLayout.RowCount++;
                contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                contentLayout.Controls.Add(control, 0, contentLayout.RowCount - 1);
            }
            
            contentLayout.ResumeLayout(true);
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.Parent != null)
                {
                    this.Parent.SizeChanged -= Parent_SizeChanged;
                }
            }
            base.Dispose(disposing);
        }
    }
}
