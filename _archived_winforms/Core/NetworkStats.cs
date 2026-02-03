using System;

namespace SlskDown.Core
{
    /// <summary>
    /// Estadísticas de uso de una red P2P
    /// </summary>
    public class NetworkStats
    {
        public string NetworkName { get; set; }
        public int TotalSearches { get; set; }
        public int TotalResults { get; set; }
        public int TotalDownloads { get; set; }
        public long TotalBytesDownloaded { get; set; }
        public int SuccessfulDownloads { get; set; }
        public int FailedDownloads { get; set; }
        public DateTime LastUsed { get; set; }
        public TimeSpan TotalSearchTime { get; set; }
        public double AverageSearchTime => TotalSearches > 0 ? TotalSearchTime.TotalSeconds / TotalSearches : 0;
        public double AverageResultsPerSearch => TotalSearches > 0 ? (double)TotalResults / TotalSearches : 0;
        public double DownloadSuccessRate => TotalDownloads > 0 ? (double)SuccessfulDownloads / TotalDownloads * 100 : 0;

        public NetworkStats(string networkName)
        {
            NetworkName = networkName;
            LastUsed = DateTime.MinValue;
            TotalSearchTime = TimeSpan.Zero;
        }

        public void RecordSearch(int results, TimeSpan duration)
        {
            TotalSearches++;
            TotalResults += results;
            TotalSearchTime += duration;
            LastUsed = DateTime.Now;
        }

        public void RecordDownload(bool success, long bytes = 0)
        {
            TotalDownloads++;
            if (success)
            {
                SuccessfulDownloads++;
                TotalBytesDownloaded += bytes;
            }
            else
            {
                FailedDownloads++;
            }
            LastUsed = DateTime.Now;
        }

        public string GetFormattedSize()
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            if (TotalBytesDownloaded >= GB)
                return $"{TotalBytesDownloaded / (double)GB:F2} GB";
            if (TotalBytesDownloaded >= MB)
                return $"{TotalBytesDownloaded / (double)MB:F2} MB";
            if (TotalBytesDownloaded >= KB)
                return $"{TotalBytesDownloaded / (double)KB:F2} KB";
            return $"{TotalBytesDownloaded} bytes";
        }

        public override string ToString()
        {
            return $"{NetworkName}: {TotalSearches} búsquedas, {TotalResults} resultados, {TotalDownloads} descargas ({DownloadSuccessRate:F1}% éxito)";
        }
    }
}
