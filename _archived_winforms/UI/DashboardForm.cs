using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SlskDown.Core;
using SlskDown.Models;

namespace SlskDown.UI
{
    /// <summary>
    /// Dashboard visual de estadísticas con gráficos en tiempo real
    /// </summary>
    public class DashboardForm : Form
    {
        private readonly StatisticsManager statsManager;
        // TODO: Descomentar cuando se corrija API de DownloadManager
        // private readonly DownloadManager downloadManager;
        
        // Controles
        private Panel mainPanel;
        private Panel statsPanel;
        private Panel chartsPanel;
        private Panel providersPanel;
        private System.Windows.Forms.Timer refreshTimer;
        
        // Labels de estadísticas
        private Label lblTotalSearches;
        private Label lblSuccessRate;
        private Label lblTotalDownloads;
        private Label lblDownloadSuccessRate;
        private Label lblTotalData;
        private Label lblAvgSpeed;
        private Label lblActiveDownloads;
        private Label lblQueuedDownloads;
        
        // Gráficos
        private PictureBox chartSpeed;
        private PictureBox chartSuccess;
        private ListBox lstTopProviders;
        
        // Datos para gráficos
        private List<double> speedHistory = new List<double>();
        private const int MAX_HISTORY_POINTS = 60; // 60 segundos
        
        public DashboardForm(StatisticsManager statistics, object downloads = null)
        {
            statsManager = statistics ?? throw new ArgumentNullException(nameof(statistics));
            // downloadManager = downloads as DownloadManager;
            
            InitializeComponents();
            ApplyDarkTheme();
            StartRefreshTimer();
        }
        
