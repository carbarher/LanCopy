using System;
using SlskDown.Models;

namespace SlskDown.UI
{
    /// <summary>
    /// Helper para generar mensajes de estado descriptivos inspirados en Nicotine+
    /// Estados claros y accionables para mejor UX
    /// </summary>
    public static class TransferStatusHelper
    {
        /// <summary>
        /// Obtiene un mensaje de estado amigable para el usuario
        /// </summary>
        public static string GetUserFriendlyStatus(DownloadTask task)
        {
            if (task == null)
                return "Desconocido";

            return task.Status switch
            {
                TransferStatus.Queued => GetQueuedStatus(task),
                TransferStatus.WaitingForSlot => $"Esperando slot disponible (posición {task.QueuePosition ?? 0})",
                TransferStatus.GettingUserStatus => "Consultando estado del usuario...",
                TransferStatus.EstablishingConnection => "Estableciendo conexión...",
                TransferStatus.Negotiating => "Negociando transferencia...",
                TransferStatus.Transferring => GetTransferringStatus(task),
                TransferStatus.Paused => "Pausado por el usuario",
                TransferStatus.Finished => "Completado ✓",
                TransferStatus.Filtered => "Filtrado (no cumple criterios)",
                TransferStatus.ConnectionTimeout => GetTimeoutStatus(task),
                TransferStatus.UserLoggedOff => "Usuario desconectado (se reintentará)",
                TransferStatus.UserBusy => GetUserBusyStatus(task),
                TransferStatus.FileNotShared => "Archivo ya no está compartido",
                TransferStatus.FileNotAvailable => "Archivo no disponible",
                TransferStatus.QueueFull => GetQueueFullStatus(task),
                TransferStatus.Banned => "Usuario te ha bloqueado",
                TransferStatus.Cancelled => "Cancelado por el usuario",
                TransferStatus.Aborted => "Abortado",
                TransferStatus.RetryScheduled => GetRetryScheduledStatus(task),
                TransferStatus.SearchingAlternative => GetSearchingAlternativeStatus(task),
                TransferStatus.NetworkError => GetNetworkErrorStatus(task),
                TransferStatus.DiskFull => "Disco lleno - Libera espacio",
                TransferStatus.PermissionDenied => "Permiso denegado - Verifica permisos",
                TransferStatus.FileCorrupted => "Archivo corrupto - Reintentando",
                _ => task.Status.ToString()
            };
        }

        private static string GetQueuedStatus(DownloadTask task)
        {
            if (task.QueuePosition.HasValue && task.QueuePosition.Value > 0)
                return $"En cola (posición {task.QueuePosition.Value})";
            
            return "En cola";
        }

        private static string GetTransferringStatus(DownloadTask task)
        {
            var speed = FormatSpeed(task.Speed);
            var progress = task.Progress;
            var eta = task.EstimatedTimeRemaining;

            if (eta.HasValue && eta.Value.TotalSeconds > 0)
                return $"Descargando ({progress:F1}%, {speed}, quedan {FormatTimeSpan(eta.Value)})";
            
            return $"Descargando ({progress:F1}%, {speed})";
        }

        private static string GetTimeoutStatus(DownloadTask task)
        {
            if (task.RetryAt.HasValue)
            {
                var retryIn = task.RetryAt.Value - DateTime.UtcNow;
                if (retryIn.TotalSeconds > 0)
                    return $"Timeout de conexión (reintento en {FormatTimeSpan(retryIn)})";
            }

            return "Timeout de conexión (reintentando...)";
        }

        private static string GetUserBusyStatus(DownloadTask task)
        {
            if (task.RetryAt.HasValue)
            {
                var retryIn = task.RetryAt.Value - DateTime.UtcNow;
                if (retryIn.TotalSeconds > 0)
                    return $"Usuario ocupado (reintento en {FormatTimeSpan(retryIn)})";
            }

            return "Usuario ocupado (reintentando...)";
        }

        private static string GetQueueFullStatus(DownloadTask task)
        {
            if (task.RetryAt.HasValue)
            {
                var retryIn = task.RetryAt.Value - DateTime.UtcNow;
                if (retryIn.TotalSeconds > 0)
                    return $"Cola llena (reintento en {FormatTimeSpan(retryIn)})";
            }

            return "Cola del usuario llena (reintentando...)";
        }

        private static string GetRetryScheduledStatus(DownloadTask task)
        {
            if (task.RetryAt.HasValue)
            {
                var retryIn = task.RetryAt.Value - DateTime.UtcNow;
                if (retryIn.TotalSeconds > 0)
                    return $"Reintentando en {FormatTimeSpan(retryIn)} (intento {task.RetryCount + 1})";
            }

            return $"Reintentando... (intento {task.RetryCount + 1})";
        }

        private static string GetSearchingAlternativeStatus(DownloadTask task)
        {
            var attempts = task.AlternativeAttempts ?? 0;
            var maxAttempts = 3; // Configurable
            return $"Buscando proveedor alternativo ({attempts}/{maxAttempts})";
        }

