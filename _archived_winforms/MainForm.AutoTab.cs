using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using SlskDown.UI;

namespace SlskDown
{
    public partial class MainForm
    {
        /// <summary>
        /// Crea el tab de Gestión Automática con paneles colapsables para controles
        /// </summary>
        private void CreateAutoTabOptimized(Panel parent)
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
            
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Header
            var lblTitle = new Label 
            { 
                Text = "📚 GESTIÓN AUTOMÁTICA", 
                AutoSize = true,
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                Margin = new Padding(5, 10, 0, 0)
            };
            mainLayout.Controls.Add(lblTitle, 0, 0);

            // Layout principal: 2 columnas
            var contentLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            
            mainLayout.Controls.Add(contentLayout, 0, 1);
            parent.Controls.Add(mainLayout);

            // === COLUMNA IZQUIERDA: AUTORES ===
            CreateAutoTabLeftColumn(contentLayout);
            
            // === COLUMNA DERECHA: LOG Y CONTROLES ===
            CreateAutoTabRightColumn(contentLayout);
        }
        
        private void CreateAutoTabLeftColumn(TableLayoutPanel contentLayout)
        {
            var leftPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(0, 0, 10, 0)
            };
            
            var leftLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // Header con contador
            var headerPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(5, 8, 5, 8)
            };
            
            var lblAuthors = new Label
            {
                Text = "📖 Autores",
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 200, 255),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Margin = new Padding(0, 3, 15, 0)
            };
            headerPanel.Controls.Add(lblAuthors);
            
            lblAuthorCount = new Label
            {
                Name = "lblAuthorCount",
                AutoSize = true,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9),
                Text = "0 autores",
                Margin = new Padding(0, 6, 0, 0)
            };
            headerPanel.Controls.Add(lblAuthorCount);
            
            leftLayout.Controls.Add(headerPanel, 0, 0);

            // ListView de autores
            lvAutoAuthors = new ListView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                CheckBoxes = false,
                Font = new Font("Segoe UI", 9),
                VirtualMode = true,
                HoverSelection = false
            };
            
            lvAutoAuthors.Columns.Add("Autor", 250);
            lvAutoAuthors.Columns.Add("Coincidencias", 120);
            
            typeof(Control).GetProperty("DoubleBuffered", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(lvAutoAuthors, true, null);
            
            lvAutoAuthors.ColumnClick += LvAutoAuthors_ColumnClick;
            lvAutoAuthors.RetrieveVirtualItem += LvAutoAuthors_RetrieveVirtualItem;
            lvAutoAuthors.CacheVirtualItems += LvAutoAuthors_CacheVirtualItems;
            
            lvAutoAuthors.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    var hitTest = lvAutoAuthors.HitTest(e.Location);
                    if (hitTest.Item != null)
                    {
                        int index = hitTest.Item.Index;
                        if (index >= 0 && index < filteredAuthorsData.Count)
                        {
                            var author = filteredAuthorsData[index];
                            author.IsChecked = !author.IsChecked;
                            lvAutoAuthors.RedrawItems(index, index, false);
                            AutoLog($"Autor '{author.Name}' {(author.IsChecked ? "seleccionado" : "deseleccionado")}");
                        }
                    }
                }
            };
            
            var authorsContextMenu = new ContextMenuStrip
            {
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White
            };
            
            var menuDelete = new ToolStripMenuItem("Eliminar autor(es)");
            menuDelete.Click += (s, e) => DeleteSelectedAuthorsFromContext();
            authorsContextMenu.Items.Add(menuDelete);
            
            var menuViewFiles = new ToolStripMenuItem("Ver archivos únicos");
            menuViewFiles.Click += (s, e) => ViewAuthorUniqueFiles();
            authorsContextMenu.Items.Add(menuViewFiles);
            
            lvAutoAuthors.ContextMenuStrip = authorsContextMenu;
            leftLayout.Controls.Add(lvAutoAuthors, 0, 1);

            // Panel de botones con scroll
            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(24, 24, 24),
                Padding = new Padding(8)
            };
            
            var buttonsFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };

            var btnLoadAuthors = new ModernButton
            {
                Text = "CARGAR",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 40),
                Padding = new Padding(20, 0, 20, 0),
                BackColor = Color.FromArgb(0, 120, 215),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 8)
            };
            btnLoadAuthors.Click += (s, e) => SelectAndLoadAuthorsFile();
            AddTooltip(btnLoadAuthors, "Carga una lista de autores desde un archivo de texto (un autor por línea) para vigilancia automática");
            buttonsFlow.Controls.Add(btnLoadAuthors);

            var btnSelectAll = new ModernButton
            {
                Text = "TODOS",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 40),
                Padding = new Padding(20, 0, 20, 0),
                BackColor = Color.FromArgb(60, 60, 60),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 8)
            };
            btnSelectAll.Click += (s, e) =>
            {
                foreach (var author in filteredAuthorsData)
                    author.IsChecked = true;
                RefreshAuthorsListView();
                UpdateAuthorCount();
            };
            buttonsFlow.Controls.Add(btnSelectAll);

            var btnSelectNone = new ModernButton
            {
                Text = "NINGUNO",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 40),
                Padding = new Padding(20, 0, 20, 0),
                BackColor = Color.FromArgb(60, 60, 60),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 8)
            };
            btnSelectNone.Click += (s, e) =>
            {
                foreach (var author in filteredAuthorsData)
                    author.IsChecked = false;
                RefreshAuthorsListView();
                UpdateAuthorCount();
            };
            buttonsFlow.Controls.Add(btnSelectNone);

            var btnDelete = new ModernButton
            {
                Text = "BORRAR",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 40),
                Padding = new Padding(20, 0, 20, 0),
                BackColor = Color.FromArgb(180, 50, 50),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 8)
            };
            btnDelete.Click += (s, e) => DeleteSelectedAuthors();
            buttonsFlow.Controls.Add(btnDelete);

            var btnDownloadAuthors = new ModernButton
            {
                Text = "DESCARGAR",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 40),
                Padding = new Padding(20, 0, 20, 0),
                BackColor = Color.FromArgb(0, 150, 0),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 8)
            };
            btnDownloadAuthors.Click += async (s, e) => await DownloadSelectedAuthorsFiles();
            AddTooltip(btnDownloadAuthors, "Descarga todos los archivos encontrados de los autores seleccionados en la lista");
            buttonsFlow.Controls.Add(btnDownloadAuthors);

            var btnSaveUnpurged = new ModernButton
            {
                Text = "GUARDAR",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 40),
                Padding = new Padding(20, 0, 20, 0),
                BackColor = Color.FromArgb(60, 60, 60),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 8)
            };
            btnSaveUnpurged.Click += (s, e) =>
            {
                if (allAuthorsData.Count == 0) return;
                using (var sfd = new SaveFileDialog { Filter = "TXT|*.txt", Title = "Guardar autores", FileName = "authors_unpurged.txt" })
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        var lines = allAuthorsData.Select(a => a.Name).ToArray();
                        File.WriteAllLines(sfd.FileName, lines);
                        AutoLog($"Guardados {lines.Length:N0} autores en {Path.GetFileName(sfd.FileName)}");
                    }
                }
            };
            AddTooltip(btnSaveUnpurged, "Guarda la lista actual de autores (sin purgar) en un archivo de texto para respaldo");
            buttonsFlow.Controls.Add(btnSaveUnpurged);

            scrollPanel.Controls.Add(buttonsFlow);
            leftLayout.Controls.Add(scrollPanel, 0, 2);
            
            leftPanel.Controls.Add(leftLayout);
            contentLayout.Controls.Add(leftPanel, 0, 0);
        }
        
        private void CreateAutoTabRightColumn(TableLayoutPanel contentLayout)
        {
            var rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(10, 0, 0, 0)
            };
            
            var rightLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // Header log
            var headerLogPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(0, 8, 0, 0)
            };
            
            var lblLog = new Label
            {
                Text = "📋 Log",
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 200, 255),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Margin = new Padding(0, 3, 15, 0)
            };
            headerLogPanel.Controls.Add(lblLog);
            
            lblPurgeProgress = new Label
            {
                Text = "",
                AutoSize = true,
                ForeColor = Color.FromArgb(255, 200, 100),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Visible = false,
                Margin = new Padding(0, 5, 0, 0)
            };
            headerLogPanel.Controls.Add(lblPurgeProgress);
            
            rightLayout.Controls.Add(headerLogPanel, 0, 0);

            // TextBox log
            txtAutoLog = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(25, 25, 25),
                ForeColor = Color.LightGray,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9)
            };
            rightLayout.Controls.Add(txtAutoLog, 0, 1);

            // Usar SectionContainer - layout vertical simple con scroll
            var sectionContainer = new SectionContainer
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 24, 24)
            };
            
            // Sección 1: Opciones de búsqueda
            var searchPanel = new SectionPanel("🔍 OPCIONES DE BÚSQUEDA", headerColor: Color.FromArgb(40, 40, 40));
            
            // FlowLayoutPanel horizontal para los 3 checkboxes en una línea
            var checkboxesFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            
            var chkOnlyNew = new CheckBox
            {
                Text = "Solo archivos nuevos",
                AutoSize = true,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                Checked = onlyNewFilesInAutoSearch,
                Margin = new Padding(0, 5, 15, 5)
            };
            chkOnlyNew.CheckedChanged += (s, e) =>
            {
                onlyNewFilesInAutoSearch = chkOnlyNew.Checked;
                SaveConfig();
                AutoLog($"Filtro 'Solo Nuevos': {(onlyNewFilesInAutoSearch ? "ACTIVADO" : "DESACTIVADO")}");
            };
            checkboxesFlow.Controls.Add(chkOnlyNew);
            
            var chkAutoSpanishDocuments = new CheckBox
            {
                Text = "Solo documentos en español",
                AutoSize = true,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                Checked = false,
                Margin = new Padding(0, 5, 0, 5)
            };
            chkAutoSpanishDocuments.CheckedChanged += (s, e) =>
            {
                SaveConfig();
                AutoLog($"Filtro 'Solo Español': {(chkAutoSpanishDocuments.Checked ? "ACTIVADO" : "DESACTIVADO")}");
            };
            checkboxesFlow.Controls.Add(chkAutoSpanishDocuments);
            
            searchPanel.AddContent(checkboxesFlow);
            sectionContainer.AddSection(searchPanel);
            
            // Sección 2: Acciones
            var actionsPanel = new SectionPanel("⚡ ACCIONES", headerColor: Color.FromArgb(40, 40, 40));
            
            var actionsFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            
            btnStartAuto = new ModernButton
            {
                Text = "▶ INICIAR",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 45),
                Padding = new Padding(20, 0, 20, 0),
                BackColor = Color.FromArgb(0, 150, 0),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 8)
            };
            btnStartAuto.Click += async (s, e) => await StartAutomaticSearch();
            actionsFlow.Controls.Add(btnStartAuto);
            
            btnStopAuto = new ModernButton
            {
                Text = "⏹️ DETENER",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 45),
                Padding = new Padding(20, 0, 20, 0),
                BackColor = Color.FromArgb(150, 0, 0),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 8),
                Enabled = false
            };
            btnStopAuto.Click += async (s, e) =>
            {
                bool anyActive = false;

                if (autoSearchRunning)
                {
                    AutoLog("⏹️ Cancelando búsqueda automática...");
                    anyActive = true;
                }

                if (autoPurgeRunning)
                {
                    AutoLog("⏹️ Cancelando purga automática...");
                    anyActive = true;
                }

                if (!anyActive)
                {
                    AutoLog("⚠️ No hay procesos automáticos activos.");
                    return;
                }

                // CANCELACIÓN AGRESIVA: Marcar flags primero
                autoSearchRunning = false;
                autoPurgeRunning = false;
                
                // Cancelar token
                try
                {
                    autoSearchCts?.Cancel();
                    AutoLog("✅ Token de cancelación enviado");
                }
                catch (Exception ex)
                {
                    AutoLog($"❌ Error cancelando token: {ex.Message}");
                }
                
                // Esperar un momento para que las tareas se cancelen
                AutoLog("⏳ Esperando que las tareas se detengan...");
                await Task.Delay(500);

                AutoLog("✅ Proceso detenido");
                
                // Restaurar botones
                if (btnStopAuto != null)
                {
                    btnStopAuto.Enabled = false;
                    btnStopAuto.BackColor = Color.FromArgb(150, 0, 0);
                }
                if (btnPurge != null)
                    btnPurge.Enabled = true;
                if (btnStartAuto != null)
                    btnStartAuto.Enabled = true;
            };
            actionsFlow.Controls.Add(btnStopAuto);
            
            btnPurge = new ModernButton
            {
                Text = "🗑️ PURGAR",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 45),
                Padding = new Padding(20, 0, 20, 0),
                BackColor = Color.FromArgb(180, 50, 50),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 8)
            };
            btnPurge.Click += async (s, e) =>
            {
                try
                {
                    await PurgeAuthorsWithoutResults();
                }
                catch (Exception ex)
                {
                    AutoLog($"ERROR en purga: {ex.Message}");
                }
            };
            actionsFlow.Controls.Add(btnPurge);
            
            btnOpenAutoResults = new ModernButton
            {
                Text = "📊 RESULTADOS",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 45),
                Padding = new Padding(20, 0, 20, 0),
                BackColor = Color.FromArgb(0, 120, 215),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 8)
            };
            btnOpenAutoResults.Click += (s, e) =>
            {
                string csvPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "auto_search_results.csv");
                if (System.IO.File.Exists(csvPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", csvPath);
                }
                else
                {
                    MessageBox.Show("El archivo de resultados aún no se ha generado.", "Resultados", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            actionsFlow.Controls.Add(btnOpenAutoResults);
            
            var btnCopyLog = new ModernButton
            {
                Text = "📋 COPIAR LOG",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 45),
                Padding = new Padding(20, 0, 20, 0),
                BackColor = Color.FromArgb(30, 100, 200),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 8)
            };
            btnCopyLog.Click += (s, e) =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(txtAutoLog.Text))
                    {
                        Clipboard.SetText(txtAutoLog.Text);
                        AutoLog("📋 Log copiado al portapapeles");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al copiar: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            AddTooltip(btnCopyLog, "Copia el contenido completo del log de búsquedas automáticas al portapapeles");
            actionsFlow.Controls.Add(btnCopyLog);
            
            actionsPanel.AddContent(actionsFlow);
            sectionContainer.AddSection(actionsPanel);
            
            rightLayout.Controls.Add(sectionContainer, 0, 2);
            
            rightPanel.Controls.Add(rightLayout);
            contentLayout.Controls.Add(rightPanel, 1, 0);
        }
    }
}
