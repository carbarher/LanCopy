using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SlskDown.Core.AI
{
    /// <summary>
    /// Modo Turbo: Selecciona automáticamente el modelo más rápido según la complejidad
    /// </summary>
    public class TurboMode
    {
        private readonly OllamaClient ollamaClient;
        private List<string> availableModels = new List<string>();
        private DateTime lastModelCheck = DateTime.MinValue;
        private const int MODEL_CACHE_MINUTES = 5;

        public bool Enabled { get; set; } = true;
        public string FallbackModel { get; set; } = "llama3.2:3b";

        public TurboMode(OllamaClient client)
        {
            ollamaClient = client;
        }

        /// <summary>
        /// Selecciona el modelo óptimo según la complejidad de la consulta
        /// </summary>
        public async Task<string> SelectOptimalModelAsync(string query, string defaultModel)
        {
            if (!Enabled)
                return defaultModel;

            // Actualizar lista de modelos si es necesario
            if ((DateTime.Now - lastModelCheck).TotalMinutes > MODEL_CACHE_MINUTES)
            {
                availableModels = await ollamaClient.GetAvailableModelsAsync() ?? new List<string>();
                lastModelCheck = DateTime.Now;
            }

            var complexity = ModelSelector.DetermineComplexity(query);
            return ModelSelector.SelectBestModel(defaultModel, complexity, availableModels);
        }

        /// <summary>
        /// Obtiene estadísticas de uso de modelos
        /// </summary>
        public Dictionary<string, int> GetModelUsageStats()
        {
            return new Dictionary<string, int>
            {
                ["tinyllama"] = 0,
                ["phi3:mini"] = 0,
                ["llama3.2:3b"] = 0,
                ["llama2"] = 0
            };
        }
    }
}
