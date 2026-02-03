using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ScottPlot.WinForms;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using DrawingFontStyle = System.Drawing.FontStyle;
using DrawingContentAlignment = System.Drawing.ContentAlignment;

namespace SlskDown.UI
{
    /// <summary>
    /// Dashboard de rendimiento en tiempo real
    /// Muestra métricas, gráficos y estadísticas del sistema
    /// </summary>
    public class PerformanceDashboard : Form
    {
        private readonly PerformanceMetrics _metrics;
        private FormsPlot _speedChart;
        private FormsPlot _successRateChart;
        private System.Windows.Forms.Label _lblTotalSearches;
        private System.Windows.Forms.Label _lblAvgSearchTime;
        private System.Windows.Forms.Label _lblAvgDownloadSpeed;
        private System.Windows.Forms.Label _lblSuccessRate;
        private System.Windows.Forms.Label _lblActiveDownloads;
        private System.Windows.Forms.Label _lblTotalDownloaded;
        private System.Windows.Forms.Timer _updateTimer;

        public PerformanceDashboard(PerformanceMetrics metrics)
        {
            _metrics = metrics;
            InitializeComponents();
            StartAutoUpdate();
        }

        private void InitializeComponents()
        {
            Text = "Dashboard de Rendimiento";
            Size = new Size(1000, 700);
            StartPosition = FormStartPosition.CenterScreen;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(10)
            };

            // Fila 1: Métricas principales
            var metricsPanel = CreateMetricsPanel();
            mainLayout.Controls.Add(metricsPanel, 0, 0);
            mainLayout.SetColumnSpan(metricsPanel, 2);

            // Fila 2: Gráfico de velocidad
            _speedChart = new FormsPlot
            {
                Dock = DockStyle.Fill,
                Height = 250
            };
            ConfigureSpeedChart();
            mainLayout.Controls.Add(_speedChart, 0, 1);

            // Fila 3: Gráfico de tasa de éxito
            _successRateChart = new FormsPlot
            {
                Dock = DockStyle.Fill,
                Height = 250
            };
            ConfigureSuccessRateChart();
            mainLayout.Controls.Add(_successRateChart, 1, 1);

            Controls.Add(mainLayout);
        }

