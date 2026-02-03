using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;
using SlskDown.UI;

namespace SlskDown
{
    public partial class MainForm
    {
        private TextBox txtConfigSearch;
        private List<SectionPanel> allConfigPanels = new List<SectionPanel>();
        private Dictionary<string, List<Control>> panelSearchableControls = new Dictionary<string, List<Control>>();
        /// <summary>
        /// Crea el tab de Configuración con secciones fijas - todo siempre visible
        /// </summary>
        private void CreateConfigTab(Panel parent)
        {
            parent.BackColor = Color.FromArgb(30, 30, 30);
            
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(15),
                BackColor = Color.FromArgb(30, 30, 30),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Header con título, búsqueda y presets
            var headerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            
            var lblTitle = new Label 
            { 
                Text = "⚙️ Configuración", 
                AutoSize = true,
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                Location = new Point(5, 10)
            };
            headerPanel.Controls.Add(lblTitle);
            
            // Barra de búsqueda
            var searchPanel = new FlowLayoutPanel
            {
                Location = new Point(5, 50),
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent
            };
            
            var lblSearch = new Label
            {
                Text = "🔍",
                Font = new Font("Segoe UI", 14),
                ForeColor = Color.LightGray,
                AutoSize = true,
                Margin = new Padding(0, 5, 5, 0)
            };
            searchPanel.Controls.Add(lblSearch);
            
            txtConfigSearch = new TextBox
            {
                Width = 300,
                Height = 28,
                BackColor = Color.FromArgb(50, 50, 50),
                Font = new Font("Segoe UI", 10),
                Text = "Buscar configuración...",
                ForeColor = Color.Gray
            };
            AddTooltip(txtConfigSearch, "Busca opciones de configuración escribiendo palabras clave (usuario, descarga, timeout, etc.)");
            
            txtConfigSearch.GotFocus += (s, e) =>
            {
                if (txtConfigSearch.Text == "Buscar configuración...")
                {
                    txtConfigSearch.Text = "";
                    txtConfigSearch.ForeColor = Color.White;
                }
            };
            
            txtConfigSearch.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtConfigSearch.Text))
                {
                    txtConfigSearch.Text = "Buscar configuración...";
                    txtConfigSearch.ForeColor = Color.Gray;
                }
            };
            
            searchPanel.Controls.Add(txtConfigSearch);
            headerPanel.Controls.Add(searchPanel);
            
            // Botones de presets
            var presetsPanel = new FlowLayoutPanel
            {
                Location = new Point(5, 90),
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent
            };
            
            var lblPresets = new Label
            {
                Text = "Presets:",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.LightGray,
                AutoSize = true,
                Margin = new Padding(0, 8, 10, 0)
            };
            presetsPanel.Controls.Add(lblPresets);
            
            var btnConservador = CreateStyledButton("🐢 Conservador", Color.FromArgb(100, 100, 100), 140, 35);
            btnConservador.Margin = new Padding(0, 0, 8, 0);
            AddTooltip(btnConservador, "Configuración conservadora: pocas descargas simultáneas, timeouts largos, bajo uso de recursos");
            btnConservador.Click += (s, e) => ApplyPreset("conservador");
            presetsPanel.Controls.Add(btnConservador);
            
            var btnBalanceado = CreateStyledButton("⚖️ Balanceado", Color.FromArgb(0, 120, 215), 140, 35);
            btnBalanceado.Margin = new Padding(0, 0, 8, 0);
            AddTooltip(btnBalanceado, "Configuración balanceada: equilibrio entre velocidad y estabilidad, recomendado para la mayoría");
            btnBalanceado.Click += (s, e) => ApplyPreset("balanceado");
            presetsPanel.Controls.Add(btnBalanceado);
            
            var btnAgresivo = CreateStyledButton("🚀 Agresivo", Color.FromArgb(200, 100, 0), 140, 35);
            btnAgresivo.Margin = new Padding(0, 0, 8, 0);
            AddTooltip(btnAgresivo, "Configuración agresiva: máximas descargas simultáneas, timeouts cortos, máxima velocidad pero más uso de recursos");
            btnAgresivo.Click += (s, e) => ApplyPreset("agresivo");
            presetsPanel.Controls.Add(btnAgresivo);
            
            headerPanel.Controls.Add(presetsPanel);
            mainLayout.Controls.Add(headerPanel, 0, 0);

            // Usar SectionContainer - layout vertical simple con scroll
            var sectionContainer = new SectionContainer
            {
                Dock = DockStyle.Fill
            };
            mainLayout.Controls.Add(sectionContainer, 0, 1);
            parent.Controls.Add(mainLayout);
            
            // Configurar búsqueda en tiempo real
            txtConfigSearch.TextChanged += (s, e) => FilterConfigPanels();
            
            // === SECCIÓN 1: CUENTA ===
            var cuentaPanel = new SectionPanel("🔐 CUENTA", headerColor: Color.FromArgb(45, 45, 45));
            CreateCuentaSection(cuentaPanel);
            sectionContainer.AddSection(cuentaPanel);
            allConfigPanels.Add(cuentaPanel);
            panelSearchableControls["cuenta"] = new List<Control>();
            
            // === SECCIÓN 2: OPCIONES GENERALES ===
            var opcionesPanel = new SectionPanel("⚡ OPCIONES GENERALES", headerColor: Color.FromArgb(40, 40, 40));
            CreateOpcionesSection(opcionesPanel);
            sectionContainer.AddSection(opcionesPanel);
            allConfigPanels.Add(opcionesPanel);
            panelSearchableControls["opciones"] = new List<Control>();
            
            // === SECCIÓN 3: DESCARGAS ===
            var descargasPanel = new SectionPanel("📥 DESCARGAS", headerColor: Color.FromArgb(40, 40, 40));
            CreateDescargasSection(descargasPanel);
            sectionContainer.AddSection(descargasPanel);
            allConfigPanels.Add(descargasPanel);
            panelSearchableControls["descargas"] = new List<Control>();
            
            // === SECCIÓN 4: RED Y BÚSQUEDA ===
            var redPanel = new SectionPanel("🌐 RED Y BÚSQUEDA", headerColor: Color.FromArgb(40, 40, 40));
            CreateRedSection(redPanel);
            sectionContainer.AddSection(redPanel);
            allConfigPanels.Add(redPanel);
            panelSearchableControls["red"] = new List<Control>();
            
            // === SECCIÓN 5: NICOTINE+ ===
            var nicotinePanel = new SectionPanel("🚀 MEJORAS NICOTINE+", headerColor: Color.FromArgb(40, 40, 40));
            CreateNicotineSection(nicotinePanel);
            sectionContainer.AddSection(nicotinePanel);
            allConfigPanels.Add(nicotinePanel);
            panelSearchableControls["nicotine"] = new List<Control>();
            
            // === SECCIÓN 6: INTERFAZ ===
            var interfazPanel = new SectionPanel("🎨 INTERFAZ", headerColor: Color.FromArgb(40, 40, 40));
            CreateInterfazSection(interfazPanel);
            sectionContainer.AddSection(interfazPanel);
            allConfigPanels.Add(interfazPanel);
            panelSearchableControls["interfaz"] = new List<Control>();
            
            // === SECCIÓN 7: INTELIGENCIA ARTIFICIAL ===
            var aiPanel = new SectionPanel("🤖 INTELIGENCIA ARTIFICIAL", headerColor: Color.FromArgb(40, 40, 40));
            CreateAISection(aiPanel);
            sectionContainer.AddSection(aiPanel);
            allConfigPanels.Add(aiPanel);
            panelSearchableControls["ai"] = new List<Control>();
        }
        
        private void CreateCuentaSection(SectionPanel panel)
        {
            // Usuario
            var userRow = VisualFeedbackHelper.CreateConfigRow(
                "Usuario Soulseek:",
                txtUsername = new TextBox 
                { 
                    Size = new Size(250, 32), 
                    BackColor = Color.FromArgb(50, 50, 50), 
                    ForeColor = Color.White, 
                    Font = new Font("Segoe UI", 10), 
                    Text = username 
                }
            );
            txtUsername.TextChanged += (s, e) => { username = txtUsername.Text; SaveConfig(); };
            panel.AddContent(userRow);
            
            // Contraseña
            var passRow = VisualFeedbackHelper.CreateConfigRow(
                "Contraseña:",
                txtPassword = new TextBox 
                { 
                    Size = new Size(250, 32), 
                    BackColor = Color.FromArgb(50, 50, 50), 
                    ForeColor = Color.White, 
                    Font = new Font("Segoe UI", 10), 
                    Text = password, 
                    UseSystemPasswordChar = true 
                }
            );
            txtPassword.TextChanged += (s, e) => { password = txtPassword.Text; SaveConfig(); };
            panel.AddContent(passRow);
            
            // Carpeta de descargas
            var dirRow = new FlowLayoutPanel 
            { 
                FlowDirection = FlowDirection.LeftToRight, 
                AutoSize = true, 
                WrapContents = false, 
                BackColor = Color.Transparent, 
                Margin = new Padding(0, 8, 0, 8) 
            };
            
            var lblDir = new Label 
            { 
                Text = "Descargas:", 
                Size = new Size(150, 36), 
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 10, FontStyle.Bold), 
                TextAlign = ContentAlignment.MiddleLeft 
            };
            
            txtDownloadDir = new TextBox 
            { 
                Size = new Size(250, 32), 
                BackColor = Color.FromArgb(50, 50, 50), 
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 10), 
                Text = downloadDir, 
                ReadOnly = true
            };
            AddTooltip(txtDownloadDir, "Carpeta donde se guardarán todos los archivos descargados. Haz clic en el botón para cambiarla");
            txtDownloadDir.TextChanged += (s, e) => { downloadDir = txtDownloadDir.Text; SaveConfig(); };
            
            var btnBrowse = CreateStyledButton("...", Color.FromArgb(60, 60, 60), 60, 50);
            btnBrowse.Click += (s, e) => 
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.SelectedPath = downloadDir;
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        txtDownloadDir.Text = dialog.SelectedPath;
                    }
                }
            };
            
            var btnOpenFolder = CreateStyledButton("ABRIR", Color.FromArgb(0, 120, 215), 120, 45);
            btnOpenFolder.Click += (s, e) => 
            {
                try
                {
                    if (System.IO.Directory.Exists(downloadDir))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", downloadDir);
                    }
                    else
                    {
                        MessageBox.Show($"La carpeta no existe:\n{downloadDir}", "Carpeta no encontrada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al abrir carpeta:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            dirRow.Controls.Add(lblDir);
            dirRow.Controls.Add(txtDownloadDir);
            dirRow.Controls.Add(btnBrowse);
            dirRow.Controls.Add(btnOpenFolder);
            panel.AddContent(dirRow);
        }
        
        private void CreateOpcionesSection(SectionPanel panel)
        {
            // Usar grid 2 columnas para checkboxes
            chkAutoConnect = new CheckBox 
            { 
                Text = "Auto-conectar al iniciar", 
                AutoSize = true, 
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 10),
                Checked = autoConnect
            };
            AddTooltip(chkAutoConnect, "Conecta automáticamente a Soulseek al iniciar la aplicación sin necesidad de hacer clic en el botón");
            chkAutoConnect.CheckedChanged += (s, e) => { autoConnect = chkAutoConnect.Checked; SaveConfig(); };
            
            chkOrganizeByAuthor = new CheckBox 
            { 
                Text = "Organizar por autor", 
                AutoSize = true, 
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 10),
                Checked = organizeByAuthor
            };
            AddTooltip(chkOrganizeByAuthor, "Crea subcarpetas automáticamente por autor al descargar archivos para mantener la biblioteca organizada");
            chkOrganizeByAuthor.CheckedChanged += (s, e) => { organizeByAuthor = chkOrganizeByAuthor.Checked; SaveConfig(); };
            
            chkAutoBackup = new CheckBox 
            { 
                Text = "Backup automático", 
                AutoSize = true, 
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 10),
                Checked = autoBackup
            };
            AddTooltip(chkAutoBackup, "Crea copias de seguridad automáticas de la configuración y listas de autores periódicamente");
            chkAutoBackup.CheckedChanged += (s, e) => { autoBackup = chkAutoBackup.Checked; SaveConfig(); };
            
            chkShowExactSizes = new CheckBox 
            { 
                Text = "Tamaños exactos en bytes", 
                AutoSize = true, 
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 10),
                Checked = showExactSizes
            };
            AddTooltip(chkShowExactSizes, "Muestra los tamaños de archivo en bytes exactos en lugar de formato legible (KB, MB, GB)");
            chkShowExactSizes.CheckedChanged += (s, e) => 
            { 
                showExactSizes = chkShowExactSizes.Checked; 
                SaveConfig(); 
                UpdateSearchResults(allResults);
            };
            
            var grid = VisualFeedbackHelper.CreateTwoColumnCheckboxGrid(
                chkAutoConnect, 
                chkOrganizeByAuthor, 
                chkAutoBackup, 
                chkShowExactSizes
            );
            
            panel.AddContent(grid);
        }
        
        private void CreateDescargasSection(SectionPanel panel)
        {
            // Modo Turbo con feedback visual
            chkTurboMode = new CheckBox 
            { 
                Text = "🚀 Modo Turbo", 
                AutoSize = true, 
                ForeColor = Color.Orange, 
                Font = new Font("Segoe UI", 11, FontStyle.Bold), 
                Checked = maxParallelDownloads > 3,
                Margin = new Padding(0, 5, 0, 10)
            };
            
            chkTurboMode.CheckedChanged += (s, e) => 
            { 
                if (chkTurboMode.Checked)
                {
                    maxParallelDownloads = 8;
                    maxSimultaneousDownloads = 8;
                    maxParallelSearches = 12;
                    searchTimeout = 20;
                    if (numParallelDownloads != null) numParallelDownloads.Value = 8;
                    
                    // Mostrar badges con valores afectados
                    VisualFeedbackHelper.ShowRelatedValuesBadges(
                        chkTurboMode,
                        ("Descargas: 8", Color.FromArgb(0, 150, 136)),
                        ("Búsquedas: 12", Color.FromArgb(255, 152, 0)),
                        ("Timeout: 20s", Color.FromArgb(63, 81, 181))
                    );
                    
                    Log("Modo Turbo ACTIVADO: 8 descargas + 12 búsquedas + timeout 20s");
                }
                else
                {
                    maxParallelDownloads = 3;
                    maxSimultaneousDownloads = 3;
                    maxParallelSearches = 3;
                    searchTimeout = 30;
                    if (numParallelDownloads != null) numParallelDownloads.Value = 3;
                    
                    Log("Modo Normal: 3 descargas + 3 búsquedas + timeout 30s");
                }
                
                if (numParallelDownloads != null)
                {
                    numParallelDownloads.Enabled = chkTurboMode.Checked;
                }
                
                SaveConfig(); 
            };
            
            panel.AddContent(chkTurboMode);
            
            // Descargas simultáneas (indentado)
            var parallelRow = VisualFeedbackHelper.CreateConfigRow(
                "  Descargas simultáneas:",
                numParallelDownloads = new NumericUpDown 
                { 
                    Size = new Size(60, 25), 
                    BackColor = Color.FromArgb(60, 60, 60), 
                    ForeColor = Color.White, 
                    Minimum = 1, 
                    Maximum = 10, 
                    Value = maxParallelDownloads, 
                    Enabled = chkTurboMode.Checked 
                },
                labelWidth: 200
            );
            numParallelDownloads.ValueChanged += (s, e) => 
            { 
                maxParallelDownloads = (int)numParallelDownloads.Value; 
                maxSimultaneousDownloads = (int)numParallelDownloads.Value;
                SaveConfig(); 
            };
            panel.AddContent(parallelRow);
            
            // Reintentos
            var retriesRow = VisualFeedbackHelper.CreateConfigRow(
                "Reintentos automáticos:",
                numMaxRetries = new NumericUpDown 
                { 
                    Size = new Size(60, 25), 
                    BackColor = Color.FromArgb(60, 60, 60), 
                    ForeColor = Color.White, 
                    Minimum = 0, 
                    Maximum = 10, 
                    Value = maxRetries 
                },
                "(0 = sin reintentos)"
            );
            numMaxRetries.ValueChanged += (s, e) => { maxRetries = (int)numMaxRetries.Value; SaveConfig(); };
            panel.AddContent(retriesRow);
            
            // Proveedores alternativos
            var altRow = VisualFeedbackHelper.CreateConfigRow(
                "Proveedores alternativos:",
                new NumericUpDown 
                { 
                    Size = new Size(60, 25), 
                    BackColor = Color.FromArgb(60, 60, 60), 
                    ForeColor = Color.White, 
                    Minimum = 0, 
                    Maximum = 10, 
                    Value = maxAlternativeRetries 
                },
                $"(máx {MAX_TOTAL_ATTEMPTS} intentos)"
            );
            ((NumericUpDown)altRow.Controls[1]).ValueChanged += (s, e) => 
            { 
                maxAlternativeRetries = (int)((NumericUpDown)altRow.Controls[1]).Value; 
                SaveConfig(); 
            };
            panel.AddContent(altRow);
            
            // Tamaño mínimo
            var minSizeRow = VisualFeedbackHelper.CreateConfigRow(
                "Tamaño mínimo (KB):",
                new NumericUpDown 
                { 
                    Size = new Size(80, 25), 
                    BackColor = Color.FromArgb(60, 60, 60), 
                    ForeColor = Color.White, 
                    Minimum = 0, 
                    Maximum = 1048576, 
                    Value = minFileSizeKB 
                },
                "(0 = sin límite)"
            );
            ((NumericUpDown)minSizeRow.Controls[1]).ValueChanged += (s, e) => 
            { 
                minFileSizeKB = (int)((NumericUpDown)minSizeRow.Controls[1]).Value; 
                SaveConfig(); 
            };
            panel.AddContent(minSizeRow);
        }
        
        private void CreateRedSection(SectionPanel panel)
        {
            // Timeout
            var timeoutRow = VisualFeedbackHelper.CreateConfigRow(
                "Timeout (seg):",
                new NumericUpDown 
                { 
                    Size = new Size(80, 25), 
                    BackColor = Color.FromArgb(60, 60, 60), 
                    ForeColor = Color.White, 
                    Minimum = 0, 
                    Maximum = 999999, 
                    Value = searchTimeout 
                },
                "(0 = sin límite)"
            );
            ((NumericUpDown)timeoutRow.Controls[1]).ValueChanged += (s, e) => 
            { 
                searchTimeout = (int)((NumericUpDown)timeoutRow.Controls[1]).Value; 
                SaveConfig(); 
            };
            panel.AddContent(timeoutRow);
            
            // Límite de respuestas
            var responseRow = VisualFeedbackHelper.CreateConfigRow(
                "Respuestas:",
                new NumericUpDown 
                { 
                    Size = new Size(80, 25), 
                    BackColor = Color.FromArgb(60, 60, 60), 
                    ForeColor = Color.White, 
                    Minimum = 0, 
                    Maximum = 999999, 
                    Value = responseLimit 
                },
                "(0 = sin límite)"
            );
            ((NumericUpDown)responseRow.Controls[1]).ValueChanged += (s, e) => 
            { 
                responseLimit = (int)((NumericUpDown)responseRow.Controls[1]).Value; 
                SaveConfig(); 
            };
            panel.AddContent(responseRow);
            
            // Límite de archivos
            var fileRow = VisualFeedbackHelper.CreateConfigRow(
                "Archivos:",
                new NumericUpDown 
                { 
                    Size = new Size(80, 25), 
                    BackColor = Color.FromArgb(60, 60, 60), 
                    ForeColor = Color.White, 
                    Minimum = 0, 
                    Maximum = 999999, 
                    Value = fileLimit 
                },
                "(0 = sin límite)"
            );
            ((NumericUpDown)fileRow.Controls[1]).ValueChanged += (s, e) => 
            { 
                fileLimit = (int)((NumericUpDown)fileRow.Controls[1]).Value; 
                SaveConfig(); 
            };
            panel.AddContent(fileRow);
            
            // Búsquedas simultáneas
            var searchesRow = VisualFeedbackHelper.CreateConfigRow(
                "Búsquedas simultáneas:",
                new NumericUpDown 
                { 
                    Size = new Size(80, 25), 
                    BackColor = Color.FromArgb(60, 60, 60), 
                    ForeColor = Color.White, 
                    Minimum = 1, 
                    Maximum = 15, 
                    Value = maxParallelSearches 
                }
            );
            ((NumericUpDown)searchesRow.Controls[1]).ValueChanged += (s, e) => 
            { 
                maxParallelSearches = (int)((NumericUpDown)searchesRow.Controls[1]).Value; 
                SaveConfig(); 
            };
            panel.AddContent(searchesRow);
            
            // Puerto de escucha
            var portRow = VisualFeedbackHelper.CreateConfigRow(
                "Puerto de escucha:",
                new NumericUpDown 
                { 
                    Size = new Size(80, 25), 
                    BackColor = Color.FromArgb(60, 60, 60), 
                    ForeColor = Color.White, 
                    Minimum = 1024, 
                    Maximum = 65535, 
                    Value = listenPort 
                },
                "(para compartir)"
            );
            ((NumericUpDown)portRow.Controls[1]).ValueChanged += (s, e) => 
            { 
                listenPort = (int)((NumericUpDown)portRow.Controls[1]).Value; 
                SaveConfig(); 
            };
            panel.AddContent(portRow);
            
            // Red distribuida
            var chkDistributed = new CheckBox 
            { 
                Text = "Habilitar red distribuida", 
                AutoSize = true, 
                ForeColor = Color.White, 
                Checked = enableDistributedNetwork,
                Margin = new Padding(0, 10, 0, 5)
            };
            chkDistributed.CheckedChanged += (s, e) => { enableDistributedNetwork = chkDistributed.Checked; SaveConfig(); };
            panel.AddContent(chkDistributed);
        }
        
        private void CreateNicotineSection(SectionPanel panel)
        {
            var chkAutoReconnect = new CheckBox 
            { 
                Text = "Reconexión automática", 
                AutoSize = true, 
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 10), 
                Checked = autoReconnectEnabled 
            };
            AddTooltip(chkAutoReconnect, "Reconecta automáticamente a Soulseek si se pierde la conexión por problemas de red");
            chkAutoReconnect.CheckedChanged += (s, e) => { autoReconnectEnabled = chkAutoReconnect.Checked; SaveConfig(); };
            
            var chkAutoRetry = new CheckBox 
            { 
                Text = $"Auto-retry descargas ({autoRetryIntervalMinutes} min)", 
                AutoSize = true, 
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 10), 
                Checked = autoRetryEnabled 
            };
            AddTooltip(chkAutoRetry, "Reintenta automáticamente las descargas fallidas cada cierto intervalo de tiempo");
            chkAutoRetry.CheckedChanged += (s, e) => { autoRetryEnabled = chkAutoRetry.Checked; SaveConfig(); };
            
            var chkBatchSmall = new CheckBox 
            { 
                Text = $"Batch archivos pequeños (<{smallFileThresholdBytes / 1024 / 1024}MB)", 
                AutoSize = true, 
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 10), 
                Checked = batchSmallFiles 
            };
            AddTooltip(chkBatchSmall, "Agrupa archivos pequeños para descargarlos en lote, mejorando la eficiencia de la cola");
            chkBatchSmall.CheckedChanged += (s, e) => { batchSmallFiles = chkBatchSmall.Checked; SaveConfig(); };
            
            var chkPrioritySize = new CheckBox 
            { 
                Text = "Priorizar archivos pequeños", 
                AutoSize = true, 
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 10), 
                Checked = priorityBySize 
            };
            AddTooltip(chkPrioritySize, "Descarga primero los archivos más pequeños para completar más descargas rápidamente");
            chkPrioritySize.CheckedChanged += (s, e) => { priorityBySize = chkPrioritySize.Checked; SaveConfig(); PrioritizeDownloads(); };
            
            var chkPrioritySlots = new CheckBox 
            { 
                Text = "Priorizar usuarios con pocos slots", 
                AutoSize = true, 
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 10), 
                Checked = priorityBySlots 
            };
            AddTooltip(chkPrioritySlots, "Prioriza descargas de usuarios con menos slots ocupados para aprovechar mejor la disponibilidad");
            chkPrioritySlots.CheckedChanged += (s, e) => { priorityBySlots = chkPrioritySlots.Checked; SaveConfig(); PrioritizeDownloads(); };
            
            var chkContinuousSearch = new CheckBox 
            { 
                Text = "Búsqueda continua (cada 5s)", 
                AutoSize = true, 
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 10), 
                Checked = continuousSearch 
            };
            AddTooltip(chkContinuousSearch, "Ejecuta búsquedas continuas cada 5 segundos para encontrar nuevos resultados en tiempo real");
            chkContinuousSearch.CheckedChanged += (s, e) => { continuousSearch = chkContinuousSearch.Checked; SaveConfig(); };
            
            var grid = VisualFeedbackHelper.CreateTwoColumnCheckboxGrid(
                chkAutoReconnect,
                chkAutoRetry,
                chkBatchSmall,
                chkPrioritySize,
                chkPrioritySlots,
                chkContinuousSearch
            );
            
            panel.AddContent(grid);
            
            // Botón métricas
            var btnShowMetrics = CreateStyledButton("📊 VER MÉTRICAS", Color.FromArgb(0, 120, 215), 180, 40);
            btnShowMetrics.Margin = new Padding(0, 15, 0, 5);
            btnShowMetrics.Click += (s, e) => LogMetrics();
            AddTooltip(btnShowMetrics, "Muestra estadísticas detalladas de rendimiento: búsquedas, descargas, cache hits, bloom filter y más");
            panel.AddContent(btnShowMetrics);
        }
        
        private void CreateInterfazSection(SectionPanel panel)
        {
            var chkNotifications = new CheckBox 
            { 
                Text = "Notificaciones del sistema", 
                AutoSize = true, 
                ForeColor = Color.White, 
                Checked = enableNotifications 
            };
            chkNotifications.CheckedChanged += (s, e) => { enableNotifications = chkNotifications.Checked; SaveConfig(); };
            
            var chkSounds = new CheckBox 
            { 
                Text = "Sonidos de notificación", 
                AutoSize = true, 
                ForeColor = Color.White, 
                Checked = enableSounds 
            };
            chkSounds.CheckedChanged += (s, e) => { enableSounds = chkSounds.Checked; SaveConfig(); };
            
            var grid = VisualFeedbackHelper.CreateTwoColumnCheckboxGrid(chkNotifications, chkSounds);
            panel.AddContent(grid);
        }
        
        private void CreateAISection(SectionPanel panel)
        {
            var lblInfo = new Label 
            { 
                Text = "💡 Ollama es GRATIS y funciona localmente", 
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 200, 255), 
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                Margin = new Padding(0, 0, 0, 10)
            };
            panel.AddContent(lblInfo);
            
            var chkEnableAI = new CheckBox 
            { 
                Text = "✅ Activar IA con Ollama", 
                AutoSize = true,
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Checked = aiEnabled,
                Margin = new Padding(0, 8, 0, 8)
            };
            chkEnableAI.CheckedChanged += (s, e) => 
            { 
                aiEnabled = chkEnableAI.Checked; 
                SaveConfig(); 
                Log($"IA con Ollama: {(aiEnabled ? "ACTIVADA ✅" : "DESACTIVADA ❌")}");
            };
            panel.AddContent(chkEnableAI);
            
            var aiButtonsRow = new FlowLayoutPanel 
            { 
                FlowDirection = FlowDirection.LeftToRight, 
                AutoSize = true, 
                BackColor = Color.Transparent, 
                Margin = new Padding(0, 8, 0, 8) 
            };
            
            var btnConfigAI = CreateStyledButton("⚙️ INFO", Color.FromArgb(138, 43, 226), 120, 40);
            btnConfigAI.Margin = new Padding(0, 0, 8, 0);
            btnConfigAI.Click += (s, e) => ShowAIPanel();
            AddTooltip(btnConfigAI, "Muestra información sobre la configuración y estado del asistente de IA (Ollama)");
            aiButtonsRow.Controls.Add(btnConfigAI);
            
            panel.AddContent(aiButtonsRow);
        }
        
        /// <summary>
        /// Filtra los paneles de configuración según el texto de búsqueda
        /// </summary>
        private void FilterConfigPanels()
        {
            if (txtConfigSearch == null || allConfigPanels.Count == 0) return;
            
            string searchText = txtConfigSearch.Text.ToLower();
            
            // Si está vacío o es el placeholder, mostrar todos
            if (string.IsNullOrWhiteSpace(searchText) || searchText == "buscar configuración...")
            {
                foreach (var panel in allConfigPanels)
                {
                    panel.Visible = true;
                }
                return;
            }
            
            // Buscar coincidencias en títulos y contenido
            var keywords = new Dictionary<string, string[]>
            {
                ["cuenta"] = new[] { "usuario", "contraseña", "password", "carpeta", "descargas", "directorio" },
                ["opciones"] = new[] { "auto", "conectar", "organizar", "autor", "backup", "tamaño", "bytes" },
                ["descargas"] = new[] { "turbo", "descarga", "simultánea", "paralela", "reintento", "retry", "proveedor", "alternativa", "mínimo", "kb" },
                ["red"] = new[] { "timeout", "respuesta", "archivo", "búsqueda", "simultánea", "puerto", "listen", "red", "distribuida" },
                ["nicotine"] = new[] { "reconexión", "retry", "batch", "pequeño", "prioridad", "slot", "continua" },
                ["interfaz"] = new[] { "notificación", "sonido", "interfaz", "ui" },
                ["ai"] = new[] { "ia", "inteligencia", "artificial", "ollama", "asistente", "chat" }
            };
            
            bool anyMatch = false;
            
            foreach (var panel in allConfigPanels)
            {
                bool matches = false;
                
                // Buscar en título del panel
                string panelTitle = panel.Text.ToLower();
                if (panelTitle.Contains(searchText))
                {
                    matches = true;
                }
                
                // Buscar en keywords
                if (!matches)
                {
                    foreach (var kvp in keywords)
                    {
                        if (panelTitle.Contains(kvp.Key))
                        {
                            foreach (var keyword in kvp.Value)
                            {
                                if (keyword.Contains(searchText) || searchText.Contains(keyword))
                                {
                                    matches = true;
                                    break;
                                }
                            }
                        }
                        if (matches) break;
                    }
                }
                
                panel.Visible = matches;
                
                // Marcar si hay coincidencia
                if (matches)
                {
                    anyMatch = true;
                }
            }
            
            // Si no hay coincidencias, mostrar mensaje
            if (!anyMatch)
            {
                Log($"No se encontraron opciones para: '{searchText}'");
            }
        }
        
        /// <summary>
        /// Aplica un preset de configuración predefinido
        /// </summary>
        private void ApplyPreset(string preset)
        {
            switch (preset.ToLower())
            {
                case "conservador":
                    // Configuración conservadora: segura y lenta
                    maxParallelDownloads = 3;
                    maxSimultaneousDownloads = 3;
                    maxParallelSearches = 3;
                    searchTimeout = 30;
                    maxRetries = 2;
                    maxAlternativeRetries = 2;
                    responseLimit = 50;
                    fileLimit = 100;
                    
                    if (chkTurboMode != null) chkTurboMode.Checked = false;
                    if (numParallelDownloads != null) numParallelDownloads.Value = 3;
                    if (numMaxRetries != null) numMaxRetries.Value = 2;
                    
                    Log("✅ Preset CONSERVADOR aplicado:");
                    Log("  • 3 descargas simultáneas");
                    Log("  • 3 búsquedas paralelas");
                    Log("  • Timeout: 30 segundos");
                    Log("  • 2 reintentos por archivo");
                    Log("  • Ideal para conexiones lentas o inestables");
                    
                    VisualFeedbackHelper.ShowTemporaryBadge(
                        chkTurboMode ?? (Control)this,
                        "🐢 Modo Conservador",
                        Color.FromArgb(100, 100, 100),
                        3000
                    );
                    break;
                    
                case "balanceado":
                    // Configuración balanceada: equilibrio entre velocidad y estabilidad
                    maxParallelDownloads = 5;
                    maxSimultaneousDownloads = 5;
                    maxParallelSearches = 6;
                    searchTimeout = 25;
                    maxRetries = 3;
                    maxAlternativeRetries = 3;
                    responseLimit = 100;
                    fileLimit = 200;
                    
                    if (chkTurboMode != null) chkTurboMode.Checked = false;
                    if (numParallelDownloads != null) numParallelDownloads.Value = 5;
                    if (numMaxRetries != null) numMaxRetries.Value = 3;
                    
                    Log("✅ Preset BALANCEADO aplicado:");
                    Log("  • 5 descargas simultáneas");
                    Log("  • 6 búsquedas paralelas");
                    Log("  • Timeout: 25 segundos");
                    Log("  • 3 reintentos por archivo");
                    Log("  • Recomendado para la mayoría de usuarios");
                    
                    VisualFeedbackHelper.ShowTemporaryBadge(
                        chkTurboMode ?? (Control)this,
                        "⚖️ Modo Balanceado",
                        Color.FromArgb(0, 120, 215),
                        3000
                    );
                    break;
                    
                case "agresivo":
                    // Configuración agresiva: máxima velocidad
                    maxParallelDownloads = 8;
                    maxSimultaneousDownloads = 8;
                    maxParallelSearches = 12;
                    searchTimeout = 20;
                    maxRetries = 3;
                    maxAlternativeRetries = 3;
                    responseLimit = 200;
                    fileLimit = 500;
                    
                    if (chkTurboMode != null) chkTurboMode.Checked = true;
                    if (numParallelDownloads != null) numParallelDownloads.Value = 8;
                    if (numMaxRetries != null) numMaxRetries.Value = 3;
                    
                    Log("✅ Preset AGRESIVO aplicado:");
                    Log("  • 8 descargas simultáneas (Modo Turbo)");
                    Log("  • 12 búsquedas paralelas");
                    Log("  • Timeout: 20 segundos");
                    Log("  • 3 reintentos por archivo");
                    Log("  • Ideal para conexiones rápidas y estables");
                    
                    VisualFeedbackHelper.ShowTemporaryBadge(
                        chkTurboMode ?? (Control)this,
                        "🚀 Modo Agresivo",
                        Color.FromArgb(200, 100, 0),
                        3000
                    );
                    break;
            }
            
            SaveConfig();
        }
    }
}
