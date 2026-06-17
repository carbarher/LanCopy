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

    private static readonly BlockingCollection<string> _queue = new(new ConcurrentQueue<string>());
    private static readonly Lazy<bool> _init = new(Init);

    private static bool Init()
    {
        try { Directory.CreateDirectory(LogDir); } catch { }
        var t = new Thread(WriterLoop) { IsBackground = true, Name = "LanCopy-Log" };
        t.Start();
        try { PruneOld(); } catch { }
        return true;
    }

    private static void WriterLoop()
    {
        foreach (var line in _queue.GetConsumingEnumerable())
        {
            try
            {
                var file = Path.Combine(LogDir, $"lancopy-{DateTime.UtcNow:yyyyMMdd}.log");
                File.AppendAllText(file, line, Encoding.UTF8);
            }
            catch { /* el logging nunca debe romper el flujo principal */ }
        }
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
                _queue.Add(JsonSerializer.Serialize(rec) + Environment.NewLine);
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
    /// Señala fin de escrituras y espera a que el hilo background drene la cola.
    /// Llamar desde el evento Closing de la ventana principal para no perder entradas.
    /// </summary>
    public static void Shutdown(int maxWaitMs = 2000)
    {
        try
        {
            if (!_queue.IsAddingCompleted) _queue.CompleteAdding();
        }
        catch { }
    }
}