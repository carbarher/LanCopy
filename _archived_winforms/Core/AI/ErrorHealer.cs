using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core.AI
{
    public class HealingAction
    {
        public string Problem { get; set; }
        public string Solution { get; set; }
        public List<string> Steps { get; set; } = new List<string>();
        public bool AutoExecute { get; set; }
    }

    /// <summary>
    /// Sistema de auto-curación de errores
    /// </summary>
    public class ErrorHealer
    {
        private Dictionary<string, int> errorFrequency = new Dictionary<string, int>();
        private Dictionary<string, HealingAction> knownSolutions = new Dictionary<string, HealingAction>();

        public ErrorHealer()
        {
            InitializeKnownSolutions();
        }

        private void InitializeKnownSolutions()
        {
            // Descarga lenta
            knownSolutions["slow_download"] = new HealingAction
            {
                Problem = "Descarga muy lenta",
                Solution = "Buscar proveedor alternativo más rápido",
                Steps = new List<string>
                {
                    "Cancelar descarga actual",
                    "Buscar proveedores alternativos",
                    "Seleccionar el más rápido",
                    "Reiniciar descarga"
                },
                AutoExecute = true
            };

            // Sin resultados
            knownSolutions["no_results"] = new HealingAction
            {
                Problem = "Búsqueda sin resultados",
                Solution = "Optimizar términos de búsqueda",
                Steps = new List<string>
                {
                    "Expandir nombre de autor",
                    "Probar variaciones del nombre",
                    "Buscar en otro idioma",
                    "Ampliar criterios de búsqueda"
                },
                AutoExecute = true
            };

            // Usuario offline
            knownSolutions["user_offline"] = new HealingAction
            {
                Problem = "Usuario desconectado",
                Solution = "Buscar proveedor alternativo",
                Steps = new List<string>
                {
                    "Marcar usuario como offline",
                    "Buscar mismo archivo en otros usuarios",
                    "Agregar a cola de reintentos"
                },
                AutoExecute = true
            };

            // Archivo corrupto
            knownSolutions["corrupt_file"] = new HealingAction
            {
                Problem = "Archivo corrupto detectado",
                Solution = "Re-descargar de otra fuente",
                Steps = new List<string>
                {
                    "Eliminar archivo corrupto",
                    "Marcar fuente como no confiable",
                    "Buscar fuente alternativa",
                    "Re-descargar"
                },
                AutoExecute = false // Requiere confirmación
            };

            // Espacio en disco
            knownSolutions["disk_full"] = new HealingAction
            {
                Problem = "Espacio en disco insuficiente",
                Solution = "Liberar espacio automáticamente",
                Steps = new List<string>
                {
                    "Pausar descargas",
                    "Analizar archivos temporales",
                    "Sugerir archivos a eliminar",
                    "Esperar confirmación del usuario"
                },
                AutoExecute = false
            };
        }

        public void RecordError(string errorType)
        {
            if (!errorFrequency.ContainsKey(errorType))
                errorFrequency[errorType] = 0;
            
            errorFrequency[errorType]++;
        }

        public HealingAction DiagnoseAndHeal(string errorType, Dictionary<string, string> context = null)
        {
            RecordError(errorType);

            if (knownSolutions.TryGetValue(errorType, out var solution))
            {
                return solution;
            }

            // Intentar diagnóstico genérico
            return new HealingAction
            {
                Problem = $"Error: {errorType}",
                Solution = "Reintentar operación",
                Steps = new List<string> { "Esperar 30 segundos", "Reintentar automáticamente" },
                AutoExecute = true
            };
        }

        public string GenerateHealingReport(HealingAction action)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("🔧 AUTO-CURACIÓN ACTIVADA\n");
            sb.AppendLine($"Problema: {action.Problem}");
            sb.AppendLine($"Solución: {action.Solution}\n");
            sb.AppendLine("PASOS:");
            
            for (int i = 0; i < action.Steps.Count; i++)
            {
                sb.AppendLine($"  {i + 1}. {action.Steps[i]}");
            }

            sb.AppendLine();
            if (action.AutoExecute)
                sb.AppendLine("✅ Ejecutando automáticamente...");
            else
                sb.AppendLine("⚠️ Requiere tu confirmación. ¿Proceder?");

            return sb.ToString();
        }

        public Dictionary<string, int> GetErrorStatistics()
        {
            return errorFrequency.OrderByDescending(kvp => kvp.Value)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }
}
