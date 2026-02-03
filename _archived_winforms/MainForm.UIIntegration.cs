using System;
using System.Drawing;
using System.Windows.Forms;
using SlskDown.Core.Collections;
using SlskDown.Core.Integrations;
using SlskDown.UI;

namespace SlskDown
{
    /// <summary>
    /// Partial class de MainForm para integración de nuevas funcionalidades en UI
    /// </summary>
    public partial class MainForm
    {
        // ============================================================================
        // INTEGRACIÓN UI - Nuevas Funcionalidades 2026
        // ============================================================================
        
        private CollectionManager collectionManager;
        private CalibreIntegration calibreIntegration;
        private OpenLibraryIntegration openLibraryIntegration;
        
        private Button btnDashboard;
        private Button btnCollections;
        private Button btnCalibre;
        private ToolStripMenuItem menuCollections;
        private ToolStripMenuItem menuIntegrations;

        /// <summary>
        /// Inicializa todas las nuevas funcionalidades
        /// </summary>
        private void InitializeAdvancedFeatures()
        {
            try
            {
                // 1. Notificaciones Desktop
                InitializeNotifications();
                
                // 2. Modo Coleccionista
                InitializeCollections();
                
                // 3. Integración Calibre
                InitializeCalibre();
                
                // 4. Integración OpenLibrary
                InitializeOpenLibrary();
                
                // 5. Crear botones en UI
                CreateAdvancedFeaturesUI();
                
                Log("Funcionalidades avanzadas inicializadas");
            }
            catch (Exception ex)
            {
                Log($"Error inicializando funcionalidades avanzadas: {ex.Message}");
            }
        }

        private void InitializeCollections()
        {
            try
            {
                collectionManager = new CollectionManager(dataDir);
                collectionManager.OnLog += (msg) => Log($"[Collections] {msg}");
                collectionManager.OnCollectionUpdated += OnCollectionUpdated;
                collectionManager.OnItemFound += OnCollectionItemFound;
                
                // Cargar colecciones guardadas
                _ = collectionManager.LoadAsync();
                
                Log("CollectionManager inicializado");
            }
            catch (Exception ex)
            {
                Log($"Error inicializando CollectionManager: {ex.Message}");
            }
        }

        private void InitializeCalibre()
        {
            try
            {
                calibreIntegration = new CalibreIntegration();
                calibreIntegration.OnLog += (msg) => Log($"[Calibre] {msg}");
                
                if (calibreIntegration.IsAvailable)
                {
                    Log($"Calibre detectado: {calibreIntegration.LibraryPath}");
                }
                else
                {
                    Log("Calibre no detectado (puedes configurarlo manualmente)");
                }
            }
            catch (Exception ex)
            {
                Log($"Error inicializando Calibre: {ex.Message}");
            }
        }

        private void InitializeOpenLibrary()
        {
            try
            {
                openLibraryIntegration = new OpenLibraryIntegration();
                openLibraryIntegration.OnLog += (msg) => Log($"[OpenLibrary] {msg}");
                
                Log("OpenLibrary inicializado");
            }
            catch (Exception ex)
            {
                Log($"Error inicializando OpenLibrary: {ex.Message}");
            }
        }

        private void CreateAdvancedFeaturesUI()
        {
            try
            {
                // Crear botones en toolbar principal
                if (Controls.Find("mainToolbar", true).Length > 0)
                {
                    var toolbar = Controls.Find("mainToolbar", true)[0] as ToolStrip;
                    if (toolbar != null)
                    {
                        // Botón Dashboard
                        var btnDashboardItem = new ToolStripButton
                        {
                            Text = "Dashboard",
                            ToolTipText = "Ver dashboard avanzado con estadísticas",
                            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
                        };
                        btnDashboardItem.Click += (s, e) => ShowEnhancedDashboard();
                        toolbar.Items.Add(btnDashboardItem);

                        // Botón Colecciones
                        var btnCollectionsItem = new ToolStripButton
                        {
                            Text = "Colecciones",
                            ToolTipText = "Gestionar colecciones de archivos",
                            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
                        };
                        btnCollectionsItem.Click += (s, e) => ShowCollectionsManager();
                        toolbar.Items.Add(btnCollectionsItem);

                        // Botón Calibre
                        if (calibreIntegration?.IsAvailable == true)
                        {
                            var btnCalibreItem = new ToolStripButton
                            {
                                Text = "Calibre",
                                ToolTipText = "Sincronizar con Calibre",
                                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
                            };
                            btnCalibreItem.Click += (s, e) => ShowCalibreSync();
                            toolbar.Items.Add(btnCalibreItem);
                        }
                    }
                }
                
                Log("UI de funcionalidades avanzadas creada");
            }
            catch (Exception ex)
            {
                Log($"Error creando UI avanzada: {ex.Message}");
            }
        }

