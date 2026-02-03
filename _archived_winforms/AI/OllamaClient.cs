using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SlskDown.AI
{
    /// <summary>
    /// Cliente HTTP para comunicarse con Ollama API
    /// </summary>
    public class OllamaClient
    {
        private readonly HttpClient httpClient;
        private readonly string baseUrl;
        private readonly string defaultModel;

        public OllamaClient(string baseUrl = "http://localhost:11434", string model = "llama3.2")
        {
            this.baseUrl = baseUrl;
            this.defaultModel = model;
            this.httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
        }

        /// <summary>
        /// Genera una respuesta usando Ollama
        /// </summary>
        public async Task<string> GenerateAsync(string prompt, string model = null, bool stream = false)
        {
            try
            {
                var requestData = new
                {
                    model = model ?? defaultModel,
                    prompt = prompt,
                    stream = stream
                };

                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{baseUrl}/api/generate", content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OllamaResponse>(responseJson);

                return result?.response ?? string.Empty;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error comunicando con Ollama: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Genera una respuesta con chat (mantiene contexto)
        /// </summary>
        public async Task<string> ChatAsync(List<ChatMessage> messages, string model = null)
        {
            try
            {
                var requestData = new
                {
                    model = model ?? defaultModel,
                    messages = messages,
                    stream = false
                };

                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{baseUrl}/api/chat", content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OllamaChatResponse>(responseJson);

                return result?.message?.content ?? string.Empty;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error en chat con Ollama: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Genera embeddings para búsqueda semántica
        /// </summary>
        public async Task<float[]> GenerateEmbeddingsAsync(string text, string model = "nomic-embed-text")
        {
            try
            {
                var requestData = new
                {
                    model = model,
                    prompt = text
                };

                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{baseUrl}/api/embeddings", content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseJson);

                return result?.embedding ?? Array.Empty<float>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generando embeddings: {ex.Message}", ex);
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
        public async Task<List<string>> ListModelsAsync()
        {
            try
            {
                var response = await httpClient.GetAsync($"{baseUrl}/api/tags");
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OllamaModelsResponse>(responseJson);

                var models = new List<string>();
                if (result?.models != null)
                {
                    foreach (var model in result.models)
                    {
                        models.Add(model.name);
                    }
                }

                return models;
            }
            catch
            {
                return new List<string>();
            }
        }
    }

    public class OllamaResponse
    {
        public string model { get; set; }
        public string response { get; set; }
        public bool done { get; set; }
    }

    public class OllamaChatResponse
    {
        public string model { get; set; }
        public ChatMessage message { get; set; }
        public bool done { get; set; }
    }

    public class ChatMessage
    {
        public string role { get; set; } // "system", "user", "assistant"
        public string content { get; set; }
    }

    public class OllamaEmbeddingResponse
    {
        public float[] embedding { get; set; }
    }

    public class OllamaModelsResponse
    {
        public List<OllamaModel> models { get; set; }
    }

    public class OllamaModel
    {
        public string name { get; set; }
        public long size { get; set; }
        public string digest { get; set; }
    }
}
