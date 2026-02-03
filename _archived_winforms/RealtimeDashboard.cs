using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace SlskDown
{
    /// <summary>
    /// Dashboard de métricas en tiempo real con gráficos
    /// Características: CPU, memoria, velocidad, búsquedas, descargas, latencia
    /// </summary>
    public class RealtimeDashboard : Form
    {
        private readonly System.Windows.Forms.Timer updateTimer;
        private readonly PerformanceCounter cpuCounter;
        private readonly Process currentProcess;
        
        // Controles UI
        private Panel pnlMain;
        private TableLayoutPanel layoutMain;
        
        // Métricas en tiempo real
        private Label lblConnectionStatus;
        private Label lblHealthStatus;
        private Label lblCpuUsage;
        private Label lblMemoryUsage;
        private Label lblSearchRate;
        private Label lblDownloadRate;
        private Label lblSuccessRate;
        private Label lblLatency;
        
        // Gráficos
        private ProgressBar pbCpu;
        private ProgressBar pbMemory;
        private Panel pnlSpeedGraph;
        private Panel pnlSearchGraph;
        
        // Historial para gráficos
        private Queue<double> speedHistory = new Queue<double>(60); // 60 segundos
        private Queue<double> searchHistory = new Queue<double>(60);
        private Queue<double> cpuHistory = new Queue<double>(60);
        
        // Referencia a servicios
        private TelemetryService telemetry;
        private HealthMonitor healthMonitor;
        
        public RealtimeDashboard(TelemetryService telemetry, HealthMonitor healthMonitor)
        {
            this.telemetry = telemetry;
            this.healthMonitor = healthMonitor;
            this.currentProcess = Process.GetCurrentProcess();
            this.cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            
            InitializeUI();
            
            // Timer para actualizar cada segundo
            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = 1000;
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();
        }
        
        private void InitializeUI()
        {
            // Configuración del Form
            this.Text = "📊 Dashboard de Métricas - SlskDown";
            this.Size = new Size(900, 600);
            this.BackColor = Color.FromArgb(18, 18, 18);
            this.ForeColor = Color.White;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            
            // Panel principal
            pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 18, 18),
                Padding = new Padding(20)
            };
            
            // Layout principal
            layoutMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                BackColor = Color.FromArgb(18, 18, 18)
            };
            
            layoutMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layoutMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layoutMain.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Header
            layoutMain.RowStyles.Add(new RowStyle(SizeType.Percent, 25)); // Conexión
            layoutMain.RowStyles.Add(new RowStyle(SizeType.Percent, 25)); // Recursos
            layoutMain.RowStyles.Add(new RowStyle(SizeType.Percent, 25)); // Actividad
            layoutMain.RowStyles.Add(new RowStyle(SizeType.Percent, 25)); // Gráficos
            
            // Header
            var lblTitle = new Label
            {
                Text = "📊 DASHBOARD DE MÉTRICAS EN TIEMPO REAL",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 200, 255),
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 60
            };
            layoutMain.Controls.Add(lblTitle, 0, 0);
            layoutMain.SetColumnSpan(lblTitle, 2);
            
            // Sección: Estado de Conexión
            CreateConnectionSection();
            
            // Sección: Recursos del Sistema
            CreateResourcesSection();
            
            // Sección: Actividad
            CreateActivitySection();
            
            // Sección: Gráficos
            CreateGraphsSection();
            
            pnlMain.Controls.Add(layoutMain);
            this.Controls.Add(pnlMain);
        }
        
        private void CreateConnectionSection()
        {
            var pnlConnection = CreateMetricPanel("🔌 ESTADO DE CONEXIÓN");
            
            lblConnectionStatus = CreateMetricLabel("Estado: Conectado", Color.LightGreen);
            lblHealthStatus = CreateMetricLabel("Salud: Excelente", Color.LightGreen);
            lblLatency = CreateMetricLabel("Latencia: 0 ms", Color.White);
            
            pnlConnection.Controls.Add(lblConnectionStatus);
            pnlConnection.Controls.Add(lblHealthStatus);
            pnlConnection.Controls.Add(lblLatency);
            
            layoutMain.Controls.Add(pnlConnection, 0, 1);
        }
        
        private void CreateResourcesSection()
        {
            var pnlResources = CreateMetricPanel("💻 RECURSOS DEL SISTEMA");
            
            lblCpuUsage = CreateMetricLabel("CPU: 0%", Color.White);
            pbCpu = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 20,
                Style = ProgressBarStyle.Continuous,
                ForeColor = Color.FromArgb(100, 200, 255)
            };
            
            lblMemoryUsage = CreateMetricLabel("Memoria: 0 MB", Color.White);
            pbMemory = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 20,
                Style = ProgressBarStyle.Continuous,
                ForeColor = Color.FromArgb(255, 150, 100)
            };
            
            pnlResources.Controls.Add(lblCpuUsage);
            pnlResources.Controls.Add(pbCpu);
            pnlResources.Controls.Add(lblMemoryUsage);
            pnlResources.Controls.Add(pbMemory);
            
            layoutMain.Controls.Add(pnlResources, 1, 1);
        }
        
        private void CreateActivitySection()
        {
            var pnlActivity = CreateMetricPanel("📈 ACTIVIDAD");
            
            lblSearchRate = CreateMetricLabel("Búsquedas: 0/min", Color.White);
            lblDownloadRate = CreateMetricLabel("Descargas: 0 MB/s", Color.White);
            lblSuccessRate = CreateMetricLabel("Tasa de éxito: 0%", Color.White);
            
            pnlActivity.Controls.Add(lblSearchRate);
            pnlActivity.Controls.Add(lblDownloadRate);
            pnlActivity.Controls.Add(lblSuccessRate);
            
            layoutMain.Controls.Add(pnlActivity, 0, 2);
            layoutMain.SetRowSpan(pnlActivity, 2);
        }
        
        private void CreateGraphsSection()
        {
            var pnlGraphs = CreateMetricPanel("📊 GRÁFICOS (Últimos 60s)");
            
            pnlSpeedGraph = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(30, 30, 30),
                BorderStyle = BorderStyle.FixedSingle
            };
            pnlSpeedGraph.Paint += PnlSpeedGraph_Paint;
            
            pnlSearchGraph = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(30, 30, 30),
                BorderStyle = BorderStyle.FixedSingle
            };
            pnlSearchGraph.Paint += PnlSearchGraph_Paint;
            
            pnlGraphs.Controls.Add(new Label 
            { 
                Text = "Velocidad de Descarga", 
                Dock = DockStyle.Top, 
                ForeColor = Color.LightGray,
                Height = 20
            });
            pnlGraphs.Controls.Add(pnlSpeedGraph);
            pnlGraphs.Controls.Add(new Label 
            { 
                Text = "Búsquedas por Minuto", 
                Dock = DockStyle.Top, 
                ForeColor = Color.LightGray,
                Height = 20,
                Margin = new Padding(0, 10, 0, 0)
            });
            pnlGraphs.Controls.Add(pnlSearchGraph);
            
            layoutMain.Controls.Add(pnlGraphs, 1, 2);
            layoutMain.SetRowSpan(pnlGraphs, 2);
        }
        
        private Panel CreateMetricPanel(string title)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(25, 25, 25),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(15),
                Margin = new Padding(5)
            };
            
            var lblTitle = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 200, 255),
                Height = 30
            };
            
            panel.Controls.Add(lblTitle);
            return panel;
        }
        
        private Label CreateMetricLabel(string text, Color color)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 10),
                ForeColor = color,
                Height = 25,
                Padding = new Padding(5, 5, 0, 0)
            };
        }
        
        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                UpdateMetrics();
                UpdateGraphs();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Dashboard] Error actualizando: {ex.Message}");
            }
        }
        
        private void UpdateMetrics()
        {
            // CPU
            float cpuUsage = cpuCounter.NextValue();
            lblCpuUsage.Text = $"CPU: {cpuUsage:F1}%";
            pbCpu.Value = Math.Min((int)cpuUsage, 100);
            cpuHistory.Enqueue(cpuUsage);
            if (cpuHistory.Count > 60) cpuHistory.Dequeue();
            
            // Memoria
            long memoryMB = currentProcess.WorkingSet64 / 1024 / 1024;
            lblMemoryUsage.Text = $"Memoria: {memoryMB:N0} MB";
            pbMemory.Value = Math.Min((int)(memoryMB / 10), 100); // Escala a 1GB max
            
            // Health Monitor
            if (healthMonitor != null)
            {
                var stats = healthMonitor.GetStats();
                lblHealthStatus.Text = stats.CircuitBreakerOpen 
                    ? "Salud: ⚠️ Degradada" 
                    : "Salud: ✅ Excelente";
                lblHealthStatus.ForeColor = stats.CircuitBreakerOpen ? Color.Orange : Color.LightGreen;
                
                lblLatency.Text = $"Latencia: {stats.AverageLatencyMs:F1} ms";
            }
            
            // Telemetría
            if (telemetry != null)
            {
                long searches = telemetry.GetCounter("search.completed");
                long downloads = telemetry.GetCounter("downloads.completed");
                long failed = telemetry.GetCounter("downloads.failed");
                
                double successRate = (downloads + failed) > 0 
                    ? (double)downloads / (downloads + failed) * 100 
                    : 0;
                
                lblSearchRate.Text = $"Búsquedas: {searches:N0} total";
                lblDownloadRate.Text = $"Descargas: {downloads:N0} completadas";
                lblSuccessRate.Text = $"Tasa de éxito: {successRate:F1}%";
                lblSuccessRate.ForeColor = successRate > 80 ? Color.LightGreen : 
                                           successRate > 50 ? Color.Orange : Color.Red;
            }
        }
        
        private void UpdateGraphs()
        {
            pnlSpeedGraph?.Invalidate();
            pnlSearchGraph?.Invalidate();
        }
        
        private void PnlSpeedGraph_Paint(object sender, PaintEventArgs e)
        {
            DrawLineGraph(e.Graphics, speedHistory, pnlSpeedGraph.Size, Color.FromArgb(100, 200, 255));
        }
        
        private void PnlSearchGraph_Paint(object sender, PaintEventArgs e)
        {
            DrawLineGraph(e.Graphics, searchHistory, pnlSearchGraph.Size, Color.FromArgb(255, 150, 100));
        }
        
        private void DrawLineGraph(Graphics g, Queue<double> data, Size size, Color color)
        {
            if (data.Count < 2) return;
            
            var points = new List<PointF>();
            var dataArray = data.ToArray();
            double max = dataArray.Max();
            if (max == 0) max = 1;
            
            for (int i = 0; i < dataArray.Length; i++)
            {
                float x = (float)i / (dataArray.Length - 1) * size.Width;
                float y = size.Height - (float)(dataArray[i] / max * (size.Height - 10));
                points.Add(new PointF(x, y));
            }
            
            using (var pen = new Pen(color, 2))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.DrawLines(pen, points.ToArray());
            }
        }
        
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            updateTimer?.Stop();
            updateTimer?.Dispose();
            cpuCounter?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
