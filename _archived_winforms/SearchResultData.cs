namespace SlskDown
{
    public class SearchResultData
    {
        public string Username { get; set; } = "";
        public string Filename { get; set; } = "";
        public long Size { get; set; }
    }

    public class DownloadInfo
    {
        public string Username { get; set; } = "";
        public string Filename { get; set; } = "";
        public string LocalPath { get; set; } = "";
        public long TotalBytes { get; set; }
        public long BytesDownloaded { get; set; }
    }

    public class AppConfig
    {
        public string Username { get; set; } = "carbar";
        public string Password { get; set; } = "Carlos66*";
        public string DownloadDir { get; set; } = @"c:\p2p\downloads";
    }
}

