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
        private NumericUpDown minSizeBox = null!;
        private NumericUpDown maxSizeBox = null!;
        private TextBox extensionBox = null!;
        private TextBox filterTextBox = null!;
        private ComboBox favoritesBox = null!;
        private Button downloadSelectedButton = null!;
        private Button openFolderButton = null!;
        private Label selectedCountLabel = null!;
        private CheckBox incognitoModeCheckBox = null!;
        private NumericUpDown autoDownloadLimit = null!;
        private CheckBox multiSearchCheckBox = null!;
        
        // Variables de estado
        private bool isSearching = false;
        private string downloadDir = @"c:\p2p\downloads";
        private string username = "carbar";
        private string password = "Carlos66*";
        private List<string> searchHistory = new List<string>();
        private List<string> favorites = new List<string>();
        private Dictionary<string, DownloadInfo> activeDownloads = new Dictionary<string, DownloadInfo>();
        
        public MainForm()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("[MainForm] ðŸ—ï¸ CONSTRUCTOR INICIADO");
            Console.WriteLine("========================================");
            
            InitializeComponent();
            SetupUI();
            LoadConfig();
            LoadSearchHistory();
            LoadFavorites();
            
            Console.WriteLine("[MainForm] âœ… Constructor completado");
        }
        
        private void InitializeComponent()
        {
            this.Text = "SlskDown - Soulseek Downloader v3.0";
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(32, 32, 32);
            this.ForeColor = Color.White;
        }
        
        private void SetupUI()
        {
            // Panel superior
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(45, 45, 45)
            };
            this.Controls.Add(topPanel);
            
            // BotÃ³n de conexiÃ³n
            connectButton = new Button
            {
                Text = "ðŸ”Œ Conectar",
                Location = new Point(10, 15),
                Size = new Size(100, 30),
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
                Location = new Point(120, 20),
                Size = new Size(150, 20),
                ForeColor = Color.Red
            };
            topPanel.Controls.Add(connectionStatus);
            
            // Caja de bÃºsqueda
            searchBox = new ComboBox
            {
                Location = new Point(300, 15),
                Size = new Size(400, 30),
                BackColor = Color.FromArgb(64, 64, 64),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f)
            };
            topPanel.Controls.Add(searchBox);
            
            // BotÃ³n de bÃºsqueda
            searchButton = new Button
            {
                Text = "ðŸ” Buscar",
                Location = new Point(710, 15),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(0, 150, 136),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            searchButton.Click += SearchButton_Click;
            topPanel.Controls.Add(searchButton);
            
            // TabControl principal
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(32, 32, 32)
            };
            this.Controls.Add(tabControl);
            
            // PestaÃ±a de resultados
            var resultsTab = new TabPage("ðŸ“Š Resultados");
            tabControl.TabPages.Add(resultsTab);
            
            // Panel de filtros en pestaÃ±a de resultados
            var filterPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(45, 45, 45)
            };
            resultsTab.Controls.Add(filterPanel);
            
            // Filtros
            var lblMinSize = new Label { Text = "TamaÃ±o MÃ­n:", Location = new Point(10, 10), Size = new Size(80, 20) };
            filterPanel.Controls.Add(lblMinSize);
            
            minSizeBox = new NumericUpDown
            {
                Location = new Point(90, 8),
                Size = new Size(80, 20),
                Minimum = 0,
                Maximum = 1000000,
                Value = 0
            };
            filterPanel.Controls.Add(minSizeBox);
            
            var lblMaxSize = new Label { Text = "TamaÃ±o MÃ¡x:", Location = new Point(180, 10), Size = new Size(80, 20) };
            filterPanel.Controls.Add(lblMaxSize);
            
            maxSizeBox = new NumericUpDown
            {
                Location = new Point(260, 8),
                Size = new Size(80, 20),
                Minimum = 0,
                Maximum = 1000000,
                Value = 0
            };
            filterPanel.Controls.Add(maxSizeBox);
            
            var lblExtension = new Label { Text = "ExtensiÃ³n:", Location = new Point(350, 10), Size = new Size(70, 20) };
            filterPanel.Controls.Add(lblExtension);
            
            extensionBox = new TextBox
            {
                Location = new Point(420, 8),
                Size = new Size(60, 20),
                Text = ""
            };
            filterPanel.Controls.Add(extensionBox);
            
            var lblFilter = new Label { Text = "Filtro:", Location = new Point(490, 10), Size = new Size(40, 20) };
            filterPanel.Controls.Add(lblFilter);
            
            filterTextBox = new TextBox
            {
                Location = new Point(530, 8),
                Size = new Size(200, 20)
            };
            filterTextBox.TextChanged += FilterTextBox_TextChanged;
            filterPanel.Controls.Add(filterTextBox);
            
            // Favoritos
            var lblFavorites = new Label { Text = "â­ Favoritos:", Location = new Point(10, 40), Size = new Size(80, 20) };
            filterPanel.Controls.Add(lblFavorites);
            
            favoritesBox = new ComboBox
            {
                Location = new Point(90, 38),
                Size = new Size(200, 20),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            filterPanel.Controls.Add(favoritesBox);
            
            // Modo incÃ³gnito
            incognitoModeCheckBox = new CheckBox
            {
                Text = "ðŸ•µï¸ Modo IncÃ³gnito",
                Location = new Point(300, 40),
                Size = new Size(150, 20),
                ForeColor = Color.White
            };
            filterPanel.Controls.Add(incognitoModeCheckBox);
            
            // BÃºsqueda mÃºltiple
            multiSearchCheckBox = new CheckBox
            {
                Text = "ðŸ”€ BÃºsqueda MÃºltiple (separar por comas)",
                Location = new Point(460, 40),
                Size = new Size(250, 20),
                ForeColor = Color.White
            };
            filterPanel.Controls.Add(multiSearchCheckBox);
            
            // Auto-descarga
            var lblAutoDownload = new Label { Text = "Auto-descargar:", Location = new Point(720, 40), Size = new Size(100, 20) };
            filterPanel.Controls.Add(lblAutoDownload);
            
            autoDownloadLimit = new NumericUpDown
            {
                Location = new Point(820, 38),
                Size = new Size(60, 20),
                Minimum = 1,
                Maximum = 20,
                Value = 5
            };
            filterPanel.Controls.Add(autoDownloadLimit);
            
            // ListView de resultados
            resultsListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = true
            };
            
            resultsListView.Columns.Add("Usuario", 120);
            resultsListView.Columns.Add("PaÃ­s", 60);
            resultsListView.Columns.Add("Archivo", 300);
            resultsListView.Columns.Add("TamaÃ±o", 100);
            resultsListView.Columns.Add("Ext", 50);
            resultsListView.Columns.Add("Bitrate", 80);
            resultsListView.Columns.Add("DuraciÃ³n", 80);
            resultsListView.Columns.Add("Carpeta", 200);
            
            resultsListView.DoubleClick += ResultsListView_DoubleClick;
            resultsListView.SelectedIndexChanged += ResultsListView_SelectedIndexChanged;
            
            resultsTab.Controls.Add(resultsListView);
            
            // Panel de botones inferior
            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = Color.FromArgb(45, 45, 45)
            };
            resultsTab.Controls.Add(bottomPanel);
            
            // Contador de selecciÃ³n
            selectedCountLabel = new Label
            {
                Text = "Seleccionados: 0",
                Location = new Point(10, 10),
                Size = new Size(120, 20)
            };
            bottomPanel.Controls.Add(selectedCountLabel);
            
            // BotÃ³n de descarga
            downloadSelectedButton = new Button
            {
                Text = "â¬‡ï¸ Descargar SelecciÃ³n",
                Location = new Point(140, 5),
                Size = new Size(150, 30),
                BackColor = Color.FromArgb(0, 150, 136),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            downloadSelectedButton.Click += DownloadSelectedButton_Click;
            bottomPanel.Controls.Add(downloadSelectedButton);
            
            // BotÃ³n abrir carpeta
            openFolderButton = new Button
            {
                Text = "ðŸ“ Abrir Carpeta",
                Location = new Point(300, 5),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(139, 69, 19),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            openFolderButton.Click += OpenFolderButton_Click;
            bottomPanel.Controls.Add(openFolderButton);
            
            // Estado
            statusLabel = new Label
            {
                Text = "Listo",
                Location = new Point(430, 10),
                Size = new Size(300, 20)
            };
            bottomPanel.Controls.Add(statusLabel);
            
            // PestaÃ±a de descargas
            var downloadsTab = new TabPage("ðŸ“¥ Descargas");
            tabControl.TabPages.Add(downloadsTab);
            
            downloadsListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                FullRowSelect = true,
                GridLines = true
            };
            
            downloadsListView.Columns.Add("Archivo", 300);
            downloadsListView.Columns.Add("Usuario", 120);
            downloadsListView.Columns.Add("Progreso", 150);
            downloadsListView.Columns.Add("Velocidad", 100);
            downloadsListView.Columns.Add("Estado", 100);
            
            downloadsListView.DoubleClick += DownloadsListView_DoubleClick;
            downloadsTab.Controls.Add(downloadsListView);
            
            // Barra de progreso
            downloadProgress = new ProgressBar
            {
                Dock = DockStyle.Bottom,
                Height = 5,
                Style = ProgressBarStyle.Continuous
            };
            downloadsTab.Controls.Add(downloadProgress);
        }
        
        private async void ConnectButton_Click(object? sender, EventArgs e)
        {
            if (client?.State == SoulseekClientStates.Connected || client?.State == SoulseekClientStates.Connecting)
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
                connectButton.Text = "ðŸ”„ Conectando...";
                connectButton.Enabled = false;
                statusLabel.Text = "Conectando a Soulseek...";
                
                client = new SoulseekClient();
                client.StateChanged += Client_StateChanged;
                
                await client.ConnectAsync(username, password);
                
                statusLabel.Text = "âœ… Conectado a Soulseek";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error de conexiÃ³n: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = $"âŒ Error: {ex.Message}";
                connectButton.Text = "ðŸ”Œ Conectar";
                connectButton.Enabled = true;
            }
        }
        
        private void Client_StateChanged(object? sender, SoulseekClientStateChangedEventArgs e)
        {
            this.Invoke((MethodInvoker)delegate
            {
                switch (e.State)
                {
                    case SoulseekClientStates.Connected:
                        connectionStatus.Text = "ðŸŸ¢ Conectado";
                        connectionStatus.ForeColor = Color.Green;
                        connectButton.Text = "ðŸ”Œ Desconectar";
                        connectButton.Enabled = true;
                        break;
                    case SoulseekClientStates.Disconnected:
                        connectionStatus.Text = "ðŸ”´ Desconectado";
                        connectionStatus.ForeColor = Color.Red;
                        connectButton.Text = "ðŸ”Œ Conectar";
                        connectButton.Enabled = true;
                        break;
                    case SoulseekClientStates.Connecting:
                        connectionStatus.Text = "ðŸŸ¡ Conectando...";
                        connectionStatus.ForeColor = Color.Yellow;
                        break;
                }
            });
        }
        
        private async void SearchButton_Click(object? sender, EventArgs e)
        {
            if (client?.State != SoulseekClientStates.Connected)
            {
                MessageBox.Show("Debes estar conectado para buscar", "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            if (isSearching)
            {
                isSearching = false;
                searchButton.Text = "ðŸ” Buscar";
                searchButton.BackColor = Color.FromArgb(0, 150, 136);
                statusLabel.Text = "BÃºsqueda cancelada";
                return;
            }
            
            var searchText = searchBox.Text.Trim();
            if (string.IsNullOrEmpty(searchText))
                return;
            
            isSearching = true;
            searchButton.Text = "â¹ï¸ Detener";
            searchButton.BackColor = Color.FromArgb(220, 53, 69);
            
            resultsListView.Items.Clear();
            statusLabel.Text = $"ðŸ” Buscando: {searchText}...";
            
            if (!incognitoModeCheckBox.Checked)
            {
                AddToSearchHistory(searchText);
            }
            
            try
            {
                if (multiSearchCheckBox.Checked && searchText.Contains(','))
                {
                    // BÃºsqueda mÃºltiple
                    var terms = searchText.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToArray();
                    await MultipleSearchAsync(terms);
                }
                else
                {
                    // BÃºsqueda simple
                    var results = await client.SearchAsync(SearchText.FromQuery(searchText), options: new SearchOptions
                    {
                        FilterResponses = true,
                        ResponseLimit = 100,
                        FileFilter = file => !string.IsNullOrEmpty(file.Filename)
                    });
                    
                    await ProcessSearchResults(results, searchText);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error en bÃºsqueda: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = $"âŒ Error: {ex.Message}";
            }
            finally
            {
                isSearching = false;
                searchButton.Text = "ðŸ” Buscar";
                searchButton.BackColor = Color.FromArgb(0, 150, 136);
            }
        }
        
        private async Task MultipleSearchAsync(string[] terms)
        {
            var tasks = terms.Take(3).Select(term => 
                client.SearchAsync(SearchText.FromQuery(term), options: new SearchOptions
                {
                    FilterResponses = true,
                    ResponseLimit = 50,
                    FileFilter = file => !string.IsNullOrEmpty(file.Filename)
                }).ContinueWith(async t => await ProcessSearchResults(t.Result, term))
            ).ToArray();
            
            await Task.WhenAll(tasks);
            statusLabel.Text = $"âœ… BÃºsqueda mÃºltiple completada: {string.Join(", ", terms)}";
        }
        
        private async Task ProcessSearchResults(SearchResponse results, string searchTerm)
        {
            var filteredResults = results.Files.Where(file =>
            {
                var sizeMB = file.Size / (1024.0 * 1024.0);
                if (minSizeBox.Value > 0 && sizeMB < minSizeBox.Value) return false;
                if (maxSizeBox.Value > 0 && sizeMB > maxSizeBox.Value) return false;
                if (!string.IsNullOrEmpty(extensionBox.Text) && !file.Filename.EndsWith(extensionBox.Text, StringComparison.OrdinalIgnoreCase)) return false;
                if (!string.IsNullOrEmpty(filterTextBox.Text) && !file.Filename.Contains(filterTextBox.Text, StringComparison.OrdinalIgnoreCase)) return false;
                return true;
            }).OrderByDescending(f => f.Size).Take(1000).ToList();
            
            this.Invoke((MethodInvoker)delegate
            {
                foreach (var file in filteredResults)
                {
                    var item = new ListViewItem(new string[]
                    {
                        results.Username,
                        "??", // PaÃ­s - no disponible en respuesta bÃ¡sica
                        Path.GetFileName(file.Filename),
                        FormatFileSize(file.Size),
                        Path.GetExtension(file.Filename).TrimStart('.'),
                        file.BitRate.HasValue ? $"{file.BitRate}kbps" : "",
                        file.Duration.HasValue ? $"{file.Duration:mm\\:ss}" : "",
                        Path.GetDirectoryName(file.Filename) ?? ""
                    });
                    item.Tag = new SearchResultData
                    {
                        Username = results.Username,
                        Filename = file.Filename,
                        Size = file.Size,
                        File = file,
                        Response = results
                    };
                    resultsListView.Items.Add(item);
                }
                
                statusLabel.Text = $"âœ… {filteredResults.Count} resultados para: {searchTerm}";
                
                // Auto-descarga si estÃ¡ habilitada
                if (autoDownloadLimit.Value > 0 && filteredResults.Count > 0)
                {
                    AutoDownloadTopFiles(filteredResults, (int)autoDownloadLimit.Value);
                }
            });
        }
        
        private void AutoDownloadTopFiles(IEnumerable<Soulseek.File> files, int count)
        {
            var topFiles = files.Take(count).ToList();
            statusLabel.Text = $"â¬‡ï¸ Auto-descargando {topFiles.Count} archivos...";
            
            foreach (var file in topFiles)
            {
                var downloadPath = Path.Combine(downloadDir, Path.GetFileName(file.Filename));
                StartDownload(file, downloadPath);
            }
        }
        
        private void FilterTextBox_TextChanged(object? sender, EventArgs e)
        {
            var filter = filterTextBox.Text.ToLower();
            foreach (ListViewItem item in resultsListView.Items)
            {
                item.Visible = string.IsNullOrEmpty(filter) || 
                              item.SubItems[2].Text.ToLower().Contains(filter);
            }
        }
        
        private void ResultsListView_SelectedIndexChanged(object? sender, EventArgs e)
        {
            selectedCountLabel.Text = $"Seleccionados: {resultsListView.SelectedItems.Count}";
        }
        
        private void ResultsListView_DoubleClick(object? sender, EventArgs e)
        {
            if (resultsListView.SelectedItems.Count > 0)
            {
                var item = resultsListView.SelectedItems[0];
                var data = (SearchResultData)item.Tag!;
                
                var dialog = new SaveFileDialog
                {
                    FileName = Path.GetFileName(data.Filename),
                    InitialDirectory = downloadDir,
                    Filter = "Todos los archivos|*.*"
                };
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    StartDownload(data.File, dialog.FileName);
                }
            }
        }
        
        private void DownloadSelectedButton_Click(object? sender, EventArgs e)
        {
            if (resultsListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("Selecciona al menos un archivo para descargar", "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            var dialog = new FolderBrowserDialog
            {
                SelectedPath = downloadDir,
                Description = "Selecciona carpeta para descargas"
            };
            
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                downloadDir = dialog.SelectedPath;
                SaveConfig();
                
                foreach (ListViewItem item in resultsListView.SelectedItems)
                {
                    var data = (SearchResultData)item.Tag!;
                    var downloadPath = Path.Combine(downloadDir, Path.GetFileName(data.Filename));
                    StartDownload(data.File, downloadPath);
                }
            }
        }
        
        private async void StartDownload(Soulseek.File file, string localPath)
        {
            try
            {
                if (client?.State != SoulseekClientStates.Connected)
                {
                    MessageBox.Show("No estÃ¡s conectado", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                // Crear directorio si no existe
                Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                
                // Agregar a ListView de descargas
                var downloadItem = new ListViewItem(new string[]
                {
                    Path.GetFileName(localPath),
                    "Descargando...",
                    "0%",
                    "0 KB/s",
                    "Iniciando"
                });
                downloadsListView.Items.Add(downloadItem);
                
                // Iniciar descarga
                var download = await client.DownloadAsync(file.Username, file.Filename, localPath, 
                    new TransferOptions(stateChanged: (e) =>
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        var percent = (int)(e.PercentComplete);
                        downloadItem.SubItems[2].Text = $"{percent}%";
                        downloadItem.SubItems[4].Text = e.State.ToString();
                    });
                },
                progressUpdated: (e) =>
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        var speed = e.BytesTransferred / (1024.0 * 1024.0) / e.ElapsedTime.TotalSeconds;
                        downloadItem.SubItems[3].Text = $"{speed:F1} MB/s";
                        downloadItem.SubItems[2].Text = $"{(int)e.PercentComplete}%";
                    });
                }));
                
                downloadItem.SubItems[1].Text = file.Username;
                downloadItem.SubItems[4].Text = "âœ… Completado";
                downloadItem.Tag = localPath;
                
                statusLabel.Text = $"âœ… Descargado: {Path.GetFileName(localPath)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error descargando {file.Filename}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void OpenFolderButton_Click(object? sender, EventArgs e)
        {
            try
            {
                Directory.CreateDirectory(downloadDir);
                System.Diagnostics.Process.Start("explorer.exe", downloadDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo carpeta: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void DownloadsListView_DoubleClick(object? sender, EventArgs e)
        {
            if (downloadsListView.SelectedItems.Count > 0)
            {
                var item = downloadsListView.SelectedItems[0];
                if (item.Tag is string filePath && File.Exists(filePath))
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", filePath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error abriendo archivo: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
        
        private void LoadConfig()
        {
            try
            {
                var configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (File.Exists(configFile))
                {
                    var json = File.ReadAllText(configFile);
                    var config = JsonSerializer.Deserialize<AppConfig>(json);
                    if (config != null)
                    {
                        username = config.Username;
                        password = config.Password;
                        downloadDir = config.DownloadDir;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cargando config: {ex.Message}");
            }
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
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json"), json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error guardando config: {ex.Message}");
            }
        }
        
        private void LoadSearchHistory()
        {
            try
            {
                var historyFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "search_history.json");
                if (File.Exists(historyFile))
                {
                    var json = File.ReadAllText(historyFile);
                    searchHistory = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                    searchBox.Items.AddRange(searchHistory.ToArray());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cargando historial: {ex.Message}");
            }
        }
        
        private void SaveSearchHistory()
        {
            try
            {
                var json = JsonSerializer.Serialize(searchHistory, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "search_history.json"), json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error guardando historial: {ex.Message}");
            }
        }
        
        private void AddToSearchHistory(string term)
        {
            if (!searchHistory.Contains(term))
            {
                searchHistory.Insert(0, term);
                if (searchHistory.Count > 50)
                    searchHistory.RemoveAt(searchHistory.Count - 1);
                
                SaveSearchHistory();
                
                searchBox.Items.Clear();
                searchBox.Items.AddRange(searchHistory.ToArray());
            }
        }
        
        private void LoadFavorites()
        {
            try
            {
                var favoritesFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "favorites.json");
                if (File.Exists(favoritesFile))
                {
                    var json = File.ReadAllText(favoritesFile);
                    favorites = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                    favoritesBox.Items.AddRange(favorites.ToArray());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cargando favoritos: {ex.Message}");
            }
        }
        
        private void SaveFavorites()
        {
            try
            {
                var json = JsonSerializer.Serialize(favorites, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "favorites.json"), json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error guardando favoritos: {ex.Message}");
            }
        }
        
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
        
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            SaveConfig();
            SaveSearchHistory();
            base.OnFormClosed(e);
        }
    }
    
    public class SearchResultData
    {
        public string Username { get; set; } = "";
        public string Filename { get; set; } = "";
        public long Size { get; set; }
        public Soulseek.File File { get; set; } = null!;
        public SearchResponse Response { get; set; } = null!;
    }
    
    public class AppConfig
    {
        public string Username { get; set; } = "carbar";
        public string Password { get; set; } = "Carlos66*";
        public string DownloadDir { get; set; } = @"c:\p2p\downloads";
    }
    
    public class DownloadInfo
    {
        public string Username { get; set; } = "";
        public string Filename { get; set; } = "";
        public string LocalPath { get; set; } = "";
        public long TotalBytes { get; set; }
        public long BytesDownloaded { get; set; }
    }
}

