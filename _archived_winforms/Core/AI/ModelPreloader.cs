using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace SlskDown.Core.AI
{
    /// <summary>
    /// Pre-carga el modelo de Ollama en memoria para eliminar latencia inicial
    /// </summary>
    public class ModelPreloader
    {
        private readonly string baseUrl;
        private readonly HttpClient httpClient;
        private bool isPreloaded = false;

        public ModelPreloader(string ollamaUrl)
        {
            baseUrl = ollamaUrl;
            httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        /// <summary>
        /// Pre-carga el modelo en memoria con una consulta dummy
        /// </summary>
        public async Task<bool> PreloadModelAsync(string model)
        {
            if (isPreloaded)
                return true;

            try
            {
                var request = new
                {
                    model = model,
                    prompt = "Hi",
                    stream = false,
                    options = new
                    {
                        num_predict = 1  // Solo 1 token para warm-up rápido
                    }
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{baseUrl}/api/generate", content);
                
                if (response.IsSuccessStatusCode)
                {
                    isPreloaded = true;
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Pre-carga múltiples modelos en paralelo
        /// </summary>
        public async Task PreloadMultipleModelsAsync(params string[] models)
        {
            var tasks = new System.Collections.Generic.List<Task<bool>>();
            
            foreach (var model in models)
            {
                tasks.Add(PreloadModelAsync(model));
            }

            await Task.WhenAll(tasks);
        }

        public bool IsPreloaded => isPreloaded;
    }
}
