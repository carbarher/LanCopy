using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;

namespace SlskDown.Core.AI
{
    public class Macro
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> Commands { get; set; } = new List<string>();
        public DateTime Created { get; set; } = DateTime.Now;
        public int TimesExecuted { get; set; } = 0;
    }

    /// <summary>
    /// Sistema de macros conversacionales - secuencias de comandos
    /// </summary>
    public class ConversationalMacros
    {
        private Dictionary<string, Macro> macros = new Dictionary<string, Macro>(StringComparer.OrdinalIgnoreCase);
        private string macrosFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "macros.json");

        public ConversationalMacros()
        {
            // Macros predefinidas
            CreateDefaultMacros();
        }

        private void CreateDefaultMacros()
        {
            // Rutina nocturna
            macros["rutina nocturna"] = new Macro
            {
                Name = "rutina nocturna",
                Description = "Busca fantasía, filtra español epub, descarga y genera reporte",
                Commands = new List<string>
                {
                    "busca libros de fantasía",
                    "filtra: español, epub, >1MB",
                    "descarga los 5 mejores",
                    "reporte diario"
                }
            };

            // Búsqueda rápida
            macros["búsqueda rápida"] = new Macro
            {
                Name = "búsqueda rápida",
                Description = "Busca autor favorito y descarga automáticamente",
                Commands = new List<string>
                {
                    "busca autores favoritos",
                    "descarga todo en español epub"
                }
            };

            // Limpieza
            macros["limpieza"] = new Macro
            {
                Name = "limpieza",
                Description = "Limpia caché, historial y archivos temporales",
                Commands = new List<string>
                {
                    "limpiar caché",
                    "pausar descargas fallidas",
                    "estadísticas"
                }
            };
        }

        public void CreateMacro(string name, List<string> commands, string description = "")
        {
            macros[name] = new Macro
            {
                Name = name,
                Description = description,
                Commands = commands
            };
            Save();
        }

        public List<string> ExecuteMacro(string name)
        {
            if (macros.TryGetValue(name, out var macro))
            {
                macro.TimesExecuted++;
                Save();
                return macro.Commands;
            }

            return null;
        }

        public bool MacroExists(string name)
        {
            return macros.ContainsKey(name);
        }

        public List<Macro> GetAllMacros()
        {
            return macros.Values.OrderByDescending(m => m.TimesExecuted).ToList();
        }

        public string GenerateMacroList()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("⚡ MACROS DISPONIBLES:\n");

            foreach (var macro in macros.Values.OrderBy(m => m.Name))
            {
                sb.AppendLine($"📌 {macro.Name}");
                if (!string.IsNullOrEmpty(macro.Description))
                    sb.AppendLine($"   {macro.Description}");
                sb.AppendLine($"   Comandos: {macro.Commands.Count}");
                if (macro.TimesExecuted > 0)
                    sb.AppendLine($"   Ejecutada: {macro.TimesExecuted} veces");
                sb.AppendLine();
            }

            sb.AppendLine("Usa: 'ejecuta macro [nombre]' o simplemente '[nombre]'");

            return sb.ToString();
        }

        public void Load()
        {
            try
            {
                if (File.Exists(macrosFile))
                {
                    var json = File.ReadAllText(macrosFile);
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, Macro>>(json);
                    if (loaded != null)
                    {
                        foreach (var kvp in loaded)
                        {
                            macros[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                var dataDir = Path.GetDirectoryName(macrosFile);
                Directory.CreateDirectory(dataDir);

                var json = JsonSerializer.Serialize(macros, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(macrosFile, json);
            }
            catch { }
        }
    }
}
