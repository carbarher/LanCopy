using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SlskDown
{
    /// <summary>
    /// Sistema de notificaciones inteligentes para SlskDown
    /// </summary>
    public partial class MainForm
    {
        private System.Windows.Forms.Timer? notificationTimer;
        private List<NotificationRule> notificationRules = new();
        private Queue<NotificationMessage> notificationQueue = new();
        
        [StructLayout(LayoutKind.Sequential)]
        public struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }
        
        [DllImport("shell32.dll")]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA pnid);
        
        private const int NIM_ADD = 0x00000000;
        private const int NIM_DELETE = 0x00000002;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIF_INFO = 0x00000010;
        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;
        
        public struct NotificationRule
        {
            public string Type { get; set; }
            public bool Enabled { get; set; }
            public string Condition { get; set; }
            public string Message { get; set; }
            public NotificationUrgency Urgency { get; set; }
        }
        
        public enum NotificationUrgency
        {
            Low,
            Medium,
            High,
            Critical
        }
        
        public struct NotificationMessage
        {
            public string Title { get; set; }
            public string Body { get; set; }
            public NotificationUrgency Urgency { get; set; }
            public DateTime Timestamp { get; set; }
            public Action? OnClick { get; set; }
        }
        
        /// <summary>
        /// Inicializar sistema de notificaciones inteligentes
        /// </summary>
        private void InitializeSmartNotifications()
        {
            LoadNotificationRules();
            
            notificationTimer = new System.Windows.Forms.Timer();
            notificationTimer.Interval = 5000; // Revisar cada 5 segundos
            notificationTimer.Tick += ProcessNotificationQueue;
            notificationTimer.Start();
            
            Console.WriteLine("[Notifications] ðŸ”” Sistema de notificaciones inteligentes iniciado");
        }
        
        /// <summary>
        /// Cargar reglas de notificaciÃ³n
        /// </summary>
        private void LoadNotificationRules()
        {
            notificationRules = new List<NotificationRule>
            {
                new NotificationRule
                {
                    Type = "DownloadComplete",
                    Enabled = true,
                    Condition = "download_count > 0",
                    Message = "ðŸŽµ {count} descargas completadas",
                    Urgency = NotificationUrgency.Medium
                },
                new NotificationRule
                {
                    Type = "SearchComplete",
                    Enabled = true,
                    Condition = "result_count > 100",
                    Message = "ðŸ” BÃºsqueda completa: {count} resultados encontrados",
                    Urgency = NotificationUrgency.Low
                },
                new NotificationRule
                {
                    Type = "ConnectionLost",
                    Enabled = true,
                    Condition = "connection_status == 'disconnected'",
                    Message = "âš ï¸ ConexiÃ³n perdida - reconectando automÃ¡ticamente",
                    Urgency = NotificationUrgency.High
                },
                new NotificationRule
                {
                    Type = "MemoryHigh",
                    Enabled = true,
                    Condition = "memory_usage > 80",
                    Message = "ðŸ§  Uso de memoria elevado: {usage}%",
                    Urgency = NotificationUrgency.Medium
                },
                new NotificationRule
                {
                    Type = "NewArtistFound",
                    Enabled = true,
                    Condition = "new_artists > 0",
                    Message = "ðŸŽ¤ {count} nuevos artistas encontrados",
                    Urgency = NotificationUrgency.Low
                }
            };
            
            Console.WriteLine($"[Notifications] ðŸ“‹ Cargadas {notificationRules.Count} reglas de notificaciÃ³n");
        }
        
        /// <summary>
        /// Agregar notificaciÃ³n a la cola
        /// </summary>
        private void QueueNotification(string title, string body, NotificationUrgency urgency = NotificationUrgency.Medium, Action? onClick = null)
        {
            var notification = new NotificationMessage
            {
                Title = title,
                Body = body,
                Urgency = urgency,
                Timestamp = DateTime.Now,
                OnClick = onClick
            };
            
            notificationQueue.Enqueue(notification);
            Console.WriteLine($"[Notifications] ðŸ“¨ NotificaciÃ³n encolada: {title}");
        }
        
        /// <summary>
        /// Procesar cola de notificaciones
        /// </summary>
        private void ProcessNotificationQueue(object? sender, EventArgs e)
        {
            while (notificationQueue.Count > 0)
            {
                var notification = notificationQueue.Dequeue();
                ShowNotification(notification);
            }
        }
        
        /// <summary>
        /// Mostrar notificaciÃ³n del sistema
        /// </summary>
        private void ShowNotification(NotificationMessage notification)
        {
            try
            {
                // Usar notificaciÃ³n del sistema o toast personalizado
                if (Environment.OSVersion.Version.Major >= 10) // Windows 10+
                {
                    ShowWindows10Notification(notification);
                }
                else
                {
                    ShowLegacyNotification(notification);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Notifications] âŒ Error mostrando notificaciÃ³n: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Mostrar notificaciÃ³n en Windows 10+
        /// </summary>
        private void ShowWindows10Notification(NotificationMessage notification)
        {
            // Implementar notificaciones modernas de Windows 10
            // Por ahora, usar MessageBox simple
            if (notification.Urgency >= NotificationUrgency.High)
            {
                MessageBox.Show(notification.Body, notification.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        
        /// <summary>
        /// Mostrar notificaciÃ³n legada
        /// </summary>
        private void ShowLegacyNotification(NotificationMessage notification)
        {
            if (notification.Urgency >= NotificationUrgency.High)
            {
                MessageBox.Show(notification.Body, notification.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        
        /// <summary>
        /// Evaluar reglas de notificaciÃ³n y activar las correspondientes
        /// </summary>
        private void EvaluateNotificationRules()
        {
            try
            {
                var currentContext = GetCurrentNotificationContext();
                
                foreach (var rule in notificationRules.Where(r => r.Enabled))
                {
                    if (EvaluateCondition(rule.Condition, currentContext))
                    {
                        var message = ProcessMessageTemplate(rule.Message, currentContext);
                        QueueNotification("SlskDown", message, rule.Urgency);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Notifications] âŒ Error evaluando reglas: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtener contexto actual para evaluaciÃ³n de reglas
        /// </summary>
        private Dictionary<string, object> GetCurrentNotificationContext()
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var memoryUsage = (process.WorkingSet64 / 1024 / 1024 * 100) / (System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 1024 / 1024);
            
            return new Dictionary<string, object>
            {
                ["download_count"] = GetCompletedDownloadsCount(),
                ["result_count"] = resultsListView.Items.Count,
                ["connection_status"] = client?.State.ToString().ToLower() ?? "disconnected",
                ["memory_usage"] = Math.Min(100, memoryUsage),
                ["new_artists"] = GetNewArtistsCount()
            };
        }
        
        /// <summary>
        /// Evaluar condiciÃ³n de notificaciÃ³n
        /// </summary>
        private bool EvaluateCondition(string condition, Dictionary<string, object> context)
        {
            try
            {
                // EvaluaciÃ³n simple de condiciones
                if (condition.Contains("download_count > 0"))
                    return (int)context["download_count"] > 0;
                    
                if (condition.Contains("result_count > 100"))
                    return (int)context["result_count"] > 100;
                    
                if (condition.Contains("connection_status == 'disconnected'"))
                    return context["connection_status"].ToString() == "disconnected";
                    
                if (condition.Contains("memory_usage > 80"))
                    return (int)context["memory_usage"] > 80;
                    
                if (condition.Contains("new_artists > 0"))
                    return (int)context["new_artists"] > 0;
                    
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Procesar template de mensaje con variables
        /// </summary>
        private string ProcessMessageTemplate(string template, Dictionary<string, object> context)
        {
            var result = template;
            
            result = result.Replace("{count}", context["download_count"].ToString());
            result = result.Replace("{results}", context["result_count"].ToString());
            result = result.Replace("{usage}", context["memory_usage"].ToString());
            result = result.Replace("{artists}", context["new_artists"].ToString());
            
            return result;
        }
        
        private int GetCompletedDownloadsCount()
        {
            return downloadsListView.Items.Cast<ListViewItem>()
                .Count(item => item.SubItems.Count > 2 && 
                              item.SubItems[2].Text.Contains("100%"));
        }
        
        private int GetNewArtistsCount()
        {
            // Implementar lÃ³gica real
            return new Random().Next(0, 5);
        }
        
        /// <summary>
        /// Configurar notificaciones personalizadas
        /// </summary>
        private void ConfigureNotifications()
        {
            // Implementar UI de configuraciÃ³n de notificaciones
            Console.WriteLine("[Notifications] âš™ï¸ ConfiguraciÃ³n de notificaciones");
        }
    }
}

