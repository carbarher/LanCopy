using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Soulseek;

namespace SlskDown
{
    public class MainForm : Form
    {
        private SoulseekClient? client;
        private Button connectButton;
        private Button searchButton;
        private TextBox searchBox;
        private ListView resultsListView;
        private Label statusLabel;
        private TabControl tabControl;
        
        public MainForm()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.Text = "SlskDown - Cliente Soulseek";
            this.Size = new Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 30, 30);
            
            // TabControl
            tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;
            tabControl.BackColor = Color.FromArgb(40, 40, 40);
            
            // PestaÃ±a BÃºsqueda
            var searchTab = new TabPage("ðŸ” BÃºsqueda");
            CreateSearchTab(searchTab);
            
            // PestaÃ±a Descargas  
            var downloadsTab = new TabPage("â¬‡ï¸ Descargas");
            CreateDownloadsTab(downloadsTab);
            
            tabControl.TabPages.Add(searchTab);
            tabControl.TabPages.Add(downloadsTab);
            
            this.Controls.Add(tabControl);
        }
        
        private void CreateSearchTab(TabPage parent)
        {
            var panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(20);
            
            // BotÃ³n conectar
            connectButton = new Button();
            connectButton.Text = "ðŸ”Œ Conectar";
            connectButton.Location = new Point(20, 20);
            connectButton.Size = new Size(120, 35);
            connectButton.BackColor = Color.FromArgb(0, 150, 0);
            connectButton.ForeColor = Color.White;
            connectButton.FlatStyle = FlatStyle.Flat;
            connectButton.Click += ConnectButton_Click;
            panel.Controls.Add(connectButton);
            
            // Search box
            searchBox = new TextBox();
            searchBox.Location = new Point(160, 20);
            searchBox.Size = new Size(400, 35);
            searchBox.BackColor = Color.FromArgb(60, 60, 60);
            searchBox.ForeColor = Color.White;
            searchBox.Font = new Font("Segoe UI", 10);
            panel.Controls.Add(searchBox);
            
            // BotÃ³n buscar
            searchButton = new Button();
            searchButton.Text = "ðŸ” Buscar";
            searchButton.Location = new Point(580, 20);
            searchButton.Size = new Size(100, 35);
            searchButton.BackColor = Color.FromArgb(0, 120, 215);
            searchButton.ForeColor = Color.White;
            searchButton.FlatStyle = FlatStyle.Flat;
            searchButton.Enabled = false;
            searchButton.Click += SearchButton_Click;
            panel.Controls.Add(searchButton);
            
            // Lista de resultados
            resultsListView = new ListView();
            resultsListView.Location = new Point(20, 70);
            resultsListView.Size = new Size(900, 450);
            resultsListView.BackColor = Color.FromArgb(50, 50, 50);
            resultsListView.ForeColor = Color.White;
            resultsListView.View = View.Details;
            resultsListView.FullRowSelect = true;
            resultsListView.GridLines = true;
            
            // Columnas
            resultsListView.Columns.Add("Usuario", 150);
            resultsListView.Columns.Add("Archivo", 350);
            resultsListView.Columns.Add("TamaÃ±o", 100);
            resultsListView.Columns.Add("Velocidad", 100);
            resultsListView.Columns.Add("Carpeta", 200);
            
            panel.Controls.Add(resultsListView);
            
            // Status label
            statusLabel = new Label();
            statusLabel.Text = "Desconectado - Haz clic en Conectar";
            statusLabel.Location = new Point(20, 540);
            statusLabel.Size = new Size(600, 20);
            statusLabel.ForeColor = Color.LightGray;
            panel.Controls.Add(statusLabel);
            
            parent.Controls.Add(panel);
        }
        
        private void CreateDownloadsTab(TabPage parent)
        {
            var panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(20);
            
            var label = new Label();
            label.Text = "Descargas activas aparecerÃ¡n aquÃ­";
            label.Location = new Point(20, 20);
            label.Size = new Size(400, 20);
            label.ForeColor = Color.LightGray;
            panel.Controls.Add(label);
            
            parent.Controls.Add(panel);
        }
        
        private async void ConnectButton_Click(object? sender, EventArgs e)
        {
            if (client != null)
            {
                // Desconectar
                client.Dispose();
                client = null;
                connectButton.Text = "ðŸ”Œ Conectar";
                connectButton.BackColor = Color.FromArgb(0, 150, 0);
                searchButton.Enabled = false;
                statusLabel.Text = "Desconectado";
                resultsListView.Items.Clear();
            }
            else
            {
                // Conectar
                try
                {
                    connectButton.Text = "â³ Conectando...";
                    connectButton.BackColor = Color.FromArgb(150, 150, 0);
                    connectButton.Enabled = false;
                    statusLabel.Text = "Conectando a Soulseek...";
                    
                    var options = new SoulseekClientOptions(
                        listenPort: new Random().Next(50000, 60000),
                        enableDistributedNetwork: false
                    );
                    
                    client = new SoulseekClient(options);
                    await client.ConnectAsync("carbar", "Carlos66*");
                    
                    connectButton.Text = "ðŸ”Œ Desconectar";
                    connectButton.BackColor = Color.FromArgb(200, 50, 50);
                    connectButton.Enabled = true;
                    searchButton.Enabled = true;
                    statusLabel.Text = "âœ… Conectado - Listo para buscar";
                }
                catch (Exception ex)
                {
                    client?.Dispose();
                    client = null;
                    connectButton.Text = "ðŸ”Œ Conectar";
                    connectButton.BackColor = Color.FromArgb(0, 150, 0);
                    connectButton.Enabled = true;
                    searchButton.Enabled = false;
                    statusLabel.Text = $"âŒ Error de conexiÃ³n: {ex.Message}";
                }
            }
        }
        
        private async void SearchButton_Click(object? sender, EventArgs e)
        {
            if (client == null || string.IsNullOrWhiteSpace(searchBox.Text))
            {
                statusLabel.Text = "âŒ Debes estar conectado y escribir un tÃ©rmino de bÃºsqueda";
                return;
            }
            
            try
            {
                searchButton.Text = "â³ Buscando...";
                searchButton.Enabled = false;
                statusLabel.Text = $"ðŸ” Buscando: {searchBox.Text}...";
                resultsListView.Items.Clear();
                
                var results = await client.SearchAsync(
                    SearchQuery.FromText(searchBox.Text),
                    options: new SearchOptions(
                        responseLimit: 100,
                        fileLimit: 100,
                        filterResponses: true
                    )
                );
                
                int totalFiles = 0;
                foreach (var response in results.Take(100))
                {
                    foreach (var file in response.Files.Take(10))
                    {
                        var item = new ListViewItem(new string[]
                        {
                            response.Username,
                            Path.GetFileName(file.Filename),
                            FormatFileSize(file.Size),
                            file.UploadSpeed.ToString(),
                            Path.GetDirectoryName(file.Filename) ?? ""
                        });
                        resultsListView.Items.Add(item);
                        totalFiles++;
                    }
                }
                
                statusLabel.Text = $"âœ… {totalFiles} archivos encontrados de {results.Count()} usuarios";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"âŒ Error en bÃºsqueda: {ex.Message}";
            }
            finally
            {
                searchButton.Text = "ðŸ” Buscar";
                searchButton.Enabled = true;
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
        
        // Deshabilitado para evitar mÃºltiples puntos de entrada
        // [STAThread]
        static void Main_DISABLED()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}

