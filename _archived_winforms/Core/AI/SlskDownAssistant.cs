using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SlskDown.Core.AI
{
    /// <summary>
    /// Chatbot asistente inteligente para SlskDown
    /// </summary>
    public class SlskDownAssistant
    {
        private readonly OllamaClient ollama;
        private readonly List<ChatMessage> conversationHistory;
        private const string SYSTEM_PROMPT = @"
Eres un asistente experto en SlskDown, un cliente P2P avanzado para Soulseek.

Ayudas a usuarios a:
- Buscar archivos eficientemente
- Configurar descargas y optimizar velocidad
- Organizar colecciones de archivos
- Resolver problemas técnicos
- Descubrir funcionalidades avanzadas
- Usar las nuevas funcionalidades de IA

Responde de forma concisa, práctica y amigable.
Usa emojis ocasionalmente para hacer las respuestas más visuales.
Si el usuario pregunta cómo hacer algo, proporciona pasos específicos.
";

        public event Action<string> OnLog;

        public SlskDownAssistant(OllamaClient ollama)
        {
            this.ollama = ollama;
            conversationHistory = new List<ChatMessage>();
        }

        /// <summary>
        /// Envía un mensaje al asistente
        /// </summary>
        public async Task<string> ChatAsync(string userMessage)
        {
            try
            {
                Log($"💬 Usuario: {userMessage}");

                conversationHistory.Add(new ChatMessage("user", userMessage));

                var response = await ollama.ChatAsync(SYSTEM_PROMPT, conversationHistory);

                if (!string.IsNullOrEmpty(response))
                {
                    conversationHistory.Add(new ChatMessage("assistant", response));
                    Log($"🤖 Asistente: {response.Substring(0, Math.Min(100, response.Length))}...");
                }

                return response;
            }
            catch (Exception ex)
            {
                Log($"❌ Error en ChatAsync: {ex.Message}");
                return "Lo siento, ocurrió un error. Por favor intenta de nuevo.";
            }
        }

        /// <summary>
        /// Obtiene sugerencias basadas en contexto
        /// </summary>
        public async Task<List<string>> GetSuggestionsAsync(string context)
        {
            try
            {
                var prompt = $@"
Contexto: {context}

Genera 3 sugerencias útiles para el usuario.
Formato: Una sugerencia por línea, sin numeración.
";

                var response = await ollama.GetCompletionAsync(prompt, temperature: 0.7);
                
                if (string.IsNullOrEmpty(response))
                    return new List<string>();

                return response
                    .Split('\n')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Take(3)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log($"❌ Error en GetSuggestionsAsync: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Analiza un error y proporciona solución
        /// </summary>
        public async Task<ErrorSolution> AnalyzeErrorAsync(string errorMessage, string context = null)
        {
            try
            {
                var prompt = $@"
Error en SlskDown:
{errorMessage}

{(context != null ? $"Contexto: {context}" : "")}

Proporciona en formato JSON:
{{
  ""cause"": ""Causa probable"",
  ""solution"": ""Solución paso a paso"",
  ""prevention"": ""Cómo evitarlo en el futuro""
}}
";

                var response = await ollama.GetCompletionAsync(prompt, temperature: 0.3);
                
                if (string.IsNullOrEmpty(response))
                    return null;

                return ParseErrorSolution(response);
            }
            catch (Exception ex)
            {
                Log($"❌ Error en AnalyzeErrorAsync: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Genera tutorial paso a paso
        /// </summary>
        public async Task<Tutorial> GenerateTutorialAsync(string topic)
        {
            try
            {
                var prompt = $@"
Genera un tutorial paso a paso sobre: {topic}

Formato JSON:
{{
  ""title"": ""Título del tutorial"",
  ""steps"": [
    {{""number"": 1, ""description"": ""Paso 1"", ""tip"": ""Consejo opcional""}},
    {{""number"": 2, ""description"": ""Paso 2"", ""tip"": ""Consejo opcional""}}
  ],
  ""estimatedTime"": ""5 minutos""
}}
";

                var response = await ollama.GetCompletionAsync(prompt, temperature: 0.5);
                
                if (string.IsNullOrEmpty(response))
                    return null;

                return ParseTutorial(response);
            }
            catch (Exception ex)
            {
                Log($"❌ Error en GenerateTutorialAsync: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Limpia el historial de conversación
        /// </summary>
        public void ClearHistory()
        {
            conversationHistory.Clear();
            Log("🗑️ Historial de conversación limpiado");
        }

        /// <summary>
        /// Obtiene resumen de la conversación
        /// </summary>
        public async Task<string> GetConversationSummaryAsync()
        {
            try
            {
                if (conversationHistory.Count == 0)
                    return "No hay conversación activa.";

                var conversationText = string.Join("\n", 
                    conversationHistory.Select(m => $"{m.Role}: {m.Content}"));

                var prompt = $@"
Resume esta conversación en 2-3 líneas:

{conversationText}
";

                return await ollama.GetCompletionAsync(prompt, temperature: 0.3);
            }
            catch (Exception ex)
            {
                Log($"❌ Error en GetConversationSummaryAsync: {ex.Message}");
                return "Error generando resumen.";
            }
        }

        private ErrorSolution ParseErrorSolution(string jsonResponse)
        {
            try
            {
                var startIndex = jsonResponse.IndexOf('{');
                var endIndex = jsonResponse.LastIndexOf('}');
                
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    var jsonText = jsonResponse.Substring(startIndex, endIndex - startIndex + 1);
                    return System.Text.Json.JsonSerializer.Deserialize<ErrorSolution>(jsonText);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private Tutorial ParseTutorial(string jsonResponse)
        {
            try
            {
                var startIndex = jsonResponse.IndexOf('{');
                var endIndex = jsonResponse.LastIndexOf('}');
                
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    var jsonText = jsonResponse.Substring(startIndex, endIndex - startIndex + 1);
                    return System.Text.Json.JsonSerializer.Deserialize<Tutorial>(jsonText);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
    }

    public class ErrorSolution
    {
        public string cause { get; set; }
        public string solution { get; set; }
        public string prevention { get; set; }
    }

    public class Tutorial
    {
        public string title { get; set; }
        public List<TutorialStep> steps { get; set; }
        public string estimatedTime { get; set; }
    }

    public class TutorialStep
    {
        public int number { get; set; }
        public string description { get; set; }
        public string tip { get; set; }
    }
}
