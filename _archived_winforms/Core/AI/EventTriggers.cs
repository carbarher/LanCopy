using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core.AI
{
    public enum TriggerEvent
    {
        DownloadCompleted,
        DownloadFailed,
        SearchCompleted,
        QueueEmpty,
        UserOnline,
        TimeOfDay,
        DiskSpaceLow
    }

    public class Trigger
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public TriggerEvent Event { get; set; }
        public Dictionary<string, string> Conditions { get; set; } = new Dictionary<string, string>();
        public List<string> Actions { get; set; } = new List<string>();
        public bool Enabled { get; set; } = true;
        public int TimesTriggered { get; set; } = 0;
        public DateTime LastTriggered { get; set; }
    }

    /// <summary>
    /// Sistema de triggers basados en eventos
    /// </summary>
    public class EventTriggers
    {
        private List<Trigger> triggers = new List<Trigger>();

        public void AddTrigger(Trigger trigger)
        {
            triggers.Add(trigger);
        }

        public List<string> CheckTriggers(TriggerEvent eventType, Dictionary<string, string> eventData = null)
        {
            var actionsToExecute = new List<string>();

            foreach (var trigger in triggers.Where(t => t.Enabled && t.Event == eventType))
            {
                if (EvaluateConditions(trigger, eventData))
                {
                    trigger.TimesTriggered++;
                    trigger.LastTriggered = DateTime.Now;
                    actionsToExecute.AddRange(trigger.Actions);
                }
            }

            return actionsToExecute;
        }

        private bool EvaluateConditions(Trigger trigger, Dictionary<string, string> eventData)
        {
            if (trigger.Conditions.Count == 0)
                return true;

            if (eventData == null)
                return false;

            foreach (var condition in trigger.Conditions)
            {
                if (!eventData.TryGetValue(condition.Key, out var value))
                    return false;

                if (!value.Equals(condition.Value, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        public Trigger CreateTriggerFromNaturalLanguage(string description)
        {
            var trigger = new Trigger
            {
                Name = ExtractTriggerName(description)
            };

            var lower = description.ToLower();

            // Detectar evento
            if (lower.Contains("cuando termine") || lower.Contains("al completar"))
                trigger.Event = TriggerEvent.DownloadCompleted;
            else if (lower.Contains("cuando falle") || lower.Contains("si falla"))
                trigger.Event = TriggerEvent.DownloadFailed;
            else if (lower.Contains("cuando termine búsqueda") || lower.Contains("después de buscar"))
                trigger.Event = TriggerEvent.SearchCompleted;
            else if (lower.Contains("cola vacía") || lower.Contains("sin descargas"))
                trigger.Event = TriggerEvent.QueueEmpty;

            // Detectar condiciones
            if (lower.Contains("de asimov") || lower.Contains("autor"))
            {
                trigger.Conditions["author"] = ExtractAuthor(description);
            }

            // Detectar acciones
            if (lower.Contains("busca") || lower.Contains("search"))
                trigger.Actions.Add("buscar más del mismo autor");
            
            if (lower.Contains("notifica") || lower.Contains("avisa"))
                trigger.Actions.Add("notificar");

            if (lower.Contains("descarga") || lower.Contains("download"))
                trigger.Actions.Add("descargar automáticamente");

            return trigger;
        }

        private string ExtractTriggerName(string description)
        {
            return description.Length > 50 
                ? description.Substring(0, 47) + "..." 
                : description;
        }

        private string ExtractAuthor(string description)
        {
            var patterns = new[] { "de ", "autor ", "author " };
            foreach (var pattern in patterns)
            {
                var index = description.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    var afterPattern = description.Substring(index + pattern.Length);
                    var words = afterPattern.Split(new[] { ' ', ',', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length > 0)
                        return words[0];
                }
            }
            return null;
        }

        public string GenerateTriggerList()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("⚡ TRIGGERS ACTIVOS:\n");

            if (triggers.Count == 0)
            {
                sb.AppendLine("No hay triggers configurados.");
                sb.AppendLine("\nEjemplo: 'crea trigger: cuando termine descarga de Asimov, busca más del mismo género'");
                return sb.ToString();
            }

            foreach (var trigger in triggers.OrderByDescending(t => t.TimesTriggered))
            {
                var status = trigger.Enabled ? "✅" : "❌";
                sb.AppendLine($"{status} {trigger.Name}");
                sb.AppendLine($"   Evento: {GetEventName(trigger.Event)}");
                sb.AppendLine($"   Acciones: {string.Join(", ", trigger.Actions)}");
                if (trigger.TimesTriggered > 0)
                    sb.AppendLine($"   Ejecutado: {trigger.TimesTriggered} veces");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string GetEventName(TriggerEvent evt)
        {
            return evt switch
            {
                TriggerEvent.DownloadCompleted => "Descarga completada",
                TriggerEvent.DownloadFailed => "Descarga fallida",
                TriggerEvent.SearchCompleted => "Búsqueda completada",
                TriggerEvent.QueueEmpty => "Cola vacía",
                TriggerEvent.UserOnline => "Usuario online",
                TriggerEvent.TimeOfDay => "Hora del día",
                TriggerEvent.DiskSpaceLow => "Espacio bajo",
                _ => evt.ToString()
            };
        }

        public List<Trigger> GetAllTriggers() => triggers;

        public void RemoveTrigger(string id)
        {
            triggers.RemoveAll(t => t.Id == id);
        }

        public void EnableTrigger(string id, bool enabled)
        {
            var trigger = triggers.FirstOrDefault(t => t.Id == id);
            if (trigger != null)
                trigger.Enabled = enabled;
        }
    }
}
