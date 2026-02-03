using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;

namespace SlskDown.Core
{
    public class AutomationRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Enabled { get; set; } = true;
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime? LastTriggered { get; set; }
        public int TimesTriggered { get; set; } = 0;

        // Condiciones
        public string AuthorPattern { get; set; }
        public string TitlePattern { get; set; }
        public string ExtensionFilter { get; set; }
        public long? MinSizeBytes { get; set; }
        public long? MaxSizeBytes { get; set; }
        public bool SpanishOnly { get; set; }

        // Acciones
        public bool AutoDownload { get; set; }
        public bool AutoSearch { get; set; }
        public int? Priority { get; set; }
        public string TargetFolder { get; set; }
    }

    public static class RuleEngine
    {
        private static List<AutomationRule> rules = new List<AutomationRule>();
        private static string rulesFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "automation_rules.json");

        public static void LoadRules()
        {
            try
            {
                if (File.Exists(rulesFile))
                {
                    var json = File.ReadAllText(rulesFile);
                    rules = JsonSerializer.Deserialize<List<AutomationRule>>(json) ?? new List<AutomationRule>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cargando reglas: {ex.Message}");
            }
        }

        public static void SaveRules()
        {
            try
            {
                var dataDir = Path.GetDirectoryName(rulesFile);
                Directory.CreateDirectory(dataDir);

                var json = JsonSerializer.Serialize(rules, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(rulesFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error guardando reglas: {ex.Message}");
            }
        }

        public static void AddRule(AutomationRule rule)
        {
            if (rule == null) return;
            
            rules.Add(rule);
            SaveRules();
        }

        public static void RemoveRule(string ruleId)
        {
            rules.RemoveAll(r => r.Id == ruleId);
            SaveRules();
        }

        public static void UpdateRule(AutomationRule rule)
        {
            var existing = rules.FirstOrDefault(r => r.Id == rule.Id);
            if (existing != null)
            {
                var index = rules.IndexOf(existing);
                rules[index] = rule;
                SaveRules();
            }
        }

        public static List<AutomationRule> GetAllRules() => rules.ToList();

        public static List<AutomationRule> GetEnabledRules() => rules.Where(r => r.Enabled).ToList();

        public static AutomationRule GetRule(string ruleId) => rules.FirstOrDefault(r => r.Id == ruleId);

        public static bool ShouldAutoDownload(string filename, string author, long sizeBytes, string extension)
        {
            foreach (var rule in GetEnabledRules())
            {
                if (!rule.AutoDownload)
                    continue;

                bool matches = true;

                // Verificar autor
                if (!string.IsNullOrEmpty(rule.AuthorPattern))
                {
                    if (!author.Contains(rule.AuthorPattern, StringComparison.OrdinalIgnoreCase))
                        matches = false;
                }

                // Verificar título
                if (!string.IsNullOrEmpty(rule.TitlePattern))
                {
                    if (!filename.Contains(rule.TitlePattern, StringComparison.OrdinalIgnoreCase))
                        matches = false;
                }

                // Verificar extensión
                if (!string.IsNullOrEmpty(rule.ExtensionFilter))
                {
                    if (!extension.Equals(rule.ExtensionFilter, StringComparison.OrdinalIgnoreCase))
                        matches = false;
                }

                // Verificar tamaño
                if (rule.MinSizeBytes.HasValue && sizeBytes < rule.MinSizeBytes.Value)
                    matches = false;

                if (rule.MaxSizeBytes.HasValue && sizeBytes > rule.MaxSizeBytes.Value)
                    matches = false;

                if (matches)
                {
                    rule.LastTriggered = DateTime.Now;
                    rule.TimesTriggered++;
                    SaveRules();
                    return true;
                }
            }

            return false;
        }

        public static AutomationRule CreateQuickRule(string name, string author, bool autoDownload)
        {
            return new AutomationRule
            {
                Name = name,
                Description = $"Auto-descarga para {author}",
                AuthorPattern = author,
                AutoDownload = autoDownload,
                Enabled = true
            };
        }

        public static string GenerateRuleSummary()
        {
            var summary = new System.Text.StringBuilder();
            summary.AppendLine($"📋 REGLAS DE AUTOMATIZACIÓN: {rules.Count}\n");

            if (rules.Count == 0)
            {
                summary.AppendLine("No hay reglas configuradas.");
                return summary.ToString();
            }

            var enabled = rules.Count(r => r.Enabled);
            summary.AppendLine($"Activas: {enabled} | Inactivas: {rules.Count - enabled}\n");

            foreach (var rule in rules.OrderByDescending(r => r.TimesTriggered))
            {
                var status = rule.Enabled ? "✅" : "❌";
                summary.AppendLine($"{status} {rule.Name}");
                summary.AppendLine($"   Autor: {rule.AuthorPattern ?? "Cualquiera"}");
                
                if (rule.AutoDownload)
                    summary.AppendLine($"   Acción: Auto-descargar");
                
                if (rule.TimesTriggered > 0)
                    summary.AppendLine($"   Ejecutada: {rule.TimesTriggered} veces");
                
                summary.AppendLine();
            }

            return summary.ToString();
        }

        public static List<AutomationRule> FindMatchingRules(string filename, string author)
        {
            var matching = new List<AutomationRule>();

            foreach (var rule in GetEnabledRules())
            {
                bool matches = true;

                if (!string.IsNullOrEmpty(rule.AuthorPattern))
                {
                    if (!author.Contains(rule.AuthorPattern, StringComparison.OrdinalIgnoreCase))
                        matches = false;
                }

                if (!string.IsNullOrEmpty(rule.TitlePattern))
                {
                    if (!filename.Contains(rule.TitlePattern, StringComparison.OrdinalIgnoreCase))
                        matches = false;
                }

                if (matches)
                    matching.Add(rule);
            }

            return matching;
        }

        public static void DisableRule(string ruleId)
        {
            var rule = GetRule(ruleId);
            if (rule != null)
            {
                rule.Enabled = false;
                SaveRules();
            }
        }

        public static void EnableRule(string ruleId)
        {
            var rule = GetRule(ruleId);
            if (rule != null)
            {
                rule.Enabled = true;
                SaveRules();
            }
        }

        public static void ClearRules()
        {
            rules.Clear();
            SaveRules();
        }
    }
}
