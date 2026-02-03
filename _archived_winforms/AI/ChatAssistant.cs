using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SlskDown.AI
{
    /// <summary>
    /// FUNCIONALIDAD #5: Asistente de Chat para Gestión
    /// </summary>
    public class ChatAssistant
    {
        private readonly OllamaClient ollamaClient;
        private readonly List<ChatMessage> conversationHistory = new List<ChatMessage>();
        private readonly bool enabled;

        public ChatAssistant(OllamaClient client, bool enabled = true)
        {
            this.ollamaClient = client;
            this.enabled = enabled;

            // Mensaje del sistema para definir el comportamiento del asistente
            conversationHistory.Add(new ChatMessage
            {
                role = "system",
                content = @"Eres un asistente inteligente para SlskDown, una aplicación de descargas P2P.
Tu función es ayudar al usuario a gestionar sus descargas mediante comandos en lenguaje natural.

Puedes ayudar con:
- Buscar y descargar archivos
- Gestionar la cola de descargas
- Pausar, reanudar o cancelar descargas
- Obtener estadísticas y reportes
- Recomendar archivos basándose en el historial
- Clasificar y organizar archivos
- Configurar la aplicación

Responde de forma concisa y útil. Si necesitas información adicional, pregunta al usuario."
            });
        }

        /// <summary>
        /// Procesa un mensaje del usuario y genera respuesta
        /// </summary>
        public async Task<ChatResponse> ProcessMessageAsync(string userMessage)
        {
            if (!enabled)
                return new ChatResponse { Message = "Asistente de chat deshabilitado." };

            try
            {
                // Añadir mensaje del usuario al historial
                conversationHistory.Add(new ChatMessage
                {
                    role = "user",
                    content = userMessage
                });

                // Generar respuesta
                var response = await ollamaClient.ChatAsync(conversationHistory);

                // Añadir respuesta al historial
                conversationHistory.Add(new ChatMessage
                {
                    role = "assistant",
                    content = response
                });

                // Analizar si el mensaje contiene un comando
                var command = await ParseCommandAsync(userMessage);

                return new ChatResponse
                {
                    Message = response,
                    Command = command,
                    RequiresAction = command != null
                };
            }
            catch (Exception ex)
            {
                return new ChatResponse
                {
                    Message = $"Error: {ex.Message}",
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Analiza un mensaje para extraer comandos
        /// </summary>
        private async Task<AssistantCommand> ParseCommandAsync(string message)
        {
            try
            {
                var prompt = $@"Analiza este mensaje del usuario y determina si contiene un comando:

""{message}""

Comandos posibles:
- SEARCH: Buscar archivos (ej: ""busca libros de Asimov"")
- DOWNLOAD: Descargar archivos (ej: ""descarga todos los epub"")
- PAUSE: Pausar descargas (ej: ""pausa las descargas"")
- RESUME: Reanudar descargas (ej: ""continúa descargando"")
- CANCEL: Cancelar descargas (ej: ""cancela la descarga de..."")
- STATUS: Ver estado (ej: ""¿cómo van las descargas?"")
- STATS: Ver estadísticas (ej: ""muéstrame las estadísticas"")
- RECOMMEND: Pedir recomendaciones (ej: ""recomiéndame algo"")
- CLASSIFY: Clasificar archivos (ej: ""organiza mis archivos"")
- CONFIG: Configurar (ej: ""cambia la velocidad máxima"")

Responde en formato:
COMANDO: [tipo de comando o NONE]
PARAMETROS: [parámetros extraídos del mensaje]";

                var response = await ollamaClient.GenerateAsync(prompt);
                return ParseCommandResponse(response);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Genera ayuda contextual
        /// </summary>
        public async Task<string> GetContextualHelpAsync(string context)
        {
            if (!enabled)
                return "Ayuda no disponible.";

            try
            {
                var prompt = $@"El usuario está en este contexto de la aplicación:
{context}

Proporciona ayuda breve y útil sobre qué puede hacer en esta situación.
Máximo 3-4 líneas.";

                return await ollamaClient.GenerateAsync(prompt);
            }
            catch
            {
                return "No se pudo generar ayuda contextual.";
            }
        }

        /// <summary>
        /// Sugiere acciones basándose en el estado actual
        /// </summary>
        public async Task<List<string>> SuggestActionsAsync(string currentState)
        {
            if (!enabled)
                return new List<string>();

            try
            {
                var prompt = $@"Basándote en este estado actual de la aplicación:
{currentState}

Sugiere 3-5 acciones útiles que el usuario podría querer realizar.
Responde con una acción por línea, en formato de comando natural.
Ejemplo:
- Buscar más libros de este autor
- Ver estadísticas de descargas
- Pausar descargas para ahorrar ancho de banda";

                var response = await ollamaClient.GenerateAsync(prompt);
                return response.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim().TrimStart('-', '*').Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Take(5)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Explica un error en lenguaje natural
        /// </summary>
        public async Task<string> ExplainErrorAsync(string errorMessage)
        {
            if (!enabled)
                return errorMessage;

            try
            {
                var prompt = $@"Este error técnico ocurrió:
{errorMessage}

Explícalo en lenguaje simple y sugiere cómo solucionarlo.
Máximo 2-3 líneas.";

                return await ollamaClient.GenerateAsync(prompt);
            }
            catch
            {
                return errorMessage;
            }
        }

        /// <summary>
        /// Genera resumen de actividad
        /// </summary>
        public async Task<string> GenerateActivitySummaryAsync(Dictionary<string, object> stats)
        {
            if (!enabled)
                return "Resumen no disponible.";

            try
            {
                var statsText = string.Join("\n", stats.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
                var prompt = $@"Genera un resumen amigable de esta actividad:

{statsText}

Hazlo conversacional y destaca lo más importante.
Máximo 3-4 líneas.";

                return await ollamaClient.GenerateAsync(prompt);
            }
            catch
            {
                return "No se pudo generar resumen.";
            }
        }

        /// <summary>
        /// Limpia el historial de conversación
        /// </summary>
        public void ClearHistory()
        {
            conversationHistory.Clear();
            // Re-añadir mensaje del sistema
            conversationHistory.Add(new ChatMessage
            {
                role = "system",
                content = conversationHistory[0].content
            });
        }

        /// <summary>
        /// Obtiene el historial de conversación
        /// </summary>
        public List<ChatMessage> GetHistory()
        {
            return new List<ChatMessage>(conversationHistory);
        }

        private AssistantCommand ParseCommandResponse(string response)
        {
            var command = new AssistantCommand();
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.StartsWith("COMANDO:", StringComparison.OrdinalIgnoreCase))
                {
                    var cmdType = line.Substring(8).Trim().ToUpperInvariant();
                    if (cmdType != "NONE")
                        command.Type = cmdType;
                }
                else if (line.StartsWith("PARAMETROS:", StringComparison.OrdinalIgnoreCase))
                {
                    command.Parameters = line.Substring(11).Trim();
                }
            }

            return command.Type != null ? command : null;
        }
    }

    public class ChatResponse
    {
        public string Message { get; set; }
        public AssistantCommand Command { get; set; }
        public bool RequiresAction { get; set; }
        public string Error { get; set; }
    }

    public class AssistantCommand
    {
        public string Type { get; set; } // SEARCH, DOWNLOAD, PAUSE, etc.
        public string Parameters { get; set; }
    }
}
