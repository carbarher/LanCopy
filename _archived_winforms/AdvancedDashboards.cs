using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;

namespace SlskDown
{
    public class AdvancedDashboards
    {
        private InterestsSystem interestsSystem;
        private QueueManagementSystem queueManagement;
        private ShareScannerOptimized shareScanner;
        private Action<string> logAction;
        
        public AdvancedDashboards(
            InterestsSystem interests,
            QueueManagementSystem queue,
            ShareScannerOptimized scanner,
            Action<string> logger)
        {
            interestsSystem = interests;
            queueManagement = queue;
            shareScanner = scanner;
            logAction = logger;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // DASHBOARD DE INTERESES
        // ═══════════════════════════════════════════════════════════════
        
        public Panel CreateInterestsDashboard()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(10)
            };
            
            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 250,
                BackColor = Color.FromArgb(40, 40, 40)
            };
            
            // Panel superior: Gráfico de usuarios similares
            var topPanel = CreateSimilarUsersChart();
            
            // Panel inferior: Top 20 intereses
            var bottomPanel = CreateTopInterestsPanel();
            
            splitContainer.Panel1.Controls.Add(topPanel);
            splitContainer.Panel2.Controls.Add(bottomPanel);
            
            panel.Controls.Add(splitContainer);
            
            return panel;
        }
        
        private Panel CreateSimilarUsersChart()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(35, 35, 35),
                Padding = new Padding(10)
            };
            
            var lblTitle = new Label
            {
                Text = "📊 Distribución de Usuarios Similares por Score",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };
            
            var chartPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 40, 40)
            };
            
            // Obtener datos
            var users = interestsSystem.GetSimilarUsers();
            var ranges = new Dictionary<string, int>
            {
                { "90-100%", users.Count(u => u.SimilarityScore >= 90) },
                { "70-89%", users.Count(u => u.SimilarityScore >= 70 && u.SimilarityScore < 90) },
                { "50-69%", users.Count(u => u.SimilarityScore >= 50 && u.SimilarityScore < 70) },
                { "30-49%", users.Count(u => u.SimilarityScore >= 30 && u.SimilarityScore < 50) },
                { "0-29%", users.Count(u => u.SimilarityScore < 30) }
            };
            
            // Dibujar barras
            chartPanel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                int y = 10;
                int maxCount = ranges.Values.Max();
                if (maxCount == 0) maxCount = 1;
                
                foreach (var range in ranges)
                {
                    int barWidth = (int)((range.Value / (float)maxCount) * (chartPanel.Width - 200));
                    
                    var brush = new SolidBrush(GetColorForRange(range.Key));
                    g.FillRectangle(brush, 150, y, barWidth, 30);
                    
                    g.DrawString(range.Key, new Font("Segoe UI", 9), Brushes.White, 10, y + 7);
                    g.DrawString(range.Value.ToString(), new Font("Segoe UI", 9, FontStyle.Bold), 
                        Brushes.White, 160 + barWidth, y + 7);
                    
                    y += 40;
                }
            };
            
            panel.Controls.Add(chartPanel);
            panel.Controls.Add(lblTitle);
            
            return panel;
        }
        
        private Color GetColorForRange(string range)
        {
            switch (range)
            {
                case "90-100%": return Color.FromArgb(0, 200, 0);
                case "70-89%": return Color.FromArgb(150, 200, 0);
                case "50-69%": return Color.FromArgb(200, 200, 0);
                case "30-49%": return Color.FromArgb(200, 100, 0);
                case "0-29%": return Color.FromArgb(200, 0, 0);
                default: return Color.Gray;
            }
        }
        
        private Panel CreateTopInterestsPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(35, 35, 35),
                Padding = new Padding(10)
            };
            
            var lblTitle = new Label
            {
                Text = "🔥 Top 20 Intereses Más Populares",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };
            
            var lvInterests = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9)
            };
            
            lvInterests.Columns.Add("Rank", 50);
            lvInterests.Columns.Add("Interés", 200);
            lvInterests.Columns.Add("Usuarios", 80);
            lvInterests.Columns.Add("Trending", 80);
            
            // Datos simulados (en producción vendría del servidor)
            var topInterests = new[]
            {
                ("Science Fiction", 1250, "↑ 15%"),
                ("Fantasy", 980, "↑ 8%"),
                ("Mystery", 750, "↓ 3%"),
                ("Romance", 680, "↑ 12%"),
                ("Thriller", 620, "→ 0%")
            };
            
            int rank = 1;
            foreach (var (interest, users, trend) in topInterests)
            {
                var item = new ListViewItem(rank.ToString());
                item.SubItems.Add(interest);
                item.SubItems.Add(users.ToString());
                item.SubItems.Add(trend);
                
                if (trend.StartsWith("↑"))
                    item.ForeColor = Color.LightGreen;
                else if (trend.StartsWith("↓"))
                    item.ForeColor = Color.LightCoral;
                
                lvInterests.Items.Add(item);
                rank++;
            }
            
            panel.Controls.Add(lvInterests);
            panel.Controls.Add(lblTitle);
            
            return panel;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // DASHBOARD DE COLA
        // ═══════════════════════════════════════════════════════════════
        
        public Panel CreateQueueDashboard(Func<List<object>> getQueue)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(10)
            };
            
            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 300,
                BackColor = Color.FromArgb(40, 40, 40)
            };
            
            // Panel izquierdo: Distribución de prioridades (pie chart)
            var leftPanel = CreatePriorityPieChart(getQueue);
            
            // Panel derecho: Estadísticas de cola
            var rightPanel = CreateQueueStatsPanel(getQueue);
            
            splitContainer.Panel1.Controls.Add(leftPanel);
            splitContainer.Panel2.Controls.Add(rightPanel);
            
            panel.Controls.Add(splitContainer);
            
            return panel;
        }
        
        private Panel CreatePriorityPieChart(Func<List<object>> getQueue)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(35, 35, 35),
                Padding = new Padding(10)
            };
            
            var lblTitle = new Label
            {
                Text = "📊 Distribución de Prioridades",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };
            
            var chartPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 40, 40)
            };
            
            chartPanel.Paint += (s, e) =>
            {
                var queue = getQueue();
                if (queue == null || queue.Count == 0) return;
                
                var priorities = new Dictionary<QueuePriority, int>
                {
                    { QueuePriority.Critical, 0 },
                    { QueuePriority.High, 0 },
                    { QueuePriority.Normal, 0 },
                    { QueuePriority.Low, 0 }
                };
                
                foreach (var item in queue)
                {
                    var task = item as dynamic;
                    if (task != null)
                    {
                        var priority = (QueuePriority)(task.Priority ?? (int)QueuePriority.Normal);
                        priorities[priority]++;
                    }
                }
                
                // Dibujar pie chart
                var g = e.Graphics;
                int centerX = chartPanel.Width / 2;
                int centerY = chartPanel.Height / 2;
                int radius = Math.Min(centerX, centerY) - 50;
                
                float startAngle = 0;
                int total = priorities.Values.Sum();
                if (total == 0) return;
                
                var colors = new Dictionary<QueuePriority, Color>
                {
                    { QueuePriority.Critical, Color.FromArgb(200, 0, 0) },
                    { QueuePriority.High, Color.FromArgb(200, 100, 0) },
                    { QueuePriority.Normal, Color.FromArgb(0, 150, 0) },
                    { QueuePriority.Low, Color.FromArgb(0, 100, 200) }
                };
                
                foreach (var kvp in priorities)
                {
                    if (kvp.Value == 0) continue;
                    
                    float sweepAngle = (kvp.Value / (float)total) * 360;
                    
                    using (var brush = new SolidBrush(colors[kvp.Key]))
                    {
                        g.FillPie(brush, centerX - radius, centerY - radius, 
                            radius * 2, radius * 2, startAngle, sweepAngle);
                    }
                    
                    // Etiqueta
                    float labelAngle = startAngle + sweepAngle / 2;
                    float labelX = centerX + (float)(radius * 0.7 * Math.Cos(labelAngle * Math.PI / 180));
                    float labelY = centerY + (float)(radius * 0.7 * Math.Sin(labelAngle * Math.PI / 180));
                    
                    string label = $"{kvp.Key}\n{kvp.Value}";
                    g.DrawString(label, new Font("Segoe UI", 9, FontStyle.Bold), 
                        Brushes.White, labelX - 20, labelY - 10);
                    
                    startAngle += sweepAngle;
                }
            };
            
            panel.Controls.Add(chartPanel);
            panel.Controls.Add(lblTitle);
            
            return panel;
        }
        
        private Panel CreateQueueStatsPanel(Func<List<object>> getQueue)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(35, 35, 35),
                Padding = new Padding(10)
            };
            
            var lblTitle = new Label
            {
                Text = "📈 Estadísticas de Cola",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };
            
            var statsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 40, 40),
                Padding = new Padding(10),
                AutoScroll = true
            };
            
            var queue = getQueue();
            int y = 10;
            
            // Total en cola
            AddStatLabel(statsPanel, "Total en Cola:", queue?.Count.ToString() ?? "0", ref y);
            
            // Tamaño total
            long totalSize = 0;
            if (queue != null)
            {
                foreach (var item in queue)
                {
                    var task = item as dynamic;
                    if (task != null)
                    {
                        totalSize += task.Size ?? 0;
                    }
                }
            }
            AddStatLabel(statsPanel, "Tamaño Total:", FormatSize(totalSize), ref y);
            
            // Tiempo estimado
            AddStatLabel(statsPanel, "Tiempo Estimado:", "~2.5 horas", ref y);
            
            // Tasa de éxito
            AddStatLabel(statsPanel, "Tasa de Éxito:", "87%", ref y);
            
            panel.Controls.Add(statsPanel);
            panel.Controls.Add(lblTitle);
            
            return panel;
        }
        
        private void AddStatLabel(Panel panel, string label, string value, ref int y)
        {
            var lblLabel = new Label
            {
                Text = label,
                Location = new Point(10, y),
                AutoSize = true,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 10)
            };
            
            var lblValue = new Label
            {
                Text = value,
                Location = new Point(200, y),
                AutoSize = true,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            
            panel.Controls.Add(lblLabel);
            panel.Controls.Add(lblValue);
            
            y += 35;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // DASHBOARD DE SHARES
        // ═══════════════════════════════════════════════════════════════
        
        public Panel CreateSharesDashboard()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(10)
            };
            
            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 200,
                BackColor = Color.FromArgb(40, 40, 40)
            };
            
            // Panel superior: Estadísticas generales
            var topPanel = CreateSharesStatsPanel();
            
            // Panel inferior: Distribución de tipos de archivo
            var bottomPanel = CreateFileTypesChart();
            
            splitContainer.Panel1.Controls.Add(topPanel);
            splitContainer.Panel2.Controls.Add(bottomPanel);
            
            panel.Controls.Add(splitContainer);
            
            return panel;
        }
        
        private Panel CreateSharesStatsPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(35, 35, 35),
                Padding = new Padding(10)
            };
            
            var lblTitle = new Label
            {
                Text = "📁 Estadísticas de Shares",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };
            
            var statsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 40, 40),
                Padding = new Padding(10)
            };
            
            int y = 10;
            
            AddStatLabel(statsPanel, "Total de Archivos:", shareScanner.GetFileCount().ToString(), ref y);
            AddStatLabel(statsPanel, "Tamaño Total:", FormatSize(shareScanner.GetTotalSize()), ref y);
            AddStatLabel(statsPanel, "Velocidad de Escaneo:", "1,250 archivos/s", ref y);
            AddStatLabel(statsPanel, "Último Escaneo:", "Hace 2 horas", ref y);
            
            panel.Controls.Add(statsPanel);
            panel.Controls.Add(lblTitle);
            
            return panel;
        }
        
        private Panel CreateFileTypesChart()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(35, 35, 35),
                Padding = new Padding(10)
            };
            
            var lblTitle = new Label
            {
                Text = "📊 Distribución de Tipos de Archivo",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };
            
            var lvTypes = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9)
            };
            
            lvTypes.Columns.Add("Tipo", 100);
            lvTypes.Columns.Add("Cantidad", 100);
            lvTypes.Columns.Add("Porcentaje", 100);
            lvTypes.Columns.Add("Tamaño", 120);
            
            var fileTypes = shareScanner.GetFileTypeStats();
            int total = fileTypes.Values.Sum();
            
            foreach (var kvp in fileTypes.OrderByDescending(x => x.Value).Take(20))
            {
                var item = new ListViewItem(kvp.Key);
                item.SubItems.Add(kvp.Value.ToString());
                item.SubItems.Add($"{(kvp.Value * 100.0 / total):F1}%");
                item.SubItems.Add("N/A");
                
                lvTypes.Items.Add(item);
            }
            
            panel.Controls.Add(lvTypes);
            panel.Controls.Add(lblTitle);
            
            return panel;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // UTILIDADES
        // ═══════════════════════════════════════════════════════════════
        
        private string FormatSize(long bytes)
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
