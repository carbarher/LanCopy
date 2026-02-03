using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SlskDown
{
    /// <summary>
    /// MÃ©todos de persistencia para reconexiÃ³n automÃ¡tica con continuaciÃ³n de bÃºsqueda
    /// </summary>
    public partial class MainForm
    {
        // Evita reentradas del Tick de reconexiÃ³n
        private bool isReconnectTickRunning = false;
        /// <summary>
        /// Cargar estado previo al iniciar aplicaciÃ³n
        /// </summary>
        private void LoadPreviousAutoSearchState()
        {
            try
            {
                var state = LoadAutoSearchState();
                
                if (state.WasRunning)
                {
                    Console.WriteLine($"[MainForm] ðŸ”„ Detectada bÃºsqueda automÃ¡tica interrumpida");
                    Console.WriteLine($"[MainForm] ðŸ“ Ãšltimo Ã­ndice: {state.CurrentIndex}/{state.TotalAuthors}");
                    Console.WriteLine($"[MainForm] ðŸ”„ Pase: {state.CompletedPasses}/{state.MaxPasses}");
                    Console.WriteLine($"[MainForm] â° Hora: {state.LastSearchTime:HH:mm:ss}");
                    
                    // Restaurar variables de estado
                    currentAuthorIndex = state.CurrentIndex;
                    currentPass = state.CompletedPasses;
                    autoSearchWasRunning = true;
                    
                    // Si hay conexiÃ³n, reanudar automÃ¡ticamente
                    if (client?.State == SoulseekClientStates.Connected)
                    {
                        Console.WriteLine("[MainForm] âœ… Ya conectado - reanudando bÃºsqueda automÃ¡tica");
                        Task.Run(async () => 
                        {
                            await Task.Delay(2000); // Esperar 2 segundos
                            ResumeAutoSearch();
                        });
                    }
                    else
                    {
                        Console.WriteLine("[MainForm] â³ Esperando conexiÃ³n para reanudar bÃºsqueda automÃ¡tica");
                    }
                }
                else
                {
                    Console.WriteLine("[MainForm] â„¹ï¸ No hay bÃºsqueda automÃ¡tica previa");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainForm] âŒ Error cargando estado previo: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Guardar estado actual de bÃºsqueda automÃ¡tica
        /// </summary>
        private void SaveAutoSearchState(bool wasRunning, int currentIndex, string[] authors, int completedPasses)
        {
            try
            {
                var state = new AutoSearchState
                {
                    WasRunning = wasRunning,
                    CurrentIndex = currentIndex,
                    LastSearchTime = DateTime.Now,
                    Authors = authors,
                    TotalAuthors = authors.Length,
                    CompletedPasses = completedPasses,
                    MaxPasses = 10,
                    TimeoutSeconds = 60,
                    IsPaused = !wasRunning,
                    PauseReason = wasRunning ? "" : "BÃºsqueda detenida"
                };
                
                state.Save(autoSearchStateFile);
                Console.WriteLine($"[MainForm] ðŸ’¾ Estado auto-bÃºsqueda guardado: {state}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainForm] âŒ Error guardando estado auto-bÃºsqueda: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Cargar estado previo de bÃºsqueda automÃ¡tica
        /// </summary>
        private AutoSearchState LoadAutoSearchState()
        {
            var state = AutoSearchState.Load(autoSearchStateFile);
            Console.WriteLine($"[MainForm] ðŸ“‚ Estado auto-bÃºsqueda cargado: {state}");
            return state;
        }
        
        /// <summary>
        /// Restaurar bÃºsqueda automÃ¡tica despuÃ©s de reconexiÃ³n
        /// </summary>
        private void ResumeAutoSearch()
        {
            try
            {
                var state = LoadAutoSearchState();
                
                if (state.WasRunning && state.Authors.Length > 0)
                {
                    try
                    {
                        LoadPurgeCache();
                        var now = DateTime.Now;
                        state.Authors = state.Authors
                            .Where(a => !string.IsNullOrWhiteSpace(a))
                            .Where(a =>
                            {
                                if (!purgeCache.TryGetValue(a, out var cached))
                                {
                                    return true;
                                }

                                var age = now - cached.lastCheck;
                                return cached.hasFiles || age.TotalDays >= PURGE_CACHE_DAYS;
                            })
                            .ToArray();
                        state.TotalAuthors = state.Authors.Length;
                        if (state.CurrentIndex >= state.TotalAuthors)
                        {
                            state.CurrentIndex = Math.Max(0, state.TotalAuthors - 1);
                        }
                    }
                    catch
                    {
                    }

                    if (state.Authors.Length == 0)
                    {
                        Console.WriteLine("[MainForm] âš ï¸ BÃºsqueda automÃ¡tica previa sin autores (filtrada por purga)");
                        return;
                    }

                    Console.WriteLine($"[MainForm] ðŸ”„ Restaurando bÃºsqueda automÃ¡tica desde Ã­ndice {state.CurrentIndex}");
                    
                    // Restaurar autores si la lista estÃ¡ vacÃ­a
                    if (authorsList.Items.Count == 0 && state.Authors.Length > 0)
                    {
                        authorsList.Items.AddRange(state.Authors);
                        Console.WriteLine($"[MainForm] ðŸ“‹ Restaurados {state.Authors.Length} autores");
                    }
                    
                    // Iniciar bÃºsqueda desde donde se quedÃ³
                    StartAutoSearchFromState(state);
                }
                else
                {
                    Console.WriteLine("[MainForm] â„¹ï¸ No hay bÃºsqueda automÃ¡tica previa para restaurar");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainForm] âŒ Error restaurando bÃºsqueda automÃ¡tica: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Iniciar bÃºsqueda automÃ¡tica desde estado guardado
        /// </summary>
        private async void StartAutoSearchFromState(AutoSearchState state)
        {
            if (authorsList.Items.Count == 0)
            {
                Console.WriteLine("[MainForm] âš ï¸ No hay autores para continuar bÃºsqueda");
                return;
            }

            try
            {
                LoadPurgeCache();
            }
            catch
            {
            }
            
            Console.WriteLine($"[MainForm] ðŸš€ Continuando bÃºsqueda automÃ¡tica: Ã­ndice {state.CurrentIndex}, pase {state.CompletedPasses + 1}");
            
            // Actualizar UI para mostrar estado de continuaciÃ³n
            if (authorSearchLog.InvokeRequired)
            {
                authorSearchLog.Invoke(() =>
                {
                    authorSearchLog.AppendText($"ðŸ”„ CONEXIÃ“N RESTABLECIDA - Continuando bÃºsqueda automÃ¡tica...\r\n");
                    authorSearchLog.AppendText($"ðŸ“ Reanudando desde autor #{state.CurrentIndex + 1}\r\n");
                    authorSearchLog.AppendText($"ðŸ”„ Pase actual: {state.CompletedPasses + 1}/{state.MaxPasses}\r\n");
                });
            }
            else
            {
                authorSearchLog.AppendText($"ðŸ”„ CONEXIÃ“N RESTABLECIDA - Continuando bÃºsqueda automÃ¡tica...\r\n");
                authorSearchLog.AppendText($"ðŸ“ Reanudando desde autor #{state.CurrentIndex + 1}\r\n");
                authorSearchLog.AppendText($"ðŸ”„ Pase actual: {state.CompletedPasses + 1}/{state.MaxPasses}\r\n");
            }
            
            // Simular lÃ³gica de bÃºsqueda continuada
            for (int i = state.CurrentIndex; i < authorsList.Items.Count; i++)
            {
                if (client?.State != SoulseekClientStates.Connected)
                {
                    Console.WriteLine("[MainForm] âš ï¸ ConexiÃ³n perdida durante bÃºsqueda - guardando estado");
                    SaveAutoSearchState(true, i, authorsList.Items.Cast<string>().ToArray(), state.CompletedPasses);
                    break;
                }
                
                string author = authorsList.Items[i].ToString()!;

                try
                {
                    if (purgeCache.TryGetValue(author, out var cached))
                    {
                        var age = DateTime.Now - cached.lastCheck;
                        if (!cached.hasFiles && age.TotalDays < PURGE_CACHE_DAYS)
                        {
                            int progress = (int)((double)(i + 1) / authorsList.Items.Count * 100);
                            UpdateSearchProgress($"Autor: {author} (omitido por purga) ({i + 1}/{authorsList.Items.Count})", progress);
                            continue;
                        }
                    }
                }
                catch
                {
                }

                Console.WriteLine($"[MainForm] ðŸ” Buscando autor (continuaciÃ³n): {author}");
                
                // AquÃ­ irÃ­a la lÃ³gica real de bÃºsqueda
                await Task.Delay(1000); // SimulaciÃ³n
                
                // Actualizar progreso
                int progress = (int)((double)(i + 1) / authorsList.Items.Count * 100);
                UpdateSearchProgress($"Autor: {author} ({i + 1}/{authorsList.Items.Count})", progress);
            }
            
            // Limpiar estado al completar
            if (client?.State == SoulseekClientStates.Connected)
            {
                var newState = new AutoSearchState();
                newState.Save(autoSearchStateFile);
                Console.WriteLine("[MainForm] âœ… BÃºsqueda automÃ¡tica completada - estado limpiado");
            }
        }
        
        /// <summary>
        /// Iniciar timer de reconexiÃ³n automÃ¡tica
        /// </summary>
        private void StartReconnectionTimer()
        {
            if (reconnectionTimer != null)
            {
                reconnectionTimer.Stop();
                reconnectionTimer.Dispose();
            }
            
            reconnectionTimer = new System.Windows.Forms.Timer();
            reconnectionTimer.Interval = 5000; // 5 segundos
            reconnectionTimer.Tick += async (sender, e) =>
            {
                if (isReconnectTickRunning)
                    return;
                isReconnectTickRunning = true;
                try
                {
                    await TryReconnectAndResume();
                }
                finally
                {
                    isReconnectTickRunning = false;
                }
            };
            reconnectionTimer.Start();
            
            Console.WriteLine("[MainForm] â° Timer de reconexiÃ³n iniciado (5s)");
        }
        
        /// <summary>
        /// Intentar reconectar y reanudar bÃºsqueda
        /// </summary>
        private async Task TryReconnectAndResume()
        {
            try
            {
                if (client?.State == SoulseekClientStates.Connected)
                {
                    Console.WriteLine("[MainForm] âœ… ConexiÃ³n restablecida - deteniendo timer de reconexiÃ³n");
                    reconnectionTimer?.Stop();
                    
                    // Esperar un momento y luego reanudar
                    await Task.Delay(1000);
                    ResumeAutoSearch();
                }
                else
                {
                    Console.WriteLine($"[MainForm] â³ Esperando reconexiÃ³n (estado: {client?.State})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainForm] âŒ Error en reconexiÃ³n automÃ¡tica: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Actualizar progreso de bÃºsqueda de forma segura
        /// </summary>
        private void UpdateSearchProgress(string status, int progress)
        {
            if (InvokeRequired)
            {
                Invoke(() => UpdateSearchProgress(status, progress));
                return;
            }
            
            try
            {
                if (searchProgressLabel != null)
                    searchProgressLabel.Text = status;
                    
                if (searchProgressBar != null)
                    searchProgressBar.Value = progress;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainForm] âŒ Error actualizando progreso: {ex.Message}");
            }
        }
    }
}

