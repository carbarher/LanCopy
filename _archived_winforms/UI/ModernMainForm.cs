using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown.UI
{
    /// <summary>
    /// Nueva GUI moderna para SlskDown con diseño limpio y profesional
    /// </summary>
    public partial class ModernMainForm : Form
    {
        // Colores del tema
        private static readonly Color DarkBackground = Color.FromArgb(18, 18, 18);
        private static readonly Color CardBackground = Color.FromArgb(30, 30, 30);
        private static readonly Color AccentBlue = Color.FromArgb(0, 120, 215);
        private static readonly Color AccentGreen = Color.FromArgb(0, 200, 100);
        private static readonly Color TextPrimary = Color.White;
        private static readonly Color TextSecondary = Color.FromArgb(180, 180, 180);
        
        // Controles principales
        private Panel sidebarPanel;
        private Panel contentPanel;
        private Panel topBarPanel;
        private Label lblTitle;
        private Label lblStatus;
        private Button btnConnect;
        
        // Paneles de contenido para cada sección
        private Panel searchPanel;
        private Panel downloadsPanel;
        private Panel configPanel;
        
        public ModernMainForm()
        {
            InitializeComponent();
            SetupModernUI();
        }
        
        private void InitializeComponent()
        {
            this.Text = "SlskDown - Soulseek Downloader";
            this.Size = new Size(1200, 800);
            this.MinimumSize = new Size(1000, 600);
            this.BackColor = DarkBackground;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9F);
        }
        
        private void SetupModernUI()
        {
            // Layout principal usando TableLayoutPanel
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = DarkBackground,
                Padding = new Padding(0)
            };
            
            // Columna 1: Sidebar (200px fijo)
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            // Columna 2: Contenido (resto del espacio)
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            
            // Crear sidebar
            CreateSidebar();
            mainLayout.Controls.Add(sidebarPanel, 0, 0);
            
            // Crear panel de contenido principal
            CreateContentArea();
            mainLayout.Controls.Add(contentPanel, 1, 0);
            
            this.Controls.Add(mainLayout);
        }
        
        private void CreateSidebar()
        {
            sidebarPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(25, 25, 25),
                Padding = new Padding(0)
            };
            
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 10,
                ColumnCount = 1,
                BackColor = Color.Transparent,
                Padding = new Padding(10)
            };
            
            // Logo/Título
            var lblLogo = new Label
            {
                Text = "🎵 SlskDown",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = AccentBlue,
                TextAlign = ContentAlignment.MiddleLeft,
                Height = 50,
                Dock = DockStyle.Top
            };
            layout.Controls.Add(lblLogo);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            
            // Botones de navegación
            var btnSearch = CreateNavButton("Búsqueda", true);
            btnSearch.Click += (s, e) => ShowPanel(searchPanel);
            layout.Controls.Add(btnSearch);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            
            var btnDownloads = CreateNavButton("Descargas", false);
            btnDownloads.Click += (s, e) => ShowPanel(downloadsPanel);
            layout.Controls.Add(btnDownloads);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            
            var btnConfig = CreateNavButton("Configuración", false);
            btnConfig.Click += (s, e) => ShowPanel(configPanel);
            layout.Controls.Add(btnConfig);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            
            var btnAuthors = CreateNavButton("Autores", false);
            layout.Controls.Add(btnAuthors);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            
            var btnFiles = CreateNavButton("Archivos", false);
            layout.Controls.Add(btnFiles);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            
            var btnWishlist = CreateNavButton("Wishlist", false);
            layout.Controls.Add(btnWishlist);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            
            var btnHistory = CreateNavButton("Historial", false);
            layout.Controls.Add(btnHistory);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            
            var btnAuto = CreateNavButton("Automático", false);
            layout.Controls.Add(btnAuto);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            
            // Espacio flexible
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            
            // Estado de conexión en la parte inferior
            lblStatus = new Label
            {
                Text = "● Desconectado",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.Gray,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 30,
                Dock = DockStyle.Bottom
            };
            layout.Controls.Add(lblStatus);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            
            sidebarPanel.Controls.Add(layout);
        }
        
        private Button CreateNavButton(string text, bool isActive)
        {
            var btn = new Button
            {
                Text = text,
                Height = 40,
                Dock = DockStyle.Top,
                FlatStyle = FlatStyle.Flat,
                BackColor = isActive ? AccentBlue : Color.Transparent,
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 10),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(15, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 40, 40);
            
            return btn;
        }
        
        private void CreateContentArea()
        {
            contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DarkBackground,
                Padding = new Padding(0)
            };
            
            // Barra superior
            topBarPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = CardBackground,
                Padding = new Padding(20, 15, 20, 15)
            };
            
            lblTitle = new Label
            {
                Text = "Búsqueda",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = TextPrimary,
                AutoSize = true,
                Location = new Point(20, 15)
            };
            topBarPanel.Controls.Add(lblTitle);
            
            btnConnect = new ModernButton
            {
                Text = "Conectar",
                Size = new Size(120, 35),
                Location = new Point(topBarPanel.Width - 140, 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            topBarPanel.Controls.Add(btnConnect);
            
            contentPanel.Controls.Add(topBarPanel);
            
            // Crear paneles de contenido
            CreateSearchPanel();
            CreateDownloadsPanel();
            CreateConfigPanel();
            
            // Mostrar panel de búsqueda por defecto
            ShowPanel(searchPanel);
        }
        
        private void CreateSearchPanel()
        {
            searchPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DarkBackground,
                Padding = new Padding(20),
                Visible = false
            };
            
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                BackColor = Color.Transparent
            };
            
            // Fila 1: Barra de búsqueda (60px)
            var searchBar = CreateSearchBar();
            layout.Controls.Add(searchBar, 0, 0);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            
            // Fila 2: Filtros (45px)
            var filterBar = CreateFilterBar();
            layout.Controls.Add(filterBar, 0, 1);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            
            // Fila 3: Resultados (resto del espacio)
            var resultsPanel = CreateResultsPanel();
            layout.Controls.Add(resultsPanel, 0, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            
            searchPanel.Controls.Add(layout);
            contentPanel.Controls.Add(searchPanel);
        }
        
        private Panel CreateSearchBar()
        {
            var panel = new ModernCard
            {
                Dock = DockStyle.Fill,
                BorderRadius = 8,
                Padding = new Padding(15, 10, 15, 10)
            };
            
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent,
                WrapContents = false
            };
            
            var txtSearch = new TextBox
            {
                Width = 400,
                Height = 35,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11),
                Margin = new Padding(0, 0, 10, 0)
            };
            flow.Controls.Add(txtSearch);
            
            var btnSearch = new ModernButton
            {
                Text = "Buscar",
                Size = new Size(100, 35),
                Margin = new Padding(0, 0, 10, 0)
            };
            flow.Controls.Add(btnSearch);
            
            var btnStop = new ModernButton
            {
                Text = "Detener",
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(180, 0, 0)
            };
            flow.Controls.Add(btnStop);
            
            panel.Controls.Add(flow);
            return panel;
        }
        
        private Panel CreateFilterBar()
        {
            var panel = new ModernCard
            {
                Dock = DockStyle.Fill,
                BorderRadius = 8,
                Padding = new Padding(15, 8, 15, 8)
            };
            
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent,
                WrapContents = false
            };
            
            var lblSize = new Label
            {
                Text = "Tamaño:",
                ForeColor = TextSecondary,
                AutoSize = true,
                Margin = new Padding(0, 5, 5, 0)
            };
            flow.Controls.Add(lblSize);
            
            var numMin = new NumericUpDown
            {
                Width = 80,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = TextPrimary,
                Margin = new Padding(0, 0, 5, 0)
            };
            flow.Controls.Add(numMin);
            
            var lblTo = new Label
            {
                Text = "-",
                ForeColor = TextSecondary,
                AutoSize = true,
                Margin = new Padding(0, 5, 5, 0)
            };
            flow.Controls.Add(lblTo);
            
            var numMax = new NumericUpDown
            {
                Width = 80,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = TextPrimary,
                Margin = new Padding(0, 0, 10, 0)
            };
            flow.Controls.Add(numMax);
            
            var cmbType = new ComboBox
            {
                Width = 120,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = TextPrimary,
                FlatStyle = FlatStyle.Flat,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbType.Items.AddRange(new object[] { "Todos", "Documentos", "Audio", "Video" });
            cmbType.SelectedIndex = 0;
            flow.Controls.Add(cmbType);
            
            panel.Controls.Add(flow);
            return panel;
        }
        
        private Panel CreateResultsPanel()
        {
            var panel = new ModernCard
            {
                Dock = DockStyle.Fill,
                BorderRadius = 8,
                Padding = new Padding(0)
            };
            
            var lv = new ModernListView
            {
                Dock = DockStyle.Fill,
                VirtualMode = false
            };
            
            lv.Columns.Add("Usuario", 150);
            lv.Columns.Add("Archivo", 400);
            lv.Columns.Add("Tamaño", 100);
            lv.Columns.Add("Tipo", 80);
            lv.Columns.Add("Carpeta", 200);
            
            panel.Controls.Add(lv);
            return panel;
        }
        
        private void CreateDownloadsPanel()
        {
            downloadsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DarkBackground,
                Padding = new Padding(20),
                Visible = false
            };
            
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Color.Transparent
            };
            
            // Botones de control
            var buttonBar = new ModernCard
            {
                Dock = DockStyle.Fill,
                Height = 60,
                BorderRadius = 8,
                Padding = new Padding(15, 10, 15, 10)
            };
            
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent
            };
            
            flow.Controls.Add(new ModernButton { Text = "Limpiar Todo", Size = new Size(120, 35), BackColor = Color.FromArgb(180, 50, 50), Margin = new Padding(0, 0, 5, 0) });
            flow.Controls.Add(new ModernButton { Text = "Reintentar", Size = new Size(120, 35), BackColor = Color.FromArgb(200, 100, 0), Margin = new Padding(0, 0, 5, 0) });
            flow.Controls.Add(new ModernButton { Text = "Pausar", Size = new Size(100, 35), Margin = new Padding(0, 0, 5, 0) });
            flow.Controls.Add(new ModernButton { Text = "Reanudar", Size = new Size(100, 35), BackColor = AccentGreen });
            
            buttonBar.Controls.Add(flow);
            layout.Controls.Add(buttonBar, 0, 0);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            
            // Lista de descargas
            var downloadsCard = new ModernCard
            {
                Dock = DockStyle.Fill,
                BorderRadius = 8,
                Padding = new Padding(0)
            };
            
            var lvDownloads = new ModernListView
            {
                Dock = DockStyle.Fill,
                VirtualMode = false
            };
            
            lvDownloads.Columns.Add("Archivo", 300);
            lvDownloads.Columns.Add("Usuario", 120);
            lvDownloads.Columns.Add("Progreso", 100);
            lvDownloads.Columns.Add("Estado", 100);
            lvDownloads.Columns.Add("Tamaño", 80);
            
            downloadsCard.Controls.Add(lvDownloads);
            layout.Controls.Add(downloadsCard, 0, 1);
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            
            downloadsPanel.Controls.Add(layout);
            contentPanel.Controls.Add(downloadsPanel);
        }
        
        private void CreateConfigPanel()
        {
            configPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DarkBackground,
                Padding = new Padding(20),
                Visible = false,
                AutoScroll = true
            };
            
            var card = new ModernCard
            {
                Dock = DockStyle.Top,
                Height = 400,
                BorderRadius = 8,
                Padding = new Padding(20)
            };
            
            var lblSection = new Label
            {
                Text = "Configuración de Cuenta",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = AccentBlue,
                Location = new Point(20, 20),
                AutoSize = true
            };
            card.Controls.Add(lblSection);
            
            var lblUser = new Label
            {
                Text = "Usuario:",
                ForeColor = TextSecondary,
                Location = new Point(20, 60),
                AutoSize = true
            };
            card.Controls.Add(lblUser);
            
            var txtUser = new TextBox
            {
                Location = new Point(150, 57),
                Width = 300,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = TextPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };
            card.Controls.Add(txtUser);
            
            var lblPass = new Label
            {
                Text = "Contraseña:",
                ForeColor = TextSecondary,
                Location = new Point(20, 100),
                AutoSize = true
            };
            card.Controls.Add(lblPass);
            
            var txtPass = new TextBox
            {
                Location = new Point(150, 97),
                Width = 300,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                UseSystemPasswordChar = true
            };
            card.Controls.Add(txtPass);
            
            var lblDir = new Label
            {
                Text = "Carpeta descargas:",
                ForeColor = TextSecondary,
                Location = new Point(20, 140),
                AutoSize = true
            };
            card.Controls.Add(lblDir);
            
            var txtDir = new TextBox
            {
                Location = new Point(150, 137),
                Width = 250,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = TextPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };
            card.Controls.Add(txtDir);
            
            var btnBrowse = new ModernButton
            {
                Text = "...",
                Location = new Point(410, 135),
                Size = new Size(40, 25)
            };
            card.Controls.Add(btnBrowse);
            
            configPanel.Controls.Add(card);
            contentPanel.Controls.Add(configPanel);
        }
        
        private void ShowPanel(Panel panel)
        {
            searchPanel.Visible = false;
            downloadsPanel.Visible = false;
            configPanel.Visible = false;
            
            panel.Visible = true;
            panel.BringToFront();
            
            // Actualizar título
            if (panel == searchPanel) lblTitle.Text = "Búsqueda";
            else if (panel == downloadsPanel) lblTitle.Text = "Descargas";
            else if (panel == configPanel) lblTitle.Text = "Configuración";
        }
    }
}
