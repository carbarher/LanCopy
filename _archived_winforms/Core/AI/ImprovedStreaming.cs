using System;
using System.Collections.Generic;
using System.Text;

namespace SlskDown.Core.AI
{
    /// <summary>
    /// Sistema de streaming mejorado que muestra palabras completas
    /// </summary>
    public class ImprovedStreaming
    {
        private StringBuilder wordBuffer = new StringBuilder();
        private StringBuilder fullResponse = new StringBuilder();
        private List<string> completedWords = new List<string>();

        public event Action<string> OnWordCompleted;
        public event Action<string> OnSentenceCompleted;

        /// <summary>
        /// Procesa un chunk de texto y detecta palabras completas
        /// </summary>
        public void ProcessChunk(string chunk)
        {
            foreach (char c in chunk)
            {
                fullResponse.Append(c);
                wordBuffer.Append(c);

                // Detectar fin de palabra
                if (IsWordSeparator(c))
                {
                    var word = wordBuffer.ToString().Trim();
                    if (!string.IsNullOrEmpty(word))
                    {
                        completedWords.Add(word);
                        OnWordCompleted?.Invoke(word);

                        // Detectar fin de oración
                        if (IsSentenceEnd(c))
                        {
                            var sentence = string.Join(" ", completedWords);
                            OnSentenceCompleted?.Invoke(sentence);
                            completedWords.Clear();
                        }
                    }
                    wordBuffer.Clear();
                }
            }
        }

        /// <summary>
        /// Obtiene la respuesta completa hasta el momento
        /// </summary>
        public string GetFullResponse()
        {
            return fullResponse.ToString();
        }

        /// <summary>
        /// Obtiene las últimas N palabras
        /// </summary>
        public List<string> GetLastWords(int count)
        {
            var totalWords = completedWords.Count;
            var startIndex = Math.Max(0, totalWords - count);
            return completedWords.GetRange(startIndex, totalWords - startIndex);
        }

        /// <summary>
        /// Reinicia el buffer
        /// </summary>
        public void Reset()
        {
            wordBuffer.Clear();
            fullResponse.Clear();
            completedWords.Clear();
        }

        private bool IsWordSeparator(char c)
        {
            return char.IsWhiteSpace(c) || 
                   c == '.' || c == ',' || c == ';' || c == ':' || 
                   c == '!' || c == '?' || c == '\n' || c == '\r';
        }

        private bool IsSentenceEnd(char c)
        {
            return c == '.' || c == '!' || c == '?' || c == '\n';
        }

        /// <summary>
        /// Formatea el texto para mejor visualización
        /// </summary>
        public string FormatForDisplay(string text)
        {
            // Eliminar espacios múltiples
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
            
            // Asegurar espacio después de puntuación
            text = System.Text.RegularExpressions.Regex.Replace(text, @"([.!?])([A-Z])", "$1 $2");
            
            return text.Trim();
        }
    }
}
