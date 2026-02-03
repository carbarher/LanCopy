using System;
using System.Windows.Forms;
using System.Drawing;

namespace SlskDown
{
    public class ToastNotifications
    {
        private static Action<string> logAction;
        
        public static void Initialize(Action<string> logger)
        {
            logAction = logger;
        }
        
        public static void ShowDownloadComplete(string filename, string path)
        {
            try
            {
                // Usar NotifyIcon de Windows Forms como alternativa a UWP
                var notifyIcon = new NotifyIcon
                {
                    Icon = SystemIcons.Information,
                    Visible = true,
                    BalloonTipTitle = "Descarga Completada",
                    BalloonTipText = $"{filename}\n\nClick para abrir carpeta",
                    BalloonTipIcon = ToolTipIcon.Info
                };
                
                notifyIcon.BalloonTipClicked += (s, e) =>
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                    }
                    catch { }
                    
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                };
                
                notifyIcon.BalloonTipClosed += (s, e) =>
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                };
                
                notifyIcon.ShowBalloonTip(5000);
                
                logAction?.Invoke($"🔔 Notificación mostrada: {filename}");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error mostrando notificación: {ex.Message}");
            }
        }
        
        public static void ShowSearchComplete(int resultsCount, string query)
        {
            try
            {
                var notifyIcon = new NotifyIcon
                {
                    Icon = SystemIcons.Information,
                    Visible = true,
                    BalloonTipTitle = "Búsqueda Completada",
                    BalloonTipText = $"Encontrados {resultsCount} resultados para:\n{query}",
                    BalloonTipIcon = ToolTipIcon.Info
                };
                
                notifyIcon.BalloonTipClosed += (s, e) =>
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                };
                
                notifyIcon.ShowBalloonTip(3000);
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error mostrando notificación: {ex.Message}");
            }
        }
        
        public static void ShowError(string title, string message)
        {
            try
            {
                var notifyIcon = new NotifyIcon
                {
                    Icon = SystemIcons.Error,
                    Visible = true,
                    BalloonTipTitle = title,
                    BalloonTipText = message,
                    BalloonTipIcon = ToolTipIcon.Error
                };
                
                notifyIcon.BalloonTipClosed += (s, e) =>
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                };
                
                notifyIcon.ShowBalloonTip(5000);
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error mostrando notificación de error: {ex.Message}");
            }
        }
        
        public static void ShowWarning(string title, string message)
        {
            try
            {
                var notifyIcon = new NotifyIcon
                {
                    Icon = SystemIcons.Warning,
                    Visible = true,
                    BalloonTipTitle = title,
                    BalloonTipText = message,
                    BalloonTipIcon = ToolTipIcon.Warning
                };
                
                notifyIcon.BalloonTipClosed += (s, e) =>
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                };
                
                notifyIcon.ShowBalloonTip(4000);
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error mostrando notificación de advertencia: {ex.Message}");
            }
        }
    }
}
