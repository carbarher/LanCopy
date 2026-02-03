using System;
using System.Collections.Generic;
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
        private ComboBox searchBox = null!;
        private Button searchButton = null!;
        private ListView resultsListView = null!;
        private ListView downloadsListView = null!;
        private Label statusLabel = null!;
        private ProgressBar downloadProgress = null!;
        private Button connectButton = null!;
        private Label connectionStatus = null!;
        private TabControl tabControl = null!;

        public MainForm()
        {
            InitializeComponent();
            InitializeUI();
        }

        private void InitializeComponent()
        {
            this.Text = "SlskDown - Soulseek Downloader";
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
        }

        private void InitializeUI()
        {
            // Crear TabControl
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Location = new Point(0, 100)
            };

            // PestaÃ±a de Resultados
            var resultsTab = new TabPage("ðŸ“Š Resultados");
            resultsListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            resultsListView.Columns.Add("Usuario", 150);
            resultsListView.Columns.Add("Archivo", 300);
            resultsListView.Columns.Add("TamaÃ±o", 100);
            resultsListView.Columns.Add("Bitrate", 80);
            resultsListView.Columns.Add("DuraciÃ³n", 80);
            resultsListView.Columns.Add("Carpeta", 200);
            resultsTab.Controls.Add(resultsListView);

            // PestaÃ±a de Descargas
            var downloadsTab = new TabPage("ðŸ“¥ Descargas");
            downloadsListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            downloadsListView.Columns.Add("Archivo", 300);
            downloadsListView.Columns.Add("Progreso", 150);
            downloadsListView.Columns.Add("Estado", 100);
            downloadsListView.Columns.Add("TamaÃ±o", 100);
            downloadsTab.Controls.Add(downloadsListView);

            tabControl.TabPages.Add(resultsTab);
            tabControl.TabPages.Add(downloadsTab);

            // Panel superior
            var topPanel = new Panel
            {
                Height = 100,
                Dock = DockStyle.Top
            };

            searchBox = new ComboBox
            {
                Location = new Point(10, 10),
                Width = 300,
                DropDownStyle = ComboBoxStyle.DropDown
            };

            searchButton = new Button
            {
                Text = "ðŸ” Buscar",
                Location = new Point(320, 8),
                Width = 100,
                Height = 27
            };
            searchButton.Click += SearchButton_Click;

            connectButton = new Button
            {
                Text = "ðŸ”Œ Conectar",
                Location = new Point(430, 8),
                Width = 100,
                Height = 27
            };
            connectButton.Click += ConnectButton_Click;

            statusLabel = new Label
            {
                Text = "Desconectado",
                Location = new Point(10, 45),
                Width = 200,
                ForeColor = Color.Red
            };

            downloadProgress = new ProgressBar
            {
                Location = new Point(10, 70),
                Width = 400,
                Height = 20
            };

            topPanel.Controls.AddRange(new Control[] { searchBox, searchButton, connectButton, statusLabel, downloadProgress });

            this.Controls.AddRange(new Control[] { topPanel, tabControl });
        }

        private async void SearchButton_Click(object? sender, EventArgs e)
        {
            if (client == null || !client.State.HasFlag(SoulseekClientStates.Connected))
            {
                MessageBox.Show("Debes conectar primero", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string searchText = searchBox.Text.Trim();
            if (string.IsNullOrEmpty(searchText))
                return;

            try
            {
                statusLabel.Text = "Buscando...";
                resultsListView.Items.Clear();

                var results = await client.SearchAsync(searchText, options: new SearchOptions
                {
                    ResponseLimit = 100,
                    FileLimit = 1000
                });

                foreach (var result in results)
                {
                    foreach (var file in result.Files)
                    {
                        var item = new ListViewItem(new string[]
                        {
                            result.Username,
                            file.Filename,
                            FormatSize(file.Size),
                            file.Bitrate?.ToString() ?? "",
                            file.Duration?.ToString() ?? "",
                            file.Directory ?? ""
                        });
                        resultsListView.Items.Add(item);
                    }
                }

                statusLabel.Text = $"Se encontraron {resultsListView.Items.Count} archivos";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error en bÃºsqueda: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "Error en bÃºsqueda";
            }
        }

        private async void ConnectButton_Click(object? sender, EventArgs e)
        {
            try
            {
                if (client != null && client.State.HasFlag(SoulseekClientStates.Connected))
                {
                    await client.DisconnectAsync();
                    client = null;
                    connectButton.Text = "ðŸ”Œ Conectar";
                    statusLabel.Text = "Desconectado";
                    statusLabel.ForeColor = Color.Red;
                    return;
                }

                statusLabel.Text = "Conectando...";
                connectButton.Enabled = false;

                client = new SoulseekClient();
                
                // Conectar con credenciales por defecto
                await client.ConnectAsync("carbar", "Carlos66*");
                
                connectButton.Text = "ðŸ”Œ Desconectar";
                statusLabel.Text = "Conectado";
                statusLabel.ForeColor = Color.Green;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error de conexiÃ³n: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "Error de conexiÃ³n";
                statusLabel.ForeColor = Color.Red;
            }
            finally
            {
                connectButton.Enabled = true;
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

