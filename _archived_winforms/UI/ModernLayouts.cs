using System;
using System.Drawing;
using System.Windows.Forms;
using FontAwesome.Sharp;

namespace SlskDown.UI
{
    public static class ModernLayouts
    {
        // Colores del tema
        public static readonly Color DarkBackground = Color.FromArgb(18, 18, 18);
        public static readonly Color CardBackground = Color.FromArgb(30, 30, 30);
        public static readonly Color AccentBlue = Color.FromArgb(0, 120, 215);
        public static readonly Color AccentGreen = Color.FromArgb(0, 200, 100);
        public static readonly Color TextPrimary = Color.White;
        public static readonly Color TextSecondary = Color.FromArgb(180, 180, 180);
        
        public static Panel CreateModernSearchLayout()
        {
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DarkBackground,
                Padding = new Padding(20)
            };
            
            // Card superior con controles de búsqueda
            var searchCard = new ModernCard
            {
                Location = new Point(20, 20),
                Size = new Size(mainPanel.Width - 40, 120),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            
            // Título de la sección
            var lblTitle = new Label
            {
                Text = "Búsqueda de Archivos",
                Location = new Point(15, 15),
                Size = new Size(300, 30),
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = TextPrimary
            };
            searchCard.Controls.Add(lblTitle);
            
            // Campo de búsqueda moderno
            var txtSearch = new ModernTextBox
            {
                Location = new Point(15, 55),
                Size = new Size(searchCard.Width - 200, 35),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            searchCard.Controls.Add(txtSearch);
            
            // Botón de búsqueda con icono
            var btnSearch = new ModernButton
            {
                Text = "Buscar",
                Location = new Point(searchCard.Width - 170, 55),
                Size = new Size(150, 35),
                IconChar = IconChar.Search,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            searchCard.Controls.Add(btnSearch);
            
            mainPanel.Controls.Add(searchCard);
            
            // Card de resultados
            var resultsCard = new ModernCard
            {
                Location = new Point(20, 160),
                Size = new Size(mainPanel.Width - 40, mainPanel.Height - 200),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            
            var lblResults = new Label
            {
                Text = "Resultados",
                Location = new Point(15, 15),
                Size = new Size(200, 25),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = TextPrimary
            };
            resultsCard.Controls.Add(lblResults);
            
            var lvResults = new ModernListView
            {
                Location = new Point(15, 50),
                Size = new Size(resultsCard.Width - 30, resultsCard.Height - 65),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            lvResults.Columns.Add("Archivo", 300);
            lvResults.Columns.Add("Tamaño", 100);
            lvResults.Columns.Add("Usuario", 150);
            lvResults.Columns.Add("Velocidad", 100);
            lvResults.Columns.Add("Calidad", 80);
            resultsCard.Controls.Add(lvResults);
            
            mainPanel.Controls.Add(resultsCard);
            
            return mainPanel;
        }
        
        public static Panel CreateModernDownloadsLayout()
        {
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DarkBackground,
                Padding = new Padding(20)
            };
            
            // Panel superior con estadísticas
            var statsPanel = new FlowLayoutPanel
            {
                Location = new Point(20, 20),
                Size = new Size(mainPanel.Width - 40, 100),
                FlowDirection = FlowDirection.LeftToRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            
            // Cards de estadísticas
            var activeCard = CreateStatCard("Activas", "0", IconChar.Download, AccentBlue);
            var completedCard = CreateStatCard("Completadas", "0", IconChar.CheckCircle, AccentGreen);
            var speedCard = CreateStatCard("Velocidad", "0 MB/s", IconChar.Gauge, Color.FromArgb(255, 165, 0));
            
            statsPanel.Controls.Add(activeCard);
            statsPanel.Controls.Add(completedCard);
            statsPanel.Controls.Add(speedCard);
            
            mainPanel.Controls.Add(statsPanel);
            
            // Card de descargas
            var downloadsCard = new ModernCard
            {
                Location = new Point(20, 140),
                Size = new Size(mainPanel.Width - 40, mainPanel.Height - 180),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            
            var lblDownloads = new Label
            {
                Text = "Cola de Descargas",
                Location = new Point(15, 15),
                Size = new Size(200, 25),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = TextPrimary
            };
            downloadsCard.Controls.Add(lblDownloads);
            
            // Botones de control
            var btnPauseAll = new ModernButton
            {
                Text = "Pausar Todo",
                Location = new Point(downloadsCard.Width - 380, 12),
                Size = new Size(120, 30),
                IconChar = IconChar.Pause,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            downloadsCard.Controls.Add(btnPauseAll);
            
            var btnResumeAll = new ModernButton
            {
                Text = "Reanudar",
                Location = new Point(downloadsCard.Width - 250, 12),
                Size = new Size(110, 30),
                IconChar = IconChar.Play,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            downloadsCard.Controls.Add(btnResumeAll);
            
            var btnClearCompleted = new ModernButton
            {
                Text = "Limpiar",
                Location = new Point(downloadsCard.Width - 130, 12),
                Size = new Size(100, 30),
                IconChar = IconChar.Broom,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            downloadsCard.Controls.Add(btnClearCompleted);
            
            var lvDownloads = new ModernListView
            {
                Location = new Point(15, 55),
                Size = new Size(downloadsCard.Width - 30, downloadsCard.Height - 70),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            lvDownloads.Columns.Add("Archivo", 300);
            lvDownloads.Columns.Add("Progreso", 150);
            lvDownloads.Columns.Add("Velocidad", 100);
            lvDownloads.Columns.Add("Tamaño", 100);
            lvDownloads.Columns.Add("Estado", 100);
            lvDownloads.Columns.Add("Usuario", 120);
            downloadsCard.Controls.Add(lvDownloads);
            
            mainPanel.Controls.Add(downloadsCard);
            
            return mainPanel;
        }
        
        public static Panel CreateModernConfigLayout()
        {
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DarkBackground,
                Padding = new Padding(20),
                AutoScroll = true
            };
            
            int yPos = 20;
            
            // Sección de Conexión
            var connectionCard = CreateConfigSection("Conexión", yPos, mainPanel.Width - 40);
            AddConfigField(connectionCard, "Usuario:", 60, new ModernTextBox { Width = 250 });
            AddConfigField(connectionCard, "Contraseña:", 110, new ModernTextBox { Width = 250, UseSystemPasswordChar = true });
            
            var chkAutoConnect = new CheckBox
            {
                Text = "Conectar automáticamente al iniciar",
                Location = new Point(150, 160),
                Size = new Size(300, 25),
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 9)
            };
            connectionCard.Controls.Add(chkAutoConnect);
            
            mainPanel.Controls.Add(connectionCard);
            yPos += connectionCard.Height + 20;
            
            // Sección de Descargas
            var downloadsCard = CreateConfigSection("Descargas", yPos, mainPanel.Width - 40);
            AddConfigField(downloadsCard, "Carpeta de destino:", 60, new ModernTextBox { Width = 350 });
            
            var btnBrowse = new ModernButton
            {
                Text = "Examinar",
                Location = new Point(downloadsCard.Width - 130, 55),
                Size = new Size(100, 30),
                IconChar = IconChar.FolderOpen
            };
            downloadsCard.Controls.Add(btnBrowse);
            
            AddConfigField(downloadsCard, "Descargas simultáneas:", 110, new NumericUpDown 
            { 
                Width = 100, 
                Minimum = 1, 
                Maximum = 20, 
                Value = 5,
                BackColor = CardBackground,
                ForeColor = TextPrimary
            });
            
            mainPanel.Controls.Add(downloadsCard);
            yPos += downloadsCard.Height + 20;
            
            // Sección de Filtros
            var filtersCard = CreateConfigSection("Filtros", yPos, mainPanel.Width - 40);
            
            var chkSpanish = new CheckBox
            {
                Text = "Solo archivos en español",
                Location = new Point(150, 60),
                Size = new Size(250, 25),
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 9)
            };
            filtersCard.Controls.Add(chkSpanish);
            
            var chkHighQuality = new CheckBox
            {
                Text = "Solo alta calidad (>128kbps)",
                Location = new Point(150, 95),
                Size = new Size(250, 25),
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 9)
            };
            filtersCard.Controls.Add(chkHighQuality);
            
            AddConfigField(filtersCard, "Tamaño mínimo (MB):", 130, new NumericUpDown 
            { 
                Width = 100,
                BackColor = CardBackground,
                ForeColor = TextPrimary
            });
            
            mainPanel.Controls.Add(filtersCard);
            
            // Botón de guardar
            var btnSave = new ModernButton
            {
                Text = "Guardar Configuración",
                Location = new Point(mainPanel.Width - 220, yPos + 220),
                Size = new Size(180, 40),
                IconChar = IconChar.Save,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            mainPanel.Controls.Add(btnSave);
            
            return mainPanel;
        }
        
        private static ModernCard CreateStatCard(string title, string value, IconChar icon, Color accentColor)
        {
            var card = new ModernCard
            {
                Size = new Size(200, 90),
                Margin = new Padding(0, 0, 15, 0)
            };
            
            var iconLabel = new IconButton
            {
                IconChar = icon,
                IconColor = accentColor,
                IconSize = 32,
                Location = new Point(15, 20),
                Size = new Size(40, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent
            };
            iconLabel.FlatAppearance.BorderSize = 0;
            card.Controls.Add(iconLabel);
            
            var lblValue = new Label
            {
                Text = value,
                Location = new Point(65, 15),
                Size = new Size(120, 30),
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = TextPrimary
            };
            card.Controls.Add(lblValue);
            
            var lblTitle = new Label
            {
                Text = title,
                Location = new Point(65, 45),
                Size = new Size(120, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = TextSecondary
            };
            card.Controls.Add(lblTitle);
            
            return card;
        }
        
        private static ModernCard CreateConfigSection(string title, int yPos, int width)
        {
            var card = new ModernCard
            {
                Location = new Point(20, yPos),
                Size = new Size(width, 200),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            
            var lblTitle = new Label
            {
                Text = title,
                Location = new Point(15, 15),
                Size = new Size(300, 30),
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = TextPrimary
            };
            card.Controls.Add(lblTitle);
            
            return card;
        }
        
        private static void AddConfigField(ModernCard card, string label, int yPos, Control control)
        {
            var lbl = new Label
            {
                Text = label,
                Location = new Point(15, yPos),
                Size = new Size(130, 25),
                Font = new Font("Segoe UI", 9),
                ForeColor = TextSecondary,
                TextAlign = ContentAlignment.MiddleRight
            };
            card.Controls.Add(lbl);
            
            control.Location = new Point(150, yPos - 5);
            card.Controls.Add(control);
        }
    }
}
