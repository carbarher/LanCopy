using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SlskDown.Core.Browse;
using SlskDown.Models;

namespace SlskDown
{
    /// <summary>
    /// Partial class de MainForm para funcionalidad de Browse User
    /// </summary>
    public partial class MainForm
    {
        // ============================================================================
        // BROWSE USER - Explorar carpetas y archivos de usuarios
        // ============================================================================
        
        private UserBrowser userBrowser;
        private TreeView tvBrowse;
        private ListView lvBrowseFiles;
        private TextBox txtBrowseUsername;
        private Button btnBrowseUser;
        private Label lblBrowseStats;
        private TextBox txtBrowseSearch;
        private Button btnBrowseSearch;
        private Button btnDownloadSelected;
        private Button btnDownloadFolder;
        private BrowseResult currentBrowseResult;

        private void InitializeUserBrowser()
        {
            try
            {
                userBrowser = new UserBrowser(client);
                userBrowser.OnLog += (message) => Log($"[Browse] {message}");
                Log("✅ UserBrowser inicializado");
            }
            catch (Exception ex)
            {
                Log($"❌ Error inicializando UserBrowser: {ex.Message}");
            }
        }

        private TabPage CreateBrowseTab()
        {
            var tab = new TabPage("Explorar Usuario")
            {
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(10)
            };

            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30)
            };

