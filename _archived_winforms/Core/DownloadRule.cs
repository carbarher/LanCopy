using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core
{
    public enum RuleConditionType
    {
        AuthorEquals,
        AuthorContains,
        FileSizeGreaterThan,
        FileSizeLessThan,
        ExtensionEquals,
        LanguageEquals,
        FilenameContains,
        FilenameNotContains
    }

    public enum RuleActionType
    {
        AutoDownload,
        AddToQueue,
        SkipFile,
        SetPriority,
        Notify
    }

    public class RuleCondition
    {
        public RuleConditionType Type { get; set; }
        public string Value { get; set; }
        public bool Enabled { get; set; } = true;
    }

    public class RuleAction
    {
        public RuleActionType Type { get; set; }
        public string Parameter { get; set; }
        public bool Enabled { get; set; } = true;
    }

    public class DownloadRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public bool Enabled { get; set; } = true;
        public List<RuleCondition> Conditions { get; set; } = new List<RuleCondition>();
        public List<RuleAction> Actions { get; set; } = new List<RuleAction>();
        public int Priority { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int TimesTriggered { get; set; } = 0;

        public bool Matches(string filename, string author, long fileSize, string extension, bool isSpanish)
        {
            if (!Enabled || Conditions.Count == 0)
                return false;

            // Todas las condiciones deben cumplirse (AND)
            foreach (var condition in Conditions.Where(c => c.Enabled))
            {
                bool matches = condition.Type switch
                {
                    RuleConditionType.AuthorEquals => 
                        author?.Equals(condition.Value, StringComparison.OrdinalIgnoreCase) ?? false,
                    
                    RuleConditionType.AuthorContains => 
                        author?.Contains(condition.Value, StringComparison.OrdinalIgnoreCase) ?? false,
                    
                    RuleConditionType.FileSizeGreaterThan => 
                        long.TryParse(condition.Value, out var minSize) && fileSize > minSize * 1024,
                    
                    RuleConditionType.FileSizeLessThan => 
                        long.TryParse(condition.Value, out var maxSize) && fileSize < maxSize * 1024,
                    
                    RuleConditionType.ExtensionEquals => 
                        extension?.Equals(condition.Value, StringComparison.OrdinalIgnoreCase) ?? false,
                    
                    RuleConditionType.LanguageEquals => 
                        (condition.Value.Equals("español", StringComparison.OrdinalIgnoreCase) && isSpanish) ||
                        (condition.Value.Equals("inglés", StringComparison.OrdinalIgnoreCase) && !isSpanish),
                    
                    RuleConditionType.FilenameContains => 
                        filename?.Contains(condition.Value, StringComparison.OrdinalIgnoreCase) ?? false,
                    
                    RuleConditionType.FilenameNotContains => 
                        !(filename?.Contains(condition.Value, StringComparison.OrdinalIgnoreCase) ?? false),
                    
                    _ => false
                };

                if (!matches)
                    return false;
            }

            return true;
        }

        public override string ToString()
        {
            var conditionsStr = string.Join(" Y ", Conditions.Select(c => 
                $"{c.Type}: {c.Value}"));
            var actionsStr = string.Join(", ", Actions.Select(a => a.Type.ToString()));
            return $"{Name} - SI {conditionsStr} ENTONCES {actionsStr}";
        }
    }

    public static class RuleEngine
    {
        private static List<DownloadRule> rules = new List<DownloadRule>();

        public static void AddRule(DownloadRule rule)
        {
            if (rule != null && !rules.Any(r => r.Id == rule.Id))
            {
                rules.Add(rule);
                SaveRules();
            }
        }

        public static void RemoveRule(string ruleId)
        {
            rules.RemoveAll(r => r.Id == ruleId);
            SaveRules();
        }

        public static void UpdateRule(DownloadRule rule)
        {
            var existing = rules.FirstOrDefault(r => r.Id == rule.Id);
            if (existing != null)
            {
                var index = rules.IndexOf(existing);
                rules[index] = rule;
                SaveRules();
            }
        }

        public static List<DownloadRule> GetAllRules() => rules.ToList();

        public static List<DownloadRule> GetMatchingRules(string filename, string author, long fileSize, string extension, bool isSpanish)
        {
            return rules.Where(r => r.Enabled && r.Matches(filename, author, fileSize, extension, isSpanish))
                       .OrderByDescending(r => r.Priority)
                       .ToList();
        }

        public static bool ShouldAutoDownload(string filename, string author, long fileSize, string extension, bool isSpanish)
        {
            var matchingRules = GetMatchingRules(filename, author, fileSize, extension, isSpanish);
            
            foreach (var rule in matchingRules)
            {
                rule.TimesTriggered++;
                
                if (rule.Actions.Any(a => a.Enabled && a.Type == RuleActionType.AutoDownload))
                    return true;
                
                if (rule.Actions.Any(a => a.Enabled && a.Type == RuleActionType.SkipFile))
                    return false;
            }

            return false;
        }

        public static void LoadRules()
        {
            try
            {
                var rulesFile = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "data", "download_rules.json");

                if (System.IO.File.Exists(rulesFile))
                {
                    var json = System.IO.File.ReadAllText(rulesFile);
                    rules = System.Text.Json.JsonSerializer.Deserialize<List<DownloadRule>>(json) 
                           ?? new List<DownloadRule>();
                }
            }
            catch { }
        }

        public static void SaveRules()
        {
            try
            {
                var dataDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
                System.IO.Directory.CreateDirectory(dataDir);

                var rulesFile = System.IO.Path.Combine(dataDir, "download_rules.json");
                var json = System.Text.Json.JsonSerializer.Serialize(rules, 
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                
                System.IO.File.WriteAllText(rulesFile, json);
            }
            catch { }
        }

        public static DownloadRule CreateQuickRule(string name, string author, bool autoDownload = true)
        {
            return new DownloadRule
            {
                Name = name,
                Conditions = new List<RuleCondition>
                {
                    new RuleCondition
                    {
                        Type = RuleConditionType.AuthorEquals,
                        Value = author
                    }
                },
                Actions = new List<RuleAction>
                {
                    new RuleAction
                    {
                        Type = autoDownload ? RuleActionType.AutoDownload : RuleActionType.AddToQueue
                    }
                }
            };
        }
    }
}
