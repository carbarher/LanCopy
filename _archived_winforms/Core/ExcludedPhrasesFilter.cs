using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core
{
    /// <summary>
    /// Filtro de frases excluidas del servidor Soulseek (2024+)
    /// Implementa ExcludedSearchPhrasesReceived del protocolo
    /// </summary>
    public class ExcludedPhrasesFilter
    {
        private readonly HashSet<string> excludedPhrases;
        private readonly object phrasesLock = new object();
        private DateTime lastUpdate;
        
        public int Count => excludedPhrases.Count;
        public DateTime LastUpdate => lastUpdate;
        
        public ExcludedPhrasesFilter()
        {
            excludedPhrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Actualiza lista de frases excluidas (del servidor)
        /// </summary>
        public void UpdatePhrases(List<string> phrases)
        {
            lock (phrasesLock)
            {
                excludedPhrases.Clear();
                
                foreach (var phrase in phrases)
                {
                    if (!string.IsNullOrWhiteSpace(phrase))
                        excludedPhrases.Add(phrase.Trim().ToLower());
                }
                
                lastUpdate = DateTime.Now;
            }
        }
        
        /// <summary>
        /// Verifica si un archivo debe ser excluido
        /// </summary>
        public bool ShouldExclude(string filename, string path = null)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return false;
            
            lock (phrasesLock)
            {
                var fullPath = string.IsNullOrEmpty(path) 
                    ? filename.ToLower() 
                    : $"{path}/{filename}".ToLower();
                
                foreach (var phrase in excludedPhrases)
                {
                    if (fullPath.Contains(phrase))
                        return true;
                }
                
                return false;
            }
        }
        
        /// <summary>
        /// Filtra una lista de archivos
        /// </summary>
        public List<T> FilterFiles<T>(List<T> files, Func<T, string> getFilename, Func<T, string> getPath = null)
        {
            if (excludedPhrases.Count == 0)
                return files;
            
            return files.Where(file =>
            {
                var filename = getFilename(file);
                var path = getPath?.Invoke(file);
                return !ShouldExclude(filename, path);
            }).ToList();
        }
        
        /// <summary>
        /// Obtiene frases excluidas (para debugging)
        /// </summary>
        public List<string> GetExcludedPhrases()
        {
            lock (phrasesLock)
            {
                return excludedPhrases.ToList();
            }
        }
        
        /// <summary>
        /// Verifica si hay frases excluidas activas
        /// </summary>
        public bool HasExcludedPhrases()
        {
            return excludedPhrases.Count > 0;
        }
        
        /// <summary>
        /// Agrega frase manualmente (para testing o blacklist local)
        /// </summary>
        public void AddPhrase(string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase))
                return;
            
            lock (phrasesLock)
            {
                excludedPhrases.Add(phrase.Trim().ToLower());
            }
        }
        
        /// <summary>
        /// Remueve frase manualmente
        /// </summary>
        public void RemovePhrase(string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase))
                return;
            
            lock (phrasesLock)
            {
                excludedPhrases.Remove(phrase.Trim().ToLower());
            }
        }
        
        /// <summary>
        /// Limpia todas las frases
        /// </summary>
        public void Clear()
        {
            lock (phrasesLock)
            {
                excludedPhrases.Clear();
            }
        }
        
        /// <summary>
        /// Obtiene estadísticas de filtrado
        /// </summary>
        public object GetStats(int totalFiles, int filteredFiles)
        {
            return new
            {
                excludedPhrasesCount = Count,
                lastUpdate = LastUpdate,
                totalFilesChecked = totalFiles,
                filesFiltered = filteredFiles,
                filterRate = totalFiles > 0 ? (filteredFiles / (double)totalFiles) * 100 : 0
            };
        }
    }
}
