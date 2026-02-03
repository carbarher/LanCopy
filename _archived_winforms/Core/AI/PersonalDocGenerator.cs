using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SlskDown.Core.AI
{
    /// <summary>
    /// Generador de documentación personal basada en historial de uso
    /// </summary>
    public class PersonalDocGenerator
    {
        public static string GenerateUserGuide(Dictionary<string, int> commandStats, List<string> favoriteAuthors)
        {
            var sb = new StringBuilder();
            sb.AppendLine("📖 TU GUÍA PERSONAL DE USO\n");
            sb.AppendLine($"Generada: {DateTime.Now:dd/MM/yyyy HH:mm}\n");
            sb.AppendLine(new string('═', 50));
            sb.AppendLine();

            // Comandos más usados
            if (commandStats.Count > 0)
            {
                sb.AppendLine("⭐ TUS COMANDOS FAVORITOS:\n");
                foreach (var cmd in commandStats.OrderByDescending(x => x.Value).Take(5))
                {
                    sb.AppendLine($"  • {cmd.Key} (usado {cmd.Value} veces)");
                }
                sb.AppendLine();
            }

            // Autores favoritos
            if (favoriteAuthors != null && favoriteAuthors.Count > 0)
            {
                sb.AppendLine("📚 TUS AUTORES FAVORITOS:\n");
                foreach (var author in favoriteAuthors.Take(10))
                {
                    sb.AppendLine($"  • {author}");
                }
                sb.AppendLine();
            }

            // Tips personalizados
            sb.AppendLine("💡 TIPS PERSONALIZADOS:\n");
            
            if (commandStats.GetValueOrDefault("busca", 0) > commandStats.GetValueOrDefault("descarga", 0))
            {
                sb.AppendLine("  • Exploras mucho antes de descargar. Considera usar filtros");
                sb.AppendLine("    para reducir resultados y encontrar más rápido.");
            }

            if (favoriteAuthors != null && favoriteAuthors.Count > 5)
            {
                sb.AppendLine("  • Tienes muchos autores favoritos. Crea reglas de auto-descarga");
                sb.AppendLine("    para automatizar tus búsquedas frecuentes.");
            }

            sb.AppendLine();
            sb.AppendLine("🚀 ATAJOS RECOMENDADOS:\n");
            sb.AppendLine("  • Ctrl+Enter - Enviar mensaje en el chat");
            sb.AppendLine("  • ↑/↓ - Navegar historial de comandos");
            sb.AppendLine("  • 'dashboard' - Ver métricas de IA");
            sb.AppendLine("  • 'sugerencias' - Ver recomendaciones");

            return sb.ToString();
        }

        public static string GenerateWorkflowAnalysis(List<string> recentCommands)
        {
            var sb = new StringBuilder();
            sb.AppendLine("🔄 ANÁLISIS DE TU FLUJO DE TRABAJO\n");
            sb.AppendLine(new string('═', 50));
            sb.AppendLine();

            // Detectar patrones
            var patterns = DetectPatterns(recentCommands);

            if (patterns.Count > 0)
            {
                sb.AppendLine("📊 PATRONES DETECTADOS:\n");
                foreach (var pattern in patterns)
                {
                    sb.AppendLine($"  • {pattern}");
                }
                sb.AppendLine();
            }

            // Sugerencias de optimización
            sb.AppendLine("⚡ SUGERENCIAS DE OPTIMIZACIÓN:\n");
            
            var searchCount = recentCommands.Count(c => c.Contains("busca", StringComparison.OrdinalIgnoreCase));
            var downloadCount = recentCommands.Count(c => c.Contains("descarga", StringComparison.OrdinalIgnoreCase));

            if (searchCount > downloadCount * 3)
            {
                sb.AppendLine("  • Haces muchas búsquedas. Considera:");
                sb.AppendLine("    - Usar filtros más específicos");
                sb.AppendLine("    - Crear atajos para búsquedas frecuentes");
                sb.AppendLine("    - Activar auto-descarga para autores favoritos");
            }

            if (recentCommands.Count > 20)
            {
                sb.AppendLine("  • Eres un usuario activo. Considera:");
                sb.AppendLine("    - Usar el modo experto para mensajes más técnicos");
                sb.AppendLine("    - Crear más reglas de automatización");
                sb.AppendLine("    - Revisar el dashboard de métricas regularmente");
            }

            return sb.ToString();
        }

        private static List<string> DetectPatterns(List<string> commands)
        {
            var patterns = new List<string>();

            // Detectar búsquedas repetidas del mismo autor
            var authorSearches = commands
                .Where(c => c.Contains("busca", StringComparison.OrdinalIgnoreCase))
                .GroupBy(c => ExtractAuthor(c))
                .Where(g => !string.IsNullOrEmpty(g.Key) && g.Count() > 2)
                .ToList();

            foreach (var group in authorSearches)
            {
                patterns.Add($"Buscas frecuentemente a '{group.Key}' ({group.Count()} veces)");
            }

            // Detectar horarios de uso
            // (Esto requeriría timestamps, simplificado aquí)
            if (commands.Count > 10)
            {
                patterns.Add("Usas la aplicación regularmente");
            }

            return patterns;
        }

        private static string ExtractAuthor(string command)
        {
            var patterns = new[] { "de ", "by ", "autor " };
            foreach (var pattern in patterns)
            {
                var index = command.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    var afterPattern = command.Substring(index + pattern.Length);
                    var words = afterPattern.Split(new[] { ' ', ',', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length > 0)
                        return words[0];
                }
            }
            return null;
        }

        public static string GenerateQuickReference()
        {
            var sb = new StringBuilder();
            sb.AppendLine("📋 REFERENCIA RÁPIDA DE COMANDOS\n");
            sb.AppendLine(new string('═', 50));
            sb.AppendLine();

            sb.AppendLine("🔍 BÚSQUEDA:");
            sb.AppendLine("  • busca [autor/título]");
            sb.AppendLine("  • busca autores: Asimov, Clarke");
            sb.AppendLine("  • libros sobre [tema]");
            sb.AppendLine();

            sb.AppendLine("⬇️ DESCARGA:");
            sb.AppendLine("  • descarga [autor]");
            sb.AppendLine("  • bájate todo de [autor] en español");
            sb.AppendLine();

            sb.AppendLine("📊 INFORMACIÓN:");
            sb.AppendLine("  • estado - Ver descargas activas");
            sb.AppendLine("  • estadísticas - Resumen de actividad");
            sb.AppendLine("  • dashboard - Métricas de IA");
            sb.AppendLine("  • historial - Ver historial inteligente");
            sb.AppendLine();

            sb.AppendLine("⚙️ CONFIGURACIÓN:");
            sb.AppendLine("  • reglas - Ver reglas de automatización");
            sb.AppendLine("  • atajos - Ver atajos personalizados");
            sb.AppendLine("  • sugerencias - Ver recomendaciones");
            sb.AppendLine();

            sb.AppendLine("🎯 AVANZADO:");
            sb.AppendLine("  • crea regla \"nombre\" para autor \"X\" auto-descargar");
            sb.AppendLine("  • crea atajo \"nombre\" = comando");
            sb.AppendLine("  • analiza mi biblioteca");
            sb.AppendLine("  • reporte semanal");

            return sb.ToString();
        }
    }
}
