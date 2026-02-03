using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace SlskDown.Core
{
    public static class RustFileOperations
    {
        public static bool IsAvailable() => true;

        public sealed class FileValidationResult
        {
            public bool IsValid { get; set; }

            public bool HasCorruption { get; set; }

            public string FileType { get; set; } = string.Empty;

            public string ErrorMessage { get; set; } = string.Empty;
        }
        
        public static bool ValidateFile(string path)
        {
            var result = ValidateFileIntegrity(path);
            return result != null && result.IsValid;
        }

        public static FileValidationResult ValidateFileIntegrity(string path)
        {
            var result = new FileValidationResult();
            
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    result.IsValid = false;
                    result.HasCorruption = true;
                    result.ErrorMessage = "Archivo no encontrado";
                    return result;
                }

                var fileInfo = new FileInfo(path);
                if (fileInfo.Length == 0)
                {
                    result.IsValid = false;
                    result.HasCorruption = true;
                    result.ErrorMessage = "Archivo vacío (0 bytes)";
                    return result;
                }

                var ext = Path.GetExtension(path).ToLowerInvariant();
                result.FileType = ext;

                switch (ext)
                {
                    case ".epub":
                        return ValidateEpub(path);
                    
                    case ".pdf":
                        return ValidatePdf(path);
                    
                    case ".mobi":
                    case ".azw":
                    case ".azw3":
                        return ValidateMobi(path);
                    
                    case ".mp3":
                    case ".flac":
                    case ".m4a":
                    case ".ogg":
                    case ".wav":
                    case ".aac":
                        return ValidateAudio(path);
                    
                    case ".mp4":
                    case ".mkv":
                    case ".avi":
                    case ".mov":
                    case ".wmv":
                    case ".webm":
                        return ValidateVideo(path);
                    
                    default:
                        // Otros archivos: validación básica (existe y no está vacío)
                        result.IsValid = true;
                        result.HasCorruption = false;
                        return result;
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.HasCorruption = true;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private static FileValidationResult ValidateEpub(string path)
        {
            var result = new FileValidationResult { FileType = ".epub" };
            
            try
            {
                // EPUB es un archivo ZIP - validación básica pero efectiva
                using (var archive = ZipFile.OpenRead(path))
                {
                    // 1. Verificar que tiene entradas
                    if (archive.Entries.Count == 0)
                    {
                        result.IsValid = false;
                        result.HasCorruption = true;
                        result.ErrorMessage = "EPUB vacío (sin entradas ZIP)";
                        return result;
                    }

                    // 2. Buscar archivos típicos de EPUB (validación flexible)
                    bool hasMimetype = archive.Entries.Any(e => 
                        e.FullName.Equals("mimetype", StringComparison.OrdinalIgnoreCase));
                    
                    bool hasContainer = archive.Entries.Any(e => 
                        e.FullName.Contains("container.xml", StringComparison.OrdinalIgnoreCase));
                    
                    bool hasContentFiles = archive.Entries.Any(e => 
                        e.FullName.EndsWith(".opf", StringComparison.OrdinalIgnoreCase) ||
                        e.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                        e.FullName.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase));

                    // EPUB válido si tiene al menos 2 de 3 características
                    int validityScore = (hasMimetype ? 1 : 0) + (hasContainer ? 1 : 0) + (hasContentFiles ? 1 : 0);
                    
                    if (validityScore >= 2)
                    {
                        result.IsValid = true;
                        result.HasCorruption = false;
                        return result;
                    }
                    else
                    {
                        result.IsValid = false;
                        result.HasCorruption = true;
                        result.ErrorMessage = $"EPUB sospechoso: score {validityScore}/3 (mimetype:{hasMimetype}, container:{hasContainer}, content:{hasContentFiles})";
                        return result;
                    }
                }
            }
            catch (InvalidDataException)
            {
                result.IsValid = false;
                result.HasCorruption = true;
                result.ErrorMessage = "EPUB corrupto: no es un archivo ZIP válido";
                return result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.HasCorruption = true;
                result.ErrorMessage = $"Error validando EPUB: {ex.Message}";
                return result;
            }
        }

        private static FileValidationResult ValidatePdf(string path)
        {
            var result = new FileValidationResult { FileType = ".pdf" };
            
            try
            {
                // PDF debe empezar con %PDF- (validación flexible)
                using (var stream = File.OpenRead(path))
                {
                    if (stream.Length < 100)
                    {
                        result.IsValid = false;
                        result.HasCorruption = true;
                        result.ErrorMessage = "PDF demasiado pequeño";
                        return result;
                    }

                    var buffer = new byte[10];
                    var bytesRead = stream.Read(buffer, 0, 10);
                    
                    if (bytesRead < 5)
                    {
                        result.IsValid = false;
                        result.HasCorruption = true;
                        result.ErrorMessage = "PDF demasiado pequeño";
                        return result;
                    }

                    var header = System.Text.Encoding.ASCII.GetString(buffer, 0, Math.Min(10, bytesRead));
                    if (!header.StartsWith("%PDF-"))
                    {
                        result.IsValid = false;
                        result.HasCorruption = true;
                        result.ErrorMessage = "PDF inválido: header incorrecto";
                        return result;
                    }

                    // Validación flexible de EOF - buscar en últimos 2KB
                    long seekPos = Math.Max(0, stream.Length - 2048);
                    stream.Seek(seekPos, SeekOrigin.Begin);
                    var endBuffer = new byte[2048];
                    var endBytesRead = stream.Read(endBuffer, 0, endBuffer.Length);
                    var endContent = System.Text.Encoding.ASCII.GetString(endBuffer, 0, endBytesRead);
                    
                    // Buscar %%EOF o EOF (algunos PDFs no tienen %%)
                    if (!endContent.Contains("%%EOF") && !endContent.Contains("EOF"))
                    {
                        // Advertencia pero no fallo - algunos PDFs válidos no tienen EOF explícito
                        result.IsValid = true;
                        result.HasCorruption = false;
                        result.ErrorMessage = "PDF sin marcador EOF (puede ser válido)";
                        return result;
                    }

                    result.IsValid = true;
                    result.HasCorruption = false;
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.HasCorruption = true;
                result.ErrorMessage = $"Error validando PDF: {ex.Message}";
                return result;
            }
        }

        private static FileValidationResult ValidateMobi(string path)
        {
            var result = new FileValidationResult { FileType = ".mobi" };
            
            try
            {
                // MOBI/AZW debe tener header específico
                using (var stream = File.OpenRead(path))
                {
                    if (stream.Length < 68)
                    {
                        result.IsValid = false;
                        result.HasCorruption = true;
                        result.ErrorMessage = "MOBI demasiado pequeño";
                        return result;
                    }

                    // Verificar PalmDB header
                    var buffer = new byte[68];
                    stream.Read(buffer, 0, 68);
                    
                    // Bytes 60-67 deben ser "BOOKMOBI" o "TEXtREAd"
                    var identifier = System.Text.Encoding.ASCII.GetString(buffer, 60, 8);
                    if (identifier != "BOOKMOBI" && identifier != "TEXtREAd")
                    {
                        result.IsValid = false;
                        result.HasCorruption = true;
                        result.ErrorMessage = "MOBI inválido: identificador incorrecto";
                        return result;
                    }

                    result.IsValid = true;
                    result.HasCorruption = false;
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.HasCorruption = true;
                result.ErrorMessage = $"Error validando MOBI: {ex.Message}";
                return result;
            }
        }

        private static FileValidationResult ValidateAudio(string path)
        {
            var result = new FileValidationResult { FileType = Path.GetExtension(path) };
            
            try
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                using (var stream = File.OpenRead(path))
                {
                    if (stream.Length < 100)
                    {
                        result.IsValid = false;
                        result.HasCorruption = true;
                        result.ErrorMessage = "Archivo de audio demasiado pequeño";
                        return result;
                    }

                    var buffer = new byte[12];
                    stream.Read(buffer, 0, Math.Min(12, (int)stream.Length));

                    switch (ext)
                    {
                        case ".mp3":
                            // MP3: ID3 tag o FF Fx (frame sync)
                            if (!(buffer[0] == 'I' && buffer[1] == 'D' && buffer[2] == '3') &&
                                !(buffer[0] == 0xFF && (buffer[1] & 0xE0) == 0xE0))
                            {
                                result.IsValid = false;
                                result.HasCorruption = true;
                                result.ErrorMessage = "MP3 inválido: header incorrecto";
                                return result;
                            }
                            break;

                        case ".flac":
                            // FLAC: "fLaC"
                            if (!(buffer[0] == 'f' && buffer[1] == 'L' && buffer[2] == 'a' && buffer[3] == 'C'))
                            {
                                result.IsValid = false;
                                result.HasCorruption = true;
                                result.ErrorMessage = "FLAC inválido: header incorrecto";
                                return result;
                            }
                            break;

                        case ".ogg":
                            // OGG: "OggS"
                            if (!(buffer[0] == 'O' && buffer[1] == 'g' && buffer[2] == 'g' && buffer[3] == 'S'))
                            {
                                result.IsValid = false;
                                result.HasCorruption = true;
                                result.ErrorMessage = "OGG inválido: header incorrecto";
                                return result;
                            }
                            break;

                        case ".wav":
                            // WAV: "RIFF" ... "WAVE"
                            if (!(buffer[0] == 'R' && buffer[1] == 'I' && buffer[2] == 'F' && buffer[3] == 'F' &&
                                  buffer[8] == 'W' && buffer[9] == 'A' && buffer[10] == 'V' && buffer[11] == 'E'))
                            {
                                result.IsValid = false;
                                result.HasCorruption = true;
                                result.ErrorMessage = "WAV inválido: header incorrecto";
                                return result;
                            }
                            break;
                    }

                    result.IsValid = true;
                    result.HasCorruption = false;
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.HasCorruption = true;
                result.ErrorMessage = $"Error validando audio: {ex.Message}";
                return result;
            }
        }

        private static FileValidationResult ValidateVideo(string path)
        {
            var result = new FileValidationResult { FileType = Path.GetExtension(path) };
            
            try
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                using (var stream = File.OpenRead(path))
                {
                    if (stream.Length < 100)
                    {
                        result.IsValid = false;
                        result.HasCorruption = true;
                        result.ErrorMessage = "Archivo de video demasiado pequeño";
                        return result;
                    }

                    var buffer = new byte[12];
                    stream.Read(buffer, 0, Math.Min(12, (int)stream.Length));

                    switch (ext)
                    {
                        case ".mp4":
                        case ".m4a":
                            // MP4: ftyp box
                            if (!(buffer[4] == 'f' && buffer[5] == 't' && buffer[6] == 'y' && buffer[7] == 'p'))
                            {
                                result.IsValid = false;
                                result.HasCorruption = true;
                                result.ErrorMessage = "MP4 inválido: header incorrecto";
                                return result;
                            }
                            break;

                        case ".mkv":
                        case ".webm":
                            // MKV/WebM: EBML header
                            if (!(buffer[0] == 0x1A && buffer[1] == 0x45 && buffer[2] == 0xDF && buffer[3] == 0xA3))
                            {
                                result.IsValid = false;
                                result.HasCorruption = true;
                                result.ErrorMessage = "MKV/WebM inválido: header incorrecto";
                                return result;
                            }
                            break;

                        case ".avi":
                            // AVI: "RIFF" ... "AVI "
                            if (!(buffer[0] == 'R' && buffer[1] == 'I' && buffer[2] == 'F' && buffer[3] == 'F' &&
                                  buffer[8] == 'A' && buffer[9] == 'V' && buffer[10] == 'I' && buffer[11] == ' '))
                            {
                                result.IsValid = false;
                                result.HasCorruption = true;
                                result.ErrorMessage = "AVI inválido: header incorrecto";
                                return result;
                            }
                            break;
                    }

                    result.IsValid = true;
                    result.HasCorruption = false;
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.HasCorruption = true;
                result.ErrorMessage = $"Error validando video: {ex.Message}";
                return result;
            }
        }
        
        public static string ComputeHash(string path) => "";
        
        public static bool CopyFile(string source, string dest) => false;
        
        public static bool MoveFile(string source, string dest) => false;
    }
}
