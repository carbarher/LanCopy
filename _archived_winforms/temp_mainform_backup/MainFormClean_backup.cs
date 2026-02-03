using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Soulseek;
using System.Text.Json;
using System.Collections.Generic;

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
        private Button connectButton = null!;
        private Label connectionStatus = null!;
        private TabControl tabControl = null!;
        private ListBox authorsListBox = null!;
        private RichTextBox authorSearchLog = null!;
        private Button startAuthorSearchButton = null!;
        private bool isAuthorSearchRunning;
        
        // Config
        private TextBox usernameTextBox = null!;
        private TextBox passwordTextBox = null!;
        private TextBox downloadDirTextBox = null!;
        private Button saveConfigButton = null!;
        
        // Filtros
        private TextBox filterTextBox = null!;
        private ComboBox extensionFilterBox = null!;
        private NumericUpDown minSizeBox = null!;
        private NumericUpDown maxSizeBox = null!;
        
        // Descargas
        private Dictionary<string, DownloadInfo> activeDownloads = new();
        private Button downloadSelectedButton = null!;
        private Button openFolderButton = null!;
        private Label selectedCountLabel = null!;
        
        // Historial y favoritos
        private List<string> searchHistory = new();
        private List<string> favorites = new();
        private Button addToFavoritesButton = null!;
        private ComboBox favoritesBox = null!;

        public MainForm()
        {
            InitializeComponent();
            SetupUI();
            LoadConfig();
            LoadSearchHistory();
            LoadFavorites();
        }

        private void InitializeComponent()
        {
            this.Text = "SlskDown v3.1 - Soulseek Client";
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
        }

        private void SetupUI()
        {
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(35, 35, 38)
            };
            this.Controls.Add(topPanel);

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

            connectionStatus = new Label
            {
                Text = "ðŸ”´ Desconectado",
                Location = new Point(140, 20),
                Size = new Size(150, 20),
                ForeColor = Color.Red
            };
            topPanel.Controls.Add(connectionStatus);

            searchBox = new ComboBox
            {
                Location = new Point(320, 15),
                Size = new Size(300, 30),
                Font = new Font("Segoe UI", 10f),
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White,
                DropDownStyle = ComboBoxStyle.DropDown
            };
            topPanel.Controls.Add(searchBox);

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

            addToFavoritesButton = new Button
            {
                Text = "â­ Favorito",
                Location = new Point(740, 15),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(255, 193, 7),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat
            };
            addToFavoritesButton.Click += AddToFavoritesButton_Click;
            topPanel.Controls.Add(addToFavoritesButton);

            favoritesBox = new ComboBox
            {
                Location = new Point(850, 15),
                Size = new Size(200, 30),
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            favoritesBox.SelectedIndexChanged += FavoritesBox_SelectedIndexChanged;
            topPanel.Controls.Add(favoritesBox);

            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 48)
            };
            this.Controls.Add(tabControl);

            var resultsTab = new TabPage("ðŸ“Š Resultados");
            resultsTab.BackColor = Color.FromArgb(45, 45, 48);
            tabControl.TabPages.Add(resultsTab);

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
            resultsListView.MultiSelect = true;
            resultsListView.SelectedIndexChanged += (s, e) => 
            {
                if (selectedCountLabel != null)
                    selectedCountLabel.Text = $"Seleccionados: {resultsListView.SelectedItems.Count}";
            };
            resultsListView.DoubleClick += ResultsListView_DoubleClick;
            
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(35, 35, 38)
            };
            resultsTab.Controls.Add(buttonPanel);

            downloadSelectedButton = new Button
            {
                Text = "ðŸ“¥ Descargar Seleccionados",
                Location = new Point(10, 5),
                Size = new Size(180, 30),
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            downloadSelectedButton.Click += DownloadSelectedButton_Click;
            buttonPanel.Controls.Add(downloadSelectedButton);

            var selectAllButton = new Button
            {
                Text = "âœ“ Seleccionar Todo",
                Location = new Point(200, 5),
                Size = new Size(130, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            selectAllButton.Click += (s, e) => { foreach (ListViewItem item in resultsListView.Items) item.Selected = true; };
            buttonPanel.Controls.Add(selectAllButton);
            
            resultsTab.Controls.Add(resultsListView);

            var downloadsTab = new TabPage("ðŸ“¥ Descargas");
            downloadsTab.BackColor = Color.FromArgb(45, 45, 48);
            tabControl.TabPages.Add(downloadsTab);

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
            
            var downloadButtonPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(35, 35, 38)
            };
            downloadsTab.Controls.Add(downloadButtonPanel);

            openFolderButton = new Button
            {
                Text = "ðŸ“‚ Abrir Carpeta",
                Location = new Point(10, 5),
                Size = new Size(150, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            openFolderButton.Click += (s, e) => System.Diagnostics.Process.Start("explorer", downloadDir);
            downloadButtonPanel.Controls.Add(openFolderButton);
            
            downloadsTab.Controls.Add(downloadsListView);

            var autoSearchTab = new TabPage("ðŸš€ Auto-BÃºsqueda");
            autoSearchTab.BackColor = Color.FromArgb(45, 45, 48);
            tabControl.TabPages.Add(autoSearchTab);

            var autoPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };
            autoSearchTab.Controls.Add(autoPanel);

            authorsListBox = new ListBox
            {
                Location = new Point(10, 10),
                Size = new Size(300, 200),
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White
            };
            autoPanel.Controls.Add(authorsListBox);

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

            // PestaÃ±a Filtros
            var filtersTab = new TabPage("ðŸ”§ Filtros");
            filtersTab.BackColor = Color.FromArgb(45, 45, 48);
            tabControl.TabPages.Add(filtersTab);

            var filtersPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20)
            };
            filtersTab.Controls.Add(filtersPanel);

            var filterLabel = new Label
            {
                Text = "Filtrar por texto:",
                Location = new Point(20, 20),
                Size = new Size(150, 20),
                ForeColor = Color.White
            };
            filtersPanel.Controls.Add(filterLabel);

            filterTextBox = new TextBox
            {
                Location = new Point(180, 20),
                Size = new Size(300, 25),
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White
            };
            filtersPanel.Controls.Add(filterTextBox);

            var extLabel = new Label
            {
                Text = "ExtensiÃ³n:",
                Location = new Point(20, 60),
                Size = new Size(150, 20),
                ForeColor = Color.White
            };
            filtersPanel.Controls.Add(extLabel);

            extensionFilterBox = new ComboBox
            {
                Location = new Point(180, 60),
                Size = new Size(150, 25),
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            extensionFilterBox.Items.AddRange(new object[] { "Todas", "epub", "mobi", "pdf", "azw3", "mp3", "flac" });
            extensionFilterBox.SelectedIndex = 0;
            filtersPanel.Controls.Add(extensionFilterBox);

            var minSizeLabel = new Label
            {
                Text = "TamaÃ±o mÃ­nimo (MB):",
                Location = new Point(20, 100),
                Size = new Size(150, 20),
                ForeColor = Color.White
            };
            filtersPanel.Controls.Add(minSizeLabel);

            minSizeBox = new NumericUpDown
            {
                Location = new Point(180, 100),
                Size = new Size(100, 25),
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White,
                Minimum = 0,
                Maximum = 10000,
                Value = 0
            };
            filtersPanel.Controls.Add(minSizeBox);

            var maxSizeLabel = new Label
            {
                Text = "TamaÃ±o mÃ¡ximo (MB):",
                Location = new Point(20, 140),
                Size = new Size(150, 20),
                ForeColor = Color.White
            };
            filtersPanel.Controls.Add(maxSizeLabel);

            maxSizeBox = new NumericUpDown
            {
                Location = new Point(180, 140),
                Size = new Size(100, 25),
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White,
                Minimum = 0,
                Maximum = 10000,
                Value = 0
            };
            filtersPanel.Controls.Add(maxSizeBox);

            var applyFiltersButton = new Button
            {
                Text = "âœ“ Aplicar Filtros",
                Location = new Point(20, 190),
                Size = new Size(150, 35),
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            applyFiltersButton.Click += ApplyFiltersButton_Click;
            filtersPanel.Controls.Add(applyFiltersButton);

            // PestaÃ±a Config
            var configTab = new TabPage("âš™ï¸ Config");
            configTab.BackColor = Color.FromArgb(45, 45, 48);
            tabControl.TabPages.Add(configTab);

            var configPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20)
            };
            configTab.Controls.Add(configPanel);

            var userLabel = new Label
            {
                Text = "Usuario:",
                Location = new Point(20, 20),
                Size = new Size(150, 20),
                ForeColor = Color.White
            };
            configPanel.Controls.Add(userLabel);

            usernameTextBox = new TextBox
            {
                Location = new Point(180, 20),
                Size = new Size(300, 25),
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White,
                Text = username
            };
            configPanel.Controls.Add(usernameTextBox);

            var passLabel = new Label
            {
                Text = "ContraseÃ±a:",
                Location = new Point(20, 60),
                Size = new Size(150, 20),
                ForeColor = Color.White
            };
            configPanel.Controls.Add(passLabel);

            passwordTextBox = new TextBox
            {
                Location = new Point(180, 60),
                Size = new Size(300, 25),
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White,
                Text = password,
                UseSystemPasswordChar = true
            };
            configPanel.Controls.Add(passwordTextBox);

            var dirLabel = new Label
            {
                Text = "Carpeta descargas:",
                Location = new Point(20, 100),
                Size = new Size(150, 20),
                ForeColor = Color.White
            };
            configPanel.Controls.Add(dirLabel);

            downloadDirTextBox = new TextBox
            {
                Location = new Point(180, 100),
                Size = new Size(300, 25),
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White,
                Text = downloadDir
            };
            configPanel.Controls.Add(downloadDirTextBox);

            saveConfigButton = new Button
            {
                Text = "ðŸ’¾ Guardar ConfiguraciÃ³n",
                Location = new Point(20, 150),
                Size = new Size(200, 35),
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            saveConfigButton.Click += SaveConfigButton_Click;
            configPanel.Controls.Add(saveConfigButton);

            var statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                BackColor = Color.FromArgb(35, 35, 38)
            };
            this.Controls.Add(statusPanel);

            statusLabel = new Label
            {
                Text = "Listo - SlskDown v3.1 Simple",
                Location = new Point(10, 5),
                Size = new Size(500, 20),
                ForeColor = Color.White
            };
            statusPanel.Controls.Add(statusLabel);

            selectedCountLabel = new Label
            {
                Text = "Seleccionados: 0",
                Location = new Point(520, 5),
                Size = new Size(200, 20),
                ForeColor = Color.LightBlue
            };
            statusPanel.Controls.Add(selectedCountLabel);
        }

        private async void ConnectButton_Click(object? sender, EventArgs e)
        {
            if (client?.State == SoulseekClientStates.Connected)
            {
                client.Disconnect();
                connectionStatus.Text = "ðŸ”´ Desconectado";
                connectionStatus.ForeColor = Color.Red;
                connectButton.Text = "ðŸ”Œ Conectar";
                return;
            }

            try
            {
                connectButton.Enabled = false;
                client = new SoulseekClient();
                await client.ConnectAsync("carbar", "Carlos66*");
                connectionStatus.Text = "ðŸŸ¢ Conectado";
                connectionStatus.ForeColor = Color.Lime;
                connectButton.Text = "ðŸ”Œ Desconectar";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show("Debes estar conectado", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string query = searchBox.Text.Trim();
            if (string.IsNullOrEmpty(query)) return;

            // Agregar al historial
            if (!searchHistory.Contains(query))
            {
                searchHistory.Add(query);
                searchBox.Items.Add(query);
                SaveSearchHistory();
            }

            try
            {
                searchButton.Enabled = false;
                resultsListView.Items.Clear();
                var searchResult = await client.SearchAsync(SearchQuery.FromText(query));
                var results = searchResult.Responses;
                
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
                            file.Length.HasValue ? $"{file.Length}s" : "",
                            Path.GetDirectoryName(file.Filename) ?? ""
                        });
                        item.Tag = new SearchResultData
                        {
                            Username = result.Username,
                            Filename = file.Filename,
                            Size = file.Size
                        };
                        resultsListView.Items.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                Filter = "Archivos de texto (*.txt)|*.txt|Todos (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var authors = System.IO.File.ReadAllLines(openFileDialog.FileName)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .ToList();
                    
                    authorsListBox.Items.Clear();
                    foreach (var author in authors)
                    {
                        authorsListBox.Items.Add(author);
                    }
                    
                    authorSearchLog.AppendText($"âœ… Cargados {authors.Count} autores\r\n");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void StartAuthorSearchButton_Click(object? sender, EventArgs e)
        {
            if (isAuthorSearchRunning)
            {
                isAuthorSearchRunning = false;
                startAuthorSearchButton.Text = "ðŸš€ Iniciar BÃºsqueda";
                startAuthorSearchButton.BackColor = Color.FromArgb(0, 120, 215);
                authorSearchLog.AppendText("\r\nðŸ›‘ BÃºsqueda detenida\r\n");
                return;
            }

            if (client?.State != SoulseekClientStates.Connected)
            {
                MessageBox.Show("Debes estar conectado", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (authorsListBox.Items.Count == 0)
            {
                MessageBox.Show("Carga una lista de autores primero", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            isAuthorSearchRunning = true;
            startAuthorSearchButton.Text = "â¹ï¸ Detener BÃºsqueda";
            startAuthorSearchButton.BackColor = Color.FromArgb(220, 53, 69);
            
            authorSearchLog.Clear();
            authorSearchLog.AppendText($"ðŸš€ Iniciando bÃºsqueda...\r\n");
            authorSearchLog.AppendText($"ðŸ“š Total: {authorsListBox.Items.Count}\r\n");
            authorSearchLog.AppendText($"========================================\r\n");

            try
            {
                var authors = authorsListBox.Items.Cast<string>().ToList();
                int processed = 0;
                int totalFiles = 0;

                foreach (var author in authors)
                {
                    if (!isAuthorSearchRunning) break;

                    try
                    {
                        authorSearchLog.AppendText($"\r\nðŸ” {author}\r\n");
                        var searchResult = await client.SearchAsync(SearchQuery.FromText(author));
                        var results = searchResult.Responses;
                        
                        int count = 0;
                        foreach (var result in results.Take(50))
                        {
                            if (!isAuthorSearchRunning) break;
                            count += result.Files.Take(5).Count(f => f.Size > 1024 * 1024);
                        }
                        
                        processed++;
                        totalFiles += count;
                        authorSearchLog.AppendText($"   ðŸ“¥ {count} archivos\r\n");
                        await Task.Delay(1000);
                    }
                    catch (Exception ex)
                    {
                        authorSearchLog.AppendText($"   âŒ Error: {ex.Message}\r\n");
                    }
                }

                authorSearchLog.AppendText($"\r\n========================================\r\n");
                authorSearchLog.AppendText($"âœ… COMPLETADO\r\n");
                authorSearchLog.AppendText($"ðŸ“š Procesados: {processed}\r\n");
                authorSearchLog.AppendText($"ðŸ“¥ Total archivos: {totalFiles}\r\n");
            }
            catch (Exception ex)
            {
                authorSearchLog.AppendText($"\r\nâŒ Error: {ex.Message}\r\n");
            }
            finally
            {
                isAuthorSearchRunning = false;
                startAuthorSearchButton.Text = "ðŸš€ Iniciar BÃºsqueda";
                startAuthorSearchButton.BackColor = Color.FromArgb(0, 120, 215);
            }
        }

        
        private void AddToFavoritesButton_Click(object? sender, EventArgs e)
        {
            string query = searchBox.Text.Trim();
            if (string.IsNullOrEmpty(query)) return;

            if (!favorites.Contains(query))
            {
                favorites.Add(query);
                favoritesBox.Items.Add(query);
                SaveFavorites();
                MessageBox.Show("Agregado a favoritos", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void FavoritesBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (favoritesBox.SelectedItem != null)
            {
                searchBox.Text = favoritesBox.SelectedItem.ToString();
            }
        }

        private async void DownloadSelectedButton_Click(object? sender, EventArgs e)
        {
            if (resultsListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("Selecciona archivos para descargar", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            foreach (ListViewItem item in resultsListView.SelectedItems)
            {
                if (item.Tag is SearchResultData data)
                {
                    await DownloadFile(data);
                }
            }
        }

        private async void ResultsListView_DoubleClick(object? sender, EventArgs e)
        {
            if (resultsListView.SelectedItems.Count > 0)
            {
                var item = resultsListView.SelectedItems[0];
                if (item.Tag is SearchResultData data)
                {
                    await DownloadFile(data);
                }
            }
        }

        private async Task DownloadFile(SearchResultData data)
        {
            if (client?.State != SoulseekClientStates.Connected) return;

            string key = $"{data.Username}_{Path.GetFileName(data.Filename)}";
            if (activeDownloads.ContainsKey(key)) return;

            try
            {
                Directory.CreateDirectory(downloadDir);
                string localPath = Path.Combine(downloadDir, Path.GetFileName(data.Filename));

                var downloadInfo = new DownloadInfo
                {
                    Username = data.Username,
                    Filename = data.Filename,
                    LocalPath = localPath,
                    TotalBytes = data.Size
                };
                activeDownloads[key] = downloadInfo;

                var downloadItem = new ListViewItem(new string[]
                {
                    Path.GetFileName(data.Filename),
                    "0%",
                    "0 KB/s",
                    "Descargando..."
                });
                downloadItem.Tag = key;
                downloadsListView.Items.Add(downloadItem);

                await client.DownloadAsync(
                    data.Username,
                    data.Filename,
                    localPath,
                    options: new TransferOptions(
                        progressUpdated: (progress) =>
                        {
                            if (this.InvokeRequired)
                            {
                                this.Invoke(() => UpdateDownloadProgress(key, progress));
                            }
                            else
                            {
                                UpdateDownloadProgress(key, progress);
                            }
                        }
                    )
                );

                UpdateDownloadStatus(key, "Completado");
            }
            catch (Exception ex)
            {
                UpdateDownloadStatus(key, $"Error: {ex.Message}");
            }
        }

        private void UpdateDownloadProgress(string key, TransferProgressUpdatedEventArgs progress)
        {
            if (!activeDownloads.ContainsKey(key)) return;

            var info = activeDownloads[key];
            info.BytesDownloaded = progress.BytesTransferred;

            foreach (ListViewItem item in downloadsListView.Items)
            {
                if (item.Tag?.ToString() == key)
                {
                    double percent = (double)progress.BytesTransferred / progress.Size * 100;
                    item.SubItems[1].Text = $"{percent:0.0}%";
                    item.SubItems[2].Text = $"{FormatFileSize((long)progress.AverageSpeed)}/s";
                    break;
                }
            }
        }

        private void UpdateDownloadStatus(string key, string status)
        {
            foreach (ListViewItem item in downloadsListView.Items)
            {
                if (item.Tag?.ToString() == key)
                {
                    item.SubItems[3].Text = status;
                    break;
                }
            }
        }

        private void ApplyFiltersButton_Click(object? sender, EventArgs e)
        {
            string textFilter = filterTextBox.Text.Trim().ToLower();
            string extFilter = extensionFilterBox.SelectedItem?.ToString() ?? "Todas";
            long minSize = (long)(minSizeBox.Value * 1024 * 1024);
            long maxSize = (long)(maxSizeBox.Value * 1024 * 1024);

            var filteredItems = new List<ListViewItem>();
            foreach (ListViewItem item in resultsListView.Items)
            {
                bool visible = true;

                if (!string.IsNullOrEmpty(textFilter))
                {
                    string filename = item.SubItems[1].Text.ToLower();
                    if (!filename.Contains(textFilter))
                        visible = false;
                }

                if (extFilter != "Todas")
                {
                    string ext = item.SubItems[3].Text;
                    if (!ext.Equals(extFilter, StringComparison.OrdinalIgnoreCase))
                        visible = false;
                }

                if (item.Tag is SearchResultData data)
                {
                    if (minSize > 0 && data.Size < minSize)
                        visible = false;
                    if (maxSize > 0 && data.Size > maxSize)
                        visible = false;
                }

                if (visible)
                    filteredItems.Add(item);
            }

            resultsListView.Items.Clear();
            resultsListView.Items.AddRange(filteredItems.ToArray());
            statusLabel.Text = $"Filtros aplicados - {resultsListView.Items.Count} resultados";
        }

        private void SaveConfigButton_Click(object? sender, EventArgs e)
        {
            username = usernameTextBox.Text.Trim();
            password = passwordTextBox.Text;
            downloadDir = downloadDirTextBox.Text.Trim();
            
            SaveConfig();
            MessageBox.Show("ConfiguraciÃ³n guardada", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void LoadConfig()
        {
            try
            {
                string configFile = "config.json";
                if (System.IO.File.Exists(configFile))
                {
                    var json = System.IO.File.ReadAllText(configFile);
                    var config = JsonSerializer.Deserialize<AppConfig>(json);
                    if (config != null)
                    {
                        username = config.Username;
                        password = config.Password;
                        downloadDir = config.DownloadDir;
                    }
                }
            }
            catch { }
        }

        private void SaveConfig()
        {
            try
            {
                var config = new AppConfig
                {
                    Username = username,
                    Password = password,
                    DownloadDir = downloadDir
                };
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText("config.json", json);
            }
            catch { }
        }

        private void LoadSearchHistory()
        {
            try
            {
                string historyFile = "search_history.json";
                if (System.IO.File.Exists(historyFile))
                {
                    var json = System.IO.File.ReadAllText(historyFile);
                    searchHistory = JsonSerializer.Deserialize<List<string>>(json) ?? new();
                    foreach (var item in searchHistory)
                        searchBox.Items.Add(item);
                }
            }
            catch { }
        }

        private void SaveSearchHistory()
        {
            try
            {
                var json = JsonSerializer.Serialize(searchHistory);
                System.IO.File.WriteAllText("search_history.json", json);
            }
            catch { }
        }

        private void LoadFavorites()
        {
            try
            {
                string favoritesFile = "favorites.json";
                if (System.IO.File.Exists(favoritesFile))
                {
                    var json = System.IO.File.ReadAllText(favoritesFile);
                    favorites = JsonSerializer.Deserialize<List<string>>(json) ?? new();
                    foreach (var item in favorites)
                        favoritesBox.Items.Add(item);
                }
            }
            catch { }
        }

        private void SaveFavorites()
        {
            try
            {
                var json = JsonSerializer.Serialize(favorites);
                System.IO.File.WriteAllText("favorites.json", json);
            }
            catch { }
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


