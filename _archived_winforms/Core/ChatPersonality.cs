using System;
using System.Collections.Generic;

namespace SlskDown.Core
{
    public enum PersonalityType
    {
        Professional,
        Friendly,
        Concise,
        Detailed,
        Humorous
    }

    public class PersonalityProfile
    {
        public PersonalityType Type { get; set; }
        public string Greeting { get; set; }
        public string SuccessPrefix { get; set; }
        public string ErrorPrefix { get; set; }
        public string ThinkingMessage { get; set; }
        public bool UseEmojis { get; set; }
        public bool VerboseExplanations { get; set; }
    }

    public static class ChatPersonality
    {
        private static PersonalityType currentPersonality = PersonalityType.Friendly;

        private static readonly Dictionary<PersonalityType, PersonalityProfile> profiles = new()
        {
            [PersonalityType.Professional] = new PersonalityProfile
            {
                Type = PersonalityType.Professional,
                Greeting = "Sistema listo. Esperando comandos.",
                SuccessPrefix = "Operación completada:",
                ErrorPrefix = "Error detectado:",
                ThinkingMessage = "Procesando solicitud...",
                UseEmojis = false,
                VerboseExplanations = true
            },
            [PersonalityType.Friendly] = new PersonalityProfile
            {
                Type = PersonalityType.Friendly,
                Greeting = "¡Hola! 👋 ¿En qué puedo ayudarte hoy?",
                SuccessPrefix = "¡Genial! ✅",
                ErrorPrefix = "Ups, algo salió mal 😅",
                ThinkingMessage = "Déjame pensar... 🤔",
                UseEmojis = true,
                VerboseExplanations = true
            },
            [PersonalityType.Concise] = new PersonalityProfile
            {
                Type = PersonalityType.Concise,
                Greeting = "Listo.",
                SuccessPrefix = "✓",
                ErrorPrefix = "✗",
                ThinkingMessage = "...",
                UseEmojis = false,
                VerboseExplanations = false
            },
            [PersonalityType.Detailed] = new PersonalityProfile
            {
                Type = PersonalityType.Detailed,
                Greeting = "Sistema de asistencia inicializado. Todos los módulos operativos. Esperando instrucciones del usuario.",
                SuccessPrefix = "Operación completada exitosamente. Detalles:",
                ErrorPrefix = "Se ha producido un error. Información de diagnóstico:",
                ThinkingMessage = "Analizando solicitud y preparando respuesta detallada...",
                UseEmojis = false,
                VerboseExplanations = true
            },
            [PersonalityType.Humorous] = new PersonalityProfile
            {
                Type = PersonalityType.Humorous,
                Greeting = "¡Ey! 🎉 Tu asistente favorito está aquí. ¿Qué libros vamos a cazar hoy?",
                SuccessPrefix = "¡Tachán! 🎊",
                ErrorPrefix = "Auch... 💥",
                ThinkingMessage = "Consultando con mi bola de cristal... 🔮",
                UseEmojis = true,
                VerboseExplanations = true
            }
        };

        public static void SetPersonality(PersonalityType type)
        {
            currentPersonality = type;
        }

        public static PersonalityType GetCurrentPersonality() => currentPersonality;

        public static PersonalityProfile GetCurrentProfile() => profiles[currentPersonality];

        public static string FormatMessage(string message, string messageType = "info")
        {
            var profile = GetCurrentProfile();

            if (profile.Type == PersonalityType.Concise)
            {
                // Modo conciso: solo lo esencial
                return message.Length > 100 ? message.Substring(0, 97) + "..." : message;
            }

            var prefix = messageType switch
            {
                "success" => profile.SuccessPrefix,
                "error" => profile.ErrorPrefix,
                "thinking" => profile.ThinkingMessage,
                _ => ""
            };

            return string.IsNullOrEmpty(prefix) ? message : $"{prefix} {message}";
        }

        public static string GetGreeting() => profiles[currentPersonality].Greeting;

        public static string FormatSearchResult(int count, string author)
        {
            var profile = GetCurrentProfile();

            return profile.Type switch
            {
                PersonalityType.Professional => $"Búsqueda completada. {count} resultados encontrados para {author}.",
                PersonalityType.Friendly => $"¡Encontré {count} libros de {author}! 📚 ¿Los descargamos?",
                PersonalityType.Concise => $"{count} resultados: {author}",
                PersonalityType.Detailed => $"Búsqueda finalizada exitosamente. Se han localizado {count} archivos coincidentes con el autor '{author}'. Los resultados están ordenados por relevancia.",
                PersonalityType.Humorous => count > 20 ? $"¡Jackpot! 🎰 {count} libros de {author}. ¡Esto es una mina de oro!" : $"Encontré {count} libros de {author}. No está mal, ¿eh? 😎",
                _ => $"{count} resultados para {author}"
            };
        }

        public static string FormatDownloadComplete(string filename)
        {
            var profile = GetCurrentProfile();

            return profile.Type switch
            {
                PersonalityType.Professional => $"Descarga completada: {filename}",
                PersonalityType.Friendly => $"¡Listo! ✅ Ya tienes '{filename}' 🎉",
                PersonalityType.Concise => $"✓ {filename}",
                PersonalityType.Detailed => $"Proceso de descarga finalizado exitosamente. Archivo '{filename}' guardado en el directorio de descargas configurado.",
                PersonalityType.Humorous => $"¡Boom! 💥 '{filename}' está en tu biblioteca. ¡Otro más para la colección!",
                _ => $"Descargado: {filename}"
            };
        }

        public static string FormatError(string error)
        {
            var profile = GetCurrentProfile();

            return profile.Type switch
            {
                PersonalityType.Professional => $"Error: {error}. Consulte la documentación para más información.",
                PersonalityType.Friendly => $"Ups... 😅 {error}. ¿Intentamos de nuevo?",
                PersonalityType.Concise => $"✗ {error}",
                PersonalityType.Detailed => $"Se ha producido un error durante la operación. Descripción: {error}. Se recomienda verificar la configuración y reintentar.",
                PersonalityType.Humorous => $"¡Ay caramba! 🤦 {error}. Pero no te preocupes, ¡lo arreglamos!",
                _ => $"Error: {error}"
            };
        }

        public static string FormatSeriesSuggestion(string seriesName, int missing)
        {
            var profile = GetCurrentProfile();

            return profile.Type switch
            {
                PersonalityType.Professional => $"Detectada serie incompleta: {seriesName}. Faltan {missing} volúmenes. ¿Desea buscarlos?",
                PersonalityType.Friendly => $"¡Hey! 👋 Veo que tienes '{seriesName}' pero te faltan {missing} libros. ¿Los buscamos?",
                PersonalityType.Concise => $"{seriesName}: -{missing} vol.",
                PersonalityType.Detailed => $"Análisis de biblioteca: Se ha detectado la serie '{seriesName}' con {missing} volúmenes faltantes. Se recomienda completar la colección para una experiencia de lectura óptima.",
                PersonalityType.Humorous => $"¡Ojo! 👀 Tu colección de '{seriesName}' tiene {missing} agujeros. ¿Los tapamos? 🔧",
                _ => $"Serie {seriesName}: faltan {missing} volúmenes"
            };
        }
    }
}
