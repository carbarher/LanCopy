using System;
using System.IO;
using System.Text.RegularExpressions;

namespace SlskDown
{
    /// <summary>
    /// Organizador automático de archivos por autor
    /// Normaliza nombres de autor: sin puntos, con espacios separadores
    /// Ejemplo: "A. A. Pepe" → "a a pepe"
    /// </summary>
    public class FileOrganizer
    {
        private readonly string baseDirectory;
        
        // Estadísticas
        private long totalFilesOrganized = 0;
        private long totalDirectoriesCreated = 0;
        private long totalFilesMoved = 0;
        private long totalErrors = 0;
        
        public FileOrganizer(string baseDirectory)
        {
            this.baseDirectory = baseDirectory;
            
            // Crear directorio base si no existe
            Directory.CreateDirectory(baseDirectory);
            
            Console.WriteLine($"[FileOrganizer] Inicializado: {baseDirectory}");
        }
        
        /// <summary>
        /// Organiza un archivo descargado por autor
        /// </summary>
        public OrganizeResult OrganizeFile(string filePath, string author)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new OrganizeResult
                    {
                        Success = false,
                        ErrorMessage = "Archivo no existe",
                        OriginalPath = filePath
                    };
                }
                
                if (string.IsNullOrWhiteSpace(author))
                {
                    return new OrganizeResult
                    {
                        Success = false,
                        ErrorMessage = "Autor no especificado",
                        OriginalPath = filePath
                    };
                }
                
                // Normalizar nombre de autor
                string normalizedAuthor = NormalizeAuthorName(author);
                
                // Crear directorio del autor
                string authorDirectory = Path.Combine(baseDirectory, normalizedAuthor);
                
                if (!Directory.Exists(authorDirectory))
                {
                    Directory.CreateDirectory(authorDirectory);
                    totalDirectoriesCreated++;
                    Console.WriteLine($"[FileOrganizer] 📁 Directorio creado: {normalizedAuthor}");
                }
                
                // Ruta destino
                string fileName = Path.GetFileName(filePath);
                string destinationPath = Path.Combine(authorDirectory, fileName);
                
                // Verificar si ya existe
                if (File.Exists(destinationPath))
                {
                    // Generar nombre único
                    destinationPath = GenerateUniquePath(destinationPath);
                }
                
                // Mover archivo
                File.Move(filePath, destinationPath);
                totalFilesMoved++;
                totalFilesOrganized++;
                
                Console.WriteLine($"[FileOrganizer] ✅ Organizado: {fileName} → {normalizedAuthor}/");
                
