using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SlskDown.Services
{
    /// <summary>
    /// Gestiona la detección y persistencia de duplicados por hash de contenido.
    /// </summary>
    public sealed class ContentDuplicateDetector
    {
        private readonly object syncRoot = new();
        private readonly string indexFilePath;
        private readonly Action<string>? log;
        private Dictionary<string, DuplicateEntry> index = new(StringComparer.OrdinalIgnoreCase);

        public ContentDuplicateDetector(string indexFilePath, Action<string>? log = null)
        {
            if (string.IsNullOrWhiteSpace(indexFilePath))
            {
                throw new ArgumentException("Index file path cannot be null or empty", nameof(indexFilePath));
            }

            this.indexFilePath = indexFilePath;
            this.log = log;

            LoadIndex();
        }

        /// <summary>
        /// Registra un hash y devuelve la ruta existente si se detecta duplicado.
        /// </summary>
        public string? RegisterAndGetExisting(string hash, string filePath, long sizeBytes)
        {
            if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            lock (syncRoot)
            {
                if (index.TryGetValue(hash, out var existing) && File.Exists(existing.FilePath))
                {
                    return existing.FilePath;
                }

                index[hash] = new DuplicateEntry
                {
                    Hash = hash,
                    FilePath = filePath,
                    SizeBytes = sizeBytes,
                    CreatedAtUtc = DateTime.UtcNow
                };

                PersistIndex();
                return null;
            }
        }

        /// <summary>
        /// Elimina entradas cuyo archivo ya no exista en disco.
        /// </summary>
        public void PruneMissingEntries()
        {
            lock (syncRoot)
            {
                var removed = index
                    .Where(pair => !File.Exists(pair.Value.FilePath))
                    .Select(pair => pair.Key)
                    .ToList();

                if (removed.Count == 0)
                {
                    return;
                }

                foreach (var key in removed)
                {
                    index.Remove(key);
                }

                log?.Invoke($"🧹 Índice de duplicados depurado: {removed.Count} entradas obsoletas eliminadas");
                PersistIndex();
            }
        }

        private void LoadIndex()
        {
            try
            {
                if (!File.Exists(indexFilePath))
                {
                    return;
                }

                var json = File.ReadAllText(indexFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                var entries = JsonSerializer.Deserialize<List<DuplicateEntry>>(json);
                if (entries == null)
                {
                    return;
                }

                index = entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.Hash) && !string.IsNullOrWhiteSpace(e.FilePath))
                    .GroupBy(e => e.Hash, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(e => e.CreatedAtUtc).First())
                    .ToDictionary(e => e.Hash, e => e, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                log?.Invoke($"No se pudo cargar el índice de duplicados: {ex.Message}");
                index = new Dictionary<string, DuplicateEntry>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void PersistIndex()
        {
            try
            {
                var directory = Path.GetDirectoryName(indexFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(index.Values.ToList(), new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(indexFilePath, json);
            }
            catch (Exception ex)
            {
                log?.Invoke($"No se pudo guardar el índice de duplicados: {ex.Message}");
            }
        }

        private class DuplicateEntry
        {
            public string Hash { get; set; } = string.Empty;
            public string FilePath { get; set; } = string.Empty;
            public long SizeBytes { get; set; }
            public DateTime CreatedAtUtc { get; set; }
        }
    }
}
