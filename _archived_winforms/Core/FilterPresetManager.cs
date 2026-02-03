using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SlskDown.Core
{
    /// <summary>
    /// Gestor de presets de filtros de búsqueda guardados
    /// </summary>
    public class FilterPresetManager
    {
        private List<SavedSearchFilter> _presets;
        private readonly string _presetsFilePath;
        private readonly object _lock = new object();

        public event EventHandler<SavedSearchFilter> PresetAdded;
        public event EventHandler<SavedSearchFilter> PresetRemoved;
        public event EventHandler<SavedSearchFilter> PresetUpdated;

        public FilterPresetManager(string dataDirectory)
        {
            _presetsFilePath = Path.Combine(dataDirectory, "search_filter_presets.json");
            _presets = new List<SavedSearchFilter>();
            LoadPresets();
            
            // Crear presets por defecto si no hay ninguno
            if (_presets.Count == 0)
            {
                CreateDefaultPresets();
            }
        }

        /// <summary>
        /// Obtiene todos los presets guardados
        /// </summary>
        public List<SavedSearchFilter> GetAllPresets()
        {
            lock (_lock)
            {
                return new List<SavedSearchFilter>(_presets);
            }
        }

        /// <summary>
        /// Obtiene un preset por ID
        /// </summary>
        public SavedSearchFilter GetPreset(string id)
        {
            lock (_lock)
            {
                return _presets.FirstOrDefault(p => p.Id == id);
            }
        }

        /// <summary>
        /// Guarda el filtro actual como un nuevo preset
        /// </summary>
        public SavedSearchFilter SaveCurrentFilter(SavedSearchFilter filter)
        {
            lock (_lock)
            {
                // Asegurar que tiene un ID único
                if (string.IsNullOrEmpty(filter.Id))
                {
                    filter.Id = Guid.NewGuid().ToString();
                }

                // Verificar si ya existe
                var existing = _presets.FirstOrDefault(p => p.Id == filter.Id);
                if (existing != null)
                {
                    // Actualizar existente
                    _presets.Remove(existing);
                }

                filter.Created = DateTime.Now;
                filter.LastUsed = DateTime.Now;
                _presets.Add(filter);

                SavePresets();
                PresetAdded?.Invoke(this, filter);

                return filter;
            }
        }

        /// <summary>
        /// Actualiza un preset existente
        /// </summary>
        public bool UpdatePreset(SavedSearchFilter filter)
        {
            lock (_lock)
            {
                var existing = _presets.FirstOrDefault(p => p.Id == filter.Id);
                if (existing == null)
                    return false;

                _presets.Remove(existing);
                _presets.Add(filter);

                SavePresets();
                PresetUpdated?.Invoke(this, filter);

                return true;
            }
        }

        /// <summary>
        /// Elimina un preset
        /// </summary>
        public bool DeletePreset(string id)
        {
            lock (_lock)
            {
                var preset = _presets.FirstOrDefault(p => p.Id == id);
                if (preset == null)
                    return false;

                _presets.Remove(preset);
                SavePresets();
                PresetRemoved?.Invoke(this, preset);

                return true;
            }
        }

        /// <summary>
        /// Marca un preset como usado (actualiza estadísticas)
        /// </summary>
        public void MarkAsUsed(string id)
        {
            lock (_lock)
            {
                var preset = _presets.FirstOrDefault(p => p.Id == id);
                if (preset != null)
                {
                    preset.LastUsed = DateTime.Now;
                    preset.TimesUsed++;
                    SavePresets();
                }
            }
        }

        /// <summary>
        /// Exporta presets a un archivo JSON
        /// </summary>
        public void ExportPresets(string filePath, List<string> presetIds = null)
        {
            lock (_lock)
            {
                var presetsToExport = presetIds == null
                    ? _presets
                    : _presets.Where(p => presetIds.Contains(p.Id)).ToList();

                var json = JsonSerializer.Serialize(presetsToExport, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(filePath, json);
            }
        }

        /// <summary>
        /// Importa presets desde un archivo JSON
        /// </summary>
        public int ImportPresets(string filePath, bool overwriteExisting = false)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var importedPresets = JsonSerializer.Deserialize<List<SavedSearchFilter>>(json);

                if (importedPresets == null || importedPresets.Count == 0)
                    return 0;

                lock (_lock)
                {
                    int imported = 0;
                    foreach (var preset in importedPresets)
                    {
                        var existing = _presets.FirstOrDefault(p => p.Id == preset.Id);
                        
                        if (existing != null && !overwriteExisting)
                        {
                            // Crear nuevo ID para evitar conflictos
                            preset.Id = Guid.NewGuid().ToString();
                            preset.Name += " (importado)";
                        }
                        else if (existing != null)
                        {
                            _presets.Remove(existing);
                        }

                        _presets.Add(preset);
                        imported++;
                    }

                    SavePresets();
                    return imported;
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Carga presets desde el archivo
        /// </summary>
        private void LoadPresets()
        {
            try
            {
                if (File.Exists(_presetsFilePath))
                {
                    var json = File.ReadAllText(_presetsFilePath);
                    _presets = JsonSerializer.Deserialize<List<SavedSearchFilter>>(json) ?? new List<SavedSearchFilter>();
                }
            }
            catch
            {
                _presets = new List<SavedSearchFilter>();
            }
        }

        /// <summary>
        /// Guarda presets al archivo
        /// </summary>
        private void SavePresets()
        {
            try
            {
                var json = JsonSerializer.Serialize(_presets, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var directory = Path.GetDirectoryName(_presetsFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(_presetsFilePath, json);
            }
            catch
            {
                // Ignorar errores de guardado
            }
        }

        /// <summary>
        /// Crea presets por defecto
        /// </summary>
        private void CreateDefaultPresets()
        {
            var defaultPresets = new List<SavedSearchFilter>
            {
                new SavedSearchFilter
                {
                    Name = "Libros Técnicos",
                    Icon = "📚",
                    Description = "Libros técnicos en EPUB/MOBI, alta calidad",
                    AllowedExtensions = new List<string> { "epub", "mobi" },
                    MinSizeBytes = 500 * 1024, // 500 KB
                    ExcludedKeywords = new List<string> { "scan", "ocr", "bad" }
                },
                new SavedSearchFilter
                {
                    Name = "Música Alta Calidad",
                    Icon = "🎵",
                    Description = "FLAC o MP3 320kbps",
                    AllowedExtensions = new List<string> { "flac", "mp3" },
                    MinBitrate = 320,
                    MinSizeBytes = 30 * 1024 * 1024, // 30 MB
                    FreeSlotOnly = true
                },
                new SavedSearchFilter
                {
                    Name = "Audiobooks",
                    Icon = "🎧",
                    Description = "Audiolibros en M4B o MP3",
                    AllowedExtensions = new List<string> { "m4b", "mp3" },
                    MinSizeBytes = 50 * 1024 * 1024, // 50 MB
                    RequiredKeywords = new List<string> { "audiobook" }
                },
                new SavedSearchFilter
                {
                    Name = "Novelas EPUB",
                    Icon = "📖",
                    Description = "Novelas en formato EPUB",
                    AllowedExtensions = new List<string> { "epub" },
                    MinSizeBytes = 200 * 1024, // 200 KB
                    MaxSizeBytes = 5 * 1024 * 1024, // 5 MB
                    ExcludedKeywords = new List<string> { "sample", "preview" }
                },
                new SavedSearchFilter
                {
                    Name = "Música FLAC",
                    Icon = "💿",
                    Description = "Solo archivos FLAC sin comprimir",
                    AllowedExtensions = new List<string> { "flac" },
                    MinSizeBytes = 20 * 1024 * 1024, // 20 MB
                    FreeSlotOnly = true
                }
            };

            foreach (var preset in defaultPresets)
            {
                _presets.Add(preset);
            }

            SavePresets();
        }
    }
}
