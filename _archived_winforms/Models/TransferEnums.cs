using System;

namespace SlskDown.Models
{
    /// <summary>
    /// Estados granulares de transferencia inspirados en Nicotine+
    /// Expandido de 8 a 15+ estados para mejor visibilidad
    /// </summary>
    public enum TransferStatus
    {
        // Estados de cola
        Queued,
        WaitingForSlot,
        
        // Estados de conexión
        GettingUserStatus,
        EstablishingConnection,
        Negotiating,
        
        // Estados de transferencia activa
        Transferring,
        Paused,
        
        // Estados finales exitosos
        Finished,
        Filtered,
        
        // Estados de error específicos
        ConnectionTimeout,
        UserLoggedOff,
        UserBusy,
        FileNotShared,
        FileNotAvailable,
        QueueFull,
        Banned,
        Cancelled,
        Aborted,
        
        // Estados de reintento
        RetryScheduled,
        SearchingAlternative,
        
        // Estados de error de red/sistema
        NetworkError,
        DiskFull,
        PermissionDenied,
        FileCorrupted,
        
        // Estado desconocido
        Unknown
    }

    /// <summary>
    /// Razones detalladas de fallo de transferencia
    /// Basado en análisis de Nicotine+ para reintentos inteligentes
    /// </summary>
    public enum TransferFailureReason
    {
        None,
        
        // Errores de conexión (retryable)
        ConnectionTimeout,
        ConnectionRefused,
        ConnectionReset,
        HostUnreachable,
        
        // Errores de usuario (parcialmente retryable)
        UserLoggedOff,
        UserBusy,
        UserBanned,
        
        // Errores de archivo (no retryable)
        FileNotShared,
        FileNotAvailable,
        FileDeleted,
        FileCorrupted,
        
        // Errores de cola (retryable con delay)
        QueueFull,
        TooManyConnections,
        RateLimitExceeded,
        
        // Errores de red (retryable)
        NetworkError,
        SocketError,
        TimeoutError,
        
        // Errores de sistema (no retryable)
        DiskFull,
        InsufficientSpace,
        PermissionDenied,
        PathTooLong,
        
        // Errores de protocolo (retryable con precaución)
        ProtocolError,
        InvalidResponse,
        UnexpectedDisconnect,
        
        // Errores de validación (no retryable)
        ValidationFailed,
        HashMismatch,
        SizeMismatch,
        
        // Otros
        UserCancelled,
        SystemShutdown,
        Unknown
    }

    /// <summary>
    /// Información detallada de error de transferencia
    /// </summary>
    public class TransferError
    {
        public TransferFailureReason Reason { get; set; }
        public string Message { get; set; }
        public string DetailedMessage { get; set; }
        public DateTime OccurredAt { get; set; }
        public bool IsRetryable { get; set; }
        public TimeSpan SuggestedRetryDelay { get; set; }
        public int RetryAttempts { get; set; }
        public Exception InnerException { get; set; }

        public TransferError()
        {
            OccurredAt = DateTime.UtcNow;
            IsRetryable = false;
            SuggestedRetryDelay = TimeSpan.Zero;
        }

        /// <summary>
        /// Crea un TransferError desde una excepción
        /// </summary>
        public static TransferError FromException(Exception ex)
        {
            if (ex == null)
                return new TransferError { Reason = TransferFailureReason.Unknown };

            var error = new TransferError
            {
                Message = ex.Message,
                DetailedMessage = ex.ToString(),
                InnerException = ex
            };

            // Clasificar por tipo de excepción
            switch (ex)
            {
                case TimeoutException:
                    error.Reason = TransferFailureReason.TimeoutError;
                    error.IsRetryable = true;
                    error.SuggestedRetryDelay = TimeSpan.FromMinutes(2);
                    break;

                case System.Net.Sockets.SocketException sockEx:
                    error.Reason = ClassifySocketException(sockEx);
                    error.IsRetryable = true;
                    error.SuggestedRetryDelay = TimeSpan.FromMinutes(1);
                    break;

                case System.IO.IOException ioEx:
                    error.Reason = ClassifyIOException(ioEx);
                    error.IsRetryable = error.Reason == TransferFailureReason.NetworkError;
                    error.SuggestedRetryDelay = error.IsRetryable ? TimeSpan.FromMinutes(1) : TimeSpan.Zero;
                    break;

                case UnauthorizedAccessException:
                    error.Reason = TransferFailureReason.PermissionDenied;
                    error.IsRetryable = false;
                    break;

                case OperationCanceledException:
                    error.Reason = TransferFailureReason.UserCancelled;
                    error.IsRetryable = false;
                    break;

                default:
                    error.Reason = TransferFailureReason.Unknown;
                    error.IsRetryable = true;
                    error.SuggestedRetryDelay = TimeSpan.FromMinutes(5);
                    break;
            }

            return error;
        }

