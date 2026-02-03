using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core.AI
{
    /// <summary>
    /// Sistema de respuestas predictivas para comandos comunes
    /// </summary>
    public class PredictiveResponses
    {
        private static readonly Dictionary<string, string> quickResponses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Comandos de estado
            ["estado"] = "📊 Mostrando estado de descargas...",
            ["status"] = "📊 Showing download status...",
            ["ayuda"] = "📚 Mostrando comandos disponibles...",
            ["help"] = "📚 Showing available commands...",
            
            // Comandos de gestión
            ["pausar"] = "⏸️ Pausando descargas...",
            ["pause"] = "⏸️ Pausing downloads...",
            ["reanudar"] = "▶️ Reanudando descargas...",
            ["resume"] = "▶️ Resuming downloads...",
            
            // Comandos de información
            ["estadísticas"] = "📈 Generando estadísticas...",
            ["stats"] = "📈 Generating statistics...",
            ["metricas"] = "📊 Mostrando métricas del sistema...",
            ["metrics"] = "📊 Showing system metrics...",
            
            // Comandos de consulta
            ["series"] = "📚 Analizando series detectadas...",
            ["atajos"] = "⚡ Mostrando atajos personalizados...",
            ["shortcuts"] = "⚡ Showing custom shortcuts...",
            ["reglas"] = "📋 Mostrando reglas de automatización...",
            ["rules"] = "📋 Showing automation rules..."
        };

        private static readonly Dictionary<string, List<string>> autocompleteSuggestions = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["busca"] = new List<string> { "busca libros de ", "busca autores: ", "busca sobre " },
            ["descarga"] = new List<string> { "descarga todo de ", "descarga autores: ", "descarga en formato " },
            ["search"] = new List<string> { "search books by ", "search authors: ", "search about " },
            ["download"] = new List<string> { "download all from ", "download authors: ", "download in format " },
            ["crea"] = new List<string> { "crea atajo ", "crea regla ", "crea búsqueda " },
            ["create"] = new List<string> { "create shortcut ", "create rule ", "create search " },
            ["reporte"] = new List<string> { "reporte semanal", "reporte diario", "reporte mensual" },
            ["report"] = new List<string> { "weekly report", "daily report", "monthly report" },
            ["analiza"] = new List<string> { "analiza mi biblioteca", "analiza descargas", "analiza búsquedas" },
            ["analyze"] = new List<string> { "analyze my library", "analyze downloads", "analyze searches" }
        };

        /// <summary>
        /// Obtiene una respuesta instantánea si el comando es conocido
        /// </summary>
        public static string GetQuickResponse(string input)
        {
            var trimmed = input.Trim();
            
            if (quickResponses.TryGetValue(trimmed, out var response))
                return response;

            return null;
        }

        /// <summary>
        /// Obtiene sugerencias de autocompletado
        /// </summary>
        public static List<string> GetAutocompleteSuggestions(string partialInput)
        {
            if (string.IsNullOrWhiteSpace(partialInput))
                return new List<string>();

            var suggestions = new List<string>();
            var lower = partialInput.ToLower();

            // Buscar coincidencias exactas
            foreach (var kvp in autocompleteSuggestions)
            {
                if (lower.StartsWith(kvp.Key.ToLower()))
                {
                    suggestions.AddRange(kvp.Value);
                }
            }

            // Buscar coincidencias parciales en comandos conocidos
            foreach (var cmd in quickResponses.Keys)
            {
                if (cmd.ToLower().StartsWith(lower))
                {
                    suggestions.Add(cmd);
                }
            }

            return suggestions.Distinct().Take(5).ToList();
        }

        /// <summary>
        /// Registra un nuevo comando frecuente
        /// </summary>
        public static void LearnCommand(string command, string response)
        {
            if (!quickResponses.ContainsKey(command))
            {
                quickResponses[command] = response;
            }
        }

        /// <summary>
        /// Obtiene estadísticas de uso de respuestas rápidas
        /// </summary>
        public static int GetQuickResponseCount()
        {
            return quickResponses.Count;
        }
    }
}
