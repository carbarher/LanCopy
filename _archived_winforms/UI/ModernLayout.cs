using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown.UI
{
    /// <summary>
    /// Sistema de layout moderno con constantes de diseño y helpers responsive
    /// </summary>
    public static class ModernLayout
    {
        // === PALETA DE COLORES MODERNA ===
        public static class Colors
        {
            // Backgrounds
            public static readonly Color Background = Color.FromArgb(20, 20, 20);
            public static readonly Color BackgroundLight = Color.FromArgb(30, 30, 30);
            public static readonly Color BackgroundCard = Color.FromArgb(35, 35, 35);
            public static readonly Color BackgroundHover = Color.FromArgb(45, 45, 45);
            
            // Primarios
            public static readonly Color Primary = Color.FromArgb(0, 120, 215);
            public static readonly Color PrimaryHover = Color.FromArgb(0, 140, 235);
            public static readonly Color PrimaryLight = Color.FromArgb(30, 150, 225);
            
            // Secundarios
            public static readonly Color Success = Color.FromArgb(16, 185, 129);
            public static readonly Color Warning = Color.FromArgb(245, 158, 11);
            public static readonly Color Error = Color.FromArgb(239, 68, 68);
            public static readonly Color Info = Color.FromArgb(59, 130, 246);
            
            // Textos
            public static readonly Color TextPrimary = Color.FromArgb(255, 255, 255);
            public static readonly Color TextSecondary = Color.FromArgb(156, 163, 175);
            public static readonly Color TextMuted = Color.FromArgb(107, 114, 128);
            
            // Acentos
            public static readonly Color Gold = Color.FromArgb(251, 191, 36);
            public static readonly Color Purple = Color.FromArgb(168, 85, 247);
            public static readonly Color Pink = Color.FromArgb(236, 72, 153);
            public static readonly Color Cyan = Color.FromArgb(6, 182, 212);
        }
        
        // === ESPACIADO MODERNO (8px base) ===
        public static class Spacing
        {
            public const int XSmall = 4;
            public const int Small = 8;
            public const int Medium = 16;
            public const int Large = 24;
            public const int XLarge = 32;
            public const int XXLarge = 48;
            public const int Huge = 64;
        }
        
        // === TIPOGRAFÍA MODERNA ===
        public static class Typography
        {
            public static readonly Font Heading1 = new Font("Segoe UI", 24, FontStyle.Bold);
            public static readonly Font Heading2 = new Font("Segoe UI", 20, FontStyle.Bold);
            public static readonly Font Heading3 = new Font("Segoe UI", 16, FontStyle.Bold);
            public static readonly Font Heading4 = new Font("Segoe UI", 14, FontStyle.Bold);
            
            public static readonly Font BodyLarge = new Font("Segoe UI", 12, FontStyle.Regular);
            public static readonly Font Body = new Font("Segoe UI", 10, FontStyle.Regular);
            public static readonly Font BodySmall = new Font("Segoe UI", 9, FontStyle.Regular);
            
            public static readonly Font ButtonLarge = new Font("Segoe UI", 12, FontStyle.Bold);
            public static readonly Font Button = new Font("Segoe UI", 10, FontStyle.Bold);
            public static readonly Font ButtonSmall = new Font("Segoe UI", 9, FontStyle.Bold);
            
            public static readonly Font Code = new Font("Consolas", 10, FontStyle.Regular);
        }
        
        // === TAMAÑOS DE CONTROLES ===
        public static class ControlSizes
        {
            // Botones
            public static readonly Size ButtonSmall = new Size(100, 32);
            public static readonly Size ButtonMedium = new Size(140, 40);
            public static readonly Size ButtonLarge = new Size(180, 48);
            
            // Inputs
            public static readonly Size InputSmall = new Size(120, 28);
            public static readonly Size InputMedium = new Size(200, 32);
            public static readonly Size InputLarge = new Size(300, 36);
            
            // Mínimos para responsive
            public const int MinButtonHeight = 36;
            public const int MinInputHeight = 32;
            public const int MinLabelHeight = 24;
        }
        
        // === BORDES Y RADIOS ===
        public static class BorderRadius
        {
            public const int Small = 4;
            public const int Medium = 8;
            public const int Large = 12;
            public const int XLarge = 16;
            public const int Round = 999;
        }
        
        // === HELPERS DE LAYOUT ===
        
        /// <summary>
        /// Crea un panel con padding moderno
        /// </summary>
        public static Panel CreateCard(int padding = Spacing.Large)
        {
            return new Panel
            {
                BackColor = Colors.BackgroundCard,
                Padding = new Padding(padding),
                Dock = DockStyle.Fill
            };
        }
        
        /// <summary>
        /// Crea un FlowLayoutPanel con configuración moderna
        /// </summary>
        public static FlowLayoutPanel CreateFlowPanel(
            FlowDirection direction = FlowDirection.LeftToRight,
            bool autoSize = true,
            int padding = Spacing.Medium)
        {
            return new FlowLayoutPanel
            {
                FlowDirection = direction,
                AutoSize = autoSize,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                BackColor = Color.Transparent,
                Padding = new Padding(padding)
            };
        }
        
        /// <summary>
        /// Crea un TableLayoutPanel responsive
        /// </summary>
        public static TableLayoutPanel CreateResponsiveTable(int columns, int rows)
        {
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = columns,
                RowCount = rows,
                BackColor = Color.Transparent,
                Padding = new Padding(Spacing.Large)
            };
            
            return table;
        }
        
        /// <summary>
        /// Crea un label con estilo moderno
        /// </summary>
        public static Label CreateLabel(
            string text,
            Font font = null,
            Color? color = null,
            ContentAlignment alignment = ContentAlignment.MiddleLeft)
        {
            return new Label
            {
                Text = text,
                Font = font ?? Typography.Body,
                ForeColor = color ?? Colors.TextPrimary,
                TextAlign = alignment,
                AutoSize = true,
                BackColor = Color.Transparent
            };
        }
        
        /// <summary>
        /// Crea un heading (título) con estilo moderno
        /// </summary>
        public static Label CreateHeading(
            string text,
            int level = 2,
            Color? color = null)
        {
            Font font = level switch
            {
                1 => Typography.Heading1,
                2 => Typography.Heading2,
                3 => Typography.Heading3,
                4 => Typography.Heading4,
                _ => Typography.Heading2
            };
            
            return new Label
            {
                Text = text,
                Font = font,
                ForeColor = color ?? Colors.Primary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, Spacing.Medium)
            };
        }
        
        /// <summary>
        /// Crea un separador visual
        /// </summary>
        public static Panel CreateSeparator(int height = 1, int margin = Spacing.Medium)
        {
            return new Panel
            {
                Height = height,
                Dock = DockStyle.Top,
                BackColor = Colors.BackgroundHover,
                Margin = new Padding(0, margin, 0, margin)
            };
        }
        
        /// <summary>
        /// Aplica estilo moderno a un botón existente
        /// </summary>
        public static void StyleButton(
            Button button,
            Color? backgroundColor = null,
            Color? foreColor = null,
            Font font = null,
            Size? size = null)
        {
            button.BackColor = backgroundColor ?? Colors.Primary;
            button.ForeColor = foreColor ?? Colors.TextPrimary;
            button.Font = font ?? Typography.Button;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Cursor = Cursors.Hand;
            
            if (size.HasValue)
                button.Size = size.Value;
            else
                button.MinimumSize = new Size(ControlSizes.ButtonMedium.Width, ControlSizes.MinButtonHeight);
            
            // Efecto hover
            button.MouseEnter += (s, e) => button.BackColor = Colors.PrimaryHover;
            button.MouseLeave += (s, e) => button.BackColor = backgroundColor ?? Colors.Primary;
        }
        
        /// <summary>
        /// Aplica estilo moderno a un TextBox
        /// </summary>
        public static void StyleTextBox(
            TextBox textBox,
            Font font = null,
            int padding = Spacing.Small)
        {
            textBox.BackColor = Colors.BackgroundCard;
            textBox.ForeColor = Colors.TextPrimary;
            textBox.Font = font ?? Typography.Body;
            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.MinimumSize = new Size(ControlSizes.InputMedium.Width, ControlSizes.MinInputHeight);
        }
        
        /// <summary>
        /// Aplica estilo moderno a un NumericUpDown
        /// </summary>
        public static void StyleNumeric(
            NumericUpDown numeric,
            Font font = null)
        {
            numeric.BackColor = Colors.BackgroundCard;
            numeric.ForeColor = Colors.TextPrimary;
            numeric.Font = font ?? Typography.Body;
            numeric.BorderStyle = BorderStyle.FixedSingle;
            numeric.MinimumSize = new Size(ControlSizes.InputSmall.Width, ControlSizes.MinInputHeight);
        }
        
        /// <summary>
        /// Aplica estilo moderno a un ComboBox
        /// </summary>
        public static void StyleComboBox(
            ComboBox comboBox,
            Font font = null)
        {
            comboBox.BackColor = Colors.BackgroundCard;
            comboBox.ForeColor = Colors.TextPrimary;
            comboBox.Font = font ?? Typography.Body;
            comboBox.FlatStyle = FlatStyle.Flat;
            comboBox.MinimumSize = new Size(ControlSizes.InputMedium.Width, ControlSizes.MinInputHeight);
        }
        
        /// <summary>
        /// Aplica estilo moderno a un CheckBox
        /// </summary>
        public static void StyleCheckBox(
            CheckBox checkBox,
            Font font = null)
        {
            checkBox.ForeColor = Colors.TextPrimary;
            checkBox.Font = font ?? Typography.Body;
            checkBox.BackColor = Color.Transparent;
            checkBox.AutoSize = true;
        }
        
        /// <summary>
        /// Aplica estilo moderno a un ListView
        /// </summary>
        public static void StyleListView(
            ListView listView,
            Font font = null)
        {
            listView.BackColor = Colors.BackgroundCard;
            listView.ForeColor = Colors.TextPrimary;
            listView.Font = font ?? Typography.Body;
            listView.BorderStyle = BorderStyle.None;
            listView.FullRowSelect = true;
            listView.GridLines = true;
            listView.View = View.Details;
            
            // Habilitar double buffering para mejor rendimiento
            typeof(Control).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(listView, true, null);
        }
        
        /// <summary>
        /// Calcula tamaño responsive basado en porcentaje del contenedor
        /// </summary>
        public static int GetResponsiveWidth(Control container, float percentage)
        {
            return (int)(container.Width * percentage);
        }
        
        /// <summary>
        /// Calcula tamaño responsive basado en porcentaje del contenedor
        /// </summary>
        public static int GetResponsiveHeight(Control container, float percentage)
        {
            return (int)(container.Height * percentage);
        }
        
        /// <summary>
        /// Configura un control para que sea responsive al resize del contenedor
        /// </summary>
        public static void MakeResponsive(Control control, Control container, float widthPercent = 1.0f, float heightPercent = 1.0f)
        {
            void UpdateSize(object sender, EventArgs e)
            {
                if (widthPercent > 0)
                    control.Width = GetResponsiveWidth(container, widthPercent);
                if (heightPercent > 0)
                    control.Height = GetResponsiveHeight(container, heightPercent);
            }
            
            container.Resize += UpdateSize;
            UpdateSize(container, EventArgs.Empty); // Aplicar inmediatamente
        }
    }
}
