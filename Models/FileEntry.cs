using System.Text.Json.Serialization;

namespace LanCopy.Models;

public class FileEntry
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public long LastWriteUtcTicks { get; set; }

    [JsonIgnore] public string Icon => IsDirectory ? "📁" : "📄";
    [JsonIgnore] public string SizeText => IsDirectory ? "—" : FormatSize(Size);
    // Marcador interno (ej: "skip") — no se serializa
    [JsonIgnore] public string? Tag { get; set; }

    public static string FormatSize(long b)
    {
        if (b >= 1_073_741_824) return $"{b / 1_073_741_824.0:F1} GB";
        if (b >= 1_048_576) return $"{b / 1_048_576.0:F1} MB";
        if (b >= 1024) return $"{b / 1024.0:F1} KB";
        return $"{b} B";
    }
}
