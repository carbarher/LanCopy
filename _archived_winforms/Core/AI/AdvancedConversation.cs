using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SlskDown.Core.AI
{
    /// <summary>
    /// Sistema de conversaciones contextuales largas con memoria extendida
    /// </summary>
    public class AdvancedConversation
    {
        private List<ConversationTurn> conversationHistory = new List<ConversationTurn>();
        private Dictionary<string, object> conversationState = new Dictionary<string, object>();
        private string currentTopic = null;
        private DateTime conversationStart = DateTime.Now;

        public class ConversationTurn
        {
            public DateTime Timestamp { get; set; }
            public string UserMessage { get; set; }
            public string AIResponse { get; set; }
            public string Topic { get; set; }
            public Dictionary<string, string> ExtractedEntities { get; set; } = new Dictionary<string, string>();
        }

        public void AddTurn(string userMessage, string aiResponse)
        {
            var turn = new ConversationTurn
            {
                Timestamp = DateTime.Now,
                UserMessage = userMessage,
                AIResponse = aiResponse,
                Topic = currentTopic,
                ExtractedEntities = ExtractEntities(userMessage)
            };

            conversationHistory.Add(turn);

            // Mantener últimas 100 conversaciones
            if (conversationHistory.Count > 100)
                conversationHistory.RemoveAt(0);

            // Actualizar estado
            UpdateConversationState(turn);
        }

        private Dictionary<string, string> ExtractEntities(string message)
        {
            var entities = new Dictionary<string, string>();
            var lower = message.ToLower();

            // Detectar autores mencionados
            var commonAuthors = new[] { "asimov", "clarke", "heinlein", "herbert", "bradbury", "dick", "verne", "wells" };
            foreach (var author in commonAuthors)
            {
                if (lower.Contains(author))
                    entities["author"] = author;
            }

            // Detectar formatos
            var formats = new[] { "epub", "pdf", "mobi", "azw3", "txt" };
            foreach (var format in formats)
            {
                if (lower.Contains(format))
                    entities["format"] = format;
            }

            // Detectar idiomas
            if (lower.Contains("español") || lower.Contains("spanish"))
                entities["language"] = "español";
            else if (lower.Contains("inglés") || lower.Contains("english"))
                entities["language"] = "inglés";

            return entities;
        }

        private void UpdateConversationState(ConversationTurn turn)
        {
            // Actualizar preferencias implícitas
            foreach (var entity in turn.ExtractedEntities)
            {
                conversationState[$"last_{entity.Key}"] = entity.Value;
            }

            // Detectar cambio de tema
            if (turn.UserMessage.Length > 20 && !IsFollowUp(turn.UserMessage))
            {
                currentTopic = turn.UserMessage.Substring(0, Math.Min(50, turn.UserMessage.Length));
            }
        }

        private bool IsFollowUp(string message)
        {
            var followUpPhrases = new[] { "también", "además", "y", "pero", "más", "otro", "ese", "eso", "lo mismo" };
            var lower = message.ToLower();
            return followUpPhrases.Any(p => lower.StartsWith(p));
        }

        public string BuildContextualPrompt(string newMessage)
        {
            var prompt = new StringBuilder();

            // Agregar contexto de conversación
            if (conversationHistory.Count > 0)
            {
                prompt.AppendLine("CONTEXTO DE CONVERSACIÓN:");
                
                // Últimas 5 interacciones
                foreach (var turn in conversationHistory.TakeLast(5))
                {
                    prompt.AppendLine($"Usuario: {turn.UserMessage}");
                    prompt.AppendLine($"IA: {turn.AIResponse.Substring(0, Math.Min(100, turn.AIResponse.Length))}...");
                }
                prompt.AppendLine();
            }

            // Agregar estado actual
            if (conversationState.Count > 0)
            {
                prompt.AppendLine("PREFERENCIAS DETECTADAS:");
                foreach (var state in conversationState)
                {
                    prompt.AppendLine($"- {state.Key}: {state.Value}");
                }
                prompt.AppendLine();
            }

            // Mensaje actual
            prompt.AppendLine($"MENSAJE ACTUAL: {newMessage}");

            return prompt.ToString();
        }

        public Dictionary<string, object> GetConversationState() => new Dictionary<string, object>(conversationState);

        public List<ConversationTurn> GetRecentTurns(int count = 10) => conversationHistory.TakeLast(count).ToList();

        public string GetCurrentTopic() => currentTopic;

        public void Reset()
        {
            conversationHistory.Clear();
            conversationState.Clear();
            currentTopic = null;
            conversationStart = DateTime.Now;
        }
    }
}