                return new OrganizeResult
                {
                    Success = true,
                    OriginalPath = filePath,
                    NewPath = destinationPath,
                    AuthorDirectory = authorDirectory,
                    NormalizedAuthor = normalizedAuthor
                };
            }
            catch (Exception ex)
            {
                totalErrors++;
                Console.WriteLine($"[FileOrganizer] ❌ Error organizando: {ex.Message}");
                
                return new OrganizeResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    OriginalPath = filePath
                };
            }
        }
        
        /// <summary>
        /// Normaliza el nombre del autor según las reglas:
        /// - Sin puntos
        /// - Con espacios separadores
        /// - Respetando mayúsculas/minúsculas
        /// - Sin caracteres especiales
        /// </summary>
        public string NormalizeAuthorName(string author)
        {
            if (string.IsNullOrWhiteSpace(author))
                return "Desconocido";
            
            // NO convertir a minúsculas - respetar mayúsculas originales
            string normalized = author;
            
            // Eliminar puntos
            normalized = normalized.Replace(".", "");
            
            // Reemplazar múltiples espacios por uno solo
            normalized = Regex.Replace(normalized, @"\s+", " ");
            
            // Eliminar espacios al inicio y final
            normalized = normalized.Trim();
            
            // Eliminar caracteres no válidos para nombres de carpeta
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                normalized = normalized.Replace(c.ToString(), "");
            }
            
            // Reemplazar caracteres especiales comunes
            normalized = normalized.Replace("/", " ");
            normalized = normalized.Replace("\\", " ");
            normalized = normalized.Replace(":", "");
            normalized = normalized.Replace("*", "");
            normalized = normalized.Replace("?", "");
            normalized = normalized.Replace("\"", "");
            normalized = normalized.Replace("<", "");
            normalized = normalized.Replace(">", "");
            normalized = normalized.Replace("|", "");
            
            // Normalizar espacios múltiples nuevamente
            normalized = Regex.Replace(normalized, @"\s+", " ");
            normalized = normalized.Trim();
            
            // Si quedó vacío, usar "Desconocido"
            if (string.IsNullOrWhiteSpace(normalized))
                return "Desconocido";
            
            return normalized;
        }
        
        /// <summary>
        /// Genera una ruta única si el archivo ya existe
        /// </summary>
        private string GenerateUniquePath(string path)
        {
            if (!File.Exists(path))
                return path;
            
            string directory = Path.GetDirectoryName(path);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            
            int counter = 1;
            string newPath;
            
            do
            {
                newPath = Path.Combine(directory, $"{fileNameWithoutExt} ({counter}){extension}");
                counter++;
            }
            while (File.Exists(newPath));
            
            return newPath;
        }
        
        /// <summary>
        /// Organiza múltiples archivos en lote
        /// </summary>
        public BatchOrganizeResult OrganizeBatch(string[] filePaths, string[] authors)
        {
            if (filePaths.Length != authors.Length)
            {
                throw new ArgumentException("El número de archivos y autores debe coincidir");
            }
            
            var result = new BatchOrganizeResult
            {
                TotalFiles = filePaths.Length,
                Results = new OrganizeResult[filePaths.Length]
            };
            
            for (int i = 0; i < filePaths.Length; i++)
            {
                result.Results[i] = OrganizeFile(filePaths[i], authors[i]);
                
                if (result.Results[i].Success)
                    result.SuccessCount++;
                else
                    result.FailureCount++;
            }
            
            Console.WriteLine($"[FileOrganizer] 📊 Lote completado: {result.SuccessCount}/{result.TotalFiles} exitosos");
            
            return result;
        }
        
        /// <summary>
        /// Obtiene el directorio de un autor (normalizado)
        /// </summary>
        public string GetAuthorDirectory(string author)
        {
            string normalizedAuthor = NormalizeAuthorName(author);
            return Path.Combine(baseDirectory, normalizedAuthor);
        }
        
        /// <summary>
        /// Verifica si un autor tiene directorio
        /// </summary>
        public bool AuthorDirectoryExists(string author)
        {
            string authorDir = GetAuthorDirectory(author);
            return Directory.Exists(authorDir);
        }
        
        /// <summary>
        /// Cuenta archivos de un autor
        /// </summary>
        public int CountAuthorFiles(string author)
        {
            string authorDir = GetAuthorDirectory(author);
            
            if (!Directory.Exists(authorDir))
                return 0;
            
            return Directory.GetFiles(authorDir).Length;
        }
        
        /// <summary>
        /// Lista todos los autores organizados
        /// </summary>
        public string[] GetOrganizedAuthors()
        {
            if (!Directory.Exists(baseDirectory))
                return new string[0];
            
            var directories = Directory.GetDirectories(baseDirectory);
            var authors = new string[directories.Length];
            
            for (int i = 0; i < directories.Length; i++)
            {
                authors[i] = Path.GetFileName(directories[i]);
            }
            
            return authors;
        }
        
        /// <summary>
        /// Obtiene estadísticas del organizador
        /// </summary>
        public OrganizerStats GetStats()
        {
            int totalAuthors = 0;
            long totalSize = 0;
            
            if (Directory.Exists(baseDirectory))
            {
                var authorDirs = Directory.GetDirectories(baseDirectory);
                totalAuthors = authorDirs.Length;
                
                foreach (var dir in authorDirs)
                {
                    var files = Directory.GetFiles(dir);
                    foreach (var file in files)
                    {
                        try
                        {
                            totalSize += new FileInfo(file).Length;
                        }
                        catch { }
                    }
                }
            }
            
            return new OrganizerStats
            {
                TotalFilesOrganized = totalFilesOrganized,
                TotalDirectoriesCreated = totalDirectoriesCreated,
                TotalFilesMoved = totalFilesMoved,
                TotalErrors = totalErrors,
                TotalAuthors = totalAuthors,
                TotalSizeBytes = totalSize
            };
        }
        
        /// <summary>
        /// Ejemplos de normalización
        /// </summary>
        public static void ShowNormalizationExamples()
        {
            var organizer = new FileOrganizer("temp");
            
            Console.WriteLine("Ejemplos de normalización (respetando mayúsculas):");
            Console.WriteLine($"  'A. A. Pepe' → '{organizer.NormalizeAuthorName("A. A. Pepe")}'");
            Console.WriteLine($"  'Jorge Luis Borges' → '{organizer.NormalizeAuthorName("Jorge Luis Borges")}'");
            Console.WriteLine($"  'J.R.R. Tolkien' → '{organizer.NormalizeAuthorName("J.R.R. Tolkien")}'");
            Console.WriteLine($"  'Gabriel García Márquez' → '{organizer.NormalizeAuthorName("Gabriel García Márquez")}'");
            Console.WriteLine($"  'Stephen   King' → '{organizer.NormalizeAuthorName("Stephen   King")}'");
            Console.WriteLine($"  'C. S. Lewis' → '{organizer.NormalizeAuthorName("C. S. Lewis")}'");
            Console.WriteLine($"  'a. a. pepe' → '{organizer.NormalizeAuthorName("a. a. pepe")}'");
        }
    }
    
    /// <summary>
    /// Resultado de organización de un archivo
    /// </summary>
    public class OrganizeResult
    {
        public bool Success { get; set; }
        public string OriginalPath { get; set; }
        public string NewPath { get; set; }
        public string AuthorDirectory { get; set; }
        public string NormalizedAuthor { get; set; }
        public string ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// Resultado de organización en lote
    /// </summary>
    public class BatchOrganizeResult
    {
        public int TotalFiles { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public OrganizeResult[] Results { get; set; }
        
        public double SuccessRate => TotalFiles > 0 
            ? (double)SuccessCount / TotalFiles * 100 
            : 0;
    }
    
    /// <summary>
    /// Estadísticas del organizador
    /// </summary>
    public class OrganizerStats
    {
        public long TotalFilesOrganized { get; set; }
        public long TotalDirectoriesCreated { get; set; }
        public long TotalFilesMoved { get; set; }
        public long TotalErrors { get; set; }
        public int TotalAuthors { get; set; }
        public long TotalSizeBytes { get; set; }
        
        public override string ToString()
        {
            return $"File Organizer Stats:\n" +
                   $"  Archivos organizados: {TotalFilesOrganized:N0}\n" +
                   $"  Directorios creados: {TotalDirectoriesCreated:N0}\n" +
                   $"  Archivos movidos: {TotalFilesMoved:N0}\n" +
                   $"  Errores: {TotalErrors:N0}\n" +
                   $"  Autores: {TotalAuthors:N0}\n" +
                   $"  Tamaño total: {TotalSizeBytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
        }
    }
}
