using System;
using System.Windows.Forms;
using SlskDown.Data;
using SlskDown.Services;

namespace SlskDown.UI
{
    /// <summary>
    /// UserControl para la configuración de la aplicación
    /// Extrae ~500 líneas de MainForm.cs
    /// </summary>
    public partial class SettingsUserControl : UserControl
    {
        private ConfigManager configManager;
        
        // Controles de UI (crear en Designer o programáticamente)
        private TextBox txtUsername;
        private TextBox txtPassword;
        private TextBox txtDownloadDir;
        private Button btnBrowseDir;
        private NumericUpDown numPort;
        private NumericUpDown numMaxDownloads;
        private NumericUpDown numMaxSearches;
        private NumericUpDown numMaxRetries;
        private NumericUpDown numMaxAlternativeRetries;
        private NumericUpDown numSearchTimeout;
        private NumericUpDown numResponseLimit;
        private NumericUpDown numFileLimit;
        private NumericUpDown numMinFileSizeKB;
        private CheckBox chkAutoConnect;
        private CheckBox chkAutoBackup;
        private CheckBox chkAutoMode;
        private CheckBox chkInstantDownload;
        private CheckBox chkOrganizeByAuthor;
        private CheckBox chkEnableDistributedNetwork;
        private CheckBox chkOnlyNewFiles;
        private CheckBox chkPriorityBySize;
        private Button btnSave;
        private Button btnCancel;
        private Button btnTest;
        private Button btnBackup;
        private Button btnRestore;
        private Label lblStatus;
        
        public SettingsUserControl(ConfigManager config)
        {
            configManager = config ?? throw new ArgumentNullException(nameof(config));
            InitializeComponent();
            LoadSettings();
        }
        
