using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SlskDown.Core.Optimization;

namespace SlskDown.Core.AI
{
    /// <summary>
    /// Cliente para Ollama - Modelos de IA locales (GRATIS, sin API Key)
    /// </summary>
    public class OllamaClient
    {
        private readonly HttpClient httpClient;
        private readonly string baseUrl;
        private readonly string model;

        public event Action<string> OnLog;

        public OllamaClient(string baseUrl = "http://localhost:11434", string model = "llama2")
        {
            httpClient = OptimizedHttpClient.CreateCustomClient(
                maxConnectionsPerServer: 2,
                timeout: TimeSpan.FromMinutes(5)
            );
            this.baseUrl = baseUrl;
            this.model = model;
        }

        /// <summary>
        /// Obtiene una respuesta del modelo local
        /// </summary>
        public async Task<string> GetCompletionAsync(string prompt, string systemPrompt = null, double temperature = 0.7)
        {
            try
            {
                var fullPrompt = string.IsNullOrEmpty(systemPrompt) 
                    ? prompt 
                    : $"{systemPrompt}\n\n{prompt}";

                var request = new
                {
                    model = model,
                    prompt = fullPrompt,
                    stream = false,
                    options = new
                    {
                        temperature = temperature,
                        num_predict = 500
                    }
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{baseUrl}/api/generate", content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Log($"❌ Error Ollama: {response.StatusCode} - {responseText}");
                    return null;
                }

                var result = JsonSerializer.Deserialize<OllamaResponse>(responseText);
                return result?.Response;
            }
            catch (Exception ex)
            {
                Log($"❌ Error en GetCompletionAsync: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Chat con historial de conversación
        /// </summary>
        public async Task<string> ChatAsync(string systemPrompt, List<ChatMessage> conversationHistory)
        {
            try
            {
                var messages = new List<object>
                {
                    new { role = "system", content = systemPrompt }
                };

                foreach (var msg in conversationHistory)
                {
                    messages.Add(new { role = msg.Role, content = msg.Content });
                }

                var request = new
                {
                    model = model,
                    messages = messages,
                    stream = false,
                    options = new
                    {
                        temperature = 0.7,
                        num_predict = 300,   // Reducido de 500 a 300 tokens
                        top_k = 40,          // Limitar opciones
                        top_p = 0.9,
                        num_ctx = 2048,      // Contexto reducido
                        num_thread = 8       // Usar más threads CPU
                    }
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{baseUrl}/api/chat", content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Log($"❌ Error Ollama Chat: {response.StatusCode}");
                    return null;
                }

                var result = JsonSerializer.Deserialize<OllamaChatResponse>(responseText);
                return result?.Message?.Content;
            }
            catch (Exception ex)
            {
                Log($"❌ Error en ChatAsync: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Genera embeddings para búsqueda semántica (si el modelo lo soporta)
        /// </summary>
        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            try
            {
                var request = new
                {
                    model = "nomic-embed-text", // Modelo específico para embeddings
                    prompt = text
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{baseUrl}/api/embeddings", content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Log($"⚠️ Embeddings no disponibles, usando fallback");
                    return null;
                }

                var result = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseText);
                return result?.Embedding;
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error en GetEmbeddingAsync: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Verifica si Ollama está disponible
        /// </summary>
        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                var response = await httpClient.GetAsync($"{baseUrl}/api/tags");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Lista modelos disponibles
        /// </summary>
        public async Task<List<string>> GetAvailableModelsAsync()
        {
            try
            {
                var response = await httpClient.GetAsync($"{baseUrl}/api/tags");
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return new List<string>();

                var result = JsonSerializer.Deserialize<OllamaTagsResponse>(responseText);
                return result?.Models?.Select(m => m.Name).ToList() ?? new List<string>();
            }
            catch (Exception ex)
            {
                Log($"❌ Error obteniendo modelos: {ex.Message}");
                return new List<string>();
            }
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
    }

    // Modelos de respuesta
    public class OllamaResponse
    {
        public string Model { get; set; }
        public string Response { get; set; }
        public bool Done { get; set; }
    }

    public class OllamaChatResponse
    {
        public OllamaMessage Message { get; set; }
        public bool Done { get; set; }
    }

    public class OllamaMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }

    public class OllamaEmbeddingResponse
    {
        public float[] Embedding { get; set; }
    }

    public class OllamaTagsResponse
    {
        public List<OllamaModel> Models { get; set; }
    }

    public class OllamaModel
    {
        public string Name { get; set; }
        public long Size { get; set; }
    }

    public class ChatMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }
}
