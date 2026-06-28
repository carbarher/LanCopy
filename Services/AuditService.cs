using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LanCopy.Services;

/// <summary>
/// Auditoría de transferencias: quién, qué, cuándo, IP, resultado.
/// Persiste en JSONL rotado por día bajo %LocalAppData%\LanCopy\audit\.
/// Retención: 90 días.
/// </summary>
public static class AuditService
{
    public sealed class AuditRecord
    {
        [JsonPropertyName("ts")]    public string  Timestamp  { get; init; } = DateTime.UtcNow.ToString("O");
        [JsonPropertyName("ip")]    public string  Ip         { get; init; } = "";
        [JsonPropertyName("op")]    public string  Operation  { get; init; } = "";  // send|receive|text|sync
        [JsonPropertyName("file")]  public string  FileName   { get; init; } = "";
        [JsonPropertyName("bytes")] public long    Bytes      { get; init; }
        [JsonPropertyName("ok")]    public bool    Success    { get; init; }
        [JsonPropertyName("ms")]    public long    DurationMs { get; init; }
        [JsonPropertyName("err")]   public string? Error      { get; init; }
        [JsonPropertyName("pin")]   public bool    PinUsed    { get; init; }
    }

    // SemaphoreSlim: garantiza escrituras serializadas (AppendAllTextAsync no es thread-safe).
    private static readonly System.Threading.SemaphoreSlim _writeLock = new(1, 1);

    private static readonly string AuditDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LanCopy", "audit");

    public static void Record(string ip, string operation, string fileName,
        long bytes, bool success, long durationMs,
        string? error = null, bool pinUsed = false)
    {
        var rec = new AuditRecord
        {
            Timestamp  = DateTime.UtcNow.ToString("O"),
            Ip         = ip,
            Operation  = operation,
            FileName   = fileName,
            Bytes      = bytes,
            Success    = success,
            DurationMs = durationMs,
            Error      = error,
            PinUsed    = pinUsed
        };
        _ = Task.Run(() => WriteAsync(rec));
    }

    private static async Task WriteAsync(AuditRecord rec)
    {
        await _writeLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(AuditDir);
            var file = Path.Combine(AuditDir, $"audit-{DateTime.UtcNow:yyyyMMdd}.jsonl");
            await File.AppendAllTextAsync(file,
                JsonSerializer.Serialize(rec) + Environment.NewLine, Encoding.UTF8);
            PruneOld();
        }
        catch { }
        finally { _writeLock.Release(); }
    }

    private static void PruneOld()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-90);
            foreach (var f in Directory.EnumerateFiles(AuditDir, "audit-*.jsonl"))
            {
                var stem = Path.GetFileNameWithoutExtension(f).Replace("audit-", "");
                if (DateTime.TryParseExact(stem, "yyyyMMdd", null,
                    System.Globalization.DateTimeStyles.None, out var d) && d < cutoff)
                    File.Delete(f);
            }
        }
        catch { }
    }

    /// <summary>Lee registros de un día (yyyyMMdd). Si null, hoy.</summary>
    public static List<AuditRecord> ReadDay(string? date = null)
    {
        var dateStr = date ?? DateTime.UtcNow.ToString("yyyyMMdd");
        var file = Path.Combine(AuditDir, $"audit-{dateStr}.jsonl");
        if (!File.Exists(file)) return [];
        var list = new List<AuditRecord>();
        try
        {
            foreach (var line in File.ReadLines(file))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var r = JsonSerializer.Deserialize<AuditRecord>(line);
                if (r != null) list.Add(r);
            }
        }
        catch { }
        return list;
    }
}
