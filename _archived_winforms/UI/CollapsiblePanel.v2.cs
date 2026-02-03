using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown.UI
{
    /// <summary>
    /// Panel colapsable COMPLETAMENTE REESCRITO usando TableLayoutPanel
    /// Solución definitiva sin solapamientos
    /// </summary>
    public class CollapsiblePanelV2 : UserControl
    {
        private TableLayoutPanel mainLayout;
        private Panel headerPanel;
        private Label iconLabel;
        private Label titleLabel;
        private TableLayoutPanel contentLayout;
        private System.Windows.Forms.Timer animationTimer;
        private int targetHeight;
        private int currentContentHeight;
        private bool isAnimating = false;
        
        public bool IsExpanded { get; private set; }
        public Color HeaderColor { get; set; }
        public Color HeaderTextColor { get; set; } = Color.White;
        public Color ExpandedIconColor { get; set; } = Color.FromArgb(100, 200, 255);
        
        public event EventHandler ExpandedChanged;
        
        public CollapsiblePanelV2(string title, bool expandedByDefault = false, Color? headerColor = null)
        {
            IsExpanded = expandedByDefault;
            HeaderColor = headerColor ?? Color.FromArgb(40, 40, 40);
            
            InitializeComponent(title);
            SetupAnimation();
            
            if (!IsExpanded)
            {
                contentLayout.Visible = false;
                Height = 45;
            }
        }
        
        private void InitializeComponent(string title)
        {
            // UserControl principal
            BackColor = Color.FromArgb(35, 35, 35);
            Margin = new Padding(0, 0, 0, 8);
            AutoSize = false;
            
            // TableLayoutPanel principal: 2 filas (header + content)
            mainLayout = new TableLayoutPanel
            {
                RowCount = 2,
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            
            // Fila 0: Header fijo 45px
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            // Fila 1: Content auto-size
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            
            // Header panel
            headerPanel = new Panel
            {
                Height = 45,
                Dock = DockStyle.Fill,
                BackColor = HeaderColor,
                Cursor = Cursors.Hand,
                Margin = new Padding(0)
            };
            
            // Icono
            iconLabel = new Label
            {
                Text = IsExpanded ? "▼" : "▶",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = IsExpanded ? ExpandedIconColor : Color.Gray,
                AutoSize = true,
                Location = new Point(15, 13),
                Cursor = Cursors.Hand
            };
            
            // Título
            titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = HeaderTextColor,
                AutoSize = true,
                Location = new Point(45, 12),
                Cursor = Cursors.Hand
            };
            
            headerPanel.Controls.Add(iconLabel);
            headerPanel.Controls.Add(titleLabel);
            
            // Eventos de clic
            headerPanel.Click += (s, e) => Toggle();
            iconLabel.Click += (s, e) => Toggle();
            titleLabel.Click += (s, e) => Toggle();
            
            // Hover effect
            headerPanel.MouseEnter += (s, e) =>
            {
                headerPanel.BackColor = Color.FromArgb(
                    Math.Min(HeaderColor.R + 10, 255),
                    Math.Min(HeaderColor.G + 10, 255),
                    Math.Min(HeaderColor.B + 10, 255)
                );
            };
            headerPanel.MouseLeave += (s, e) => headerPanel.BackColor = HeaderColor;
            
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
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                Visible = IsExpanded
            };
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            
            // Agregar a mainLayout
            mainLayout.Controls.Add(headerPanel, 0, 0);
            mainLayout.Controls.Add(contentLayout, 0, 1);
            
            Controls.Add(mainLayout);
        }
        
        private void SetupAnimation()
        {
            animationTimer = new System.Windows.Forms.Timer { Interval = 10 };
            animationTimer.Tick += AnimationTimer_Tick;
        }
        
        public void Toggle()
        {
            if (isAnimating) return;
            
            IsExpanded = !IsExpanded;
            UpdateIcon();
            
            if (IsExpanded)
            {
                contentLayout.Visible = true;
                contentLayout.PerformLayout();
                currentContentHeight = contentLayout.PreferredSize.Height;
                targetHeight = 45 + currentContentHeight;
            }
            else
            {
                targetHeight = 45;
            }
            
            isAnimating = true;
            animationTimer.Start();
            
            ExpandedChanged?.Invoke(this, EventArgs.Empty);
        }
        
        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            int step = Math.Max(1, Math.Abs(targetHeight - Height) / 15);
            
            if (IsExpanded)
            {
                if (Height < targetHeight)
                {
                    Height = Math.Min(Height + step, targetHeight);
                }
                else
                {
                    Height = targetHeight;
                    isAnimating = false;
                    animationTimer.Stop();
                }
            }
            else
            {
                if (Height > targetHeight)
                {
                    Height = Math.Max(Height - step, targetHeight);
                }
                else
                {
                    Height = targetHeight;
                    contentLayout.Visible = false;
                    isAnimating = false;
                    animationTimer.Stop();
                }
            }
        }
        
        private void UpdateIcon()
        {
            iconLabel.Text = IsExpanded ? "▼" : "▶";
            iconLabel.ForeColor = IsExpanded ? ExpandedIconColor : Color.Gray;
        }
        
        public void AddContent(Control control)
        {
            control.Dock = DockStyle.Top;
            control.Margin = new Padding(0, 0, 0, 5);
            
            contentLayout.RowCount++;
            contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contentLayout.Controls.Add(control, 0, contentLayout.RowCount - 1);
            
            if (IsExpanded && !isAnimating)
            {
                contentLayout.PerformLayout();
                Height = 45 + contentLayout.PreferredSize.Height;
            }
        }
        
        public void AddContentRange(params Control[] controls)
        {
            contentLayout.SuspendLayout();
            
            foreach (var control in controls)
            {
                control.Dock = DockStyle.Top;
                control.Margin = new Padding(0, 0, 0, 5);
                
                contentLayout.RowCount++;
                contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                contentLayout.Controls.Add(control, 0, contentLayout.RowCount - 1);
            }
            
            contentLayout.ResumeLayout();
            
            if (IsExpanded && !isAnimating)
            {
                contentLayout.PerformLayout();
                Height = 45 + contentLayout.PreferredSize.Height;
            }
        }
        
        public void Expand()
        {
            if (!IsExpanded) Toggle();
        }
        
        public void Collapse()
        {
            if (IsExpanded) Toggle();
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                animationTimer?.Stop();
                animationTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
