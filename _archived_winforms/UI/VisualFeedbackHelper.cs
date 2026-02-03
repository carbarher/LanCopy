using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown.UI
{
    /// <summary>
    /// Helper para mostrar feedback visual temporal (badges, resaltados, tooltips)
    /// </summary>
    public static class VisualFeedbackHelper
    {
        /// <summary>
        /// Muestra un badge temporal junto a un control
        /// </summary>
        public static void ShowTemporaryBadge(Control targetControl, string text, Color backgroundColor, int durationMs = 2000)
        {
            if (targetControl?.Parent == null) return;
            
            var badge = new Label
            {
                Text = text,
                AutoSize = true,
                BackColor = backgroundColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Padding = new Padding(6, 3, 6, 3),
                Location = new Point(
                    targetControl.Right + 10,
                    targetControl.Top + (targetControl.Height - 20) / 2
                )
            };
            
            targetControl.Parent.Controls.Add(badge);
            badge.BringToFront();
            
            // Fade in
            badge.Visible = true;
            
            // Timer para fade out y eliminar
            var timer = new System.Windows.Forms.Timer { Interval = durationMs };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                
                // Fade out animation
                var fadeTimer = new System.Windows.Forms.Timer { Interval = 50 };
                int alpha = 255;
                fadeTimer.Tick += (s2, e2) =>
                {
                    alpha -= 25;
                    if (alpha <= 0)
                    {
                        fadeTimer.Stop();
                        fadeTimer.Dispose();
                        targetControl.Parent.Controls.Remove(badge);
                        badge.Dispose();
                    }
                    else
                    {
                        badge.ForeColor = Color.FromArgb(alpha, Color.White);
                        badge.BackColor = Color.FromArgb(alpha, backgroundColor);
                    }
                };
                fadeTimer.Start();
            };
            timer.Start();
        }
        
        /// <summary>
        /// Resalta temporalmente un control con un borde de color
        /// </summary>
        public static void HighlightControl(Control control, Color highlightColor, int durationMs = 1500)
        {
            if (control == null) return;
            
            var originalBackColor = control.BackColor;
            var highlightBackColor = Color.FromArgb(
                Math.Min(highlightColor.R + 20, 255),
                Math.Min(highlightColor.G + 20, 255),
                Math.Min(highlightColor.B + 20, 255)
            );
            
            // Crear panel de borde
            var borderPanel = new Panel
            {
                Location = new Point(control.Left - 2, control.Top - 2),
                Size = new Size(control.Width + 4, control.Height + 4),
                BackColor = highlightColor
            };
            
            if (control.Parent != null)
            {
                control.Parent.Controls.Add(borderPanel);
                borderPanel.SendToBack();
                
                // Timer para remover borde
                var timer = new System.Windows.Forms.Timer { Interval = durationMs };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    control.Parent.Controls.Remove(borderPanel);
                    borderPanel.Dispose();
                };
                timer.Start();
            }
        }
        
        /// <summary>
        /// Crea un panel de 2 columnas para checkboxes
        /// </summary>
        public static TableLayoutPanel CreateTwoColumnCheckboxGrid(params CheckBox[] checkboxes)
        {
            var grid = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = (checkboxes.Length + 1) / 2,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 5, 0, 5),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            
            for (int i = 0; i < checkboxes.Length; i++)
            {
                int row = i / 2;
                int col = i % 2;
                
                if (i / 2 >= grid.RowStyles.Count)
                {
                    grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                }
                
                checkboxes[i].Margin = new Padding(0, 5, 10, 5);
                checkboxes[i].AutoSize = true;
                grid.Controls.Add(checkboxes[i], col, row);
            }
            
            return grid;
        }
        
        /// <summary>
        /// Crea una fila de configuración con label + control + info opcional
        /// </summary>
        public static FlowLayoutPanel CreateConfigRow(string labelText, Control control, string infoText = null, int labelWidth = 180)
        {
            var row = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 5, 0, 5)
            };
            
            var label = new Label
            {
                Text = labelText,
                Size = new Size(labelWidth, 25),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 10),
                TextAlign = ContentAlignment.MiddleLeft
            };
            
            row.Controls.Add(label);
            row.Controls.Add(control);
            
            if (!string.IsNullOrEmpty(infoText))
            {
                var infoLabel = new Label
                {
                    Text = infoText,
                    AutoSize = true,
                    ForeColor = Color.Gray,
                    Font = new Font("Segoe UI", 8),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Margin = new Padding(8, 0, 0, 0)
                };
                row.Controls.Add(infoLabel);
            }
            
            return row;
        }
        
        /// <summary>
        /// Muestra múltiples badges para valores relacionados
        /// </summary>
        public static void ShowRelatedValuesBadges(Control sourceControl, params (string text, Color color)[] badges)
        {
            if (sourceControl?.Parent == null || badges.Length == 0) return;
            
            int xOffset = sourceControl.Right + 10;
            int yOffset = sourceControl.Top;
            
            foreach (var (text, color) in badges)
            {
                var badge = new Label
                {
                    Text = text,
                    AutoSize = true,
                    BackColor = color,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 8, FontStyle.Bold),
                    Padding = new Padding(6, 3, 6, 3),
                    Location = new Point(xOffset, yOffset)
                };
                
                sourceControl.Parent.Controls.Add(badge);
                badge.BringToFront();
                
                xOffset += badge.Width + 8;
                
                // Auto-remove después de 3 segundos
                var removeTimer = new System.Windows.Forms.Timer { Interval = 3000 };
                removeTimer.Tick += (s, e) =>
                {
                    removeTimer.Stop();
                    removeTimer.Dispose();
                    if (badge.Parent != null)
                    {
                        badge.Parent.Controls.Remove(badge);
                    }
                    badge.Dispose();
                };
                removeTimer.Start();
            }
        }
        
        /// <summary>
        /// Crea un tooltip enriquecido con múltiples líneas
        /// </summary>
        public static void SetRichTooltip(Control control, params string[] lines)
        {
            var tooltip = new ToolTip
            {
                AutoPopDelay = 5000,
                InitialDelay = 500,
                ReshowDelay = 100,
                ShowAlways = true,
                IsBalloon = false
            };
            
            tooltip.SetToolTip(control, string.Join("\n", lines));
        }
    }
}
