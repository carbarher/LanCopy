using System;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using Soulseek;
using SlskDown.Services;
using System.Threading.Tasks;

namespace SlskDown
{
    public class BasicMainForm : Form
    {
        private SoulseekClient client;
        private TextBox searchBox;
        private Button searchButton;
        private ListView resultsListView;
        private Label statusLabel;
        private TabControl tabControl;

        // Descargas
        private ListView downloadsListView;
        private Button openFolderButton;

        // EstadÃ­sticas
        private int _searchCount = 0;
        private int _resultCount = 0;
        private int _downloadCount = 0; // placeholder
        private Label statsLabel;

        // Auto-BÃºsqueda
        private ListBox authorsListBox;
        private Button startAutoButton;
        private Button stopAutoButton;
        private TextBox autoLog;
        private bool autoRunning = false;
        private Label autoProgressLabel;
        private ProgressBar autoProgressBar;
        private Button loadAuthorsButton;
        private Button saveAuthorsButton;
        private readonly string authorsListDefaultPath = @"c:\p2p\authors_list.txt";
        private readonly string autoSearchStateFile = @"c:\p2p\SlskDown\auto_search_state.json";
        private int currentAuthorIndex = 0;
        private int currentPass = 0;
        private System.Windows.Forms.Timer reconnectionTimer;
        private bool isReconnectTickRunning = false;

        // Watchlist
        private ListBox watchlistBox;
        private Button addWatchButton;
        private Button removeWatchButton;
        private TextBox watchlistLog;
        private System.Windows.Forms.Timer watchlistTimer;
        private readonly string watchlistPath = @"c:\p2p\watchlist.txt";

        // Blacklist
        private ListBox blacklistBox;
        private Button addBlacklistButton;
        private Button removeBlacklistButton;
        private readonly string blacklistPath = @"c:\p2p\blacklist.json";
        private HashSet<string> blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Filtros
        private NumericUpDown minSizeBox;
        private NumericUpDown maxSizeBox;
        private TextBox extBox;
        private readonly string filtersPath = @"c:\p2p\filters.json";
        private int minKB = 0;
        private int maxKB = 0;
        private HashSet<string> allowedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Config
        private readonly string configPath = @"c:\p2p\config.json";
        private string downloadsFolder = @"c:\p2p\downloads";
        private TextBox downloadsFolderBox;
        private NumericUpDown concurrencyBox;
        private NumericUpDown retriesBox;
        private int maxConcurrency = 2;
        private int maxRetries = 2;
        private Label connStatusLabel;
        private Button testConnButton;

        // Cola de descargas
        private class DownloadRequest
        {
            public string User { get; set; }
            public string File { get; set; }
            public string Dest { get; set; }
            public ListViewItem ViewItem { get; set; }
            public long? SizeBytes { get; set; }
        }
        private readonly ConcurrentQueue<DownloadRequest> downloadQueue = new ConcurrentQueue<DownloadRequest>();
        private SemaphoreSlim concurrencySem;
        private bool downloaderRunning = false;
        
        // ConfigService (configuraciÃ³n completa con cifrado)
        private IConfigService _configService;
        private Services.AppConfig _appConfig;

        public BasicMainForm()
        {
            this.Text = "SlskDown - Limpio y Funcional";
            this.Size = new Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 30, 30);
            
            client = new SoulseekClient();
            _configService = new ConfigService((ISecurityService)new SecurityService());
            _appConfig = _configService.LoadConfig();
            if (!string.IsNullOrWhiteSpace(_appConfig.DownloadDirectory))
                downloadsFolder = _appConfig.DownloadDirectory;
            CreateControls();
            this.Load += async (s, e) =>
            {
                EnsureDataFolder();
                LoadConfig();
                LoadFilters();
                LoadWatchlist();
                LoadBlacklist();
                InitWatchlistTimer();
                LoadPreviousAutoSearchState();
                await AutoConnect();
                UpdateStatsLabel();
                StartDownloaderLoop();
            };
        }

