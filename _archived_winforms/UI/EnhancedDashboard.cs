using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ScottPlot;
using ScottPlot.WinForms;
using SPColor = ScottPlot.Color;

namespace SlskDown.UI
{
    /// <summary>
    /// Dashboard mejorado con estadísticas avanzadas y visualizaciones
    /// </summary>
    public class EnhancedDashboard : Form
    {
        private readonly PerformanceMetrics metrics;
        private readonly Func<List<(string username, int downloads, double avgSpeed)>> getTopUsers;
        private readonly Func<List<(string extension, int count)>> getTopFiles;
        private readonly Func<Dictionary<int, int>> getActivityByHour;
        
        private ScottPlot.WinForms.FormsPlot speedChart;
        private ScottPlot.WinForms.FormsPlot activityChart;
        private ScottPlot.WinForms.FormsPlot filesChart;
        private ListView lvTopUsers;
        private ListView lvTopFiles;
        private Panel statsPanel;
        private System.Windows.Forms.Timer updateTimer;

        public EnhancedDashboard(
            PerformanceMetrics metrics,
            Func<List<(string, int, double)>> getTopUsers,
            Func<List<(string, int)>> getTopFiles,
            Func<Dictionary<int, int>> getActivityByHour)
        {
            this.metrics = metrics;
            this.getTopUsers = getTopUsers;
            this.getTopFiles = getTopFiles;
            this.getActivityByHour = getActivityByHour;
            
            InitializeComponents();
            StartAutoUpdate();
        }

        private void InitializeComponents()
        {
            Text = "Dashboard Avanzado - SlskDown";
            Size = new Size(1400, 900);
            BackColor = Color.FromArgb(20, 20, 20);
            StartPosition = FormStartPosition.CenterScreen;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(10)
            };

            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            // Panel de estadísticas generales
            statsPanel = CreateStatsPanel();
            mainLayout.Controls.Add(statsPanel, 0, 0);
            mainLayout.SetColumnSpan(statsPanel, 2);

            // Gráfico de velocidad
            speedChart = CreateSpeedChart();
            mainLayout.Controls.Add(speedChart, 0, 1);

            // Gráfico de actividad por hora
            activityChart = CreateActivityChart();
            mainLayout.Controls.Add(activityChart, 1, 1);

            // Top usuarios
            lvTopUsers = CreateTopUsersView();
            mainLayout.Controls.Add(lvTopUsers, 0, 2);

            // Top archivos
            var filesPanel = new Panel { Dock = DockStyle.Fill };
            lvTopFiles = CreateTopFilesView();
            filesChart = CreateFilesChart();
            
            var filesSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 200
            };
            filesSplit.Panel1.Controls.Add(lvTopFiles);
            filesSplit.Panel2.Controls.Add(filesChart);
            filesPanel.Controls.Add(filesSplit);
            
            mainLayout.Controls.Add(filesPanel, 1, 2);

