using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown
{
    /// <summary>
    /// Estadísticas del filtro de idioma en tiempo real
    /// </summary>
    public class LanguageFilterStats
    {
        private static readonly Lazy<LanguageFilterStats> instance = 
            new Lazy<LanguageFilterStats>(() => new LanguageFilterStats());
        
        public static LanguageFilterStats Instance => instance.Value;
        
        // Contadores por idioma
        private long filteredEnglish;
        private long filteredItalian;
        private long filteredFrench;
        private long filteredGerman;
        private long filteredPortuguese;
        private long passedSpanish;
        private long totalProcessed;
        
        // Palabras españolas más comunes encontradas
        private readonly ConcurrentDictionary<string, int> spanishWordsFound = 
            new ConcurrentDictionary<string, int>();
        
        // Razones de rechazo
        private readonly ConcurrentDictionary<string, int> rejectionReasons = 
            new ConcurrentDictionary<string, int>();
        
        private LanguageFilterStats()
        {
            Reset();
        }
        
        public void RecordFiltered(string language, string reason = null)
        {
            System.Threading.Interlocked.Increment(ref totalProcessed);
            
            switch (language.ToLower())
            {
                case "english":
                case "inglés":
                case "ingles":
                    System.Threading.Interlocked.Increment(ref filteredEnglish);
                    break;
                case "italian":
                case "italiano":
                    System.Threading.Interlocked.Increment(ref filteredItalian);
                    break;
                case "french":
                case "francés":
                case "frances":
                    System.Threading.Interlocked.Increment(ref filteredFrench);
                    break;
                case "german":
                case "alemán":
                case "aleman":
                    System.Threading.Interlocked.Increment(ref filteredGerman);
                    break;
                case "portuguese":
                case "portugués":
                case "portugues":
                    System.Threading.Interlocked.Increment(ref filteredPortuguese);
                    break;
            }
            
            if (!string.IsNullOrEmpty(reason))
            {
                rejectionReasons.AddOrUpdate(reason, 1, (k, v) => v + 1);
            }
        }
        
        public void RecordPassed(string spanishWord = null)
        {
            System.Threading.Interlocked.Increment(ref totalProcessed);
            System.Threading.Interlocked.Increment(ref passedSpanish);
            
            if (!string.IsNullOrEmpty(spanishWord))
            {
                spanishWordsFound.AddOrUpdate(spanishWord, 1, (k, v) => v + 1);
            }
        }
        
        public void Reset()
        {
            filteredEnglish = 0;
            filteredItalian = 0;
            filteredFrench = 0;
            filteredGerman = 0;
            filteredPortuguese = 0;
            passedSpanish = 0;
            totalProcessed = 0;
            spanishWordsFound.Clear();
            rejectionReasons.Clear();
        }
        
        public FilterStatsReport GetReport()
        {
            long total = totalProcessed;
            long filtered = filteredEnglish + filteredItalian + filteredFrench + 
                           filteredGerman + filteredPortuguese;
            
            return new FilterStatsReport
            {
                TotalProcessed = total,
                PassedSpanish = passedSpanish,
                TotalFiltered = filtered,
                FilteredEnglish = filteredEnglish,
                FilteredItalian = filteredItalian,
                FilteredFrench = filteredFrench,
                FilteredGerman = filteredGerman,
                FilteredPortuguese = filteredPortuguese,
                PassRate = total > 0 ? (double)passedSpanish / total * 100 : 0,
                FilterRate = total > 0 ? (double)filtered / total * 100 : 0,
                TopSpanishWords = spanishWordsFound
                    .OrderByDescending(kv => kv.Value)
                    .Take(10)
                    .ToDictionary(kv => kv.Key, kv => kv.Value),
                TopRejectionReasons = rejectionReasons
                    .OrderByDescending(kv => kv.Value)
                    .Take(10)
                    .ToDictionary(kv => kv.Key, kv => kv.Value)
            };
        }
        
        public string GetSummary()
        {
            var report = GetReport();
            return $"Filtrados: {report.FilteredItalian:N0} italiano | " +
                   $"{report.FilteredEnglish:N0} inglés | " +
                   $"{report.FilteredPortuguese:N0} portugués | " +
                   $"{report.FilteredFrench:N0} francés | " +
                   $"{report.FilteredGerman:N0} alemán | " +
                   $"✅ {report.PassedSpanish:N0} español ({report.PassRate:F1}%)";
        }
    }
    
    public class FilterStatsReport
    {
        public long TotalProcessed { get; set; }
        public long PassedSpanish { get; set; }
        public long TotalFiltered { get; set; }
        public long FilteredEnglish { get; set; }
        public long FilteredItalian { get; set; }
        public long FilteredFrench { get; set; }
        public long FilteredGerman { get; set; }
        public long FilteredPortuguese { get; set; }
        public double PassRate { get; set; }
        public double FilterRate { get; set; }
        public Dictionary<string, int> TopSpanishWords { get; set; }
        public Dictionary<string, int> TopRejectionReasons { get; set; }
        
        public override string ToString()
        {
            return $"Total: {TotalProcessed:N0} | Español: {PassedSpanish:N0} ({PassRate:F1}%) | " +
                   $"Filtrados: {TotalFiltered:N0} ({FilterRate:F1}%)\n" +
                   $"  Italiano: {FilteredItalian:N0} | Inglés: {FilteredEnglish:N0} | " +
                   $"Portugués: {FilteredPortuguese:N0} | Francés: {FilteredFrench:N0} | Alemán: {FilteredGerman:N0}";
        }
    }
}