        private void InitializeComponent()
        {
            // Crear controles programáticamente
            // (Idealmente esto se haría en el Designer)
            
            this.SuspendLayout();
            
            // Username
            var lblUsername = new Label { Text = "Usuario:", Location = new System.Drawing.Point(10, 10), AutoSize = true };
            txtUsername = new TextBox { Location = new System.Drawing.Point(150, 10), Width = 200 };
            
            // Password
            var lblPassword = new Label { Text = "Contraseña:", Location = new System.Drawing.Point(10, 40), AutoSize = true };
            txtPassword = new TextBox { Location = new System.Drawing.Point(150, 40), Width = 200, UseSystemPasswordChar = true };
            
            // Download Directory
            var lblDownloadDir = new Label { Text = "Directorio de descargas:", Location = new System.Drawing.Point(10, 70), AutoSize = true };
            txtDownloadDir = new TextBox { Location = new System.Drawing.Point(150, 70), Width = 200 };
            btnBrowseDir = new Button { Text = "...", Location = new System.Drawing.Point(360, 70), Width = 30 };
            btnBrowseDir.Click += BtnBrowseDir_Click;
            
            // Port
            var lblPort = new Label { Text = "Puerto:", Location = new System.Drawing.Point(10, 100), AutoSize = true };
            numPort = new NumericUpDown { Location = new System.Drawing.Point(150, 100), Width = 100, Minimum = 1024, Maximum = 65535, Value = 50000 };
            
            // Max Downloads
            var lblMaxDownloads = new Label { Text = "Descargas paralelas:", Location = new System.Drawing.Point(10, 130), AutoSize = true };
            numMaxDownloads = new NumericUpDown { Location = new System.Drawing.Point(150, 130), Width = 100, Minimum = 1, Maximum = 10, Value = 3 };
            
            // Max Searches
            var lblMaxSearches = new Label { Text = "Búsquedas paralelas:", Location = new System.Drawing.Point(10, 160), AutoSize = true };
            numMaxSearches = new NumericUpDown { Location = new System.Drawing.Point(150, 160), Width = 100, Minimum = 1, Maximum = 10, Value = 3 };
            
            // Max Retries
            var lblMaxRetries = new Label { Text = "Reintentos máximos:", Location = new System.Drawing.Point(10, 190), AutoSize = true };
            numMaxRetries = new NumericUpDown { Location = new System.Drawing.Point(150, 190), Width = 100, Minimum = 0, Maximum = 10, Value = 3 };
            
            // Max Alternative Retries
            var lblMaxAltRetries = new Label { Text = "Proveedores alternativos:", Location = new System.Drawing.Point(10, 220), AutoSize = true };
            numMaxAlternativeRetries = new NumericUpDown { Location = new System.Drawing.Point(150, 220), Width = 100, Minimum = 0, Maximum = 10, Value = 3 };
            
            // Search Timeout
            var lblSearchTimeout = new Label { Text = "Timeout de búsqueda (s):", Location = new System.Drawing.Point(10, 250), AutoSize = true };
            numSearchTimeout = new NumericUpDown { Location = new System.Drawing.Point(150, 250), Width = 100, Minimum = 5, Maximum = 300, Value = 30 };
            
            // Response Limit
            var lblResponseLimit = new Label { Text = "Límite de respuestas:", Location = new System.Drawing.Point(10, 280), AutoSize = true };
            numResponseLimit = new NumericUpDown { Location = new System.Drawing.Point(150, 280), Width = 100, Minimum = 100, Maximum = 10000, Value = 5000 };
            
            // File Limit
            var lblFileLimit = new Label { Text = "Límite de archivos:", Location = new System.Drawing.Point(10, 310), AutoSize = true };
            numFileLimit = new NumericUpDown { Location = new System.Drawing.Point(150, 310), Width = 100, Minimum = 0, Maximum = 10000, Value = 0 };
            
            // Min File Size
            var lblMinFileSize = new Label { Text = "Tamaño mínimo (KB):", Location = new System.Drawing.Point(10, 340), AutoSize = true };
            numMinFileSizeKB = new NumericUpDown { Location = new System.Drawing.Point(150, 340), Width = 100, Minimum = 0, Maximum = 10240, Value = 0 };
            
            // Checkboxes
            chkAutoConnect = new CheckBox { Text = "Conectar automáticamente", Location = new System.Drawing.Point(10, 370), AutoSize = true };
            chkAutoBackup = new CheckBox { Text = "Backup automático", Location = new System.Drawing.Point(10, 395), AutoSize = true };
            chkAutoMode = new CheckBox { Text = "Modo automático", Location = new System.Drawing.Point(10, 420), AutoSize = true };
            chkInstantDownload = new CheckBox { Text = "Descarga instantánea", Location = new System.Drawing.Point(10, 445), AutoSize = true };
            chkOrganizeByAuthor = new CheckBox { Text = "Organizar por autor", Location = new System.Drawing.Point(10, 470), AutoSize = true };
            chkEnableDistributedNetwork = new CheckBox { Text = "Red distribuida", Location = new System.Drawing.Point(200, 370), AutoSize = true };
            chkOnlyNewFiles = new CheckBox { Text = "Solo archivos nuevos", Location = new System.Drawing.Point(200, 395), AutoSize = true };
            chkPriorityBySize = new CheckBox { Text = "Prioridad por tamaño", Location = new System.Drawing.Point(200, 420), AutoSize = true };
            
            // Buttons
            btnSave = new Button { Text = "Guardar", Location = new System.Drawing.Point(10, 510), Width = 100 };
            btnSave.Click += BtnSave_Click;
            
            btnCancel = new Button { Text = "Cancelar", Location = new System.Drawing.Point(120, 510), Width = 100 };
            btnCancel.Click += BtnCancel_Click;
            
            btnTest = new Button { Text = "Probar Conexión", Location = new System.Drawing.Point(230, 510), Width = 120 };
            btnTest.Click += BtnTest_Click;
            
            btnBackup = new Button { Text = "Crear Backup", Location = new System.Drawing.Point(360, 510), Width = 100 };
            btnBackup.Click += BtnBackup_Click;
            
            btnRestore = new Button { Text = "Restaurar", Location = new System.Drawing.Point(470, 510), Width = 100 };
            btnRestore.Click += BtnRestore_Click;
            
            // Status Label
            lblStatus = new Label { Location = new System.Drawing.Point(10, 545), AutoSize = true, ForeColor = System.Drawing.Color.Green };
            
            // Add all controls
            this.Controls.AddRange(new Control[]
            {
                lblUsername, txtUsername,
                lblPassword, txtPassword,
                lblDownloadDir, txtDownloadDir, btnBrowseDir,
                lblPort, numPort,
                lblMaxDownloads, numMaxDownloads,
                lblMaxSearches, numMaxSearches,
                lblMaxRetries, numMaxRetries,
                lblMaxAltRetries, numMaxAlternativeRetries,
                lblSearchTimeout, numSearchTimeout,
                lblResponseLimit, numResponseLimit,
                lblFileLimit, numFileLimit,
                lblMinFileSize, numMinFileSizeKB,
                chkAutoConnect, chkAutoBackup, chkAutoMode, chkInstantDownload,
                chkOrganizeByAuthor, chkEnableDistributedNetwork, chkOnlyNewFiles, chkPriorityBySize,
                btnSave, btnCancel, btnTest, btnBackup, btnRestore,
                lblStatus
            });
            
            this.Size = new System.Drawing.Size(600, 580);
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        
        private void LoadSettings()
        {
            try
            {
                txtUsername.Text = configManager.GetValue("username", "");
                txtPassword.Text = configManager.GetValue("password", "");
                txtDownloadDir.Text = configManager.GetValue("downloadDir", "");
                numPort.Value = configManager.GetValue("listenPort", 50000);
                numMaxDownloads.Value = configManager.GetValue("maxParallelDownloads", 3);
                numMaxSearches.Value = configManager.GetValue("maxParallelSearches", 3);
                numMaxRetries.Value = configManager.GetValue("maxRetries", 3);
                numMaxAlternativeRetries.Value = configManager.GetValue("maxAlternativeRetries", 3);
                numSearchTimeout.Value = configManager.GetValue("searchTimeout", 30);
                numResponseLimit.Value = configManager.GetValue("responseLimit", 5000);
                numFileLimit.Value = configManager.GetValue("fileLimit", 0);
                numMinFileSizeKB.Value = configManager.GetValue("minFileSizeKB", 0);
                chkAutoConnect.Checked = configManager.GetValue("autoConnect", true);
                chkAutoBackup.Checked = configManager.GetValue("autoBackup", false);
                chkAutoMode.Checked = configManager.GetValue("autoMode", false);
                chkInstantDownload.Checked = configManager.GetValue("instantDownload", true);
                chkOrganizeByAuthor.Checked = configManager.GetValue("organizeByAuthor", false);
                chkEnableDistributedNetwork.Checked = configManager.GetValue("enableDistributedNetwork", true);
                chkOnlyNewFiles.Checked = configManager.GetValue("onlyNewFilesInAutoSearch", false);
                chkPriorityBySize.Checked = configManager.GetValue("priorityBySize", false);
                
                ShowStatus("Configuración cargada", System.Drawing.Color.Green);
            }
            catch (Exception ex)
            {
                ShowStatus($"Error cargando configuración: {ex.Message}", System.Drawing.Color.Red);
            }
        }
        
        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (!ValidateAndSave())
                return;
            
            ShowStatus("Configuración guardada correctamente", System.Drawing.Color.Green);
        }
        