            Controls.Add(mainLayout);
        }

        private Panel CreateStatsPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(10)
            };

            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };

            // Crear cards de estadísticas
            layout.Controls.Add(CreateStatCard("Total Búsquedas", "0", Color.FromArgb(33, 150, 243)));
            layout.Controls.Add(CreateStatCard("Archivos Bajados", "0", Color.FromArgb(76, 175, 80)));
            layout.Controls.Add(CreateStatCard("Archivos Subidos", "0", Color.FromArgb(255, 152, 0)));
            layout.Controls.Add(CreateStatCard("Velocidad Promedio", "0 KB/s", Color.FromArgb(156, 39, 176)));
            layout.Controls.Add(CreateStatCard("Tasa de Éxito", "0%", Color.FromArgb(0, 150, 136)));
            layout.Controls.Add(CreateStatCard("Total Descargado", "0 GB", Color.FromArgb(63, 81, 181)));
            layout.Controls.Add(CreateStatCard("Total Subido", "0 GB", Color.FromArgb(233, 30, 99)));
            layout.Controls.Add(CreateStatCard("Ratio Compartición", "0.00", Color.FromArgb(255, 87, 34)));

            panel.Controls.Add(layout);
            return panel;
        }

        private Panel CreateStatCard(string title, string value, System.Drawing.Color accentColor)
        {
            var card = new Panel
            {
                Width = 150,
                Height = 80,
                BackColor = Color.FromArgb(40, 40, 40),
                Margin = new Padding(5),
                Tag = title
            };

            var lblTitle = new Label
            {
                Text = title,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 8F),
                Location = new Point(10, 10),
                AutoSize = true
            };

            var lblValue = new Label
            {
                Text = value,
                ForeColor = accentColor,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                Location = new Point(10, 35),
                AutoSize = true,
                Tag = "value"
            };

            card.Controls.AddRange(new Control[] { lblTitle, lblValue });
            return card;
        }

        private FormsPlot CreateSpeedChart()
        {
            var chart = new FormsPlot
            {
                Dock = DockStyle.Fill
            };

            chart.Plot.Style(Style.Black);
            chart.Plot.Title("Velocidad de Descarga (últimos 60 min)");
            chart.Plot.XLabel("Tiempo (min)");
            chart.Plot.YLabel("Velocidad (KB/s)");

            return chart;
        }

        private FormsPlot CreateActivityChart()
        {
            var chart = new FormsPlot
            {
                Dock = DockStyle.Fill
            };

            chart.Plot.Style(Style.Black);
            chart.Plot.Title("Actividad por Hora del Día");
            chart.Plot.XLabel("Hora");
            chart.Plot.YLabel("Descargas");

            return chart;
        }

        private FormsPlot CreateFilesChart()
        {
            var chart = new FormsPlot
            {
                Dock = DockStyle.Fill
            };

            chart.Plot.Style(Style.Black);
            chart.Plot.Title("Distribución de Tipos de Archivo");

            return chart;
        }

        private ListView CreateTopUsersView()
        {
            var lv = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None
            };

            lv.Columns.Add("Usuario", 150);
            lv.Columns.Add("Descargas", 80);
            lv.Columns.Add("Vel. Promedio", 100);

            return lv;
        }

        private ListView CreateTopFilesView()
        {
            var lv = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None
            };

            lv.Columns.Add("Extensión", 100);
            lv.Columns.Add("Cantidad", 80);
            lv.Columns.Add("Porcentaje", 80);

            return lv;
        }

        private void StartAutoUpdate()
        {
            updateTimer = new System.Windows.Forms.Timer
            {
                Interval = 2000
            };
            updateTimer.Tick += (s, e) => UpdateDashboard();
            updateTimer.Start();

            UpdateDashboard();
        }

        private void UpdateDashboard()
        {
            try
            {
                UpdateStats();
                UpdateSpeedChart();
                UpdateActivityChart();
                UpdateTopUsers();
                UpdateTopFiles();
                UpdateFilesChart();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error actualizando dashboard: {ex.Message}");
            }
        }

        private void UpdateStats()
        {
            foreach (Control card in statsPanel.Controls[0].Controls)
            {
                if (card is Panel panel && panel.Tag is string title)
                {
                    var lblValue = panel.Controls.OfType<Label>().FirstOrDefault(l => l.Tag?.ToString() == "value");
                    if (lblValue == null) continue;

                    switch (title)
                    {
                        case "Total Búsquedas":
                            lblValue.Text = metrics.TotalSearches.ToString("N0");
                            break;
                        case "Archivos Bajados":
                            lblValue.Text = metrics.TotalFilesDownloaded.ToString("N0");
                            break;
                        case "Archivos Subidos":
                            lblValue.Text = metrics.TotalFilesUploaded.ToString("N0");
                            break;
                        case "Velocidad Promedio":
                            lblValue.Text = $"{metrics.AverageDownloadSpeed / 1024:F1} KB/s";
                            break;
                        case "Tasa de Éxito":
                            lblValue.Text = $"{metrics.SuccessRate:F1}%";
                            break;
                        case "Total Descargado":
                            lblValue.Text = $"{metrics.TotalBytesDownloaded / (1024.0 * 1024 * 1024):F2} GB";
                            break;
                        case "Total Subido":
                            lblValue.Text = $"{metrics.TotalBytesUploaded / (1024.0 * 1024 * 1024):F2} GB";
                            break;
                        case "Ratio Compartición":
                            var ratio = metrics.TotalBytesDownloaded > 0
                                ? (double)metrics.TotalBytesUploaded / metrics.TotalBytesDownloaded
                                : 0;
                            lblValue.Text = $"{ratio:F2}";
                            break;
                    }
                }
            }
        }

        private void UpdateSpeedChart()
        {
            var speeds = metrics.GetRecentSpeeds(60);
            if (!speeds.Any()) return;

            speedChart.Plot.Clear();

            var xs = Enumerable.Range(0, speeds.Count).Select(i => (double)i).ToArray();
            var ys = speeds.Select(s => s / 1024).ToArray();

            var signal = speedChart.Plot.AddSignal(ys);
            signal.Color = Color.FromArgb(33, 150, 243);
            signal.LineWidth = 2;

            speedChart.Refresh();
        }

        private void UpdateActivityChart()
        {
            var activity = getActivityByHour?.Invoke();
            if (activity == null || !activity.Any()) return;

            activityChart.Plot.Clear();

            var hours = activity.Keys.OrderBy(h => h).ToArray();
            var counts = hours.Select(h => (double)activity[h]).ToArray();
            var positions = hours.Select(h => (double)h).ToArray();

            var bar = activityChart.Plot.AddBar(counts, positions);
            bar.FillColor = Color.FromArgb(76, 175, 80);

            activityChart.Plot.XTicks(positions, hours.Select(h => $"{h}:00").ToArray());
            activityChart.Refresh();
        }

        private void UpdateTopUsers()
        {
            var topUsers = getTopUsers?.Invoke();
            if (topUsers == null) return;

            lvTopUsers.Items.Clear();

            foreach (var (username, downloads, avgSpeed) in topUsers.Take(10))
            {
                var item = new ListViewItem(username);
                item.SubItems.Add(downloads.ToString());
                item.SubItems.Add($"{avgSpeed / 1024:F1} KB/s");
                lvTopUsers.Items.Add(item);
            }
        }

        private void UpdateTopFiles()
        {
            var topFiles = getTopFiles?.Invoke();
            if (topFiles == null) return;

            lvTopFiles.Items.Clear();

            var total = topFiles.Sum(f => f.count);

            foreach (var (extension, count) in topFiles.Take(10))
            {
                var percentage = total > 0 ? (count * 100.0 / total) : 0;
                var item = new ListViewItem(extension);
                item.SubItems.Add(count.ToString());
                item.SubItems.Add($"{percentage:F1}%");
                lvTopFiles.Items.Add(item);
            }
        }

        private void UpdateFilesChart()
        {
            var topFiles = getTopFiles?.Invoke();
            if (topFiles == null || !topFiles.Any()) return;

            filesChart.Plot.Clear();

            var data = topFiles.Take(5).Select(f => (double)f.count).ToArray();
            var labels = topFiles.Take(5).Select(f => f.extension).ToArray();

            var pie = filesChart.Plot.AddPie(data);
            pie.SliceLabels = labels;
            pie.ShowLabels = true;

            filesChart.Refresh();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            updateTimer?.Stop();
            updateTimer?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
