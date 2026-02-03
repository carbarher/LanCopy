using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// MEJORA #8 (Nicotine+ 3.3.8): Share Scanning con Progreso Granular
    /// Muestra progreso detallado durante el escaneo de carpetas compartidas
    /// </summary>
    public class ShareScannerWithProgress
    {
        private readonly Action<string> log;
        
        public event Action<ScanProgressInfo> OnProgress;
        public event Action<ScanResult> OnCompleted;
        public event Action<Exception> OnError;

        public ShareScannerWithProgress(Action<string> log = null)
        {
            this.log = log;
        }

        /// <summary>
        /// Escanea carpetas compartidas con reporte de progreso
        /// </summary>
        public async Task<ScanResult> ScanFoldersAsync(
            List<string> folders, 
            CancellationToken cancellationToken = default)
        {
            var result = new ScanResult
            {
                StartTime = DateTime.Now
            };

            try
            {
                log?.Invoke($"Iniciando escaneo de {folders.Count} carpetas...");

                // Fase 1: Contar archivos totales (para progreso preciso)
                var totalFiles = await CountFilesAsync(folders, cancellationToken);
                log?.Invoke($"Total de archivos a procesar: {totalFiles:N0}");

                // Fase 2: Escanear carpetas
                int processedFiles = 0;
                int processedFolders = 0;

                foreach (var folder in folders)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (!Directory.Exists(folder))
                    {
                        log?.Invoke($"Carpeta no existe: {folder}");
                        continue;
                    }

                    // Escanear carpeta
                    var folderResult = await ScanFolderAsync(
                        folder, 
                        totalFiles, 
                        processedFiles,
                        cancellationToken);

                    result.Merge(folderResult);
                    processedFiles = folderResult.ProcessedFiles;
                    processedFolders++;

                    // Reportar progreso por carpeta
                    OnProgress?.Invoke(new ScanProgressInfo
                    {
                        CurrentFolder = folder,
                        ProcessedFiles = processedFiles,
                        TotalFiles = totalFiles,
                        ProcessedFolders = processedFolders,
                        TotalFolders = folders.Count,
                        ElapsedTime = DateTime.Now - result.StartTime
                    });
                }

                result.EndTime = DateTime.Now;
                result.Success = !cancellationToken.IsCancellationRequested;

                log?.Invoke($"Escaneo completado: {result.FilesScanned:N0} archivos en {result.Duration.TotalSeconds:F1}s");
                log?.Invoke($"   Carpetas: {result.FoldersScanned}");
                log?.Invoke($"   Tamaño total: {FormatFileSize(result.TotalSize)}");
                log?.Invoke($"   Velocidad: {result.FilesPerSecond:F0} archivos/s");

                OnCompleted?.Invoke(result);
                return result;
            }
            catch (Exception ex)
            {
                log?.Invoke($"Error en escaneo: {ex.Message}");
                OnError?.Invoke(ex);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private async Task<int> CountFilesAsync(List<string> folders, CancellationToken cancellationToken)
        {
            int count = 0;

            await Task.Run(() =>
            {
                foreach (var folder in folders)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (!Directory.Exists(folder))
                        continue;

                    try
                    {
                        count += Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Count();
                    }
                    catch
                    {
                        // Ignorar errores de acceso
                    }
                }
            }, cancellationToken);

            return count;
        }

        private async Task<ScanResult> ScanFolderAsync(
            string folder, 
            int totalFiles,
            int processedFiles,
            CancellationToken cancellationToken)
        {
            var result = new ScanResult();

            await Task.Run(() =>
            {
                try
                {
                    var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories);

                    foreach (var file in files)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        try
                        {
                            var fileInfo = new FileInfo(file);
                            result.FilesScanned++;
                            result.TotalSize += fileInfo.Length;
                            processedFiles++;

                            // Reportar progreso cada 100 archivos
                            if (processedFiles % 100 == 0)
                            {
                                OnProgress?.Invoke(new ScanProgressInfo
                                {
                                    CurrentFolder = folder,
                                    CurrentFile = fileInfo.Name,
                                    ProcessedFiles = processedFiles,
                                    TotalFiles = totalFiles,
                                    ElapsedTime = DateTime.Now - result.StartTime
                                });
                            }
                        }
                        catch
                        {
                            // Ignorar archivos inaccesibles
                        }
                    }

                    result.FoldersScanned++;
                }
                catch (Exception ex)
                {
                    log?.Invoke($"Error escaneando {folder}: {ex.Message}");
                }
            }, cancellationToken);

            result.ProcessedFiles = processedFiles;
            return result;
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        public class ScanProgressInfo
        {
            public string CurrentFolder { get; set; }
            public string CurrentFile { get; set; }
            public int ProcessedFiles { get; set; }
            public int TotalFiles { get; set; }
            public int ProcessedFolders { get; set; }
            public int TotalFolders { get; set; }
            public TimeSpan ElapsedTime { get; set; }

            public double PercentComplete => TotalFiles > 0 ? (ProcessedFiles * 100.0 / TotalFiles) : 0;
            
            public TimeSpan EstimatedTimeRemaining
            {
                get
                {
                    if (ProcessedFiles == 0 || ElapsedTime.TotalSeconds == 0)
                        return TimeSpan.Zero;

                    var rate = ProcessedFiles / ElapsedTime.TotalSeconds;
                    var remaining = TotalFiles - ProcessedFiles;
                    return TimeSpan.FromSeconds(remaining / rate);
                }
            }

            public string DisplayText => 
                $"{CurrentFolder}\n" +
                $"{ProcessedFiles:N0}/{TotalFiles:N0} archivos ({PercentComplete:F1}%)\n" +
                $"{ElapsedTime.TotalSeconds:F0}s transcurridos, ~{EstimatedTimeRemaining.TotalSeconds:F0}s restantes";
        }

        public class ScanResult
        {
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public int FilesScanned { get; set; }
            public int FoldersScanned { get; set; }
            public long TotalSize { get; set; }
            public int ProcessedFiles { get; set; }
            public bool Success { get; set; }
            public string ErrorMessage { get; set; }

            public TimeSpan Duration => EndTime - StartTime;
            public double FilesPerSecond => Duration.TotalSeconds > 0 ? FilesScanned / Duration.TotalSeconds : 0;

            public void Merge(ScanResult other)
            {
                FilesScanned += other.FilesScanned;
                FoldersScanned += other.FoldersScanned;
                TotalSize += other.TotalSize;
                ProcessedFiles += other.ProcessedFiles;
            }
        }
    }
}