        public bool ValidateAndSave()
        {
            try
            {
                // Validar configuración
                var (isValid, error) = ValidationHelpers.ValidateConfiguration(
                    txtUsername.Text,
                    txtPassword.Text,
                    txtDownloadDir.Text,
                    (int)numPort.Value
                );
                
                if (!isValid)
                {
                    MessageBox.Show(error, "Error de Configuración", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ShowStatus($"{error}", System.Drawing.Color.Red);
                    return false;
                }
                
                // Guardar valores
                configManager.SetValue("username", txtUsername.Text);
                configManager.SetValue("password", txtPassword.Text);
                configManager.SetValue("downloadDir", txtDownloadDir.Text);
                configManager.SetValue("listenPort", (int)numPort.Value);
                configManager.SetValue("maxParallelDownloads", (int)numMaxDownloads.Value);
                configManager.SetValue("maxParallelSearches", (int)numMaxSearches.Value);
                configManager.SetValue("maxRetries", (int)numMaxRetries.Value);
                configManager.SetValue("maxAlternativeRetries", (int)numMaxAlternativeRetries.Value);
                configManager.SetValue("searchTimeout", (int)numSearchTimeout.Value);
                configManager.SetValue("responseLimit", (int)numResponseLimit.Value);
                configManager.SetValue("fileLimit", (int)numFileLimit.Value);
                configManager.SetValue("minFileSizeKB", (int)numMinFileSizeKB.Value);
                configManager.SetValue("autoConnect", chkAutoConnect.Checked);
                configManager.SetValue("autoBackup", chkAutoBackup.Checked);
                configManager.SetValue("autoMode", chkAutoMode.Checked);
                configManager.SetValue("instantDownload", chkInstantDownload.Checked);
                configManager.SetValue("organizeByAuthor", chkOrganizeByAuthor.Checked);
                configManager.SetValue("enableDistributedNetwork", chkEnableDistributedNetwork.Checked);
                configManager.SetValue("onlyNewFilesInAutoSearch", chkOnlyNewFiles.Checked);
                configManager.SetValue("priorityBySize", chkPriorityBySize.Checked);
                
                // Guardar
                configManager.Save();
                
                // Backup automático si está activado
                if (chkAutoBackup.Checked)
                {
                    configManager.CreateBackup();
                }
                
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error guardando configuración: {ex.Message}", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ShowStatus($"Error: {ex.Message}", System.Drawing.Color.Red);
                return false;
            }
        }
        
        private void BtnCancel_Click(object sender, EventArgs e)
        {
            LoadSettings(); // Recargar valores originales
            ShowStatus("Cambios cancelados", System.Drawing.Color.Blue);
        }
        
        private async void BtnTest_Click(object sender, EventArgs e)
        {
            btnTest.Enabled = false;
            ShowStatus("Probando conexión...", System.Drawing.Color.Blue);
            
            try
            {
                // Verificar Internet
                if (!await NetworkHelpers.IsInternetAvailableAsync())
                {
                    ShowStatus("Sin conexión a Internet", System.Drawing.Color.Red);
                    return;
                }
                
                ShowStatus("Conexión a Internet OK", System.Drawing.Color.Green);
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}", System.Drawing.Color.Red);
            }
            finally
            {
                btnTest.Enabled = true;
            }
        }
        