        private void InitializeComponents()
        {
            // Configuración del Form
            Text = "Dashboard - SlskDown";
            Size = new Size(1000, 700);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(800, 600);
            
            // Panel principal con scroll
            mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            Controls.Add(mainPanel);
            
            // Crear secciones
            CreateStatsSection();
            CreateChartsSection();
            CreateProvidersSection();
            
            // Botón cerrar
            var btnClose = new Button
            {
                Text = "✖ Cerrar",
                Location = new Point(mainPanel.Width - 120, mainPanel.Height - 50),
                Size = new Size(100, 35),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnClose.Click += (s, e) => Close();
            mainPanel.Controls.Add(btnClose);
        }
        
        private void CreateStatsSection()
        {
            statsPanel = new Panel
            {
                Location = new Point(10, 10),
                Size = new Size(960, 150),
                BorderStyle = BorderStyle.FixedSingle
            };
            mainPanel.Controls.Add(statsPanel);
            
            var title = new Label
            {
                Text = "📈 ESTADÍSTICAS GENERALES",
                Location = new Point(10, 10),
                Size = new Size(300, 25),
                Font = new Font("Segoe UI", 12, FontStyle.Bold)
            };
            statsPanel.Controls.Add(title);
            
            int x = 10, y = 45;
            int labelWidth = 220, labelHeight = 20;
            
            // Columna 1
            AddStatLabel("Total Búsquedas:", ref lblTotalSearches, x, y);
            y += 25;
            AddStatLabel("Tasa de Éxito:", ref lblSuccessRate, x, y);
            y += 25;
            AddStatLabel("Total Descargas:", ref lblTotalDownloads, x, y);
            y += 25;
            AddStatLabel("Éxito Descargas:", ref lblDownloadSuccessRate, x, y);
            
            // Columna 2
            x = 250; y = 45;
            AddStatLabel("Total Descargado:", ref lblTotalData, x, y);
            y += 25;
            AddStatLabel("Velocidad Promedio:", ref lblAvgSpeed, x, y);
            y += 25;
            AddStatLabel("Descargas Activas:", ref lblActiveDownloads, x, y);
            y += 25;
            AddStatLabel("En Cola:", ref lblQueuedDownloads, x, y);
        }
        
        private void AddStatLabel(string caption, ref Label valueLabel, int x, int y)
        {
            var lblCaption = new Label
            {
                Text = caption,
                Location = new Point(x, y),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            statsPanel.Controls.Add(lblCaption);
            
            valueLabel = new Label
            {
                Text = "0",
                Location = new Point(x + 155, y),
                Size = new Size(200, 20),
                Font = new Font("Segoe UI", 9)
            };
            statsPanel.Controls.Add(valueLabel);
        }
        
        private void CreateChartsSection()
        {
            chartsPanel = new Panel
            {
                Location = new Point(10, 170),
                Size = new Size(960, 300),
                BorderStyle = BorderStyle.FixedSingle
            };
            mainPanel.Controls.Add(chartsPanel);
            
            var title = new Label
            {
                Text = "📉 GRÁFICOS EN TIEMPO REAL",
                Location = new Point(10, 10),
                Size = new Size(300, 25),
                Font = new Font("Segoe UI", 12, FontStyle.Bold)
            };
            chartsPanel.Controls.Add(title);
            
            // Gráfico de velocidad
            var lblSpeed = new Label
            {
                Text = "Velocidad de Descarga (KB/s)",
                Location = new Point(10, 45),
                Size = new Size(450, 20),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            chartsPanel.Controls.Add(lblSpeed);
            
            chartSpeed = new PictureBox
            {
                Location = new Point(10, 70),
                Size = new Size(450, 200),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            chartSpeed.Paint += ChartSpeed_Paint;
            chartsPanel.Controls.Add(chartSpeed);
            
            // Gráfico de éxito
            var lblSuccess = new Label
            {
                Text = "Tasa de Éxito (%)",
                Location = new Point(480, 45),
                Size = new Size(450, 20),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            chartsPanel.Controls.Add(lblSuccess);
            
            chartSuccess = new PictureBox
            {
                Location = new Point(480, 70),
                Size = new Size(450, 200),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            chartSuccess.Paint += ChartSuccess_Paint;
            chartsPanel.Controls.Add(chartSuccess);
        }
        
        private void CreateProvidersSection()
        {
            providersPanel = new Panel
            {
                Location = new Point(10, 480),
                Size = new Size(960, 150),
                BorderStyle = BorderStyle.FixedSingle
            };
            mainPanel.Controls.Add(providersPanel);
            
            var title = new Label
            {
                Text = "🏆 TOP 10 PROVEEDORES MÁS CONFIABLES",
                Location = new Point(10, 10),
                Size = new Size(400, 25),
                Font = new Font("Segoe UI", 12, FontStyle.Bold)
            };
            providersPanel.Controls.Add(title);
            
            lstTopProviders = new ListBox
            {
                Location = new Point(10, 45),
                Size = new Size(930, 90),
                Font = new Font("Consolas", 9),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            providersPanel.Controls.Add(lstTopProviders);
        }
        
        private void ChartSpeed_Paint(object sender, PaintEventArgs e)
        {
            if (speedHistory.Count < 2) return;
            
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            
            int width = chartSpeed.Width;
            int height = chartSpeed.Height;
            int padding = 30;
            
            // Fondo
            g.Clear(Color.FromArgb(30, 30, 30));
            
            // Ejes
            using (var axisPen = new Pen(Color.Gray, 1))
            {
                g.DrawLine(axisPen, padding, height - padding, width - padding, height - padding); // X
                g.DrawLine(axisPen, padding, padding, padding, height - padding); // Y
            }
            
            // Encontrar max para escala
            double maxSpeed = speedHistory.Max();
            if (maxSpeed == 0) maxSpeed = 1;
            
            // Dibujar línea
            using (var linePen = new Pen(Color.FromArgb(0, 200, 255), 2))
            {
                var points = new List<PointF>();
                
                for (int i = 0; i < speedHistory.Count; i++)
                {
                    float x = padding + (float)(i * (width - 2 * padding) / (double)MAX_HISTORY_POINTS);
                    float y = height - padding - (float)(speedHistory[i] / maxSpeed * (height - 2 * padding));
                    points.Add(new PointF(x, y));
                }
                
                if (points.Count > 1)
                {
                    g.DrawLines(linePen, points.ToArray());
                }
            }
            
            // Labels
            using (var font = new Font("Segoe UI", 8))
            using (var brush = new SolidBrush(Color.LightGray))
            {
                g.DrawString($"{maxSpeed:F0} KB/s", font, brush, padding + 5, padding);
                g.DrawString("0", font, brush, padding + 5, height - padding - 15);
                g.DrawString($"{speedHistory.Count}s", font, brush, width - padding - 30, height - padding + 5);
            }
        }
        
        private void ChartSuccess_Paint(object sender, PaintEventArgs e)
        {
            var stats = statsManager.GetStatistics();
            
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            
            int width = chartSuccess.Width;
            int height = chartSuccess.Height;
            
            g.Clear(Color.FromArgb(30, 30, 30));
            
            // Calcular tasas
            double searchRate = stats.TotalSearches > 0 ? (stats.SuccessfulSearches * 100.0 / stats.TotalSearches) : 0;
            double downloadRate = stats.TotalDownloads > 0 ? (stats.SuccessfulDownloads * 100.0 / stats.TotalDownloads) : 0;
            
            // Dibujar barras
            int barWidth = 150;
            int barHeight = 200;
            int spacing = 100;
            int startX = (width - (2 * barWidth + spacing)) / 2;
            int startY = height - 50;
            
            // Barra búsquedas
            DrawBar(g, startX, startY, barWidth, barHeight, searchRate, "Búsquedas", Color.FromArgb(100, 200, 100));
            
            // Barra descargas
            DrawBar(g, startX + barWidth + spacing, startY, barWidth, barHeight, downloadRate, "Descargas", Color.FromArgb(100, 150, 255));
        }
        
        private void DrawBar(Graphics g, int x, int y, int width, int maxHeight, double percentage, string label, Color color)
        {
            int barHeight = (int)(maxHeight * percentage / 100.0);
            
            // Barra
            using (var brush = new SolidBrush(color))
            {
                g.FillRectangle(brush, x, y - barHeight, width, barHeight);
            }
            
            // Borde
            using (var pen = new Pen(Color.White, 2))
            {
                g.DrawRectangle(pen, x, y - barHeight, width, barHeight);
            }
            
            // Porcentaje
            using (var font = new Font("Segoe UI", 14, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.White))
            {
                string text = $"{percentage:F1}%";
                var size = g.MeasureString(text, font);
                g.DrawString(text, font, brush, x + (width - size.Width) / 2, y - barHeight - 25);
            }
            
            // Label
            using (var font = new Font("Segoe UI", 10))
            using (var brush = new SolidBrush(Color.LightGray))
            {
                var size = g.MeasureString(label, font);
                g.DrawString(label, font, brush, x + (width - size.Width) / 2, y + 10);
            }
        }
        
        private void StartRefreshTimer()
        {
            refreshTimer = new System.Windows.Forms.Timer { Interval = 1000 }; // 1 segundo
            refreshTimer.Tick += RefreshTimer_Tick;
            refreshTimer.Start();
        }
        
        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            UpdateStatistics();
            UpdateCharts();
            UpdateTopProviders();
        }
        
        private void UpdateStatistics()
        {
            var stats = statsManager.GetStatistics();
            // TODO: Descomentar cuando se corrija API de DownloadManager
            // var queue = downloadManager.GetQueueSnapshot();
            
            lblTotalSearches.Text = stats.TotalSearches.ToString("N0");
            
            double searchRate = stats.TotalSearches > 0 ? (stats.SuccessfulSearches * 100.0 / stats.TotalSearches) : 0;
            lblSuccessRate.Text = $"{searchRate:F1}%";
            
            lblTotalDownloads.Text = stats.TotalDownloads.ToString("N0");
            
            double downloadRate = stats.TotalDownloads > 0 ? (stats.SuccessfulDownloads * 100.0 / stats.TotalDownloads) : 0;
            lblDownloadSuccessRate.Text = $"{downloadRate:F1}%";
            
            lblTotalData.Text = FormatBytes(stats.TotalBytesDownloaded);
            lblAvgSpeed.Text = $"{stats.AverageDownloadSpeed / 1024:F1} KB/s";
            
            // TODO: Descomentar cuando se corrija API de DownloadManager
            // int activeCount = queue.Count(t => t.Status == DownloadStatus.Downloading);
            lblActiveDownloads.Text = "0"; // activeCount.ToString();
            
            // int queuedCount = queue.Count(t => t.Status == DownloadStatus.Pending);
            lblQueuedDownloads.Text = "0"; // queuedCount.ToString();
        }
        
        private void UpdateCharts()
        {
            var stats = statsManager.GetStatistics();
            
            // Agregar velocidad actual
            speedHistory.Add(stats.AverageDownloadSpeed / 1024);
            
            // Mantener solo últimos N puntos
            if (speedHistory.Count > MAX_HISTORY_POINTS)
            {
                speedHistory.RemoveAt(0);
            }
            
            // Redibujar gráficos
            chartSpeed.Invalidate();
            chartSuccess.Invalidate();
        }
        
        private void UpdateTopProviders()
        {
            var topProviders = statsManager.GetTopProviders(10);
            
            lstTopProviders.Items.Clear();
            lstTopProviders.Items.Add("Rank | Usuario                | Descargas | Éxito  | Velocidad    | Total");
            lstTopProviders.Items.Add("".PadRight(80, '─'));
            
            int rank = 1;
            foreach (var provider in topProviders)
            {
                string line = $"{rank,4} | {provider.Username,-22} | {provider.TotalDownloads,9} | {provider.SuccessRate,5:F1}% | {provider.AverageSpeed / 1024,8:F1} KB/s | {FormatBytes(provider.TotalBytesDownloaded),10}";
                lstTopProviders.Items.Add(line);
                rank++;
            }
        }
        
        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            
            return $"{len:F2} {sizes[order]}";
        }
        
        private void ApplyDarkTheme()
        {
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.White;
            
            foreach (Control control in Controls)
            {
                ApplyDarkThemeRecursive(control);
            }
        }
        
        private void ApplyDarkThemeRecursive(Control control)
        {
            if (control is Panel || control is Form)
            {
                control.BackColor = Color.FromArgb(45, 45, 48);
                control.ForeColor = Color.White;
            }
            else if (control is Button btn)
            {
                btn.BackColor = Color.FromArgb(60, 60, 60);
                btn.ForeColor = Color.White;
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            }
            
            foreach (Control child in control.Controls)
            {
                ApplyDarkThemeRecursive(child);
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
