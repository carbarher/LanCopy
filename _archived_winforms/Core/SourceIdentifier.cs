using System;
using System.Drawing;

namespace SlskDown.Core
{
    /// <summary>
    /// Identificador visual de fuentes de búsqueda (Soulseek, eMule, etc.)
    /// </summary>
    public static class SourceIdentifier
    {
        public enum SearchSource
        {
            Soulseek,
            EMule,
            Unknown
        }

        // Iconos/Prefijos para cada fuente
        public static string GetSourceIcon(SearchSource source)
        {
            return source switch
            {
                SearchSource.Soulseek => "🎵",
                SearchSource.EMule => "🔷",
                _ => "❓"
            };
        }

        public static string GetSourceName(SearchSource source)
        {
            return source switch
            {
                SearchSource.Soulseek => "Soulseek",
                SearchSource.EMule => "eMule",
                _ => "Unknown"
            };
        }

        public static string GetSourceShortName(SearchSource source)
        {
            return source switch
            {
                SearchSource.Soulseek => "SLSK",
                SearchSource.EMule => "EMUL",
                _ => "????"
            };
        }

        // Colores para cada fuente (tema oscuro)
        public static Color GetSourceColor(SearchSource source)
        {
            return source switch
            {
                SearchSource.Soulseek => Color.FromArgb(100, 200, 255), // Azul claro
                SearchSource.EMule => Color.FromArgb(255, 150, 100),    // Naranja
                _ => Color.Gray
            };
        }

        // Color de fondo para resaltar la fuente
        public static Color GetSourceBackColor(SearchSource source)
        {
            return source switch
            {
                SearchSource.Soulseek => Color.FromArgb(20, 40, 60),  // Azul oscuro
                SearchSource.EMule => Color.FromArgb(60, 40, 20),     // Naranja oscuro
                _ => Color.FromArgb(40, 40, 40)
            };
        }

        // Detectar fuente desde nombre de usuario o contexto
        public static SearchSource DetectSource(string username, string context = null)
        {
            // Si viene de NetworkOrchestrator, el contexto puede indicar la fuente
            if (!string.IsNullOrEmpty(context))
            {
                if (context.Contains("emule", StringComparison.OrdinalIgnoreCase))
                    return SearchSource.EMule;
                if (context.Contains("soulseek", StringComparison.OrdinalIgnoreCase) ||
                    context.Contains("slsk", StringComparison.OrdinalIgnoreCase))
                    return SearchSource.Soulseek;
            }

            // Por defecto, asumimos Soulseek (es la red principal)
            return SearchSource.Soulseek;
        }

        // Formatear texto con icono de fuente
        public static string FormatWithSource(SearchSource source, string text)
        {
            return $"{GetSourceIcon(source)} {text}";
        }

        // Obtener tag de fuente para agregar al ListViewItem
        public static string GetSourceTag(SearchSource source)
        {
            return $"[{GetSourceShortName(source)}]";
        }

        // Generar tooltip descriptivo
        public static string GetSourceTooltip(SearchSource source)
        {
            return source switch
            {
                SearchSource.Soulseek => "Resultado de Soulseek - Red P2P especializada en música y libros",
                SearchSource.EMule => "Resultado de eMule - Red P2P de propósito general",
                _ => "Fuente desconocida"
            };
        }
    }
}
