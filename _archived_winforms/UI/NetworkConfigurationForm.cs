using System;
using System.Drawing;
using System.Windows.Forms;
using SlskDown.Core;

namespace SlskDown.UI
{
    /// <summary>
    /// Formulario de configuración de red Soulseek
    /// </summary>
    public partial class NetworkConfigurationForm : Form
    {
        private NetworkConfiguration _config;
        
        // Controles
        private GroupBox grpSoulseek;
        private CheckBox chkEnableSoulseek;
        private CheckBox chkSoulseekAutoConnect;
        
        
        private GroupBox grpPreferences;
        private CheckBox chkUseCache;
        private Label lblCacheExpiration;
        private NumericUpDown numCacheExpiration;
        
        private Label lblStatus;
        private Button btnTest;
        private Button btnSave;
        private Button btnCancel;

        public NetworkConfigurationForm()
        {
            InitializeComponent();
            LoadConfiguration();
        }

        private void InitializeComponent()
        {
            this.Text = "CONFIGURACIÓN REDES V17";
            this.Size = new Size(1100, 1000);
            this.MinimumSize = new Size(900, 800);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.StartPosition = FormStartPosition.CenterParent;
            this.AutoScroll = true;
            
            // Aplicar tema oscuro
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;

            int yPos = 30;

            // ============================================
            // Grupo Soulseek
            // ============================================
            grpSoulseek = new GroupBox
            {
                Text = "🔵 Soulseek",
                Location = new Point(40, yPos),
                Size = new Size(1000, 120),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            chkEnableSoulseek = new CheckBox
            {
                Text = "Habilitar Soulseek (siempre activo)",
                Location = new Point(30, 45),
                AutoSize = true,
                Checked = true,
                Enabled = false, // No se puede desmarcar
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            // chkEnableSoulseek.CheckedChanged += OnNetworkEnabledChanged; // No necesario si está deshabilitado

            chkSoulseekAutoConnect = new CheckBox
            {
                Text = "Conectar automáticamente al iniciar",
                Location = new Point(30, 75),
                AutoSize = true,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };

            grpSoulseek.Controls.AddRange(new Control[]
            {
                chkEnableSoulseek, chkSoulseekAutoConnect
            });

            this.Controls.Add(grpSoulseek);
            yPos += 140;

            // ============================================
            // Grupo Preferencias
            // ============================================
            grpPreferences = new GroupBox
            {
                Text = "Preferencias",
                Location = new Point(40, yPos),
                Size = new Size(1000, 120),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            chkUseCache = new CheckBox
            {
                Text = "Usar caché de búsquedas",
                Location = new Point(30, 35),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };

            lblCacheExpiration = new Label
            {
                Text = "Expiración caché (min):",
                Location = new Point(30, 70),
                AutoSize = true,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                TextAlign = ContentAlignment.MiddleLeft
            };

            numCacheExpiration = new NumericUpDown
            {
                Location = new Point(260, 67),
                Size = new Size(130, 30),
                Minimum = 5,
                Maximum = 120,
                Value = 30,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };

            grpPreferences.Controls.AddRange(new Control[]
            {
                chkUseCache, lblCacheExpiration, numCacheExpiration
            });

            this.Controls.Add(grpPreferences);
            yPos += 200;

            // ============================================
            // Status y Botones
            // ============================================
            lblStatus = new Label
            {
                Location = new Point(40, yPos),
                Size = new Size(1000, 35),
                Text = "🔵 Red: Soulseek",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.LightGreen,
                TextAlign = ContentAlignment.MiddleLeft
            };
            this.Controls.Add(lblStatus);
            yPos += 50;

            btnTest = new Button
            {
                Text = "Probar Conexión",
                Location = new Point(40, yPos),
                Size = new Size(280, 55),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnTest.Click += BtnTest_Click;

            btnSave = new Button
            {
                Text = "Guardar",
                Location = new Point(550, yPos),
                Size = new Size(240, 55),
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button
            {
                Text = "Cancelar",
                Location = new Point(810, yPos),
                Size = new Size(230, 55),
                DialogResult = DialogResult.Cancel,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };

            this.Controls.AddRange(new Control[] { btnTest, btnSave, btnCancel });
            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;
        }

        private void LoadConfiguration()
        {
            _config = NetworkConfiguration.Load();

            // Cargar valores Soulseek
            chkEnableSoulseek.Checked = _config.SoulseekEnabled;
            chkSoulseekAutoConnect.Checked = _config.SoulseekAutoConnect;

            // Cargar preferencias
            chkUseCache.Checked = _config.UseCache;
            numCacheExpiration.Value = _config.CacheExpirationMinutes;

            UpdateStatus();
            UpdateControlStates();
        }

        private void OnNetworkEnabledChanged(object sender, EventArgs e)
        {
            UpdateStatus();
            UpdateControlStates();
        }

        private void UpdateControlStates()
        {
            // Habilitar/deshabilitar controles según checkboxes
            // Soulseek siempre habilitado
        }

        private void UpdateStatus()
        {
            lblStatus.Text = "🔵 Red: Soulseek";
            lblStatus.ForeColor = Color.LightGreen;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            // Guardar valores Soulseek
            _config.SoulseekEnabled = chkEnableSoulseek.Checked;
            _config.SoulseekAutoConnect = chkSoulseekAutoConnect.Checked;

            // Guardar preferencias
            _config.UseCache = chkUseCache.Checked;
            _config.CacheExpirationMinutes = (int)numCacheExpiration.Value;

            // Validar
            var (isValid, error) = _config.Validate();
            if (!isValid)
            {
                MessageBox.Show(error, "Error de Validación", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            // Guardar
            _config.Save();

            MessageBox.Show(
                $"Configuración guardada correctamente.\n\n{_config.GetModeDescription()}",
                "Configuración Guardada",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private async void BtnTest_Click(object sender, EventArgs e)
        {
            btnTest.Enabled = false;
            btnTest.Text = "Probando...";

            try
            {
                var results = new System.Text.StringBuilder();
                results.AppendLine("Resultados de Prueba:\n");

                // Probar Soulseek
                results.AppendLine("Soulseek:");
                results.AppendLine("   Habilitado");
                results.AppendLine("   Credenciales configuradas en pantalla principal");

                MessageBox.Show(results.ToString(), "Prueba de Conexión",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                btnTest.Enabled = true;
                btnTest.Text = "Probar Conexión";
            }
        }

        public NetworkConfiguration GetConfiguration()
        {
            return _config;
        }
    }
}
