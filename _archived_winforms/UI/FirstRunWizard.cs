using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SlskDown.UI
{
    public class FirstRunWizard : Form
    {
        private Panel currentPanel;
        private int currentStep = 0;
        private Button btnNext;
        private Button btnBack;
        private Button btnFinish;
        private Label lblStep;
        
        public string SharedFolder { get; private set; }
        public string DownloadFolder { get; private set; }
        public string SelectedTheme { get; private set; } = "Dark Modern";
        public bool EnableNotifications { get; private set; } = true;
        public bool EnableAutoBackup { get; private set; } = true;
        
        public FirstRunWizard()
        {
            InitializeComponent();
            ShowStep(0);
        }
        
        private void InitializeComponent()
        {
            this.Text = "Asistente de Configuración Inicial - SlskDown";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(30, 30, 30);
            
            // Label de paso
            lblStep = new Label
            {
                Location = new Point(20, 20),
                Size = new Size(760, 30),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 10F),
                Text = "Paso 1 de 5"
            };
            this.Controls.Add(lblStep);
            
            // Botones
            btnBack = new Button
            {
                Text = "← Atrás",
                Location = new Point(480, 520),
                Size = new Size(90, 35),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F),
                Enabled = false
            };
            btnBack.Click += (s, e) => ShowStep(currentStep - 1);
            this.Controls.Add(btnBack);
            
            btnNext = new Button
            {
                Text = "Siguiente →",
                Location = new Point(580, 520),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F)
            };
            btnNext.Click += (s, e) => ShowStep(currentStep + 1);
            this.Controls.Add(btnNext);
            
            btnFinish = new Button
            {
                Text = "Finalizar",
                Location = new Point(690, 520),
                Size = new Size(90, 35),
                BackColor = Color.FromArgb(0, 150, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F),
                Visible = false
            };
            btnFinish.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };
            this.Controls.Add(btnFinish);
        }
        
        private void ShowStep(int step)
        {
            if (step < 0 || step > 5) return;
            
            currentStep = step;
            lblStep.Text = $"Paso {step + 1} de 6";
            
            // Remover panel actual
            if (currentPanel != null)
            {
                this.Controls.Remove(currentPanel);
                currentPanel.Dispose();
            }
            
            // Crear nuevo panel
            currentPanel = step switch
            {
                0 => CreateWelcomePanel(),
                1 => CreateSharedFoldersPanel(),
                2 => CreateDownloadFoldersPanel(),
                3 => CreateThemePanel(),
                4 => CreateNotificationsPanel(),
                5 => CreateSummaryPanel(),
                _ => CreateWelcomePanel()
            };
            
            this.Controls.Add(currentPanel);
            
            // Actualizar botones
            btnBack.Enabled = step > 0;
            btnNext.Visible = step < 5;
            btnFinish.Visible = step == 5;
        }
        
        private Panel CreateWelcomePanel()
        {
            var panel = new Panel
            {
                Location = new Point(20, 60),
                Size = new Size(760, 440),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            
            var lblTitle = new Label
            {
                Text = "¡Bienvenido a SlskDown!",
                Location = new Point(20, 20),
                Size = new Size(720, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            panel.Controls.Add(lblTitle);
            
            var lblSubtitle = new Label
            {
                Text = "El cliente Soulseek más avanzado y completo",
                Location = new Point(20, 80),
                Size = new Size(720, 30),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 12F),
                TextAlign = ContentAlignment.MiddleCenter
            };
            panel.Controls.Add(lblSubtitle);
            
            var lblFeatures = new Label
            {
                Text = @"✨ Características principales:

• 55 características avanzadas implementadas
• Sistema de plugins y temas personalizables
• Virtual scrolling para listas infinitas
• Estadísticas avanzadas con heatmaps
• Backup automático de configuración
• Cifrado de mensajes RSA 2048
• Monitor de salud de red en tiempo real
• Sistema de prioridades de 5 niveles
• Y mucho más...

Este asistente te ayudará a configurar SlskDown en pocos pasos.",
                Location = new Point(50, 130),
                Size = new Size(660, 280),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F)
            };
            panel.Controls.Add(lblFeatures);
            
            return panel;
        }
        
        private Panel CreateSharedFoldersPanel()
        {
            var panel = new Panel
            {
                Location = new Point(20, 60),
                Size = new Size(760, 440),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            
            var lblTitle = new Label
            {
                Text = "Configurar Carpetas Compartidas",
                Location = new Point(20, 20),
                Size = new Size(720, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold)
            };
            panel.Controls.Add(lblTitle);
            
            var lblDesc = new Label
            {
                Text = "Selecciona las carpetas que deseas compartir en la red Soulseek.\nSe aplicarán exclusiones automáticas para archivos del sistema.",
                Location = new Point(20, 70),
                Size = new Size(720, 40),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 10F)
            };
            panel.Controls.Add(lblDesc);
            
            var txtFolder = new TextBox
            {
                Location = new Point(20, 130),
                Size = new Size(600, 30),
                Font = new Font("Segoe UI", 11F),
                Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Shared")
            };
            panel.Controls.Add(txtFolder);
            
            var btnBrowse = new Button
            {
                Text = "Examinar...",
                Location = new Point(630, 130),
                Size = new Size(110, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnBrowse.Click += (s, e) =>
            {
                var dialog = new FolderBrowserDialog { SelectedPath = txtFolder.Text };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtFolder.Text = dialog.SelectedPath;
                    SharedFolder = dialog.SelectedPath;
                }
            };
            panel.Controls.Add(btnBrowse);
            
            var lblExclusions = new Label
            {
                Text = @"Exclusiones automáticas activas:

• Archivos temporales (*.tmp, *.temp, *.cache)
• Archivos del sistema (Thumbs.db, .DS_Store, desktop.ini)
• Carpetas del sistema (Windows, Program Files, $RECYCLE.BIN)
• Archivos de desarrollo (.git, .svn, node_modules)
• Descargas parciales (*.partial, *.crdownload)",
                Location = new Point(20, 180),
                Size = new Size(720, 150),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9F)
            };
            panel.Controls.Add(lblExclusions);
            
            var chkAutoRescan = new CheckBox
            {
                Text = "Habilitar rescanning automático (detecta cambios en carpetas)",
                Location = new Point(20, 350),
                Size = new Size(720, 25),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F),
                Checked = true
            };
            panel.Controls.Add(chkAutoRescan);
            
            SharedFolder = txtFolder.Text;
            
            return panel;
        }
        
        private Panel CreateDownloadFoldersPanel()
        {
            var panel = new Panel
            {
                Location = new Point(20, 60),
                Size = new Size(760, 440),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            
            var lblTitle = new Label
            {
                Text = "Configurar Carpeta de Descargas",
                Location = new Point(20, 20),
                Size = new Size(720, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold)
            };
            panel.Controls.Add(lblTitle);
            
            var lblDesc = new Label
            {
                Text = "Selecciona dónde se guardarán tus descargas.",
                Location = new Point(20, 70),
                Size = new Size(720, 30),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 10F)
            };
            panel.Controls.Add(lblDesc);
            
            var txtFolder = new TextBox
            {
                Location = new Point(20, 120),
                Size = new Size(600, 30),
                Font = new Font("Segoe UI", 11F),
                Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "SlskDown")
            };
            panel.Controls.Add(txtFolder);
            
            var btnBrowse = new Button
            {
                Text = "Examinar...",
                Location = new Point(630, 120),
                Size = new Size(110, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnBrowse.Click += (s, e) =>
            {
                var dialog = new FolderBrowserDialog { SelectedPath = txtFolder.Text };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtFolder.Text = dialog.SelectedPath;
                    DownloadFolder = dialog.SelectedPath;
                }
            };
            panel.Controls.Add(btnBrowse);
            
            var lblSettings = new Label
            {
                Text = @"Configuración de descargas:

• Sistema de prioridades de 5 niveles
• Retry inteligente con backoff exponencial
• Verificación de integridad MD5
• Balanceo de carga automático
• Máximo 2 descargas por usuario
• Fuentes alternativas automáticas",
                Location = new Point(20, 170),
                Size = new Size(720, 150),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 10F)
            };
            panel.Controls.Add(lblSettings);
            
            DownloadFolder = txtFolder.Text;
            
            return panel;
        }
        
        private Panel CreateThemePanel()
        {
            var panel = new Panel
            {
                Location = new Point(20, 60),
                Size = new Size(760, 440),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            
            var lblTitle = new Label
            {
                Text = "Seleccionar Tema",
                Location = new Point(20, 20),
                Size = new Size(720, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold)
            };
            panel.Controls.Add(lblTitle);
            
            var lblDesc = new Label
            {
                Text = "Elige el tema que prefieras. Podrás cambiarlo en cualquier momento.",
                Location = new Point(20, 70),
                Size = new Size(720, 30),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 10F)
            };
            panel.Controls.Add(lblDesc);
            
            // Dark Modern
            var radioDark = new RadioButton
            {
                Text = "Dark Modern (Recomendado)",
                Location = new Point(40, 120),
                Size = new Size(300, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F),
                Checked = true
            };
            radioDark.CheckedChanged += (s, e) => { if (radioDark.Checked) SelectedTheme = "Dark Modern"; };
            panel.Controls.Add(radioDark);
            
            var lblDark = new Label
            {
                Text = "Tema oscuro moderno con colores vibrantes",
                Location = new Point(60, 150),
                Size = new Size(680, 20),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9F)
            };
            panel.Controls.Add(lblDark);
            
            // Light
            var radioLight = new RadioButton
            {
                Text = "Light",
                Location = new Point(40, 190),
                Size = new Size(300, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F)
            };
            radioLight.CheckedChanged += (s, e) => { if (radioLight.Checked) SelectedTheme = "Light"; };
            panel.Controls.Add(radioLight);
            
            var lblLight = new Label
            {
                Text = "Tema claro para ambientes bien iluminados",
                Location = new Point(60, 220),
                Size = new Size(680, 20),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9F)
            };
            panel.Controls.Add(lblLight);
            
            // High Contrast
            var radioContrast = new RadioButton
            {
                Text = "High Contrast",
                Location = new Point(40, 260),
                Size = new Size(300, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F)
            };
            radioContrast.CheckedChanged += (s, e) => { if (radioContrast.Checked) SelectedTheme = "High Contrast"; };
            panel.Controls.Add(radioContrast);
            
            var lblContrast = new Label
            {
                Text = "Alto contraste para mejor legibilidad",
                Location = new Point(60, 290),
                Size = new Size(680, 20),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9F)
            };
            panel.Controls.Add(lblContrast);
            
            return panel;
        }
        
        private Panel CreateNotificationsPanel()
        {
            var panel = new Panel
            {
                Location = new Point(20, 60),
                Size = new Size(760, 440),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            
            var lblTitle = new Label
            {
                Text = "Configurar Notificaciones",
                Location = new Point(20, 20),
                Size = new Size(720, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold)
            };
            panel.Controls.Add(lblTitle);
            
            var chkNotifications = new CheckBox
            {
                Text = "Habilitar notificaciones de escritorio",
                Location = new Point(40, 80),
                Size = new Size(680, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F),
                Checked = true
            };
            chkNotifications.CheckedChanged += (s, e) => EnableNotifications = chkNotifications.Checked;
            panel.Controls.Add(chkNotifications);
            
            var lblNotifTypes = new Label
            {
                Text = @"Tipos de notificaciones:

• Descargas completadas
• Descargas iniciadas
• Mensajes recibidos
• Wishlist matches
• Conexión perdida/restaurada",
                Location = new Point(60, 120),
                Size = new Size(680, 120),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 10F)
            };
            panel.Controls.Add(lblNotifTypes);
            
            var chkBackup = new CheckBox
            {
                Text = "Habilitar backup automático de configuración",
                Location = new Point(40, 260),
                Size = new Size(680, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F),
                Checked = true
            };
            chkBackup.CheckedChanged += (s, e) => EnableAutoBackup = chkBackup.Checked;
            panel.Controls.Add(chkBackup);
            
            var lblBackup = new Label
            {
                Text = "Se guardarán hasta 10 versiones de tu configuración automáticamente.",
                Location = new Point(60, 295),
                Size = new Size(680, 25),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9F)
            };
            panel.Controls.Add(lblBackup);
            
            return panel;
        }
        
        private Panel CreateSummaryPanel()
        {
            var panel = new Panel
            {
                Location = new Point(20, 60),
                Size = new Size(760, 440),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            
            var lblTitle = new Label
            {
                Text = "Configuración Completa",
                Location = new Point(20, 20),
                Size = new Size(720, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold)
            };
            panel.Controls.Add(lblTitle);
            
            var lblSummary = new Label
            {
                Text = $@"Resumen de tu configuración:

Carpeta compartida: {SharedFolder ?? "No configurada"}
Carpeta de descargas: {DownloadFolder ?? "No configurada"}
Tema: {SelectedTheme}
Notificaciones: {(EnableNotifications ? "Habilitadas" : "Deshabilitadas")}
Backup automático: {(EnableAutoBackup ? "Habilitado" : "Deshabilitado")}

¡Todo listo! Haz clic en Finalizar para comenzar a usar SlskDown.

Consejos rápidos:
• Usa Ctrl+Shift+P para abrir la paleta de comandos rápidos
• Presiona F1 para ver ayuda contextual
• Visita el dashboard de estadísticas con Ctrl+Shift+S
• Personaliza atajos de teclado en Configuración > Plugins y Temas",
                Location = new Point(20, 70),
                Size = new Size(720, 350),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F)
            };
            panel.Controls.Add(lblSummary);
            
            return panel;
        }
    }
}
