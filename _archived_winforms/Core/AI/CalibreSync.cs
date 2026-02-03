using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SlskDown.Core.AI
{
    public class CalibreBook
    {
        public string Title { get; set; }
        public string Author { get; set; }
        public string Path { get; set; }
        public string Format { get; set; }
        public DateTime Added { get; set; }
    }

    /// <summary>
    /// Sincronización con Calibre (detector de biblioteca)
    /// </summary>
    public class CalibreSync
    {
        private string calibreLibraryPath;

        public bool DetectCalibreLibrary()
        {
            // Rutas comunes de Calibre
            var commonPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Calibre Library"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Calibre Library"),
                @"C:\Calibre Library",
                @"D:\Calibre Library"
            };

            foreach (var path in commonPaths)
            {
                if (Directory.Exists(path) && File.Exists(Path.Combine(path, "metadata.db")))
                {
                    calibreLibraryPath = path;
                    return true;
                }
            }

            return false;
        }

        public List<CalibreBook> ScanCalibreLibrary()
        {
            var books = new List<CalibreBook>();

            if (string.IsNullOrEmpty(calibreLibraryPath) || !Directory.Exists(calibreLibraryPath))
                return books;

            try
            {
                // Escanear directorios de autores
                var authorDirs = Directory.GetDirectories(calibreLibraryPath);

                foreach (var authorDir in authorDirs)
                {
                    var author = Path.GetFileName(authorDir);
                    var bookDirs = Directory.GetDirectories(authorDir);

                    foreach (var bookDir in bookDirs)
                    {
                        var title = Path.GetFileName(bookDir);
                        var files = Directory.GetFiles(bookDir);

                        foreach (var file in files)
                        {
                            var ext = Path.GetExtension(file).ToLower();
                            if (ext == ".epub" || ext == ".pdf" || ext == ".mobi" || ext == ".azw3")
                            {
                                books.Add(new CalibreBook
                                {
                                    Title = title,
                                    Author = author,
                                    Path = file,
                                    Format = ext.TrimStart('.'),
                                    Added = File.GetCreationTime(file)
                                });
                            }
                        }
                    }
                }
            }
            catch { }

            return books;
        }

        public List<string> FindMissingInCalibre(List<string> downloadedFiles)
        {
            var calibreBooks = ScanCalibreLibrary();
            var calibreTitles = new HashSet<string>(
                calibreBooks.Select(b => NormalizeTitle(b.Title)),
                StringComparer.OrdinalIgnoreCase
            );

            var missing = new List<string>();

            foreach (var file in downloadedFiles)
            {
                var normalized = NormalizeTitle(Path.GetFileNameWithoutExtension(file));
                if (!calibreTitles.Contains(normalized))
                {
                    missing.Add(file);
                }
            }

            return missing;
        }

        private string NormalizeTitle(string title)
        {
            // Remover caracteres especiales y normalizar
            return System.Text.RegularExpressions.Regex.Replace(title, @"[^\w\s]", "")
                .ToLower()
                .Trim();
        }

        public string GenerateSyncReport(List<CalibreBook> calibreBooks, List<string> newFiles)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("📚 SINCRONIZACIÓN CON CALIBRE\n");

            if (string.IsNullOrEmpty(calibreLibraryPath))
            {
                sb.AppendLine("❌ No se detectó biblioteca de Calibre");
                sb.AppendLine("\nRutas buscadas:");
                sb.AppendLine("  • ~/Calibre Library");
                sb.AppendLine("  • C:\\Calibre Library");
                sb.AppendLine("\n¿Quieres especificar la ruta manualmente?");
                return sb.ToString();
            }

            sb.AppendLine($"✅ Biblioteca detectada: {calibreLibraryPath}\n");
            sb.AppendLine($"📊 ESTADÍSTICAS:");
            sb.AppendLine($"  • Libros en Calibre: {calibreBooks.Count}");
            sb.AppendLine($"  • Archivos nuevos: {newFiles.Count}");

            var missing = FindMissingInCalibre(newFiles);
            if (missing.Count > 0)
            {
                sb.AppendLine($"  • No están en Calibre: {missing.Count}\n");
                sb.AppendLine("📥 ARCHIVOS PARA IMPORTAR:");
                foreach (var file in missing.Take(10))
                {
                    sb.AppendLine($"  • {Path.GetFileName(file)}");
                }
                if (missing.Count > 10)
                    sb.AppendLine($"  ... y {missing.Count - 10} más");
            }

            return sb.ToString();
        }

        public bool ExportToCalibre(string filePath)
        {
            if (string.IsNullOrEmpty(calibreLibraryPath))
                return false;

            try
            {
                // Aquí se podría integrar con calibredb CLI
                // Por ahora, solo copiamos a una carpeta de importación
                var importFolder = Path.Combine(calibreLibraryPath, "_import");
                Directory.CreateDirectory(importFolder);

                var destPath = Path.Combine(importFolder, Path.GetFileName(filePath));
                File.Copy(filePath, destPath, true);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public string GetCalibreLibraryPath() => calibreLibraryPath;

        public void SetCalibreLibraryPath(string path)
        {
            if (Directory.Exists(path))
                calibreLibraryPath = path;
        }
    }
}
