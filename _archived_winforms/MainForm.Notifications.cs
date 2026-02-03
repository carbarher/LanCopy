using System;
using System.Drawing;
using System.Windows.Forms;
using SlskDown.Models;

namespace SlskDown
{
    /// <summary>
    /// Partial class de MainForm para sistema de notificaciones desktop
    /// </summary>
    public partial class MainForm
    {
        // ============================================================================
        // NOTIFICACIONES DESKTOP
        // ============================================================================
        
        private NotifyIcon notifyIcon;
        private bool notificationsEnabled = true;
        private bool notifyOnDownloadComplete = true;
        private bool notifyOnWishlistResult = true;
        private bool notifyOnLargeFile = true;
        private long largeFileThresholdBytes = 100 * 1024 * 1024; // 100 MB

        private void InitializeNotifications()
        {
            try
            {
                // Crear NotifyIcon
                notifyIcon = new NotifyIcon
                {
                    Icon = SystemIcons.Application,
                    Visible = true,
                    Text = "SlskDown - Cliente Soulseek"
                };

                // Menú contextual
                var contextMenu = new ContextMenuStrip();
                
                contextMenu.Items.Add("Mostrar", null, (s, e) => 
                {
                    Show();
                    WindowState = FormWindowState.Normal;
                    Activate();
                });
                
                contextMenu.Items.Add(new ToolStripSeparator());
                
                var notifItem = new ToolStripMenuItem("Notificaciones Habilitadas")
                {
                    Checked = notificationsEnabled,
                    CheckOnClick = true
                };
                notifItem.CheckedChanged += (s, e) => 
                {
                    notificationsEnabled = notifItem.Checked;
                    SaveConfig();
                };
                contextMenu.Items.Add(notifItem);
                
                contextMenu.Items.Add(new ToolStripSeparator());
                
                contextMenu.Items.Add("Salir", null, (s, e) => 
                {
                    notifyIcon.Visible = false;
                    Application.Exit();
                });

                notifyIcon.ContextMenuStrip = contextMenu;
                
                // Doble clic para mostrar ventana
                notifyIcon.DoubleClick += (s, e) =>
                {
                    Show();
                    WindowState = FormWindowState.Normal;
                    Activate();
                };

                // Cargar configuración
                LoadNotificationSettings();
                
                Log("✅ Sistema de notificaciones inicializado");
            }
            catch (Exception ex)
            {
                Log($"❌ Error inicializando notificaciones: {ex.Message}");
            }
        }