        private static string GetNetworkErrorStatus(DownloadTask task)
        {
            if (task.RetryAt.HasValue)
            {
                var retryIn = task.RetryAt.Value - DateTime.UtcNow;
                if (retryIn.TotalSeconds > 0)
                    return $"Error de red (reintento en {FormatTimeSpan(retryIn)})";
            }

            return "Error de red (reintentando...)";
        }

        /// <summary>
        /// Genera un tooltip detallado para una transferencia
        /// </summary>
        public static string GenerateTransferTooltip(DownloadTask task)
        {
            if (task == null)
                return "";

            var lines = new System.Collections.Generic.List<string>
            {
                $"Archivo: {task.FileName}",
                $"Usuario: {task.Username}",
                $"Red: {task.Network ?? "Soulseek"}"
            };

            // Progreso
            if (task.FileSize > 0)
            {
                var downloaded = FormatFileSize(task.CurrentByteOffset ?? 0);
                var total = FormatFileSize(task.FileSize);
                lines.Add($"Progreso: {task.Progress:F1}% ({downloaded}/{total})");
            }

            // Velocidad
            if (task.Speed > 0)
            {
                lines.Add($"Velocidad: {FormatSpeed(task.Speed)}");
            }

            // Tiempo restante
            if (task.EstimatedTimeRemaining.HasValue && task.EstimatedTimeRemaining.Value.TotalSeconds > 0)
            {
                lines.Add($"Tiempo restante: {FormatTimeSpan(task.EstimatedTimeRemaining.Value)}");
            }

            // Reintentos
            if (task.RetryCount > 0)
            {
                lines.Add($"Reintentos: {task.RetryCount}");
            }

            // Próximo reintento
            if (task.RetryAt.HasValue)
            {
                var retryIn = task.RetryAt.Value - DateTime.UtcNow;
                if (retryIn.TotalSeconds > 0)
                {
                    lines.Add($"Próximo reintento: {FormatTimeSpan(retryIn)}");
                }
            }

            // Error
            if (!string.IsNullOrEmpty(task.ErrorMessage))
            {
                lines.Add($"Último error: {task.ErrorMessage}");
            }

            // Fechas
            if (task.StartedAt.HasValue)
            {
                lines.Add($"Iniciado: {task.StartedAt.Value:g}");
            }

            if (task.CompletedAt.HasValue)
            {
                lines.Add($"Completado: {task.CompletedAt.Value:g}");
            }

            // Ruta
            if (!string.IsNullOrEmpty(task.FilePath))
            {
                lines.Add($"Ruta: {task.FilePath}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Formatea velocidad en formato legible
        /// </summary>
        private static string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond <= 0)
                return "0 KB/s";

            const double KB = 1024;
            const double MB = KB * 1024;

            if (bytesPerSecond >= MB)
                return $"{bytesPerSecond / MB:F2} MB/s";
            
            return $"{bytesPerSecond / KB:F2} KB/s";
        }

        /// <summary>
        /// Formatea tamaño de archivo en formato legible
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            if (bytes <= 0)
                return "0 bytes";

            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            if (bytes >= GB)
                return $"{(double)bytes / GB:F2} GB";
            
            if (bytes >= MB)
                return $"{(double)bytes / MB:F2} MB";
            
            if (bytes >= KB)
                return $"{(double)bytes / KB:F2} KB";
            
            return $"{bytes} bytes";
        }

        /// <summary>
        /// Formatea TimeSpan en formato legible
        /// </summary>
        private static string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalSeconds < 60)
                return $"{timeSpan.TotalSeconds:F0}s";

            if (timeSpan.TotalMinutes < 60)
                return $"{timeSpan.TotalMinutes:F0}m";

            if (timeSpan.TotalHours < 24)
                return $"{timeSpan.TotalHours:F1}h";

            return $"{timeSpan.TotalDays:F1}d";
        }

        /// <summary>
        /// Obtiene un color para el estado
        /// </summary>
        public static System.Drawing.Color GetStatusColor(TransferStatus status)
        {
            return status switch
            {
                TransferStatus.Transferring => System.Drawing.Color.FromArgb(100, 200, 100),
                TransferStatus.Finished => System.Drawing.Color.FromArgb(100, 255, 100),
                TransferStatus.Paused => System.Drawing.Color.FromArgb(200, 200, 100),
                TransferStatus.Queued or TransferStatus.WaitingForSlot => System.Drawing.Color.FromArgb(150, 150, 200),
                TransferStatus.RetryScheduled or TransferStatus.SearchingAlternative => System.Drawing.Color.FromArgb(200, 150, 100),
                TransferStatus.ConnectionTimeout or TransferStatus.NetworkError => System.Drawing.Color.FromArgb(255, 150, 100),
                TransferStatus.Banned or TransferStatus.FileNotShared => System.Drawing.Color.FromArgb(255, 100, 100),
                _ => System.Drawing.Color.FromArgb(180, 180, 180)
            };
        }
    }
}
