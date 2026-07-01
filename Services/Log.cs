using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace LanCopy.Services;

// Logger estructurado, ligero y sin dependencias externas.
// Escribe JSON-lines a %LocalAppData%\LanCopy\logs\lancopy-YYYYMMDD.log de forma asincrona
// (cola en background) para no bloquear el hilo de UI ni el de red. Rota por dia y poda
// ficheros con mas de RetentionDays dias.
public static class Log
{
    public enum Level { Debug, Info, Warn, Error }

    public static Level Minimum { get; set; } = Level.Info;
    public static int RetentionDays { get; set; } = 14;

    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LanCopy", "logs");

    // Q3/P1: capacity 4096 para backpressure — TryAdd drops en overflow en lugar de bloquear handlers
    private static readonly BlockingCollection<string> _queue = new(4096);
    // B2: contador de entradas descartadas por overflow de cola (expuesto para diagnóstico en health endpoint)
    private static int _droppedLogs;
    public static int DroppedLogs => Volatile.Read(ref _droppedLogs);
    private static readonly Lazy<bool> _init = new(Init);

    private static Thread? _writerThread; // B6: guardamos referencia para Join en Shutdown

    private static bool Init()
    {
        try { Directory.CreateDirectory(LogDir); } catch { }
        _writerThread = new Thread(WriterLoop) { IsBackground = true, Name = "LanCopy-Log" };
        _writerThread.Start();
        try { PruneOld(); } catch { }
        return true;
    }

    private static void WriterLoop()
    {
        // P2: reusar StreamWriter con flush periódico (no por línea) → -3 syscalls por entrada
        StreamWriter? sw = null;
        string? currentFile = null;
        var lastFlush = DateTime.UtcNow;
        const int FlushIntervalMs = 500;

        foreach (var line in _queue.GetConsumingEnumerable())
        {
            try
            {
                var file = Path.Combine(LogDir, $"lancopy-{DateTime.UtcNow:yyyyMMdd}.log");
                if (file != currentFile)
                {
                    sw?.Flush(); sw?.Dispose();
                    Directory.CreateDirectory(LogDir);
                    sw = new StreamWriter(file, append: true, Encoding.UTF8) { AutoFlush = false };
                    currentFile = file;
                }
                sw!.Write(line); // P1: line ya incluye NewLine del Add(); WriteLine a\u00f1adir\u00eda doble newline
                var now = DateTime.UtcNow;
                if ((now - lastFlush).TotalMilliseconds >= FlushIntervalMs || _queue.Count == 0)
                {
                    sw.Flush();
                    lastFlush = now;
                }
            }
            catch { try { sw?.Dispose(); } catch { } sw = null; currentFile = null; }
        }
        try { sw?.Flush(); sw?.Dispose(); } catch { }
    }

    private static void PruneOld()
    {
        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
        foreach (var f in Directory.EnumerateFiles(LogDir, "lancopy-*.log"))
        {
            try { if (File.GetLastWriteTimeUtc(f) < cutoff) File.Delete(f); } catch { }
        }
    }

    public static void Write(Level level, string category, string message, object? data = null)
    {
        if (level < Minimum) return;
        _ = _init.Value;
        try
        {
            var rec = new
            {
                ts = DateTime.UtcNow.ToString("O"),
                level = level.ToString().ToLowerInvariant(),
                category,
                message,
                data
            };
            if (!_queue.IsAddingCompleted)
                // B3: TryAdd(timeout=0) descarta entradas si la cola está llena en lugar de bloquear
                // B2: contar drops para diagnóstico
                if (!_queue.TryAdd(JsonSerializer.Serialize(rec) + Environment.NewLine, 0))
                    System.Threading.Interlocked.Increment(ref _droppedLogs);
        }
        catch { }
    }

    public static void Debug(string category, string message, object? data = null) => Write(Level.Debug, category, message, data);
    public static void Info(string category, string message, object? data = null) => Write(Level.Info, category, message, data);
    public static void Warn(string category, string message, object? data = null) => Write(Level.Warn, category, message, data);
    public static void Error(string category, string message, object? data = null) => Write(Level.Error, category, message, data);

    public static string CurrentLogFile => Path.Combine(LogDir, $"lancopy-{DateTime.UtcNow:yyyyMMdd}.log");
    public static string Directory_ => LogDir;

    /// <summary>
    /// SeÃ±ala fin de escrituras y espera a que el hilo background drene la cola.
    /// Llamar desde el evento Closing de la ventana principal para no perder entradas.
    /// </summary>
    public static void Shutdown(int maxWaitMs = 2000)
    {
        try { if (!_queue.IsAddingCompleted) _queue.CompleteAdding(); } catch { }
        // B6: esperar realmente a que el hilo drene la cola (antes maxWaitMs se ignoraba)
        try { _writerThread?.Join(maxWaitMs); } catch { }
    }
}