using System;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using static SlskDown.UI.ModernLayout;

namespace SlskDown.UI
{
    /// <summary>
    /// Métodos de extensión para modernizar las pestañas de MainForm
    /// </summary>
    public static class ModernTabs
    {
        /// <summary>
        /// Moderniza la pestaña de Búsqueda con layout responsive
        /// </summary>
        public static void ModernizeSearchTab(Panel parent, 
            TextBox txtSearch,
            Button btnSearch,
            Button btnContinue,
            Button btnStop,
            CheckBox chkFilterSpanish,
            ListView lvResults,
            Label lblResultCount,
            Label lblSearchStatus,
            Panel advancedFiltersPanel)
        {
            parent.BackColor = Colors.Background;
            parent.Padding = new Padding(Spacing.Large);
            
            // Layout principal con TableLayoutPanel
            var mainLayout = CreateResponsiveTable(1, 3);
            mainLayout.RowStyles.Clear();
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Header
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Results
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Footer
            
            // === HEADER ===
            var headerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 0, 0, Spacing.Large)
            };
            
            var headerFlow = CreateFlowPanel(FlowDirection.TopDown, true, 0);
            headerFlow.Dock = DockStyle.Fill;
            
            // Título
            var title = CreateHeading("BÚSQUEDA", 1);
            headerFlow.Controls.Add(title);
            
            // Barra de búsqueda
            var searchBar = CreateFlowPanel(FlowDirection.LeftToRight, true, Spacing.Small);
            searchBar.Width = parent.Width - (Spacing.Large * 2);
            
            // TextBox de búsqueda más grande
            txtSearch.Font = Typography.BodyLarge;
            txtSearch.Height = 40;
            txtSearch.Width = 400;
            StyleTextBox(txtSearch, Typography.BodyLarge);
            searchBar.Controls.Add(txtSearch);
            
            // Botones modernizados
            btnSearch.Text = "Buscar";
            btnSearch.Size = ControlSizes.ButtonLarge;
            StyleButton(btnSearch, Colors.Primary, Colors.TextPrimary, Typography.ButtonLarge);
            searchBar.Controls.Add(btnSearch);
            
            btnContinue.Text = "Continuar";
            btnContinue.Size = ControlSizes.ButtonMedium;
            StyleButton(btnContinue, Colors.Success, Colors.TextPrimary, Typography.Button);
            searchBar.Controls.Add(btnContinue);
            
            btnStop.Text = "Detener";
            btnStop.Size = ControlSizes.ButtonMedium;
            StyleButton(btnStop, Colors.Error, Colors.TextPrimary, Typography.Button);
            searchBar.Controls.Add(btnStop);
            
            headerFlow.Controls.Add(searchBar);
            
            // Filtros y opciones
            var optionsBar = CreateFlowPanel(FlowDirection.LeftToRight, true, Spacing.Small);
            optionsBar.Width = parent.Width - (Spacing.Large * 2);
            
            StyleCheckBox(chkFilterSpanish, Typography.Body);
            chkFilterSpanish.Font = Typography.BodyLarge;
            optionsBar.Controls.Add(chkFilterSpanish);
            
            headerFlow.Controls.Add(optionsBar);
            
            // Panel de filtros avanzados (si existe)
            if (advancedFiltersPanel != null)
            {
                advancedFiltersPanel.BackColor = Colors.BackgroundCard;
                advancedFiltersPanel.Padding = new Padding(Spacing.Medium);
                headerFlow.Controls.Add(advancedFiltersPanel);
            }
            
            headerPanel.Controls.Add(headerFlow);
            mainLayout.Controls.Add(headerPanel, 0, 0);
            
            // === RESULTS ===
            var resultsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            
            // ListView modernizado
            StyleListView(lvResults, Typography.Body);
            lvResults.Dock = DockStyle.Fill;
            lvResults.Font = Typography.BodyLarge;
            
            // Ajustar anchos de columnas proporcionalmente
            lvResults.ColumnWidthChanged += (s, e) => { };
            
            resultsPanel.Controls.Add(lvResults);
            mainLayout.Controls.Add(resultsPanel, 0, 1);
            
