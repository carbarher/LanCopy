using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SlskDown.Core.Integrations
{
    /// <summary>
    /// Integración con Calibre para gestión de biblioteca de ebooks
    /// </summary>
    public class CalibreIntegration
    {
        private string calibreLibraryPath;
        private string calibreDbPath;
        private bool calibreAvailable;

        public event Action<string> OnLog;

        public CalibreIntegration()
        {
            LoadSavedPath();
            if (string.IsNullOrEmpty(calibreLibraryPath))
            {
                DetectCalibre();
            }
        }

        /// <summary>
        /// Detecta si Calibre está instalado y encuentra la biblioteca
        /// </summary>
        private void DetectCalibre()
        {
            try
            {
                // Buscar en ubicaciones comunes
                var possiblePaths = new List<string>
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Calibre Library"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Calibre Library"),
                    @"C:\Calibre Library",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "Calibre Library"),
                    @"D:\Calibre Library",
                    @"E:\Calibre Library",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Calibre Library"),
                    Path.Combine(@"C:\Users", Environment.UserName, "Calibre Library")
                };

                // Buscar en todas las unidades disponibles
                try
                {
                    var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed);
                    foreach (var drive in drives)
                    {
                        possiblePaths.Add(Path.Combine(drive.RootDirectory.FullName, "Calibre Library"));
                    }
                }
                catch { }

                Log($"Buscando biblioteca de Calibre en {possiblePaths.Count} ubicaciones...");

                foreach (var path in possiblePaths)
                {
                    try
                    {
                        var dbPath = Path.Combine(path, "metadata.db");
                        if (File.Exists(dbPath))
                        {
                            calibreLibraryPath = path;
                            calibreDbPath = dbPath;
                            calibreAvailable = true;
                            Log($"Calibre detectado: {path}");
                            return;
                        }
                    }
                    catch { }
                }

                Log("Calibre no detectado automáticamente en ubicaciones estándar");
                Log("Usa 'Configurar Ruta' para especificar la ubicación de tu biblioteca");
                calibreAvailable = false;
            }
            catch (Exception ex)
            {
                Log($"Error detectando Calibre: {ex.Message}");
                calibreAvailable = false;
            }
        }

        /// <summary>
        /// Configura manualmente la ruta de la biblioteca Calibre
        /// </summary>
        public bool SetLibraryPath(string path)
        {
            try
            {
                var dbPath = Path.Combine(path, "metadata.db");
                if (!File.Exists(dbPath))
                {
                    Log($"No se encontró metadata.db en: {path}");
                    return false;
                }

                calibreLibraryPath = path;
                calibreDbPath = dbPath;
                calibreAvailable = true;
                SavePath(path);
                Log($"Biblioteca Calibre configurada: {path}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Error configurando biblioteca: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Guarda la ruta de la biblioteca para recordarla
        /// </summary>
        private void SavePath(string path)
        {
            try
            {
                var configDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SlskDown");
                
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                
                var configPath = Path.Combine(configDir, "calibre_path.txt");
                File.WriteAllText(configPath, path);
            }
            catch (Exception ex)
            {
                Log($"No se pudo guardar la ruta: {ex.Message}");
            }
        }

        /// <summary>
        /// Guarda la preferencia de auto-agregar
        /// </summary>
        public void SaveAutoAddPreference(bool autoAdd)
        {
            try
            {
                var configDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SlskDown");
                
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                
                var configPath = Path.Combine(configDir, "calibre_autoadd.txt");
                File.WriteAllText(configPath, autoAdd.ToString());
            }
            catch (Exception ex)
            {
                Log($"No se pudo guardar la preferencia: {ex.Message}");
            }
        }

        /// <summary>
        /// Carga la preferencia de auto-agregar
        /// </summary>
        public bool LoadAutoAddPreference()
        {
            try
            {
                var configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SlskDown",
                    "calibre_autoadd.txt");
                
                if (File.Exists(configPath))
                {
                    var value = File.ReadAllText(configPath).Trim();
                    if (bool.TryParse(value, out bool autoAdd))
                    {
                        return autoAdd;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error cargando preferencia: {ex.Message}");
            }
            
            return true; // Valor por defecto
        }

        /// <summary>
        /// Carga la ruta guardada anteriormente
        /// </summary>
        private void LoadSavedPath()
        {
            try
            {
                var configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SlskDown",
                    "calibre_path.txt");
                
                if (File.Exists(configPath))
                {
                    var path = File.ReadAllText(configPath).Trim();
                    var dbPath = Path.Combine(path, "metadata.db");
                    
                    if (File.Exists(dbPath))
                    {
                        calibreLibraryPath = path;
                        calibreDbPath = dbPath;
                        calibreAvailable = true;
                        Log($"Ruta de Calibre cargada: {path}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error cargando ruta guardada: {ex.Message}");
            }
        }

        /// <summary>
        /// Verifica si Calibre está disponible
        /// </summary>
        public bool IsAvailable => calibreAvailable;

        /// <summary>
        /// Obtiene la ruta de la biblioteca
        /// </summary>
        public string LibraryPath => calibreLibraryPath;

        /// <summary>
        /// Agrega un libro a Calibre usando calibredb
        /// </summary>
        public async Task<bool> AddBookAsync(string filePath, string author = null, string title = null)
        {
            if (!calibreAvailable)
            {
                Log("Calibre no está disponible");
                return false;
            }

            try
            {
                var calibreDbExe = FindCalibreDb();
                if (calibreDbExe == null)
                {
                    Log("No se encontró calibredb.exe");
                    return false;
                }

                // Comando: calibredb add "archivo.epub" --with-library "C:\Calibre Library"
                var args = $"add \"{filePath}\" --with-library \"{calibreLibraryPath}\"";

                // Agregar metadata si está disponible
                if (!string.IsNullOrEmpty(author))
                {
                    args += $" --authors \"{author}\"";
                }
                if (!string.IsNullOrEmpty(title))
                {
                    args += $" --title \"{title}\"";
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = calibreDbExe,
                        Arguments = args,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    Log($"Libro agregado a Calibre: {Path.GetFileName(filePath)}");
                    return true;
                }
                else
                {
                    Log($"Error agregando libro: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log($"Error agregando libro a Calibre: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verifica si un libro ya existe en Calibre
        /// </summary>
        public async Task<bool> BookExistsAsync(string title, string author = null)
        {
            if (!calibreAvailable)
                return false;

            try
            {
                var calibreDbExe = FindCalibreDb();
                if (calibreDbExe == null)
                    return false;

                // Comando: calibredb list --search "title:titulo" --with-library "C:\Calibre Library"
                var searchQuery = $"title:\"{title}\"";
                if (!string.IsNullOrEmpty(author))
                {
                    searchQuery += $" author:\"{author}\"";
                }

                var args = $"list --search \"{searchQuery}\" --with-library \"{calibreLibraryPath}\" --for-machine";

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = calibreDbExe,
                        Arguments = args,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                // Si hay resultados, el libro existe
                return !string.IsNullOrWhiteSpace(output) && output.Trim() != "[]";
            }
            catch (Exception ex)
            {
                Log($"Error verificando libro: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Obtiene información de libros en Calibre
        /// </summary>
        public async Task<List<CalibreBook>> GetBooksAsync(string searchQuery = null)
        {
            if (!calibreAvailable)
            {
                Log("GetBooksAsync: Calibre no disponible");
                return new List<CalibreBook>();
            }

            try
            {
                var calibreDbExe = FindCalibreDb();
                if (calibreDbExe == null)
                {
                    Log("GetBooksAsync: No se encontró calibredb.exe");
                    return new List<CalibreBook>();
                }

                Log($"Usando calibredb: {calibreDbExe}");
                Log($"Ruta biblioteca: {calibreLibraryPath}");

                // Verificar si Calibre está corriendo
                var calibreProcesses = Process.GetProcessesByName("calibre");
                bool calibreWasClosed = false;
                
                if (calibreProcesses.Length > 0)
                {
                    Log($"Calibre está abierto ({calibreProcesses.Length} proceso(s)). Cerrando temporalmente...");
                    foreach (var proc in calibreProcesses)
                    {
                        try
                        {
                            proc.CloseMainWindow();
                            proc.WaitForExit(3000); // Esperar máximo 3 segundos
                            if (!proc.HasExited)
                            {
                                proc.Kill();
                            }
                        }
                        catch { }
                    }
                    calibreWasClosed = true;
                    System.Threading.Thread.Sleep(500); // Pequeña pausa para asegurar que se cerró
                    Log("Calibre cerrado temporalmente");
                }

                var args = $"list --library-path \"{calibreLibraryPath}\" --for-machine";
                if (!string.IsNullOrEmpty(searchQuery))
                {
                    args += $" --search \"{searchQuery}\"";
                }

                Log($"Ejecutando: {calibreDbExe} {args}");

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = calibreDbExe,
                        Arguments = args,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                Log($"Exit code: {process.ExitCode}");
                if (!string.IsNullOrEmpty(error))
                {
                    Log($"Error output: {error}");
                }

                List<CalibreBook> result;
                
                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    Log($"Output length: {output.Length} chars");
                    
                    // Mostrar una muestra del JSON para debug
                    if (output.Length > 500)
                    {
                        Log($"JSON sample: {output.Substring(0, 500)}...");
                    }
                    
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    
                    var books = JsonSerializer.Deserialize<List<CalibreBook>>(output, options);
                    Log($"Libros deserializados: {books?.Count ?? 0}");
                    
                    if (books != null && books.Count > 0)
                    {
                        var firstBook = books[0];
                        Log($"Primer libro: Title='{firstBook.Title}', Authors='{firstBook.Authors}', Id={firstBook.Id}");
                    }
                    
                    result = books ?? new List<CalibreBook>();
                }
                else
                {
                    Log("No hay output o exit code != 0");
                    result = new List<CalibreBook>();
                }
                
                // Reabrir Calibre si lo cerramos
                if (calibreWasClosed)
                {
                    Log("Reabriendo Calibre...");
                    try
                    {
                        var calibreExe = FindCalibreExecutable();
                        if (calibreExe != null)
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = calibreExe,
                                Arguments = $"--with-library \"{calibreLibraryPath}\"",
                                UseShellExecute = true
                            });
                            Log("Calibre reabierto");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"No se pudo reabrir Calibre: {ex.Message}");
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Log($"Error obteniendo libros: {ex.Message}");
                Log($"Stack trace: {ex.StackTrace}");
                return new List<CalibreBook>();
            }
        }

        /// <summary>
        /// Elimina todos los libros de la biblioteca
        /// </summary>
        public async Task<bool> RemoveAllBooksAsync()
        {
            if (!calibreAvailable)
                return false;

            try
            {
                var calibreDbExe = FindCalibreDb();
                if (calibreDbExe == null)
                    return false;

                Log("Eliminando todos los libros de la biblioteca...");

                // Obtener todos los IDs de libros
                var books = await GetBooksAsync();
                if (books.Count == 0)
                {
                    Log("No hay libros para eliminar");
                    return true;
                }

                var totalBooks = books.Count;
                Log($"Total de libros a eliminar: {totalBooks:N0}");

                // Eliminar en lotes de 100 libros para evitar exceder el límite de longitud de línea de comandos
                const int batchSize = 100;
                var batches = (int)Math.Ceiling((double)totalBooks / batchSize);
                
                for (int i = 0; i < batches; i++)
                {
                    var batch = books.Skip(i * batchSize).Take(batchSize).ToList();
                    var bookIds = string.Join(",", batch.Select(b => b.Id));
                    
                    Log($"Eliminando lote {i + 1}/{batches} ({batch.Count} libros)...");
                    
                    var args = $"remove --library-path \"{calibreLibraryPath}\" {bookIds}";

                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = calibreDbExe,
                            Arguments = args,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0)
                    {
                        Log($"Error eliminando lote {i + 1}: {error}");
                        return false;
                    }
                    
                    Log($"Lote {i + 1}/{batches} eliminado");
                }

                Log($"Todos los libros eliminados exitosamente ({totalBooks:N0} libros)");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Error eliminando todos los libros: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Exporta un libro desde Calibre
        /// </summary>
        public async Task<bool> ExportBookAsync(int bookId, string destinationPath)
        {
            if (!calibreAvailable)
                return false;

            try
            {
                var calibreDbExe = FindCalibreDb();
                if (calibreDbExe == null)
                    return false;

                var args = $"export {bookId} --with-library \"{calibreLibraryPath}\" --to-dir \"{destinationPath}\"";

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = calibreDbExe,
                        Arguments = args,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    Log($"Libro exportado: ID {bookId}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Log($"Error exportando libro: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Busca calibredb.exe en ubicaciones comunes
        /// </summary>
        private string FindCalibreDb()
        {
            var possiblePaths = new[]
            {
                @"C:\Program Files\Calibre2\calibredb.exe",
                @"C:\Program Files (x86)\Calibre2\calibredb.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Calibre2", "calibredb.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Calibre2", "calibredb.exe")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                    return path;
            }

            // Buscar en PATH
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (pathEnv != null)
            {
                var paths = pathEnv.Split(';');
                foreach (var path in paths)
                {
                    var calibreDb = Path.Combine(path.Trim(), "calibredb.exe");
                    if (File.Exists(calibreDb))
                        return calibreDb;
                }
            }

            return null;
        }

        /// <summary>
        /// Obtiene estadísticas de la biblioteca
        /// </summary>
        public async Task<CalibreStats> GetStatsAsync()
        {
            if (!calibreAvailable)
                return null;

            try
            {
                var books = await GetBooksAsync();
                
                var stats = new CalibreStats
                {
                    TotalBooks = books.Count,
                    Authors = books.Select(b => b.Authors).Distinct().Count(),
                    Formats = books.SelectMany(b => b.Formats ?? new List<string>()).Distinct().Count()
                };

                return stats;
            }
            catch (Exception ex)
            {
                Log($"Error obteniendo estadísticas: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Vacía completamente la biblioteca de Calibre eliminando todos los libros
        /// </summary>
        public bool ClearLibrary()
        {
            if (!calibreAvailable)
            {
                Log("Calibre no está disponible");
                return false;
            }

            try
            {
                Log("Iniciando vaciado de biblioteca Calibre...");
                
                // Usar calibredb para eliminar todos los libros
                var calibreDbExe = FindCalibreDbExecutable();
                
                if (string.IsNullOrEmpty(calibreDbExe))
                {
                    Log("No se encontró calibredb.exe");
                    return false;
                }
                
                // Obtener lista de todos los IDs de libros
                var listProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = calibreDbExe,
                        Arguments = $"list --library-path=\"{calibreLibraryPath}\" --for-machine",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                
                listProcess.Start();
                var output = listProcess.StandardOutput.ReadToEnd();
                listProcess.WaitForExit();
                
                if (listProcess.ExitCode != 0)
                {
                    Log($"Error listando libros: {listProcess.StandardError.ReadToEnd()}");
                    return false;
                }
                
                // Parsear IDs de libros
                var bookIds = new List<int>();
                try
                {
                    var books = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(output);
                    if (books != null)
                    {
                        foreach (var book in books)
                        {
                            if (book.ContainsKey("id"))
                            {
                                bookIds.Add(Convert.ToInt32(book["id"]));
                            }
                        }
                    }
                }
                catch
                {
                    // Si falla el parsing JSON, intentar método alternativo
                    Log("Usando método alternativo para obtener IDs");
                    
                    // Eliminar todos usando rango (asumiendo IDs consecutivos)
                    var removeAllProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = calibreDbExe,
                            Arguments = $"remove --library-path=\"{calibreLibraryPath}\" 1-999999",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };
                    
                    removeAllProcess.Start();
                    removeAllProcess.WaitForExit();
                    
                    Log($"Biblioteca vaciada (método alternativo)");
                    return true;
                }
                
                if (bookIds.Count == 0)
                {
                    Log("La biblioteca ya está vacía");
                    return true;
                }
                
                Log($"Eliminando {bookIds.Count} libros...");
                
                // Eliminar libros en lotes de 100 para evitar línea de comandos muy larga
                const int batchSize = 100;
                int totalDeleted = 0;
                
                for (int i = 0; i < bookIds.Count; i += batchSize)
                {
                    var batch = bookIds.Skip(i).Take(batchSize).ToList();
                    var ids = string.Join(",", batch);
                    
                    var removeProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = calibreDbExe,
                            Arguments = $"remove --library-path=\"{calibreLibraryPath}\" {ids}",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };
                    
                    removeProcess.Start();
                    removeProcess.WaitForExit();
                    
                    if (removeProcess.ExitCode == 0)
                    {
                        totalDeleted += batch.Count;
                        Log($"Eliminados {totalDeleted}/{bookIds.Count} libros...");
                    }
                    else
                    {
                        var error = removeProcess.StandardError.ReadToEnd();
                        Log($"Error eliminando lote: {error}");
                    }
                }
                
                Log($"Biblioteca vaciada exitosamente: {totalDeleted} libros eliminados");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Error vaciando biblioteca: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Encuentra el ejecutable calibredb
        /// </summary>
        private string FindCalibreDbExecutable()
        {
            var possiblePaths = new[]
            {
                @"C:\Program Files\Calibre2\calibredb.exe",
                @"C:\Program Files (x86)\Calibre2\calibredb.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Calibre2", "calibredb.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Calibre2", "calibredb.exe")
            };
            
            return possiblePaths.FirstOrDefault(File.Exists);
        }
        
        /// <summary>
        /// Encuentra el ejecutable de Calibre
        /// </summary>
        private string FindCalibreExecutable()
        {
            var possiblePaths = new[]
            {
                @"C:\Program Files\Calibre2\calibre.exe",
                @"C:\Program Files (x86)\Calibre2\calibre.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Calibre2", "calibre.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Calibre2", "calibre.exe")
            };
            
            return possiblePaths.FirstOrDefault(File.Exists);
        }
        
        /// <summary>
        /// Abre Calibre automáticamente si no está abierto
        /// </summary>
        public bool OpenCalibreIfNeeded()
        {
            try
            {
                // Verificar si Calibre ya está abierto
                var calibreProcesses = Process.GetProcessesByName("calibre");
                if (calibreProcesses.Length > 0)
                {
                    Log("Calibre ya está abierto");
                    return true;
                }
                
                // Abrir Calibre
                var calibreExe = FindCalibreExecutable();
                if (string.IsNullOrEmpty(calibreExe))
                {
                    Log("No se encontró el ejecutable de Calibre");
                    return false;
                }
                
                Log("Abriendo Calibre automáticamente...");
                
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = calibreExe,
                    Arguments = $"--with-library \"{calibreLibraryPath}\"",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Minimized
                });
                
                // Esperar a que Calibre inicie (máximo 5 segundos)
                System.Threading.Thread.Sleep(2000);
                
                Log("Calibre abierto correctamente");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Error abriendo Calibre: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Cierra Calibre automáticamente
        /// </summary>
        public bool CloseCalibreIfNeeded()
        {
            try
            {
                var calibreProcesses = Process.GetProcessesByName("calibre");
                
                if (calibreProcesses.Length == 0)
                {
                    Log("Calibre ya está cerrado");
                    return true;
                }
                
                Log("Cerrando Calibre automáticamente...");
                
                foreach (var process in calibreProcesses)
                {
                    try
                    {
                        process.CloseMainWindow();
                        
                        // Esperar hasta 3 segundos a que cierre
                        if (!process.WaitForExit(3000))
                        {
                            // Si no cierra, forzar
                            process.Kill();
                        }
                        
                        process.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log($"Error cerrando proceso de Calibre: {ex.Message}");
                    }
                }
                
                Log("Calibre cerrado correctamente");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Error cerrando Calibre: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Ejecuta una operación con apertura y cierre automático de Calibre
        /// </summary>
        public async Task<T> ExecuteWithCalibreAsync<T>(Func<Task<T>> operation)
        {
            bool wasOpened = false;
            
            try
            {
                // Abrir Calibre si es necesario
                var calibreProcesses = Process.GetProcessesByName("calibre");
                if (calibreProcesses.Length == 0)
                {
                    OpenCalibreIfNeeded();
                    wasOpened = true;
                }
                
                // Ejecutar operación
                var result = await operation();
                
                return result;
            }
            finally
            {
                // Cerrar Calibre solo si lo abrimos nosotros
                if (wasOpened)
                {
                    // Pequeño delay para que termine la operación
                    await Task.Delay(1000);
                    CloseCalibreIfNeeded();
                }
            }
        }
        
        /// <summary>
        /// Ejecuta una operación con apertura y cierre automático de Calibre (versión síncrona)
        /// </summary>
        public T ExecuteWithCalibre<T>(Func<T> operation)
        {
            bool wasOpened = false;
            
            try
            {
                // Abrir Calibre si es necesario
                var calibreProcesses = Process.GetProcessesByName("calibre");
                if (calibreProcesses.Length == 0)
                {
                    OpenCalibreIfNeeded();
                    wasOpened = true;
                }
                
                // Ejecutar operación
                var result = operation();
                
                return result;
            }
            finally
            {
                // Cerrar Calibre solo si lo abrimos nosotros
                if (wasOpened)
                {
                    // Pequeño delay para que termine la operación
                    System.Threading.Thread.Sleep(1000);
                    CloseCalibreIfNeeded();
                }
            }
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
    }

    public class CalibreBook
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        
        [JsonPropertyName("title")]
        public string Title { get; set; }
        
        [JsonPropertyName("authors")]
        public string Authors { get; set; }
        
        [JsonPropertyName("formats")]
        public List<string> Formats { get; set; }
        
        [JsonPropertyName("size")]
        public string Size { get; set; }
        
        [JsonPropertyName("timestamp")]
        public DateTime? Timestamp { get; set; }
        
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; }
        
        [JsonPropertyName("series")]
        public string Series { get; set; }
        
        [JsonPropertyName("publisher")]
        public string Publisher { get; set; }
    }

    public class CalibreStats
    {
        public int TotalBooks { get; set; }
        public int Authors { get; set; }
        public int Formats { get; set; }
    }
}
