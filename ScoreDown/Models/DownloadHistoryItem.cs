namespace ScoreDown.Models;

public sealed class DownloadHistoryItem
{
    public string FileName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime DownloadedAt { get; set; }
    public long SizeBytes { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FilePath { get; set; }

    public string Summary => $"{DownloadedAt:dd/MM HH:mm} · {Source} · {FormatBytes(SizeBytes)} · {Status}";

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        if (bytes >= 1024L * 1024L * 1024L) return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        if (bytes >= 1024L * 1024L) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        if (bytes >= 1024L) return $"{bytes / 1024.0:F0} KB";
        return $"{bytes} B";
    }
}