            // === FOOTER ===
            var footerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Colors.BackgroundCard,
                Padding = new Padding(Spacing.Medium),
                Height = 60
            };
            
            var footerFlow = CreateFlowPanel(FlowDirection.LeftToRight, false, Spacing.Small);
            footerFlow.Dock = DockStyle.Fill;
            
            // Labels de estado más grandes
            if (lblResultCount != null)
            {
                lblResultCount.Font = Typography.BodyLarge;
                lblResultCount.ForeColor = Colors.Primary;
                footerFlow.Controls.Add(lblResultCount);
            }
            
            if (lblSearchStatus != null)
            {
                lblSearchStatus.Font = Typography.Body;
                lblSearchStatus.ForeColor = Colors.TextSecondary;
                footerFlow.Controls.Add(lblSearchStatus);
            }
            
            footerPanel.Controls.Add(footerFlow);
            mainLayout.Controls.Add(footerPanel, 0, 2);
            
            parent.Controls.Clear();
            parent.Controls.Add(mainLayout);
            
            // Hacer responsive
            parent.Resize += (s, e) =>
            {
                if (searchBar != null)
                    searchBar.Width = parent.Width - (Spacing.Large * 2);
                if (optionsBar != null)
                    optionsBar.Width = parent.Width - (Spacing.Large * 2);
            };
        }
        
        /// <summary>
        /// Moderniza la pestaña de Descargas con stats visibles
        /// </summary>
        public static void ModernizeDownloadsTab(Panel parent,
            ListView lvDownloads,
            Label lblTotalDownloads,
            Label lblActiveDownloads,
            Label lblQueuedDownloads,
            Label lblDownloadSpeed,
            Button btnPauseAll,
            Button btnResumeAll,
            Button btnClearCompleted)
        {
            parent.BackColor = Colors.Background;
            parent.Padding = new Padding(Spacing.Large);
            
            var mainLayout = CreateResponsiveTable(1, 3);
            mainLayout.RowStyles.Clear();
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Header
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Downloads list
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Stats
            
            // === HEADER ===
            var headerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 0, 0, Spacing.Large)
            };
            
            var headerFlow = CreateFlowPanel(FlowDirection.TopDown, true, 0);
            headerFlow.Dock = DockStyle.Fill;
            
            var title = CreateHeading("DESCARGAS", 1);
            headerFlow.Controls.Add(title);
            
            // Botones de control
            var controlBar = CreateFlowPanel(FlowDirection.LeftToRight, true, Spacing.Small);
            
            if (btnPauseAll != null)
            {
                btnPauseAll.Text = "Pausar Todo";
                btnPauseAll.Size = ControlSizes.ButtonMedium;
                StyleButton(btnPauseAll, Colors.Warning, Colors.TextPrimary, Typography.Button);
                controlBar.Controls.Add(btnPauseAll);
            }
            
            if (btnResumeAll != null)
            {
                btnResumeAll.Text = "Reanudar Todo";
                btnResumeAll.Size = ControlSizes.ButtonMedium;
                StyleButton(btnResumeAll, Colors.Success, Colors.TextPrimary, Typography.Button);
                controlBar.Controls.Add(btnResumeAll);
            }
            
            if (btnClearCompleted != null)
            {
                btnClearCompleted.Text = "Limpiar Completadas";
                btnClearCompleted.Size = ControlSizes.ButtonMedium;
                StyleButton(btnClearCompleted, Colors.BackgroundHover, Colors.TextPrimary, Typography.Button);
                controlBar.Controls.Add(btnClearCompleted);
            }
            
            headerFlow.Controls.Add(controlBar);
            headerPanel.Controls.Add(headerFlow);
            mainLayout.Controls.Add(headerPanel, 0, 0);
            
            // === DOWNLOADS LIST ===
            var listPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            
            StyleListView(lvDownloads, Typography.Body);
            lvDownloads.Dock = DockStyle.Fill;
            lvDownloads.Font = Typography.BodyLarge;
            
            listPanel.Controls.Add(lvDownloads);
            mainLayout.Controls.Add(listPanel, 0, 1);
            
            // === STATS PANEL ===
            var statsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Colors.BackgroundCard,
                Padding = new Padding(Spacing.Large),
                Height = 100
            };
            
            var statsLayout = CreateResponsiveTable(4, 1);
            statsLayout.Dock = DockStyle.Fill;
            statsLayout.ColumnStyles.Clear();
            statsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            statsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            statsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            statsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            
            // Stat cards
            var statCards = new[]
            {
                (lblTotalDownloads, "Total", Colors.Primary),
                (lblActiveDownloads, "Activas", Colors.Success),
                (lblQueuedDownloads, "En Cola", Colors.Warning),
                (lblDownloadSpeed, "Velocidad", Colors.Cyan)
            };
            
            for (int i = 0; i < statCards.Length; i++)
            {
                var (label, titleText, color) = statCards[i];
                if (label != null)
                {
                    var card = CreateStatCard(label, titleText, color);
                    statsLayout.Controls.Add(card, i, 0);
                }
            }
            
            statsPanel.Controls.Add(statsLayout);
            mainLayout.Controls.Add(statsPanel, 0, 2);
            
            parent.Controls.Clear();
            parent.Controls.Add(mainLayout);
        }
        
        /// <summary>
        /// Crea una tarjeta de estadística
        /// </summary>
        private static Panel CreateStatCard(Label valueLabel, string cardTitle, Color accentColor)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Colors.BackgroundLight,
                Padding = new Padding(Spacing.Medium),
                Margin = new Padding(Spacing.Small)
            };
            
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Color.Transparent
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            
            var titleLabel = CreateLabel(cardTitle, Typography.BodySmall, Colors.TextSecondary);
            layout.Controls.Add(titleLabel, 0, 0);
            
            valueLabel.Font = Typography.Heading3;
            valueLabel.ForeColor = accentColor;
            valueLabel.Dock = DockStyle.Fill;
            layout.Controls.Add(valueLabel, 0, 1);
            
            card.Controls.Add(layout);
            return card;
        }
    }
}
