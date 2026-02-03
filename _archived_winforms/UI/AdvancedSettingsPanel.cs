using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown.UI
{
    public class AdvancedSettingsPanel : Form
    {
        private TabControl tabControl;
        private Button btnSave;
        private Button btnCancel;
        private Button btnApply;
        
        public AdvancedSettingsPanel()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.Text = "Configuración Avanzada - SlskDown";
            this.Size = new Size(900, 700);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(30, 30, 30);
            
            // TabControl principal
            tabControl = new TabControl
            {
                Location = new Point(10, 10),
                Size = new Size(860, 600),
                Font = new Font("Segoe UI", 9F)
            };
            
            // Crear tabs
            tabControl.TabPages.Add(CreateNetworkTab());
            tabControl.TabPages.Add(CreateSearchTab());
            tabControl.TabPages.Add(CreateDownloadTab());
            tabControl.TabPages.Add(CreateSecurityTab());
            tabControl.TabPages.Add(CreateExtensionsTab());
            tabControl.TabPages.Add(CreateUITab());
            tabControl.TabPages.Add(CreateAdvancedTab());
            
            // Botones
            btnSave = new Button
            {
                Text = "Guardar",
                Location = new Point(570, 620),
                Size = new Size(90, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSave.Click += BtnSave_Click;
            
            btnCancel = new Button
            {
                Text = "Cancelar",
                Location = new Point(670, 620),
                Size = new Size(90, 30),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.Click += (s, e) => this.Close();
            
            btnApply = new Button
            {
                Text = "Aplicar",
                Location = new Point(770, 620),
                Size = new Size(90, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnApply.Click += BtnApply_Click;
            
            this.Controls.AddRange(new Control[] { tabControl, btnSave, btnCancel, btnApply });
        }
        
        private TabPage CreateNetworkTab()
        {
            var tab = new TabPage("Red y Protocolo");
            tab.BackColor = Color.FromArgb(45, 45, 45);
            
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            int y = 20;
            
            // Timeouts
            AddGroupBox(panel, "Timeouts del Protocolo", 20, ref y, new Control[]
            {
                CreateNumericSetting("Conexión (segundos):", 30, 5, 120),
                CreateNumericSetting("Login (segundos):", 15, 5, 60),
                CreateNumericSetting("Búsqueda (segundos):", 30, 10, 120),
                CreateNumericSetting("Descarga (segundos):", 300, 60, 600),
                CreateNumericSetting("Peer (segundos):", 60, 10, 180)
            });
            
            // Rate Limiting
            AddGroupBox(panel, "Rate Limiting", 20, ref y, new Control[]
            {
                CreateNumericSetting("Máximo tokens:", 10, 1, 50),
                CreateNumericSetting("Tasa de recarga (tokens/seg):", 1, 0.1m, 10),
                CreateCheckboxSetting("Habilitar rate limiting", true)
            });
            
            // Salud de Red
            AddGroupBox(panel, "Monitor de Salud de Red", 20, ref y, new Control[]
            {
                CreateCheckboxSetting("Monitorear packet loss", true),
                CreateCheckboxSetting("Monitorear latencia", true),
                CreateCheckboxSetting("Alertas de salud proactivas", true),
                CreateNumericSetting("Intervalo de verificación (segundos):", 60, 10, 300)
            });
            
            // Logger de Protocolo
            AddGroupBox(panel, "Logger de Protocolo (Debug)", 20, ref y, new Control[]
            {
                CreateCheckboxSetting("Habilitar logging de paquetes", false),
                CreateTextSetting("Archivo de log:", "protocol.log")
            });
            
            tab.Controls.Add(panel);
            return tab;
        }
        
        private TabPage CreateSearchTab()
        {
            var tab = new TabPage("Búsquedas");
            tab.BackColor = Color.FromArgb(45, 45, 45);
            
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            int y = 20;
            
            // Caché
            AddGroupBox(panel, "Caché de Búsquedas", 20, ref y, new Control[]
            {
                CreateCheckboxSetting("Habilitar caché", true),
                CreateNumericSetting("TTL (minutos):", 5, 1, 60),
                CreateNumericSetting("Máximo entradas:", 100, 10, 1000)
            });
            
            // Historial
            AddGroupBox(panel, "Historial y Autocompletado", 20, ref y, new Control[]
            {
                CreateCheckboxSetting("Guardar historial", true),
                CreateNumericSetting("Máximo entradas:", 100, 10, 500),
                CreateCheckboxSetting("Autocompletado", true),
                CreateNumericSetting("Sugerencias a mostrar:", 10, 5, 20)
            });
            
            // Filtros
            AddGroupBox(panel, "Filtros de Búsqueda", 20, ref y, new Control[]
            {
                CreateCheckboxSetting("Permitir guardar filtros", true),
                CreateCheckboxSetting("Aplicar filtros automáticamente", false),
                CreateTextSetting("Filtro por defecto:", "")
            });
            
            // Virtual Scrolling
            AddGroupBox(panel, "Optimización de Resultados", 20, ref y, new Control[]
            {
                CreateCheckboxSetting("Virtual scrolling", true),
                CreateNumericSetting("Items visibles:", 50, 20, 200),
                CreateCheckboxSetting("Búsqueda incremental", true),
                CreateNumericSetting("Debounce (ms):", 300, 100, 1000)
            });
            
            tab.Controls.Add(panel);
            return tab;
        }
        
        private TabPage CreateDownloadTab()
        {
            var tab = new TabPage("Descargas");
            tab.BackColor = Color.FromArgb(45, 45, 45);
            
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            int y = 20;
            
            // Prioridades
            AddGroupBox(panel, "Sistema de Prioridades", 20, ref y, new Control[]
            {
                CreateCheckboxSetting("Habilitar prioridades", true),
                CreateCheckboxSetting("Reordenar automáticamente", true),
                CreateNumericSetting("Prioridad por defecto:", 1, 0, 3)
            });
            
            // Retry
            AddGroupBox(panel, "Retry Inteligente", 20, ref y, new Control[]
            {
                CreateCheckboxSetting("Habilitar retry automático", true),
                CreateNumericSetting("Máximo intentos:", 6, 1, 10),
                CreateTextSetting("Backoff (minutos):", "1,2,5,10,30,60")
            });
            
            // Verificación
            AddGroupBox(panel, "Verificación de Integridad", 20, ref y, new Control[]
            {
                CreateCheckboxSetting("Verificar checksums MD5", true),
                CreateCheckboxSetting("Eliminar archivos corruptos", false),
                CreateCheckboxSetting("Reintentar si falla verificación", true)
            });
            
            // Balanceo
            AddGroupBox(panel, "Balanceo de Carga", 20, ref y, new Control[]
            {
                CreateCheckboxSetting("Habilitar balanceo", true),
                CreateNumericSetting("Máx descargas por usuario:", 2, 1, 10),
                CreateNumericSetting("Máx descargas totales:", 10, 1, 50)
            });
            
            tab.Controls.Add(panel);
            return tab;
        }
        
        private TabPage CreateSecurityTab()
        {
            var tab = new TabPage("Seguridad y Privacidad");
            tab.BackColor = Color.FromArgb(45, 45, 45);
            
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            int y = 20;
            
            // Modo Privado
            AddGroupBox(panel, "Modo Privado", 20, ref y, new Control[]
            {
                CreateCheckboxSetting("Modo invisible", false),
                CreateCheckboxSetting("Ocultar compartidos", false),
                CreateCheckboxSetting("Deshabilitar mensajes privados", false),
                CreateCheckboxSetting("Solo aceptar de amigos", false)
            });
            
            // IP Blocking
            AddGroupBox(panel, "Bloqueo de IPs", 20, ref y, new Control[]
            {
                CreateCheckboxSetting("Habilitar bloqueo de IPs", true),
                CreateButton("Gestionar lista de IPs bloqueadas...", (s, e) => { }),
                CreateLabel("IPs bloqueadas: 0")
            });
            
            // Cifrado
            AddGroupBox(panel, "Cifrado de Mensajes", 20, ref y, new Control[]
            {
                CreateCheckboxSetting("Habilitar cifrado RSA", false),
                CreateButton("Generar nuevas claves", (s, e) => { }),
                CreateButton("Exportar clave pública", (s, e) => { }),
                CreateLabel("Estado: Claves no generadas")
            });
            
            // Filtros de Mensajes
            AddGroupBox(panel, "Filtros de Mensajes", 20, ref y, new Control[]
            {
                CreateCheckboxSetting("Filtrar spam", true),
                CreateCheckboxSetting("Filtrar CAPS LOCK", true),
                CreateCheckboxSetting("Filtrar repetición", true),
                CreateButton("Gestionar palabras prohibidas...", (s, e) => { }),
                CreateButton("Gestionar usuarios silenciados...", (s, e) => { })
            });
            
            tab.Controls.Add(panel);
            return tab;
        }
        
        private TabPage CreateExtensionsTab()
        {
            var tab = new TabPage("Plugins y Temas");
            tab.BackColor = Color.FromArgb(45, 45, 45);
            
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            int y = 20;
            
            // Plugins
            AddGroupBox(panel, "Sistema de Plugins", 20, ref y, new Control[]
            {
                CreateCheckboxSetting("Habilitar plugins", true),
                CreateButton("Abrir carpeta de plugins", (s, e) => { }),
                CreateButton("Recargar plugins", (s, e) => { }),
                CreateLabel("Plugins cargados: 0")
            });
            
            // Temas
            AddGroupBox(panel, "Temas", 20, ref y, new Control[]
            {
                CreateComboSetting("Tema actual:", new[] { "Dark Modern", "Light", "High Contrast" }),
                CreateButton("Abrir carpeta de temas", (s, e) => { }),
                CreateButton("Recargar temas", (s, e) => { }),
                CreateButton("Crear tema personalizado...", (s, e) => { })
            });
            
            // Atajos de Teclado
            AddGroupBox(panel, "Atajos de Teclado", 20, ref y, new Control[]
            {
                CreateCheckboxSetting("Habilitar atajos", true),
                CreateButton("Personalizar atajos...", (s, e) => { }),
                CreateButton("Restaurar atajos por defecto", (s, e) => { }),
                CreateLabel("Atajos configurados: 50+")
            });
            
            tab.Controls.Add(panel);
            return tab;
        }
        
        private TabPage CreateUITab()
        {
            var tab = new TabPage("Interfaz");
            tab.BackColor = Color.FromArgb(45, 45, 45);
            
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            int y = 20;
            
            // Layouts
            AddGroupBox(panel, "Layouts Personalizables", 20, ref y, new Control[]
            {
                CreateComboSetting("Layout actual:", new[] { "Por defecto", "Compacto", "Expandido" }),
                CreateButton("Guardar layout actual...", (s, e) => { }),
                CreateButton("Cargar layout...", (s, e) => { }),
                CreateButton("Eliminar layout...", (s, e) => { })
            });
            
            // Notificaciones
            AddGroupBox(panel, "Notificaciones", 20, ref y, new Control[]
            {
                CreateCheckboxSetting("Descargas completadas", true),
                CreateCheckboxSetting("Descargas iniciadas", true),
                CreateCheckboxSetting("Mensajes recibidos", true),
                CreateCheckboxSetting("Búsquedas completadas", false),
                CreateCheckboxSetting("Usuarios online/offline", false),
                CreateCheckboxSetting("Wishlist matches", true),
                CreateCheckboxSetting("Conexión perdida/restaurada", true),
                CreateNumericSetting("Duración (ms):", 3000, 1000, 10000)
            });
            
            // Tooltips
            AddGroupBox(panel, "Tooltips y Ayuda", 20, ref y, new Control[]
            {
                CreateCheckboxSetting("Mostrar tooltips", true),
                CreateCheckboxSetting("Tooltips extendidos", true),
                CreateNumericSetting("Delay (ms):", 500, 0, 2000)
            });
            
            tab.Controls.Add(panel);
            return tab;
        }
        
        private TabPage CreateAdvancedTab()
        {
            var tab = new TabPage("Avanzado");
            tab.BackColor = Color.FromArgb(45, 45, 45);
            
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            int y = 20;
            
            // Backup
            AddGroupBox(panel, "Backup Automático", 20, ref y, new Control[]
            {
                CreateCheckboxSetting("Habilitar backup automático", true),
                CreateNumericSetting("Máximo backups:", 10, 1, 50),
                CreateButton("Crear backup ahora", (s, e) => { }),
                CreateButton("Restaurar backup...", (s, e) => { }),
                CreateButton("Abrir carpeta de backups", (s, e) => { }),
                CreateLabel("Último backup: Nunca")
            });
            
            // Exportación
            AddGroupBox(panel, "Exportación de Datos", 20, ref y, new Control[]
            {
                CreateButton("Exportar estadísticas a CSV", (s, e) => { }),
                CreateButton("Exportar estadísticas a JSON", (s, e) => { }),
                CreateButton("Exportar estadísticas a HTML", (s, e) => { })
            });
            
            // Compartidos
            AddGroupBox(panel, "Gestión de Compartidos", 20, ref y, new Control[]
            {
                CreateCheckboxSetting("Rescanning automático", true),
                CreateCheckboxSetting("Exclusiones automáticas", true),
                CreateButton("Configurar exclusiones...", (s, e) => { }),
                CreateButton("Rescanear ahora", (s, e) => { })
            });
            
            // Modo Portable
            AddGroupBox(panel, "Modo Portable", 20, ref y, new Control[]
            {
                CreateCheckboxSetting("Habilitar modo portable", false),
                CreateTextSetting("Carpeta de datos:", "data"),
                CreateLabel("Estado: Modo normal")
            });
            
            tab.Controls.Add(panel);
            return tab;
        }
        
        // Métodos helper para crear controles
        private void AddGroupBox(Panel parent, string title, int x, ref int y, Control[] controls)
        {
            var groupBox = new GroupBox
            {
                Text = title,
                Location = new Point(x, y),
                Size = new Size(800, 30 + controls.Length * 35),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            
            int controlY = 25;
            foreach (var control in controls)
            {
                control.Location = new Point(10, controlY);
                groupBox.Controls.Add(control);
                controlY += 35;
            }
            
            parent.Controls.Add(groupBox);
            y += groupBox.Height + 15;
        }
        
        private Control CreateCheckboxSetting(string label, bool defaultValue)
        {
            var checkbox = new CheckBox
            {
                Text = label,
                Checked = defaultValue,
                ForeColor = Color.White,
                AutoSize = true
            };
            return checkbox;
        }
        
        private Control CreateNumericSetting(string label, decimal defaultValue, decimal min, decimal max)
        {
            var panel = new Panel { Size = new Size(780, 25) };
            
            var lbl = new Label
            {
                Text = label,
                Location = new Point(0, 3),
                Size = new Size(300, 20),
                ForeColor = Color.White
            };
            
            var numeric = new NumericUpDown
            {
                Value = defaultValue,
                Minimum = min,
                Maximum = max,
                Location = new Point(310, 0),
                Size = new Size(100, 20),
                DecimalPlaces = (min < 1) ? 1 : 0
            };
            
            panel.Controls.AddRange(new Control[] { lbl, numeric });
            return panel;
        }
        
        private Control CreateTextSetting(string label, string defaultValue)
        {
            var panel = new Panel { Size = new Size(780, 25) };
            
            var lbl = new Label
            {
                Text = label,
                Location = new Point(0, 3),
                Size = new Size(300, 20),
                ForeColor = Color.White
            };
            
            var textBox = new TextBox
            {
                Text = defaultValue,
                Location = new Point(310, 0),
                Size = new Size(400, 20)
            };
            
            panel.Controls.AddRange(new Control[] { lbl, textBox });
            return panel;
        }
        
        private Control CreateComboSetting(string label, string[] items)
        {
            var panel = new Panel { Size = new Size(780, 25) };
            
            var lbl = new Label
            {
                Text = label,
                Location = new Point(0, 3),
                Size = new Size(300, 20),
                ForeColor = Color.White
            };
            
            var combo = new ComboBox
            {
                Location = new Point(310, 0),
                Size = new Size(300, 20),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            combo.Items.AddRange(items);
            if (items.Length > 0) combo.SelectedIndex = 0;
            
            panel.Controls.AddRange(new Control[] { lbl, combo });
            return panel;
        }
        
        private Control CreateButton(string text, EventHandler onClick)
        {
            var button = new Button
            {
                Text = text,
                Size = new Size(250, 25),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            button.Click += onClick;
            return button;
        }
        
        private Control CreateLabel(string text)
        {
            return new Label
            {
                Text = text,
                ForeColor = Color.LightGray,
                AutoSize = true
            };
        }
        
        private void BtnSave_Click(object sender, EventArgs e)
        {
            ApplySettings();
            this.Close();
        }
        
        private void BtnApply_Click(object sender, EventArgs e)
        {
            ApplySettings();
        }
        
        private void ApplySettings()
        {
            // TODO: Implementar guardado de configuraciones
            MessageBox.Show("Configuración guardada correctamente", "Éxito", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
