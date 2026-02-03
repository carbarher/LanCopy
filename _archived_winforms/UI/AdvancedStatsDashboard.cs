using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ScottPlot;
using ScottPlot.WinForms;
using System.Drawing;

namespace SlskDown.UI
{
    public class AdvancedStatsDashboard : Form
    {
        private readonly FormsPlot _speedPlot;
        private readonly FormsPlot _networkPlot;
        private readonly List<double> _speedHistory = new();
        private readonly List<double> _timePoints = new();
        private DateTime _startTime;

        public AdvancedStatsDashboard()
        {
            Text = "📊 Dashboard de Estadísticas Avanzadas";
            Size = new Size(800, 600);
            StartPosition = FormStartPosition.CenterParent;
            
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            _speedPlot = new FormsPlot { Dock = DockStyle.Fill };
            _networkPlot = new FormsPlot { Dock = DockStyle.Fill };

            table.Controls.Add(_speedPlot, 0, 0);
            table.Controls.Add(_networkPlot, 0, 1);
            Controls.Add(table);

            _startTime = DateTime.Now;
            SetupPlots();
        }

        private void SetupPlots()
        {
            _speedPlot.Plot.Title("Velocidad de Descarga Real (MB/s)");
            _speedPlot.Plot.XLabel("Tiempo (s)");
            _speedPlot.Plot.YLabel("MB/s");

            _networkPlot.Plot.Title("Distribución por Red");
            _networkPlot.Refresh();
        }

        public void UpdateSpeed(double speedMbps)
        {
            double elapsed = (DateTime.Now - _startTime).TotalSeconds;
            _speedHistory.Add(speedMbps);
            _timePoints.Add(elapsed);

            if (_speedHistory.Count > 100)
            {
                _speedHistory.RemoveAt(0);
                _timePoints.RemoveAt(0);
            }

            _speedPlot.Plot.Clear();
            _speedPlot.Plot.Add.Scatter(_timePoints.ToArray(), _speedHistory.ToArray());
            _speedPlot.Plot.Axes.AutoScale();
            _speedPlot.Refresh();
        }

        public void UpdateNetworkStats(Dictionary<string, int> stats)
        {
            _networkPlot.Plot.Clear();
            var pies = new List<ScottPlot.PieSlice>();
            
            foreach (var stat in stats)
            {
                pies.Add(new ScottPlot.PieSlice { Value = stat.Value, Label = stat.Key });
            }

            var pie = _networkPlot.Plot.Add.Pie(pies);
            pie.ExplodeFraction = 0.1;
            // pie.ShowSliceLabels = true; // Removido por incompatibilidad con la versión actual de ScottPlot
            
            _networkPlot.Refresh();
        }
    }
}
