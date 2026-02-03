using System;
using System.Collections.Generic;
using System.Text;
using SlskDown.Core;

namespace SlskDown.Core.AI
{
    /// <summary>
    /// Generador de scripts y automatizaciones desde lenguaje natural
    /// </summary>
    public class AutomationScriptGenerator
    {
        public static AutomationRule GenerateRuleFromNaturalLanguage(string description)
        {
            var rule = new AutomationRule
            {
                Name = ExtractRuleName(description),
                Description = description,
                Enabled = true
            };

            var lower = description.ToLower();

            // Detectar condiciones
            if (lower.Contains("asimov") || lower.Contains("autor"))
            {
                rule.AuthorPattern = ExtractAuthor(description);
            }

            if (lower.Contains("epub") || lower.Contains("pdf") || lower.Contains("mobi"))
            {
                rule.ExtensionFilter = ExtractExtension(description);
            }

            if (lower.Contains("español") || lower.Contains("spanish"))
            {
                rule.SpanishOnly = true;
            }

            if (lower.Contains("mayor") || lower.Contains("más de"))
            {
                rule.MinSizeBytes = ExtractMinSize(description);
            }

            // Detectar acciones
            if (lower.Contains("descarga") || lower.Contains("download") || lower.Contains("auto"))
            {
                rule.AutoDownload = true;
            }

            if (lower.Contains("busca") || lower.Contains("search"))
            {
                rule.AutoSearch = true;
            }

            return rule;
        }

        private static string ExtractRuleName(string description)
        {
            // Intentar extraer nombre entre comillas
            var match = System.Text.RegularExpressions.Regex.Match(description, "\"([^\"]+)\"");
            if (match.Success)
                return match.Groups[1].Value;

            // Generar nombre automático
            return $"Regla {DateTime.Now:yyyyMMdd_HHmmss}";
        }

        private static string ExtractAuthor(string description)
        {
            var patterns = new[] { "de ", "autor ", "author ", "from " };
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

        private static string ExtractExtension(string description)
        {
            var extensions = new[] { "epub", "pdf", "mobi", "azw3", "txt" };
            foreach (var ext in extensions)
            {
                if (description.Contains(ext, StringComparison.OrdinalIgnoreCase))
                    return ext;
            }
            return null;
        }

        private static long? ExtractMinSize(string description)
        {
            var match = System.Text.RegularExpressions.Regex.Match(description, @"(\d+)\s*(kb|mb|gb)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var value = long.Parse(match.Groups[1].Value);
                var unit = match.Groups[2].Value.ToLower();
                
                return unit switch
                {
                    "kb" => value * 1024,
                    "mb" => value * 1024 * 1024,
                    "gb" => value * 1024 * 1024 * 1024,
                    _ => value * 1024 * 1024
                };
            }
            return null;
        }

        public static string GenerateRulePreview(AutomationRule rule)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"📋 VISTA PREVIA DE REGLA: {rule.Name}\n");
            sb.AppendLine($"Descripción: {rule.Description}\n");
            
            sb.AppendLine("CONDICIONES:");
            if (!string.IsNullOrEmpty(rule.AuthorPattern))
                sb.AppendLine($"  • Autor contiene: {rule.AuthorPattern}");
            if (!string.IsNullOrEmpty(rule.ExtensionFilter))
                sb.AppendLine($"  • Formato: {rule.ExtensionFilter.ToUpper()}");
            if (rule.SpanishOnly)
                sb.AppendLine($"  • Solo español");
            if (rule.MinSizeBytes.HasValue)
                sb.AppendLine($"  • Tamaño mínimo: {FormatSize(rule.MinSizeBytes.Value)}");
            
            sb.AppendLine("\nACCIONES:");
            if (rule.AutoDownload)
                sb.AppendLine($"  • ⬇️ Descargar automáticamente");
            if (rule.AutoSearch)
                sb.AppendLine($"  • 🔍 Buscar automáticamente");
            
            sb.AppendLine($"\nEstado: {(rule.Enabled ? "✅ Activa" : "❌ Inactiva")}");
            
            return sb.ToString();
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }
}
