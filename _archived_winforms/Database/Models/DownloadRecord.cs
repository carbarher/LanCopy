using System;

namespace SlskDown.Database.Models
{
    /// <summary>
    /// Registro de descarga en la base de datos
    /// </summary>
    public class DownloadRecord
    {
        public long Id { get; set; }
        public string FileName { get; set; }
        public string Author { get; set; }
        public string Username { get; set; }
        public long SizeBytes { get; set; }
        public string Status { get; set; }
        public DateTime DownloadedAt { get; set; }
        public string FilePath { get; set; }
        public double? Speed { get; set; }
        public string Language { get; set; }
        public string MD5Hash { get; set; }
        public string SHA1Hash { get; set; }
    }
}
