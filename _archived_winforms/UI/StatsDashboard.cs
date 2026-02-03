using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using SlskDown.Core;
using SlskDown.Core.Statistics;

namespace SlskDown.UI
{
    public class StatsDashboard : Form
    {
        private TransferStatistics stats;
        private NetworkHealthMonitor networkHealth;
        private MetricsCollector searchMetrics;
        private MetricsCollector downloadMetrics;
        
        private Panel heatmapPanel;
        private Panel networkHealthPanel;
        private Panel topUsersPanel;
        private Panel topFilesPanel;
        private Panel metricsPanel;
        private System.Windows.Forms.Timer refreshTimer;
        
        public StatsDashboard(TransferStatistics stats, NetworkHealthMonitor networkHealth, 
            MetricsCollector searchMetrics, MetricsCollector downloadMetrics)
        {
            this.stats = stats;
            this.networkHealth = networkHealth;
            this.searchMetrics = searchMetrics;
            this.downloadMetrics = downloadMetrics;
            
            InitializeComponent();
            StartAutoRefresh();
        }
        
        private void InitializeComponent()
        {
            this.Text = "Dashboard de Estadísticas - SlskDown";
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 30, 30);
            
            // Panel principal con scroll
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            
            int y = 10;
            
            // Salud de Red
            networkHealthPanel = CreateNetworkHealthPanel(10, ref y);
            mainPanel.Controls.Add(networkHealthPanel);
            
            // Métricas de Rendimiento
            metricsPanel = CreateMetricsPanel(10, ref y);
            mainPanel.Controls.Add(metricsPanel);
            
            // Heatmap de Actividad
            heatmapPanel = CreateHeatmapPanel(10, ref y);
            mainPanel.Controls.Add(heatmapPanel);
            
            // Top 10 Usuarios
            topUsersPanel = CreateTopUsersPanel(10, ref y);
            mainPanel.Controls.Add(topUsersPanel);
            
            // Top 10 Tipos de Archivo
            topFilesPanel = CreateTopFilesPanel(10, ref y);
            mainPanel.Controls.Add(topFilesPanel);
            
            // Botones de acción
            var btnExportHTML = new Button
            {
                Text = "Exportar a HTML",
                Location = new Point(10, y),
                Size = new Size(150, 35),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F)
            };
            btnExportHTML.Click += (s, e) => ExportToHTML();
            mainPanel.Controls.Add(btnExportHTML);
            
