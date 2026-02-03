using System;

namespace SlskDown.Database.Models
{
    /// <summary>
    /// Puntuación de una fuente/usuario
    /// </summary>
    public class SourceRating
    {
        public string Username { get; set; }
        public double AverageSpeed { get; set; }
        public double SuccessRate { get; set; }
        public int TotalDownloads { get; set; }
        public int FailedDownloads { get; set; }
        public int DisconnectionCount { get; set; }
        public DateTime LastSeen { get; set; }
        public double Score { get; set; }
    }
}
