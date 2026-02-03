using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using SlskDown.Models;
using SlskDown.Services;
using SlskDown.Core.AI;
using SlskDown.Core.Performance;

namespace SlskDown.UI
{
    /// <summary>
    /// Dashboard de métricas en tiempo real con visualización avanzada
    /// </summary>
    public class DashboardControl : UserControl
    {
        private readonly MainForm _mainForm;
        private readonly AutoLearningEngine _autoLearningEngine;
        private readonly MemoryPoolManager _memoryPool;
        
        // Controles principales
        private TabControl tabDashboard;
        private TabPage tabOverview, tabDownloads, tabSearches, tabPerformance, tabAI;
        
        // Overview
        private Panel panelOverview;
        private Label lblTotalDownloads, lblActiveDownloads, lblQueueSize, lblSuccessRate;
        private Label lblTotalSearches, lblAvgResponseTime, lblNetworkStatus, lblUptime;
        
        // Gráficos
        private Chart chartDownloadSpeed;
        private Chart chartSearchHistory;
        private Chart chartNetworkUsage;
        private Chart chartAIMetrics;
        
        // Métricas de rendimiento
        private Label lblMemoryUsage, lblCPUUsage, lblGPUTemperature, lblCacheHitRate;
        private ProgressBar progressBarMemory, progressBarCPU;
        
        // Métricas de IA
        private Label lblPredictionsAccuracy, lblClassificationsCount, lblLearningDataSize;
        private ProgressBar progressBarAccuracy;
        
        // Timer para actualización
        private Timer updateTimer;
        
        // Datos históricos
        private readonly Queue<MetricSnapshot> _metricHistory = new Queue<MetricSnapshot>();
        private DateTime _startTime = DateTime.UtcNow;

        public DashboardControl(MainForm mainForm)
        {
            _mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
            _autoLearningEngine = new AutoLearningEngine();
            _memoryPool = MemoryPoolManager.Instance;
            
            InitializeComponents();
            SetupCharts();
            StartDataCollection();
        }

        /// <summary>
        /// Inicializa componentes del dashboard
        /// </summary>
        private void InitializeComponents()
        {
            BackColor = Color.FromArgb(45, 45, 48);
            Dock = DockStyle.Fill;

            // TabControl principal
            tabDashboard = new TabControl
            {
                Dock = DockStyle.Fill,
                Appearance = TabAppearance.FlatButtons,
                BackColor = BackColor
            };

            // Crear pestañas
            CreateOverviewTab();
            CreateDownloadsTab();
            CreateSearchesTab();
            CreatePerformanceTab();
            CreateAITab();

            tabDashboard.TabPages.AddRange(new TabPage[]
            {
                tabOverview, tabDownloads, tabSearches, tabPerformance, tabAI
            });

            Controls.Add(tabDashboard);
        }

        /// <summary>
        /// Crea pestaña de overview
        /// </summary>
        private void CreateOverviewTab()
        {
            tabOverview = new TabPage("📊 Resumen")
            {
                BackColor = BackColor
            };

            panelOverview = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };

            // Crear tarjetas de métricas
            var y = 20;
            var cardWidth = 200;
            var cardHeight = 120;
            var spacing = 20;
            var cardsPerRow = 4;

            // Tarjeta 1: Descargas totales
            var card1 = CreateMetricCard("Descargas Totales", "0", Color.FromArgb(13, 110, 253), 20, y, cardWidth, cardHeight);
            lblTotalDownloads = (Label)card1.Controls[1];
            panelOverview.Controls.Add(card1);

            // Tarjeta 2: Descargas activas
            var card2 = CreateMetricCard("Activas", "0", Color.FromArgb(25, 135, 84), 20 + (cardWidth + spacing), y, cardWidth, cardHeight);
            lblActiveDownloads = (Label)card2.Controls[1];
            panelOverview.Controls.Add(card2);

            // Tarjeta 3: Tamaño cola
            var card3 = CreateMetricCard("Cola", "0", Color.FromArgb(255, 193, 7), 20 + (cardWidth + spacing) * 2, y, cardWidth, cardHeight);
            lblQueueSize = (Label)card3.Controls[1];
            panelOverview.Controls.Add(card3);

