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
        private ListBox authorsListBox = null!;
        private RichTextBox authorSearchLog = null!;
        private Button startAuthorSearchButton = null!;
        private bool isAuthorSearchRunning = false;

        public MainForm()
        {
            InitializeComponent();
            SetupUI();
        }

        private void InitializeComponent()
        {
            this.Text = "SlskDown v3.1 - Soulseek Client";
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(45, 45, 48);
        }

        private void SetupUI()
        {
            // Panel superior
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(35, 35, 38)
            };
            this.Controls.Add(topPanel);

            // BotÃ³n conectar
            connectButton = new Button
            {
                Text = "ðŸ”Œ Conectar",
                Location = new Point(10, 15),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            connectButton.Click += ConnectButton_Click;
            topPanel.Controls.Add(connectButton);

            // Estado de conexiÃ³n
            connectionStatus = new Label
            {
                Text = "ðŸ”´ Desconectado",
                Location = new Point(140, 20),
                Size = new Size(150, 20),
                ForeColor = Color.Red
            };
            topPanel.Controls.Add(connectionStatus);

            // Buscador
            searchBox = new ComboBox
            {
                Location = new Point(320, 15),
                Size = new Size(300, 30),
                Font = new Font("Segoe UI", 10f),
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White
            };
            topPanel.Controls.Add(searchBox);

            // BotÃ³n buscar
            searchButton = new Button
            {
                Text = "ðŸ” Buscar",
                Location = new Point(630, 15),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            searchButton.Click += SearchButton_Click;
            topPanel.Controls.Add(searchButton);

            // TabControl
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 48)
            };
            this.Controls.Add(tabControl);

            // PestaÃ±a Resultados
            var resultsTab = new TabPage("ðŸ“Š Resultados");
            resultsTab.BackColor = Color.FromArgb(45, 45, 48);
            tabControl.TabPages.Add(resultsTab);

            // ListView resultados
            resultsListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White
            };
            resultsListView.Columns.Add("Usuario", 120);
            resultsListView.Columns.Add("Archivo", 300);
            resultsListView.Columns.Add("TamaÃ±o", 100);
            resultsListView.Columns.Add("Ext", 60);
            resultsListView.Columns.Add("Bitrate", 80);
            resultsListView.Columns.Add("DuraciÃ³n", 80);
            resultsListView.Columns.Add("Carpeta", 200);
            resultsTab.Controls.Add(resultsListView);

            // PestaÃ±a Descargas
            var downloadsTab = new TabPage("ðŸ“¥ Descargas");
            downloadsTab.BackColor = Color.FromArgb(45, 45, 48);
            tabControl.TabPages.Add(downloadsTab);

            // ListView descargas
            downloadsListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White
            };
            downloadsListView.Columns.Add("Archivo", 300);
            downloadsListView.Columns.Add("Progreso", 150);
            downloadsListView.Columns.Add("Velocidad", 100);
            downloadsListView.Columns.Add("Estado", 100);
            downloadsTab.Controls.Add(downloadsListView);

            // PestaÃ±a Auto-BÃºsqueda
            var autoSearchTab = new TabPage("ðŸš€ Auto-BÃºsqueda");
            autoSearchTab.BackColor = Color.FromArgb(45, 45, 48);
            tabControl.TabPages.Add(autoSearchTab);

            // Panel auto-bÃºsqueda
            var autoPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };
            autoSearchTab.Controls.Add(autoPanel);

            // Lista de autores
            authorsListBox = new ListBox
            {
                Location = new Point(10, 10),
                Size = new Size(300, 200),
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White
            };
            autoPanel.Controls.Add(authorsListBox);

            // BotÃ³n cargar autores
            var loadAuthorsButton = new Button
            {
                Text = "ðŸ“‚ Cargar Autores",
                Location = new Point(320, 10),
                Size = new Size(150, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            loadAuthorsButton.Click += LoadAuthorsButton_Click;
            autoPanel.Controls.Add(loadAuthorsButton);

            // BotÃ³n unificado de bÃºsqueda
            startAuthorSearchButton = new Button
            {
                Text = "ðŸš€ Iniciar BÃºsqueda",
                Location = new Point(320, 50),
                Size = new Size(150, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            startAuthorSearchButton.Click += StartAuthorSearchButton_Click;
            autoPanel.Controls.Add(startAuthorSearchButton);

            // Log de bÃºsqueda
            authorSearchLog = new RichTextBox
            {
                Location = new Point(10, 220),
                Size = new Size(760, 300),
                BackColor = Color.FromArgb(30, 30, 32),
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 9f),
                ReadOnly = true
            };
            autoPanel.Controls.Add(authorSearchLog);

            // Barra de estado
            var statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                BackColor = Color.FromArgb(35, 35, 38)
            };
            this.Controls.Add(statusPanel);

            statusLabel = new Label
            {
                Text = "Listo",
                Location = new Point(10, 5),
                Size = new Size(400, 20),
                ForeColor = Color.White
            };
            statusPanel.Controls.Add(statusLabel);

            downloadProgress = new ProgressBar
            {
                Location = new Point(500, 5),
                Size = new Size(200, 20),
                Style = ProgressBarStyle.Continuous
            };
            statusPanel.Controls.Add(downloadProgress);
        }

        private async void ConnectButton_Click(object? sender, EventArgs e)
        {
            if (client?.State == SoulseekClientStates.Connected)
            {
                await client.DisconnectAsync();
                connectionStatus.Text = "ðŸ”´ Desconectado";
                connectionStatus.ForeColor = Color.Red;
                connectButton.Text = "ðŸ”Œ Conectar";
                statusLabel.Text = "Desconectado";
                return;
            }

            try
            {
                connectButton.Enabled = false;
                statusLabel.Text = "Conectando...";
                
                client = new SoulseekClient();
                
                // Conectar con credenciales por defecto
                await client.ConnectAsync("carbar", "Carlos66*");
                
                connectionStatus.Text = "ðŸŸ¢ Conectado";
                connectionStatus.ForeColor = Color.Lime;
                connectButton.Text = "ðŸ”Œ Desconectar";
                statusLabel.Text = $"Conectado - {client.Username}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al conectar: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "Error de conexiÃ³n";
            }
            finally
            {
                connectButton.Enabled = true;
            }
        }

        private async void SearchButton_Click(object? sender, EventArgs e)
        {
            if (client?.State != SoulseekClientStates.Connected)
            {
                MessageBox.Show("Debes estar conectado para buscar", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string query = searchBox.Text.Trim();
            if (string.IsNullOrEmpty(query))
                return;

            try
            {
                searchButton.Enabled = false;
                statusLabel.Text = $"Buscando: {query}";
                resultsListView.Items.Clear();

                var results = await client.SearchAsync(SearchQuery.FromText(query));
                
                foreach (var result in results.Take(100))
                {
                    foreach (var file in result.Files.Take(10))
                    {
                        var item = new ListViewItem(new string[]
                        {
                            result.Username,
                            Path.GetFileNameWithoutExtension(file.Filename),
                            FormatFileSize(file.Size),
                            Path.GetExtension(file.Filename).TrimStart('.'),
                            file.BitRate.HasValue ? $"{file.BitRate} kbps" : "",
                            file.Duration.HasValue ? $"{file.Duration:mm\\:ss}" : "",
                            Path.GetDirectoryName(file.Filename) ?? ""
                        });
                        item.Tag = new { Result = result, File = file };
                        resultsListView.Items.Add(item);
                    }
                }

                statusLabel.Text = $"Se encontraron {resultsListView.Items.Count} archivos";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error en bÃºsqueda: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                searchButton.Enabled = true;
            }
        }

        private void LoadAuthorsButton_Click(object? sender, EventArgs e)
        {
            using var openFileDialog = new OpenFileDialog
            {
                Filter = "Archivos de texto (*.txt)|*.txt|Todos los archivos (*.*)|*.*",
                Title = "Seleccionar lista de autores"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var authors = File.ReadAllLines(openFileDialog.FileName)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .ToList();
                    
                    authorsListBox.Items.Clear();
                    foreach (var author in authors)
                    {
                        authorsListBox.Items.Add(author);
                    }
                    
                    authorSearchLog.AppendText($"âœ… Cargados {authors.Count} autores desde {Path.GetFileName(openFileDialog.FileName)}\r\n");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al cargar autores: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void StartAuthorSearchButton_Click(object? sender, EventArgs e)
        {
            if (isAuthorSearchRunning)
            {
                // Detener bÃºsqueda
                isAuthorSearchRunning = false;
                startAuthorSearchButton.Text = "ðŸš€ Iniciar BÃºsqueda";
                startAuthorSearchButton.BackColor = Color.FromArgb(0, 120, 215);
                authorSearchLog.AppendText("\r\nðŸ›‘ BÃºsqueda detenida por el usuario\r\n");
                return;
            }

            if (client?.State != SoulseekClientStates.Connected)
            {
                MessageBox.Show("Debes estar conectado para iniciar bÃºsqueda", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (authorsListBox.Items.Count == 0)
            {
                MessageBox.Show("Debes cargar una lista de autores primero", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Iniciar bÃºsqueda
            isAuthorSearchRunning = true;
            startAuthorSearchButton.Text = "â¹ï¸ Detener BÃºsqueda";
            startAuthorSearchButton.BackColor = Color.FromArgb(220, 53, 69);
            
            authorSearchLog.Clear();
            authorSearchLog.AppendText($"ðŸš€ Iniciando bÃºsqueda automÃ¡tica...\r\n");
            authorSearchLog.AppendText($"ðŸ“š Total autores: {authorsListBox.Items.Count}\r\n");
            authorSearchLog.AppendText($"â° Inicio: {DateTime.Now:HH:mm:ss}\r\n");
            authorSearchLog.AppendText($"========================================\r\n");

            try
            {
                var authors = authorsListBox.Items.Cast<string>().ToList();
                int processedAuthors = 0;

                foreach (var author in authors)
                {
                    if (!isAuthorSearchRunning) break;

                    try
                    {
                        authorSearchLog.AppendText($"\r\nðŸ” Buscando: {author}\r\n");
                        
                        var results = await client.SearchAsync(SearchQuery.FromText(author));
                        var validResults = results.Take(50).ToList();
                        
                        int downloadsThisAuthor = 0;
                        foreach (var result in validResults)
                        {
                            if (!isAuthorSearchRunning) break;
                            
                            foreach (var file in result.Files.Take(5))
                            {
                                if (file.Size > 1024 * 1024) // Mayor a 1MB
                                {
                                    downloadsThisAuthor++;
                                    // AquÃ­ se iniciarÃ­a la descarga real
                                }
                            }
                        }
                        
                        processedAuthors++;
                        authorSearchLog.AppendText($"   ðŸ“¥ {downloadsThisAuthor} archivos encontrados\r\n");
                        authorSearchLog.AppendText($"   â±ï¸ {DateTime.Now:HH:mm:ss}\r\n");
                        
                        // PequeÃ±a pausa entre bÃºsquedas
                        await Task.Delay(1000);
                    }
                    catch (Exception ex)
                    {
                        authorSearchLog.AppendText($"   âŒ Error: {ex.Message}\r\n");
                    }
                }

                // Resumen final
                authorSearchLog.AppendText($"\r\n========================================\r\n");
                authorSearchLog.AppendText($"âœ… BÃšSQUEDA COMPLETADA\r\n");
                authorSearchLog.AppendText($"ðŸ“š Autores procesados: {processedAuthors}\r\n");
                authorSearchLog.AppendText($"â° Finalizado: {DateTime.Now:HH:mm:ss}\r\n");
            }
            catch (Exception ex)
            {
                authorSearchLog.AppendText($"\r\nâŒ Error general: {ex.Message}\r\n");
            }
            finally
            {
                isAuthorSearchRunning = false;
                startAuthorSearchButton.Text = "ðŸš€ Iniciar BÃºsqueda";
                startAuthorSearchButton.BackColor = Color.FromArgb(0, 120, 215);
            }
        }

        private static string FormatFileSize(long bytes)
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

