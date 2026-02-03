using System;

namespace SlskDown.Models
{
    /// <summary>
    /// Representa un archivo que ya fue "descargado" (simulado)
    /// </summary>
    public class DownloadedFile
    {
        public string Filename { get; set; } = "";
        public string Username { get; set; } = "";
        public long Size { get; set; }
        public string Author { get; set; } = "";
        public DateTime DownloadedDate { get; set; }
        
        /// <summary>
        /// Clave Ãºnica para identificar el archivo (solo nombre, sin path)
        /// </summary>
        public string GetKey()
        {
            // Usar solo el nombre del archivo sin path para evitar duplicados
            var filenameOnly = System.IO.Path.GetFileName(Filename);
            return $"{filenameOnly}_{Size}".ToLower();
        }
    }
}

