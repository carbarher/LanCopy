using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// Gestor de logs con compresión automática y rotación inteligente
    /// Características: GZIP, rotación por tamaño/tiempo, búsqueda en comprimidos
    /// </summary>
    public class CompressedLogManager : IDisposable
    {
        private readonly string logDirectory;
        private readonly string currentLogFile;
        private readonly System.Threading.Timer rotationTimer;
        private readonly object writeLock = new object();
        
        // Configuración
        private const long MAX_LOG_SIZE_BYTES = 10 * 1024 * 1024; // 10 MB
        private const int MAX_LOG_AGE_DAYS = 7;
        private const int ROTATION_CHECK_INTERVAL_MS = 3600000; // 1 hora
        private const int MAX_ARCHIVED_LOGS = 30;
        
        // Niveles de log
        public enum LogLevel
        {
            Trace = 0,
            Debug = 1,
            Info = 2,
            Warning = 3,
            Error = 4,
            Critical = 5
        }
        
        private LogLevel minimumLevel = LogLevel.Info;
        
        // Estadísticas
        private long totalBytesWritten = 0;
        private long totalLinesWritten = 0;
        private long totalBytesCompressed = 0;
        private int totalRotations = 0;
        
        public CompressedLogManager(string logDirectory, LogLevel minimumLevel = LogLevel.Info)
        {
            this.logDirectory = logDirectory;
            this.minimumLevel = minimumLevel;
            
            // Crear directorio si no existe
            Directory.CreateDirectory(logDirectory);
            
            // Archivo de log actual
            currentLogFile = Path.Combine(logDirectory, "slskdown.log");
            
            // Timer para rotación periódica
            rotationTimer = new System.Threading.Timer((state) => CheckAndRotate(), null, 
                System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            rotationTimer.Change(ROTATION_CHECK_INTERVAL_MS, ROTATION_CHECK_INTERVAL_MS);
            
            Console.WriteLine($"[LogManager] Inicializado: {logDirectory}");
            Console.WriteLine($"[LogManager] Nivel mínimo: {minimumLevel}");
        }
        
        /// <summary>
        /// Escribe una línea de log
        /// </summary>
        public void Log(LogLevel level, string message, Exception exception = null)
        {
            if (level < minimumLevel)
                return;
            
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var levelStr = level.ToString().ToUpperInvariant().PadRight(8);
                var threadId = Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(4);
                
                var logLine = new StringBuilder();
                logLine.Append($"[{timestamp}] [{levelStr}] [T{threadId}] {message}");
                
                if (exception != null)
                {
                    logLine.AppendLine();
                    logLine.Append($"    Exception: {exception.GetType().Name}: {exception.Message}");
                    logLine.AppendLine();
                    logLine.Append($"    StackTrace: {exception.StackTrace}");
                }
                
                var line = logLine.ToString();
                
                lock (writeLock)
                {
                    File.AppendAllText(currentLogFile, line + Environment.NewLine);
                    totalBytesWritten += Encoding.UTF8.GetByteCount(line);
                    totalLinesWritten++;
                }
                
                // Verificar si necesita rotación
                CheckAndRotate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogManager] Error escribiendo log: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Métodos de conveniencia para cada nivel
        /// </summary>
        public void Trace(string message) => Log(LogLevel.Trace, message);
        public void Debug(string message) => Log(LogLevel.Debug, message);
        public void Info(string message) => Log(LogLevel.Info, message);
        public void Warning(string message) => Log(LogLevel.Warning, message);
        public void Error(string message, Exception ex = null) => Log(LogLevel.Error, message, ex);
        public void Critical(string message, Exception ex = null) => Log(LogLevel.Critical, message, ex);
        
        /// <summary>
        /// Verifica y rota el log si es necesario
        /// </summary>
        private void CheckAndRotate()
        {
            try
            {
                lock (writeLock)
                {
                    if (!File.Exists(currentLogFile))
                        return;
                    
                    var fileInfo = new FileInfo(currentLogFile);
                    
                    // Rotación por tamaño
                    if (fileInfo.Length >= MAX_LOG_SIZE_BYTES)
                    {
                        RotateLog("size");
                        return;
                    }
                    
                    // Rotación por tiempo (diaria)
                    var fileAge = DateTime.Now - fileInfo.LastWriteTime;
                    if (fileAge.TotalDays >= 1)
                    {
                        RotateLog("time");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogManager] Error verificando rotación: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Rota el log actual
        /// </summary>
        private void RotateLog(string reason)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var rotatedFile = Path.Combine(logDirectory, $"slskdown_{timestamp}.log");
                
                // Mover archivo actual
                File.Move(currentLogFile, rotatedFile);
                
                Console.WriteLine($"[LogManager] Log rotado ({reason}): {Path.GetFileName(rotatedFile)}");
                
                // Comprimir en background
                Task.Run(() => CompressLogFile(rotatedFile));
                
                totalRotations++;
                
                // Limpiar logs antiguos
                CleanOldLogs();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogManager] Error rotando log: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Comprime un archivo de log con GZIP
        /// </summary>
        private void CompressLogFile(string logFile)
        {
            try
            {
                var compressedFile = logFile + ".gz";
                
                using (var sourceStream = File.OpenRead(logFile))
                using (var destStream = File.Create(compressedFile))
                using (var gzipStream = new GZipStream(destStream, CompressionMode.Compress))
                {
                    sourceStream.CopyTo(gzipStream);
                }
                
                var originalSize = new FileInfo(logFile).Length;
                var compressedSize = new FileInfo(compressedFile).Length;
                var ratio = (1.0 - (double)compressedSize / originalSize) * 100;
                
                totalBytesCompressed += originalSize;
                
                Console.WriteLine($"[LogManager] Comprimido: {Path.GetFileName(logFile)} " +
                                $"({FormatBytes(originalSize)} → {FormatBytes(compressedSize)}, " +
                                $"{ratio:F1}% reducción)");
                
                // Eliminar archivo original
                File.Delete(logFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogManager] Error comprimiendo log: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Limpia logs antiguos
        /// </summary>
        private void CleanOldLogs()
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-MAX_LOG_AGE_DAYS);
                var logFiles = Directory.GetFiles(logDirectory, "slskdown_*.log*")
                    .Select(f => new FileInfo(f))
                    .Where(fi => fi.LastWriteTime < cutoffDate)
                    .OrderBy(fi => fi.LastWriteTime)
                    .ToList();
                
                foreach (var file in logFiles)
                {
                    File.Delete(file.FullName);
                    Console.WriteLine($"[LogManager] Eliminado log antiguo: {file.Name}");
                }
                
                // Limitar número de archivos archivados
                var archivedFiles = Directory.GetFiles(logDirectory, "slskdown_*.log.gz")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(fi => fi.LastWriteTime)
                    .Skip(MAX_ARCHIVED_LOGS)
                    .ToList();
                
                foreach (var file in archivedFiles)
                {
                    File.Delete(file.FullName);
                    Console.WriteLine($"[LogManager] Eliminado archivo excedente: {file.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogManager] Error limpiando logs: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Busca en logs (incluyendo comprimidos)
        /// </summary>
        public List<string> SearchLogs(string searchTerm, int maxResults = 100)
        {
            var results = new List<string>();
            
            try
            {
                // Buscar en log actual
                if (File.Exists(currentLogFile))
                {
                    var lines = File.ReadLines(currentLogFile)
                        .Where(line => line.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                        .Take(maxResults);
                    
                    results.AddRange(lines);
                }
                
                // Buscar en logs comprimidos
                var compressedLogs = Directory.GetFiles(logDirectory, "slskdown_*.log.gz")
                    .OrderByDescending(f => f);
                
                foreach (var compressedLog in compressedLogs)
                {
                    if (results.Count >= maxResults)
                        break;
                    
                    var lines = ReadCompressedLog(compressedLog)
                        .Where(line => line.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                        .Take(maxResults - results.Count);
                    
                    results.AddRange(lines);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogManager] Error buscando en logs: {ex.Message}");
            }
            
            return results;
        }
        
        /// <summary>
        /// Lee un log comprimido
        /// </summary>
        private IEnumerable<string> ReadCompressedLog(string compressedFile)
        {
            using (var fileStream = File.OpenRead(compressedFile))
            using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzipStream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }
        
        /// <summary>
        /// Obtiene estadísticas del log manager
        /// </summary>
        public LogManagerStats GetStats()
        {
            var currentSize = File.Exists(currentLogFile) 
                ? new FileInfo(currentLogFile).Length 
                : 0;
            
            var archivedFiles = Directory.GetFiles(logDirectory, "slskdown_*.log.gz");
            var totalArchivedSize = archivedFiles.Sum(f => new FileInfo(f).Length);
            
            return new LogManagerStats
            {
                TotalBytesWritten = totalBytesWritten,
                TotalLinesWritten = totalLinesWritten,
                TotalBytesCompressed = totalBytesCompressed,
                TotalRotations = totalRotations,
                CurrentLogSize = currentSize,
                ArchivedLogsCount = archivedFiles.Length,
                TotalArchivedSize = totalArchivedSize,
                CompressionRatio = totalBytesCompressed > 0 
                    ? (1.0 - (double)totalArchivedSize / totalBytesCompressed) * 100 
                    : 0
            };
        }
        
        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            
            return $"{len:F2} {sizes[order]}";
        }
        
        public void Dispose()
        {
            rotationTimer?.Dispose();
            Console.WriteLine("[LogManager] Detenido");
        }
    }
    
    /// <summary>
    /// Estadísticas del log manager
    /// </summary>
    public class LogManagerStats
    {
        public long TotalBytesWritten { get; set; }
        public long TotalLinesWritten { get; set; }
        public long TotalBytesCompressed { get; set; }
        public int TotalRotations { get; set; }
        public long CurrentLogSize { get; set; }
        public int ArchivedLogsCount { get; set; }
        public long TotalArchivedSize { get; set; }
        public double CompressionRatio { get; set; }
        
        public override string ToString()
        {
            return $"Log Manager Stats:\n" +
                   $"  Líneas escritas: {TotalLinesWritten:N0}\n" +
                   $"  Bytes escritos: {TotalBytesWritten:N0}\n" +
                   $"  Rotaciones: {TotalRotations}\n" +
                   $"  Logs archivados: {ArchivedLogsCount}\n" +
                   $"  Ratio compresión: {CompressionRatio:F1}%";
        }
    }
}
