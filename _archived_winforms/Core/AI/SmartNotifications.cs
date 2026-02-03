using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core.AI
{
    public enum NotificationPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class Notification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; }
        public string Message { get; set; }
        public NotificationPriority Priority { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool Read { get; set; } = false;
        public string Category { get; set; }
    }

    /// <summary>
    /// Sistema de notificaciones inteligentes
    /// </summary>
    public class SmartNotifications
    {
        private List<Notification> notifications = new List<Notification>();
        private Dictionary<string, DateTime> lastNotificationTime = new Dictionary<string, DateTime>();

        public void Notify(string title, string message, NotificationPriority priority = NotificationPriority.Medium, string category = "general")
        {
            // Evitar spam de notificaciones similares
            var key = $"{category}_{title}";
            if (lastNotificationTime.TryGetValue(key, out var lastTime))
            {
                if ((DateTime.Now - lastTime).TotalMinutes < 5)
                    return; // No notificar si fue hace menos de 5 minutos
            }

            var notification = new Notification
            {
                Title = title,
                Message = message,
                Priority = priority,
                Category = category
            };

            notifications.Add(notification);
            lastNotificationTime[key] = DateTime.Now;

            // Mantener solo últimas 100 notificaciones
            if (notifications.Count > 100)
            {
                notifications.RemoveAt(0);
            }
        }

        public void NotifyDownloadComplete(string filename, string author)
        {
            Notify(
                "✅ Descarga Completada",
                $"{filename}\nAutor: {author}",
                NotificationPriority.Medium,
                "download"
            );
        }

        public void NotifySeriesComplete(string seriesName, int totalBooks)
        {
            Notify(
                "🎉 Serie Completa",
                $"¡Has completado la serie '{seriesName}'!\nTotal: {totalBooks} libros",
                NotificationPriority.High,
                "series"
            );
        }

        public void NotifyQueueEmpty()
        {
            Notify(
                "📭 Cola Vacía",
                "Todas las descargas han terminado",
                NotificationPriority.Low,
                "queue"
            );
        }

        public void NotifyError(string errorMessage)
        {
            Notify(
                "❌ Error",
                errorMessage,
                NotificationPriority.High,
                "error"
            );
        }

        public void NotifyNewRecommendation(string recommendation)
        {
            Notify(
                "💡 Nueva Recomendación",
                recommendation,
                NotificationPriority.Low,
                "recommendation"
            );
        }

        public List<Notification> GetUnreadNotifications()
        {
            return notifications.Where(n => !n.Read)
                .OrderByDescending(n => n.Priority)
                .ThenByDescending(n => n.Timestamp)
                .ToList();
        }

        public List<Notification> GetNotificationsByCategory(string category)
        {
            return notifications.Where(n => n.Category == category)
                .OrderByDescending(n => n.Timestamp)
                .ToList();
        }

        public void MarkAsRead(string notificationId)
        {
            var notification = notifications.FirstOrDefault(n => n.Id == notificationId);
            if (notification != null)
                notification.Read = true;
        }

        public void MarkAllAsRead()
        {
            foreach (var notification in notifications)
            {
                notification.Read = true;
            }
        }

        public string GenerateNotificationSummary()
        {
            var unread = GetUnreadNotifications();
            
            if (unread.Count == 0)
                return "📬 No tienes notificaciones nuevas";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"🔔 NOTIFICACIONES ({unread.Count} nuevas)\n");

            var byPriority = unread.GroupBy(n => n.Priority)
                .OrderByDescending(g => g.Key);

            foreach (var group in byPriority)
            {
                var icon = group.Key switch
                {
                    NotificationPriority.Critical => "🚨",
                    NotificationPriority.High => "⚠️",
                    NotificationPriority.Medium => "ℹ️",
                    _ => "📌"
                };

                sb.AppendLine($"{icon} {group.Key.ToString().ToUpper()}:");
                foreach (var notification in group.Take(5))
                {
                    var timeAgo = FormatTimeAgo(notification.Timestamp);
                    sb.AppendLine($"  • {notification.Title} ({timeAgo})");
                }
                if (group.Count() > 5)
                    sb.AppendLine($"  ... y {group.Count() - 5} más");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string FormatTimeAgo(DateTime timestamp)
        {
            var span = DateTime.Now - timestamp;
            
            if (span.TotalMinutes < 1)
                return "ahora";
            if (span.TotalMinutes < 60)
                return $"hace {(int)span.TotalMinutes}m";
            if (span.TotalHours < 24)
                return $"hace {(int)span.TotalHours}h";
            return $"hace {(int)span.TotalDays}d";
        }

        public void ClearOldNotifications(int daysToKeep = 7)
        {
            var cutoff = DateTime.Now.AddDays(-daysToKeep);
            notifications.RemoveAll(n => n.Timestamp < cutoff && n.Read);
        }

        public Dictionary<string, int> GetNotificationStats()
        {
            return new Dictionary<string, int>
            {
                ["total"] = notifications.Count,
                ["unread"] = notifications.Count(n => !n.Read),
                ["high_priority"] = notifications.Count(n => n.Priority >= NotificationPriority.High),
                ["today"] = notifications.Count(n => n.Timestamp.Date == DateTime.Today)
            };
        }
    }
}
