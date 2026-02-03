using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using FontAwesome.Sharp;

namespace SlskDown.UI
{
    public class ModernCard : Panel
    {
        private int borderRadius = 10;
        private Color shadowColor = Color.FromArgb(50, 0, 0, 0);
        
        public int BorderRadius
        {
            get => borderRadius;
            set { borderRadius = value; Invalidate(); }
        }
        
        public ModernCard()
        {
            BackColor = Color.FromArgb(45, 45, 48);
            Padding = new Padding(0);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | 
                     ControlStyles.OptimizedDoubleBuffer, true);
        }
        
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            
            // Sombra
            using (var shadowPath = GetRoundedRect(new Rectangle(2, 2, Width - 4, Height - 4), borderRadius))
            using (var shadowBrush = new SolidBrush(shadowColor))
            {
                e.Graphics.FillPath(shadowBrush, shadowPath);
            }
            
            // Fondo del card
            using (var path = GetRoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), borderRadius))
            using (var brush = new SolidBrush(BackColor))
            {
                e.Graphics.FillPath(brush, path);
            }
            
            base.OnPaint(e);
        }
        
        private GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            
            return path;
        }
    }
    
    public class ModernButton : Button
    {
        private bool isHovered = false;
        private Color hoverColor = Color.FromArgb(0, 150, 255);
        private Color normalColor = Color.FromArgb(0, 120, 215);
        private int borderRadius = 8;
        
        public IconChar IconChar { get; set; } = IconChar.None;
        public int IconSize { get; set; } = 20;
        
        public ModernButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = normalColor;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10, FontStyle.Bold);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | 
                     ControlStyles.OptimizedDoubleBuffer, true);
        }
        
        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            isHovered = true;
            Invalidate();
        }
        
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            isHovered = false;
            Invalidate();
        }
        
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            
            var currentColor = isHovered ? hoverColor : normalColor;
            
            using (var path = GetRoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), borderRadius))
            using (var brush = new SolidBrush(currentColor))
            {
                e.Graphics.FillPath(brush, path);
            }
            
            // Dibujar icono si existe
            if (IconChar != IconChar.None)
            {
                using (var iconFont = new Font("Font Awesome 6 Free", IconSize, FontStyle.Regular))
                using (var brush = new SolidBrush(ForeColor))
                {
                    var iconChar = IconChar.ToChar();
                    var iconSize = e.Graphics.MeasureString(iconChar.ToString(), iconFont);
                    var iconX = 10;
                    var iconY = (Height - iconSize.Height) / 2;
                    e.Graphics.DrawString(iconChar.ToString(), iconFont, brush, iconX, iconY);
                }
            }
            
            // Dibujar texto
            using (var brush = new SolidBrush(ForeColor))
            {
                var textSize = e.Graphics.MeasureString(Text, Font);
                var textX = IconChar != IconChar.None ? 40 : (Width - textSize.Width) / 2;
                var textY = (Height - textSize.Height) / 2;
                e.Graphics.DrawString(Text, Font, brush, textX, textY);
            }
        }
        
        private GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            
            return path;
        }
    }
    
    public class ModernTextBox : TextBox
    {
        private Color borderColor = Color.FromArgb(0, 120, 215);
        private Color focusedBorderColor = Color.FromArgb(0, 150, 255);
        private bool isFocused = false;
        
        public ModernTextBox()
        {
            BorderStyle = BorderStyle.None;
            BackColor = Color.FromArgb(60, 60, 65);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 11);
            Padding = new Padding(10);
        }
        
        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            isFocused = true;
            Invalidate();
        }
        
        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            isFocused = false;
            Invalidate();
        }
        
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            
            if (m.Msg == 0xF) // WM_PAINT
            {
                using (var g = Graphics.FromHwnd(Handle))
                {
                    var currentColor = isFocused ? focusedBorderColor : borderColor;
                    using (var pen = new Pen(currentColor, 2))
                    {
                        g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                    }
                }
            }
        }
    }
    
    public class ModernListView : ListView
    {
        public ModernListView()
        {
            View = View.Details;
            FullRowSelect = true;
            GridLines = false;
            BorderStyle = BorderStyle.None;
            BackColor = Color.FromArgb(37, 37, 38);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9);
            OwnerDraw = true;
            DoubleBuffered = true;
            HoverSelection = false;
            
            // NO usar UserPaint cuando OwnerDraw está activo - causa conflictos
            SetStyle(ControlStyles.OptimizedDoubleBuffer | 
                     ControlStyles.AllPaintingInWmPaint, true);
            
            DrawColumnHeader += OnDrawColumnHeader;
            DrawItem += OnDrawItem;
            DrawSubItem += OnDrawSubItem;
        }
        
        private void OnDrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(51, 51, 55)), e.Bounds);
            
            using (var font = new Font("Segoe UI", 10, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.White))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                
                // Añadir un pequeño margen izquierdo al texto del encabezado
                var textBounds = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height);
                e.Graphics.DrawString(e.Header.Text, font, brush, textBounds, sf);
            }

            // Dibujar una línea sutil en la parte inferior del encabezado
            using (var pen = new Pen(Color.FromArgb(80, 80, 80)))
            {
                e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }
        }
        
        private void OnDrawItem(object sender, DrawListViewItemEventArgs e)
        {
            if ((e.State & ListViewItemStates.Selected) != 0)
            {
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(0, 120, 215)), e.Bounds);
            }
            else if (e.ItemIndex % 2 == 0)
            {
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(42, 42, 45)), e.Bounds);
            }
            else
            {
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(37, 37, 38)), e.Bounds);
            }
            
            e.DrawFocusRectangle();
        }
        
        private void OnDrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            var textColor = e.Item.Selected ? Color.White : e.Item.ForeColor;
            
            using (var brush = new SolidBrush(textColor))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                e.Graphics.DrawString(e.SubItem.Text, Font, brush, e.Bounds, sf);
            }
        }
    }
    
    public class ModernProgressBar : ProgressBar
    {
        private Color progressColor = Color.FromArgb(0, 200, 100);
        private Color backgroundColor = Color.FromArgb(60, 60, 65);
        
        public Color ProgressColor
        {
            get => progressColor;
            set { progressColor = value; Invalidate(); }
        }
        
        public ModernProgressBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | 
                     ControlStyles.OptimizedDoubleBuffer, true);
        }
        
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            
            // Fondo
            using (var brush = new SolidBrush(backgroundColor))
            {
                e.Graphics.FillRectangle(brush, 0, 0, Width, Height);
            }
            
            // Barra de progreso
            if (Value > 0)
            {
                var progressWidth = (int)((double)Value / Maximum * Width);
                using (var brush = new LinearGradientBrush(
                    new Rectangle(0, 0, progressWidth, Height),
                    progressColor,
                    Color.FromArgb(progressColor.A, 
                                   Math.Min(255, progressColor.R + 30),
                                   Math.Min(255, progressColor.G + 30),
                                   Math.Min(255, progressColor.B + 30)),
                    LinearGradientMode.Horizontal))
                {
                    e.Graphics.FillRectangle(brush, 0, 0, progressWidth, Height);
                }
            }
            
            // Texto de porcentaje
            var percentage = (int)((double)Value / Maximum * 100);
            var text = $"{percentage}%";
            using (var font = new Font("Segoe UI", 9, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.White))
            {
                var textSize = e.Graphics.MeasureString(text, font);
                var x = (Width - textSize.Width) / 2;
                var y = (Height - textSize.Height) / 2;
                e.Graphics.DrawString(text, font, brush, x, y);
            }
        }
    }
}