            // Tarjeta 4: Tasa éxito
            var card4 = CreateMetricCard("Éxito", "100%", Color.FromArgb(220, 53, 69), 20 + (cardWidth + spacing) * 3, y, cardWidth, cardHeight);
            lblSuccessRate = (Label)card4.Controls[1];
            panelOverview.Controls.Add(card4);

            y += cardHeight + spacing;

            // Tarjeta 5: Búsquedas totales
            var card5 = CreateMetricCard("Búsquedas", "0", Color.FromArgb(108, 117, 125), 20, y, cardWidth, cardHeight);
            lblTotalSearches = (Label)card5.Controls[1];
            panelOverview.Controls.Add(card5);

            // Tarjeta 6: Tiempo respuesta
            var card6 = CreateMetricCard("Respuesta", "0ms", Color.FromArgb(13, 202, 240), 20 + (cardWidth + spacing), y, cardWidth, cardHeight);
            lblAvgResponseTime = (Label)card6.Controls[1];
            panelOverview.Controls.Add(card6);

            // Tarjeta 7: Estado red
            var card7 = CreateMetricCard("Red", "Conectado", Color.FromArgb(25, 135, 84), 20 + (cardWidth + spacing) * 2, y, cardWidth, cardHeight);
            lblNetworkStatus = (Label)card7.Controls[1];
            panelOverview.Controls.Add(card7);

            // Tarjeta 8: Tiempo activo
            var card8 = CreateMetricCard("Activo", "00:00:00", Color.FromArgb(111, 66, 193), 20 + (cardWidth + spacing) * 3, y, cardWidth, cardHeight);
            lblUptime = (Label)card8.Controls[1];
            panelOverview.Controls.Add(card8);

            // Gráfico de velocidad de descarga
            y += cardHeight + spacing;
            var speedChartPanel = CreateChartPanel("Velocidad de Descarga (KB/s)", 20, y, 850, 200);
            chartDownloadSpeed = (Chart)speedChartPanel.Controls[1];
            panelOverview.Controls.Add(speedChartPanel);

            tabOverview.Controls.Add(panelOverview);
        }

        /// <summary>
        /// Crea pestaña de descargas
        /// </summary>
        private void CreateDownloadsTab()
        {
            tabDownloads = new TabPage("📥 Descargas")
            {
                BackColor = BackColor
            };

            var panel = new Panel { Dock = DockStyle.Fill };
            
            // Gráfico de historial de descargas
            var historyPanel = CreateChartPanel("📊 Historial de Descargas", 20, 20, 850, 250);
            chartDownloadSpeed = (Chart)historyPanel.Controls[1];
            panel.Controls.Add(historyPanel);

            // Estadísticas detalladas
            var statsPanel = new Panel
            {
                Location = new Point(20, 290),
                Size = new Size(850, 200),
                BackColor = Color.FromArgb(30, 30, 30)
            };

            var statsLabel = new Label
            {
                Text = "📈 Estadísticas Detalladas",
                Location = new Point(10, 10),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White
            };
            statsPanel.Controls.Add(statsLabel);

            tabDownloads.Controls.Add(panel);
        }

        /// <summary>
        /// Crea pestaña de búsquedas
        /// </summary>
        private void CreateSearchesTab()
        {
            tabSearches = new TabPage("Búsquedas")
            {
                BackColor = BackColor
            };

            var panel = new Panel { Dock = DockStyle.Fill };
            
            // Gráfico de historial de búsquedas
            var searchPanel = CreateChartPanel("Historial de Búsquedas", 20, 20, 850, 250);
            chartSearchHistory = (Chart)searchPanel.Controls[1];
            panel.Controls.Add(searchPanel);

            tabSearches.Controls.Add(panel);
        }

