using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// IntegraciÃ³n con Calibre para gestiÃ³n de biblioteca de eBooks
    /// </summary>
    public class CalibreIntegration
    {
        private readonly string _calibreDbPath;
        private readonly string _calibreExePath;
        private readonly Services.ILoggingService? _logger;
        private bool _isAvailable;

        public bool IsAvailable => _isAvailable;

        public CalibreIntegration(string calibreDbPath = "", Services.ILoggingService? logger = null)
        {
            _logger = logger;
            
            // Detectar instalaciÃ³n de Calibre
            _calibreExePath = FindCalibreExecutable();
            
            // Usar ruta de biblioteca por defecto o personalizada
            _calibreDbPath = string.IsNullOrEmpty(calibreDbPath) 
                ? GetDefaultCalibreLibrary() 
                : calibreDbPath;
            
            _isAvailable = !string.IsNullOrEmpty(_calibreExePath) && 
                           !string.IsNullOrEmpty(_calibreDbPath);
            
            if (_isAvailable)
            {
                _logger?.Info($"Calibre detectado: {_calibreExePath}");
                _logger?.Info($"Biblioteca: {_calibreDbPath}");
            }
            else
            {
                _logger?.Warning("Calibre no detectado o no configurado");
            }
        }

        /// <summary>
        /// Busca el ejecutable de Calibre en ubicaciones comunes
        /// </summary>
        private string FindCalibreExecutable()
        {
            var possiblePaths = new[]
            {
                @"C:\Program Files\Calibre2\calibredb.exe",
                @"C:\Program Files (x86)\Calibre2\calibredb.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Calibre2\calibredb.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Calibre2\calibredb.exe")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // Buscar en PATH
            try
            {
                var result = RunCommand("where", "calibredb");
                if (!string.IsNullOrEmpty(result))
                {
                    return result.Split('\n')[0].Trim();
                }
            }
            catch { }

            return string.Empty;
        }

        /// <summary>
        /// Obtiene la ruta de la biblioteca por defecto de Calibre
        /// </summary>
        private string GetDefaultCalibreLibrary()
        {
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var defaultPath = Path.Combine(documentsPath, "Calibre Library");
            
            return Directory.Exists(defaultPath) ? defaultPath : string.Empty;
        }

        /// <summary>
        /// Agrega un libro a Calibre
        /// </summary>
        /// <param name="filePath">Ruta del archivo</param>
        /// <param name="author">Autor del libro</param>
        /// <param name="title">TÃ­tulo del libro (opcional)</param>
        /// <param name="tags">Tags/etiquetas (opcional)</param>
        /// <returns>True si se agregÃ³ correctamente</returns>
        public async Task<bool> AddBookAsync(
            string filePath, 
            string author = "", 
            string title = "",
            string[] tags = null)
        {
            if (!_isAvailable)
            {
                _logger?.Warning("Calibre no estÃ¡ disponible");
                return false;
            }

            if (!File.Exists(filePath))
            {
                _logger?.Error($"Archivo no encontrado: {filePath}");
                return false;
            }

            try
            {
                var args = new List<string>
                {
                    "add",
                    $"--library-path=\"{_calibreDbPath}\"",
                    $"\"{filePath}\""
                };

                // Agregar autor si se especifica
                if (!string.IsNullOrEmpty(author))
                {
                    args.Add($"--authors=\"{author}\"");
                }

                // Agregar tÃ­tulo si se especifica
                if (!string.IsNullOrEmpty(title))
                {
                    args.Add($"--title=\"{title}\"");
                }

                // Agregar tags si se especifican
                if (tags != null && tags.Length > 0)
                {
                    var tagsStr = string.Join(",", tags);
                    args.Add($"--tags=\"{tagsStr}\"");
                }

                var result = await Task.Run(() => RunCalibreCommand(string.Join(" ", args)));
                
                if (result.Contains("Added book ids"))
                {
                    _logger?.Info($"Libro agregado a Calibre: {Path.GetFileName(filePath)}");
                    return true;
                }
                else
                {
                    _logger?.Warning($"No se pudo agregar a Calibre: {result}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error agregando libro a Calibre: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Busca libros en Calibre
        /// </summary>
        /// <param name="query">TÃ©rmino de bÃºsqueda</param>
        /// <returns>Lista de libros encontrados</returns>
        public List<CalibreBook> SearchBooks(string query)
        {
            if (!_isAvailable)
                return new List<CalibreBook>();

            try
            {
                var args = $"list --library-path=\"{_calibreDbPath}\" --search=\"{query}\" --for-machine";
                var result = RunCalibreCommand(args);
                
                if (string.IsNullOrEmpty(result))
                    return new List<CalibreBook>();

                // Parsear resultado JSON
                var books = JsonSerializer.Deserialize<List<CalibreBook>>(result);
                return books ?? new List<CalibreBook>();
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error buscando en Calibre: {ex.Message}", ex);
                return new List<CalibreBook>();
            }
        }

        /// <summary>
        /// Obtiene informaciÃ³n de un libro por ID
        /// </summary>
        public CalibreBook? GetBookById(int bookId)
        {
            if (!_isAvailable)
                return null;

            try
            {
                var args = $"show_metadata --library-path=\"{_calibreDbPath}\" {bookId} --as-opf";
                var result = RunCalibreCommand(args);
                
                // Parsear XML OPF (simplificado)
                return ParseOPF(result, bookId);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error obteniendo metadata: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Actualiza metadata de un libro
        /// </summary>
        public bool UpdateMetadata(int bookId, string field, string value)
        {
            if (!_isAvailable)
                return false;

            try
            {
                var args = $"set_metadata --library-path=\"{_calibreDbPath}\" {bookId} --field=\"{field}:{value}\"";
                var result = RunCalibreCommand(args);
                
                return !result.Contains("Error");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error actualizando metadata: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Abre Calibre en un libro especÃ­fico
        /// </summary>
        public void OpenInCalibre(int bookId)
        {
            if (!_isAvailable)
                return;

            try
            {
                var calibreGuiPath = _calibreExePath.Replace("calibredb.exe", "calibre.exe");
                
                if (File.Exists(calibreGuiPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = calibreGuiPath,
                        Arguments = $"--library-path=\"{_calibreDbPath}\" --select-book={bookId}",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error abriendo Calibre: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Obtiene estadÃ­sticas de la biblioteca
        /// </summary>
        public CalibreStats GetLibraryStats()
        {
            if (!_isAvailable)
                return new CalibreStats();

            try
            {
                var args = $"list --library-path=\"{_calibreDbPath}\" --for-machine";
                var result = RunCalibreCommand(args);
                
                if (string.IsNullOrEmpty(result))
                    return new CalibreStats();

                var books = JsonSerializer.Deserialize<List<CalibreBook>>(result);
                
                return new CalibreStats
                {
                    TotalBooks = books?.Count ?? 0,
                    Authors = books?.Select(b => b.Authors).Distinct().Count() ?? 0,
                    Tags = books?.SelectMany(b => b.Tags ?? Array.Empty<string>()).Distinct().Count() ?? 0
                };
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error obteniendo estadÃ­sticas: {ex.Message}", ex);
                return new CalibreStats();
            }
        }

        /// <summary>
        /// Ejecuta un comando de Calibre
        /// </summary>
        private string RunCalibreCommand(string arguments)
        {
            return RunCommand(_calibreExePath, arguments);
        }

        /// <summary>
        /// Ejecuta un comando del sistema
        /// </summary>
        private string RunCommand(string command, string arguments)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return string.IsNullOrEmpty(error) ? output : error;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error ejecutando comando: {ex.Message}", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Parsea XML OPF (simplificado)
        /// </summary>
        private CalibreBook? ParseOPF(string opf, int bookId)
        {
            // ImplementaciÃ³n simplificada
            // En producciÃ³n usar XDocument para parsear XML correctamente
            return new CalibreBook
            {
                Id = bookId,
                Title = ExtractXmlValue(opf, "dc:title"),
                Authors = ExtractXmlValue(opf, "dc:creator"),
                Tags = ExtractXmlValue(opf, "dc:subject").Split(',')
            };
        }

        private string ExtractXmlValue(string xml, string tag)
        {
            var startTag = $"<{tag}>";
            var endTag = $"</{tag}>";
            var start = xml.IndexOf(startTag);
            var end = xml.IndexOf(endTag);
            
            if (start >= 0 && end > start)
            {
                return xml.Substring(start + startTag.Length, end - start - startTag.Length);
            }
            
            return string.Empty;
        }
    }

    /// <summary>
    /// Representa un libro en Calibre
    /// </summary>
    public class CalibreBook
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Authors { get; set; } = string.Empty;
        public string[] Tags { get; set; } = Array.Empty<string>();
        public string Format { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public DateTime DateAdded { get; set; }
    }

    /// <summary>
    /// EstadÃ­sticas de la biblioteca de Calibre
    /// </summary>
    public class CalibreStats
    {
        public int TotalBooks { get; set; }
        public int Authors { get; set; }
        public int Tags { get; set; }
    }
}

