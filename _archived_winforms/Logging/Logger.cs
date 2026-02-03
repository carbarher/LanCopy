using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Logging
{
    /// <summary>
    /// Sistema de logging estructurado y asíncrono
    /// </summary>
    public class Logger : IDisposable
    {
        private static readonly Lazy<Logger> _instance = new(() => new Logger());
        public static Logger Instance => _instance.Value;
        
        private readonly ConcurrentQueue<LogEntry> _logQueue = new();
        private readonly SemaphoreSlim _signal = new(0);
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _writerTask;
        private readonly string _logDirectory;
        private readonly int _maxLogFiles = 10;
        private readonly long _maxLogSizeBytes = 10 * 1024 * 1024; // 10MB
        
        private Logger()
        {
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(_logDirectory);
            
            // Iniciar tarea de escritura en background
            _writerTask = Task.Run(ProcessLogQueueAsync);
            
            // Limpiar logs antiguos
            CleanupOldLogs();
        }
        
        /// <summary>
        /// Registra un mensaje de debug
        /// </summary>
        public void Debug(string message, Exception? exception = null)
        {
            Log(LogLevel.Debug, message, exception);
        }
        
        /// <summary>
        /// Registra un mensaje informativo
        /// </summary>
        public void Info(string message, Exception? exception = null)
        {
            Log(LogLevel.Info, message, exception);
        }
        
        /// <summary>
        /// Registra una advertencia
        /// </summary>
        public void Warning(string message, Exception? exception = null)
        {
            Log(LogLevel.Warning, message, exception);
        }
        
        /// <summary>
        /// Registra un error
        /// </summary>
        public void Error(string message, Exception? exception = null)
        {
            Log(LogLevel.Error, message, exception);
        }
        
        /// <summary>
        /// Registra un error crítico
        /// </summary>
        public void Critical(string message, Exception? exception = null)
        {
            Log(LogLevel.Critical, message, exception);
        }
        
        /// <summary>
        /// Registra un mensaje con nivel específico
        /// </summary>
        private void Log(LogLevel level, string message, Exception? exception = null)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Message = message,
                Exception = exception,
                ThreadId = Thread.CurrentThread.ManagedThreadId
            };
            
            _logQueue.Enqueue(entry);
            _signal.Release();
            
            // También escribir a consola en desarrollo
            #if DEBUG
            Console.WriteLine($"[{level}] {message}");
            if (exception != null)
                Console.WriteLine($"  Exception: {exception.Message}");
            #endif
        }
        
        /// <summary>
        /// Procesa la cola de logs de forma asíncrona
        /// </summary>
        private async Task ProcessLogQueueAsync()
        {
            var currentLogFile = GetCurrentLogFilePath();
            
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await _signal.WaitAsync(_cts.Token);
                    
                    var batch = new StringBuilder();
                    var count = 0;
                    
                    // Procesar hasta 100 entradas por lote
                    while (count < 100 && _logQueue.TryDequeue(out var entry))
                    {
                        batch.AppendLine(FormatLogEntry(entry));
                        count++;
                    }
                    
                    if (batch.Length > 0)
                    {
                        // Rotar log si es necesario
                        var fileInfo = new FileInfo(currentLogFile);
                        if (fileInfo.Exists && fileInfo.Length > _maxLogSizeBytes)
                        {
                            currentLogFile = RotateLogFile(currentLogFile);
                        }
                        
                        await File.AppendAllTextAsync(currentLogFile, batch.ToString(), _cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Fallback: escribir a consola si falla el logging
                    Console.WriteLine($"Logger error: {ex.Message}");
                }
            }
            
            // Escribir entradas restantes al cerrar
            FlushRemainingLogs(currentLogFile);
        }
        
        /// <summary>
        /// Formatea una entrada de log
        /// </summary>
        private string FormatLogEntry(LogEntry entry)
        {
            var sb = new StringBuilder();
            
            sb.Append($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] ");
            sb.Append($"[{entry.Level,-8}] ");
            sb.Append($"[T{entry.ThreadId:D3}] ");
            sb.Append(entry.Message);
            
            if (entry.Exception != null)
            {
                sb.AppendLine();
                sb.Append($"  Exception: {entry.Exception.GetType().Name}: {entry.Exception.Message}");
                sb.AppendLine();
                sb.Append($"  StackTrace: {entry.Exception.StackTrace}");
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Obtiene la ruta del archivo de log actual
        /// </summary>
        private string GetCurrentLogFilePath()
        {
            var date = DateTime.Now.ToString("yyyyMMdd");
            return Path.Combine(_logDirectory, $"slskdown_{date}.log");
        }
        
        /// <summary>
        /// Rota el archivo de log
        /// </summary>
        private string RotateLogFile(string currentFile)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var newFile = Path.Combine(_logDirectory, $"slskdown_{timestamp}.log");
            
            try
            {
                File.Move(currentFile, newFile);
            }
            catch
            {
                // Si falla, continuar con el archivo actual
            }
            
            return GetCurrentLogFilePath();
        }
        
        /// <summary>
        /// Limpia logs antiguos
        /// </summary>
        private void CleanupOldLogs()
        {
            try
            {
                var logFiles = Directory.GetFiles(_logDirectory, "slskdown_*.log");
                
                if (logFiles.Length > _maxLogFiles)
                {
                    Array.Sort(logFiles);
                    var filesToDelete = logFiles.Length - _maxLogFiles;
                    
                    for (int i = 0; i < filesToDelete; i++)
                    {
                        File.Delete(logFiles[i]);
                    }
                }
            }
            catch
            {
                // Ignorar errores de limpieza
            }
        }
        
        /// <summary>
        /// Escribe las entradas restantes al cerrar
        /// </summary>
        private void FlushRemainingLogs(string logFile)
        {
            var batch = new StringBuilder();
            
            while (_logQueue.TryDequeue(out var entry))
            {
                batch.AppendLine(FormatLogEntry(entry));
            }
            
            if (batch.Length > 0)
            {
                try
                {
                    File.AppendAllText(logFile, batch.ToString());
                }
                catch
                {
                    // Último recurso: consola
                    Console.WriteLine(batch.ToString());
                }
            }
        }
        
        public void Dispose()
        {
            _cts.Cancel();
            _writerTask.Wait(TimeSpan.FromSeconds(5));
            _cts.Dispose();
            _signal.Dispose();
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
        Error,
        Critical
    }
    
    /// <summary>
    /// Entrada de log
    /// </summary>
    internal class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public int ThreadId { get; set; }
    }
}
