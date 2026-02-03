using System;
using System.Collections.Generic;

namespace SlskDown.Core.AI
{
    /// <summary>
    /// Selecciona el modelo de Ollama más apropiado según el tipo de consulta
    /// para maximizar velocidad sin sacrificar calidad
    /// </summary>
    public static class ModelSelector
    {
        // Modelos rápidos para consultas simples
        private static readonly HashSet<string> FastModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "llama3.2:1b",      // Muy rápido, 1B parámetros
            "phi3:mini",        // Rápido, bueno para tareas simples
            "gemma:2b",         // Rápido, 2B parámetros
            "tinyllama"         // Ultra rápido, 1.1B parámetros
        };

        // Modelos medianos para consultas normales
        private static readonly HashSet<string> MediumModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "llama3.2:3b",      // Balance velocidad/calidad
            "phi3",             // Bueno para razonamiento
            "gemma:7b",         // Bueno para español
            "mistral"           // Rápido y preciso
        };

        // Modelos grandes para consultas complejas
        private static readonly HashSet<string> LargeModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "llama2",           // Modelo por defecto
            "llama3",           // Mejor calidad
            "mixtral",          // Excelente razonamiento
            "codellama"         // Para código
        };

        public enum QueryComplexity
        {
            Simple,     // Comandos directos, consultas cortas
            Medium,     // Búsquedas, recomendaciones
            Complex     // Análisis, razonamiento complejo
        }

        /// <summary>
        /// Determina la complejidad de una consulta
        /// </summary>
        public static QueryComplexity DetermineComplexity(string query)
        {
            var lower = query.ToLower();

            // Comandos simples (muy rápidos)
            if (lower.Length < 20 ||
                lower.StartsWith("busca ") ||
                lower.StartsWith("descarga ") ||
                lower.StartsWith("lista ") ||
                lower.Contains("estado") ||
                lower.Contains("ayuda"))
            {
                return QueryComplexity.Simple;
            }

            // Consultas complejas (necesitan modelo grande)
            if (lower.Contains("por qué") ||
                lower.Contains("explica") ||
                lower.Contains("analiza") ||
                lower.Contains("compara") ||
                lower.Contains("razona") ||
                lower.Contains("diagnostica") ||
                lower.Length > 100)
            {
                return QueryComplexity.Complex;
            }

            // Por defecto: complejidad media
            return QueryComplexity.Medium;
        }

        /// <summary>
        /// Selecciona el mejor modelo disponible según complejidad
        /// </summary>
        public static string SelectBestModel(string defaultModel, QueryComplexity complexity, List<string> availableModels)
        {
            if (availableModels == null || availableModels.Count == 0)
                return defaultModel;

            switch (complexity)
            {
                case QueryComplexity.Simple:
                    // Buscar modelo rápido
                    foreach (var model in FastModels)
                    {
                        if (availableModels.Contains(model))
                            return model;
                    }
                    // Fallback a modelo mediano
                    foreach (var model in MediumModels)
                    {
                        if (availableModels.Contains(model))
                            return model;
                    }
                    break;

                case QueryComplexity.Medium:
                    // Buscar modelo mediano
                    foreach (var model in MediumModels)
                    {
                        if (availableModels.Contains(model))
                            return model;
                    }
                    break;

                case QueryComplexity.Complex:
                    // Buscar modelo grande
                    foreach (var model in LargeModels)
                    {
                        if (availableModels.Contains(model))
                            return model;
                    }
                    break;
            }

            // Si no encuentra nada, usar el modelo por defecto
            return defaultModel;
        }

        /// <summary>
        /// Obtiene parámetros optimizados según el modelo
        /// </summary>
        public static object GetOptimizedOptions(string model)
        {
            // Modelos pequeños: pueden generar más tokens rápidamente
            if (FastModels.Contains(model))
            {
                return new
                {
                    num_predict = 400,
                    temperature = 0.7,
                    top_k = 40,
                    top_p = 0.9,
                    num_ctx = 2048,
                    num_thread = 8
                };
            }

            // Modelos medianos: balance
            if (MediumModels.Contains(model))
            {
                return new
                {
                    num_predict = 300,
                    temperature = 0.7,
                    top_k = 40,
                    top_p = 0.9,
                    num_ctx = 2048,
                    num_thread = 8
                };
            }

            // Modelos grandes: limitar tokens para velocidad
            return new
            {
                num_predict = 250,
                temperature = 0.7,
                top_k = 40,
                top_p = 0.9,
                num_ctx = 2048,
                num_thread = 8
            };
        }

        /// <summary>
        /// Recomienda el mejor modelo para instalar
        /// </summary>
        public static string GetRecommendedModel()
        {
            return "llama3.2:3b"; // Balance perfecto velocidad/calidad
        }

        /// <summary>
        /// Obtiene información sobre un modelo
        /// </summary>
        public static string GetModelInfo(string model)
        {
            if (FastModels.Contains(model))
                return "⚡ Modelo rápido - Ideal para consultas simples";
            
            if (MediumModels.Contains(model))
                return "⚖️ Modelo balanceado - Buena velocidad y calidad";
            
            if (LargeModels.Contains(model))
                return "🎯 Modelo avanzado - Máxima calidad";
            
            return "📦 Modelo personalizado";
        }
    }
}
