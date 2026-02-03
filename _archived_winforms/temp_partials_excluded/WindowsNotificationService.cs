using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;

namespace SlskDown
{
    /// <summary>
    /// Sistema de notificaciones nativas de Windows
    /// </summary>
    public partial class MainForm
    {
        private NotifyIcon trayIcon = null!;
        private bool notificationsEnabled = true;
        
        /// <summary>
        /// Inicializar sistema de notificaciones Windows
        /// </summary>
        private void InitializeWindowsNotifications()
        {
            try
            {
                Console.WriteLine("[Notifications] ðŸ”” Inicializando sistema de notificaciones Windows");
                
                // Crear icono en bandeja del sistema
                trayIcon = new NotifyIcon()
                {
                    Icon = SystemIcons.Application,
                    Text = "SlskDown - BÃºsqueda Ultra-RÃ¡pida",
                    Visible = true
                };
                
                // MenÃº contextual del icono
                var contextMenu = new ContextMenuStrip();
                
                var showItem = new ToolStripItem("ðŸ“‹ Mostrar SlskDown", null, (s, e) => 
                {
                    this.Show();
                    this.WindowState = FormWindowState.Normal;
                    this.BringToFront();
                });
                
                var notificationsItem = new ToolStripItem(notificationsEnabled ? "ðŸ”” Silenciar Notificaciones" : "ðŸ”• Activar Notificaciones", null, (s, e) => 
                {
                    ToggleNotifications();
                });
                
                var exitItem = new ToolStripItem("âŒ Salir", null, (s, e) => 
                {
                    Application.Exit();
                });
                
                contextMenu.Items.AddRange(new ToolStripItem[] { showItem, notificationsItem, new ToolStripSeparator(), exitItem });
                trayIcon.ContextMenuStrip = contextMenu;
                
                // Doble clic para mostrar ventana
                trayIcon.DoubleClick += (s, e) => 
                {
                    this.Show();
                    this.WindowState = FormWindowState.Normal;
                    this.BringToFront();
                };
                
                // NotificaciÃ³n de inicio
                ShowNotification("ðŸš€ SlskDown Iniciado", "Sistema ultra-rÃ¡pido listo para usar", NotificationType.Info);
                
                Console.WriteLine("[Notifications] âœ… Sistema de notificaciones inicializado");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Notifications] âŒ Error inicializando notificaciones: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Tipos de notificaciÃ³n
        /// </summary>
        public enum NotificationType
        {
            Info,
            Success,
            Warning,
            Error
        }
        
        /// <summary>
        /// Mostrar notificaciÃ³n nativa de Windows
        /// </summary>
        private void ShowNotification(string title, string message, NotificationType type = NotificationType.Info, int durationMs = 5000)
        {
            try
            {
                if (!notificationsEnabled) return;
                
                // Usar notificaciÃ³n del sistema (Windows 10/11)
                if (Environment.OSVersion.Version >= new Version(10, 0))
                {
                    ShowModernNotification(title, message, type, durationMs);
                }
                else
                {
                    // Fallback para versiones antiguas
                    trayIcon.ShowBalloonTip(durationMs / 1000, title, message, GetToolTipIcon(type));
                }
                
                // TambiÃ©n mostrar en el log
                string logMessage = $"ðŸ”” {title}: {message}";
                AddColoredLogMessage(logMessage, GetLogMessageType(type));
                
                Console.WriteLine($"[Notifications] ðŸ”” NotificaciÃ³n mostrada: {title}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Notifications] âŒ Error mostrando notificaciÃ³n: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Mostrar notificaciÃ³n moderna (Windows 10/11)
        /// </summary>
        private void ShowModernNotification(string title, string message, NotificationType type, int durationMs)
        {
            try
            {
                // Para Windows 10/11 usar Toast notifications
                var toastTitle = title;
                var toastMessage = message;
                
                // Simular notificaciÃ³n moderna con NotifyIcon mejorado
                trayIcon.BalloonTipTitle = $"ðŸ”” {toastTitle}";
                trayIcon.BalloonTipText = toastMessage;
                trayIcon.BalloonTipIcon = GetToolTipIcon(type);
                
                // Personalizar duraciÃ³n (mÃ¡ximo 30 segundos en Windows)
                var durationSeconds = Math.Min(30, Math.Max(1, durationMs / 1000));
                trayIcon.ShowBalloonTip(durationSeconds);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Notifications] âŒ Error en notificaciÃ³n moderna: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtener icono para ToolTip segÃºn tipo
        /// </summary>
        private ToolTipIcon GetToolTipIcon(NotificationType type)
        {
            return type switch
            {
                NotificationType.Success => ToolTipIcon.Info,
                NotificationType.Warning => ToolTipIcon.Warning,
                NotificationType.Error => ToolTipIcon.Error,
                _ => ToolTipIcon.Info
            };
        }
        
        /// <summary>
        /// Obtener tipo de mensaje para log segÃºn tipo de notificaciÃ³n
        /// </summary>
        private LogMessageType GetLogMessageType(NotificationType type)
        {
            return type switch
            {
                NotificationType.Success => LogMessageType.Success,
                NotificationType.Warning => LogMessageType.Warning,
                NotificationType.Error => LogMessageType.Error,
                _ => LogMessageType.Info
            };
        }
        
        /// <summary>
        /// Alternar estado de notificaciones
        /// </summary>
        private void ToggleNotifications()
        {
            try
            {
                notificationsEnabled = !notificationsEnabled;
                
                var status = notificationsEnabled ? "activadas" : "silenciadas";
                ShowNotification("ðŸ”” Notificaciones", $"Notificaciones {status}", NotificationType.Info);
                
                // Actualizar menÃº contextual
                if (trayIcon?.ContextMenuStrip?.Items.Count > 1)
                {
                    trayIcon.ContextMenuStrip.Items[1].Text = notificationsEnabled ? "ðŸ”” Silenciar Notificaciones" : "ðŸ”• Activar Notificaciones";
                }
                
                Console.WriteLine($"[Notifications] ðŸ”” Notificaciones {status}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Notifications] âŒ Error alternando notificaciones: {ex.Message}");
            }
        }
        
        /// <summary>
        /// NotificaciÃ³n de bÃºsqueda completada
        /// </summary>
        private void NotifySearchCompleted(int authorsProcessed, int filesFound, TimeSpan elapsedTime)
        {
            try
            {
                var title = "ðŸ† BÃºsqueda Ultra-RÃ¡pida Completada";
                var message = $"ðŸ“š {authorsProcessed} autores procesados\nðŸ“ {filesFound:N0} archivos encontrados\nâ±ï¸ {elapsedTime:mm\\:ss} tiempo total";
                
                ShowNotification(title, message, NotificationType.Success, 8000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Notifications] âŒ Error notificando bÃºsqueda: {ex.Message}");
            }
        }
        
        /// <summary>
        /// NotificaciÃ³n de autor eliminado
        /// </summary>
        private void NotifyAuthorRemoved(string authorName, int failedAttempts)
        {
            try
            {
                var title = "ðŸ§¹ Autor Eliminado";
                var message = $"ðŸ“š {authorName}\nâŒ Sin resultados por {failedAttempts} intentos consecutivos";
                
                ShowNotification(title, message, NotificationType.Warning, 6000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Notifications] âŒ Error notificando eliminaciÃ³n: {ex.Message}");
            }
        }
        
        /// <summary>
        /// NotificaciÃ³n de conexiÃ³n/desconexiÃ³n
        /// </summary>
        private void NotifyConnectionStatus(bool connected)
        {
            try
            {
                var title = connected ? "ðŸŒ Conectado a Soulseek" : "ðŸ“µ Desconectado de Soulseek";
                var message = connected ? "âœ… Lista para bÃºsquedas ultra-rÃ¡pidas" : "âš ï¸ ReconexiÃ³n automÃ¡tica en progreso";
                var type = connected ? NotificationType.Success : NotificationType.Warning;
                
                ShowNotification(title, message, type, 5000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Notifications] âŒ Error notificando conexiÃ³n: {ex.Message}");
            }
        }
        
        /// <summary>
        /// NotificaciÃ³n de hito personal
        /// </summary>
        private void NotifyMilestone(string milestone, string details)
        {
            try
            {
                var title = "ðŸŽ¯ Â¡Nuevo Hito Personal!";
                var message = $"ðŸ† {milestone}\nðŸ“Š {details}";
                
                ShowNotification(title, message, NotificationType.Success, 7000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Notifications] âŒ Error notificando hito: {ex.Message}");
            }
        }
        
        /// <summary>
        /// NotificaciÃ³n de descarga completada
        /// </summary>
        private void NotifyDownloadCompleted(string fileName, long fileSize)
        {
            try
            {
                var title = "ðŸ“ Descarga Completada";
                var message = $"ðŸ“„ {fileName}\nðŸ’¾ {FormatBytes(fileSize)}";
                
                ShowNotification(title, message, NotificationType.Success, 4000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Notifications] âŒ Error notificando descarga: {ex.Message}");
            }
        }
        
        /// <summary>
        /// NotificaciÃ³n de error crÃ­tico
        /// </summary>
        private void NotifyError(string errorType, string details)
        {
            try
            {
                var title = $"âŒ Error: {errorType}";
                var message = details;
                
                ShowNotification(title, message, NotificationType.Error, 10000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Notifications] âŒ Error notificando error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Formatear bytes para legibilidad
        /// </summary>
        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
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
        /// Limpiar recursos de notificaciones
        /// </summary>
        private void CleanupNotifications()
        {
            try
            {
                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                    trayIcon = null;
                }
                
                Console.WriteLine("[Notifications] ðŸ§¹ Recursos de notificaciones limpiados");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Notifications] âŒ Error limpiando notificaciones: {ex.Message}");
            }
        }
        
        /// <summary>
        /// API pÃºblica para mostrar notificaciones desde otras partes del cÃ³digo
        /// </summary>
        public void ShowWindowsNotification(string title, string message, NotificationType type = NotificationType.Info)
        {
            ShowNotification(title, message, type);
        }
    }
}