            // Panel superior - Búsqueda de usuario
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(40, 40, 40),
                Padding = new Padding(10)
            };

            var lblUsername = new Label
            {
                Text = "Usuario:",
                ForeColor = Color.White,
                Location = new Point(10, 15),
                AutoSize = true
            };

            txtBrowseUsername = new TextBox
            {
                Location = new Point(80, 12),
                Width = 200,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            btnBrowseUser = new Button
            {
                Text = "🔍 Explorar",
                Location = new Point(290, 10),
                Width = 100,
                Height = 25,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnBrowseUser.FlatAppearance.BorderSize = 0;
            btnBrowseUser.Click += async (s, e) => await BrowseUserClickAsync();

            lblBrowseStats = new Label
            {
                Location = new Point(10, 45),
                AutoSize = true,
                ForeColor = Color.LightGray,
                Text = "Selecciona un usuario para explorar sus archivos compartidos"
            };

            topPanel.Controls.AddRange(new Control[] { lblUsername, txtBrowseUsername, btnBrowseUser, lblBrowseStats });

            // Panel de búsqueda en browse
            var searchPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(35, 35, 35),
                Padding = new Padding(10, 5, 10, 5)
            };

            txtBrowseSearch = new TextBox
            {
                Location = new Point(10, 8),
                Width = 250,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Buscar en archivos del usuario..."
            };

            btnBrowseSearch = new Button
            {
                Text = "🔎 Buscar",
                Location = new Point(270, 6),
                Width = 80,
                Height = 25,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnBrowseSearch.FlatAppearance.BorderSize = 0;
            btnBrowseSearch.Click += (s, e) => SearchInBrowse();

            searchPanel.Controls.AddRange(new Control[] { txtBrowseSearch, btnBrowseSearch });

            // Panel de contenido - Split entre carpetas y archivos
            var contentPanel = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 300,
                BackColor = Color.FromArgb(45, 45, 45)
            };

            // Panel izquierdo - TreeView de carpetas
            tvBrowse = new TreeView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9F)
            };
            tvBrowse.AfterSelect += TvBrowse_AfterSelect;

            var leftPanel = new Panel { Dock = DockStyle.Fill };
            leftPanel.Controls.Add(tvBrowse);

            // Panel derecho - ListView de archivos
            lvBrowseFiles = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9F)
            };

            lvBrowseFiles.Columns.Add("Archivo", 300);
            lvBrowseFiles.Columns.Add("Tamaño", 100);
            lvBrowseFiles.Columns.Add("Extensión", 80);
            lvBrowseFiles.DoubleClick += LvBrowseFiles_DoubleClick;

            // Panel de botones
            var buttonsPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = Color.FromArgb(35, 35, 35),
                Padding = new Padding(10, 5, 10, 5)
            };

            btnDownloadSelected = new Button
            {
                Text = "Descargar Seleccionados",
                Location = new Point(10, 8),
                Width = 180,
                Height = 25,
                BackColor = Color.FromArgb(0, 150, 136),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            btnDownloadSelected.FlatAppearance.BorderSize = 0;
            btnDownloadSelected.Click += async (s, e) => await DownloadSelectedFilesAsync();

            btnDownloadFolder = new Button
            {
                Text = "📂 Descargar Carpeta Completa",
                Location = new Point(200, 8),
                Width = 200,
                Height = 25,
                BackColor = Color.FromArgb(156, 39, 176),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            btnDownloadFolder.FlatAppearance.BorderSize = 0;
            btnDownloadFolder.Click += async (s, e) => await DownloadFolderAsync();

            buttonsPanel.Controls.AddRange(new Control[] { btnDownloadSelected, btnDownloadFolder });

            var rightPanel = new Panel { Dock = DockStyle.Fill };
            rightPanel.Controls.Add(lvBrowseFiles);
            rightPanel.Controls.Add(buttonsPanel);

            contentPanel.Panel1.Controls.Add(leftPanel);
            contentPanel.Panel2.Controls.Add(rightPanel);

            mainPanel.Controls.Add(contentPanel);
            mainPanel.Controls.Add(searchPanel);
            mainPanel.Controls.Add(topPanel);

            tab.Controls.Add(mainPanel);
            return tab;
        }

        private async Task BrowseUserClickAsync()
        {
            var username = txtBrowseUsername.Text.Trim();
            if (string.IsNullOrEmpty(username))
            {
                MessageBox.Show("Ingresa un nombre de usuario", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                btnBrowseUser.Enabled = false;
                btnBrowseUser.Text = "⏳ Explorando...";
                tvBrowse.Nodes.Clear();
                lvBrowseFiles.Items.Clear();

                currentBrowseResult = await userBrowser.BrowseUserAsync(username);

                // Actualizar estadísticas
                lblBrowseStats.Text = $"📊 {currentBrowseResult.TotalDirectories} carpetas, " +
                                     $"{currentBrowseResult.TotalFiles} archivos, " +
                                     $"{FormatFileSize(currentBrowseResult.TotalSize)}";

                // Poblar TreeView
                var rootNode = new TreeNode($"📁 {username} ({currentBrowseResult.TotalDirectories} carpetas)")
                {
                    Tag = null
                };

                foreach (var dir in currentBrowseResult.Directories.OrderBy(d => d.Name))
                {
                    var dirNode = new TreeNode($"📂 {dir.Name} ({dir.FileCount} archivos)")
                    {
                        Tag = dir
                    };
                    rootNode.Nodes.Add(dirNode);
                }

                tvBrowse.Nodes.Add(rootNode);
                rootNode.Expand();

                Log($"✅ Browse completado: {username}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error explorando usuario: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log($"❌ Error en browse: {ex.Message}");
            }
            finally
            {
                btnBrowseUser.Enabled = true;
                btnBrowseUser.Text = "🔍 Explorar";
            }
        }

        private void TvBrowse_AfterSelect(object sender, TreeViewEventArgs e)
        {
            lvBrowseFiles.Items.Clear();
            btnDownloadFolder.Enabled = false;

            if (e.Node.Tag is BrowseDirectory dir)
            {
                foreach (var file in dir.Files.OrderBy(f => f.FileName))
                {
                    var item = new ListViewItem(file.FileName);
                    item.SubItems.Add(FormatFileSize(file.Size));
                    item.SubItems.Add(file.Extension);
                    item.Tag = file;
                    lvBrowseFiles.Items.Add(item);
                }

                btnDownloadFolder.Enabled = true;
                btnDownloadSelected.Enabled = lvBrowseFiles.Items.Count > 0;
            }
        }

        private void SearchInBrowse()
        {
            var searchTerm = txtBrowseSearch.Text.Trim();
            if (string.IsNullOrEmpty(searchTerm) || currentBrowseResult == null)
                return;

            lvBrowseFiles.Items.Clear();
            tvBrowse.CollapseAll();

            var results = new List<BrowseFile>();
            foreach (var dir in currentBrowseResult.Directories)
            {
                var matchingFiles = dir.Files.Where(f =>
                    f.FileName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                results.AddRange(matchingFiles);
            }

            foreach (var file in results.OrderBy(f => f.FileName))
            {
                var item = new ListViewItem(file.FileName);
                item.SubItems.Add(FormatFileSize(file.Size));
                item.SubItems.Add(file.Extension);
                item.SubItems.Add(file.Directory);
                item.Tag = file;
                lvBrowseFiles.Items.Add(item);
            }

            Log($"🔎 Búsqueda en browse: {results.Count} resultados para '{searchTerm}'");
        }

        private async void LvBrowseFiles_DoubleClick(object sender, EventArgs e)
        {
            if (lvBrowseFiles.SelectedItems.Count > 0)
            {
                await DownloadSelectedFilesAsync();
            }
        }

        private async Task DownloadSelectedFilesAsync()
        {
            if (lvBrowseFiles.SelectedItems.Count == 0)
                return;

            try
            {
                var filesToDownload = new List<BrowseFile>();
                foreach (ListViewItem item in lvBrowseFiles.SelectedItems)
                {
                    if (item.Tag is BrowseFile file)
                        filesToDownload.Add(file);
                }

                foreach (var file in filesToDownload)
                {
                    var result = new AutoSearchFileResult
                    {
                        Username = file.Username,
                        FileName = file.FileName,
                        SizeBytes = file.Size,
                        Size = file.Size,
                        Directory = file.Directory,
                        Extension = file.Extension,
                        Network = "Soulseek"
                    };

                    await AddDownloadTask(result);
                }

                Log($"✅ {filesToDownload.Count} archivos agregados a la cola de descarga");
                MessageBox.Show($"{filesToDownload.Count} archivos agregados a la cola", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error agregando descargas: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task DownloadFolderAsync()
        {
            if (tvBrowse.SelectedNode?.Tag is not BrowseDirectory dir)
                return;

            var result = MessageBox.Show(
                $"¿Descargar toda la carpeta '{dir.Name}'?\n\n" +
                $"Archivos: {dir.FileCount}\n" +
                $"Tamaño total: {FormatFileSize(dir.TotalSize)}",
                "Confirmar descarga",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            try
            {
                foreach (var file in dir.Files)
                {
                    var downloadResult = new AutoSearchFileResult
                    {
                        Username = file.Username,
                        FileName = file.FileName,
                        SizeBytes = file.Size,
                        Size = file.Size,
                        Directory = file.Directory,
                        Extension = file.Extension,
                        Network = "Soulseek"
                    };

                    await AddDownloadTask(downloadResult);
                }

                Log($"✅ Carpeta completa agregada: {dir.Name} ({dir.FileCount} archivos)");
                MessageBox.Show($"Carpeta completa agregada: {dir.FileCount} archivos", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error descargando carpeta: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Explora un usuario desde código
        /// </summary>
        public async Task<BrowseResult> BrowseUserAsync(string username)
        {
            return await userBrowser?.BrowseUserAsync(username);
        }

        /// <summary>
        /// Obtiene estadísticas de browse de un usuario
        /// </summary>
        public async Task<BrowseStats> GetUserBrowseStatsAsync(string username)
        {
            return await userBrowser?.GetBrowseStatsAsync(username);
        }
    }
}
