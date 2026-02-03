using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace SlskDown
{
    /// <summary>
    /// Partial class para configuraciÃ³n y preferencias de MainForm
    /// </summary>
    public partial class MainForm
    {
        // NOTA: Las variables de configuraciÃ³n estÃ¡n definidas en MainForm.cs
        // No duplicar: usernameTextBox, passwordTextBox, downloadDirTextBox, etc.
        
        /// <summary>
        /// Agregar pestaÃ±a de configuraciÃ³n dinÃ¡micamente
        /// </summary>
        private void AddConfigTab()
        {
            isLoadingConfig = true; // Activar bandera para evitar auto-guardado durante creaciÃ³n de controles
            
            var configTab = new TabPage("âš™ï¸ Config");
            configTab.BackColor = Color.FromArgb(30, 30, 30);
            
            var configPanel = new Panel();
            configPanel.Dock = DockStyle.Fill;
            configPanel.Padding = new Padding(20);
            configPanel.AutoScroll = true; // Permitir scroll para ver todos los controles
            
            // SecciÃ³n de conexiÃ³n
            CreateConnectionSection(configPanel);
            
            // SecciÃ³n de descargas
            CreateDownloadSection(configPanel);
            
            // SecciÃ³n de lÃ­mites
            CreateLimitsSection(configPanel);
            
            // SecciÃ³n de opciones avanzadas
            CreateAdvancedSection(configPanel);
            
            // BotÃ³n de prueba de conexiÃ³n
            CreateTestConnectionSection(configPanel);
            
            configTab.Controls.Add(configPanel);
            tabControl.TabPages.Add(configTab);
            
            // Cargar configuraciÃ³n existente
            LoadConfigToControls();
            
            isLoadingConfig = false;
            
            Console.WriteLine("[MainForm.Config] âœ… PestaÃ±a de configuraciÃ³n agregada");
        }
        
        /// <summary>
        /// Crear secciÃ³n de conexiÃ³n
        /// </summary>
        private void CreateConnectionSection(Panel parent)
        {
            var y = 20;
            
            // TÃ­tulo de secciÃ³n
            var titleLabel = new Label();
            titleLabel.Text = "ðŸ” CONEXIÃ“N SOULSEEK";
            titleLabel.Location = new Point(20, y);
            titleLabel.Size = new Size(300, 25);
            titleLabel.ForeColor = Color.FromArgb(59, 130, 246);
            titleLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            parent.Controls.Add(titleLabel);
            
            y += 35;
            
            // Usuario
            var userLabel = new Label();
            userLabel.Text = "Usuario:";
            userLabel.Location = new Point(20, y);
            userLabel.Size = new Size(100, 20);
            userLabel.ForeColor = Color.LightGray;
            parent.Controls.Add(userLabel);
            
            usernameTextBox = new TextBox();
            usernameTextBox.Location = new Point(130, y - 3);
            usernameTextBox.Size = new Size(300, 25);
            usernameTextBox.BackColor = Color.FromArgb(60, 60, 60);
            usernameTextBox.ForeColor = Color.White;
            usernameTextBox.TextChanged += (s, e) => { if (!isLoadingConfig) SaveConfigSilent(); };
            parent.Controls.Add(usernameTextBox);
            
            y += 35;
            
            // ContraseÃ±a
            var passLabel = new Label();
            passLabel.Text = "ContraseÃ±a:";
            passLabel.Location = new Point(20, y);
            passLabel.Size = new Size(100, 20);
            passLabel.ForeColor = Color.LightGray;
            parent.Controls.Add(passLabel);
            
            passwordTextBox = new TextBox();
            passwordTextBox.Location = new Point(130, y - 3);
            passwordTextBox.Size = new Size(300, 25);
            passwordTextBox.UseSystemPasswordChar = false;
            passwordTextBox.PasswordChar = '\0';
            passwordTextBox.Font = new Font("Segoe UI", 9, FontStyle.Regular);
            passwordTextBox.BackColor = Color.FromArgb(60, 60, 60);
            passwordTextBox.ForeColor = Color.White;
            passwordTextBox.TextChanged += (s, e) => { if (!isLoadingConfig) SaveConfigSilent(); };
            parent.Controls.Add(passwordTextBox);
            
            y += 35;
            
            // Auto-conectar
            autoConnectCheckBox = new CheckBox();
            autoConnectCheckBox.Text = "Conectar automÃ¡ticamente al iniciar";
            autoConnectCheckBox.Location = new Point(130, y);
            autoConnectCheckBox.Size = new Size(250, 25);
            autoConnectCheckBox.ForeColor = Color.LightGray;
            autoConnectCheckBox.CheckedChanged += (s, e) => { if (!isLoadingConfig) SaveConfigSilent(); };
            parent.Controls.Add(autoConnectCheckBox);
        }
        
        /// <summary>
        /// Crear secciÃ³n de descargas
        /// </summary>
        private void CreateDownloadSection(Panel parent)
        {
            var y = 180;
            
            // TÃ­tulo de secciÃ³n
            var titleLabel = new Label();
            titleLabel.Text = "ðŸ“ DESCARGAS";
            titleLabel.Location = new Point(20, y);
            titleLabel.Size = new Size(200, 25);
            titleLabel.ForeColor = Color.FromArgb(59, 130, 246);
            titleLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            parent.Controls.Add(titleLabel);
            
            y += 35;
            
            // Carpeta de descargas
            var dirLabel = new Label();
            dirLabel.Text = "Carpeta:";
            dirLabel.Location = new Point(20, y);
            dirLabel.Size = new Size(100, 20);
            dirLabel.ForeColor = Color.LightGray;
            parent.Controls.Add(dirLabel);
            
            downloadDirTextBox = new TextBox();
            downloadDirTextBox.Location = new Point(130, y - 3);
            downloadDirTextBox.Size = new Size(400, 25);
            downloadDirTextBox.BackColor = Color.FromArgb(60, 60, 60);
            downloadDirTextBox.ForeColor = Color.White;
            downloadDirTextBox.TextChanged += (s, e) => { if (!isLoadingConfig) SaveConfigSilent(); };
            parent.Controls.Add(downloadDirTextBox);
            
            var browseBtn = new Button();
            browseBtn.Text = "ðŸ“‚";
            browseBtn.Location = new Point(540, y - 5);
            browseBtn.Size = new Size(40, 30);
            browseBtn.BackColor = Color.FromArgb(70, 70, 70);
            browseBtn.ForeColor = Color.White;
            browseBtn.FlatStyle = FlatStyle.Flat;
            browseBtn.FlatAppearance.BorderSize = 0;
            browseBtn.Click += BrowseFolder_Click;
            parent.Controls.Add(browseBtn);
            
            y += 35;
            
            // Archivo de tracking
            var trackingLabel = new Label();
            trackingLabel.Text = "Tracking:";
            trackingLabel.Location = new Point(20, y);
            trackingLabel.Size = new Size(100, 20);
            trackingLabel.ForeColor = Color.LightGray;
            parent.Controls.Add(trackingLabel);
            
            downloadedFilesListTextBox = new TextBox();
            downloadedFilesListTextBox.Location = new Point(130, y - 3);
            downloadedFilesListTextBox.Size = new Size(400, 25);
            downloadedFilesListTextBox.BackColor = Color.FromArgb(60, 60, 60);
            downloadedFilesListTextBox.ForeColor = Color.White;
            downloadedFilesListTextBox.TextChanged += (s, e) => { if (!isLoadingConfig) SaveConfigSilent(); };
            parent.Controls.Add(downloadedFilesListTextBox);
            
            y += 35;
            
            // Auto-descarga
            autoDownloadCheckBox = new CheckBox();
            autoDownloadCheckBox.Text = "Auto-descargar mejores resultados";
            autoDownloadCheckBox.Location = new Point(130, y);
            autoDownloadCheckBox.Size = new Size(250, 25);
            autoDownloadCheckBox.ForeColor = Color.LightGray;
            autoDownloadCheckBox.CheckedChanged += (s, e) => { if (!isLoadingConfig) SaveConfigSilent(); };
            parent.Controls.Add(autoDownloadCheckBox);
        }
        
        /// <summary>
        /// Crear secciÃ³n de lÃ­mites
        /// </summary>
        private void CreateLimitsSection(Panel parent)
        {
            var y = 340;
            
            // TÃ­tulo de secciÃ³n
            var titleLabel = new Label();
            titleLabel.Text = "âš™ï¸ LÃMITES Y RENDIMIENTO";
            titleLabel.Location = new Point(20, y);
            titleLabel.Size = new Size(250, 25);
            titleLabel.ForeColor = Color.FromArgb(59, 130, 246);
            titleLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            parent.Controls.Add(titleLabel);
            
            y += 35;
            
            // Timeout de bÃºsqueda
            var timeoutLabel = new Label();
            timeoutLabel.Text = "Timeout (seg):";
            timeoutLabel.Location = new Point(20, y);
            timeoutLabel.Size = new Size(120, 20);
            timeoutLabel.ForeColor = Color.LightGray;
            parent.Controls.Add(timeoutLabel);
            
            timeoutBox = new NumericUpDown();
            timeoutBox.Location = new Point(150, y - 3);
            timeoutBox.Size = new Size(80, 25);
            timeoutBox.Minimum = 10;
            timeoutBox.Maximum = 300;
            timeoutBox.Value = 60;
            timeoutBox.BackColor = Color.FromArgb(60, 60, 60);
            timeoutBox.ForeColor = Color.White;
            timeoutBox.ValueChanged += (s, e) => { if (!isLoadingConfig) SaveConfigSilent(); };
            parent.Controls.Add(timeoutBox);
            
            y += 35;
            
            // MÃ¡ximos resultados
            var maxResultsLabel = new Label();
            maxResultsLabel.Text = "MÃ¡x. resultados:";
            maxResultsLabel.Location = new Point(20, y);
            maxResultsLabel.Size = new Size(120, 20);
            maxResultsLabel.ForeColor = Color.LightGray;
            parent.Controls.Add(maxResultsLabel);
            
            maxResultsBox = new NumericUpDown();
            maxResultsBox.Location = new Point(150, y - 3);
            maxResultsBox.Size = new Size(80, 25);
            maxResultsBox.Minimum = 100;
            maxResultsBox.Maximum = 10000;
            maxResultsBox.Value = 1000;
            maxResultsBox.BackColor = Color.FromArgb(60, 60, 60);
            maxResultsBox.ForeColor = Color.White;
            maxResultsBox.ValueChanged += (s, e) => { if (!isLoadingConfig) SaveConfigSilent(); };
            parent.Controls.Add(maxResultsBox);
            
            y += 35;
            
            // MÃ¡ximas descargas
            var maxDownloadsLabel = new Label();
            maxDownloadsLabel.Text = "MÃ¡x. descargas:";
            maxDownloadsLabel.Location = new Point(20, y);
            maxDownloadsLabel.Size = new Size(120, 20);
            maxDownloadsLabel.ForeColor = Color.LightGray;
            parent.Controls.Add(maxDownloadsLabel);
            
            maxDownloadsBox = new NumericUpDown();
            maxDownloadsBox.Location = new Point(150, y - 3);
            maxDownloadsBox.Size = new Size(80, 25);
            maxDownloadsBox.Minimum = 1;
            maxDownloadsBox.Maximum = 20;
            maxDownloadsBox.Value = 5;
            maxDownloadsBox.BackColor = Color.FromArgb(60, 60, 60);
            maxDownloadsBox.ForeColor = Color.White;
            maxDownloadsBox.ValueChanged += (s, e) => { if (!isLoadingConfig) SaveConfigSilent(); };
            parent.Controls.Add(maxDownloadsBox);
            
            y += 35;
            
            // LÃ­mite de intentos sin resultados (auto-limpieza)
            var failedAttemptsLabel = new Label();
            failedAttemptsLabel.Text = "LÃ­m. intentos sin resultados:";
            failedAttemptsLabel.Location = new Point(20, y);
            failedAttemptsLabel.Size = new Size(180, 20);
            failedAttemptsLabel.ForeColor = Color.LightGray;
            parent.Controls.Add(failedAttemptsLabel);
            
            maxFailedAttemptsBox = new NumericUpDown();
            maxFailedAttemptsBox.Location = new Point(210, y - 3);
            maxFailedAttemptsBox.Size = new Size(80, 25);
            maxFailedAttemptsBox.Minimum = 1;
            maxFailedAttemptsBox.Maximum = 10;
            maxFailedAttemptsBox.Value = 3;
            maxFailedAttemptsBox.BackColor = Color.FromArgb(60, 60, 60);
            maxFailedAttemptsBox.ForeColor = Color.White;
            maxFailedAttemptsBox.ValueChanged += (s, e) => { if (!isLoadingConfig) SaveConfigSilent(); };
            parent.Controls.Add(maxFailedAttemptsBox);
        }
        
        /// <summary>
        /// Crear secciÃ³n de opciones avanzadas
        /// </summary>
        private void CreateAdvancedSection(Panel parent)
        {
            var y = 480;
            
            // TÃ­tulo de secciÃ³n
            var titleLabel = new Label();
            titleLabel.Text = "ðŸš€ OPCIONES AVANZADAS";
            titleLabel.Location = new Point(20, y);
            titleLabel.Size = new Size(250, 25);
            titleLabel.ForeColor = Color.FromArgb(59, 130, 246);
            titleLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            parent.Controls.Add(titleLabel);
            
            y += 35;
            
            // InformaciÃ³n del sistema
            var infoLabel = new Label();
            infoLabel.Text = $"SlskDown v2.0 - Core Rust: {(useRustCore ? "âœ… Activo" : "âŒ Inactivo")}";
            infoLabel.Location = new Point(20, y);
            infoLabel.Size = new Size(400, 20);
            infoLabel.ForeColor = useRustCore ? Color.LightGreen : Color.Orange;
            infoLabel.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            parent.Controls.Add(infoLabel);
            
            y += 25;
            
            var optimizationLabel = new Label();
            optimizationLabel.Text = "âœ… Optimizaciones: SIMD | Object Pool | Memory Cache | Streaming";
            optimizationLabel.Location = new Point(20, y);
            optimizationLabel.Size = new Size(500, 20);
            optimizationLabel.ForeColor = Color.LightGray;
            parent.Controls.Add(optimizationLabel);
        }
        
        /// <summary>
        /// Crear secciÃ³n de prueba de conexiÃ³n
        /// </summary>
        private void CreateTestConnectionSection(Panel parent)
        {
            var y = 580;
            
            // BotÃ³n de prueba
            testConnectionButton = new Button();
            testConnectionButton.Text = "ðŸ§ª Probar ConexiÃ³n";
            testConnectionButton.Location = new Point(20, y);
            testConnectionButton.Size = new Size(150, 35);
            testConnectionButton.BackColor = Color.FromArgb(59, 130, 246);
            testConnectionButton.ForeColor = Color.White;
            testConnectionButton.FlatStyle = FlatStyle.Flat;
            testConnectionButton.FlatAppearance.BorderSize = 0;
            testConnectionButton.Click += TestConnection_Click;
            parent.Controls.Add(testConnectionButton);
            
            // Label de estado
            configStatusLabel = new Label();
            configStatusLabel.Text = "Listo para probar conexiÃ³n";
            configStatusLabel.Location = new Point(180, y + 8);
            configStatusLabel.Size = new Size(300, 20);
            configStatusLabel.ForeColor = Color.LightGray;
            parent.Controls.Add(configStatusLabel);
        }
        
        /// <summary>
        /// Cargar configuraciÃ³n en los controles
        /// </summary>
        private void LoadConfigToControls()
        {
            try
            {
                usernameTextBox.Text = username;
                passwordTextBox.Text = password;
                downloadDirTextBox.Text = downloadDir;
                downloadedFilesListTextBox.Text = downloadedFilesList;
                
                autoConnectCheckBox.Checked = autoConnect;
                autoDownloadCheckBox.Checked = autoDownload;
                
                timeoutBox.Value = defaultSearchTimeoutSecs;
                maxResultsBox.Value = maxResults;
                maxDownloadsBox.Value = maxConcurrentDownloads;
                maxFailedAttemptsBox.Value = maxFailedAttempts;
                
                Console.WriteLine("[MainForm.Config] âœ… ConfiguraciÃ³n cargada en controles");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainForm.Config] âŒ Error cargando configuraciÃ³n: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Guardar configuraciÃ³n desde controles
        /// </summary>
        private void SaveConfigFromControls()
        {
            try
            {
                username = usernameTextBox.Text;
                password = passwordTextBox.Text;
                downloadDir = downloadDirTextBox.Text;
                downloadedFilesList = downloadedFilesListTextBox.Text;
                
                autoConnect = autoConnectCheckBox.Checked;
                autoDownload = autoDownloadCheckBox.Checked;
                
                defaultSearchTimeoutSecs = (int)timeoutBox.Value;
                maxResults = (int)maxResultsBox.Value;
                maxConcurrentDownloads = (int)maxDownloadsBox.Value;
                maxFailedAttempts = (int)maxFailedAttemptsBox.Value;
                
                SaveConfigSilent();
                
                Console.WriteLine("[MainForm.Config] âœ… ConfiguraciÃ³n guardada desde controles");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainForm.Config] âŒ Error guardando configuraciÃ³n: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Evento de prueba de conexiÃ³n
        /// </summary>
        private async void TestConnection_Click(object? sender, EventArgs e)
        {
            try
            {
                testConnectionButton.Enabled = false;
                testConnectionButton.Text = "ðŸ”„ Probando...";
                configStatusLabel.Text = "Conectando al servidor...";
                configStatusLabel.ForeColor = Color.Yellow;
                
                // Guardar configuraciÃ³n actual
                SaveConfigFromControls();
                
                // Probar conexiÃ³n con credenciales nuevas
                var testClient = new SoulseekClient(options: new SoulseekClientOptions(
                    username: username,
                    password: password,
                    enableListener: false,
                    listenerPort: 0
                ));
                
                await testClient.ConnectAsync();
                await testClient.LoginAsync();
                
                configStatusLabel.Text = "âœ… ConexiÃ³n exitosa";
                configStatusLabel.ForeColor = Color.LightGreen;
                
                testClient.Disconnect();
                
                Console.WriteLine("[MainForm.Config] âœ… Prueba de conexiÃ³n exitosa");
            }
            catch (Exception ex)
            {
                configStatusLabel.Text = $"âŒ Error: {ex.Message}";
                configStatusLabel.ForeColor = Color.Red;
                
                Console.WriteLine($"[MainForm.Config] âŒ Error en prueba de conexiÃ³n: {ex.Message}");
            }
            finally
            {
                testConnectionButton.Enabled = true;
                testConnectionButton.Text = "ðŸ§ª Probar ConexiÃ³n";
            }
        }
        
        /// <summary>
        /// Evento de selecciÃ³n de carpeta
        /// </summary>
        private void BrowseFolder_Click(object? sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            dialog.SelectedPath = downloadDir;
            dialog.Description = "Seleccionar carpeta de descargas";
            
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                downloadDirTextBox.Text = dialog.SelectedPath;
                SaveConfigSilent();
                
                Console.WriteLine($"[MainForm.Config] ðŸ“ Carpeta seleccionada: {dialog.SelectedPath}");
            }
        }
    }
}

