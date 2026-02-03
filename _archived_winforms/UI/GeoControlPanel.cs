using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SlskDown.Core;

namespace SlskDown.UI
{
    /// <summary>
    /// Panel de control para configuración geo-aware
    /// </summary>
    public class GeoControlPanel : Form
    {
        private readonly GeoLocationService geoService;
        private readonly GeoAwarePrioritizer prioritizer;
        
        private CheckBox enableGeoCheckBox;
        private TrackBar proximityWeightTrackBar;
        private Label proximityWeightLabel;
        private ListBox recommendationsListBox;
        private Button showMapButton;
        private Button refreshButton;
        private Label statsLabel;
        
        public bool GeoAwareEnabled { get; private set; }
        public double ProximityWeight { get; private set; } = 0.3;
        
        public GeoControlPanel(GeoLocationService service, GeoAwarePrioritizer prior)
        {
            geoService = service;
            prioritizer = prior;
            InitializeComponents();
            LoadStats();
        }
        
        private void InitializeComponents()
        {
            Text = "🌍 Control Geo-Aware - SlskDown";
            Size = new Size(800, 850);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(30, 30, 30);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimumSize = new Size(700, 750);
            
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                AutoScroll = true
            };
            
            int yPos = 10;
            
            // Título
            var titleLabel = new Label
            {
                Text = "🌍 OPTIMIZACIÓN GEOGRÁFICA",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 200, 255),
                AutoSize = true,
                Location = new Point(20, yPos)
            };
            mainPanel.Controls.Add(titleLabel);
            yPos += 50;
            
            // Habilitar/Deshabilitar
            enableGeoCheckBox = new CheckBox
            {
                Text = "Habilitar priorización basada en ubicación geográfica",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                AutoSize = true,
                Location = new Point(20, yPos),
                Checked = true
            };
            enableGeoCheckBox.CheckedChanged += (s, e) =>
            {
                GeoAwareEnabled = enableGeoCheckBox.Checked;
                proximityWeightTrackBar.Enabled = GeoAwareEnabled;
            };
            mainPanel.Controls.Add(enableGeoCheckBox);
            yPos += 40;
            
            // Peso de proximidad
            var weightLabel = new Label
            {
                Text = "Peso de proximidad geográfica:",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                AutoSize = true,
                Location = new Point(20, yPos)
            };
            mainPanel.Controls.Add(weightLabel);
            yPos += 30;
            
            proximityWeightTrackBar = new TrackBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 30,
                TickFrequency = 10,
                Location = new Point(20, yPos),
                Width = 600,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            proximityWeightTrackBar.ValueChanged += (s, e) =>
            {
                ProximityWeight = proximityWeightTrackBar.Value / 100.0;
                proximityWeightLabel.Text = $"{proximityWeightTrackBar.Value}%";
            };
            mainPanel.Controls.Add(proximityWeightTrackBar);
            
            proximityWeightLabel = new Label
            {
                Text = "30%",
                ForeColor = Color.FromArgb(0, 200, 255),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(630, yPos + 5),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            mainPanel.Controls.Add(proximityWeightLabel);
            yPos += 60;
            
            // Estadísticas
            var statsGroupBox = new GroupBox
            {
                Text = "📊 Estadísticas",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = new Point(20, yPos),
                Size = new Size(740, 150),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            
            statsLabel = new Label
            {
                ForeColor = Color.White,
                Font = new Font("Consolas", 9),
                Location = new Point(10, 25),
                Size = new Size(720, 115),
                Text = "Cargando...",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            statsGroupBox.Controls.Add(statsLabel);
            mainPanel.Controls.Add(statsGroupBox);
            yPos += 170;
            
            // Recomendaciones de proveedores
            var recLabel = new Label
            {
                Text = "Top Proveedores (por proximidad y calidad):",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, yPos)
            };
            mainPanel.Controls.Add(recLabel);
            yPos += 35;
            
            recommendationsListBox = new ListBox
            {
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                Font = new Font("Consolas", 9),
                Location = new Point(20, yPos),
                Size = new Size(740, 220),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            mainPanel.Controls.Add(recommendationsListBox);
            yPos += 230;
            
            // Botones
            showMapButton = new Button
            {
                Text = "Ver Mapa Mundial",
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(20, yPos),
                Size = new Size(220, 40),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            showMapButton.FlatAppearance.BorderSize = 0;
            showMapButton.Click += ShowMapButton_Click;
            mainPanel.Controls.Add(showMapButton);
            
            refreshButton = new Button
            {
                Text = "Actualizar",
                BackColor = Color.FromArgb(0, 150, 100),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(260, yPos),
                Size = new Size(180, 40),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            refreshButton.FlatAppearance.BorderSize = 0;
            refreshButton.Click += (s, e) => LoadStats();
            mainPanel.Controls.Add(refreshButton);
            
            var closeButton = new Button
            {
                Text = "Cerrar",
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(460, yPos),
                Size = new Size(180, 40),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Click += (s, e) => Close();
            mainPanel.Controls.Add(closeButton);
            
            Controls.Add(mainPanel);
            
            GeoAwareEnabled = true;
        }
        
        private void LoadStats()
        {
            try
            {
                var stats = geoService.GetStats();
                
                var text = $"📍 Tu ubicación: {stats.MyLocation?.ToString() ?? "Desconocida"}\n";
                text += $"📊 Ubicaciones en cache: {stats.TotalCached}\n";
                
                if (stats.AverageDistance > 0)
                {
                    text += $"📏 Distancia promedio: {stats.AverageDistance:F0} km\n";
                    text += $"   Más cercano: {stats.MinDistance:F0} km | Más lejano: {stats.MaxDistance:F0} km";
                }
                
                statsLabel.Text = text;
                
                // Cargar top países
                recommendationsListBox.Items.Clear();
                
                if (stats.CountryDistribution?.Any() == true)
                {
                    recommendationsListBox.Items.Add("═══════════════════════════════════════════════════");
                    recommendationsListBox.Items.Add("  TOP PAÍSES POR CANTIDAD DE PROVEEDORES");
                    recommendationsListBox.Items.Add("═══════════════════════════════════════════════════");
                    
                    int rank = 1;
                    foreach (var country in stats.CountryDistribution.Take(10))
                    {
                        var medal = rank <= 3 ? new[] { "🥇", "🥈", "🥉" }[rank - 1] : $"{rank}.";
                        var bar = new string('█', Math.Min(20, country.Value));
                        recommendationsListBox.Items.Add($"{medal} {country.Key,-20} {bar} ({country.Value})");
                        rank++;
                    }
                }
                else
                {
                    recommendationsListBox.Items.Add("No hay datos de proveedores aún.");
                    recommendationsListBox.Items.Add("Realiza algunas búsquedas para poblar el mapa.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cargando estadísticas: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void ShowMapButton_Click(object sender, EventArgs e)
        {
            try
            {
                var mapForm = new GeoMapForm(geoService);
                mapForm.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo mapa: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
