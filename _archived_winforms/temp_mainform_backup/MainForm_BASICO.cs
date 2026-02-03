using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Soulseek;

namespace SlskDown
{
    public partial class MainForm : Form
    {
        private SoulseekClient? client;
        private Button connectButton;
        private Button searchButton;
        private TextBox searchBox;
        private ListView resultsListView;
        private Label statusLabel;
        
        public MainForm()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.Text = "SlskDown";
            this.Size = new Size(800, 600);
            this.BackColor = Color.FromArgb(30, 30, 30);
            
            // BotÃ³n conectar
            connectButton = new Button();
            connectButton.Text = "Conectar";
            connectButton.Location = new Point(20, 20);
            connectButton.Size = new Size(100, 30);
            connectButton.Click += ConnectButton_Click;
            this.Controls.Add(connectButton);
            
            // BÃºsqueda
            searchBox = new TextBox();
            searchBox.Location = new Point(140, 20);
            searchBox.Size = new Size(300, 30);
            this.Controls.Add(searchBox);
            
            searchButton = new Button();
            searchButton.Text = "Buscar";
            searchButton.Location = new Point(450, 20);
            searchButton.Size = new Size(80, 30);
            searchButton.Click += SearchButton_Click;
            this.Controls.Add(searchButton);
            
            // Resultados
            resultsListView = new ListView();
            resultsListView.Location = new Point(20, 60);
            resultsListView.Size = new Size(750, 400);
            resultsListView.View = View.Details;
            resultsListView.FullRowSelect = true;
            
            resultsListView.Columns.Add("Usuario", 150);
            resultsListView.Columns.Add("Archivo", 300);
            resultsListView.Columns.Add("TamaÃ±o", 100);
            
            this.Controls.Add(resultsListView);
            
            // Status
            statusLabel = new Label();
            statusLabel.Text = "Listo";
            statusLabel.Location = new Point(20, 480);
            statusLabel.Size = new Size(500, 20);
            statusLabel.ForeColor = Color.White;
            this.Controls.Add(statusLabel);
        }
        
        private async void ConnectButton_Click(object? sender, EventArgs e)
        {
            if (client != null)
            {
                client.Dispose();
                client = null;
                connectButton.Text = "Conectar";
                statusLabel.Text = "Desconectado";
            }
            else
            {
                try
                {
                    client = new SoulseekClient();
                    await client.ConnectAsync("carbar", "Carlos66*");
                    connectButton.Text = "Desconectar";
                    statusLabel.Text = "Conectado";
                }
                catch (Exception ex)
                {
                    statusLabel.Text = $"Error: {ex.Message}";
                }
            }
        }
        
        private async void SearchButton_Click(object? sender, EventArgs e)
        {
            if (client == null || string.IsNullOrEmpty(searchBox.Text))
                return;
                
            try
            {
                statusLabel.Text = "Buscando...";
                resultsListView.Items.Clear();
                
                var results = await client.SearchAsync(SearchQuery.FromText(searchBox.Text));
                
                foreach (var result in results.Take(100))
                {
                    foreach (var file in result.Files.Take(5))
                    {
                        var item = new ListViewItem(new string[]
                        {
                            result.Username,
                            Path.GetFileName(file.Filename),
                            FormatSize(file.Size)
                        });
                        resultsListView.Items.Add(item);
                    }
                }
                
                statusLabel.Text = $"{resultsListView.Items.Count} resultados";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Error: {ex.Message}";
            }
        }
        
        private string FormatSize(long bytes)
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

