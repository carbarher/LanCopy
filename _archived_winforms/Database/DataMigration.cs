using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SlskDown.Database.Models;

namespace SlskDown.Database
{
    /// <summary>
    /// Herramienta para migrar datos existentes a SQLite
    /// </summary>
    public class DataMigration
    {
        private readonly SlskDatabase db;

        public DataMigration(SlskDatabase database)
        {
            db = database;
        }

        /// <summary>
        /// Migra el historial de descargas desde JSON
        /// </summary>
        public async Task<int> MigrateDownloadHistoryFromJsonAsync(string jsonPath)
        {
            if (!File.Exists(jsonPath))
                return 0;

            try
            {
                var json = await File.ReadAllTextAsync(jsonPath);
                var history = JsonSerializer.Deserialize<List<DownloadHistoryItem>>(json);

                if (history == null || history.Count == 0)
                    return 0;

                int migrated = 0;
                foreach (var item in history)
                {
                    var record = new DownloadRecord
                    {
                        FileName = item.FileName ?? "",
                        Author = item.Author,
                        Username = item.Username ?? "",
                        SizeBytes = item.SizeBytes,
                        Status = item.Status ?? "Completed",
                        DownloadedAt = item.DownloadedAt,
                        FilePath = item.FilePath,
                        Speed = null,
                        Language = null
                    };

                    await db.InsertDownloadAsync(record);
                    migrated++;
                }

                return migrated;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error migrando historial JSON: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Migra el historial de descargas desde CSV
        /// </summary>
        public async Task<int> MigrateDownloadHistoryFromCsvAsync(string csvPath)
        {
            if (!File.Exists(csvPath))
                return 0;

            try
            {
                var lines = await File.ReadAllLinesAsync(csvPath);
                if (lines.Length <= 1) // Solo header o vacío
                    return 0;

                int migrated = 0;
                for (int i = 1; i < lines.Length; i++) // Skip header
                {
                    var parts = ParseCsvLine(lines[i]);
                    if (parts.Length < 6)
                        continue;

                    var record = new DownloadRecord
                    {
                        FileName = parts[1],
                        Author = parts[2],
                        Username = parts[3],
                        SizeBytes = long.TryParse(parts[4], out var size) ? size : 0,
                        Status = parts[5],
                        DownloadedAt = DateTime.TryParse(parts[0], out var date) ? date : DateTime.UtcNow,
                        FilePath = null,
                        Speed = null,
                        Language = null
                    };

                    await db.InsertDownloadAsync(record);
                    migrated++;
                }

                return migrated;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error migrando historial CSV: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Escanea la carpeta de descargas y calcula hashes para detección de duplicados
        /// </summary>
        public async Task<int> ScanDownloadFolderAsync(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                return 0;

            try
            {
                var files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
                int scanned = 0;

                foreach (var filePath in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        var fileName = fileInfo.Name;
                        var size = fileInfo.Length;

                        // Calcular MD5 solo para archivos < 100MB (para no tardar mucho)
                        string md5Hash = null;
                        if (size < 100_000_000)
                        {
                            md5Hash = await CalculateMD5Async(filePath);
                        }

                        await db.InsertFileHashAsync(filePath, fileName, size, md5Hash, null);
                        scanned++;
                    }
                    catch
                    {
                        // Ignorar archivos que no se pueden leer
                        continue;
                    }
                }

                return scanned;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error escaneando carpeta: {ex.Message}");
                return 0;
            }
        }

        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = "";
            bool inQuotes = false;

            foreach (var c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current);
                    current = "";
                }
                else
                {
                    current += c;
                }
            }

            result.Add(current);
            return result.ToArray();
        }

        private async Task<string> CalculateMD5Async(string filePath)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            using var stream = File.OpenRead(filePath);
            var hash = await md5.ComputeHashAsync(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        // Clase auxiliar para deserializar JSON existente
        private class DownloadHistoryItem
        {
            public string FileName { get; set; }
            public string Author { get; set; }
            public string Username { get; set; }
            public long SizeBytes { get; set; }
            public string Status { get; set; }
            public DateTime DownloadedAt { get; set; }
            public string FilePath { get; set; }
        }
    }
}
