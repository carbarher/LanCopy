using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SlskDown.Core;

namespace SlskDown.UI
{
    /// <summary>
    /// Formulario que muestra un mapa mundial de ubicaciones de proveedores
    /// </summary>
    public class GeoMapForm : Form
    {
        private readonly GeoLocationService geoService;
        private PictureBox mapPictureBox;
        private Label statsLabel;
        private System.Windows.Forms.Timer refreshTimer;
        
        public GeoMapForm(GeoLocationService service)
        {
            geoService = service;
            InitializeComponents();
            DrawMap();
        }
        
        private void InitializeComponents()
        {
            Text = "🌍 Mapa Mundial de Proveedores - SlskDown";
            Size = new Size(1200, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(30, 30, 30);
            
            // Mapa
            mapPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 20),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            mapPictureBox.Paint += MapPictureBox_Paint;
            
            // Panel de estadísticas
            var statsPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 120,
                BackColor = Color.FromArgb(40, 40, 40),
                Padding = new Padding(10)
            };
            
            statsLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Consolas", 10),
                Text = "Cargando estadísticas..."
            };
            
            statsPanel.Controls.Add(statsLabel);
            
            Controls.Add(mapPictureBox);
            Controls.Add(statsPanel);
            
            // Timer para actualizar cada 5 segundos
            refreshTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            refreshTimer.Tick += (s, e) => DrawMap();
            refreshTimer.Start();
        }
        
        private void DrawMap()
        {
            var stats = geoService.GetStats();
            
            // Actualizar estadísticas
            var statsText = $"📍 Tu ubicación: {stats.MyLocation?.ToString() ?? "Desconocida"}\n";
            statsText += $"📊 Ubicaciones en cache: {stats.TotalCached}\n";
            
            if (stats.AverageDistance > 0)
            {
                statsText += $"📏 Distancia promedio: {stats.AverageDistance:F0} km | ";
                statsText += $"Más cercano: {stats.MinDistance:F0} km | ";
                statsText += $"Más lejano: {stats.MaxDistance:F0} km\n";
            }
            
            if (stats.CountryDistribution?.Any() == true)
            {
                statsText += "🌍 Top países: ";
                statsText += string.Join(", ", stats.CountryDistribution.Take(5).Select(x => $"{x.Key} ({x.Value})"));
            }
            
            statsLabel.Text = statsText;
            
            // Redibujar mapa
            mapPictureBox.Invalidate();
        }
        
        private void MapPictureBox_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            
            var width = mapPictureBox.Width;
            var height = mapPictureBox.Height;
            
            // Dibujar grid del mapa
            using (var gridPen = new Pen(Color.FromArgb(50, 50, 50), 1))
            {
                // Líneas verticales (longitud)
                for (int lon = -180; lon <= 180; lon += 30)
                {
                    var x = (int)((lon + 180) / 360.0 * width);
                    g.DrawLine(gridPen, x, 0, x, height);
                }
                
                // Líneas horizontales (latitud)
                for (int lat = -90; lat <= 90; lat += 30)
                {
                    var y = (int)((90 - lat) / 180.0 * height);
                    g.DrawLine(gridPen, 0, y, width, y);
                }
            }
            
            // Dibujar ecuador y meridiano
            using (var mainPen = new Pen(Color.FromArgb(80, 80, 80), 2))
            {
                // Ecuador
                var equatorY = height / 2;
                g.DrawLine(mainPen, 0, equatorY, width, equatorY);
                
                // Meridiano de Greenwich
                var meridianX = width / 2;
                g.DrawLine(mainPen, meridianX, 0, meridianX, height);
            }
            
            var stats = geoService.GetStats();
            
            // Dibujar tu ubicación
            if (stats.MyLocation != null)
            {
                var myPoint = LatLonToPoint(stats.MyLocation.Latitude, stats.MyLocation.Longitude, width, height);
                
                // Círculo pulsante para tu ubicación
                using (var myBrush = new SolidBrush(Color.FromArgb(200, 0, 255, 0)))
                using (var myPen = new Pen(Color.FromArgb(255, 255, 255), 2))
                {
                    var size = 12;
                    g.FillEllipse(myBrush, myPoint.X - size/2, myPoint.Y - size/2, size, size);
                    g.DrawEllipse(myPen, myPoint.X - size/2, myPoint.Y - size/2, size, size);
                    
                    // Etiqueta
                    using (var font = new Font("Arial", 9, FontStyle.Bold))
                    using (var textBrush = new SolidBrush(Color.White))
                    {
                        g.DrawString("TÚ", font, textBrush, myPoint.X + 8, myPoint.Y - 8);
                    }
                }
            }
            
            // Dibujar ubicaciones de proveedores (simulado - necesitarías integrar con datos reales)
            // Por ahora, dibujar las ubicaciones del cache
            DrawCachedLocations(g, width, height, stats);
        }
        
        private void DrawCachedLocations(Graphics g, int width, int height, GeoStats stats)
        {
            // Aquí dibujarías las ubicaciones reales de los proveedores
            // Por ahora, mostrar distribución por país
            
            if (stats.CountryDistribution == null || !stats.CountryDistribution.Any())
                return;
            
            var random = new Random(42); // Seed fijo para consistencia
            
            foreach (var country in stats.CountryDistribution.Take(20))
            {
                // Generar puntos aleatorios para cada país (en producción usarías coordenadas reales)
                for (int i = 0; i < Math.Min(country.Value, 10); i++)
                {
                    var lat = random.Next(-90, 90);
                    var lon = random.Next(-180, 180);
                    var point = LatLonToPoint(lat, lon, width, height);
                    
                    // Color basado en cantidad
                    var intensity = Math.Min(255, country.Value * 10);
                    using (var brush = new SolidBrush(Color.FromArgb(150, intensity, 100, 255 - intensity)))
                    {
                        g.FillEllipse(brush, point.X - 3, point.Y - 3, 6, 6);
                    }
                }
            }
        }
        
        private Point LatLonToPoint(double lat, double lon, int width, int height)
        {
            // Proyección Mercator simple
            var x = (int)((lon + 180) / 360.0 * width);
            var y = (int)((90 - lat) / 180.0 * height);
            
            return new Point(x, y);
        }
        
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            refreshTimer?.Stop();
            refreshTimer?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
