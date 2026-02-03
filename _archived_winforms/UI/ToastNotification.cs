using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown.UI
{
    /// <summary>
    /// Tipo de notificaciÃ³n toast
    /// </summary>
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// PosiciÃ³n de la notificaciÃ³n toast
    /// </summary>
    public enum ToastPosition
    {
        TopRight,
        TopLeft,
        BottomRight,
        BottomLeft,
        Center
    }

    /// <summary>
    /// NotificaciÃ³n toast no-intrusiva
    /// </summary>
    public class ToastNotification : Form
    {
        private readonly System.Windows.Forms.Timer _timer;
        private int _opacity = 0;
        private bool _fadingIn = true;

        public string Message { get; set; } = string.Empty;
        public ToastType Type { get; set; } = ToastType.Info;
        public int Duration { get; set; } = 3000;
        public ToastPosition Position { get; set; } = ToastPosition.BottomRight;

        public ToastNotification()
        {
            // ConfiguraciÃ³n del formulario
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Opacity = 0;
            Size = new Size(350, 80);

            _timer = new System.Windows.Forms.Timer { Interval = 50 };
            _timer.Tick += Timer_Tick;
        }

        /// <summary>
        /// Muestra la notificaciÃ³n toast
        /// </summary>
        public void Show(Form parent)
        {
            // Configurar colores segÃºn el tipo
            var colors = GetColors();
            BackColor = colors.background;

            // Crear label con el mensaje
            var label = new Label
            {
                Text = GetIcon() + " " + Message,
                ForeColor = colors.foreground,
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(15, 10, 15, 10)
            };

            Controls.Add(label);

            // Posicionar
            SetPosition(parent);

            // Mostrar y animar
            Show();
            _timer.Start();

            // Auto-cerrar despuÃ©s del duration
            var closeTimer = new System.Windows.Forms.Timer { Interval = Duration };
            closeTimer.Tick += (s, e) =>
            {
                _fadingIn = false;
                closeTimer.Stop();
                closeTimer.Dispose();
            };
            closeTimer.Start();
        }

        private void SetPosition(Form parent)
        {
            int x = 0, y = 0;
            int margin = 20;

            switch (Position)
            {
                case ToastPosition.TopRight:
                    x = parent.Right - Width - margin;
                    y = parent.Top + margin;
                    break;
                case ToastPosition.TopLeft:
                    x = parent.Left + margin;
                    y = parent.Top + margin;
                    break;
                case ToastPosition.BottomRight:
                    x = parent.Right - Width - margin;
                    y = parent.Bottom - Height - margin;
                    break;
                case ToastPosition.BottomLeft:
                    x = parent.Left + margin;
                    y = parent.Bottom - Height - margin;
                    break;
                case ToastPosition.Center:
                    x = parent.Left + (parent.Width - Width) / 2;
                    y = parent.Top + (parent.Height - Height) / 2;
                    break;
            }

            Location = new Point(x, y);
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_fadingIn)
            {
                _opacity += 10;
                if (_opacity >= 100)
                {
                    _opacity = 100;
                    _fadingIn = false;
                    _timer.Stop();
                }
            }
            else
            {
                _opacity -= 10;
                if (_opacity <= 0)
                {
                    _opacity = 0;
                    _timer.Stop();
                    Close();
                    Dispose();
                }
            }

            Opacity = _opacity / 100.0;
        }

        private (Color background, Color foreground) GetColors()
        {
            return Type switch
            {
                ToastType.Success => (Color.FromArgb(76, 175, 80), Color.White),
                ToastType.Warning => (Color.FromArgb(255, 152, 0), Color.White),
                ToastType.Error => (Color.FromArgb(244, 67, 54), Color.White),
                _ => (Color.FromArgb(33, 150, 243), Color.White) // Info
            };
        }

        private string GetIcon()
        {
            return Type switch
            {
                ToastType.Success => "âœ“",
                ToastType.Warning => "âš ",
                ToastType.Error => "âœ—",
                _ => "â„¹"
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Helper estÃ¡tico para mostrar toasts fÃ¡cilmente
    /// </summary>
    public static class Toast
    {
        public static void Show(Form parent, string message, ToastType type = ToastType.Info, int duration = 3000)
        {
            var toast = new ToastNotification
            {
                Message = message,
                Type = type,
                Duration = duration,
                Position = ToastPosition.BottomRight
            };
            toast.Show(parent);
        }

        public static void Info(Form parent, string message) => Show(parent, message, ToastType.Info);
        public static void Success(Form parent, string message) => Show(parent, message, ToastType.Success);
        public static void Warning(Form parent, string message) => Show(parent, message, ToastType.Warning);
        public static void Error(Form parent, string message) => Show(parent, message, ToastType.Error);
    }
}

