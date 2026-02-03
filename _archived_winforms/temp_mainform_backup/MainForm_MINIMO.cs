using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Soulseek;

namespace SlskDown
{
    public partial class MainForm : Form
    {
        // Variables bÃ¡sicas
        private SoulseekClient? client;
        private Button connectButton = null!;
        private Button searchButton = null!;
        private TextBox searchBox = null!;
        private ListView resultsListView = null!;
        private ListView downloadsListView = null!;
        private Label connectionStatus = null!;
        private Label statusLabel = null!;
        private TabControl tabControl = null!;
        
        // ConfiguraciÃ³n
        private string username = "carbar";
        private string password = "Carlos66*";
        private string downloadDir = @"c:\p2p\downloads";
        private bool isLoadingConfig = false;
        
        // Variables para configuraciÃ³n
        private TextBox usernameTextBox = null!;
        private TextBox passwordTextBox = null!;
        private TextBox downloadDirTextBox = null!;
        private NumericUpDown timeoutBox = null!;
        private NumericUpDown maxResultsBox = null!;
        private NumericUpDown maxDownloadsBox = null!;
        private NumericUpDown maxFailedAttemptsBox = null!;
        private CheckBox autoConnectCheckBox = null!;
        private Button testConnectionButton = null!;
        private Label configStatusLabel = null!;
        
        public MainForm()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.Size = new Size(1100, 700);
            this.BackColor = Color.FromArgb(18, 18, 18);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "SlskDown - Cliente Soulseek";
            
            // TabControl principal
            tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;
            tabControl.BackColor = Color.FromArgb(30, 30, 30);
            
            // PestaÃ±a de bÃºsqueda
            var searchTab = new TabPage("ðŸ” BÃºsqueda");
            searchTab.BackColor = Color.FromArgb(30, 30, 30);
            CreateSearchTab(searchTab);
            
            // PestaÃ±a de descargas
            var downloadsTab = new TabPage("â¬‡ï¸ Descargas");
            downloadsTab.BackColor = Color.FromArgb(30, 30, 30);
            CreateDownloadsTab(downloadsTab);
            
            tabControl.TabPages.Add(searchTab);
            tabControl.TabPages.Add(downloadsTab);
            
            this.Controls.Add(tabControl);
            
            // Agregar pestaÃ±a de configuraciÃ³n
            AddConfigTab();
        }
        
        private void CreateSearchTab(TabPage parent)
        {
            var panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(20);
            
            // ConexiÃ³n
            connectionStatus = new Label();
            connectionStatus.Text = "â— Desconectado";
            connectionStatus.ForeColor = Color.Red;
            connectionStatus.Location = new Point(20, 20);
            connectionStatus.Size = new Size(200, 20);
            panel.Controls.Add(connectionStatus);
            
            connectButton = new Button();
            connectButton.Text = "ðŸ”Œ Conectar";
            connectButton.Location = new Point(240, 15);
            connectButton.Size = new Size(120, 30);
            connectButton.BackColor = Color.FromArgb(0, 150, 0);
            connectButton.ForeColor = Color.White;
            connectButton.FlatStyle = FlatStyle.Flat;
            connectButton.Click += async (s, e) => await ToggleConnection();
            panel.Controls.Add(connectButton);
            
            // BÃºsqueda
            searchBox = new TextBox();
            searchBox.Location = new Point(20, 60);
            searchBox.Size = new Size(400, 30);
            searchBox.BackColor = Color.FromArgb(60, 60, 60);
            searchBox.ForeColor = Color.White;
            panel.Controls.Add(searchBox);
            
            searchButton = new Button();
            searchButton.Text = "ðŸ” Buscar";
            searchButton.Location = new Point(430, 60);
            searchButton.Size = new Size(100, 30);
            searchButton.BackColor = Color.FromArgb(0, 120, 215);
            searchButton.ForeColor = Color.White;
            searchButton.FlatStyle = FlatStyle.Flat;
            searchButton.Enabled = false;
            searchButton.Click += async (s, e) => await PerformSearch();
            panel.Controls.Add(searchButton);
            
            // Resultados
            resultsListView = new ListView();
            resultsListView.Location = new Point(20, 110);
            resultsListView.Size = new Size(900, 400);
            resultsListView.BackColor = Color.FromArgb(40, 40, 40);
            resultsListView.ForeColor = Color.White;
            resultsListView.View = View.Details;
            resultsListView.FullRowSelect = true;
            resultsListView.GridLines = true;
            
            // Columnas
            resultsListView.Columns.Add("Usuario", 150);
            resultsListView.Columns.Add("Archivo", 300);
            resultsListView.Columns.Add("TamaÃ±o", 100);
            resultsListView.Columns.Add("Velocidad", 100);
            resultsListView.Columns.Add("Carpeta", 200);
            
            panel.Controls.Add(resultsListView);
            
            // Status
            statusLabel = new Label();
            statusLabel.Text = "Listo";
            statusLabel.Location = new Point(20, 520);
            statusLabel.Size = new Size(500, 20);
            statusLabel.ForeColor = Color.LightGray;
            panel.Controls.Add(statusLabel);
            
            parent.Controls.Add(panel);
        }
        
