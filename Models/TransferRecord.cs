namespace LanCopy.Models;

public sealed class TransferRecord
{
    public string Time      { get; init; } = "";
    public string Text      { get; init; } = "";
    public string Color     { get; init; } = "#CCCCCC";
    // Metadatos de auditoría (compatibles con historial existente)
    public string Operation { get; init; } = "";   // "send" | "receive" | "text" | "sync"
    public string PeerIp    { get; init; } = "";
    public long   Bytes     { get; init; }
    public bool   Success   { get; init; } = true;
    // U4: ruta local del fichero transferido — permite que el double-click lo revele en Explorer
    public string LocalPath { get; init; } = "";

    // Propiedades computadas para bindings en la UI
    public string BytesText => Bytes > 0 ? FileEntry.FormatSize(Bytes) : "";
    public bool   HasBytes  => Bytes > 0;
}
