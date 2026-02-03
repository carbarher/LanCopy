using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using SlskDown.Core;

namespace SlskDown.UI
{
    /// <summary>
    /// Control de progreso mejorado con gráficos y estadísticas en tiempo real
    /// </summary>
    public partial class EnhancedProgressControl : UserControl
    {
        private PurgeProgress _currentProgress;
        private StealthPurgeManager.StealthMode _currentMode = StealthPurgeManager.StealthMode.Conservative;
        private DateTime _startTime;
        private Timer _updateTimer;
        
        // Colores para tema oscuro
        private readonly Color _backgroundColor = Color.FromArgb(30, 30, 30);
        private readonly Color _foregroundColor = Color.FromArgb(45, 45, 45);
        private readonly Color _primaryColor = Color.FromArgb(0, 120, 215);
        private readonly Color _successColor = Color.FromArgb(40, 167, 69);
        private readonly Color _warningColor = Color.FromArgb(255, 193, 7);
        private readonly Color _dangerColor = Color.FromArgb(220, 53, 69);
        private readonly Color _textColor = Color.White;
        
        public EnhancedProgressControl()
        {
            InitializeComponent();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | 
                    ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);
            
            InitializeTimer();
        }
        
        private void InitializeComponent()
        {
            BackColor = _backgroundColor;
            Size = new Size(600, 120);
            MinimumSize = new Size(400, 100);
        }
        
        private void InitializeTimer()
        {
            _updateTimer = new Timer
            {
                Interval = 500 // Actualizar cada 500ms
            };
            _updateTimer.Tick += (s, e) => Invalidate();
        }
        
        /// <summary>
        /// Actualiza el progreso actual
        /// </summary>
        public void UpdateProgress(PurgeProgress progress, StealthPurgeManager.StealthMode mode)
        {
            _currentProgress = progress;
            _currentMode = mode;
            
            if (!_updateTimer.Enabled)
            {
                _startTime = DateTime.UtcNow;
                _updateTimer.Start();
            }
            
            Invalidate();
        }
        
        /// <summary>
        /// Detiene la actualización automática
        /// </summary>
        public void Stop()
        {
            _updateTimer?.Stop();
        }
        
        /// <summary>
        /// Reinicia el control
        /// </summary>
        public void Reset()
        {
            _currentProgress = null;
            _currentMode = StealthPurgeManager.StealthMode.Conservative;
            _updateTimer?.Stop();
            Invalidate();
        }
        
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            
            // Fondo
            using var bgBrush = new SolidBrush(_backgroundColor);
            g.FillRectangle(bgBrush, ClientRectangle);
            
            if (_currentProgress == null)
            {
                DrawEmptyState(g);
                return;
            }
            
            // Dibujar componentes
            var rect = ClientRectangle;
            rect.Inflate(-10, -10);
            
            // Barra de progreso principal
            var progressBarRect = new Rectangle(rect.X, rect.Y, rect.Width, 30);
            DrawProgressBar(g, progressBarRect);
            
            // Estadísticas
            var statsRect = new Rectangle(rect.X, progressBarRect.Bottom + 10, rect.Width, 40);
            DrawStatistics(g, statsRect);
            
