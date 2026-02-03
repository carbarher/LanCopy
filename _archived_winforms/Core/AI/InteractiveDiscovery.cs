using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core.AI
{
    public class DiscoverySession
    {
        public string Topic { get; set; }
        public int CurrentStep { get; set; }
        public Dictionary<string, string> Answers { get; set; } = new Dictionary<string, string>();
        public DateTime Started { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Modo interactivo de descubrimiento con preguntas guiadas
    /// </summary>
    public class InteractiveDiscovery
    {
        private DiscoverySession currentSession;

        private static readonly Dictionary<string, List<string>> discoveryQuestions = new Dictionary<string, List<string>>
        {
            ["ciencia ficción"] = new List<string>
            {
                "¿Prefieres ciencia ficción clásica o moderna?",
                "¿Te gusta más space opera o cyberpunk?",
                "¿Prefieres historias optimistas o distópicas?",
                "¿Qué te interesa más: tecnología, sociedad o exploración espacial?"
            },
            ["fantasía"] = new List<string>
            {
                "¿Prefieres alta fantasía o fantasía urbana?",
                "¿Te gustan las historias épicas o más íntimas?",
                "¿Prefieres magia sistemática o misteriosa?",
                "¿Qué te atrae más: aventuras, política o romance?"
            },
            ["misterio"] = new List<string>
            {
                "¿Prefieres detectives clásicos o thrillers modernos?",
                "¿Te gustan los crímenes complejos o las historias psicológicas?",
                "¿Prefieres ambientación histórica o contemporánea?",
                "¿Qué te interesa más: el quién o el por qué?"
            }
        };

        public string StartDiscovery(string topic)
        {
            currentSession = new DiscoverySession
            {
                Topic = topic,
                CurrentStep = 0
            };

            if (discoveryQuestions.TryGetValue(topic.ToLower(), out var questions))
            {
                return $"🔍 DESCUBRIMIENTO INTERACTIVO: {topic.ToUpper()}\n\n" +
                       $"Voy a hacerte algunas preguntas para encontrar los libros perfectos para ti.\n\n" +
                       $"Pregunta 1/{questions.Count}:\n{questions[0]}";
            }

            return $"No tengo un cuestionario específico para '{topic}', pero puedo ayudarte. ¿Qué tipo de libros te gustan?";
        }

        public string ProcessAnswer(string answer)
        {
            if (currentSession == null)
                return "No hay una sesión de descubrimiento activa. Usa 'descubrir [tema]' para empezar.";

            if (!discoveryQuestions.TryGetValue(currentSession.Topic.ToLower(), out var questions))
                return "Sesión inválida.";

            // Guardar respuesta
            currentSession.Answers[$"step_{currentSession.CurrentStep}"] = answer;
            currentSession.CurrentStep++;

            // Verificar si hay más preguntas
            if (currentSession.CurrentStep < questions.Count)
            {
                return $"Pregunta {currentSession.CurrentStep + 1}/{questions.Count}:\n{questions[currentSession.CurrentStep]}";
            }
            else
            {
                // Generar recomendaciones basadas en respuestas
                return GenerateRecommendations();
            }
        }

        private string GenerateRecommendations()
        {
            var recommendations = new System.Text.StringBuilder();
            recommendations.AppendLine($"✨ RECOMENDACIONES PERSONALIZADAS\n");
            recommendations.AppendLine($"Basado en tus respuestas, te recomiendo:\n");

            // Analizar respuestas y generar recomendaciones
            var answers = string.Join(" ", currentSession.Answers.Values).ToLower();

            if (currentSession.Topic.ToLower() == "ciencia ficción")
            {
                if (answers.Contains("clásica"))
                {
                    recommendations.AppendLine("📚 AUTORES CLÁSICOS:");
                    recommendations.AppendLine("  • Isaac Asimov - Fundación");
                    recommendations.AppendLine("  • Arthur C. Clarke - 2001: Odisea del Espacio");
                    recommendations.AppendLine("  • Philip K. Dick - ¿Sueñan los androides?");
                }
                else if (answers.Contains("moderna"))
                {
                    recommendations.AppendLine("📚 AUTORES MODERNOS:");
                    recommendations.AppendLine("  • Andy Weir - El Marciano");
                    recommendations.AppendLine("  • Liu Cixin - El Problema de los Tres Cuerpos");
                    recommendations.AppendLine("  • Neal Stephenson - Snow Crash");
                }

                if (answers.Contains("space opera"))
                {
                    recommendations.AppendLine("\n🚀 SPACE OPERA:");
                    recommendations.AppendLine("  • Frank Herbert - Dune");
                    recommendations.AppendLine("  • Iain M. Banks - Consider Phlebas");
                    recommendations.AppendLine("  • Dan Simmons - Hyperion");
                }
                else if (answers.Contains("cyberpunk"))
                {
                    recommendations.AppendLine("\n💻 CYBERPUNK:");
                    recommendations.AppendLine("  • William Gibson - Neuromante");
                    recommendations.AppendLine("  • Neal Stephenson - Snow Crash");
                    recommendations.AppendLine("  • Richard K. Morgan - Altered Carbon");
                }
            }

            recommendations.AppendLine("\n¿Quieres que busque alguno de estos libros?");

            // Limpiar sesión
            currentSession = null;

            return recommendations.ToString();
        }

        public bool IsSessionActive() => currentSession != null;

        public string GetSessionStatus()
        {
            if (currentSession == null)
                return "No hay sesión activa.";

            var questions = discoveryQuestions[currentSession.Topic.ToLower()];
            return $"Sesión activa: {currentSession.Topic}\n" +
                   $"Progreso: {currentSession.CurrentStep}/{questions.Count} preguntas";
        }

        public void CancelSession()
        {
            currentSession = null;
        }
    }
}
