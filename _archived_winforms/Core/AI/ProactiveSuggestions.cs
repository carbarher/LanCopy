using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core.AI
{
    public class Suggestion
    {
        public string Message { get; set; }
        public string Action { get; set; }
        public int Priority { get; set; } // 1-5, 5 = más importante
        public DateTime Created { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Sistema de sugerencias proactivas basado en patrones de uso
    /// </summary>
    public class ProactiveSuggestions
    {
        private readonly List<Suggestion> suggestions = new List<Suggestion>();
        private DateTime lastAnalysis = DateTime.MinValue;

        /// <summary>
        /// Analiza patrones y genera sugerencias
        /// </summary>
        public List<Suggestion> AnalyzeAndSuggest()
        {
            // Solo analizar cada 5 minutos
            if ((DateTime.Now - lastAnalysis).TotalMinutes < 5)
                return suggestions;

            suggestions.Clear();
            lastAnalysis = DateTime.Now;

            // Analizar historial
            var stats = SmartHistory.GetStats();
            var patterns = SmartHistory.FindPatterns();

            // Sugerencia: Crear reglas automáticas
            if (stats.ContainsKey("searches") && stats["searches"] > 10)
            {
                var frequentSearches = patterns
                    .Where(p => p.StartsWith("Autores frecuentes:"))
                    .FirstOrDefault();

                if (frequentSearches != null)
                {
                    suggestions.Add(new Suggestion
                    {
                        Message = "💡 Noté que buscas frecuentemente los mismos autores. ¿Quieres crear reglas de auto-descarga?",
                        Action = "create_auto_rules",
                        Priority = 4
                    });
                }
            }

            // Sugerencia: Optimizar búsquedas
            if (stats.ContainsKey("searches") && stats["searches"] > 20)
            {
                suggestions.Add(new Suggestion
                {
                    Message = "⚡ Has realizado muchas búsquedas. ¿Quieres crear atajos para las más comunes?",
                    Action = "create_shortcuts",
                    Priority = 3
                });
            }

            // Sugerencia: Limpiar descargas fallidas
            suggestions.Add(new Suggestion
            {
                Message = "🧹 ¿Quieres que limpie las descargas fallidas antiguas?",
                Action = "clean_failed",
                Priority = 2
            });

            // Sugerencia: Generar reporte
            var lastReport = SmartHistory.GetLast("command");
            if (lastReport == null || (DateTime.Now - lastReport.Timestamp).TotalDays > 7)
            {
                suggestions.Add(new Suggestion
                {
                    Message = "📊 Han pasado varios días. ¿Quieres ver un reporte de actividad?",
                    Action = "generate_report",
                    Priority = 3
                });
            }

            return suggestions.OrderByDescending(s => s.Priority).ToList();
        }

        /// <summary>
        /// Obtiene la siguiente sugerencia de alta prioridad
        /// </summary>
        public Suggestion GetNextSuggestion()
        {
            var activeSuggestions = AnalyzeAndSuggest();
            return activeSuggestions.FirstOrDefault(s => s.Priority >= 3);
        }

        /// <summary>
        /// Descarta una sugerencia
        /// </summary>
        public void DismissSuggestion(string action)
        {
            suggestions.RemoveAll(s => s.Action == action);
        }

        /// <summary>
        /// Obtiene todas las sugerencias activas
        /// </summary>
        public List<Suggestion> GetAllSuggestions()
        {
            return suggestions.OrderByDescending(s => s.Priority).ToList();
        }
    }
}