        /// <summary>
        /// Crea pestaña de rendimiento
        /// </summary>
        private void CreatePerformanceTab()
        {
            tabPerformance = new TabPage("Rendimiento")
            {
                BackColor = BackColor
            };

            var panel = new Panel { Dock = DockStyle.Fill };
            
            // Métricas de sistema
            var y = 20;
            
            // Uso de memoria
            panel.Controls.Add(new Label { Text = "Uso de Memoria:", Location = new Point(20, y), ForeColor = Color.White });
            lblMemoryUsage = new Label { Text = "0 MB", Location = new Point(200, y), ForeColor = Color.LightGreen };
            progressBarMemory = new ProgressBar { Location = new Point(20, y + 25), Width = 300, Height = 20 };
            panel.Controls.AddRange(new Control[] { lblMemoryUsage, progressBarMemory });
            y += 60;

            // Uso de CPU
            panel.Controls.Add(new Label { Text = "Uso de CPU:", Location = new Point(20, y), ForeColor = Color.White });
            lblCPUUsage = new Label { Text = "0%", Location = new Point(200, y), ForeColor = Color.LightGreen };
            progressBarCPU = new ProgressBar { Location = new Point(20, y + 25), Width = 300, Height = 20 };
            panel.Controls.AddRange(new Control[] { lblCPUUsage, progressBarCPU });
            y += 60;

            // Cache hit rate
            panel.Controls.Add(new Label { Text = "Cache Hit Rate:", Location = new Point(20, y), ForeColor = Color.White });
            lblCacheHitRate = new Label { Text = "0%", Location = new Point(200, y), ForeColor = Color.LightBlue };
            panel.Controls.AddRange(new Control[] { lblCacheHitRate });
            y += 40;

            // Gráfico de uso de red
            var networkPanel = CreateChartPanel("Uso de Red", 20, y, 850, 200);
            chartNetworkUsage = (Chart)networkPanel.Controls[1];
            panel.Controls.Add(networkPanel);

            tabPerformance.Controls.Add(panel);
        }

        /// <summary>
        /// Crea pestaña de IA
        /// </summary>
        private void CreateAITab()
        {
            tabAI = new TabPage("🧠 Inteligencia Artificial")
            {
                BackColor = BackColor
            };

            var panel = new Panel { Dock = DockStyle.Fill };
            
            var y = 20;
            
            // Precisión de predicciones
            panel.Controls.Add(new Label { Text = "🎯 Precisión Predicciones:", Location = new Point(20, y), ForeColor = Color.White });
            lblPredictionsAccuracy = new Label { Text = "0%", Location = new Point(250, y), ForeColor = Color.LightGreen };
            progressBarAccuracy = new ProgressBar { Location = new Point(20, y + 25), Width = 300, Height = 20 };
            panel.Controls.AddRange(new Control[] { lblPredictionsAccuracy, progressBarAccuracy });
            y += 60;

            // Contador de clasificaciones
            panel.Controls.Add(new Label { Text = "Clasificaciones:", Location = new Point(20, y), ForeColor = Color.White });
            lblClassificationsCount = new Label { Text = "0", Location = new Point(250, y), ForeColor = Color.LightBlue };
            panel.Controls.AddRange(new Control[] { lblClassificationsCount });
            y += 40;

            // Tamaño datos de aprendizaje
            panel.Controls.Add(new Label { Text = "Datos Aprendizaje:", Location = new Point(20, y), ForeColor = Color.White });
            lblLearningDataSize = new Label { Text = "0 MB", Location = new Point(250, y), ForeColor = Color.LightYellow };
            panel.Controls.AddRange(new Control[] { lblLearningDataSize });
            y += 60;

            // Gráfico de métricas de IA
            var aiPanel = CreateChartPanel("Métricas de IA", 20, y, 850, 200);
            chartAIMetrics = (Chart)aiPanel.Controls[1];
            panel.Controls.Add(aiPanel);

            tabAI.Controls.Add(panel);
        }

        /// <summary>
        /// Crea tarjeta de métrica
        /// </summary>
        private Panel CreateMetricCard(string title, string value, Color color, int x, int y, int width, int height)
        {
            var card = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(width, height),
                BackColor = Color.FromArgb(30, 30, 30),
                BorderStyle = BorderStyle.None
            };

            // Título
            var titleLabel = new Label
            {
                Text = title,
                Location = new Point(15, 15),
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.LightGray,
                AutoSize = true
            };

