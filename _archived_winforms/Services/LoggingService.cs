using System;
using System.IO;

namespace SlskDown.Services
{
    /// <summary>
    /// ImplementaciÃ³n simple de logging a archivo
    /// TODO: Reemplazar con Serilog cuando se agregue el paquete NuGet
    /// </summary>
    public class LoggingService : ILoggingService
    {
        private readonly string _logPath;
        private readonly object _lock = new object();

        public LoggingService()
        {
            var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logsDir);
            
            var fileName = $"slskdown-{DateTime.Now:yyyy-MM-dd}.txt";
            _logPath = Path.Combine(logsDir, fileName);
        }

        // MÃ©todos de la interfaz ILoggingService
        public void LogDebug(string message) => Log("DEBUG", message);
        public void LogInfo(string message) => Log("INFO", message);
        public void LogWarning(string message) => Log("WARN", message);
        
        public void LogError(string message, Exception? ex = null)
        {
            var fullMessage = ex != null 
                ? $"{message}\nException: {ex.Message}\nStackTrace: {ex.StackTrace}"
                : message;
            Log("ERROR", fullMessage);
        }

        // MÃ©todos de conveniencia (aliases)
        public void Debug(string message) => LogDebug(message);
        public void Info(string message) => LogInfo(message);
        public void Warning(string message) => LogWarning(message);
        public void Error(string message, Exception? exception = null) => LogError(message, exception);

        private void Log(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var logEntry = $"[{timestamp}] [{level}] {message}\n";
                    File.AppendAllText(_logPath, logEntry);
                }
            }
            catch
            {
                // Ignorar errores de logging para no romper la aplicaciÃ³n
            }
        }
    }
}

/* 
 * NOTA: Para usar Serilog completo, agregar al .csproj:
 * 
 * <PackageReference Include="Serilog" Version="3.1.1" />
 * <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
 * <PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />
 * 
 * Y reemplazar esta implementaciÃ³n con:
 * 
 * using Serilog;
 * 
 * Log.Logger = new LoggerConfiguration()
 *     .MinimumLevel.Debug()
 *     .WriteTo.File("logs/slskdown-.txt", rollingInterval: RollingInterval.Day)
 *     .WriteTo.Console()
 *     .CreateLogger();
 */

