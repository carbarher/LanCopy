using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown
{
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class ToastNotification : Form
    {
        private System.Windows.Forms.Timer fadeTimer;
        private System.Windows.Forms.Timer displayTimer;
        private double opacity = 0;
        private bool closing = false;
        private Label lblIcon;
        private Label lblTitle;
        private Label lblMessage;
        private Panel closeButton;

        public ToastNotification(string title, string message, ToastType type, int durationMs = 4000)
        {
            InitializeComponents(title, message, type);
            SetupTimers(durationMs);
            PositionToast();
        }

        private void InitializeComponents(string title, string message, ToastType type)
        {
            // Configuración del formulario
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(350, 100);
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.Opacity = 0;

            // Panel principal con borde
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 48),
                Padding = new Padding(2)
            };
            this.Controls.Add(mainPanel);

            // Borde de color según tipo
            var borderPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 5,
                BackColor = GetColorForType(type)
            };
            mainPanel.Controls.Add(borderPanel);

            // Contenedor de contenido
            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 48),
                Padding = new Padding(15, 10, 15, 10)
            };
            mainPanel.Controls.Add(contentPanel);

            // Icono
            lblIcon = new Label
            {
                Text = GetIconForType(type),
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = GetColorForType(type),
                AutoSize = true,
                Location = new Point(10, 25)
            };
            contentPanel.Controls.Add(lblIcon);

            // Título
            lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(260, 25),
                Location = new Point(50, 15)
            };
            contentPanel.Controls.Add(lblTitle);

            // Mensaje
            lblMessage = new Label
            {
                Text = message,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.LightGray,
                AutoSize = false,
                Size = new Size(260, 45),
                Location = new Point(50, 35)
            };
            contentPanel.Controls.Add(lblMessage);

            // Botón cerrar
            closeButton = new Panel
            {
                Size = new Size(20, 20),
                Location = new Point(320, 10),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            
            var closeLabel = new Label
            {
                Text = "✕",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.Gray,
                AutoSize = true,
                Location = new Point(4, 0),
                Cursor = Cursors.Hand
            };
            closeButton.Controls.Add(closeLabel);
            contentPanel.Controls.Add(closeButton);

            // Eventos
            closeButton.Click += (s, e) => Close();
            closeLabel.Click += (s, e) => Close();
            closeButton.MouseEnter += (s, e) => closeLabel.ForeColor = Color.White;
            closeButton.MouseLeave += (s, e) => closeLabel.ForeColor = Color.Gray;
            this.Click += (s, e) => Close();
        }

        private void SetupTimers(int durationMs)
        {
            // Timer para fade in/out
            fadeTimer = new System.Windows.Forms.Timer { Interval = 20 };
            fadeTimer.Tick += FadeTimer_Tick;
            fadeTimer.Start();

            // Timer para duración de visualización
            displayTimer = new System.Windows.Forms.Timer { Interval = durationMs };
            displayTimer.Tick += (s, e) =>
            {
                displayTimer.Stop();
                closing = true;
            };
            displayTimer.Start();
        }

        private void FadeTimer_Tick(object sender, EventArgs e)
        {
            if (!closing)
            {
                // Fade in
                if (opacity < 1.0)
                {
                    opacity += 0.05;
                    this.Opacity = opacity;
                }
            }
            else
            {
                // Fade out
                if (opacity > 0)
                {
                    opacity -= 0.05;
                    this.Opacity = opacity;
                }
                else
                {
                    fadeTimer.Stop();
                    this.Close();
                }
            }
        }

        private void PositionToast()
        {
            var screen = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(screen.Right - this.Width - 20, screen.Bottom - this.Height - 20);
        }

        private Color GetColorForType(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success: return Color.FromArgb(76, 175, 80);
                case ToastType.Warning: return Color.FromArgb(255, 152, 0);
                case ToastType.Error: return Color.FromArgb(244, 67, 54);
                case ToastType.Info:
                default: return Color.FromArgb(33, 150, 243);
            }
        }

        private string GetIconForType(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success: return "✓";
                case ToastType.Warning: return "⚠";
                case ToastType.Error: return "✕";
                case ToastType.Info:
                default: return "ℹ";
            }
        }

        public static void Show(string title, string message, ToastType type = ToastType.Info, int durationMs = 4000)
        {
            var toast = new ToastNotification(title, message, type, durationMs);
            toast.Show();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                fadeTimer?.Dispose();
                displayTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // Gestor de múltiples notificaciones
    public static class ToastManager
    {
        private static System.Collections.Generic.List<ToastNotification> activeToasts = new System.Collections.Generic.List<ToastNotification>();
        private static readonly object lockObj = new object();

        public static void Show(string title, string message, ToastType type = ToastType.Info, int durationMs = 4000)
        {
            lock (lockObj)
            {
                // Reposicionar toasts existentes
                var screen = Screen.PrimaryScreen.WorkingArea;
                int yOffset = 20;

                foreach (var toast in activeToasts)
                {
                    if (toast != null && !toast.IsDisposed)
                    {
                        toast.Location = new Point(screen.Right - toast.Width - 20, screen.Bottom - toast.Height - yOffset);
                        yOffset += toast.Height + 10;
                    }
                }

                // Crear nuevo toast
                var newToast = new ToastNotification(title, message, type, durationMs);
                newToast.Location = new Point(screen.Right - newToast.Width - 20, screen.Bottom - newToast.Height - yOffset);
                newToast.FormClosed += (s, e) =>
                {
                    lock (lockObj)
                    {
                        activeToasts.Remove(newToast);
                    }
                };

                activeToasts.Add(newToast);
                newToast.Show();

                // Limpiar toasts cerrados
                activeToasts.RemoveAll(t => t == null || t.IsDisposed);
            }
        }
    }
}
