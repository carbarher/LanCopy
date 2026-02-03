using System;
using System.IO;
using System.Linq;
using System.Text;

namespace SlskDown.Services
{
    public sealed class QuarantineService
    {
        private readonly string _quarantineLogPath;
        private readonly string _logDir;
        private bool? _useExtendedFormat;

        private const long MaxQuarantineLogBytes = 10 * 1024 * 1024;
        private const int MaxQuarantineLogBackups = 5;

        public QuarantineService(string dataDir)
        {
            if (string.IsNullOrWhiteSpace(dataDir))
            {
                throw new ArgumentException("dataDir is required", nameof(dataDir));
            }

            _quarantineLogPath = Path.Combine(dataDir, "quarantine_log.csv");
            _logDir = dataDir;
        }

        public void AppendLog(string action, string fileName, string originalPath, string newPath, string detector)
        {
            AppendLog(action, fileName, originalPath, newPath, detector, reason: null, sizeBytes: null, username: null, authorGroup: null);
        }

        public void AppendLog(
            string action,
            string fileName,
            string originalPath,
            string newPath,
            string detector,
            string? reason,
            long? sizeBytes,
            string? username,
            string? authorGroup)
        {
            try
            {
                RotateIfTooLarge();
                EnsureLogHeader();

                if (_useExtendedFormat == true)
                {
                    var line = string.Join(",", new[]
                    {
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        EscapeCsv(action),
                        EscapeCsv(fileName),
                        EscapeCsv(originalPath),
                        EscapeCsv(newPath),
                        EscapeCsv(detector),
                        EscapeCsv(reason ?? string.Empty),
                        EscapeCsv(sizeBytes?.ToString() ?? string.Empty),
                        EscapeCsv(username ?? string.Empty),
                        EscapeCsv(authorGroup ?? string.Empty)
                    });
                    File.AppendAllText(_quarantineLogPath, line + Environment.NewLine, Encoding.UTF8);
                    return;
                }

                var legacyLine = string.Join(",", new[]
                {
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    EscapeCsv(action),
                    EscapeCsv(fileName),
                    EscapeCsv(originalPath),
                    EscapeCsv(newPath),
                    EscapeCsv(detector)
                });
                File.AppendAllText(_quarantineLogPath, legacyLine + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
            }
        }

        private void RotateIfTooLarge()
        {
            try
            {
                if (!File.Exists(_quarantineLogPath))
                {
                    return;
                }

                var info = new FileInfo(_quarantineLogPath);
                if (info.Length <= MaxQuarantineLogBytes)
                {
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(_quarantineLogPath) ?? _logDir);

                var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var rotated = Path.Combine(_logDir, $"quarantine_log_{stamp}.csv");
                File.Move(_quarantineLogPath, rotated);

                var backups = Directory.GetFiles(_logDir, "quarantine_log_*.csv")
                    .Select(p => new FileInfo(p))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .Skip(MaxQuarantineLogBackups)
                    .ToList();

                foreach (var b in backups)
                {
                    try
                    {
                        b.Delete();
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        public void EnsureInitialized()
        {
            EnsureLogHeader();
        }

        public string MoveToNonSpanish(string filePath, string rootDir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    return filePath;
                }

                if (string.IsNullOrWhiteSpace(rootDir))
                {
                    return filePath;
                }

                var rootFull = Path.GetFullPath(rootDir);
                var fileFull = Path.GetFullPath(filePath);
                var targetDir = Path.Combine(rootFull, "Otros_Idiomas");
                var targetDirFull = Path.GetFullPath(targetDir);

                if (fileFull.StartsWith(targetDirFull, StringComparison.OrdinalIgnoreCase))
                {
                    return filePath;
                }

                Directory.CreateDirectory(targetDirFull);

                var fileName = Path.GetFileName(fileFull);
                var baseName = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName);
                var candidatePath = Path.Combine(targetDirFull, fileName);
                var counter = 1;

                while (File.Exists(candidatePath))
                {
                    candidatePath = Path.Combine(targetDirFull, $"{baseName}_{counter}{extension}");
                    counter++;
                }

                File.Move(fileFull, candidatePath);
                return candidatePath;
            }
            catch
            {
                return filePath;
            }
        }

        public string MoveToCorrupt(string filePath, string downloadDir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    return filePath;
                }

                var fileName = Path.GetFileName(filePath);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    return filePath;
                }

                var quarantineDir = Path.Combine(downloadDir, "_Corruptos");
                Directory.CreateDirectory(quarantineDir);

                var targetPath = Path.Combine(quarantineDir, fileName);
                if (File.Exists(targetPath))
                {
                    var baseName = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    targetPath = Path.Combine(quarantineDir, $"{baseName}__{stamp}{ext}");
                }

                File.Move(filePath, targetPath);
                return targetPath;
            }
            catch
            {
                return filePath;
            }
        }

        private void EnsureLogHeader()
        {
            try
            {
                if (_useExtendedFormat == null && File.Exists(_quarantineLogPath))
                {
                    var firstLine = File.ReadLines(_quarantineLogPath, Encoding.UTF8).FirstOrDefault() ?? string.Empty;
                    var columns = firstLine.Split(',');
                    _useExtendedFormat = columns.Length >= 10;
                }

                if (!File.Exists(_quarantineLogPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_quarantineLogPath) ?? _logDir);
                    File.WriteAllText(_quarantineLogPath, "timestamp,action,file_name,original_path,new_path,detector,reason,size_bytes,username,author_group\n", Encoding.UTF8);
                    _useExtendedFormat = true;
                }

                _useExtendedFormat ??= true;
            }
            catch
            {
            }
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Contains('"'))
            {
                value = value.Replace("\"", "\"\"");
            }

            return value.IndexOfAny(new[] { ',', '\n', '\r' }) >= 0 ? $"\"{value}\"" : value;
        }
    }
}
