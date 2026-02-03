using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SlskDown
{
    /// <summary>
    /// Extrae metadatos de archivos EPUB y PDF
    /// </summary>
    public static class MetadataExtractor
    {
        public class BookMetadata
        {
            public string Title { get; set; }
            public string Author { get; set; }
            public int? PublicationYear { get; set; }
            public string Publisher { get; set; }
            public string ISBN { get; set; }
            public string Language { get; set; }
            public string Description { get; set; }
            public byte[] CoverImage { get; set; }
        }

        /// <summary>
        /// Extrae metadatos de un archivo EPUB
        /// </summary>
        public static BookMetadata ExtractEpubMetadata(string filePath)
        {
            var metadata = new BookMetadata();

            try
            {
                if (!File.Exists(filePath))
                    return metadata;

                using (var archive = ZipFile.OpenRead(filePath))
                {
                    // Buscar el archivo content.opf (puede estar en diferentes ubicaciones)
                    var opfEntry = archive.Entries.FirstOrDefault(e => 
                        e.FullName.EndsWith(".opf", StringComparison.OrdinalIgnoreCase));

                    if (opfEntry == null)
                        return metadata;

                    using (var stream = opfEntry.Open())
                    using (var reader = new StreamReader(stream))
                    {
                        var content = reader.ReadToEnd();
                        var doc = XDocument.Parse(content);

                        // Namespaces comunes en EPUB
                        XNamespace dc = "http://purl.org/dc/elements/1.1/";
                        XNamespace opf = "http://www.idpf.org/2007/opf";

                        // Extraer título
                        metadata.Title = doc.Descendants(dc + "title").FirstOrDefault()?.Value?.Trim();

                        // Extraer autor
                        metadata.Author = doc.Descendants(dc + "creator").FirstOrDefault()?.Value?.Trim();

                        // Extraer editorial
                        metadata.Publisher = doc.Descendants(dc + "publisher").FirstOrDefault()?.Value?.Trim();

                        // Extraer idioma
                        metadata.Language = doc.Descendants(dc + "language").FirstOrDefault()?.Value?.Trim();

                        // Extraer descripción
                        metadata.Description = doc.Descendants(dc + "description").FirstOrDefault()?.Value?.Trim();

                        // Extraer fecha de publicación
                        var dateStr = doc.Descendants(dc + "date").FirstOrDefault()?.Value?.Trim();
                        if (!string.IsNullOrEmpty(dateStr))
                        {
                            // Intentar extraer el año (formato común: YYYY-MM-DD o solo YYYY)
                            var yearMatch = Regex.Match(dateStr, @"(\d{4})");
                            if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out int year))
                            {
                                metadata.PublicationYear = year;
                            }
                        }

                        // Extraer ISBN
                        var identifiers = doc.Descendants(dc + "identifier");
                        foreach (var id in identifiers)
                        {
                            var scheme = id.Attribute(opf + "scheme")?.Value ?? 
                                        id.Attribute("scheme")?.Value;
                            
                            if (scheme != null && scheme.Equals("ISBN", StringComparison.OrdinalIgnoreCase))
                            {
                                metadata.ISBN = id.Value?.Trim();
                                break;
                            }
                        }

                        // Si no se encontró ISBN con scheme, buscar cualquier identificador que parezca ISBN
                        if (string.IsNullOrEmpty(metadata.ISBN))
                        {
                            foreach (var id in identifiers)
                            {
                                var value = id.Value?.Trim();
                                if (!string.IsNullOrEmpty(value) && 
                                    (value.StartsWith("978") || value.StartsWith("979")) &&
                                    value.Length >= 10)
                                {
                                    metadata.ISBN = value;
                                    break;
                                }
                            }
                        }
                    }

                    // Intentar extraer portada
                    try
                    {
                        var coverEntry = archive.Entries.FirstOrDefault(e =>
                            e.Name.ToLowerInvariant().Contains("cover") &&
                            (e.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                             e.Name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                             e.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)));

                        if (coverEntry != null)
                        {
                            using (var coverStream = coverEntry.Open())
                            using (var memoryStream = new MemoryStream())
                            {
                                coverStream.CopyTo(memoryStream);
                                metadata.CoverImage = memoryStream.ToArray();
                            }
                        }
                    }
                    catch { /* Ignorar errores al extraer portada */ }
                }
            }
            catch (Exception ex)
            {
                // Log error pero devolver metadata parcial
                Console.WriteLine($"Error extrayendo metadatos EPUB de {filePath}: {ex.Message}");
            }

            return metadata;
        }

        /// <summary>
        /// Extrae metadatos de un archivo PDF
        /// </summary>
        public static BookMetadata ExtractPdfMetadata(string filePath)
        {
            var metadata = new BookMetadata();

            try
            {
                if (!File.Exists(filePath))
                    return metadata;

                // Leer los primeros KB del archivo para buscar metadatos
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // Leer hasta 50KB (suficiente para metadatos en la mayoría de PDFs)
                    var bufferSize = Math.Min(50 * 1024, (int)fs.Length);
                    var buffer = new byte[bufferSize];
                    fs.Read(buffer, 0, bufferSize);
                    var content = Encoding.UTF8.GetString(buffer);

                    // Buscar metadatos en formato PDF
                    // Título
                    var titleMatch = Regex.Match(content, @"/Title\s*\(([^)]+)\)");
                    if (titleMatch.Success)
                    {
                        metadata.Title = CleanPdfString(titleMatch.Groups[1].Value);
                    }

                    // Autor
                    var authorMatch = Regex.Match(content, @"/Author\s*\(([^)]+)\)");
                    if (authorMatch.Success)
                    {
                        metadata.Author = CleanPdfString(authorMatch.Groups[1].Value);
                    }

                    // Fecha de creación (intentar extraer año)
                    var dateMatch = Regex.Match(content, @"/CreationDate\s*\(D:(\d{4})");
                    if (dateMatch.Success && int.TryParse(dateMatch.Groups[1].Value, out int year))
                    {
                        metadata.PublicationYear = year;
                    }

                    // Productor/Creador (puede dar pistas sobre la editorial)
                    var producerMatch = Regex.Match(content, @"/Producer\s*\(([^)]+)\)");
                    if (producerMatch.Success)
                    {
                        var producer = CleanPdfString(producerMatch.Groups[1].Value);
                        // Solo usar como publisher si parece una editorial real, no software
                        if (!producer.Contains("Adobe") && !producer.Contains("Microsoft") && 
                            !producer.Contains("iText") && producer.Length < 50)
                        {
                            metadata.Publisher = producer;
                        }
                    }

                    // Buscar ISBN en el contenido
                    var isbnMatch = Regex.Match(content, @"ISBN[:\s-]*(\d{3}[-\s]?\d{1,5}[-\s]?\d{1,7}[-\s]?\d{1,7}[-\s]?\d{1})");
                    if (isbnMatch.Success)
                    {
                        metadata.ISBN = isbnMatch.Groups[1].Value.Replace("-", "").Replace(" ", "");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extrayendo metadatos PDF de {filePath}: {ex.Message}");
            }

            return metadata;
        }

        /// <summary>
        /// Limpia strings extraídos de PDFs (elimina caracteres de escape, etc.)
        /// </summary>
        private static string CleanPdfString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Eliminar caracteres de escape comunes en PDFs
            var cleaned = input.Replace("\\(", "(")
                              .Replace("\\)", ")")
                              .Replace("\\\\", "\\")
                              .Replace("\\r", "")
                              .Replace("\\n", " ");

            return cleaned.Trim();
        }
    }
}
