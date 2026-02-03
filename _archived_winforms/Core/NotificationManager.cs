using System;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

namespace SlskDown.Core
{
    /// <summary>
    /// Gestor de notificaciones del sistema (Windows Toast Notifications)
    /// </summary>
    public class NotificationManager
    {
        private bool _enabled = true;
        private bool _playSound = true;
        private bool _notifyDownloads = true;
        private bool _notifyWishlist = true;
        private bool _notifyMessages = true;
        private bool _notifyErrors = true;
        private bool _onlyWhenMinimized = false;

        public event EventHandler<NotificationClickedEventArgs> NotificationClicked;

        // Configuración
        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public bool PlaySound
        {
            get => _playSound;
            set => _playSound = value;
        }

        public bool NotifyDownloads
        {
            get => _notifyDownloads;
            set => _notifyDownloads = value;
        }

        public bool NotifyWishlist
        {
            get => _notifyWishlist;
            set => _notifyWishlist = value;
        }

        public bool NotifyMessages
        {
            get => _notifyMessages;
            set => _notifyMessages = value;
        }

        public bool NotifyErrors
        {
            get => _notifyErrors;
            set => _notifyErrors = value;
        }

        public bool OnlyWhenMinimized
        {
            get => _onlyWhenMinimized;
            set => _onlyWhenMinimized = value;
        }

        /// <summary>
        /// Notifica cuando se completa una descarga
        /// </summary>
        public void NotifyDownloadComplete(string filename, long sizeBytes, string username, string filePath)
        {
            if (!_enabled || !_notifyDownloads)
                return;

            if (_onlyWhenMinimized && !IsMainFormMinimized())
                return;

            var size = FormatSize(sizeBytes);
            var title = "📥 Descarga Completada";
            var message = $"{filename}\n{size} • {username}";

            ShowNotification(title, message, NotificationType.DownloadComplete, filePath);
        }

        /// <summary>
        /// Notifica cuando hay nuevos resultados en wishlist
        /// </summary>
        public void NotifyWishlistMatch(string query, int newResultsCount)
        {
            if (!_enabled || !_notifyWishlist)
                return;

            if (_onlyWhenMinimized && !IsMainFormMinimized())
                return;

            var title = "🔍 Wishlist";
            var message = $"{newResultsCount} nuevos resultados\n\"{query}\"";

            ShowNotification(title, message, NotificationType.WishlistMatch, query);
        }

        /// <summary>
        /// Notifica cuando se recibe un mensaje privado
        /// </summary>
        public void NotifyMessageReceived(string from, string messagePreview)
        {
            if (!_enabled || !_notifyMessages)
                return;

            if (_onlyWhenMinimized && !IsMainFormMinimized())
                return;

            var title = "💬 Nuevo Mensaje";
            var message = $"De: {from}\n{TruncateMessage(messagePreview, 100)}";

            ShowNotification(title, message, NotificationType.MessageReceived, from);
        }

        /// <summary>
        /// Notifica cuando un usuario favorito se conecta
        /// </summary>
        public void NotifyUserOnline(string username)
        {
            if (!_enabled)
                return;

            if (_onlyWhenMinimized && !IsMainFormMinimized())
                return;

            var title = "👤 Usuario Conectado";
            var message = $"{username} está online";

            ShowNotification(title, message, NotificationType.UserOnline, username);
        }

        /// <summary>
        /// Notifica cuando ocurre un error en descarga
        /// </summary>
        public void NotifyDownloadError(string filename, string errorMessage)
        {
            if (!_enabled || !_notifyErrors)
                return;

            var title = "⚠️ Error de Descarga";
            var message = $"{filename}\n{errorMessage}";

            ShowNotification(title, message, NotificationType.DownloadError, filename);
        }

        /// <summary>
        /// Notifica múltiples descargas completadas
        /// </summary>
        public void NotifyMultipleDownloadsComplete(int count, long totalBytes)
        {
            if (!_enabled || !_notifyDownloads)
                return;

            if (_onlyWhenMinimized && !IsMainFormMinimized())
                return;

            var title = "📥 Descargas Completadas";
            var message = $"{count} archivos descargados\nTotal: {FormatSize(totalBytes)}";

            ShowNotification(title, message, NotificationType.MultipleDownloads, null);
        }

        /// <summary>
        /// Muestra una notificación del sistema
        /// </summary>
        private void ShowNotification(string title, string message, NotificationType type, string data)
        {
            try
            {
                // Usar NotifyIcon para mostrar globo de notificación
                // Esto es compatible con todas las versiones de Windows
                var notifyIcon = new NotifyIcon
                {
                    Icon = System.Drawing.SystemIcons.Information,
                    Visible = true,
                    BalloonTipTitle = title,
                    BalloonTipText = message,
                    BalloonTipIcon = ToolTipIcon.Info
                };

                // Reproducir sonido si está habilitado
                if (_playSound)
                {
                    System.Media.SystemSounds.Asterisk.Play();
                }

                // Manejar click en la notificación
                notifyIcon.BalloonTipClicked += (s, e) =>
                {
                    NotificationClicked?.Invoke(this, new NotificationClickedEventArgs
                    {
                        Type = type,
                        Data = data
                    });

                    // Limpiar el icono
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                };

                // Limpiar el icono después de cerrar
                notifyIcon.BalloonTipClosed += (s, e) =>
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                };

                // Mostrar notificación
                notifyIcon.ShowBalloonTip(5000);
            }
            catch (Exception ex)
            {
                // Ignorar errores de notificación
                Debug.WriteLine($"Error mostrando notificación: {ex.Message}");
            }
        }

        /// <summary>
        /// Verifica si el formulario principal está minimizado
        /// </summary>
        private bool IsMainFormMinimized()
        {
            try
            {
                var mainForm = Application.OpenForms[0];
                return mainForm?.WindowState == FormWindowState.Minimized;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Formatea el tamaño de archivo
        /// </summary>
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

        /// <summary>
        /// Trunca un mensaje a una longitud máxima
        /// </summary>
        private string TruncateMessage(string message, int maxLength)
        {
            if (string.IsNullOrEmpty(message))
                return string.Empty;

            if (message.Length <= maxLength)
                return message;

            return message.Substring(0, maxLength - 3) + "...";
        }

        /// <summary>
        /// Abre la carpeta de un archivo
        /// </summary>
        public void OpenFileLocation(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                else
                {
                    var directory = Path.GetDirectoryName(filePath);
                    if (Directory.Exists(directory))
                    {
                        Process.Start("explorer.exe", directory);
                    }
                }
            }
            catch
            {
                // Ignorar errores
            }
        }
    }

    /// <summary>
    /// Tipos de notificación
    /// </summary>
    public enum NotificationType
    {
        DownloadComplete,
        WishlistMatch,
        MessageReceived,
        UserOnline,
        DownloadError,
        MultipleDownloads
    }

    /// <summary>
    /// Argumentos del evento de click en notificación
    /// </summary>
    public class NotificationClickedEventArgs : EventArgs
    {
        public NotificationType Type { get; set; }
        public string Data { get; set; }
    }
}
