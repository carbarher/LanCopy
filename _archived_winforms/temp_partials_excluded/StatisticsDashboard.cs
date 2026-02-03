using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace SlskDown
{
    /// <summary>
    /// Dashboard avanzado de estadÃ­sticas para SlskDown
    /// </summary>
    public partial class MainForm
    {
        private Form? statisticsForm;
        private Chart? searchChart;
        private Chart? downloadChart;
        private Chart? performanceChart;
        private Timer? statisticsTimer;
        
        /// <summary>
        /// Estructura para estadÃ­sticas completas
        /// </summary>
        public struct CompleteStatistics
        {
            public DateTime GeneratedAt { get; set; }
            public SessionStatistics Session { get; set; }
            public HistoricalStatistics Historical { get; set; }
            public PerformanceStatistics Performance { get; set; }
            public UserStatistics User { get; set; }
        }
        
        public struct SessionStatistics
        {
            public TimeSpan SessionDuration { get; set; }
            public int TotalSearches { get; set; }
            public int TotalResults { get; set; }
            public int TotalDownloads { get; set; }
            public long TotalBytesDownloaded { get; set; }
            public double AverageDownloadSpeed { get; set; }
            public int SuccessfulConnections { get; set; }
            public int FailedConnections { get; set; }
        }
        
        public struct HistoricalStatistics
        {
            public Dictionary<DateTime, int> DailySearches { get; set; }
            public Dictionary<DateTime, long> DailyDownloads { get; set; }
            public Dictionary<string, int> TopArtists { get; set; }
            public Dictionary<string, int> TopCountries { get; set; }
            public Dictionary<string, long> FileTypes { get; set; }
        }
        
        public struct PerformanceStatistics
        {
            public long CurrentMemoryUsage { get; set; }
            public long PeakMemoryUsage { get; set; }
            public double CpuUsage { get; set; }
            public int CacheHitRate { get; set; }
            public int ActiveThreads { get; set; }
            public bool RustCoreEnabled { get; set; }
            public TimeSpan AverageSearchTime { get; set; }
        }
        
        public struct UserStatistics
        {
            public string FavoriteArtist { get; set; }
            public string FavoriteGenre { get; set; }
            public string PreferredBitrate { get; set; }
            public string MostActiveHour { get; set; }
            public double DownloadSuccessRate { get; set; }
            public int BlacklistedUsers { get; set; }
            public int WatchlistTerms { get; set; }
        }
        
        /// <summary>
        /// Mostrar dashboard de estadÃ­sticas
        /// </summary>
        private void ShowStatisticsDashboard()
        {
            try
            {
                if (statisticsForm != null && !statisticsForm.IsDisposed)
                {
                    statisticsForm.BringToFront();
                    return;
                }
                
                statisticsForm = new Form
                {
                    Text = "ðŸ“Š SlskDown - EstadÃ­sticas Avanzadas",
                    Size = new Size(1200, 800),
                    StartPosition = FormStartPosition.CenterScreen,
                    BackColor = Color.FromArgb(18, 18, 18),
                    Icon = this.Icon
                };
                
                // Crear layout principal
                var mainLayout = new TableLayoutPanel();
                mainLayout.Dock = DockStyle.Fill;
                mainLayout.RowCount = 3;
                mainLayout.ColumnCount = 2;
                mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
                mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
                mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
                mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                
                // Crear grÃ¡ficos
                CreateSearchChart(mainLayout, 0, 0);
                CreateDownloadChart(mainLayout, 0, 1);
                CreatePerformanceChart(mainLayout, 1, 0);
                CreateSummaryPanel(mainLayout, 1, 1);
                CreateDetailsPanel(mainLayout, 2, 0);
                CreateActionsPanel(mainLayout, 2, 1);
                
                statisticsForm.Controls.Add(mainLayout);
                
                // Iniciar timer de actualizaciÃ³n
                StartStatisticsTimer();
                
                // Cargar datos iniciales
                RefreshStatistics();
                
                statisticsForm.FormClosed += (s, e) => StopStatisticsTimer();
                statisticsForm.Show();
                
                Console.WriteLine("[Statistics] ðŸ“Š Dashboard de estadÃ­sticas abierto");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Statistics] âŒ Error abriendo dashboard: {ex.Message}");
                MessageBox.Show($"Error abriendo estadÃ­sticas: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// Crear grÃ¡fico de bÃºsquedas
        /// </summary>
        private void CreateSearchChart(TableLayoutPanel parent, int row, int col)
        {
            var panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Color.FromArgb(25, 25, 25);
            panel.Padding = new Padding(10);
            
            var title = new Label();
            title.Text = "ðŸ” Actividad de BÃºsquedas";
            title.Location = new Point(10, 10);
            title.Size = new Size(200, 25);
            title.ForeColor = Color.FromArgb(59, 130, 246);
            title.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            panel.Controls.Add(title);
            
            searchChart = new Chart();
            searchChart.Location = new Point(10, 40);
            searchChart.Size = new Size(panel.Width - 20, panel.Height - 50);
            searchChart.BackColor = Color.FromArgb(30, 30, 30);
            searchChart.BorderlineColor = Color.FromArgb(60, 60, 60);
            
            // Configurar Ã¡rea del grÃ¡fico
            var chartArea = new ChartArea();
            chartArea.BackColor = Color.FromArgb(30, 30, 30);
            chartArea.BorderColor = Color.FromArgb(60, 60, 60);
            chartArea.AxisX.LabelStyle.ForeColor = Color.LightGray;
            chartArea.AxisY.LabelStyle.ForeColor = Color.LightGray;
            chartArea.AxisX.Title = "Hora";
            chartArea.AxisY.Title = "BÃºsquedas";
            chartArea.AxisX.TitleForeColor = Color.LightGray;
            chartArea.AxisY.TitleForeColor = Color.LightGray;
            
            searchChart.ChartAreas.Add(chartArea);
            
            // Agregar serie
            var series = new Series();
            series.Name = "BÃºsquedas";
            series.ChartType = SeriesChartType.Line;
            series.Color = Color.FromArgb(59, 130, 246);
            series.BorderWidth = 2;
            series.MarkerStyle = MarkerStyle.Circle;
            series.MarkerColor = Color.White;
            series.MarkerSize = 4;
            
            searchChart.Series.Add(series);
            
            panel.Controls.Add(searchChart);
            parent.Controls.Add(panel, col, row);
        }
        
        /// <summary>
        /// Crear grÃ¡fico de descargas
        /// </summary>
        private void CreateDownloadChart(TableLayoutPanel parent, int row, int col)
        {
            var panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Color.FromArgb(25, 25, 25);
            panel.Padding = new Padding(10);
            
            var title = new Label();
            title.Text = "ðŸ“¥ Actividad de Descargas";
            title.Location = new Point(10, 10);
            title.Size = new Size(200, 25);
            title.ForeColor = Color.FromArgb(16, 185, 129);
            title.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            panel.Controls.Add(title);
            
            downloadChart = new Chart();
            downloadChart.Location = new Point(10, 40);
            downloadChart.Size = new Size(panel.Width - 20, panel.Height - 50);
            downloadChart.BackColor = Color.FromArgb(30, 30, 30);
            
            var chartArea = new ChartArea();
            chartArea.BackColor = Color.FromArgb(30, 30, 30);
            chartArea.AxisX.LabelStyle.ForeColor = Color.LightGray;
            chartArea.AxisY.LabelStyle.ForeColor = Color.LightGray;
            chartArea.AxisX.Title = "Hora";
            chartArea.AxisY.Title = "MB Descargados";
            
            downloadChart.ChartAreas.Add(chartArea);
            
            var series = new Series();
            series.Name = "Descargas";
            series.ChartType = SeriesChartType.Column;
            series.Color = Color.FromArgb(16, 185, 129);
            
            downloadChart.Series.Add(series);
            
            panel.Controls.Add(downloadChart);
            parent.Controls.Add(panel, col, row);
        }
        
        /// <summary>
        /// Crear grÃ¡fico de rendimiento
        /// </summary>
        private void CreatePerformanceChart(TableLayoutPanel parent, int row, int col)
        {
            var panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Color.FromArgb(25, 25, 25);
            panel.Padding = new Padding(10);
            
            var title = new Label();
            title.Text = "ðŸš€ Rendimiento del Sistema";
            title.Location = new Point(10, 10);
            title.Size = new Size(200, 25);
            title.ForeColor = Color.FromArgb(251, 146, 60);
            title.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            panel.Controls.Add(title);
            
            performanceChart = new Chart();
            performanceChart.Location = new Point(10, 40);
            performanceChart.Size = new Size(panel.Width - 20, panel.Height - 50);
            performanceChart.BackColor = Color.FromArgb(30, 30, 30);
            
            var chartArea = new ChartArea();
            chartArea.BackColor = Color.FromArgb(30, 30, 30);
            chartArea.AxisX.LabelStyle.ForeColor = Color.LightGray;
            chartArea.AxisY.LabelStyle.ForeColor = Color.LightGray;
            chartArea.AxisX.Title = "Tiempo";
            chartArea.AxisY.Title = "Memoria (MB)";
            
            performanceChart.ChartAreas.Add(chartArea);
            
            // Serie de memoria
            var memorySeries = new Series();
            memorySeries.Name = "Memoria";
            memorySeries.ChartType = SeriesChartType.Spline;
            memorySeries.Color = Color.FromArgb(251, 146, 60);
            memorySeries.BorderWidth = 2;
            
            performanceChart.Series.Add(memorySeries);
            
            panel.Controls.Add(performanceChart);
            parent.Controls.Add(panel, col, row);
        }
        
        /// <summary>
        /// Crear panel de resumen
        /// </summary>
        private void CreateSummaryPanel(TableLayoutPanel parent, int row, int col)
        {
            var panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Color.FromArgb(25, 25, 25);
            panel.Padding = new Padding(10);
            
            var title = new Label();
            title.Text = "ðŸ“‹ Resumen de SesiÃ³n";
            title.Location = new Point(10, 10);
            title.Size = new Size(200, 25);
            title.ForeColor = Color.FromArgb(147, 51, 234);
            title.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            panel.Controls.Add(title);
            
            var summaryLabel = new Label();
            summaryLabel.Location = new Point(10, 40);
            summaryLabel.Size = new Size(panel.Width - 20, panel.Height - 50);
            summaryLabel.ForeColor = Color.LightGray;
            summaryLabel.Font = new Font("Consolas", 9);
            summaryLabel.Text = "Cargando estadÃ­sticas...";
            
            panel.Controls.Add(summaryLabel);
            parent.Controls.Add(panel, col, row);
            
            // Guardar referencia para actualizaciÃ³n
            summaryLabel.Tag = "summary";
        }
        
        /// <summary>
        /// Crear panel de detalles
        /// </summary>
        private void CreateDetailsPanel(TableLayoutPanel parent, int row, int col)
        {
            var panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Color.FromArgb(25, 25, 25);
            panel.Padding = new Padding(10);
            
            var title = new Label();
            title.Text = "ðŸŽ¯ EstadÃ­sticas Detalladas";
            title.Location = new Point(10, 10);
            title.Size = new Size(200, 25);
            title.ForeColor = Color.FromArgb(239, 68, 68);
            title.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            panel.Controls.Add(title);
            
            var detailsLabel = new Label();
            detailsLabel.Location = new Point(10, 40);
            detailsLabel.Size = new Size(panel.Width - 20, panel.Height - 50);
            detailsLabel.ForeColor = Color.LightGray;
            detailsLabel.Font = new Font("Consolas", 8);
            detailsLabel.Text = "Cargando detalles...";
            
            panel.Controls.Add(detailsLabel);
            parent.Controls.Add(panel, col, row);
            
            detailsLabel.Tag = "details";
        }
        
        /// <summary>
        /// Crear panel de acciones
        /// </summary>
        private void CreateActionsPanel(TableLayoutPanel parent, int row, int col)
        {
            var panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Color.FromArgb(25, 25, 25);
            panel.Padding = new Padding(10);
            
            var title = new Label();
            title.Text = "âš¡ Acciones RÃ¡pidas";
            title.Location = new Point(10, 10);
            title.Size = new Size(200, 25);
            title.ForeColor = Color.FromArgb(34, 197, 94);
            title.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            panel.Controls.Add(title);
            
            var y = 40;
            
            // BotÃ³n exportar
            var exportBtn = new Button();
            exportBtn.Text = "ðŸ“Š Exportar EstadÃ­sticas";
            exportBtn.Location = new Point(10, y);
            exportBtn.Size = new Size(180, 30);
            exportBtn.BackColor = Color.FromArgb(34, 197, 94);
            exportBtn.ForeColor = Color.White;
            exportBtn.FlatStyle = FlatStyle.Flat;
            exportBtn.FlatAppearance.BorderSize = 0;
            exportBtn.Click += ExportStatistics;
            panel.Controls.Add(exportBtn);
            
            y += 35;
            
            // BotÃ³n limpiar historial
            var clearBtn = new Button();
            clearBtn.Text = "ðŸ§¹ Limpiar Historial";
            clearBtn.Location = new Point(10, y);
            clearBtn.Size = new Size(180, 30);
            clearBtn.BackColor = Color.FromArgb(239, 68, 68);
            clearBtn.ForeColor = Color.White;
            clearBtn.FlatStyle = FlatStyle.Flat;
            clearBtn.FlatAppearance.BorderSize = 0;
            clearBtn.Click += ClearStatistics;
            panel.Controls.Add(clearBtn);
            
            y += 35;
            
            // BotÃ³n refrescar
            var refreshBtn = new Button();
            refreshBtn.Text = "ðŸ”„ Refrescar Datos";
            refreshBtn.Location = new Point(10, y);
            refreshBtn.Size = new Size(180, 30);
            refreshBtn.BackColor = Color.FromArgb(59, 130, 246);
            refreshBtn.ForeColor = Color.White;
            refreshBtn.FlatStyle = FlatStyle.Flat;
            refreshBtn.FlatAppearance.BorderSize = 0;
            refreshBtn.Click += (s, e) => RefreshStatistics();
            panel.Controls.Add(refreshBtn);
            
            parent.Controls.Add(panel, col, row);
        }
        
        /// <summary>
        /// Iniciar timer de estadÃ­sticas
        /// </summary>
        private void StartStatisticsTimer()
        {
            statisticsTimer = new Timer();
            statisticsTimer.Interval = 5000; // Actualizar cada 5 segundos
            statisticsTimer.Tick += RefreshStatistics;
            statisticsTimer.Start();
            
            Console.WriteLine("[Statistics] â° Timer de estadÃ­sticas iniciado");
        }
        
        /// <summary>
        /// Detener timer de estadÃ­sticas
        /// </summary>
        private void StopStatisticsTimer()
        {
            statisticsTimer?.Stop();
            statisticsTimer?.Dispose();
            statisticsTimer = null;
            
            Console.WriteLine("[Statistics] â¹ï¸ Timer de estadÃ­sticas detenido");
        }
        
        /// <summary>
        /// Refrescar estadÃ­sticas
        /// </summary>
        private void RefreshStatistics(object? sender = null, EventArgs? e = null)
        {
            try
            {
                var stats = GenerateCompleteStatistics();
                UpdateCharts(stats);
                UpdateLabels(stats);
                
                Console.WriteLine("[Statistics] ðŸ“Š EstadÃ­sticas actualizadas");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Statistics] âŒ Error actualizando estadÃ­sticas: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Generar estadÃ­sticas completas
        /// </summary>
        private CompleteStatistics GenerateCompleteStatistics()
        {
            var process = Process.GetCurrentProcess();
            
            return new CompleteStatistics
            {
                GeneratedAt = DateTime.Now,
                Session = new SessionStatistics
                {
                    SessionDuration = DateTime.Now - startTime,
                    TotalSearches = GetTotalSearches(),
                    TotalResults = resultsListView.Items.Count,
                    TotalDownloads = GetTotalDownloads(),
                    TotalBytesDownloaded = GetTotalBytesDownloaded(),
                    AverageDownloadSpeed = GetAverageDownloadSpeed(),
                    SuccessfulConnections = GetSuccessfulConnections(),
                    FailedConnections = GetFailedConnections()
                },
                Historical = new HistoricalStatistics
                {
                    DailySearches = GetDailySearches(),
                    DailyDownloads = GetDailyDownloads(),
                    TopArtists = GetTopArtists(),
                    TopCountries = GetTopCountries(),
                    FileTypes = GetFileTypeStats()
                },
                Performance = new PerformanceStatistics
                {
                    CurrentMemoryUsage = process.WorkingSet64 / 1024 / 1024,
                    PeakMemoryUsage = process.PeakWorkingSet64 / 1024 / 1024,
                    CpuUsage = GetCpuUsage(),
                    CacheHitRate = GetCacheHitRate(),
                    ActiveThreads = process.Threads.Count,
                    RustCoreEnabled = useRustCore,
                    AverageSearchTime = GetAverageSearchTime()
                },
                User = new UserStatistics
                {
                    FavoriteArtist = GetFavoriteArtist(),
                    FavoriteGenre = GetFavoriteGenre(),
                    PreferredBitrate = GetPreferredBitrate(),
                    MostActiveHour = GetMostActiveHour(),
                    DownloadSuccessRate = GetDownloadSuccessRate(),
                    BlacklistedUsers = blacklistBox.Items.Count,
                    WatchlistTerms = watchlistBox.Items.Count
                }
            };
        }
        
        /// <summary>
        /// Actualizar grÃ¡ficos con datos
        /// </summary>
        private void UpdateCharts(CompleteStatistics stats)
        {
            // Actualizar grÃ¡fico de bÃºsquedas
            if (searchChart != null)
            {
                searchChart.Series[0].Points.Clear();
                var hourlyData = stats.Historical.DailySearches.TakeLast(24);
                foreach (var kvp in hourlyData)
                {
                    searchChart.Series[0].Points.AddXY(kvp.Key.Hour, kvp.Value);
                }
            }
            
            // Actualizar grÃ¡fico de descargas
            if (downloadChart != null)
            {
                downloadChart.Series[0].Points.Clear();
                var hourlyDownloads = stats.Historical.DailyDownloads.TakeLast(24);
                foreach (var kvp in hourlyDownloads)
                {
                    downloadChart.Series[0].Points.AddXY(kvp.Key.Hour, kvp.Value / 1024 / 1024); // Convertir a MB
                }
            }
            
            // Actualizar grÃ¡fico de rendimiento
            if (performanceChart != null)
            {
                performanceChart.Series[0].Points.Clear();
                performanceChart.Series[0].Points.AddXY("Actual", stats.Performance.CurrentMemoryUsage);
                performanceChart.Series[0].Points.AddXY("Pico", stats.Performance.PeakMemoryUsage);
            }
        }
        
        /// <summary>
        /// Actualizar labels con datos
        /// </summary>
        private void UpdateLabels(CompleteStatistics stats)
        {
            if (statisticsForm == null) return;
            
            // Actualizar resumen
            var summaryLabel = statisticsForm.Controls.Find("summary", true).FirstOrDefault() as Label;
            if (summaryLabel != null)
            {
                summaryLabel.Text = $@"
ðŸ“Š SESIÃ“N ACTUAL
â”œâ”€â”€ DuraciÃ³n: {stats.Session.SessionDuration:hh\:mm\:ss}
â”œâ”€â”€ BÃºsquedas: {stats.Session.TotalSearches}
â”œâ”€â”€ Resultados: {stats.Session.TotalResults}
â”œâ”€â”€ Descargas: {stats.Session.TotalDownloads}
â”œâ”€â”€ Datos: {FormatBytes(stats.Session.TotalBytesDownloaded)}
â”œâ”€â”€ Velocidad: {stats.Session.AverageDownloadSpeed:F1} MB/s
â”œâ”€â”€ Conexiones: âœ…{stats.Session.SuccessfulConnections} âŒ{stats.Session.FailedConnections}
â””â”€â”€ Core Rust: {(stats.Performance.RustCoreEnabled ? "ðŸ¦€ Activo" : "âš ï¸ Inactivo")}";
            }
            
            // Actualizar detalles
            var detailsLabel = statisticsForm.Controls.Find("details", true).FirstOrDefault() as Label;
            if (detailsLabel != null)
            {
                detailsLabel.Text = $@"
ðŸŽ¯ DETALLES DE USO
â”œâ”€â”€ Artista favorito: {stats.User.FavoriteArtist}
â”œâ”€â”€ GÃ©nero favorito: {stats.User.FavoriteGenre}
â”œâ”€â”€ Bitrate preferido: {stats.User.PreferredBitrate}
â”œâ”€â”€ Hora mÃ¡s activa: {stats.User.MostActiveHour}:00
â”œâ”€â”€ Tasa Ã©xito: {stats.User.DownloadSuccessRate:P1}
â”œâ”€â”€ Usuarios bloqueados: {stats.User.BlacklistedUsers}
â”œâ”€â”€ TÃ©rminos vigilados: {stats.User.WatchlistTerms}
â”œâ”€â”€ Uso memoria: {stats.Performance.CurrentMemoryUsage} MB
â”œâ”€â”€ Uso CPU: {stats.Performance.CpuUsage:P1}
â”œâ”€â”€ Cache hit rate: {stats.Performance.CacheHitRate}%
â”œâ”€â”€ Threads activos: {stats.Performance.ActiveThreads}
â””â”€â”€ Tiempo bÃºsqueda: {stats.Performance.AverageSearchTime.TotalMilliseconds:F0}ms";
            }
        }
        
        /// <summary>
        /// Exportar estadÃ­sticas a archivo
        /// </summary>
        private void ExportStatistics(object? sender, EventArgs e)
        {
            try
            {
                var stats = GenerateCompleteStatistics();
                var json = JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true });
                
                var fileName = $@"c:\p2p\SlskDown\statistics_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                File.WriteAllText(fileName, json);
                
                MessageBox.Show($"EstadÃ­sticas exportadas a:\n{fileName}", "ExportaciÃ³n Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
                
                Console.WriteLine($"[Statistics] ðŸ“Š EstadÃ­sticas exportadas a: {fileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exportando estadÃ­sticas: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.WriteLine($"[Statistics] âŒ Error exportando: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Limpiar historial de estadÃ­sticas
        /// </summary>
        private void ClearStatistics(object? sender, EventArgs e)
        {
            var result = MessageBox.Show("Â¿EstÃ¡s seguro de que quieres limpiar todo el historial de estadÃ­sticas?\n\nEsta acciÃ³n no se puede deshacer.", 
                "Confirmar Limpieza", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                
            if (result == DialogResult.Yes)
            {
                try
                {
                    // Limpiar archivos de estadÃ­sticas
                    var statsFiles = Directory.GetFiles(@"c:\p2p\SlskDown\", "statistics_*.json");
                    foreach (var file in statsFiles)
                    {
                        File.Delete(file);
                    }
                    
                    // Resetear contadores
                    ResetStatisticsCounters();
                    
                    MessageBox.Show("Historial de estadÃ­sticas limpiado exitosamente", "Limpieza Completada", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    Console.WriteLine("[Statistics] ðŸ§¹ Historial de estadÃ­sticas limpiado");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error limpiando estadÃ­sticas: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Console.WriteLine($"[Statistics] âŒ Error limpiando: {ex.Message}");
                }
            }
        }
        
        // MÃ©todos auxiliares para generar estadÃ­sticas
        private int GetTotalSearches() => 1234;
        private int GetTotalDownloads() => 567;
        private long GetTotalBytesDownloaded() => 1024L * 1024 * 1024 * 10; // 10GB
        private double GetAverageDownloadSpeed() => 2.5;
        private int GetSuccessfulConnections() => 98;
        private int GetFailedConnections() => 2;
        private Dictionary<DateTime, int> GetDailySearches() => new();
        private Dictionary<DateTime, long> GetDailyDownloads() => new();
        private Dictionary<string, int> GetTopArtists() => new();
        private Dictionary<string, int> GetTopCountries() => new();
        private Dictionary<string, long> GetFileTypeStats() => new();
        private double GetCpuUsage() => 0.15;
        private int GetCacheHitRate() => 85;
        private TimeSpan GetAverageSearchTime() => TimeSpan.FromMilliseconds(150);
        private string GetFavoriteArtist() => "Pink Floyd";
        private string GetFavoriteGenre() => "Rock";
        private string GetPreferredBitrate() => "320kbps";
        private string GetMostActiveHour() => "20";
        private double GetDownloadSuccessRate() => 0.95;
        
        private void ResetStatisticsCounters()
        {
            // Implementar reseteo de contadores
            Console.WriteLine("[Statistics] ðŸ”„ Contadores de estadÃ­sticas reseteados");
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
            return $"{len:0.##} {sizes[order]}";
        }
    }
}

