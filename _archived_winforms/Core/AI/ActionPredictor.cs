using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core.AI
{
    public class PredictedAction
    {
        public string Action { get; set; }
        public string Description { get; set; }
        public double Confidence { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Predictor de próxima acción basado en patrones de uso
    /// </summary>
    public class ActionPredictor
    {
        private List<string> recentActions = new List<string>();
        private Dictionary<string, List<string>> actionSequences = new Dictionary<string, List<string>>();

        public void RecordAction(string action)
        {
            recentActions.Add(action);
            
            // Mantener últimas 50 acciones
            if (recentActions.Count > 50)
                recentActions.RemoveAt(0);

            // Aprender secuencias
            if (recentActions.Count >= 2)
            {
                var previous = recentActions[recentActions.Count - 2];
                if (!actionSequences.ContainsKey(previous))
                    actionSequences[previous] = new List<string>();
                
                actionSequences[previous].Add(action);
            }
        }

        public PredictedAction PredictNextAction()
        {
            if (recentActions.Count == 0)
                return null;

            var lastAction = recentActions.Last();

            // Buscar patrones comunes
            if (actionSequences.TryGetValue(lastAction, out var nextActions))
            {
                // Encontrar acción más frecuente
                var mostCommon = nextActions
                    .GroupBy(a => a)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault();

                if (mostCommon != null)
                {
                    var confidence = (double)mostCommon.Count() / nextActions.Count;
                    
                    if (confidence > 0.5) // Solo predecir si confianza > 50%
                    {
                        return new PredictedAction
                        {
                            Action = mostCommon.Key,
                            Description = GenerateDescription(lastAction, mostCommon.Key),
                            Confidence = confidence
                        };
                    }
                }
            }

            // Predicciones basadas en patrones conocidos
            return PredictFromKnownPatterns(lastAction);
        }

        private PredictedAction PredictFromKnownPatterns(string lastAction)
        {
            var lower = lastAction.ToLower();

            // Patrón: Búsqueda → Descarga
            if (lower.Contains("busca") || lower.Contains("search"))
            {
                return new PredictedAction
                {
                    Action = "download",
                    Description = "Después de buscar, usualmente descargas. ¿Quieres descargar los resultados?",
                    Confidence = 0.7
                };
            }

            // Patrón: Descarga → Ver Estado
            if (lower.Contains("descarga") || lower.Contains("download"))
            {
                return new PredictedAction
                {
                    Action = "status",
                    Description = "¿Quieres ver el estado de las descargas?",
                    Confidence = 0.6
                };
            }

            // Patrón: Estado → Estadísticas
            if (lower.Contains("estado") || lower.Contains("status"))
            {
                return new PredictedAction
                {
                    Action = "stats",
                    Description = "¿Quieres ver estadísticas detalladas?",
                    Confidence = 0.5
                };
            }

            return null;
        }

        private string GenerateDescription(string previous, string predicted)
        {
            return $"Después de '{previous}', usualmente haces '{predicted}'. ¿Quieres que lo ejecute?";
        }

        public List<string> GetRecentActions(int count = 10)
        {
            return recentActions.TakeLast(count).ToList();
        }

        public Dictionary<string, int> GetActionFrequency()
        {
            return recentActions
                .GroupBy(a => a)
                .ToDictionary(g => g.Key, g => g.Count());
        }
    }
}
