using System;
using System.Linq;
using SlskDown.Models;
using SlskDown.UI;

namespace SlskDown
{
    /// <summary>
    /// Partial class de MainForm para métricas y estadísticas
    /// </summary>
    public partial class MainForm
    {
        // ============================================================================
        // MÉTRICAS - Contadores de archivos subidos y bajados
        // ============================================================================
        
        private PerformanceMetrics performanceMetrics;
        private int totalFilesDownloaded = 0;
        private int totalFilesUploaded = 0;
        private long totalBytesUploaded = 0;

        private void InitializeMetrics()
        {
            try
            {
                performanceMetrics = new PerformanceMetrics();
                
                // Cargar métricas guardadas
                LoadMetricsFromConfig();
                
                Log("✅ Sistema de métricas inicializado");
            }
            catch (Exception ex)
            {
                Log($"❌ Error inicializando métricas: {ex.Message}");
            }
        }

        private void LoadMetricsFromConfig()
        {
            try
            {
                if (configManager != null)
                {
                    totalFilesDownloaded = configManager.GetValue("totalFilesDownloaded", 0);
                    totalFilesUploaded = configManager.GetValue("totalFilesUploaded", 0);
                    totalBytesUploaded = configManager.GetValue("totalBytesUploaded", 0L);
                    
                    // Actualizar métricas
                    for (int i = 0; i < totalFilesDownloaded; i++)
                    {
                        performanceMetrics?.RecordFileDownloaded();
                    }
                    
                    Log($"📊 Métricas cargadas: {totalFilesDownloaded} bajados, {totalFilesUploaded} subidos");
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error cargando métricas: {ex.Message}");
            }
        }

        private void SaveMetricsToConfig()
        {
            try
            {
                if (configManager != null)
                {
                    configManager.SetValue("totalFilesDownloaded", totalFilesDownloaded);
                    configManager.SetValue("totalFilesUploaded", totalFilesUploaded);
                    configManager.SetValue("totalBytesUploaded", totalBytesUploaded);
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error guardando métricas: {ex.Message}");
            }
        }

        /// <summary>
        /// Registra una descarga completada
        /// </summary>
        public void RecordFileDownloaded(long bytes)
        {
            try
            {
                totalFilesDownloaded++;
                performanceMetrics?.RecordFileDownloaded();
                performanceMetrics?.RecordBytesDownloaded(bytes);
                
                // Guardar cada 10 archivos
                if (totalFilesDownloaded % 10 == 0)
                {
                    SaveMetricsToConfig();
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error registrando descarga: {ex.Message}");
            }
        }

        /// <summary>
        /// Registra una subida completada
        /// </summary>
        public void RecordFileUploaded(long bytes)
        {
            try
            {
                totalFilesUploaded++;
                totalBytesUploaded += bytes;
                performanceMetrics?.RecordFileUploaded(bytes);
                
                Log($"📤 Archivo subido ({totalFilesUploaded} total) - {FormatFileSize(bytes)}");
                
                // Guardar cada 10 archivos
                if (totalFilesUploaded % 10 == 0)
                {
                    SaveMetricsToConfig();
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error registrando subida: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene las métricas actuales
        /// </summary>
        public (int downloaded, int uploaded, long bytesUploaded) GetFileMetrics()
        {
            return (totalFilesDownloaded, totalFilesUploaded, totalBytesUploaded);
        }

        /// <summary>
        /// Muestra el dashboard de métricas
        /// </summary>
        public void ShowMetricsDashboard()
        {
            try
            {
                if (performanceMetrics != null)
                {
                    var dashboard = new PerformanceDashboard(performanceMetrics);
                    dashboard.Show();
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Error mostrando dashboard: {ex.Message}");
            }
        }

        /// <summary>
        /// Actualiza las métricas en la UI principal
        /// </summary>
        private void UpdateMetricsDisplay()
        {
            try
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(UpdateMetricsDisplay));
                    return;
                }

                // Actualizar labels si existen
                if (lblTotalDescargas != null)
                {
                    lblTotalDescargas.Text = $"📥 Bajados: {totalFilesDownloaded}";
                }

                if (lblTotalSubidas != null)
                {
                    lblTotalSubidas.Text = $"📤 Subidos: {totalFilesUploaded}";
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error actualizando display de métricas: {ex.Message}");
            }
        }

        // Labels para métricas en UI
        private System.Windows.Forms.Label lblTotalDescargas;
        private System.Windows.Forms.Label lblTotalSubidas;

        /// <summary>
        /// Crea panel de métricas para la UI
        /// </summary>
        private System.Windows.Forms.Panel CreateMetricsPanel()
        {
            var panel = new System.Windows.Forms.Panel
            {
                Dock = System.Windows.Forms.DockStyle.Top,
                Height = 40,
                BackColor = System.Drawing.Color.FromArgb(35, 35, 35),
                Padding = new System.Windows.Forms.Padding(10, 5, 10, 5)
            };

            lblTotalDescargas = new System.Windows.Forms.Label
            {
                Text = $"📥 Bajados: {totalFilesDownloaded}",
                ForeColor = System.Drawing.Color.LightGreen,
                AutoSize = true,
                Location = new System.Drawing.Point(10, 12),
                Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold)
            };

            lblTotalSubidas = new System.Windows.Forms.Label
            {
                Text = $"📤 Subidos: {totalFilesUploaded}",
                ForeColor = System.Drawing.Color.LightBlue,
                AutoSize = true,
                Location = new System.Drawing.Point(150, 12),
                Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold)
            };

            var btnShowDashboard = new System.Windows.Forms.Button
            {
                Text = "📊 Ver Métricas",
                Location = new System.Drawing.Point(290, 8),
                Width = 120,
                Height = 25,
                BackColor = System.Drawing.Color.FromArgb(60, 60, 60),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = System.Windows.Forms.FlatStyle.Flat
            };
            btnShowDashboard.FlatAppearance.BorderSize = 0;
            btnShowDashboard.Click += (s, e) => ShowMetricsDashboard();

            panel.Controls.AddRange(new System.Windows.Forms.Control[] 
            { 
                lblTotalDescargas, 
                lblTotalSubidas, 
                btnShowDashboard 
            });

            return panel;
        }
    }
}