        /// <summary>
        /// Muestra el dashboard mejorado
        /// </summary>
        private void ShowEnhancedDashboard()
        {
            try
            {
                if (performanceMetrics == null)
                {
                    MessageBox.Show("Sistema de métricas no disponible", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var dashboard = new EnhancedDashboard(
                    performanceMetrics,
                    GetTopUsersForDashboard,
                    GetTopFilesForDashboard,
                    GetActivityByHourForDashboard
                );

                dashboard.Show();
                Log("Dashboard avanzado abierto");
            }
            catch (Exception ex)
            {
                Log($"Error mostrando dashboard: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Muestra el gestor de colecciones
        /// </summary>
        private void ShowCollectionsManager()
        {
            try
            {
                var form = new Form
                {
                    Text = "Gestor de Colecciones",
                    Size = new Size(900, 600),
                    BackColor = Color.FromArgb(30, 30, 30),
                    StartPosition = FormStartPosition.CenterParent
                };

                var collections = collectionManager.GetAllCollections();
                
                var lv = new ListView
                {
                    Dock = DockStyle.Fill,
                    View = View.Details,
                    FullRowSelect = true,
                    GridLines = true,
                    BackColor = Color.FromArgb(40, 40, 40),
                    ForeColor = Color.White
                };

                lv.Columns.Add("Nombre", 200);
                lv.Columns.Add("Tipo", 100);
                lv.Columns.Add("Items", 80);
                lv.Columns.Add("Completados", 100);
                lv.Columns.Add("Progreso", 100);

                foreach (var collection in collections)
                {
                    var stats = collectionManager.GetStats(collection.Id);
                    var item = new ListViewItem(collection.Name);
                    item.SubItems.Add(collection.Type.ToString());
                    item.SubItems.Add(stats.TotalItems.ToString());
                    item.SubItems.Add(stats.DownloadedItems.ToString());
                    item.SubItems.Add($"{stats.CompletionPercentage:F1}%");
                    item.Tag = collection;
                    lv.Items.Add(item);
                }

                var btnPanel = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 50,
                    BackColor = Color.FromArgb(35, 35, 35)
                };

                var btnNew = new Button
                {
                    Text = "Nueva Colección",
                    Location = new Point(10, 10),
                    Width = 150,
                    BackColor = Color.FromArgb(0, 120, 215),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnNew.Click += (s, e) => CreateNewCollection();

                btnPanel.Controls.Add(btnNew);
                form.Controls.Add(lv);
                form.Controls.Add(btnPanel);
                form.Show();

                Log("Gestor de colecciones abierto");
            }
            catch (Exception ex)
            {
                Log($"Error mostrando colecciones: {ex.Message}");
            }
        }

        /// <summary>
        /// Muestra sincronización con Calibre
        /// </summary>
        private void ShowCalibreSync()
        {
            try
            {
                if (calibreIntegration == null || !calibreIntegration.IsAvailable)
                {
                    MessageBox.Show("Calibre no está disponible. Configúralo en Ajustes.", "Calibre", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var form = new Form
                {
                    Text = "Sincronización con Calibre",
                    Size = new Size(700, 500),
                    BackColor = Color.FromArgb(30, 30, 30),
                    StartPosition = FormStartPosition.CenterParent
                };

                var lblInfo = new Label
                {
                    Text = $"Biblioteca: {calibreIntegration.LibraryPath}",
                    ForeColor = Color.White,
                    Location = new Point(10, 10),
                    AutoSize = true
                };

                var btnSync = new Button
                {
                    Text = "Sincronizar Descargas Recientes",
                    Location = new Point(10, 50),
                    Width = 250,
                    BackColor = Color.FromArgb(0, 150, 136),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnSync.Click += async (s, e) => await SyncRecentDownloadsToCalibre();

                form.Controls.AddRange(new Control[] { lblInfo, btnSync });
                form.Show();

                Log("Sincronización Calibre abierta");
            }
            catch (Exception ex)
            {
                Log($"Error mostrando Calibre: {ex.Message}");
            }
        }

        /// <summary>
        /// Sincroniza descargas recientes con Calibre
        /// </summary>
        private async System.Threading.Tasks.Task SyncRecentDownloadsToCalibre()
        {
            try
            {
                // Obtener archivos ebook descargados recientemente
                var ebookExtensions = new[] { ".epub", ".pdf", ".mobi", ".azw3" };
                var recentFiles = System.IO.Directory.GetFiles(downloadDir, "*.*", System.IO.SearchOption.AllDirectories)
                    .Where(f => ebookExtensions.Contains(System.IO.Path.GetExtension(f).ToLower()))
                    .Where(f => System.IO.File.GetCreationTime(f) > DateTime.Now.AddDays(-7))
                    .ToList();

                if (!recentFiles.Any())
                {
                    MessageBox.Show("No hay ebooks recientes para sincronizar", "Calibre", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                int added = 0;
                foreach (var file in recentFiles)
                {
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(file);
                    
                    // Intentar extraer autor del nombre del archivo
                    string author = null;
                    if (fileName.Contains(" - "))
                    {
                        author = fileName.Split(new[] { " - " }, StringSplitOptions.None)[0];
                    }

                    var success = await calibreIntegration.AddBookAsync(file, author);
                    if (success)
                    {
                        added++;
                        NotifyInfo("Libro agregado a Calibre", System.IO.Path.GetFileName(file));
                    }
                }

                MessageBox.Show($"Sincronización completada: {added}/{recentFiles.Count} libros agregados", "Calibre", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Log($"Sincronización Calibre: {added} libros agregados");
            }
            catch (Exception ex)
            {
                Log($"Error sincronizando con Calibre: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Crea una nueva colección
        /// </summary>
        private void CreateNewCollection()
        {
            try
            {
                var dialog = new Form
                {
                    Text = "Nueva Colección",
                    Size = new Size(400, 250),
                    BackColor = Color.FromArgb(30, 30, 30),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };

                var lblName = new Label { Text = "Nombre:", ForeColor = Color.White, Location = new Point(10, 20), AutoSize = true };
                var txtName = new TextBox { Location = new Point(10, 45), Width = 360, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White };

                var lblDesc = new Label { Text = "Descripción:", ForeColor = Color.White, Location = new Point(10, 80), AutoSize = true };
                var txtDesc = new TextBox { Location = new Point(10, 105), Width = 360, Height = 60, Multiline = true, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White };

                var lblType = new Label { Text = "Tipo:", ForeColor = Color.White, Location = new Point(10, 175), AutoSize = true };
                var cmbType = new ComboBox { Location = new Point(60, 172), Width = 150, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White };
                cmbType.Items.AddRange(new object[] { "Books", "Music", "Series", "Custom" });
                cmbType.SelectedIndex = 0;

                var btnCreate = new Button
                {
                    Text = "Crear",
                    Location = new Point(220, 170),
                    Width = 150,
                    BackColor = Color.FromArgb(0, 120, 215),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };

                btnCreate.Click += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(txtName.Text))
                    {
                        MessageBox.Show("Ingresa un nombre", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    var type = (CollectionType)Enum.Parse(typeof(CollectionType), cmbType.SelectedItem.ToString());
                    collectionManager.CreateCollection(txtName.Text, txtDesc.Text, type);
                    _ = collectionManager.SaveAsync();
                    
                    dialog.Close();
                    ShowCollectionsManager();
                };

                dialog.Controls.AddRange(new Control[] { lblName, txtName, lblDesc, txtDesc, lblType, cmbType, btnCreate });
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                Log($"Error creando colección: {ex.Message}");
            }
        }

        // Eventos de colecciones
        private void OnCollectionUpdated(Collection collection)
        {
            try
            {
                var stats = collectionManager.GetStats(collection.Id);
                Log($"Colección actualizada: {collection.Name} - {stats.CompletionPercentage:F1}% completo");
                
                _ = collectionManager.SaveAsync();
            }
            catch (Exception ex)
            {
                Log($"Error en evento de colección: {ex.Message}");
            }
        }

        private void OnCollectionItemFound(Collection collection, CollectionItem item)
        {
            try
            {
                NotifyInfo($"Item encontrado en '{collection.Name}'", item.Name);
            }
            catch (Exception ex)
            {
                Log($"Error en evento de item: {ex.Message}");
            }
        }

        // Métodos auxiliares para dashboard
        private System.Collections.Generic.List<(string, int, double)> GetTopUsersForDashboard()
        {
            // Implementar según tus datos
            return new System.Collections.Generic.List<(string, int, double)>();
        }

        private System.Collections.Generic.List<(string, int)> GetTopFilesForDashboard()
        {
            // Implementar según tus datos
            return new System.Collections.Generic.List<(string, int)>();
        }

        private System.Collections.Generic.Dictionary<int, int> GetActivityByHourForDashboard()
        {
            // Implementar según tus datos
            return new System.Collections.Generic.Dictionary<int, int>();
        }

        private void NotifyInfo(string title, string message)
        {
            ShowNotification(title, message, ToolTipIcon.Info);
        }
    }
}
