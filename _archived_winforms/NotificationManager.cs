using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown
{
    /// <summary>
    /// Gestor de notificaciones de Windows
    /// </summary>
    public class NotificationManager : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Form _mainForm;
        private bool _disposed = false;

        public NotificationManager(Form mainForm, Icon appIcon = null)
        {
            _mainForm = mainForm;
            
            _notifyIcon = new NotifyIcon
            {
                Icon = appIcon ?? SystemIcons.Application,
                Visible = true,
                Text = "SlskDown"
            };

            // Doble click para mostrar ventana
            _notifyIcon.DoubleClick += (s, e) =>
            {
                _mainForm.Show();
                _mainForm.WindowState = FormWindowState.Normal;
                _mainForm.BringToFront();
            };

            // MenÃº contextual
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Mostrar", null, (s, e) =>
            {
                _mainForm.Show();
                _mainForm.WindowState = FormWindowState.Normal;
            });
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Salir", null, (s, e) => Application.Exit());
            
            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        /// <summary>
        /// Muestra una notificaciÃ³n de descarga completada
        /// </summary>
        public void NotifyDownloadCompleted(string filename, string author)
        {
            ShowNotification(
                "Descarga Completada",
                $"{filename}\nAutor: {author}",
                ToolTipIcon.Info,
                5000
            );
        }

        /// <summary>
        /// Muestra una notificaciÃ³n de watchlist
        /// </summary>
        public void NotifyWatchlistMatch(string term, int resultsCount)
        {
            ShowNotification(
                "Watchlist - Nuevo Resultado",
                $"Se encontraron {resultsCount} resultados para '{term}'",
                ToolTipIcon.Info,
                5000
            );
        }

        /// <summary>
        /// Muestra una notificaciÃ³n de memoria crÃ­tica
        /// </summary>
        public void NotifyMemoryCritical(long memoryMB)
        {
            ShowNotification(
                "Memoria CrÃ­tica",
                $"Uso de memoria: {memoryMB} MB\nSe recomienda reiniciar la aplicaciÃ³n",
                ToolTipIcon.Warning,
                10000
            );
        }

        /// <summary>
        /// Muestra una notificaciÃ³n de error de conexiÃ³n
        /// </summary>
        public void NotifyConnectionError(string error)
        {
            ShowNotification(
                "Error de ConexiÃ³n",
                $"No se pudo conectar a Soulseek:\n{error}",
                ToolTipIcon.Error,
                8000
            );
        }

        /// <summary>
        /// Muestra una notificaciÃ³n de bÃºsqueda de autor completada
        /// </summary>
        public void NotifyAuthorSearchCompleted(string author, int filesFound, int downloaded)
        {
            ShowNotification(
                "BÃºsqueda de Autor Completada",
                $"{author}\nEncontrados: {filesFound}\nDescargados: {downloaded}",
                ToolTipIcon.Info,
                6000
            );
        }

        /// <summary>
        /// Muestra una notificaciÃ³n de nuevo libro de autor favorito
        /// </summary>
        public void NotifyNewBookFromFavorite(string author, string bookTitle)
        {
            ShowNotification(
                "Nuevo Libro Disponible",
                $"{author} - {bookTitle}",
                ToolTipIcon.Info,
                7000
            );
        }

        /// <summary>
        /// Muestra una notificaciÃ³n genÃ©rica
        /// </summary>
        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int duration = 5000)
        {
            if (_disposed || _notifyIcon == null)
                return;

            try
            {
                _notifyIcon.ShowBalloonTip(duration, title, message, icon);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error mostrando notificaciÃ³n: {ex.Message}");
            }
        }

        /// <summary>
        /// Minimiza la aplicaciÃ³n al system tray
        /// </summary>
        public void MinimizeToTray()
        {
            _mainForm.Hide();
            ShowNotification(
                "SlskDown",
                "AplicaciÃ³n minimizada al system tray",
                ToolTipIcon.Info,
                3000
            );
        }

        /// <summary>
        /// Actualiza el texto del tooltip del icono
        /// </summary>
        public void UpdateTooltip(string text)
        {
            if (_disposed || _notifyIcon == null)
                return;

            // Limitar a 63 caracteres (lÃ­mite de Windows)
            if (text.Length > 63)
                text = text.Substring(0, 60) + "...";

            _notifyIcon.Text = text;
        }

        /// <summary>
        /// Actualiza el icono
        /// </summary>
        public void UpdateIcon(Icon icon)
        {
            if (_disposed || _notifyIcon == null || icon == null)
                return;

            _notifyIcon.Icon = icon;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
        }
    }
}