        private static TransferFailureReason ClassifySocketException(System.Net.Sockets.SocketException ex)
        {
            return ex.SocketErrorCode switch
            {
                System.Net.Sockets.SocketError.TimedOut => TransferFailureReason.ConnectionTimeout,
                System.Net.Sockets.SocketError.ConnectionRefused => TransferFailureReason.ConnectionRefused,
                System.Net.Sockets.SocketError.ConnectionReset => TransferFailureReason.ConnectionReset,
                System.Net.Sockets.SocketError.HostUnreachable => TransferFailureReason.HostUnreachable,
                System.Net.Sockets.SocketError.NetworkUnreachable => TransferFailureReason.NetworkError,
                _ => TransferFailureReason.SocketError
            };
        }

        private static TransferFailureReason ClassifyIOException(System.IO.IOException ex)
        {
            var message = ex.Message.ToLowerInvariant();

            if (message.Contains("disk") && message.Contains("full"))
                return TransferFailureReason.DiskFull;

            if (message.Contains("space"))
                return TransferFailureReason.InsufficientSpace;

            if (message.Contains("access") && message.Contains("denied"))
                return TransferFailureReason.PermissionDenied;

            if (message.Contains("path") && message.Contains("long"))
                return TransferFailureReason.PathTooLong;

            return TransferFailureReason.NetworkError;
        }

        /// <summary>
        /// Crea un error específico de Soulseek
        /// </summary>
        public static TransferError FromSoulseekRejection(string rejectionMessage)
        {
            var error = new TransferError
            {
                Message = rejectionMessage ?? "Transfer rejected",
                DetailedMessage = $"Soulseek rejection: {rejectionMessage}"
            };

            var lower = rejectionMessage?.ToLowerInvariant() ?? "";

            if (lower.Contains("banned") || lower.Contains("blocked"))
            {
                error.Reason = TransferFailureReason.UserBanned;
                error.IsRetryable = false;
            }
            else if (lower.Contains("queue") && lower.Contains("full"))
            {
                error.Reason = TransferFailureReason.QueueFull;
                error.IsRetryable = true;
                error.SuggestedRetryDelay = TimeSpan.FromMinutes(10);
            }
            else if (lower.Contains("file") && (lower.Contains("not") || lower.Contains("unavailable")))
            {
                error.Reason = TransferFailureReason.FileNotAvailable;
                error.IsRetryable = false;
            }
            else if (lower.Contains("busy") || lower.Contains("slots"))
            {
                error.Reason = TransferFailureReason.UserBusy;
                error.IsRetryable = true;
                error.SuggestedRetryDelay = TimeSpan.FromMinutes(5);
            }
            else
            {
                error.Reason = TransferFailureReason.Unknown;
                error.IsRetryable = true;
                error.SuggestedRetryDelay = TimeSpan.FromMinutes(3);
            }

            return error;
        }

        /// <summary>
        /// Obtiene un mensaje amigable para el usuario
        /// </summary>
        public string GetUserFriendlyMessage()
        {
            return Reason switch
            {
                TransferFailureReason.ConnectionTimeout => "Tiempo de conexión agotado. Se reintentará automáticamente.",
                TransferFailureReason.UserLoggedOff => "Usuario desconectado. Se reintentará cuando vuelva a conectarse.",
                TransferFailureReason.UserBusy => "Usuario ocupado. Se reintentará en unos minutos.",
                TransferFailureReason.FileNotShared => "Archivo ya no está compartido.",
                TransferFailureReason.QueueFull => "Cola del usuario llena. Se reintentará más tarde.",
                TransferFailureReason.DiskFull => "Disco lleno. Libera espacio para continuar.",
                TransferFailureReason.PermissionDenied => "Permiso denegado. Verifica permisos de carpeta.",
                TransferFailureReason.UserBanned => "Usuario te ha bloqueado.",
                TransferFailureReason.NetworkError => "Error de red. Se reintentará automáticamente.",
                TransferFailureReason.UserCancelled => "Descarga cancelada por el usuario.",
                _ => Message ?? "Error desconocido"
            };
        }

        public override string ToString()
        {
            return $"{Reason}: {Message} (Retryable: {IsRetryable}, Delay: {SuggestedRetryDelay})";
        }
    }
}