        private async Task DownloadSelectionAsync()
        {
            if (resultsListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("No hay selecciÃ³n", "Descargar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            System.IO.Directory.CreateDirectory(downloadsFolder);
            foreach (ListViewItem it in resultsListView.SelectedItems)
            {
                var user = it.SubItems[0].Text;
                var file = it.SubItems[1].Text;
                var size = it.SubItems[2].Text;
                long? sizeBytes = null;
                try { if (it.Tag is long l) sizeBytes = l; } catch { }

                var dItem = new ListViewItem(file);
                dItem.SubItems.Add(user);
                dItem.SubItems.Add("0%");
                dItem.SubItems.Add("En cola");
                downloadsListView.Items.Add(dItem);
                var safeName = string.Join("_", file.Split(Path.GetInvalidFileNameChars()));
                var dest = Path.Combine(downloadsFolder, safeName);
                downloadQueue.Enqueue(new DownloadRequest { User = user, File = file, Dest = dest, ViewItem = dItem, SizeBytes = sizeBytes });
                dItem.SubItems[3].Text = "En cola";
            }
            await Task.CompletedTask;
        }

        private void InitSemaphore()
        {
            if (concurrencySem != null) concurrencySem.Dispose();
            concurrencySem = new SemaphoreSlim(Math.Max(1, maxConcurrency));
        }

        private void StartDownloaderLoop()
        {
            if (downloaderRunning) return;
            InitSemaphore();
            downloaderRunning = true;
            _ = Task.Run(async () => await RunDownloaderLoopAsync());
        }

        private async Task RunDownloaderLoopAsync()
        {
            while (downloaderRunning)
            {
                if (!downloadQueue.TryDequeue(out var req))
                {
                    await Task.Delay(300);
                    continue;
                }
                await concurrencySem.WaitAsync();
                _ = Task.Run(async () =>
                {
                    try { await ProcessOneDownloadAsync(req); }
                    finally { concurrencySem.Release(); }
                });
            }
        }

        private async Task ProcessOneDownloadAsync(DownloadRequest req)
        {
            int attempt = 0;
            while (true)
            {
                attempt++;
                try
                {
                    req.ViewItem.SubItems[3].Text = attempt == 1 ? "Descargando" : $"Reintentando ({attempt}/{maxRetries})";
                    var options = new Soulseek.TransferOptions(
                        stateChanged: (e) =>
                        {
                            this.Invoke((MethodInvoker)delegate
                            {
                                try
                                {
                                    int percent = 0;
                                    if (req.SizeBytes.HasValue && req.SizeBytes.Value > 0)
                                    {
                                        var done = e.Transfer.BytesTransferred;
                                        percent = (int)Math.Clamp((done * 100.0 / req.SizeBytes.Value), 0, 100);
                                    }
                                    req.ViewItem.SubItems[2].Text = percent + "%";
                                    req.ViewItem.SubItems[3].Text = e.Transfer.State.ToString();
                                }
                                catch { }
                            });
                        },
                        progressUpdated: (p) =>
                        {
                            this.Invoke((MethodInvoker)delegate
                            {
                                try
                                {
                                    if (req.SizeBytes.HasValue && req.SizeBytes.Value > 0)
                                    {
                                        var done = p.Transfer.BytesTransferred;
                                        int percent = (int)Math.Clamp((done * 100.0 / req.SizeBytes.Value), 0, 100);
                                        req.ViewItem.SubItems[2].Text = percent + "%";
                                    }
                                }
                                catch { }
                            });
                        }
                    );

                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(req.Dest) ?? downloadsFolder);
                    await client.DownloadAsync(
                        req.User,
                        req.File,
                        () => Task.FromResult<Stream>(new System.IO.FileStream(req.Dest, System.IO.FileMode.Create)),
                        req.SizeBytes,
                        options: options
                    );

                    req.ViewItem.SubItems[2].Text = "100%";
                    req.ViewItem.SubItems[3].Text = "Completado";
                    Interlocked.Increment(ref _downloadCount);
                    this.Invoke((MethodInvoker)(UpdateStatsLabel));
                    return;
                }
                catch (Exception ex)
                {
                    req.ViewItem.SubItems[3].Text = "Error: " + ex.Message;
                    if (attempt <= maxRetries)
                    {
                        await Task.Delay(1500);
                        continue;
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }
        
        private void CreateControls()
        {
            // TabControl con 7 pestaÃ±as
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Appearance = TabAppearance.Normal,
                ItemSize = new Size(120, 28),
                SizeMode = TabSizeMode.Normal,
                BackColor = Color.FromArgb(40, 40, 40)
            };

            // 1) PestaÃ±a Buscar (colocamos la UI existente)
            var tabBuscar = new TabPage("ðŸ” Buscar") { BackColor = Color.FromArgb(30, 30, 30) };

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
                Text = "ðŸ” Buscar",
                Location = new Point(730, 20),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
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
            resultsListView.Columns.Add("Archivo", 500);
            resultsListView.Columns.Add("TamaÃ±o", 100);

            // MenÃº contextual: Descargar selecciÃ³n
            var ctx = new ContextMenuStrip();
            var miDownload = new ToolStripMenuItem("ðŸ“¥ Descargar selecciÃ³n");
            miDownload.Click += async (s, e) => await DownloadSelectionAsync();
            ctx.Items.Add(miDownload);
            resultsListView.ContextMenuStrip = ctx;

            statusLabel = new Label
            {
                Location = new Point(20, 620),
                Size = new Size(950, 30),
                ForeColor = Color.LimeGreen,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Text = "Iniciando..."
            };

            tabBuscar.Controls.Add(searchBox);
            tabBuscar.Controls.Add(searchButton);
            tabBuscar.Controls.Add(resultsListView);
            tabBuscar.Controls.Add(statusLabel);

            // 2) PestaÃ±a Descargas
            var tabDescargas = new TabPage("ðŸ“¥ Descargas") { BackColor = Color.FromArgb(30, 30, 30) };
            downloadsListView = new ListView
            {
                Location = new Point(20, 20),
                Size = new Size(940, 540),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White
            };
            downloadsListView.Columns.Add("Archivo", 450);
            downloadsListView.Columns.Add("Usuario", 150);
            downloadsListView.Columns.Add("Progreso", 120);
            downloadsListView.Columns.Add("Estado", 150);
            openFolderButton = new Button
            {
                Text = "ðŸ“‚ Abrir carpeta",
                Location = new Point(20, 570),
                Size = new Size(140, 30),
                BackColor = Color.FromArgb(50, 120, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            openFolderButton.Click += (s, e) =>
            {
                try { System.Diagnostics.Process.Start("explorer", downloadsFolder); } catch { }
            };
            tabDescargas.Controls.Add(downloadsListView);
            tabDescargas.Controls.Add(openFolderButton);

            // 3) PestaÃ±a EstadÃ­sticas
            var tabStats = new TabPage("ðŸ“Š EstadÃ­sticas") { BackColor = Color.FromArgb(30, 30, 30) };
            statsLabel = new Label { Location = new Point(20, 20), Size = new Size(920, 200), ForeColor = Color.White, Font = new Font("Consolas", 10) };
            var refreshStats = new Button { Text = "ðŸ”„ Actualizar", Location = new Point(20, 230), Size = new Size(140, 30), BackColor = Color.FromArgb(0,120,215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            refreshStats.Click += (s, e) => UpdateStatsLabel();
            tabStats.Controls.Add(statsLabel);
            tabStats.Controls.Add(refreshStats);

            // 4) PestaÃ±a Auto-BÃºsqueda
            var tabAuto = new TabPage("ðŸ“š Auto-BÃºsqueda") { BackColor = Color.FromArgb(30, 30, 30) };
            authorsListBox = new ListBox { Location = new Point(20, 60), Size = new Size(250, 500), BackColor = Color.FromArgb(45,45,45), ForeColor = Color.White };
            var addAuthor = new Button { Text = "âž• Agregar", Location = new Point(20, 20), Size = new Size(120, 30), BackColor = Color.FromArgb(0,120,215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            var removeAuthor = new Button { Text = "âž– Eliminar", Location = new Point(150, 20), Size = new Size(120, 30), BackColor = Color.FromArgb(200,50,50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            loadAuthorsButton = new Button { Text = "ðŸ“‚ Cargar lista", Location = new Point(20, 570), Size = new Size(120, 28), BackColor = Color.FromArgb(60,60,60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            saveAuthorsButton = new Button { Text = "ðŸ’¾ Guardar lista", Location = new Point(150, 570), Size = new Size(120, 28), BackColor = Color.FromArgb(60,60,60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            addAuthor.Click += (s, e) => {
                var input = Microsoft.VisualBasic.Interaction.InputBox("Autor", "Agregar autor", "");
                if (!string.IsNullOrWhiteSpace(input)) authorsListBox.Items.Add(input.Trim());
            };
            removeAuthor.Click += (s, e) => {
                var items = authorsListBox.SelectedItems.Cast<object>().ToList();
                foreach (var it in items) authorsListBox.Items.Remove(it);
            };
            loadAuthorsButton.Click += (s, e) => LoadAuthorsListFromFile();
            saveAuthorsButton.Click += (s, e) => SaveAuthorsListToFile();
            startAutoButton = new Button { Text = "ðŸš€ Iniciar", Location = new Point(290, 20), Size = new Size(100, 30), BackColor = Color.FromArgb(0,150,0), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            stopAutoButton = new Button { Text = "â¹ï¸ Detener", Location = new Point(400, 20), Size = new Size(100, 30), BackColor = Color.FromArgb(150,0,0), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            autoLog = new TextBox { Location = new Point(290, 60), Size = new Size(670, 500), Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(20,20,20), ForeColor = Color.LightGreen, Font = new Font("Consolas", 9) };
            startAutoButton.Click += async (s, e) => { autoRunning = true; await RunAutoSearchAsync(); };
            stopAutoButton.Click += (s, e) => { autoRunning = false; };
            autoProgressLabel = new Label { Location = new Point(290, 570), Size = new Size(500, 20), ForeColor = Color.White, Text = "Listo" };
            autoProgressBar = new ProgressBar { Location = new Point(290, 595), Size = new Size(670, 20), Minimum = 0, Maximum = 100 };
            tabAuto.Controls.Add(addAuthor);
            tabAuto.Controls.Add(removeAuthor);
            tabAuto.Controls.Add(authorsListBox);
            tabAuto.Controls.Add(startAutoButton);
            tabAuto.Controls.Add(stopAutoButton);
            tabAuto.Controls.Add(autoLog);
            tabAuto.Controls.Add(loadAuthorsButton);
            tabAuto.Controls.Add(saveAuthorsButton);
            tabAuto.Controls.Add(autoProgressLabel);
            tabAuto.Controls.Add(autoProgressBar);

            // 5) PestaÃ±a Watchlist
            var tabWatch = new TabPage("ðŸ‘ï¸ Watchlist") { BackColor = Color.FromArgb(30, 30, 30) };
            watchlistBox = new ListBox { Location = new Point(20, 60), Size = new Size(250, 500), BackColor = Color.FromArgb(45,45,45), ForeColor = Color.White };
            addWatchButton = new Button { Text = "âž• Agregar", Location = new Point(20, 20), Size = new Size(120, 30), BackColor = Color.FromArgb(0,120,215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            removeWatchButton = new Button { Text = "âž– Eliminar", Location = new Point(150, 20), Size = new Size(120, 30), BackColor = Color.FromArgb(200,50,50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            addWatchButton.Click += (s, e) => {
                var input = Microsoft.VisualBasic.Interaction.InputBox("TÃ©rmino", "Agregar a Watchlist", "");
                if (!string.IsNullOrWhiteSpace(input)) { watchlistBox.Items.Add(input.Trim()); SaveWatchlist(); }
            };
            removeWatchButton.Click += (s, e) => {
                var items = watchlistBox.SelectedItems.Cast<object>().ToList();
                foreach (var it in items) watchlistBox.Items.Remove(it);
                SaveWatchlist();
            };
            watchlistLog = new TextBox { Location = new Point(290, 60), Size = new Size(670, 500), Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(20,20,20), ForeColor = Color.Lime, Font = new Font("Consolas", 9), Text = "Esperando ejecutar watchlist...\r\n" };
            tabWatch.Controls.Add(addWatchButton);
            tabWatch.Controls.Add(removeWatchButton);
            tabWatch.Controls.Add(watchlistBox);
            tabWatch.Controls.Add(watchlistLog);

            // 6) PestaÃ±a Blacklist
            var tabBlack = new TabPage("ðŸš« Blacklist") { BackColor = Color.FromArgb(30, 30, 30) };
            blacklistBox = new ListBox { Location = new Point(20, 60), Size = new Size(250, 500), BackColor = Color.FromArgb(45,45,45), ForeColor = Color.White };
            addBlacklistButton = new Button { Text = "âž• Bloquear", Location = new Point(20, 20), Size = new Size(120, 30), BackColor = Color.FromArgb(200,50,50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            removeBlacklistButton = new Button { Text = "âœ… Desbloquear", Location = new Point(150, 20), Size = new Size(120, 30), BackColor = Color.FromArgb(0,150,0), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            addBlacklistButton.Click += (s, e) => {
                var input = Microsoft.VisualBasic.Interaction.InputBox("Usuario", "Bloquear usuario", "");
                if (!string.IsNullOrWhiteSpace(input)) { blacklist.Add(input.Trim()); SaveBlacklist(); RefreshBlacklistUI(); }
            };
            removeBlacklistButton.Click += (s, e) => {
                var items = blacklistBox.SelectedItems.Cast<string>().ToList();
                foreach (var u in items) blacklist.Remove(u);
                SaveBlacklist();
                RefreshBlacklistUI();
            };
            tabBlack.Controls.Add(addBlacklistButton);
            tabBlack.Controls.Add(removeBlacklistButton);
            tabBlack.Controls.Add(blacklistBox);

            // 7) PestaÃ±a Filtros
            var tabFiltros = new TabPage("âš™ï¸ Filtros") { BackColor = Color.FromArgb(30, 30, 30) };
            tabFiltros.Controls.Add(new Label { Text = "TamaÃ±o mÃ­nimo (KB):", ForeColor = Color.White, Location = new Point(20, 20), AutoSize = true });
            minSizeBox = new NumericUpDown { Location = new Point(200, 18), Minimum = 0, Maximum = 10_000_000, Increment = 100, Size = new Size(100, 25) };
            tabFiltros.Controls.Add(minSizeBox);
            tabFiltros.Controls.Add(new Label { Text = "TamaÃ±o mÃ¡ximo (KB):", ForeColor = Color.White, Location = new Point(20, 55), AutoSize = true });
            maxSizeBox = new NumericUpDown { Location = new Point(200, 53), Minimum = 0, Maximum = 10_000_000, Increment = 100, Size = new Size(100, 25) };
            tabFiltros.Controls.Add(maxSizeBox);
            tabFiltros.Controls.Add(new Label { Text = "Extensiones (coma):", ForeColor = Color.White, Location = new Point(20, 90), AutoSize = true });
            extBox = new TextBox { Location = new Point(200, 88), Size = new Size(300, 25), BackColor = Color.FromArgb(45,45,45), ForeColor = Color.White };
            var saveFiltersBtn = new Button { Text = "ðŸ’¾ Guardar filtros", Location = new Point(20, 130), Size = new Size(150, 30), BackColor = Color.FromArgb(0,120,215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            saveFiltersBtn.Click += (s, e) => { SaveFilters(); MessageBox.Show("Filtros guardados", "Filtros"); };
            tabFiltros.Controls.Add(extBox);
            tabFiltros.Controls.Add(saveFiltersBtn);

            // ConfiguraciÃ³n de carpeta de descargas
            tabFiltros.Controls.Add(new Label { Text = "Carpeta de descargas:", ForeColor = Color.White, Location = new Point(20, 180), AutoSize = true });
            downloadsFolderBox = new TextBox { Location = new Point(200, 178), Size = new Size(380, 25), BackColor = Color.FromArgb(45,45,45), ForeColor = Color.White };
            var browseFolderBtn = new Button { Text = "Elegir...", Location = new Point(590, 176), Size = new Size(80, 28), BackColor = Color.FromArgb(60,60,60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            var saveConfigBtn = new Button { Text = "ðŸ’¾ Guardar configuraciÃ³n", Location = new Point(20, 215), Size = new Size(180, 30), BackColor = Color.FromArgb(0,120,215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            browseFolderBtn.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.SelectedPath = downloadsFolderBox.Text;
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                        downloadsFolderBox.Text = dlg.SelectedPath;
                }
            };
            saveConfigBtn.Click += (s, e) => { downloadsFolder = downloadsFolderBox.Text; SaveConfig(); MessageBox.Show("ConfiguraciÃ³n guardada", "Config"); };
            tabFiltros.Controls.Add(downloadsFolderBox);
            tabFiltros.Controls.Add(browseFolderBtn);
            tabFiltros.Controls.Add(saveConfigBtn);

            // Concurrencia y reintentos
            tabFiltros.Controls.Add(new Label { Text = "Concurrencia descargas:", ForeColor = Color.White, Location = new Point(20, 255), AutoSize = true });
            concurrencyBox = new NumericUpDown { Location = new Point(200, 253), Minimum = 1, Maximum = 10, Value = maxConcurrency, Size = new Size(80, 25) };
            tabFiltros.Controls.Add(concurrencyBox);
            tabFiltros.Controls.Add(new Label { Text = "Reintentos:", ForeColor = Color.White, Location = new Point(300, 255), AutoSize = true });
            retriesBox = new NumericUpDown { Location = new Point(380, 253), Minimum = 0, Maximum = 10, Value = maxRetries, Size = new Size(80, 25) };
            tabFiltros.Controls.Add(retriesBox);
            var saveQueueBtn = new Button { Text = "ðŸ’¾ Guardar cola", Location = new Point(480, 250), Size = new Size(120, 30), BackColor = Color.FromArgb(0,120,215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            saveQueueBtn.Click += (s, e) => { maxConcurrency = (int)concurrencyBox.Value; maxRetries = (int)retriesBox.Value; SaveConfig(); InitSemaphore(); MessageBox.Show("Concurrencia y reintentos guardados", "Config"); };
            tabFiltros.Controls.Add(saveQueueBtn);

            // Estado y Test conexiÃ³n
            connStatusLabel = new Label { Text = "Estado: Desconectado", ForeColor = Color.Orange, Location = new Point(20, 295), AutoSize = true };
            testConnButton = new Button { Text = "ðŸ§ª Test conexiÃ³n", Location = new Point(200, 290), Size = new Size(140, 30), BackColor = Color.FromArgb(0,120,215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            testConnButton.Click += async (s, e) => await TestConnectionAsync();
            tabFiltros.Controls.Add(connStatusLabel);
            tabFiltros.Controls.Add(testConnButton);

            tabControl.TabPages.Add(tabBuscar);
            tabControl.TabPages.Add(tabDescargas);
            tabControl.TabPages.Add(tabStats);
            tabControl.TabPages.Add(tabAuto);
            tabControl.TabPages.Add(tabWatch);
            tabControl.TabPages.Add(tabBlack);
            tabControl.TabPages.Add(tabFiltros);

            this.Controls.Clear();
            this.Controls.Add(tabControl);
        }
        
        private async Task AutoConnect()
        {
            try
            {
                statusLabel.Text = "â³ Conectando a Soulseek...";
                statusLabel.ForeColor = Color.Yellow;
                var (u, p) = _configService.GetCredentials();
                await client.ConnectAsync(u, p);
                
                statusLabel.Text = "âœ… Conectado a Soulseek - Listo para buscar";
                statusLabel.ForeColor = Color.LimeGreen;
                if (connStatusLabel != null) { connStatusLabel.Text = "Estado: Conectado"; connStatusLabel.ForeColor = Color.LimeGreen; }
            }
            catch (Exception ex)
            {
                statusLabel.Text = "âŒ Error: " + ex.Message;
                statusLabel.ForeColor = Color.Red;
                if (connStatusLabel != null) { connStatusLabel.Text = "Estado: Error de conexiÃ³n"; connStatusLabel.ForeColor = Color.Red; }
            }
        }
        
        private async Task Search()
        {
            try
            {
                string query = searchBox.Text.Trim();
                if (string.IsNullOrEmpty(query))
                {
                    MessageBox.Show("Por favor ingresa un tÃ©rmino de bÃºsqueda", "BÃºsqueda vacÃ­a", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                resultsListView.Items.Clear();
                statusLabel.Text = "ðŸ” Buscando: " + query + "...";
                statusLabel.ForeColor = Color.Yellow;
                searchButton.Enabled = false;
                
                var results = await client.SearchAsync(SearchQuery.FromText(query));
                
                int count = 0;
                foreach (var response in results.Responses)
                {
                    // omitir usuarios en blacklist
                    if (blacklist.Contains(response.Username)) continue;
                    foreach (var file in response.Files)
                    {
                        if (!PassesFilters(file.Filename, file.Size)) continue;
                        var item = new ListViewItem(response.Username);
                        item.SubItems.Add(file.Filename);
                        item.SubItems.Add(FormatFileSize(file.Size));
                        item.Tag = file.Size;
                        resultsListView.Items.Add(item);
                        count++;
                        
                        // Limitar a 500 resultados para no saturar la UI
                        if (count >= 500) break;
                    }
                    if (count >= 500) break;
                }
                
                statusLabel.Text = $"âœ… {count} resultados encontrados para '{query}'";
                statusLabel.ForeColor = Color.LimeGreen;
                _searchCount++;
                _resultCount += count;
                UpdateStatsLabel();
            }
            catch (Exception ex)
            {
                statusLabel.Text = "âŒ Error: " + ex.Message;
                statusLabel.ForeColor = Color.Red;
                MessageBox.Show("Error al buscar: " + ex.Message, "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                searchButton.Enabled = true;
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

        // Helpers: filtros/blacklist/watchlist/stats
        private void EnsureDataFolder()
        {
            try { System.IO.Directory.CreateDirectory("c:/p2p"); } catch { }
        }

        private void LoadConfig()
        {
            try
            {
                if (System.IO.File.Exists(configPath))
                {
                    var doc = JsonSerializer.Deserialize<Dictionary<string, string>>(System.IO.File.ReadAllText(configPath));
                    if (doc != null && doc.TryGetValue("downloadsFolder", out var path) && !string.IsNullOrWhiteSpace(path))
                        downloadsFolder = path.Trim();
                }
            }
            catch { }
            try { System.IO.Directory.CreateDirectory(downloadsFolder); } catch { }
            if (downloadsFolderBox != null) downloadsFolderBox.Text = downloadsFolder;
        }

        private void SaveConfig()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(downloadsFolder)) downloadsFolder = @"c:\p2p\downloads";
                System.IO.Directory.CreateDirectory(downloadsFolder);
                var doc = new Dictionary<string, string> { { "downloadsFolder", downloadsFolder } };
                System.IO.File.WriteAllText(configPath, JsonSerializer.Serialize(doc));
            }
            catch { }
        }

        private bool PassesFilters(string filename, long sizeBytes)
        {
            try
            {
                long sizeKB = sizeBytes / 1024;
                if (minKB > 0 && sizeKB < minKB) return false;
                if (maxKB > 0 && sizeKB > maxKB) return false;
                if (allowedExts.Count > 0)
                {
                    var ext = Path.GetExtension(filename)?.TrimStart('.').ToLowerInvariant();
                    if (string.IsNullOrEmpty(ext) || !allowedExts.Contains(ext)) return false;
                }
            }
            catch { }
            return true;
        }

        private void LoadFilters()
        {
            try
            {
                if (System.IO.File.Exists(filtersPath))
                {
                    var json = System.IO.File.ReadAllText(filtersPath);
                    var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    if (data != null)
                    {
                        if (data.TryGetValue("minKB", out var a)) minKB = Convert.ToInt32(a);
                        if (data.TryGetValue("maxKB", out var b)) maxKB = Convert.ToInt32(b);
                        if (data.TryGetValue("exts", out var c))
                        {
                            var list = c.ToString() ?? string.Empty;
                            allowedExts = new HashSet<string>(list.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);
                        }
                    }
                }
                if (minSizeBox != null) minSizeBox.Value = Math.Min(minSizeBox.Maximum, Math.Max(minSizeBox.Minimum, minKB));
                if (maxSizeBox != null) maxSizeBox.Value = Math.Min(maxSizeBox.Maximum, Math.Max(maxSizeBox.Minimum, maxKB));
                if (extBox != null) extBox.Text = string.Join(",", allowedExts);
            }
            catch { }
        }

        private void SaveFilters()
        {
            try
            {
                minKB = (int)minSizeBox.Value;
                maxKB = (int)maxSizeBox.Value;
                allowedExts = new HashSet<string>((extBox.Text ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);
                var data = new { minKB, maxKB, exts = string.Join(",", allowedExts) };
                System.IO.File.WriteAllText(filtersPath, JsonSerializer.Serialize(data));
            }
            catch { }
        }

        private void LoadWatchlist()
        {
            try
            {
                if (System.IO.File.Exists(watchlistPath))
                {
                    var lines = System.IO.File.ReadAllLines(watchlistPath);
                    watchlistBox?.Items.Clear();
                    foreach (var l in lines) if (!string.IsNullOrWhiteSpace(l)) watchlistBox?.Items.Add(l.Trim());
                }
            }
            catch { }
        }

        private void SaveWatchlist()
        {
            try
            {
                var lines = watchlistBox.Items.Cast<object>().Select(o => o.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                System.IO.File.WriteAllLines(watchlistPath, lines);
            }
            catch { }
        }

        private void InitWatchlistTimer()
        {
            watchlistTimer = new System.Windows.Forms.Timer();
            watchlistTimer.Interval = 60 * 60 * 1000; // 1 hora
            watchlistTimer.Tick += async (s, e) => await RunWatchlistAsync();
            watchlistTimer.Start();
        }

        private async Task RunWatchlistAsync()
        {
            if (watchlistBox == null || client == null) return;
            watchlistLog.AppendText($"\r\n=== Watchlist {DateTime.Now:HH:mm:ss} ===\r\n");
            foreach (var term in watchlistBox.Items.Cast<object>().Select(o => o.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                try
                {
                    watchlistLog.AppendText($"ðŸ” {term}\r\n");
                    var res = await client.SearchAsync(SearchQuery.FromText(term!));
                    int found = 0;
                    foreach (var resp in res.Responses)
                    {
                        if (blacklist.Contains(resp.Username)) continue;
                        foreach (var f in resp.Files)
                            if (PassesFilters(f.Filename, f.Size)) found++;
                    }
                    watchlistLog.AppendText($"âœ… {found} encontrados\r\n");
                }
                catch (Exception ex)
                {
                    watchlistLog.AppendText($"âŒ Error: {ex.Message}\r\n");
                }
            }
        }

        private async Task RunAutoSearchAsync()
        {
            autoLog.AppendText($"Iniciando auto-bÃºsqueda...\r\n");
            var authors = authorsListBox.Items.Cast<object>().Select(o => o.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (authors.Count == 0)
            {
                autoLog.AppendText("âš ï¸ No hay autores en la lista\r\n");
                return;
            }
            currentAuthorIndex = 0;
            currentPass = 0;
            while (autoRunning)
            {
                for (int i = currentAuthorIndex; i < authors.Count; i++)
                {
                    if (!autoRunning) break;
                    if (client?.State != SoulseekClientStates.Connected)
                    {
                        SaveAutoSearchState(true, i, authors.ToArray(), currentPass);
                        StartReconnectionTimer();
                        autoLog.AppendText("â³ ConexiÃ³n perdida. Guardado estado y esperando reconexiÃ³n...\r\n");
                        autoRunning = false;
                        return;
                    }
                    var author = authors[i]!;
                    try
                    {
                        autoLog.AppendText($"ðŸ” Autor: {author}\r\n");
                        searchBox.Text = author;
                        await Search();
                        currentAuthorIndex = i + 1;
                        int pct = (int)Math.Clamp((currentAuthorIndex * 100.0 / Math.Max(1, authors.Count)), 0, 100);
                        UpdateSearchProgress($"Autor: {author} ({currentAuthorIndex}/{authors.Count})", pct);
                    }
                    catch (Exception ex)
                    {
                        autoLog.AppendText($"âŒ Error: {ex.Message}\r\n");
                    }
                }
                currentPass++;
                currentAuthorIndex = 0;
                SaveAutoSearchState(false, 0, Array.Empty<string>(), currentPass);
                autoLog.AppendText($"âœ… Pase {currentPass} completado\r\n");
                break; // una pasada por ahora
            }
            autoLog.AppendText($"Auto-bÃºsqueda finalizada\r\n");
            autoRunning = false;
        }

        private void LoadBlacklist()
        {
            try
            {
                if (System.IO.File.Exists(blacklistPath))
                {
                    var set = JsonSerializer.Deserialize<HashSet<string>>(System.IO.File.ReadAllText(blacklistPath));
                    if (set != null) blacklist = new HashSet<string>(set, StringComparer.OrdinalIgnoreCase);
                }
                RefreshBlacklistUI();
            }
            catch { }
        }

        private void SaveBlacklist()
        {
            try { System.IO.File.WriteAllText(blacklistPath, JsonSerializer.Serialize(blacklist)); } catch { }
        }

        private void RefreshBlacklistUI()
        {
            if (blacklistBox == null) return;
            blacklistBox.Items.Clear();
            foreach (var u in blacklist.OrderBy(s => s)) blacklistBox.Items.Add(u);
        }

        private void UpdateStatsLabel()
        {
            if (statsLabel == null) return;
            statsLabel.Text = $"BÃºsquedas: {_searchCount}\r\nResultados totales: {_resultCount}\r\nDescargas: {_downloadCount}";
        }

        // ===== Auto-Search: persistencia y reconexiÃ³n =====
        private void LoadPreviousAutoSearchState()
        {
            try
            {
                var state = LoadAutoSearchState();
                if (state.WasRunning)
                {
                    autoLog.AppendText("ðŸ”„ Detectada bÃºsqueda automÃ¡tica interrumpida\r\n");
                    if (client?.State == SoulseekClientStates.Connected)
                    {
                        ResumeAutoSearch();
                    }
                    else
                    {
                        StartReconnectionTimer();
                    }
                }
            }
            catch { }
        }

        private void SaveAutoSearchState(bool wasRunning, int index, string[] authors, int completedPasses)
        {
            try
            {
                var state = new AutoSearchState
                {
                    WasRunning = wasRunning,
                    CurrentIndex = index,
                    LastSearchTime = DateTime.Now,
                    Authors = authors,
                    TotalAuthors = authors?.Length ?? 0,
                    CompletedPasses = completedPasses,
                    MaxPasses = 10,
                    TimeoutSeconds = 60,
                    IsPaused = !wasRunning,
                    PauseReason = wasRunning ? "" : "Detenido"
                };
                state.Save(autoSearchStateFile);
            }
            catch { }
        }

        private AutoSearchState LoadAutoSearchState()
        {
            return AutoSearchState.Load(autoSearchStateFile);
        }

        private void ResumeAutoSearch()
        {
            try
            {
                var state = LoadAutoSearchState();
                if (state.WasRunning && state.Authors.Length > 0)
                {
                    if (authorsListBox.Items.Count == 0)
                    {
                        authorsListBox.Items.AddRange(state.Authors);
                    }
                    autoRunning = true;
                    currentAuthorIndex = state.CurrentIndex;
                    currentPass = state.CompletedPasses;
                    _ = Task.Run(async () => await RunAutoSearchAsync());
                }
            }
            catch { }
        }

        private void StartReconnectionTimer()
        {
            if (reconnectionTimer != null)
            {
                reconnectionTimer.Stop();
                reconnectionTimer.Dispose();
            }
            reconnectionTimer = new System.Windows.Forms.Timer();
            reconnectionTimer.Interval = 5000;
            reconnectionTimer.Tick += async (s, e) =>
            {
                if (isReconnectTickRunning) return;
                isReconnectTickRunning = true;
                try
                {
                    await TryReconnectAndResume();
                }
                finally
                {
                    isReconnectTickRunning = false;
                }
            };
            reconnectionTimer.Start();
        }

        private async Task TryReconnectAndResume()
        {
            try
            {
                if (client?.State == SoulseekClientStates.Connected)
                {
                    reconnectionTimer?.Stop();
                    ResumeAutoSearch();
                }
                else
                {
                    // Intentar reconectar automÃ¡ticamente
                    var (u, p) = _configService.GetCredentials();
                    try
                    {
                        await client.ConnectAsync(u, p);
                        connStatusLabel.Text = "Estado: Conectado";
                        connStatusLabel.ForeColor = Color.LimeGreen;
                        ResumeAutoSearch();
                    }
                    catch (Exception ex)
                    {
                        connStatusLabel.Text = "Estado: Reintentando...";
                        connStatusLabel.ForeColor = Color.Orange;
                    }
                }
            }
            catch { }
        }

        private async Task TestConnectionAsync()
        {
            try
            {
                connStatusLabel.Text = "Estado: Probando...";
                connStatusLabel.ForeColor = Color.Yellow;
                var (u, p) = _configService.GetCredentials();
                if (client?.State != SoulseekClientStates.Connected)
                {
                    await client.ConnectAsync(u, p);
                }
                connStatusLabel.Text = "Estado: Conectado";
                connStatusLabel.ForeColor = Color.LimeGreen;
                MessageBox.Show("ConexiÃ³n OK", "Test conexiÃ³n", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                connStatusLabel.Text = "Estado: Error";
                connStatusLabel.ForeColor = Color.Red;
                MessageBox.Show("Error de conexiÃ³n: " + ex.Message, "Test conexiÃ³n", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateSearchProgress(string status, int progress)
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)(() => UpdateSearchProgress(status, progress)));
                return;
            }
            try
            {
                if (autoProgressLabel != null) autoProgressLabel.Text = status;
                if (autoProgressBar != null) autoProgressBar.Value = Math.Max(autoProgressBar.Minimum, Math.Min(autoProgressBar.Maximum, progress));
            }
            catch { }
        }

        // Autores: cargar/guardar
        private void LoadAuthorsListFromFile()
        {
            try
            {
                using (var ofd = new OpenFileDialog { Filter = "TXT|*.txt|Todos|*.*", FileName = authorsListDefaultPath })
                {
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        var lines = System.IO.File.ReadAllLines(ofd.FileName)
                            .Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
                        authorsListBox.Items.Clear();
                        authorsListBox.Items.AddRange(lines);
                        autoLog.AppendText($"ðŸ“‚ Cargados {lines.Length} autores\r\n");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error cargando lista: " + ex.Message, "Autores");
            }
        }

        private void SaveAuthorsListToFile()
        {
            try
            {
                using (var sfd = new SaveFileDialog { Filter = "TXT|*.txt", FileName = authorsListDefaultPath })
                {
                    if (sfd.ShowDialog(this) == DialogResult.OK)
                    {
                        var lines = authorsListBox.Items.Cast<object>().Select(o => o.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                        System.IO.File.WriteAllLines(sfd.FileName, lines);
                        autoLog.AppendText($"ðŸ’¾ Guardados {lines.Length} autores\r\n");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error guardando lista: " + ex.Message, "Autores");
            }
        }
    }
}

