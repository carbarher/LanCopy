using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core.AI
{
    public class Contradiction
    {
        public string OldPreference { get; set; }
        public string NewPreference { get; set; }
        public DateTime OldTimestamp { get; set; }
        public DateTime NewTimestamp { get; set; }
        public string Category { get; set; }
    }

    /// <summary>
    /// Detector de contradicciones en preferencias del usuario
    /// </summary>
    public class ContradictionDetector
    {
        private Dictionary<string, (string value, DateTime timestamp)> preferences = new Dictionary<string, (string, DateTime)>();

        public Contradiction DetectContradiction(string category, string newValue)
        {
            if (preferences.TryGetValue(category, out var existing))
            {
                // Verificar si es diferente
                if (!existing.value.Equals(newValue, StringComparison.OrdinalIgnoreCase))
                {
                    return new Contradiction
                    {
                        OldPreference = existing.value,
                        NewPreference = newValue,
                        OldTimestamp = existing.timestamp,
                        NewTimestamp = DateTime.Now,
                        Category = category
                    };
                }
            }

            return null;
        }

        public void UpdatePreference(string category, string value)
        {
            preferences[category] = (value, DateTime.Now);
        }

        public string GetPreference(string category)
        {
            return preferences.TryGetValue(category, out var pref) ? pref.value : null;
        }

        public string GenerateContradictionMessage(Contradiction contradiction)
        {
            var daysAgo = (DateTime.Now - contradiction.OldTimestamp).TotalDays;
            var timeRef = daysAgo < 1 ? "hoy mismo" :
                         daysAgo < 7 ? $"hace {(int)daysAgo} días" :
                         $"hace {(int)(daysAgo / 7)} semanas";

            return $"🤔 CAMBIO DE PREFERENCIA DETECTADO\n\n" +
                   $"Categoría: {contradiction.Category}\n" +
                   $"Antes ({timeRef}): {contradiction.OldPreference}\n" +
                   $"Ahora: {contradiction.NewPreference}\n\n" +
                   $"¿Quieres que actualice tu preferencia a '{contradiction.NewPreference}'?";
        }

        public Dictionary<string, string> GetAllPreferences()
        {
            return preferences.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.value);
        }
    }
}