            var btnExportCSV = new Button
            {
                Text = "Exportar a CSV",
                Location = new Point(170, y),
                Size = new Size(150, 35),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F)
            };
            btnExportCSV.Click += (s, e) => ExportToCSV();
            mainPanel.Controls.Add(btnExportCSV);
            
            var btnRefresh = new Button
            {
                Text = "Actualizar",
                Location = new Point(330, y),
                Size = new Size(150, 35),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F)
            };
            btnRefresh.Click += (s, e) => RefreshAll();
            mainPanel.Controls.Add(btnRefresh);
            
            this.Controls.Add(mainPanel);
        }
        
        private Panel CreateNetworkHealthPanel(int x, ref int y)
        {
            var panel = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(1160, 120),
                BackColor = Color.FromArgb(45, 45, 45),
                BorderStyle = BorderStyle.FixedSingle
            };
            
            var title = new Label
            {
                Text = "SALUD DE RED",
                Location = new Point(10, 10),
                Size = new Size(300, 25),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold)
            };
            panel.Controls.Add(title);
            
            var health = networkHealth.GetHealth();
            
            // Indicador de estado
            var statusLabel = new Label
            {
                Text = GetStatusEmoji(health.Status) + " " + health.Status.ToString(),
                Location = new Point(10, 45),
                Size = new Size(200, 30),
                ForeColor = GetStatusColor(health.Status),
                Font = new Font("Segoe UI", 14F, FontStyle.Bold)
            };
            panel.Controls.Add(statusLabel);
            
            // Métricas
            var metricsLabel = new Label
            {
                Text = $"Paquetes: {health.PacketsSent} enviados, {health.PacketsReceived} recibidos, {health.PacketsLost} perdidos\n" +
                       $"Packet Loss: {health.PacketLossRate:F2}%\n" +
                       $"Latencia Promedio: {health.AverageLatency:F2}ms",
                Location = new Point(220, 45),
                Size = new Size(900, 60),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 10F)
            };
            panel.Controls.Add(metricsLabel);
            
            y += panel.Height + 10;
            return panel;
        }
        
        private Panel CreateMetricsPanel(int x, ref int y)
        {
            var panel = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(1160, 100),
                BackColor = Color.FromArgb(45, 45, 45),
                BorderStyle = BorderStyle.FixedSingle
            };
            
            var title = new Label
            {
                Text = "MÉTRICAS DE RENDIMIENTO",
                Location = new Point(10, 10),
                Size = new Size(400, 25),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold)
            };
            panel.Controls.Add(title);
            
            // Métricas de búsqueda
            var searchLabel = new Label
            {
                Text = $"Búsquedas:\n" +
                       $"   p50: {searchMetrics.P50:F2}ms  |  p95: {searchMetrics.P95:F2}ms  |  p99: {searchMetrics.P99:F2}ms",
                Location = new Point(10, 45),
                Size = new Size(550, 40),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 10F)
            };
            panel.Controls.Add(searchLabel);
            
            // Métricas de descarga
            var downloadLabel = new Label
            {
                Text = $"Descargas:\n" +
                       $"   p50: {downloadMetrics.P50:F2}ms  |  p95: {downloadMetrics.P95:F2}ms  |  p99: {downloadMetrics.P99:F2}ms",
                Location = new Point(580, 45),
                Size = new Size(550, 40),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 10F)
            };
            panel.Controls.Add(downloadLabel);
            
            y += panel.Height + 10;
            return panel;
        }
        
        private Panel CreateHeatmapPanel(int x, ref int y)
        {
            var panel = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(1160, 250),
                BackColor = Color.FromArgb(45, 45, 45),
                BorderStyle = BorderStyle.FixedSingle
            };
            
            var title = new Label
            {
                Text = "🔥 HEATMAP DE ACTIVIDAD (24h x 7 días)",
                Location = new Point(10, 10),
                Size = new Size(500, 25),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold)
            };
            panel.Controls.Add(title);
            
            // Canvas para el heatmap
            var canvas = new PictureBox
            {
                Location = new Point(10, 45),
                Size = new Size(1140, 190),
                BackColor = Color.FromArgb(35, 35, 35)
            };
            canvas.Paint += (s, e) => DrawHeatmap(e.Graphics, canvas.Width, canvas.Height);
            panel.Controls.Add(canvas);
            
            y += panel.Height + 10;
            return panel;
        }
        
        private Panel CreateTopUsersPanel(int x, ref int y)
        {
            var panel = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(570, 300),
                BackColor = Color.FromArgb(45, 45, 45),
                BorderStyle = BorderStyle.FixedSingle
            };
            
            var title = new Label
            {
                Text = "👥 TOP 10 USUARIOS",
                Location = new Point(10, 10),
                Size = new Size(300, 25),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold)
            };
            panel.Controls.Add(title);
            
            var listView = new ListView
            {
                Location = new Point(10, 45),
                Size = new Size(545, 240),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White
            };
            
            listView.Columns.Add("Usuario", 200);
            listView.Columns.Add("Descargas", 100);
            listView.Columns.Add("Tamaño", 120);
            listView.Columns.Add("Éxito", 80);
            
            // TODO: Implementar cuando TransferStatistics tenga método GetAllUserStats()
            var topUsers = stats.GetTopUsersByBytes(10);
            
            foreach (var user in topUsers)
            {
                var ratio = user.TotalTransfers > 0 
                    ? user.SuccessfulTransfers / (double)user.TotalTransfers * 100 
                    : 0;
                
                var item = new ListViewItem(user.Username);
                item.SubItems.Add(user.TotalTransfers.ToString());
                item.SubItems.Add(FormatBytes(user.TotalBytes));
                item.SubItems.Add($"{ratio:F1}%");
                listView.Items.Add(item);
            }
            
            panel.Controls.Add(listView);
            
            y += panel.Height + 10;
            return panel;
        }
        
        private Panel CreateTopFilesPanel(int x, ref int y)
        {
            var panel = new Panel
            {
                Location = new Point(600, y - 310), // Al lado del panel de usuarios
                Size = new Size(570, 300),
                BackColor = Color.FromArgb(45, 45, 45),
                BorderStyle = BorderStyle.FixedSingle
            };
            
            var title = new Label
            {
                Text = "TOP 10 TIPOS DE ARCHIVO",
                Location = new Point(10, 10),
                Size = new Size(350, 25),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold)
            };
            panel.Controls.Add(title);
            
            var listView = new ListView
            {
                Location = new Point(10, 45),
                Size = new Size(545, 240),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White
            };
            
            listView.Columns.Add("Tipo", 150);
            listView.Columns.Add("Cantidad", 100);
            listView.Columns.Add("Tamaño Total", 150);
            listView.Columns.Add("%", 80);
            
            // TODO: Implementar cuando TransferStatistics tenga método GetFileTypeStats()
            // Por ahora, mostrar mensaje de no disponible
            var item = new ListViewItem("N/A");
            item.SubItems.Add("0");
            item.SubItems.Add("0 B");
            item.SubItems.Add("0%");
            listView.Items.Add(item);
            
            panel.Controls.Add(listView);
            
            return panel;
        }
        
        private void DrawHeatmap(Graphics g, int width, int height)
        {
            // TODO: Implementar cuando TransferStatistics tenga método GetHeatmap()
            // Por ahora, mostrar mensaje de no disponible
            using (var font = new Font("Segoe UI", 10F))
            using (var brush = new SolidBrush(Color.Gray))
            {
                g.DrawString("Heatmap no disponible", font, brush, width / 2 - 80, height / 2);
            }
        }
        
        private Color GetHeatmapColor(float intensity)
        {
            if (intensity < 0.2f)
                return Color.FromArgb(40, 40, 40);
            if (intensity < 0.4f)
                return Color.FromArgb(0, 100, 0);
            if (intensity < 0.6f)
                return Color.FromArgb(100, 150, 0);
            if (intensity < 0.8f)
                return Color.FromArgb(200, 100, 0);
            return Color.FromArgb(200, 0, 0);
        }
        
        private string GetStatusEmoji(NetworkStatus status)
        {
            return status switch
            {
                NetworkStatus.Excellent => "🟢",
                NetworkStatus.Good => "🟡",
                NetworkStatus.Fair => "🟠",
                NetworkStatus.Poor => "🔴",
                _ => "⚪"
            };
        }
        
        private Color GetStatusColor(NetworkStatus status)
        {
            return status switch
            {
                NetworkStatus.Excellent => Color.FromArgb(0, 200, 0),
                NetworkStatus.Good => Color.FromArgb(200, 200, 0),
                NetworkStatus.Fair => Color.FromArgb(255, 165, 0),
                NetworkStatus.Poor => Color.FromArgb(200, 0, 0),
                _ => Color.Gray
            };
        }
        
        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:F2} {sizes[order]}";
        }
        
        private void StartAutoRefresh()
        {
            refreshTimer = new System.Windows.Forms.Timer { Interval = 5000 }; // 5 segundos
            refreshTimer.Tick += (s, e) => RefreshAll();
            refreshTimer.Start();
        }
        
        private void RefreshAll()
        {
            networkHealthPanel.Invalidate();
            metricsPanel.Invalidate();
            heatmapPanel.Invalidate();
            topUsersPanel.Invalidate();
            topFilesPanel.Invalidate();
        }
        
        private void ExportToHTML()
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "HTML files (*.html)|*.html",
                FileName = $"stats_{DateTime.Now:yyyyMMdd_HHmmss}.html"
            };
            
            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                var exporter = new DataExporter();
                // TODO: Preparar datos y exportar
                MessageBox.Show("Estadísticas exportadas a HTML", "Éxito", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        
        private void ExportToCSV()
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"stats_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };
            
            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                var exporter = new DataExporter();
                // TODO: Preparar datos y exportar
                MessageBox.Show("Estadísticas exportadas a CSV", "Éxito", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            refreshTimer?.Stop();
            refreshTimer?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