        private Panel CreateMetricsPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 150,
                BackColor = DrawingColor.FromArgb(240, 240, 240)
            };

            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(10)
            };

            // Métricas individuales
            _lblTotalSearches = CreateMetricLabel("Total Búsquedas", "0");
            _lblAvgSearchTime = CreateMetricLabel("Tiempo Promedio", "0ms");
            _lblAvgDownloadSpeed = CreateMetricLabel("Velocidad Promedio", "0 KB/s");
            _lblSuccessRate = CreateMetricLabel("Tasa de Éxito", "0%");
            _lblActiveDownloads = CreateMetricLabel("Descargas Activas", "0");
            _lblTotalDownloaded = CreateMetricLabel("Total Descargado", "0 MB");

            layout.Controls.Add(_lblTotalSearches);
            layout.Controls.Add(_lblAvgSearchTime);
            layout.Controls.Add(_lblAvgDownloadSpeed);
            layout.Controls.Add(_lblSuccessRate);
            layout.Controls.Add(_lblActiveDownloads);
            layout.Controls.Add(_lblTotalDownloaded);

            panel.Controls.Add(layout);
            return panel;
        }

        private System.Windows.Forms.Label CreateMetricLabel(string title, string value)
        {
            var container = new Panel
            {
                Width = 150,
                Height = 80,
                Margin = new Padding(5),
                BackColor = DrawingColor.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblTitle = new System.Windows.Forms.Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Font = new DrawingFont("Segoe UI", 9, DrawingFontStyle.Bold),
                ForeColor = DrawingColor.Gray,
                TextAlign = DrawingContentAlignment.MiddleCenter,
                Height = 30
            };

            var lblValue = new System.Windows.Forms.Label
            {
                Text = value,
                Dock = DockStyle.Fill,
                Font = new DrawingFont("Segoe UI", 14, DrawingFontStyle.Bold),
                ForeColor = DrawingColor.FromArgb(0, 120, 215),
                TextAlign = DrawingContentAlignment.MiddleCenter
            };

            container.Controls.Add(lblValue);
            container.Controls.Add(lblTitle);

            return lblValue;
        }

        private void ConfigureSpeedChart()
        {
            _speedChart.Plot.Title("Velocidad de Descarga (últimos 60 min)");
            _speedChart.Plot.XLabel("Tiempo");
            _speedChart.Plot.YLabel("KB/s");
        }

        private void ConfigureSuccessRateChart()
        {
            _successRateChart.Plot.Title("Tasa de Éxito de Búsquedas");
            _successRateChart.Plot.XLabel("Hora");
            _successRateChart.Plot.YLabel("% Éxito");
        }

        private void StartAutoUpdate()
        {
            _updateTimer = new System.Windows.Forms.Timer
            {
                Interval = 2000 // Actualizar cada 2 segundos
            };
            _updateTimer.Tick += (s, e) => UpdateDashboard();
            _updateTimer.Start();
        }

        private void UpdateDashboard()
        {
            if (_metrics == null) return;

            // Actualizar métricas
            _lblTotalSearches.Text = _metrics.TotalSearches.ToString();
            _lblAvgSearchTime.Text = $"{_metrics.AverageSearchTime:F0}ms";
            _lblAvgDownloadSpeed.Text = $"{_metrics.AverageDownloadSpeed / 1024:F1} KB/s";
            _lblSuccessRate.Text = $"{_metrics.SuccessRate:F1}%";
            _lblActiveDownloads.Text = _metrics.ActiveDownloads.ToString();
            _lblTotalDownloaded.Text = $"{_metrics.TotalBytesDownloaded / (1024 * 1024):F0} MB";

            // Actualizar gráficos
            UpdateSpeedChart();
            UpdateSuccessRateChart();
        }

        private void UpdateSpeedChart()
        {
            var speeds = _metrics.GetRecentSpeeds(60); // Últimos 60 minutos
            if (!speeds.Any()) return;

            _speedChart.Plot.Clear();
            
            var times = speeds.Select((s, i) => (double)i).ToArray();
            var values = speeds.Select(s => s / 1024.0).ToArray(); // Convertir a KB/s

            var scatter = _speedChart.Plot.Add.Scatter(times, values);
            scatter.Color = ScottPlot.Color.FromColor(DrawingColor.Blue);
            scatter.LineWidth = 2;
            _speedChart.Plot.Axes.AutoScale();
            _speedChart.Refresh();
        }

        private void UpdateSuccessRateChart()
        {
            var rates = _metrics.GetHourlySuccessRates(24); // Últimas 24 horas
            if (!rates.Any()) return;

            _successRateChart.Plot.Clear();
            
            var hours = rates.Select((r, i) => (double)i).ToArray();
            var values = rates.ToArray();

            var scatter = _successRateChart.Plot.Add.Scatter(hours, values);
            scatter.Color = ScottPlot.Color.FromColor(DrawingColor.Green);
            scatter.LineWidth = 2;
            _successRateChart.Plot.Axes.SetLimitsY(0, 100);
            _successRateChart.Refresh();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
            base.OnFormClosing(e);
        }
    }

    /// <summary>
    /// Métricas de rendimiento del sistema
    /// </summary>
    public class PerformanceMetrics
    {
        private readonly List<SpeedSample> _speedHistory;
        private readonly List<SearchMetric> _searchHistory;
        private readonly object _lock = new object();

        public int TotalSearches { get; private set; }
        public double AverageSearchTime { get; private set; }
        public double AverageDownloadSpeed { get; private set; }
        public double SuccessRate { get; private set; }
        public int ActiveDownloads { get; set; }
        public long TotalBytesDownloaded { get; private set; }
        
        // Contadores de archivos
        public int TotalFilesDownloaded { get; private set; }
        public int TotalFilesUploaded { get; private set; }
        public long TotalBytesUploaded { get; private set; }

        public PerformanceMetrics()
        {
            _speedHistory = new List<SpeedSample>();
            _searchHistory = new List<SearchMetric>();
        }

        public void RecordSearch(TimeSpan duration, int resultsCount, bool success)
        {
            lock (_lock)
            {
                _searchHistory.Add(new SearchMetric
                {
                    Timestamp = DateTime.Now,
                    Duration = duration,
                    ResultsCount = resultsCount,
                    Success = success
                });

                TotalSearches++;
                
                // Calcular promedio de tiempo de búsqueda
                var recentSearches = _searchHistory.Where(s => s.Timestamp > DateTime.Now.AddHours(-1)).ToList();
                AverageSearchTime = recentSearches.Any() 
                    ? recentSearches.Average(s => s.Duration.TotalMilliseconds) 
                    : 0;

                // Calcular tasa de éxito
                var successCount = _searchHistory.Count(s => s.Success);
                SuccessRate = TotalSearches > 0 ? (successCount * 100.0 / TotalSearches) : 0;
            }
        }

        public void RecordSpeed(double bytesPerSecond)
        {
            lock (_lock)
            {
                _speedHistory.Add(new SpeedSample
                {
                    Timestamp = DateTime.Now,
                    BytesPerSecond = bytesPerSecond
                });

                // Mantener solo últimas 2 horas
                var cutoff = DateTime.Now.AddHours(-2);
                _speedHistory.RemoveAll(s => s.Timestamp < cutoff);

                // Calcular promedio
                AverageDownloadSpeed = _speedHistory.Any() 
                    ? _speedHistory.Average(s => s.BytesPerSecond) 
                    : 0;
            }
        }

        public void RecordBytesDownloaded(long bytes)
        {
            TotalBytesDownloaded += bytes;
        }

        public List<double> GetRecentSpeeds(int minutes)
        {
            lock (_lock)
            {
                var cutoff = DateTime.Now.AddMinutes(-minutes);
                return _speedHistory
                    .Where(s => s.Timestamp > cutoff)
                    .Select(s => s.BytesPerSecond)
                    .ToList();
            }
        }

        public List<double> GetHourlySuccessRates(int hours)
        {
            lock (_lock)
            {
                var rates = new List<double>();
                var now = DateTime.Now;

                for (int i = hours - 1; i >= 0; i--)
                {
                    var hourStart = now.AddHours(-i - 1);
                    var hourEnd = now.AddHours(-i);

                    var hourSearches = _searchHistory
                        .Where(s => s.Timestamp >= hourStart && s.Timestamp < hourEnd)
                        .ToList();

                    if (hourSearches.Any())
                    {
                        var successCount = hourSearches.Count(s => s.Success);
                        rates.Add(successCount * 100.0 / hourSearches.Count);
                    }
                    else
                    {
                        rates.Add(0);
                    }
                }

                return rates;
            }
        }

        private class SpeedSample
        {
            public DateTime Timestamp { get; set; }
            public double BytesPerSecond { get; set; }
        }

        private class SearchMetric
        {
            public DateTime Timestamp { get; set; }
            public TimeSpan Duration { get; set; }
            public int ResultsCount { get; set; }
            public bool Success { get; set; }
        }
    }
}
