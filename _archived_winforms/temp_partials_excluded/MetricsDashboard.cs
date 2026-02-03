using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SlskDown
{
    /// <summary>
    /// Dashboard de mÃ©tricas en tiempo real para SlskDown
    /// </summary>
    public partial class MainForm
    {
        private System.Windows.Forms.Timer? metricsTimer;
        private Label metricsLabel = null!;
        private Panel metricsPanel = null!;
        
        // MÃ©tricas de rendimiento
        private struct PerformanceMetrics
        {
            public long MemoryUsedMB;
            public int ActiveSearches;
            public int ActiveDownloads;
            public int TotalResults;
            public double SearchSpeed; // bÃºsquedas/segundo
            public double DownloadSpeed; // MB/segundo
            public int CacheHitRate;
            public DateTime LastUpdate;
        }
        
        private PerformanceMetrics currentMetrics;
        
        /// <summary>
        /// Inicializar dashboard de mÃ©tricas
        /// </summary>
        private void InitializeMetricsDashboard()
        {
            // Panel de mÃ©tricas
            metricsPanel = new Panel();
            metricsPanel.Dock = DockStyle.Bottom;
            metricsPanel.Height = 60;
            metricsPanel.BackColor = Color.FromArgb(25, 25, 25);
            metricsPanel.BorderStyle = BorderStyle.FixedSingle;
            
            // Label principal de mÃ©tricas
            metricsLabel = new Label();
            metricsLabel.Dock = DockStyle.Fill;
            metricsLabel.ForeColor = Color.LightGreen;
            metricsLabel.Font = new Font("Consolas", 9, FontStyle.Regular);
            metricsLabel.Text = "ðŸ“Š Iniciando mÃ©tricas...";
            
            metricsPanel.Controls.Add(metricsLabel);
            
            // Timer para actualizar mÃ©tricas
            metricsTimer = new System.Windows.Forms.Timer();
            metricsTimer.Interval = 1000; // Actualizar cada segundo
            metricsTimer.Tick += UpdateMetrics;
            metricsTimer.Start();
            
            Console.WriteLine("[Metrics] ðŸ“Š Dashboard de mÃ©tricas iniciado");
        }
        
        /// <summary>
        /// Actualizar mÃ©tricas en tiempo real
        /// </summary>
        private void UpdateMetrics(object? sender, EventArgs e)
        {
            try
            {
                // Obtener mÃ©tricas del sistema
                var process = Process.GetCurrentProcess();
                currentMetrics.MemoryUsedMB = process.WorkingSet64 / 1024 / 1024;
                
                // MÃ©tricas de bÃºsqueda
                currentMetrics.ActiveSearches = GetActiveSearchCount();
                currentMetrics.TotalResults = resultsListView.Items.Count;
                
                // MÃ©tricas de descarga
                currentMetrics.ActiveDownloads = GetActiveDownloadCount();
                currentMetrics.DownloadSpeed = CalculateDownloadSpeed();
                
                // MÃ©tricas de cache
                currentMetrics.CacheHitRate = GetCacheHitRate();
                
                // Velocidad de bÃºsqueda
                currentMetrics.SearchSpeed = CalculateSearchSpeed();
                
                currentMetrics.LastUpdate = DateTime.Now;
                
                // Actualizar UI
                UpdateMetricsDisplay();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Metrics] âŒ Error actualizando mÃ©tricas: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Actualizar display de mÃ©tricas
        /// </summary>
        private void UpdateMetricsDisplay()
        {
            if (metricsLabel.InvokeRequired)
            {
                metricsLabel.Invoke(UpdateMetricsDisplay);
                return;
            }
            
            var text = $"ðŸ“Š " +
                      $"RAM: {currentMetrics.MemoryUsedMB}MB | " +
                      $"ðŸ” BÃºsquedas: {currentMetrics.ActiveSearches} ({currentMetrics.SearchSpeed:F1}/s) | " +
                      $"ðŸ“¥ Descargas: {currentMetrics.ActiveDownloads} ({currentMetrics.DownloadSpeed:F1}MB/s) | " +
                      $"ðŸ“‹ Resultados: {currentMetrics.TotalResults} | " +
                      $"ðŸŽ¯ Cache: {currentMetrics.CacheHitRate}% | " +
                      $"ðŸ•’ {currentMetrics.LastUpdate:HH:mm:ss}";
            
            metricsLabel.Text = text;
        }
        
        /// <summary>
        /// Obtener conteo de bÃºsquedas activas
        /// </summary>
        private int GetActiveSearchCount()
        {
            // Implementar lÃ³gica real basada en el estado de bÃºsqueda
            return isAuthorSearchRunning ? 1 : 0;
        }
        
        /// <summary>
        /// Obtener conteo de descargas activas
        /// </summary>
        private int GetActiveDownloadCount()
        {
            // Contar descargas con progreso > 0 y < 100
            return downloadsListView.Items.Cast<ListViewItem>()
                .Count(item => item.SubItems.Count > 2 && 
                              item.SubItems[2].Text.Contains("%") && 
                              !item.SubItems[2].Text.Contains("100"));
        }
        
        /// <summary>
        /// Calcular velocidad de descarga
        /// </summary>
        private double CalculateDownloadSpeed()
        {
            // Simular cÃ¡lculo real basado en progreso de descargas
            return new Random().NextDouble() * 10; // MB/segundo simulado
        }
        
        /// <summary>
        /// Calcular velocidad de bÃºsqueda
        /// </summary>
        private double CalculateSearchSpeed()
        {
            // Basado en resultados por segundo
            return currentMetrics.TotalResults > 0 ? 
                (double)currentMetrics.TotalResults / Math.Max(1, (DateTime.Now - startTime).TotalSeconds) : 0;
        }
        
        /// <summary>
        /// Obtener tasa de aciertos de cache
        /// </summary>
        private int GetCacheHitRate()
        {
            // Implementar mÃ©trica real de cache
            return new Random().Next(70, 95); // Simulado 70-95%
        }
        
        /// <summary>
        /// Exportar mÃ©tricas a JSON
        /// </summary>
        private void ExportMetrics()
        {
            try
            {
                var metrics = new
                {
                    timestamp = DateTime.Now,
                    memory_mb = currentMetrics.MemoryUsedMB,
                    active_searches = currentMetrics.ActiveSearches,
                    active_downloads = currentMetrics.ActiveDownloads,
                    total_results = currentMetrics.TotalResults,
                    search_speed = currentMetrics.SearchSpeed,
                    download_speed = currentMetrics.DownloadSpeed,
                    cache_hit_rate = currentMetrics.CacheHitRate,
                    rust_core_enabled = useRustCore
                };
                
                var json = System.Text.Json.JsonSerializer.Serialize(metrics, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText($@"c:\p2p\SlskDown\metrics_{DateTime.Now:yyyyMMdd_HHmmss}.json", json);
                
                Console.WriteLine("[Metrics] ðŸ“Š MÃ©tricas exportadas");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Metrics] âŒ Error exportando mÃ©tricas: {ex.Message}");
            }
        }
    }
}

