namespace LanCopy.Models;

// Feature 3: cola persistente
public record QueueItem(
    string[] FilePaths,
    string[] DestPaths,
    bool IsUpload,
    string RemoteIp,
    int RemotePort,
    string? CreatedUtc = null,
    int Attempt = 0
);