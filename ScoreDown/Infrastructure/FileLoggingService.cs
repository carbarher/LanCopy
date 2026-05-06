using System;
using System.IO;
using System.Text;
using System.Threading;

namespace ScoreDown.Infrastructure;

/// <summary>
/// Centralizes logging to file + memory with daily rotation.
/// Thread-safe, async-friendly.
/// </summary>
public class FileLoggingService
{
    private readonly string _logDir;
    private string _currentLogFile = string.Empty;
    private readonly object _lockObj = new();

    public FileLoggingService(string? logDir = null)
    {
        _logDir = logDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScoreDown", "Logs");

        Directory.CreateDirectory(_logDir);
        RotateLogIfNeeded();
    }

    public void Log(string message)
    {
        lock (_lockObj)
        {
            RotateLogIfNeeded();
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var line = $"[{timestamp}] {message}";

            try
            {
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        using var fs = new FileStream(_currentLogFile, FileMode.Append, FileAccess.Write, FileShare.Read);
                        using var sw = new StreamWriter(fs, Encoding.UTF8);
                        sw.WriteLine(line);
                        break;
                    }
                    catch (IOException) when (attempt < 2)
                    {
                        // Otra instancia puede tener lock temporal; reintento breve.
                        Thread.Sleep(10);
                    }
                }
            }
            catch
            {
                // Silently fail if disk full/permissions error
            }
        }
    }

    private void RotateLogIfNeeded()
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        var expectedFile = Path.Combine(_logDir, $"scoredown_{today}.log");

        if (_currentLogFile != expectedFile)
        {
            _currentLogFile = expectedFile;
            try
            {
                if (!File.Exists(_currentLogFile))
                    File.WriteAllText(_currentLogFile, $"=== ScoreDown Log {DateTime.Now:yyyy-MM-dd} ==={Environment.NewLine}", Encoding.UTF8);
            }
            catch { }
        }
    }

    public string GetLogDirectory() => _logDir;

    public void Cleanup(int keepDays = 30)
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-keepDays);
            foreach (var file in Directory.GetFiles(_logDir, "scoredown_*.log"))
            {
                if (File.GetCreationTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch { }
    }
}
