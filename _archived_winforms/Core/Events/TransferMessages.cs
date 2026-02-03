using System;

namespace SlskDown.Core.Events
{
    /// <summary>
    /// Mensaje publicado cuando se inicia una transferencia
    /// </summary>
    public class TransferStartedMessage
    {
        public string FileName { get; set; }
        public string Username { get; set; }
        public long FileSize { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Mensaje publicado durante el progreso de una transferencia
    /// </summary>
    public class TransferProgressMessage
    {
        public string FileName { get; set; }
        public long BytesTransferred { get; set; }
        public double Speed { get; set; }
        public double Progress { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Mensaje publicado cuando se completa una transferencia
    /// </summary>
    public class TransferCompletedMessage
    {
        public string FileName { get; set; }
        public long BytesTransferred { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Mensaje publicado cuando falla una transferencia
    /// </summary>
    public class TransferFailedMessage
    {
        public string FileName { get; set; }
        public string ErrorMessage { get; set; }
        public Models.TransferFailureReason Reason { get; set; }
        public DateTime FailedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Mensaje publicado cuando se cancela una transferencia
    /// </summary>
    public class TransferCancelledMessage
    {
        public string FileName { get; set; }
        public string Username { get; set; }
        public DateTime CancelledAt { get; set; } = DateTime.UtcNow;
    }
}
