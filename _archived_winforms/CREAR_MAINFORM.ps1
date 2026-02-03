$content = @'
using System;
using System.Windows.Forms;
using System.Drawing;
using Soulseek;
using System.Threading.Tasks;

namespace SlskDown
{
    public class MainForm : Form
    {
        private SoulseekClient client;
        private TextBox searchBox;
        private Button searchButton;
        private ListView resultsListView;
        private Label statusLabel;
        
        public MainForm()
        {
            this.Text = "SlskDown - Funcional";
            this.Size = new Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 30, 30);
            
            client = new SoulseekClient();
            CreateControls();
            this.Load += async (s, e) => await AutoConnect();
        }
        
        private void CreateControls()
        {
            searchBox = new TextBox
            {
                Location = new Point(20, 20),
                Size = new Size(700, 30),
                Font = new Font("Segoe UI", 12),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White
            };
            
            searchButton = new Button
            {
                Text = "Buscar",
                Location = new Point(730, 20),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            searchButton.Click += async (s, e) => await Search();
            
            resultsListView = new ListView
            {
                Location = new Point(20, 60),
                Size = new Size(950, 550),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White
            };
            
            resultsListView.Columns.Add("Usuario", 150);
            resultsListView.Columns.Add("Archivo", 400);
            resultsListView.Columns.Add("Tamaño", 100);
            
            statusLabel = new Label
            {
                Location = new Point(20, 620),
                Size = new Size(950, 30),
                ForeColor = Color.LimeGreen,
                Text = "Iniciando..."
            };
            
            this.Controls.Add(searchBox);
            this.Controls.Add(searchButton);
            this.Controls.Add(resultsListView);
            this.Controls.Add(statusLabel);
        }
        
        private async Task AutoConnect()
        {
            try
            {
                statusLabel.Text = "Conectando...";
                await client.ConnectAsync("carbar", "Carlos66*");
                statusLabel.Text = "Conectado";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Error: " + ex.Message;
            }
        }
        
        private async Task Search()
        {
            try
            {
                string query = searchBox.Text.Trim();
                if (string.IsNullOrEmpty(query)) return;
                
                resultsListView.Items.Clear();
                statusLabel.Text = "Buscando...";
                
                var results = await client.SearchAsync(SearchQuery.FromText(query));
                
                int count = 0;
                foreach (var response in results.Responses)
                {
                    foreach (var file in response.Files)
                    {
                        var item = new ListViewItem(response.Username);
                        item.SubItems.Add(file.Filename);
                        item.SubItems.Add((file.Size / 1024 / 1024).ToString() + " MB");
                        resultsListView.Items.Add(item);
                        count++;
                    }
                }
                
                statusLabel.Text = count + " resultados";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Error: " + ex.Message;
            }
        }
    }
}
'@

Set-Content -Path "MainForm.cs" -Value $content -Encoding UTF8
Write-Host "MainForm.cs creado exitosamente"
