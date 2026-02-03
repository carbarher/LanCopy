using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// Sistema de timeout adaptativo para mÃ¡xima velocidad
    /// </summary>
    public partial class MainForm
    {
        // ConfiguraciÃ³n adaptativa
        private static readonly Dictionary<string, TimeoutStats> authorTimeoutStats = new();
        private static int defaultAdaptiveTimeout = 30; // segundos
        private static int minTimeoutSecs = 10;
        private static int maxTimeoutSecs = 60;
        
        /// <summary>
        /// EstadÃ­sticas de timeout por autor
        /// </summary>
        public struct TimeoutStats
        {
            public int SuccessfulSearches { get; set; }
            public int FailedSearches { get; set; }
            public double AverageResponseTime { get; set; }
            public int CurrentTimeout { get; set; }
            public DateTime LastSearch { get; set; }
            public bool IsFastAuthor { get; set; }
        }
        
        /// <summary>
        /// Obtener timeout Ã³ptimo para autor especÃ­fico
        /// </summary>
        private int GetOptimalTimeout(string author)
        {
            try
            {
                var key = author.ToLower().Trim();
                
                if (!authorTimeoutStats.ContainsKey(key))
                {
                    // Nuevo autor - usar timeout por defecto
                    authorTimeoutStats[key] = new TimeoutStats
                    {
                        CurrentTimeout = defaultAdaptiveTimeout,
                        AverageResponseTime = defaultAdaptiveTimeout * 0.8,
                        LastSearch = DateTime.Now
                    };
                    
                    return defaultAdaptiveTimeout;
                }
                
                var stats = authorTimeoutStats[key];
                
                // Autores rÃ¡pidos: timeout reducido
                if (stats.IsFastAuthor && stats.SuccessfulSearches > 2)
                {
                    return Math.Max(minTimeoutSecs, (int)(stats.AverageResponseTime * 1.5));
                }
                
                // Autores lentos: timeout extendido
                if (stats.FailedSearches > stats.SuccessfulSearches)
                {
                    return Math.Min(maxTimeoutSecs, stats.CurrentTimeout + 10);
                }
                
                return stats.CurrentTimeout;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdaptiveTimeout] âŒ Error obteniendo timeout: {ex.Message}");
                return defaultAdaptiveTimeout;
            }
        }
        
        /// <summary>
        /// Actualizar estadÃ­sticas de timeout despuÃ©s de bÃºsqueda
        /// </summary>
        private void UpdateTimeoutStats(string author, bool success, double responseTime, int filesFound)
        {
            try
            {
                var key = author.ToLower().Trim();
                
                if (!authorTimeoutStats.ContainsKey(key))
                {
                    authorTimeoutStats[key] = new TimeoutStats();
                }
                
                var stats = authorTimeoutStats[key];
                
                if (success)
                {
                    stats.SuccessfulSearches++;
                    
                    // Actualizar tiempo de respuesta promedio
                    if (stats.AverageResponseTime == 0)
                    {
                        stats.AverageResponseTime = responseTime;
                    }
                    else
                    {
                        stats.AverageResponseTime = (stats.AverageResponseTime * 0.7) + (responseTime * 0.3);
                    }
                    
                    // Marcar como autor rÃ¡pido si responde rÃ¡pido y tiene archivos
                    if (responseTime < 10 && filesFound > 5)
                    {
                        stats.IsFastAuthor = true;
                    }
                    
                    // Reducir timeout gradualmente para autores rÃ¡pidos
                    if (stats.IsFastAuthor && stats.CurrentTimeout > minTimeoutSecs)
                    {
                        stats.CurrentTimeout = Math.Max(minTimeoutSecs, stats.CurrentTimeout - 2);
                    }
                }
                else
                {
                    stats.FailedSearches++;
                    
                    // Aumentar timeout para autores lentos
                    if (stats.CurrentTimeout < maxTimeoutSecs)
                    {
                        stats.CurrentTimeout = Math.Min(maxTimeoutSecs, stats.CurrentTimeout + 5);
                    }
                }
                
                stats.LastSearch = DateTime.Now;
                authorTimeoutStats[key] = stats;
                
                Console.WriteLine($"[AdaptiveTimeout] ðŸ“Š {author}: Timeout={stats.CurrentTimeout}s, Ã‰xitos={stats.SuccessfulSearches}, Fallos={stats.FailedSearches}, RÃ¡pido={stats.IsFastAuthor}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdaptiveTimeout] âŒ Error actualizando stats: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Optimizar timeouts para bÃºsqueda masiva
        /// </summary>
        private void OptimizeTimeoutsForBatch(List<string> authors)
        {
            try
            {
                Console.WriteLine("[AdaptiveTimeout] ðŸš€ Optimizando timeouts para bÃºsqueda masiva");
                
                var fastAuthors = new List<string>();
                var slowAuthors = new List<string>();
                var unknownAuthors = new List<string>();
                
                foreach (var author in authors)
                {
                    var key = author.ToLower().Trim();
                    
                    if (!authorTimeoutStats.ContainsKey(key))
                    {
                        unknownAuthors.Add(author);
                    }
                    else if (authorTimeoutStats[key].IsFastAuthor)
                    {
                        fastAuthors.Add(author);
                    }
                    else
                    {
                        slowAuthors.Add(author);
                    }
                }
                
                Console.WriteLine($"[AdaptiveTimeout] ðŸ“Š ClasificaciÃ³n:");
                Console.WriteLine($"  âš¡ Autores rÃ¡pidos: {fastAuthors.Count} (timeout 10-15s)");
                Console.WriteLine($"  ðŸŒ Autores lentos: {slowAuthors.Count} (timeout 30-60s)");
                Console.WriteLine($"  â“ Autores nuevos: {unknownAuthors.Count} (timeout 30s)");
                
                // Guardar estadÃ­sticas
                SaveTimeoutStats();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdaptiveTimeout] âŒ Error optimizando timeouts: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Guardar estadÃ­sticas de timeout
        /// </summary>
        private void SaveTimeoutStats()
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(authorTimeoutStats, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(@"c:\p2p\SlskDown\timeout_stats.json", json);
                
                Console.WriteLine("[AdaptiveTimeout] ðŸ’¾ EstadÃ­sticas de timeout guardadas");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdaptiveTimeout] âŒ Error guardando stats: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Cargar estadÃ­sticas de timeout
        /// </summary>
        private void LoadTimeoutStats()
        {
            try
            {
                var file = @"c:\p2p\SlskDown\timeout_stats.json";
                
                if (System.IO.File.Exists(file))
                {
                    var json = System.IO.File.ReadAllText(file);
                    var loaded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, TimeoutStats>>(json);
                    
                    if (loaded != null)
                    {
                        authorTimeoutStats.Clear();
                        foreach (var kvp in loaded)
                        {
                            authorTimeoutStats[kvp.Key] = kvp.Value;
                        }
                    }
                    
                    Console.WriteLine($"[AdaptiveTimeout] ðŸ“‚ Cargadas {authorTimeoutStats.Count} estadÃ­sticas de timeout");
                }
                else
                {
                    Console.WriteLine("[AdaptiveTimeout] â„¹ï¸ No existe archivo de estadÃ­sticas - iniciando desde cero");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdaptiveTimeout] âŒ Error cargando stats: {ex.Message}");
                authorTimeoutStats.Clear();
            }
        }
        
        /// <summary>
        /// Resetear estadÃ­sticas de timeout
        /// </summary>
        private void ResetTimeoutStats()
        {
            try
            {
                authorTimeoutStats.Clear();
                SaveTimeoutStats();
                
                Console.WriteLine("[AdaptiveTimeout] ðŸ”„ EstadÃ­sticas de timeout reseteadas");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdaptiveTimeout] âŒ Error reseteando stats: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Mostrar reporte de optimizaciÃ³n de timeouts
        /// </summary>
        private void ShowTimeoutOptimizationReport()
        {
            try
            {
                var totalAuthors = authorTimeoutStats.Count;
                var fastAuthors = authorTimeoutStats.Values.Count(s => s.IsFastAuthor);
                var slowAuthors = authorTimeoutStats.Values.Count(s => !s.IsFastAuthor && s.SuccessfulSearches > 0);
                var failedAuthors = authorTimeoutStats.Values.Count(s => s.FailedSearches > s.SuccessfulSearches);
                
                var avgTimeout = totalAuthors > 0 ? authorTimeoutStats.Values.Average(s => s.CurrentTimeout) : defaultAdaptiveTimeout;
                var avgResponseTime = totalAuthors > 0 ? authorTimeoutStats.Values.Average(s => s.AverageResponseTime) : 0;
                
                var report = $"""
ðŸ“Š REPORTE DE OPTIMIZACIÃ“N DE TIMEOUTS
========================================
ðŸ“ˆ EstadÃ­sticas Generales:
â”œâ”€â”€ Total autores analizados: {totalAuthors}
â”œâ”€â”€ Autores rÃ¡pidos: {fastAuthors} ({(totalAuthors > 0 ? fastAuthors * 100 / totalAuthors : 0)}%)
â”œâ”€â”€ Autores lentos: {slowAuthors} ({(totalAuthors > 0 ? slowAuthors * 100 / totalAuthors : 0)}%)
â”œâ”€â”€ Autores fallidos: {failedAuthors} ({(totalAuthors > 0 ? failedAuthors * 100 / totalAuthors : 0)}%)
â”œâ”€â”€ Timeout promedio: {avgTimeout:F1}s
â””â”€â”€ Tiempo respuesta promedio: {avgResponseTime:F1}s

âš¡ Optimizaciones Aplicadas:
â”œâ”€â”€ Autores rÃ¡pidos: timeout 10-15s
â”œâ”€â”€ Autores nuevos: timeout 30s
â”œâ”€â”€ Autores lentos: timeout 30-60s
â””â”€â”€ Ajuste dinÃ¡mico: +5s / -2s

ðŸ’¾ Persistencia: timeout_stats.json
ðŸ”„ Ãšltima actualizaciÃ³n: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
""";
                
                Console.WriteLine(report);
                MessageBox.Show(report, "Reporte de OptimizaciÃ³n de Timeouts", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdaptiveTimeout] âŒ Error generando reporte: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Configurar modo rÃ¡pido para bÃºsqueda masiva
        /// </summary>
        private void EnableFastMode()
        {
            try
            {
                fastModeEnabled = true;
                defaultAdaptiveTimeout = 15; // Reducido drÃ¡sticamente
                minTimeoutSecs = 8;
                maxTimeoutSecs = 30;
                
                Console.WriteLine("[AdaptiveTimeout] ðŸš€ Modo rÃ¡pido activado:");
                Console.WriteLine($"  Timeout por defecto: {defaultAdaptiveTimeout}s");
                Console.WriteLine($"  Timeout mÃ­nimo: {minTimeoutSecs}s");
                Console.WriteLine($"  Timeout mÃ¡ximo: {maxTimeoutSecs}s");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdaptiveTimeout] âŒ Error activando modo rÃ¡pido: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Configurar modo normal para bÃºsqueda cuidadosa
        /// </summary>
        private void EnableNormalMode()
        {
            try
            {
                fastModeEnabled = false;
                defaultAdaptiveTimeout = 30;
                minTimeoutSecs = 10;
                maxTimeoutSecs = 60;
                
                Console.WriteLine("[AdaptiveTimeout] ðŸŒ Modo normal activado:");
                Console.WriteLine($"  Timeout por defecto: {defaultAdaptiveTimeout}s");
                Console.WriteLine($"  Timeout mÃ­nimo: {minTimeoutSecs}s");
                Console.WriteLine($"  Timeout mÃ¡ximo: {maxTimeoutSecs}s");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdaptiveTimeout] âŒ Error activando modo normal: {ex.Message}");
            }
        }
    }
}