            // Modo y tiempo
            var modeRect = new Rectangle(rect.X, statsRect.Bottom + 5, rect.Width, 25);
            DrawModeInfo(g, modeRect);
        }
        
        private void DrawEmptyState(Graphics g)
        {
            using var brush = new SolidBrush(Color.FromArgb(100, _textColor));
            using var font = new Font("Segoe UI", 12, FontStyle.Regular);
            
            var text = "Esperando operación...";
            var size = g.MeasureString(text, font);
            var location = new PointF(
                (Width - size.Width) / 2,
                (Height - size.Height) / 2
            );
            
            g.DrawString(text, font, brush, location);
        }
        
        private void DrawProgressBar(Graphics g, Rectangle rect)
        {
            // Fondo de la barra
            using var bgBrush = new SolidBrush(_foregroundColor);
            g.FillRectangle(bgBrush, rect);
            
            // Progreso
            if (_currentProgress.TotalItems > 0)
            {
                var progressWidth = (int)(rect.Width * _currentProgress.PercentComplete / 100);
                var progressRect = new Rectangle(rect.X, rect.Y, progressWidth, rect.Height);
                
                // Gradiente según velocidad
                var color = GetProgressColor();
                using var progressBrush = new LinearGradientBrush(
                    progressRect, 
                    Color.FromArgb(100, color), 
                    color, 
                    LinearGradientMode.Vertical
                );
                g.FillRectangle(progressBrush, progressRect);
                
                // Borde
                using var pen = new Pen(color, 1);
                g.DrawRectangle(pen, rect);
                
                // Texto de porcentaje
                using var textBrush = new SolidBrush(_textColor);
                using var font = new Font("Segoe UI", 10, FontStyle.Bold);
                var percentText = $"{_currentProgress.PercentComplete:F1}%";
                var textSize = g.MeasureString(percentText, font);
                var textLocation = new PointF(
                    rect.X + (rect.Width - textSize.Width) / 2,
                    rect.Y + (rect.Height - textSize.Height) / 2
                );
                g.DrawString(percentText, font, textBrush, textLocation);
            }
        }
        
        private void DrawStatistics(Graphics g, Rectangle rect)
        {
            using var brush = new SolidBrush(_textColor);
            using var font = new Font("Segoe UI", 9, FontStyle.Regular);
            
            var stats = new[]
            {
                $"{_currentProgress.ValidItems} válidos",
                $"{_currentProgress.InvalidItems} inválidos",
                $"{_currentProgress.ElapsedTime.TotalMinutes:F1}min",
                $"ETA: {_currentProgress.EstimatedTimeRemaining.TotalMinutes:F1}min"
            };
            
            var spacing = rect.Width / stats.Length;
            for (int i = 0; i < stats.Length; i++)
            {
                var x = rect.X + i * spacing + 10;
                var y = rect.Y + 5;
                g.DrawString(stats[i], font, brush, x, y);
            }
        }
        
        private void DrawModeInfo(Graphics g, Rectangle rect)
        {
            using var brush = new SolidBrush(_textColor);
            using var font = new Font("Segoe UI", 8, FontStyle.Regular);
            
            // Modo actual
            var modeText = $"🥷 Modo: {_currentMode}";
            var modeColor = GetModeColor();
            using var modeBrush = new SolidBrush(modeColor);
            g.DrawString(modeText, font, modeBrush, rect.X, rect.Y);
            
            // Velocidad actual
            var elapsed = DateTime.UtcNow - _startTime;
            var speed = elapsed.TotalMinutes > 0 ? _currentProgress.ProcessedItems / elapsed.TotalMinutes : 0;
            var speedText = $"{speed:F1} items/min";
            g.DrawString(speedText, font, brush, rect.X + 150, rect.Y);
            
            // Items procesados
            var itemsText = $"{_currentProgress.ProcessedItems}/{_currentProgress.TotalItems}";
            g.DrawString(itemsText, brush, rect.X + 300, rect.Y);
            
            // Estado del servidor
            var serverStatus = GetServerStatusText();
            var serverColor = GetServerStatusColor();
            using var serverBrush = new SolidBrush(serverColor);
            g.DrawString(serverStatus, font, serverBrush, rect.X + 450, rect.Y);
        }
        
        private Color GetProgressColor()
        {
            var speed = _currentProgress.ProcessedItems / Math.Max(_currentProgress.ElapsedTime.TotalMinutes, 1);
            
            return speed switch
            {
                < 5 => _dangerColor,      // Muy lento
                < 10 => _warningColor,    // Lento
                < 20 => _primaryColor,    // Normal
                _ => _successColor        // Rápido
            };
        }
        
        private Color GetModeColor()
        {
            return _currentMode switch
            {
                StealthPurgeManager.StealthMode.Conservative => _primaryColor,
                StealthPurgeManager.StealthMode.Stealth => _warningColor,
                StealthPurgeManager.StealthMode.UltraStealth => _dangerColor,
                _ => _textColor
            };
        }
        
        private string GetServerStatusText()
        {
            // Esto debería conectarse al RateLimitDetector para obtener estado real
            return "🟢 Servidor OK";
        }
        
        private Color GetServerStatusColor()
        {
            // Esto debería conectarse al RateLimitDetector para obtener estado real
            return _successColor;
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _updateTimer?.Stop();
                _updateTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
    
    /// <summary>
    /// Panel de control para modo de purga
    /// </summary>
    public partial class PurgeModePanel : UserControl
    {
        public event EventHandler<StealthPurgeManager.StealthMode> ModeChanged;
        
        private readonly RadioButton _rbConservative;
        private readonly RadioButton _rbStealth;
        private readonly RadioButton _rbUltraStealth;
        
        public StealthPurgeManager.StealthMode CurrentMode { get; private set; }
        
        public PurgeModePanel()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            BackColor = Color.FromArgb(30, 30, 30);
            Size = new Size(300, 100);
            
            // Título
            var lblTitle = new Label
            {
                Text = "🥷 Modo de Purga:",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = new Point(10, 10),
                AutoSize = true
            };
            Controls.Add(lblTitle);
            
            // Radio buttons
            _rbConservative = CreateRadioButton("Conservador", "4-6s", new Point(20, 35));
            _rbStealth = CreateRadioButton("Stealth", "6-15s", new Point(20, 55));
            _rbUltraStealth = CreateRadioButton("Ultra Stealth", "10-30s", new Point(20, 75));
            
            _rbConservative.Checked = true;
            CurrentMode = StealthPurgeManager.StealthMode.Conservative;
            
            // Eventos
            _rbConservative.CheckedChanged += (s, e) => OnModeChanged(StealthPurgeManager.StealthMode.Conservative);
            _rbStealth.CheckedChanged += (s, e) => OnModeChanged(StealthPurgeManager.StealthMode.Stealth);
            _rbUltraStealth.CheckedChanged += (s, e) => OnModeChanged(StealthPurgeManager.StealthMode.UltraStealth);
        }
        
        private RadioButton CreateRadioButton(string text, string description, Point location)
        {
            var rb = new RadioButton
            {
                Text = $"{text} ({description})",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Location = location,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            Controls.Add(rb);
            return rb;
        }
        
        private void OnModeChanged(StealthPurgeManager.StealthMode mode)
        {
            if (mode != CurrentMode)
            {
                CurrentMode = mode;
                ModeChanged?.Invoke(this, mode);
            }
        }
    }
}