            // Valor
            var valueLabel = new Label
            {
                Text = value,
                Location = new Point(15, 50),
                Font = new Font("Segoe UI", 24f, FontStyle.Bold),
                ForeColor = color,
                AutoSize = true
            };

            // Indicador visual
            var indicator = new Panel
            {
                Location = new Point(width - 10, 10),
                Size = new Size(4, height - 20),
                BackColor = color
            };

            card.Controls.AddRange(new Control[] { titleLabel, valueLabel, indicator });
            return card;
        }

        /// <summary>
        /// Crea panel con gráfico
        /// </summary>
        private Panel CreateChartPanel(string title, int x, int y, int width, int height)
        {
            var panel = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(width, height),
                BackColor = Color.FromArgb(30, 30, 30)
            };

            var titleLabel = new Label
            {
                Text = title,
                Location = new Point(10, 5),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White
            };

            var chart = new Chart
            {
                Location = new Point(10, 30),
                Size = new Size(width - 20, height - 40),
                BackColor = Color.FromArgb(45, 45, 48)
            };

            panel.Controls.AddRange(new Control[] { titleLabel, chart });
            return panel;
        }

        /// <summary>
        /// Configura gráficos
        /// </summary>
        private void SetupCharts()
        {
            // Configurar gráfico de velocidad de descarga
            SetupDownloadSpeedChart();
            
            // Configurar gráfico de historial de búsquedas
            SetupSearchHistoryChart();
            
            // Configurar gráfico de uso de red
            SetupNetworkUsageChart();
            
            // Configurar gráfico de métricas de IA
            SetupAIMetricsChart();
        }

        /// <summary>
        /// Configura gráfico de velocidad de descarga
        /// </summary>
        private void SetupDownloadSpeedChart()
        {
            var chartArea = new ChartArea
            {
                Name = "MainArea",
                BackColor = Color.FromArgb(45, 45, 48),
                AxisX =
                {
                    LabelStyle = { ForeColor = Color.LightGray },
                    MajorGrid = { LineColor = Color.FromArgb(60, 60, 60) },
                    Title = "Tiempo"
                },
                AxisY =
                {
                    LabelStyle = { ForeColor = Color.LightGray },
                    MajorGrid = { LineColor = Color.FromArgb(60, 60, 60) },
                    Title = "Velocidad (KB/s)"
                }
            };

            chartDownloadSpeed.ChartAreas.Add(chartArea);

            var series = new Series
            {
                Name = "Velocidad",
                ChartType = SeriesChartType.Line,
                Color = Color.FromArgb(13, 110, 253),
                BorderWidth = 2,
                IsVisibleInLegend = false
            };

            chartDownloadSpeed.Series.Add(series);
        }

        /// <summary>
        /// Configura gráfico de historial de búsquedas
        /// </summary>
        private void SetupSearchHistoryChart()
        {
            var chartArea = new ChartArea
            {
                Name = "MainArea",
                BackColor = Color.FromArgb(45, 45, 48),
                AxisX =
                {
                    LabelStyle = { ForeColor = Color.LightGray },
                    MajorGrid = { LineColor = Color.FromArgb(60, 60, 60) },
                    Title = "Tiempo"
                },
                AxisY =
                {
                    LabelStyle = { ForeColor = Color.LightGray },
                    MajorGrid = { LineColor = Color.FromArgb(60, 60, 60) },
                    Title = "Resultados"
                }
            };

            chartSearchHistory.ChartAreas.Add(chartArea);

            var series = new Series
            {
                Name = "Resultados",
                ChartType = SeriesChartType.Column,
                Color = Color.FromArgb(25, 135, 84),
                IsVisibleInLegend = false
            };

            chartSearchHistory.Series.Add(series);
        }

        /// <summary>
        /// Configura gráfico de uso de red
        /// </summary>
        private void SetupNetworkUsageChart()
        {
            var chartArea = new ChartArea
            {
                Name = "MainArea",
                BackColor = Color.FromArgb(45, 45, 48),
                AxisX =
                {
                    LabelStyle = { ForeColor = Color.LightGray },
                    MajorGrid = { LineColor = Color.FromArgb(60, 60, 60) },
                    Title = "Tiempo"
                },
                AxisY =
                {
                    LabelStyle = { ForeColor = Color.LightGray },
                    MajorGrid = { LineColor = Color.FromArgb(60, 60, 60) },
                    Title = "Ancho de Banda (KB/s)"
                }
            };

            chartNetworkUsage.ChartAreas.Add(chartArea);

            var seriesUpload = new Series
            {
                Name = "Subida",
                ChartType = SeriesChartType.Line,
                Color = Color.FromArgb(255, 193, 7),
                BorderWidth = 2
            };

            var seriesDownload = new Series
            {
                Name = "Bajada",
                ChartType = SeriesChartType.Line,
                Color = Color.FromArgb(13, 110, 253),
                BorderWidth = 2
            };

            chartNetworkUsage.Series.Add(seriesUpload);
            chartNetworkUsage.Series.Add(seriesDownload);
        }

        /// <summary>
        /// Configura gráfico de métricas de IA
        /// </summary>
        private void SetupAIMetricsChart()
        {
            var chartArea = new ChartArea
            {
                Name = "MainArea",
                BackColor = Color.FromArgb(45, 45, 48),
                AxisX =
                {
                    LabelStyle = { ForeColor = Color.LightGray },
                    MajorGrid = { LineColor = Color.FromArgb(60, 60, 60) },
                    Title = "Tiempo"
                },
                AxisY =
                {
                    LabelStyle = { ForeColor = Color.LightGray },
                    MajorGrid = { LineColor = Color.FromArgb(60, 60, 60) },
                    Title = "Precisión (%)"
                }
            };

            chartAIMetrics.ChartAreas.Add(chartArea);

            var series = new Series
            {
                Name = "Precisión IA",
                ChartType = SeriesChartType.Spline,
                Color = Color.FromArgb(220, 53, 69),
                BorderWidth = 3
            };

            chartAIMetrics.Series.Add(series);
        }

        /// <summary>
        /// Inicia recolección de datos
        /// </summary>
        private void StartDataCollection()
        {
            updateTimer = new Timer
            {
                Interval = 1000 // Actualizar cada segundo
            };
            updateTimer.Tick += UpdateMetrics;
            updateTimer.Start();
        }

        /// <summary>
        /// Actualiza métricas
        /// </summary>
        private void UpdateMetrics(object sender, EventArgs e)
        {
            try
            {
                // Crear snapshot actual
                var snapshot = CreateMetricSnapshot();
                _metricHistory.Enqueue(snapshot);

                // Mantener solo últimos 100 snapshots
                while (_metricHistory.Count > 100)
                {
                    _metricHistory.Dequeue();
                }

                // Actualizar UI
                UpdateOverviewMetrics(snapshot);
                UpdatePerformanceMetrics(snapshot);
                UpdateAIMetrics(snapshot);
                UpdateCharts(snapshot);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating metrics: {ex.Message}");
            }
        }

        /// <summary>
        /// Crea snapshot de métricas actuales
        /// </summary>
        private MetricSnapshot CreateMetricSnapshot()
        {
            return new MetricSnapshot
            {
                Timestamp = DateTime.UtcNow,
                TotalDownloads = GetTotalDownloads(),
                ActiveDownloads = GetActiveDownloads(),
                QueueSize = GetQueueSize(),
                SuccessRate = GetSuccessRate(),
                TotalSearches = GetTotalSearches(),
                AvgResponseTime = GetAvgResponseTime(),
                NetworkStatus = GetNetworkStatus(),
                Uptime = DateTime.UtcNow - _startTime,
                MemoryUsage = GetMemoryUsage(),
                CPUUsage = GetCPUUsage(),
                CacheHitRate = GetCacheHitRate(),
                PredictionsAccuracy = GetPredictionsAccuracy(),
                ClassificationsCount = GetClassificationsCount(),
                LearningDataSize = GetLearningDataSize()
            };
        }

        /// <summary>
        /// Actualiza métricas de overview
        /// </summary>
        private void UpdateOverviewMetrics(MetricSnapshot snapshot)
        {
            _mainForm.SafeBeginInvoke(() =>
            {
                lblTotalDownloads.Text = snapshot.TotalDownloads.ToString("N0");
                lblActiveDownloads.Text = snapshot.ActiveDownloads.ToString();
                lblQueueSize.Text = snapshot.QueueSize.ToString("N0");
                lblSuccessRate.Text = $"{snapshot.SuccessRate:P1}";
                lblTotalSearches.Text = snapshot.TotalSearches.ToString("N0");
                lblAvgResponseTime.Text = $"{snapshot.AvgResponseTime:F0}ms";
                lblNetworkStatus.Text = snapshot.NetworkStatus;
                lblNetworkStatus.ForeColor = snapshot.NetworkStatus == "Conectado" ? Color.LightGreen : Color.Red;
                lblUptime.Text = FormatUptime(snapshot.Uptime);
            });
        }

        /// <summary>
        /// Actualiza métricas de rendimiento
        /// </summary>
        private void UpdatePerformanceMetrics(MetricSnapshot snapshot)
        {
            _mainForm.SafeBeginInvoke(() =>
            {
                lblMemoryUsage.Text = $"{snapshot.MemoryUsage:F0} MB";
                lblCPUUsage.Text = $"{snapshot.CPUUsage:F1}%";
                lblCacheHitRate.Text = $"{snapshot.CacheHitRate:P1}";

                progressBarMemory.Value = (int)(snapshot.MemoryUsage / 1024); // Asumir 8GB max
                progressBarCPU.Value = (int)snapshot.CPUUsage;
            });
        }

        /// <summary>
        /// Actualiza métricas de IA
        /// </summary>
        private void UpdateAIMetrics(MetricSnapshot snapshot)
        {
            _mainForm.SafeBeginInvoke(() =>
            {
                lblPredictionsAccuracy.Text = $"{snapshot.PredictionsAccuracy:P1}";
                lblClassificationsCount.Text = snapshot.ClassificationsCount.ToString("N0");
                lblLearningDataSize.Text = $"{snapshot.LearningDataSize:F1} MB";

                progressBarAccuracy.Value = (int)(snapshot.PredictionsAccuracy * 100);
            });
        }

        /// <summary>
        /// Actualiza gráficos
        /// </summary>
        private void UpdateCharts(MetricSnapshot snapshot)
        {
            _mainForm.SafeBeginInvoke(() =>
            {
                // Actualizar gráfico de velocidad de descarga
                UpdateDownloadSpeedChart(snapshot);
                
                // Actualizar gráfico de historial de búsquedas
                UpdateSearchHistoryChart(snapshot);
                
                // Actualizar gráfico de uso de red
                UpdateNetworkUsageChart(snapshot);
                
                // Actualizar gráfico de métricas de IA
                UpdateAIMetricsChart(snapshot);
            });
        }

        /// <summary>
        /// Actualiza gráfico de velocidad de descarga
        /// </summary>
        private void UpdateDownloadSpeedChart(MetricSnapshot snapshot)
        {
            if (chartDownloadSpeed.Series[0].Points.Count > 50)
            {
                chartDownloadSpeed.Series[0].Points.RemoveAt(0);
            }

            chartDownloadSpeed.Series[0].Points.AddXY(
                snapshot.Timestamp.ToString("HH:mm:ss"),
                snapshot.ActiveDownloads * 100 // Simular velocidad
            );
        }

        /// <summary>
        /// Actualiza gráfico de historial de búsquedas
        /// </summary>
        private void UpdateSearchHistoryChart(MetricSnapshot snapshot)
        {
            if (chartSearchHistory.Series[0].Points.Count > 20)
            {
                chartSearchHistory.Series[0].Points.RemoveAt(0);
            }

            chartSearchHistory.Series[0].Points.AddXY(
                snapshot.Timestamp.ToString("HH:mm"),
                snapshot.TotalSearches
            );
        }

        /// <summary>
        /// Actualiza gráfico de uso de red
        /// </summary>
        private void UpdateNetworkUsageChart(MetricSnapshot snapshot)
        {
            if (chartNetworkUsage.Series[0].Points.Count > 30)
            {
                chartNetworkUsage.Series[0].Points.RemoveAt(0);
                chartNetworkUsage.Series[1].Points.RemoveAt(0);
            }

            chartNetworkUsage.Series[0].Points.AddXY(
                snapshot.Timestamp.ToString("HH:mm:ss"),
                snapshot.ActiveDownloads * 50 // Simular subida
            );

            chartNetworkUsage.Series[1].Points.AddXY(
                snapshot.Timestamp.ToString("HH:mm:ss"),
                snapshot.ActiveDownloads * 200 // Simular bajada
            );
        }

        /// <summary>
        /// Actualiza gráfico de métricas de IA
        /// </summary>
        private void UpdateAIMetricsChart(MetricSnapshot snapshot)
        {
            if (chartAIMetrics.Series[0].Points.Count > 40)
            {
                chartAIMetrics.Series[0].Points.RemoveAt(0);
            }

            chartAIMetrics.Series[0].Points.AddXY(
                snapshot.Timestamp.ToString("HH:mm:ss"),
                snapshot.PredictionsAccuracy * 100
            );
        }

        #region Métodos de Obtención de Métricas

        private int GetTotalDownloads()
        {
            // Implementar con datos reales de MainForm
            return 1234; // Simulado
        }

        private int GetActiveDownloads()
        {
            // Implementar con datos reales de MainForm
            return 5; // Simulado
        }

        private int GetQueueSize()
        {
            // Implementar con datos reales de MainForm
            return 42; // Simulado
        }

        private double GetSuccessRate()
        {
            // Implementar con datos reales de MainForm
            return 0.87; // 87% simulado
        }

        private int GetTotalSearches()
        {
            // Implementar con datos reales de MainForm
            return 567; // Simulado
        }

        private double GetAvgResponseTime()
        {
            // Implementar con datos reales de MainForm
            return 245.5; // ms simulado
        }

        private string GetNetworkStatus()
        {
            // Implementar con datos reales de MainForm
            return "Conectado"; // Simulado
        }

        private double GetMemoryUsage()
        {
            // Implementar con datos reales del sistema
            return 512.5; // MB simulado
        }

        private double GetCPUUsage()
        {
            // Implementar con datos reales del sistema
            return 15.3; // % simulado
        }

        private double GetCacheHitRate()
        {
            // Implementar con datos reales del cache
            return 0.92; // 92% simulado
        }

        private double GetPredictionsAccuracy()
        {
            // Implementar con datos reales del AutoLearningEngine
            return 0.78; // 78% simulado
        }

        private int GetClassificationsCount()
        {
            // Implementar con datos reales del ContentClassifier
            return 892; // Simulado
        }

        private double GetLearningDataSize()
        {
            // Implementar con datos reales del AutoLearningEngine
            return 12.4; // MB simulado
        }

        #endregion

        /// <summary>
        /// Formatea tiempo de actividad
        /// </summary>
        private string FormatUptime(TimeSpan uptime)
        {
            return $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
        }

        /// <summary>
        /// Exporta métricas a archivo
        /// </summary>
        public void ExportMetrics(string filePath)
        {
            try
            {
                var metrics = _metricHistory.ToList();
                // Implementar exportación a CSV/JSON
                System.IO.File.WriteAllText(filePath, "Metrics export placeholder");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exportando métricas: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Libera recursos
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                updateTimer?.Stop();
                updateTimer?.Dispose();
                _autoLearningEngine?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    #region Modelos

    public class MetricSnapshot
    {
        public DateTime Timestamp { get; set; }
        public int TotalDownloads { get; set; }
        public int ActiveDownloads { get; set; }
        public int QueueSize { get; set; }
        public double SuccessRate { get; set; }
        public int TotalSearches { get; set; }
        public double AvgResponseTime { get; set; }
        public string NetworkStatus { get; set; }
        public TimeSpan Uptime { get; set; }
        public double MemoryUsage { get; set; }
        public double CPUUsage { get; set; }
        public double CacheHitRate { get; set; }
        public double PredictionsAccuracy { get; set; }
        public int ClassificationsCount { get; set; }
        public double LearningDataSize { get; set; }
    }

    #endregion
}