        private void CreateDownloadsTab(TabPage parent)
        {
            var panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(20);
            
            downloadsListView = new ListView();
            downloadsListView.Location = new Point(20, 20);
            downloadsListView.Size = new Size(900, 500);
            downloadsListView.BackColor = Color.FromArgb(40, 40, 40);
            downloadsListView.ForeColor = Color.White;
            downloadsListView.View = View.Details;
            downloadsListView.FullRowSelect = true;
            downloadsListView.GridLines = true;
            
            downloadsListView.Columns.Add("Archivo", 300);
            downloadsListView.Columns.Add("Usuario", 150);
            downloadsListView.Columns.Add("Progreso", 100);
            downloadsListView.Columns.Add("Velocidad", 100);
            downloadsListView.Columns.Add("Estado", 150);
            
            panel.Controls.Add(downloadsListView);
            parent.Controls.Add(panel);
        }
        
        private async Task ToggleConnection()
        {
            if (client != null)
            {
                // Desconectar
                client.Dispose();
                client = null;
                connectButton.Text = "ðŸ”Œ Conectar";
                connectButton.BackColor = Color.FromArgb(0, 150, 0);
                connectionStatus.Text = "â— Desconectado";
                connectionStatus.ForeColor = Color.Red;
                searchButton.Enabled = false;
            }
            else
            {
                // Conectar
                try
                {
                    connectButton.Enabled = false;
                    connectButton.Text = "â³ Conectando...";
                    
                    var options = new SoulseekClientOptions(
                        listenPort: new Random().Next(50000, 60000),
                        enableDistributedNetwork: false
                    );
                    client = new SoulseekClient(options);
                    
                    await client.ConnectAsync(username, password);
                    
                    connectButton.Text = "ðŸ”Œ Desconectar";
                    connectButton.BackColor = Color.FromArgb(200, 50, 50);
                    connectButton.Enabled = true;
                    connectionStatus.Text = "â— Conectado";
                    connectionStatus.ForeColor = Color.LimeGreen;
                    searchButton.Enabled = true;
                    statusLabel.Text = "Conectado - Listo para buscar";
                }
                catch (Exception ex)
                {
                    client?.Dispose();
                    client = null;
                    connectButton.Text = "ðŸ”Œ Conectar";
                    connectButton.BackColor = Color.FromArgb(0, 150, 0);
                    connectButton.Enabled = true;
                    connectionStatus.Text = "â— Error";
                    connectionStatus.ForeColor = Color.Red;
                    statusLabel.Text = $"Error: {ex.Message}";
                }
            }
        }
        
        private async Task PerformSearch()
        {
            if (client == null || string.IsNullOrWhiteSpace(searchBox.Text))
                return;
                
            try
            {
                statusLabel.Text = "Buscando...";
                resultsListView.Items.Clear();
                
                var results = await client.SearchAsync(
                    SearchQuery.FromText(searchBox.Text),
                    options: new SearchOptions(
                        responseLimit: 100,
                        fileLimit: 100,
                        filterResponses: true
                    )
                );
                
                foreach (var result in results.Take(1000))
                {
                    foreach (var file in result.Files.Take(10))
                    {
                        var item = new ListViewItem(new string[]
                        {
                            result.Username,
                            Path.GetFileName(file.Filename),
                            FormatFileSize(file.Size),
                            file.UploadSpeed.ToString(),
                            Path.GetDirectoryName(file.Filename) ?? ""
                        });
                        resultsListView.Items.Add(item);
                    }
                }
                
                statusLabel.Text = $"Resultados: {resultsListView.Items.Count} archivos";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Error en bÃºsqueda: {ex.Message}";
            }
        }
        
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}

