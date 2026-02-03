using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SlskDown.UI
{
    public class QuickCommandPalette : Form
    {
        private TextBox txtSearch;
        private ListBox lstCommands;
        private List<QuickCommand> allCommands;
        private List<QuickCommand> filteredCommands;
        
        public QuickCommandPalette()
        {
            InitializeCommands();
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.Text = "Comandos Rápidos";
            this.Size = new Size(700, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(40, 40, 40);
            this.ShowInTaskbar = false;
            this.TopMost = true;
            
            // Borde
            this.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(0, 120, 215), 2))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
                }
            };
            
            // Título
            var lblTitle = new Label
            {
                Text = "Comandos Rápidos (Ctrl+Shift+P)",
                Location = new Point(15, 10),
                Size = new Size(670, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold)
            };
            this.Controls.Add(lblTitle);
            
            // Búsqueda
            txtSearch = new TextBox
            {
                Location = new Point(15, 50),
                Size = new Size(670, 30),
                Font = new Font("Segoe UI", 11F),
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            txtSearch.TextChanged += TxtSearch_TextChanged;
            txtSearch.KeyDown += TxtSearch_KeyDown;
            this.Controls.Add(txtSearch);
            
            // Lista de comandos
            lstCommands = new ListBox
            {
                Location = new Point(15, 90),
                Size = new Size(670, 390),
                Font = new Font("Segoe UI", 10F),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 40
            };
            lstCommands.DrawItem += LstCommands_DrawItem;
            lstCommands.DoubleClick += LstCommands_DoubleClick;
            lstCommands.KeyDown += LstCommands_KeyDown;
            this.Controls.Add(lstCommands);
            
            // Cargar comandos
            FilterCommands("");
            
            // Focus en búsqueda
            txtSearch.Focus();
        }
        
        private void InitializeCommands()
        {
            allCommands = new List<QuickCommand>
            {
                // Búsquedas
                new QuickCommand("Buscar: Nueva búsqueda", "search_new", "Ctrl+F"),
                new QuickCommand("Buscar: Aplicar filtro guardado", "search_filter", ""),
                new QuickCommand("Buscar: Guardar filtro actual", "search_save_filter", "Ctrl+S"),
                new QuickCommand("Buscar: Ver historial", "search_history", "Ctrl+H"),
                new QuickCommand("Buscar: Limpiar historial", "search_clear_history", ""),
                new QuickCommand("Buscar: Nueva pestaña", "search_new_tab", "Ctrl+T"),
                new QuickCommand("Buscar: Cerrar pestaña", "search_close_tab", "Ctrl+W"),
                
                // Descargas
                new QuickCommand("Descargas: Ver cola", "downloads_view", "Ctrl+D"),
                new QuickCommand("Descargas: Cambiar prioridad a Alta", "downloads_priority_high", ""),
                new QuickCommand("Descargas: Cambiar prioridad a Normal", "downloads_priority_normal", ""),
                new QuickCommand("Descargas: Cambiar prioridad a Baja", "downloads_priority_low", ""),
                new QuickCommand("Descargas: Pausar todas", "downloads_pause_all", ""),
                new QuickCommand("Descargas: Reanudar todas", "downloads_resume_all", ""),
                new QuickCommand("Descargas: Limpiar completadas", "downloads_clear_completed", ""),
                new QuickCommand("Descargas: Reintentar fallidas", "downloads_retry_failed", ""),
                
                // Estadísticas
                new QuickCommand("Estadísticas: Ver dashboard", "stats_dashboard", "Ctrl+Shift+S"),
                new QuickCommand("Estadísticas: Exportar a HTML", "stats_export_html", ""),
                new QuickCommand("Estadísticas: Exportar a CSV", "stats_export_csv", ""),
                new QuickCommand("Estadísticas: Exportar a JSON", "stats_export_json", ""),
                new QuickCommand("Estadísticas: Ver salud de red", "stats_network_health", ""),
                
                // Configuración
                new QuickCommand("Configuración: Abrir panel avanzado", "config_advanced", "Ctrl+,"),
                new QuickCommand("Configuración: Red y protocolo", "config_network", ""),
                new QuickCommand("Configuración: Búsquedas", "config_search", ""),
                new QuickCommand("Configuración: Descargas", "config_downloads", ""),
                new QuickCommand("Configuración: Seguridad", "config_security", ""),
                new QuickCommand("Configuración: Plugins y temas", "config_extensions", ""),
                
                // Temas
                new QuickCommand("Tema: Cambiar a Dark Modern", "theme_dark", ""),
                new QuickCommand("Tema: Cambiar a Light", "theme_light", ""),
                new QuickCommand("Tema: Cambiar a High Contrast", "theme_contrast", ""),
                new QuickCommand("Tema: Abrir carpeta de temas", "theme_folder", ""),
                new QuickCommand("Tema: Recargar temas", "theme_reload", ""),
                
                // Plugins
                new QuickCommand("Plugins: Ver cargados", "plugins_view", ""),
                new QuickCommand("Plugins: Recargar todos", "plugins_reload", ""),
                new QuickCommand("Plugins: Abrir carpeta", "plugins_folder", ""),
                
                // Backup
                new QuickCommand("Backup: Crear ahora", "backup_create", ""),
                new QuickCommand("Backup: Restaurar", "backup_restore", ""),
                new QuickCommand("Backup: Ver disponibles", "backup_list", ""),
                new QuickCommand("Backup: Abrir carpeta", "backup_folder", ""),
                
                // Compartidos
                new QuickCommand("Compartidos: Rescanear ahora", "shares_rescan", "F5"),
                new QuickCommand("Compartidos: Configurar exclusiones", "shares_exclusions", ""),
                new QuickCommand("Compartidos: Ver estadísticas", "shares_stats", ""),
                
                // Red
                new QuickCommand("Red: Ver estado de conexión", "network_status", ""),
                new QuickCommand("Red: Reconectar", "network_reconnect", ""),
                new QuickCommand("Red: Desconectar", "network_disconnect", ""),
                new QuickCommand("Red: Ver salud de red", "network_health", ""),
                
                // Usuarios
                new QuickCommand("Usuarios: Ver notas", "users_notes", ""),
                new QuickCommand("Usuarios: Gestionar amigos", "users_friends", ""),
                new QuickCommand("Usuarios: Ver bloqueados", "users_blocked", ""),
                new QuickCommand("Usuarios: Buscar similares", "users_similar", ""),
                
                // UI
                new QuickCommand("UI: Guardar layout actual", "ui_save_layout", "Ctrl+Shift+L"),
                new QuickCommand("UI: Cargar layout", "ui_load_layout", ""),
                new QuickCommand("UI: Restaurar layout por defecto", "ui_reset_layout", ""),
                
                // Ayuda
                new QuickCommand("Ayuda: Ver ayuda contextual", "help_context", "F1"),
                new QuickCommand("Ayuda: Ver atajos de teclado", "help_shortcuts", ""),
                new QuickCommand("Ayuda: Acerca de", "help_about", ""),
                new QuickCommand("Ayuda: Abrir documentación", "help_docs", "")
            };
        }
        
        private void FilterCommands(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                filteredCommands = allCommands.ToList();
            }
            else
            {
                var lowerFilter = filter.ToLower();
                filteredCommands = allCommands
                    .Where(c => c.Name.ToLower().Contains(lowerFilter) || 
                               c.Id.ToLower().Contains(lowerFilter))
                    .ToList();
            }
            
            lstCommands.Items.Clear();
            foreach (var cmd in filteredCommands)
            {
                lstCommands.Items.Add(cmd);
            }
            
            if (lstCommands.Items.Count > 0)
            {
                lstCommands.SelectedIndex = 0;
            }
        }
        
        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            FilterCommands(txtSearch.Text);
        }
        
        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down && lstCommands.Items.Count > 0)
            {
                lstCommands.Focus();
                lstCommands.SelectedIndex = 0;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
            else if (e.KeyCode == Keys.Enter && lstCommands.SelectedItem != null)
            {
                ExecuteCommand((QuickCommand)lstCommands.SelectedItem);
            }
        }
        
        private void LstCommands_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && lstCommands.SelectedItem != null)
            {
                ExecuteCommand((QuickCommand)lstCommands.SelectedItem);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }
        
        private void LstCommands_DoubleClick(object sender, EventArgs e)
        {
            if (lstCommands.SelectedItem != null)
            {
                ExecuteCommand((QuickCommand)lstCommands.SelectedItem);
            }
        }
        
        private void LstCommands_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            
            var cmd = (QuickCommand)lstCommands.Items[e.Index];
            
            // Fondo
            var bgColor = (e.State & DrawItemState.Selected) == DrawItemState.Selected
                ? Color.FromArgb(0, 120, 215)
                : Color.FromArgb(45, 45, 45);
            
            using (var brush = new SolidBrush(bgColor))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
            
            // Nombre del comando
            using (var font = new Font("Segoe UI", 10F))
            using (var brush = new SolidBrush(Color.White))
            {
                e.Graphics.DrawString(cmd.Name, font, brush, e.Bounds.X + 10, e.Bounds.Y + 5);
            }
            
            // Atajo de teclado
            if (!string.IsNullOrEmpty(cmd.Shortcut))
            {
                using (var font = new Font("Segoe UI", 9F))
                using (var brush = new SolidBrush(Color.LightGray))
                {
                    var size = e.Graphics.MeasureString(cmd.Shortcut, font);
                    e.Graphics.DrawString(cmd.Shortcut, font, brush, 
                        e.Bounds.Right - size.Width - 10, e.Bounds.Y + 8);
                }
            }
        }
        
        private void ExecuteCommand(QuickCommand cmd)
        {
            this.DialogResult = DialogResult.OK;
            this.Tag = cmd.Id;
            this.Close();
        }
        
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
    
    public class QuickCommand
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Shortcut { get; set; }
        
        public QuickCommand(string name, string id, string shortcut)
        {
            Name = name;
            Id = id;
            Shortcut = shortcut;
        }
        
        public override string ToString()
        {
            return Name;
        }
    }
}
