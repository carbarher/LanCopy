using System;
using System.IO;
using System.Linq;

namespace SlskDown.Services
{
    /// <summary>
    /// Utilidades para manejo de archivos, formateo y validaciones
    /// </summary>
    public static class FileHelpers
    {
        private static readonly string[] GarbageExtensions = new[]
        {
            ".exe", ".dll", ".msi", ".bat", ".cmd", ".scr", ".vbs", ".js",
            ".jar", ".app", ".deb", ".rpm", ".dmg", ".pkg", ".apk",
            ".torrent", ".magnet", ".nfo", ".sfv", ".m3u", ".pls",
            ".url", ".lnk", ".desktop", ".ini", ".cfg", ".conf",
            ".log", ".tmp", ".temp", ".bak", ".old", ".cache",
            ".db", ".sqlite", ".mdb", ".accdb",
            ".part", ".crdownload", ".download", ".!ut",
            ".DS_Store", ".thumbs.db", "desktop.ini",
            ".ds_store"
        };

        /// <summary>
        /// Formatea un tamaño en bytes a formato legible (KB, MB, GB)
        /// </summary>
        public static string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            else if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F2} KB";
            else if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F2} MB";
            else
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }

        /// <summary>
        /// Formatea una duración en segundos a formato legible (HH:MM:SS o MM:SS)
        /// </summary>
        public static string FormatDuration(int seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.Hours > 0 
                ? $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}" 
                : $"{ts.Minutes}:{ts.Seconds:D2}";
        }

        /// <summary>
        /// Verifica si un archivo es basura (extensiones no deseadas)
        /// </summary>
        public static bool IsGarbageFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return true;

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return GarbageExtensions.Contains(extension) || fileName.ToLowerInvariant().Contains(".ds_store");
        }

        /// <summary>
        /// Verifica si un archivo es un documento (ebook, PDF, etc.)
        /// </summary>
        public static bool IsDocument(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension == ".pdf" || extension == ".epub" || extension == ".mobi" || 
                   extension == ".azw" || extension == ".azw3" || extension == ".djvu" ||
                   extension == ".doc" || extension == ".docx" || extension == ".rtf" ||
                   extension == ".txt" || extension == ".odt";
        }

        /// <summary>
        /// Verifica si un archivo es de audio
        /// </summary>
        public static bool IsAudioFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension == ".mp3" || extension == ".flac" || extension == ".wav" ||
                   extension == ".m4a" || extension == ".aac" || extension == ".ogg" ||
                   extension == ".wma" || extension == ".opus" || extension == ".ape";
        }

        /// <summary>
        /// Verifica si un archivo es de video
        /// </summary>
        public static bool IsVideoFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension == ".mp4" || extension == ".mkv" || extension == ".avi" ||
                   extension == ".mov" || extension == ".wmv" || extension == ".flv" ||
                   extension == ".webm" || extension == ".m4v" || extension == ".mpg" ||
                   extension == ".mpeg";
        }

        /// <summary>
        /// Sanitiza un nombre de archivo removiendo caracteres inválidos
        /// </summary>
        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "unnamed";

            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }

            // Remover caracteres problemáticos adicionales
            fileName = fileName.Replace(":", "_")
                               .Replace("*", "_")
                               .Replace("?", "_")
                               .Replace("\"", "_")
                               .Replace("<", "_")
                               .Replace(">", "_")
                               .Replace("|", "_");

            return fileName;
        }

        /// <summary>
        /// Crea un hardlink entre dos archivos (para deduplicación)
        /// </summary>
        public static bool CreateHardLink(string existingFilePath, string newFilePath)
        {
            try
            {
                // En Windows, usar CreateHardLink de kernel32
                return NativeMethods.CreateHardLink(newFilePath, existingFilePath, IntPtr.Zero);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verifica si dos archivos son el mismo (mismo inode/hardlink)
        /// </summary>
        public static bool AreFilesIdentical(string path1, string path2)
        {
            if (!File.Exists(path1) || !File.Exists(path2))
                return false;

            var info1 = new FileInfo(path1);
            var info2 = new FileInfo(path2);

            // Comparar tamaño y fecha de modificación como heurística rápida
            return info1.Length == info2.Length && 
                   Math.Abs((info1.LastWriteTime - info2.LastWriteTime).TotalSeconds) < 2;
        }

        /// <summary>
        /// Obtiene el directorio de descargas del usuario
        /// </summary>
        public static string GetDefaultDownloadDirectory()
        {
            var downloadsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads"
            );

            if (!Directory.Exists(downloadsPath))
            {
                Directory.CreateDirectory(downloadsPath);
            }

            return downloadsPath;
        }

        /// <summary>
        /// Escribe un archivo con backup automático (inspirado en Nicotine+)
        /// Si existe archivo previo, crea backup antes de escribir
        /// Si falla la escritura, restaura el backup automáticamente
        /// </summary>
        public static void WriteFileWithBackup(string filePath, string content)
        {
            var backupPath = $"{filePath}.backup";

            try
            {
                // Si existe archivo actual, hacer backup
                if (File.Exists(filePath))
                {
                    File.Copy(filePath, backupPath, overwrite: true);
                }

                // Escribir nuevo archivo
                File.WriteAllText(filePath, content);

                // Si escritura exitosa, eliminar backup
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
            catch
            {
                // Si falla, restaurar backup
                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, filePath, overwrite: true);
                }
                throw;
            }
        }

        /// <summary>
        /// Versión asíncrona de WriteFileWithBackup
        /// </summary>
        public static async System.Threading.Tasks.Task WriteFileWithBackupAsync(string filePath, string content)
        {
            var backupPath = $"{filePath}.backup";

            try
            {
                // Si existe archivo actual, hacer backup
                if (File.Exists(filePath))
                {
                    File.Copy(filePath, backupPath, overwrite: true);
                }

                // Escribir nuevo archivo
                await File.WriteAllTextAsync(filePath, content);

                // Si escritura exitosa, eliminar backup
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
            catch
            {
                // Si falla, restaurar backup
                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, filePath, overwrite: true);
                }
                throw;
            }
        }
    }

    /// <summary>
    /// Métodos nativos de Windows para operaciones de bajo nivel
    /// </summary>
    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        internal static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);
    }
}
