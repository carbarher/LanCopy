using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading.Tasks;
using Soulseek;
using SlskDown.UI;

namespace SlskDown
{
    public partial class MainForm
    {
        /// <summary>
        /// Crea el tab de Búsqueda con filtros avanzados organizados en paneles colapsables
        /// </summary>
        private void CreateSearchTabOptimized(Panel parent)
        {
            parent.BackColor = Color.FromArgb(30, 30, 30);
            
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(30, 30, 30),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Búsqueda
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Filtros
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Resultados
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            
            // === BARRA DE BÚSQUEDA ===
            CreateSearchBar(mainLayout);
            
            // === PANEL DE FILTROS CON COLAPSABLES ===
            CreateSearchFiltersPanel(mainLayout);
            
            // === RESULTADOS ===
            CreateSearchResults(mainLayout);
            
            parent.Controls.Add(mainLayout);
        }
        
        private void CreateSearchBar(TableLayoutPanel mainLayout)
        {
            var searchCard = new ModernCard 
            { 
                Dock = DockStyle.Fill, 
                AutoSize = true, 
                BorderRadius = 12, 
                Padding = new Padding(12),
                Margin = new Padding(0, 0, 0, 10)
            };
            
            var searchFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent
            };
            
            // Campo de búsqueda
            cmbSearch = new ComboBox 
            { 
                Width = 500,
                Height = 40,
                BackColor = Color.FromArgb(40, 40, 40), 
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11),
                Margin = new Padding(0, 0, 10, 10)
            };
            cmbSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) Task.Run(async () => await SearchAsync()); };
            
            var searchTooltip = new ToolTip();
            searchTooltip.SetToolTip(cmbSearch, "Introduce el término de búsqueda y presiona Enter o haz clic en BUSCAR");
            searchFlow.Controls.Add(cmbSearch);
            
            // Botón buscar
            btnSearch = new ModernButton 
            { 
                Text = "BUSCAR", 
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 50),
                Padding = new Padding(20, 0, 20, 0),
                BackColor = Color.FromArgb(0, 120, 215),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Enabled = false,
                Margin = new Padding(0, 0, 10, 10)
            };
            btnSearch.Click += async (s, e) => await SearchAsync();
            
            var btnSearchTooltip = new ToolTip();
            btnSearchTooltip.SetToolTip(btnSearch, "Iniciar búsqueda en la red Soulseek (requiere conexión activa)");
            searchFlow.Controls.Add(btnSearch);
            
            // Botón detener
            btnStopSearch = new ModernButton 
            { 
                Text = "DETENER", 
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 50),
                Padding = new Padding(20, 0, 20, 0),
                BackColor = Color.FromArgb(180, 50, 50),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Enabled = false,
                Margin = new Padding(0, 0, 10, 10)
            };
            btnStopSearch.Click += (s, e) => StopSearch();
            searchFlow.Controls.Add(btnStopSearch);
            
            // Contador de resultados
            lblResultsCount = new Label 
            { 
                Text = "0 resultados", 
                AutoSize = true, 
                ForeColor = Color.FromArgb(100, 200, 255), 
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Margin = new Padding(0, 15, 10, 0)
            };
            searchFlow.Controls.Add(lblResultsCount);
            
            // Estado de conexión
            lblStatus = new Label
            {
                Text = "● Desconectado",
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Margin = new Padding(0, 15, 10, 0)
            };
            searchFlow.Controls.Add(lblStatus);
            
            // Botón conectar
            btnConnect = new ModernButton 
            { 
                Text = "CONECTAR", 
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 50),
                Padding = new Padding(20, 0, 20, 0),
                BackColor = Color.FromArgb(0, 120, 215),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 10)
            };
            
            var btnConnectTooltip = new ToolTip();
            btnConnectTooltip.SetToolTip(btnConnect, "Conectar/Desconectar de la red Soulseek");
            btnConnect.Click += async (s, e) =>
            {
                try
                {
                    if (client != null && client.State.HasFlag(SoulseekClientStates.Connected))
                    {
                        client.Disconnect();
                        btnConnect.Text = "CONECTAR";
                        btnSearch.Enabled = false;
                        lblStatus.Text = "● Desconectado";
                        lblStatus.ForeColor = Color.Gray;
                        Log("Desconectado de Soulseek");
                    }
                    else
                    {
                        await ConnectToSoulseek();
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error en conexión: {ex.Message}");
                    MessageBox.Show($"Error al conectar:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            searchFlow.Controls.Add(btnConnect);
            
            searchCard.Controls.Add(searchFlow);
            mainLayout.Controls.Add(searchCard, 0, 0);
        }
        
        private void CreateSearchFiltersPanel(TableLayoutPanel mainLayout)
        {
            // Usar SectionContainer - layout vertical simple con scroll
            var sectionContainer = new SectionContainer
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                MaximumSize = new Size(0, 500)
            };
            
            // Sección 1: Filtros Básicos
            var basicPanel = new SectionPanel("🔍 FILTROS BÁSICOS", headerColor: Color.FromArgb(45, 45, 45));
            CreateBasicFilters(basicPanel);
            sectionContainer.AddSection(basicPanel);
            
            // Sección 2: Filtros de Archivo
            var filePanel = new SectionPanel("📁 FILTROS DE ARCHIVO", headerColor: Color.FromArgb(40, 40, 40));
            CreateFileFilters(filePanel);
            sectionContainer.AddSection(filePanel);
            
            // Sección 3: Filtros de Usuario
            var userPanel = new SectionPanel("👤 FILTROS DE USUARIO", headerColor: Color.FromArgb(40, 40, 40));
            CreateUserFilters(userPanel);
            sectionContainer.AddSection(userPanel);
            
            // Sección 4: Acciones
            var actionsPanel = new SectionPanel("⚡ ACCIONES", headerColor: Color.FromArgb(40, 40, 40));
            CreateSearchActions(actionsPanel);
            sectionContainer.AddSection(actionsPanel);
            
            mainLayout.Controls.Add(sectionContainer, 0, 1);
        }
        
        private void CreateBasicFilters(SectionPanel panel)
        {
            var flow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            
            // Filtro de texto
            var lblFilter = new Label 
            { 
                Text = "Filtrar resultados:", 
                AutoSize = true, 
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Margin = new Padding(0, 8, 8, 0)
            };
            flow.Controls.Add(lblFilter);
            
            txtFilterResults = new TextBox 
            { 
                Size = new Size(250, 28), 
                BackColor = Color.FromArgb(50, 50, 50), 
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 10),
                Margin = new Padding(0, 4, 15, 8)
            };
            txtFilterResults.TextChanged += (s, e) => FilterResults();
            flow.Controls.Add(txtFilterResults);
            
            // Checkbox español
            chkSpanishOnly = new CheckBox 
            { 
                Text = "Solo español", 
                AutoSize = true, 
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 10),
                Margin = new Padding(0, 8, 15, 8)
            };
            
            var spanishTooltip = new ToolTip();
            spanishTooltip.SetToolTip(chkSpanishOnly, "Filtrar resultados para mostrar solo archivos en español");
            flow.Controls.Add(chkSpanishOnly);
            
            // Calidad mínima
            var lblQuality = new Label 
            { 
                Text = "Calidad mín:", 
                AutoSize = true, 
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Margin = new Padding(0, 8, 8, 0)
            };
            flow.Controls.Add(lblQuality);
            
            chkQualityFilter = new CheckBox 
            { 
                Text = "Activar", 
                AutoSize = true, 
                Checked = true, 
                ForeColor = Color.White,
                Margin = new Padding(0, 8, 5, 0)
            };
            flow.Controls.Add(chkQualityFilter);
            
            numMinQuality = new NumericUpDown 
            { 
                Size = new Size(60, 28), 
                BackColor = Color.FromArgb(50, 50, 50), 
                ForeColor = Color.White, 
                Minimum = 0, 
                Maximum = 100, 
                Value = 60,
                Margin = new Padding(0, 4, 8, 8)
            };
            
            var qualityTooltip = new ToolTip();
            qualityTooltip.SetToolTip(numMinQuality, "Calidad mínima del archivo (0-100). Valores más altos filtran archivos de baja calidad");
            flow.Controls.Add(numMinQuality);
            
            panel.AddContent(flow);
        }
        
        private void CreateFileFilters(SectionPanel panel)
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 4,
                RowCount = 3,
                AutoSize = true,
                BackColor = Color.Transparent,
                Padding = new Padding(5)
            };
            
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            
            // Fila 1: Tamaño
            grid.Controls.Add(new Label { Text = "Tamaño (KB):", ForeColor = Color.LightGray, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 0);
            
            var sizeFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Dock = DockStyle.Fill };
            numMinSize = new NumericUpDown { Size = new Size(70, 25), BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, Maximum = 999999 };
            sizeFlow.Controls.Add(numMinSize);
            sizeFlow.Controls.Add(new Label { Text = "-", ForeColor = Color.Gray, Margin = new Padding(5, 3, 5, 0) });
            numMaxSize = new NumericUpDown { Size = new Size(70, 25), BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, Maximum = 999999 };
            sizeFlow.Controls.Add(numMaxSize);
            grid.Controls.Add(sizeFlow, 1, 0);
            
            // Tipo de archivo
            grid.Controls.Add(new Label { Text = "Tipo:", ForeColor = Color.LightGray, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 2, 0);
            cmbExtension = new ComboBox { Size = new Size(140, 25), BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            cmbExtension.Items.AddRange(new object[] { "Todos", "Documentos", "Comics", "Videos", "Musica", "Comprimidos" });
            cmbExtension.SelectedIndex = 0;
            grid.Controls.Add(cmbExtension, 3, 0);
            
            // Fila 2: Extensión específica
            grid.Controls.Add(new Label { Text = "Extensión:", ForeColor = Color.LightGray, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 1);
            cmbExtensionFilter = new ComboBox { Size = new Size(140, 25), BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            cmbExtensionFilter.Items.AddRange(new object[] { "Todos", ".epub", ".mobi", ".pdf", ".azw3", ".txt", ".mp3", ".flac", ".m4a" });
            cmbExtensionFilter.SelectedIndex = 0;
            grid.Controls.Add(cmbExtensionFilter, 1, 1);
            
            // Bitrate mínimo
            grid.Controls.Add(new Label { Text = "Bitrate mín:", ForeColor = Color.LightGray, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 2, 1);
            var bitrateFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Dock = DockStyle.Fill };
            numMinBitrate = new NumericUpDown { Size = new Size(70, 25), BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, Maximum = 320, Increment = 32 };
            bitrateFlow.Controls.Add(numMinBitrate);
            bitrateFlow.Controls.Add(new Label { Text = "kbps", ForeColor = Color.Gray, Margin = new Padding(5, 3, 0, 0) });
            grid.Controls.Add(bitrateFlow, 3, 1);
            
            // Fila 3: Ordenar
            grid.Controls.Add(new Label { Text = "Ordenar por:", ForeColor = Color.LightGray, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 2);
            cmbSortBy = new ComboBox { Size = new Size(140, 25), BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            cmbSortBy.Items.AddRange(new object[] { "Relevancia", "Tamaño ↑", "Tamaño ↓", "Nombre A-Z", "Usuario A-Z", "Velocidad ↑" });
            cmbSortBy.SelectedIndex = 0;
            grid.Controls.Add(cmbSortBy, 1, 2);
            
            panel.AddContent(grid);
        }
        
        private void CreateUserFilters(SectionPanel panel)
        {
            var flow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            
            chkOnlyFreeSlots = new CheckBox 
            { 
                Text = "Solo usuarios con slots libres", 
                AutoSize = true, 
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 10),
                Margin = new Padding(0, 5, 15, 5)
            };
            flow.Controls.Add(chkOnlyFreeSlots);
            
            var chkHighSpeed = new CheckBox 
            { 
                Text = "Solo alta velocidad (>1MB/s)", 
                AutoSize = true, 
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 10),
                Margin = new Padding(0, 5, 15, 5)
            };
            flow.Controls.Add(chkHighSpeed);
            
            panel.AddContent(flow);
        }
        
        private void CreateSearchActions(SectionPanel panel)
        {
            var flow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            
            var btnOpenFolder = CreateStyledButton("📁 CARPETA", Color.FromArgb(0, 120, 215), 130, 40);
            btnOpenFolder.Margin = new Padding(0, 0, 8, 8);
            btnOpenFolder.Click += (s, e) => System.Diagnostics.Process.Start("explorer.exe", downloadDir);
            flow.Controls.Add(btnOpenFolder);
            
            btnClearResults = CreateStyledButton("🗑️ LIMPIAR", Color.FromArgb(180, 50, 50), 130, 40);
            btnClearResults.Margin = new Padding(0, 0, 8, 8);
            btnClearResults.Click += (s, e) =>
            {
                var result = ShowDarkDialog(
                    "¿Eliminar TODOS los resultados de búsqueda?",
                    "Confirmar limpieza",
                    MessageBoxButtons.YesNo,
                    "ADVERTENCIA"
                );
                
                if (result == DialogResult.Yes)
                {
                    SafeInvoke(() =>
                    {
                        lvResults.Items.Clear();
                        allResults?.Clear();
                        lblResultsCount.Text = "0 resultados";
                        Log("Resultados limpiados");
                    });
                }
            };
            flow.Controls.Add(btnClearResults);
            
            btnExportCSV = CreateStyledButton("📊 EXPORTAR", Color.FromArgb(0, 150, 0), 140, 40);
            btnExportCSV.Margin = new Padding(0, 0, 8, 8);
            btnExportCSV.Click += (s, e) => ExportSearchResultsToCSV();
            flow.Controls.Add(btnExportCSV);
            
            panel.AddContent(flow);
        }
        
        private void CreateSearchResults(TableLayoutPanel mainLayout)
        {
            lvResults = new ModernListView 
            { 
                Dock = DockStyle.Fill,
                VirtualMode = false
            };
            
            lvResults.Columns.Add("Usuario", 150);
            lvResults.Columns.Add("Archivo", 500);
            lvResults.Columns.Add("Tamaño", 100);
            lvResults.Columns.Add("Extensión", 80);
            lvResults.Columns.Add("Carpeta", 220);
            
            lvResults.DoubleClick += async (s, e) =>
            {
                if (lvResults.SelectedItems.Count > 0)
                {
                    await DownloadMultipleAsync();
                }
            };
            
            mainLayout.Controls.Add(lvResults, 0, 2);
        }
        
        private void ExportSearchResultsToCSV()
        {
            try
            {
                if (allResults == null || allResults.Count == 0)
                {
                    MessageBox.Show("No hay resultados para exportar", "Exportar CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                
                var csvPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "search_results.csv");
                using (var writer = new System.IO.StreamWriter(csvPath, false, System.Text.Encoding.UTF8))
                {
                    writer.WriteLine("Usuario,Archivo,Tamaño,Extensión,Carpeta,Slots Libres,Velocidad");
                    foreach (var result in allResults)
                    {
                        var size = result.Size > 1024 * 1024 ? $"{result.Size / (1024.0 * 1024):F2} MB" : $"{result.Size / 1024.0:F2} KB";
                        var ext = System.IO.Path.GetExtension(result.Filename);
                        var folder = System.IO.Path.GetDirectoryName(result.Filename)?.Replace("\\", "/");
                        writer.WriteLine($"\"{result.Username}\",\"{result.Filename}\",\"{size}\",\"{ext}\",\"{folder}\",{result.FreeUploadSlots},{result.UploadSpeed}");
                    }
                }
                
                Log($"Resultados exportados: {allResults.Count} archivos");
                MessageBox.Show($"Exportado {allResults.Count} resultados a:\n{csvPath}", "Exportar CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log($"Error al exportar CSV: {ex.Message}");
                MessageBox.Show($"Error al exportar:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
