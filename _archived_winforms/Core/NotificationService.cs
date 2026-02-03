using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown.Core
{
    /// <summary>
    /// MEJORA: Servicio de notificaciones del sistema para eventos importantes
    /// Muestra notificaciones toast en Windows para alertar al usuario sobre:
    /// - Purgas completadas
    /// - Archivos corruptos detectados
    /// - Desconexiones prolongadas
    /// - Descargas importantes completadas
    /// </summary>
    public class NotificationService : IDisposable
    {
        private NotifyIcon notifyIcon;
        private readonly Form parentForm;
        private bool isDisposed = false;
        
        // Contadores para evitar spam de notificaciones
        private DateTime lastPurgeNotification = DateTime.MinValue;
        private DateTime lastCorruptionNotification = DateTime.MinValue;
        private DateTime lastDisconnectionNotification = DateTime.MinValue;
        private readonly TimeSpan notificationCooldown = TimeSpan.FromMinutes(5);
        
        public NotificationService(Form parentForm, Icon appIcon = null)
        {
            this.parentForm = parentForm;
            
            // Crear NotifyIcon para mostrar notificaciones
            notifyIcon = new NotifyIcon
            {
                Icon = appIcon ?? SystemIcons.Application,
                Visible = true,
                Text = "SlskDown"
            };
            
            // Evento para restaurar ventana al hacer clic
            notifyIcon.Click += (s, e) =>
            {
                if (parentForm != null && !parentForm.IsDisposed)
                {
                    parentForm.WindowState = FormWindowState.Normal;
                    parentForm.Activate();
                }
            };
        }
        
        /// <summary>
        /// Notifica que una purga ha completado
        /// </summary>
        public void NotifyPurgeCompleted(int totalAuthors, int authorsWithFiles, TimeSpan duration)
        {
            if (!CanNotify(ref lastPurgeNotification))
                return;
            
            var title = "Purga Completada";
            var message = $"Procesados: {totalAuthors} autores\n" +
                         $"Con archivos: {authorsWithFiles}\n" +
                         $"Duración: {FormatDuration(duration)}";
            
            ShowNotification(title, message, ToolTipIcon.Info);
        }
        
        /// <summary>
        /// Notifica que se detectaron archivos corruptos
        /// </summary>
        public void NotifyCorruptedFiles(int count)
        {
            if (!CanNotify(ref lastCorruptionNotification))
                return;
            
            var title = "Archivos Corruptos Detectados";
            var message = $"Se detectaron {count} archivo(s) corrupto(s).\n" +
                         $"Se reintentará la descarga automáticamente.";
            
            ShowNotification(title, message, ToolTipIcon.Warning);
        }
        
        /// <summary>
        /// Notifica una desconexión prolongada
        /// </summary>
        public void NotifyDisconnection(TimeSpan duration)
        {
            if (!CanNotify(ref lastDisconnectionNotification))
                return;
            
            var title = "Desconexión Detectada";
            var message = $"Desconectado durante {FormatDuration(duration)}.\n" +
                         $"Intentando reconectar...";
            
            ShowNotification(title, message, ToolTipIcon.Warning);
        }
        
        /// <summary>
        /// Notifica una reconexión exitosa
        /// </summary>
        public void NotifyReconnected()
        {
            var title = "Reconectado";
            var message = "Conexión restablecida exitosamente.";
            
            ShowNotification(title, message, ToolTipIcon.Info, 3000);
        }
        
        /// <summary>
        /// Notifica que una descarga importante ha completado
        /// </summary>
        public void NotifyDownloadCompleted(string fileName, long sizeBytes)
        {
            var title = "📥 Descarga Completada";
            var message = $"{fileName}\n" +
                         $"Tamaño: {FormatFileSize(sizeBytes)}";
            
            ShowNotification(title, message, ToolTipIcon.Info, 5000);
        }
        
        /// <summary>
        /// Notifica que se alcanzó un límite de rate
        /// </summary>
        public void NotifyRateLimitReached(int waitSeconds)
        {
            var title = "Límite de Búsquedas";
            var message = $"Esperando {waitSeconds}s para continuar.\n" +
                         $"Evitando sobrecarga del servidor.";
            
            ShowNotification(title, message, ToolTipIcon.Info, 3000);
        }
        
        /// <summary>
        /// Notifica un evento personalizado
        /// </summary>
        public void NotifyCustom(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int durationMs = 5000)
        {
            ShowNotification(title, message, icon, durationMs);
        }
        
        /// <summary>
        /// Muestra una notificación del sistema
        /// </summary>
        private void ShowNotification(string title, string message, ToolTipIcon icon, int durationMs = 5000)
        {
            if (isDisposed || notifyIcon == null)
                return;
            
            try
            {
                // Invocar en el thread de UI si es necesario
                if (parentForm != null && parentForm.InvokeRequired)
                {
                    parentForm.BeginInvoke(new Action(() =>
                    {
                        notifyIcon.ShowBalloonTip(durationMs, title, message, icon);
                    }));
                }
                else
                {
                    notifyIcon.ShowBalloonTip(durationMs, title, message, icon);
                }
            }
            catch
            {
                // Ignorar errores de notificación
            }
        }
        
        /// <summary>
        /// Verifica si se puede mostrar una notificación (cooldown)
        /// </summary>
        private bool CanNotify(ref DateTime lastNotification)
        {
            var now = DateTime.UtcNow;
            if (now - lastNotification < notificationCooldown)
                return false;
            
            lastNotification = now;
            return true;
        }
        
        /// <summary>
        /// Formatea una duración de tiempo de forma legible
        /// </summary>
        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 1)
                return $"{(int)duration.TotalDays}d {duration.Hours}h";
            if (duration.TotalHours >= 1)
                return $"{(int)duration.TotalHours}h {duration.Minutes}m";
            if (duration.TotalMinutes >= 1)
                return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
            return $"{(int)duration.TotalSeconds}s";
        }
        
        /// <summary>
        /// Formatea el tamaño de un archivo
        /// </summary>
        private string FormatFileSize(long bytes)
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
        
        public void Dispose()
        {
            if (isDisposed)
                return;
            
            isDisposed = true;
            
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
                notifyIcon = null;
            }
        }
    }
}
