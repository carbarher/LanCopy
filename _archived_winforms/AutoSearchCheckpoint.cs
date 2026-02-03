using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace SlskDown
{
    /// <summary>
    /// Sistema de checkpoint para búsqueda automática
    /// Guarda progreso cada N autores para reanudar si hay desconexión
    /// </summary>
    public class AutoSearchCheckpoint
    {
        public DateTime Timestamp { get; set; }
        public List<string> ProcessedAuthors { get; set; } = new List<string>();
        public List<string> RemainingAuthors { get; set; } = new List<string>();
        public int TotalFilesFound { get; set; }
        public int CurrentRound { get; set; }
        public Dictionary<string, int> AuthorFileCount { get; set; } = new Dictionary<string, int>();

        private static string GetCheckpointPath(string dataDir)
        {
            return Path.Combine(dataDir, "auto_search_checkpoint.json");
        }

        /// <summary>
        /// Guarda checkpoint del progreso actual
        /// </summary>
        public static void Save(string dataDir, AutoSearchCheckpoint checkpoint)
        {
            try
            {
                var path = GetCheckpointPath(dataDir);
                var json = JsonConvert.SerializeObject(checkpoint, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error guardando checkpoint: {ex.Message}");
            }
        }

        /// <summary>
        /// Carga checkpoint guardado (si existe)
        /// </summary>
        public static AutoSearchCheckpoint Load(string dataDir)
        {
            try
            {
                var path = GetCheckpointPath(dataDir);
                if (!File.Exists(path))
                    return null;

                var json = File.ReadAllText(path);
                var checkpoint = JsonConvert.DeserializeObject<AutoSearchCheckpoint>(json);

                // Verificar que no sea muy antiguo (más de 24 horas)
                if ((DateTime.Now - checkpoint.Timestamp).TotalHours > 24)
                {
                    Delete(dataDir);
                    return null;
                }

                return checkpoint;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error cargando checkpoint: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Elimina checkpoint guardado
        /// </summary>
        public static void Delete(string dataDir)
        {
            try
            {
                var path = GetCheckpointPath(dataDir);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }

        /// <summary>
        /// Verifica si hay checkpoint disponible para reanudar
        /// </summary>
        public static bool HasCheckpoint(string dataDir)
        {
            var checkpoint = Load(dataDir);
            return checkpoint != null && checkpoint.RemainingAuthors.Count > 0;
        }
    }
}
