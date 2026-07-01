using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LanCopy.Services;

/// <summary>
/// AuditorÃ­a de transferencias: quiÃ©n, quÃ©, cuÃ¡ndo, IP, resultado.
/// Persiste en JSONL rotado por dÃ­a bajo %LocalAppData%\LanCopy\audit\.
/// RetenciÃ³n: 90 dÃ­as.
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
    private static DateTime _lastPrunedDay = DateTime.MinValue; // P1: rate-limit PruneOld

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
        // Q3: observar excepciones del Task.Run para evitar unhandled faults silenciosos
        // Q2: usar Log.Warn en lugar de Debug.WriteLine — visible en builds Release
        _ = Task.Run(() => WriteAsync(rec)).ContinueWith(
            t => Log.Warn("audit", $"WriteAsync failed: {t.Exception?.GetBaseException().Message}"),
            System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
    }

    private static async Task WriteAsync(AuditRecord rec)
    {
        await _writeLock.WaitAsync();
        try
        {
            // B5: capturar fecha DENTRO del lock — evita escribir en el día equivocado si el semáforo
            // espera mucho cerca de medianoche (el registro iba al fichero del día anterior).
            var auditToday = DateTime.UtcNow.ToString("yyyyMMdd");
            Directory.CreateDirectory(AuditDir);
            var file = Path.Combine(AuditDir, $"audit-{auditToday}.jsonl");
            await File.AppendAllTextAsync(file,
                JsonSerializer.Serialize(rec) + Environment.NewLine, Encoding.UTF8);
        }
        catch { }
        finally { _writeLock.Release(); }
        // B2: PruneOld fuera del lock — el barrido de ficheros no debe bloquear audit writes durante
        // la poda diaria. La carrera en _lastPrunedDay es inofensiva (peor caso: dos pruning el mismo día).
        var today = DateTime.UtcNow.Date;
        if (today != _lastPrunedDay)
        {
            _lastPrunedDay = today;
            PruneOld();
        }
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
    public static async Task<List<AuditRecord>> ReadDay(string? date = null)
    {
        var dateStr = date ?? DateTime.UtcNow.ToString("yyyyMMdd");
        var file = Path.Combine(AuditDir, $"audit-{dateStr}.jsonl");
        if (!File.Exists(file)) return [];
        // B6: File.ReadLines() es lazy y mantiene el fichero abierto → deadlock con _writeLock.
        // Leer TODO el contenido de golpe bajo el semáforo para evitar conflicto con WriteAsync.
        // P2: WaitAsync() en lugar de Wait() — ReadDay se llama desde UI thread en ShowAudit_Click;
        // Wait() síncrono bloquea el UI thread durante I/O de disco (potencialmente cientos de ms)
        string raw;
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try { raw = await File.ReadAllTextAsync(file).ConfigureAwait(false); }
        catch { return []; }
        finally { _writeLock.Release(); }

        var list = new List<AuditRecord>();
        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var r = JsonSerializer.Deserialize<AuditRecord>(line.TrimEnd('\r'));
                if (r != null) list.Add(r);
            }
            catch { }
        }
        return list;
    }
}
