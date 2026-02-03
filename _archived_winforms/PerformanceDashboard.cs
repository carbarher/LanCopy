using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace SlskDown
{
    /// <summary>
    /// Dashboard de mÃ©tricas de rendimiento en tiempo real
    /// </summary>
    public class PerformanceDashboard : IDisposable
    {
        private readonly System.Threading.Timer _updateTimer;
        private readonly object _lock = new object();
        private bool _disposed = false;

        // MÃ©tricas
        private int _searchesPerMinute;
        private int _resultsPerSecond;
        private long _currentMemoryMB;
        private long _peakMemoryMB;
        private int _cacheHits;
        private int _cacheMisses;
        private int _totalDownloads;
        private int _downloadsToday;
        private int _downloadsThisWeek;
        private int _downloadsThisMonth;
        private double _avgDownloadSpeed;
        
        // Contadores temporales
        private readonly Queue<DateTime> _recentSearches = new Queue<DateTime>();
        private readonly Queue<DateTime> _recentResults = new Queue<DateTime>();
        private readonly Dictionary<string, int> _topAuthors = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _topSearchTerms = new Dictionary<string, int>();
        private readonly List<long> _memoryHistory = new List<long>();

        public event EventHandler<DashboardUpdateEventArgs> DashboardUpdated;

        public PerformanceDashboard(int updateIntervalMs = 1000)
        {
            _updateTimer = new System.Threading.Timer(UpdateMetrics, null, updateIntervalMs, updateIntervalMs);
        }

        private void UpdateMetrics(object state)
        {
            lock (_lock)
            {
                // Limpiar bÃºsquedas antiguas (>1 minuto)
                var oneMinuteAgo = DateTime.Now.AddMinutes(-1);
                while (_recentSearches.Count > 0 && _recentSearches.Peek() < oneMinuteAgo)
                {
                    _recentSearches.Dequeue();
                }
                _searchesPerMinute = _recentSearches.Count;

                // Limpiar resultados antiguos (>1 segundo)
                var oneSecondAgo = DateTime.Now.AddSeconds(-1);
                while (_recentResults.Count > 0 && _recentResults.Peek() < oneSecondAgo)
                {
                    _recentResults.Dequeue();
                }
                _resultsPerSecond = _recentResults.Count;

                // Actualizar memoria
                _currentMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
                if (_currentMemoryMB > _peakMemoryMB)
                {
                    _peakMemoryMB = _currentMemoryMB;
                }

                // Guardar historial de memoria (Ãºltimos 60 valores)
                _memoryHistory.Add(_currentMemoryMB);
                if (_memoryHistory.Count > 60)
                {
                    _memoryHistory.RemoveAt(0);
                }

                // Notificar actualizaciÃ³n
                DashboardUpdated?.Invoke(this, new DashboardUpdateEventArgs
                {
                    SearchesPerMinute = _searchesPerMinute,
                    ResultsPerSecond = _resultsPerSecond,
                    CurrentMemoryMB = _currentMemoryMB,
                    PeakMemoryMB = _peakMemoryMB,
                    CacheHitRate = GetCacheHitRate(),
                    TotalDownloads = _totalDownloads,
                    DownloadsToday = _downloadsToday,
                    AvgDownloadSpeed = _avgDownloadSpeed
                });
            }
        }

        /// <summary>
        /// Registra una bÃºsqueda
        /// </summary>
        public void RecordSearch(string searchTerm)
        {
            lock (_lock)
            {
                _recentSearches.Enqueue(DateTime.Now);
                
                // Actualizar top tÃ©rminos
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    if (_topSearchTerms.ContainsKey(searchTerm))
                        _topSearchTerms[searchTerm]++;
                    else
                        _topSearchTerms[searchTerm] = 1;
                }
            }
        }

        /// <summary>
        /// Registra resultados recibidos
        /// </summary>
        public void RecordResults(int count)
        {
            lock (_lock)
            {
                for (int i = 0; i < count; i++)
                {
                    _recentResults.Enqueue(DateTime.Now);
                }
            }
        }

        /// <summary>
        /// Registra un hit de cachÃ©
        /// </summary>
        public void RecordCacheHit()
        {
            lock (_lock)
            {
                _cacheHits++;
            }
        }

        /// <summary>
        /// Registra un miss de cachÃ©
        /// </summary>
        public void RecordCacheMiss()
        {
            lock (_lock)
            {
                _cacheMisses++;
            }
        }

        /// <summary>
        /// Registra una descarga completada
        /// </summary>
        public void RecordDownload(string author, double speedMBps)
        {
            lock (_lock)
            {
                _totalDownloads++;
                _downloadsToday++;
                _downloadsThisWeek++;
                _downloadsThisMonth++;

                // Actualizar velocidad promedio
                _avgDownloadSpeed = (_avgDownloadSpeed * (_totalDownloads - 1) + speedMBps) / _totalDownloads;

                // Actualizar top autores
                if (!string.IsNullOrEmpty(author))
                {
                    if (_topAuthors.ContainsKey(author))
                        _topAuthors[author]++;
                    else
                        _topAuthors[author] = 1;
                }
            }
        }

        /// <summary>
        /// Resetea contadores diarios
        /// </summary>
        public void ResetDailyCounters()
        {
            lock (_lock)
            {
                _downloadsToday = 0;
            }
        }

        /// <summary>
        /// Resetea contadores semanales
        /// </summary>
        public void ResetWeeklyCounters()
        {
            lock (_lock)
            {
                _downloadsThisWeek = 0;
            }
        }

        /// <summary>
        /// Resetea contadores mensuales
        /// </summary>
        public void ResetMonthlyCounters()
        {
            lock (_lock)
            {
                _downloadsThisMonth = 0;
            }
        }

        /// <summary>
        /// Obtiene el ratio de hits de cachÃ©
        /// </summary>
        public double GetCacheHitRate()
        {
            lock (_lock)
            {
                int total = _cacheHits + _cacheMisses;
                return total > 0 ? (double)_cacheHits / total * 100 : 0;
            }
        }

        /// <summary>
        /// Obtiene los top 10 autores mÃ¡s buscados
        /// </summary>
        public List<(string author, int count)> GetTopAuthors(int count = 10)
        {
            lock (_lock)
            {
                return _topAuthors
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(count)
                    .Select(kvp => (kvp.Key, kvp.Value))
                    .ToList();
            }
        }

        /// <summary>
        /// Obtiene los top 10 tÃ©rminos de bÃºsqueda
        /// </summary>
        public List<(string term, int count)> GetTopSearchTerms(int count = 10)
        {
            lock (_lock)
            {
                return _topSearchTerms
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(count)
                    .Select(kvp => (kvp.Key, kvp.Value))
                    .ToList();
            }
        }

        /// <summary>
        /// Obtiene el historial de memoria
        /// </summary>
        public List<long> GetMemoryHistory()
        {
            lock (_lock)
            {
                return new List<long>(_memoryHistory);
            }
        }

        /// <summary>
        /// Obtiene un resumen en texto de las mÃ©tricas
        /// </summary>
        public string GetSummary()
        {
            lock (_lock)
            {
                var sb = new StringBuilder();
                sb.AppendLine("â•”â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•—");
                sb.AppendLine("â•‘           DASHBOARD DE RENDIMIENTO                      â•‘");
                sb.AppendLine("â•šâ•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•");
                sb.AppendLine();
                sb.AppendLine("ðŸ“Š MÃ‰TRICAS EN TIEMPO REAL:");
                sb.AppendLine($"  â€¢ BÃºsquedas/minuto:    {_searchesPerMinute}");
                sb.AppendLine($"  â€¢ Resultados/segundo:  {_resultsPerSecond}");
                sb.AppendLine($"  â€¢ Memoria actual:      {_currentMemoryMB} MB");
                sb.AppendLine($"  â€¢ Memoria pico:        {_peakMemoryMB} MB");
                sb.AppendLine($"  â€¢ Cache hit rate:      {GetCacheHitRate():F1}%");
                sb.AppendLine();
                sb.AppendLine("ðŸ“¥ DESCARGAS:");
                sb.AppendLine($"  â€¢ Total:               {_totalDownloads}");
                sb.AppendLine($"  â€¢ Hoy:                 {_downloadsToday}");
                sb.AppendLine($"  â€¢ Esta semana:         {_downloadsThisWeek}");
                sb.AppendLine($"  â€¢ Este mes:            {_downloadsThisMonth}");
                sb.AppendLine($"  â€¢ Velocidad promedio:  {_avgDownloadSpeed:F2} MB/s");
                sb.AppendLine();
                sb.AppendLine("ðŸ† TOP 5 AUTORES:");
                var topAuthors = GetTopAuthors(5);
                for (int i = 0; i < topAuthors.Count; i++)
                {
                    sb.AppendLine($"  {i + 1}. {topAuthors[i].author} ({topAuthors[i].count})");
                }
                sb.AppendLine();
                sb.AppendLine("ðŸ” TOP 5 BÃšSQUEDAS:");
                var topTerms = GetTopSearchTerms(5);
                for (int i = 0; i < topTerms.Count; i++)
                {
                    sb.AppendLine($"  {i + 1}. {topTerms[i].term} ({topTerms[i].count})");
                }

                return sb.ToString();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _updateTimer?.Dispose();
        }
    }

    /// <summary>
    /// Argumentos del evento de actualizaciÃ³n del dashboard
    /// </summary>
    public class DashboardUpdateEventArgs : EventArgs
    {
        public int SearchesPerMinute { get; set; }
        public int ResultsPerSecond { get; set; }
        public long CurrentMemoryMB { get; set; }
        public long PeakMemoryMB { get; set; }
        public double CacheHitRate { get; set; }
        public int TotalDownloads { get; set; }
        public int DownloadsToday { get; set; }
        public double AvgDownloadSpeed { get; set; }
    }
}