        private void LoadNotificationSettings()
        {
            try
            {
                if (configManager != null)
                {
                    notificationsEnabled = configManager.GetValue("notificationsEnabled", true);
                    notifyOnDownloadComplete = configManager.GetValue("notifyOnDownloadComplete", true);
                    notifyOnWishlistResult = configManager.GetValue("notifyOnWishlistResult", true);
                    notifyOnLargeFile = configManager.GetValue("notifyOnLargeFile", true);
                    largeFileThresholdBytes = configManager.GetValue("largeFileThresholdMB", 100L) * 1024 * 1024;
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error cargando configuración de notificaciones: {ex.Message}");
            }
        }

        private void SaveNotificationSettings()
        {
            try
            {
                if (configManager != null)
                {
                    configManager.SetValue("notificationsEnabled", notificationsEnabled);
                    configManager.SetValue("notifyOnDownloadComplete", notifyOnDownloadComplete);
                    configManager.SetValue("notifyOnWishlistResult", notifyOnWishlistResult);
                    configManager.SetValue("notifyOnLargeFile", notifyOnLargeFile);
                    configManager.SetValue("largeFileThresholdMB", largeFileThresholdBytes / (1024 * 1024));
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error guardando configuración de notificaciones: {ex.Message}");
            }
        }

        /// <summary>
        /// Muestra una notificación desktop
        /// </summary>
        private void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            try
            {
                if (!notificationsEnabled || notifyIcon == null)
                    return;

                if (InvokeRequired)
                {
                    Invoke(new Action(() => ShowNotification(title, message, icon)));
                    return;
                }

                notifyIcon.BalloonTipTitle = title;
                notifyIcon.BalloonTipText = message;
                notifyIcon.BalloonTipIcon = icon;
                notifyIcon.ShowBalloonTip(5000);
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error mostrando notificación: {ex.Message}");
            }
        }

        /// <summary>
        /// Notifica descarga completada
        /// </summary>
        public void NotifyDownloadComplete(string fileName, long bytes)
        {
            try
            {
                if (!notifyOnDownloadComplete)
                    return;

                var size = FormatFileSize(bytes);
                var isLarge = bytes >= largeFileThresholdBytes;

                if (isLarge && notifyOnLargeFile)
                {
                    ShowNotification(
                        "🎉 Descarga Grande Completada",
                        $"{fileName}\n{size}",
                        ToolTipIcon.Info
                    );
                }
                else
                {
                    ShowNotification(
                        "✅ Descarga Completada",
                        $"{fileName}\n{size}",
                        ToolTipIcon.Info
                    );
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error en notificación de descarga: {ex.Message}");
            }
        }

        /// <summary>
        /// Notifica nuevo resultado en wishlist
        /// </summary>
        public void NotifyWishlistResult(string searchTerm, string fileName)
        {
            try
            {
                if (!notifyOnWishlistResult)
                    return;

                ShowNotification(
                    "🔔 Nuevo en Wishlist",
                    $"'{searchTerm}'\n{fileName}",
                    ToolTipIcon.Info
                );
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error en notificación de wishlist: {ex.Message}");
            }
        }

        /// <summary>
        /// Notifica usuario de wishlist conectado
        /// </summary>
        public void NotifyUserOnline(string username)
        {
            try
            {
                if (!notificationsEnabled)
                    return;

                ShowNotification(
                    "🟢 Usuario Conectado",
                    $"{username} está ahora en línea",
                    ToolTipIcon.Info
                );
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error en notificación de usuario: {ex.Message}");
            }
        }

        /// <summary>
        /// Notifica error crítico
        /// </summary>
        public void NotifyError(string title, string message)
        {
            try
            {
                if (!notificationsEnabled)
                    return;

                ShowNotification(
                    $"❌ {title}",
                    message,
                    ToolTipIcon.Error
                );
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error en notificación de error: {ex.Message}");
            }
        }

        /// <summary>
        /// Notifica advertencia
        /// </summary>
        public void NotifyWarning(string title, string message)
        {
            try
            {
                if (!notificationsEnabled)
                    return;

                ShowNotification(
                    $"⚠️ {title}",
                    message,
                    ToolTipIcon.Warning
                );
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error en notificación de advertencia: {ex.Message}");
            }
        }

        /// <summary>
        /// Crea panel de configuración de notificaciones
        /// </summary>
        private Panel CreateNotificationSettingsPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 150,
                BackColor = Color.FromArgb(35, 35, 35),
                Padding = new Padding(10)
            };

            var lblTitle = new Label
            {
                Text = "Notificaciones Desktop",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Location = new Point(10, 10),
                AutoSize = true
            };

            var chkEnabled = new CheckBox
            {
                Text = "Habilitar notificaciones",
                ForeColor = Color.White,
                Location = new Point(10, 40),
                AutoSize = true,
                Checked = notificationsEnabled
            };
            chkEnabled.CheckedChanged += (s, e) =>
            {
                notificationsEnabled = chkEnabled.Checked;
                SaveNotificationSettings();
            };

            var chkDownloads = new CheckBox
            {
                Text = "Notificar descargas completadas",
                ForeColor = Color.White,
                Location = new Point(10, 65),
                AutoSize = true,
                Checked = notifyOnDownloadComplete
            };
            chkDownloads.CheckedChanged += (s, e) =>
            {
                notifyOnDownloadComplete = chkDownloads.Checked;
                SaveNotificationSettings();
            };

            var chkWishlist = new CheckBox
            {
                Text = "Notificar nuevos resultados en wishlist",
                ForeColor = Color.White,
                Location = new Point(10, 90),
                AutoSize = true,
                Checked = notifyOnWishlistResult
            };
            chkWishlist.CheckedChanged += (s, e) =>
            {
                notifyOnWishlistResult = chkWishlist.Checked;
                SaveNotificationSettings();
            };

            var chkLargeFiles = new CheckBox
            {
                Text = "Notificar archivos grandes (>100MB)",
                ForeColor = Color.White,
                Location = new Point(10, 115),
                AutoSize = true,
                Checked = notifyOnLargeFile
            };
            chkLargeFiles.CheckedChanged += (s, e) =>
            {
                notifyOnLargeFile = chkLargeFiles.Checked;
                SaveNotificationSettings();
            };

            panel.Controls.AddRange(new Control[] 
            { 
                lblTitle, 
                chkEnabled, 
                chkDownloads, 
                chkWishlist, 
                chkLargeFiles 
            });

            return panel;
        }

        /// <summary>
        /// Limpia recursos de notificaciones
        /// </summary>
        private void CleanupNotifications()
        {
            try
            {
                if (notifyIcon != null)
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                    notifyIcon = null;
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error limpiando notificaciones: {ex.Message}");
            }
        }
    }
}
