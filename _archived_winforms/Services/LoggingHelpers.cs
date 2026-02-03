using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Services
{
    /// <summary>
    /// Utilidades para logging thread-safe y eficiente
    /// </summary>
    public class LoggingHelpers
    {
        private readonly string _logDirectory;
        private readonly ConcurrentQueue<string> _logQueue;
        private readonly SemaphoreSlim _writeSemaphore;
        private readonly int _maxLogFiles;
        private readonly long _maxLogSizeBytes;
        private bool _isDisposed;

        public LoggingHelpers(string logDirectory, int maxLogFiles = 10, long maxLogSizeMB = 10)
        {
            _logDirectory = logDirectory;
            _logQueue = new ConcurrentQueue<string>();
            _writeSemaphore = new SemaphoreSlim(1, 1);
            _maxLogFiles = maxLogFiles;
            _maxLogSizeBytes = maxLogSizeMB * 1024 * 1024;
            _isDisposed = false;

            // Crear directorio si no existe
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            // Iniciar procesador de logs en background
            Task.Run(() => ProcessLogQueue());
        }

        /// <summary>
        /// Agrega un mensaje al log
        /// </summary>
        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            if (_isDisposed || string.IsNullOrWhiteSpace(message))
                return;

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var levelStr = GetLevelString(level);
            var logEntry = $"[{timestamp}] [{levelStr}] {message}";

            _logQueue.Enqueue(logEntry);
        }

        /// <summary>
        /// Agrega un mensaje de error al log
        /// </summary>
        public void LogError(string message, Exception ex = null)
        {
            var fullMessage = ex != null 
                ? $"{message}\nException: {ex.Message}\nStackTrace: {ex.StackTrace}"
                : message;

            Log(fullMessage, LogLevel.Error);
        }

        /// <summary>
        /// Agrega un mensaje de advertencia al log
        /// </summary>
        public void LogWarning(string message)
        {
            Log(message, LogLevel.Warning);
        }

        /// <summary>
        /// Agrega un mensaje de debug al log
        /// </summary>
        public void LogDebug(string message)
        {
            Log(message, LogLevel.Debug);
        }

        /// <summary>
        /// Procesa la cola de logs de forma asíncrona
        /// </summary>
        private async Task ProcessLogQueue()
        {
            var buffer = new StringBuilder();
            var lastWrite = DateTime.Now;

            while (!_isDisposed)
            {
                try
                {
                    // Procesar mensajes en lotes cada 1 segundo o cuando hay 100 mensajes
                    if (_logQueue.Count >= 100 || (DateTime.Now - lastWrite).TotalSeconds >= 1)
                    {
                        buffer.Clear();

                        while (_logQueue.TryDequeue(out var message) && buffer.Length < 100000)
                        {
                            buffer.AppendLine(message);
                        }

                        if (buffer.Length > 0)
                        {
                            await WriteToFileAsync(buffer.ToString());
                            lastWrite = DateTime.Now;
                        }
                    }

                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in log processor: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Escribe logs al archivo de forma thread-safe
        /// </summary>
        private async Task WriteToFileAsync(string content)
        {
            await _writeSemaphore.WaitAsync();
            try
            {
                var logFile = GetCurrentLogFile();
                
                // Rotar log si es muy grande
                if (File.Exists(logFile) && new FileInfo(logFile).Length > _maxLogSizeBytes)
                {
                    RotateLogFiles();
                    logFile = GetCurrentLogFile();
                }

                await File.AppendAllTextAsync(logFile, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to log file: {ex.Message}");
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        /// <summary>
        /// Obtiene la ruta del archivo de log actual
        /// </summary>
        private string GetCurrentLogFile()
        {
            var fileName = $"slskdown_{DateTime.Now:yyyyMMdd}.log";
            return Path.Combine(_logDirectory, fileName);
        }

        /// <summary>
        /// Rota los archivos de log (mantiene solo los últimos N)
        /// </summary>
        private void RotateLogFiles()
        {
            try
            {
                var logFiles = Directory.GetFiles(_logDirectory, "slskdown_*.log");
                
                if (logFiles.Length >= _maxLogFiles)
                {
                    // Ordenar por fecha de creación
                    Array.Sort(logFiles, (a, b) => 
                        File.GetCreationTime(a).CompareTo(File.GetCreationTime(b)));

                    // Eliminar los más antiguos
                    for (int i = 0; i < logFiles.Length - _maxLogFiles + 1; i++)
                    {
                        try
                        {
                            File.Delete(logFiles[i]);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error deleting old log file {logFiles[i]}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rotating log files: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene el string del nivel de log
        /// </summary>
        private string GetLevelString(LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => "DEBUG",
                LogLevel.Info => "INFO ",
                LogLevel.Warning => "WARN ",
                LogLevel.Error => "ERROR",
                _ => "INFO "
            };
        }

        /// <summary>
        /// Limpia todos los logs
        /// </summary>
        public void ClearLogs()
        {
            try
            {
                var logFiles = Directory.GetFiles(_logDirectory, "slskdown_*.log");
                foreach (var file in logFiles)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deleting log file {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing logs: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene los últimos N mensajes del log
        /// </summary>
        public string[] GetRecentLogs(int count = 100)
        {
            try
            {
                var logFile = GetCurrentLogFile();
                if (File.Exists(logFile))
                {
                    var lines = File.ReadAllLines(logFile);
                    var startIndex = Math.Max(0, lines.Length - count);
                    var result = new string[Math.Min(count, lines.Length)];
                    Array.Copy(lines, startIndex, result, 0, result.Length);
                    return result;
                }
            }
            catch { }

            return Array.Empty<string>();
        }

        /// <summary>
        /// Libera recursos
        /// </summary>
        public void Dispose()
        {
            _isDisposed = true;
            
            // Procesar mensajes pendientes
            while (_logQueue.TryDequeue(out var message))
            {
                try
                {
                    var logFile = GetCurrentLogFile();
                    File.AppendAllText(logFile, message + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing to log file: {ex.Message}");
                }
            }

            _writeSemaphore?.Dispose();
        }
    }

    /// <summary>
    /// Niveles de log
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}
