using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SlskDown.Core;

namespace SlskDown
{
    public partial class MainForm
    {
        private Panel dashboardPanel;
        private Label lblTodayDownloads;
        private Label lblTodaySearches;
        private Label lblTotalSpeed;
        private Label lblActiveTime;
        private Label lblTopAuthor;
        private Label lblSeriesDetected;
        private DateTime sessionStart = DateTime.Now;

        private void CreateDashboardPanel()
        {
            dashboardPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 200,
                BackColor = Color.FromArgb(25, 25, 25),
                Padding = new Padding(20),
                Visible = true
            };

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = Color.Transparent
            };

            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            // Tarjeta 1: Descargas de hoy
            var card1 = CreateStatCard("📥 Descargas Hoy", "0 archivos", "0 MB", Color.FromArgb(0, 120, 215));
            lblTodayDownloads = card1.Controls.OfType<Label>().Skip(1).First();
            mainLayout.Controls.Add(card1, 0, 0);

            // Tarjeta 2: Búsquedas
            var card2 = CreateStatCard("🔍 Búsquedas", "0 realizadas", "0 resultados", Color.FromArgb(138, 43, 226));
            lblTodaySearches = card2.Controls.OfType<Label>().Skip(1).First();
            mainLayout.Controls.Add(card2, 1, 0);

            // Tarjeta 3: Velocidad
            var card3 = CreateStatCard("⚡ Velocidad", "0 MB/s", "Promedio", Color.FromArgb(255, 140, 0));
            lblTotalSpeed = card3.Controls.OfType<Label>().Skip(1).First();
            mainLayout.Controls.Add(card3, 2, 0);

            // Tarjeta 4: Tiempo activo
            var card4 = CreateStatCard("⏱️ Sesión", "0h 0m", "Activo", Color.FromArgb(60, 120, 60));
            lblActiveTime = card4.Controls.OfType<Label>().Skip(1).First();
            mainLayout.Controls.Add(card4, 0, 1);

            // Tarjeta 5: Top autor
            var card5 = CreateStatCard("⭐ Top Autor", "N/A", "0 descargas", Color.FromArgb(255, 100, 100));
            lblTopAuthor = card5.Controls.OfType<Label>().Skip(1).First();
            mainLayout.Controls.Add(card5, 1, 1);

            // Tarjeta 6: Series detectadas
            var card6 = CreateStatCard("📚 Series", "0 detectadas", "0 completas", Color.FromArgb(100, 200, 255));
            lblSeriesDetected = card6.Controls.OfType<Label>().Skip(1).First();
            mainLayout.Controls.Add(card6, 2, 1);

            dashboardPanel.Controls.Add(mainLayout);
        }

        private Panel CreateStatCard(string title, string value, string subtitle, Color accentColor)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(35, 35, 35),
                Margin = new Padding(5),
                Padding = new Padding(15)
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = accentColor,
                AutoSize = true,
                Location = new Point(10, 10)
            };
            card.Controls.Add(lblTitle);

            var lblValue = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(10, 40)
            };
            card.Controls.Add(lblValue);

            var lblSubtitle = new Label
            {
                Text = subtitle,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.Gray,
                AutoSize = true,
                Location = new Point(10, 75)
            };
            card.Controls.Add(lblSubtitle);

            return card;
        }

        private void UpdateDashboardStats()
        {
            try
            {
                SafeInvoke(() =>
                {
                    if (lblTodayDownloads == null) return;

                    // Descargas de hoy
                    int todayDownloads = 0;
                    long todaySize = 0;
                    lock (downloadQueueLock)
                    {
                        var today = DateTime.Today;
                        todayDownloads = downloadQueue.Count(d => 
                            d.Status == DownloadStatus.Completed && 
                            d.CompletedAt.HasValue && 
                            d.CompletedAt.Value.Date == today);
                        
                        todaySize = downloadQueue
                            .Where(d => d.Status == DownloadStatus.Completed && 
                                       d.CompletedAt.HasValue && 
                                       d.CompletedAt.Value.Date == today)
                            .Sum(d => d.File?.SizeBytes ?? 0);
                    }
                    lblTodayDownloads.Text = $"{todayDownloads} archivos\n{FormatBytes(todaySize)}";

                    // Búsquedas
                    lblTodaySearches.Text = $"{searchCount} realizadas\n{allResults?.Count ?? 0} resultados";

                    // Velocidad
                    var currentSpeed = GetTotalDownloadSpeed();
                    lblTotalSpeed.Text = $"{FormatSpeed(currentSpeed)}\nActual";

                    // Tiempo activo
                    var elapsed = DateTime.Now - sessionStart;
                    lblActiveTime.Text = $"{elapsed.Hours}h {elapsed.Minutes}m\nEn línea";

                    // Top autor
                    var topAuthor = GetTopAuthor();
                    if (topAuthor != null)
                    {
                        lblTopAuthor.Text = $"{topAuthor.Item1}\n{topAuthor.Item2} descargas";
                    }

                    // Series detectadas
                    var seriesStats = DetectSeriesInDownloads();
                    lblSeriesDetected.Text = $"{seriesStats.Item1} series\n{seriesStats.Item2} completas";
                });
            }
            catch { }
        }

        private Tuple<string, int> GetTopAuthor()
        {
            try
            {
                lock (downloadQueueLock)
                {
                    var authorCounts = downloadQueue
                        .Where(d => d.Status == DownloadStatus.Completed && !string.IsNullOrEmpty(d.File?.Username))
                        .GroupBy(d => d.File.Username)
                        .Select(g => new { Author = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .FirstOrDefault();

                    if (authorCounts != null)
                        return Tuple.Create(authorCounts.Author, authorCounts.Count);
                }
            }
            catch { }
            return null;
        }

        private Tuple<int, int> DetectSeriesInDownloads()
        {
            try
            {
                lock (downloadQueueLock)
                {
                    var completedFiles = downloadQueue
                        .Where(d => d.Status == DownloadStatus.Completed && d.File != null)
                        .Select(d => d.File.FileName)
                        .ToList();

                    var seriesGroups = SeriesDetector.GroupBySeries(completedFiles);
                    var completeSeries = seriesGroups.Count(kvp => 
                        SeriesDetector.FindMissingVolumes(kvp.Value).Count == 0 &&
                        kvp.Value.All(s => s.VolumeNumber.HasValue));

                    return Tuple.Create(seriesGroups.Count, completeSeries);
                }
            }
            catch { }
            return Tuple.Create(0, 0);
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }

        private string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond < 1024) return $"{bytesPerSecond:F0} B/s";
            if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024:F1} KB/s";
            return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
        }
    }
}