        private void BtnBackup_Click(object sender, EventArgs e)
        {
            try
            {
                configManager.CreateBackup();
                ShowStatus("Backup creado correctamente", System.Drawing.Color.Green);
            }
            catch (Exception ex)
            {
                ShowStatus($"Error creando backup: {ex.Message}", System.Drawing.Color.Red);
            }
        }
        
        private void BtnRestore_Click(object sender, EventArgs e)
        {
            try
            {
                var backups = configManager.GetAvailableBackups();
                if (backups.Count == 0)
                {
                    MessageBox.Show("No hay backups disponibles", "Información", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                
                // Mostrar lista de backups
                var backupList = string.Join("\n", backups.Select((b, i) => 
                    $"{i + 1}. {System.IO.Path.GetFileName(b)}"));
                
                var result = MessageBox.Show(
                    $"Backups disponibles:\n\n{backupList}\n\n¿Restaurar el más reciente?",
                    "Restaurar Backup",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );
                
                if (result == DialogResult.Yes)
                {
                    if (configManager.RestoreFromBackup(backups[0]))
                    {
                        LoadSettings();
                        ShowStatus("Backup restaurado correctamente", System.Drawing.Color.Green);
                    }
                    else
                    {
                        ShowStatus("Error restaurando backup", System.Drawing.Color.Red);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}", System.Drawing.Color.Red);
            }
        }
        
        private void BtnBrowseDir_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Seleccionar directorio de descargas";
                dialog.SelectedPath = txtDownloadDir.Text;
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtDownloadDir.Text = dialog.SelectedPath;
                }
            }
        }
        
        private void ShowStatus(string message, System.Drawing.Color color)
        {
            if (lblStatus.InvokeRequired)
            {
                lblStatus.Invoke(new Action(() => ShowStatus(message, color)));
                return;
            }
            
            lblStatus.Text = message;
            lblStatus.ForeColor = color;
        }
    }
}
