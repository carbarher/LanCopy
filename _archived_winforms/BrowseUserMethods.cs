using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SlskDown.Core.Browse;

namespace SlskDown
{
    /// <summary>
    /// Métodos para explorar archivos compartidos de usuarios
    /// </summary>
    public partial class MainForm
    {
        private UserBrowser userBrowser;
        
        /// <summary>
        /// Maneja el click del botón "Explorar Usuario" (LEGACY - usar BrowseUserFiles en MainForm.cs)
        /// </summary>
        private async Task BrowseUserButton_Click_Legacy()
        {
            try
            {
                // Obtener usuario seleccionado del ListView de resultados
                if (lvResults.SelectedItems.Count == 0)
                {
                    MessageBox.Show("Selecciona un resultado para explorar al usuario.", "Sin selección", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                
                // Solo explorar el primer usuario seleccionado
                var selectedItem = lvResults.SelectedItems[0];
                var username = selectedItem.SubItems[2].Text; // Columna 2 = Usuario
                
                if (string.IsNullOrWhiteSpace(username))
                {
                    MessageBox.Show("No se pudo obtener el nombre de usuario.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                // Mostrar ventana de exploración
                await ShowUserBrowseWindowAsync(username);
            }
            catch (Exception ex)
            {
                Log($"❌ Error en BrowseUserButton_Click: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// Muestra ventana con los archivos compartidos del usuario
        /// </summary>
        private async Task ShowUserBrowseWindowAsync(string username)
        {
            var browseForm = new Form
            {
                Text = $"📂 Archivos de {username}",
                Size = new Size(1000, 700),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(30, 30, 30),
                MinimizeBox = true,
                MaximizeBox = true
            };
            
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Estado
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Búsqueda
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // ListView
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Botones
            
            // Panel de estado
            var statusLabel = new Label
            {
                Text = "🔄 Cargando archivos...",
                Dock = DockStyle.Fill,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Padding = new Padding(5)
            };
            mainLayout.Controls.Add(statusLabel, 0, 0);
            
            // Panel de búsqueda
            var searchPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 40,
                Padding = new Padding(5)
            };
            
            var txtSearch = new TextBox
            {
                Location = new Point(5, 8),
                Width = 300,
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White
            };
            txtSearch.PlaceholderText = "Buscar en archivos...";
            searchPanel.Controls.Add(txtSearch);
            
            var cmbFilter = new ComboBox
            {
                Location = new Point(315, 8),
                Width = 150,
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbFilter.Items.AddRange(new object[] { "Todos", "Audio", "Video", "Documentos", "Imágenes", "Otros" });
            cmbFilter.SelectedIndex = 0;
            searchPanel.Controls.Add(cmbFilter);
            
            mainLayout.Controls.Add(searchPanel, 0, 1);
            
            // ListView de archivos
            var lvFiles = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };
            
            lvFiles.Columns.Add("Archivo", 400);
            lvFiles.Columns.Add("Carpeta", 300);
            lvFiles.Columns.Add("Tamaño", 100);
            lvFiles.Columns.Add("Extensión", 80);
            
            mainLayout.Controls.Add(lvFiles, 0, 2);
            
            // Panel de botones
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Height = 50,
                Padding = new Padding(5)
            };
            
            var btnDownloadSelected = new Button
            {
                Text = "⬇️ Descargar Seleccionados",
                AutoSize = true,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Padding = new Padding(10, 5, 10, 5),
                Margin = new Padding(0, 0, 10, 0)
            };
            buttonPanel.Controls.Add(btnDownloadSelected);
            
            var btnDownloadAll = new Button
            {
                Text = "⬇️ Descargar Todo",
                AutoSize = true,
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Padding = new Padding(10, 5, 10, 5),
                Margin = new Padding(0, 0, 10, 0)
            };
            buttonPanel.Controls.Add(btnDownloadAll);
            
            var btnClose = new Button
            {
                Text = "❌ Cerrar",
                AutoSize = true,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9),
                Padding = new Padding(10, 5, 10, 5)
            };
            btnClose.Click += (s, e) => browseForm.Close();
            buttonPanel.Controls.Add(btnClose);
            
            mainLayout.Controls.Add(buttonPanel, 0, 3);
            
            browseForm.Controls.Add(mainLayout);
            
            // Cargar archivos en segundo plano
            List<BrowseFile> allFiles = new List<BrowseFile>();
            
            _ = Task.Run(async () =>
            {
                try
                {
                    // Inicializar UserBrowser si es necesario
                    if (userBrowser == null)
                        userBrowser = new UserBrowser(client);
                    
                    Log($"Explorando archivos de {username}...");
                    
                    var browse = await userBrowser.BrowseUserAsync(username);
                    
                    // Extraer todos los archivos de todos los directorios
                    allFiles = browse.Directories.SelectMany(d => d.Files).ToList();
                    
                    // Actualizar UI
                    if (browseForm.InvokeRequired)
                    {
                        browseForm.Invoke(new Action(() =>
                        {
                            statusLabel.Text = $"{browse.TotalFiles:N0} archivos | {browse.Directories.Count:N0} carpetas | {FormatFileSize(browse.TotalSize)}";
                            statusLabel.ForeColor = Color.LimeGreen;
                            
                            lvFiles.BeginUpdate();
                            foreach (var file in allFiles)
                            {
                                var item = new ListViewItem(file.FileName);
                                item.SubItems.Add(file.Directory);
                                item.SubItems.Add(FormatFileSize(file.Size));
                                item.SubItems.Add(file.Extension);
                                item.Tag = file;
                                lvFiles.Items.Add(item);
                            }
                            lvFiles.EndUpdate();
                            
                            Log($"Exploración completada: {username} - {browse.TotalFiles} archivos");
                        }));
                    }
                }
                catch (Exception ex)
                {
                    if (browseForm.InvokeRequired)
                    {
                        browseForm.Invoke(new Action(() =>
                        {
                            statusLabel.Text = $"❌ Error: {ex.Message}";
                            statusLabel.ForeColor = Color.Red;
                        }));
                    }
                    
                    Log($"❌ Error explorando {username}: {ex.Message}");
                }
            });
            
            // Búsqueda en tiempo real
            txtSearch.TextChanged += (s, e) =>
            {
                var searchText = txtSearch.Text.ToLower();
                var filter = cmbFilter.SelectedItem?.ToString() ?? "Todos";
                
                lvFiles.BeginUpdate();
                lvFiles.Items.Clear();
                
                var filtered = allFiles.Where(f =>
                {
                    bool matchesSearch = string.IsNullOrEmpty(searchText) || 
                                        f.FileName.ToLower().Contains(searchText) ||
                                        f.Directory.ToLower().Contains(searchText);
                    
                    bool matchesFilter = filter == "Todos" ||
                        (filter == "Audio" && IsAudioFile(f.Extension)) ||
                        (filter == "Video" && IsVideoFile(f.Extension)) ||
                        (filter == "Documentos" && IsDocumentFile(f.Extension)) ||
                        (filter == "Imágenes" && IsImageFile(f.Extension));
                    
                    return matchesSearch && matchesFilter;
                }).ToList();
                
                foreach (var file in filtered)
                {
                    var item = new ListViewItem(file.FileName);
                    item.SubItems.Add(file.Directory);
                    item.SubItems.Add(FormatFileSize(file.Size));
                    item.SubItems.Add(file.Extension);
                    item.Tag = file;
                    lvFiles.Items.Add(item);
                }
                
                lvFiles.EndUpdate();
            };
            
            cmbFilter.SelectedIndexChanged += (s, e) => txtSearch.Text = txtSearch.Text; // Trigger search
            
            // Descargar seleccionados
            btnDownloadSelected.Click += async (s, e) =>
            {
                if (lvFiles.SelectedItems.Count == 0)
                {
                    MessageBox.Show("Selecciona al menos un archivo", "Descargar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                
                foreach (ListViewItem item in lvFiles.SelectedItems)
                {
                    var file = item.Tag as BrowseFile;
                    if (file != null)
                    {
                        await DownloadBrowseFileAsync(username, file);
                    }
                }
                
                MessageBox.Show($"{lvFiles.SelectedItems.Count} archivos agregados a la cola de descarga", "Descargar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            
            // Descargar todo
            btnDownloadAll.Click += async (s, e) =>
            {
                var result = MessageBox.Show(
                    $"¿Descargar TODOS los {allFiles.Count} archivos de {username}?\n\nEsto puede tardar mucho tiempo.",
                    "Confirmar descarga masiva",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );
                
                if (result == DialogResult.Yes)
                {
                    foreach (var file in allFiles)
                    {
                        await DownloadBrowseFileAsync(username, file);
                    }
                    
                    MessageBox.Show($"{allFiles.Count} archivos agregados a la cola de descarga", "Descargar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            
            browseForm.ShowDialog();
        }
        
        /// <summary>
        /// Descarga un archivo desde el browse de un usuario
        /// </summary>
        private async Task DownloadBrowseFileAsync(string username, BrowseFile file)
        {
            try
            {
                var fullPath = System.IO.Path.Combine(file.Directory, file.FileName);
                
                Log($"⬇️ Descargando: {file.FileName} de {username}");
                
                // Iniciar descarga directamente
                var remotePath = fullPath.Replace("\\", "/");
                var localPath = System.IO.Path.Combine(downloadDir ?? "Downloads", file.FileName);
                
                await client.DownloadAsync(
                    username,
                    remotePath,
                    () => Task.FromResult<Stream>(new FileStream(localPath, FileMode.Create))
                );
            }
            catch (Exception ex)
            {
                Log($"❌ Error descargando {file.FileName}: {ex.Message}");
            }
        }
        
        // Métodos auxiliares para filtros
        private bool IsAudioFile(string ext)
        {
            var audioExts = new[] { ".mp3", ".flac", ".wav", ".m4a", ".aac", ".ogg", ".wma" };
            return audioExts.Contains(ext.ToLower());
        }
        
        private bool IsVideoFile(string ext)
        {
            var videoExts = new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm" };
            return videoExts.Contains(ext.ToLower());
        }
        
        private bool IsImageFile(string ext)
        {
            var imgExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg" };
            return imgExts.Contains(ext.ToLower());
        }
    }
}
