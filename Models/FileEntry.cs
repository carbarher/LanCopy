using System.IO;
using System.Text.Json.Serialization;

namespace LanCopy.Models;

public class FileEntry
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public long LastWriteUtcTicks { get; set; }

    // M5: campos de caché lazy — SizeText/DateText son inmutables post-construcción pero Avalonia
    // los solicita en cada render/scroll del ListBox (virtualiza el binding). ??= garantiza
    // cómputo único sin necesidad de lock (FileEntry no muta Size ni LastWriteUtcTicks tras creación).
    private string? _sizeText;
    [JsonIgnore] public string SizeText => _sizeText ??= IsDirectory ? "—" : FormatSize(Size);

    private string? _dateText;
    [JsonIgnore] public string DateText => _dateText ??= LastWriteUtcTicks > 0
        ? new DateTime(LastWriteUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yy HH:mm")
        : "";

    [JsonIgnore] public string Icon => IsDirectory ? "📁" : "📄";

    // Marcador interno (ej: "skip") — no se serializa
    [JsonIgnore] public string? Tag { get; set; }

    // Extensiones cuyo contenido ya está comprimido internamente;
    // aplicar deflate sobre ellas solo gasta CPU sin reducir tamaño.
    private static readonly HashSet<string> CompressedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Images
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".heif", ".avif",
        // Video
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm",
        // Audio
        ".mp3", ".aac", ".ogg", ".flac", ".wma", ".m4a", ".opus",
        // Archives
        ".zip", ".rar", ".7z", ".gz", ".bz2", ".xz", ".zst", ".tgz", ".lz4",
        // Other (internally compressed / zip-based)
        ".pdf", ".docx", ".xlsx", ".pptx"
    };

    /// <summary>
    /// Returns true if the file extension indicates the content is already
    /// compressed and deflate should be skipped during transfer.
    /// </summary>
    public static bool IsAlreadyCompressed(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return false;
        var ext = Path.GetExtension(fileName);
        return ext != null && CompressedExtensions.Contains(ext);
    }

    public static string FormatSize(long b)
    {
        if (b >= 1_073_741_824) return $"{b / 1_073_741_824.0:F1} GB";
        if (b >= 1_048_576) return $"{b / 1_048_576.0:F1} MB";
        if (b >= 1024) return $"{b / 1024.0:F1} KB";
        return $"{b} B";
    }
}
